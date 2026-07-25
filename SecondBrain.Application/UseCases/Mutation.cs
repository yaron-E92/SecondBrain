using SecondBrain.Application.Ports;

namespace SecondBrain.Application.UseCases;

internal sealed record Mutation<T>(
    CoreKnowledgeState? State,
    CoreOperationResult<T> Result)
{
    public static Mutation<T> Succeeded(
        CoreKnowledgeState state,
        T value) =>
        new(state, CoreOperationResult<T>.Success(value));

    public static Mutation<T> Failed(
        CoreOperationErrorCode code,
        string message) =>
        new(null, CoreOperationResult<T>.Failure(code, message));

    public static Mutation<T> Failed(CoreOperationError error) =>
        Failed(error.Code, error.Message);

    public static Mutation<T> NotFound(string entityName, Guid id) =>
        Failed(
            CoreOperationErrorCode.NotFound,
            $"{entityName} '{id}' was not found.");
}
