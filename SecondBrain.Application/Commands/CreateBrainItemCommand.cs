using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record CreateBrainItemCommand(BrainItem Item);

public sealed record DeriveBrainItemCommand(
    BrainItem Item,
    IReadOnlyCollection<SecondBrainItemId> SourceCaptureIds,
    bool MarkSourcesReferenced);
