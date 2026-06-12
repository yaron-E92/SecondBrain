namespace SecondBrain.Abstractions.Items;

public sealed record SecondBrainItemSummary(
    SecondBrainItemReference Reference,
    string Title,
    string? Subtitle,
    string? Preview,
    DateTimeOffset? OccurredAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyCollection<string> Tags);
