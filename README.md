# 蓝键精灵（PindleRunner）- 游戏自动化脚本工具

这是一个基于 C# 开发的游戏自动化脚本框架。项目专注于高效的图像识别与稳定的窗口控制，旨在实现自动化的游戏操作逻辑。可以支持暗黑破坏神 2（Diablo 2）或者League of Legends等游戏的自动化脚本编写。

## 🚀 核心功能

### 1. 窗口定位与控制 (Window Management)
通过 `WINDOW` 指令实现对游戏窗口的精确控制，支持多环境下的窗口句柄捕获。
* **用法示例**：`WINDOW "League of Legends"` (支持动态匹配标题关键字)
* **逻辑**：自动查找目标进程，设置 `FOUND=1/0` 状态位，确保后续脚本在正确的上下文执行。

### 2. 高性能找图 (FINDPIC Logic)
项目内置了自定义的 `BmpLoader`，直接解析位图二进制数据，脱离重型库，实现毫秒级的图像比对。
* **技术细节**：
  * 支持 `.bmp` 格式模板加载（自动校验 `0x4D42` 标识）。
  * 针对复杂场景（如掉落范围大、背景变化、文字颜色固定）进行了优化。
  * **建议**：在背景干扰严重的场景下，优先提取目标特征色，利用“精简图像像素”模式提高识别率。

### 3. 热键与输入模拟
* **HotkeyService**: 提供全局热键响应（如 F9 取色、F10 局部放大、Ctrl+X 截取模板）。
* **InputSimulator**: 模拟真实的鼠标点击与键盘按键，避开部分简单的防作弊检测。

## 🛠 脚本开发参考

目前支持的指令集（持续更新中）：
- `PICK`: 移动并拾取物品。
- `MOVE TO X Y`: 移动至指定坐标。
- `DELAY [ms]`: 毫秒级延迟控制。
- `WINDOW [Title]`: 切换当前操作的目标窗口。

## 📂 项目结构
- `ScriptEngine.cs`: 脚本解析与执行核心。
- `BmpLoader.cs`: 极简 BMP 加载器，支持内存像素比对。
- `TemplateMatcher.cs`: 核心找图算法逻辑。
          


## 📘 新增指令详解（参数 + 示例）

### 1) WINDOW / TARGETWINDOW / SETWINDOW
用于切换脚本当前操作的目标窗口（通过窗口标题关键字进行模糊匹配），并尝试激活窗口。

**语法**
```
WINDOW "窗口标题关键字"
```

**参数**
- `窗口标题关键字`：会在系统所有顶层窗口标题中做“包含匹配”（不区分大小写），匹配到第一个窗口即作为目标窗口

**输出变量**
- `FOUND`：找到并激活成功为 `1`，否则为 `0`

**示例：切换到 LoL 后再执行脚本动作**
```
WINDOW "League of Legends"
IF FOUND == 0 THEN END
DELAY 300
PRESS F1
```

### 2) WATCHDOG（脚本参数变量）
用于控制“连续找不到指定颜色时自动重启脚本”的阈值。目前 watchdog 只对 `FINDCOLOR ... "#008400" ...`（绿色血条等）生效，并且是“同一条 FINDCOLOR(同区域同颜色)”连续失败计数。

**语法**
```
WATCHDOG = N
```

**参数**
- `N`：阈值（连续失败次数）
  - `N <= 0`：关闭 watchdog
  - 建议范围：`30 ~ 300`（取决于你循环 DELAY 的间隔）

**示例：每秒检查一次绿色血条，60 秒找不到就自动重启脚本**
```
WATCHDOG = 60
DO
  FINDCOLOR 0 0 1279 799 "#008400" 0.80 OUT GX OUT GY
  DELAY 1000
LOOP
```

### 3) FINDCOLOR（增强：支持 OUT X OUT Y）
在指定区域内寻找接近目标颜色的像素点。

**语法**
```
FINDCOLOR x1 y1 x2 y2 "#RRGGBB" sim OUT X OUT Y
```

**参数**
- `x1 y1 x2 y2`：搜索区域（左上/右下坐标；也支持反向输入，程序会自动归一化）
- `"#RRGGBB"`：目标颜色（F9 取色得到）
- `sim`：相似度 `0~1`，越大越严格（例如 `0.99/0.98/0.90`）
- `OUT X OUT Y`：可选，输出变量名；不写则默认输出到 `X`、`Y`

**输出变量**
- `FOUND`：找到为 `1`，否则 `0`
- `X, Y`：找到的像素坐标；找不到时为 `0,0`

**示例：找到橙色就移动过去点击拾取**
```
FINDCOLOR 200 200 1100 760 "#FFA800" 0.95 OUT IX OUT IY
IF FOUND == 1 THEN MOVE TO IX,IY
IF FOUND == 1 THEN LEFTCLICK 1
```

### 4) FINDCOLORN（新增：最小像素数过滤）
用于“区域内至少有 N 个接近目标色的像素”才算找到，可过滤掉背景里偶尔出现的单个噪点像素。

**语法**
```
FINDCOLORN x1 y1 x2 y2 "#RRGGBB" sim minCount OUT X OUT Y
```

**参数**
- `x1 y1 x2 y2`：搜索区域
- `"#RRGGBB"`：目标颜色
- `sim`：相似度
- `minCount`：至少命中多少个像素才算找到（例如 `8/15/40`）
- `OUT X OUT Y`：可选输出变量名（默认 `X,Y`）

**输出变量**
- `FOUND`：满足 `minCount` 为 `1`，否则 `0`
- `X, Y`：命中的第一个像素坐标；找不到时为 `0,0`

**示例：监测血瓶 1/2 高度附近是否还“有足够红色像素”**
```
FINDCOLORN HPX-4 HPY-4 HPX+4 HPY+4 "#8D0B0C" 0.78 8 OUT HX OUT HY
IF FOUND == 0 THEN PRESS 1
```

### 5) FINDCOLORBLOB（新增：连通块检测）
用于检测“文字/图标”这类由很多像素组成的目标。它不是找单点，而是找一团相连的像素（连通块），并返回该连通块的中心点。

**语法**
```
FINDCOLORBLOB x1 y1 x2 y2 "#RRGGBB" sim minCount maxCount OUT X OUT Y
```

**参数**
- `x1 y1 x2 y2`：搜索区域
- `"#RRGGBB"`：目标颜色
- `sim`：相似度（建议从 `0.99` 开始，不行再降到 `0.985/0.98`）
- `minCount`：连通块最少像素数（过滤噪点；例如 `15~40`）
- `maxCount`：连通块最大像素数（过滤 UI 大面积同色区域；例如 `300~2000`）
- `OUT X OUT Y`：可选输出变量名（默认 `X,Y`）

**输出变量**
- `FOUND`：找到符合 min/max 的连通块为 `1`，否则 `0`
- `X, Y`：连通块中心点坐标；找不到时为 `0,0`

**示例：检测到“精”字就直接去拾取**
```
FINDCOLORBLOB 200 200 1100 760 "#C7B377" 0.985 25 1200 OUT JX OUT JY
IF FOUND == 1 THEN MOVE TO JX,JY
IF FOUND == 1 THEN LEFTCLICK 1
```

### 6) FINDPIC（增强：支持 OUT X OUT Y）
在指定区域内寻找模板图（bmp），并返回命中坐标。

**语法**
```
FINDPIC x1 y1 x2 y2 "template.bmp" sim OUT X OUT Y
```

**参数**
- `x1 y1 x2 y2`：搜索区域
- `"template.bmp"`：模板路径（相对/绝对均可；通常放在程序目录）
- `sim`：相似度阈值（例如 `0.75/0.85/0.90`）
- `OUT X OUT Y`：可选输出变量名（默认 `X,Y`）

**输出变量**
- `FOUND`：找到为 `1`，否则 `0`
- `X, Y`：命中位置坐标（通常是模板左上角）

**示例：最多尝试 3 次找图，找到就提前退出循环**
```
FOUND_TEMPLATE = 0
FOR TRY = 1 TO 3
  FINDPIC 0 400 500 800 "template.bmp" 0.75 OUT TX OUT TY
  IF FOUND == 1 THEN GOTO TEMPLATE_HIT
  DELAY 500
  GOTO TEMPLATE_NEXT

TEMPLATE_HIT:
  FOUND_TEMPLATE = 1
  EXIT

TEMPLATE_NEXT:
NEXT
```

### 7) KEYDOWN / KEYUP（新增：按住与抬起）
用于模拟“按住某键不放”与“松开某键”，常用于原地攻击（SHIFT）、强制显示物品（ALT）、组合键等。

**语法**
```
KEYDOWN "KEY"
KEYUP "KEY"
```

**参数**
- `"KEY"`：支持 `CTRL/SHIFT/ALT/WIN`、字母数字、`F1~F12`、方向键等

**示例：按住 SHIFT 原地攻击 10 次**
```
KEYDOWN "SHIFT"
LEFTCLICK 10
KEYUP "SHIFT"
```

### 8) STOP / END / EXITSCRIPT（新增：停止脚本）
立即停止脚本执行。

**语法**
```
STOP
END
EXITSCRIPT
```

**示例：找不到目标就直接结束**
```
FINDCOLOR 0 0 1279 799 "#FFA800" 0.95
IF FOUND == 0 THEN END
```

### 9) EXIT / EXITFOR（新增：退出 FOR 循环）
用于在 `FOR ... NEXT` 循环内部提前退出当前循环。

**语法**
```
EXIT
EXITFOR
```

**示例：只要找到一次就退出循环**
```
FOR I = 1 TO 10
  FINDCOLOR 200 200 1100 760 "#FFA800" 0.95
  IF FOUND == 1 THEN EXIT
  DELAY 100
NEXT
```

### 10) TGMSG / TGSHOT（新增：Telegram 发消息与截屏）
用于通过 Telegram Bot 给指定账号（chat_id）发送文本消息或当前目标窗口截图。

**脚本内配置（推荐）**
在脚本文件开头写两行（这是脚本语法，不是 PowerShell；只影响脚本运行时配置）：
```vb
$env:TELEGRAM_BOT_TOKEN = "123456789:AAE..."
$env:TELEGRAM_CHAT_ID = "123456789"
```

也支持不写 `$env:` 前缀：
```vb
TELEGRAM_BOT_TOKEN = "123456789:AAE..."
TELEGRAM_CHAT_ID = "123456789"
```

**语法**
```
TGMSG "要发送的文本"
TGSHOT
TGSHOT "截图说明文字(可选)"
TGSHOT x1 y1 x2 y2 "截图说明文字(可选)"
```

**可选：在指令里直接传 token/chat_id（不推荐写进脚本仓库）**
```
TGMSG "123456:ABC..." "123456789" "hello"
TGSHOT "123456:ABC..." "123456789"
TGSHOT "123456:ABC..." "123456789" 0 0 1279 799 "full window"
```

**输出变量**
- `FOUND`：发送成功为 `1`，失败为 `0`

**示例：找到目标颜色就发消息 + 截屏**
```
FINDCOLOR 0 0 1279 799 "#FFA800" 0.95
IF FOUND == 1 THEN TGMSG "检测到橙色物品"
IF FOUND == 1 THEN TGSHOT "当前画面"
```

## 🤖 Telegram Bot 创建与绑定教程（详细）

### 1) 创建 Bot 并拿到 Token
1. 打开 Telegram，搜索并进入 `@BotFather`
2. 发送 `/newbot`
3. 按提示输入：
   - Bot 显示名（随意）
   - Bot 用户名（必须以 `bot` 结尾，例如 `pindle_runner_bot`）
4. BotFather 会返回一串 token，形如：
   - `123456789:AAE...`  
   这就是 `TELEGRAM_BOT_TOKEN`

### 2) 获取你的 Bot 链接（让账号先“启动”Bot）
Bot 的公开链接就是：
- `https://t.me/<你的bot用户名>`

注意：Bot 不能主动给“从未和它聊过天”的账号发第一条消息。你需要先点开链接并对 Bot 发一次 `/start`（或随便发一句话）。

### 3) 获取 chat_id（最推荐的方法：getUpdates）
1. 用你的账号打开刚创建的 Bot，对它发一条消息（例如 `/start` 或 `hi`）
2. 在浏览器打开下面这个链接（把 `<TOKEN>` 换成你的 token）：
   - `https://api.telegram.org/bot<TOKEN>/getUpdates`
3. 在返回的 JSON 里找到：
   - `message` -> `chat` -> `id`
4. 这个 `id` 就是 `TELEGRAM_CHAT_ID`

补充说明：
- 个人聊天：`chat.id` 通常是正整数
- 群/超级群：`chat.id` 可能是负数（超级群经常是 `-100...`）
- 频道：需要让 Bot 具备向频道发消息的权限，chat_id 也可能是 `-100...`（或使用 `@channel_username`，但仍建议用 id）

### 4) 在 Windows 配置环境变量（PowerShell）
只对当前 PowerShell 窗口生效：
```powershell
$env:TELEGRAM_BOT_TOKEN = "123456789:AAE..."
$env:TELEGRAM_CHAT_ID = "123456789"
```

永久写入用户环境变量（写入后需要重启 PindleRunner 才会读到）：
```powershell
setx TELEGRAM_BOT_TOKEN "123456789:AAE..."
setx TELEGRAM_CHAT_ID "123456789"
```

### 5) 常见问题排查
- `chat not found`：通常是 chat_id 不对，或你还没给 Bot 发过消息（没 /start）
- 群里收不到：把 Bot 拉进群后先发几条消息，再用 `getUpdates` 重新取一次群的 chat_id
- Token 泄漏：立刻去 BotFather 执行 `/revoke`（或重新生成 token），不要把 token 提交到代码仓库
