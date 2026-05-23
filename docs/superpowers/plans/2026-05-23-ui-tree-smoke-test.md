# UI ツリー取得 スモークテスト手順

`GetElementTreeAsJson` と `GetElementById` の実挙動を確認する。

> **重要(Windows 11 の制約):** Windows 11 のモダンアプリ(WinUI3 / UWP — 例: 電卓、新しいメモ帳、ペイントのキャンバス部分)は、対話コントロールを `Microsoft.UI.Content.DesktopChildSiteBridge` などの XAML アイランド内にホストしている。アウトプロセスの UIA3 は既定でこれらのアイランド内を辿れないため、これらのアプリではツリーが浅くなる(`Pane` どまり)。これは Flax のバグではなく UIA 自体の制約。**深いツリーの確認にはクラシックな Win32 / WinForms / WPF アプリを使うこと。** 以下では `mspaint.exe`(ネイティブのタイトルバー/メニュー/ボタンを持つ)を主対象にする。

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
        f.Process.Run("mspaint.exe");
        System.Threading.Thread.Sleep(1500);

        // "%...%" は部分一致(Contains)。英語環境は "%Paint%"。
        using (var w = f.GetWindow("%ペイント%"))
        {
            string json = w.GetElementTreeAsJson();
            Console.WriteLine(json);

            // JSON を見て操作したいノードの id を確認し、その id を指定する。
            // 例: "閉じる" ボタン等の id を見て
            // var e = w.GetElementById(7);
            // e?.Click();   // ※ Click は副作用があるので確認用途では注意
        }
    }
}
```

## 合格条件(mspaint.exe)

- [ ] 出力 JSON のルートが `"controlType":"Window"` を含む。
- [ ] ツリーが複数階層になっている(例: `Window > TitleBar > MenuBar > MenuItem`、`Window > TitleBar > Button`)。
- [ ] `controlType` に操作可能な種別(`Button` / `MenuItem` / `MenuBar` / `TitleBar` 等)を含むノードがある。
- [ ] 各ノードの `id` が 0 から連番・一意になっている。
- [ ] `GetElementById(<上で確認した id>)` が非 null の `UIElement` を返す(必要なら `.Click()` で操作できる)。
- [ ] `GetElementTreeAsJson(maxDepth: 1)` の JSON が、無制限(既定)よりノード数が少なく浅い。

## 参考: 2026-05-23 の実機検証結果

`mspaint.exe` に対する実測(抜粋):

```
id=0  Window    "タイトルなし - ペイント"
id=1  Pane      (DesktopChildSiteBridge — WinUI3 キャンバス。アイランド内は辿れない)
id=2  TitleBar  automationId=TitleBar
id=3  MenuBar   name=システム  automationId=SystemMenuBar
id=4  MenuItem  name=システム
id=5  Button    name=最小化   automationId=Minimize-Restore
id=6  Button    name=最大化   automationId=Maximize-Restore
id=7  Button    name=閉じる   automationId=Close
```

- 全 8 ノード・複数階層・連番一意 ID を確認。`maxDepth:1` は 3 ノード(無制限は 8)で正しく浅くなった。`GetElementById(5..7)` はいずれも非 null のボタンを返した。
- 電卓・新メモ帳は WinUI3 ホストのためルート + `Pane` 数個の浅いツリーになる(上記の制約どおり)。
- `regedit.exe` は UAC 昇格のため attach 不可(OS の権限境界)。
