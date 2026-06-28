using Microsoft.Extensions.Configuration;

namespace DevControl.Infrastructure.Email;

public sealed class EmailConfiguration
{
    public string Mode { get; init; } = "log";

    public string FromEmail { get; init; } = "devcontrol@localhost";

    public string FromName { get; init; } = "DevControl";

    public string? SmtpHost { get; init; }

    public int SmtpPort { get; init; } = 587;

    public string? SmtpUsername { get; init; }

    public string? SmtpPassword { get; init; }

    public bool SmtpUseStartTls { get; init; } = true;

    public static EmailConfiguration FromConfiguration(IConfiguration configuration)
    {
        var mode = configuration["EMAIL_MODE"];
        var smtpPortText = configuration["SMTP_PORT"];
        var smtpStartTlsText = configuration["SMTP_USE_STARTTLS"];

        return new EmailConfiguration
        {
            Mode = string.IsNullOrWhiteSpace(mode) ? "log" : mode.Trim(),
            FromEmail = configuration["EMAIL_FROM_ADDRESS"] ?? "devcontrol@localhost",
            FromName = configuration["EMAIL_FROM_NAME"] ?? "DevControl",
            SmtpHost = configuration["SMTP_HOST"],
            SmtpPort = int.TryParse(smtpPortText, out var smtpPort) ? smtpPort : 587,
            SmtpUsername = configuration["SMTP_USERNAME"],
            SmtpPassword = configuration["SMTP_PASSWORD"],
            SmtpUseStartTls = !bool.TryParse(smtpStartTlsText, out var useStartTls) || useStartTls
        };
    }
}
