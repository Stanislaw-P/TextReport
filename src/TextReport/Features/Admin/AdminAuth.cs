using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace TextReport.Features.Admin;

internal sealed class AdminCredentials
{
    private readonly PasswordHasher<string> hasher = new();
    private readonly string username;
    private readonly string? passwordHash;

    public AdminCredentials(IConfiguration configuration)
    {
        username = configuration["Admin:Username"]?.Trim() ?? "";
        var password = configuration["Admin:Password"];
        if (username.Length > 0 && !string.IsNullOrWhiteSpace(password))
            passwordHash = hasher.HashPassword(username, password);
    }

    public bool IsConfigured => passwordHash is not null;

    public bool Verify(string login, string password) =>
        passwordHash is not null
        && hasher.VerifyHashedPassword(username, passwordHash, password) != PasswordVerificationResult.Failed
        && string.Equals(username, login, StringComparison.Ordinal);
}

internal static class AdminAuth
{
    public const string Policy = "TextReportAdmin";
    public const string Role = "Administrator";
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    private const string ExpiresAtClaim = "textreport:admin-expires-at";

    public static bool HasActiveSession(ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue(ExpiresAtClaim), NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAt)
        && expiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static void MapAdminSession(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/admin/session/login", LoginAsync);
        endpoints.MapPost("/admin/session/logout", LogoutAsync).RequireAuthorization(Policy);
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context, IAntiforgery antiforgery, AdminCredentials credentials, CancellationToken cancellationToken)
    {
        if (!context.Request.HasFormContentType || !await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest();

        var form = await context.Request.ReadFormAsync(cancellationToken);
        var login = form["username"].ToString().Trim();
        var password = form["password"].ToString();
        if (!credentials.Verify(login, password))
            return Results.LocalRedirect("/admin/login?error=invalid");

        var expiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime);
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, login),
                new Claim(ClaimTypes.Role, Role),
                new Claim(ExpiresAtClaim, expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { ExpiresUtc = expiresAt });
        return Results.LocalRedirect("/admin");
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest();

        await context.SignOutAsync();
        return Results.LocalRedirect("/admin/login");
    }
}
