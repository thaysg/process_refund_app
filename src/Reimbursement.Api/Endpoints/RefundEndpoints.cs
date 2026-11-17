using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Reimbursement.Api.Contracts;
using Reimbursement.Core.Application;
using Reimbursement.Core.Csv;
using Reimbursement.Core.Domain;

namespace Reimbursement.Api.Endpoints;

public static class RefundEndpoints
{
    public static IEndpointRouteBuilder MapRefundEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

        app.MapPost("/api/reimbursements/process", ProcessAsync)
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> ProcessAsync(
        HttpRequest request,
        IReimbursementProcessor processor,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("RefundProcessing");

        if (!request.HasFormContentType)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request",
                detail: "Expected multipart/form-data with a CSV file and a 'referenceDate' field (yyyy-MM-dd).");
        }

        var form = await request.ReadFormAsync(ct);

        var referenceDateStr = form["referenceDate"].ToString();
        if (string.IsNullOrWhiteSpace(referenceDateStr) ||
            !DateOnly.TryParseExact(referenceDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var referenceDate))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["referenceDate"] = ["referenceDate is required and must be in yyyy-MM-dd format."]
            }, title: "Validation error", detail: "Invalid or missing referenceDate.");
        }

        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["A non-empty CSV file is required in the 'file' field."]
            }, title: "Validation error", detail: "Missing CSV file.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var requestsList = CsvReader.Read(stream);
            var results = processor.Process(requestsList, referenceDate);

            var format = request.Query["format"].ToString();
            var accept = request.Headers.Accept.ToString();
            var wantsCsv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
                           || accept.Contains("text/csv", StringComparison.OrdinalIgnoreCase);

            logger.LogInformation("Processed {Count} requests for reference date {ReferenceDate}.", results.Count, referenceDate);

            if (wantsCsv)
            {
                using var ms = new MemoryStream();
                CsvWriter.Write(ms, results);
                return Results.File(ms.ToArray(), "text/csv; charset=utf-8", "reimbursements.csv");
            }

            return Results.Ok(BuildResponse(referenceDate, results));
        }
        catch (CsvParseException ex)
        {
            return CsvErrors(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process reimbursement CSV.");
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Processing error",
                detail: "Failed to process the CSV file.");
        }
    }

    private static IResult CsvErrors(CsvParseException ex)
    {
        var errors = ex.Errors
            .Select(e => new CsvValidationErrorResponse(e.LineNumber, e.Field, e.Message))
            .ToArray();

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "CSV validation error",
            Detail = errors.Length == 1 ? errors[0].Message : $"{errors.Length} validation errors found in the CSV file."
        };
        problem.Extensions["errors"] = errors;
        return Results.Json(problem, statusCode: StatusCodes.Status400BadRequest, contentType: "application/problem+json");
    }

    private static ProcessingResponse BuildResponse(DateOnly referenceDate, IReadOnlyList<ReimbursementResult> results)
    {
        var items = results.Select(r => new ProcessingItem(
            r.Id,
            r.ProcedureType.ToDisplayString(),
            r.ProcedureDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.AmountPaid,
            r.ReimbursedAmount,
            r.ClientId,
            r.Status.ToDisplayString(),
            r.Reason.ToDisplayString(),
            r.Reason.ToString())).ToList();

        var summary = new ProcessingSummary(
            results.Count,
            results.Count(r => r.Status == RequestStatus.Aprovado),
            results.Count(r => r.Status == RequestStatus.Rejeitado),
            results.Count(r => r.Status == RequestStatus.SuspeitoDeFraude),
            results.Where(r => r.Status == RequestStatus.Aprovado).Sum(r => r.ReimbursedAmount));

        return new ProcessingResponse(
            referenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            summary,
            items);
    }
}
