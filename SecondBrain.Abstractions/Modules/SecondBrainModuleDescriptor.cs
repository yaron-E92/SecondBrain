namespace SecondBrain.Abstractions.Modules;

public sealed record SecondBrainModuleDescriptor(
    SecondBrainModuleId ModuleId,
    string DisplayName,
    string Description,
    bool IsCoreModule,
    bool IsEnabledByDefault);
