using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Flax.Windows
{
    public static class Win32API
    {
        public static class User32
        {
            static uint WM_CLOSE = 0x10;
            public const int SW_HIDE = 0;              //ウィンドウを非表示にし、他のウィンドウをアクティブにします。
            public const int SW_SHOWNORMAL = 1;        //ウィンドウをアクティブにして表示します。ウィンドウが最小化または最大化されていた場合は、その位置とサイズを元に戻します。
            public const int SW_SHOWMINIMIZED = 2;     //ウィンドウをアクティブにして、最小化します。
            public const int SW_SHOWMAXIMIZED = 3;     //ウィンドウをアクティブにして、最大化します。
            public const int SW_MAXIMIZE = 3;          //ウィンドウを最大化します。
            public const int SW_SHOWNOACTIVATE = 4;    //ウィンドウを直前の位置とサイズで表示します。
            public const int SW_SHOW = 5;              //ウィンドウをアクティブにして、現在の位置とサイズで表示します。
            public const int SW_MINIMIZE = 6;          //ウィンドウを最小化し、Z オーダーが次のトップレベルウィンドウをアクティブにします。
            public const int SW_SHOWMINNOACTIVE = 7;   //ウィンドウを最小化します。(アクティブにはしない)
            public const int SW_SHOWNA = 8;            //ウィンドウを現在のサイズと位置で表示します。(アクティブにはしない)
            public const int SW_RESTORE = 9;           //ウィンドウをアクティブにして表示します。最小化または最大化されていたウィンドウは、元の位置とサイズに戻ります。
            public const int SW_SHOWDEFAULT = 10;      //アプリケーションを起動したプログラムが 関数に渡した 構造体で指定された SW_ フラグに従って表示状態を設定します。
            public const int SW_FORCEMINIMIZE = 11;    //たとえウィンドウを所有するスレッドがハングしていても、ウィン

            public delegate int EnumWindowsDelegate(IntPtr hWnd, int lParam);

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }

            public static bool CloseWindow2(IntPtr hWnd)
            {
                return PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }

            [DllImport("user32.dll")]
            public static extern int EnumWindows(EnumWindowsDelegate lpEnumFunc, int lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
            public static string GetClassNameA(IntPtr hWnd)
            {
                StringBuilder buffer = new StringBuilder(128);
                GetClassName(hWnd, buffer, buffer.Capacity);
                return buffer.ToString();
            }

            [DllImport("user32.dll")]
            private static extern IntPtr GetWindowRect(IntPtr hWnd, ref RECT rect);

            public static System.Drawing.Rectangle GetWindowRectHelper(IntPtr hWnd)
            {
                var r = new RECT();
                GetWindowRect(hWnd, ref r);
                return new System.Drawing.Rectangle(r.left, r.top, r.right - r.left, r.bottom - r.top);
            }

            [DllImport("user32.dll")]
            private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpStr, int nMaxCount);

            [DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("user32.dll")]
            public static extern bool IsIconic(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            public static string GetWindowTextHelper(IntPtr hWnd)
            {
                string title = "";
                StringBuilder sb = new StringBuilder(0x1024);
                if (IsWindowVisible(hWnd) && GetWindowText(hWnd, sb, sb.Capacity) != 0) {
                    title = sb.ToString();
                }
                return title;
            }

            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("user32.dll", SetLastError = true)]
            static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

            [DllImport("user32.dll")]
            public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

            [DllImport("User32.dll")]
            public static extern bool UpdateWindow(IntPtr hWnd);

        }


        public static class Kernel32
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct OSVERSIONINFOEX
            {
                public int dwOSVersionInfoSize;
                public int dwMajorVersion;
                public int dwMinorVersion;
                public int dwBuildNumber;
                public int dwPlatformId;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
                public string szCSDVersion;
                public short wServicePackMajor;
                public short wServicePackMinor;
                public short wSuiteMask;
                public byte wProductType;
                public byte wReserved;
            }
            public enum COMPUTER_NAME_FORMAT
            {
                ComputerNameNetBIOS,
                ComputerNameDnsHostname,
                ComputerNameDnsDomain,
                ComputerNameDnsFullyQualified,
                ComputerNamePhysicalNetBIOS,
                ComputerNamePhysicalDnsHostname,
                ComputerNamePhysicalDnsDomain,
                ComputerNamePhysicalDnsFullyQualified,
            }

            // pdwReturnedProductType定義値
            public const uint PRODUCT_UNDEFINED = 0x00000000;       // An unknown product
            public const uint PRODUCT_ULTIMATE = 0x00000001;        // Ultimate Edition
            public const uint PRODUCT_HOME_BASIC = 0x00000002;      // Home Basic Edition
            public const uint PRODUCT_HOME_PREMIUM = 0x00000003;    // Home Premium Edition
            public const uint PRODUCT_ENTERPRISE = 0x00000004;      // Enterprise Edition
            public const uint PRODUCT_BUSINESS = 0x00000006;        // Business Edition
            public const uint PRODUCT_PROFESSIONAL = 0x00000030;    // Professional

            [DllImport("kernel32.dll")]
            internal static extern bool CloseHandle(IntPtr handle);

            [DllImport("Kernel32.dll")]
            public static extern bool GetProductInfo(
                                      int dwOSMajorVersion,
                                      int dwOSMinorVersion,
                                      int dwSpMajorVersion,
                                      int dwSpMinorVersion,
                                      out uint pdwReturnedProductType);

            [DllImport("kernel32.Dll")]
            public static extern bool GetVersionEx(ref OSVERSIONINFOEX o);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern bool SetComputerNameEx(COMPUTER_NAME_FORMAT NameType, string lpBuffer);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool lpSystemInfo);

            [DllImport("kernel32.dll")]
            internal static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

            [DllImport("kernel32.dll")]
            public static extern bool SetComputerName(string lpComputerName);
        }

        public static class PSAPI
        {
            [DllImport("psapi.dll")]
            internal static extern int GetModuleBaseName(IntPtr hWnd, IntPtr hModule, StringBuilder lpFileName, int nSize);

            [DllImport("psapi.dll")]
            internal static extern int GetModuleFileNameEx(IntPtr hWnd, IntPtr hModule, StringBuilder lpFileName, int nSize);
        }

    }
}
