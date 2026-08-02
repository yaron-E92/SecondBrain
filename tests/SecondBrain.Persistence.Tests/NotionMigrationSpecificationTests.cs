using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SecondBrain.Persistence.Tests;

[TestFixture]
public sealed partial class NotionMigrationSpecificationTests
{
    private static readonly string[] ExpectedSourceDatabases =
    [
        "Projects",
        "Areas",
        "Notes",
        "Ideas",
        "Journals",
        "Captures",
        "Resources",
    ];

    private static readonly string[] ExpectedTargets =
    [
        "Project",
        "Area",
        "Note",
        "Idea",
        "JournalEntry",
        "KnowledgeCapture",
        "ResourceTopic",
        "ResourceArtifact",
    ];

    [Test]
    public void Version_one_specification_maps_every_core_database_and_policy()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mappingPath = Path.Combine(
            repositoryRoot,
            "docs",
            "migration",
            "notion-export",
            "v1",
            "mapping.md");
        var mapping = File.ReadAllText(mappingPath);

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Does.Contain("Specification version: `1.0`"));

            foreach (var database in ExpectedSourceDatabases)
            {
                Assert.That(mapping, Does.Contain($"| {database}"));
            }

            Assert.That(mapping, Does.Contain("classified `Topic`"));
            Assert.That(mapping, Does.Contain("classified `Artifact`"));
            Assert.That(mapping, Does.Contain("classified `Note`"));
            Assert.That(mapping, Does.Contain("_all.csv"));
            Assert.That(mapping, Does.Contain("unresolved-link diagnostic"));
            Assert.That(mapping, Does.Contain("module-owned-shuffletask"));
            Assert.That(mapping, Does.Contain("module-owned-phoodab"));
            Assert.That(mapping, Does.Contain("Notion page ID"));
        });
    }

    [Test]
    public void Sanitized_fixtures_cover_representative_export_decisions()
    {
        var fixtureDirectory = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "fixtures",
            "notion-export",
            "v1");
        var sourceJson = File.ReadAllText(
            Path.Combine(fixtureDirectory, "representative-export.json"));
        var expectedJson = File.ReadAllText(
            Path.Combine(fixtureDirectory, "expected-migration.json"));

        using var source = JsonDocument.Parse(sourceJson);
        using var expected = JsonDocument.Parse(expectedJson);

        var sourceRoot = source.RootElement;
        var files = sourceRoot.GetProperty("files").EnumerateArray().ToArray();
        var canonicalDatabases = files
            .Where(file => !file.GetProperty("fileName").GetString()!
                .EndsWith("_all.csv", StringComparison.OrdinalIgnoreCase))
            .Select(file => file.GetProperty("database").GetString())
            .ToHashSet(StringComparer.Ordinal);
        var targets = expected.RootElement
            .GetProperty("imports")
            .EnumerateArray()
            .Select(item => item.GetProperty("target").GetString())
            .ToHashSet(StringComparer.Ordinal);
        var skipReasons = expected.RootElement
            .GetProperty("skips")
            .EnumerateArray()
            .Select(item => item.GetProperty("reason").GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(sourceRoot.GetProperty("fixtureVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(sourceRoot.GetProperty("synthetic").GetBoolean(), Is.True);
            Assert.That(expected.RootElement
                .GetProperty("specificationVersion")
                .GetString(), Is.EqualTo("1.0"));
            Assert.That(canonicalDatabases, Is.SupersetOf(ExpectedSourceDatabases));
            Assert.That(targets, Is.SupersetOf(ExpectedTargets));
            Assert.That(skipReasons, Does.Contain("duplicate-all-view"));
            Assert.That(skipReasons, Does.Contain("module-owned-shuffletask"));
            Assert.That(skipReasons, Does.Contain("module-owned-phoodab"));
            Assert.That(expected.RootElement
                .GetProperty("deferred")[0]
                .GetProperty("reason")
                .GetString(), Is.EqualTo("ambiguous-resource-classification-required"));
            Assert.That(expected.RootElement
                .GetProperty("unresolvedLinks")[0]
                .GetProperty("policy")
                .GetString(), Is.EqualTo("report-only-no-placeholder"));
            Assert.That(sourceJson, Does.Not.Contain("@"));
            Assert.That(sourceJson, Does.Not.Contain(@"C:\"));
            Assert.That(sourceJson, Does.Not.Contain("/Users/"));
        });

        AssertNotionIdsAreStable(sourceRoot);
    }

    private static void AssertNotionIdsAreStable(JsonElement sourceRoot)
    {
        var ids = sourceRoot
            .GetProperty("files")
            .EnumerateArray()
            .SelectMany(file =>
                file.GetProperty("rows")
                    .EnumerateArray()
                    .Select(row => row.GetProperty("notionId").GetString()))
            .Where(id => id is not null)
            .Cast<string>()
            .ToArray();

        Assert.That(ids, Is.Not.Empty);
        Assert.That(ids, Has.All.Matches<string>(id => NotionIdPattern().IsMatch(id)));
    }

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

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing SecondBrain.slnx.");
    }

    [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex NotionIdPattern();
}
