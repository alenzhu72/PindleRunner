using System;
using System.Threading;


namespace PindleRunner
{
    public sealed class RunnerConfig
    {
        public string WindowTitleKeyword { get; set; } = "Diablo II";
        public int ClientWidth { get; set; } = 1280;
        public int ClientHeight { get; set; } = 800;
        public int DelayMs { get; set; } = 30;
    }

    public sealed class BotRunner
    {
        readonly WindowManager window;
        readonly InputSimulator input;
        readonly ScreenCapture screen;
        readonly RunnerConfig config;

        public BotRunner(WindowManager window, InputSimulator input, ScreenCapture screen, RunnerConfig config)
        {
            this.window = window;
            this.input = input;
            this.screen = screen;
            this.config = config;
        }

        public void RunSinglePindle()
        {
            if (!window.GetClientRect(out var rect)) return;
            RouteToAnya(rect);
            EnterRedPortal(rect);
            FightPindle(rect);
            ExitGame(rect);
        }

        void RouteToAnya(RectI rect)
        {
            var points = new[]
            {
                Client(rect, 0.72, 0.28),
                Client(rect, 0.80, 0.35),
                Client(rect, 0.88, 0.46),
                Client(rect, 0.92, 0.56),
                Client(rect, 0.90, 0.65),
                Client(rect, 0.86, 0.72)
            };
            foreach (var p in points)
            {
                input.ClickClient(window.HWnd, p.X, p.Y);
                Thread.Sleep(550);
            }
        }

        void EnterRedPortal(RectI rect)
        {
            var p = Client(rect, 0.86, 0.72);
            input.ClickClient(window.HWnd, p.X, p.Y);
            Thread.Sleep(1200);
            input.ClickClient(window.HWnd, p.X, p.Y);
            Thread.Sleep(2000);
        }

        void FightPindle(RectI rect)
        {
            var p = Client(rect, 0.50, 0.60);
            input.RightClickClient(window.HWnd, p.X, p.Y);
            Thread.Sleep(250);
            for (int i = 0; i < 12; i++)
            {
                input.RightClickClient(window.HWnd, p.X, p.Y);
                Thread.Sleep(120);
            }
            Thread.Sleep(1000);
        }

        void ExitGame(RectI rect)
        {
            input.KeyPress(VirtualKey.ESCAPE);
            Thread.Sleep(300);
            var leave = Client(rect, 0.50, 0.66);
            input.ClickClient(window.HWnd, leave.X, leave.Y);
            Thread.Sleep(2500);
        }

        static PointI Client(RectI rect, double nx, double ny)
        {
            return new PointI(rect.X + (int)(rect.Width * nx), rect.Y + (int)(rect.Height * ny));
        }
    }
}
