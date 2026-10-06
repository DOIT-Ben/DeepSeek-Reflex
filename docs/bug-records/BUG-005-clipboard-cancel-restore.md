# BUG-005 取词取消路径未恢复本次复制改写的剪贴板

- 状态：已修复关闭
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

以下保留发现时的修复建议，实施结果见末尾。

取消与超时路径也要确认本次复制的序列和所有权；只恢复属于本次操作的内容，不能覆盖用户或其他应用后来写入的剪贴板。

## 验收标准

覆盖取消、延迟复制、第三方并发写入、剪贴板忙与格式限制；取消不填入请求，正常取词可恢复；真实 Word／浏览器验收另行完成。

发现时的验收要求保留；本轮状态以以下实施记录为准。

## v1.0.16 实施与验证（2026-10-06）

先记录复制产生的序列号，再处理失焦取消；只有源应用仍拥有相同序列的复制结果时才恢复快照。内存适配器运行生产逻辑：成功恢复、复制后失焦恢复、其他应用更新保留，三条通过。相同进程内另一次复制无法仅靠进程所有者区分；真实 Word 等跨应用测试未在本轮执行。

修复源码：[v1.0.16](https://github.com/DOIT-Ben/DeepSeek-Reflex/tree/v1.0.16)。统一证据与范围：[本轮回归说明](../testing/1.0.16-regressions.md)。发现时源码快照仍保留。
