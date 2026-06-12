using SecondBrain.Application.Queries;
using SecondBrain.Application.UseCases;
using NUnit.Framework;

namespace SecondBrain.Application.Tests.UseCases;

[TestFixture]
public sealed class GetApplicationStatusUseCaseTests
{
    [Test]
    public void Handle_ReturnsReadyStatus()
    {
        var useCase = new GetApplicationStatusUseCase();

        var status = useCase.Handle(new GetApplicationStatusQuery());

        Assert.Multiple(() =>
        {
            Assert.That(status.Name, Is.EqualTo("SecondBrain"));
            Assert.That(status.IsReady, Is.True);
        });
    }

    [Test]
    public void Handle_ThrowsForNullQuery()
    {
        var useCase = new GetApplicationStatusUseCase();

        Assert.Throws<ArgumentNullException>(() => useCase.Handle(null!));
    }
}
