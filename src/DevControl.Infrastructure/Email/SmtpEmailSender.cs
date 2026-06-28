using System.Net;
using System.Net.Mail;
using DevControl.Application.Email;

namespace DevControl.Infrastructure.Email;

public sealed class SmtpEmailSender(EmailConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.SmtpHost))
        {
            throw new InvalidOperationException("DEVCONTROL_SMTP_HOST is required when DEVCONTROL_EMAIL_MODE=smtp.");
        }

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(configuration.FromEmail, configuration.FromName),
            Subject = message.Subject,
            Body = message.TextBody,
            IsBodyHtml = false
        };

        mailMessage.To.Add(new MailAddress(message.ToEmail));
        mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            message.HtmlBody,
            contentType: new System.Net.Mime.ContentType("text/html")));

        using var client = new SmtpClient(configuration.SmtpHost, configuration.SmtpPort)
        {
            EnableSsl = configuration.SmtpUseStartTls
        };

        if (!string.IsNullOrWhiteSpace(configuration.SmtpUsername))
        {
            client.Credentials = new NetworkCredential(configuration.SmtpUsername, configuration.SmtpPassword);
        }

        using var registration = cancellationToken.Register(client.SendAsyncCancel);
        await client.SendMailAsync(mailMessage, cancellationToken);
    }
}
