```


**新增指令**
- KEYDOWN "CTRL"
- KEYUP "CTRL"
- 可以和任何支持的键一起用：CTRL/SHIFT/ALT/WIN/字母数字/F1–F12/方向键等

**示例：按住 CTRL 拖拽后抬起**
```
KEYDOWN "CTRL"
MOVE TO 500,400
LEFTCLICK 1
DELAY 300
MOVE TO 700,450
LEFTCLICK 1
KEYUP "CTRL"
```

**示例：组合键+按住**
```
KEYPRESS "CTRL+S"      // 组合键发送
KEYDOWN "SHIFT"        // 再按住
DELAY 200
KEYUP "SHIFT"
```

实现说明：
- 底层按住/抬起在 [InputSimulator.cs](file:///c:/Users/AlenZhu/Documents/trae_projects/diablo1/PindleRunner/InputSimulator.cs) 新增 KeyDown/KeyUp
- 脚本解析在 [ScriptEngine.cs](file:///c:/Users/AlenZhu/Documents/trae_projects/diablo1/PindleRunner/ScriptEngine.cs) 增加了 KEYDOWN/KEYUP 分支；KEYPRESS 也支持 "CTRL+S" 这类组合

如果你还需要“鼠标按住拖拽”（例如按住左键移动再抬起），我也可以加 MOUSEDOWN/MOUSEUP 指令（LEFT/RIGHT 选择）来做连续拖拽。
