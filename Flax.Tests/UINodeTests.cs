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
            Assert.That((bool)obj["enabled"], Is.True);
            Assert.That((bool)obj["visible"], Is.True);
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

        [Test]
        public void ToJson_NestsGrandchildren()
        {
            var root = new UINode
            {
                Id = 0,
                ControlType = "Window",
                Rect = new[] { 0, 0, 100, 100 },
                Enabled = true,
                Visible = true,
                Children = new List<UINode>
                {
                    new UINode
                    {
                        Id = 1,
                        ControlType = "Pane",
                        Rect = new[] { 0, 0, 50, 50 },
                        Enabled = true,
                        Visible = true,
                        Children = new List<UINode>
                        {
                            new UINode
                            {
                                Id = 2,
                                ControlType = "Button",
                                Name = "OK",
                                Rect = new[] { 0, 0, 20, 20 },
                                Enabled = true,
                                Visible = true
                            }
                        }
                    }
                }
            };

            var obj = JObject.Parse(root.ToJson());
            var grandchild = obj["children"][0]["children"][0];

            Assert.That((int)grandchild["id"], Is.EqualTo(2));
            Assert.That((string)grandchild["controlType"], Is.EqualTo("Button"));
            Assert.That((string)grandchild["name"], Is.EqualTo("OK"));
        }
    }
}
