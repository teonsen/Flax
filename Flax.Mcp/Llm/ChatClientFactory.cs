using System;
using System.ClientModel;
using Microsoft.Extensions.AI;

namespace Flax.Mcp.Llm;

/// <summary>
/// Builds a Microsoft.Extensions.AI IChatClient for the configured provider. The connectors absorb
/// the wire-format differences; this class only selects the provider and passes credentials.
/// </summary>
public static class ChatClientFactory
{
    public static IChatClient Create(LlmOptions o, string apiKey)
    {
        switch (o.Provider)
        {
            case "openai":
            {
                var options = new OpenAI.OpenAIClientOptions();
                if (!string.IsNullOrWhiteSpace(o.BaseUrl)) options.Endpoint = new Uri(o.BaseUrl);
                var client = new OpenAI.OpenAIClient(new ApiKeyCredential(apiKey), options);
                return client.GetChatClient(o.Model).AsIChatClient();
            }
            case "azure":
            {
                if (string.IsNullOrWhiteSpace(o.BaseUrl))
                    throw new InvalidOperationException("azure provider requires FLAX_LLM_BASE_URL (the Azure OpenAI endpoint).");
                var client = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(o.BaseUrl), new ApiKeyCredential(apiKey));
                return client.GetChatClient(o.Model).AsIChatClient();   // o.Model = deployment name
            }
            case "anthropic":
            {
                return new Anthropic.SDK.AnthropicClient(apiKey).Messages;   // MessagesEndpoint implements IChatClient
            }
            default:
                throw new InvalidOperationException($"Unsupported LLM provider: '{o.Provider}'.");
        }
    }
}
