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
        var viewModel = new NotionParityAuditViewModel(new NotionParityAuditUseCase(reader));

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

    private sealed class ControlledReader : INotionExportReader
    {
        private readonly Dictionary<string, TaskCompletionSource<NotionExportMetadata>> _reads = [];

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
    }
}
