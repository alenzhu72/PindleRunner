using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace PindleRunner
{
    public sealed class HotkeyService
    {
        readonly WindowManager window;
        readonly ScreenCapture capture;
        readonly InputSimulator input;
        readonly TemplateMatcher matcher;
        readonly List<PointI> route = new();
        ImageData? template;
        Thread? thread;
        bool running;
        uint threadId;

        public HotkeyService(WindowManager window, ScreenCapture capture, InputSimulator input, TemplateMatcher matcher)
        {
            this.window = window;
            this.capture = capture;
            this.input = input;
            this.matcher = matcher;
        }

        public void Start()
        {
            if (running) return;
            running = true;
            thread = new Thread(Loop) { IsBackground = true };
            thread.Start();
            Console.WriteLine("热键: F9取色(Focus窗口相对坐标)/F10记录坐标/Ctrl+Shift+Z清空/Ctrl+Shift+X执行/F11截64x64模板(鼠标为左上角)保存到根目录/template.bmp与template.tmp/F12加载模板搜索并点击/Ctrl+Shift+C运行脚本");
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            if (threadId != 0) PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        void Loop()
        {
            threadId = GetCurrentThreadId();
            RegisterHotKey(IntPtr.Zero, 1, 0, (int)VirtualKey.F9);
            RegisterHotKey(IntPtr.Zero, 2, 0, (int)VirtualKey.F10);
            RegisterHotKey(IntPtr.Zero, 3, MOD_CONTROL | MOD_SHIFT, VK_Z);
            RegisterHotKey(IntPtr.Zero, 4, MOD_CONTROL | MOD_SHIFT, VK_X);
            RegisterHotKey(IntPtr.Zero, 5, 0, (int)VirtualKey.F11);
            RegisterHotKey(IntPtr.Zero, 6, 0, (int)VirtualKey.F12);
            RegisterHotKey(IntPtr.Zero, 7, MOD_CONTROL | MOD_SHIFT, VK_C);

            MSG msg;
            while (running && GetMessage(out msg, IntPtr.Zero, 0, 0))
            {
                if (msg.message == WM_HOTKEY)
                {
                    int id = (int)msg.wParam;
                    HandleHotkey(id);
                }
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            UnregisterHotKey(IntPtr.Zero, 1);
            UnregisterHotKey(IntPtr.Zero, 2);
            UnregisterHotKey(IntPtr.Zero, 3);
            UnregisterHotKey(IntPtr.Zero, 4);
            UnregisterHotKey(IntPtr.Zero, 5);
            UnregisterHotKey(IntPtr.Zero, 6);
            UnregisterHotKey(IntPtr.Zero, 7);
            threadId = 0;
        }

        void HandleHotkey(int id)
        {
            if (!window.Activate()) return;
            if (!window.GetClientRect(out var rect)) return;
            POINT p;
            GetCursorPos(out p);
            var img = capture.Capture(rect);
            switch (id)
            {
                case 1:
                {
                    if (p.X >= rect.X && p.X < rect.X + rect.Width && p.Y >= rect.Y && p.Y < rect.Y + rect.Height)
                    {
                        int rx = p.X - rect.X;
                        int ry = p.Y - rect.Y;
                        var o = ry * img.Stride + rx * 4;
                        var b = img.Buffer[o + 0];
                        var g = img.Buffer[o + 1];
                        var r = img.Buffer[o + 2];
                        Console.WriteLine($"相对坐标=({rx},{ry}) 颜色=#{r:X2}{g:X2}{b:X2}");
                    }
                    break;
                }
                case 2:
                {
                    if (p.X >= rect.X && p.X < rect.X + rect.Width && p.Y >= rect.Y && p.Y < rect.Y + rect.Height)
                    {
                        route.Add(new PointI(p.X, p.Y));
                        Console.WriteLine($"记录坐标#{route.Count}: ({p.X},{p.Y})");
                    }
                    break;
                }
                case 3:
                {
                    route.Clear();
                    Console.WriteLine("已清空坐标序列");
                    break;
                }
                case 4:
                {
                    foreach (var pt in route)
                    {
                        input.ClickClient(window.HWnd, pt.X - rect.X, pt.Y - rect.Y);
                        Thread.Sleep(500);
                    }
                    Console.WriteLine("已执行坐标序列");
                    break;
                }
                case 5:
                {
                    int size = 64;
                    int left = Math.Max(rect.X, p.X);
                    int top = Math.Max(rect.Y, p.Y);
                    left = Math.Min(left, rect.X + rect.Width - size);
                    top = Math.Min(top, rect.Y + rect.Height - size);
                    var tplRect = new RectI(left, top, size, size);
                    template = capture.Capture(tplRect);
                    var pathBmp = System.IO.Path.Combine(AppContext.BaseDirectory, "template.bmp");
                    var pathTmp = System.IO.Path.Combine(AppContext.BaseDirectory, "template.tmp");
                    if (BmpLoader.SaveBmp(pathBmp, template.Value) && BmpLoader.SaveBmp(pathTmp, template.Value))
                    {
                        Console.WriteLine($"已截模板 {size}x{size} 于 ({left},{top}) 并保存到 {pathBmp} / {pathTmp}");
                    }
                    else
                    {
                        Console.WriteLine("保存模板失败");
                    }
                    break;
                }
                case 6:
                {
                    var pathBmp = System.IO.Path.Combine(AppContext.BaseDirectory, "template.bmp");
                    var pathTmp = System.IO.Path.Combine(AppContext.BaseDirectory, "template.tmp");
                    ImageData tpl;
                    string usedPath;
                    if (BmpLoader.LoadBmp(pathBmp, out tpl)) usedPath = pathBmp;
                    else if (BmpLoader.LoadBmp(pathTmp, out tpl)) usedPath = pathTmp;
                    else
                    {
                        Console.WriteLine($"未找到或无法加载模板文件: {pathBmp} 或 {pathTmp}");
                        break;
                    }
                    var search = new RectI(0, 0, rect.Width, rect.Height);
                    var ok = matcher.Find(img, tpl, search, 0.75, out var hit, out var best);
                    if (ok)
                    {
                        input.MoveClient(window.HWnd, hit.X + tpl.Width / 2, hit.Y + tpl.Height / 2);
                        Thread.Sleep(200);
                        input.ClickClient(window.HWnd, hit.X + tpl.Width / 2, hit.Y + tpl.Height / 2);
                        Console.WriteLine($"模板({System.IO.Path.GetFileName(usedPath)})命中于 ({hit.X},{hit.Y}) score={best} 并已点击");
                    }
                    else
                    {
                        Console.WriteLine($"模板未命中 best={best}");
                    }
                    break;
                }
                case 7:
                {
                    var path = System.IO.Path.Combine(AppContext.BaseDirectory, "script.txt");
                    if (System.IO.File.Exists(path))
                    {
                        var engine = new ScriptEngine(window, capture, input, matcher);
                        engine.RunFile(path);
                        Console.WriteLine("脚本执行完成");
                    }
                    else
                    {
                        Console.WriteLine($"未找到脚本文件: {path}");
                    }
                    break;
                }
            }
        }

        const int WM_HOTKEY = 0x0312;
        const uint WM_QUIT = 0x0012;
        const int MOD_CONTROL = 0x0002;
        const int MOD_SHIFT = 0x0004;
        const int VK_Z = 0x5A;
        const int VK_X = 0x58;
        const int VK_C = 0x43;

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hWnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }
    }
}
