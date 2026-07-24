using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Domain.Entities;

public sealed class Area
{
    public Area(AreaId id, ParaContextName name)
    {
        ValidateId(id);
        ValidateName(name);
        Id = id;
        Name = name;
    }

    public AreaId Id { get; }

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
            throw new InvalidOperationException("Area is already archived.");
        }

        IsArchived = true;
    }

    public void Restore()
    {
        if (!IsArchived)
        {
            throw new InvalidOperationException("Area is not archived.");
        }

        IsArchived = false;
    }

    private void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Archived areas cannot be changed.");
        }
    }

    private static void ValidateId(AreaId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Area ID cannot be empty.", nameof(id));
        }
    }

    private static void ValidateName(ParaContextName name)
    {
        if (string.IsNullOrWhiteSpace(name.Value))
        {
            throw new ArgumentException("Area name cannot be empty.", nameof(name));
        }
    }
}
