using SecondBrain.Domain.ValueObjects;
using NUnit.Framework;

namespace SecondBrain.Domain.Tests.ValueObjects;

[TestFixture]
public sealed class SecondBrainItemIdTests
{
    [Test]
    public void Constructor_ThrowsForEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => _ = new SecondBrainItemId(Guid.Empty));
    }

    [Test]
    public void Constructor_StoresNonEmptyGuid()
    {
        var value = Guid.NewGuid();

        var id = new SecondBrainItemId(value);

        Assert.That(id.Value, Is.EqualTo(value));
    }

    [Test]
    public void New_CreatesNonEmptyId()
    {
        var id = SecondBrainItemId.New();

        Assert.That(id.Value, Is.Not.EqualTo(Guid.Empty));
    }
}
