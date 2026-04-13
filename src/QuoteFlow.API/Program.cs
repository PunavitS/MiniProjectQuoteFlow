using QuoteFlow.API.Middleware;
using QuoteFlow.Application;
using QuoteFlow.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "QuoteFlow API";
    options.Theme = ScalarTheme.Purple;
});
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program { }
