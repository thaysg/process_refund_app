using System.Globalization;
using Reimbursement.Core.Application;
using Reimbursement.Core.Csv;

namespace Reimbursement.Cli;

/// <summary>
/// Parses CLI arguments and orchestrates CSV-in/CSV-out processing so argument and I/O behavior is testable without a process boundary.
/// </summary>
public sealed class CliRunner
{
    private readonly IReimbursementProcessor _processor;
    private readonly TimeProvider _timeProvider;

    public CliRunner()
        : this(new ReimbursementProcessor(), TimeProvider.System)
    {
    }

    public CliRunner(IReimbursementProcessor processor, TimeProvider? timeProvider = null)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdin);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        try
        {
            if (TryHandleHelp(args, stdout))
            {
                return 0;
            }

            var parseResult = ParseArguments(args);
            if (!parseResult.Success)
            {
                stderr.WriteLine(parseResult.Error);
                stderr.WriteLine();
                WriteUsage(stderr);
                return 2;
            }

            var referenceDate = parseResult.ReferenceDate ?? DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

            var text = stdin.ReadToEnd();
            if (string.IsNullOrWhiteSpace(text))
            {
                stderr.WriteLine("Error: input CSV is empty.");
                return 1;
            }

            var requests = CsvReader.Parse(text);
            var results = _processor.Process(requests, referenceDate);

            CsvWriter.Write(stdout, results);
            return 0;
        }
        catch (CsvParseException ex)
        {
            foreach (var error in ex.Errors)
            {
                if (error.LineNumber > 0)
                {
                    var field = string.IsNullOrWhiteSpace(error.Field) ? string.Empty : $" [{error.Field}]";
                    stderr.WriteLine($"Line {error.LineNumber}{field}: {error.Message}");
                }
                else
                {
                    stderr.WriteLine(error.Message);
                }
            }
            return 1;
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    private static bool TryHandleHelp(string[] args, TextWriter stdout)
    {
        if (args.Any(a => a is "--help" or "-h"))
        {
            WriteHelp(stdout);
            return true;
        }
        return false;
    }

    private static ArgumentParseResult ParseArguments(string[] args)
    {
        string? value = null;
        bool provided = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--reference-date" || arg == "-r")
            {
                if (i + 1 >= args.Length)
                {
                    return ArgumentParseResult.Fail("Error: --reference-date requires a value in yyyy-MM-dd format.");
                }
                value = args[++i];
                provided = true;
                continue;
            }

            if (arg.StartsWith("--reference-date=", StringComparison.Ordinal))
            {
                value = arg["--reference-date=".Length..];
                provided = true;
                continue;
            }

            return ArgumentParseResult.Fail($"Error: unknown argument '{arg}'.");
        }

        if (!provided)
        {
            value = Environment.GetEnvironmentVariable("REFERENCE_DATE");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return ArgumentParseResult.Ok(null);
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return ArgumentParseResult.Fail($"Error: invalid reference date '{value}'. Expected format yyyy-MM-dd.");
        }

        return ArgumentParseResult.Ok(date);
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: reimbursement-cli [--reference-date <yyyy-MM-dd> | -r <yyyy-MM-dd>]");
        writer.WriteLine("       reimbursement-cli --help");
    }

    private static void WriteHelp(TextWriter writer)
    {
        writer.WriteLine("Reimbursement CLI");
        writer.WriteLine();
        writer.WriteLine("Reads reimbursement requests CSV from stdin and writes processed CSV to stdout.");
        writer.WriteLine("Errors and diagnostics are written to stderr.");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  reimbursement-cli [--reference-date <yyyy-MM-dd> | -r <yyyy-MM-dd>]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --reference-date <date>  Reference date in yyyy-MM-dd format.");
        writer.WriteLine("  -r <date>                Short form for --reference-date.");
        writer.WriteLine("                           Defaults to REFERENCE_DATE env var or today's local date.");
        writer.WriteLine("  -h, --help               Show this help message.");
        writer.WriteLine();
        writer.WriteLine("Exit codes:");
        writer.WriteLine("  0  Success");
        writer.WriteLine("  1  Invalid input data");
        writer.WriteLine("  2  Invalid or unknown arguments / invalid or missing date value");
        writer.WriteLine();
        writer.WriteLine("Example:");
        writer.WriteLine("  cat samples/sample.csv | dotnet run --project src/Reimbursement.Cli -- --reference-date 2024-05-01 > out.csv");
    }

    private readonly record struct ArgumentParseResult(bool Success, DateOnly? ReferenceDate, string? Error)
    {
        public static ArgumentParseResult Ok(DateOnly? date) => new(true, date, null);
        public static ArgumentParseResult Fail(string error) => new(false, null, error);
    }
}
