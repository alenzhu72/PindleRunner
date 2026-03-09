# PindleRunner 脚本指令说明（中文）

脚本文件默认放在程序目录下：`script.txt`，按热键 `Ctrl+Shift+C` 运行。

## 1. 基本语法

### 1.1 坐标系
- 脚本里的坐标 `X/Y` 默认都是游戏窗口客户区左上角为 `(0,0)` 的相对坐标。
- 程序内部会自动加上窗口在屏幕上的偏移。

### 1.2 分隔与大小写
- 指令不区分大小写：`move` / `MOVE` 都可以。
- 参数可以用空格或逗号分隔，例如：`MOVE TO 100,200` 或 `MOVE TO 100 200`。

### 1.3 字符串
- 字符串必须用英文双引号包起来：`"template.bmp"`、`"#00FF00"`、`"CTRL+S"`。

### 1.4 注释
- 单行注释：`// ...`
- 块注释：`/* ... */`（可跨多行）

### 1.5 标签与跳转
- 标签写法：`labelName:`
- 同一行可以写“标签 + 指令”：`start: MOVE TO 10,10`
- 跳转使用：`IF ... THEN GOTO labelName`

## 2. 变量与表达式

### 2.1 变量类型
- 变量是数字（内部用 `double` 存储）。
- 未定义的变量按 0 处理。

### 2.2 内置变量
- `FOUND`：上一次 `FIND*` 是否找到（找到为 1，否则为 0）
- `X`、`Y`：上一次 `FIND*` 返回坐标（或你指定的输出变量）

### 2.3 赋值
支持两种形式：
- `i = 1`
- `i = i + 1`（也支持 `-` / `*` / `/`）

### 2.4 表达式（在指令参数里）
- 支持“单个运算符”的表达式写在同一个 token 里，例如：`X+100`、`Y-20`。
- 注意：`MOVE TO X + 100, Y` 这种带空格的写法会被拆成多个 token，可能导致参数不匹配；建议写成 `MOVE TO X+100, Y`，或先用赋值算出结果再用。

### 2.5 条件表达式
- 条件只支持三段：`A OP B`
- OP 支持：`==`、`=`、`>`、`<`、`>=`、`<=`、`!=`、`<>`
- 示例：`IF FOUND == 1 THEN GOTO ok`

## 3. 指令列表

### 3.1 鼠标

#### MOVE TO
```
MOVE TO x, y
```
- `x, y`：目标坐标（客户区相对坐标），可用变量/表达式（如 `X+10`）。

#### LEFTCLICK / RIGHTCLICK
```
LEFTCLICK [n]
RIGHTCLICK [n]
```
- `n`：点击次数（可选，默认 1）
- 点击位置：当前鼠标所在位置。

### 3.2 键盘

#### PRESS / KEYPRESS
```
PRESS "KEY"
KEYPRESS "KEY"
```
- 两者等价。
- `KEY` 支持单键：`"ESC"`、`"F1"`、`"A"`、`"1"` 等
- 支持组合键：`"CTRL+S"`、`"ALT+F4"`、`"CTRL+SHIFT+ESC"`、`"WIN+R"`
- 常用别名：`ESC`、`ENTER/RETURN`、`SPACE`、`CTRL/CONTROL`、`SHIFT`、`ALT`、`WIN`

#### KEYDOWN / KEYUP（按住与抬起）
```
KEYDOWN "KEY"
KEYUP "KEY"
```
- 用于“按住+延时+抬起”的场景（例如按住 CTRL 拖拽）

### 3.3 延时与输出

#### DELAY
```
DELAY ms
```
- `ms`：毫秒数

#### PRINT
```
PRINT "文本"
PRINT 变量
PRINT "前缀", 变量, "后缀"
```
- 多参数会拼接输出。

### 3.4 找色 / 找图

#### FINDCOLOR（单点/区域找色）
```
FINDCOLOR x1, y1, x2, y2, "#RRGGBB", sim
```
- `x1,y1,x2,y2`：搜索矩形（客户区相对坐标）。如果 `x1==x2` 且 `y1==y2`，就是检测单点。
- `"#RRGGBB"`：目标颜色（可带或不带 `#`）
- `sim`：相似度（0~1），越接近 1 越严格。
- 输出：`FOUND`、`X`、`Y`

#### FIND3COLOR（三点找色）
```
FIND3COLOR x1,y1,x2,y2, dx1,dy1,"#RRGGBB", dx2,dy2,"#RRGGBB", dx3,dy3,"#RRGGBB", sim [OUT outX] [OUT outY]
```
- 在搜索矩形内枚举一个“锚点 (x,y)”，要求锚点附近三个相对点颜色同时满足：
  - `(x+dx1, y+dy1)` 匹配 color1
  - `(x+dx2, y+dy2)` 匹配 color2
  - `(x+dx3, y+dy3)` 匹配 color3
- `sim`：相似度（0~1），越接近 1 越严格。
- 输出：`FOUND`，默认写入 `X`,`Y`；也可用 `OUT outX outY` 写到自定义变量（也支持中文关键字 `输出`）。

#### FINDPIC（找图）
```
FINDPIC x1,y1,x2,y2,"path.bmp", sim [OUT outX] [OUT outY]
```
- `x1,y1,x2,y2`：搜索矩形（客户区相对坐标）
- `"path.bmp"`：模板图片路径（相对路径会以程序目录为基准）
- `sim`：相似度阈值（0~1），越接近 1 越严格
- 输出：`FOUND`，默认写入 `X`,`Y`（命中点为模板左上角坐标）；也可用 `OUT` 指定变量名（也支持中文关键字 `输出`）。

## 4. 流程控制

### 4.1 IF ... THEN GOTO
```
IF A OP B THEN GOTO label
```
- 目前仅支持 THEN 后面跟 `GOTO label`。

### 4.2 DO / LOOP
```
DO
  ...
LOOP
```
- 无条件循环回到对应的 `DO`。

### 4.3 DO / LOOP WHILE
```
DO
  ...
LOOP WHILE A OP B
```
- 条件为真则继续循环，否则退出循环。

### 4.4 FOR / NEXT
```
FOR i = start TO end [STEP step]
  ...
NEXT
```
- `STEP` 默认 1。

## 5. 常用示例

### 5.1 等待某点变为绿色
```
DO
  FINDCOLOR 640,360,640,360,"#00FF00",0.99
  IF FOUND == 1 THEN GOTO ok
  DELAY 200
LOOP

ok:
PRINT "已变为绿色"
```

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
