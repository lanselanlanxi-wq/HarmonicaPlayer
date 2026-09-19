# v0.2.1 候选包验证记录

日期：2026-09-19。基准：main 8173cfd。开发环境：Linux，.NET SDK 8.0.425。

## 上一轮已执行并通过（本次未重跑）

- Parser/Core：原99项 + 新增54项，共153项。
- 主程序 Release build：0 warnings / 0 errors。
- WindowsSmoke 项目 Release build：0 warnings / 0 errors，仅编译，未运行。
- git diff --check。

新增核心测试覆盖新格式往返、正文空白保留、BPM优先级、大小写和BOM、旧谱文件名BPM、非法/重复/未知文件头、无效速度、UTF-8、1MB限制、旧谱覆盖保护、写入失败保护及临时文件清理、最短音边界和错误位置。

Windows测试覆盖慢保存期间输入无效值、A→B→A、关闭保存最终有效值，以及曲谱未保存标记、取消关闭、保存、保存后重新导入同一路径、取消导入、旧谱覆盖保护、非法文件头保留编辑内容和关闭时保存。本次将间隔断言更新为“修改间隔标记未保存，恢复原值恢复干净状态”，未运行Windows测试。

## 本次间隔补充的定向验证

新增GapDocumentTests的24项定向检查已通过，未重复执行上一轮153项测试。命令：

WindowsSmoke项目（含主程序全部源文件）编译通过：0 warnings / 0 errors；仅编译，未执行Windows测试。git diff --check通过。

```powershell
dotnet run --project .\Tests\ParserTests.csproj -c Release -- --gap-only
```

覆盖@gap读取、缺省20（新旧谱）、大小写、非法值、重复字段、保存当前间隔、磁盘往返及最短音限制。正常完整测试入口仍保留全部测试，Build-Release.ps1发布流程也保留原有测试门槛；本次没有执行该脚本。

## 未执行，需Windows完成

- WindowsSmoke测试实际运行。
- Build-Release.ps1完整脚本及自包含EXE启动。
- 游戏内开始/停止热键、倒计时停止、演奏中停止、关闭、实际音高。
- 原生文件对话框、不同显示缩放下界面布局、真实磁盘权限错误。

源码包不含bin/obj或用户曲谱文件夹，不会自动删除本地文件。更新前备份源码，关闭旧程序，把压缩包根目录内容覆盖到项目根目录；保留本地“简谱”和自有TXT。

在Windows PowerShell逐行执行：

```powershell
cd D:\Downloads\HarmonicaPlayer
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Release.ps1
```

脚本先执行两组测试，失败即停止发布。成功后运行它打开的新目录中的EXE，勿误用旧目录EXE。建议依次检查：

1. 导入旧谱“歌名 bpm100.txt”，速度自动为100，正文无文件头。
2. 导入score-format-demo.txt，速度120；调整后另存，再导入确认一致。
3. 新格式文件名写bpm90、头部写120，应提示冲突且以120为准。
4. 修改谱面后关闭并取消，内容不丢失；保存失败时也不退出。
5. 旧谱另存时选原文件名，应拒绝覆盖；改用新文件名成功。
6. 确认自定义快捷键、停止保护、游戏演奏和关闭均正常。

这些实测通过后再发布v0.2.1、处理PR。本次没有远端推送、合并、关闭PR或发送评论。
