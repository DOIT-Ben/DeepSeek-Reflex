# BUG-005 取词取消路径未恢复本次复制改写的剪贴板

- 状态：未修复
- 优先级：P2
- 发现时间与方式：2026-10-06，全项目审查 F05。
- 受检版本：v1.0.14；提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`。
- 影响面：Ctrl+C 回退取词与剪贴板保全。
- 证据边界：未修改的生产 SelectionCapture.cs 配合内存化键盘／窗口／剪贴板替身重放；没有向真实应用发送复制按键。
- 证据汇总：[审查范围与脱敏结果](evidence/2026-10-06-audit.md)。

## 现象

取词取消路径未恢复本次复制改写的剪贴板。正常复制 restored=True；复制后切换窗口返回取消，restored=False，剪贴板仍为测试选中文字。

## 复现

让复制完成后、20ms 轮询前切换前台窗口；比较同一测试中的正常复制路径。

已知阴性对照：正常复制路径能恢复原剪贴板；对照素材仅使用合成文本。

## 代码/行号证据

以下行号固定到受检提交，不随后续代码变化漂移。

- [webview-host/SelectionCapture.cs:118](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/SelectionCapture.cs#L118)
- [webview-host/SelectionCapture.cs:126](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/SelectionCapture.cs#L126)
- [webview-host/SelectionCapture.cs:134](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/SelectionCapture.cs#L134)

## 根因

轮询先检查 SameWindow 再记录复制后的序列；提前返回时 copied 仍为 0，finally 跳过恢复。

## 修复方案

以下为建议，尚未实施。

取消与超时路径也要确认本次复制的序列和所有权；只恢复属于本次操作的内容，不能覆盖用户或其他应用后来写入的剪贴板。

## 验收标准

覆盖取消、延迟复制、第三方并发写入、剪贴板忙与格式限制；取消不填入请求，正常取词可恢复；真实 Word／浏览器验收另行完成。

验收前保持“未修复”；提交修复后记录确切提交、定向测试结果及仍未覆盖的真实场景。
