# BUG-007 跨显示器 DPI 变化未统一重算布局

- 状态：未修复
- 优先级：P2
- 发现时间与方式：2026-10-06，全项目审查 F07。
- 受检版本：v1.0.14；提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`。
- 影响面：布局、圆角缓存、命中区域与设置面板。
- 证据边界：源码确认动态 DPI 接线缺口；实体混合 DPI 移动过程与具体视觉故障未实测。
- 证据汇总：[审查范围与脱敏结果](evidence/2026-10-06-audit.md)。

## 现象

跨显示器 DPI 变化未统一重算布局。初始化 scale 固定，主窗 AutoScaleMode=None；部分按钮绘制采用当前 Graphics.DpiX，布局与绘制尺度来源不一致。

## 复现

检索主窗、设置和 SmoothFrame 的 readonly scale、AutoScaleMode 与 DPI 变化处理；后续在 100% 与 150%／200% 双屏往返拖动。

已知阴性对照：分别在默认／高 DPI 启动时既有回归通过，不能代替跨屏动态验证。

## 代码/行号证据

以下行号固定到受检提交，不随后续代码变化漂移。

- [webview-host/ChatWindow.cs:97](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L97)
- [webview-host/ChatWindow.cs:109](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L109)
- [webview-host/SmoothFrame.cs:13](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/SmoothFrame.cs#L13)
- [webview-host/SmoothFrame.cs:121](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/SmoothFrame.cs#L121)
- [webview-host/SettingsDialog.cs:21](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/SettingsDialog.cs#L21)
- [webview-host/SettingsPanelControls.cs:62](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/SettingsPanelControls.cs#L62)
- [webview-host/SettingsPanelControls.cs:89](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/SettingsPanelControls.cs#L89)
- [webview-host/SettingsPanelControls.cs:117](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/SettingsPanelControls.cs#L117)

## 根因

各对象保存初始化比例，没有统一处理运行中的 DPI 变化；跨屏表现仍属于待实测范围。

## 修复方案

以下为建议，尚未实施。

在真实 DPI 变化时统一更新布局、命中及圆角图块；仅在 DPI 变化时重建缓存；明确 DIP 与屏幕像素的坐标转换。

## 验收标准

实体混合 DPI 双屏往返、在副屏打开设置／引导、移除显示器与保存位置恢复；尺寸及点击区域正确，拖动性能不回退。

验收前保持“未修复”；提交修复后记录确切提交、定向测试结果及仍未覆盖的真实场景。
