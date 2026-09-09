namespace SecondBrain.Presentation;

public sealed class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        Title = "Settings";
        BackgroundColor = SecondBrainVisual.Background;

        var import = SecondBrainVisual.SecondaryButton(
            "Import from Notion",
            "SettingsDataImport");
        import.HorizontalOptions = LayoutOptions.Start;
        import.Clicked += async (_, _) => await Shell.Current.GoToAsync("//data-import");

        var data = SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow("Data"),
            SecondBrainVisual.SectionTitle("Bring your knowledge with you"),
            SecondBrainVisual.Body(
                "Import existing knowledge locally. You will preview and review changes before anything is written."),
            import);

        var body = new VerticalStackLayout
        {
            Padding = new Thickness(24, 20, 24, 36),
            Spacing = 18,
            MaximumWidthRequest = 900,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                SecondBrainVisual.PageTitle("Settings"),
                SecondBrainVisual.Body(
                    "Manage data and application preferences without interrupting the way you capture, process, and find knowledge."),
                data,
            },
        };

        Content = new ScrollView
        {
            Content = body,
        };
    }
}
