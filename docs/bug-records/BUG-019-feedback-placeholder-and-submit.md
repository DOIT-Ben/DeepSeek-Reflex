# BUG-019：反馈输入提示被原生文本框遮挡，发送中可重新启用提交

- 优先级：P2
- 状态：已修复，专项验收通过，已更新本机；安装实例内反馈页的最终截图待复核
- 范围：`FeedbackDialog`，不修改反馈接口、配置格式或聊天窗口交互。

## 证据与根因

用户实机截图显示两个无说明的空框。提示标签虽 `Visible=true`，但 `Controls.Add` 顺序令原生 TextBox 位于它前方。`DrawToBitmap` 的渲染顺序又与真实 HWND 不一致，因此旧截图看起来有占位提示，实机却没有。

修改前专项在 `empty feedback placeholder is above the native edit control` 失败，日志为 `webview-host/tests/results/ui-20261106/feedback-before.log`。新增 `GetChildAtPoint` 检查能抓到该问题；修复后采用原生 `PrintWindow` 截图确认实际提示可见。

另发现输入框未绑定共享外框的焦点状态，且 `TextChanged` 无视发送中状态重新启用提交。通过 busy 状态锁定本次表单与提交入口，避免重复发送；切换只读状态重建编辑句柄时保留 busy guard，防止附带的 TextChanged 清空状态。

## 修复与验收

常驻字段标签、默认单选类型、常驻计数、提示置前、焦点蓝边、发送锁定及按需展开错误区一并完成。两套编译各 82 项专项通过，覆盖浅深主题、三档 DPI、原生层级与截图、重复提交阻止、失败保留、成功与联系方式记忆。详情见 [验收报告](../testing/2026-11-06-feedback-polish.md)。
