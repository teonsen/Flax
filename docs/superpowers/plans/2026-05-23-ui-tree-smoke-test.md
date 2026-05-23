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
