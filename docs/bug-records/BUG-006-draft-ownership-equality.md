# BUG-006 仅凭文本相同误判草稿所有权

- 状态：未修复
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

以下为建议，尚未实施。

所有权绑定文档、编辑器和编辑版本；用户编辑、发送或切换对话时撤销；取消文本相等带来的替换授权，保留复制请求回退。

## 验收标准

覆盖 SPA 切换、复用编辑器、用户编辑后恢复相同文本、发送后的重用和 contenteditable；已有草稿不覆盖；始终由用户确认发送。

验收前保持“未修复”；提交修复后记录确切提交、定向测试结果及仍未覆盖的真实场景。
