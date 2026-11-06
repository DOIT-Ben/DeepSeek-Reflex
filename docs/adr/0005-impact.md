# ADR-0005 来源与影响

2026-11-05。需求来源：用户报告模式残留污染（同日会话）——"不同模式容易干扰同一个窗口的上下文，比如我让她总结模式，等第二轮想做普通对话，他就会继承这个总结模式的指令"；经架构澄清（非反代、无中间层，唯一作用面是发出消息的文本）与两轮方案收敛，用户提出"加个：现在是xxx模式"，补强为三要素声明头（身份+旧指令作废+内容保留）。详细决策见 [ADR-0005](0005-mode-directive-header.md)。

## 变更面

| 文件 | 变更 |
| --- | --- |
| `webview-host/ChatModes.cs` | 新增 `Directive(mode)`；`Prefix` 改为头+指令+信封，normal 从空串改为仅头；`All` 出厂文本零改动 |
| `webview-host/PromptInserter.cs` | `Compose`/`ComposeInstruction` 指令前拼同一 Directive |
| `webview-host/ChatModeInjector.cs` | 零改动（catalog 由 Prefix 生成自动带头；normal 提前返回分支语义见 D2） |
| `docs/chat-modes.md` | 同步：「普通对话｜原文输入，不添加提示词」改为带模式声明头；补头的三要素说明 |
| `webview-host/tests`（SettingsTests/BoundaryTests 对应层） | 新增断言：三链同头、normal 带头、头不随 modes.json 覆盖变化、All 指纹不变、空草稿不注入、marker 替换回归 |
| `plugins/dsh-reflex` | 零改动：build.mjs 提取的 All 未受影响，桥经宿主 Prefix 自动带头 |

## 不变边界

无新增网络调用、无新落盘（modes.json 结构与敏感级别不动）。DSH 桥协议（0002）不动。`window-settings.json` 结构不动。注入可见性原则（输入框可见全文）不动。空材料不产生纯提示词草稿的既有合同不动。
