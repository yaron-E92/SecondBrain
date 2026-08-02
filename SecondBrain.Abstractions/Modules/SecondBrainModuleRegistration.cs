using Microsoft.Extensions.DependencyInjection;

namespace SecondBrain.Abstractions.Modules;

public sealed record SecondBrainModuleRegistration(
    SecondBrainModuleDescriptor Descriptor,
    Type ModuleType);

public static class SecondBrainModuleRegistrationExtensions
{
    public static IServiceCollection AddSecondBrainModule<TModule>(
        this IServiceCollection services,
        SecondBrainModuleDescriptor descriptor,
        Action<IServiceCollection>? configureServices = null)
        where TModule : class, ISecondBrainModule
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);

        Validate(descriptor.ModuleId);
        ThrowIfDuplicate(services, descriptor.ModuleId);

        var registration = new SecondBrainModuleRegistration(descriptor, typeof(TModule));
        services.AddSingleton(registration);
        services.AddSingleton<TModule>();
        services.AddSingleton<ISecondBrainModule>(provider =>
            provider.GetRequiredService<TModule>());
        configureServices?.Invoke(services);

        return services;
    }

    private static void Validate(SecondBrainModuleId? moduleId)
    {
        if (moduleId is null)
        {
            throw new ArgumentException("A module ID is required.", nameof(moduleId));
        }

        if (moduleId.Id == Guid.Empty)
        {
            throw new ArgumentException("A module ID must contain a non-empty GUID.", nameof(moduleId));
        }

        if (string.IsNullOrWhiteSpace(moduleId.Name))
        {
            throw new ArgumentException("A module ID must contain a name.", nameof(moduleId));
        }
    }

    private static void ThrowIfDuplicate(
        IServiceCollection services,
        SecondBrainModuleId moduleId)
    {
        var registrations = services
            .Where(service => service.ServiceType == typeof(SecondBrainModuleRegistration))
            .Select(service => service.ImplementationInstance)
            .OfType<SecondBrainModuleRegistration>();

        if (registrations.Any(registration => registration.Descriptor.ModuleId.Id == moduleId.Id))
        {
            throw new InvalidOperationException(
                $"A module with ID '{moduleId.Id}' is already registered.");
        }

        if (registrations.Any(registration => string.Equals(
                registration.Descriptor.ModuleId.Name,
                moduleId.Name,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"A module named '{moduleId.Name}' is already registered.");
        }
    }
}
