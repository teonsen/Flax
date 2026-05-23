# Flax MCP サーバー 設計書

- 日付: 2026-05-23
- 対象: Flax の UI 要素取得機能を MCP ツール化し、LLM が自然言語で Windows GUI アプリを操作できるようにする
- ゴール: 再利用可能な製品レベルの MCP サーバー

## 1. 背景と目的

Flax は FlaUI(UIA3) のラッパー（net472 C# クラスライブラリ）。直近で UI 要素ツリーを
トークン効率の良い JSON として取得する機能（`FlaxWindow.GetElementTreeAsJson` /
`GetElementById`）を追加した。

本設計では、この機能を MCP（Model Context Protocol）サーバーのツールとして公開し、
LLM が「電卓で1+1を計算して」のような指示から、

1. アプリ起動 → 2. UI 要素ツリー取得 → 3. 目的要素（位置）特定 → 4. クリック実行

という一連の操作を自律的に行えるようにする。

### 確定した前提

- **ゴール**: 再利用可能な製品レベル
- **クライアント**: Claude Desktop / Claude Code（→ stdio トランスポート）
- **Click 方式**: ID 優先・座標 fallback
- **モダンアプリ対策**: スクリーンショット + Vision で補完
- **ランタイム/統合方式**: 方式A（net8.0-windows + 公式 C# MCP SDK、Flax をマルチターゲット化）
- **セッション管理**: sessionId 明示方式

### 既知の重要な制約（UIA の限界）

Windows 11 のモダンアプリ（WinUI3 / UWP、例: 新しい電卓・メモ帳・Paint のキャンバス）は、
コントロールが XAML island に存在するため、アウトプロセスの UI Automation ではツリーが浅くしか
取得できない（README に明記済み）。指定例の「電卓」はこれに該当するため、UIA ツリーだけでは
数字ボタンを特定できない。本設計では **スクリーンショット + Vision による座標クリック** を
二段構えのフォールバックとして用意し、この制約を吸収する。

## 2. アーキテクチャ

### プロジェクト構成

```
Flax.sln
├── Flax/                    ← net472;net8.0-windows にマルチターゲット化（既存ライブラリ）
├── Flax.Tests/              ← 既存テスト
└── Flax.Mcp/                ← 新規: net8.0-windows コンソール（MCP サーバー本体）
       ├── Program.cs              ホスト構築・stdio 起動
       ├── SessionManager.cs       セッション登録簿
       └── Tools/
            ├── WindowTools.cs     launch_app / list_windows / open_window / activate_window / close_window
            ├── InspectionTools.cs get_element_tree / find_element / capture_window
            └── ActionTools.cs     click / type_text / send_keys / scroll
```

- 公式 `ModelContextProtocol` C# SDK を使用。`[McpServerTool]` 属性でツールを定義し、stdio で起動。
- `Flax.Mcp` は `Flax`（net8.0-windows ターゲット）を参照する。
- Flax のマルチターゲット化に伴い、`net8.0-windows` 側で `UseWindowsForms` / `UseWPF` を有効化し、
  FlaUI 3.0 / System.Drawing / WinForms / WPF 参照が解決することを確認する（FlaUI 3.0 は modern .NET 対応のため低リスク。要検証項目）。

### セッションモデル（最重要）

MCP のツール呼び出しは個別だが、UIA セッション（`WindowsAutomation` と開いた `FlaxWindow`、
その `_elementMap` スナップショット）はプロセスをまたいで生かす必要がある。stdio サーバーは
長命プロセスなので、これを `SessionManager` がシングルトンで保持する。

- `open_window` が `FlaxWindow` を生成し、短い文字列の `sessionId` を発番して登録簿に格納。
  以降の全ツールは `sessionId` を引数に取る。
- 要素 `id` は既存仕様どおり「直近の `get_element_tree`（または `find_element`）スナップショット内でのみ有効」。
  `click(elementId)` は該当セッションの `GetElementById` を引く。
- `find_element` は見つけた要素をそのセッションの map に新規 id 登録して返す（→ 既存の
  ID 優先・座標 fallback クリックがそのまま使える）。
- `close_window` / セッション解放で `FlaxWindow.Dispose()`（COM ハンドル解放）。

## 3. ツール一覧（v1）

| ツール | 入力 | 出力 | ラップ先 |
|---|---|---|---|
| `launch_app` | `path`(exe/名前), `args?` | 起動可否 | `Process.Run` |
| `list_windows` | なし | `[{title,pid,className,rect,minimized}]` | `GetWindowList` |
| `open_window` | `titleQuery`(% ワイルドカード可), `timeoutSec?` | `sessionId`, ウィンドウ情報 | `GetWindow`+`SetFlaUIWindow` |
| `activate_window` | `sessionId` | 結果 | `FlaxWindow.Activate` |
| `close_window` | `sessionId` | 解放結果 | `FlaxWindow.Dispose`/`Close` |
| `get_element_tree` | `sessionId`, `maxDepth?`, `includeOffscreen?` | JSON ツリー(`id`/`rect`…) | `GetElementTreeAsJson` |
| `find_element` | `sessionId`, `name` | 要素情報(新規 id 付与) | `GetElementByName` |
| `capture_window` | `sessionId` | image content(PNG base64) | `FlaxWindow.Capture` |
| `click` | `sessionId`, `{elementId}` か `{x,y}`, `button?`, `double?` | クリック結果 | `GetElementById().Click()` / `Mouse.Click(x,y)` |
| `type_text` | `sessionId`, `text` | 入力結果 | `Keyboard.Type` |
| `send_keys` | `sessionId`, `keys`(例 "ENTER") | 入力結果 | `Keyboard.Enter()` 等へマップ |
| `scroll` | `sessionId`, `lines`, `horizontal?` | 結果 | `Mouse.VerticalScroll`/`HorizontalScroll` |

### click のディスパッチ（ID 優先・座標 fallback）

- `elementId` 指定時: 該当要素の UIA Click を試行 → 失敗または要素が失効していれば、その要素の
  中心座標へマウスクリックでフォールバック。
- `x,y` 指定時: `Mouse.Click(x,y)` を直接実行（Vision 経路）。
- `button`(left/right)、`double` を任意指定可能。

### send_keys のキーマップ

`"ENTER"`, `"ESC"`, `"TAB"`, `"BACKSPACE"`, `"DELETE"`, 方向キー、`"CTRL+A/C/V"` 等を
`FlaxKeyboard` の対応メソッドへマップする。任意文字列の入力は `type_text` を使う。

## 4. データフロー（「電卓で1+1」エンドツーエンド）

```
LLM                          MCP(Flax.Mcp)              Windows/UIA
 │ launch_app("calc.exe")  ─────▶ Process.Run            ─▶ 電卓起動
 │ open_window("%電卓%")    ─────▶ GetWindow+SetFlaUI     ─▶ sessionId 発番
 │ get_element_tree(sid)    ─────▶ GetElementTreeAsJson   ─▶ (WinUI3=浅いツリー)
 │   └─ ボタンが取れない と判断
 │ capture_window(sid)      ─────▶ Capture → PNG          ─▶ 画像返却(Vision)
 │   └─ Vision が "1""+""=" のピクセル座標を読む
 │ click(sid, x,y) ×数回    ─────▶ Mouse.Click(x,y)       ─▶ 実クリック
 │ capture_window(sid)      ─────▶ 結果確認(=2 を確認)
```

クラシックアプリ（メモ帳 / Win32 / WPF）の場合は手順 4-5（capture + Vision）が不要で、
`get_element_tree` → `click(sid, elementId)`(UIA Invoke) で完結する。WinUI3 では Vision 経由の
座標クリックに自然に切り替わる、という二段構えが設計の肝。

## 5. エラー処理

ツールは例外を投げず、**構造化された結果**（成功フラグ＋メッセージ）を返す。
LLM が次の手（再スナップショット、capture_window への切替）を選べるよう、メッセージに
回復のヒントを含める。

- `session_not_found`: 不正/期限切れ sessionId。
- `window_not_found`: `open_window` がタイムアウト。
- `element_not_found`: `click(elementId)` で id がスナップショットに無い（→「get_element_tree を再取得して」と促す）。
- `stale_element`(COMException): クリック時に要素が失効 → 座標 fallback を試行、それも不可ならエラー。
- `click_failed` / `offscreen`: 対象が画面外。

## 6. テスト

- **単体**: `SessionManager`（発番・期限切れ・解放）と `click` のディスパッチ判定
  （elementId 有→UIA、失敗→座標 fallback、x,y 指定→直接）を、UIA を抽象化した小さな
  ファサード越しにモックして検証。
- **統合スモーク**: 既存 `Flax.Tests/SmokeTests.cs` に倣い、クラシックアプリ（mspaint / notepad）に対して
  open_window → get_element_tree → click(elementId) の往復を検証。
- **手動 E2E**: Claude Desktop に登録し「電卓で1+1」を実走（WinUI3 + Vision 経路の確認）。

## 7. 未確定・要検証項目

- Flax を `net8.0-windows` にマルチターゲット化したときの FlaUI 3.0 / WinForms / WPF 参照解決。
- 公式 `ModelContextProtocol` C# SDK のバージョンと stdio / 画像コンテンツ返却の API 詳細。
- セッションの寿命（アイドルタイムアウトの要否）。
