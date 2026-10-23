# ADR-0002 来源与架构影响

日期 2026-10-19，检查代码 main@7a5902d；工作树干净，origin 为 DOIT-Ben/DeepSeek-Reflex。

## 原始来源

- U1：本会话用户截图询问“这里的布局你觉得放到哪里更好？因为放到这个里面他挡住了高度”。附件仍由原消息留存，未复制到仓库。
- U2：本会话用户要求“记住要在我们的这个逻辑上，做成插件形式……比如能不能直接在输入框进行润色”，并选择优先“支持不同的模式”。此前实现的主任务隔离、官网处理和确认回填边界沿用。
- U3：2026-10-19，助手建议优先紧凑模式入口、一键带入，其次自定义模式/贴边收纳；用户随后要求“开始按照ADR推进”。本轮采用前两项作为首轮实施范围，后两项不扩入。

## 只读代码证据与落点

| 模块 | 既有职责/接口 | 事实与风险 | 新职责落点 |
| --- | --- | --- | --- |
| ChatWindow.cs，约 835 行，LayoutContent/RefreshChatMode/UpdateChatMode | 原生布局、生命周期、模式协调、唤起 | 原工具栏固定 38 DIP；已协调较多窗口状态，不能再放 IPC 解析/进程安全 | 只迁移按钮父级、更新布局/错误反馈，加窄 stage 回调 |
| Program.cs，约 55 行 | CLI shortcut、单实例、主窗入口 | 现 OpenMessage 只能唤起，不传文字 | 在互斥前分派固定桥接辅助入口 |
| ChatModes.cs / ChatModeInjector.cs | 六模式目录、前缀/一次性处理所有权 | 插件构建从目录提取，避免另一套指令；现有 ExpectOneShot 与 adopt 可复用 | 原目录继续作为提示词唯一来源 |
| PromptInserter.cs，约 65 行，InsertAsync(core,prompt,previous) | 官网 origin、唯一编辑器、草稿所有权与填入 | previous=null 拒绝任何非空草稿；不发送 | 复用，不放入传输职责 |
| plugins/dsh-reflex/index.js / launcher.js | Cordis 本机受限路由、固定 EXE 唤起 | 原路由零正文，无传递能力；不能沿用无体检查到新接口，也不能放开全部旧路由 | 新 HTTP body 有界读取模块与固定桥接启动函数 |
| plugins/dsh-reflex/src/client.js / core.js | 显式复制、快照、确认回填/版本保护 | 主入口仍要用户粘贴；异步结束后会话需二次核对 | 新 stage 动作复用快照，保留旧动作 |

中等影响：新增本机 IPC 和 stdin 文本传递，应单独测试长度、来源、进程身份、超时与关闭。原有布局/DPI/登录数据和 DSH 会话写入边界不扩大。当前没发现需要全仓重构的证据。

一手 API 依据：[GetNamedPipeServerProcessId](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeserverprocessid)、[GetNamedPipeClientProcessId](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeclientprocessid)。.NET Framework 4.8 的 ACL 构造与异步管道能力以本机编译/隔离传输测试核验，不把较新版 .NET 示例直接套入。
