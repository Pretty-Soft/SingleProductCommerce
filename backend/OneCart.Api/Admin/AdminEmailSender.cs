using System.Net;
using System.Net.Mail;

namespace OneCart.Api.Admin;

public interface IAdminEmailSender
{
    Task SendPasswordResetAsync(string recipient, Uri resetLink, CancellationToken cancellationToken);
}

public sealed class SmtpAdminEmailSender(IConfiguration configuration) : IAdminEmailSender
{
    public async Task SendPasswordResetAsync(string recipient, Uri resetLink, CancellationToken cancellationToken)
    {
        var host = configuration["Email:Smtp:Host"];
        var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Configure Email:Smtp:Host and Email:From to send administrator password-reset messages.");

        var port = int.TryParse(configuration["Email:Smtp:Port"], out var configuredPort) ? configuredPort : 587;
        using var message = new MailMessage(from, recipient)
        {
            Subject = "Reset your OneCart administrator password",
            Body = $"<p>A password reset was requested for your OneCart administrator account.</p><p><a href=\"{WebUtility.HtmlEncode(resetLink.AbsoluteUri)}\">Reset password</a></p><p>If you did not request this, you can ignore this email.</p>",
            IsBodyHtml = true
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000
        };
        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username)) client.Credentials = new NetworkCredential(username, password);
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}
