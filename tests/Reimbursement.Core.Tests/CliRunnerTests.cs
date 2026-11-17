using System.Globalization;
using System.Text;
using FluentAssertions;
using Reimbursement.Cli;
using Reimbursement.Core.Application;
using Xunit;

namespace Reimbursement.Core.Tests;

public class CliRunnerTests
{
    private const string SampleInput = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n" +
                                       "1,Consulta Médica,2024-02-10,400,123\n" +
                                       "2,Exame de Imagem,2024-03-01,600,124\n" +
                                       "3,Exame Laboratorial,2024-04-15,200,125\n" +
                                       "4,Outros,2024-01-20,150,126\n";

    private const string SampleOutput = "Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status\n" +
                                        "1,Consulta Médica,2024-02-10,400,320,123,Aprovado\n" +
                                        "2,Exame de Imagem,2024-03-01,600,500,124,Aprovado\n" +
                                        "3,Exame Laboratorial,2024-04-15,200,140,125,Aprovado\n" +
                                        "4,Outros,2024-01-20,150,75,126,Rejeitado\n";

    [Fact]
    public void Run_SampleInputWithReferenceDate_WritesExpectedCsvAndReturns0()
    {
        var runner = new CliRunner();
        using var input = new StringReader(SampleInput);
        using var output = new StringWriter(new StringBuilder()) { NewLine = "\n" };
        using var error = new StringWriter();

        var code = runner.Run(["--reference-date", "2024-05-01"], input, output, error);

        code.Should().Be(0);
        output.ToString().Should().Be(SampleOutput);
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Run_UnknownArgument_Returns2WritesUsageAndNothingOnStdout()
    {
        var runner = new CliRunner();
        using var input = new StringReader(SampleInput);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = runner.Run(["--reference-dat", "2024-05-01"], input, output, error);

        code.Should().Be(2);
        output.ToString().Should().BeEmpty();
        error.ToString().Should().Contain("unknown argument");
        error.ToString().Should().Contain("Usage:");
    }

    [Fact]
    public void Run_InvalidDateValue_Returns2()
    {
        var runner = new CliRunner();
        using var input = new StringReader(SampleInput);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = runner.Run(["--reference-date", "01-05-2024"], input, output, error);

        code.Should().Be(2);
        output.ToString().Should().BeEmpty();
        error.ToString().Should().Contain("invalid reference date");
    }

    [Fact]
    public void Run_InvalidCsv_Returns1WritesErrorsToStderrAndNothingOnStdout()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Desconhecido,2024-04-10,100,1\n";
        var runner = new CliRunner();
        using var input = new StringReader(csv);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = runner.Run(["--reference-date", "2024-05-01"], input, output, error);

        code.Should().Be(1);
        output.ToString().Should().BeEmpty();
        error.ToString().Should().Contain("Line 2");
        error.ToString().Should().Contain("TipoProcedimento");
    }

    [Fact]
    public void Run_NoReferenceDate_UsesInjectedClock()
    {
        var runner = new CliRunner(
            new ReimbursementProcessor(),
            new FakeTimeProvider(new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.Zero)));
        using var input = new StringReader(SampleInput);
        using var output = new StringWriter(new StringBuilder()) { NewLine = "\n" };
        using var error = new StringWriter();

        var original = Environment.GetEnvironmentVariable("REFERENCE_DATE");
        try
        {
            Environment.SetEnvironmentVariable("REFERENCE_DATE", null);
            var code = runner.Run([], input, output, error);

            code.Should().Be(0);
            output.ToString().Should().Be(SampleOutput);
        }
        finally
        {
            Environment.SetEnvironmentVariable("REFERENCE_DATE", original);
        }
    }

    [Fact]
    public void Run_PreservesAccentsInStdout()
    {
        var runner = new CliRunner();
        using var input = new StringReader(SampleInput);
        using var output = new StringWriter(new StringBuilder()) { NewLine = "\n" };
        using var error = new StringWriter();

        runner.Run(["--reference-date", "2024-05-01"], input, output, error);

        output.ToString().Should().Contain("Consulta Médica");
        output.ToString().Should().Contain("Exame de Imagem");
        output.ToString().Should().Contain("Exame Laboratorial");
    }

    [Fact]
    public void Run_EmptyStdin_Returns1WithClearMessage()
    {
        var runner = new CliRunner();
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = runner.Run(["--reference-date", "2024-05-01"], input, output, error);

        code.Should().Be(1);
        output.ToString().Should().BeEmpty();
        error.ToString().Should().Contain("empty");
    }

    [Fact]
    public void Run_ShortReferenceDateFlag_Works()
    {
        var runner = new CliRunner();
        using var input = new StringReader(SampleInput);
        using var output = new StringWriter(new StringBuilder()) { NewLine = "\n" };
        using var error = new StringWriter();

        var code = runner.Run(["-r", "2024-05-01"], input, output, error);

        code.Should().Be(0);
        output.ToString().Should().Be(SampleOutput);
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Run_ReferenceDateEqualsForm_Works()
    {
        var runner = new CliRunner();
        using var input = new StringReader(SampleInput);
        using var output = new StringWriter(new StringBuilder()) { NewLine = "\n" };
        using var error = new StringWriter();

        var code = runner.Run(["--reference-date=2024-05-01"], input, output, error);

        code.Should().Be(0);
        output.ToString().Should().Be(SampleOutput);
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Run_Help_Returns0AndPrintsExitCodes()
    {
        var runner = new CliRunner();
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = runner.Run(["--help"], input, output, error);

        code.Should().Be(0);
        error.ToString().Should().BeEmpty();
        output.ToString().Should().Contain("Exit codes");
        output.ToString().Should().Contain("  0  Success");
        output.ToString().Should().Contain("  1  Invalid input data");
        output.ToString().Should().Contain("  2  Invalid or unknown arguments");
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
