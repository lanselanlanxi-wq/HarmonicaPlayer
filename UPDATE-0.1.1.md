# 0.1.1替换说明

关闭正在运行的播放器，把本包中的文件复制到原HarmonicaPlayer文件夹，覆盖同名文件：

- Program.cs
- ScoreParser.cs
- Tests/Program.cs（注意是在Tests子文件夹内）

NativeInput.cs和两个csproj文件无需替换。

在项目目录执行：

```powershell
dotnet run --project .\Tests\ParserTests.csproj
dotnet run --project .\HarmonicaPlayer.csproj
```

预期测试输出：PASS: 29 tests。本交付环境无.NET SDK，未执行C#编译或测试；请在本机执行。

新增空格额外停顿100ms、换行额外停顿300ms，可分别设为0关闭，最大10000ms。
每音时长仍包含音间留白；新的分组停顿是在其基础上额外增加的静音。
多个空格/Tab/全角空格合并成一次；CRLF计一次；连续空行合并一次。
两个音符之间同时包含空格和换行时，只按换行处理。即使换行设0也不回退到空格停顿。
首尾空白不增加时间。括号内空格生效，跨括号的空白也按同一组分隔处理。
0仍占一个完整音符时长，其前后的空格或换行仍按设置增加停顿。
程序文本框自动折行只是视觉排版，不产生停顿；必须按Enter或TXT中有实际换行。
播放期间设置不可修改。初次先保持“仅日志测试”，在日志中检查开始时间。

示例：每音300ms，空格100ms，换行300ms：

```text
12 3
4
```

开始时间应依次为0、300、700、1300ms，总时长1600ms。

想只按乐句停顿：空格设0，换行设300，然后逐行放入UP主原谱。
这些是手动调试参数，不代表自动还原原曲节奏。

如需更新原来的EXE，重新发布：

```powershell
dotnet publish .\HarmonicaPlayer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -o .\publish
```
