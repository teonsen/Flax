using Flax.Mcp;
using Flax.Windows;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class SendKeysMapTests
{
    [Test]
    public void TryGet_KnownKey_ReturnsTrue()
        => Assert.That(SendKeysMap.TryGet("ENTER", out _), Is.True);

    [Test]
    public void TryGet_IsCaseInsensitive_AndTrims()
        => Assert.That(SendKeysMap.TryGet("  enter ", out _), Is.True);

    [Test]
    public void TryGet_Combo_ReturnsTrue()
        => Assert.That(SendKeysMap.TryGet("CTRL+A", out _), Is.True);

    [Test]
    public void TryGet_UnknownKey_ReturnsFalse()
        => Assert.That(SendKeysMap.TryGet("F13", out _), Is.False);
}
