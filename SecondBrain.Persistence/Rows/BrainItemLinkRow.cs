using SecondBrain.Domain.Entities;

namespace SecondBrain.Persistence;

internal sealed class BrainItemLinkRow
{
    public Guid Id { get; set; }
    public Guid BrainItemId { get; set; }
    public BrainItemLinkType Type { get; set; }
    public Guid TargetModuleId { get; set; }
    public string TargetModuleName { get; set; } = "";
    public string TargetExternalId { get; set; } = "";
    public string TargetItemType { get; set; } = "";
    public BrainItemLinkTargetState TargetState { get; set; }
}
