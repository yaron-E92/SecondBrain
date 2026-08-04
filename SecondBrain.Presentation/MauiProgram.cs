using Microsoft.Extensions.Logging;
using SecondBrain.Application;
using SecondBrain.Application.UseCases;
using SecondBrain.Presentation.ViewModels;
using SecondBrain.Persistence;

namespace SecondBrain.Presentation;

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
        builder.Services.AddScoped<DashboardUseCase>();
        builder.Services.AddSingleton<InboxViewModel>();
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<ParaBrowserViewModel>();
        builder.Services.AddSingleton<CoreSearchViewModel>();
        builder.Services.AddSingleton<CoreEditorViewModel>();
        builder.Services.AddSingleton<JournalBrowserViewModel>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<InboxPage>();
        builder.Services.AddSingleton<ParaBrowserPage>();
        builder.Services.AddSingleton<CoreSearchPage>();
        builder.Services.AddSingleton<CoreEditorPage>();
        builder.Services.AddSingleton<JournalBrowserPage>();

        var app = builder.Build();
        app.Services
            .GetRequiredService<SecondBrainPersistenceInitializer>()
            .TryInitialize();
        return app;
    }
}
