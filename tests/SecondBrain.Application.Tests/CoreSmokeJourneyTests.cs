using NUnit.Framework;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Application.Tests;

[TestFixture]
public sealed class CoreSmokeJourneyTests
{
    private static readonly DateTimeOffset _now =
        new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task FreshInstall_CreatesContextsContentAndRelatedWorkspaces()
    {
        var repository = new JourneyRepository(EmptyState());
        var useCases = new CoreKnowledgeUseCases(repository);
        var browser = CreateBrowser(repository, useCases);

        await browser.LoadCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(browser.AreCatalogAreasEmpty, Is.True);
            Assert.That(browser.AreCatalogProjectsEmpty, Is.True);
        });

        var area = await CreateContextAsync(
            browser,
            ParaContextKind.Area,
            "Writing");
        var project = await CreateContextAsync(
            browser,
            ParaContextKind.Project,
            "Launch",
            "Ship the Core journey");

        browser.OpenWorkspace(project, "home");
        var noteTarget = browser.GetWorkspaceCreateTarget(BrainItemKind.Note);
        Assert.That(noteTarget, Is.Not.Null);
        var projectEditor = new CoreEditorViewModel(useCases, () => _now);
        projectEditor.BeginCreate(noteTarget!.Kind, noteTarget.Placement);
        projectEditor.Title = "Launch brief";
        projectEditor.Content = "A project note created from its workspace.";
        await projectEditor.SaveCommand.ExecuteAsync(null);
        await browser.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(projectEditor.HasError, Is.False);
            Assert.That(browser.WorkspaceReturnRoute, Is.EqualTo("home"));
            Assert.That(
                browser.WorkspaceNotes.Select(item => item.Title),
                Is.EqualTo(new[] { "Launch brief" }));
        });

        browser.OpenWorkspace(area);
        var resourceTarget = browser.GetWorkspaceCreateTarget(
            BrainItemKind.ResourceArtifact);
        Assert.That(resourceTarget, Is.Not.Null);
        var areaEditor = new CoreEditorViewModel(useCases, () => _now.AddMinutes(1));
        areaEditor.BeginCreate(resourceTarget!.Kind, resourceTarget.Placement);
        areaEditor.Title = "Writing guide";
        areaEditor.Content = "Reusable guidance created from the Area workspace.";
        await areaEditor.SaveCommand.ExecuteAsync(null);
        await browser.LoadCommand.ExecuteAsync(null);

        browser.OpenWorkspace(project);
        browser.SelectedItem = browser.WorkspaceNotes.Single();
        var areaResource = browser.AvailableLinkTargets.Single(item =>
            item.Title == "Writing guide");
        Assert.That(
            await browser.AddLinkToSelectedAsync(areaResource),
            Is.True);

        browser.OpenWorkspace(area);
        var relatedProject = browser.WorkspaceRelatedContexts.Single();
        browser.OpenRelatedWorkspace(relatedProject);

        Assert.Multiple(() =>
        {
            Assert.That(areaEditor.HasError, Is.False);
            Assert.That(relatedProject.Kind, Is.EqualTo(ParaContextKind.Project));
            Assert.That(browser.Workspace!.Name, Is.EqualTo("Launch"));
            Assert.That(browser.TryReturnToPreviousWorkspace(), Is.True);
            Assert.That(browser.Workspace!.Name, Is.EqualTo("Writing"));
            Assert.That(
                browser.WorkspaceResources.Select(item => item.Title),
                Is.EqualTo(new[] { "Writing guide" }));
        });
    }

    [Test]
    public async Task QuickCapture_CanBeEditedMovedArchivedRestoredAndRetrieved()
    {
        var repository = new JourneyRepository(EmptyState());
        var useCases = new CoreKnowledgeUseCases(repository);
        var browser = CreateBrowser(repository, useCases);
        await browser.LoadCommand.ExecuteAsync(null);
        var area = await CreateContextAsync(
            browser,
            ParaContextKind.Area,
            "Writing");
        var project = await CreateContextAsync(
            browser,
            ParaContextKind.Project,
            "Launch",
            "Ship the capture");

        var inbox = new InboxViewModel(new DashboardUseCase(repository));
        var dashboard = new DashboardViewModel(
            new DashboardUseCase(repository),
            inbox)
        {
            CaptureText = "Review the launch outline",
        };
        await dashboard.CaptureCommand.ExecuteAsync(null);
        await inbox.LoadCommand.ExecuteAsync(null);
        var captured = repository.State.BrainItems.Single();

        var editor = new CoreEditorViewModel(useCases, () => _now.AddMinutes(2));
        await editor.LoadAsync(captured.Id);
        editor.Title = "Launch outline review";
        editor.Content = "Typed Idea content ready for the project.";
        await editor.SaveCommand.ExecuteAsync(null);

        await browser.LoadCommand.ExecuteAsync(null);
        browser.SelectedContext = browser.Contexts.Single(context =>
            context.Kind == ParaContextKind.Inbox);
        browser.SelectedItem = browser.Items.Single();
        var projectDestination = browser.Destinations.Single(destination =>
            destination.Placement == project.Placement());
        Assert.That(
            await browser.MoveSelectedAsync(projectDestination),
            Is.True);

        browser.OpenWorkspace(project);
        browser.SelectedItem = browser.WorkspaceIdeas.Single();
        Assert.That(await browser.ArchiveSelectedAsync(), Is.True);

        browser.CloseWorkspace();
        browser.SelectedContext = browser.Contexts.Single(context =>
            context.Kind == ParaContextKind.Archive);
        browser.SelectedItem = browser.Items.Single();
        Assert.That(await browser.RestoreSelectedAsync(), Is.True);

        browser.OpenWorkspace(project);
        browser.SelectedItem = browser.WorkspaceIdeas.Single();
        var areaDestination = browser.Destinations.Single(destination =>
            destination.Placement == area.Placement());
        Assert.That(
            await browser.MoveSelectedAsync(areaDestination),
            Is.True);
        browser.OpenWorkspace(area);

        Assert.Multiple(() =>
        {
            Assert.That(dashboard.CaptureStatus, Is.EqualTo("Captured to Inbox."));
            Assert.That(inbox.Items.Single().Id, Is.EqualTo(captured.Id));
            Assert.That(editor.Kind, Is.EqualTo(BrainItemKind.Idea));
            Assert.That(editor.HasError, Is.False);
            Assert.That(repository.State.BrainItems.Single().IsArchived, Is.False);
            Assert.That(
                browser.WorkspaceIdeas.Select(item => item.Title),
                Is.EqualTo(new[] { "Launch outline review" }));
        });
    }

    [Test]
    public async Task LoadAndSaveFailures_RetainUsefulStateAndCanBeRetried()
    {
        var repository = new JourneyRepository(EmptyState())
        {
            LoadFailuresRemaining = 1,
        };
        var useCases = new CoreKnowledgeUseCases(repository);
        var browser = CreateBrowser(repository, useCases);

        await browser.LoadCommand.ExecuteAsync(null);
        Assert.That(browser.ErrorMessage, Does.Contain("Temporary load failure"));

        await browser.LoadCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(browser.HasError, Is.False);
            Assert.That(browser.AreCatalogAreasEmpty, Is.True);
        });

        repository.FailSaves = true;
        browser.BeginCreateContext(ParaContextKind.Area);
        browser.ContextName = "Writing";
        Assert.That(await browser.SaveContextAsync(), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(browser.ContextName, Is.EqualTo("Writing"));
            Assert.That(browser.IsContextEditorVisible, Is.True);
            Assert.That(browser.ContextEditorError, Does.Contain("Temporary save failure"));
        });

        repository.FailSaves = false;
        Assert.That(await browser.SaveContextAsync(), Is.True);
        var area = browser.ContextCatalog.Single(context =>
            context.Kind == ParaContextKind.Area);

        repository.FailSaves = true;
        var editor = new CoreEditorViewModel(useCases, () => _now);
        editor.BeginCreate(BrainItemKind.Note, area.Placement());
        editor.Title = "Keep this draft";
        editor.Content = "Unsaved but still editable.";
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(editor.ErrorMessage, Does.Contain("Temporary save failure"));
            Assert.That(editor.Title, Is.EqualTo("Keep this draft"));
            Assert.That(editor.Content, Is.EqualTo("Unsaved but still editable."));
            Assert.That(editor.IsDirty, Is.True);
        });

        repository.FailSaves = false;
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            Assert.That(editor.HasError, Is.False);
            Assert.That(editor.IsDirty, Is.False);
            Assert.That(repository.State.BrainItems.Single().Title, Is.EqualTo(
                "Keep this draft"));
        });
    }

    private static ParaBrowserViewModel CreateBrowser(
        ICoreKnowledgeRepository repository,
        CoreKnowledgeUseCases useCases) =>
        new(repository, useCases, () => _now.AddMinutes(3));

    private static async Task<ContextCatalogItem> CreateContextAsync(
        ParaBrowserViewModel browser,
        ParaContextKind kind,
        string name,
        string? outcome = null)
    {
        browser.BeginCreateContext(kind);
        browser.ContextName = name;
        if (kind == ParaContextKind.Project)
        {
            browser.ProjectOutcome = outcome!;
        }

        Assert.That(await browser.SaveContextAsync(), Is.True);
        return browser.ContextCatalog.Single(context =>
            context.Kind == kind && context.Name == name);
    }

    private static CoreKnowledgeState EmptyState() =>
        new([], [], [], [], [], []);

    private sealed class JourneyRepository(CoreKnowledgeState state)
        : ICoreKnowledgeRepository
    {
        public CoreKnowledgeState State { get; private set; } = state;

        public int LoadFailuresRemaining { get; set; }

        public bool FailSaves { get; set; }

        public Task<CoreKnowledgeState> LoadStateAsync(
            CancellationToken cancellationToken = default)
        {
            if (LoadFailuresRemaining > 0)
            {
                LoadFailuresRemaining--;
                return Task.FromException<CoreKnowledgeState>(
                    new InvalidOperationException("Temporary load failure"));
            }

            return Task.FromResult(State);
        }

        public Task SaveStateAsync(
            CoreKnowledgeState newState,
            CancellationToken cancellationToken = default)
        {
            if (FailSaves)
            {
                return Task.FromException(
                    new InvalidOperationException("Temporary save failure"));
            }

            State = newState;
            return Task.CompletedTask;
        }
    }
}

internal static class CoreSmokeJourneyContextExtensions
{
    public static PrimaryPlacement Placement(this ContextCatalogItem context) =>
        context.Kind switch
        {
            ParaContextKind.Project => PrimaryPlacement.InProject(
                new ProjectId(context.Id)),
            ParaContextKind.Area => PrimaryPlacement.InArea(new AreaId(context.Id)),
            ParaContextKind.ResourceTopic => PrimaryPlacement.InResourceTopic(
                new ResourceTopicId(context.Id)),
            _ => throw new ArgumentOutOfRangeException(nameof(context)),
        };
}
