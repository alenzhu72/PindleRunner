using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace PindleRunner
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        static void Main(string[] args)
        {
            SetProcessDPIAware();
            var config = new RunnerConfig
            {
                WindowTitleKeyword = args.Length > 0 ? args[0] : "Diablo II",
                ClientWidth = 1280,
                ClientHeight = 800,
                DelayMs = 30
            };

            var window = new WindowManager(config.WindowTitleKeyword);
            var input = new InputSimulator();
            var screen = new ScreenCapture();
            var matcher = new TemplateMatcher();
            var hotkeys = new HotkeyService(window, screen, input, matcher);
            hotkeys.Start();

            bool exitRequested = false;
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                exitRequested = true;
                hotkeys.Stop();
                Console.WriteLine("已请求退出");
            };

            while (!exitRequested)
            {
                if (!window.Activate())
                {
                    Console.WriteLine("未找到目标窗口");
                    Thread.Sleep(2000);
                    continue;
                }
                Thread.Sleep(1000);
            }
        }
    }
}
