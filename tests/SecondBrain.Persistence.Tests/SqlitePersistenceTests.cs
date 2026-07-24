using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using SecondBrain.Abstractions.Items;
using SecondBrain.Abstractions.Modules;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Persistence.Tests;

[TestFixture]
public sealed class SqlitePersistenceTests
{
    private readonly List<string> _databasePaths = [];

    [TearDown]
    public void DeleteTestDatabases()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in _databasePaths)
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Migrate_CreatesNewSqliteDatabaseAtConfiguredPath()
    {
        var path = CreateDatabasePath();
        await using var context = CreateContext(path);

        await context.Database.MigrateAsync();

        Assert.That(File.Exists(path), Is.True);
        var migrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.That(migrations, Is.EqualTo(["20260724153000_InitialCorePersistence"]));
    }

    [Test]
    public async Task Store_RoundTripsCoreModelAcrossContexts()
    {
        var path = CreateDatabasePath();
        var snapshot = CreateSnapshot();

        await using (var writeContext = CreateContext(path))
        {
            await writeContext.Database.MigrateAsync();
            await new SecondBrainDataStore(writeContext).ReplaceAsync(snapshot);
        }

        await using var readContext = CreateContext(path);
        var loaded = await new SecondBrainDataStore(readContext).LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Projects.Single().Status, Is.EqualTo(ProjectStatus.Completed));
            Assert.That(loaded.Areas.Single().IsArchived, Is.True);
            Assert.That(loaded.ResourceTopics.Single().Name.Value, Is.EqualTo("Databases"));
            Assert.That(loaded.Tags.Single(tag => tag.Parent is not null).Parent!.Name, Is.EqualTo("Engineering"));
            Assert.That(
                loaded.BrainItems.Select(item => item.Kind),
                Is.EquivalentTo(Enum.GetValues<BrainItemKind>()));
            Assert.That(
                loaded.BrainItems.Single(item => item.Kind == BrainItemKind.Note).Links.Single().TargetState,
                Is.EqualTo(BrainItemLinkTargetState.Stale));
            Assert.That(
                loaded.BrainItems.Single(item => item.Kind == BrainItemKind.KnowledgeCapture).DerivedItemLinks,
                Has.Count.EqualTo(1));
            Assert.That(
                loaded.BrainItems.Single(item => item.Kind == BrainItemKind.ResourceArtifact).ProvenanceSourceLinks,
                Has.Count.EqualTo(1));
            Assert.That(loaded.Journals.Single().Entries, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task DatabaseConstraints_RejectInvalidIdentityPlacementHierarchyAndLifecycle()
    {
        var path = CreateDatabasePath();
        await using var context = CreateContext(path);
        await context.Database.MigrateAsync();

        var connection = (SqliteConnection)context.Database.GetDbConnection();
        await connection.OpenAsync();

        await AssertInvalidAsync(
            connection,
            """
            INSERT INTO Projects (Id, Name, Outcome, Status, Priority, IsArchived)
            VALUES ('00000000-0000-0000-0000-000000000000', 'Name', 'Outcome', 0, 1, 0);
            """);
        await AssertInvalidAsync(
            connection,
            """
            INSERT INTO Tags (Id, Name, ParentId)
            VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Cycle', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
            """);
        await AssertInvalidAsync(
            connection,
            ValidBrainItemInsert(
                "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                projectId: "NULL",
                noteKind: "1"));

        await ExecuteAsync(
            connection,
            """
            INSERT INTO Projects (Id, Name, Outcome, Status, Priority, IsArchived)
            VALUES ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'Name', 'Outcome', 0, 1, 0);
            """);
        await AssertInvalidAsync(
            connection,
            ValidBrainItemInsert(
                "dddddddd-dddd-dddd-dddd-dddddddddddd",
                projectId: "'cccccccc-cccc-cccc-cccc-cccccccccccc'",
                noteKind: "NULL"));
    }

    private static async Task AssertInvalidAsync(SqliteConnection connection, string sql)
    {
        var exception = Assert.ThrowsAsync<SqliteException>(
            async () => await ExecuteAsync(connection, sql));
        Assert.That(exception!.SqliteErrorCode, Is.EqualTo(19));
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string ValidBrainItemInsert(
        string id,
        string projectId,
        string noteKind) =>
        $"""
        INSERT INTO BrainItems (
            Id, Kind, Title, Content, PlacementKind, ProjectId, CreatedAt, UpdatedAt,
            NoteKind, IsArchived, IsFavorite)
        VALUES (
            '{id}', 1, 'Title', 'Content', 0, {projectId},
            '2026-07-24 10:00:00+00:00', '2026-07-24 10:00:00+00:00',
            {noteKind}, 0, 0);
        """;

    private static SecondBrainDataSnapshot CreateSnapshot()
    {
        var project = new Project(
            ProjectId.New(),
            new ParaContextName("Persistence"),
            "Ship durable storage",
            ProjectPriority.High,
            new DateOnly(2026, 8, 1));
        project.Activate();
        project.Complete();

        var area = new Area(AreaId.New(), new ParaContextName("Engineering"));
        area.Archive();
        var topic = new ResourceTopic(
            ResourceTopicId.New(),
            new ParaContextName("Databases"));
        var rootTag = new Tag(TagId.New(), "Engineering");
        var childTag = new Tag(TagId.New(), "SQLite", rootTag);
        var createdAt = new DateTimeOffset(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);

        var idea = new BrainItem(
            SecondBrainItemId.New(),
            BrainItemKind.Idea,
            "Use SQLite",
            "Keep local data durable.",
            PrimaryPlacement.InArea(area.Id),
            createdAt,
            ideaMaturity: IdeaMaturity.Actionable);
        var link = new BrainItemLink(
            BrainItemLinkId.New(),
            BrainItemLinkType.References,
            new SecondBrainItemReference(
                new SecondBrainModuleId(Guid.NewGuid(), "ShuffleTask"),
                "task-42",
                "Task"));
        link.MarkTargetStale();
        var note = new BrainItem(
            SecondBrainItemId.New(),
            BrainItemKind.Note,
            "Persistence note",
            "SQLite is the selected provider.",
            PrimaryPlacement.InProject(project.Id),
            createdAt,
            noteKind: NoteKind.General,
            tags: ["database"],
            contextualLinks: [idea.Id],
            tagIds: [childTag.Id],
            links: [link]);
        note.MarkFavorite();
        var entry = new BrainItem(
            SecondBrainItemId.New(),
            BrainItemKind.JournalEntry,
            "Migration day",
            "Created the first migration.",
            PrimaryPlacement.InResourceTopic(topic.Id),
            createdAt,
            entryDate: new DateOnly(2026, 7, 24));
        var capture = new BrainItem(
            SecondBrainItemId.New(),
            BrainItemKind.KnowledgeCapture,
            "EF documentation",
            "Provider documentation.",
            PrimaryPlacement.InProject(project.Id),
            createdAt,
            captureSourceType: CaptureSourceType.Article,
            sourceUri: new Uri("https://learn.microsoft.com/ef/core/"),
            sourceCitation: "EF Core documentation",
            reminderAt: createdAt.AddDays(1),
            captureProcessingState: CaptureProcessingState.Distilled,
            derivedItemLinks: [note.Id]);
        var resource = new BrainItem(
            SecondBrainItemId.New(),
            BrainItemKind.ResourceArtifact,
            "SQLite guide",
            "Operational notes.",
            PrimaryPlacement.InArea(area.Id),
            createdAt,
            resourceArtifactKind: ResourceArtifactKind.Guide,
            resourceFreshness: ResourceFreshness.Current,
            reviewDate: new DateOnly(2026, 12, 1),
            provenanceSources: [capture]);
        var journal = new Journal(SecondBrainItemId.New(), "Engineering journal");
        journal.AddEntry(entry);

        return new SecondBrainDataSnapshot(
            [project],
            [area],
            [topic],
            [rootTag, childTag],
            [note, idea, entry, capture, resource],
            [journal]);
    }

    private SecondBrainDbContext CreateContext(string path)
    {
        var options = new DbContextOptionsBuilder<SecondBrainDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new SecondBrainDbContext(options);
    }

    private string CreateDatabasePath()
    {
        var path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"secondbrain-{Guid.NewGuid():N}.db");
        _databasePaths.Add(path);
        return path;
    }
}
