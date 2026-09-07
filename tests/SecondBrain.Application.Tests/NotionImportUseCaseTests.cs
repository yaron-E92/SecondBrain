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
    public void Plan_preserves_declared_relation_kind_and_defaults_related_to_contextual()
    {
        var areaId = "20000000000000000000000000000011";
        var sourceId = "20000000000000000000000000000012";
        var derivedTargetId = "20000000000000000000000000000013";
        var relatedTargetId = "20000000000000000000000000000014";
        var sourceRelations = new NotionExportRelation[]
        {
            new("derivedRelationNotionIds", [derivedTargetId]) { DeclaredType = "Derived" },
            new("relatedNotionIds", [relatedTargetId]),
        };
        var export = new NotionExportMetadata(
        [
            Table("Areas", "10000000000000000000000000000011",
                [Row(areaId, new Dictionary<string, string> { ["name"] = "Synthetic area" })]),
            Table("Notes", "10000000000000000000000000000012",
            [
                BrainItemRow(sourceId, areaId, "Source", sourceRelations),
                BrainItemRow(derivedTargetId, areaId, "Derived target"),
                BrainItemRow(relatedTargetId, areaId, "Related target"),
            ]),
        ], []);
        var useCase = new NotionImportUseCase(new UnusedReader(), new RecordingExecutor());

        var plan = useCase.Plan(export);
        var source = plan.Records.Single(record => record.PageNotionId == sourceId);

        Assert.Multiple(() =>
        {
            Assert.That(plan.RequiresReview, Is.False);
            Assert.That(source.Relations.Single(relation => relation.FieldName == "derivedRelationNotionIds").Kind,
                Is.EqualTo(NotionImportRelationKind.Derived));
            Assert.That(source.Relations.Single(relation => relation.FieldName == "relatedNotionIds").Kind,
                Is.EqualTo(NotionImportRelationKind.Contextual));
        });
    }

    [Test]
    public void Plan_blocks_relation_type_that_mapping_cannot_preserve()
    {
        var areaId = "20000000000000000000000000000021";
        var sourceId = "20000000000000000000000000000022";
        var targetId = "20000000000000000000000000000023";
        var export = new NotionExportMetadata(
        [
            Table("Areas", "10000000000000000000000000000021",
                [Row(areaId, new Dictionary<string, string> { ["name"] = "Synthetic area" })]),
            Table("Notes", "10000000000000000000000000000022",
            [
                BrainItemRow(sourceId, areaId, "Source",
                    [new NotionExportRelation("causalRelationNotionIds", [targetId]) { DeclaredType = "Causal" }]),
                BrainItemRow(targetId, areaId, "Target"),
            ]),
        ], []);
        var useCase = new NotionImportUseCase(new UnusedReader(), new RecordingExecutor());

        var plan = useCase.Plan(export);

        Assert.Multiple(() =>
        {
            Assert.That(plan.RequiresReview, Is.True);
            Assert.That(plan.Records.Select(record => record.PageNotionId), Does.Not.Contain(sourceId));
            Assert.That(plan.Deferred, Has.Some.Property("Code").EqualTo("unsupported-relation-type"));
        });
    }

    [Test]
    public void Plan_reports_known_relation_that_current_core_model_cannot_represent()
    {
        var projectId = "20000000000000000000000000000031";
        var areaId = "20000000000000000000000000000032";
        var export = new NotionExportMetadata(
        [
            Table("Projects", "10000000000000000000000000000031",
                [Row(projectId, new Dictionary<string, string> { ["name"] = "Synthetic project" })]),
            Table("Areas", "10000000000000000000000000000032",
                [Row(areaId, new Dictionary<string, string> { ["name"] = "Synthetic area" },
                    [new NotionExportRelation("projectNotionIds", [projectId])])]),
        ], []);
        var useCase = new NotionImportUseCase(new UnusedReader(), new RecordingExecutor());

        var plan = useCase.Plan(export);

        Assert.That(plan.UnresolvedLinks, Has.Some.Matches<NotionImportDiagnostic>(diagnostic =>
            diagnostic.Code == "relation-not-representable" &&
            diagnostic.SourceNotionId == areaId &&
            diagnostic.TargetNotionId == projectId));
    }

    [Test]
    public void Plan_classifies_shuffle_task_exclusion_case_insensitively()
    {
        var export = new NotionExportMetadata(
        [
            Table("tasks", "10000000000000000000000000000041",
                [Row("30000000000000000000000000000041", new Dictionary<string, string> { ["name"] = "Synthetic task" })]),
        ], []);
        var useCase = new NotionImportUseCase(new UnusedReader(), new RecordingExecutor());

        var plan = useCase.Plan(export);

        Assert.That(plan.Skips, Has.Some.Property("Code").EqualTo("module-owned-shuffletask"));
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

    private static NotionExportRowMetadata BrainItemRow(
        string id,
        string areaId,
        string name,
        IReadOnlyList<NotionExportRelation>? relations = null) =>
        Row(id, new Dictionary<string, string>
        {
            ["name"] = name,
            ["content"] = $"Synthetic content for {name}.",
            ["primaryType"] = "Area",
            ["primaryNotionId"] = areaId,
        }, relations);

    private static NotionExportRowMetadata Row(
        string id,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<NotionExportRelation>? relations = null) =>
        new(id, null, false, false, relations ?? [])
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
