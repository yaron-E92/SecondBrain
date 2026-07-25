namespace SecondBrain.Application.Ports;

public interface ICoreKnowledgeRepository
{
    Task<CoreKnowledgeState> LoadStateAsync(
        CancellationToken cancellationToken = default);

    Task SaveStateAsync(
        CoreKnowledgeState state,
        CancellationToken cancellationToken = default);
}
