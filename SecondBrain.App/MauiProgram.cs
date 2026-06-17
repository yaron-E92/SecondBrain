using SecondBrain.Application.UseCases;

namespace SecondBrain.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<GetApplicationStatusUseCase>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
