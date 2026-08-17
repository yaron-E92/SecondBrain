using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;

namespace SecondBrain.Presentation;

internal sealed record ReviewNavigationTarget(
    string Route,
    IDictionary<string, object> Parameters);

internal static class ReviewNavigation
{
    public static ReviewNavigationTarget? Open(ReviewQueueItem? item)
    {
        if (item is null)
        {
            return null;
        }

        if (item.BrainItemId is { } brainItemId)
        {
            return new ReviewNavigationTarget(
                "//editor",
                new Dictionary<string, object>
                {
                    ["itemId"] = brainItemId.Value.ToString(),
                    ["returnRoute"] = "review",
                });
        }

        var contextKind = item.TargetKind switch
        {
            ReviewTargetKind.Project => ParaContextKind.Project,
            ReviewTargetKind.Area => ParaContextKind.Area,
            _ => throw new InvalidOperationException("Unknown review target."),
        };
        return new ReviewNavigationTarget(
            "//para",
            new Dictionary<string, object>
            {
                ["contextKind"] = contextKind.ToString(),
                ["contextId"] = item.TargetId.ToString(),
                ["returnRoute"] = "review",
            });
    }

    public static ReviewNavigationTarget? Move(ReviewQueueItem? item)
    {
        if (item?.BrainItemId is not { } brainItemId)
        {
            return null;
        }

        return new ReviewNavigationTarget(
            "//para",
            new Dictionary<string, object>
            {
                ["mode"] = "move",
                ["itemId"] = brainItemId.Value.ToString(),
                ["returnRoute"] = "review",
            });
    }
}
