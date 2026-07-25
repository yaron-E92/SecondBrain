using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SecondBrain.Application;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;

namespace SecondBrain.Persistence.Tests;

[TestFixture]
public sealed class DependencyInjectionRegistrationTests
{
    private readonly List<string> temporaryDirectories = [];

    [Test]
    public void Registrations_use_expected_lifetimes_and_resolve_core_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecondBrainApplication();
        services.AddSecondBrainPersistence(CreateDatabasePath());

        Assert.That(
            FindDescriptor<GetApplicationStatusUseCase>(services).Lifetime,
            Is.EqualTo(ServiceLifetime.Singleton));
        Assert.That(
            FindDescriptor<CoreKnowledgeUseCases>(services).Lifetime,
            Is.EqualTo(ServiceLifetime.Scoped));
        Assert.That(
            FindDescriptor<SecondBrainDbContext>(services).Lifetime,
            Is.EqualTo(ServiceLifetime.Scoped));
        Assert.That(
            FindDescriptor<ICoreKnowledgeRepository>(services).Lifetime,
            Is.EqualTo(ServiceLifetime.Scoped));
        Assert.That(
            FindDescriptor<SecondBrainPersistenceInitializer>(services).Lifetime,
            Is.EqualTo(ServiceLifetime.Singleton));

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var firstUseCases =
            firstScope.ServiceProvider.GetRequiredService<CoreKnowledgeUseCases>();
        Assert.That(
            firstScope.ServiceProvider.GetRequiredService<CoreKnowledgeUseCases>(),
            Is.SameAs(firstUseCases));
        Assert.That(
            secondScope.ServiceProvider.GetRequiredService<CoreKnowledgeUseCases>(),
            Is.Not.SameAs(firstUseCases));
        Assert.That(
            firstScope.ServiceProvider.GetRequiredService<ICoreKnowledgeRepository>(),
            Is.TypeOf<SecondBrainDataStore>());
        Assert.That(
            provider.GetRequiredService<GetApplicationStatusUseCase>(),
            Is.Not.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Persistence_registration_rejects_missing_database_path(string? path)
    {
        var services = new ServiceCollection();

        Assert.That(
            () => services.AddSecondBrainPersistence(path!),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void Initialization_is_explicit_and_creates_the_database()
    {
        var databasePath = CreateDatabasePath();
        using var provider = CreateProvider(databasePath);

        Assert.That(File.Exists(databasePath), Is.False);

        var initializer =
            provider.GetRequiredService<SecondBrainPersistenceInitializer>();
        Assert.That(initializer.IsInitialized, Is.False);
        Assert.That(initializer.TryInitialize(), Is.True);
        Assert.That(initializer.IsInitialized, Is.True);
        Assert.That(initializer.UserFacingError, Is.Null);
        Assert.That(File.Exists(databasePath), Is.True);
    }

    [Test]
    public void Failed_initialization_can_be_retried_after_storage_recovers()
    {
        var parentPath = CreateTemporaryDirectoryPath();
        var databasePath = Path.Combine(parentPath, "secondbrain.db");
        using var provider = CreateProvider(databasePath);
        var initializer =
            provider.GetRequiredService<SecondBrainPersistenceInitializer>();

        Assert.That(initializer.TryInitialize(), Is.False);
        Assert.That(initializer.IsInitialized, Is.False);
        Assert.That(initializer.UserFacingError, Is.Not.Empty);

        Directory.CreateDirectory(parentPath);

        Assert.That(initializer.TryInitialize(), Is.True);
        Assert.That(initializer.IsInitialized, Is.True);
        Assert.That(initializer.UserFacingError, Is.Null);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();

        foreach (var directory in temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }

        temporaryDirectories.Clear();
    }

    private ServiceProvider CreateProvider(string databasePath)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecondBrainApplication();
        services.AddSecondBrainPersistence(databasePath);
        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
    }

    private string CreateDatabasePath()
    {
        var directory = CreateTemporaryDirectoryPath();
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "secondbrain.db");
    }

    private string CreateTemporaryDirectoryPath()
    {
        var directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"secondbrain-di-{Guid.NewGuid():N}");
        temporaryDirectories.Add(directory);
        return directory;
    }

    private static ServiceDescriptor FindDescriptor<TService>(
        IServiceCollection services) =>
        services.Single(descriptor => descriptor.ServiceType == typeof(TService));
}
