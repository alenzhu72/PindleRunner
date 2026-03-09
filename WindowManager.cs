using System;
using System.Runtime.InteropServices;
using System.Text;


namespace PindleRunner
{
    public sealed class WindowManager
    {
        public IntPtr HWnd { get; private set; }
        readonly string titleKeyword;

        public WindowManager(string titleKeyword)
        {
            this.titleKeyword = titleKeyword;
        }

        public bool Activate()
        {
            HWnd = FindWindowByPartialTitle(titleKeyword);
            if (HWnd == IntPtr.Zero) return false;
            SetForegroundWindow(HWnd);
            return true;
        }

        public bool GetClientRect(out RectI rect)
        {
            rect = default;
            if (HWnd == IntPtr.Zero) return false;
            RECT r;
            if (!GetClientRect(HWnd, out r)) return false;
            POINT p = new POINT { X = 0, Y = 0 };
            ClientToScreen(HWnd, ref p);
            rect = new RectI(p.X, p.Y, r.Right - r.Left, r.Bottom - r.Top);
            return true;
        }

        static IntPtr FindWindowByPartialTitle(string keyword)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                var sb = new StringBuilder(256);
                GetWindowText(h, sb, sb.Capacity);
                var title = sb.ToString();
                if (!string.IsNullOrEmpty(title) && title.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
        struct POINT { public int X; public int Y; }
    }
}
