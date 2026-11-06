# BUG-036：设置窗口首次打开固定在屏幕左上侧

- 优先级：P2
- 状态：已修复，本地两套位置专项通过。
- 影响：设置主页、快捷指令和聊天模式直接入口。

## 现象

主窗口移到其他位置后，新开的设置仍出现在屏幕左上侧，而非围绕主窗口定位。

## 复现

使用非模态 Show(owner)，将所有者放到工作区右下侧，再创建设置窗口。新增检查在修改前失败，记录于忽略目录 `webview-host/tests/results/settings-placement-before.log`。

## 代码证据

`SettingsDialog.cs` 的 StartPosition、构造期 ApplyDialogHeight、Shown 和 PositionAtOwner；`ChatWindow.ShowSettingsPage`；`SettingsTests.TestAuditUi` 的 owner placement 场景。

## 根因

非模态设置仍沿用 CenterParent，实际首次显示未得到预期的父窗居中；构造期工作区适配已把默认坐标收进屏幕左上边缘。没有独立的首次显示定位步骤。

## 修复方案

首次显示显式围绕 Owner 居中，使用 Owner 所在屏幕的工作区限制边界。跨屏 DPI 消息处理后按最终物理尺寸再居中一次；回调检查生命周期版本，收起后不应用过期定位。重复激活及整组隐藏恢复保留用户手动位置。主窗最小化时使用 RestoreBounds，避免用 Windows 位于屏幕外的最小化图标坐标。

## 验收标准与结果

`settings-placement-final.log` 普通／高 DPI 各 12 项通过。位置场景遍历实际连接显示器、三个页面入口，检查首次居中、屏幕边界、跨屏逻辑宽度、圆角 HWND 对齐和原生所有者、手动移动后隐藏恢复，以及所有者移动后重新打开。位置比较基于真实窗口坐标，不依赖模态 CenterParent。

最小化分支补充负例见 `settings-placement-minimized-before.log`：原实现使用图标坐标，最终位置专项验证修复后的 RestoreBounds 分支。首次居中修复后，焦点与选区专项普通／高 DPI 各 250 项通过，证据为 `settings-placement-focus.log`；之后仅补充最小化定位分支，焦点逻辑未再变更。不发送全局组合键，不改个人配置；显示器热插拔未在本轮测试。
