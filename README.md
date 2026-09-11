# 口琴简谱播放器 0.1 — 源码测试版

Windows 10/11 x64，C# + WPF，.NET 8 SDK。独立实现，未复制参考仓库源码。
本交付环境没有 .NET SDK：未编译、未运行 Windows GUI、未进行游戏实测，不含已验证的 EXE。

## 创建与运行

1. 安装 Windows x64 的 .NET 8 **SDK**，不是仅安装 Runtime：
   https://dotnet.microsoft.com/en-us/download/dotnet/8.0
2. 解压源码，例如放到 `D:\Projects\HarmonicaPlayer`。打开 PowerShell：

```powershell
cd D:\Projects\HarmonicaPlayer
dotnet --list-sdks
dotnet run --project .\Tests\ParserTests.csproj
dotnet run --project .\HarmonicaPlayer.csproj
```

项目已包含所需文件，不必执行 `dotnet new`，不需要 Visual Studio；VS Code可用于编辑。
如手动创建项目，新建文件夹并按源码包中的文件名保存全部文件即可。
测试项目不发送任何输入，只测试解析器；不替代Windows输入和时序测试。

## 发布可双击的EXE

```powershell
dotnet publish .\HarmonicaPlayer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -o .\publish
.\publish\HarmonicaPlayer.exe
```

EXE包含运行库，较大是正常现象。首次还原/发布需要网络。不要启用WPF不适用的裁剪。
发布说明：https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview

## 使用

1. 导入UTF-8 TXT或直接编辑文本框，删除标题、歌词、速度说明等非谱面文字。
2. 默认勾选“仅日志测试”，先开始检查解析和进度。它不发声，也不发送真实输入。
3. 总时长默认300ms，其中末尾20ms留白；休止0占完整300ms。
4. 确认使用场景允许自动输入后，取消仅日志测试，点击开始或按F6。
5. 确认提示后3秒内切回已装备口琴的目标窗口，保持前台。F8随时停止。
6. 点击其他窗口会自动停止；重新开始从头演奏。不支持暂停、跳转。

真实模式会锁定倒计时结束时的外部前台窗口，**不会识别它是不是游戏**。
务必自己切到正确窗口，不要把聊天软件、终端作为目标。同一游戏窗口内打开聊天框/菜单也无法识别。
不自动申请管理员权限、不绕过输入限制；若SendInput失败会停止。
SendInput成功只说明系统接收了事件，不保证游戏识别。
普通热键注册可能被占用，注册失败时禁止真实演奏；关掉冲突程序再重启。

## 语法

| 写法 | 含义 |
|---|---|
| 1~7 | 普通音 |
| 【123】 | 高音范围 |
| （123）或(123) | 低音范围 |
| #6 | 下一个音升半音 |
| 【#6】 | 高音6升半音 |
| 0 | 休止 |
| 空格、换行、竖线 | 排版，不改变时值 |

不支持括号嵌套、延音线、附点、三连音、8作为高音1或自动节奏推断。
非法字符会阻止演奏并显示字符位置。#跨空格作用于下一音，但不可跨音区括号。
键位：普通1~7=Z X C V B N M，高1=逗号，高2~7=右键加对应键，低音=左键加对应键，升半音=额外中键。
高1直接使用逗号，其他高音使用右键；需在你的游戏版本中验证。

## 最小验收

- 用test-score.txt验证普通、高低音、半音、111重复音、0休止。
- 测试倒计时取消、长音中F8停止、鼠标修饰键中途停止、切出窗口、关闭窗口。
- 日志和解析器测试通过并不代表游戏测试通过。
- 严重调度超时会停止，避免批量补发；默认80~5000ms每音，留白至少10ms。
- 只释放本程序尝试按下的键；最终释放失败会提示手动按下并松开相关键。
- 工作线程退出后才能再次启动；UI阻塞、强杀、系统崩溃时不能保证立即停止或清理。
- 不应因其他项目能运行就认定游戏允许自动输入；被限制时停止，不绕过反作弊。

## 文件

- Program.cs：WPF界面、快捷键、倒计时、播放任务、焦点保护。
- ScoreParser.cs：解析器。
- NativeInput.cs：扫描码/鼠标输出和释放。
- Tests：无第三方测试包的解析自测。

后续再增加可配置符号、真实声音预览、曲库和播放列表；0.1不含MIDI。
