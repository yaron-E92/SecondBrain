using SecondBrain.Application.Queries;
using SecondBrain.Application.UseCases;
using SecondBrain.Persistence;

namespace SecondBrain.App;

public sealed class MainPage : ContentPage
{
    public MainPage(
        GetApplicationStatusUseCase getApplicationStatus,
        SecondBrainPersistenceInitializer persistenceInitializer)
    {
        ArgumentNullException.ThrowIfNull(getApplicationStatus);
        ArgumentNullException.ThrowIfNull(persistenceInitializer);

        var status = getApplicationStatus.Handle(new GetApplicationStatusQuery());
        var persistenceStatus = new Label
        {
            FontSize = 16,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Colors.DarkRed
        };
        var retryButton = new Button
        {
            Text = "Retry database startup",
            HorizontalOptions = LayoutOptions.Center
        };

        void RefreshPersistenceStatus()
        {
            persistenceStatus.Text = persistenceInitializer.IsInitialized
                ? "Local database is ready."
                : persistenceInitializer.UserFacingError;
            persistenceStatus.TextColor = persistenceInitializer.IsInitialized
                ? Colors.DarkGreen
                : Colors.DarkRed;
            retryButton.IsVisible = !persistenceInitializer.IsInitialized;
        }

        retryButton.Clicked += (_, _) =>
        {
            persistenceInitializer.TryInitialize();
            RefreshPersistenceStatus();
        };
        RefreshPersistenceStatus();

        Title = "Home";
        BackgroundColor = Colors.White;
        Content = new Grid
        {
            Padding = new Thickness(24),
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 12,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = status.Name,
                            FontSize = 32,
                            FontAttributes = FontAttributes.Bold,
                            HorizontalTextAlignment = TextAlignment.Center,
                            TextColor = Colors.Black
                        },
                        new Label
                        {
                            Text = status.IsReady
                                ? "SecondBrain shell is ready."
                                : "SecondBrain shell is starting.",
                            FontSize = 18,
                            HorizontalTextAlignment = TextAlignment.Center,
                            TextColor = Colors.DarkSlateGray
                        },
                        persistenceStatus,
                        retryButton
                    }
                }
            }
        };
    }
}
