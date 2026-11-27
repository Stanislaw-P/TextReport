using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using TextReport.Features.Orders;
using TextReport.Infrastructure.Persistence;
using TextReport.Workers;

namespace TextReport.Features.Admin;

internal enum OrderFilter
{
    All, Payment, Processing, Email, Completed, Failed
}

internal sealed class AdminOrders(
    IDbContextFactory<TextReportDbContext> dbFactory,
    IOptions<WorkerOptions> options,
    IHostEnvironment environment,
    AuthenticationStateProvider authentication,
    IAuthorizationService authorization)
{
    public const int PageSize = 25;
    public const string ResetConfirmation = "УДАЛИТЬ";
    public int MaxAttempts => options.Value.MaxAttempts;

    public bool CanReset
    {
        get
        {
            if (!environment.IsDevelopment())
                return false;

            using var db = dbFactory.CreateDbContext();
            var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            var localHost = connection.Host is "localhost" or "127.0.0.1" or "::1";
            var composeHost = connection.Host == "postgres"
                && string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true",
                    StringComparison.OrdinalIgnoreCase);
            return connection.Database == "textreport" && (localHost || composeHost);
        }
    }

    public async Task<Order[]> ListAsync(
        OrderFilter filter, bool newestFirst, int page, CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<Order> query = db.Orders.AsNoTracking().Include(x => x.Payment);
        var max = MaxAttempts;
        query = filter switch
        {
            OrderFilter.Payment => query.Where(x => x.PaidAt == null && x.Payment.Attempts.Count < max),
            OrderFilter.Processing => query.Where(x => x.PaidAt != null && x.ProcessedAt == null
                && x.ProcessingAttempts.Count < max),
            OrderFilter.Email => query.Where(x => x.ProcessedAt != null && x.EmailSentAt == null
                && x.EmailAttempts.Count < max),
            OrderFilter.Completed => query.Where(x => x.EmailSentAt != null),
            OrderFilter.Failed => query.Where(x =>
                (x.PaidAt == null && x.Payment.Attempts.Count >= max)
                || (x.ProcessedAt == null && x.ProcessingAttempts.Count >= max)
                || (x.EmailSentAt == null && x.EmailAttempts.Count >= max)),
            _ => query
        };

        var sorted = newestFirst
            ? query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            : query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id);
        return await sorted.Skip(page * PageSize).Take(PageSize + 1).ToArrayAsync(cancellationToken);
    }

    public async Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Orders.AsNoTracking().Include(x => x.Payment)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> ResetAsync(string confirmation, CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync();
        if (!CanReset || !string.Equals(confirmation.Trim(), ResetConfirmation, StringComparison.Ordinal))
            return false;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        return true;
    }

    private async Task EnsureAuthorizedAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        if (!(await authorization.AuthorizeAsync(user, AdminAuth.Policy)).Succeeded)
            throw new UnauthorizedAccessException();
    }
}
