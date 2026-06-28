using DevControl.Application.Email;
using DevControl.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevControl.Infrastructure.Database;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDevControlInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);

        services.AddDbContext<DevControlDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        var emailConfiguration = EmailConfiguration.FromConfiguration(configuration);
        services.AddSingleton(emailConfiguration);
        services.AddSingleton<IEmailSender>(serviceProvider =>
        {
            return string.Equals(emailConfiguration.Mode, "smtp", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(serviceProvider)
                : ActivatorUtilities.CreateInstance<LoggingEmailSender>(serviceProvider);
        });

        return services;
    }
}
