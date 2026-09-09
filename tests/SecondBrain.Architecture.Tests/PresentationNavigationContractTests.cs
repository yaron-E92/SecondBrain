using NUnit.Framework;

namespace SecondBrain.Architecture.Tests;

[TestFixture]
public sealed class PresentationNavigationContractTests
{
    [Test]
    public void AppShell_PrimaryNavigationExpressesProductLifecycle()
    {
        var source = ReadPresentationFile("AppShell.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("PrimaryItem(\"home\", \"Home\""));
            Assert.That(source, Does.Contain("PrimaryItem(\"inbox\", \"Process\""));
            Assert.That(source, Does.Contain("PrimaryItem(\"para\", \"Browse\""));
            Assert.That(source, Does.Contain("PrimaryItem(\"search\", \"Search\""));
            Assert.That(source, Does.Contain("PrimaryItem(\"review\", \"Review\""));

            Assert.That(source, Does.Contain("PrimaryContent(\"home\", \"Home\""));
            Assert.That(source, Does.Contain("PrimaryContent(\"inbox\", \"Process\""));
            Assert.That(source, Does.Contain("PrimaryContent(\"para\", \"Browse\""));
            Assert.That(source, Does.Contain("PrimaryContent(\"search\", \"Search\""));
            Assert.That(source, Does.Contain("PrimaryContent(\"review\", \"Review\""));
        });
    }

    [Test]
    public void AppShell_ImplementationAndUtilitySurfacesRemainContextual()
    {
        var source = ReadPresentationFile("AppShell.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("HiddenItem(\"inbox-process\""));
            Assert.That(source, Does.Contain("HiddenItem(\"journals\""));
            Assert.That(source, Does.Contain("HiddenItem(\"create\""));
            Assert.That(source, Does.Contain("HiddenItem(\"editor\""));
            Assert.That(source, Does.Contain("HiddenItem(\"settings\""));
            Assert.That(source, Does.Contain("HiddenItem(\"data-import\""));

            foreach (var route in new[] { "journals", "create", "editor", "settings", "data-import" })
            {
                Assert.That(source, Does.Not.Contain($"PrimaryItem(\"{route}\""));
                Assert.That(source, Does.Not.Contain($"PrimaryContent(\"{route}\""));
            }
        });
    }

    [Test]
    public void KnowledgeCreationAndEditing_AreSeparateContextualSurfaces()
    {
        var root = FindRepositoryRoot();
        var shell = ReadPresentationFile("AppShell.cs");
        var create = ReadPresentationFile("CoreCreatePage.cs");
        var edit = ReadPresentationFile("CoreEditPage.cs");
        var combinedEditorPath = Path.Combine(
            root,
            "SecondBrain.Presentation",
            "CoreEditorPage.cs");

        Assert.Multiple(() =>
        {
            Assert.That(shell, Does.Contain("CoreCreatePage createPage"));
            Assert.That(shell, Does.Contain("CoreEditPage editPage"));
            Assert.That(shell, Does.Not.Contain("CoreEditorPage coreEditorPage"));
            Assert.That(File.Exists(combinedEditorPath), Is.False,
                "The old combined Create/Edit surface must not remain available for accidental reuse.");

            Assert.That(create, Does.Contain("Title = \"Create knowledge\""));
            Assert.That(create, Does.Not.Contain("Existing item"));

            Assert.That(edit, Does.Contain("Title = \"Edit knowledge\""));
            Assert.That(edit, Does.Contain("Creation is a separate flow."));
            Assert.That(edit, Does.Not.Contain("New item"));
        });
    }

    [Test]
    public void DesktopRail_UsesExplicitReadableInactiveText()
    {
        var shell = ReadPresentationFile("AppShell.cs");
        var visual = ReadPresentationFile("SecondBrainVisual.cs");

        Assert.Multiple(() =>
        {
            Assert.That(visual, Does.Contain("NavigationText = Color.FromArgb(\"#D9E4EE\")"));
            Assert.That(shell, Does.Contain("Shell.SetUnselectedColor(this, SecondBrainVisual.NavigationText)"));
            Assert.That(shell, Does.Contain("Shell.SetUnselectedColor(item, SecondBrainVisual.NavigationText)"));
        });
    }

    private static string ReadPresentationFile(string fileName) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "SecondBrain.Presentation",
            fileName));

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
}
