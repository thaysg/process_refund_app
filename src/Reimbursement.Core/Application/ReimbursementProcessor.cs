using Reimbursement.Core.Domain;

namespace Reimbursement.Core.Application;

/// <summary>
/// Evaluates a batch against eligibility and anti-fraud rules while preserving input order in results.
/// </summary>
public interface IReimbursementProcessor
{
    IReadOnlyList<ReimbursementResult> Process(
        IReadOnlyList<ReimbursementRequest> requests,
        DateOnly referenceDate);
}

/// <summary>
/// Applies chronological per-client evaluation so approved-only rolling-window limits are enforced deterministically.
/// </summary>
public sealed class ReimbursementProcessor(
    RefundPolicy policy,
    IReimbursementCalculator calculator) : IReimbursementProcessor
{
    private readonly RefundPolicy _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    private readonly IReimbursementCalculator _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));

    public ReimbursementProcessor() : this(RefundPolicy.Default, new ReimbursementCalculator(RefundPolicy.Default))
    {
    }

    public IReadOnlyList<ReimbursementResult> Process(
        IReadOnlyList<ReimbursementRequest> requests,
        DateOnly referenceDate)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var resultsById = new Dictionary<int, ReimbursementResult>(requests.Count);

        foreach (var group in requests.GroupBy(r => r.ClientId))
        {
            ProcessClient(group, referenceDate, resultsById);
        }

        return requests
            .OrderBy(r => r.InputIndex)
            .Select(r => resultsById[r.Id])
            .ToList();
    }

    private void ProcessClient(
        IEnumerable<ReimbursementRequest> clientRequests,
        DateOnly referenceDate,
        IDictionary<int, ReimbursementResult> resultsById)
    {
        // Chronological evaluation is required for rolling-window fraud checks; same-date ties use input order
        // so outcomes do not depend on arbitrary identifiers.
        var ordered = clientRequests
            .OrderBy(r => r.ProcedureDate)
            .ThenBy(r => r.InputIndex)
            .ToList();

        var approvedHistory = new Queue<(DateOnly Date, decimal ReimbursedAmount)>();
        var runningCount = 0;
        var runningTotal = 0m;

        foreach (var request in ordered)
        {
            var reimbursedAmount = _calculator.Calculate(request.AmountPaid, request.ProcedureType);
            var (status, reason) = EvaluateStatus(request, referenceDate, reimbursedAmount, approvedHistory, ref runningCount, ref runningTotal);

            resultsById[request.Id] = new ReimbursementResult(
                request.Id,
                request.ProcedureType,
                request.ProcedureDate,
                request.AmountPaid,
                reimbursedAmount,
                request.ClientId,
                status,
                reason,
                request.InputIndex);

            if (status == RequestStatus.Aprovado)
            {
                approvedHistory.Enqueue((request.ProcedureDate, reimbursedAmount));
                runningCount++;
                runningTotal += reimbursedAmount;
            }
        }
    }

    private (RequestStatus Status, DecisionReason Reason) EvaluateStatus(
        ReimbursementRequest request,
        DateOnly referenceDate,
        decimal reimbursedAmount,
        Queue<(DateOnly Date, decimal ReimbursedAmount)> approvedHistory,
        ref int runningCount,
        ref decimal runningTotal)
    {
        var age = referenceDate.DayNumber - request.ProcedureDate.DayNumber;
        if (age < 0)
        {
            return (RequestStatus.Rejeitado, DecisionReason.ProcedureInFuture);
        }

        if (age > _policy.ProcessingWindowDays)
        {
            return (RequestStatus.Rejeitado, DecisionReason.ProcedureTooOld);
        }

        var windowStart = request.ProcedureDate.AddDays(-_policy.FraudWindowDays);
        while (approvedHistory.Count > 0 && approvedHistory.Peek().Date < windowStart)
        {
            var removed = approvedHistory.Dequeue();
            runningCount--;
            runningTotal -= removed.ReimbursedAmount;
        }

        // Conflict resolution: count limit is checked before total limit so the reported reason is deterministic
        // when either or both limits would be violated by approving this request.
        var countViolated = runningCount + 1 > _policy.MaxRequestsPerWindow;
        var amountViolated = runningTotal + reimbursedAmount > _policy.MaxTotalPerWindow;

        if (countViolated)
        {
            return (RequestStatus.SuspeitoDeFraude, DecisionReason.CustomerRequestLimitExceeded);
        }

        if (amountViolated)
        {
            return (RequestStatus.SuspeitoDeFraude, DecisionReason.CustomerTotalLimitExceeded);
        }

        return (RequestStatus.Aprovado, DecisionReason.Approved);
    }
}
