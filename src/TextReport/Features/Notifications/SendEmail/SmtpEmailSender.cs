using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TextReport.Features.Notifications.SendEmail;

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(Guid orderId, string email, string resultJson, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("TextReport", smtp.From));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = $"TextReport — отчёт по заявке {orderId}";
        message.Body = new TextPart("html") { Text = EmailReportRenderer.Render(orderId, resultJson) };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, smtp.SocketOptions, timeout.Token);
        if (!string.IsNullOrWhiteSpace(smtp.Username))
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? "", timeout.Token);

        await client.SendAsync(FormatOptions.Default, new SendRequest(message), timeout.Token);
    }
}
