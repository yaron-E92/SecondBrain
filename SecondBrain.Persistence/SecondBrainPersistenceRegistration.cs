using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SecondBrain.Persistence;

public static class SecondBrainPersistenceRegistration
{
    public static IServiceCollection AddSecondBrainPersistence(
        this IServiceCollection services,
        string databasePath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        services.AddDbContext<SecondBrainDbContext>(
            options => options.UseSqlite($"Data Source={fullPath}"));
        services.AddScoped<SecondBrainDataStore>();
        return services;
    }

    public static void InitializeSecondBrainPersistence(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SecondBrainDbContext>().Database.Migrate();
    }
}
