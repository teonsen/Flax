using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Flax.Windows
{
    public class FlaxKeyboard
    {
        public void Alt()
        {
            Keyboard.Type(VirtualKeyShort.ALT);
        }

        public void BackSpace(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                Keyboard.Type(VirtualKeyShort.BACK);
                System.Threading.Thread.Sleep(20);
            }
        }

        public void CtrlA()
        {
            Keyboard.Type(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        }
        public void CtrlC()
        {
            Keyboard.Type(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);
        }
        public void CtrlV()
        {
            Keyboard.Type(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
        }

        public void Delete(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                Keyboard.Type(VirtualKeyShort.DELETE);
                System.Threading.Thread.Sleep(20);
            }
        }

        public void Down(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                Keyboard.Type(VirtualKeyShort.DOWN);
                System.Threading.Thread.Sleep(20);
            }
        }

        public void Enter()
        {
            Keyboard.Type(VirtualKeyShort.ENTER);
        }

        public void Esc()
        {
            Keyboard.Type(VirtualKeyShort.ESC);
        }

        public void Left()
        {
            Keyboard.Type(VirtualKeyShort.LEFT);
        }

        public void Right()
        {
            Keyboard.Type(VirtualKeyShort.RIGHT);
        }

        public void Type(string text)
        {
            Keyboard.Type(text);
        }

        public void Type(string text, bool Ctrl, bool Alt, bool Shift)
        {
            if (text.Length == 0) return;
            ushort code = (ushort)User32.VkKeyScan(text[0]);

            if (!Ctrl && !Alt && !Shift)
            {
                Keyboard.Type(text);
            }
            else if (!Ctrl && !Alt && Shift)
            {
                Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, (VirtualKeyShort)code);
            }
            else if (!Ctrl && Alt && !Shift)
            {
                Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, (VirtualKeyShort)code);
            }
            else if (!Ctrl && Alt && Shift)
            {
                Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.SHIFT, (VirtualKeyShort)code);
            }
            else if (Ctrl && !Alt && !Shift)
            {
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, (VirtualKeyShort)code);
            }
            else if (Ctrl && !Alt && Shift)
            {
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, (VirtualKeyShort)code);
            }
            else if (Ctrl && Alt && !Shift)
            {
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, (VirtualKeyShort)code);
            }
            else if (Ctrl && Alt && Shift)
            {
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.SHIFT, (VirtualKeyShort)code);
            }
        }

        public void Space()
        {
            Keyboard.Type(VirtualKeyShort.SPACE);
        }

        public void Tab()
        {
            Keyboard.Type(VirtualKeyShort.TAB);
        }

        public void Up(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                Keyboard.Type(VirtualKeyShort.UP);
                System.Threading.Thread.Sleep(20);
            }
        }
    }
}
