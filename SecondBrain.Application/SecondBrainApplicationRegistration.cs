using Microsoft.Extensions.DependencyInjection;
using SecondBrain.Application.UseCases;

namespace SecondBrain.Application;

public static class SecondBrainApplicationRegistration
{
    public static IServiceCollection AddSecondBrainApplication(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<GetApplicationStatusUseCase>();
        services.AddScoped<CoreKnowledgeUseCases>();
        return services;
    }
}
