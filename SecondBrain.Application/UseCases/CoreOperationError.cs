namespace SecondBrain.Application.UseCases;

public sealed record CoreOperationError(
    CoreOperationErrorCode Code,
    string Message);
