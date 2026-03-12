using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        CancellationTokenSource? scriptCts;
        readonly object scriptLock = new();
        MagnifierController? magnifier;

        public bool IsRunning => running;

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
            Console.WriteLine("热键: F9取色(Focus窗口相对坐标)/F10局部放大镜(鼠标附近)/Ctrl+Shift+X执行/F11截64x64模板(鼠标为左上角)保存到根目录/template.bmp与template.tmp/Ctrl+Shift+Z停止脚本(保留热键与控制台窗口)/Ctrl+Shift+C运行脚本");
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            scriptCts?.Cancel();
            magnifier?.Dispose();
            if (threadId != 0) PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        void Loop()
        {
            threadId = GetCurrentThreadId();
            PeekMessage(out var _, IntPtr.Zero, 0, 0, PM_NOREMOVE);
            RegisterHotKeyOrLog(1, 0, (int)VirtualKey.F9);
            RegisterHotKeyOrLog(2, 0, (int)VirtualKey.F10);
            RegisterHotKeyOrLog(4, MOD_CONTROL | MOD_SHIFT, VK_X);
            RegisterHotKeyOrLog(5, 0, (int)VirtualKey.F11);
            RegisterHotKeyOrLog(6, MOD_CONTROL | MOD_SHIFT, VK_Z);
            RegisterHotKeyOrLog(7, MOD_CONTROL | MOD_SHIFT, VK_C);

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
            UnregisterHotKey(IntPtr.Zero, 4);
            UnregisterHotKey(IntPtr.Zero, 5);
            UnregisterHotKey(IntPtr.Zero, 6);
            UnregisterHotKey(IntPtr.Zero, 7);
            threadId = 0;
        }

        void RegisterHotKeyOrLog(int id, int fsModifiers, int vk)
        {
            if (RegisterHotKey(IntPtr.Zero, id, fsModifiers, vk)) return;
            int err = Marshal.GetLastWin32Error();
            Console.WriteLine($"注册热键失败: id={id} vk=0x{vk:X} mod=0x{fsModifiers:X} err={err}");
        }

        void HandleHotkey(int id)
        {
            if (id == 6)
            {
                StopScript();
                return;
            }

            if (id == 7)
            {
                StartScript(false, null);
                return;
            }

            if (!window.Activate()) return;
            if (!window.GetClientRect(out var rect)) return;
            POINT p;
            GetCursorPos(out p);

            switch (id)
            {
                case 1:
                {
                    var img = capture.Capture(rect);
                    if (p.X >= rect.X && p.X < rect.X + rect.Width && p.Y >= rect.Y && p.Y < rect.Y + rect.Height)
                    {
                        int rx = p.X - rect.X;
                        int ry = p.Y - rect.Y;
                        GetPixel(img, rx, ry, out var r, out var g, out var b);
                        Console.WriteLine($"相对坐标=({rx},{ry}) 颜色=#{r:X2}{g:X2}{b:X2}");

                        var ok = TryFindMostDistinctPixel(img, rx, ry, 6, out var bx, out var by, out var br, out var bg, out var bb);
                        if (ok)
                        {
                            Console.WriteLine($"近邻最佳坐标=({bx},{by}) 颜色=#{br:X2}{bg:X2}{bb:X2}");
                        }
                    }
                    break;
                }
                case 2:
                {
                    magnifier ??= new MagnifierController(capture);
                    magnifier.Toggle();
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
            }
        }

        void StopScript()
        {
            lock (scriptLock)
            {
                if (scriptCts == null)
                {
                    Console.WriteLine("脚本未在运行");
                    return;
                }
                Console.WriteLine("已请求停止脚本（热键仍可用，可 Ctrl+Shift+C 重新启动）");
                scriptCts.Cancel();
            }
        }

        static void GetPixel(ImageData img, int x, int y, out byte r, out byte g, out byte b)
        {
            var o = y * img.Stride + x * 4;
            b = img.Buffer[o + 0];
            g = img.Buffer[o + 1];
            r = img.Buffer[o + 2];
        }

        static bool TryFindMostDistinctPixel(ImageData img, int cx, int cy, int radius, out int bx, out int by, out byte br, out byte bg, out byte bb)
        {
            int x1 = cx - radius;
            int y1 = cy - radius;
            int x2 = cx + radius;
            int y2 = cy + radius;
            if (x2 < 0 || y2 < 0 || x1 >= img.Width || y1 >= img.Height)
            {
                bx = by = 0;
                br = bg = bb = 0;
                return false;
            }

            if (x1 < 0) x1 = 0;
            if (y1 < 0) y1 = 0;
            if (x2 >= img.Width) x2 = img.Width - 1;
            if (y2 >= img.Height) y2 = img.Height - 1;

            long sumR = 0, sumG = 0, sumB = 0;
            int n = 0;
            for (int y = y1; y <= y2; y++)
            {
                int row = y * img.Stride;
                for (int x = x1; x <= x2; x++)
                {
                    int o = row + x * 4;
                    sumB += img.Buffer[o + 0];
                    sumG += img.Buffer[o + 1];
                    sumR += img.Buffer[o + 2];
                    n++;
                }
            }
            if (n <= 0)
            {
                bx = by = 0;
                br = bg = bb = 0;
                return false;
            }

            int meanR = (int)(sumR / n);
            int meanG = (int)(sumG / n);
            int meanB = (int)(sumB / n);

            int bestScore = -1;
            bx = cx; by = cy;
            br = bg = bb = 0;

            for (int y = y1; y <= y2; y++)
            {
                int row = y * img.Stride;
                for (int x = x1; x <= x2; x++)
                {
                    int o = row + x * 4;
                    int pb = img.Buffer[o + 0];
                    int pg = img.Buffer[o + 1];
                    int pr = img.Buffer[o + 2];
                    int dr = pr - meanR;
                    int dg = pg - meanG;
                    int db = pb - meanB;
                    int score = dr * dr + dg * dg + db * db;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bx = x;
                        by = y;
                        br = (byte)pr;
                        bg = (byte)pg;
                        bb = (byte)pb;
                    }
                }
            }

            return bestScore > 0;
        }

        void StartScript(bool autoRestart, string? reason)
        {
            if (!running) return;
            lock (scriptLock)
            {
                if (autoRestart && !string.IsNullOrWhiteSpace(reason))
                {
                    Console.WriteLine($"[WARNING] {reason}");
                }

                scriptCts?.Cancel();
                scriptCts?.Dispose();
                scriptCts = new CancellationTokenSource();
                var token = scriptCts.Token;

                var path = System.IO.Path.Combine(AppContext.BaseDirectory, "script.vb");
                if (!System.IO.File.Exists(path))
                {
                    Console.WriteLine($"未找到脚本文件: {path}");
                    return;
                }

                if (!window.Activate())
                {
                    Console.WriteLine($"[WARNING] 未找到目标窗口(当前关键字=\"{window.TitleKeyword}\")，脚本仍会启动；可在脚本里用 WINDOW 指令切换目标窗口");
                }

                Task.Run(() =>
                {
                    try
                    {
                        var engine = new ScriptEngine(window, capture, input, matcher);
                        engine.RunFile(path, token);
                    }
                    catch (ScriptRestartException ex)
                    {
                        if (token.IsCancellationRequested) return;
                        if (!running) return;
                        StartScript(true, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        if (token.IsCancellationRequested) return;
                        Console.WriteLine($"脚本异常: {ex.Message}");
                    }
                }, token);
            }
        }

        const int WM_HOTKEY = 0x0312;
        const uint WM_QUIT = 0x0012;
        const int MOD_CONTROL = 0x0002;
        const int MOD_SHIFT = 0x0004;
        const int VK_Z = 0x5A;
        const int VK_X = 0x58;
        const int VK_C = 0x43;
        const uint PM_NOREMOVE = 0;

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

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

        sealed class MagnifierController : IDisposable
        {
            readonly ScreenCapture capture;
            Thread? uiThread;
            MagnifierForm? form;
            readonly ManualResetEventSlim ready = new(false);

            public MagnifierController(ScreenCapture capture)
            {
                this.capture = capture;
            }

            public void Toggle()
            {
                EnsureStarted();
                var f = form;
                if (f is null || f.IsDisposed) return;
                f.BeginInvoke(new Action(() =>
                {
                    if (f.Visible) f.Hide();
                    else f.Show();
                }));
            }

            void EnsureStarted()
            {
                if (uiThread != null && uiThread.IsAlive && form is { IsDisposed: false }) return;
                uiThread = null;
                form = null;
                ready.Reset();
                uiThread = new Thread(() =>
                {
                    Application.SetHighDpiMode(HighDpiMode.SystemAware);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.ThreadException += (_, e) => Console.WriteLine($"放大镜异常: {e.Exception.Message}");
                    form = new MagnifierForm(capture);
                    _ = form.Handle;
                    ready.Set();
                    Application.Run(form);
                })
                { IsBackground = true };
                uiThread.SetApartmentState(ApartmentState.STA);
                uiThread.Start();
                ready.Wait();
            }

            public void Dispose()
            {
                if (form is { IsDisposed: false })
                {
                    try
                    {
                        form.BeginInvoke(new Action(() => form.Close()));
                    }
                    catch
                    {
                    }
                }
                uiThread = null;
                form = null;
            }
        }

        sealed class MagnifierForm : Form
        {
            readonly ScreenCapture capture;
            readonly System.Windows.Forms.Timer timer;
            readonly PictureBox box;
            readonly int src;
            readonly int scale;
            Bitmap? lastBitmap;
            int lastLeft;
            int lastTop;

            protected override bool ShowWithoutActivation => true;

            public MagnifierForm(ScreenCapture capture)
            {
                this.capture = capture;

                src = 32;
                scale = 10;
                int dst = src * scale;

                AutoScaleMode = AutoScaleMode.None;
                FormBorderStyle = FormBorderStyle.FixedToolWindow;
                ShowInTaskbar = false;
                TopMost = true;
                Text = "Magnifier";
                ClientSize = new Size(dst, dst);

                var vs = SystemInformation.VirtualScreen;
                Location = new Point(vs.Right - Width - 20, vs.Top + 20);

                box = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Normal
                };
                box.MouseDown += (_, e) =>
                {
                    int sx = e.X / scale;
                    int sy = e.Y / scale;
                    if (sx < 0) sx = 0;
                    if (sy < 0) sy = 0;
                    if (sx >= src) sx = src - 1;
                    if (sy >= src) sy = src - 1;
                    SetCursorPos(lastLeft + sx, lastTop + sy);
                };
                Controls.Add(box);

                timer = new System.Windows.Forms.Timer { Interval = 33 };
                timer.Tick += (_, __) => SafeUpdateFrame();

                Shown += (_, __) => timer.Start();
                VisibleChanged += (_, __) =>
                {
                    if (Visible) timer.Start();
                    else timer.Stop();
                };
                FormClosing += (_, __) =>
                {
                    timer.Stop();
                    if (lastBitmap != null) { lastBitmap.Dispose(); lastBitmap = null; }
                };

                Hide();
            }

            void SafeUpdateFrame()
            {
                try
                {
                    UpdateFrame();
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    Hide();
                    Console.WriteLine($"放大镜异常: {ex.Message}");
                }
            }

            void UpdateFrame()
            {
                if (!Visible) return;

                if (!GetCursorPos(out var p)) return;
                var vs = SystemInformation.VirtualScreen;
                int half = src / 2;
                int left = p.X - half;
                int top = p.Y - half;
                if (left < vs.Left) left = vs.Left;
                if (top < vs.Top) top = vs.Top;
                if (left + src > vs.Right) left = vs.Right - src;
                if (top + src > vs.Bottom) top = vs.Bottom - src;
                lastLeft = left;
                lastTop = top;

                var img = capture.Capture(new RectI(left, top, src, src));
                var srcBmp = ToBitmap(img);
                try
                {
                    var dstBmp = new Bitmap(src * scale, src * scale, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(dstBmp))
                    {
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.SmoothingMode = SmoothingMode.None;
                        g.DrawImage(srcBmp, new Rectangle(0, 0, src * scale, src * scale), new Rectangle(0, 0, src, src), GraphicsUnit.Pixel);

                        int c = (src * scale) / 2;
                        using var pen = new Pen(Color.Lime, 1);
                        g.DrawLine(pen, c, 0, c, src * scale);
                        g.DrawLine(pen, 0, c, src * scale, c);
                    }

                    var old = lastBitmap;
                    box.Image = dstBmp;
                    lastBitmap = dstBmp;
                    if (old != null) old.Dispose();
                }
                finally
                {
                    srcBmp.Dispose();
                }
            }

            static Bitmap ToBitmap(ImageData img)
            {
                var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                var data = bmp.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int srcStride = img.Stride;
                    int dstStride = data.Stride;
                    int rowBytes = img.Width * 4;
                    for (int y = 0; y < img.Height; y++)
                    {
                        Marshal.Copy(img.Buffer, y * srcStride, IntPtr.Add(data.Scan0, y * dstStride), rowBytes);
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                return bmp;
            }

            [DllImport("user32.dll")]
            static extern bool GetCursorPos(out POINT lpPoint);

            [DllImport("user32.dll")]
            static extern bool SetCursorPos(int X, int Y);
        }
    }
}
