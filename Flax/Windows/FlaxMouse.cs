using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Flax.Windows
{
    public class FlaxMouse
    {
        public Point Position {
            get {
                return Mouse.Position;
            }
            set {
                Mouse.Position = value;
            }
        }

        public void Click()
        {
            Mouse.Click();
        }

        public void Click(int x, int y)
        {
            Mouse.Click(new Point(x, y));
        }

        public void DoubleClick()
        {
            Mouse.DoubleClick();
        }

        public void DoubleClick(int x, int y)
        {
            Mouse.DoubleClick(new Point(x, y));
        }

        public void Drag(Point startingPoint, Point endingPoint)
        {
            Mouse.Drag(startingPoint, endingPoint);
        }

        public void RightClick()
        {
            Mouse.RightClick();
        }

        public void RightClick(int x, int y)
        {
            Mouse.RightClick(new System.Drawing.Point(x, y));
        }

        public void Scroll(double lines)
        {
            Mouse.Scroll(lines);
        }
    }
}
