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
        FlyoutBackgroundColor = SecondBrainVisual.Navigation;
        Shell.SetBackgroundColor(this, SecondBrainVisual.Navigation);
        Shell.SetForegroundColor(this, Colors.White);
        Shell.SetTitleColor(this, Colors.White);

        var isDesktop = DeviceInfo.Idiom == DeviceIdiom.Desktop;
        FlyoutBehavior = isDesktop
            ? FlyoutBehavior.Locked
            : FlyoutBehavior.Disabled;

        if (isDesktop)
        {
            ConfigureDesktopRail(mainPage);
            Shell.SetNavBarIsVisible(mainPage, false);
            Shell.SetNavBarIsVisible(inboxPage, false);
            Shell.SetNavBarIsVisible(paraBrowserPage, false);
            Shell.SetNavBarIsVisible(searchPage, false);
            Shell.SetNavBarIsVisible(reviewPage, false);

            Items.Add(PrimaryItem("home", "Home", mainPage));
            Items.Add(PrimaryItem("inbox", "Process", inboxPage));
            Items.Add(PrimaryItem("para", "Browse", paraBrowserPage));
            Items.Add(PrimaryItem("search", "Search", searchPage));
            Items.Add(PrimaryItem("review", "Review", reviewPage));
        }
        else
        {
            ConfigureMobileActions(mainPage);
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

    private void ConfigureDesktopRail(MainPage mainPage)
    {
        var capture = new Button
        {
            Text = "+ Capture",
            AutomationId = "GlobalCapture",
            BackgroundColor = SecondBrainVisual.Accent,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            BorderWidth = 0,
            CornerRadius = 10,
            MinimumHeightRequest = 44,
        };
        capture.Clicked += async (_, _) =>
        {
            await GoToAsync("//home");
            mainPage.FocusCapture();
        };

        FlyoutHeader = new VerticalStackLayout
        {
            Padding = new Thickness(18, 22, 18, 14),
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = "SecondBrain",
                    FontSize = 24,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                },
                new Label
                {
                    Text = "Capture → Process → Find → Review",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#C7D3DF"),
                },
                capture,
            },
        };

        var settings = new Button
        {
            Text = "Settings",
            AutomationId = "GlobalSettings",
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White,
            BorderColor = Color.FromArgb("#40556A"),
            BorderWidth = 1,
            CornerRadius = 10,
            MinimumHeightRequest = 44,
            Margin = new Thickness(14, 8, 14, 14),
        };
        settings.Clicked += async (_, _) => await GoToAsync("//settings");
        FlyoutFooter = settings;
    }

    private void ConfigureMobileActions(MainPage mainPage)
    {
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
