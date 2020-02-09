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

        public void MoveTo(Point newPosition)
        {
            Mouse.MoveTo(newPosition);
        }

        public void MoveTo(int newX, int newY)
        {
            Mouse.MoveTo(newX, newY);
        }

        public void RightClick()
        {
            Mouse.RightClick();
        }

        public void RightClick(int x, int y)
        {
            Mouse.RightClick(new System.Drawing.Point(x, y));
        }

        public static void VerticalScroll(double lines)
        {
            Mouse.Scroll(lines);
        }

        public static void HorizontalScroll(double lines)
        {
            Mouse.HorizontalScroll(lines);
        }

        public void MouseAction(ClickType clickType, MouseButton mouseButton)
        {
            switch (clickType)
            {
                case ClickType.Single:
                    Mouse.Click((FlaUI.Core.Input.MouseButton)mouseButton);
                    break;
                case ClickType.Double:
                    Mouse.DoubleClick((FlaUI.Core.Input.MouseButton)mouseButton);
                    break;
                case ClickType.Down:
                    Mouse.Down((FlaUI.Core.Input.MouseButton)mouseButton);
                    break;
                case ClickType.Up:
                    Mouse.Up((FlaUI.Core.Input.MouseButton)mouseButton);
                    break;
                default:
                    break;
            }
        }

    }
}
