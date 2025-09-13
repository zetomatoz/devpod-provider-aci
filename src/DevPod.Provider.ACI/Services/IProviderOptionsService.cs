using DevPod.Provider.ACI.Models;

namespace DevPod.Provider.ACI.Services;

public interface IProviderOptionsService
{
    ProviderOptions GetOptions();
    bool ValidateOptions(ProviderOptions options, out List<string> errors);
}
