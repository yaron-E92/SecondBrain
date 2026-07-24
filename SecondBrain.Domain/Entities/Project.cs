using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Domain.Entities;

public sealed class Project
{
    public Project(
        ProjectId id,
        ParaContextName name,
        string outcome,
        ProjectPriority priority = ProjectPriority.Normal,
        DateOnly? targetDate = null)
    {
        ValidateId(id);
        ValidateName(name);

        if (string.IsNullOrWhiteSpace(outcome))
        {
            throw new ArgumentException("Project outcome cannot be empty.", nameof(outcome));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        Id = id;
        Name = name;
        Outcome = outcome.Trim();
        Priority = priority;
        TargetDate = targetDate;
        Status = ProjectStatus.Planned;
    }

    public ProjectId Id { get; }

    public ParaContextName Name { get; private set; }

    public string Outcome { get; private set; }

    public ProjectStatus Status { get; private set; }

    public ProjectPriority Priority { get; private set; }

    public DateOnly? TargetDate { get; private set; }

    public bool IsArchived { get; private set; }

    public void Rename(ParaContextName name)
    {
        EnsureNotArchived();
        ValidateName(name);
        Name = name;
    }

    public void UpdateMetadata(
        string outcome,
        ProjectPriority priority,
        DateOnly? targetDate)
    {
        EnsureNotArchived();

        if (string.IsNullOrWhiteSpace(outcome))
        {
            throw new ArgumentException("Project outcome cannot be empty.", nameof(outcome));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        Outcome = outcome.Trim();
        Priority = priority;
        TargetDate = targetDate;
    }

    public void Activate()
    {
        EnsureNotArchived();
        EnsureStatus(ProjectStatus.Planned);
        Status = ProjectStatus.Active;
    }

    public void Complete()
    {
        EnsureNotArchived();
        EnsureStatus(ProjectStatus.Active);
        Status = ProjectStatus.Completed;
    }

    public void Cancel()
    {
        EnsureNotArchived();

        if (Status is not (ProjectStatus.Planned or ProjectStatus.Active))
        {
            throw new InvalidOperationException(
                $"Cannot cancel a project with status {Status}.");
        }

        Status = ProjectStatus.Cancelled;
    }

    public void Archive()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Project is already archived.");
        }

        IsArchived = true;
    }

    public void Restore()
    {
        if (!IsArchived)
        {
            throw new InvalidOperationException("Project is not archived.");
        }

        IsArchived = false;
    }

    private void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Archived projects cannot be changed.");
        }
    }

    private void EnsureStatus(ProjectStatus required)
    {
        if (Status != required)
        {
            throw new InvalidOperationException(
                $"Project must have status {required}, but has status {Status}.");
        }
    }

    private static void ValidateId(ProjectId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Project ID cannot be empty.", nameof(id));
        }
    }

    private static void ValidateName(ParaContextName name)
    {
        if (string.IsNullOrWhiteSpace(name.Value))
        {
            throw new ArgumentException("Project name cannot be empty.", nameof(name));
        }
    }
}
