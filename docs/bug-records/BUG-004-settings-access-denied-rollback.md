# BUG-004 设置保存被拒绝时，实际热键未回滚

- 状态：未修复
- 优先级：P2
- 发现时间与方式：2026-10-06，全项目审查 F04。
- 受检版本：v1.0.14；提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`。
- 影响面：偏好保存、快捷键与界面状态一致性。
- 证据边界：隔离原生窗口与自建偏好目录复现；未改真实用户设置。
- 证据汇总：[审查范围与脱敏结果](evidence/2026-10-06-audit.md)。

## 现象

设置保存被拒绝时，实际热键未回滚。UnauthorizedAccessException；内存设置保留旧键，实际注册键已变为新键（458886 → 458883）。

## 复现

在隔离偏好目录把写入目标 .new 建成目录；把唤起键从 Ctrl+Alt+Shift+F23 改为 Ctrl+Alt+Shift+F20 并保存。

已知阴性对照：已有 IOException 分支的回滚检查通过；正常保存可保持配置和绑定一致。

## 代码/行号证据

以下行号固定到受检提交，不随后续代码变化漂移。

- [webview-host/ChatWindow.cs:404](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L404)
- [webview-host/ChatWindow.cs:328](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L328)
- [webview-host/Preferences.cs:23](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/Preferences.cs#L23)

## 根因

ApplySettings 先注册新热键，保存失败只捕获 IOException；UnauthorizedAccessException 跳出事务，实际绑定没有回滚。置顶等写入入口的异常边界也需一并检查，尚未逐项运行复现。

## 修复方案

以下为建议，尚未实施。

覆盖预期的访问拒绝与 I/O 异常；恢复实际绑定和界面状态，保留未保存编辑内容；回滚失败也须显示错误。

## 验收标准

覆盖访问拒绝、文件占用、替换失败和回滚失败；真实绑定、内存及文件一致；编辑内容不丢失；相关置顶写入入口另做定向回归。

验收前保持“未修复”；提交修复后记录确切提交、定向测试结果及仍未覆盖的真实场景。
