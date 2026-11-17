namespace Reimbursement.Core.Csv;

public sealed record CsvValidationError(int LineNumber, string? Field, string Message);

public sealed class CsvParseException : Exception
{
    public IReadOnlyList<CsvValidationError> Errors { get; }

    public CsvParseException(IReadOnlyList<CsvValidationError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    public CsvParseException(string message, int? lineNumber = null, string? fieldName = null)
        : this([new CsvValidationError(lineNumber ?? 0, fieldName, message)])
    {
    }

    public int? LineNumber => Errors.Count == 1 ? Errors[0].LineNumber : null;
    public string? FieldName => Errors.Count == 1 ? Errors[0].Field : null;

    private static string BuildMessage(IReadOnlyList<CsvValidationError> errors)
    {
        if (errors.Count == 0)
        {
            return "CSV validation failed.";
        }

        if (errors.Count == 1)
        {
            var e = errors[0];
            return e.LineNumber > 0 ? $"Line {e.LineNumber}: {e.Message}" : e.Message;
        }

        return $"{errors.Count} validation errors found in CSV input.";
    }
}
