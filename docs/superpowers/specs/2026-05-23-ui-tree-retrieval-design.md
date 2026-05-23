# UI ツリー取得機能(LLM 連携向け)設計

- 日付: 2026-05-23
- 対象リポジトリ: Flax(FlaUI / UIA3 ラッパー, .NET Framework 4.7.2)
- ステータス: 設計承認済み

## 1. 目的・背景

LLM に Windows アプリの UI 要素ツリーを渡し、「何をクリック/操作すべきか」を判断させたい。
computer-use や Playwright のアクセシビリティスナップショットに近い使い方を、Flax 上で実現する。

現状の Flax は `FindFirstDescendant` ベースの**単一要素取得**(`GetElementByName` / `GetElementByAutomationID`)のみで、
複数要素やツリーを一覧取得する API が存在しない。本機能でこれを追加する。

## 2. ユースケース(1 ターンのループ)

```
window.GetElementTreeAsJson()  ->  JSON 文字列を LLM へ
                                     |
                          LLM が「id:42 をクリック」と判断
                                     v
window.GetElementById(42).Click()  ->  実操作
```

- 取得 -> LLM 判断 -> ID 指定で操作、を毎ターン繰り返す前提。
- 毎ターン再取得するため、ID は**スナップショット内で一意**であれば十分。永続的な安定性は不要。

## 3. データモデル: `UIElement` への追加

`Flax/Windows/UIElement.cs` の `UIElement` に以下を追加する。

| メンバ | 型 | 説明 |
| --- | --- | --- |
| `Id` | `int` | ツリー採番時の一意 ID。単一取得時は `-1`。`internal set`。 |
| `ControlType` | `string` | 例: `"Button"`, `"Edit"`, `"Window"`。コンストラクタで `_ae` から安全に取得、失敗時は `""`。 |
| `Children` | `IReadOnlyList<UIElement>` | ツリー取得時のみ設定。単一取得時は空リスト。`internal set`。 |

- `ControlType` は現状欠けており、LLM が要素種別を判断するために必須。
- `ControlType` の取得は例外を投げうるため try/catch で囲み、失敗時は `""` を設定する。

## 4. `FlaxWindow` への追加メソッド

`Flax/Windows/FlaxWindow.cs` に以下を追加する。

### `string GetElementTreeAsJson(int maxDepth = -1, bool includeOffscreen = false)`

1. 最小化ウィンドウは既存 `GetElementCommon` と同様に復元/アクティブ化する。
2. `_FlaUIWindow` を起点に子孫を**プレオーダー(深さ優先・前順)走査**し、連番 ID を採番しながら `UIElement` ツリーを構築する。
   - ルート(ウィンドウ自身)を `id = 0` とし、以降出現順に採番。
   - 子の列挙は FlaUI の `FindAllChildren()` を再帰的に使用。
3. `maxDepth`:
   - `-1`(既定) = 無制限。
   - `0` = ルートのみ。
   - `n` = ルートから深さ `n` まで。
4. `includeOffscreen`:
   - `false`(既定) = `IsOffscreen` の要素を**除外**(トークン節約)。除外した要素の子孫も辿らない。
   - `true` = オフスクリーン要素も含める。
5. 採番した `id -> UIElement` を `FlaxWindow` 内部の `Dictionary<int, UIElement>` に保持する(呼び出しごとにリセット)。
6. ルートを JSON 化して文字列で返す。

### `UIElement GetElementById(int id)`

- 直近の `GetElementTreeAsJson` で構築したマップから該当 `UIElement` を引いて返す。
- マップ未構築、または `id` が存在しない場合は `null`。
- 返した `UIElement` は内部に `_ae` を保持しているため、そのまま `Click()` / `DoubleClick()` 等が可能。

## 5. JSON スキーマ(トークン効率重視)

`UIElement` を直接シリアライズせず、**専用の軽量 DTO `UINode`** に詰め替えてから Newtonsoft で出力する。
理由: JSON 形状を完全に制御でき、かつ `UINode` は FlaUI 非依存のため単体テストが容易になる。

### `UINode`(シリアライズ専用、private/internal)

```csharp
internal class UINode
{
    public int id;
    public string controlType;
    public string name;         // 空なら省略
    public string automationId; // 空なら省略
    public string className;    // 空なら省略
    public int[] rect;          // [x, y, width, height]
    public bool enabled;
    public bool visible;
    public List<UINode> children; // 空なら省略
}
```

### シリアライズ設定

- `NullValueHandling.Ignore` を使い、空文字は `null` に置き換えて省略する(トークン節約)。
- `children` は空のとき省略する。
- `rect` は `[x, y, width, height]` の配列で簡潔に表現する。

### 出力例

```json
{
  "id": 0,
  "controlType": "Window",
  "name": "電卓",
  "rect": [0, 0, 322, 460],
  "enabled": true,
  "visible": true,
  "children": [
    {
      "id": 1,
      "controlType": "Button",
      "name": "1",
      "automationId": "num1Button",
      "rect": [10, 400, 70, 50],
      "enabled": true,
      "visible": true
    }
  ]
}
```

## 6. エラー処理・境界条件

- `ControlType` 取得失敗 -> `""`。
- `GetElementById` をツリー取得前に呼ぶ -> `null`。
- `id` がマップに無い -> `null`。
- `maxDepth = 0` -> ルートノードのみ。
- 巨大 UI(数千〜数万要素)-> `maxDepth` + `includeOffscreen=false` の既定で抑制。
- 最小化ウィンドウ -> 復元/アクティブ化してから走査。

## 7. 依存追加

- `Newtonsoft.Json`(Json.NET)を NuGet で追加。
  - `Flax/packages.config` にパッケージ記述を追加。
  - `Flax/Flax.csproj` に `<Reference>`(HintPath 付き)を追加。
  - パッケージ復元(`nuget restore` / VS の復元)が必要。

## 8. テスト方針

本リポジトリは現状テストプロジェクトを持たない。UIA 走査の実機テストは環境依存で難しいため、2 段構えとする。

### 8.1 純粋ロジックの単体テスト

- 最小のテストプロジェクト(NUnit 等)を追加。
- `UINode` ツリー -> JSON 変換の形状を検証:
  - 空フィールド(`name` / `automationId` / `className`)が省略されること。
  - `rect` が `[x, y, width, height]` の配列で出力されること。
  - 空の `children` が省略されること。
  - ネストしたツリーが正しく入れ子の JSON になること。
- `UINode` は FlaUI 非依存なので、ハンドメイドのツリーで検証可能。

### 8.2 実機スモークテスト(手動)

- 電卓(`calc.exe`)またはメモ帳を起動。
- `GetElementTreeAsJson()` の結果に期待する `controlType`(例: `Button`)や `name` が含まれることを目視確認。
- `GetElementById(n).Click()` で対象要素がクリックされることを確認。
- 手順は本 spec に記録し、回帰時に再実行できるようにする。

## 9. スコープ外(YAGNI)

- 条件一致の複数要素取得(`GetElementsByName` 等)は今回のスコープ外。必要になれば別途追加。
- ツリーの遅延読み込み(ラジー)は採用しない(毎ターン再取得する前提のため不要)。
- ID の永続的な安定化(UI 変化をまたいだ同一性保証)は行わない。
```