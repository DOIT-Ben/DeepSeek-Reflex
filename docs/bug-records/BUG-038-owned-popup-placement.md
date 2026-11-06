# BUG-038：自有弹窗没有统一首次定位

- 优先级：P2
- 状态：已修复，本地两套专项通过。

## 现象

登录／外链非模态窗口落在左上侧。反馈和帮助的常规模态打开可居中，但分别维护定位，缺少共同的所有者、工作区和 DPI 稳定规则。

## 复现

所有者置于工作区右下侧，打开 PopupWindow。修改前真实窗口位置为 (52,52)，预期为所有者中心经工作区适配后的 (1187,260)。负例见忽略目录 `webview-host/tests/results/owned-popups-before.log`。

## 代码证据

PopupWindow、FeedbackDialog、GettingStartedDialog 的 Shown 与 StartPosition；SettingsDialog.PositionAtOwner；新增 OwnedWindowPlacement。

## 根因

非模态打开不能依赖旧 CenterParent 路径；各窗体的首次显示与工作区适配分散，设置中的修复未复用到其他自有弹窗。

## 修复方案

设置复用共享几何算法，其余自有对话窗接入共享首次显示策略。统一按所有者当前位置／最小化恢复矩形定位，限制在所有者屏幕工作区，DPI 稳定后校正一次。回调验证可见性版本，不重新定位已恢复的同一会话；短工作区允许约束过大的最小尺寸。

## 验收标准与结果

owned-popups-accepted.log 普通／高 DPI 各 13 项通过。弹窗场景覆盖两块连接显示器，反馈／帮助的真实模态路径、登录／外链宿主的非模态路径，父窗移动后重新打开、屏幕边缘、原生所有者、圆角对齐、初始焦点和原生截图。没有加载登录页面或发送反馈。完整范围见[弹窗验收](../testing/2026-11-06-owned-popups.md)。
