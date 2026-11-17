namespace Reimbursement.Api.Contracts;

public sealed record ProcessingResponse(
    string ReferenceDate,
    ProcessingSummary Summary,
    IReadOnlyList<ProcessingItem> Items);

public sealed record ProcessingSummary(
    int Total,
    int Approved,
    int Rejected,
    int SuspectedFraud,
    decimal TotalReimbursed);

public sealed record ProcessingItem(
    int Id,
    string ProcedureType,
    string ProcedureDate,
    decimal AmountPaid,
    decimal ReimbursedAmount,
    int ClientId,
    string Status,
    string Reason,
    string ReasonCode);

public sealed record CsvValidationErrorResponse(int Line, string? Field, string Message);
