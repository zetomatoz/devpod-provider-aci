using System.Reflection;

namespace DevPod.Provider.ACI.Tests;

public class AciServiceMappingTests
{
    private static Type GetTypeStrict(string fullName)
    {
        foreach (var asmName in new[] { "Azure.ResourceManager.ContainerInstance", "Azure.ResourceManager", "Azure.Core" })
        {
            TryLoad(asmName);
            var t = Type.GetType($"{fullName}, {asmName}", throwOnError: false, ignoreCase: false);
            if (t != null)
            {
                return t;
            }
        }

        var resolved = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName, throwOnError: false, ignoreCase: false))
            .FirstOrDefault(t => t != null);
        if (resolved == null)
        {
            throw new InvalidOperationException($"Type not found: {fullName}");
        }
        return resolved;
    }

    private static void TryLoad(string simpleName)
    {
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == simpleName))
        {
            return;
        }
        try
        {
            // Try by simple name
            Assembly.Load(new AssemblyName(simpleName));
            return;
        }
        catch
        {
            // Try load from bin folder
            var candidate = Path.Combine(AppContext.BaseDirectory, simpleName + ".dll");
            if (File.Exists(candidate))
            {
                try { Assembly.LoadFrom(candidate); } catch { /* ignore */ }
            }
        }
    }

    private static object CreateAzureContainerGroupData(string name, string provisioningState = null)
    {
        var azureLocationType = GetTypeStrict("Azure.Core.AzureLocation");
        var containerType = GetTypeStrict("Azure.ResourceManager.ContainerInstance.Models.ContainerInstanceContainer");
        var reqsContentType = GetTypeStrict("Azure.ResourceManager.ContainerInstance.Models.ContainerResourceRequestsContent");
        var reqsType = GetTypeStrict("Azure.ResourceManager.ContainerInstance.Models.ContainerResourceRequirements");
        var osEnumType = GetTypeStrict("Azure.ResourceManager.ContainerInstance.Models.ContainerInstanceOperatingSystemType");
        var groupDataType = GetTypeStrict("Azure.ResourceManager.ContainerInstance.Models.ContainerGroupData");

        var location = Activator.CreateInstance(azureLocationType, "eastus")!;
        var reqsContent = Activator.CreateInstance(reqsContentType, 1.0, 1.0)!;
        var reqs = Activator.CreateInstance(reqsType, reqsContent)!;
        var container = Activator.CreateInstance(containerType, "c1", "ubuntu:latest", reqs)!;
        var containersArray = Array.CreateInstance(containerType, 1);
        containersArray.SetValue(container, 0);
        var osLinux = Enum.Parse(osEnumType, "Linux");

        var cgData = Activator.CreateInstance(groupDataType, location, containersArray, osLinux)!;
        groupDataType.GetProperty("Name")!.SetValue(cgData, name);

        if (provisioningState is not null)
        {
            groupDataType.GetProperty("ProvisioningState")!.SetValue(cgData, provisioningState);
        }

        return cgData;
    }

    [Fact]
    public void MapToContainerStatus_Unknown_WhenNoStates()
    {
        var cgData = CreateAzureContainerGroupData("cg-unknown");
        var method = typeof(DevPod.Provider.ACI.Services.AciService).GetMethod(
            "MapToContainerStatus",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var status = (DevPod.Provider.ACI.Models.ContainerStatus)method!.Invoke(null, new[] { cgData })!;
        status.Name.Should().Be("cg-unknown");
        status.State.Should().Be("Unknown");
        status.ProvisioningState.Should().Be("Unknown");
        status.Containers.Should().ContainKey("c1");
    }

    [Fact]
    public void MapToContainerStatus_Failed_WhenProvisioningFailed()
    {
        var cgData = CreateAzureContainerGroupData("cg-failed", provisioningState: "Failed");
        var method = typeof(DevPod.Provider.ACI.Services.AciService).GetMethod(
            "MapToContainerStatus",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var status = (DevPod.Provider.ACI.Models.ContainerStatus)method!.Invoke(null, new[] { cgData })!;
        status.Name.Should().Be("cg-failed");
        status.State.Should().Be("Failed");
        status.ProvisioningState.Should().Be("Failed");
    }
}

