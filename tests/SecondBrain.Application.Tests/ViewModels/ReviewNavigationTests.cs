using NUnit.Framework;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation;

namespace SecondBrain.Application.Tests.ViewModels;

[TestFixture]
public sealed class ReviewNavigationTests
{
    [Test]
    public void Open_ResourceItem_RoutesItsBrainItemToEditor()
    {
        var itemId = SecondBrainItemId.New();
        var item = new ReviewQueueItem(
            ReviewTargetKind.Resource,
            itemId.Value,
            "Resource",
            "Details",
            itemId);

        var target = ReviewNavigation.Open(item);

        Assert.Multiple(() =>
        {
            Assert.That(target?.Route, Is.EqualTo("//editor"));
            Assert.That(target?.Parameters["itemId"], Is.EqualTo(itemId.Value.ToString()));
            Assert.That(target?.Parameters["returnRoute"], Is.EqualTo("review"));
        });
    }

    [TestCase(ReviewTargetKind.Project, "Project")]
    [TestCase(ReviewTargetKind.Area, "Area")]
    public void Open_ParaContainer_RoutesToItsWorkspace(
        ReviewTargetKind targetKind,
        string expectedContextKind)
    {
        var targetId = Guid.NewGuid();
        var item = new ReviewQueueItem(
            targetKind,
            targetId,
            "Workspace",
            "Details");

        var target = ReviewNavigation.Open(item);

        Assert.Multiple(() =>
        {
            Assert.That(target?.Route, Is.EqualTo("//para"));
            Assert.That(target?.Parameters["contextKind"], Is.EqualTo(expectedContextKind));
            Assert.That(target?.Parameters["contextId"], Is.EqualTo(targetId.ToString()));
            Assert.That(target?.Parameters["returnRoute"], Is.EqualTo("review"));
        });
    }

    [Test]
    public void Move_BrainItem_RoutesToMoveSelectorAndReturnsToReview()
    {
        var itemId = SecondBrainItemId.New();
        var item = new ReviewQueueItem(
            ReviewTargetKind.InboxItem,
            itemId.Value,
            "Inbox item",
            "Details",
            itemId);

        var target = ReviewNavigation.Move(item);

        Assert.Multiple(() =>
        {
            Assert.That(target?.Route, Is.EqualTo("//para"));
            Assert.That(target?.Parameters["mode"], Is.EqualTo("move"));
            Assert.That(target?.Parameters["itemId"], Is.EqualTo(itemId.Value.ToString()));
            Assert.That(target?.Parameters["returnRoute"], Is.EqualTo("review"));
        });
    }
}
