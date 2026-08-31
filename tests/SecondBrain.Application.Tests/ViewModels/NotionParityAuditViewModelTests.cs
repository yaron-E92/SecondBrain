using NUnit.Framework;
using SecondBrain.Application.NotionAudit;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Application.Tests.ViewModels;

[TestFixture]
public sealed class NotionParityAuditViewModelTests
{
    [Test]
    public async Task Starting_replacement_scan_ignores_stale_completion_from_canceled_scan()
    {
        var reader = new ControlledReader();
        var viewModel = CreateViewModel(reader);

        var firstScan = viewModel.ScanAsync("first");
        var replacementScan = viewModel.ScanAsync("replacement");
        reader.Complete("first");
        await firstScan;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsScanning, Is.True);
            Assert.That(viewModel.CanCancel, Is.True);
            Assert.That(viewModel.StatusMessage, Does.StartWith("Scanning locally"));
            Assert.That(viewModel.Report, Is.Null);
        });

        reader.Complete("replacement");
        await replacementScan;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsScanning, Is.False);
            Assert.That(viewModel.Report, Is.Not.Null);
            Assert.That(viewModel.StatusMessage, Does.StartWith("Audit complete"));
        });
    }

    [Test]
    public async Task Canceling_replacement_scan_preserves_previous_report()
    {
        var reader = new ControlledReader();
        var viewModel = CreateViewModel(reader);

        var initialScan = viewModel.ScanAsync("initial");
        reader.Complete("initial");
        await initialScan;
        var initialReport = viewModel.Report;

        var replacementScan = viewModel.ScanAsync("replacement");
        viewModel.Cancel();
        reader.Complete("replacement");
        await replacementScan;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsScanning, Is.False);
            Assert.That(viewModel.Report, Is.SameAs(initialReport));
            Assert.That(viewModel.StatusMessage, Does.StartWith("Replacement scan canceled"));
            Assert.That(viewModel.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public async Task Safe_failure_keeps_previous_report_and_retry_replaces_it()
    {
        var reader = new ControlledReader();
        var viewModel = CreateViewModel(reader);

        var initialScan = viewModel.ScanAsync("initial");
        reader.Complete("initial");
        await initialScan;
        var initialReport = viewModel.Report;

        var failedScan = viewModel.ScanAsync("malformed");
        reader.Fail("malformed", new InvalidDataException("Synthetic malformed export."));
        await failedScan;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Report, Is.SameAs(initialReport));
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Does.Contain("retry"));
            Assert.That(viewModel.StatusMessage, Does.StartWith("Audit failed safely"));
        });

        var retry = viewModel.ScanAsync("retry");
        reader.Complete("retry");
        await retry;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Report, Is.Not.Null);
            Assert.That(viewModel.Report, Is.Not.SameAs(initialReport));
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(viewModel.StatusMessage, Does.StartWith("Audit complete"));
        });
    }

    [Test]
    public async Task Folder_picker_selection_scans_the_selected_source()
    {
        var reader = new ControlledReader();
        var picker = new ControlledPicker { FolderPath = "selected-folder" };
        var viewModel = CreateViewModel(reader, picker);

        var selection = viewModel.SelectFolderAsync();
        reader.Complete("selected-folder");
        await selection;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Report, Is.Not.Null);
            Assert.That(viewModel.StatusMessage, Does.StartWith("Audit complete"));
            Assert.That(picker.FolderSelections, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Canceled_archive_picker_preserves_the_previous_report()
    {
        var reader = new ControlledReader();
        var picker = new ControlledPicker();
        var viewModel = CreateViewModel(reader, picker);

        var initialScan = viewModel.ScanAsync("initial");
        reader.Complete("initial");
        await initialScan;
        var initialReport = viewModel.Report;

        await viewModel.SelectArchiveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Report, Is.SameAs(initialReport));
            Assert.That(viewModel.StatusMessage, Does.StartWith("Selection canceled"));
            Assert.That(picker.ArchiveSelections, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Picker_permission_failure_is_actionable_and_does_not_scan()
    {
        var reader = new ControlledReader();
        var picker = new ControlledPicker
        {
            Failure = new UnauthorizedAccessException("Synthetic denied path.")
        };
        var viewModel = CreateViewModel(reader, picker);

        await viewModel.SelectFolderAsync();

        Assert.Multiple(() =>
        {
            Assert.That(reader.RequestedSources, Is.Empty);
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Does.Contain("Grant file access"));
            Assert.That(viewModel.StatusMessage, Does.StartWith("Selection failed safely"));
        });
    }

    private static NotionParityAuditViewModel CreateViewModel(
        ControlledReader reader,
        ControlledPicker? picker = null) =>
        new(new NotionParityAuditUseCase(reader), picker ?? new ControlledPicker());

    private sealed class ControlledReader : INotionExportReader
    {
        private readonly Dictionary<string, TaskCompletionSource<NotionExportMetadata>> _reads = [];

        public IReadOnlyCollection<string> RequestedSources => _reads.Keys;

        public Task<NotionExportMetadata> ReadAsync(
            string sourcePath,
            CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<NotionExportMetadata>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _reads.Add(sourcePath, completion);
            return completion.Task;
        }

        public void Complete(string sourcePath) =>
            _reads[sourcePath].SetResult(new NotionExportMetadata([], []));

        public void Fail(string sourcePath, Exception exception) =>
            _reads[sourcePath].SetException(exception);
    }

    private sealed class ControlledPicker : INotionExportSourcePicker
    {
        public string? FolderPath { get; init; }

        public string? ArchivePath { get; init; }

        public Exception? Failure { get; init; }

        public int FolderSelections { get; private set; }

        public int ArchiveSelections { get; private set; }

        public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        {
            FolderSelections++;
            return SelectAsync(FolderPath);
        }

        public Task<string?> PickArchiveAsync(CancellationToken cancellationToken = default)
        {
            ArchiveSelections++;
            return SelectAsync(ArchivePath);
        }

        private Task<string?> SelectAsync(string? path) => Failure is null
            ? Task.FromResult(path)
            : Task.FromException<string?>(Failure);
    }
}
