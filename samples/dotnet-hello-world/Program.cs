var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => new
{
    message = "Hello from DevPod on Azure Kubernetes Service",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName,
    container = Environment.GetEnvironmentVariable("HOSTNAME") ?? "unknown",
    platform = "Azure Kubernetes Service",
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow,
}));

app.MapGet("/info", () => new
{
    application = "DevPod AKS Sample",
    version = "1.0.0",
    dotnetVersion = Environment.Version.ToString(),
    osVersion = Environment.OSVersion.ToString(),
    processorCount = Environment.ProcessorCount,
    workingSet = (Environment.WorkingSet / (1024 * 1024)) + " MB",
    machineName = Environment.MachineName,
    userName = Environment.UserName,
    currentDirectory = Environment.CurrentDirectory,
});

var azureSuggestions = new[]
{
    "Azure Storage",
    "Azure Service Bus",
    "Azure Cosmos DB",
    "Azure Key Vault",
    "Azure App Configuration",
};

app.MapGet("/azure", (IConfiguration configuration) => new
{
    message = "Ready to integrate with Azure services from AKS workspaces",
    suggestions = azureSuggestions,
    kubernetesInfo = new
    {
        resourceGroup = Environment.GetEnvironmentVariable("AKS_RESOURCE_GROUP"),
        clusterName = Environment.GetEnvironmentVariable("AKS_NAME"),
        workspaceId = Environment.GetEnvironmentVariable("DEVPOD_WORKSPACE_ID"),
    },
});

app.Run();
