namespace SecondBrain.Presentation;

public sealed class AppShell : Shell
{
    public AppShell(
        MainPage mainPage,
        InboxPage inboxPage,
        CoreEditorPage coreEditorPage)
    {
        Title = "SecondBrain";

        Items.Add(new TabBar
        {
            Items =
            {
                new ShellContent
                {
                    Route = "home",
                    Title = "Home",
                    Content = mainPage
                },
                new ShellContent
                {
                    Route = "inbox",
                    Title = "Inbox",
                    Content = inboxPage
                },
                new ShellContent
                {
                    Route = "editor",
                    Title = "Editor",
                    Content = coreEditorPage
                }
            }
        });
    }
}
