using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace Flax.Windows
{
    public class FlaxWindow : IDisposable
    {
        public int Id { get; internal set; }
        public IntPtr hWnd { get; internal set; }
        public string Title { get; set; } = "";
        public string ClassName { get; internal set; }
        public string ProcessName { get; internal set; }
        public string ProcessPath { get; internal set; }
        public int PID { get; internal set; }
        public int Left { get; internal set; }
        public int Top { get; internal set; }
        public int Right { get; internal set; }
        public int Bottom { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public bool IsMinimized { get; internal set; }
        public Rectangle Rect { get; internal set; }
        //public UIElement UIElement { get; private set; }
        internal bool IsValid { get; set; } = false;
        private FlaUI.Core.AutomationElements.Window _FlaUIWindow { get; set; }

        public FlaxWindow(IntPtr hwnd, int idx)
        {
            hWnd = hwnd;
            Id = idx;
            int pid = 0;
            string procName = "", procPath = "";
            Title = Win32API.User32.GetWindowTextHelper(hwnd);
            // Get the process name and path from its window handle.
            GetProcessInfo(hwnd, ref procName, ref procPath, ref pid);
            ProcessName = procName;
            ProcessPath = procPath;
            PID = pid;
            ClassName = Win32API.User32.GetClassNameA(hWnd);

            if (ClassName.Equals("Windows.UI.Core.CoreWindow"))
            {
                IsValid = false;
                return;
            }
            var r = Win32API.User32.GetWindowRectHelper(hWnd);
            IsMinimized = Win32API.User32.IsIconic(hWnd);
            if (IsMinimized)
            {
                IsValid = true;
            }
            else
            {
                IsValid = r.Top >= -8 && r.Left >= -8 &&
                                   r.Bottom - r.Top >= 40 &&
                                   r.Width >= 40 &&
                                   r.Left <= 10000 &&
                                   r.Top <= 10000;
            }
            Left = r.Left;
            Top = r.Top;
            Right = r.Right;
            Bottom = r.Bottom;
            Width = r.Width;
            Height = r.Height;
            Rect = r;
        }

        internal void SetFlaUIWindow()
        {
            using (var automation = new FlaUI.UIA3.UIA3Automation())
            {
                var app = FlaUI.Core.Application.Attach(PID);
                _FlaUIWindow = app.GetMainWindow(automation);
                //UIElement = new UIElement(this, _FlaUIWindow);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }

        private void GetProcessInfo(IntPtr hWnd, ref string exeName, ref string exePath, ref int pid)
        {
            uint lpdwProcessId;
            Win32API.User32.GetWindowThreadProcessId(hWnd, out lpdwProcessId);

            IntPtr hProcess = Win32API.Kernel32.OpenProcess(0x0410, false, lpdwProcessId);

            var name = new StringBuilder(0x1024);
            var path = new StringBuilder(0x1024);
            int baseName = Win32API.PSAPI.GetModuleBaseName(hProcess, IntPtr.Zero, name, name.Capacity);
            int fileName = Win32API.PSAPI.GetModuleFileNameEx(hProcess, IntPtr.Zero, path, path.Capacity);

            Win32API.Kernel32.CloseHandle(hProcess);
            exeName = baseName > 0 ? name.ToString() : "";
            exePath = fileName > 0 ? path.ToString() : "";
            pid = (int)lpdwProcessId;
        }

        public void Activate()
        {
            User32.SetForegroundWindow(hWnd);
        }

        public void Capture(string savePath)
        {
            Windows.Capture.Window(hWnd, savePath);
        }

        public bool Close()
        {
            return Win32API.User32.CloseWindow2(hWnd);
        }

        public void Maximize()
        {
            Win32API.User32.ShowWindow(hWnd, Win32API.User32.SW_MAXIMIZE);
        }
        public void Minimize()
        {
            Win32API.User32.ShowWindow(hWnd, Win32API.User32.SW_MINIMIZE);
        }
        public void Restore()
        {
            Win32API.User32.ShowWindow(hWnd, Win32API.User32.SW_RESTORE);
        }

        public void MoveTo(int x, int y)
        {
            _FlaUIWindow.Move(x, y);
        }

        public void Resize(int x, int y, int width, int height)
        {
            const short SWP_NOZORDER = 0X4;
            const int SWP_SHOWWINDOW = 0x0040;
            User32.SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_SHOWWINDOW);
        }

        public UIElement GetElementByName(string name)
        {
            return GetElementCommon(name, FindBy.Text);
        }

        public UIElement GetElementByAutomationID(string automationID)
        {
            return GetElementCommon(automationID, FindBy.AutomationID);
        }

        private UIElement GetElementCommon(string id, Enum findBy)
        {
            if (this.IsMinimized)
            {
                this.Restore();
                this.SetFlaUIWindow();
            }
            this.Activate();
            AutomationElement ae = null;
            switch (findBy)
            {
                case FindBy.AutomationID:
                    ae = Retry.WhileNull(() => _FlaUIWindow.FindFirstDescendant(cf => cf.ByAutomationId(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
                case FindBy.ClassName:
                    ae = Retry.WhileNull(() => _FlaUIWindow.FindFirstDescendant(cf => cf.ByClassName(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
                case FindBy.HelpText:
                    ae = Retry.WhileNull(() => _FlaUIWindow.FindFirstDescendant(cf => cf.ByHelpText(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
                case FindBy.Name:
                    ae = Retry.WhileNull(() => _FlaUIWindow.FindFirstDescendant(cf => cf.ByName(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
                case FindBy.Text:
                    ae = Retry.WhileNull(() => _FlaUIWindow.FindFirstDescendant(cf => cf.ByText(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
                case FindBy.Value:
                    ae = Retry.WhileNull(() => _FlaUIWindow.FindFirstDescendant(cf => cf.ByValue(id)), throwOnTimeout: false, ignoreException: true).Result;
                    break;
            }
            if (ae != null) return new UIElement(ae);
            return null;
        }

        public override string ToString()
        {
            return Title;
        }
    }
}
