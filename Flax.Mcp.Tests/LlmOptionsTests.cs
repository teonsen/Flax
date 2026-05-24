using System.Collections.Generic;
using Flax.Mcp.Llm;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class LlmOptionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (k, v) in pairs) dict[k] = v;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Test]
    public void Env_Overrides_AppSettings()
    {
        var config = Config(("Llm:Provider", "anthropic"), ("Llm:Model", "from-config"));
        var env = new Dictionary<string, string?> { ["FLAX_LLM_MODEL"] = "from-env" };

        var o = LlmOptions.Resolve(config, k => env.GetValueOrDefault(k));

        Assert.That(o.Provider, Is.EqualTo("anthropic"));
        Assert.That(o.Model, Is.EqualTo("from-env"));
    }

    [Test]
    public void Config_Used_When_Env_Missing()
    {
        var config = Config(("Llm:Provider", "openai"), ("Llm:Model", "gpt-x"));
        var o = LlmOptions.Resolve(config, _ => null);
        Assert.That(o.Provider, Is.EqualTo("openai"));
        Assert.That(o.Model, Is.EqualTo("gpt-x"));
    }

    [Test]
    public void Provider_Is_Lowercased_And_Trimmed()
    {
        var o = LlmOptions.Resolve(Config(), k => k == "FLAX_LLM_PROVIDER" ? "  OpenAI " : null);
        Assert.That(o.Provider, Is.EqualTo("openai"));
    }

    [Test]
    public void Default_ApiKeyEnvVar_Per_Provider()
    {
        Assert.That(LlmOptions.DefaultApiKeyEnvVar("openai"), Is.EqualTo("OPENAI_API_KEY"));
        Assert.That(LlmOptions.DefaultApiKeyEnvVar("azure"), Is.EqualTo("AZURE_OPENAI_API_KEY"));
        Assert.That(LlmOptions.DefaultApiKeyEnvVar("anthropic"), Is.EqualTo("ANTHROPIC_API_KEY"));
    }

    [Test]
    public void Resolve_Fills_Default_ApiKeyEnvVar_From_Provider()
    {
        var o = LlmOptions.Resolve(Config(("Llm:Provider", "anthropic")), _ => null);
        Assert.That(o.ApiKeyEnvVar, Is.EqualTo("ANTHROPIC_API_KEY"));
    }

    [Test]
    public void Explicit_ApiKeyEnvVar_Wins_Over_Default()
    {
        var env = new Dictionary<string, string?>
        {
            ["FLAX_LLM_PROVIDER"] = "openai",
            ["FLAX_LLM_API_KEY_ENV"] = "MY_KEY"
        };
        var o = LlmOptions.Resolve(Config(), k => env.GetValueOrDefault(k));
        Assert.That(o.ApiKeyEnvVar, Is.EqualTo("MY_KEY"));
    }

    [Test]
    public void MaxOutputTokens_Defaults_To_1024_And_Parses()
    {
        Assert.That(LlmOptions.Resolve(Config(), _ => null).MaxOutputTokens, Is.EqualTo(1024));
        var o = LlmOptions.Resolve(Config(("Llm:MaxOutputTokens", "256")), _ => null);
        Assert.That(o.MaxOutputTokens, Is.EqualTo(256));
    }

    [Test]
    public void GetStatus_NotConfigured_When_Provider_Empty()
    {
        var o = LlmOptions.Resolve(Config(), _ => null);
        Assert.That(o.GetStatus("key"), Is.EqualTo(LocatorStatus.NotConfigured));
    }

    [Test]
    public void GetStatus_KeyMissing_When_Provider_Set_But_No_Key()
    {
        var o = LlmOptions.Resolve(Config(("Llm:Provider", "openai")), _ => null);
        Assert.That(o.GetStatus(null), Is.EqualTo(LocatorStatus.KeyMissing));
        Assert.That(o.GetStatus(""), Is.EqualTo(LocatorStatus.KeyMissing));
    }

    [Test]
    public void GetStatus_Ready_When_Provider_And_Key_Present()
    {
        var o = LlmOptions.Resolve(Config(("Llm:Provider", "openai")), _ => null);
        Assert.That(o.GetStatus("sk-123"), Is.EqualTo(LocatorStatus.Ready));
    }
}
