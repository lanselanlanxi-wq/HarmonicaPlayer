# v0.3.3 验证

2026年9月25日：用户确认本版实机测试无问题，批准发布。以下Linux验证记录保留为构建记录；不表示用户实测仍未完成。

- 共408项核心检查通过：既有343项（起音保留检查限定为长音），新增65项。
- 新增检查使用用户原始片段，覆盖BPM90/120/180、括号内外时值等价、40ms分块与连续输出一致、静音gap、平滑起止边界、短音中段能量及重复音分音。
- 重复音波形比较容许1个PCM量化单位的浮点舍入差异；同一片段分块输出仍要求逐样本完全一致。
- 主程序与Windows窗口测试项目Release编译通过，均为0警告、0错误。
- 检查了WinMM三缓冲轮换和界面异步进度回调，未从代码中确认短音专属的缓冲错误，本次不修改播放后端。
- Linux环境未运行WPF窗口测试、WinMM实际出声及Windows发布脚本。数字信号检查不能替代主观试听。

## 更新与重点试听

完整覆盖源码（包括Assets），保留自己的简谱，运行：

    powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Release.ps1

脚本运行核心和窗口测试，再生成bin/Release/HarmonicaPlayer-v0.3.3-win-x64.zip。

重点用反馈片段试听短音、长音及重复音，无需重做此前已通过的整套手动功能测试。若WAV正常但程序试听仍有突突声，请提供实际出声录音及BPM/gap，以继续检查实时输出。

定向检查：

    dotnet run --project .\Tests\ParserTests.csproj -c Release -- --short-note-only

生成新版对比WAV：

    dotnet run --project .\Tests\ParserTests.csproj -c Release -- --render-example .\AudioExamples\phrase-v0.3.3-bpm120.wav
