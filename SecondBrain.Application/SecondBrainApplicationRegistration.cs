using Microsoft.Extensions.DependencyInjection;
using SecondBrain.Application.NotionAudit;
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
        services.AddScoped<ReviewUseCase>();
        services.AddSingleton<NotionParityAuditUseCase>();
        services.AddSingleton<NotionImportUseCase>();
        return services;
    }
}
