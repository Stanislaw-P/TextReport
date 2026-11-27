using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TextReport.Components.Admin;
using TextReport.Features.Admin;
using TextReport.Features.Health;
using TextReport.Features.Notifications.SendEmail;
using TextReport.Features.Orders.CreateOrder;
using TextReport.Features.Orders.ProcessOrder;
using TextReport.Infrastructure.Persistence;
using TextReport.Product.TextAnalysis;
using TextReport.Workers;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration
        .AddJsonFile("appsettings.Local.json", optional: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

var connectionString = builder.Configuration.GetConnectionString("TextReport");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Set ConnectionStrings__TextReport to the PostgreSQL connection string.");
}

builder.Services.AddDbContextFactory<TextReportDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddOptions<WorkerOptions>()
    .BindConfiguration("Workers")
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "TextReport.Admin";
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/login";
        options.ExpireTimeSpan = AdminAuth.SessionLifetime;
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorizationBuilder().AddPolicy(AdminAuth.Policy,
    policy => policy.RequireAuthenticatedUser().RequireRole(AdminAuth.Role)
        .RequireAssertion(context => AdminAuth.HasActiveSession(context.User)));
builder.Services.AddSingleton<AdminCredentials>();
builder.Services.AddScoped<AdminOrders>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql", timeout: TimeSpan.FromSeconds(5));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<TextAnalyzer>();
    builder.Services.AddSingleton<AiRecommendations>();
    var ai = builder.Configuration.GetSection("Ai").Get<AiOptions>();
    if (ai is { Enabled: true }
        && !string.IsNullOrWhiteSpace(ai.ApiKey)
        && !string.IsNullOrWhiteSpace(ai.Model)
        && Uri.TryCreate(ai.Endpoint, UriKind.Absolute, out var endpoint)
        && endpoint.Scheme == Uri.UriSchemeHttps)
    {
#pragma warning disable SKEXP0010 // DeepSeek uses the OpenAI connector with a custom endpoint.
        builder.Services.AddSingleton<IChatCompletionService>(
            _ => new OpenAIChatCompletionService(ai.Model, endpoint, ai.ApiKey));
#pragma warning restore SKEXP0010
    }
    builder.Services.AddScoped<IOrderProcessor, TextReportProcessor>();
    builder.Services.AddOptions<SmtpOptions>()
        .BindConfiguration("Smtp")
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
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
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapHealthChecks("/health/ready");
app.MapCreateOrder();
app.MapAdminSession();
app.MapRazorComponents<AdminApp>().AddInteractiveServerRenderMode();

app.Run();
