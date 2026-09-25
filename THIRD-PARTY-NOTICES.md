# VCSL 口琴采样

本地试听使用 Versilian Studios LLC 发布的 Versilian Community Sample Library (VCSL) 中的 Hohner Special 20 C 调 Normal 录音。Hohner为原乐器品牌，本软件与乐器厂商不存在隶属关系。

- 来源：https://github.com/sgossner/VCSL
- 固定提交：c1ea7bcc3c7309650ab0da9d15c9cd1fbc4a4c7e
- 原目录：Aerophones/Free Aerophones/Harmonica-Hohner-Special20-C/Sustains/Normal
- 原文件：Hohner-Special20_Normal_C3.wav、E3.wav、C4.wav、E4.wav、G4.wav、C5.wav（后五个名称均有相同Hohner-Special20_Normal_前缀）。
- 授权：CC0 1.0 Universal。完整文本随源码存放于Assets/Harmonica/LICENSE-CC0.txt，发布包中为LICENSE-CC0.txt。
- 修改：转16位PCM、按实测频率调音、切除起音前静音、滤除直流/超高频、调整响度、提取长音循环区；运行时按目标音高变调、交叉淡化循环与淡出。
- 原文件的C3实际约262Hz，本程序映射为科学音高C4（MIDI60），其他文件相应校正。低音区由采样降调补齐。
- 逐文件SHA256、实测频率、循环位置及处理参数：Assets/Harmonica/SOURCES.json。
- 处理工具：Tools/prepare_harmonica.py；只在重新生成采样时需要Python/numpy/scipy。普通编译和用户运行不依赖Python。

采样已嵌入EXE，没有联网请求。MIDI导出只含MIDI事件，不打包采样。

## v0.3.2 收音采样

- 同一仓库、固定提交及 CC0 授权，目录：Aerophones/Free Aerophones/Harmonica-Hohner-Special20-C/Releases/Normal。
- 原文件：Hohner-Special20_Normal_rel_C3.wav、E3.wav、C4.wav、E4.wav、G4.wav、C5.wav（后五个名称均有相同 Hohner-Special20_Normal_rel_ 前缀）。
- 转单声道 44.1kHz/16位 PCM，采用对应持续音的调音比例与增益，40Hz 高通、17kHz 低通，素材末尾平滑淡出；运行时交叉淡化及限制尾音时长。
- 来源文件 SHA256 与输出帧数记录在 SOURCES.json 的 release 字段中。
- 重建工具：Tools/prepare_releases.py，需在持续音处理完成后运行，输入为原始 release WAV 所在目录。仅重建素材需要 Python/numpy/scipy。
