using Flax.Mcp.Llm;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class LocateResultParserTests
{
    [Test]
    public void ParseTree_Found_With_Id()
    {
        var r = LocateResultParser.ParseTree("{\"found\":true,\"id\":12,\"confidence\":0.9,\"reasoning\":\"the 1 button\"}");
        Assert.That(r.Found, Is.True);
        Assert.That(r.Id, Is.EqualTo(12));
        Assert.That(r.Confidence, Is.EqualTo(0.9).Within(1e-9));
        Assert.That(r.Reasoning, Is.EqualTo("the 1 button"));
    }

    [Test]
    public void ParseTree_Found_False()
    {
        var r = LocateResultParser.ParseTree("{\"found\":false}");
        Assert.That(r.Found, Is.False);
        Assert.That(r.Id, Is.Null);
    }

    [Test]
    public void ParseTree_Extracts_Object_From_Surrounding_Prose()
    {
        var r = LocateResultParser.ParseTree("Sure! Here you go:\n{\"found\":true,\"id\":7}\nHope that helps.");
        Assert.That(r.Found, Is.True);
        Assert.That(r.Id, Is.EqualTo(7));
    }

    [Test]
    public void ParseTree_Found_True_But_No_Id_Is_NotFound()
    {
        var r = LocateResultParser.ParseTree("{\"found\":true}");
        Assert.That(r.Found, Is.False);
    }

    [Test]
    public void ParseTree_Invalid_Json_Returns_NotFound_With_RawReasoning()
    {
        var r = LocateResultParser.ParseTree("I could not find it.");
        Assert.That(r.Found, Is.False);
        Assert.That(r.Reasoning, Does.Contain("could not find"));
    }

    [Test]
    public void ParseVision_Found_With_Pixels()
    {
        var r = LocateResultParser.ParseVision("{\"found\":true,\"px\":120,\"py\":340,\"confidence\":0.8}");
        Assert.That(r.Found, Is.True);
        Assert.That(r.Px, Is.EqualTo(120));
        Assert.That(r.Py, Is.EqualTo(340));
    }

    [Test]
    public void ParseVision_Missing_Py_Is_NotFound()
    {
        var r = LocateResultParser.ParseVision("{\"found\":true,\"px\":120}");
        Assert.That(r.Found, Is.False);
    }
}
