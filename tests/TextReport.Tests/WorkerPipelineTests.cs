using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using TextReport.Features.Notifications.SendEmail;
using TextReport.Features.Orders;
using TextReport.Features.Orders.ProcessOrder;
using TextReport.Features.Payments;
using TextReport.Infrastructure.Persistence;
using TextReport.Workers;

namespace TextReport.Tests;

public sealed class WorkerPipelineTests
{
    private const string ConnectionVariable = "TEXTREPORT_TEST_POSTGRES";
    public static bool PostgresIsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable));

    [Fact(SkipUnless = nameof(PostgresIsConfigured), Skip = "Set TEXTREPORT_TEST_POSTGRES to run worker integration tests.")]
    public async Task Workers_RespectStageOrderAndRetryOnlyFailedDelivery()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable))
        {
            Database = $"textreport_test_{Guid.NewGuid():N}"
        };
        var processor = new ControlledProcessor();
        var sender = new FailOnceSender();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddDbContextFactory<TextReportDbContext>(options => options.UseNpgsql(connection.ConnectionString))
            .AddSingleton<IOrderProcessor>(processor)
            .AddSingleton<IEmailSender>(sender)
            .AddSingleton(Options.Create(new WorkerOptions
            {
                PollIntervalSeconds = 1, RetryDelaySeconds = 3, MaxAttempts = 3
            }))
            .BuildServiceProvider();

        var database = services.GetRequiredService<IDbContextFactory<TextReportDbContext>>();
        await using var setup = await database.CreateDbContextAsync(cancellationToken);
        await setup.Database.EnsureCreatedAsync(cancellationToken);
        try
        {
            var id = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            setup.Orders.Add(new Order
            {
                Id = id, Email = "reader@example.org", InputText = "A sample text for the pipeline.", CreatedAt = now,
                Payment = new Payment { Id = Guid.NewGuid(), OrderId = id, Amount = 100, Currency = "RUB", CreatedAt = now }
            });
            await setup.SaveChangesAsync(cancellationToken);

            using var payment = ActivatorUtilities.CreateInstance<PaymentWorker>(services);
            using var processing = ActivatorUtilities.CreateInstance<OrderWorker>(services);
            using var email = ActivatorUtilities.CreateInstance<EmailWorker>(services);
            try
            {
                await processing.StartAsync(cancellationToken);
                await email.StartAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken);
                Assert.False(processor.Started.Task.IsCompleted);
                Assert.Equal(0, sender.Calls);

                await payment.StartAsync(cancellationToken);
                await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                var processingOrder = await ReadAsync(database, id, cancellationToken);
                Assert.NotNull(processingOrder.PaidAt);
                Assert.Null(processingOrder.ProcessedAt);
                Assert.Equal(0, sender.Calls);
                processor.AllowCompletion.SetResult();

                var failure = await WaitForAsync(database, id, x => x.EmailAttempts.LastError is not null, cancellationToken);
                Assert.Null(failure.EmailSentAt);
                Assert.NotNull(failure.EmailAttempts.NextAttemptAt);
                Assert.Equal(1, failure.EmailAttempts.Count);

                var sent = await WaitForAsync(database, id, x => x.EmailSentAt is not null, cancellationToken);
                Assert.Equal(PaymentStatus.Paid, sent.Payment.Status);
                Assert.Equal(1, sent.Payment.Attempts.Count);
                Assert.Equal(1, sent.ProcessingAttempts.Count);
                Assert.Equal(failure.ProcessedAt, sent.ProcessedAt);
                Assert.Equal(failure.ResultJson, sent.ResultJson);
                Assert.Equal(2, sent.EmailAttempts.Count);
                Assert.Null(sent.EmailAttempts.LastError);
                Assert.Null(sent.EmailAttempts.NextAttemptAt);
                Assert.Equal(2, sender.Calls);
                Assert.Equal(1, processor.Calls);
            }
            finally
            {
                await email.StopAsync(CancellationToken.None);
                await processing.StopAsync(CancellationToken.None);
                await payment.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    private static async Task<Order> ReadAsync(
        IDbContextFactory<TextReportDbContext> factory, Guid id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Orders.AsNoTracking().Include(x => x.Payment).SingleAsync(x => x.Id == id, cancellationToken);
    }

    private static async Task<Order> WaitForAsync(IDbContextFactory<TextReportDbContext> factory, Guid id,
        Func<Order, bool> condition, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var order = await ReadAsync(factory, id, timeout.Token);
            if (condition(order))
                return order;
            await Task.Delay(150, timeout.Token);
        }
    }

    private sealed class ControlledProcessor : IOrderProcessor
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<string> ProcessAsync(string inputText, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            Started.TrySetResult();
            await AllowCompletion.Task.WaitAsync(cancellationToken);
            return """{"wordCount":6}""";
        }
    }

    private sealed class FailOnceSender : IEmailSender
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);

        public Task SendAsync(Guid orderId, string email, string resultJson, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new IOException("Simulated SMTP failure.");
            return Task.CompletedTask;
        }
    }
}
