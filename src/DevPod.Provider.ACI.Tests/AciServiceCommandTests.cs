using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.ContainerInstance.Models;
using Azure.ResourceManager.Resources;
using DevPod.Provider.ACI.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevPod.Provider.ACI.Tests;

public class AciServiceCommandTests
{
    [Fact]
    public async Task ExecuteCommandAsync_ReturnsStdoutAndExitCode()
    {
        var containerData = CreateContainerGroupData("workspace");
        ContainerExecContent? capturedContent = null;

        var containerGroup = CreateContainerGroupMock(containerData, out var execResultResponse, (name, content, _) =>
        {
            capturedContent = content;
            name.Should().Be("workspace");
        });

        var webSocketMessages = new[]
        {
            FakeWebSocketMessage.Text("hello\n"),
            FakeWebSocketMessage.Text("\n__ACI_EXIT_CODE__:0\n"),
        };

        var fakeWebSocket = new FakeWebSocketClient(webSocketMessages);
        var webSocketFactory = new Mock<IWebSocketClientFactory>();
        webSocketFactory.Setup(f => f.Create()).Returns(fakeWebSocket);

        var service = new TestAciService(containerGroup, webSocketFactory.Object);

        var result = await service.ExecuteCommandAsync("devpod-group", "echo hello");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Be("hello\n");
        result.Stderr.Should().BeEmpty();

        capturedContent.Should().NotBeNull();
        capturedContent!.Command.Should().Contain("__ACI_EXIT_CODE__");

        fakeWebSocket.SentMessages.Should().Equal("pwd");
        fakeWebSocket.CloseRequested.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteCommandAsync_ParsesStderrAndExitCode()
    {
        var containerData = CreateContainerGroupData("workspace");
        ContainerExecContent? capturedContent = null;

        var containerGroup = CreateContainerGroupMock(containerData, out _, (_, content, _) => capturedContent = content);

        var webSocketMessages = new[]
        {
            FakeWebSocketMessage.Binary(2, "failure"),
            FakeWebSocketMessage.Text("\n__ACI_EXIT_CODE__:23\n"),
        };

        var fakeWebSocket = new FakeWebSocketClient(webSocketMessages);
        var webSocketFactory = new Mock<IWebSocketClientFactory>();
        webSocketFactory.Setup(f => f.Create()).Returns(fakeWebSocket);

        var service = new TestAciService(containerGroup, webSocketFactory.Object);

        var result = await service.ExecuteCommandAsync("devpod-group", "run");

        result.ExitCode.Should().Be(23);
        result.Stdout.Should().BeEmpty();
        result.Stderr.Should().Be("failure");
        capturedContent.Should().NotBeNull();
    }

    private static ContainerGroupData CreateContainerGroupData(string containerName)
    {
        var location = new AzureLocation("eastus");
        var resources = new ContainerResourceRequirements(new ContainerResourceRequestsContent(1.0, 1.0));
        var container = new ContainerInstanceContainer(containerName, "mcr.microsoft.com/devcontainers/base:latest", resources);
        return new ContainerGroupData(location, new[] { container }, ContainerInstanceOperatingSystemType.Linux);
    }

    private static ContainerGroupResource CreateContainerGroupMock(
        ContainerGroupData data,
        out Response<ContainerExecResult> execResponse,
        Action<string, ContainerExecContent, CancellationToken>? callback = null)
    {
        var execResult = ArmContainerInstanceModelFactory.ContainerExecResult(new Uri("wss://localhost/socket"), "pwd");
        execResponse = Response.FromValue(execResult, new FakeResponse(200));

        var mock = new Mock<ContainerGroupResource> { CallBase = true };
        mock.SetupGet(m => m.HasData).Returns(true);
        mock.SetupGet(m => m.Data).Returns(data);
        mock
            .Setup(m => m.ExecuteContainerCommandAsync(It.IsAny<string>(), It.IsAny<ContainerExecContent>(), It.IsAny<CancellationToken>()))
            .Callback(callback ?? ((_, _, _) => { }))
            .ReturnsAsync(execResponse);

        return mock.Object;
    }

    private sealed class TestAciService : AciService
    {
        private readonly ContainerGroupResource _containerGroup;

        public TestAciService(ContainerGroupResource containerGroup, IWebSocketClientFactory factory)
            : base(
                NullLogger<AciService>.Instance,
                Mock.Of<IAuthenticationService>(),
                Mock.Of<IProviderOptionsService>(),
                factory)
        {
            _containerGroup = containerGroup;
        }

        protected override Task<ContainerGroupResource?> GetContainerGroupAsync(string name)
        {
            return Task.FromResult<ContainerGroupResource?>(_containerGroup);
        }
    }

    private sealed class FakeWebSocketClient : IWebSocketClient
    {
        private readonly Queue<FakeWebSocketMessage> _messages;
        private WebSocketState _state = WebSocketState.None;

        public FakeWebSocketClient(IEnumerable<FakeWebSocketMessage> messages)
        {
            _messages = new Queue<FakeWebSocketMessage>(messages);
        }

        public List<string> SentMessages { get; } = new List<string>();

        public bool CloseRequested { get; private set; }

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            _state = WebSocketState.Open;
            return Task.CompletedTask;
        }

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            var payload = buffer.Array is null
                ? Array.Empty<byte>()
                : buffer.Array[buffer.Offset..(buffer.Offset + buffer.Count)];

            SentMessages.Add(Encoding.UTF8.GetString(payload));
            return Task.CompletedTask;
        }

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (_messages.Count == 0)
            {
                return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, null));
            }

            var message = _messages.Dequeue();
            if (buffer.Array is null)
            {
                throw new InvalidOperationException("Buffer array cannot be null.");
            }

            Array.Copy(message.Payload, 0, buffer.Array, buffer.Offset, message.Payload.Length);
            return Task.FromResult(new WebSocketReceiveResult(message.Payload.Length, message.MessageType, message.EndOfMessage));
        }

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            CloseRequested = true;
            return Task.CompletedTask;
        }

        public WebSocketState State
        {
            get { return _state; }
        }

        public ValueTask DisposeAsync()
        {
            _state = WebSocketState.Closed;
            return ValueTask.CompletedTask;
        }
    }

    private sealed record FakeWebSocketMessage(WebSocketMessageType MessageType, byte[] Payload, bool EndOfMessage = true)
    {
        public static FakeWebSocketMessage Text(string text)
        {
            return new FakeWebSocketMessage(WebSocketMessageType.Text, Encoding.UTF8.GetBytes(text));
        }

        public static FakeWebSocketMessage Binary(byte channel, string text)
        {
            var textBytes = Encoding.UTF8.GetBytes(text);
            var payload = new byte[textBytes.Length + 1];
            payload[0] = channel;
            Array.Copy(textBytes, 0, payload, 1, textBytes.Length);
            return new FakeWebSocketMessage(WebSocketMessageType.Binary, payload);
        }
    }

    private sealed class FakeResponse : Response
    {
        public FakeResponse(int status)
        {
            Status = status;
        }

        public override int Status { get; }
        public override string ReasonPhrase => string.Empty;
        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; } = Guid.NewGuid().ToString();

        public override void Dispose()
        {
        }

        protected override bool TryGetHeader(string name, out string value)
        {
            value = string.Empty;
            return false;
        }

        protected override bool ContainsHeader(string name)
        {
            return false;
        }

        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
        {
            values = Array.Empty<string>();
            return false;
        }

        protected override IEnumerable<HttpHeader> EnumerateHeaders()
        {
            return Array.Empty<HttpHeader>();
        }
    }
}
