using System.Globalization;
using System.Text;
using Reimbursement.Core.Domain;

namespace Reimbursement.Core.Csv;

/// <summary>
/// Writes reimbursement results as CSV to match the challenge output contract (7 columns, invariant money format, LF line endings).
/// </summary>
public sealed class CsvWriter
{
    /// <summary>
    /// Writes results to a stream using UTF-8 without BOM.
    /// </summary>
    public static void Write(Stream output, IReadOnlyList<ReimbursementResult> results)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(results);

        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        Write(writer, results);
        writer.Flush();
    }

    /// <summary>
    /// Writes results to a text writer. Caller controls the underlying destination; output uses LF line endings and no BOM.
    /// </summary>
    public static void Write(TextWriter output, IReadOnlyList<ReimbursementResult> results)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(results);

        output.Write("Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status\n");

        foreach (var r in results)
        {
            output.Write(r.Id.ToString(CultureInfo.InvariantCulture));
            output.Write(',');
            output.Write(Escape(r.ProcedureType.ToDisplayString()));
            output.Write(',');
            output.Write(r.ProcedureDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            output.Write(',');
            output.Write(FormatMoney(r.AmountPaid));
            output.Write(',');
            output.Write(FormatMoney(r.ReimbursedAmount));
            output.Write(',');
            output.Write(r.ClientId.ToString(CultureInfo.InvariantCulture));
            output.Write(',');
            output.Write(Escape(r.Status.ToDisplayString()));
            output.Write('\n');
        }
    }

    public static string FormatMoney(decimal value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        if (rounded == decimal.Truncate(rounded))
        {
            return decimal.Truncate(rounded).ToString("0", CultureInfo.InvariantCulture);
        }

        return rounded.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
