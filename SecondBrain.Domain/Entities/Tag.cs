using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Domain.Entities;

public sealed class Tag
{
    public Tag(TagId id, string name, Tag? parent = null)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Tag ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tag name cannot be empty.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        MoveUnder(parent);
    }

    public TagId Id { get; }

    public string Name { get; }

    public Tag? Parent { get; private set; }

    public void MoveUnder(Tag? parent)
    {
        for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor.Id == Id)
            {
                throw new InvalidOperationException("Tag hierarchies cannot contain cycles.");
            }
        }

        Parent = parent;
    }
}
