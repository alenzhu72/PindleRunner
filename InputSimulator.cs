using System;
using System.Runtime.InteropServices;

namespace PindleRunner
{
    public enum VirtualKey : ushort
    {
        BACK = 0x08,
        TAB = 0x09,
        RETURN = 0x0D,
        ESCAPE = 0x1B,
        SPACE = 0x20,
        LWIN = 0x5B,
        RWIN = 0x5C,
        PRIOR = 0x21,
        NEXT = 0x22,
        END = 0x23,
        HOME = 0x24,
        LEFT = 0x25,
        UP = 0x26,
        RIGHT = 0x27,
        DOWN = 0x28,
        INSERT = 0x2D,
        DELETE = 0x2E,
        CAPITAL = 0x14,
        MENU = 0x12,
        LSHIFT = 0xA0,
        RSHIFT = 0xA1,
        LCONTROL = 0xA2,
        RCONTROL = 0xA3,
        LMENU = 0xA4,
        RMENU = 0xA5,
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B
    }

    public sealed class InputSimulator
    {
        public void MoveClient(IntPtr hwnd, int cx, int cy)
        {
            var p = new POINT { X = cx, Y = cy };
            ClientToScreen(hwnd, ref p);
            MoveMouse(p.X, p.Y);
        }

        public void MoveScreen(int x, int y)
        {
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                U = new InputUnion { mi = new MOUSEINPUT { dx = x, dy = y, dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE } }
            };
            Normalize(ref input.U.mi);
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public void KeyPress(VirtualKey key)
        {
            var down = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)key } }
            };
            var up = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)key, dwFlags = KEYEVENTF_KEYUP } }
            };
            SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
        }

        public void KeyChord(VirtualKey[] modifiers, VirtualKey key)
        {
            var inputs = new System.Collections.Generic.List<INPUT>();
            foreach (var m in modifiers)
            {
                inputs.Add(new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)m } } });
            }
            inputs.Add(new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)key } } });
            inputs.Add(new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)key, dwFlags = KEYEVENTF_KEYUP } } });
            for (int i = modifiers.Length - 1; i >= 0; i--)
            {
                inputs.Add(new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)modifiers[i], dwFlags = KEYEVENTF_KEYUP } } });
            }
            if (inputs.Count > 0)
            {
                SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
            }
        }

        public void KeyDown(VirtualKey key)
        {
            var down = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)key } } };
            SendInput(1, new[] { down }, Marshal.SizeOf<INPUT>());
        }

        public void KeyUp(VirtualKey key)
        {
            var up = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)key, dwFlags = KEYEVENTF_KEYUP } } };
            SendInput(1, new[] { up }, Marshal.SizeOf<INPUT>());
        }

        public void ClickClient(IntPtr hwnd, int cx, int cy)
        {
            var p = new POINT { X = cx, Y = cy };
            ClientToScreen(hwnd, ref p);
            MoveMouse(p.X, p.Y);
            MouseClickLeft();
        }

        public void RightClickClient(IntPtr hwnd, int cx, int cy)
        {
            var p = new POINT { X = cx, Y = cy };
            ClientToScreen(hwnd, ref p);
            MoveMouse(p.X, p.Y);
            MouseClickRight();
        }

        void MoveMouse(int x, int y)
        {
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                U = new InputUnion { mi = new MOUSEINPUT { dx = x, dy = y, dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE } }
            };
            Normalize(ref input.U.mi);
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        void MouseClickLeft()
        {
            var down = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } } };
            var up = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } } };
            SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
        }

        void MouseClickRight()
        {
            var down = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTDOWN } } };
            var up = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTUP } } };
            SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
        }

        static void Normalize(ref MOUSEINPUT mi)
        {
            var screenX = GetSystemMetrics(0);
            var screenY = GetSystemMetrics(1);
            mi.dx = (int)Math.Round(mi.dx * (65535.0 / (screenX - 1)));
            mi.dy = (int)Math.Round(mi.dy * (65535.0 / (screenY - 1)));
        }

        [DllImport("user32.dll")]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        const int INPUT_MOUSE = 0;
        const int INPUT_KEYBOARD = 1;
        const int KEYEVENTF_KEYUP = 0x0002;
        const int MOUSEEVENTF_MOVE = 0x0001;
        const int MOUSEEVENTF_ABSOLUTE = 0x8000;
        const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        const int MOUSEEVENTF_LEFTUP = 0x0004;
        const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const int MOUSEEVENTF_RIGHTUP = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public int type; public InputUnion U; }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public int dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public int dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public int uMsg;
            public short wParamL;
            public short wParamH;
        }

        struct POINT { public int X; public int Y; }
    }
}
