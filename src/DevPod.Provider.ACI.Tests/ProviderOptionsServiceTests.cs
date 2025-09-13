using Microsoft.Extensions.Logging.Abstractions;

namespace DevPod.Provider.ACI.Tests;

public class ProviderOptionsServiceTests
{
    [Fact]
    public void GetOptions_CachesEnvironmentValues()
    {
        // Arrange
        var logger = NullLogger<ProviderOptionsService>.Instance;
        var svc = new ProviderOptionsService(logger);

        var original = Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP");
        try
        {
            Environment.SetEnvironmentVariable("AZURE_RESOURCE_GROUP", "rg-one");

            // Act
            var first = svc.GetOptions();
            first.AzureResourceGroup.Should().Be("rg-one");

            // Change the environment and verify cache still returns first
            Environment.SetEnvironmentVariable("AZURE_RESOURCE_GROUP", "rg-two");

            var second = svc.GetOptions();
            second.AzureResourceGroup.Should().Be("rg-one");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_RESOURCE_GROUP", original);
        }
    }

    [Fact]
    public void ValidateOptions_ValidOptions_ReturnsTrue()
    {
        var logger = NullLogger<ProviderOptionsService>.Instance;
        var svc = new ProviderOptionsService(logger);
        var options = new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            AciCpuCores = 1.0,
            AciMemoryGb = 2.0,
        };

        var valid = svc.ValidateOptions(options, out var errors);
        valid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateOptions_MissingRequired_ReturnsErrors()
    {
        var logger = NullLogger<ProviderOptionsService>.Instance;
        var svc = new ProviderOptionsService(logger);
        var options = new ProviderOptions
        {
            AzureSubscriptionId = null,
            AzureResourceGroup = "",
            AzureRegion = "",
        };

        var valid = svc.ValidateOptions(options, out var errors);
        valid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("AZURE_SUBSCRIPTION_ID"));
        errors.Should().Contain(e => e.Contains("AZURE_RESOURCE_GROUP"));
        errors.Should().Contain(e => e.Contains("AZURE_REGION"));
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(8.0)]
    public void ValidateOptions_InvalidCpu_ReturnsError(double cpu)
    {
        var logger = NullLogger<ProviderOptionsService>.Instance;
        var svc = new ProviderOptionsService(logger);
        var options = new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            AciCpuCores = cpu,
            AciMemoryGb = 2.0,
        };

        var valid = svc.ValidateOptions(options, out var errors);
        valid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("ACI_CPU_CORES"));
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(32.0)]
    public void ValidateOptions_InvalidMemory_ReturnsError(double mem)
    {
        var logger = NullLogger<ProviderOptionsService>.Instance;
        var svc = new ProviderOptionsService(logger);
        var options = new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            AciCpuCores = 1.0,
            AciMemoryGb = mem,
        };

        var valid = svc.ValidateOptions(options, out var errors);
        valid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("ACI_MEMORY_GB"));
    }

    [Fact]
    public void ValidateOptions_VnetWithoutSubnet_ReturnsError()
    {
        var logger = NullLogger<ProviderOptionsService>.Instance;
        var svc = new ProviderOptionsService(logger);
        var options = new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            AciCpuCores = 1.0,
            AciMemoryGb = 2.0,
            AciVnetName = "vnet",
            AciSubnetName = null,
        };

        var valid = svc.ValidateOptions(options, out var errors);
        valid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("ACI_SUBNET_NAME"));
    }

    [Fact]
    public void ValidateOptions_AcrServerWithoutCreds_ReturnsError_InUsernamePasswordMode()
    {
        var logger = NullLogger<ProviderOptionsService>.Instance;
        var svc = new ProviderOptionsService(logger);
        var options = new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            AciCpuCores = 1.0,
            AciMemoryGb = 2.0,
            AcrServer = "example.azurecr.io",
            AcrUsername = null,
            AcrPassword = null,
            AcrAuthMode = "UsernamePassword",
        };

        var valid = svc.ValidateOptions(options, out var errors);
        valid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("ACR_USERNAME"));
        errors.Should().Contain(e => e.Contains("ACR_PASSWORD"));
    }

    [Fact]
    public void ValidateOptions_AcrServer_NoCreds_Ok_InManagedIdentityMode()
    {
        var svc = new ProviderOptionsService(NullLogger<ProviderOptionsService>.Instance);
        var options = new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            AciCpuCores = 1.0,
            AciMemoryGb = 2.0,
            AcrServer = "example.azurecr.io",
            AcrAuthMode = "ManagedIdentity",
        };

        var valid = svc.ValidateOptions(options, out var errors);
        valid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateOptions_AcrServer_KeyVaultMode_RequiresVaultAndSecretNames()
    {
        var svc = new ProviderOptionsService(NullLogger<ProviderOptionsService>.Instance);
        var options = new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            AciCpuCores = 1.0,
            AciMemoryGb = 2.0,
            AcrServer = "example.azurecr.io",
            AcrAuthMode = "KeyVault",
        };

        var valid = svc.ValidateOptions(options, out var errors);
        valid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("KEYVAULT_URI"));
        errors.Should().Contain(e => e.Contains("ACR_USERNAME_SECRET_NAME"));
        errors.Should().Contain(e => e.Contains("ACR_PASSWORD_SECRET_NAME"));
    }

    [Fact]
    public void ValidateOptions_StorageWithoutKey_ReturnsError()
    {
        var logger = NullLogger<ProviderOptionsService>.Instance;
        var svc = new ProviderOptionsService(logger);
        var options = new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            AciCpuCores = 1.0,
            AciMemoryGb = 2.0,
            AciStorageAccountName = "account",
            AciStorageAccountKey = null,
        };

        var valid = svc.ValidateOptions(options, out var errors);
        valid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("ACI_STORAGE_ACCOUNT_KEY"));
    }
}
