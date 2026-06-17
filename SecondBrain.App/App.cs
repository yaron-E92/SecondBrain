namespace SecondBrain.App;

public sealed class App : Microsoft.Maui.Controls.Application
{
    private readonly AppShell shell;

    public App(AppShell shell)
    {
        this.shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(shell);
}
