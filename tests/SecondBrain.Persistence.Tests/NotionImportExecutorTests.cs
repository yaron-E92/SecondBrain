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
