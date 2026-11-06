# ADR-0004 来源与影响

2026-11-04。需求来源：用户对 DeepSeek-Reflex 的三项反馈（同日会话）——①"能够自定义不同模式的嵌入提示词"，且明确选择"内置可改 + 可新增自定义模式"；②"黑白主题对浏览器内部好像不能实现"；③设置界面线框太多、嵌套丑、悬浮效果差、铺不满。③属视觉整改，记录于 `docs/ui-style-guide.md`，不进本 ADR。

## 变更面

| 文件 | 变更 |
| --- | --- |
| `webview-host/ModeCatalog.cs` | 新增：modes.json 存取/校验/容错（合同对齐 commands.json） |
| `webview-host/ChatModes.cs` | 新增 Effective 目录层与 Apply/Reset/IsBuiltin；All 出厂目录不动；Find 改查 Effective |
| `webview-host/ChatModeInjector.cs` | catalog 生成改读 Effective（一行） |
| `webview-host/PromptInserter.cs` | 无改动（经 Find 间接消费 Effective） |
| `webview-host/ChatWindow.cs` | 启动加载 modes.json；菜单遍历 Effective+「自定义提示词…」入口；ReloadChatModes（保存回调、删除回退）；ApplyWebProfileTheme（PreferredColorScheme） |
| `webview-host/SettingsDialog.cs` | 第三页「聊天模式」（列表/编辑卡/恢复默认/新增）+ 动作行入口 + 页切换与 footer 三态 |
| `webview-host/SettingsPanelControls.cs`、`FeedbackDialog.cs` | 填充式整改（独立于本 ADR 决策，见 ui-style-guide） |
| `plugins/dsh-reflex` | 零改动：构建期提取 All 不受影响；未知 id 宿主回退兼容旧 bundle |

## 不变边界

无新增网络调用、无正文落盘（modes.json 只存用户自编提示词，与 commands.json 同敏感级别）、无剪贴板暗写、无新监听端口。DSH 桥协议（0002）不动。`window-settings.json` 结构不动（ChatMode 仍存单一 id）。
