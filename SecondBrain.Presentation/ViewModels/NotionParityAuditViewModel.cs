using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Application.NotionAudit;
using System.Text.Json;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class NotionParityAuditViewModel(
    NotionParityAuditUseCase auditUseCase,
    INotionExportSourcePicker sourcePicker) : ObservableObject
{
    private CancellationTokenSource? _scanCancellation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReport))]
    public partial NotionAuditReport? Report { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } =
        "Choose a local Notion export. Nothing will be imported or saved.";

    public bool HasReport => Report is not null;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanCancel => IsScanning;

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
        try
        {
            var report = await auditUseCase.AuditAsync(sourcePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (ReferenceEquals(_scanCancellation, scanCancellation))
            {
                Report = report;
                StatusMessage = "Audit complete. Review every warning before importing.";
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
