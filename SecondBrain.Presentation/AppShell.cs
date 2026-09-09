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
        BackgroundColor = Color.FromArgb("#F6F8FB");
        Shell.SetBackgroundColor(this, Color.FromArgb("#17283A"));
        Shell.SetForegroundColor(this, Colors.White);
        FlyoutBehavior = DeviceInfo.Idiom == DeviceIdiom.Desktop
            ? FlyoutBehavior.Locked
            : FlyoutBehavior.Disabled;

        var capture = new ToolbarItem
        {
            Text = "+ Capture",
            Order = ToolbarItemOrder.Primary,
            Priority = 0,
            AutomationId = "GlobalCapture"
        };
        capture.Clicked += async (_, _) =>
        {
            await GoToAsync("//home");
            mainPage.FocusCapture();
        };
        ToolbarItems.Add(capture);

        var settings = new ToolbarItem
        {
            Text = "Settings",
            Order = ToolbarItemOrder.Secondary,
            Priority = 1,
            AutomationId = "GlobalSettings"
        };
        settings.Clicked += async (_, _) => await GoToAsync("//settings");
        ToolbarItems.Add(settings);

        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
        {
            Items.Add(PrimaryItem("home", "Home", mainPage));
            Items.Add(PrimaryItem("inbox", "Inbox", inboxPage));
            Items.Add(PrimaryItem("para", "Browse", paraBrowserPage));
            Items.Add(PrimaryItem("search", "Search", searchPage));
            Items.Add(PrimaryItem("review", "Review", reviewPage));
        }
        else
        {
            Items.Add(new TabBar
            {
                Title = "SecondBrain",
                Items =
                {
                    PrimaryContent("home", "Home", mainPage),
                    PrimaryContent("inbox", "Inbox", inboxPage),
                    PrimaryContent("para", "Browse", paraBrowserPage),
                    PrimaryContent("search", "Search", searchPage),
                    PrimaryContent("review", "Review", reviewPage),
                }
            });
        }

        Items.Add(new FlyoutItem
        {
            Route = "journals",
            Title = "Journals",
            FlyoutItemIsVisible = false,
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
            FlyoutItemIsVisible = false,
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

    private static FlyoutItem PrimaryItem(string route, string title, Page page) =>
        new()
        {
            Route = route,
            Title = title,
            AutomationId = $"Primary{title}",
            Items = { new ShellContent { Title = title, Content = page } },
        };

    private static ShellContent PrimaryContent(string route, string title, Page page) =>
        new()
        {
            Route = route,
            Title = title,
            Content = page,
            AutomationId = $"Primary{title}",
        };
}
