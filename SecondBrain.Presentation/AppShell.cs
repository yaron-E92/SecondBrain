namespace SecondBrain.Presentation;

public sealed class AppShell : Shell
{
    public AppShell(
        MainPage mainPage,
        InboxPage inboxPage,
        InboxProcessPage inboxProcessPage,
        ParaBrowserPage paraBrowserPage,
        CoreSearchPage searchPage,
        JournalBrowserPage journalBrowserPage,
        CoreEditorPage coreEditorPage,
        ReviewPage reviewPage,
        SettingsPage settingsPage,
        DataImportPage dataImportPage)
    {
        Title = "SecondBrain";
        BackgroundColor = SecondBrainVisual.Background;
        Shell.SetBackgroundColor(this, SecondBrainVisual.Navigation);
        Shell.SetForegroundColor(this, Colors.White);
        Shell.SetTitleColor(this, Colors.White);
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
            Items.Add(PrimaryItem("inbox", "Process", inboxPage));
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
                    PrimaryContent("inbox", "Process", inboxPage),
                    PrimaryContent("para", "Browse", paraBrowserPage),
                    PrimaryContent("search", "Search", searchPage),
                    PrimaryContent("review", "Review", reviewPage),
                }
            });
        }

        Items.Add(HiddenItem("inbox-process", "Process item", inboxProcessPage));
        Items.Add(HiddenItem("journals", "Journals", journalBrowserPage));
        Items.Add(HiddenItem("settings", "Settings", settingsPage));
        Items.Add(HiddenItem("data-import", "Import", dataImportPage));
        Items.Add(HiddenItem("editor", "Editor", coreEditorPage));
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

    private static FlyoutItem HiddenItem(string route, string title, Page page) =>
        new()
        {
            Route = route,
            Title = title,
            FlyoutItemIsVisible = false,
            Items =
            {
                new ShellContent
                {
                    Title = title,
                    Content = page,
                },
            },
        };
}
