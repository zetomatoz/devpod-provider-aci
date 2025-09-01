using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Define endpoints
app.MapGet("/", () => new
{
    message = "Hello from DevPod on Azure Container Instances! 🚀",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName,
    container = Environment.GetEnvironmentVariable("HOSTNAME") ?? "unknown",
    platform = "Azure Container Instances"
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow
}));

app.MapGet("/info", () => new
{
    application = "DevPod ACI Sample",
    version = "1.0.0",
    dotnetVersion = Environment.Version.ToString(),
    osVersion = Environment.OSVersion.ToString(),
    processorCount = Environment.ProcessorCount,
    workingSet = Environment.WorkingSet / (1024 * 1024) + " MB",
    machineName = Environment.MachineName,
    userName = Environment.UserName,
    currentDirectory = Environment.CurrentDirectory
});

// Demonstrate Azure integration
app.MapGet("/azure", async (IConfiguration configuration) =>
{
    // This would typically connect to Azure services
    return new
    {
        message = "Ready to integrate with Azure services",
        suggestions = new[]
        {
            "Azure Storage",
            "Azure Service Bus",
            "Azure Cosmos DB",
            "Azure Key Vault",
            "Azure App Configuration"
        },
        containerInfo = new
        {
            resourceGroup = Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP"),
            region = Environment.GetEnvironmentVariable("AZURE_REGION"),
            containerGroup = Environment.GetEnvironmentVariable("MACHINE_ID")
        }
    };
});

app.Run();
