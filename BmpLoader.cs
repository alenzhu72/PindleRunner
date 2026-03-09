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
            try
            {
                var fullPath = path;
                if (!Path.IsPathRooted(fullPath))
                    fullPath = Path.Combine(AppContext.BaseDirectory, fullPath);
                using var fs = File.OpenRead(fullPath);
                using var br = new BinaryReader(fs);
                if (br.ReadUInt16() != 0x4D42) return false;
                br.ReadUInt32();
                br.ReadUInt16();
                br.ReadUInt16();
                uint offBits = br.ReadUInt32();
                uint biSize = br.ReadUInt32();
                if (biSize < 40) return false;
                int width = br.ReadInt32();
                int height = br.ReadInt32();
                ushort planes = br.ReadUInt16();
                ushort bpp = br.ReadUInt16();
                uint compression = br.ReadUInt32();
                br.ReadUInt32();
                br.ReadInt32();
                br.ReadInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                if (planes != 1 || bpp != 32 || compression != 0) return false;
                bool topDown = height < 0;
                int absHeight = Math.Abs(height);
                int stride = width * 4;
                fs.Seek(offBits, SeekOrigin.Begin);
                var buffer = br.ReadBytes(stride * absHeight);
                if (buffer.Length != stride * absHeight) return false;
                if (!topDown)
                {
                    var flipped = new byte[buffer.Length];
                    for (int y = 0; y < absHeight; y++)
                    {
                        Buffer.BlockCopy(buffer, y * stride, flipped, (absHeight - 1 - y) * stride, stride);
                    }
                    buffer = flipped;
                }
                img = new ImageData(width, absHeight, stride, buffer);
                return true;
            }
            catch
            {
                return false;
            }
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
