using SecondBrain.Application.Queries;
using SecondBrain.Application.Services;

namespace SecondBrain.Application.UseCases;

public sealed class GetApplicationStatusUseCase
{
    public ApplicationStatus Handle(GetApplicationStatusQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new ApplicationStatus("SecondBrain", IsReady: true);
    }
}
