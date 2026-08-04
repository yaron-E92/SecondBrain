using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Persistence.Tests;

[TestFixture]
public sealed class CoreSearchQueryServiceTests
{
    private string? _databasePath;

    [TearDown]
    public void DeleteDatabase()
    {
        SqliteConnection.ClearAllPools();
        if (_databasePath is not null)
        {
            File.Delete(_databasePath);
        }
    }

    [Test]
    public async Task Search_RanksAndPagesDeterministically_AndKeepsTypedBacklinks()
    {
        await using var context = await CreatePopulatedContextAsync();
        var service = new CoreSearchQueryService(context);

        var first = await service.SearchAsync(new CoreSearchQuery(
            Text: "alpha",
            IsArchived: null,
            PageSize: 2));
        var second = await service.SearchAsync(new CoreSearchQuery(
            Text: "alpha",
            IsArchived: null,
            Offset: 2,
            PageSize: 2));

        Assert.Multiple(() =>
        {
            Assert.That(first.TotalCount, Is.EqualTo(4));
            Assert.That(
                first.Items.Concat(second.Items).Select(item => item.Title),
                Is.EqualTo(new[]
                {
                    "Alpha",
                    "Alpha prefix",
                    "My alpha reference",
                    "Body match",
                }));
            Assert.That(
                first.Items[0].Backlinks.Select(link => link.Kind),
                Is.EquivalentTo(new[]
                {
                    CoreBacklinkKind.Contextual,
                    CoreBacklinkKind.Derived,
                    CoreBacklinkKind.Provenance,
                }));
        });
    }

    [Test]
    public async Task Search_AppliesFiltersAndTreatsEmptyOrMalformedTextSafely()
    {
        await using var context = await CreatePopulatedContextAsync();
        var service = new CoreSearchQueryService(context);
        var areaId = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var filtered = await service.SearchAsync(new CoreSearchQuery(
            Kind: BrainItemKind.Note,
            Tag: "focus",
            PlacementKind: PrimaryPlacementKind.Area,
            PlacementId: areaId,
            IsArchived: false));
        var empty = await service.SearchAsync(new CoreSearchQuery(
            Text: "   ",
            IsArchived: null,
            PageSize: 100));
        var malformed = await service.SearchAsync(new CoreSearchQuery(
            Text: "%_'(",
            IsArchived: null));

        Assert.Multiple(() =>
        {
            Assert.That(filtered.Items.Select(item => item.Title), Is.EqualTo(
                new[] { "Alpha" }));
            Assert.That(empty.TotalCount, Is.EqualTo(7));
            Assert.That(malformed.Items, Is.Empty);
        });
    }

    private async Task<SecondBrainDbContext> CreatePopulatedContextAsync()
    {
        _databasePath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"core-search-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<SecondBrainDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;
        var context = new SecondBrainDbContext(options);
        await context.Database.MigrateAsync();

        var area = new Area(
            new AreaId(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            new ParaContextName("Writing"));
        var tag = new Tag(
            new TagId(new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            "focus");
        var created = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);
        var target = Note(
            "10000000-0000-0000-0000-000000000001",
            "Alpha",
            "Exact title",
            area,
            created,
            [tag.Id]);
        target.MarkFavorite();
        var prefix = Note(
            "10000000-0000-0000-0000-000000000002",
            "Alpha prefix",
            "Prefix title",
            area,
            created.AddMinutes(4));
        var contains = Note(
            "10000000-0000-0000-0000-000000000003",
            "My alpha reference",
            "Contains title",
            area,
            created.AddMinutes(3));
        var body = Note(
            "10000000-0000-0000-0000-000000000004",
            "Body match",
            "The alpha term is in the body.",
            area,
            created.AddMinutes(2));
        var contextual = new BrainItem(
            new SecondBrainItemId(new Guid("10000000-0000-0000-0000-000000000005")),
            BrainItemKind.Idea,
            "Context source",
            "Links to the exact item.",
            PrimaryPlacement.InArea(area.Id),
            created,
            ideaMaturity: IdeaMaturity.Captured,
            contextualLinks: [target.Id]);
        var derived = new BrainItem(
            new SecondBrainItemId(new Guid("10000000-0000-0000-0000-000000000006")),
            BrainItemKind.KnowledgeCapture,
            "Derived source",
            "Produces the exact item.",
            PrimaryPlacement.InArea(area.Id),
            created,
            captureSourceType: CaptureSourceType.Article,
            sourceUri: new Uri("https://example.com/source"),
            sourceCitation: "Example",
            captureProcessingState: CaptureProcessingState.Distilled,
            derivedItemLinks: [target.Id]);
        var provenance = new BrainItem(
            new SecondBrainItemId(new Guid("10000000-0000-0000-0000-000000000007")),
            BrainItemKind.ResourceArtifact,
            "Provenance source",
            "Uses the exact item as provenance.",
            PrimaryPlacement.InArea(area.Id),
            created,
            resourceArtifactKind: ResourceArtifactKind.Guide,
            resourceFreshness: ResourceFreshness.Current,
            provenanceSources: [target]);

        await new SecondBrainDataStore(context).ReplaceAsync(
            new SecondBrainDataSnapshot(
                [],
                [area],
                [],
                [tag],
                [target, prefix, contains, body, contextual, derived, provenance],
                []));
        return context;
    }

    private static BrainItem Note(
        string id,
        string title,
        string content,
        Area area,
        DateTimeOffset updatedAt,
        IEnumerable<TagId>? tags = null) => new(
            new SecondBrainItemId(Guid.Parse(id)),
            BrainItemKind.Note,
            title,
            content,
            PrimaryPlacement.InArea(area.Id),
            updatedAt.AddMinutes(-1),
            noteKind: NoteKind.General,
            updatedAt: updatedAt,
            tagIds: tags);
}
