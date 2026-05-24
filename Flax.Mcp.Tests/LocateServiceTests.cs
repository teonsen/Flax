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
