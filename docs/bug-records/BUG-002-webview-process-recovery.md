# BUG-002 WebView 进程故障没有分类恢复

- 状态：已修复关闭
- 优先级：P2
- 发现时间与方式：2026-10-06，全项目审查 F02。
- 受检版本：v1.0.14；提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`。
- 影响面：官网浏览区、故障提示与刷新。
- 证据边界：源码与微软运行合同确认；没有终止真实账号的浏览器进程。
- 证据汇总：[审查范围与脱敏结果](evidence/2026-10-06-audit.md)。

## 现象

WebView 进程故障没有分类恢复。所有 ProcessFailed 均显示同一遮罩；现有恢复路径对已存在的 CoreWebView2 执行 Reload，未按故障类型重建。

## 复现

核对 ProcessFailed、设置刷新和 InitializeBrowser 的调用链；后续在隔离环境分别注入浏览器、渲染器、GPU 与辅助进程故障。

已知阴性对照：主渲染器退出可以通过重新加载恢复，Reload 在该分支仍是有效手段。

## 代码/行号证据

以下行号固定到受检提交，不随后续代码变化漂移。

- [webview-host/ChatWindow.cs:421](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L421)
- [webview-host/ChatWindow.cs:480](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L480)
- [webview-host/ChatWindow.cs:497](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L497)
- [webview-host/ChatWindow.cs:512](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L512)

## 根因

浏览器进程退出需要重建 WebView；GPU 等故障可以自动恢复。统一刷新和依赖 NavigationCompleted 撤销遮罩的策略无法覆盖这两类恢复语义。

## 修复方案

以下保留发现时的修复建议，实施结果见末尾。

按 ProcessFailedKind 分流，协调浏览器退出后的控制器重建；渲染器故障允许刷新；自动恢复类型避免永久遮罩，并提供失败后的明确重试入口。

## 验收标准

隔离故障注入验证不同进程类型、重复事件和连续失败；自动恢复后遮罩退出；重建不删除登录资料，不记录聊天内容；真实草稿恢复单独核验。

发现时的验收要求保留；本轮状态以以下实施记录为准。

## v1.0.16 实施与验证（2026-10-06）

区分浏览器重建、渲染器刷新与 GPU／辅助进程自动恢复。隔离真实 WebView 浏览器进程被终止后，重新创建控制器成功，保留同一个用户资料目录；GPU 分类不新增阻塞遮罩。未对真实登录账号注入故障，也未实际终止 GPU／渲染器进程。

修复源码：[v1.0.16](https://github.com/DOIT-Ben/DeepSeek-Reflex/tree/v1.0.16)。统一证据与范围：[本轮回归说明](../testing/1.0.16-regressions.md)。发现时源码快照仍保留。
