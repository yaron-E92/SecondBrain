using NUnit.Framework;

namespace SecondBrain.Architecture.Tests;

[TestFixture]
public sealed class PresentationNavigationContractTests
{
    [Test]
    public void AppShell_PrimaryNavigationExpressesProductLifecycle()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "SecondBrain.Presentation",
            "AppShell.cs"));

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
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "SecondBrain.Presentation",
            "AppShell.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("HiddenItem(\"inbox-process\""));
            Assert.That(source, Does.Contain("HiddenItem(\"journals\""));
            Assert.That(source, Does.Contain("HiddenItem(\"settings\""));
            Assert.That(source, Does.Contain("HiddenItem(\"data-import\""));
            Assert.That(source, Does.Contain("HiddenItem(\"editor\""));

            Assert.That(source, Does.Not.Contain("PrimaryItem(\"journals\""));
            Assert.That(source, Does.Not.Contain("PrimaryItem(\"settings\""));
            Assert.That(source, Does.Not.Contain("PrimaryItem(\"data-import\""));
            Assert.That(source, Does.Not.Contain("PrimaryItem(\"editor\""));
            Assert.That(source, Does.Not.Contain("PrimaryContent(\"journals\""));
            Assert.That(source, Does.Not.Contain("PrimaryContent(\"settings\""));
            Assert.That(source, Does.Not.Contain("PrimaryContent(\"data-import\""));
            Assert.That(source, Does.Not.Contain("PrimaryContent(\"editor\""));
        });
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
}
