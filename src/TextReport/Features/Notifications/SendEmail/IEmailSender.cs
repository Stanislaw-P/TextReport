namespace TextReport.Features.Notifications.SendEmail;

internal interface IEmailSender
{
    Task SendAsync(Guid orderId, string email, string resultJson, CancellationToken cancellationToken);
}
