namespace SecondBrain.Presentation;

public sealed class AppShell : Shell
{
    public AppShell(
        MainPage mainPage,
        InboxPage inboxPage,
        ParaBrowserPage paraBrowserPage,
        CoreSearchPage searchPage,
        JournalBrowserPage journalBrowserPage,
        CoreEditorPage coreEditorPage,
        ReviewPage reviewPage,
        DataImportPage dataImportPage)
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
                    Route = "para",
                    Title = "PARA",
                    Content = paraBrowserPage
                },
                new ShellContent
                {
                    Route = "search",
                    Title = "Search",
                    Content = searchPage
                },
                new ShellContent
                {
                    Route = "journals",
                    Title = "Journals",
                    Content = journalBrowserPage
                },
                new ShellContent
                {
                    Route = "editor",
                    Title = "Editor",
                    Content = coreEditorPage
                },
                new ShellContent
                {
                    Route = "review",
                    Title = "Review",
                    Content = reviewPage
                },
                new ShellContent
                {
                    Route = "data-import",
                    Title = "Data / Import",
                    Content = dataImportPage
                }
            }
        });
    }
}
