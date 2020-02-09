using System;
using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;

namespace Flax.Windows
{
    public class UIElement
    {
        AutomationElement _ae;

        public UIElement(AutomationElement element)
        {
            _ae = element;
            Name = FromAutomationProperty(element.FrameworkAutomationElement.Name);
            AutomationID = FromAutomationProperty(element.FrameworkAutomationElement.AutomationId);
            ClassName = FromAutomationProperty(element.FrameworkAutomationElement.ClassName);
            HelpText = FromAutomationProperty(element.FrameworkAutomationElement.HelpText);
            BoundingRectangle = element.BoundingRectangle;
            CenterX = element.BoundingRectangle.X + element.BoundingRectangle.Width / 2;
            CenterY = element.BoundingRectangle.Y + element.BoundingRectangle.Height / 2;
            Enabled = element.IsEnabled;
            Visible = !element.IsOffscreen;
        }

        private string FromAutomationProperty(FlaUI.Core.IAutomationProperty<string> value)
        {
            return value.ToDisplayText();
        }

        public string Name { get; private set; }
        public string AutomationID { get; private set; }
        public string ClassName { get; private set; }
        public string HelpText { get; private set; }
        public Rectangle BoundingRectangle { get; private set; }
        public int CenterX { get; private set; }
        public int CenterY { get; private set; }
        public bool Enabled { get; private set; }
        public bool Visible { get; private set; }

        public void Click()
        {
            _ae.Click();
        }

        public void Capture(string savePath)
        {
            Windows.Capture.Region(BoundingRectangle, savePath);
        }

        public void DoubleClick()
        {
            _ae.DoubleClick();
        }

        public void Focus()
        {
            _ae.Focus();
        }

        public void Hover()
        {
            Mouse.MoveTo(CenterX, CenterY);
        }

        public void RightClick()
        {
            _ae.RightClick();
        }

        public void RightDoubleClick()
        {
            _ae.RightDoubleClick();
        }

    }


}
