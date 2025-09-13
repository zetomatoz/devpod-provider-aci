namespace DevPod.Provider.ACI.Services;

public interface ISecretService
{
    Task<string?> GetAsync(string secretName, CancellationToken cancellationToken = default);
}

