using System.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using SecondBrain.Application.NotionAudit;

namespace SecondBrain.Persistence.Tests;

[TestFixture]
public sealed class NotionImportExecutorTests
{
    [Test]
    public async Task Synthetic_import_is_transactional_and_idempotent()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"notion-import-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<SecondBrainDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            await using var context = new SecondBrainDbContext(options);
            await context.Database.MigrateAsync();
            var reader = new NotionExportReader();
            var executor = new NotionImportExecutor(context);
            var useCase = new NotionImportUseCase(reader, executor);
            var sourcePath = Path.Combine(
                FindRepositoryRoot(), "tests", "fixtures", "notion-export", "v1", "representative-export.json");
            var ambiguousId = "2000000000000000000000000000000a";

            var blocked = await useCase.PreviewAsync(sourcePath);
            var plan = await useCase.PreviewAsync(sourcePath,
                new Dictionary<string, NotionResourceResolution> { [ambiguousId] = NotionResourceResolution.Exclude });
            var first = await useCase.ConfirmAsync(plan);
            var repeated = await useCase.ConfirmAsync(plan);

            Assert.Multiple(() =>
            {
                Assert.That(blocked.RequiresReview, Is.True);
                Assert.That(plan.RequiresReview, Is.False);
                Assert.That(plan.UnresolvedLinks, Has.Some.Property("TargetNotionId")
                    .EqualTo("2fffffffffffffffffffffffffffffff"));
                Assert.That(first.RolledBack, Is.False);
                Assert.That(first.Created, Is.EqualTo(9));
                Assert.That(first.Targets, Has.Count.EqualTo(9));
                Assert.That(repeated.Created, Is.Zero);
                Assert.That(repeated.Skipped, Is.EqualTo(9));
            });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Test]
    public async Task Import_preserves_contextual_derived_and_provenance_relation_kinds()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"notion-relations-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<SecondBrainDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            await using var context = new SecondBrainDbContext(options);
            await context.Database.MigrateAsync();
            var executor = new NotionImportExecutor(context);
            var areaId = "20000000000000000000000000000051";
            var sourceId = "20000000000000000000000000000052";
            var contextualId = "20000000000000000000000000000053";
            var derivedId = "20000000000000000000000000000054";
            var provenanceId = "20000000000000000000000000000055";
            var plan = new NotionImportPlan("1.0",
            [
                AreaRecord(areaId, "area-v1"),
                NoteRecord(sourceId, areaId, "source-v1",
                [
                    new NotionImportRelation("relatedNotionIds", NotionImportRelationKind.Contextual, [contextualId]),
                    new NotionImportRelation("derivedNotionIds", NotionImportRelationKind.Derived, [derivedId]),
                    new NotionImportRelation("sourceNotionIds", NotionImportRelationKind.Provenance, [provenanceId]),
                ]),
                NoteRecord(contextualId, areaId, "contextual-v1"),
                NoteRecord(derivedId, areaId, "derived-v1"),
                NoteRecord(provenanceId, areaId, "provenance-v1"),
            ], [], [], []);

            var result = await executor.ExecuteAsync(plan);
            var kinds = await ReadIntegerColumnAsync(context, "SELECT Kind FROM BrainItemRelations ORDER BY Kind");

            Assert.Multiple(() =>
            {
                Assert.That(result.RolledBack, Is.False);
                Assert.That(result.Created, Is.EqualTo(5));
                Assert.That(kinds, Is.EquivalentTo(new[] { 0, 1, 2 }));
            });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Test]
    public async Task Changed_source_conflict_blocks_all_mutation_and_reports_every_unapplied_record()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"notion-conflict-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<SecondBrainDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            await using var context = new SecondBrainDbContext(options);
            await context.Database.MigrateAsync();
            var executor = new NotionImportExecutor(context);
            var areaId = "20000000000000000000000000000061";
            var existingNoteId = "20000000000000000000000000000062";
            var newNoteId = "20000000000000000000000000000063";
            var firstPlan = new NotionImportPlan("1.0",
            [
                AreaRecord(areaId, "area-v1"),
                NoteRecord(existingNoteId, areaId, "note-v1"),
            ], [], [], []);
            var conflictingPlan = new NotionImportPlan("1.0",
            [
                AreaRecord(areaId, "area-v1"),
                NoteRecord(existingNoteId, areaId, "note-v2"),
                NoteRecord(newNoteId, areaId, "new-note-v1"),
            ], [], [], []);

            var first = await executor.ExecuteAsync(firstPlan);
            var blocked = await executor.ExecuteAsync(conflictingPlan);
            var provenanceCount = await ReadScalarIntAsync(context, "SELECT COUNT(*) FROM NotionImportProvenance");

            Assert.Multiple(() =>
            {
                Assert.That(first.Created, Is.EqualTo(2));
                Assert.That(blocked.BlockedByConflicts, Is.True);
                Assert.That(blocked.Created, Is.Zero);
                Assert.That(blocked.Conflicted, Is.EqualTo(1));
                Assert.That(blocked.Skipped, Is.EqualTo(2));
                Assert.That(blocked.Diagnostics, Has.Some.Property("Code").EqualTo("import-blocked-by-conflicts"));
                Assert.That(provenanceCount, Is.EqualTo(2));
            });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    private static NotionImportRecord AreaRecord(string id, string fingerprint) => new(
        "10000000000000000000000000000051",
        id,
        "Areas.csv",
        NotionImportTarget.Area,
        fingerprint,
        new Dictionary<string, string> { ["name"] = $"Area {id[^2..]}" },
        []);

    private static NotionImportRecord NoteRecord(
        string id,
        string areaId,
        string fingerprint,
        IReadOnlyList<NotionImportRelation>? relations = null) => new(
        "10000000000000000000000000000052",
        id,
        "Notes.csv",
        NotionImportTarget.Note,
        fingerprint,
        new Dictionary<string, string>
        {
            ["name"] = $"Note {id[^2..]}",
            ["content"] = "Synthetic content.",
            ["primaryType"] = "Area",
            ["primaryNotionId"] = areaId,
        },
        relations ?? []);

    private static async Task<int[]> ReadIntegerColumnAsync(SecondBrainDbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var values = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetInt32(0));
        }

        return values.ToArray();
    }

    private static async Task<int> ReadScalarIntAsync(SecondBrainDbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SecondBrain.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
