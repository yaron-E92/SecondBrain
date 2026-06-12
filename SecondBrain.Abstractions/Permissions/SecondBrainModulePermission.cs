namespace SecondBrain.Abstractions.Permissions;

public sealed record SecondBrainModulePermission(
    string Key,
    string DisplayName,
    string Description,
    bool IsEnabledByDefault);
