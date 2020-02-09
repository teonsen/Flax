using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Flax.Windows
{
    public static class Capture
    {
        /// <summary>
        /// Capture desktop
        /// </summary>
        /// <param name="savePath"></param>
        public static void Desktop(string savePath)
        {
            File.Delete(savePath);
            using (var img = Window(Win32API.User32.GetDesktopWindow(), 0, 0))
            {
                SaveByFileStream(img, savePath);
            }
        }

        public static Image Desktop()
        {
            var img = Window(Win32API.User32.GetDesktopWindow(), 0, 0);
            return img;
        }

        // Capture Window
        public static Image Window(IntPtr hWnd)
        {
            var rect = Win32API.User32.GetWindowRectHelper(hWnd);
            try
            {
                var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(rect.X, rect.Y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                }
                return bmp;
            }
            catch (ArgumentException ae)
            {
                System.Windows.Forms.MessageBox.Show("The Window might have closed after the window handle was obtained.", "ERROR", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            return null;
        }

        public static Image Window(IntPtr hWnd, int widthAdjust, int heightAdjust)
        {
            IntPtr hdcSrc = Win32API.User32.GetWindowDC(hWnd);
            var src_windowRect = Win32API.User32.GetWindowRectHelper(hWnd);

            int width = src_windowRect.Width + (widthAdjust * 2);
            int height = src_windowRect.Height + (heightAdjust * 2);
            IntPtr hdcDest = GDI32.CreateCompatibleDC(hdcSrc);
            IntPtr hBitmap = GDI32.CreateCompatibleBitmap(hdcSrc, width, height);
            IntPtr hOld = GDI32.SelectObject(hdcDest, hBitmap);
            GDI32.BitBlt(hdcDest, 0, 0, width, height, hdcSrc, -widthAdjust, -heightAdjust, GDI32.SRCCOPY);
            GDI32.SelectObject(hdcDest, hOld);
            GDI32.DeleteDC(hdcDest);
            Win32API.User32.ReleaseDC(hWnd, hdcSrc);
            Image img = Image.FromHbitmap(hBitmap);
            GDI32.DeleteObject(hBitmap);
            return img;
        }

        public static void Window(IntPtr hSrcWnd, string savePath, int widthAdjust, int heightAdjust)
        {
            using (var img = Window(hSrcWnd, widthAdjust, heightAdjust))
            {
                if (img != null)
                {
                    SaveByFileStream(img, savePath);
                }
            }
        }

        public static void Window(IntPtr hSrcWnd, string savePath)
        {
            using (var img = Window(hSrcWnd))
            {
                if (img != null)
                {
                    SaveByFileStream(img, savePath);
                }
            }
        }

        public static Image Region(Rectangle rect)
        {
            if (rect.Width == 0 && rect.Height == 0) return null;
            if (rect.Width >= 10000 || rect.Height >= 7000) return null;
            if (rect.X < -20 || rect.Y < -50) return null;
            var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(rect.X, rect.Y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            }
            return bmp;
        }

        public static void Region(Rectangle rect, string savePath)
        {
            using (var img = Region(rect))
            {
                if (img != null)
                {
                    SaveByFileStream(img, savePath);
                }
            }
        }

        public static void SaveByFileStream(Image img, string savePath)
        {
            if (img == null) return;
            
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
            using (FileStream fs = new FileStream(savePath, FileMode.Create))
            {
                string lowerdPath = savePath.ToLower();
                if (lowerdPath.EndsWith(".bmp"))
                    img.Save(fs, ImageFormat.Bmp);
                else if (lowerdPath.EndsWith(".png"))
                    img.Save(fs, ImageFormat.Png);
                else if (lowerdPath.EndsWith(".jpg"))
                    img.Save(fs, ImageFormat.Jpeg);
                else
                    img.Save(fs, ImageFormat.Bmp);
                fs.Close();
            }
        }

        /// Helper class containing Gdi32 API functions
        private class GDI32
        {
            public const int SRCCOPY = 0x00CC0020; // BitBlt dwRop parameter
            [DllImport("gdi32.dll")]
            public static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest,
                int nWidth, int nHeight, IntPtr hObjectSource,
                int nXSrc, int nYSrc, int dwRop);
            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);
            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
            [DllImport("gdi32.dll")]
            public static extern bool DeleteDC(IntPtr hDC);
            [DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr hObject);
            [DllImport("gdi32.dll")]
            public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        }
    }
}
