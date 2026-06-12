namespace SecondBrain.Abstractions.Modules;

public static class WellKnownSecondBrainModuleDescriptors
{
    public static readonly SecondBrainModuleDescriptor Core =
        new(
            ModuleId: WellKnownSecondBrainModules.SecondBrainCore,
            DisplayName: "SecondBrain Core",
            Description: "Core functionalities of the SecondBrain system.",
            IsCoreModule: true,
            IsEnabledByDefault: false);

    public static readonly SecondBrainModuleDescriptor Phoodab =
        new(
            ModuleId: WellKnownSecondBrainModules.Phoodab,
            DisplayName: "PHOODAB",
            Description: "Pantry, household inventory, replenishment, shopping, locations, and durable items.",
            IsCoreModule: false,
            IsEnabledByDefault: false);

    public static readonly SecondBrainModuleDescriptor ShuffleTask =
        new(
            ModuleId: WellKnownSecondBrainModules.ShuffleTask,
            DisplayName: "Shuffle Task",
            Description: "Module for managing and shuffling tasks.",
            IsCoreModule: false,
            IsEnabledByDefault: false);

    public static readonly SecondBrainModuleDescriptor SurvivalGarden =
        new(
            ModuleId: WellKnownSecondBrainModules.SurvivalGarden,
            DisplayName: "Survival Garden",
            Description: "Module for managing and maintaining a survival garden.",
            IsCoreModule: false,
            IsEnabledByDefault: false);
}
