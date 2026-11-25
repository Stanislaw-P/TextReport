using Microsoft.EntityFrameworkCore;
using TextReport.Features.Health;
using TextReport.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TextReport");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Set ConnectionStrings__TextReport to the PostgreSQL connection string.");
}

builder.Services.AddDbContext<TextReportDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql", timeout: TimeSpan.FromSeconds(5));

var app = builder.Build();
app.MapHealthChecks("/health/ready");

app.Run();
