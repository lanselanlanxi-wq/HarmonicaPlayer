# 0.1.5 游戏内启动与说明更新

## 修改内容

- 移除实际演奏前的确认对话框，避免游戏内按开始键后被后台弹窗阻塞。松开快捷键后直接倒计时3秒。
- 将实际演奏、游戏前台和勿碰键鼠提示放到主界面。
- 保留每次启动默认“仅日志测试”、停止热键不可用时禁止真实输出、切出目标窗口停止等现有行为。
- 使用说明.txt只收录使用步骤、两个模式的解释和常见问题。
- README同步使用方法及常见问题，并完整收录可复制给其他AI的转谱提示词。
- 转谱说明补充双高音/双低音：优先统一移调，必要时将超出范围的音按八度折回，并注明改动，不删除音符。
- 使用说明与README记录管理员身份启动、无边框模式、演奏期间除停止键外不要操作键鼠等使用建议。
- 长弓溪谷移动船只上的按键失效列为已反馈问题，原因待确认，本版不宣称修复。
- Build-Release.ps1自动将使用说明.txt放进发布目录及Windows ZIP。脚本使用UTF-8 BOM，兼容Windows PowerShell 5.1中文路径。

## 覆盖及构建

关闭旧程序，将压缩包内全部文件覆盖到HarmonicaPlayer.csproj所在目录。然后运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Release.ps1
```

构建脚本会先运行核心测试与Windows关闭回归，再生成：

`bin\Release\HarmonicaPlayer-v0.1.5-win-x64.zip`

管理员运行写入使用建议，不会强制修改EXE提权清单。仍须右键EXE选择“以管理员身份运行”。

## 验收

1. 取消“仅日志测试”，回到游戏口琴界面，按开始键。不应弹出确认框或主动切换前台窗口；松开按键并完成3秒倒计时后演奏。
2. 在软件内点击开始，3秒内切入目标游戏窗口；留在软件自身窗口时仍应取消真实输出。
3. 演奏期间停止快捷键、切出窗口停止仍有效。
4. “仅日志测试”勾选时只出日志，不发声、不发送输入。
5. 检查最终ZIP包含README.md、使用说明.txt和rhythm-demo.txt。
6. 船上问题需收集能稳定复现的操作步骤后再调查。建议先在陆地测试。

## 更新GitHub

确认测试正常后：

```powershell
git status --short
git add *.cs *.csproj Tests README.md 使用说明.txt UPDATE-0.1.5.md Build-Release.ps1
git diff --cached --stat
git commit -m "Release v0.1.5: remove start dialog and improve user guide"
git push origin main
```

创建v0.1.5 Release，Target为main，上传新的win-x64.zip。源码推送不会自动更新Release附件。

## 验证状态

84项自动测试通过；主程序及Windows关闭测试项目Release编译均通过，0警告、0错误。
制作环境为Linux，未运行Windows界面和游戏实测。游戏内无弹窗启动、管理员/无边框配置效果及船只场景须在Windows游戏环境验证。
