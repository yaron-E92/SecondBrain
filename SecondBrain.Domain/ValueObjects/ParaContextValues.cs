namespace SecondBrain.Domain.ValueObjects;

public readonly record struct ProjectId
{
    public ProjectId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Project ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ProjectId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct AreaId
{
    public AreaId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Area ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static AreaId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct ResourceTopicId
{
    public ResourceTopicId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Resource topic ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ResourceTopicId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct ParaContextName
{
    public ParaContextName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("PARA context name cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public enum PrimaryPlacementKind
{
    Project,
    Area,
    ResourceTopic,
}

public sealed record PrimaryPlacement
{
    private PrimaryPlacement(PrimaryPlacementKind kind, Guid contextId)
    {
        if (contextId == Guid.Empty)
        {
            throw new ArgumentException("Placement context ID cannot be empty.", nameof(contextId));
        }

        Kind = kind;
        ContextId = contextId;
    }

    public PrimaryPlacementKind Kind { get; }

    public Guid ContextId { get; }

    public static PrimaryPlacement InProject(ProjectId projectId) =>
        new(PrimaryPlacementKind.Project, projectId.Value);

    public static PrimaryPlacement InArea(AreaId areaId) =>
        new(PrimaryPlacementKind.Area, areaId.Value);

    public static PrimaryPlacement InResourceTopic(ResourceTopicId resourceTopicId) =>
        new(PrimaryPlacementKind.ResourceTopic, resourceTopicId.Value);

    public ProjectId GetProjectId()
    {
        EnsureKind(PrimaryPlacementKind.Project);
        return new ProjectId(ContextId);
    }

    public AreaId GetAreaId()
    {
        EnsureKind(PrimaryPlacementKind.Area);
        return new AreaId(ContextId);
    }

    public ResourceTopicId GetResourceTopicId()
    {
        EnsureKind(PrimaryPlacementKind.ResourceTopic);
        return new ResourceTopicId(ContextId);
    }

    private void EnsureKind(PrimaryPlacementKind expected)
    {
        if (Kind != expected)
        {
            throw new InvalidOperationException(
                $"Placement is for {Kind}, not {expected}.");
        }
    }
}
