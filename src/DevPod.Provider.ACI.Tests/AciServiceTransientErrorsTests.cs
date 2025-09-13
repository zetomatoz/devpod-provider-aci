using System.Reflection;
using Azure;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevPod.Provider.ACI.Tests;

public class AciServiceTransientErrorsTests
{
    [Fact]
    public void IsTransientError_ReturnsExpectedForStatusCodes()
    {
        var optionsMock = new Mock<IProviderOptionsService>();
        var authMock = new Mock<IAuthenticationService>();
        var svc = new AciService(
            NullLogger<AciService>.Instance,
            authMock.Object,
            optionsMock.Object);

        var method = typeof(AciService).GetMethod(
            "IsTransientError",
            BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        bool Invoke(int status)
        {
            var ex = new RequestFailedException(status, "msg", null, null);
            return (bool)method!.Invoke(svc, [ex])!;
        }

        Invoke(429).Should().BeTrue();
        Invoke(503).Should().BeTrue();
        Invoke(504).Should().BeTrue();
        Invoke(500).Should().BeFalse();
        Invoke(404).Should().BeFalse();
    }
}

