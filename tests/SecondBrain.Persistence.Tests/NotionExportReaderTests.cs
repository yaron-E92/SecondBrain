using NUnit.Framework;
using SecondBrain.Persistence;

using System.IO.Compression;
using SecondBrain.Application.NotionAudit;

namespace SecondBrain.Persistence.Tests;

[TestFixture]
public sealed class NotionExportReaderTests
{
    [Test]
    public async Task Reader_extracts_only_metadata_from_synthetic_manifest()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "tests", "fixtures", "notion-export", "v1", "representative-export.json");

        var export = await new NotionExportReader().ReadAsync(path);

        Assert.Multiple(() =>
        {
            Assert.That(export.Tables, Has.Count.EqualTo(13));
            Assert.That(export.Tables.Select(table => table.DatabaseName),
                Does.Contain("Global Tags").And.Contain("Archive"));
            Assert.That(export.Tables.Single(table => table.DatabaseName == "Notes" &&
                table.IsDuplicateAllView).Rows, Has.Count.EqualTo(1));
            Assert.That(export.Tables.Single(table => table.DatabaseName == "Captures")
                .Rows.Single().Relations, Has.Count.EqualTo(3));
            Assert.That(export.ToString(), Does.Not.Contain("Synthetic field notes"));
            Assert.That(export.ToString(), Does.Not.Contain("example.invalid"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Reader_classifies_native_csv_exports_and_relationship_columns(bool archive)
    {
        var testPath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"notion-export-{Guid.NewGuid():N}");
        var exportPath = Path.Combine(testPath, "export");
        var archivePath = Path.Combine(testPath, "export.zip");
        Directory.CreateDirectory(exportPath);
        try
        {
            var projectPageId = "20000000000000000000000000000001";
            var areaPageId = "20000000000000000000000000000002";
            var resourcePageId = "20000000000000000000000000000003";
            await WriteCsvAsync(exportPath, "Projects", 1,
                $"Name,Notion ID,Area\nSynthetic project,{projectPageId},{areaPageId}\n");
            await WriteCsvAsync(exportPath, "Areas", 2,
                $"Name,Notion ID,Projects\nSynthetic area,{areaPageId},{projectPageId}\n");
            await WriteCsvAsync(exportPath, "Resources", 3,
                $"Name,Notion ID,Project\nSynthetic resource,{resourcePageId},{projectPageId}\n");
            await WriteCsvAsync(exportPath, "Global Tags", 4, "Name\nSynthetic tag\n");
            await WriteCsvAsync(exportPath, "Tasks", 5, "Name\nSynthetic task\n");
            await WriteCsvAsync(exportPath, "Chores", 6, "Name\nSynthetic chore\n");
            await WriteCsvAsync(exportPath, "PHOODAB", 7, "Name\nSynthetic pantry item\n");
            await WriteCsvAsync(exportPath, "Archive", 8, "Name\nSynthetic archive item\n");
            await WriteCsvAsync(exportPath, "Notes", 9, "Name\nSynthetic note\n");
            await WriteCsvAsync(exportPath, "Notes_all", 9, "Name\nSynthetic duplicate note\n");
            if (archive)
            {
                ZipFile.CreateFromDirectory(exportPath, archivePath);
            }

            var reader = new NotionExportReader();
            var export = await reader.ReadAsync(archive ? archivePath : exportPath);
            var report = new NotionParityAuditUseCase(reader).Analyze(export);

            Assert.Multiple(() =>
            {
                Assert.That(export.Diagnostics, Is.Empty);
                Assert.That(export.Tables.Select(table => table.DatabaseName),
                    Does.Contain("Projects").And.Contain("Areas").And.Contain("Resources")
                        .And.Contain("Global Tags").And.Contain("Tasks").And.Contain("Chores")
                        .And.Contain("PHOODAB").And.Contain("Archive").And.Contain("Notes"));
                Assert.That(report.Summary.Sections.Single(section => section.Name == "Projects").Status,
                    Is.EqualTo(NotionAuditStatus.CoreSupported));
                Assert.That(report.Summary.Sections.Single(section => section.Name == "Tasks").Status,
                    Is.EqualTo(NotionAuditStatus.ModuleOwnedExcluded));
                Assert.That(report.Summary.Sections.Single(section => section.Name == "PHOODAB").Status,
                    Is.EqualTo(NotionAuditStatus.ModuleOwnedExcluded));
                Assert.That(report.Summary.Sections.Single(section => section.Name == "Resources").Status,
                    Is.EqualTo(NotionAuditStatus.Ambiguous));
                Assert.That(report.Summary.Sections.Single(section => section.Name == "Notes" &&
                    section.Status == NotionAuditStatus.DuplicateView).RowCount, Is.Zero);
                Assert.That(report.Summary.RelationshipRisks.Select(risk => $"{risk.Source}.{risk.Field}"),
                    Is.EquivalentTo(new[] { "Projects.Area", "Areas.Projects", "Resources.Project" }));
                Assert.That(export.ToString(), Does.Not.Contain("Synthetic project"));
            });
        }
        finally
        {
            Directory.Delete(testPath, recursive: true);
        }
    }

    [Test]
    public async Task Reader_normalizes_hyphenated_ids_and_redacts_non_id_relationship_values()
    {
        const string manifest = """
            {
              "files": [
                {
                  "fileName": "Notes.csv",
                  "database": "Notes",
                  "databaseNotionId": "10000000-0000-0000-0000-000000000001",
                  "rows": [
                    {
                      "notionId": "20000000-0000-0000-0000-000000000001",
                      "tags": ["Private label"],
                      "primaryNotionId": "20000000-0000-0000-0000-000000000002"
                    }
                  ]
                }
              ]
            }
            """;
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"notion-export-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, manifest);

            var export = await new NotionExportReader().ReadAsync(path);
            var table = export.Tables.Single();
            var row = table.Rows.Single();

            Assert.Multiple(() =>
            {
                Assert.That(table.DatabaseNotionId, Is.EqualTo("10000000000000000000000000000001"));
                Assert.That(row.NotionId, Is.EqualTo("20000000000000000000000000000001"));
                Assert.That(row.Relations.Single(relation => relation.FieldName == "primaryNotionId")
                    .TargetNotionIds.Single(), Is.EqualTo("20000000000000000000000000000002"));
                Assert.That(row.Relations.Single(relation => relation.FieldName == "tags")
                    .TargetNotionIds.Single(), Is.EqualTo("unresolved-export-relation"));
                Assert.That(export.ToString(), Does.Not.Contain("Private label"));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Reader_rejects_unterminated_csv_fields()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"notion-export-{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(path, "Name,Notion ID\n\"unfinished,20000000000000000000000000000001");

            var exception = Assert.ThrowsAsync<InvalidDataException>(
                async () => await new NotionExportReader().ReadAsync(path));

            Assert.That(exception!.Message, Does.Contain("unterminated quoted field"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Task WriteCsvAsync(string exportPath, string databaseName, int databaseId, string content) =>
        File.WriteAllTextAsync(
            Path.Combine(exportPath, $"{databaseName} {databaseId:x32}.csv"),
            content);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SecondBrain.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
