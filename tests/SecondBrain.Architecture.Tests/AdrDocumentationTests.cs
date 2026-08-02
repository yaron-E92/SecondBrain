using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SecondBrain.Architecture.Tests;

[TestFixture]
public sealed class AdrDocumentationTests
{
    private static readonly Regex MarkdownLinkPattern =
        new(@"\[[^\]]+\]\((?<target>[^)]+)\)", RegexOptions.Compiled);

    [Test]
    public void Adr_0001_has_one_canonical_copy()
    {
        var repositoryRoot = FindRepositoryRoot();
        var architectureDirectory = Path.Combine(repositoryRoot, "docs", "architecture");
        var matches = Directory
            .EnumerateFiles(architectureDirectory, "0001-modular-app-family-strategy.md", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .ToArray();
        var expectedPath = Path.GetFullPath(
            Path.Combine(architectureDirectory, "adr", "0001-modular-app-family-strategy.md"));

        Assert.That(matches, Is.EqualTo(new[] { expectedPath }));
    }

    [Test]
    public void Repository_does_not_reference_removed_decisions_tree()
    {
        var repositoryRoot = FindRepositoryRoot();
        var removedTree = string.Join("/", "docs", "architecture", "decisions");
        var references = EnumerateRepositoryTextFiles(repositoryRoot)
            .Where(path => File.ReadAllText(path).Replace('\\', '/').Contains(
                removedTree,
                StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .ToArray();

        Assert.That(references, Is.Empty,
            $"References to the removed ADR tree remain: {string.Join(", ", references)}");
    }

    [Test]
    public void Adr_index_local_links_resolve()
    {
        var repositoryRoot = FindRepositoryRoot();
        var indexPath = Path.Combine(repositoryRoot, "docs", "architecture", "adr", "README.md");
        var indexDirectory = Path.GetDirectoryName(indexPath)!;
        var localTargets = MarkdownLinkPattern.Matches(File.ReadAllText(indexPath))
            .Select(match => match.Groups["target"].Value)
            .Where(target => !target.StartsWith('#') && !Uri.TryCreate(target, UriKind.Absolute, out _))
            .Select(target => target.Split('#', 2)[0])
            .ToArray();
        var missingTargets = localTargets
            .Where(target => !File.Exists(Path.GetFullPath(Path.Combine(
                indexDirectory,
                target.Replace('/', Path.DirectorySeparatorChar)))))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(localTargets, Is.Not.Empty, "The ADR index must link to canonical records.");
            Assert.That(missingTargets, Is.Empty,
                $"The ADR index contains missing local links: {string.Join(", ", missingTargets)}");
        });
    }

    private static IEnumerable<string> EnumerateRepositoryTextFiles(string repositoryRoot)
    {
        var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".csproj", ".json", ".md", ".ps1", ".slnx", ".yaml", ".yml"
        };

        return Directory.EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories)
            .Where(path => textExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !HasIgnoredSegment(repositoryRoot, path));
    }

    private static bool HasIgnoredSegment(string repositoryRoot, string path)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot, path);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return segments.Any(segment =>
            segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals(".codex-run", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
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

        throw new DirectoryNotFoundException("Could not locate the repository root containing SecondBrain.slnx.");
    }
}
