using System;
using System.Runtime.InteropServices;

namespace PindleRunner
{
    public sealed class ScreenCapture
    {
        public ImageData Capture(RectI rect)
        {
            var hdcSrc = GetDC(IntPtr.Zero);
            var hdcMem = CreateCompatibleDC(hdcSrc);
            var hBmp = CreateCompatibleBitmap(hdcSrc, rect.Width, rect.Height);
            var hOld = SelectObject(hdcMem, hBmp);
            BitBlt(hdcMem, 0, 0, rect.Width, rect.Height, hdcSrc, rect.X, rect.Y, SRCCOPY);

            var info = new BITMAPINFO();
            info.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
            info.bmiHeader.biWidth = rect.Width;
            info.bmiHeader.biHeight = -rect.Height;
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;
            info.bmiHeader.biCompression = BI_RGB;

            var stride = rect.Width * 4;
            var buffer = new byte[stride * rect.Height];
            GetDIBits(hdcMem, hBmp, 0, (uint)rect.Height, buffer, ref info, DIB_RGB_COLORS);

            SelectObject(hdcMem, hOld);
            DeleteObject(hBmp);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcSrc);

            return new ImageData(rect.Width, rect.Height, stride, buffer);
        }

        const int SRCCOPY = 0x00CC0020;
        const uint BI_RGB = 0;
        const uint DIB_RGB_COLORS = 0;

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

        [DllImport("gdi32.dll")]
        static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, byte[] lpvBits, ref BITMAPINFO lpbmi, uint uUsage);

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RGBQUAD { public byte rgbBlue; public byte rgbGreen; public byte rgbRed; public byte rgbReserved; }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public RGBQUAD bmiColors;
        }
    }
}
