namespace SecondBrain.App;

public sealed class AppShell : Shell
{
    public AppShell(MainPage mainPage)
    {
        Title = "SecondBrain";

        Items.Add(new ShellContent
        {
            Title = "Home",
            Content = mainPage
        });
    }
}
