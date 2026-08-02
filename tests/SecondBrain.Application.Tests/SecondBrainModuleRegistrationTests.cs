using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SecondBrain.Abstractions.Modules;

namespace SecondBrain.Application.Tests;

[TestFixture]
public sealed class SecondBrainModuleRegistrationTests
{
    [SetUp]
    public void SetUp()
    {
        TestModule.ConstructionCount = 0;
        TestModule.InitializationCount = 0;
    }

    [Test]
    public void Module_registration_adds_metadata_module_and_capabilities()
    {
        var services = new ServiceCollection();
        var descriptor = CreateDescriptor(Guid.NewGuid(), "test.module");

        services.AddSecondBrainModule<TestModule>(
            descriptor,
            moduleServices => moduleServices.AddSingleton<TestCapability>());

        using var provider = services.BuildServiceProvider();

        var registration = provider.GetRequiredService<SecondBrainModuleRegistration>();
        Assert.That(registration.Descriptor, Is.SameAs(descriptor));
        Assert.That(registration.ModuleType, Is.EqualTo(typeof(TestModule)));
        Assert.That(provider.GetRequiredService<TestCapability>(), Is.Not.Null);
        Assert.That(
            provider.GetRequiredService<ISecondBrainModule>(),
            Is.SameAs(provider.GetRequiredService<TestModule>()));
    }

    [Test]
    public void Duplicate_module_guid_fails_deterministically()
    {
        var services = new ServiceCollection();
        var moduleGuid = Guid.NewGuid();
        services.AddSecondBrainModule<TestModule>(
            CreateDescriptor(moduleGuid, "first.module"));

        Assert.That(
            () => services.AddSecondBrainModule<OtherTestModule>(
                CreateDescriptor(moduleGuid, "second.module")),
            Throws.TypeOf<InvalidOperationException>()
                .With.Message.EqualTo($"A module with ID '{moduleGuid}' is already registered."));
    }

    [Test]
    public void Duplicate_module_name_fails_case_insensitively()
    {
        var services = new ServiceCollection();
        services.AddSecondBrainModule<TestModule>(
            CreateDescriptor(Guid.NewGuid(), "test.module"));

        Assert.That(
            () => services.AddSecondBrainModule<OtherTestModule>(
                CreateDescriptor(Guid.NewGuid(), "TEST.MODULE")),
            Throws.TypeOf<InvalidOperationException>()
                .With.Message.EqualTo("A module named 'TEST.MODULE' is already registered."));
    }

    [TestCase("empty-guid")]
    [TestCase("missing-name")]
    public void Invalid_module_id_is_rejected(string invalidPart)
    {
        var services = new ServiceCollection();
        var moduleId = invalidPart == "empty-guid"
            ? new SecondBrainModuleId(Guid.Empty, "test.module")
            : new SecondBrainModuleId(Guid.NewGuid(), " ");

        Assert.That(
            () => services.AddSecondBrainModule<TestModule>(CreateDescriptor(moduleId)),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Registering_disabled_module_does_not_construct_or_initialize_it()
    {
        var services = new ServiceCollection();
        services.AddSecondBrainModule<TestModule>(
            CreateDescriptor(Guid.NewGuid(), "disabled.module", isEnabledByDefault: false));

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true });

        Assert.That(TestModule.ConstructionCount, Is.Zero);
        Assert.That(TestModule.InitializationCount, Is.Zero);
    }

    private static SecondBrainModuleDescriptor CreateDescriptor(
        Guid id,
        string name,
        bool isEnabledByDefault = false) =>
        CreateDescriptor(new SecondBrainModuleId(id, name), isEnabledByDefault);

    private static SecondBrainModuleDescriptor CreateDescriptor(
        SecondBrainModuleId moduleId,
        bool isEnabledByDefault = false) =>
        new(
            moduleId,
            "Test module",
            "Test module description.",
            IsCoreModule: false,
            IsEnabledByDefault: isEnabledByDefault);

    private sealed class TestCapability;

    private sealed class TestModule : ISecondBrainModule
    {
        public TestModule()
        {
            ConstructionCount++;
        }

        public static int ConstructionCount { get; set; }
        public static int InitializationCount { get; set; }
        public SecondBrainModuleId Id => new(Guid.NewGuid(), "test.module");
        public string DisplayName => "Test module";
        public string Description => "Test module description.";
        public IReadOnlyCollection<SecondBrainModuleCapability> Capabilities => [];

        public Task InitializeAsync(
            SecondBrainModuleContext context,
            CancellationToken cancellationToken)
        {
            InitializationCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class OtherTestModule : ISecondBrainModule
    {
        public SecondBrainModuleId Id => new(Guid.NewGuid(), "other.module");
        public string DisplayName => "Other test module";
        public string Description => "Other test module description.";
        public IReadOnlyCollection<SecondBrainModuleCapability> Capabilities => [];

        public Task InitializeAsync(
            SecondBrainModuleContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
