using Microsoft.Maui.Controls.Shapes;

namespace SecondBrain.Presentation;

internal static class SecondBrainVisual
{
    internal static readonly Color Background = Color.FromArgb("#F5F7FA");
    internal static readonly Color Surface = Colors.White;
    internal static readonly Color Navigation = Color.FromArgb("#17283A");
    internal static readonly Color Accent = Color.FromArgb("#315C91");
    internal static readonly Color AccentSoft = Color.FromArgb("#E9F0F8");
    internal static readonly Color Ink = Color.FromArgb("#182026");
    internal static readonly Color Muted = Color.FromArgb("#65717B");
    internal static readonly Color Line = Color.FromArgb("#D8DEE4");
    internal static readonly Color Success = Color.FromArgb("#246B4A");
    internal static readonly Color Danger = Color.FromArgb("#A33232");
    internal static readonly Color Warning = Color.FromArgb("#8A5A15");

    internal static Label Eyebrow(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 12,
        CharacterSpacing = 1.2,
        FontAttributes = FontAttributes.Bold,
        TextColor = Accent,
    };

    internal static Label PageTitle(string text) => new()
    {
        Text = text,
        FontSize = 30,
        FontAttributes = FontAttributes.Bold,
        TextColor = Ink,
        LineBreakMode = LineBreakMode.WordWrap,
    };

    internal static Label SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = 18,
        FontAttributes = FontAttributes.Bold,
        TextColor = Ink,
    };

    internal static Label Body(string text) => new()
    {
        Text = text,
        FontSize = 14,
        TextColor = Muted,
        LineBreakMode = LineBreakMode.WordWrap,
    };

    internal static Border Card(params View[] children)
    {
        var layout = new VerticalStackLayout
        {
            Spacing = 12,
        };
        foreach (var child in children)
        {
            layout.Children.Add(child);
        }

        return new Border
        {
            BackgroundColor = Surface,
            Stroke = Line,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = 18,
            Content = layout,
        };
    }

    internal static Button PrimaryButton(string text, string? automationId = null) => new()
    {
        Text = text,
        AutomationId = automationId,
        BackgroundColor = Accent,
        TextColor = Colors.White,
        FontAttributes = FontAttributes.Bold,
        CornerRadius = 10,
        MinimumHeightRequest = 44,
        Padding = new Thickness(16, 10),
    };

    internal static Button SecondaryButton(string text, string? automationId = null) => new()
    {
        Text = text,
        AutomationId = automationId,
        BackgroundColor = Surface,
        TextColor = Ink,
        BorderColor = Line,
        BorderWidth = 1,
        CornerRadius = 10,
        MinimumHeightRequest = 44,
        Padding = new Thickness(16, 10),
    };

    internal static Button QuietButton(string text, string? automationId = null) => new()
    {
        Text = text,
        AutomationId = automationId,
        BackgroundColor = Colors.Transparent,
        TextColor = Accent,
        BorderWidth = 0,
        CornerRadius = 8,
        MinimumHeightRequest = 44,
        Padding = new Thickness(10, 8),
    };
}
