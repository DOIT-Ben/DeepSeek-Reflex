# 待复验候选

以下保留发现时的候选记录，未计入原 BUG-001–BUG-011；本轮处置见各条末尾。来源：2026-10-06 对 v1.0.14（`c1475a9a7cb57f01f51aaa4086a217e0535dcb05`）的源码审查。

## CAND-001 初次引导在高缩放短屏上的底部操作可达性

- 现象假设：固定高度引导可能超过有效工作区，底部按钮不可达。
- 源码依据：[GettingStartedDialog.cs](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/GettingStartedDialog.cs) 使用固定 ClientSize 和操作区；Shown 调整位置，但没有设置面板的工作区尺寸适配与滚动。
- 证据缺口：没有在实体高 DPI 短屏上观察到按钮丢失，不能宣布故障已经发生。
- 复验：在短工作区、150%／200% 缩放下走完首次引导，核对下一步、跳过、关闭及键盘焦点；与正常高度屏幕对照。
- 通过标准：全部操作可见且可达，外框四角完整；不足时允许滚动或适配尺寸。
- 处置：若复现，建立独立 BUG 档案并从此处链接；若不复现，记录环境与排除证据。

本轮：已增加工作区适配、可滚动正文和固定页脚。330 px 高的自有真实引导窗口检查通过：下一步、跳过可达，正文滚动可用；实体高缩放短屏未逐机验收。

## CAND-002 登录／外链弹窗在初始化完成前关闭

- 现象假设：异步 EnsureCoreWebView2Async 尚未完成时关闭弹窗，可能引发释放后的访问、误报或孤立窗口。
- 源码依据：[ChatWindow.cs:525](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L525) 的 OpenPopup 包含 FormClosed 释放、await 初始化和 finally 完成 deferral。
- 证据缺口：没有本轮实际异常、崩溃或孤立窗口证据；不能把历史其他窗口的 ObjectDisposedException 当作此路径复现。
- 复验：隔离环境延迟初始化，在完成前关闭弹窗，多次重复；对照正常初始化后关闭；检查异常、deferral、进程与 HWND 释放。
- 通过标准：预期关闭不弹错误、不访问已释放对象，无遗留窗口，主聊天继续可用。
- 处置：复现后单独建档；否则记录排除证据。

本轮：真实 SDK 初始化途中关闭首次测试出现 E_ABORT，暴露关闭过程中释放尚未完成的竞态；已加入关闭标记，在关闭前记录取消状态。重新测试正常返回 false，无错误弹窗，窗口已释放。证据见[回归说明](../testing/1.0.16-regressions.md)。
