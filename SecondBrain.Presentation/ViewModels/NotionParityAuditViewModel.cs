using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Application.NotionAudit;
using System.Text.Json;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class NotionParityAuditViewModel(
    NotionParityAuditUseCase auditUseCase,
    INotionExportSourcePicker sourcePicker,
    NotionImportUseCase? importUseCase = null) : ObservableObject
{
    private CancellationTokenSource? _scanCancellation;
    private readonly Dictionary<string, NotionResourceResolution> _resourceResolutions =
        new(StringComparer.OrdinalIgnoreCase);
    private string? _sourcePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReport))]
    public partial NotionAuditReport? Report { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirmImport))]
    public partial NotionImportPlan? ImportPlan { get; set; }

    [ObservableProperty]
    public partial NotionImportResult? ImportResult { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirmImport))]
    public partial bool IsImporting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } =
        "Choose a local Notion export. Nothing will be imported or saved.";

    public bool HasReport => Report is not null;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanCancel => IsScanning;

    public bool CanConfirmImport => importUseCase is not null && ImportPlan is { RequiresReview: false } && !IsImporting;

    public Task SelectFolderAsync() => SelectAndScanAsync(sourcePicker.PickFolderAsync);

    public Task SelectArchiveAsync() => SelectAndScanAsync(sourcePicker.PickArchiveAsync);

    public async Task ScanAsync(string sourcePath)
    {
        _scanCancellation?.Cancel();
        var scanCancellation = new CancellationTokenSource();
        _scanCancellation = scanCancellation;
        var cancellationToken = scanCancellation.Token;
        IsScanning = true;
        ErrorMessage = null;
        StatusMessage = "Scanning locally… No application data is being changed.";
        var sourceChanged = !string.Equals(_sourcePath, sourcePath, StringComparison.Ordinal);
        IReadOnlyDictionary<string, NotionResourceResolution> resolutions = sourceChanged
            ? new Dictionary<string, NotionResourceResolution>(StringComparer.OrdinalIgnoreCase)
            : _resourceResolutions;
        try
        {
            var report = await auditUseCase.AuditAsync(sourcePath, cancellationToken);
            var importPlan = importUseCase is null
                ? null
                : await importUseCase.PreviewAsync(sourcePath, resolutions, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (ReferenceEquals(_scanCancellation, scanCancellation))
            {
                if (sourceChanged)
                {
                    _resourceResolutions.Clear();
                }
                _sourcePath = sourcePath;
                Report = report;
                ImportPlan = importPlan;
                ImportResult = null;
                StatusMessage = importPlan is null
                    ? "Audit complete. Review every warning before importing."
                    : importPlan.RequiresReview
                        ? "Dry run complete. Resolve every blocking decision before importing."
                        : "Dry run complete. Review the report, then confirm once to import.";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (ReferenceEquals(_scanCancellation, scanCancellation))
            {
                StatusMessage = Report is null
                    ? "Scan canceled. No application data was changed."
                    : "Replacement scan canceled. The previous report is still available.";
            }
        }
        catch (UnauthorizedAccessException)
        {
            if (ReferenceEquals(_scanCancellation, scanCancellation))
            {
                ErrorMessage = "SecondBrain cannot read that export. Grant file access or choose another source, then retry.";
                StatusMessage = "Audit failed safely. No application data was changed.";
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or NotSupportedException)
        {
            if (ReferenceEquals(_scanCancellation, scanCancellation))
            {
                ErrorMessage = $"That export could not be audited. {exception.Message} Choose a supported folder, ZIP, JSON manifest, or CSV and retry.";
                StatusMessage = "Audit failed safely. No application data was changed.";
            }
        }
        finally
        {
            if (ReferenceEquals(_scanCancellation, scanCancellation))
            {
                IsScanning = false;
                _scanCancellation = null;
            }

            scanCancellation.Dispose();
        }
    }

    public void Cancel() => _scanCancellation?.Cancel();

    public async Task ResolveResourceAsync(string notionId, NotionResourceResolution resolution)
    {
        if (importUseCase is null || string.IsNullOrWhiteSpace(_sourcePath))
        {
            return;
        }

        _resourceResolutions[notionId] = resolution;
        ImportPlan = await importUseCase.PreviewAsync(_sourcePath, _resourceResolutions);
        StatusMessage = ImportPlan.RequiresReview
            ? "Decision saved. Resolve the remaining blocking items."
            : "Review complete. Confirm once to import, or leave this page to cancel.";
    }

    public async Task ConfirmImportAsync()
    {
        if (importUseCase is null || ImportPlan is not { RequiresReview: false } plan || IsImporting)
        {
            return;
        }

        IsImporting = true;
        ErrorMessage = null;
        StatusMessage = "Importing in one local transaction…";
        try
        {
            ImportResult = await importUseCase.ConfirmAsync(plan);
            if (ImportResult.RolledBack)
            {
                ErrorMessage = ImportResult.Diagnostics.LastOrDefault()?.Message ??
                    "The import was rolled back. Correct the source and retry.";
            }
            StatusMessage = ImportResult.RolledBack
                ? "Import failed and was rolled back. Review the report and retry."
                : $"Import complete: {ImportResult.Created} created, {ImportResult.Updated} updated, {ImportResult.Skipped} skipped, {ImportResult.Conflicted} conflicted.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
        {
            ErrorMessage = $"Import could not complete. No partial data was kept. {exception.Message}";
            StatusMessage = "Import failed safely. Correct the source or review choices and retry.";
        }
        finally
        {
            IsImporting = false;
        }
    }

    private async Task SelectAndScanAsync(
        Func<CancellationToken, Task<string?>> selectSource)
    {
        ErrorMessage = null;
        try
        {
            var sourcePath = await selectSource(CancellationToken.None);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                StatusMessage = Report is null
                    ? "Selection canceled. Nothing was scanned or saved."
                    : "Selection canceled. The previous report is still available.";
                return;
            }

            await ScanAsync(sourcePath);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Report is null
                ? "Selection canceled. Nothing was scanned or saved."
                : "Selection canceled. The previous report is still available.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "SecondBrain cannot open that location. Grant file access or choose another source, then retry.";
            StatusMessage = "Selection failed safely. No application data was changed.";
        }
        catch (Exception)
        {
            ErrorMessage = "SecondBrain could not open the selected export. Choose another folder or archive, then retry.";
            StatusMessage = "Selection failed safely. No application data was changed.";
        }
    }
}
