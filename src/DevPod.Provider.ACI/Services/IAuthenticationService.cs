using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

namespace DevPod.Provider.ACI.Services;

public interface IAuthenticationService
{
    Task<ArmClient> GetArmClientAsync();
    TokenCredential GetCredential();
    SubscriptionResource GetSubscriptionResource();
}
