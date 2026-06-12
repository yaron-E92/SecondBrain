namespace SecondBrain.Abstractions.Modules;

public static class WellKnownSecondBrainModules
{
    public static readonly SecondBrainModuleId SecondBrainCore =
        new(new Guid("PUT-STABLE-GUID-HERE"), "secondbrain.core");

    public static readonly SecondBrainModuleId ShuffleTask =
        new(new Guid("PUT-STABLE-GUID-HERE"), "shuffletask");

    public static readonly SecondBrainModuleId Phoodab =
        new(new Guid("PUT-STABLE-GUID-HERE"), "phoodab");

    public static readonly SecondBrainModuleId SurvivalGarden =
        new(new Guid("PUT-STABLE-GUID-HERE"), "survivalgarden");

    public static IReadOnlyList<SecondBrainModuleId> All { get; } =
    [
        SecondBrainCore,
        ShuffleTask,
        Phoodab,
        SurvivalGarden
    ];
}
