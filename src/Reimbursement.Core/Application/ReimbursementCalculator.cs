using Reimbursement.Core.Domain;

namespace Reimbursement.Core.Application;

/// <summary>
/// Computes reimbursable amount from policy so rate/cap/rounding decisions stay consistent and injectable.
/// </summary>
public interface IReimbursementCalculator
{
    decimal Calculate(decimal amountPaid, ProcedureType type);
}

/// <summary>
/// Applies procedure-type rate, per-procedure cap, and monetary rounding as defined by the active policy.
/// </summary>
public sealed class ReimbursementCalculator(RefundPolicy policy) : IReimbursementCalculator
{
    private readonly RefundPolicy _policy = policy ?? throw new ArgumentNullException(nameof(policy));

    public decimal Calculate(decimal amountPaid, ProcedureType type)
    {
        var raw = amountPaid * _policy.GetRate(type);
        var capped = Math.Min(raw, _policy.MaxReimbursementPerProcedure);
        return Math.Round(capped, 2, MidpointRounding.AwayFromZero);
    }
}
