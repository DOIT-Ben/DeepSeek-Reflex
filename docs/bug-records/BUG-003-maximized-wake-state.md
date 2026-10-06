# BUG-003 最大化状态在收起后唤回时丢失

- 状态：未修复
- 优先级：P2
- 发现时间与方式：2026-10-06，全项目审查 F03。
- 受检版本：v1.0.14；提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`。
- 影响面：快捷键和托盘恢复窗口布局。
- 证据边界：隔离原生窗口复现；不是对所有系统任务栏还原行为的结论。
- 证据汇总：[审查范围与脱敏结果](evidence/2026-10-06-audit.md)。

## 现象

最大化状态在收起后唤回时丢失。MAXIMIZED_WAKE before=Maximized hidden=Minimized resumed=Normal

## 复现

标题栏双击最大化，调用收起入口，再调用 ShowChat；按同一路径验证正常大小窗口作为对照。

已知阴性对照：Normal → Minimized → Normal 的普通窗口恢复正常。

## 代码/行号证据

以下行号固定到受检提交，不随后续代码变化漂移。

- [webview-host/ChatWindow.cs:336](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L336)
- [webview-host/ChatWindow.cs:341](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L341)
- [webview-host/ChatWindow.cs:349](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L349)

## 根因

ShowChat 对 Minimized 固定赋值 Normal，没有恢复收起前的 Normal／Maximized 状态。

## 修复方案

以下为建议，尚未实施。

保存收起前的可见窗口状态；恢复时按该状态还原，避免最小化状态覆盖有效尺寸记录。

## 验收标准

覆盖最大化／普通窗口、隐藏到托盘／最小化、热键／托盘／任务栏；验证 Snap 布局、双击标题栏和尺寸记录。

验收前保持“未修复”；提交修复后记录确切提交、定向测试结果及仍未覆盖的真实场景。
