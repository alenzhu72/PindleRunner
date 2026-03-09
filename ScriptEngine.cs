using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace PindleRunner
{
    public sealed class ScriptEngine
    {
        readonly WindowManager window;
        readonly ScreenCapture capture;
        readonly InputSimulator input;
        readonly TemplateMatcher matcher;
        readonly Dictionary<string, int> labels = new(StringComparer.OrdinalIgnoreCase);
        readonly Stack<int> doStack = new();
        readonly Stack<ForFrame> forStack = new();
        readonly Dictionary<string, double> vars = new(StringComparer.OrdinalIgnoreCase);

        struct ForFrame
        {
            public string Var;
            public double End;
            public double Step;
            public int StartIp;
        }

        public ScriptEngine(WindowManager window, ScreenCapture capture, InputSimulator input, TemplateMatcher matcher)
        {
            this.window = window;
            this.capture = capture;
            this.input = input;
            this.matcher = matcher;
        }

        public void RunFile(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"脚本文件不存在: {path}");
                return;
            }
            var lines = File.ReadAllLines(path);
            Console.WriteLine($"开始执行脚本: {path} ({lines.Length} 行)");

            if (window.GetClientRect(out var rect))
            {
                if (rect.Width != 1280 || rect.Height != 800)
                {
                    Console.WriteLine($"[WARNING] 检测到窗口分辨率为 {rect.Width}x{rect.Height}。建议设置为 1280x800 以保证坐标准确。");
                }
                else
                {
                    Console.WriteLine($"[INFO] 窗口分辨率确认: 1280x800");
                }
            }

            Run(lines);
            Console.WriteLine("脚本执行完成");
        }

        public void Run(string[] lines)
        {
            labels.Clear();
            doStack.Clear();
            forStack.Clear();
            vars.Clear();
            var cleaned = new List<string>(lines.Length);
            bool inBlock = false;
            foreach (var rawLine in lines)
            {
                var line = rawLine;
                int pos = 0;
                var sb = new System.Text.StringBuilder();
                while (pos < line.Length)
                {
                    if (!inBlock)
                    {
                        int idxBlockStart = line.IndexOf("/*", pos, StringComparison.Ordinal);
                        int idxLineComment = line.IndexOf("//", pos, StringComparison.Ordinal);
                        if (idxLineComment >= 0 && (idxBlockStart < 0 || idxLineComment < idxBlockStart))
                        {
                            sb.Append(line.AsSpan(pos, idxLineComment - pos));
                            break;
                        }
                        if (idxBlockStart >= 0)
                        {
                            sb.Append(line.AsSpan(pos, idxBlockStart - pos));
                            pos = idxBlockStart + 2;
                            inBlock = true;
                            continue;
                        }
                        sb.Append(line.AsSpan(pos));
                        break;
                    }
                    else
                    {
                        int idxBlockEnd = line.IndexOf("*/", pos, StringComparison.Ordinal);
                        if (idxBlockEnd >= 0)
                        {
                            pos = idxBlockEnd + 2;
                            inBlock = false;
                        }
                        else
                        {
                            pos = line.Length;
                            break;
                        }
                    }
                }
                cleaned.Add(sb.ToString().Trim());
            }
            lines = cleaned.ToArray();
            // First pass: scan labels
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                var labelEnd = line.IndexOf(':');
                if (labelEnd > 0)
                {
                    var label = line.Substring(0, labelEnd).Trim();
                    if (!labels.ContainsKey(label)) labels[label] = i;
                }
            }

            int ip = 0;
            while (ip < lines.Length)
            {
                var raw = lines[ip].Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("//")) { ip++; continue; }
                
                var line = raw;
                var labelEnd = line.IndexOf(':');
                if (labelEnd > 0) line = line.Substring(labelEnd + 1).Trim();
                
                if (string.IsNullOrEmpty(line)) { ip++; continue; }
                
                var tokens = Tokenize(line);
                var nextIp = Execute(tokens, ip);
                
                if (nextIp.HasValue) ip = nextIp.Value; else ip++;
            }
        }

        int? Execute(string[] t, int ip)
        {
            if (t.Length == 0) return null;

            if (t[0].Equals("MOVE", StringComparison.OrdinalIgnoreCase) && t.Length >= 4 && t[1].Equals("TO", StringComparison.OrdinalIgnoreCase))
            {
                if (!window.GetClientRect(out var rect)) { Console.WriteLine("无法获取窗口坐标"); return null; }
                int x = (int)Eval(t[2]);
                int y = (int)Eval(t[3]);
                Console.WriteLine($"[DEBUG] MOVE TO {x},{y}");
                input.MoveScreen(rect.X + x, rect.Y + y);
                return null;
            }
            
            if (t[0].Equals("LEFTCLICK", StringComparison.OrdinalIgnoreCase))
            {
                if (!window.GetClientRect(out var rect)) return null;
                int n = t.Length >= 2 ? (int)Eval(t[1]) : 1;
                Console.WriteLine($"[DEBUG] LEFTCLICK {n}");
                for (int i = 0; i < n; i++)
                {
                    POINT p; GetCursorPos(out p);
                    input.ClickClient(window.HWnd, p.X - rect.X, p.Y - rect.Y);
                    Thread.Sleep(50);
                }
                return null;
            }
            
            if (t[0].Equals("RIGHTCLICK", StringComparison.OrdinalIgnoreCase))
            {
                if (!window.GetClientRect(out var rect)) return null;
                int n = t.Length >= 2 ? (int)Eval(t[1]) : 1;
                Console.WriteLine($"[DEBUG] RIGHTCLICK {n}");
                for (int i = 0; i < n; i++)
                {
                    POINT p; GetCursorPos(out p);
                    input.RightClickClient(window.HWnd, p.X - rect.X, p.Y - rect.Y);
                    Thread.Sleep(50);
                }
                return null;
            }
            
            if ((t[0].Equals("PRESS", StringComparison.OrdinalIgnoreCase) || t[0].Equals("KEYPRESS", StringComparison.OrdinalIgnoreCase)) && t.Length >= 2)
            {
                var key = t[1].Trim('"');
                Console.WriteLine($"[DEBUG] PRESS {key}");
                if (key.Contains('+'))
                {
                    if (TryChord(key, out var mods, out var main))
                    {
                        input.KeyChord(mods, main);
                    }
                }
                else
                {
                    if (TryKey(key, out var vk)) input.KeyPress(vk);
                }
                return null;
            }
            if (t[0].Equals("KEYDOWN", StringComparison.OrdinalIgnoreCase) && t.Length >= 2)
            {
                var key = t[1].Trim('"');
                Console.WriteLine($"[DEBUG] KEYDOWN {key}");
                if (TryKey(key, out var vk)) input.KeyDown(vk);
                return null;
            }
            if (t[0].Equals("KEYUP", StringComparison.OrdinalIgnoreCase) && t.Length >= 2)
            {
                var key = t[1].Trim('"');
                Console.WriteLine($"[DEBUG] KEYUP {key}");
                if (TryKey(key, out var vk)) input.KeyUp(vk);
                return null;
            }
            if (t[0].Equals("PRINT", StringComparison.OrdinalIgnoreCase) && t.Length >= 2)
            {
                if (t.Length == 2)
                {
                    var s = t[1];
                    if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
                    {
                        Console.WriteLine(s.Substring(1, s.Length - 2));
                    }
                    else
                    {
                        Console.WriteLine(Eval(s).ToString(CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    var parts = new List<string>();
                    for (int i = 1; i < t.Length; i++)
                    {
                        var s = t[i];
                        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
                            parts.Add(s.Substring(1, s.Length - 2));
                        else
                            parts.Add(Eval(s).ToString(CultureInfo.InvariantCulture));
                    }
                    Console.WriteLine(string.Join("", parts));
                }
                return null;
            }
            
            if (t[0].Equals("DELAY", StringComparison.OrdinalIgnoreCase) && t.Length >= 2)
            {
                int ms = (int)Eval(t[1]);
                Console.WriteLine($"[DEBUG] DELAY {ms}");
                Thread.Sleep(ms);
                return null;
            }
            
            if (t[0].Equals("FINDCOLOR", StringComparison.OrdinalIgnoreCase) && t.Length >= 7)
            {
                if (!window.GetClientRect(out var rect)) return null;
                int x1 = (int)Eval(t[1]);
                int y1 = (int)Eval(t[2]);
                int x2 = (int)Eval(t[3]);
                int y2 = (int)Eval(t[4]);
                var hex = t[5].Trim('"', '#');
                double sim = double.Parse(t[6], CultureInfo.InvariantCulture);
                
                var color = ParseHex(hex);
                var img = capture.Capture(rect);
                var found = FindColor(img, x1, y1, x2, y2, color, sim, out var fx, out var fy);
                
                vars["X"] = fx;
                vars["Y"] = fy;
                vars["FOUND"] = found ? 1 : 0;
                
                Console.WriteLine($"[DEBUG] FINDCOLOR {hex}: Found={found} ({fx},{fy})");
                return null;
            }
            
            if (t[0].Equals("FINDPIC", StringComparison.OrdinalIgnoreCase) && t.Length >= 7)
            {
                if (!window.GetClientRect(out var rect)) return null;
                int x1 = (int)Eval(t[1]);
                int y1 = (int)Eval(t[2]);
                int x2 = (int)Eval(t[3]);
                int y2 = (int)Eval(t[4]);
                var path = t[5].Trim('"');
                double sim = double.Parse(t[6], CultureInfo.InvariantCulture);
                string outX = "X", outY = "Y";
                if (t.Length >= 9)
                {
                    int idx = 7;
                    if (t[idx].Equals("OUT", StringComparison.OrdinalIgnoreCase) || t[idx].Equals("输出", StringComparison.OrdinalIgnoreCase))
                    {
                        if (idx + 1 < t.Length) { outX = t[idx + 1]; idx += 2; }
                    }
                    else
                    {
                        outX = t[idx]; idx += 1;
                    }
                    if (idx < t.Length)
                    {
                        if (t[idx].Equals("OUT", StringComparison.OrdinalIgnoreCase) || t[idx].Equals("输出", StringComparison.OrdinalIgnoreCase))
                        {
                            if (idx + 1 < t.Length) outY = t[idx + 1];
                        }
                        else
                        {
                            outY = t[idx];
                        }
                    }
                }
                if (!BmpLoader.LoadBmp(path, out var tpl))
                {
                    Console.WriteLine($"[DEBUG] FINDPIC 加载失败: {path}");
                    return null;
                }
                
                var img = capture.Capture(rect);
                var search = new RectI(x1, y1, x2 - x1, y2 - y1);
                Console.WriteLine($"[DEBUG] FINDPIC searching area=({x1},{y1})-({x2},{y2}) tpl={tpl.Width}x{tpl.Height} thr={sim.ToString(CultureInfo.InvariantCulture)}");
                
                var ok = matcher.Find(img, tpl, search, sim, out var hit, out var best);
                if (ok)
                {
                    vars[outX] = hit.X;
                    vars[outY] = hit.Y;
                    vars["FOUND"] = 1;
                    Console.WriteLine($"[DEBUG] FINDPIC {Path.GetFileName(path)}: Found ({hit.X},{hit.Y}) score={best.ToString(CultureInfo.InvariantCulture)} -> {outX},{outY}");
                }
                else
                {
                    vars["FOUND"] = 0;
                    Console.WriteLine($"[DEBUG] FINDPIC {Path.GetFileName(path)}: Not Found best={best.ToString(CultureInfo.InvariantCulture)}");
                }
                return null;
            }

            if (t[0].Equals("FIND3COLOR", StringComparison.OrdinalIgnoreCase) && t.Length >= 15)
            {
                if (!window.GetClientRect(out var rect)) return null;
                int x1 = (int)Eval(t[1]);
                int y1 = (int)Eval(t[2]);
                int x2 = (int)Eval(t[3]);
                int y2 = (int)Eval(t[4]);

                int dx1 = (int)Eval(t[5]);
                int dy1 = (int)Eval(t[6]);
                var c1 = ParseHex(t[7].Trim('"', '#'));

                int dx2 = (int)Eval(t[8]);
                int dy2 = (int)Eval(t[9]);
                var c2 = ParseHex(t[10].Trim('"', '#'));

                int dx3 = (int)Eval(t[11]);
                int dy3 = (int)Eval(t[12]);
                var c3 = ParseHex(t[13].Trim('"', '#'));

                double sim = double.Parse(t[14], CultureInfo.InvariantCulture);
                string outX = "X", outY = "Y";
                if (t.Length >= 17)
                {
                    int idx = 15;
                    if (t[idx].Equals("OUT", StringComparison.OrdinalIgnoreCase) || t[idx].Equals("输出", StringComparison.OrdinalIgnoreCase))
                    {
                        if (idx + 1 < t.Length) { outX = t[idx + 1]; idx += 2; }
                    }
                    else
                    {
                        outX = t[idx]; idx += 1;
                    }
                    if (idx < t.Length)
                    {
                        if (t[idx].Equals("OUT", StringComparison.OrdinalIgnoreCase) || t[idx].Equals("输出", StringComparison.OrdinalIgnoreCase))
                        {
                            if (idx + 1 < t.Length) outY = t[idx + 1];
                        }
                        else
                        {
                            outY = t[idx];
                        }
                    }
                }

                var img = capture.Capture(rect);
                var found = Find3Color(img, x1, y1, x2, y2, dx1, dy1, c1, dx2, dy2, c2, dx3, dy3, c3, sim, out var fx, out var fy);
                vars[outX] = fx;
                vars[outY] = fy;
                vars["FOUND"] = found ? 1 : 0;
                Console.WriteLine($"[DEBUG] FIND3COLOR Found={found} ({fx},{fy}) -> {outX},{outY}");
                return null;
            }
            
            if (t[0].Equals("IF", StringComparison.OrdinalIgnoreCase))
            {
                int idxThen = Array.IndexOf(t, "THEN");
                if (idxThen > 0 && idxThen + 1 < t.Length)
                {
                    bool cond = EvalCond(t.AsSpan(1, idxThen - 1).ToArray());
                    // Console.WriteLine($"[DEBUG] IF check: {cond}");
                    if (cond)
                    {
                        if (t[idxThen + 1].Equals("GOTO", StringComparison.OrdinalIgnoreCase) && idxThen + 2 < t.Length)
                        {
                            var label = t[idxThen + 2];
                            if (labels.TryGetValue(label, out var dest)) return dest;
                        }
                    }
                }
                return null;
            }
            
            if (t[0].Equals("DO", StringComparison.OrdinalIgnoreCase))
            {
                doStack.Push(ip);
                return null;
            }
            
            if (t[0].Equals("LOOP", StringComparison.OrdinalIgnoreCase))
            {
                if (t.Length >= 2 && t[1].Equals("WHILE", StringComparison.OrdinalIgnoreCase))
                {
                    bool cond = EvalCond(t.AsSpan(2).ToArray());
                    if (cond && doStack.Count > 0) return doStack.Peek();
                    if (doStack.Count > 0) doStack.Pop();
                }
                else
                {
                    if (doStack.Count > 0) return doStack.Pop();
                }
                return null;
            }
            
            if (t[0].Equals("FOR", StringComparison.OrdinalIgnoreCase) && t.Length >= 6 && t[2] == "=")
            {
                var varName = t[1];
                double start = Eval(t[3]);
                if (!t[4].Equals("TO", StringComparison.OrdinalIgnoreCase)) return null;
                double end = Eval(t[5]);
                double step = 1;
                int idxStep = Array.IndexOf(t, "STEP");
                if (idxStep > 0 && idxStep + 1 < t.Length) step = Eval(t[idxStep + 1]);
                
                vars[varName] = start;
                Console.WriteLine($"[DEBUG] FOR {varName} = {start} TO {end}");
                forStack.Push(new ForFrame { Var = varName, End = end, Step = step, StartIp = ip });
                return null;
            }
            
            if (t[0].Equals("NEXT", StringComparison.OrdinalIgnoreCase))
            {
                if (forStack.Count == 0) return null;
                var top = forStack.Peek();
                if (!vars.TryGetValue(top.Var, out var cur)) cur = 0;
                cur += top.Step;
                bool cont = top.Step > 0 ? cur <= top.End : cur >= top.End;
                
                vars[top.Var] = cur;
                // Console.WriteLine($"[DEBUG] NEXT {top.Var} = {cur}");
                
                if (cont)
                {
                    return top.StartIp;
                }
                else
                {
                    forStack.Pop();
                }
                return null;
            }

            // Assignment: i = 0 or i = i + 1
            if (t.Length >= 3 && t[1] == "=")
            {
                var varName = t[0];
                double val = 0;
                if (t.Length == 3)
                {
                    val = Eval(t[2]);
                }
                else if (t.Length == 5)
                {
                    double a = Eval(t[2]);
                    string op = t[3];
                    double b = Eval(t[4]);
                    if (op == "+") val = a + b;
                    else if (op == "-") val = a - b;
                    else if (op == "*") val = a * b;
                    else if (op == "/") val = b != 0 ? a / b : 0;
                    else val = a;
                }
                vars[varName] = val;
                Console.WriteLine($"[DEBUG] SET {varName} = {val}");
                return null;
            }
            
            // Fallback for unrecognized commands
            Console.WriteLine($"[WARNING] 未知指令或参数格式错误: {string.Join(" ", t)}");
            return null;
        }

        string[] Tokenize(string line)
        {
            var list = new List<string>();
            bool inStr = false;
            var cur = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    inStr = !inStr;
                    cur.Append(c);
                    continue;
                }
                if ((char.IsWhiteSpace(c) || c == ',') && !inStr)
                {
                    if (cur.Length > 0) { list.Add(cur.ToString()); cur.Clear(); }
                }
                else
                {
                    cur.Append(c);
                }
            }
            if (cur.Length > 0) list.Add(cur.ToString());
            return list.ToArray();
        }

        double Eval(string token)
        {
            if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            if (vars.TryGetValue(token, out var vv)) return vv;
            int idx = FindOp(token);
            if (idx > 0 && idx + 1 < token.Length)
            {
                char op = token[idx];
                var a = token.Substring(0, idx).Trim();
                var b = token.Substring(idx + 1).Trim();
                double av = Eval(a);
                double bv = Eval(b);
                return op switch
                {
                    '+' => av + bv,
                    '-' => av - bv,
                    '*' => av * bv,
                    '/' => (Math.Abs(bv) < 1e-12 ? 0 : av / bv),
                    _ => 0
                };
            }
            return 0;
        }

        int FindOp(string s)
        {
            int best = -1;
            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '+' || c == '-' || c == '*' || c == '/')
                {
                    best = i;
                    break;
                }
            }
            return best;
        }

        bool EvalCond(string[] parts)
        {
            if (parts.Length < 3) return false;
            double a = Eval(parts[0]);
            string op = parts[1];
            double b = Eval(parts[2]);
            return op switch
            {
                "==" => a == b,
                "=" => a == b,
                ">" => a > b,
                "<" => a < b,
                ">=" => a >= b,
                "<=" => a <= b,
                "!=" => a != b,
                "<>" => a != b,
                _ => false
            };
        }

        static int[] ParseHex(string hex)
        {
            if (hex.Length == 6)
            {
                var r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                var g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                var b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return new[] { r, g, b };
            }
            return new[] { 0, 0, 0 };
        }

        bool FindColor(ImageData img, int x1, int y1, int x2, int y2, int[] rgb, double sim, out int fx, out int fy)
        {
            fx = 0; fy = 0;
            int xmin = Math.Max(0, Math.Min(x1, x2));
            int xmax = Math.Min(img.Width - 1, Math.Max(x1, x2));
            int ymin = Math.Max(0, Math.Min(y1, y2));
            int ymax = Math.Min(img.Height - 1, Math.Max(y1, y2));
            double thr = 1.0 - sim;
            for (int y = ymin; y <= ymax; y++)
            {
                var o = y * img.Stride;
                for (int x = xmin; x <= xmax; x++)
                {
                    var b = img.Buffer[o + x * 4 + 0];
                    var g = img.Buffer[o + x * 4 + 1];
                    var r = img.Buffer[o + x * 4 + 2];
                    double dr = Math.Abs(r - rgb[0]) / 255.0;
                    double dg = Math.Abs(g - rgb[1]) / 255.0;
                    double db = Math.Abs(b - rgb[2]) / 255.0;
                    double d = Math.Max(Math.Max(dr, dg), db);
                    if (d <= thr)
                    {
                        fx = x; fy = y;
                        return true;
                    }
                }
            }
            return false;
        }

        bool Find3Color(ImageData img, int x1, int y1, int x2, int y2,
            int dx1, int dy1, int[] c1, int dx2, int dy2, int[] c2, int dx3, int dy3, int[] c3,
            double sim, out int fx, out int fy)
        {
            fx = 0; fy = 0;
            int xmin = Math.Max(0, Math.Min(x1, x2));
            int xmax = Math.Min(img.Width - 1, Math.Max(x1, x2));
            int ymin = Math.Max(0, Math.Min(y1, y2));
            int ymax = Math.Min(img.Height - 1, Math.Max(y1, y2));
            double thr = 1.0 - sim;

            for (int y = ymin; y <= ymax; y++)
            {
                for (int x = xmin; x <= xmax; x++)
                {
                    if (!MatchAt(img, x + dx1, y + dy1, c1, thr)) continue;
                    if (!MatchAt(img, x + dx2, y + dy2, c2, thr)) continue;
                    if (!MatchAt(img, x + dx3, y + dy3, c3, thr)) continue;
                    fx = x; fy = y;
                    return true;
                }
            }
            return false;
        }

        bool MatchAt(ImageData img, int x, int y, int[] rgb, double thr)
        {
            if (x < 0 || y < 0 || x >= img.Width || y >= img.Height) return false;
            var o = y * img.Stride + x * 4;
            var b = img.Buffer[o + 0];
            var g = img.Buffer[o + 1];
            var r = img.Buffer[o + 2];
            double dr = Math.Abs(r - rgb[0]) / 255.0;
            double dg = Math.Abs(g - rgb[1]) / 255.0;
            double db = Math.Abs(b - rgb[2]) / 255.0;
            double d = Math.Max(Math.Max(dr, dg), db);
            return d <= thr;
        }

        bool TryKey(string key, out VirtualKey vk)
        {
            key = key.Trim().ToUpperInvariant();
            switch (key)
            {
                case "ESC": vk = VirtualKey.ESCAPE; return true;
                case "ENTER": vk = VirtualKey.RETURN; return true;
                case "RETURN": vk = VirtualKey.RETURN; return true;
                case "SPACE": vk = VirtualKey.SPACE; return true;
                case "CTRL": vk = VirtualKey.LCONTROL; return true;
                case "CONTROL": vk = VirtualKey.LCONTROL; return true;
                case "SHIFT": vk = VirtualKey.LSHIFT; return true;
                case "ALT": vk = VirtualKey.MENU; return true;
                case "WIN": vk = VirtualKey.LWIN; return true;
            }
            if (Enum.TryParse<VirtualKey>(key, true, out vk)) return true;
            if (key.Length == 1)
            {
                char c = key[0];
                int v = c switch
                {
                    >= 'A' and <= 'Z' => c,
                    >= '0' and <= '9' => c,
                    _ => 0
                };
                if (v != 0) { vk = (VirtualKey)v; return true; }
            }
            return false;
        }

        bool TryChord(string spec, out VirtualKey[] modifiers, out VirtualKey main)
        {
            modifiers = Array.Empty<VirtualKey>();
            main = default;
            var parts = spec.Split('+', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;
            var mods = new List<VirtualKey>();
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (TryKey(parts[i].Trim(), out var mv)) mods.Add(mv);
                else return false;
            }
            if (!TryKey(parts[^1].Trim(), out var kv)) return false;
            modifiers = mods.ToArray();
            main = kv;
            return true;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);
        struct POINT { public int X; public int Y; }
    }
}
