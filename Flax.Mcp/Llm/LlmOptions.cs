using System;
using Microsoft.Extensions.Configuration;

namespace Flax.Mcp.Llm;

public enum LocatorStatus { Ready, NotConfigured, KeyMissing }

/// <summary>
/// LLM configuration for the server-side element locator. Environment variables (FLAX_LLM_*) take
/// precedence over the optional appsettings "Llm" section. API keys are NEVER stored here — only the
/// NAME of the environment variable that holds the key.
/// </summary>
public sealed class LlmOptions
{
    public string Provider { get; init; } = "";   // openai | azure | anthropic | "" (off)
    public string Model { get; init; } = "";
    public string? BaseUrl { get; init; }
    public string? ApiVersion { get; init; }
    public string ApiKeyEnvVar { get; init; } = "";
    public int MaxOutputTokens { get; init; } = 1024;

    private static readonly string[] KnownProviders = { "openai", "azure", "anthropic" };

    public static LlmOptions Resolve(IConfiguration config, Func<string, string?> getEnv)
    {
        string? Pick(string envName, string configKey)
        {
            var v = getEnv(envName);
            return !string.IsNullOrWhiteSpace(v) ? v : config[$"Llm:{configKey}"];
        }

        var provider = (Pick("FLAX_LLM_PROVIDER", "Provider") ?? "").Trim().ToLowerInvariant();

        var apiKeyEnv = Pick("FLAX_LLM_API_KEY_ENV", "ApiKeyEnvVar");
        if (string.IsNullOrWhiteSpace(apiKeyEnv))
            apiKeyEnv = DefaultApiKeyEnvVar(provider);

        var maxTokens = int.TryParse(Pick("FLAX_LLM_MAX_TOKENS", "MaxOutputTokens"), out var n) && n > 0 ? n : 1024;

        return new LlmOptions
        {
            Provider = provider,
            Model = (Pick("FLAX_LLM_MODEL", "Model") ?? "").Trim(),
            BaseUrl = Pick("FLAX_LLM_BASE_URL", "BaseUrl"),
            ApiVersion = Pick("FLAX_LLM_API_VERSION", "ApiVersion"),
            ApiKeyEnvVar = apiKeyEnv ?? "",
            MaxOutputTokens = maxTokens
        };
    }

    public static string DefaultApiKeyEnvVar(string provider) => provider switch
    {
        "openai" => "OPENAI_API_KEY",
        "azure" => "AZURE_OPENAI_API_KEY",
        "anthropic" => "ANTHROPIC_API_KEY",
        _ => ""
    };

    public bool IsProviderConfigured => Array.IndexOf(KnownProviders, Provider) >= 0;

    public LocatorStatus GetStatus(string? apiKey)
    {
        if (!IsProviderConfigured) return LocatorStatus.NotConfigured;
        if (string.IsNullOrWhiteSpace(apiKey)) return LocatorStatus.KeyMissing;
        return LocatorStatus.Ready;
    }
}
