using SecondBrain.Domain.Entities;

namespace SecondBrain.Persistence;

public sealed record SecondBrainDataSnapshot(
    IReadOnlyList<Project> Projects,
    IReadOnlyList<Area> Areas,
    IReadOnlyList<ResourceTopic> ResourceTopics,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<BrainItem> BrainItems,
    IReadOnlyList<Journal> Journals);
