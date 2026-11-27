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
    DatabaseGate gate,
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
            return connection.Host is "localhost" or "127.0.0.1" or "::1"
                && connection.Database == "textreport";
        }
    }

    public async Task<Order[]> ListAsync(
        OrderFilter filter, bool newestFirst, int page, CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync();
        using var access = await gate.EnterAsync(DatabaseLane.Requests, cancellationToken);
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
        using var access = await gate.EnterAsync(DatabaseLane.Requests, cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Orders.AsNoTracking().Include(x => x.Payment)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> ResetAsync(string confirmation, CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync();
        if (!CanReset || !string.Equals(confirmation.Trim(), ResetConfirmation, StringComparison.Ordinal))
            return false;

        using var requests = await gate.EnterAsync(DatabaseLane.Requests, cancellationToken);
        using var payments = await gate.EnterAsync(DatabaseLane.Payments, cancellationToken);
        using var processing = await gate.EnterAsync(DatabaseLane.Processing, cancellationToken);
        using var emails = await gate.EnterAsync(DatabaseLane.Email, cancellationToken);
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
