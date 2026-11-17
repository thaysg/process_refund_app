namespace Reimbursement.Core.Domain;

public enum RequestStatus
{
    Aprovado,
    Rejeitado,
    SuspeitoDeFraude
}

/// <summary>
/// Explains why a request received its status so API clients and diagnostics can distinguish rejection/fraud causes.
/// </summary>
public enum DecisionReason
{
    Approved,
    ProcedureTooOld,
    ProcedureInFuture,
    CustomerRequestLimitExceeded,
    CustomerTotalLimitExceeded
}

public static class RequestStatusExtensions
{
    public static string ToDisplayString(this RequestStatus status) => status switch
    {
        RequestStatus.Aprovado => "Aprovado",
        RequestStatus.Rejeitado => "Rejeitado",
        RequestStatus.SuspeitoDeFraude => "Suspeito de Fraude",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status.")
    };
}

public static class DecisionReasonExtensions
{
    public static string ToDisplayString(this DecisionReason reason) => reason switch
    {
        DecisionReason.Approved => "Aprovado",
        DecisionReason.ProcedureTooOld => "Procedimento fora da janela de 90 dias",
        DecisionReason.ProcedureInFuture => "Data do procedimento posterior à data de referência",
        DecisionReason.CustomerRequestLimitExceeded => "Limite de solicitações em 30 dias excedido",
        DecisionReason.CustomerTotalLimitExceeded => "Limite de valor reembolsado em 30 dias excedido",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown reason.")
    };
}

