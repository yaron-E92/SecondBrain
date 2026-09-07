using Microsoft.Maui.Controls.Shapes;

namespace SecondBrain.Presentation;

public sealed class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        Title = "Settings";
        BackgroundColor = Colors.White;

        var import = new Button
        {
            Text = "Import from Notion",
            MinimumHeightRequest = 44,
            HorizontalOptions = LayoutOptions.Start,
            AutomationId = "SettingsDataImport"
        };
        import.Clicked += async (_, _) => await Shell.Current.GoToAsync("//data-import");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20, 16),
                Spacing = 20,
                Children =
                {
                    new Label
                    {
                        Text = "Settings",
                        FontSize = 28,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.Black
                    },
                    new Label
                    {
                        Text = "Manage SecondBrain data and application preferences without turning utility workflows into primary navigation.",
                        TextColor = Colors.DarkSlateGray
                    },
                    new Border
                    {
                        Stroke = Colors.LightGray,
                        StrokeShape = new RoundRectangle { CornerRadius = 10 },
                        Padding = 14,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 10,
                            Children =
                            {
                                new Label
                                {
                                    Text = "Data",
                                    FontSize = 18,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Colors.Black
                                },
                                new Label
                                {
                                    Text = "Bring existing knowledge into SecondBrain through a local preview and review before anything is changed.",
                                    TextColor = Colors.DarkSlateGray
                                },
                                import
                            }
                        }
                    }
                }
            }
        };
    }
}
