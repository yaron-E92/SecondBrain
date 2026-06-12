using SecondBrain.Abstractions.Modules;

namespace SecondBrain.Abstractions.Navigation;

public sealed record SecondBrainNavigationItem(
    SecondBrainModuleId ModuleId,
    string Route,
    string Title,
    string? IconKey,
    int SortOrder);
