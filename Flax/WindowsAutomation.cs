using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flax.Windows;

namespace Flax
{
    /// <summary>
    /// It is the wrapper of FlaUI using UIA3.
    /// </summary>
    public class WindowsAutomation : IDisposable
    {
        public FlaxCV CV = new FlaxCV();
        public FlaxKeyboard Keyboard = new FlaxKeyboard();
        public FlaxMouse Mouse = new FlaxMouse();
        public FlaxProcess Process = new FlaxProcess();

        public FlaxWindow GetWindow(string windowTitle, int timeout_sec = 10)
        {
            return GetWindowCommon(windowTitle, timeout_sec);
        }

        private FlaxWindow GetWindowCommon(string windowTitle, int timeout_sec)
        {
            int retryCount = 0;
            string searchTitle = windowTitle;

            while (true) {
                List<FlaxWindow> foundWindows = null;
                var windowList = GetWindowList();
                if (windowTitle.StartsWith("%") && windowTitle.EndsWith("%")) {
                    searchTitle = windowTitle.Substring(1, windowTitle.Length - 2);
                    foundWindows = windowList.FindAll(a => a.Title.Contains(searchTitle));
                } else if (windowTitle.StartsWith("%")) {
                    searchTitle = windowTitle.Substring(1, windowTitle.Length - 1);
                    foundWindows = windowList.FindAll(a => a.Title.EndsWith(searchTitle));
                } else if (windowTitle.EndsWith("%")) {
                    searchTitle = windowTitle.Substring(0, windowTitle.Length - 1);
                    foundWindows = windowList.FindAll(a => a.Title.StartsWith(searchTitle));
                } else {
                    foundWindows = windowList.FindAll(a => a.Title.Equals(searchTitle));
                }
                if (foundWindows.Count > 0) {
                    foundWindows.Sort((a, b) => a.Width - b.Width);
                    foundWindows[0].SetFlaUIWindow();
                    return foundWindows[0];
                }
                if (timeout_sec != 0 && (timeout_sec < 0 || retryCount >= timeout_sec)) {
                    return null;
                }
                System.Threading.Thread.Sleep(1000);
                retryCount++;
            }
        }

        public static List<FlaxWindow> GetWindowList()
        {
            int idx = 0;
            var wndList = new List<FlaxWindow>();
            Win32API.User32.EnumWindows(new Win32API.User32.EnumWindowsDelegate(delegate (IntPtr hWnd, int lParam)
            {
                var w = new FlaxWindow(hWnd, idx);
                if (w.IsValid)
                {
                    wndList.Add(w);
                }
                idx++;
                return 1;
            }), 0);
            return wndList;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }

        public void Wait(int milliSeconds)
        {
            System.Threading.Thread.Sleep(milliSeconds);
        }
    }
}
