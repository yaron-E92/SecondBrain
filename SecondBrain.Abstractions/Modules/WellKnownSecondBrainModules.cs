namespace SecondBrain.Abstractions.Modules;

public static class WellKnownSecondBrainModules
{
    public static readonly SecondBrainModuleId SecondBrainCore =
        new(new Guid("b3049ecd-8114-5197-9529-32a47a79eb22"), "secondbrain.core");

    public static readonly SecondBrainModuleId ShuffleTask =
        new(new Guid("a56641eb-f2a2-5840-8dee-0790a72bcf19"), "shuffletask");

    public static readonly SecondBrainModuleId Phoodab =
        new(new Guid("d4ab93ef-9c8f-5bac-82d8-837c37881c21"), "phoodab");

    public static readonly SecondBrainModuleId SurvivalGarden =
        new(new Guid("48146d84-3866-51a7-9713-4f4686db25f1"), "survivalgarden");

    public static IReadOnlyList<SecondBrainModuleId> All { get; } =
    [
        SecondBrainCore,
        ShuffleTask,
        Phoodab,
        SurvivalGarden
    ];
}
