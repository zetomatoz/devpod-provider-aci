namespace DevPod.Provider.ACI.Tests;

public class ProviderManifestTests
{
    [Fact]
    public void ProviderManifest_DoesNotAdvertiseRemoteDockerHostOrPrivateNetworkingOptions()
    {
        var manifest = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "provider.yaml"));

        manifest.Should().NotContain("driver: docker");
        manifest.Should().NotContain("/usr/local/bin/docker");
        manifest.Should().NotContain("ACI_VNET_NAME:");
        manifest.Should().NotContain("ACI_SUBNET_NAME:");
        manifest.Should().Contain("ACI_DNS_LABEL:");
        manifest.Should().Contain("AGENT_PATH");
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "provider.yaml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Unable to locate repository root.");
    }
}
