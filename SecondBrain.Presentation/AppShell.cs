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
        SettingsPage settingsPage,
        DataImportPage dataImportPage)
    {
        Title = "SecondBrain";

        var settings = new ToolbarItem
        {
            Text = "Settings",
            Order = ToolbarItemOrder.Primary,
            Priority = 0,
            AutomationId = "GlobalSettings"
        };
        settings.Clicked += async (_, _) => await GoToAsync("//settings");
        ToolbarItems.Add(settings);

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
            Route = "settings",
            Title = "Settings",
            Items =
            {
                new ShellContent
                {
                    Title = "Settings",
                    Content = settingsPage
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Route = "data-import",
            Title = "Data / Import",
            FlyoutItemIsVisible = false,
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
