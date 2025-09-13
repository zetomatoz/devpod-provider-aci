namespace DevPod.Provider.ACI.Tests;

public class ProviderOptionsFromEnvironmentTests
{
    [Fact]
    public void FromEnvironment_UsesDefaults_WhenNotSet()
    {
        var snapshot = new EnvSnapshot();
        snapshot.Clear(
            "AZURE_SUBSCRIPTION_ID", "AZURE_TENANT_ID", "AZURE_CLIENT_ID", "AZURE_CLIENT_SECRET",
            "AZURE_RESOURCE_GROUP", "AZURE_REGION", "ACI_CPU_CORES", "ACI_MEMORY_GB", "ACI_GPU_COUNT",
            "ACI_RESTART_POLICY", "ACI_VNET_NAME", "ACI_SUBNET_NAME", "ACI_DNS_LABEL",
            "ACR_SERVER", "ACR_USERNAME", "ACR_PASSWORD",
            "ACI_STORAGE_ACCOUNT_NAME", "ACI_STORAGE_ACCOUNT_KEY", "ACI_FILE_SHARE_NAME",
            "AGENT_PATH", "INACTIVITY_TIMEOUT", "INJECT_GIT_CREDENTIALS", "INJECT_DOCKER_CREDENTIALS",
            "MACHINE_ID", "MACHINE_FOLDER", "WORKSPACE_ID", "WORKSPACE_UID");

        var opts = ProviderOptions.FromEnvironment();
        opts.AzureResourceGroup.Should().Be("devpod-aci-rg");
        opts.AzureRegion.Should().Be("eastus");
        opts.AciCpuCores.Should().Be(2.0);
        opts.AciMemoryGb.Should().Be(4.0);
        opts.AciRestartPolicy.Should().Be("Never");
        opts.AgentPath.Should().Be("/home/devpod/.devpod");
        opts.InactivityTimeout.Should().Be("30m");
        opts.AciFileShareName.Should().Be("devpod-workspace");
    }

    [Fact]
    public void FromEnvironment_ParsesNumericAndBoolValues()
    {
        var snapshot = new EnvSnapshot();
        snapshot.Set(new()
        {
            ["AZURE_RESOURCE_GROUP"] = "rgX",
            ["AZURE_REGION"] = "westus2",
            ["ACI_CPU_CORES"] = "2.5",
            ["ACI_MEMORY_GB"] = "7.5",
            ["ACI_GPU_COUNT"] = "1",
            ["ACI_RESTART_POLICY"] = "Always",
            ["AGENT_PATH"] = "/agent",
            ["INACTIVITY_TIMEOUT"] = "60m",
            ["INJECT_GIT_CREDENTIALS"] = "false",
            ["INJECT_DOCKER_CREDENTIALS"] = "true",
            ["ACI_FILE_SHARE_NAME"] = "share1",
        });

        var opts = ProviderOptions.FromEnvironment();
        opts.AzureResourceGroup.Should().Be("rgX");
        opts.AzureRegion.Should().Be("westus2");
        opts.AciCpuCores.Should().Be(2.5);
        opts.AciMemoryGb.Should().Be(7.5);
        opts.AciGpuCount.Should().Be(1);
        opts.AciRestartPolicy.Should().Be("Always");
        opts.AgentPath.Should().Be("/agent");
        opts.InactivityTimeout.Should().Be("60m");
        opts.InjectGitCredentials.Should().BeFalse();
        opts.InjectDockerCredentials.Should().BeTrue();
        opts.AciFileShareName.Should().Be("share1");
    }

    [Fact]
    public void GetContainerGroupName_UsesMachineId_AndSanitizes()
    {
        var opts = new ProviderOptions { MachineId = "ABC_DEF" };
        var name = opts.GetContainerGroupName();
        name.Should().StartWith("devpod-");
        name.Should().Be("devpod-abc-def");

        // When no machine id, generates guid-based
        var opts2 = new ProviderOptions { MachineId = null };
        var name2 = opts2.GetContainerGroupName();
        name2.Should().StartWith("devpod-");
        name2.Length.Should().Be(15); // devpod- (7) + 8 chars
    }

    private sealed class EnvSnapshot : IDisposable
    {
        private readonly Dictionary<string, string?> _saved = new();

        public void Clear(params string[] keys)
        {
            foreach (var k in keys)
            {
                _saved[k] = Environment.GetEnvironmentVariable(k);
                Environment.SetEnvironmentVariable(k, null);
            }
        }

        public void Set(Dictionary<string, string> pairs)
        {
            foreach (var kv in pairs)
            {
                _saved[kv.Key] = Environment.GetEnvironmentVariable(kv.Key);
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
        }

        public void Dispose()
        {
            foreach (var kv in _saved)
            {
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
        }
    }
}
