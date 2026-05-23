using System.Collections.Generic;
using Flax.Mcp;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class ClickServiceTests
{
    private sealed class FakeClickable : IClickable
    {
        public (int X, int Y) Center { get; init; } = (100, 200);
        public bool UiaSucceeds { get; init; } = true;
        public bool UiaCalled { get; private set; }

        public bool TryUiaClick(ClickKind kind, out string? error)
        {
            UiaCalled = true;
            error = UiaSucceeds ? null : "boom";
            return UiaSucceeds;
        }
    }

    private sealed class FakeLookup : IElementLookup
    {
        private readonly IClickable? _result;
        public FakeLookup(IClickable? result) => _result = result;
        public IClickable? FindById(int id) => _result;
    }

    [Test]
    public void Click_ById_UiaSucceeds_ReturnsUia_NoCoordClick()
    {
        var coordCalls = new List<(int, int, ClickKind)>();
        var outcome = new ClickService().Click(
            new FakeLookup(new FakeClickable { UiaSucceeds = true }),
            elementId: 3, x: null, y: null, ClickKind.Left,
            (x, y, k) => coordCalls.Add((x, y, k)));

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Method, Is.EqualTo("uia"));
        Assert.That(coordCalls, Is.Empty);
    }

    [Test]
    public void Click_ById_UiaFails_FallsBackToElementCenter()
    {
        var coordCalls = new List<(int, int, ClickKind)>();
        var outcome = new ClickService().Click(
            new FakeLookup(new FakeClickable { UiaSucceeds = false, Center = (40, 60) }),
            elementId: 3, x: null, y: null, ClickKind.Left,
            (x, y, k) => coordCalls.Add((x, y, k)));

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Method, Is.EqualTo("coord-fallback"));
        Assert.That(coordCalls, Has.Count.EqualTo(1));
        Assert.That(coordCalls[0], Is.EqualTo((40, 60, ClickKind.Left)));
    }

    [Test]
    public void Click_ById_NotFound_ReturnsElementNotFound()
    {
        var outcome = new ClickService().Click(
            new FakeLookup(null),
            elementId: 9, x: null, y: null, ClickKind.Left,
            (_, _, _) => { });

        Assert.That(outcome.Success, Is.False);
        Assert.That(outcome.Error, Is.EqualTo("element_not_found"));
    }

    [Test]
    public void Click_ByCoordinates_ClicksDirectly()
    {
        var coordCalls = new List<(int, int, ClickKind)>();
        var outcome = new ClickService().Click(
            new FakeLookup(null),
            elementId: null, x: 11, y: 22, ClickKind.Right,
            (x, y, k) => coordCalls.Add((x, y, k)));

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Method, Is.EqualTo("coord"));
        Assert.That(coordCalls[0], Is.EqualTo((11, 22, ClickKind.Right)));
    }

    [Test]
    public void Click_NoTarget_ReturnsBadRequest()
    {
        var outcome = new ClickService().Click(
            new FakeLookup(null),
            elementId: null, x: null, y: null, ClickKind.Left,
            (_, _, _) => { });

        Assert.That(outcome.Success, Is.False);
        Assert.That(outcome.Error, Is.EqualTo("bad_request"));
    }
}
