using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevPod.Provider.ACI.Tests;

public class CreateCommandRegistryAuthTests
{
    private static (ServiceProvider sp, Func<ContainerGroupDefinition?> getCaptured) BuildProvider(
        ProviderOptions options,
        string? keyVaultUser = null,
        string? keyVaultPass = null)
    {
        ContainerGroupDefinition? capturedDefinition = null;

        var optionsSvc = new Mock<IProviderOptionsService>();
        optionsSvc.Setup(o => o.GetOptions()).Returns(options);

        var aciSvc = new Mock<IAciService>();
        aciSvc.Setup(a => a.CreateContainerGroupAsync(It.IsAny<ContainerGroupDefinition>()))
            .Callback<ContainerGroupDefinition>(def => capturedDefinition = def)
            .ReturnsAsync(new ContainerStatus { Name = options.GetContainerGroupName(), State = "Running" });

        var secretSvc = new Mock<ISecretService>();
        secretSvc.Setup(s => s.GetAsync(It.Is<string>(n => n.Contains("user", StringComparison.OrdinalIgnoreCase)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(keyVaultUser);
        secretSvc.Setup(s => s.GetAsync(It.Is<string>(n => n.Contains("pass", StringComparison.OrdinalIgnoreCase) || n.Contains("password", StringComparison.OrdinalIgnoreCase)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(keyVaultPass);

        var services = new ServiceCollection();
        services
            .AddSingleton(NullLoggerFactory.Instance)
            .AddLogging()
            .AddSingleton(optionsSvc.Object)
            .AddSingleton(new Mock<IAuthenticationService>().Object)
            .AddSingleton(aciSvc.Object)
            .AddSingleton(secretSvc.Object)
            .AddTransient<CreateCommand>();

        var sp = services.BuildServiceProvider();
        return (sp, () => capturedDefinition);
    }

    [Fact]
    public async Task ManagedIdentity_Mode_DoesNotAttachRegistryCredentials()
    {
        var opts = new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            AcrServer = "example.azurecr.io",
            AcrAuthMode = "ManagedIdentity",
            MachineId = "machine-xyz",
        };

        var (sp, get) = BuildProvider(opts);
        var cmd = sp.GetRequiredService<CreateCommand>();
        var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        var rc = await cmd.ExecuteAsync();
        Console.SetOut(originalOut);
        rc.Should().Be(0);
        var captured = get();
        captured.Should().NotBeNull();
        captured!.RegistryCredentials.Should().BeNull();
    }

    [Fact]
    public async Task KeyVault_Mode_AttachesCredentials_FromSecretService()
    {
        var opts = new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            AcrServer = "example.azurecr.io",
            AcrAuthMode = "KeyVault",
            AcrUsernameSecretName = "acr-user",
            AcrPasswordSecretName = "acr-pass",
            MachineId = "machine-xyz",
        };

        var (sp, get) = BuildProvider(opts, keyVaultUser: "u1", keyVaultPass: "p1");
        var cmd = sp.GetRequiredService<CreateCommand>();
        var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        var rc = await cmd.ExecuteAsync();
        Console.SetOut(originalOut);
        rc.Should().Be(0);
        var captured = get();
        captured.Should().NotBeNull();
        captured!.RegistryCredentials.Should().NotBeNull();
        captured.RegistryCredentials!.Username.Should().Be("u1");
        captured.RegistryCredentials.Password.Should().Be("p1");
    }

    [Fact]
    public async Task UsernamePassword_Mode_AttachesCredentials_FromOptions()
    {
        var opts = new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            AcrServer = "example.azurecr.io",
            AcrAuthMode = "UsernamePassword",
            AcrUsername = "user",
            AcrPassword = "pass",
            MachineId = "machine-xyz",
        };

        var (sp, get) = BuildProvider(opts);
        var cmd = sp.GetRequiredService<CreateCommand>();
        var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        var rc = await cmd.ExecuteAsync();
        Console.SetOut(originalOut);
        rc.Should().Be(0);
        var captured = get();
        captured.Should().NotBeNull();
        captured!.RegistryCredentials.Should().NotBeNull();
        captured.RegistryCredentials!.Username.Should().Be("user");
        captured.RegistryCredentials.Password.Should().Be("pass");
    }
}
