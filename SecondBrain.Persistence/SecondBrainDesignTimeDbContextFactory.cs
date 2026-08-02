using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SecondBrain.Persistence;

public sealed class SecondBrainDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<SecondBrainDbContext>
{
    public SecondBrainDbContext CreateDbContext(string[] args)
    {
        var databasePath = ReadDatabasePath(args);
        var options = new DbContextOptionsBuilder<SecondBrainDbContext>()
            .UseSqlite($"Data Source={Path.GetFullPath(databasePath)}")
            .Options;
        return new SecondBrainDbContext(options);
    }

    private static string ReadDatabasePath(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], "--database", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return Environment.GetEnvironmentVariable("SECOND_BRAIN_DATABASE_PATH")
            ?? Path.Combine(Directory.GetCurrentDirectory(), "secondbrain.design.db");
    }
}
