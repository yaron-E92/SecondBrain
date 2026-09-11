namespace SecondBrain.Presentation;

public sealed class App : Microsoft.Maui.Controls.Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        _shell = shell;
        UserAppTheme = AppTheme.Light;
        Resources = CreateResources();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_shell);

    private static ResourceDictionary CreateResources()
    {
        var resources = new ResourceDictionary
        {
            ["SecondBrainBackground"] = SecondBrainVisual.Background,
            ["SecondBrainSurface"] = SecondBrainVisual.Surface,
            ["SecondBrainNavigation"] = SecondBrainVisual.Navigation,
            ["SecondBrainAccent"] = SecondBrainVisual.Accent,
            ["SecondBrainInk"] = SecondBrainVisual.Ink,
            ["SecondBrainMuted"] = SecondBrainVisual.Muted,
            ["SecondBrainLine"] = SecondBrainVisual.Line,
        };

        resources.Add(new Style(typeof(ContentPage))
        {
            Setters =
            {
                new Setter
                {
                    Property = VisualElement.BackgroundColorProperty,
                    Value = SecondBrainVisual.Background,
                },
            },
        });

        resources.Add(new Style(typeof(Button))
        {
            Setters =
            {
                new Setter { Property = VisualElement.MinimumHeightRequestProperty, Value = 44d },
                new Setter { Property = Button.BackgroundColorProperty, Value = SecondBrainVisual.Surface },
                new Setter { Property = Button.TextColorProperty, Value = SecondBrainVisual.Ink },
                new Setter { Property = Button.BorderColorProperty, Value = SecondBrainVisual.Line },
                new Setter { Property = Button.BorderWidthProperty, Value = 1d },
                new Setter { Property = Button.CornerRadiusProperty, Value = 10 },
                new Setter { Property = Button.PaddingProperty, Value = new Thickness(15, 10) },
            },
        });

        resources.Add(new Style(typeof(Entry))
        {
            Setters =
            {
                new Setter { Property = VisualElement.MinimumHeightRequestProperty, Value = 44d },
                new Setter { Property = VisualElement.BackgroundColorProperty, Value = SecondBrainVisual.Surface },
                new Setter { Property = InputView.TextColorProperty, Value = SecondBrainVisual.Ink },
            },
        });

        resources.Add(new Style(typeof(Editor))
        {
            Setters =
            {
                new Setter { Property = VisualElement.MinimumHeightRequestProperty, Value = 88d },
                new Setter { Property = VisualElement.BackgroundColorProperty, Value = SecondBrainVisual.Surface },
                new Setter { Property = InputView.TextColorProperty, Value = SecondBrainVisual.Ink },
            },
        });

        resources.Add(new Style(typeof(SearchBar))
        {
            Setters =
            {
                new Setter { Property = VisualElement.MinimumHeightRequestProperty, Value = 44d },
                new Setter { Property = VisualElement.BackgroundColorProperty, Value = SecondBrainVisual.Surface },
                new Setter { Property = SearchBar.TextColorProperty, Value = SecondBrainVisual.Ink },
            },
        });

        resources.Add(new Style(typeof(Picker))
        {
            Setters =
            {
                new Setter { Property = VisualElement.MinimumHeightRequestProperty, Value = 44d },
                new Setter { Property = VisualElement.BackgroundColorProperty, Value = SecondBrainVisual.Surface },
                new Setter { Property = Picker.TextColorProperty, Value = SecondBrainVisual.Ink },
            },
        });

        return resources;
    }
}
