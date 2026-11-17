namespace Reimbursement.Core.Domain;

public sealed record ReimbursementRequest(
    int Id,
    ProcedureType ProcedureType,
    DateOnly ProcedureDate,
    decimal AmountPaid,
    int ClientId,
    int InputIndex);

public sealed record ReimbursementResult(
    int Id,
    ProcedureType ProcedureType,
    DateOnly ProcedureDate,
    decimal AmountPaid,
    decimal ReimbursedAmount,
    int ClientId,
    RequestStatus Status,
    DecisionReason Reason,
    int InputIndex);

