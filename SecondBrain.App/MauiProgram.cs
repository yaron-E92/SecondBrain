using SecondBrain.Application.UseCases;
using SecondBrain.Persistence;

namespace SecondBrain.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "secondbrain.db");
        builder.Services.AddSecondBrainPersistence(databasePath);
        builder.Services.AddSingleton<GetApplicationStatusUseCase>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<MainPage>();

        var app = builder.Build();
        app.Services.InitializeSecondBrainPersistence();
        return app;
    }
}
