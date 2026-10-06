# 待复验候选

状态均为“待复验”，不是已确认 Bug，未计入 BUG-001–BUG-011。来源：2026-10-06 对 v1.0.14（`c1475a9a7cb57f01f51aaa4086a217e0535dcb05`）的源码审查。

## CAND-001 初次引导在高缩放短屏上的底部操作可达性

- 现象假设：固定高度引导可能超过有效工作区，底部按钮不可达。
- 源码依据：[GettingStartedDialog.cs](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/GettingStartedDialog.cs) 使用固定 ClientSize 和操作区；Shown 调整位置，但没有设置面板的工作区尺寸适配与滚动。
- 证据缺口：没有在实体高 DPI 短屏上观察到按钮丢失，不能宣布故障已经发生。
- 复验：在短工作区、150%／200% 缩放下走完首次引导，核对下一步、跳过、关闭及键盘焦点；与正常高度屏幕对照。
- 通过标准：全部操作可见且可达，外框四角完整；不足时允许滚动或适配尺寸。
- 处置：若复现，建立独立 BUG 档案并从此处链接；若不复现，记录环境与排除证据。

## CAND-002 登录／外链弹窗在初始化完成前关闭

- 现象假设：异步 EnsureCoreWebView2Async 尚未完成时关闭弹窗，可能引发释放后的访问、误报或孤立窗口。
- 源码依据：[ChatWindow.cs:525](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L525) 的 OpenPopup 包含 FormClosed 释放、await 初始化和 finally 完成 deferral。
- 证据缺口：没有本轮实际异常、崩溃或孤立窗口证据；不能把历史其他窗口的 ObjectDisposedException 当作此路径复现。
- 复验：隔离环境延迟初始化，在完成前关闭弹窗，多次重复；对照正常初始化后关闭；检查异常、deferral、进程与 HWND 释放。
- 通过标准：预期关闭不弹错误、不访问已释放对象，无遗留窗口，主聊天继续可用。
- 处置：复现后单独建档；否则记录排除证据。
