using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SecondBrain.Persistence;

public sealed class SecondBrainPersistenceInitializer(
    IDbContextFactory<SecondBrainDbContext> contextFactory,
    ILogger<SecondBrainPersistenceInitializer> logger)
{
    private readonly object syncRoot = new();

    public bool IsInitialized { get; private set; }

    public string? UserFacingError { get; private set; }

    public bool TryInitialize()
    {
        lock (syncRoot)
        {
            if (IsInitialized)
            {
                return true;
            }

            try
            {
                using var context = contextFactory.CreateDbContext();
                context.Database.Migrate();
                IsInitialized = true;
                UserFacingError = null;
                logger.LogInformation("SecondBrain persistence initialized.");
                return true;
            }
            catch (Exception exception)
            {
                UserFacingError =
                    "SecondBrain could not open its local database. " +
                    "Check available storage and try again.";
                logger.LogError(
                    exception,
                    "SecondBrain persistence initialization failed.");
                return false;
            }
        }
    }
}
