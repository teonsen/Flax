# Flax.Mcp サーバ側「要素判別 LLM」設計書

- 日付: 2026-05-24
- 対象: Flax.Mcp に、設定可能な LLM プロバイダ/キーを持つサーバ側の「UI 要素判別」機能を追加する
- 前提となる既存設計: [2026-05-23-flax-mcp-server-design.md](2026-05-23-flax-mcp-server-design.md)

## 1. 背景と目的

`Flax.Mcp` は標準の stdio MCP サーバで、Claude Desktop / Claude Code に加え、
opencode・Cline など任意の MCP クライアントから利用できる。MCP クライアントは
それぞれ自前の API プロバイダ/キー（Anthropic / OpenAI 等）で「頭脳」を動かす。

本設計の目的は2つ。

1. **マルチクライアント対応の明文化**: opencode 等への登録手順をドキュメント化する
   （サーバはすでに任意クライアントで動くため、コード変更は不要）。
2. **サーバ側の「要素判別 LLM」の追加（本丸）**: UI ツリー／スクリーンショットから
   目的の UI 要素を特定する処理はトークンを大量に消費するが、高級モデルである必要はない。
   この判別だけを、**クライアントのモデルとは独立に設定した安いモデル**へオフロードできるようにする。

### 確定した前提（本設計のブレインストーミングで決定）

- **スタンドアロンホスト（独立エージェント）は作らない。** MCP クライアントが頭脳を担う。
- **対応クライアント**: Claude Desktop / Claude Code に加え opencode・Cline・汎用 stdio。
- **サポートするプロバイダ**: Anthropic / OpenAI / Azure OpenAI の3種。Vision のため画像入力必須。
- **設定方式**: `appsettings.json` にプロバイダ/モデル/baseURL/apiVersion、**API キーは環境変数**
  （env がファイルを上書き）。
- **サーバ側 LLM はホストのモデルと独立** — 要素判別専用に安いモデルを設定する。

### 解決する課題

`get_element_tree` の JSON や `capture_window` の PNG を**クライアントの高級モデル**が
直接飲み込むと、トークンを浪費する。とりわけ Windows 11 のモダンアプリ（WinUI3/UWP、例: 電卓）は
UIA ツリーが浅くしか取れず、Vision での座標読みが必要になり、画像トークンも増える。
さらに opencode 等で**非 Vision モデル**を設定している場合、`capture_window` の画像を
モデルが読めず WinUI3 アプリが操作できない。

本設計では、サーバ内に設定した安いモデルが「ツリー/画像 → 目的要素」の重い判別を肩代わりし、
クライアントには `elementId` か座標という**小さな結果だけ**を返す。

## 2. アーキテクチャ

```
Flax.sln
├── Flax/             既存ライブラリ（変更なし）
├── Flax.Mcp/         locate_element 追加 / Flax.Llm 参照 / appsettings.json 追加
├── Flax.Llm/   ★新規  プロバイダ非依存の LLM クライアント抽象 + 3実装 + 設定（net8.0・UI非依存）
└── Flax.Llm.Tests/ ★新規
```

- `Flax.Llm` は HTTP で LLM API を呼ぶだけで UI 依存がないため `net8.0`（`-windows` なし）。
  `net8.0-windows` の `Flax.Mcp` から問題なく参照できる。
- `Flax.Mcp` は `Flax.Llm` を参照し、`appsettings.json` の `"Llm"` セクションから
  `ILlmClient` を DI 登録する。**LLM 未設定でもサーバは起動する**（`locate_element` だけが
  `llm_not_configured` を返す）。

### 設定の独立性（本設計の肝）

```
MCP クライアント (opencode / Claude Desktop) ── 高級モデル（コーディング等、ユーザがクライアント側で設定）
        │  ツール呼び出し（locate_element 等）／結果は小さい
        ▼
Flax.Mcp ── ★安いモデル（要素判別専用、appsettings.json + env で設定）
        │
        ▼
Windows / UIA
```

## 3. Flax.Llm（プロバイダ抽象）

### 中立型（プロバイダ非依存）

- `LlmMessage { Role, Content[] }`（Role = system/user/assistant）
- `LlmContent`: テキスト / 画像（PNG bytes）。判別タスクは1往復なので tool_use/tool_result は不要。
- `LlmRequest { System?, Messages[], MaxTokens, Temperature? }`
- `LlmResponse { Text }`（判別結果の構造化 JSON はこの Text に入れて返させ、呼び出し側で解析）

### インターフェースと実装

- `interface ILlmClient { Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct); }`
- 実装3つ。各々が中立型 ⇄ プロバイダ wire 形式を変換し、**画像入力（base64 PNG）**に対応する。
  - `AnthropicLlmClient` — Messages API（`content` に `{type:"image", source:{type:"base64",...}}`）
  - `OpenAiLlmClient` — Chat Completions（`image_url` に `data:image/png;base64,...`）
  - `AzureOpenAiLlmClient` — OpenAI 互換だが `BaseUrl` のデプロイ名 + `api-version` クエリが必須
- `LlmClientFactory.Create(LlmOptions, HttpClient)` → 適切な実装を返す。
- `HttpClient` は `IHttpClientFactory` 経由で注入（テストで `HttpMessageHandler` 差し替え可能に）。

### 設定 `LlmOptions`

| キー | 説明 | 例 |
|---|---|---|
| `Provider` | `Anthropic` / `OpenAI` / `Azure` | `Anthropic` |
| `Model` | モデル/デプロイ名（**安いモデルを指定**） | `claude-haiku-4-5` |
| `BaseUrl` | 任意。Azure や互換エンドポイント用 | `https://xxx.openai.azure.com/...` |
| `ApiVersion` | Azure 用 | `2024-10-21` |
| `MaxTokens` | 既定 1024 | `1024` |
| `ApiKeyEnvVar` | キーを読む環境変数名（既定はプロバイダ別） | `MY_KEY` |

- `appsettings.json` の `"Llm"` セクションに記述。**API キーはファイルに書かず環境変数**から読む
  （既定 `ANTHROPIC_API_KEY` / `OPENAI_API_KEY` / `AZURE_OPENAI_API_KEY`、`ApiKeyEnvVar` で上書き）。
- 環境変数がファイル設定を上書きする（`Host.CreateApplicationBuilder` 既定の構成順）。
- `Provider` 未設定 → `ILlmClient` を登録しない（`locate_element` が `llm_not_configured` を返す）。
- `Provider` は設定済みだがキー環境変数が空 → `locate_element` 実行時に `llm_key_missing` を返す
  （サーバ起動自体は妨げない）。

## 4. `locate_element`（Flax.Mcp の新ツール）

`Tools/InspectionTools.cs` に追加（既存ツールと同じ `ToolRunner.Run` パターン）。

- **入力**: `sessionId`, `target`（自然言語、例 `"「1」ボタン"`）, `mode?`（`auto` 既定 / `tree` / `vision`）
- **動作**:
  1. **tree モード**: `window.GetElementTreeAsJson()` の JSON ＋ `target` を `ILlmClient` に渡し、
     「該当ノードの `id` を JSON で返せ。無ければ `found:false`」と依頼。`id` を解析して返す。
     → クライアントはツリーを受け取らずに済む（トークン節約の本丸）。
  2. **vision モード**: `window.CaptureToPngBytes()` の PNG ＋ `target` を渡し、
     画像内のピクセル座標を依頼。返ったピクセル `(px,py)` を**画面座標** `x=Left+px, y=Top+py`
     に変換して返す（クライアントはそのまま `click(x,y)` できる）。
  3. **auto**: tree を試し、ツリーが取得不可（`GetElementTreeAsJson==null`、WinUI3）または
     `found:false` の場合に vision へ自動フォールバック。
- **出力（成功）**: `{ ok:true, mode:"tree"|"vision", elementId?, x?, y?, confidence?, reasoning? }`
  - tree → `elementId`、vision → `x,y`(画面絶対座標)。どちらも既存 `click` にそのまま渡せる。
- **出力（エラー）**: 既存と同じ封筒 `{ ok:false, error, hint }`。
  - `session_not_found` / `llm_not_configured` / `llm_key_missing` / `llm_error`（API 失敗）/
    `element_not_found`（tree/vision とも該当なし）。
- LLM 応答の JSON 解析が失敗した場合は `llm_error`（`reasoning` に生テキスト断片を含めヒント化）。

### 既存ツールとの関係

- `get_element_tree` / `capture_window` は**据え置き**。自前で判別したいクライアント（高級 Vision を
  持つ Claude Desktop 等）は従来どおり使える。`locate_element` は安いモデルへオフロードしたい
  クライアント向けの追加経路。
- `find_element`（アクセシブル名の厳密一致、LLM 不使用）と `locate_element`（意味的判別、LLM 使用）は
  役割が異なり共存する。

## 5. データフロー（opencode + 安いモデルで「電卓の1ボタン」）

```
opencode(高級モデル)        Flax.Mcp + Flax.Llm(安いモデル)        Windows/UIA
 │ launch_app("calc")  ───▶                                   ─▶ 電卓起動
 │ open_window("%電卓%")───▶ sessionId 発番
 │ locate_element(sid,"1")─▶ tree 取得→浅い(WinUI3)→vision へ
 │                           capture→安いモデルが座標判定
 │   ◀── {mode:"vision", x,y}（小さい結果だけ返る）
 │ click(sid, x, y)    ───▶ Mouse.Click(x,y)                  ─▶ 実クリック
```

クラシックアプリ（メモ帳等）では `locate_element` が tree モードで `elementId` を返し、
`click(elementId)` で UIA Invoke。高級モデルは巨大ツリーも画像も受け取らない。

## 6. マルチクライアント・ドキュメント（README 追記）

- **opencode**: `opencode.json` の `mcp` に Flax.Mcp.exe を stdio コマンドとして登録する例。
- **Cline / 汎用 stdio クライアント**: 同様の stdio 登録例。
- 棲み分けの明記: 「クライアント側で各自の API キーを設定する」前提と、
  「**Flax.Mcp 側の `Llm` 設定は要素判別専用の安いモデル**（任意・未設定でも他ツールは動く）」。
- `appsettings.json` の `"Llm"` 設定例とプロバイダ別 env var 一覧。秘密情報を含まないため
  `appsettings.json` はコミット可。`appsettings.*.local.json` は `.gitignore`。

## 7. エラー処理

ツールは例外を投げず、構造化結果 `{ ok, error, hint }` を返す（既存方針を踏襲）。

| error | 状況 |
|---|---|
| `session_not_found` | 不正/期限切れ sessionId |
| `llm_not_configured` | `Llm:Provider` 未設定（→「クライアント側で判別するか Llm を設定」） |
| `llm_key_missing` | プロバイダ設定済みだがキー環境変数が空 |
| `llm_error` | API 呼び出し失敗 / 応答 JSON 解析失敗 |
| `element_not_found` | tree・vision とも該当要素を特定できず |

## 8. テスト

- **Flax.Llm.Tests**:
  - 各プロバイダの 中立→wire 変換を `HttpMessageHandler` モックで検証
    （リクエスト JSON 形状、画像が base64 で正しく載るか、Azure の url+api-version）。
  - 応答 wire→`LlmResponse` の解析。
  - 設定ロード: env がファイルを上書き、キー欠如の検出。
- **Flax.Mcp.Tests**: `locate_element` を fake `ILlmClient` で検証
  （tree ヒット→`elementId`、tree ミス→vision フォールバック→画面座標換算、
  `llm_not_configured` / `llm_key_missing` / `llm_error` の各封筒）。
- **統合スモーク**: 実 API キーで課金が発生するため `[Explicit]`（CI スキップ）。

## 9. 未確定・要検証項目

- 各プロバイダの最新 wire 仕様（特に画像コンテンツ）と、判別タスクに適した安価モデルの実名。
- `locate_element` に渡すツリー JSON のサイズ上限（巨大ツリー時の切り詰め要否）。
- vision モードの座標精度（DPI スケーリング下での `Left/Top` 換算）。
