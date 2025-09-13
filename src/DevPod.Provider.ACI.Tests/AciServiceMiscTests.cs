using System.Reflection;

namespace DevPod.Provider.ACI.Tests;

public class AciServiceMiscTests
{
    [Fact]
    public async Task ExecuteViaSSHAsync_ReturnsPlaceholder()
    {
        var method = typeof(AciService).GetMethod(
            "ExecuteViaSSHAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var task = (Task<string>)method!.Invoke(null, [null, "echo hi"])!;
        var result = await task;
        result.Should().Contain("echo hi");
        result.Should().Contain("queued for execution");
    }
}

