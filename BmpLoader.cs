using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PindleRunner
{
    public static class BmpLoader
    {
        public static bool LoadBmp(string path, out ImageData img)
        {
            img = default;
            IntPtr hBitmap = LoadImage(IntPtr.Zero, path, 0, 0, 0, 0x0010 | 0x0010000);
            if (hBitmap == IntPtr.Zero) return false;
            BITMAP bm = new BITMAP();
            GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), ref bm);
            var width = bm.bmWidth;
            var height = bm.bmHeight;
            var hdc = GetDC(IntPtr.Zero);
            var info = new BITMAPINFO();
            info.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
            info.bmiHeader.biWidth = width;
            info.bmiHeader.biHeight = -height;
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;
            info.bmiHeader.biCompression = 0;
            var stride = width * 4;
            var buffer = new byte[stride * height];
            GetDIBits(hdc, hBitmap, 0, (uint)height, buffer, ref info, 0);
            ReleaseDC(IntPtr.Zero, hdc);
            DeleteObject(hBitmap);
            img = new ImageData(width, height, stride, buffer);
            return true;
        }

        public static bool SaveBmp(string path, ImageData img)
        {
            try
            {
                using var fs = File.Create(path);
                using var bw = new BinaryWriter(fs);
                ushort bfType = 0x4D42; // 'BM'
                uint offBits = 14 + 40;
                uint sizeImage = (uint)(img.Stride * img.Height);
                uint fileSize = offBits + sizeImage;
                // BITMAPFILEHEADER
                bw.Write(bfType);
                bw.Write(fileSize);
                bw.Write((ushort)0);
                bw.Write((ushort)0);
                bw.Write(offBits);
                // BITMAPINFOHEADER
                bw.Write(40u);
                bw.Write(img.Width);
                bw.Write(-img.Height); // top-down DIB
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write(0u); // BI_RGB
                bw.Write(sizeImage);
                bw.Write(0); // x ppm
                bw.Write(0); // y ppm
                bw.Write(0u);
                bw.Write(0u);
                // pixels (BGRA)
                bw.Write(img.Buffer, 0, img.Buffer.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("gdi32.dll")]
        static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref BITMAP lpvObject);

        [DllImport("gdi32.dll")]
        static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, byte[] lpvBits, ref BITMAPINFO lpbmi, uint uUsage);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr ho);

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

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
