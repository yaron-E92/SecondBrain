namespace SecondBrain.Presentation;

public sealed class App : Microsoft.Maui.Controls.Application
{
    private readonly AppShell shell;

    public App(AppShell shell)
    {
        this.shell = shell;
        Resources = CreateResources();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(shell);

    private static ResourceDictionary CreateResources()
    {
        var resources = new ResourceDictionary
        {
            ["SecondBrainBackground"] = Color.FromArgb("#F6F8FB"),
            ["SecondBrainAccent"] = Color.FromArgb("#315C91"),
            ["SecondBrainInk"] = Color.FromArgb("#182026"),
            ["SecondBrainMuted"] = Color.FromArgb("#65717B"),
            ["SecondBrainLine"] = Color.FromArgb("#D8DEE4"),
        };

        resources.Add(new Style(typeof(ContentPage))
        {
            Setters =
            {
                new Setter { Property = VisualElement.BackgroundColorProperty, Value = resources["SecondBrainBackground"] },
            },
        });
        resources.Add(new Style(typeof(Button))
        {
            Setters =
            {
                new Setter { Property = VisualElement.MinimumHeightRequestProperty, Value = 44d },
                new Setter { Property = Button.CornerRadiusProperty, Value = 12 },
                new Setter { Property = Button.PaddingProperty, Value = new Thickness(15, 10) },
            },
        });
        resources.Add(new Style(typeof(Entry))
        {
            Setters =
            {
                new Setter { Property = VisualElement.MinimumHeightRequestProperty, Value = 44d },
            },
        });
        resources.Add(new Style(typeof(Picker))
        {
            Setters =
            {
                new Setter { Property = VisualElement.MinimumHeightRequestProperty, Value = 44d },
            },
        });
        return resources;
    }
}
