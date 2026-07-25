using Microsoft.Extensions.Logging;
using SecondBrain.Application;
using SecondBrain.Persistence;

namespace SecondBrain.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();
        builder.Logging.AddDebug();

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "secondbrain.db");
        builder.Services.AddSecondBrainApplication();
        builder.Services.AddSecondBrainPersistence(databasePath);
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<MainPage>();

        var app = builder.Build();
        app.Services
            .GetRequiredService<SecondBrainPersistenceInitializer>()
            .TryInitialize();
        return app;
    }
}
