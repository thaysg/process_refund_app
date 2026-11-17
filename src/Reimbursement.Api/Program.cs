using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Reimbursement.Api.Endpoints;
using Reimbursement.Core.Application;
using Reimbursement.Core.Csv;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton(RefundPolicy.Default);
builder.Services.AddSingleton<IReimbursementCalculator, ReimbursementCalculator>();
builder.Services.AddSingleton<IReimbursementProcessor, ReimbursementProcessor>();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalExceptionHandler");
        logger.LogError(ex, "Unhandled exception while processing request.");

        if (ex is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge }
            || ex is InvalidDataException ide && ide.Message.Contains("Multipart body length limit", StringComparison.OrdinalIgnoreCase)
            || ex is IOException ioe && ioe.Message.Contains("length limit", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Payload too large",
                Detail = "The uploaded file exceeds the maximum allowed size."
            });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal server error",
            Detail = "An unexpected error occurred."
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapRefundEndpoints();

app.Run();

// Needed for WebApplicationFactory in integration tests
public partial class Program { }
