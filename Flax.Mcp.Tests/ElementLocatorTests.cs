using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flax.Mcp.Llm;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class ElementLocatorTests
{
    private sealed class FakeChatClient : IChatClient
    {
        private readonly string _reply;
        public ChatOptions? LastOptions { get; private set; }
        public FakeChatClient(string reply) => _reply = reply;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _reply)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    [Test]
    public void Status_Is_Exposed()
    {
        var locator = new ElementLocator(null, LocatorStatus.NotConfigured, 1024, "");
        Assert.That(locator.Status, Is.EqualTo(LocatorStatus.NotConfigured));
    }

    [Test]
    public void LocateInTreeAsync_Parses_Client_Reply_And_Sets_Model_And_MaxTokens()
    {
        var fake = new FakeChatClient("{\"found\":true,\"id\":5}");
        var locator = new ElementLocator(fake, LocatorStatus.Ready, 256, "cheap-model");

        var r = locator.LocateInTreeAsync("{\"id\":0}", "the 1 button", CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.That(r.Found, Is.True);
        Assert.That(r.Id, Is.EqualTo(5));
        Assert.That(fake.LastOptions!.MaxOutputTokens, Is.EqualTo(256));
        Assert.That(fake.LastOptions!.ModelId, Is.EqualTo("cheap-model"));
    }

    [Test]
    public void LocateByVisionAsync_Parses_Pixels()
    {
        var fake = new FakeChatClient("{\"found\":true,\"px\":7,\"py\":8}");
        var locator = new ElementLocator(fake, LocatorStatus.Ready, 1024, "m");

        var r = locator.LocateByVisionAsync(new byte[] { 1 }, "icon", CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.That(r.Found, Is.True);
        Assert.That(r.Px, Is.EqualTo(7));
        Assert.That(r.Py, Is.EqualTo(8));
    }
}
