# v0.2.1 贡献来源

基于 v0.2.0 main `8173cfd31d931cf8fbc4e1fd18328b3215dd132a`。

## daailiuying — PR #3

[PR #3](https://github.com/lanselanlanxi-wq/HarmonicaPlayer/pull/3)

参考提交 `5160d406922474712e7ccf8f313b9f428ad27bc2` 的 generation 方案，防止旧设置保存结果覆盖最新界面提示，并扩展到编辑防抖等待期间。新增慢保存期间无效输入、A→B→A及关闭时保存最终值的Windows测试。

当前main的后台队列已经顺序写入；本修复不宣称解决已证实的磁盘乱序覆盖，也不丢弃有效的排队快照。原提交作者信息为 GitHub Copilot，含 Co-authored-by: Copilot App；PR由daailiuying提出，按其实际代码贡献记录。

## izumikonata10 — PR #1

[PR #1](https://github.com/lanselanlanxi-wq/HarmonicaPlayer/pull/1)

参考 `3b17eff` 的共用校验与边界测试思路，以及 `078d9343ab39002744bbf691168115945d8e0c2d` 等提交中的确定性等待思路。按当前main适配，保留12ms准备时间与错误字符位置；现有播放测试等待日志进度，不依赖固定3400ms。

本次未引入 ToneOutput、本地音频或“播放测试”按钮。PR #1不能整体视为已合并或被完全替代，音频部分需另行评审。

本更新包是针对当前main的适配实现，不等于远端PR已合并。建议完成Windows和游戏验证后，再向贡献者说明采纳范围，并在后续提交/发布说明中保留本文件及来源链接。

## v0.3.0 补充

本地音频（ScoreTimeline、AudioSynthesis、LocalAudioPlayer）、试听界面及 MIDI 导出为本轮独立实现，未复制或引入 PR #1 的 ToneOutput。上文“本次未引入音频”指历史 v0.2.1 的采纳范围，不表示 v0.3.0 没有音频。保留两位贡献者的来源记录；本轮没有合并、关闭或回复任何远端 PR。

## v0.3.1 音源

新增VCSL（Versilian Studios LLC）的Hohner Special 20 C调Normal真实采样，采用CC0-1.0授权。本版处理了采样八度编号、调音、响度、起音和长音循环；详细来源、固定上游提交及原文件SHA256见THIRD-PARTY-NOTICES.md与Assets/Harmonica/SOURCES.json。
