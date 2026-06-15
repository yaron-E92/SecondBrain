using System.Xml.Linq;
using NUnit.Framework;

namespace SecondBrain.Architecture.Tests;

[TestFixture]
public sealed class ProjectDependencyTests
{
    private static readonly IReadOnlyDictionary<ArchitectureLayer, ArchitectureLayer[]> AllowedDependencies =
        new Dictionary<ArchitectureLayer, ArchitectureLayer[]>
        {
            [ArchitectureLayer.Abstractions] = [],
            [ArchitectureLayer.Core] = [ArchitectureLayer.Abstractions, ArchitectureLayer.Core],
            [ArchitectureLayer.Application] =
                [ArchitectureLayer.Abstractions, ArchitectureLayer.Core, ArchitectureLayer.Application],
            [ArchitectureLayer.Persistence] =
                [ArchitectureLayer.Abstractions, ArchitectureLayer.Core, ArchitectureLayer.Application,
                    ArchitectureLayer.Persistence],
            [ArchitectureLayer.App] =
                [ArchitectureLayer.Abstractions, ArchitectureLayer.Core, ArchitectureLayer.Application,
                    ArchitectureLayer.Persistence, ArchitectureLayer.App, ArchitectureLayer.ConcreteModule],
            [ArchitectureLayer.ConcreteModule] =
                [ArchitectureLayer.Abstractions, ArchitectureLayer.ConcreteModule],
            [ArchitectureLayer.StandaloneApp] =
                [ArchitectureLayer.Abstractions, ArchitectureLayer.ConcreteModule, ArchitectureLayer.StandaloneApp]
        };

    [Test]
    public void Production_projects_have_an_architecture_layer()
    {
        var projects = LoadProductionProjects();

        var unclassifiedProjects = projects
            .Where(project => project.Layer is null)
            .Select(project => project.Name)
            .Order()
            .ToArray();

        Assert.That(unclassifiedProjects, Is.Empty,
            "Production projects must use a recognized name or set SecondBrainArchitectureLayer. " +
            $"Unclassified: {string.Join(", ", unclassifiedProjects)}");
    }

    [Test]
    public void Project_references_follow_dependency_direction_rules()
    {
        var projects = LoadProductionProjects();
        var projectsByPath = projects.ToDictionary(project => project.Path, PathComparer);
        var violations = new List<string>();

        foreach (var project in projects.Where(project => project.Layer is not null))
        {
            var projectLayer = project.Layer.GetValueOrDefault();

            foreach (var referencePath in project.ReferencePaths)
            {
                if (!projectsByPath.TryGetValue(referencePath, out var dependency))
                {
                    violations.Add($"{project.Name} references a project outside the governed graph: {referencePath}");
                    continue;
                }

                if (dependency.Layer is null)
                {
                    violations.Add($"{project.Name} references unclassified project {dependency.Name}");
                    continue;
                }

                if (!AllowedDependencies[projectLayer].Contains(dependency.Layer.Value))
                {
                    violations.Add(
                        $"{project.Name} ({project.Layer}) must not reference {dependency.Name} ({dependency.Layer})");
                }
            }
        }

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }

    private static IReadOnlyList<ProjectInfo> LoadProductionProjects()
    {
        var repositoryRoot = FindRepositoryRoot();

        return Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrTestProject(repositoryRoot, path))
            .Select(LoadProject)
            .OrderBy(project => project.Name)
            .ToArray();
    }

    private static ProjectInfo LoadProject(string projectPath)
    {
        var fullPath = Path.GetFullPath(projectPath);
        var document = XDocument.Load(fullPath);
        var projectName = GetProperty(document, "AssemblyName") ?? Path.GetFileNameWithoutExtension(fullPath);
        var configuredLayer = GetProperty(document, "SecondBrainArchitectureLayer");
        var layer = ParseLayer(configuredLayer) ?? InferLayer(projectName);
        var projectDirectory = Path.GetDirectoryName(fullPath)!;
        var references = document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, NormalizePath(include!))))
            .ToArray();

        return new ProjectInfo(projectName, fullPath, layer, references);
    }

    private static ArchitectureLayer? InferLayer(string projectName)
    {
        if (NameIs(projectName, "Brain.Abstractions", "SecondBrain.Abstractions"))
        {
            return ArchitectureLayer.Abstractions;
        }

        if (NameIs(projectName, "Brain.Core", "SecondBrain.Core", "SecondBrain.Domain"))
        {
            return ArchitectureLayer.Core;
        }

        if (NameIs(projectName, "Brain.Application", "SecondBrain.Application"))
        {
            return ArchitectureLayer.Application;
        }

        if (NameIs(projectName, "Brain.Persistence", "SecondBrain.Persistence"))
        {
            return ArchitectureLayer.Persistence;
        }

        if (NameIs(projectName, "SecondBrain.App", "Brain.App"))
        {
            return ArchitectureLayer.App;
        }

        if (HasProjectPrefix(projectName, "ShuffleTask", "PHOODAB", "SurvivalGarden"))
        {
            return ArchitectureLayer.ConcreteModule;
        }

        return null;
    }

    private static ArchitectureLayer? ParseLayer(string? configuredLayer) =>
        Enum.TryParse<ArchitectureLayer>(configuredLayer, ignoreCase: true, out var layer) ? layer : null;

    private static bool NameIs(string projectName, params string[] names) =>
        names.Contains(projectName, StringComparer.OrdinalIgnoreCase);

    private static bool HasProjectPrefix(string projectName, params string[] prefixes) =>
        prefixes.Any(prefix =>
            projectName.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            projectName.StartsWith($"{prefix}.", StringComparison.OrdinalIgnoreCase));

    private static string? GetProperty(XDocument document, string propertyName) =>
        document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == propertyName)
            ?.Value
            .Trim();

    private static string NormalizePath(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static bool IsGeneratedOrTestProject(string repositoryRoot, string projectPath)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot, projectPath);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return segments.Any(segment =>
            segment.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals(".codex-run", StringComparison.OrdinalIgnoreCase));
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

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record ProjectInfo(
        string Name,
        string Path,
        ArchitectureLayer? Layer,
        IReadOnlyList<string> ReferencePaths);

    private enum ArchitectureLayer
    {
        Abstractions,
        Core,
        Application,
        Persistence,
        App,
        ConcreteModule,
        StandaloneApp
    }
}
