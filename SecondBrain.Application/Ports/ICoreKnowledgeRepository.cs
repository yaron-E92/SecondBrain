using SecondBrain.Domain.Entities;

namespace SecondBrain.Application.Ports;

public interface ICoreKnowledgeRepository
{
    Task<CoreKnowledgeState> LoadStateAsync(
        CancellationToken cancellationToken = default);

    Task SaveStateAsync(
        CoreKnowledgeState state,
        CancellationToken cancellationToken = default);
}

public sealed record CoreKnowledgeState(
    IReadOnlyList<Project> Projects,
    IReadOnlyList<Area> Areas,
    IReadOnlyList<ResourceTopic> ResourceTopics,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<BrainItem> BrainItems,
    IReadOnlyList<Journal> Journals);
