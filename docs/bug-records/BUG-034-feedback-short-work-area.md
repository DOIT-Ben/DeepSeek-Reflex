# BUG-034：反馈布局重新撑大已适配短屏的窗口

- 优先级：P2
- 状态：已修复，本地两套构建各 102 项反馈检查通过（2026-11-06）。
- 影响面：短工作区／极高缩放下的反馈输入、错误提示、备用链接和发送入口。
- 证据边界：真实 WinForms 窗口，使用模拟 400% DPI 触发溢出；不冒充实体 400% 显示器验收。

## 现象

`FitBounds` 已按工作区限制窗口，但下一次布局直接写回 408／472 DIP 高度，重新越过屏幕边界。发送和失败后的入口可能在屏幕外。

## 复现

运行 `run-tests.ps1 -FeedbackOnly`。新检查在真实测试窗口应用 400% 几何，要求窗口位于当前工作区。修改前 `feedback-short-negative.log` 在 `feedback height is constrained to the actual working area at extreme DPI` 失败。

## 代码证据

`webview-host/FeedbackDialog.cs` 的 `Arrange`、`surface` 和 `content`；回归在 `webview-host/tests/SettingsTests.cs` 的 `TestFeedbackLayout`。日志根为忽略目录 `webview-host/tests/results/audit-20261106/`。

## 根因

布局把逻辑内容高度当成物理窗口高度，未提供溢出访问路径。只加滚动而继续忽略滚动偏移，还会产生多余的横向滚动。

## 修复方案

窗口高度始终通过工作区适配。完整表单放在一个内容面内，由外层视口纵向滚动；布局使用当前滚动偏移和可用宽度，防止横向溢出。常规尺寸沿用原有分组与间距。

## 验收标准与结果

`feedback-final-acceptance.log`：普通／高 DPI 两套各 102 项通过。包括浅深主题、100%／150%／200% 几何和原生截图，以及模拟 400% 下工作区约束、纵向滚动、发送、失败后草稿保留、备用链接和重试可达。

中间夹具曾在短屏布局之后丢失 WinForms 同步上下文，导致异步结果在布局中并发更新；已与既有反馈测试一致，在发送前重新建立 STA 同步上下文。保留失败日志，不把重跑变绿作为根因解释。

本轮未更新个人安装；当前源码、完整证据矩阵及其余平台限制见[功能边界报告](../testing/2026-11-06-functional-boundaries.md)。
