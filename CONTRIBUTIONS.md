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
