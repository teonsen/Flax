# Flax.Mcp サーバ側「要素判別 LLM」実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Flax.Mcp` に、クライアントのモデルとは独立に設定した安いモデルで「UI ツリー/スクリーンショット → 目的要素」を判別する `locate_element` ツールを追加し、トークン消費をクライアントから切り離す。

**Architecture:** 別プロジェクトは作らない。`Flax.Mcp/Llm/` に LLM 層（設定・ファクトリ・判別ロジック）を足し、`Tools/InspectionTools.cs` に `locate_element` を追加。プロバイダ差は `Microsoft.Extensions.AI` の `IChatClient` が吸収する。判別の中核ロジック（auto フォールバック・座標換算・パース）は FlaUI/IChatClient を interface で切り離して単体テストする（既存 `ClickService` の流儀）。API キーは常に環境変数からのみ読む。

**Tech Stack:** C# / net8.0-windows / NUnit 4 / `ModelContextProtocol` 1.3.0 / `Microsoft.Extensions.AI` / `Microsoft.Extensions.AI.OpenAI` / `Azure.AI.OpenAI` / `Anthropic.SDK`

**設計書:** [docs/superpowers/specs/2026-05-24-flax-mcp-server-side-llm-locator-design.md](../specs/2026-05-24-flax-mcp-server-side-llm-locator-design.md)

---

## ファイル構成

| パス | 役割 |
|---|---|
| `Flax.Mcp/Llm/LlmOptions.cs` | 設定モデル（env 優先 / appsettings フォールバック）+ `LocatorStatus` 判定 |
| `Flax.Mcp/Llm/LocateResultParser.cs` | `LocateResult` レコード + モデル応答 JSON の寛容なパース（純粋関数） |
| `Flax.Mcp/Llm/LocatorContracts.cs` | `IElementLocator` / `ILocateWindow` / `LocateMode` / `LocateOutcome` / `LocateModeParser` |
| `Flax.Mcp/Llm/LocateService.cs` | auto/tree/vision の分岐・座標換算・エラー封筒（FlaUI 非依存） |
| `Flax.Mcp/Llm/ElementLocator.cs` | `IChatClient` を使う実 `IElementLocator`（プロンプト + 呼び出し） |
| `Flax.Mcp/Llm/ChatClientFactory.cs` | `LlmOptions` → プロバイダ別 `IChatClient` を構築 |
| `Flax.Mcp/Llm/FlaxWindowLocateAdapter.cs` | `FlaxWindow` を `ILocateWindow` に適合 |
| `Flax.Mcp/Tools/InspectionTools.cs` | `locate_element` ツールを追加（既存ファイルに追記） |
| `Flax.Mcp/Program.cs` | `LlmOptions` 解決 + `ElementLocator` を DI 登録（追記） |
| `Flax.Mcp/Flax.Mcp.csproj` | NuGet 4 件追加 + `appsettings.json` の出力コピー（追記） |
| `Flax.Mcp/appsettings.json` | 任意の `"Llm"` フォールバック設定（**キーは書かない**） |
| `Flax.Mcp.Tests/LlmOptionsTests.cs` | 設定解決・既定値・`GetStatus` |
| `Flax.Mcp.Tests/LocateResultParserTests.cs` | パースの正常/異常 |
| `Flax.Mcp.Tests/LocateServiceTests.cs` | 分岐・フォールバック・座標換算・エラー封筒（フェイク使用） |
| `Flax.Mcp.Tests/LocateModeParserTests.cs` | mode 文字列の解釈 |
| `Flax.Mcp.Tests/ElementLocatorTests.cs` | フェイク `IChatClient` で呼び出し+パース連結を検証 |

---

## Task 1: NuGet パッケージ追加・appsettings 追加・ビルド確認

設計書 §2/§9 の「バージョン固定・0 エラービルド」をここで解決する。`Azure.AI.OpenAI` は spec §2 の3件に明記されていないが、`azure` プロバイダの `AzureOpenAIClient` 型に必要なため追加する（spec の補正）。

**Files:**
- Modify: `Flax.Mcp/Flax.Mcp.csproj`
- Create: `Flax.Mcp/appsettings.json`
- Modify: `.gitignore`（リポジトリ直下。無ければ作成）

- [ ] **Step 1: パッケージを追加（バージョンはここで解決・固定される）**

Run:
```powershell
dotnet add Flax.Mcp/Flax.Mcp.csproj package Microsoft.Extensions.AI
dotnet add Flax.Mcp/Flax.Mcp.csproj package Microsoft.Extensions.AI.OpenAI --prerelease
dotnet add Flax.Mcp/Flax.Mcp.csproj package Azure.AI.OpenAI
dotnet add Flax.Mcp/Flax.Mcp.csproj package Anthropic.SDK
```
Expected: 各コマンドが成功し、`Flax.Mcp.csproj` に `<PackageReference>` が4件追記される（`Microsoft.Extensions.AI.OpenAI` は preview バージョンになり得る）。

- [ ] **Step 2: appsettings.json を出力ディレクトリへコピーする設定を csproj に追加**

`Flax.Mcp/Flax.Mcp.csproj` の `</Project>` 直前に次の ItemGroup を追加:
```xml
  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

- [ ] **Step 3: appsettings.json を作成（キーは含めない）**

Create `Flax.Mcp/appsettings.json`:
```json
{
  "Llm": {
    "Provider": "",
    "Model": "",
    "MaxOutputTokens": 1024
  }
}
```

- [ ] **Step 4: ローカル秘密設定を .gitignore に追加**

`.gitignore` に次の行を追加（既存内容は残す）:
```
appsettings.*.local.json
```

- [ ] **Step 5: ビルドして 0 エラーを確認**

Run: `dotnet build Flax.Mcp/Flax.Mcp.csproj -c Debug`
Expected: `Build succeeded` / `0 Error(s)`。エラーが出る場合はパッケージのバージョン互換を調整（net8.0-windows 対応版に固定）。

- [ ] **Step 6: コミット**

```powershell
git add Flax.Mcp/Flax.Mcp.csproj Flax.Mcp/appsettings.json .gitignore
git commit -m @'
build: add Microsoft.Extensions.AI + provider connectors for server-side locator

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
'@
```

---

## Task 2: `LlmOptions`（設定解決・既定値・状態判定）

**Files:**
- Create: `Flax.Mcp/Llm/LlmOptions.cs`
- Test: `Flax.Mcp.Tests/LlmOptionsTests.cs`

- [ ] **Step 1: 失敗するテストを書く**

Create `Flax.Mcp.Tests/LlmOptionsTests.cs`:
```csharp
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
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter LlmOptionsTests`
Expected: コンパイルエラー（`LlmOptions` / `LocatorStatus` 未定義）= FAIL。

- [ ] **Step 3: 実装を書く**

Create `Flax.Mcp/Llm/LlmOptions.cs`:
```csharp
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
```

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter LlmOptionsTests`
Expected: PASS（全テスト）。

- [ ] **Step 5: コミット**

```powershell
git add Flax.Mcp/Llm/LlmOptions.cs Flax.Mcp.Tests/LlmOptionsTests.cs
git commit -m @'
feat: add LlmOptions env-first config resolution for locator

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
'@
```

---

## Task 3: `LocateResultParser`（モデル応答 JSON の寛容なパース）

設計書 §3 の方針: JSON 解析不能なら `found=false` + 生テキスト断片を `reasoning` に（`llm_error` ではなく graceful な不検出として扱う）。

**Files:**
- Create: `Flax.Mcp/Llm/LocateResultParser.cs`
- Test: `Flax.Mcp.Tests/LocateResultParserTests.cs`

- [ ] **Step 1: 失敗するテストを書く**

Create `Flax.Mcp.Tests/LocateResultParserTests.cs`:
```csharp
using Flax.Mcp.Llm;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class LocateResultParserTests
{
    [Test]
    public void ParseTree_Found_With_Id()
    {
        var r = LocateResultParser.ParseTree("{\"found\":true,\"id\":12,\"confidence\":0.9,\"reasoning\":\"the 1 button\"}");
        Assert.That(r.Found, Is.True);
        Assert.That(r.Id, Is.EqualTo(12));
        Assert.That(r.Confidence, Is.EqualTo(0.9).Within(1e-9));
        Assert.That(r.Reasoning, Is.EqualTo("the 1 button"));
    }

    [Test]
    public void ParseTree_Found_False()
    {
        var r = LocateResultParser.ParseTree("{\"found\":false}");
        Assert.That(r.Found, Is.False);
        Assert.That(r.Id, Is.Null);
    }

    [Test]
    public void ParseTree_Extracts_Object_From_Surrounding_Prose()
    {
        var r = LocateResultParser.ParseTree("Sure! Here you go:\n{\"found\":true,\"id\":7}\nHope that helps.");
        Assert.That(r.Found, Is.True);
        Assert.That(r.Id, Is.EqualTo(7));
    }

    [Test]
    public void ParseTree_Found_True_But_No_Id_Is_NotFound()
    {
        var r = LocateResultParser.ParseTree("{\"found\":true}");
        Assert.That(r.Found, Is.False);
    }

    [Test]
    public void ParseTree_Invalid_Json_Returns_NotFound_With_RawReasoning()
    {
        var r = LocateResultParser.ParseTree("I could not find it.");
        Assert.That(r.Found, Is.False);
        Assert.That(r.Reasoning, Does.Contain("could not find"));
    }

    [Test]
    public void ParseVision_Found_With_Pixels()
    {
        var r = LocateResultParser.ParseVision("{\"found\":true,\"px\":120,\"py\":340,\"confidence\":0.8}");
        Assert.That(r.Found, Is.True);
        Assert.That(r.Px, Is.EqualTo(120));
        Assert.That(r.Py, Is.EqualTo(340));
    }

    [Test]
    public void ParseVision_Missing_Py_Is_NotFound()
    {
        var r = LocateResultParser.ParseVision("{\"found\":true,\"px\":120}");
        Assert.That(r.Found, Is.False);
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter LocateResultParserTests`
Expected: コンパイルエラー（`LocateResultParser` / `LocateResult` 未定義）= FAIL。

- [ ] **Step 3: 実装を書く**

Create `Flax.Mcp/Llm/LocateResultParser.cs`:
```csharp
using System.Text.Json;

namespace Flax.Mcp.Llm;

public sealed record LocateResult(
    bool Found,
    int? Id = null,
    int? Px = null,
    int? Py = null,
    double? Confidence = null,
    string? Reasoning = null);

/// <summary>
/// Parses the locator model's JSON reply. Tolerant: extracts the first {...} block, and on any parse
/// failure (or missing required field) returns Found=false with the raw text in Reasoning so the
/// caller can fall back gracefully instead of erroring.
/// </summary>
public static class LocateResultParser
{
    public static LocateResult ParseTree(string text) => Parse(text, vision: false);
    public static LocateResult ParseVision(string text) => Parse(text, vision: true);

    private static LocateResult Parse(string text, bool vision)
    {
        var json = ExtractJsonObject(text);
        if (json == null) return new LocateResult(false, Reasoning: Trim(text));

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var found = root.TryGetProperty("found", out var f) && f.ValueKind == JsonValueKind.True;
            double? confidence = root.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var cv) ? cv : null;
            string? reasoning = root.TryGetProperty("reasoning", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;

            if (!found)
                return new LocateResult(false, Confidence: confidence, Reasoning: reasoning ?? Trim(text));

            if (vision)
            {
                int? px = root.TryGetProperty("px", out var pxe) && pxe.TryGetInt32(out var pxv) ? pxv : null;
                int? py = root.TryGetProperty("py", out var pye) && pye.TryGetInt32(out var pyv) ? pyv : null;
                if (px == null || py == null)
                    return new LocateResult(false, Confidence: confidence, Reasoning: reasoning ?? Trim(text));
                return new LocateResult(true, Px: px, Py: py, Confidence: confidence, Reasoning: reasoning);
            }
            else
            {
                int? id = root.TryGetProperty("id", out var ide) && ide.TryGetInt32(out var idv) ? idv : null;
                if (id == null)
                    return new LocateResult(false, Confidence: confidence, Reasoning: reasoning ?? Trim(text));
                return new LocateResult(true, Id: id, Confidence: confidence, Reasoning: reasoning);
            }
        }
        catch (JsonException)
        {
            return new LocateResult(false, Reasoning: Trim(text));
        }
    }

    private static string? ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : null;
    }

    private static string Trim(string text) => text.Length <= 500 ? text : text.Substring(0, 500);
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter LocateResultParserTests`
Expected: PASS（全テスト）。

- [ ] **Step 5: コミット**

```powershell
git add Flax.Mcp/Llm/LocateResultParser.cs Flax.Mcp.Tests/LocateResultParserTests.cs
git commit -m @'
feat: add tolerant parser for locator model JSON replies

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
'@
```

---

## Task 4: `LocateService`（分岐・フォールバック・座標換算）と契約型

設計書 §4/§5 の auto 判定フローと座標換算（`x=Left+px, y=Top+py`）の中核。FlaUI/IChatClient に依存しないよう `IElementLocator` / `ILocateWindow` で切り離してテストする。

**Files:**
- Create: `Flax.Mcp/Llm/LocatorContracts.cs`
- Create: `Flax.Mcp/Llm/LocateService.cs`
- Test: `Flax.Mcp.Tests/LocateServiceTests.cs`
- Test: `Flax.Mcp.Tests/LocateModeParserTests.cs`

- [ ] **Step 1: 契約型を作る（先に定義しておく）**

Create `Flax.Mcp/Llm/LocatorContracts.cs`:
```csharp
using System.Threading;
using System.Threading.Tasks;

namespace Flax.Mcp.Llm;

public enum LocateMode { Auto, Tree, Vision }

/// <summary>The cheap server-side locator model, abstracted for testing.</summary>
public interface IElementLocator
{
    LocatorStatus Status { get; }
    Task<LocateResult> LocateInTreeAsync(string treeJson, string target, CancellationToken ct);
    Task<LocateResult> LocateByVisionAsync(byte[] png, string target, CancellationToken ct);
}

/// <summary>The window surface LocateService needs; adapts FlaxWindow for testability.</summary>
public interface ILocateWindow
{
    string? GetTreeJson();
    byte[]? CapturePng();
    int Left { get; }
    int Top { get; }
}

public sealed record LocateOutcome(
    bool Ok,
    string? Mode = null,          // "tree" | "vision"
    int? ElementId = null,
    int? X = null,
    int? Y = null,
    double? Confidence = null,
    string? Reasoning = null,
    string? Error = null,
    string? Hint = null);

public static class LocateModeParser
{
    public static LocateMode Parse(string? mode) => (mode ?? "").Trim().ToLowerInvariant() switch
    {
        "tree" => LocateMode.Tree,
        "vision" => LocateMode.Vision,
        _ => LocateMode.Auto
    };
}
```

- [ ] **Step 2: 失敗するテストを書く（mode パーサ）**

Create `Flax.Mcp.Tests/LocateModeParserTests.cs`:
```csharp
using Flax.Mcp.Llm;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class LocateModeParserTests
{
    [Test] public void Null_Is_Auto() => Assert.That(LocateModeParser.Parse(null), Is.EqualTo(LocateMode.Auto));
    [Test] public void Empty_Is_Auto() => Assert.That(LocateModeParser.Parse(""), Is.EqualTo(LocateMode.Auto));
    [Test] public void Garbage_Is_Auto() => Assert.That(LocateModeParser.Parse("xyz"), Is.EqualTo(LocateMode.Auto));
    [Test] public void Tree_CaseInsensitive() => Assert.That(LocateModeParser.Parse(" Tree "), Is.EqualTo(LocateMode.Tree));
    [Test] public void Vision_CaseInsensitive() => Assert.That(LocateModeParser.Parse("VISION"), Is.EqualTo(LocateMode.Vision));
}
```

- [ ] **Step 3: 失敗するテストを書く（LocateService、フェイク使用）**

Create `Flax.Mcp.Tests/LocateServiceTests.cs`:
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Flax.Mcp.Llm;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class LocateServiceTests
{
    private sealed class FakeLocator : IElementLocator
    {
        public LocatorStatus Status { get; init; } = LocatorStatus.Ready;
        public LocateResult? TreeResult { get; init; }
        public LocateResult? VisionResult { get; init; }
        public bool Throws { get; init; }
        public int TreeCalls { get; private set; }
        public int VisionCalls { get; private set; }

        public Task<LocateResult> LocateInTreeAsync(string treeJson, string target, CancellationToken ct)
        {
            TreeCalls++;
            if (Throws) throw new InvalidOperationException("api down");
            return Task.FromResult(TreeResult ?? new LocateResult(false));
        }

        public Task<LocateResult> LocateByVisionAsync(byte[] png, string target, CancellationToken ct)
        {
            VisionCalls++;
            if (Throws) throw new InvalidOperationException("api down");
            return Task.FromResult(VisionResult ?? new LocateResult(false));
        }
    }

    private sealed class FakeWindow : ILocateWindow
    {
        public string? Tree { get; init; } = "{\"id\":0}";
        public byte[]? Png { get; init; } = new byte[] { 1, 2, 3 };
        public int Left { get; init; } = 1000;
        public int Top { get; init; } = 500;
        public string? GetTreeJson() => Tree;
        public byte[]? CapturePng() => Png;
    }

    private static LocateOutcome Run(IElementLocator locator, ILocateWindow window, LocateMode mode)
        => new LocateService().LocateAsync(locator, window, "the 1 button", mode, CancellationToken.None)
            .GetAwaiter().GetResult();

    [Test]
    public void NotConfigured_Returns_llm_not_configured()
    {
        var o = Run(new FakeLocator { Status = LocatorStatus.NotConfigured }, new FakeWindow(), LocateMode.Auto);
        Assert.That(o.Ok, Is.False);
        Assert.That(o.Error, Is.EqualTo("llm_not_configured"));
    }

    [Test]
    public void KeyMissing_Returns_llm_key_missing()
    {
        var o = Run(new FakeLocator { Status = LocatorStatus.KeyMissing }, new FakeWindow(), LocateMode.Auto);
        Assert.That(o.Ok, Is.False);
        Assert.That(o.Error, Is.EqualTo("llm_key_missing"));
    }

    [Test]
    public void Tree_Hit_Returns_ElementId()
    {
        var locator = new FakeLocator { TreeResult = new LocateResult(true, Id: 42, Confidence: 0.9) };
        var o = Run(locator, new FakeWindow(), LocateMode.Auto);
        Assert.That(o.Ok, Is.True);
        Assert.That(o.Mode, Is.EqualTo("tree"));
        Assert.That(o.ElementId, Is.EqualTo(42));
        Assert.That(o.Confidence, Is.EqualTo(0.9).Within(1e-9));
        Assert.That(locator.VisionCalls, Is.Zero, "tree hit must not call vision");
    }

    [Test]
    public void Auto_Tree_Miss_Falls_Back_To_Vision_With_ScreenCoords()
    {
        var locator = new FakeLocator
        {
            TreeResult = new LocateResult(false),
            VisionResult = new LocateResult(true, Px: 30, Py: 20)
        };
        var o = Run(locator, new FakeWindow { Left = 1000, Top = 500 }, LocateMode.Auto);
        Assert.That(o.Ok, Is.True);
        Assert.That(o.Mode, Is.EqualTo("vision"));
        Assert.That(o.X, Is.EqualTo(1030));
        Assert.That(o.Y, Is.EqualTo(520));
        Assert.That(locator.TreeCalls, Is.EqualTo(1));
        Assert.That(locator.VisionCalls, Is.EqualTo(1));
    }

    [Test]
    public void Auto_Tree_Unavailable_Goes_Straight_To_Vision()
    {
        var locator = new FakeLocator { VisionResult = new LocateResult(true, Px: 5, Py: 6) };
        var o = Run(locator, new FakeWindow { Tree = null, Left = 0, Top = 0 }, LocateMode.Auto);
        Assert.That(o.Ok, Is.True);
        Assert.That(o.Mode, Is.EqualTo("vision"));
        Assert.That(locator.TreeCalls, Is.Zero, "no tree means no tree call");
        Assert.That(o.X, Is.EqualTo(5));
        Assert.That(o.Y, Is.EqualTo(6));
    }

    [Test]
    public void Tree_Mode_Miss_Does_Not_Fall_Back()
    {
        var locator = new FakeLocator { TreeResult = new LocateResult(false) };
        var o = Run(locator, new FakeWindow(), LocateMode.Tree);
        Assert.That(o.Ok, Is.False);
        Assert.That(o.Error, Is.EqualTo("element_not_found"));
        Assert.That(locator.VisionCalls, Is.Zero);
    }

    [Test]
    public void Tree_Mode_With_Null_Tree_Returns_element_not_found()
    {
        var o = Run(new FakeLocator(), new FakeWindow { Tree = null }, LocateMode.Tree);
        Assert.That(o.Ok, Is.False);
        Assert.That(o.Error, Is.EqualTo("element_not_found"));
    }

    [Test]
    public void Vision_Mode_Skips_Tree()
    {
        var locator = new FakeLocator { VisionResult = new LocateResult(true, Px: 1, Py: 2) };
        var o = Run(locator, new FakeWindow { Left = 10, Top = 20 }, LocateMode.Vision);
        Assert.That(o.Ok, Is.True);
        Assert.That(o.Mode, Is.EqualTo("vision"));
        Assert.That(o.X, Is.EqualTo(11));
        Assert.That(o.Y, Is.EqualTo(22));
        Assert.That(locator.TreeCalls, Is.Zero);
    }

    [Test]
    public void Vision_Miss_Returns_element_not_found()
    {
        var o = Run(new FakeLocator { VisionResult = new LocateResult(false) }, new FakeWindow(), LocateMode.Vision);
        Assert.That(o.Ok, Is.False);
        Assert.That(o.Error, Is.EqualTo("element_not_found"));
    }

    [Test]
    public void Capture_Null_Returns_element_not_found()
    {
        var o = Run(new FakeLocator(), new FakeWindow { Tree = null, Png = null }, LocateMode.Vision);
        Assert.That(o.Ok, Is.False);
        Assert.That(o.Error, Is.EqualTo("element_not_found"));
    }

    [Test]
    public void Locator_Exception_Returns_llm_error()
    {
        var o = Run(new FakeLocator { Throws = true }, new FakeWindow(), LocateMode.Auto);
        Assert.That(o.Ok, Is.False);
        Assert.That(o.Error, Is.EqualTo("llm_error"));
        Assert.That(o.Hint, Does.Contain("api down"));
    }
}
```

- [ ] **Step 4: テストが失敗することを確認**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter "LocateServiceTests|LocateModeParserTests"`
Expected: コンパイルエラー（`LocateService` 未定義）= FAIL。

- [ ] **Step 5: 実装を書く**

Create `Flax.Mcp/Llm/LocateService.cs`:
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Flax.Mcp.Llm;

/// <summary>
/// Orchestrates element location: chooses tree vs vision, performs the auto fallback (tree first,
/// then vision when the tree is unavailable or yields nothing), and converts vision pixel
/// coordinates to absolute screen coordinates. FlaUI-free via ILocateWindow / IElementLocator.
/// </summary>
public sealed class LocateService
{
    public async Task<LocateOutcome> LocateAsync(
        IElementLocator locator, ILocateWindow window, string target, LocateMode mode, CancellationToken ct)
    {
        switch (locator.Status)
        {
            case LocatorStatus.NotConfigured:
                return new LocateOutcome(false, Error: "llm_not_configured",
                    Hint: "Set FLAX_LLM_PROVIDER/FLAX_LLM_MODEL and the provider's API key env var, or locate on the client side.");
            case LocatorStatus.KeyMissing:
                return new LocateOutcome(false, Error: "llm_key_missing",
                    Hint: "Provider is configured but its API key environment variable is empty.");
        }

        try
        {
            if (mode == LocateMode.Vision)
                return await VisionAsync(locator, window, target, ct);

            // Tree or Auto: try the UIA tree first.
            var treeJson = window.GetTreeJson();
            if (treeJson != null)
            {
                var tree = await locator.LocateInTreeAsync(treeJson, target, ct);
                if (tree.Found && tree.Id.HasValue)
                    return new LocateOutcome(true, "tree", ElementId: tree.Id,
                        Confidence: tree.Confidence, Reasoning: tree.Reasoning);

                if (mode == LocateMode.Tree)
                    return new LocateOutcome(false, Error: "element_not_found",
                        Hint: "No matching element in the UIA tree.", Reasoning: tree.Reasoning);
            }
            else if (mode == LocateMode.Tree)
            {
                return new LocateOutcome(false, Error: "element_not_found",
                    Hint: "UIA tree unavailable (e.g. WinUI3). Try mode=vision or auto.");
            }

            // Auto fallback to vision.
            return await VisionAsync(locator, window, target, ct);
        }
        catch (Exception ex)
        {
            return new LocateOutcome(false, Error: "llm_error", Hint: ex.Message);
        }
    }

    private static async Task<LocateOutcome> VisionAsync(
        IElementLocator locator, ILocateWindow window, string target, CancellationToken ct)
    {
        var png = window.CapturePng();
        if (png == null || png.Length == 0)
            return new LocateOutcome(false, Error: "element_not_found", Hint: "Window capture failed.");

        var v = await locator.LocateByVisionAsync(png, target, ct);
        if (v.Found && v.Px.HasValue && v.Py.HasValue)
            return new LocateOutcome(true, "vision",
                X: window.Left + v.Px.Value, Y: window.Top + v.Py.Value,
                Confidence: v.Confidence, Reasoning: v.Reasoning);

        return new LocateOutcome(false, Error: "element_not_found",
            Hint: "Could not locate the target by vision.", Reasoning: v.Reasoning);
    }
}
```

- [ ] **Step 6: テストが通ることを確認**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter "LocateServiceTests|LocateModeParserTests"`
Expected: PASS（全テスト）。

- [ ] **Step 7: コミット**

```powershell
git add Flax.Mcp/Llm/LocatorContracts.cs Flax.Mcp/Llm/LocateService.cs Flax.Mcp.Tests/LocateServiceTests.cs Flax.Mcp.Tests/LocateModeParserTests.cs
git commit -m @'
feat: add LocateService orchestration (auto/tree/vision + screen-coord conversion)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
'@
```

---

## Task 5: `ChatClientFactory`（プロバイダ別 `IChatClient` 構築）

設計書 §3/§9。ここは外部パッケージの正確な API に依存する。**API ドリフト注意:** ビルドが通らない場合は、インストールされた `Microsoft.Extensions.AI.OpenAI` / `Azure.AI.OpenAI` / `Anthropic.SDK` のバージョンに合わせて呼び出しを調整する。期待する型: OpenAI=`OpenAI.OpenAIClient` + `GetChatClient(model).AsIChatClient()`、Azure=`Azure.AI.OpenAI.AzureOpenAIClient` + `GetChatClient(deployment).AsIChatClient()`、Anthropic=`Anthropic.SDK.AnthropicClient` の `IChatClient` 実装（モデルはリクエスト毎に `ChatOptions.ModelId` で指定）。

**Files:**
- Create: `Flax.Mcp/Llm/ChatClientFactory.cs`

- [ ] **Step 1: 実装を書く**

Create `Flax.Mcp/Llm/ChatClientFactory.cs`:
```csharp
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
                return new Anthropic.SDK.AnthropicClient(apiKey).Messages;   // implements IChatClient
            }
            default:
                throw new InvalidOperationException($"Unsupported LLM provider: '{o.Provider}'.");
        }
    }
}
```

- [ ] **Step 2: ビルドして 0 エラーを確認（API 確定）**

Run: `dotnet build Flax.Mcp/Flax.Mcp.csproj -c Debug`
Expected: `Build succeeded` / `0 Error(s)`。
失敗時の対処（よくあるドリフト）:
- `AsIChatClient` が見つからない → `using Microsoft.Extensions.AI;` を確認。OpenAI 連携は `Microsoft.Extensions.AI.OpenAI` が提供。
- `ApiKeyCredential` が見つからない → `using System.ClientModel;`（パッケージ `System.ClientModel`）。
- Anthropic の `.Messages` が `IChatClient` でない → `(IChatClient)new Anthropic.SDK.AnthropicClient(apiKey)` を試す、もしくはパッケージ README の IChatClient 取得 API に合わせる。
調整したら再ビルドで 0 エラーを確認。

- [ ] **Step 3: コミット**

```powershell
git add Flax.Mcp/Llm/ChatClientFactory.cs
git commit -m @'
feat: add ChatClientFactory for openai/azure/anthropic IChatClient

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
'@
```

---

## Task 6: `ElementLocator`（実 `IElementLocator`：IChatClient 呼び出し + パース）

設計書 §3。`ChatOptions.ModelId` を全プロバイダで設定する（Anthropic は必須、OpenAI/Azure は冪等）。

**Files:**
- Create: `Flax.Mcp/Llm/ElementLocator.cs`
- Test: `Flax.Mcp.Tests/ElementLocatorTests.cs`

- [ ] **Step 1: 失敗するテストを書く（フェイク IChatClient）**

Create `Flax.Mcp.Tests/ElementLocatorTests.cs`:
```csharp
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
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter ElementLocatorTests`
Expected: コンパイルエラー（`ElementLocator` 未定義）= FAIL。

- [ ] **Step 3: 実装を書く**

Create `Flax.Mcp/Llm/ElementLocator.cs`:
```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Flax.Mcp.Llm;

/// <summary>
/// IElementLocator backed by a Microsoft.Extensions.AI IChatClient — the cheap, server-side model
/// configured independently of the MCP client's model.
/// </summary>
public sealed class ElementLocator : IElementLocator
{
    private readonly IChatClient? _client;
    private readonly int _maxTokens;
    private readonly string _model;

    public LocatorStatus Status { get; }

    public ElementLocator(IChatClient? client, LocatorStatus status, int maxOutputTokens, string model)
    {
        _client = client;
        Status = status;
        _maxTokens = maxOutputTokens;
        _model = model;
    }

    private const string TreeSystem =
        "You locate exactly one UI element in a Windows UIA element tree given as JSON. Each node has a " +
        "numeric \"id\". Reply with ONLY compact JSON: {\"found\":bool,\"id\":number,\"confidence\":0..1,\"reasoning\":\"short\"}. " +
        "If nothing matches, reply {\"found\":false}.";

    private const string VisionSystem =
        "You locate exactly one UI element in a screenshot. Reply with ONLY compact JSON: " +
        "{\"found\":bool,\"px\":number,\"py\":number,\"confidence\":0..1,\"reasoning\":\"short\"} where px,py are the " +
        "pixel coordinates of the element's center in the image (origin top-left). If not visible, reply {\"found\":false}.";

    public async Task<LocateResult> LocateInTreeAsync(string treeJson, string target, CancellationToken ct)
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, TreeSystem),
            new ChatMessage(ChatRole.User, $"Target: {target}\n\nUIA tree:\n{treeJson}")
        };
        var resp = await _client!.GetResponseAsync(messages, Options(), ct);
        return LocateResultParser.ParseTree(resp.Text ?? "");
    }

    public async Task<LocateResult> LocateByVisionAsync(byte[] png, string target, CancellationToken ct)
    {
        var user = new ChatMessage(ChatRole.User, new AIContent[]
        {
            new TextContent($"Target: {target}\nReturn the pixel coordinates of its center."),
            new DataContent(png, "image/png")
        });
        var messages = new[] { new ChatMessage(ChatRole.System, VisionSystem), user };
        var resp = await _client!.GetResponseAsync(messages, Options(), ct);
        return LocateResultParser.ParseVision(resp.Text ?? "");
    }

    private ChatOptions Options() => new()
    {
        MaxOutputTokens = _maxTokens,
        ModelId = string.IsNullOrEmpty(_model) ? null : _model
    };
}
```

> **API ドリフト注意:** `ChatResponse.Text`、`ChatMessage(ChatRole, IList<AIContent>)`、`DataContent(byte[], string)`、`ChatOptions.ModelId/MaxOutputTokens` はインストール版で名称が異なる場合がある。ビルドエラー時はパッケージの型に合わせて調整（例: `resp.Text` が無ければ `resp.Message?.Text`）。

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter ElementLocatorTests`
Expected: PASS（全テスト）。

- [ ] **Step 5: コミット**

```powershell
git add Flax.Mcp/Llm/ElementLocator.cs Flax.Mcp.Tests/ElementLocatorTests.cs
git commit -m @'
feat: add ElementLocator backed by IChatClient (tree + vision prompts)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
'@
```

---

## Task 7: `locate_element` ツール + FlaxWindow アダプタ + DI 配線

設計書 §4。既存 `InspectionTools` の `ToolRunner.Run` / `Json.Of` パターンに合わせ、`ElementLocator` を DI で受け取る。`Json.Of` は null フィールドを省くので、tree 成功時は `x/y` が、vision 成功時は `elementId` が自動的に出力から消える。

**Files:**
- Create: `Flax.Mcp/Llm/FlaxWindowLocateAdapter.cs`
- Modify: `Flax.Mcp/Tools/InspectionTools.cs`（`locate_element` を追加）
- Modify: `Flax.Mcp/Program.cs`（`LlmOptions` 解決 + `ElementLocator` 登録）

- [ ] **Step 1: FlaxWindow アダプタを作る**

Create `Flax.Mcp/Llm/FlaxWindowLocateAdapter.cs`:
```csharp
using Flax.Windows;

namespace Flax.Mcp.Llm;

/// <summary>Adapts a live FlaxWindow to the ILocateWindow surface LocateService needs.</summary>
public sealed class FlaxWindowLocateAdapter : ILocateWindow
{
    private readonly FlaxWindow _window;
    public FlaxWindowLocateAdapter(FlaxWindow window) => _window = window;

    public string? GetTreeJson() => _window.GetElementTreeAsJson(-1, false);

    public byte[]? CapturePng()
    {
        _window.Activate();
        return _window.CaptureToPngBytes();
    }

    public int Left => _window.Left;
    public int Top => _window.Top;
}
```

- [ ] **Step 2: `locate_element` ツールを `InspectionTools` に追加**

`Flax.Mcp/Tools/InspectionTools.cs` の先頭の using 群に追加:
```csharp
using System.Threading;
using Flax.Mcp.Llm;
```

`InspectionTools` クラス内（`CaptureWindow` メソッドの後、クラス閉じ `}` の前）に追加:
```csharp
    [McpServerTool, Description("Locate one UI element from a natural-language target using the server-side locator model (a cheap model configured via FLAX_LLM_*). Returns a small result you can pass straight to click: tree mode -> { elementId }, vision mode -> { x, y } in absolute screen coordinates. mode: auto (default, tree then vision) | tree | vision.")]
    public static string LocateElement(SessionManager sessions, ElementLocator locator, string sessionId, string target, string? mode = null) => ToolRunner.Run(() =>
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });

        var outcome = new LocateService()
            .LocateAsync(locator, new FlaxWindowLocateAdapter(window), target, LocateModeParser.Parse(mode), CancellationToken.None)
            .GetAwaiter().GetResult();

        return outcome.Ok
            ? Json.Of(new { ok = true, mode = outcome.Mode, elementId = outcome.ElementId, x = outcome.X, y = outcome.Y, confidence = outcome.Confidence, reasoning = outcome.Reasoning })
            : Json.Of(new { ok = false, error = outcome.Error, hint = outcome.Hint, reasoning = outcome.Reasoning });
    });
```

- [ ] **Step 3: `Program.cs` で設定解決と DI 登録を追加**

`Flax.Mcp/Program.cs` の using 群に追加:
```csharp
using Flax.Mcp.Llm;
using Microsoft.Extensions.AI;
```

`builder.Services.AddSingleton<SessionManager>();` の直後に追加:
```csharp
// Server-side element locator (cheap, independently-configured model). Always registered; the tool
// branches on Status so the MCP DI injection never breaks when the LLM is unconfigured.
var llm = LlmOptions.Resolve(builder.Configuration, Environment.GetEnvironmentVariable);
var apiKey = string.IsNullOrEmpty(llm.ApiKeyEnvVar) ? null : Environment.GetEnvironmentVariable(llm.ApiKeyEnvVar);
var locatorStatus = llm.GetStatus(apiKey);
IChatClient? chatClient = locatorStatus == LocatorStatus.Ready ? ChatClientFactory.Create(llm, apiKey!) : null;
builder.Services.AddSingleton(new ElementLocator(chatClient, locatorStatus, llm.MaxOutputTokens, llm.Model));
```

- [ ] **Step 4: ビルドして 0 エラーを確認**

Run: `dotnet build Flax.Mcp/Flax.Mcp.csproj -c Debug`
Expected: `Build succeeded` / `0 Error(s)`。

- [ ] **Step 5: 全テストを実行（既存含め回帰がないこと）**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj`
Expected: 既存 + 新規の非 `[Explicit]` テストが全て PASS（`ToolSmokeTests` は `[Explicit]` でスキップ）。

- [ ] **Step 6: コミット**

```powershell
git add Flax.Mcp/Llm/FlaxWindowLocateAdapter.cs Flax.Mcp/Tools/InspectionTools.cs Flax.Mcp/Program.cs
git commit -m @'
feat: add locate_element MCP tool wired to server-side locator

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
'@
```

---

## Task 8: マルチクライアント・ドキュメント（README 追記）

設計書 §6。`locate_element` の使い方と、opencode/Cline/汎用 stdio への登録（env でキー注入）を文書化。

**Files:**
- Modify: `README.md`（無ければ `Flax.Mcp/README.md` を新規作成。リポジトリの既存 README 配置に合わせる — まず `Glob` で確認）

- [ ] **Step 1: 既存 README の場所と MCP 記述を確認**

Run: `Glob` で `**/README*.md` を検索し、既存の Flax.Mcp / Claude Desktop 登録の節を `Grep`（パターン `claude_desktop_config`）で特定する。その節の近くに追記する。

- [ ] **Step 2: マルチクライアント節を追記**

該当 README に次を追記（実際の exe パスは各環境に合わせる旨を明記）:
````markdown
## 他の MCP クライアントから使う（opencode / Cline / 汎用 stdio）

Flax.Mcp は標準の stdio MCP サーバなので、Claude Desktop 以外のクライアントからも使えます。
サーバ側の「要素判別モデル」（`locate_element` 用、任意）は、クライアントが使う高級モデルとは
**別に・安いモデルで** 設定できます。キーは必ず環境変数から読みます（設定ファイルには書きません）。

### opencode (`opencode.json`)

```json
{
  "mcp": {
    "flax": {
      "type": "local",
      "command": ["C:\\path\\to\\Flax.Mcp.exe"],
      "environment": {
        "FLAX_LLM_PROVIDER": "openai",
        "FLAX_LLM_MODEL": "gpt-4o-mini",
        "OPENAI_API_KEY": "{env:OPENAI_API_KEY}"
      }
    }
  }
}
```

- `FLAX_LLM_PROVIDER` … `openai` / `azure` / `anthropic`
- `FLAX_LLM_MODEL` … 要素判別用の **安いモデル** 名（例: `gpt-4o-mini`）
- キーは `OPENAI_API_KEY` / `AZURE_OPENAI_API_KEY` / `ANTHROPIC_API_KEY`
  （別名にする場合は `FLAX_LLM_API_KEY_ENV` でenv名を指定）
- Azure は加えて `FLAX_LLM_BASE_URL`（エンドポイント）と必要に応じ `FLAX_LLM_API_VERSION`

### Cline / 汎用 stdio クライアント

同様に stdio コマンドとして `Flax.Mcp.exe` を登録し、`env` に `FLAX_LLM_*` とプロバイダのキーを注入します。

### 棲み分け

- **クライアントのモデル**（頭脳・ツール選択）はクライアント側で各自設定。
- **Flax.Mcp の `Llm` 設定**は `locate_element` 専用の安いモデル。**未設定でも他の全ツールは動作**します
  （`locate_element` だけが `llm_not_configured` を返す）。
- `locate_element` は UI ツリー/画像の判別をサーバ側の安いモデルに肩代わりさせ、クライアントには
  `elementId` か `x,y` という小さな結果だけ返すため、クライアント側のトークンを節約できます。

### appsettings.json（任意フォールバック）

env を使わない場合、`Flax.Mcp.exe` と同じフォルダの `appsettings.json` の `"Llm"` 節でも設定できます
（**キーは置かないこと**。キーは常に環境変数から）。
````

- [ ] **Step 3: コミット**

```powershell
git add README.md
git commit -m @'
docs: document multi-client registration and locate_element usage

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
'@
```

---

## Task 9: 最終ビルド・全テスト確認

**Files:** なし（検証のみ）

- [ ] **Step 1: ソリューション全体をビルド**

Run: `dotnet build Flax.sln -c Debug`
Expected: `Build succeeded` / `0 Error(s)`。

- [ ] **Step 2: 全テスト（非 Explicit）を実行**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj`
Expected: 全 PASS、Failed 0。

- [ ] **Step 3: 作業ツリーに想定外の差分がないか確認**

Run: `git status`
Expected: クリーン（コミット済み）。`Flax.sln` の事前変更（セッション開始時の `M Flax.sln`）はこの作業の対象外 — **コミットに含めない**。

---

## 実 API スモーク（任意・課金あり・`[Explicit]`）

`ChatClientFactory` と各コネクタの実 API・画像入力（§9）を実キーで確認したい場合のみ、`[Explicit]` テストを
`Flax.Mcp.Tests` に追加して手動実行する（CI ではスキップ）。本計画の必須スコープ外。

- 環境変数 `FLAX_LLM_PROVIDER` / `FLAX_LLM_MODEL` / 各 `*_API_KEY` を設定。
- `ChatClientFactory.Create` → `ElementLocator.LocateInTreeAsync` に小さなツリー JSON を渡し、
  例外なく `LocateResult` が返ることを確認。

---

## Self-Review（spec 対応チェック）

- **§0 ツール一覧/フロー** → `locate_element` を Task 7 で追加、README を Task 8 で更新。✅
- **§2 アーキテクチャ（別プロジェクト無し / Llm フォルダ / 4パッケージ）** → Task 1・各 Create。✅（`Azure.AI.OpenAI` 追加は spec §2 の補正としてTask 1に明記）
- **§3 LlmOptions（env優先・既定キーenv・MaxTokens）/ ChatClientFactory / ElementLocator(Status, tree/vision, JSON解析失敗→found=false)** → Task 2・5・6。✅
- **§4 locate_element（入力 sessionId/target/mode、auto/tree/vision、座標換算、出力封筒、エラー種別）** → Task 4（ロジック）+ Task 7（ツール）。✅
- **§7 エラー（session_not_found / llm_not_configured / llm_key_missing / llm_error / element_not_found）** → Task 4・7 で全て実装・テスト。✅
- **§8 テスト（LlmOptionsバインド / ElementLocatorフェイク / locate_element封筒 / 実APIは Explicit）** → Task 2・4・6 + 任意スモーク。✅
- **§9 要検証（バージョン固定・0エラービルド・AsIChatClient/DataContent・座標精度）** → Task 1・5・6 のビルド検証 + 任意スモーク。座標精度（DPI）は実機 E2E に委ねる旨を spec に残置。⚠️（実機 DPI 検証は本計画外）
- **キーは環境変数のみ・appsettings にキーを書かない** → Task 1 appsettings は空、`.gitignore` に `*.local.json`。✅
- **型整合**: `LlmOptions.GetStatus` / `LocatorStatus` / `LocateResult(Id,Px,Py)` / `IElementLocator` / `ILocateWindow` / `LocateOutcome` / `LocateMode` / `ElementLocator(client,status,maxTokens,model)` を全タスクで一貫使用。✅
