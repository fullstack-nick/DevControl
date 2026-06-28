using DevControl.Application.Email;
using Microsoft.Extensions.Logging;

namespace DevControl.Infrastructure.Email;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email log mode: would send invitation email to {ToEmail} with subject {Subject}. Text body: {TextBody}",
            message.ToEmail,
            message.Subject,
            message.TextBody);

        return Task.CompletedTask;
    }
}
