using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace TextReport.Features.Notifications.SendEmail;

internal sealed class SmtpOptions
{
    [Required]
    public string Host { get; init; } = "";

    [Range(1, 65535)]
    public int Port { get; init; }

    [Required, EmailAddress]
    public string From { get; init; } = "";

    public SecureSocketOptions SocketOptions { get; init; } = SecureSocketOptions.StartTls;
    public string? Username { get; init; }
    public string? Password { get; init; }
}
