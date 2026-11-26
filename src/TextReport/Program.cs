using Microsoft.EntityFrameworkCore;
using TextReport.Features.Health;
using TextReport.Features.Notifications.SendEmail;
using TextReport.Features.Orders.CreateOrder;
using TextReport.Features.Orders.ProcessOrder;
using TextReport.Infrastructure.Persistence;
using TextReport.Product.TextAnalysis;
using TextReport.Workers;

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

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOptions<WorkerOptions>()
        .BindConfiguration("Workers")
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddSingleton<TextAnalyzer>();
    builder.Services.AddScoped<IOrderProcessor, TextReportProcessor>();
    builder.Services.AddScoped<IEmailSender, DevelopmentEmailSender>();
    builder.Services.AddHostedService<PaymentWorker>();
    builder.Services.AddHostedService<OrderWorker>();
    builder.Services.AddHostedService<EmailWorker>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TextReportDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/health/ready");
app.MapCreateOrder();

app.Run();
