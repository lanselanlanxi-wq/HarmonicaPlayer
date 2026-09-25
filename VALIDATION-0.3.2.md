# v0.3.2 验证

## 已完成

- 核心自动检查共343项通过：既有255项，加88项尾音相关检查。
- 覆盖6个录音锚点及低音变调，短音/普通音/长音的收尾到零、gap与休止静音、无削波、分块一致性，以及起音保留、尾音能量衰减和音量为零。
- 主程序及Windows窗口测试项目均通过Release编译，0警告、0错误。

## 实测范围

本环境为Linux，未执行WPF窗口测试、WinMM实际出声或Windows发布脚本。自动检查证明数字音频边界及节奏一致，不代表已确认主观音质。原先关于杂音原因的判断仍为推测。

## Windows更新

1. 完整解压源码到项目目录，包含Assets/Harmonica，保留自己的简谱。
2. 运行：powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Release.ps1
3. 脚本执行核心和窗口测试，再生成bin/Release/HarmonicaPlayer-v0.3.2-win-x64.zip及新EXE。
4. 重点试听短音、普通音、长音和连续重复音的结尾。例如使用BPM120、gap20，试听：1_ 1 1:8 【1】_ 【1】。无需重做之前通过的完整手动功能测试。

定向尾音检查：dotnet run --project .\Tests\ParserTests.csproj -c Release -- --release-only

若新版本仍有同样杂音，需要一段实际出声录音以区分素材气声、播放缓冲或设备问题。
