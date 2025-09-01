using Azure.Core;
using Azure.ResourceManager;

namespace DevPod.Provider.ACI.Services;

public interface IAuthenticationService
{
    Task<ArmClient> GetArmClientAsync();
    TokenCredential GetCredential();
}