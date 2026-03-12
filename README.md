# PindleRunner - Game Automation Tool

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
          
已支持按住+抬起模式，并且脚本可以组合使用。

**新增指令**
- KEYDOWN "CTRL"
- KEYUP "CTRL"
- 可以和任何支持的键一起用：CTRL/SHIFT/ALT/WIN/字母数字/F1–F12/方向键等

### 5.2 按住 CTRL 拖拽后抬起
```
KEYDOWN "CTRL"
MOVE TO 500,400
LEFTCLICK 1
DELAY 300
MOVE TO 700,450
LEFTCLICK 1
KEYUP "CTRL"
```
