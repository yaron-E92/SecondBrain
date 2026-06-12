using SecondBrain.Abstractions.Modules;

namespace SecondBrain.Abstractions.Commands;

public sealed record SecondBrainCommandDescriptor(
    string Id,
    string Title,
    string? Description,
    SecondBrainModuleId ModuleId);
