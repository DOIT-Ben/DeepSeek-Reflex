# BUG-006 仅凭文本相同误判草稿所有权

- 状态：已修复关闭
- 优先级：P2
- 发现时间与方式：2026-10-06，全项目审查 F06。
- 受检版本：v1.0.14；提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`。
- 影响面：取词填入与用户草稿保护。
- 证据边界：生产 C# 生成的脚本在模拟 DOM 中重放；没有在真实对话制造覆盖。
- 证据汇总：[审查范围与脱敏结果](evidence/2026-10-06-audit.md)。

## 现象

仅凭文本相同误判草稿所有权。脚本仍返回 filled，用户粘贴的草稿被替换为 NEXT REQUEST。

## 复现

先填入工具请求，切到新编辑器，用户手动粘贴上一请求，再执行下一次取词填入；新编辑器不含工具所有权标记。

已知阴性对照：普通个人草稿保持不变；有明确所有权且未编辑的工具请求可正常替换。

## 代码/行号证据

以下行号固定到受检提交，不随后续代码变化漂移。

- [webview-host/PromptInserter.cs:31](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/PromptInserter.cs#L31)
- [webview-host/ChatWindow.cs:300](https://github.com/DOIT-Ben/DeepSeek-Reflex/blob/c1475a9a7cb57f01f51aaa4086a217e0535dcb05/webview-host/ChatWindow.cs#L300)

## 根因

old===previous 可以绕过所有权标记；lastInserted 跨对话保留。内容相同无法证明草稿仍属于工具。

## 修复方案

以下保留发现时的修复建议，实施结果见末尾。

所有权绑定文档、编辑器和编辑版本；用户编辑、发送或切换对话时撤销；取消文本相等带来的替换授权，保留复制请求回退。

## 验收标准

覆盖 SPA 切换、复用编辑器、用户编辑后恢复相同文本、发送后的重用和 contenteditable；已有草稿不覆盖；始终由用户确认发送。

发现时的验收要求保留；本轮状态以以下实施记录为准。

## v1.0.16 实施与验证（2026-10-06）

草稿所有权同时匹配编辑器、页面 URL、请求、实际内容与有效标记；用户编辑／提交使标记失效。生产生成脚本的 11 条模拟 DOM 回归通过，覆盖同文新编辑器、编辑后还原、SPA 跳转、Enter、模糊输入框与 contenteditable 个人草稿。真实官网 DOM 后续变化仍可能影响兼容性。

修复源码：[v1.0.16](https://github.com/DOIT-Ben/DeepSeek-Reflex/tree/v1.0.16)。统一证据与范围：[本轮回归说明](../testing/1.0.16-regressions.md)。发现时源码快照仍保留。
