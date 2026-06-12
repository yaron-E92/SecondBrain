using SecondBrain.Abstractions.Modules;

namespace SecondBrain.Abstractions.Items;

public sealed record SecondBrainItemReference(
    SecondBrainModuleId ModuleId,
    string ExternalId,
    string ItemType);
