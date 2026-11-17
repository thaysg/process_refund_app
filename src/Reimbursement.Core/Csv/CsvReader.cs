using System.Globalization;
using System.Text;
using Reimbursement.Core.Domain;

namespace Reimbursement.Core.Csv;

/// <summary>
/// Parses reimbursement request CSV text into domain requests while reporting all row/field validation issues in one pass.
/// </summary>
public sealed class CsvReader
{
    private static readonly string[] ExpectedHeaders =
        ["Id", "TipoProcedimento", "DataProcedimento", "ValorPago", "ClienteId"];

    public static IReadOnlyList<ReimbursementRequest> Read(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var reader = new StreamReader(
            input,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        var text = reader.ReadToEnd();
        return Parse(text);
    }

    public static IReadOnlyList<ReimbursementRequest> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        var records = ParseRecords(text);
        var errors = new List<CsvValidationError>();

        int index = 0;
        while (index < records.Count && IsBlankRecord(records[index].Fields))
        {
            index++;
        }

        if (index >= records.Count)
        {
            throw new CsvParseException("Input CSV is empty or contains no header.");
        }

        var header = records[index++];
        var headerNames = header.Fields
            .Select(f => f.Trim())
            .ToArray();

        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headerNames.Length; i++)
        {
            if (!headerMap.ContainsKey(headerNames[i]))
            {
                headerMap[headerNames[i]] = i;
            }
        }

        foreach (var expected in ExpectedHeaders)
        {
            if (!headerMap.ContainsKey(expected))
            {
                errors.Add(new CsvValidationError(header.LineNumber, expected,
                    $"Missing required column '{expected}' in header."));
            }
        }

        if (errors.Count > 0)
        {
            throw new CsvParseException(errors);
        }

        var requests = new List<ReimbursementRequest>();
        var seenIds = new HashSet<int>();
        int inputIndex = 0;

        for (; index < records.Count; index++)
        {
            var record = records[index];
            if (IsBlankRecord(record.Fields))
            {
                continue;
            }

            var rowErrorsBefore = errors.Count;
            var request = TryParseRow(record, headerMap, inputIndex, errors);

            if (errors.Count == rowErrorsBefore && request is not null)
            {
                if (!seenIds.Add(request.Id))
                {
                    errors.Add(new CsvValidationError(record.LineNumber, "Id",
                        $"Duplicate Id '{request.Id}' found."));
                }
                else
                {
                    requests.Add(request);
                }
            }

            inputIndex++;
        }

        if (errors.Count > 0)
        {
            throw new CsvParseException(errors);
        }

        return requests;
    }

    private static ReimbursementRequest? TryParseRow(
        CsvRecord record,
        IReadOnlyDictionary<string, int> headerMap,
        int inputIndex,
        List<CsvValidationError> errors)
    {
        int? id = null;
        ProcedureType? procedureType = null;
        DateOnly? procedureDate = null;
        decimal? amountPaid = null;
        int? clientId = null;

        var idStr = GetField(record.Fields, headerMap, "Id")?.Trim() ?? string.Empty;
        if (!int.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
        {
            errors.Add(new CsvValidationError(record.LineNumber, "Id",
                $"Invalid Id '{idStr}'. Expected an integer."));
        }
        else
        {
            id = parsedId;
        }

        var tipoStr = GetField(record.Fields, headerMap, "TipoProcedimento")?.Trim() ?? string.Empty;
        if (!ProcedureTypeParser.TryParse(tipoStr, out var parsedType))
        {
            errors.Add(new CsvValidationError(record.LineNumber, "TipoProcedimento",
                $"Unknown procedure type '{tipoStr}'. Valid values are: Consulta Médica, Exame de Imagem, Exame Laboratorial, Outros."));
        }
        else
        {
            procedureType = parsedType;
        }

        var dataStr = GetField(record.Fields, headerMap, "DataProcedimento")?.Trim() ?? string.Empty;
        if (!DateOnly.TryParseExact(dataStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            errors.Add(new CsvValidationError(record.LineNumber, "DataProcedimento",
                $"Invalid date '{dataStr}'. Expected format yyyy-MM-dd."));
        }
        else
        {
            procedureDate = parsedDate;
        }

        var valorStr = GetField(record.Fields, headerMap, "ValorPago")?.Trim() ?? string.Empty;
        if (!TryParseAmount(valorStr, out var parsedAmount, out var amountError))
        {
            errors.Add(new CsvValidationError(record.LineNumber, "ValorPago", amountError));
        }
        else
        {
            amountPaid = parsedAmount;
        }

        var clienteStr = GetField(record.Fields, headerMap, "ClienteId")?.Trim() ?? string.Empty;
        if (!int.TryParse(clienteStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedClientId))
        {
            errors.Add(new CsvValidationError(record.LineNumber, "ClienteId",
                $"Invalid ClienteId '{clienteStr}'. Expected an integer."));
        }
        else
        {
            clientId = parsedClientId;
        }

        if (id is null || procedureType is null || procedureDate is null || amountPaid is null || clientId is null)
        {
            return null;
        }

        return new ReimbursementRequest(
            id.Value,
            procedureType.Value,
            procedureDate.Value,
            amountPaid.Value,
            clientId.Value,
            inputIndex);
    }

    private static bool TryParseAmount(string value, out decimal amount, out string error)
    {
        amount = default;

        if (value.Contains(','))
        {
            error = $"Invalid amount '{value}'. Thousands separator is not allowed; use '.' as decimal separator.";
            return false;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            error = $"Invalid amount '{value}'. Expected a numeric value with '.' as decimal separator.";
            return false;
        }

        if (amount <= 0)
        {
            error = $"Amount paid '{value}' must be greater than zero.";
            return false;
        }

        // Decide by value, not by text length (e.g. 10.100 is equivalent to 10.10).
        if (decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
        {
            error = $"Amount paid '{value}' must have at most 2 decimal places.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string? GetField(string[] fields, IReadOnlyDictionary<string, int> headerMap, string column)
    {
        if (!headerMap.TryGetValue(column, out var idx) || idx < 0 || idx >= fields.Length)
        {
            return null;
        }
        return fields[idx];
    }

    private static bool IsBlankRecord(string[] fields)
        => fields.Length == 0 || fields.All(f => string.IsNullOrWhiteSpace(f));

    internal static IReadOnlyList<CsvRecord> ParseRecords(string text)
    {
        var records = new List<CsvRecord>();
        if (text.Length == 0)
        {
            return records;
        }

        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        bool afterClosingQuote = false;
        int line = 1;
        int recordStartLine = 1;
        int quotedFieldStartLine = 1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterClosingQuote = true;
                    }
                }
                else
                {
                    sb.Append(c);
                    if (c == '\n')
                    {
                        line++;
                    }
                }
                continue;
            }

            if (afterClosingQuote)
            {
                if (c == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                    afterClosingQuote = false;
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                    records.Add(new CsvRecord(recordStartLine, [.. fields]));
                    fields.Clear();
                    afterClosingQuote = false;
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    line++;
                    recordStartLine = line;
                    continue;
                }

                throw new CsvParseException("Text found after a closing quote in a quoted field.", recordStartLine, "CSV");
            }

            switch (c)
            {
                case '"':
                    if (sb.Length == 0)
                    {
                        inQuotes = true;
                        quotedFieldStartLine = line;
                    }
                    else
                    {
                        throw new CsvParseException("Unexpected quote character inside an unquoted field.", recordStartLine, "CSV");
                    }
                    break;
                case ',':
                    fields.Add(sb.ToString());
                    sb.Clear();
                    break;
                case '\r':
                case '\n':
                    fields.Add(sb.ToString());
                    sb.Clear();
                    records.Add(new CsvRecord(recordStartLine, [.. fields]));
                    fields.Clear();
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    line++;
                    recordStartLine = line;
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        if (inQuotes)
        {
            throw new CsvParseException("Unterminated quoted field.", quotedFieldStartLine, "CSV");
        }

        if (afterClosingQuote || sb.Length > 0 || fields.Count > 0)
        {
            fields.Add(sb.ToString());
            records.Add(new CsvRecord(recordStartLine, [.. fields]));
        }

        return records;
    }

    internal sealed record CsvRecord(int LineNumber, string[] Fields);
}
