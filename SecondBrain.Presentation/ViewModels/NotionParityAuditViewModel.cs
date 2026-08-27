using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Application.NotionAudit;
using System.Text.Json;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class NotionParityAuditViewModel(
    NotionParityAuditUseCase auditUseCase) : ObservableObject
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

    public async Task ScanAsync(string sourcePath)
    {
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        var cancellationToken = _scanCancellation.Token;
        IsScanning = true;
        ErrorMessage = null;
        StatusMessage = "Scanning locally… No application data is being changed.";
        try
        {
            var report = await auditUseCase.AuditAsync(sourcePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Report = report;
            StatusMessage = "Audit complete. Review every warning before importing.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = Report is null
                ? "Scan canceled. No application data was changed."
                : "Replacement scan canceled. The previous report is still available.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "SecondBrain cannot read that export. Grant file access or choose another source, then retry.";
            StatusMessage = "Audit failed safely. No application data was changed.";
        }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        {
            ErrorMessage = $"That export could not be audited. {exception.Message} Choose a supported folder, ZIP, JSON manifest, or CSV and retry.";
            StatusMessage = "Audit failed safely. No application data was changed.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void Cancel() => _scanCancellation?.Cancel();
}
