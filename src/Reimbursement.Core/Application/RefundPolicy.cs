using Reimbursement.Core.Domain;

namespace Reimbursement.Core.Application;

/// <summary>
/// Centralizes reimbursement and anti-fraud thresholds so business rules can be varied or tested without changing evaluation logic.
/// </summary>
public sealed record RefundPolicy
{
    public required IReadOnlyDictionary<ProcedureType, decimal> Rates { get; init; }
    public required decimal MaxReimbursementPerProcedure { get; init; }
    public required int ProcessingWindowDays { get; init; }
    public required int FraudWindowDays { get; init; }
    public required int MaxRequestsPerWindow { get; init; }
    public required decimal MaxTotalPerWindow { get; init; }

    public static RefundPolicy Default { get; } = new()
    {
        Rates = new Dictionary<ProcedureType, decimal>
        {
            [ProcedureType.ConsultaMedica] = 0.80m,
            [ProcedureType.ExameDeImagem] = 0.90m,
            [ProcedureType.ExameLaboratorial] = 0.70m,
            [ProcedureType.Outros] = 0.50m
        },
        MaxReimbursementPerProcedure = 500.00m,
        ProcessingWindowDays = 90,
        FraudWindowDays = 30,
        MaxRequestsPerWindow = 5,
        MaxTotalPerWindow = 1500.00m
    };

    public decimal GetRate(ProcedureType type)
    {
        if (!Rates.TryGetValue(type, out var rate))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown procedure type.");
        }
        return rate;
    }
}
