using QuoteFlow.Application;
using QuoteFlow.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();

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
