namespace DevPod.Provider.ACI.Tests;

public class ProviderOptionsTests
{
    [Fact]
    public void GetContainerGroupName_MachineProvider_UsesMachineId()
    {
        var options = new ProviderOptions
        {
            ProviderSetup = "Machine",
            MachineId = "Machine_One",
        };

        var name = options.GetContainerGroupName();
        name.Should().Be("devpod-machine-one");
    }

    [Fact]
    public void GetContainerGroupName_WorkspaceProvider_UsesWorkspaceUid()
    {
        var options = new ProviderOptions
        {
            ProviderSetup = "Workspace",
            WorkspaceUid = "WS-1234",
        };

        var name = options.GetContainerGroupName();
        name.Should().Be("devpod-ws-ws-1234");
    }

    [Fact]
    public void GetContainerGroupName_RespectsOverride()
    {
        var options = new ProviderOptions
        {
            ProviderSetup = "Workspace",
            WorkspaceUid = "ignored",
            AciContainerGroupName = "Custom-Name",
        };

        var name = options.GetContainerGroupName();
        name.Should().Be("custom-name");
    }
}
