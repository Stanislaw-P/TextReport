namespace TextReport.Features.Notifications.SendEmail;

internal sealed class DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger) : IEmailSender
{
    public Task SendAsync(Guid orderId, string email, string resultJson, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Simulated report delivery for order {OrderId}", orderId);
        return Task.CompletedTask;
    }
}
