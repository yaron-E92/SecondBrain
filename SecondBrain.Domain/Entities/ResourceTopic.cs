using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Domain.Entities;

public sealed class ResourceTopic
{
    public ResourceTopic(ResourceTopicId id, ParaContextName name)
    {
        ValidateId(id);
        ValidateName(name);
        Id = id;
        Name = name;
    }

    public ResourceTopicId Id { get; }

    public ParaContextName Name { get; private set; }

    public bool IsArchived { get; private set; }

    public void Rename(ParaContextName name)
    {
        EnsureNotArchived();
        ValidateName(name);
        Name = name;
    }

    public void Archive()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Resource topic is already archived.");
        }

        IsArchived = true;
    }

    public void Restore()
    {
        if (!IsArchived)
        {
            throw new InvalidOperationException("Resource topic is not archived.");
        }

        IsArchived = false;
    }

    private void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Archived resource topics cannot be changed.");
        }
    }

    private static void ValidateId(ResourceTopicId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Resource topic ID cannot be empty.", nameof(id));
        }
    }

    private static void ValidateName(ParaContextName name)
    {
        if (string.IsNullOrWhiteSpace(name.Value))
        {
            throw new ArgumentException("Resource topic name cannot be empty.", nameof(name));
        }
    }
}
