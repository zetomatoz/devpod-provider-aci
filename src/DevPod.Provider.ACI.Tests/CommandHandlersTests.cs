using System.IO;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevPod.Provider.ACI.Tests;

public class CommandHandlersTests
{
    private static ServiceProvider BuildProvider(
        Mock<IProviderOptionsService>? optionsMock = null,
        Mock<IAciService>? aciMock = null,
        Mock<IAuthenticationService>? authMock = null,
        ILoggerFactory? loggerFactory = null,
        bool configureDefaultValidation = true)
    {
        optionsMock ??= new Mock<IProviderOptionsService>();
        aciMock ??= new Mock<IAciService>();
        authMock ??= new Mock<IAuthenticationService>();
        loggerFactory ??= NullLoggerFactory.Instance;
        if (configureDefaultValidation)
        {
            var noErrors = new List<string>();
            optionsMock.Setup(o => o.ValidateOptions(It.IsAny<ProviderOptions>(), out noErrors))
                .Returns(true);
        }

        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddLogging();

        services
            .AddSingleton(new Mock<ISecretService>().Object)
            .AddSingleton(optionsMock.Object)
            .AddSingleton(authMock.Object)
            .AddSingleton(aciMock.Object)
            .AddTransient<InitCommand>()
            .AddTransient<CreateCommand>()
            .AddTransient<DeleteCommand>()
            .AddTransient<StartCommand>()
            .AddTransient<StopCommand>()
            .AddTransient<StatusCommand>()
            .AddTransient<ExecCommand>();

        return services.BuildServiceProvider();
    }

    private static ProviderOptions DefaultOptions()
    {
        return new ProviderOptions
        {
            AzureSubscriptionId = "sub",
            AzureResourceGroup = "rg",
            AzureRegion = "eastus",
            MachineId = "machine-1234",
        };
    }

    [Fact]
    public async Task Router_UnknownCommand_ReturnsError()
    {
        var sp = BuildProvider();
        var router = new CommandRouter(sp);
        var rc = await router.RouteAsync(["unknown"]);
        rc.Should().Be(1);
    }

    [Fact]
    public async Task CreateCommand_Success_PrintsDetails()
    {
        var optionsMock = new Mock<IProviderOptionsService>();
        optionsMock.Setup(o => o.GetOptions()).Returns(DefaultOptions());

        var aciMock = new Mock<IAciService>();
        aciMock.Setup(a => a.CreateContainerGroupAsync(It.IsAny<ContainerGroupDefinition>()))
            .ReturnsAsync(new ContainerStatus
            {
                Name = "devpod-machine-1234",
                State = "Running",
                Fqdn = "host.example",
                IpAddress = "1.2.3.4",
            });

        var sp = BuildProvider(optionsMock, aciMock);
        var router = new CommandRouter(sp);

        var originalImage = Environment.GetEnvironmentVariable("WORKSPACE_IMAGE");
        var originalSource = Environment.GetEnvironmentVariable("WORKSPACE_SOURCE");
        var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Environment.SetEnvironmentVariable("WORKSPACE_IMAGE", "ghcr.io/acme/devpod-provider-aci-hello-world:latest");
            Environment.SetEnvironmentVariable("WORKSPACE_SOURCE", null);
            Console.SetOut(sw);
            var rc = await router.RouteAsync([Constants.Commands.Create]);
            rc.Should().Be(0);
            var output = sw.ToString();
            output.Should().Contain("###START_CONTAINER###");
            output.Should().Contain("Name: devpod-machine-1234");
            output.Should().Contain("Status: Running");
            output.Should().MatchRegex("FQDN:|IP:");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WORKSPACE_IMAGE", originalImage);
            Environment.SetEnvironmentVariable("WORKSPACE_SOURCE", originalSource);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task StartCommand_Success_PrintsStatus()
    {
        var optionsMock = new Mock<IProviderOptionsService>();
        optionsMock.Setup(o => o.GetOptions()).Returns(DefaultOptions());

        var aciMock = new Mock<IAciService>();
        aciMock.Setup(a => a.StartContainerGroupAsync(It.IsAny<string>()))
            .ReturnsAsync(new ContainerStatus { State = "Running" });

        var sp = BuildProvider(optionsMock, aciMock);
        var router = new CommandRouter(sp);

        var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            var rc = await router.RouteAsync([Constants.Commands.Start]);
            rc.Should().Be(0);
            sw.ToString().Should().Contain("Status: Running");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task StopCommand_Success_PrintsStatus()
    {
        var optionsMock = new Mock<IProviderOptionsService>();
        optionsMock.Setup(o => o.GetOptions()).Returns(DefaultOptions());

        var aciMock = new Mock<IAciService>();
        aciMock.Setup(a => a.StopContainerGroupAsync(It.IsAny<string>()))
            .ReturnsAsync(new ContainerStatus { State = "Stopped" });

        var sp = BuildProvider(optionsMock, aciMock);
        var router = new CommandRouter(sp);

        var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            var rc = await router.RouteAsync([Constants.Commands.Stop]);
            rc.Should().Be(0);
            sw.ToString().Should().Contain("Status: Stopped");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task DeleteCommand_Success_PrintsConfirmation()
    {
        var optionsMock = new Mock<IProviderOptionsService>();
        optionsMock.Setup(o => o.GetOptions()).Returns(DefaultOptions());

        var aciMock = new Mock<IAciService>();
        aciMock.Setup(a => a.DeleteContainerGroupAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sp = BuildProvider(optionsMock, aciMock);
        var router = new CommandRouter(sp);

        var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            var rc = await router.RouteAsync([Constants.Commands.Delete]);
            rc.Should().Be(0);
            sw.ToString().Should().Contain("deleted successfully");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task StatusCommand_MapsStates_ToDevPodStatuses()
    {
        // Arrange
        var optionsMock = new Mock<IProviderOptionsService>();
        optionsMock.Setup(o => o.GetOptions()).Returns(DefaultOptions());

        var aciMock = new Mock<IAciService>();
        aciMock.SetupSequence(a => a.GetContainerGroupStatusAsync(It.IsAny<string>()))
            .ReturnsAsync(new ContainerStatus { State = "Running" })
            .ReturnsAsync(new ContainerStatus { State = "Stopped" })
            .ReturnsAsync(new ContainerStatus { State = "Pending" })
            .ReturnsAsync(new ContainerStatus { State = "Failed" })
            .ReturnsAsync(new ContainerStatus { State = "NotFound" })
            .ReturnsAsync(new ContainerStatus { State = "Unknown" });

        var sp = BuildProvider(optionsMock, aciMock);
        var router = new CommandRouter(sp);

        var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            foreach (var expected in new[] { "Running", "Stopped", "Busy", "Error", "NotFound", "Unknown" })
            {
                sw.GetStringBuilder().Clear();
                var rc = await router.RouteAsync([Constants.Commands.Status]);
                rc.Should().Be(0);
                sw.ToString().Trim().Should().Be(expected);
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task ExecCommand_ExecutesCommand_PrintsResult()
    {
        var optionsMock = new Mock<IProviderOptionsService>();
        optionsMock.Setup(o => o.GetOptions()).Returns(DefaultOptions());

        var aciMock = new Mock<IAciService>();
        aciMock.Setup(a => a.ExecuteCommandInteractiveAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<Stream>(),
                It.IsAny<Stream>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string containerGroupName, string command, Stream stdin, Stream stdout, Stream stderr, TimeSpan? timeout, CancellationToken token) =>
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes("OK");
                await stdout.WriteAsync(bytes.AsMemory(0, bytes.Length), token);
                await stdout.FlushAsync(token);
                return 0;
            });

        var sp = BuildProvider(optionsMock, aciMock);
        var router = new CommandRouter(sp);

        var original = Environment.GetEnvironmentVariable("COMMAND");
        var sw = new StringWriter();
        var errorWriter = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        try
        {
            Environment.SetEnvironmentVariable("COMMAND", "echo hi");
            Console.SetOut(sw);
            Console.SetError(errorWriter);
            var rc = await router.RouteAsync([Constants.Commands.Command]);
            rc.Should().Be(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMAND", original);
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task ExecCommand_ReturnsExitCodeAndPrintsStderr()
    {
        var optionsMock = new Mock<IProviderOptionsService>();
        optionsMock.Setup(o => o.GetOptions()).Returns(DefaultOptions());

        var aciMock = new Mock<IAciService>();
        aciMock.Setup(a => a.ExecuteCommandInteractiveAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<Stream>(),
                It.IsAny<Stream>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string containerGroupName, string command, Stream stdin, Stream stdout, Stream stderr, TimeSpan? timeout, CancellationToken token) =>
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes("boom");
                await stderr.WriteAsync(bytes.AsMemory(0, bytes.Length), token);
                await stderr.FlushAsync(token);
                return 7;
            });

        var sp = BuildProvider(optionsMock, aciMock);
        var router = new CommandRouter(sp);

        var original = Environment.GetEnvironmentVariable("COMMAND");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        try
        {
            Environment.SetEnvironmentVariable("COMMAND", "run");
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var rc = await router.RouteAsync([Constants.Commands.Command]);
            rc.Should().Be(7);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMAND", original);
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task InitCommand_FailurePath_PrintsErrorsAndReturnsNonZero()
    {
        var optionsMock = new Mock<IProviderOptionsService>();
        var opts = DefaultOptions();
        optionsMock.Setup(o => o.GetOptions()).Returns(opts);
        optionsMock.Setup(o => o.ValidateOptions(It.IsAny<ProviderOptions>(), out It.Ref<List<string>>.IsAny))
            .Callback(new ValidateOptionsCallback((ProviderOptions _, out List<string> errors) =>
            {
                errors = ["err1", "err2"];
            }))
            .Returns(false);

        var sp = BuildProvider(optionsMock, configureDefaultValidation: false);
        var router = new CommandRouter(sp);

        var sw = new StringWriter();
        var originalErr = Console.Error;
        try
        {
            Console.SetError(sw);
            var rc = await router.RouteAsync([Constants.Commands.Init]);
            rc.Should().Be(1);
            sw.ToString().Should().Contain("err1");
            sw.ToString().Should().Contain("err2");
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    private delegate void ValidateOptionsCallback(ProviderOptions options, out List<string> errors);
}
