using NUnit.Framework;
using SecondBrain.Application.NotionAudit;

namespace SecondBrain.Application.Tests;

[TestFixture]
public sealed class NotionParityAuditUseCaseTests
{
    [Test]
    public void Analyze_reports_supported_review_excluded_duplicate_and_relationship_risk()
    {
        var areaId = "20000000000000000000000000000001";
        var taskId = "30000000000000000000000000000001";
        var export = new NotionExportMetadata(
        [
            Table("Areas", "10000000000000000000000000000001",
                [Row(areaId)]),
            Table("Projects", "10000000000000000000000000000002",
                [Row("20000000000000000000000000000002", relations:
                    [new("Area relation", [areaId, taskId, "ffffffffffffffffffffffffffffffff"])])]),
            Table("Resources", "10000000000000000000000000000003",
                [Row("20000000000000000000000000000003", classification: "")]),
            Table("Tasks", "10000000000000000000000000000004", [Row(taskId)]),
            Table("Archive", "10000000000000000000000000000005",
                [Row("20000000000000000000000000000004")]),
            Table("Notes", "10000000000000000000000000000006",
                [Row("20000000000000000000000000000005")], duplicate: true),
        ], []);

        var report = new NotionParityAuditUseCase(new UnusedReader()).Analyze(export);

        Assert.Multiple(() =>
        {
            Assert.That(report.Summary.WillImport, Is.EqualTo(2));
            Assert.That(report.Summary.NeedsReview, Is.EqualTo(2));
            Assert.That(report.Summary.ModuleOwnedOrExcluded, Is.EqualTo(1));
            Assert.That(report.Summary.DuplicateRowsIgnored, Is.EqualTo(1));
            Assert.That(report.Summary.RelationshipRisks.Single().PreservationStatus,
                Is.EqualTo("needs review"));
            Assert.That(report.HumanReadableReport, Does.Contain("will be skipped"));
            Assert.That(report.HumanReadableReport, Does.Contain("No placeholder"));
            Assert.That(report.MachineReadableSummary, Does.Not.Contain(areaId));
        });
    }

    [Test]
    public void Analyze_ignores_templates_and_collapses_repeated_page_ids()
    {
        var repeated = "20000000000000000000000000000001";
        var export = new NotionExportMetadata(
        [
            Table("Notes", "10000000000000000000000000000001",
            [
                Row(repeated),
                Row(repeated),
                Row("20000000000000000000000000000002", template: true),
            ])
        ], []);

        var report = new NotionParityAuditUseCase(new UnusedReader()).Analyze(export);

        Assert.That(report.Summary.WillImport, Is.EqualTo(1));
    }

    [Test]
    public void Analyze_reports_every_common_relationship_field_for_review()
    {
        var targetId = "20000000000000000000000000000001";
        var relationshipFields = new[] { "Tags", "Links", "Placement", "Primary placement" };
        var export = new NotionExportMetadata(
        [
            Table("Areas", "10000000000000000000000000000001", [Row(targetId)]),
            new NotionExportTableMetadata(
                "source-Notes",
                "Notes",
                "10000000000000000000000000000002",
                false,
                relationshipFields,
                [Row("20000000000000000000000000000002", relations:
                    relationshipFields.Select(field => new NotionExportRelation(field, [targetId])).ToArray())]),
        ], []);

        var report = new NotionParityAuditUseCase(new UnusedReader()).Analyze(export);

        Assert.Multiple(() =>
        {
            Assert.That(report.Summary.RelationshipRisks.Select(risk => risk.Field),
                Is.EquivalentTo(relationshipFields));
            Assert.That(report.Summary.Sections.Single(section => section.Name == "Notes")
                .FieldMappings, Has.All.Property("Status").EqualTo(NotionAuditStatus.CoreSupportedWithReview));
        });
    }

    private static NotionExportTableMetadata Table(
        string name,
        string databaseId,
        IReadOnlyList<NotionExportRowMetadata> rows,
        bool duplicate = false) =>
        new($"source-{name}", name, databaseId, duplicate, ["Name"], rows);

    private static NotionExportRowMetadata Row(
        string id,
        string? classification = null,
        bool template = false,
        IReadOnlyList<NotionExportRelation>? relations = null) =>
        new(id, classification, template, false, relations ?? []);

    private sealed class UnusedReader : INotionExportReader
    {
        public Task<NotionExportMetadata> ReadAsync(
            string sourcePath,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The pure analyzer should not read files.");
    }
}
