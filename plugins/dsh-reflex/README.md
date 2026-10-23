# Reflex 官网小窗 · DSH 插件试用版

本目录属于 DeepSeek-Reflex 仓库，可作为独立 DSH bundle 安装，不另建项目、不修改 DSH 源码、不复制小窗实现。

当前是 **0.2.0 本地开发原型**，不是已发布插件。它在输入框原有工具行增加 `Reflex ▾`，复用小窗的普通、翻译、解释、理解提问、润色、总结六种模式。菜单关闭时不占额外高度。

新增“带入小窗”需要配套 ADR-0002 原生开发构建；已发布 v1.0.17 没有桥接协议，请使用原有复制路径。已在隔离测试页面验证真实 HTTP→辅助进程→管道→生产 ChatWindow/WebView 的完整带入链路；测试驱动的初始化线程故障已修复。尚未验证官网登录后的完整往返，也未升级个人安装。见 [阶段记录](../../docs/adr/0002-review.md)。

## 已实现的流程

1. 在 DSH 写好草稿，点 `Reflex ▾`，选择处理模式。
2. 配套开发版可点 **带入小窗**：仅传当前草稿与模式，小窗为空时填入，不改持久模式，也不改剪贴板。小窗已有文字时拒绝覆盖，结果未知时先检查小窗，不自动重试。此路径已通过隔离页面验收，填入后仍由用户检查并发送。
3. 兼容路径 **复制请求并打开**：把带模式提示词的文字写入系统剪贴板，唤起已安装的 Reflex。先将小窗的持续模式切到 **普通对话**，再手动粘贴完整请求、检查后发送。DSH 草稿保持原样，附件不会传给官网。官网回复完成后，手动复制需要的结果。
4. 回到插件，点 **读取复制的回复**；或手动粘贴到预览框。核对、编辑后点 **确认填回输入框**，替换原草稿文字，仍由用户决定是否发送。

复制操作会替换系统剪贴板内容，不读取或保存剪贴板历史。插件不会后台监听剪贴板、自动发送、抓取官网回复、读取主会话历史、注册模型工具或修改主任务的系统提示词。预览框中的草稿和结果只保存在组件内存，切换会话或刷新后清除。

输入及回复上限各 20,000 字，超过时拒绝，不静默截断。等待期间若文字、编辑版本、附件或会话发生变化，则拒绝覆盖；预览仍可手动复制合并。含 `@文件` 等结构化引用标签时暂不支持替换，以免把引用降成普通文字。普通图片附件留在 DSH 中。

## 环境与安装

首版支持 **Windows 本机的 DSH Web HTTP 入口**，页面地址须为 `http://127.0.0.1`、`http://localhost` 或 `http://[::1]`。DSH 的 file:// Electron 入口、远程部署、macOS、Linux 暂未适配。相应浏览器需要允许显式剪贴板读写；读取回复失败时可手动粘贴。

支持 DSH `0.1.5-rc.2` 及其后兼容版本。已在本机原有 `0.1.5-rc.2` 程序的隔离数据目录中安装，并验证真实页面入口、六种模式、小窗唤起、手动预览回填与草稿冲突保护；另有 `0.2.1-alpha.1` checkout 现有构建产物的宿主挂载证据。声明版本范围不是所有版本均已验收。系统剪贴板权限和官网登录后的完整往返仍待实测。

先正常安装 [DeepSeek-Reflex](https://github.com/DOIT-Ben/DeepSeek-Reflex/releases/latest)，按官网流程登录。默认定位 `%LOCALAPPDATA%\Programs\DeepSeekFloat\DeepSeekFloat.exe`；ZIP 用户可在 **启动 DSH 的环境**中设置 `REFLEX_EXECUTABLE` 为真实 `DeepSeekFloat.exe` 的绝对路径。插件不会安装或替换小窗。

在终端执行以下命令，`web` 必须替换为实际运行 HTTP 页面的 profile 名称：

```powershell
dsh plugin --profile web add "E:\desktop\AI\11_Products\prod\DeepSeek-Reflex\plugins\dsh-reflex"
```

然后正常重启对应 DSH 入口并刷新页面，检查插件已启用和输入工具行的 `Reflex ▾`。旧版插件列表可能显示 `include:reflex-window`。入口需要先选工作区，建立草稿会话后才出现。本轮安装在隔离数据目录的 web profile 中，没有向原个人 profile 安装插件；卸载可使用 DSH 插件管理界面移除该 bundle。

## 优势与边界

处理请求走官网小窗，不扣 DSH 配置的 API 额度。只有用户主动发送回填结果后，该结果才进入 DSH 主会话并按渠道计费。官网聊天自身仍有上下文和服务限制；不保证永久免费、固定缓存收益、最低延迟或不间断服务。

自动带入代码已提供，完整 WebView 链路仍待验收；回复继续手动复制，不自动读取官网答案。不会用 DSH 模型 API 作为隐藏替代路径。

宿主 status/open 两个本机 POST 路由仍无请求体；新增 stage 只接受受限 `{id,mode,text}` JSON，最多 90000 UTF-8 字节、20000 个 UTF-16 单元，读取期限 3 秒。要求 loopback 来源、同源 Origin 和自定义请求头；不接受客户端指定 EXE 路径或启动参数。stage 使用固定 `--reflex-bridge` 辅助入口和 stdin，正文不进命令行/临时文件；命名管道限制当前用户/登录会话，双方校验 EXE 身份。辅助进程最长 12 秒，失败不自动降级改写剪贴板。桌面权限仍属于运行 DSH 的用户。

## 开发与验证

```powershell
cd E:\desktop\AI\11_Products\prod\DeepSeek-Reflex\plugins\dsh-reflex
npm ci --ignore-scripts
npm run build
npm test
npm run check
npm pack --dry-run

# 真实固定辅助进程与管道（临时目录，无个人 WebView）
pwsh -NoProfile -File ../../webview-host/tests/run-bridge-tests.ps1

# 真实 Cordis + DSH WebServer 隔离宿主挂载，无模型、无个人会话
node scripts/runtime-smoke.mjs E:\desktop\AI\02_Agents\prod\deepseek-harness
# 追加 --open-window 才实际唤起已安装小窗

# 仅模拟 DSH 草稿和剪贴板的浏览器交互预览
npm run preview
```

`client.js` 是随插件提供的浏览器产物，安装无需运行构建脚本。构建时从 `webview-host/ChatModes.cs` 提取模式，并从原生 `PanelTheme` 提取字体、字号、颜色、圆角、控件尺寸与动效参数，避免提示词和样式各自分叉；改变这些定义后需要重新生成并检查浏览器产物。浏览器控件样式集中在 `src/styles.js`，规范及本地验收见 [共享界面样式](../../docs/ui-style-guide.md)。运行包不包含 React 副本，React 由 DSH 提供；开发预览使用的 React 不进入发布文件。新版优先用带版本校验的 `insertText`；旧版只提供 `setDraft` 时，在同步处理函数中先检查会话、文字版本、附件与引用，再执行文字替换，不在检查和替换间等待异步操作。

浏览器预览页面在顶部明确标记为隔离预览。点击“运行隔离交互检查”或在控制台执行 `await runReflexChecks()` 验证真实 React 组件的 24 项交互，使用 `?legacy=1` 覆盖旧版草稿接口；该页面使用模拟剪贴板和模拟启动器，不访问个人剪贴板，不启动小窗，不调用模型。它不能替代完整 DSH 页面和官网往返验收。

当前测试记录见 [验证记录](VERIFICATION.md)。
