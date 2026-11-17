using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Reimbursement.Api.Tests;

public class ReimbursementApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ReimbursementApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Process_HappyPath_ReturnsSummaryItemsAndReasons()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n" +
                  "1,Consulta Médica,2024-02-10,400,123\n" +
                  "4,Outros,2024-01-20,150,126\n";

        using var content = BuildContent(csv, "2024-05-01");
        var response = await _client.PostAsync("/api/reimbursements/process", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("referenceDate").GetString().Should().Be("2024-05-01");
        json.GetProperty("summary").GetProperty("total").GetInt32().Should().Be(2);
        json.GetProperty("summary").GetProperty("approved").GetInt32().Should().Be(1);
        json.GetProperty("summary").GetProperty("rejected").GetInt32().Should().Be(1);
        json.GetProperty("summary").GetProperty("suspectedFraud").GetInt32().Should().Be(0);
        json.GetProperty("summary").GetProperty("totalReimbursed").GetDecimal().Should().Be(320m);
        json.GetProperty("items").GetArrayLength().Should().Be(2);

        var item0 = json.GetProperty("items")[0];
        item0.GetProperty("id").GetInt32().Should().Be(1);
        item0.GetProperty("procedureType").GetString().Should().Be("Consulta Médica");
        item0.GetProperty("procedureDate").GetString().Should().Be("2024-02-10");
        item0.GetProperty("amountPaid").GetDecimal().Should().Be(400m);
        item0.GetProperty("reimbursedAmount").GetDecimal().Should().Be(320m);
        item0.GetProperty("clientId").GetInt32().Should().Be(123);
        item0.GetProperty("status").GetString().Should().Be("Aprovado");
        item0.GetProperty("reasonCode").GetString().Should().Be("Approved");
        item0.GetProperty("reason").GetString().Should().NotBeNullOrWhiteSpace();

        var item1 = json.GetProperty("items")[1];
        item1.GetProperty("status").GetString().Should().Be("Rejeitado");
        item1.GetProperty("reimbursedAmount").GetDecimal().Should().Be(75m);
        item1.GetProperty("reasonCode").GetString().Should().Be("ProcedureTooOld");
    }

    [Fact]
    public async Task Process_FormatCsvQuery_ReturnsCsv()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Consulta Médica,2024-02-10,400,123\n";
        using var content = BuildContent(csv, "2024-05-01");

        var response = await _client.PostAsync("/api/reimbursements/process?format=csv", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Contain("text/csv");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status");
        body.Should().Contain("1,Consulta Médica,2024-02-10,400,320,123,Aprovado");
    }

    [Fact]
    public async Task Process_AcceptCsvHeader_ReturnsCsv()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Consulta Médica,2024-02-10,400,123\n";
        using var content = BuildContent(csv, "2024-05-01");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reimbursements/process")
        {
            Content = content
        };
        request.Headers.Accept.ParseAdd("text/csv");

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Contain("text/csv");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("1,Consulta Médica,2024-02-10,400,320,123,Aprovado");
    }

    [Fact]
    public async Task Process_OversizedUpload_Returns413WithProblemDetails()
    {
        var large = new byte[11 * 1024 * 1024];
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("2024-05-01"), "referenceDate");
        var fileContent = new ByteArrayContent(large);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "big.csv");

        var response = await _client.PostAsync("/api/reimbursements/process", content);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("status").GetInt32().Should().Be(413);
        json.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Process_RequestWithOriginHeader_HasNoAccessControlAllowOrigin()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Consulta Médica,2024-02-10,400,123\n";
        using var content = BuildContent(csv, "2024-05-01");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reimbursements/process")
        {
            Content = content
        };
        request.Headers.Add("Origin", "http://example.com");

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Process_MissingReferenceDate_Returns400()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n1,Outros,2024-04-10,100,1\n";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        content.Add(fileContent, "file", "sample.csv");

        var response = await _client.PostAsync("/api/reimbursements/process", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Process_InvalidCsv_Returns400WithErrorsArray()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n" +
                  "abc,Desconhecido,10-04-2024,xyz,1\n" +
                  "2,Outros,2024-04-10,-1,nope\n";
        using var content = BuildContent(csv, "2024-05-01");

        var response = await _client.PostAsync("/api/reimbursements/process", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(1);
        var first = json.GetProperty("errors")[0];
        first.TryGetProperty("line", out _).Should().BeTrue();
        first.TryGetProperty("field", out _).Should().BeTrue();
        first.TryGetProperty("message", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Process_NotMultipart_Returns400()
    {
        var response = await _client.PostAsync("/api/reimbursements/process",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("multipart/form-data");
    }

    [Fact]
    public async Task Process_FutureDateRow_IsRejectedAndBatchStillProcessed()
    {
        var csv = "Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId\n" +
                  "1,Outros,2024-05-02,100,1\n" +
                  "2,Outros,2024-04-10,100,1\n";
        using var content = BuildContent(csv, "2024-05-01");

        var response = await _client.PostAsync("/api/reimbursements/process", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("items").GetArrayLength().Should().Be(2);
        json.GetProperty("items")[0].GetProperty("status").GetString().Should().Be("Rejeitado");
        json.GetProperty("items")[0].GetProperty("reasonCode").GetString().Should().Be("ProcedureInFuture");
        json.GetProperty("items")[1].GetProperty("status").GetString().Should().Be("Aprovado");
    }

    private static MultipartFormDataContent BuildContent(string csv, string referenceDate)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(referenceDate), "referenceDate");
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "sample.csv");
        return content;
    }
}
