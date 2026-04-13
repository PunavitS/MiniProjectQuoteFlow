using QuoteFlow.API.Middleware;
using QuoteFlow.Application;
using QuoteFlow.Infrastructure;
using Scalar.AspNetCore;
using Serilog;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, config) => config
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext} {Message:lj}{NewLine}{Exception}"));

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 10;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("CorrelationId", http.Items["X-Correlation-ID"] ?? "");
        diag.Set("ClientIp", http.Connection.RemoteIpAddress);
    };
});
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRateLimiter();
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "QuoteFlow API";
    options.Theme = ScalarTheme.Purple;
});
app.UseHttpsRedirection();
app.MapControllers().RequireRateLimiting("fixed");

app.Run();

public partial class Program { }
