using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using SecondBrain.Application.NotionAudit;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class DataImportPage : ContentPage
{
    private readonly NotionParityAuditViewModel _viewModel;
    private readonly VerticalStackLayout _reportDetails = new() { Spacing = 12 };
    private readonly VerticalStackLayout _importReview = new() { Spacing = 8 };

    public DataImportPage(NotionParityAuditViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Data / Import";
        BackgroundColor = Colors.White;

        var selectFolder = new Button
        {
            Text = "Choose export folder",
            MinimumHeightRequest = 44,
            HorizontalOptions = LayoutOptions.Start,
            AutomationId = "NotionAuditSelectFolder"
        };
        selectFolder.Clicked += async (_, _) => await _viewModel.SelectFolderAsync();

        var selectArchive = new Button
        {
            Text = "Choose archive or manifest",
            MinimumHeightRequest = 44,
            HorizontalOptions = LayoutOptions.Start,
            AutomationId = "NotionAuditSelectArchive"
        };
        selectArchive.Clicked += async (_, _) => await _viewModel.SelectArchiveAsync();

        var cancel = new Button
        {
            Text = "Cancel scan",
            MinimumHeightRequest = 44,
            AutomationId = "NotionAuditCancel"
        };
        cancel.SetBinding(IsVisibleProperty, nameof(viewModel.CanCancel));
        cancel.Clicked += (_, _) => viewModel.Cancel();

        var export = new Button
        {
            Text = "Export redacted report",
            MinimumHeightRequest = 44,
            HorizontalOptions = LayoutOptions.Start,
            AutomationId = "NotionAuditExportReport"
        };
        export.SetBinding(IsVisibleProperty, nameof(viewModel.HasReport));
        export.Clicked += async (_, _) => await ExportReportAsync();

        var confirm = new Button
        {
            Text = "Confirm import",
            MinimumHeightRequest = 44,
            AutomationId = "NotionImportConfirm"
        };
        confirm.SetBinding(IsEnabledProperty, nameof(viewModel.CanConfirmImport));
        confirm.SetBinding(IsVisibleProperty, nameof(viewModel.HasReport));
        confirm.Clicked += async (_, _) => await viewModel.ConfirmImportAsync();

        var progress = new ActivityIndicator { Color = Colors.DarkSlateBlue };
        progress.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsScanning));
        progress.SetBinding(IsVisibleProperty, nameof(viewModel.IsScanning));

        var importProgress = new ActivityIndicator { Color = Colors.DarkSlateBlue };
        importProgress.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsImporting));
        importProgress.SetBinding(IsVisibleProperty, nameof(viewModel.IsImporting));

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
            else if (args.PropertyName == nameof(viewModel.ImportPlan))
            {
                BuildImportReview(viewModel.ImportPlan);
            }
            else if (args.PropertyName == nameof(viewModel.ImportResult))
            {
                BuildImportCompletion(viewModel.ImportResult);
            }
        };
        BuildReport(viewModel.Report);
        BuildImportReview(viewModel.ImportPlan);

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
                    new FlexLayout
                    {
                        Direction = FlexDirection.Row,
                        Wrap = FlexWrap.Wrap,
                        JustifyContent = FlexJustify.Start,
                        AlignItems = FlexAlignItems.Start,
                        Children = { selectFolder, selectArchive, cancel }
                    },
                    progress,
                    importProgress,
                    status,
                    error,
                    report,
                    Card("Import review", _importReview),
                    confirm,
                    export
                }
            }
        };
    }

    private void BuildImportReview(NotionImportPlan? plan)
    {
        _importReview.Children.Clear();
        if (plan is null)
        {
            _importReview.Children.Add(new Label { Text = "Choose a source to create a dry-run import plan." });
            return;
        }

        _importReview.Children.Add(new Label
        {
            Text = $"Eligible: {plan.Records.Count} · Skipped: {plan.Skips.Count} · Blocking: {plan.Deferred.Count} · Unresolved links: {plan.UnresolvedLinks.Count}",
            TextColor = Colors.Black
        });
        foreach (var decision in plan.Deferred.Where(item =>
                     item.Code == "ambiguous-resource-classification-required" && item.SourceNotionId is not null))
        {
            var choices = new FlexLayout
            {
                Direction = FlexDirection.Row,
                Wrap = FlexWrap.Wrap,
                JustifyContent = FlexJustify.Start
            };
            foreach (var choice in Enum.GetValues<NotionResourceResolution>())
            {
                var button = new Button
                {
                    Text = choice.ToString(),
                    MinimumHeightRequest = 44,
                    MinimumWidthRequest = 72,
                    Margin = new Thickness(0, 0, 8, 8)
                };
                button.Clicked += async (_, _) => await _viewModel.ResolveResourceAsync(decision.SourceNotionId!, choice);
                choices.Children.Add(button);
            }
            _importReview.Children.Add(new Label { Text = "Ambiguous Resource requires a destination:" });
            _importReview.Children.Add(choices);
        }
    }

    private void BuildImportCompletion(NotionImportResult? result)
    {
        if (result is null)
        {
            return;
        }

        if (result.RolledBack)
        {
            _importReview.Children.Add(SectionHeading("Import rolled back"));
            foreach (var diagnostic in result.Diagnostics)
            {
                _importReview.Children.Add(ExpandableCard("Rollback diagnostic", diagnostic.Message));
            }

            return;
        }

        if (result.BlockedByConflicts)
        {
            _importReview.Children.Add(SectionHeading("Import blocked by changed source"));
            foreach (var diagnostic in result.Diagnostics.Where(item =>
                         item.Code is "changed-source-conflict" or "import-blocked-by-conflicts"))
            {
                _importReview.Children.Add(ExpandableCard("Conflict diagnostic", diagnostic.Message));
            }

            return;
        }

        _importReview.Children.Add(SectionHeading("Imported results"));
        foreach (var target in result.Targets.Take(8))
        {
            var open = new Button
            {
                Text = $"Open {target.Title}",
                MinimumHeightRequest = 44
            };
            open.Clicked += async (_, _) => await OpenImportedTargetAsync(target);
            _importReview.Children.Add(open);
        }
    }

    private static Task OpenImportedTargetAsync(NotionImportedTarget target)
    {
        if (target.Kind is NotionImportTarget.Project or NotionImportTarget.Area or NotionImportTarget.ResourceTopic)
        {
            var kind = target.Kind == NotionImportTarget.ResourceTopic ? "ResourceTopic" : target.Kind.ToString();
            return Shell.Current.GoToAsync("//para", new Dictionary<string, object>
            {
                ["contextKind"] = kind,
                ["contextId"] = target.TargetId.ToString(),
                ["returnRoute"] = "data-import",
            });
        }

        return Shell.Current.GoToAsync("//editor", new Dictionary<string, object>
        {
            ["itemId"] = target.TargetId.ToString(),
            ["returnRoute"] = "data-import",
        });
    }

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
            MinimumHeightRequest = 44,
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
