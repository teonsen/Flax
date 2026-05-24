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
