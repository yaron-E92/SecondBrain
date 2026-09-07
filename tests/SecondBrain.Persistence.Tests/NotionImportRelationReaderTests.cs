using System.Text.Json;
using NUnit.Framework;

namespace SecondBrain.Persistence.Tests;

[TestFixture]
public sealed class NotionImportRelationReaderTests
{
    [Test]
    public async Task Manifest_reader_preserves_declared_relation_type_and_audit_tag_metadata()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"notion-relations-{Guid.NewGuid():N}.json");
        try
        {
            var sourceId = "20000000000000000000000000000071";
            var targetId = "20000000000000000000000000000072";
            var manifest = new
            {
                fixtureVersion = 1,
                synthetic = true,
                files = new[]
                {
                    new
                    {
                        fileName = "Notes.csv",
                        database = "Notes",
                        databaseNotionId = "10000000000000000000000000000071",
                        rows = new object[]
                        {
                            new
                            {
                                notionId = sourceId,
                                name = "Synthetic source",
                                tags = new[] { "Example", "Migration" },
                                derivedRelationNotionIds = new
                                {
                                    relationType = "Derived",
                                    targetNotionIds = new[] { targetId },
                                },
                            },
                        },
                    },
                },
            };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest));

            var export = await new NotionExportReader().ReadAsync(path);
            var row = export.Tables.Single().Rows.Single();
            var relation = row.Relations.Single(item => item.FieldName == "derivedRelationNotionIds");

            Assert.Multiple(() =>
            {
                Assert.That(relation.DeclaredType, Is.EqualTo("Derived"));
                Assert.That(relation.TargetNotionIds, Is.EqualTo(new[] { targetId }));
                Assert.That(row.Relations.Select(item => item.FieldName), Does.Contain("tags"));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }
}
