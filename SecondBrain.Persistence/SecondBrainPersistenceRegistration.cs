using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecondBrain.Application.Ports;
using SecondBrain.Application.NotionAudit;

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
        services.AddDbContextFactory<SecondBrainDbContext>(
            options => options.UseSqlite($"Data Source={fullPath}"));
        services.AddScoped<SecondBrainDataStore>();
        services.AddScoped<ICoreKnowledgeRepository>(
            provider => provider.GetRequiredService<SecondBrainDataStore>());
        services.AddScoped<ICoreSearchQueryService, CoreSearchQueryService>();
        services.AddSingleton<INotionExportReader, NotionExportReader>();
        services.AddSingleton<INotionImportExecutor>(provider =>
            new NotionImportExecutor(
                provider.GetRequiredService<IDbContextFactory<SecondBrainDbContext>>()));
        services.AddSingleton<SecondBrainPersistenceInitializer>();
        return services;
    }
}
