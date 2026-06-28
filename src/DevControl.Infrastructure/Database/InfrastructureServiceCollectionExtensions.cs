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

        return services;
    }
}

