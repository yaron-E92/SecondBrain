using SecondBrain.Domain.Entities;

namespace SecondBrain.Application.Ports;

public sealed record CoreKnowledgeState(
    IReadOnlyList<Project> Projects,
    IReadOnlyList<Area> Areas,
    IReadOnlyList<ResourceTopic> ResourceTopics,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<BrainItem> BrainItems,
    IReadOnlyList<Journal> Journals,
    IReadOnlyList<ReviewState>? ReviewStates = null);
