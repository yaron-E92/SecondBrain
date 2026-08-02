namespace SecondBrain.Application.UseCases;

public sealed record CoreOperationResult<T>(
    T? Value,
    CoreOperationError? Error)
{
    public bool IsSuccess => Error is null;

    public static CoreOperationResult<T> Success(T value) =>
        new(value, null);

    public static CoreOperationResult<T> Failure(
        CoreOperationErrorCode code,
        string message) =>
        new(default, new CoreOperationError(code, message));
}
