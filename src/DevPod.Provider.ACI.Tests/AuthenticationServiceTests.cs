using Azure.Identity;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevPod.Provider.ACI.Tests;

public class AuthenticationServiceTests
{
    [Fact]
    public void GetCredential_UsesClientSecret_WhenServicePrincipalProvided()
    {
        // Arrange
        var options = new ProviderOptions
        {
            AzureClientId = "client-id",
            AzureClientSecret = "client-secret",
            AzureTenantId = "tenant-id",
        };
        var optionsSvc = new Mock<IProviderOptionsService>();
        optionsSvc.Setup(s => s.GetOptions()).Returns(options);

        var svc = new AuthenticationService(
            NullLogger<AuthenticationService>.Instance,
            optionsSvc.Object);

        // Act
        var cred = svc.GetCredential();

        // Assert
        cred.Should().BeOfType<ClientSecretCredential>();
    }

    [Fact]
    public void GetCredential_UsesDefaultCredential_WhenNoServicePrincipal()
    {
        // Arrange
        var options = new ProviderOptions();
        var optionsSvc = new Mock<IProviderOptionsService>();
        optionsSvc.Setup(s => s.GetOptions()).Returns(options);

        var svc = new AuthenticationService(
            NullLogger<AuthenticationService>.Instance,
            optionsSvc.Object);

        // Act
        var cred = svc.GetCredential();

        // Assert
        cred.Should().BeOfType<DefaultAzureCredential>();
    }

    [Fact]
    public async Task GetArmClientAsync_IsCached()
    {
        var options = new ProviderOptions();
        var optionsSvc = new Mock<IProviderOptionsService>();
        optionsSvc.Setup(s => s.GetOptions()).Returns(options);

        var svc = new AuthenticationService(
            NullLogger<AuthenticationService>.Instance,
            optionsSvc.Object);

        var c1 = await svc.GetArmClientAsync();
        var c2 = await svc.GetArmClientAsync();

        ReferenceEquals(c1, c2).Should().BeTrue();
    }

    [Fact]
    public void GetSubscriptionResource_UsesConfiguredSubscriptionId()
    {
        var options = new ProviderOptions
        {
            AzureSubscriptionId = "00000000-0000-0000-0000-000000000123",
        };

        var optionsSvc = new Mock<IProviderOptionsService>();
        optionsSvc.Setup(s => s.GetOptions()).Returns(options);

        var svc = new AuthenticationService(
            NullLogger<AuthenticationService>.Instance,
            optionsSvc.Object);

        var subscription = svc.GetSubscriptionResource();

        subscription.Id.Should().Be(
            SubscriptionResource.CreateResourceIdentifier(options.AzureSubscriptionId));
    }
}
