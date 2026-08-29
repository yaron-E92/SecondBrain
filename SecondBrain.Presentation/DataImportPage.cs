using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Controls.Shapes;
using SecondBrain.Application.NotionAudit;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class DataImportPage : ContentPage
{
    private readonly NotionParityAuditViewModel _viewModel;
    private readonly VerticalStackLayout _reportDetails = new() { Spacing = 12 };

    public DataImportPage(NotionParityAuditViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Data / Import";
        BackgroundColor = Colors.White;

        var selectFolder = new Button
        {
            Text = "Choose export folder",
            HorizontalOptions = LayoutOptions.Start,
            AutomationId = "NotionAuditSelectFolder"
        };
        selectFolder.Clicked += async (_, _) => await SelectFolderAsync();

        var selectArchive = new Button
        {
            Text = "Choose archive or manifest",
            HorizontalOptions = LayoutOptions.Start,
            AutomationId = "NotionAuditSelectArchive"
        };
        selectArchive.Clicked += async (_, _) => await SelectArchiveAsync();

        var cancel = new Button { Text = "Cancel scan" };
        cancel.SetBinding(IsVisibleProperty, nameof(viewModel.CanCancel));
        cancel.Clicked += (_, _) => viewModel.Cancel();

        var export = new Button
        {
            Text = "Export redacted report",
            HorizontalOptions = LayoutOptions.Start
        };
        export.SetBinding(IsVisibleProperty, nameof(viewModel.HasReport));
        export.Clicked += async (_, _) => await ExportReportAsync();

        var progress = new ActivityIndicator { Color = Colors.DarkSlateBlue };
        progress.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsScanning));
        progress.SetBinding(IsVisibleProperty, nameof(viewModel.IsScanning));

        var status = new Label { TextColor = Colors.DarkSlateGray };
        status.SetBinding(Label.TextProperty, nameof(viewModel.StatusMessage));
        var error = new Label { TextColor = Colors.DarkRed };
        error.SetBinding(Label.TextProperty, nameof(viewModel.ErrorMessage));
        error.SetBinding(IsVisibleProperty, nameof(viewModel.HasError));

        var report = Card("Parity report", _reportDetails);
        report.SetBinding(IsVisibleProperty, nameof(viewModel.HasReport));
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.Report))
            {
                BuildReport(viewModel.Report);
            }
        };
        BuildReport(viewModel.Report);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 16,
                Children =
                {
                    new Label
                    {
                        Text = "Notion parity audit",
                        FontSize = 28,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.Black
                    },
                    new Label
                    {
                        Text = "Preview what Core can represent before any import. Exported text stays local and this scan never mutates Core.",
                        TextColor = Colors.DarkSlateGray
                    },
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { selectFolder, selectArchive, cancel }
                    },
                    progress,
                    status,
                    error,
                    report,
                    export
                }
            }
        };
    }

    private async Task SelectFolderAsync()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
            if (result.IsSuccessful)
            {
                await _viewModel.ScanAsync(result.Folder.Path);
            }
            else if (result.Exception is not null)
            {
                throw result.Exception;
            }
        }
        catch (Exception exception)
        {
            await DisplayPickerErrorAsync(exception);
        }
    }

    private async Task SelectArchiveAsync()
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a Notion ZIP archive or supported manifest"
            });
            if (file is not null)
            {
                await _viewModel.ScanAsync(file.FullPath);
            }
        }
        catch (Exception exception)
        {
            await DisplayPickerErrorAsync(exception);
        }
    }

    private Task DisplayPickerErrorAsync(Exception exception) => DisplayAlertAsync(
        "Could not open picker",
        $"Choose the export again after checking file permissions. {exception.Message}",
        "OK");

    private void BuildReport(NotionAuditReport? report)
    {
        _reportDetails.Children.Clear();
        if (report is null)
        {
            return;
        }

        var summary = report.Summary;
        _reportDetails.Children.Add(new Label
        {
            Text = $"Will import: {summary.WillImport}\n" +
                $"Needs review: {summary.NeedsReview}\n" +
                $"Module-owned/excluded: {summary.ModuleOwnedOrExcluded}\n" +
                $"Cannot currently be represented: {summary.Unsupported}\n" +
                $"Duplicate-view rows ignored: {summary.DuplicateRowsIgnored}",
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        });

        _reportDetails.Children.Add(SectionHeading("Detected sections"));
        foreach (var section in summary.Sections)
        {
            var fields = section.FieldMappings.Count == 0
                ? "No fields were detected."
                : string.Join("\n", section.FieldMappings.Select(field =>
                    $"• {field.Name}: {field.Outcome}"));
            _reportDetails.Children.Add(ExpandableCard(
                $"{section.Name}: {section.RowCount} — {section.Outcome}",
                fields));
        }

        _reportDetails.Children.Add(SectionHeading("Relationship risks"));
        if (summary.RelationshipRisks.Count == 0)
        {
            _reportDetails.Children.Add(new Label
            {
                Text = "No relationship columns were detected.",
                TextColor = Colors.DarkSlateGray
            });
        }
        foreach (var risk in summary.RelationshipRisks)
        {
            _reportDetails.Children.Add(ExpandableCard(
                $"{risk.Source}.{risk.Field}: {risk.PreservationStatus}",
                $"Relationships: {risk.RelationshipCount}\n{risk.Message}"));
        }

        var ambiguous = summary.Sections
            .Where(section => section.Status == NotionAuditStatus.Ambiguous)
            .ToArray();
        _reportDetails.Children.Add(SectionHeading("Ambiguous mappings and diagnostics"));
        if (ambiguous.Length == 0 && summary.Diagnostics.Count == 0)
        {
            _reportDetails.Children.Add(new Label
            {
                Text = "No ambiguous mappings were detected.",
                TextColor = Colors.DarkSlateGray
            });
        }
        foreach (var section in ambiguous)
        {
            _reportDetails.Children.Add(ExpandableCard(
                $"{section.Name}: needs review",
                $"Rows: {section.RowCount}\n{section.Outcome}"));
        }
        foreach (var diagnostic in summary.Diagnostics)
        {
            _reportDetails.Children.Add(ExpandableCard("Review diagnostic", diagnostic));
        }
    }

    private static Label SectionHeading(string text) => new()
    {
        Text = text,
        FontSize = 18,
        FontAttributes = FontAttributes.Bold,
        TextColor = Colors.Black
    };

    private static Border ExpandableCard(string title, string details)
    {
        var detailLabel = new Label
        {
            Text = details,
            IsVisible = false,
            TextColor = Colors.DarkSlateGray,
            Margin = new Thickness(8, 0, 8, 8)
        };
        var toggle = new Button
        {
            Text = $"Show details: {title}",
            HorizontalOptions = LayoutOptions.Fill
        };
        toggle.Clicked += (_, _) =>
        {
            detailLabel.IsVisible = !detailLabel.IsVisible;
            toggle.Text = $"{(detailLabel.IsVisible ? "Hide" : "Show")} details: {title}";
        };
        return new Border
        {
            Stroke = Colors.LightGray,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = new VerticalStackLayout { Children = { toggle, detailLabel } }
        };
    }

    private async Task ExportReportAsync()
    {
        if (_viewModel.Report is not { } report)
        {
            return;
        }

        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, "notion-parity-audit-redacted.json");
        await File.WriteAllTextAsync(path, report.MachineReadableSummary);
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Export redacted Notion parity report",
            File = new ShareFile(path)
        });
    }

    private static Border Card(string title, params View[] children)
    {
        var content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                }
            }
        };
        foreach (var child in children)
        {
            content.Children.Add(child);
        }

        return new Border
        {
            Stroke = Colors.LightGray,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = 14,
            Content = content
        };
    }
}
