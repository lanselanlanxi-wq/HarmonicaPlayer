# 0.1.3 覆盖更新与GitHub发布

## 覆盖更新

以本对话提供的0.1.2为基础。此包是源码，不是编译好的EXE。
关闭旧播放器，将ZIP内容直接解压至 `D:\Downloads\HarmonicaPlayer`（HarmonicaPlayer.csproj所在目录），覆盖同名文件。必须把新增的所有CS文件也放进去，不能只覆盖Program.cs。

本包包含更新后的README、项目版本、Program.cs，以及新增的HotkeySettings.cs、NativeHotkeys.cs、HotkeyDialog.cs、AppSettings.cs、SingleInstance.cs、构建脚本和测试。ScoreParser.cs/NativeInput.cs包含用于保持完整兼容的现有实现。
不包含.git目录，不会更改远程仓库设置；不包含用户设置和《死别》谱子，也不会删除已有文件。

若你自行修改过这些源码或README，请先备份并比较。本次无法读取远程GitHub文件，因此未与远端后续改动进行合并。

## 编译

在项目目录PowerShell执行：

```powershell
dotnet run --project .\Tests\ParserTests.csproj
dotnet publish .\HarmonicaPlayer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\bin\Release\publish-v0.1.3
explorer .\bin\Release\publish-v0.1.3
```

遇到错误即停止并处理，不能把旧EXE当新版本发布。窗口标题应为0.1.3。
`Build-Release.ps1`提供测试、编译、打ZIP的一次执行版本；会使用新的时间戳输出目录，避免混入旧构建文件，并检查每一步退出码。脚本生成的ZIP位于bin/Release，已被原.gitignore的bin/规则排除。

## Windows验收

1. 默认F6/F8可用；仅日志模式导入rhythm-demo.txt，节奏模式120 BPM，预览16拍/8秒。
2. 改开始键为Ctrl+F6、停止键为Ctrl+Shift+F8；应用后显示新名称，旧快捷键不再触发。
3. 保存同样的开始/停止组合、F12、演奏键等应提示不能保存；取消编辑后原快捷键恢复。
4. 用另一个工具占用开始键并重试：停止键应保持可用，开始按钮可用。占用停止键则阻止真实演奏。
5. 解除占用后点重试，不重启就能恢复。若占用者是0.1.2旧程序，先关闭旧程序。
6. 长按开始组合键不应重复启动；松开前不倒计时/不输出。用Alt+Tab切窗口后，松开修饰键才演奏。
7. 倒计时、等待松键及长音播放中按停止，均应取消且释放程序按下的键。切出目标窗口、关闭播放器也应停止。
8. 修改BPM、留白等，退出重开应保留；“仅日志测试”每次重新开启。
9. 再次启动0.1.3不会创建第二个实例，尝试激活已有窗口；若正在演奏，切回播放器会触发原有失焦停止。
10. 至少实际演奏一段，确认没有粘键，再公开发布。

## 更新GitHub源码

不需要再次git init，也不需要再次设置origin。

先检查实际变化：

```powershell
cd D:\Downloads\HarmonicaPlayer
git status --short
```

确认覆盖内容正确后，提交这次源码及说明：

```powershell
git add Program.cs ScoreParser.cs NativeInput.cs HarmonicaPlayer.csproj HotkeySettings.cs NativeHotkeys.cs HotkeyDialog.cs AppSettings.cs SingleInstance.cs Tests/Program.cs Tests/ParserTests.csproj README.md UPDATE-0.1.3.md Build-Release.ps1 rhythm-demo.txt
git diff --cached --stat
git commit -m "Release v0.1.3: configurable hotkeys and saved settings"
git push origin main
```

如推送被拒绝，保留本地改动并检查提示，不要使用强制推送。

## 发布0.1.3

1. 将新生成的publish目录内全部文件压缩为HarmonicaPlayer-v0.1.3-win-x64.zip，或使用构建脚本生成的ZIP。
2. 打开 https://github.com/lanselanlanxi-wq/HarmonicaPlayer/releases/new
3. 新建标签v0.1.3，Target选main，标题HarmonicaPlayer v0.1.3。
4. 上传新的Windows程序ZIP，测试完成后Publish release。保留旧的v0.1.2发布，不覆盖旧标签。
5. 上传Git源码不会自动替换Releases附件，源码推送和程序发布是两个步骤。

建议发布说明：

- 支持自定义开始/停止快捷键及Ctrl、Alt、Shift组合键。
- 分别显示快捷键占用状态，支持不重启重试注册。
- 自动保存快捷键、速度、节奏模式和停顿设置。
- 防止0.1.3重复运行占用热键。
- 开始键冲突仍可点击开始；停止键不可用时禁止真实演奏。

## 本包验证状态

已使用.NET SDK 8.0.425完成：

- 75项自动测试全部通过（原有解析、节奏、快捷键状态机、设置读写）。
- Windows WPF项目Release编译通过，0警告、0错误。
- 在Linux环境交叉编译，未执行Windows GUI、全局热键、窗口激活或游戏实测；这些仍须在你的Windows电脑按上述步骤验收。
