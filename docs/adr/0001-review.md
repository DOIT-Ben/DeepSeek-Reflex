# ADR-0001 独立审查与进度

本文件是方案门禁与本轮进度的唯一记录，不属于受检 ADR／PHASE／TEST 字节集合。方案作者不自判 PASS。

## 代码身份

- 分支：main
- HEAD：b614ceb24eed1fefa3513bc2060db20159969a1f
- 原生版本：1.0.12
- 检查时工作树：干净；远端 main 与本地 HEAD 一致
- 获准本轮范围：ADR、独立审查、HTML 交互稿及设计文档提交／推送

## 阶段进度

| 阶段 | 状态 | 证据 |
|---|---|---|
| S0 文档与架构证据 | COMPLETE | 来源、影响报告、ADR 与阶段测试合同已落盘 |
| S1 独立方案审查 | COMPLETE | 两轮独立 PASS；第 2 轮对应最终格式清理后的原始字节 |
| S2 可点击设计稿 | COMPLETE | 自包含 HTML、固定两行取词栏和同框设置子页已完成 |
| S3 验收与交付 | COMPLETE_WITH_LIMITATIONS | 设计主提交 2707796 已推送并核对远端；P17 的 file:// 直接打开未实测，限制已单列 |
| N 原生接入与发行 | NOT_STARTED | 本轮范围之外 |

## 审查轮次

### 第 1 轮：PASS

审查者：独立 adr_reviewer，任务 adr0001_review。送审输入如下：

```text
ADR=docs/adr/0001-edge-dock-and-custom-prompts.md@sha256:a7ed6f32d46c6216867e7b835d9ee9afbfba7bc98506bdf9e21a91162e1978a5
PHASE=docs/adr/0001-stage-tests.md@sha256:f545965aa70a6bae08c1b204fcbc254abd6ce8bca5ac21475c0057dd2579cdb4
TEST=docs/adr/0001-stage-tests.md@sha256:f545965aa70a6bae08c1b204fcbc254abd6ce8bca5ac21475c0057dd2579cdb4
```

独立审查者在开始与结束时分别复算以上原始字节，均匹配；代码身份和远程亦匹配。全程只读，未参与撰写，未修改文件。

结论：未发现要求修改受检输入的实质问题。两功能、用户选择、阶段依赖、独立标签、模块职责、原生兼容、草稿安全、独立配置及失败恢复合同足以指导本轮 HTML 设计交付。无需第 2 轮。

核对范围：ADR、来源、影响、阶段测试、审查记录，以及 ChatWindow、PromptInserter、SelectionCapture、QuickAnswerWindow、WindowSettings、Preferences、SettingsDialog、SmoothFrame、WindowFrame、WindowModes、PanelTheme 和原生测试运行器。

非阻断待验证项：当前原生主窗／四角层使用初始化 DPI，混合 DPI 和拔屏可能出现几何偏差；独立非激活标签的实际 HWND 层级、隐藏主窗后的可见性与模态恢复尚未实测。分别由 N01、N02、N03 承接。

限制：未运行应用、原生测试、浏览器原型、官网、物理取词、性能、多屏或安装发行；未访问用户偏好或登录资料。需求以本轮来源 U1–U4 核对。PASS 不等于新增原生功能已实现或发布。

门禁：可按本轮已有授权进入 S2／S3；原生 N 阶段仍为 NOT_STARTED。受检 ADR 和合同保持字节不变，最终交付前再次复算。

### 第 2 轮：PASS（2/2，最终受检输入）

触发原因：提交前 `git diff --cached --check` 发现新文件 EOF 的额外空行。仅移除 ADR、阶段合同、来源和影响报告最后一个 LF，保留正常结尾换行；方案、状态及测试意图均未变。第 1 轮记录保留为历史，最终门禁采用本轮指纹。

```text
ADR=docs/adr/0001-edge-dock-and-custom-prompts.md@sha256:c3dec8159b77cb5fb0cf20dc279fe202832d666217052ec4f868f16cdc7f12dc
PHASE=docs/adr/0001-stage-tests.md@sha256:10e6de5f0ef6b022229bcaed84f54583b5e30dc17fa5be83ddb92afd52de928e
TEST=docs/adr/0001-stage-tests.md@sha256:10e6de5f0ef6b022229bcaed84f54583b5e30dc17fa5be83ddb92afd52de928e
```

同一独立审查者以索引保存的第 1 轮字节与当前工作文件独立比较，确认四份文件只删除 EOF 的一个 `0x0A`，并在开始、结束复算新指纹。代码身份、原生源码及版本／manifest／packaging 无变更。结论 PASS，未提出实质修正；混合 DPI、拔屏及标签 HWND 仍由 N01–N03 承接。

审查者未审原型实现或浏览器操作；P17 离线文件直接打开仍未实测，不能声明 P17 全部通过。两轮审查额度已使用完毕，本轮最终 ADR／PHASE／TEST 不再修改。

## 原型验收

实际交互、响应式检查及限制见[原型验收记录](../design/0001-reflex/README.md)。P01–P16 已完成浏览器验收；P17 的本地 HTTP、内嵌资源、刷新及错误／请求检查已完成，离线文件直接打开因工具禁止 file:// 尚未实测。未运行任何原生 N 项目，不将 HTML 渲染视为原生窗口验证。

## 交付回执

设计主提交 `27077969bdcbee3358a6e9b4ff5284161e2aca2a` 已推送至 origin/main，并通过 `git ls-remote` 核对相同远端提交。共 9 个文件，仅包含双语 README、5 份 ADR 文档及原型 HTML／说明；无原生源码、版本、manifest、安装器或流水线修改。

提交前，JavaScript 静态解析、36 个相对文档链接、资源内嵌、Logo 原字节一致性及暂存空白检查通过；Git 索引中的 ADR／PHASE／TEST 与第 2 轮受检原始字节完全一致。完整浏览器证据在仓库外保全，未把测试日志或过程截图放入仓库。

应用版本仍为 1.0.12。本轮没有发布 Release、更新安装包或安装程序；后续原生接入仍为 NOT_STARTED。
