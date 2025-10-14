using System.Collections.Generic;
using DevPod.Provider.ACI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevPod.Provider.ACI.Tests;

public class CreateCommandIntegrationTests
{
    [Fact]
    public async Task ExecuteAsync_WithDefaultProviderValues_SucceedsAndUsesDefaults()
    {
        var envOverrides = new Dictionary<string, string?>
        {
            ["AZURE_SUBSCRIPTION_ID"] = "00000000-0000-0000-0000-000000000000",
            ["AZURE_TENANT_ID"] = null,
            ["AZURE_CLIENT_ID"] = null,
            ["AZURE_CLIENT_SECRET"] = null,
            ["AZURE_RESOURCE_GROUP"] = null,
            ["AZURE_REGION"] = null,
            ["ACI_CPU_CORES"] = null,
            ["ACI_MEMORY_GB"] = null,
            ["ACI_GPU_COUNT"] = null,
            ["ACI_RESTART_POLICY"] = null,
            ["ACI_CONTAINER_GROUP_NAME"] = null,
            ["ACI_VNET_NAME"] = null,
            ["ACI_SUBNET_NAME"] = null,
            ["ACI_DNS_LABEL"] = null,
            ["ACR_SERVER"] = null,
            ["ACR_AUTH_MODE"] = null,
            ["ACR_USERNAME"] = null,
            ["ACR_PASSWORD"] = null,
            ["USER_ASSIGNED_IDENTITY_RESOURCE_ID"] = null,
            ["KEYVAULT_URI"] = null,
            ["ACR_USERNAME_SECRET_NAME"] = null,
            ["ACR_PASSWORD_SECRET_NAME"] = null,
            ["ACI_STORAGE_ACCOUNT_NAME"] = null,
            ["ACI_STORAGE_ACCOUNT_KEY"] = null,
            ["ACI_FILE_SHARE_NAME"] = null,
            ["AGENT_PATH"] = null,
            ["INACTIVITY_TIMEOUT"] = null,
            ["INJECT_GIT_CREDENTIALS"] = null,
            ["INJECT_DOCKER_CREDENTIALS"] = null,
            ["MACHINE_ID"] = "Machine-ABC123",
            ["MACHINE_FOLDER"] = null,
            ["WORKSPACE_ID"] = null,
            ["WORKSPACE_UID"] = null,
            ["WORKSPACE_SOURCE"] = "git@github.com:acme/repo.git",
            ["WORKSPACE_IMAGE"] = null,
            ["ACI_PROVIDER_SETUP"] = null,
            ["GIT_USERNAME"] = null,
            ["GIT_TOKEN"] = null,
        };

        using var env = new EnvironmentVariableScope(envOverrides);

        var optionsService = new ProviderOptionsService(NullLogger<ProviderOptionsService>.Instance);
        var expectedOptions = optionsService.GetOptions();
        var expectedContainerName = expectedOptions.GetContainerGroupName();

        ContainerGroupDefinition? capturedDefinition = null;
        var aciServiceMock = new Mock<IAciService>();
        aciServiceMock
            .Setup(s => s.CreateContainerGroupAsync(It.IsAny<ContainerGroupDefinition>()))
            .Callback<ContainerGroupDefinition>(definition => capturedDefinition = definition)
            .ReturnsAsync(new ContainerStatus
            {
                Name = expectedContainerName,
                State = "Running",
                Fqdn = "devpod.acme.example.com",
                IpAddress = "10.1.2.3",
            });

        var secretServiceMock = new Mock<ISecretService>();

        var services = new ServiceCollection();
        services
            .AddSingleton(NullLoggerFactory.Instance)
            .AddLogging()
            .AddSingleton<IProviderOptionsService>(optionsService)
            .AddSingleton(aciServiceMock.Object)
            .AddSingleton(secretServiceMock.Object)
            .AddTransient<CreateCommand>();

        await using var serviceProvider = services.BuildServiceProvider();
        var command = serviceProvider.GetRequiredService<CreateCommand>();

        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);

        try
        {
            var exitCode = await command.ExecuteAsync();
            exitCode.Should().Be(0);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        capturedDefinition.Should().NotBeNull();
        capturedDefinition!.Name.Should().Be(expectedContainerName);
        capturedDefinition.ResourceGroup.Should().Be(Constants.Defaults.ResourceGroupName);
        capturedDefinition.Location.Should().Be(Constants.Defaults.Region);
        capturedDefinition.Image.Should().Be(Constants.Defaults.BaseImage);
        capturedDefinition.CpuCores.Should().Be(Constants.Defaults.CpuCores);
        capturedDefinition.MemoryGb.Should().Be(Constants.Defaults.MemoryGb);
        capturedDefinition.GpuCount.Should().Be(0);
        capturedDefinition.RestartPolicy.Should().Be("Never");
        capturedDefinition.Ports.Should().ContainSingle(p => p == 22);
        capturedDefinition.DnsNameLabel.Should().BeNull();
        capturedDefinition.RegistryCredentials.Should().BeNull();
        capturedDefinition.FileShareVolume.Should().BeNull();
        capturedDefinition.NetworkProfile.Should().BeNull();

        capturedDefinition.EnvironmentVariables.Should().ContainKey("DEVPOD_AGENT_PATH");
        capturedDefinition.EnvironmentVariables["DEVPOD_AGENT_PATH"].Should().Be(Constants.Defaults.AgentPath);
        capturedDefinition.EnvironmentVariables.Should().ContainKey("WORKSPACE_SOURCE");
        capturedDefinition.EnvironmentVariables["WORKSPACE_SOURCE"].Should().Be("git@github.com:acme/repo.git");

        stdout.ToString().Should().Contain("###START_CONTAINER###");
        stdout.ToString().Should().Contain(expectedContainerName);
        stdout.ToString().Should().Contain("Running");
        stderr.ToString().Should().BeEmpty();

        aciServiceMock.Verify(s => s.CreateContainerGroupAsync(It.IsAny<ContainerGroupDefinition>()), Times.Once);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new();

        public EnvironmentVariableScope(IDictionary<string, string?> variables)
        {
            foreach (var kvp in variables)
            {
                _originalValues.TryAdd(kvp.Key, Environment.GetEnvironmentVariable(kvp.Key));
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }
        }

        public void Dispose()
        {
            foreach (var kvp in _originalValues)
            {
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }
        }
    }
}
