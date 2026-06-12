using System.Reflection;
using NUnit.Framework;

namespace SecondBrain.Persistence.Tests;

[TestFixture]
public sealed class PersistenceAssemblyTests
{
    [Test]
    public void PersistenceAssembly_LoadsWithoutExternalServices()
    {
        var assembly = Assembly.Load("SecondBrain.Persistence");

        Assert.That(assembly.GetName().Name, Is.EqualTo("SecondBrain.Persistence"));
    }
}
