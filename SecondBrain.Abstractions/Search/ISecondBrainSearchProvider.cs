namespace SecondBrain.Abstractions.Search;

public sealed record SecondBrainSearchRequest(string QueryText);

public sealed record SecondBrainSearchResult(
    string Id,
    string Title,
    string Preview,
    string OpenRoute);

public interface ISecondBrainSearchProvider
{
    string SourceId { get; }

    string DisplayName { get; }

    Task<IReadOnlyList<SecondBrainSearchResult>> SearchAsync(
        SecondBrainSearchRequest request,
        CancellationToken cancellationToken);
}
