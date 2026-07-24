using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using NUnit.Framework;

namespace SecondBrain.Domain.Tests;

[TestFixture]
public sealed class ParaContextsTests
{
    [TestCase("project")]
    [TestCase("area")]
    [TestCase("resource topic")]
    public void ContextIds_RejectEmptyGuids(string contextType)
    {
        TestDelegate action = contextType switch
        {
            "project" => () => _ = new ProjectId(Guid.Empty),
            "area" => () => _ = new AreaId(Guid.Empty),
            _ => () => _ = new ResourceTopicId(Guid.Empty),
        };

        Assert.Throws<ArgumentException>(action);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void ContextName_RejectsEmptyValues(string value)
    {
        Assert.Throws<ArgumentException>(() => _ = new ParaContextName(value));
    }

    [Test]
    public void ContextName_TrimsValue()
    {
        var name = new ParaContextName("  Engineering  ");

        Assert.That(name.Value, Is.EqualTo("Engineering"));
    }

    [Test]
    public void ContextEntities_RejectDefaultIdsAndNames()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(
                () => _ = new Project(
                    default,
                    new ParaContextName("Project"),
                    "Outcome"));
            Assert.Throws<ArgumentException>(
                () => _ = new Area(AreaId.New(), default));
            Assert.Throws<ArgumentException>(
                () => _ = new ResourceTopic(default, new ParaContextName("Topic")));
        });
    }

    [Test]
    public void Project_RequiresOutcomeAndDefinedPriority()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(
                () => _ = CreateProject(outcome: " "));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _ = CreateProject(priority: (ProjectPriority)99));
        });
    }

    [Test]
    public void Project_StoresBoundedMetadata()
    {
        var targetDate = new DateOnly(2027, 1, 15);

        var project = CreateProject(
            outcome: "  Ship the first release  ",
            priority: ProjectPriority.High,
            targetDate: targetDate);

        Assert.Multiple(() =>
        {
            Assert.That(project.Outcome, Is.EqualTo("Ship the first release"));
            Assert.That(project.Priority, Is.EqualTo(ProjectPriority.High));
            Assert.That(project.TargetDate, Is.EqualTo(targetDate));
            Assert.That(project.Status, Is.EqualTo(ProjectStatus.Planned));
            Assert.That(project.IsArchived, Is.False);
        });
    }

    [Test]
    public void Project_SupportsPlannedActiveCompletedLifecycle()
    {
        var project = CreateProject();

        project.Activate();
        project.Complete();

        Assert.That(project.Status, Is.EqualTo(ProjectStatus.Completed));
    }

    [Test]
    public void Project_CanBeCancelledBeforeCompletion()
    {
        var plannedProject = CreateProject();
        var activeProject = CreateProject();
        activeProject.Activate();

        plannedProject.Cancel();
        activeProject.Cancel();

        Assert.Multiple(() =>
        {
            Assert.That(plannedProject.Status, Is.EqualTo(ProjectStatus.Cancelled));
            Assert.That(activeProject.Status, Is.EqualTo(ProjectStatus.Cancelled));
        });
    }

    [Test]
    public void Project_InvalidStatusTransitionsFailPredictably()
    {
        var plannedProject = CreateProject();
        var completedProject = CreateProject();
        completedProject.Activate();
        completedProject.Complete();

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(plannedProject.Complete);
            Assert.Throws<InvalidOperationException>(completedProject.Activate);
            Assert.Throws<InvalidOperationException>(completedProject.Cancel);
        });
    }

    [Test]
    public void Project_ArchiveAndRestorePreserveStatusAndMetadata()
    {
        var project = CreateProject(
            outcome: "Publish",
            priority: ProjectPriority.High,
            targetDate: new DateOnly(2027, 2, 1));
        project.Activate();

        project.Archive();

        Assert.Multiple(() =>
        {
            Assert.That(project.IsArchived, Is.True);
            Assert.That(project.Status, Is.EqualTo(ProjectStatus.Active));
            Assert.That(project.Outcome, Is.EqualTo("Publish"));
            Assert.Throws<InvalidOperationException>(project.Complete);
        });

        project.Restore();

        Assert.Multiple(() =>
        {
            Assert.That(project.IsArchived, Is.False);
            Assert.That(project.Status, Is.EqualTo(ProjectStatus.Active));
        });
    }

    [Test]
    public void Area_ArchiveAndRestorePreserveIdentityAndName()
    {
        var id = AreaId.New();
        var area = new Area(id, new ParaContextName("Health"));

        area.Archive();
        area.Restore();

        Assert.Multiple(() =>
        {
            Assert.That(area.Id, Is.EqualTo(id));
            Assert.That(area.Name.Value, Is.EqualTo("Health"));
            Assert.That(area.IsArchived, Is.False);
        });
    }

    [Test]
    public void ResourceTopic_ArchiveAndRestorePreserveIdentityAndName()
    {
        var id = ResourceTopicId.New();
        var resourceTopic = new ResourceTopic(id, new ParaContextName("C#"));

        resourceTopic.Archive();
        resourceTopic.Restore();

        Assert.Multiple(() =>
        {
            Assert.That(resourceTopic.Id, Is.EqualTo(id));
            Assert.That(resourceTopic.Name.Value, Is.EqualTo("C#"));
            Assert.That(resourceTopic.IsArchived, Is.False);
        });
    }

    [Test]
    public void RepeatedArchiveTransitionsFailPredictably()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Health"));

        Assert.Throws<InvalidOperationException>(area.Restore);

        area.Archive();

        Assert.Throws<InvalidOperationException>(area.Archive);
    }

    [Test]
    public void PrimaryPlacement_AllowsAbsenceOrOneTypedContext()
    {
        PrimaryPlacement? noPlacement = null;
        var projectId = ProjectId.New();
        var placement = PrimaryPlacement.InProject(projectId);

        Assert.Multiple(() =>
        {
            Assert.That(noPlacement, Is.Null);
            Assert.That(placement.Kind, Is.EqualTo(PrimaryPlacementKind.Project));
            Assert.That(placement.GetProjectId(), Is.EqualTo(projectId));
        });
    }

    [Test]
    public void PrimaryPlacement_MismatchedTypedAccessFailsPredictably()
    {
        var placement = PrimaryPlacement.InArea(AreaId.New());

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => placement.GetProjectId());
            Assert.Throws<InvalidOperationException>(() => placement.GetResourceTopicId());
        });
    }

    [Test]
    public void PrimaryPlacement_RejectsDefaultContextId()
    {
        Assert.Throws<ArgumentException>(
            () => PrimaryPlacement.InResourceTopic(default));
    }

    [Test]
    public void ArchivingContext_DoesNotChangePrimaryPlacement()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Health"));
        var placement = PrimaryPlacement.InArea(area.Id);

        area.Archive();

        Assert.Multiple(() =>
        {
            Assert.That(area.IsArchived, Is.True);
            Assert.That(placement.GetAreaId(), Is.EqualTo(area.Id));
        });
    }

    private static Project CreateProject(
        string outcome = "Deliver a useful result",
        ProjectPriority priority = ProjectPriority.Normal,
        DateOnly? targetDate = null) =>
        new(
            ProjectId.New(),
            new ParaContextName("Project"),
            outcome,
            priority,
            targetDate);
}
