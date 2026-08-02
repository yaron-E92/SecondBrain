using SecondBrain.Abstractions.Items;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Domain.Entities;

public sealed class BrainItemLink
{
    public BrainItemLink(
        BrainItemLinkId id,
        BrainItemLinkType type,
        SecondBrainItemReference target)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Brain item link ID cannot be empty.", nameof(id));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(target.ModuleId);

        if (target.ModuleId.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(target.ModuleId.Name) ||
            string.IsNullOrWhiteSpace(target.ExternalId) ||
            string.IsNullOrWhiteSpace(target.ItemType))
        {
            throw new ArgumentException(
                "Link targets require a stable module, external ID, and item type.",
                nameof(target));
        }

        Id = id;
        Type = type;
        Target = target with
        {
            ModuleId = target.ModuleId with { Name = target.ModuleId.Name.Trim() },
            ExternalId = target.ExternalId.Trim(),
            ItemType = target.ItemType.Trim(),
        };
    }

    public BrainItemLinkId Id { get; }

    public BrainItemLinkType Type { get; }

    public SecondBrainItemReference Target { get; }

    public BrainItemLinkTargetState TargetState { get; private set; }

    public void MarkTargetStale()
    {
        EnsureTargetNotDeleted();

        if (TargetState == BrainItemLinkTargetState.Stale)
        {
            throw new InvalidOperationException("Link target is already stale.");
        }

        TargetState = BrainItemLinkTargetState.Stale;
    }

    public void MarkTargetAvailable()
    {
        EnsureTargetNotDeleted();

        if (TargetState == BrainItemLinkTargetState.Available)
        {
            throw new InvalidOperationException("Link target is already available.");
        }

        TargetState = BrainItemLinkTargetState.Available;
    }

    public void MarkTargetDeleted()
    {
        if (TargetState == BrainItemLinkTargetState.Deleted)
        {
            throw new InvalidOperationException("Link target is already deleted.");
        }

        TargetState = BrainItemLinkTargetState.Deleted;
    }

    private void EnsureTargetNotDeleted()
    {
        if (TargetState == BrainItemLinkTargetState.Deleted)
        {
            throw new InvalidOperationException(
                "Deleted link targets retain their reference and cannot become available.");
        }
    }
}
