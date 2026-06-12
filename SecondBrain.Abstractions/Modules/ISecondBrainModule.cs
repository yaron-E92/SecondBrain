namespace SecondBrain.Abstractions.Modules;

public interface ISecondBrainModule
{
    SecondBrainModuleId Id { get; }
    string DisplayName { get; }
    string Description { get; }

    IReadOnlyCollection<SecondBrainModuleCapability> Capabilities { get; }

    Task InitializeAsync(
    SecondBrainModuleContext context,
    CancellationToken cancellationToken);
}
