# UI ツリー取得機能(LLM 連携向け)Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Flax に、Windows の UI 要素ツリーを LLM 向け JSON で取得する `FlaxWindow.GetElementTreeAsJson()` と、そこで採番した ID から要素を取り戻して操作する `FlaxWindow.GetElementById(int)` を追加する。

**Architecture:** 各ノードに連番 ID を採番しながら `_FlaUIWindow` 配下をプレオーダー走査して `UIElement` ツリーを構築し、`id -> UIElement` のマップをウィンドウ内部に保持する。シリアライズは FlaUI 非依存の軽量 DTO `UINode` に詰め替えてから Newtonsoft.Json で出力する(JSON 形状の制御と単体テスト容易性のため)。前提として、`dotnet` CLI 単体でビルド/テストできるよう、既存の Flax プロジェクトを classic(packages.config)から SDK スタイル(PackageReference)へ移行する。

**Tech Stack:** C# / .NET Framework 4.7.2(net472, SDK スタイル csproj)、FlaUI 3.0(UIA3)、Newtonsoft.Json 13、NUnit 4 + NUnit3TestAdapter、.NET 10 SDK の `dotnet` CLI。

**全コミット共通:** コミットメッセージ末尾に必ず次のトレーラを付与する(本環境のルール)。
`Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`

---

## File Structure

| ファイル | 役割 | 操作 |
| --- | --- | --- |
| `Flax/Flax.csproj` | プロジェクト定義。SDK スタイル + PackageReference へ移行。 | 全面書き換え |
| `Flax/packages.config` | 旧 NuGet 復元定義。SDK 移行で不要。 | 削除 |
| `Flax/Windows/UINode.cs` | シリアライズ専用 DTO + `ToJson()`。FlaUI 非依存。 | 新規 |
| `Flax/Windows/UIElement.cs` | `Id` / `ControlType` / `Children` を追加。 | 変更 |
| `Flax/Windows/FlaxWindow.cs` | `GetElementTreeAsJson` / `GetElementById` とツリー走査を追加。 | 変更 |
| `Flax.Tests/Flax.Tests.csproj` | テストプロジェクト(SDK スタイル, net472, NUnit)。 | 新規 |
| `Flax.Tests/UINodeTests.cs` | `UINode.ToJson()` の純粋ロジックを検証。 | 新規 |
| `Flax.sln` | テストプロジェクトを追加。 | 変更 |
| `README.md` | 新機能の使用例を追記。 | 変更 |
| `docs/superpowers/plans/2026-05-23-ui-tree-smoke-test.md` | 実機スモークテスト手順。 | 新規 |

---

## Task 1: Flax プロジェクトを SDK スタイルへ移行

**Files:**
- Modify: `Flax/Flax.csproj`(全面書き換え)
- Delete: `Flax/packages.config`

- [ ] **Step 1: `Flax/Flax.csproj` を SDK スタイルへ全面書き換え**

`Flax/Flax.csproj` の中身を以下で完全に置き換える:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <RootNamespace>Flax</RootNamespace>
    <AssemblyName>Flax</AssemblyName>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="Accessibility" />
    <Reference Include="PresentationCore" />
    <Reference Include="PresentationFramework" />
    <Reference Include="WindowsBase" />
    <Reference Include="System.Windows.Forms" />
    <Reference Include="System.IO.Compression" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="FlaUI.Core" Version="3.0.0" />
    <PackageReference Include="FlaUI.UIA3" Version="3.0.0" />
    <PackageReference Include="Interop.UIAutomationClient" Version="10.18362.0" />
    <PackageReference Include="System.Management" Version="4.7.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>

</Project>
```

補足:
- SDK スタイルは `.cs` を自動で含めるため、`<Compile>` の列挙は不要。
- `GenerateAssemblyInfo=false` で、既存 `Properties/AssemblyInfo.cs` の属性との重複生成を防ぐ。
- 旧 csproj にあった `Interop.UIAutomationClient.targets` の手動 import と `EnsureNuGetPackageBuildImports` ターゲットは、PackageReference が自動で build/targets を取り込むため削除する。
- `System`, `System.Core`, `System.Xml`, `System.Xml.Linq`, `System.Data`, `Microsoft.CSharp`, `System.Net.Http` は SDK の net472 既定参照に含まれるため列挙しない。

- [ ] **Step 2: `Flax/packages.config` を削除**

Run:
```powershell
Remove-Item "Flax/packages.config"
```

- [ ] **Step 3: 復元 + ビルドして成功を確認**

Run:
```powershell
dotnet build Flax/Flax.csproj -c Debug
```
Expected: `Build succeeded.`(エラー 0 件)。

トラブルシュート(コンパイルで「型/名前空間が見つからない」エラーが出た場合のみ、該当する `<Reference>` を Step 1 の最初の `<ItemGroup>` に追加して再ビルドする。候補と正確な名前):
- `System.Drawing`、`System.Xml.Linq`、`System.Data.DataSetExtensions`、`System.Net.Http`、`Microsoft.CSharp`、`System.Numerics`、`System.Runtime.Serialization`

逆に「重複参照(duplicate)」の警告/エラーが出た場合は、その参照が SDK 既定に含まれているので、Step 1 の `<ItemGroup>` から該当行を削除する。

- [ ] **Step 4: コミット**

```powershell
git add Flax/Flax.csproj
git rm Flax/packages.config
git commit -m "build: migrate Flax to SDK-style project with PackageReference" -m "Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: テストプロジェクトを作成しソリューションに追加

**Files:**
- Create: `Flax.Tests/Flax.Tests.csproj`
- Create: `Flax.Tests/SmokeTests.cs`
- Modify: `Flax.sln`

- [ ] **Step 1: テストプロジェクトの csproj を作成**

`Flax.Tests/Flax.Tests.csproj` を新規作成:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Flax\Flax.csproj" />
  </ItemGroup>

</Project>
```

注: `Flax` が PackageReference で参照する `Newtonsoft.Json` は推移的に本テストプロジェクトでも利用可能になる(テスト内で `Newtonsoft.Json.Linq` を使うため)。

- [ ] **Step 2: テストハーネスが動くことを確認する最小テストを作成**

`Flax.Tests/SmokeTests.cs` を新規作成:

```csharp
using NUnit.Framework;

namespace Flax.Tests
{
    public class SmokeTests
    {
        [Test]
        public void TestHarnessRuns()
        {
            Assert.Pass();
        }
    }
}
```

- [ ] **Step 3: ソリューションにテストプロジェクトを追加**

Run:
```powershell
dotnet sln Flax.sln add Flax.Tests/Flax.Tests.csproj
```
Expected: `Project ... added to the solution.`

- [ ] **Step 4: テストを実行して成功を確認**

Run:
```powershell
dotnet test Flax.Tests/Flax.Tests.csproj
```
Expected: `Passed!  - Failed: 0, Passed: 1`(`TestHarnessRuns` が PASS)。

- [ ] **Step 5: コミット**

```powershell
git add Flax.Tests/Flax.Tests.csproj Flax.Tests/SmokeTests.cs Flax.sln
git commit -m "test: add NUnit test project (Flax.Tests)" -m "Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: `UINode` DTO と `ToJson()`(TDD)

**Files:**
- Create: `Flax/Windows/UINode.cs`
- Test: `Flax.Tests/UINodeTests.cs`

- [ ] **Step 1: 失敗するテストを書く**

`Flax.Tests/UINodeTests.cs` を新規作成:

```csharp
using System.Collections.Generic;
using Flax.Windows;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Flax.Tests
{
    public class UINodeTests
    {
        [Test]
        public void ToJson_OmitsEmptyOptionalFields()
        {
            var node = new UINode
            {
                Id = 0,
                ControlType = "Window",
                Name = "Calculator",
                Rect = new[] { 0, 0, 322, 460 },
                Enabled = true,
                Visible = true
            };

            var obj = JObject.Parse(node.ToJson());

            Assert.That((int)obj["id"], Is.EqualTo(0));
            Assert.That((string)obj["controlType"], Is.EqualTo("Window"));
            Assert.That((string)obj["name"], Is.EqualTo("Calculator"));
            Assert.That(obj.ContainsKey("automationId"), Is.False);
            Assert.That(obj.ContainsKey("className"), Is.False);
            Assert.That(obj.ContainsKey("children"), Is.False);
        }

        [Test]
        public void ToJson_EmitsRectAsArray()
        {
            var node = new UINode
            {
                Id = 1,
                ControlType = "Button",
                Rect = new[] { 10, 400, 70, 50 },
                Enabled = true,
                Visible = true
            };

            var rect = (JArray)JObject.Parse(node.ToJson())["rect"];

            Assert.That(rect.Count, Is.EqualTo(4));
            Assert.That((int)rect[0], Is.EqualTo(10));
            Assert.That((int)rect[1], Is.EqualTo(400));
            Assert.That((int)rect[2], Is.EqualTo(70));
            Assert.That((int)rect[3], Is.EqualTo(50));
        }

        [Test]
        public void ToJson_NestsChildren()
        {
            var root = new UINode
            {
                Id = 0,
                ControlType = "Window",
                Name = "Calculator",
                Rect = new[] { 0, 0, 322, 460 },
                Enabled = true,
                Visible = true,
                Children = new List<UINode>
                {
                    new UINode
                    {
                        Id = 1,
                        ControlType = "Button",
                        Name = "1",
                        AutomationId = "num1Button",
                        Rect = new[] { 10, 400, 70, 50 },
                        Enabled = true,
                        Visible = true
                    }
                }
            };

            var children = (JArray)JObject.Parse(root.ToJson())["children"];

            Assert.That(children.Count, Is.EqualTo(1));
            Assert.That((int)children[0]["id"], Is.EqualTo(1));
            Assert.That((string)children[0]["automationId"], Is.EqualTo("num1Button"));
            Assert.That(children[0]["children"], Is.Null);
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗(コンパイルエラー)を確認**

Run:
```powershell
dotnet test Flax.Tests/Flax.Tests.csproj
```
Expected: ビルド失敗 — `UINode` が未定義(`The type or namespace name 'UINode' could not be found`)。

- [ ] **Step 3: `UINode` を実装**

`Flax/Windows/UINode.cs` を新規作成:

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Flax.Windows
{
    /// <summary>
    /// Serializable snapshot of a UI element used to emit a token-efficient
    /// JSON tree for LLM consumption. Empty optional fields and empty child
    /// lists are omitted from the output.
    /// </summary>
    public class UINode
    {
        public int Id { get; set; }
        public string ControlType { get; set; }
        public string Name { get; set; }
        public string AutomationId { get; set; }
        public string ClassName { get; set; }
        public int[] Rect { get; set; }
        public bool Enabled { get; set; }
        public bool Visible { get; set; }
        public List<UINode> Children { get; set; }

        public string ToJson()
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            return JsonConvert.SerializeObject(this, settings);
        }
    }
}
```

- [ ] **Step 4: テストを実行して成功を確認**

Run:
```powershell
dotnet test Flax.Tests/Flax.Tests.csproj
```
Expected: `Passed!  - Failed: 0, Passed: 4`(Smoke 1 + UINode 3)。

- [ ] **Step 5: コミット**

```powershell
git add Flax/Windows/UINode.cs Flax.Tests/UINodeTests.cs
git commit -m "feat: add UINode DTO with token-efficient JSON serialization" -m "Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: `UIElement` に `Id` / `ControlType` / `Children` を追加

FlaUI の `AutomationElement` を必要とするため単体テストは難しい。ビルド成功で検証し、実挙動は Task 6 のスモークテストで確認する。

**Files:**
- Modify: `Flax/Windows/UIElement.cs`

- [ ] **Step 1: `using` を追加**

`Flax/Windows/UIElement.cs` の先頭、`using System.Drawing;` の直後に追加:

```csharp
using System.Collections.Generic;
```

- [ ] **Step 2: コンストラクタで `ControlType` を設定**

`UIElement` コンストラクタ内、`Visible = !element.IsOffscreen;` の直後に追加:

```csharp
            ControlType = GetControlTypeSafe(element);
```

- [ ] **Step 3: プロパティと安全取得ヘルパーを追加**

既存の `public bool Visible { get; private set; }` の直後に追加:

```csharp
        public int Id { get; internal set; } = -1;
        public string ControlType { get; private set; }
        public IReadOnlyList<UIElement> Children { get; internal set; } = new List<UIElement>();

        private static string GetControlTypeSafe(AutomationElement element)
        {
            try
            {
                return element.ControlType.ToString();
            }
            catch
            {
                return "";
            }
        }
```

- [ ] **Step 4: ビルドして成功を確認**

Run:
```powershell
dotnet build Flax/Flax.csproj -c Debug
```
Expected: `Build succeeded.`

- [ ] **Step 5: コミット**

```powershell
git add Flax/Windows/UIElement.cs
git commit -m "feat: add Id, ControlType and Children to UIElement" -m "Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: `FlaxWindow` に `GetElementTreeAsJson` / `GetElementById` を追加

FlaUI 走査を伴うため単体テストは行わず、ビルド成功 + Task 6 のスモークテストで検証する。

**Files:**
- Modify: `Flax/Windows/FlaxWindow.cs`

- [ ] **Step 1: ID マップ用のフィールドを追加**

`FlaxWindow` クラス内、`private FlaUI.Core.AutomationElements.Window _FlaUIWindow { get; set; }` の直後に追加:

```csharp
        private Dictionary<int, UIElement> _elementMap;
```

- [ ] **Step 2: 公開メソッドと走査ロジックを追加**

`public override string ToString()` メソッドの直前に、以下のメソッド群を追加:

```csharp
        public string GetElementTreeAsJson(int maxDepth = -1, bool includeOffscreen = false)
        {
            if (this.IsMinimized)
            {
                this.Restore();
                this.SetFlaUIWindow();
            }
            this.Activate();

            _elementMap = new Dictionary<int, UIElement>();
            int nextId = 0;
            UIElement root = BuildTree(_FlaUIWindow, 0, maxDepth, includeOffscreen, ref nextId);
            UINode rootNode = (root != null) ? ToNode(root) : null;
            return rootNode != null ? rootNode.ToJson() : "null";
        }

        public UIElement GetElementById(int id)
        {
            if (_elementMap != null && _elementMap.TryGetValue(id, out UIElement element))
            {
                return element;
            }
            return null;
        }

        private UIElement BuildTree(AutomationElement ae, int depth, int maxDepth, bool includeOffscreen, ref int nextId)
        {
            if (!includeOffscreen && ae.IsOffscreen)
            {
                return null;
            }

            var element = new UIElement(ae);
            element.Id = nextId++;
            _elementMap[element.Id] = element;

            var children = new List<UIElement>();
            if (maxDepth < 0 || depth < maxDepth)
            {
                AutomationElement[] childElements;
                try
                {
                    childElements = ae.FindAllChildren();
                }
                catch
                {
                    childElements = new AutomationElement[0];
                }

                foreach (var childAe in childElements)
                {
                    var child = BuildTree(childAe, depth + 1, maxDepth, includeOffscreen, ref nextId);
                    if (child != null)
                    {
                        children.Add(child);
                    }
                }
            }
            element.Children = children;
            return element;
        }

        private static UINode ToNode(UIElement e)
        {
            var node = new UINode
            {
                Id = e.Id,
                ControlType = e.ControlType,
                Name = string.IsNullOrEmpty(e.Name) ? null : e.Name,
                AutomationId = string.IsNullOrEmpty(e.AutomationID) ? null : e.AutomationID,
                ClassName = string.IsNullOrEmpty(e.ClassName) ? null : e.ClassName,
                Rect = new[]
                {
                    e.BoundingRectangle.X,
                    e.BoundingRectangle.Y,
                    e.BoundingRectangle.Width,
                    e.BoundingRectangle.Height
                },
                Enabled = e.Enabled,
                Visible = e.Visible
            };

            if (e.Children != null && e.Children.Count > 0)
            {
                node.Children = new List<UINode>();
                foreach (var child in e.Children)
                {
                    node.Children.Add(ToNode(child));
                }
            }
            return node;
        }
```

- [ ] **Step 3: ビルドして成功を確認**

Run:
```powershell
dotnet build Flax/Flax.csproj -c Debug
```
Expected: `Build succeeded.`

- [ ] **Step 4: 既存テストが壊れていないことを確認**

Run:
```powershell
dotnet test Flax.Tests/Flax.Tests.csproj
```
Expected: `Passed!  - Failed: 0, Passed: 4`

- [ ] **Step 5: コミット**

```powershell
git add Flax/Windows/FlaxWindow.cs
git commit -m "feat: add GetElementTreeAsJson and GetElementById to FlaxWindow" -m "Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: 実機スモークテスト(手動)

自動テストでは UIA 走査の実挙動を確認できないため、手順を文書化し手動で 1 回実行する。

**Files:**
- Create: `docs/superpowers/plans/2026-05-23-ui-tree-smoke-test.md`

- [ ] **Step 1: スモークテスト手順を文書化**

`docs/superpowers/plans/2026-05-23-ui-tree-smoke-test.md` を新規作成:

````markdown
# UI ツリー取得 スモークテスト手順

電卓(calc.exe)を対象に、`GetElementTreeAsJson` と `GetElementById` の実挙動を確認する。

## 準備

任意のコンソールアプリ等から Flax を参照し、以下を実行する。

```csharp
using System;
using Flax;

class Program
{
    static void Main()
    {
        var f = new WindowsAutomation();
        f.Process.Run("calc.exe");
        System.Threading.Thread.Sleep(1500);

        using (var w = f.GetWindow("電卓")) // 英語環境は "Calculator"
        {
            string json = w.GetElementTreeAsJson();
            Console.WriteLine(json);

            // JSON を見て Button("1") の id を確認し、その id を指定する(例: 42)
            // var e = w.GetElementById(42);
            // e?.Click();
        }
    }
}
```

## 合格条件

- [ ] 出力 JSON のルートが `"controlType":"Window"` を含む。
- [ ] JSON 中に `"controlType":"Button"` のノードが複数含まれる。
- [ ] 数字ボタンに対応するノードに `name`(例 `"1"`)が入っている。
- [ ] `GetElementById(<上で確認した数字ボタンの id>)` で取得した要素の `.Click()` で、電卓に数字が入力される。
- [ ] `GetElementTreeAsJson(maxDepth: 1)` を呼ぶと、ルート直下までで打ち切られた JSON が返る(ネストが浅くなる)。
````

- [ ] **Step 2: 手動でスモークテストを実行し、合格条件を確認**

上記コードを実行し、文書のチェックボックスをすべて満たすことを目視確認する。問題があれば該当 Task に戻って修正する。

- [ ] **Step 3: コミット**

```powershell
git add docs/superpowers/plans/2026-05-23-ui-tree-smoke-test.md
git commit -m "docs: add manual smoke test procedure for UI tree retrieval" -m "Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: README に使用例を追記

**Files:**
- Modify: `README.md`

- [ ] **Step 1: 新機能セクションを追記**

`README.md` の末尾(`### What is the OpenCV feature?` のコードブロックの後)に追加:

````markdown

### Getting the UI element tree (for LLMs)
Get the whole UI tree of a window as JSON, hand it to an LLM, then act on the element the LLM picked by its `id`.

```csharp
using Flax;

    var f = new WindowsAutomation();
    f.Process.Run("calc.exe");

    using (var w = f.GetWindow("Calculator"))
    {
        // Token-efficient JSON tree. Offscreen elements are skipped by default.
        string json = w.GetElementTreeAsJson();
        // ... let an LLM choose an element id from the json ...

        // Act on the chosen element by its id.
        w.GetElementById(7)?.Click();
    }
```

`GetElementTreeAsJson(int maxDepth = -1, bool includeOffscreen = false)` walks the window's descendants, assigns a sequential `id` to each node, and returns a JSON tree. `GetElementById(id)` returns the element from the most recent tree call so you can `Click()` it.
````

- [ ] **Step 2: コミット**

```powershell
git add README.md
git commit -m "docs: document GetElementTreeAsJson and GetElementById in README" -m "Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## 完了条件

- [ ] `dotnet build Flax/Flax.csproj` が成功する。
- [ ] `dotnet test Flax.Tests/Flax.Tests.csproj` が全件 PASS(4 件)。
- [ ] スモークテスト手順(Task 6)の合格条件をすべて満たす。
- [ ] README に新機能の使用例が記載されている。
