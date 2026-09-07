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
            Title = "SecondBrain",
            FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems,
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
                    Title = "Browse",
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
                    Route = "review",
                    Title = "Review",
                    Content = reviewPage
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Route = "journals",
            Title = "Journals",
            Items =
            {
                new ShellContent
                {
                    Title = "Journals",
                    Content = journalBrowserPage
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Route = "data-import",
            Title = "Data / Import",
            Items =
            {
                new ShellContent
                {
                    Title = "Import",
                    Content = dataImportPage
                }
            }
        });

        var editor = new FlyoutItem
        {
            Route = "editor",
            Title = "Editor",
            FlyoutItemIsVisible = false,
            Items =
            {
                new ShellContent
                {
                    Title = "Editor",
                    Content = coreEditorPage
                }
            }
        };
        Items.Add(editor);
    }
}
