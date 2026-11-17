namespace Reimbursement.Core.Domain;

public enum ProcedureType
{
    ConsultaMedica,
    ExameDeImagem,
    ExameLaboratorial,
    Outros
}

public static class ProcedureTypeParser
{
    private static readonly Dictionary<string, ProcedureType> Map = new(StringComparer.Ordinal)
    {
        ["Consulta Médica"] = ProcedureType.ConsultaMedica,
        ["Exame de Imagem"] = ProcedureType.ExameDeImagem,
        ["Exame Laboratorial"] = ProcedureType.ExameLaboratorial,
        ["Outros"] = ProcedureType.Outros
    };

    public static bool TryParse(string? value, out ProcedureType type)
    {
        if (value is not null && Map.TryGetValue(value.Trim(), out type))
        {
            return true;
        }

        type = default;
        return false;
    }

    public static string ToDisplayString(this ProcedureType type) => type switch
    {
        ProcedureType.ConsultaMedica => "Consulta Médica",
        ProcedureType.ExameDeImagem => "Exame de Imagem",
        ProcedureType.ExameLaboratorial => "Exame Laboratorial",
        ProcedureType.Outros => "Outros",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown procedure type.")
    };
}
