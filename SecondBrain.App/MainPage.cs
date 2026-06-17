using SecondBrain.Application.Queries;
using SecondBrain.Application.UseCases;

namespace SecondBrain.App;

public sealed class MainPage : ContentPage
{
    public MainPage(GetApplicationStatusUseCase getApplicationStatus)
    {
        ArgumentNullException.ThrowIfNull(getApplicationStatus);

        var status = getApplicationStatus.Handle(new GetApplicationStatusQuery());

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
                        }
                    }
                }
            }
        };
    }
}
