using System.Net;
using System.Net.Mail;

namespace OneCart.Api.Admin;

public interface IAdminEmailSender
{
    Task SendPasswordResetAsync(string recipient, Uri resetLink, CancellationToken cancellationToken);
}

public sealed class SmtpAdminEmailSender(AdminSmtpSettings settings) : IAdminEmailSender
{
    public Task SendPasswordResetAsync(string recipient, Uri resetLink, CancellationToken cancellationToken) =>
        SendAsync(recipient, "Reset your OneCart administrator password",
            $"<p>A password reset was requested for your OneCart administrator account.</p><p><a href=\"{WebUtility.HtmlEncode(resetLink.AbsoluteUri)}\">Reset password</a></p><p>If you did not request this, you can ignore this email.</p>", cancellationToken);

    public Task SendTestAsync(string recipient, CancellationToken cancellationToken) =>
        SendAsync(recipient, "OneCart SMTP test", "<p>Your OneCart SMTP relay accepted this test message.</p>", cancellationToken);

    private async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var (host, port, from, username, password) = settings.Get();
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Configure Email:Smtp:Host and Email:From to send administrator password-reset messages.");

        if (port is < 1 or > 65535 or 465)
            throw new InvalidOperationException("Configure a valid SMTP STARTTLS port, typically 587.");
        using var message = new MailMessage(from, recipient)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(username)) client.Credentials = new NetworkCredential(username, password);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try { await client.SendMailAsync(message, deadline.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The SMTP relay did not complete delivery within 15 seconds.");
        }
    }
}
