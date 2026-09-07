using NUnit.Framework;
using SecondBrain.Application.NotionAudit;

namespace SecondBrain.Application.Tests;

[TestFixture]
public sealed class NotionImportUseCaseTests
{
    [Test]
    public void Preview_preflight_blocks_invalid_target_data_before_mutation()
    {
        var areaId = "20000000000000000000000000000001";
        var noteId = "20000000000000000000000000000002";
        var export = new NotionExportMetadata(
        [
            Table("Areas", "10000000000000000000000000000001",
                [Row(areaId, new Dictionary<string, string> { ["name"] = "Synthetic area" })]),
            Table("Notes", "10000000000000000000000000000002",
                [Row(noteId, new Dictionary<string, string>
                {
                    ["name"] = "Synthetic note",
                    ["content"] = "",
                    ["primaryType"] = "Area",
                    ["primaryNotionId"] = areaId,
                })]),
        ], []);
        var executor = new RecordingExecutor();
        var useCase = new NotionImportUseCase(new UnusedReader(), executor);

        var plan = useCase.Plan(export);

        Assert.Multiple(() =>
        {
            Assert.That(plan.RequiresReview, Is.True);
            Assert.That(plan.Records.Select(record => record.PageNotionId), Does.Not.Contain(noteId));
            Assert.That(plan.Deferred, Has.Some.Property("Code").EqualTo("invalid-target-data"));
            Assert.That(executor.Executions, Is.Zero);
        });
    }

    [Test]
    public void Confirm_rejects_a_blocked_plan_without_calling_persistence()
    {
        var executor = new RecordingExecutor();
        var useCase = new NotionImportUseCase(new UnusedReader(), executor);
        var plan = new NotionImportPlan("1.0", [], [],
            [new NotionImportDiagnostic("review-required", null, null, "Review required.")], []);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await useCase.ConfirmAsync(plan));
        Assert.That(executor.Executions, Is.Zero);
    }

    private static NotionExportTableMetadata Table(
        string database,
        string databaseId,
        IReadOnlyList<NotionExportRowMetadata> rows) =>
        new($"{database}.csv", database, databaseId, false, ["name"], rows);

    private static NotionExportRowMetadata Row(
        string id,
        IReadOnlyDictionary<string, string> values) =>
        new(id, null, false, false, [])
        {
            ContentFingerprint = id,
            Values = values,
        };

    private sealed class UnusedReader : INotionExportReader
    {
        public Task<NotionExportMetadata> ReadAsync(
            string sourcePath,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("This test uses the pure planner.");
    }

    private sealed class RecordingExecutor : INotionImportExecutor
    {
        public int Executions { get; private set; }

        public Task<NotionImportResult> ExecuteAsync(
            NotionImportPlan plan,
            CancellationToken cancellationToken = default)
        {
            Executions++;
            return Task.FromResult(new NotionImportResult(0, 0, 0, 0, []));
        }
    }
}
