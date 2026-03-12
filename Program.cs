using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PindleRunner
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        static void Main(string[] args)
        {
            using var mutex = new Mutex(true, @"Global\PindleRunnerHotkeys", out bool createdNew);
            if (!createdNew)
            {
                Console.WriteLine("检测到已有一个 PindleRunner 在运行（热键已被占用）。请先关闭旧实例后再启动。");
                return;
            }

            StreamWriter? logWriter = null;
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var logPath = Path.Combine(AppContext.BaseDirectory, $"log_{ts}.txt");
                logWriter = new StreamWriter(File.Open(logPath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false))
                {
                    AutoFlush = true
                };
                var tee = new TeeTextWriter(Console.Out, logWriter);
                Console.SetOut(tee);
                Console.SetError(tee);
                Console.WriteLine($"日志已保存到: {logPath}");
            }
            catch
            {
            }

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

            try
            {
                while (!exitRequested)
                {
                    if (!hotkeys.IsRunning)
                    {
                        Thread.Sleep(200);
                        continue;
                    }
                    if (!window.Activate())
                    {
                        Console.WriteLine("未找到目标窗口");
                        Thread.Sleep(2000);
                        continue;
                    }
                    Thread.Sleep(1000);
                }
            }
            finally
            {
                hotkeys.Stop();
                logWriter?.Dispose();
            }
        }

        sealed class TeeTextWriter : TextWriter
        {
            readonly TextWriter a;
            readonly TextWriter b;

            public TeeTextWriter(TextWriter a, TextWriter b)
            {
                this.a = a;
                this.b = b;
            }

            public override Encoding Encoding => a.Encoding;

            public override void Write(char value)
            {
                a.Write(value);
                b.Write(value);
            }

            public override void Write(string? value)
            {
                a.Write(value);
                b.Write(value);
            }

            public override void WriteLine(string? value)
            {
                a.WriteLine(value);
                b.WriteLine(value);
            }

            public override void Flush()
            {
                a.Flush();
                b.Flush();
            }
        }
    }
}
