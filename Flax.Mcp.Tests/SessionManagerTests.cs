using System;
using Flax.Mcp;
using Flax.Windows;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class SessionManagerTests
{
    private static FlaxWindow NewWindow() => new FlaxWindow(IntPtr.Zero, 0);

    [Test]
    public void Open_ReturnsNonEmptyId_AndTryGetFindsWindow()
    {
        var sm = new SessionManager();
        var window = NewWindow();

        var id = sm.Open(window);

        Assert.That(id, Is.Not.Null.And.Not.Empty);
        Assert.That(sm.TryGet(id, out var found), Is.True);
        Assert.That(found, Is.SameAs(window));
    }

    [Test]
    public void Open_IssuesDistinctIds()
    {
        var sm = new SessionManager();
        var id1 = sm.Open(NewWindow());
        var id2 = sm.Open(NewWindow());
        Assert.That(id1, Is.Not.EqualTo(id2));
    }

    [Test]
    public void TryGet_UnknownId_ReturnsFalse()
    {
        var sm = new SessionManager();
        Assert.That(sm.TryGet("nope", out _), Is.False);
    }

    [Test]
    public void Remove_ExistingId_ReturnsTrue_AndSessionGone()
    {
        var sm = new SessionManager();
        var id = sm.Open(NewWindow());

        Assert.That(sm.Remove(id), Is.True);
        Assert.That(sm.TryGet(id, out _), Is.False);
    }

    [Test]
    public void Remove_UnknownId_ReturnsFalse()
    {
        var sm = new SessionManager();
        Assert.That(sm.Remove("nope"), Is.False);
    }
}
