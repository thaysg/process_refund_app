using System.Text;
using FluentAssertions;
using Reimbursement.Core.Application;
using Reimbursement.Core.Csv;
using Reimbursement.Core.Domain;
using Xunit;

namespace Reimbursement.Core.Tests;

public class CsvTests
{
    private static readonly DateOnly Ref = new(2024, 5, 1);

    private static Stream ToStream(string s, Encoding? enc = null)
        => new MemoryStream((enc ?? new UTF8Encoding(false)).GetBytes(s));

    [Fact]
    public void Read_SampleInput_ParsesCorrectly()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n" +
                  "1,Consulta Médica,2024-02-10,400,123\n" +
                  "2,Exame de Imagem,2024-03-01,600,124\n" +
                  "3,Exame Laboratorial,2024-04-15,200,125\n" +
                  "4,Outros,2024-01-20,150,126\n";
        var reqs = CsvReader.Read(ToStream(csv));
        reqs.Should().HaveCount(4);
        reqs[0].Id.Should().Be(1);
        reqs[0].ProcedureType.Should().Be(ProcedureType.ConsultaMedica);
        reqs[0].AmountPaid.Should().Be(400m);
    }

    [Fact]
    public void Read_Utf8WithBom_ParsesCorrectly()
    {
        var csv = "\uFEFFId,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Consulta Médica,2024-04-10,100,1\n";
        var reqs = CsvReader.Read(ToStream(csv, new UTF8Encoding(true)));
        reqs.Should().HaveCount(1);
        reqs[0].ProcedureType.Should().Be(ProcedureType.ConsultaMedica);
    }

    [Fact]
    public void Read_TrailingBlankLinesAndQuotedFields_AreHandled()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\r\n" +
                  "1,\"Consulta Médica\",2024-04-10,100,1\r\n\r\n";
        var reqs = CsvReader.Read(ToStream(csv));
        reqs.Should().HaveCount(1);
    }

    [Fact]
    public void Read_EmptyFile_Throws()
    {
        var act = () => CsvReader.Read(ToStream(""));
        act.Should().Throw<CsvParseException>();
    }

    [Fact]
    public void Read_HeaderOnly_ReturnsEmpty()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n";
        var reqs = CsvReader.Read(ToStream(csv));
        reqs.Should().BeEmpty();
    }

    [Fact]
    public void Read_MissingColumn_ThrowsWithField()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago\n1,Outros,2024-04-10,100\n";
        var act = () => CsvReader.Read(ToStream(csv));
        act.Should().Throw<CsvParseException>().Which.Errors.Should().Contain(e => e.Field == "ClienteId");
    }

    [Fact]
    public void Read_BadDate_ReportsLine()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Outros,10-04-2024,100,1\n";
        var act = () => CsvReader.Read(ToStream(csv));
        var ex = act.Should().Throw<CsvParseException>().Which;
        ex.Errors.Should().Contain(e => e.LineNumber == 2 && e.Field == "DataProcedimento");
    }

    [Fact]
    public void Read_NonNumericNegativeZeroAndTooManyDecimals_AreValidationErrors()
    {
        Action act1 = () => CsvReader.Read(ToStream(
            "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Outros,2024-04-10,abc,1\n"));
        act1.Should().Throw<CsvParseException>().Which.Errors.Should().Contain(e => e.Field == "ValorPago");

        Action act2 = () => CsvReader.Read(ToStream(
            "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Outros,2024-04-10,-5,1\n"));
        act2.Should().Throw<CsvParseException>().Which.Errors.Should().Contain(e => e.Field == "ValorPago" && e.Message.Contains("greater than zero"));

        Action act3 = () => CsvReader.Read(ToStream(
            "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Outros,2024-04-10,0,1\n"));
        act3.Should().Throw<CsvParseException>().Which.Errors.Should().Contain(e => e.Field == "ValorPago" && e.Message.Contains("greater than zero"));

        Action act4 = () => CsvReader.Read(ToStream(
            "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Outros,2024-04-10,10.123,1\n"));
        act4.Should().Throw<CsvParseException>().Which.Errors.Should().Contain(e => e.Field == "ValorPago" && e.Message.Contains("2 decimal"));
    }

    [Fact]
    public void Read_AmountWithTrailingZeroDecimal_AcceptedByValue()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Outros,2024-04-10,10.100,1\n";
        var reqs = CsvReader.Read(ToStream(csv));
        reqs.Should().HaveCount(1);
        reqs[0].AmountPaid.Should().Be(10.10m);
    }

    [Fact]
    public void Read_AmountWithMoreThanTwoSignificantDecimals_Rejected()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Outros,2024-04-10,10.101,1\n";
        var act = () => CsvReader.Read(ToStream(csv));
        act.Should().Throw<CsvParseException>().Which.Errors.Should().Contain(e => e.Field == "ValorPago");
    }

    [Fact]
    public void Read_MidFieldQuoteInUnquotedField_ReportsStructuralError()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Out\"ros,2024-04-01,10,5\n";
        var act = () => CsvReader.Read(ToStream(csv));
        var ex = act.Should().Throw<CsvParseException>().Which;
        ex.Errors.Should().ContainSingle(e =>
            e.LineNumber == 2 &&
            e.Field == "CSV" &&
            e.Message == "Unexpected quote character inside an unquoted field.");
    }

    [Fact]
    public void Read_UnterminatedQuotedField_ReportsStartLine()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,\"Outros,2024-04-01,10,5\n";
        var act = () => CsvReader.Read(ToStream(csv));
        var ex = act.Should().Throw<CsvParseException>().Which;
        ex.Errors.Should().ContainSingle(e =>
            e.LineNumber == 2 &&
            e.Field == "CSV" &&
            e.Message == "Unterminated quoted field.");
    }

    [Fact]
    public void Read_TextAfterClosingQuote_ReportsStructuralError()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,\"Outros\"x,2024-04-01,10,5\n";
        var act = () => CsvReader.Read(ToStream(csv));
        var ex = act.Should().Throw<CsvParseException>().Which;
        ex.Errors.Should().ContainSingle(e =>
            e.Field == "CSV" &&
            e.Message == "Text found after a closing quote in a quoted field.");
    }

    [Fact]
    public void Read_DuplicateIds_ReportsError()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Outros,2024-04-10,100,1\n1,Outros,2024-04-11,100,1\n";
        var act = () => CsvReader.Read(ToStream(csv));
        act.Should().Throw<CsvParseException>().Which.Errors.Should().Contain(e => e.Message.Contains("Duplicate"));
    }

    [Fact]
    public void Read_UnknownProcedureType_ReportsError()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Desconhecido,2024-04-10,100,1\n";
        var act = () => CsvReader.Read(ToStream(csv));
        act.Should().Throw<CsvParseException>().Which.Errors.Should().Contain(e => e.Field == "TipoProcedimento");
    }

    [Fact]
    public void Read_FutureDateRelativeToReference_IsParsedAndProcessedAsRejected()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Outros,2024-05-02,100,1\n2,Outros,2024-04-10,100,1\n";
        var reqs = CsvReader.Read(ToStream(csv));
        reqs.Should().HaveCount(2);
        var results = new ReimbursementProcessor().Process(reqs, Ref);
        results.Single(r => r.Id == 1).Status.Should().Be(RequestStatus.Rejeitado);
        results.Single(r => r.Id == 1).Reason.Should().Be(DecisionReason.ProcedureInFuture);
        results.Single(r => r.Id == 2).Status.Should().Be(RequestStatus.Aprovado);
    }

    [Fact]
    public void Read_ReportsMultipleErrorsInOnePass()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n" +
                  "abc,Desconhecido,10-04-2024,xyz,1\n" +
                  "2,Outros,2024-04-10,-1,nope\n";
        var act = () => CsvReader.Read(ToStream(csv));
        var ex = act.Should().Throw<CsvParseException>().Which;
        ex.Errors.Should().HaveCountGreaterThan(3);
        ex.Errors.Select(e => e.LineNumber).Should().Contain(new[] { 2, 3 });
    }

    [Fact]
    public void Read_QuotedFieldWithCommaAndEmbeddedLineBreak_AndExtraColumns_AndDifferentOrder()
    {
        var csv = "ClienteId,ValorPago,DataProcedimento,TipoProcedimento,Id,Extra\n" +
                  "1,100,2024-04-10,\"Consulta Médica\",1,\"nota, importante\"\n" +
                  "2,200,2024-04-11,\"Exame Laboratorial\",2,\"linha1\nlinha2\"\n";
        var reqs = CsvReader.Read(ToStream(csv));
        reqs.Should().HaveCount(2);
        reqs[0].Id.Should().Be(1);
        reqs[0].ClientId.Should().Be(1);
        reqs[0].ProcedureType.Should().Be(ProcedureType.ConsultaMedica);
        reqs[0].ProcedureDate.Should().Be(new DateOnly(2024, 4, 10));
        reqs[1].Id.Should().Be(2);
        reqs[1].ClientId.Should().Be(2);
        reqs[1].ProcedureType.Should().Be(ProcedureType.ExameLaboratorial);
        reqs[1].ProcedureDate.Should().Be(new DateOnly(2024, 4, 11));
    }

    [Fact]
    public void Write_FormatsMoneyAndProducesExpectedSample()
    {
        CsvWriter.FormatMoney(320m).Should().Be("320");
        CsvWriter.FormatMoney(320.0m).Should().Be("320");
        CsvWriter.FormatMoney(320.50m).Should().Be("320.50");
        CsvWriter.FormatMoney(320.55m).Should().Be("320.55");

        var results = new List<ReimbursementResult>
        {
            new(1, ProcedureType.ConsultaMedica, new DateOnly(2024,2,10), 400, 320, 123, RequestStatus.Aprovado, DecisionReason.Approved, 0),
            new(2, ProcedureType.ExameDeImagem, new DateOnly(2024,3,1), 600, 500, 124, RequestStatus.Aprovado, DecisionReason.Approved, 1),
            new(3, ProcedureType.ExameLaboratorial, new DateOnly(2024,4,15), 200, 140, 125, RequestStatus.Aprovado, DecisionReason.Approved, 2),
            new(4, ProcedureType.Outros, new DateOnly(2024,1,20), 150, 75, 126, RequestStatus.Rejeitado, DecisionReason.ProcedureTooOld, 3),
        };
        using var ms = new MemoryStream();
        CsvWriter.Write(ms, results);
        var output = Encoding.UTF8.GetString(ms.ToArray());
        output.Should().Be(
            "Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status\n" +
            "1,Consulta Médica,2024-02-10,400,320,123,Aprovado\n" +
            "2,Exame de Imagem,2024-03-01,600,500,124,Aprovado\n" +
            "3,Exame Laboratorial,2024-04-15,200,140,125,Aprovado\n" +
            "4,Outros,2024-01-20,150,75,126,Rejeitado\n");
    }

    [Fact]
    public void Write_TextWriter_DoesNotModifyCallerNewLine()
    {
        using var output = new StringWriter(new StringBuilder()) { NewLine = "\r\n" };
        var results = new List<ReimbursementResult>
        {
            new(1, ProcedureType.Outros, new DateOnly(2024, 4, 10), 100, 50, 1, RequestStatus.Aprovado, DecisionReason.Approved, 0),
        };

        CsvWriter.Write(output, results);

        output.NewLine.Should().Be("\r\n");
        output.ToString().Should().Be(
            "Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status\n" +
            "1,Outros,2024-04-10,100,50,1,Aprovado\n");
    }

    [Fact]
    public void EndToEnd_SampleInput_ProducesSampleOutput()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n" +
                  "1,Consulta Médica,2024-02-10,400,123\n" +
                  "2,Exame de Imagem,2024-03-01,600,124\n" +
                  "3,Exame Laboratorial,2024-04-15,200,125\n" +
                  "4,Outros,2024-01-20,150,126\n";
        var reqs = CsvReader.Read(ToStream(csv));
        var processor = new ReimbursementProcessor();
        var results = processor.Process(reqs, Ref);
        using var ms = new MemoryStream();
        CsvWriter.Write(ms, results);
        var output = Encoding.UTF8.GetString(ms.ToArray());
        output.Should().Contain("4,Outros,2024-01-20,150,75,126,Rejeitado");
        output.Should().Contain("1,Consulta Médica,2024-02-10,400,320,123,Aprovado");
    }
}
