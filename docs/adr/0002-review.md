# ADR-0002 审查与实施记录

基线 main@7a5902dc279e6fa9166896a1a48ee005a082a760，2026-10-19。用户授权本地 ADR 流程、实施和隔离验证；未授权本轮提交、推送、发布、替换安装版。

## 方案门禁

第 1 轮：独立 adr_reviewer（adr0002_review），结论 PASS；审查者未参与写作，未修改文件。2026-10-19 实施前再次复算下列原始字节一致，可实施 S1/S2。本文件不属于受检字节。

```text
ADR=docs/adr/0002-compact-mode-and-dsh-handoff.md@sha256:e30c7e43da1f395bdaed59f50f0b78b41111bb8d58acf12569ac1fdd306f32cf
PHASE=docs/adr/0002-stage-tests.md@sha256:13b165a9a4f6de8d33c3019e6d00eb4f292901b7d21df9e8475b514d7b198b17
TEST=docs/adr/0002-stage-tests.md@sha256:13b165a9a4f6de8d33c3019e6d00eb4f292901b7d21df9e8475b514d7b198b17
```

审查覆盖需求、职责、原生/插件调用链、安全失败处理与分层验收，无阻断问题。非阻断 P2：旧安装版持有固定单实例互斥时，开发副本无法提供按 EXE 路径区分的管道；须验证有界失败，不能停止/替换个人实例或误报成功。独立 fixture 提供开发副本服务。

## 阶段状态

| 阶段 | 状态 | 证据 |
| --- | --- | --- |
| S0 | PASSED | 第 1 轮 PASS，指纹复核一致 |
| S1 | PASSED | 离线构建、原全套回归、最终双环境各 49 项交互检查、截图复核和独立实现审查 PASS；中英文 README 已说明开发版/发行版区别 |
| S2 | PASSED | 2026-10-19 恢复验收：测试驱动 STA 消息循环修复经专项独立审查 PASS；真实 HTTP→helper→pipe→生产 WebView 带入通过，原生全套第 2 次通过；保留首次托盘失败与前台焦点跳过边界，文档已同步 |

## S1 验收

独立 reviewer `s1_review` 共三次 PASS；第 2/3 次核对菜单测试补充及测试时序修复。最终代码未改变六模式目录/持久化/注入语义。

- `pwsh -NoProfile -File webview-host/build.ps1 -Offline`：通过。
- 原 `run-tests.ps1`：首次发现遗留 title 反射引用，修复后第 2 次全套通过（default/high-dpi、102 boundary、11 draft、19 chat-mode 和 10 focus）。追加菜单键盘测试后第 3 次 default 290 通过，high-dpi 在已关闭菜单读取 Handle 失败，保留原日志。
- 对新增交互改用显式 `-InteractionOnly`：首次后处理缺 focus-script.json，修复此专用入口后第 2 次 default/high-dpi 各 49 检查通过。菜单先恢复真实 DPI 并等待激活稳定，Closed.CloseReason 必须为 Keyboard，未放松断言。默认全套路径不变。
- 临时证据分别在 `%TEMP%/Reflex-ADR0002-S1-r2`、`Reflex-ADR0002-S1-final`、`Reflex-ADR0002-compact-r2`。最后一份最小宽度默认/150% 实际 chrome 截图已目视检查，模式入口与左右按钮无重叠，无额外模式行。测试的 100/150/200% 几何矩阵全部通过。

## 外部验收边界

个人 DSH profile、个人官网对话及安装发布均未操作。隔离窗口在当前连接的屏幕间执行了实际跨屏唤起，保留 WebView/草稿/模式；这不等于已穷尽物理混合 DPI、热插拔和全部显示器组合。

## S2 实现审查

独立 reviewer `s2_review`：第 1 次 REQUEST_CHANGES，第 2/3 次 PASS（仅实现审查，不代替阶段验收）。两个独立复现问题已修复：

1. Framework 的管道挂起 ReadAsync 不会仅因 CancellationToken 取消而结束。现在 read/write deadline 注册 Dispose，UI 协调等待超时显式取消，helper 总期限也关闭管道。
2. normal 材料以空行开头时，旧的一次性 header 为空会永久拦住持续模式。现在覆盖首个非空行及其前导换行，并在清空编辑器时撤销所有权；材料原样保留。

新增真实异常帧测试又发现 InvalidDataException 不属于 IOException，显式列入恢复后超长帧不再退出服务。第 3 次审查复核此修复及真实传输证据，通过。

最终主要实现指纹（2026-10-19）：

```text
webview-host/ReflexBridge.cs sha256:8fd0471c254a51ebc6c2a118598b15ed173117677a336b05c006592b81bd683d
plugins/dsh-reflex/src/client.js sha256:886119a89aec0d8096732e03caa010a10e59f8785b16f7e3230f0a32e0f2098b
plugins/dsh-reflex/client.js sha256:a65fb1a646ce5ceb67cb2710e21062b93ac4eb13348d12cc2a45f70250d54699
```

## S2 首轮验收与历史阻塞

| 层级/合同 | 实际结果 | 边界/证据 |
| --- | --- | --- |
| Framework 离线构建 N05 | 通过 | 新模块首次编译有 C# 5 语法/await-in-catch 错误，第 2 次字段未赋值警告，修复后第 3 次通过；最终修复后再构建通过。未替换安装版。 |
| Node T01/T02/P02 | 16/16 通过 | 固定 stdin/argv、stdout/id/status、超时终止自己 helper、body 字节/字段/模式/来源、单任务、既有草稿回填等；启动器替身。新增测试首次通过。 |
| 实际 WinExe/管道/HTTP T02 | 7/7 通过 | `%TEMP%/Reflex-ADR0002-transport-r3/transport.log`；只允许当前 SID/拒绝 Network SID、真实 helper、双向身份检查的正向证据、半帧超时后可继续、超长帧恢复、拒绝非同 EXE Node 客户端、真实 HTTP→stdin→pipe、Dispose 缺服有界失败。回调为测试替身，**不证明 WebView 已填入**。首次 runner 路径失败，第 2 次暴露异常帧服务退出，第 3 次通过。尚未实测恶意服务抢占管道名的反向拒绝。 |
| 模式算法 N03 | 当前生成脚本 20/20 通过 | 从第 3 次原生 fixture 生成的脚本，含新增前导空行 normal→清空→下一条恢复持续模式；模拟 DOM，不冒充官网。 |
| 插件 React P01/P02 | 最终新旧 API 各 24/24 通过 | 本机隔离浏览器预览：stage 原文/模式、双击单任务、无剪贴板暗写/DSH 草稿改写、成功绑定、已有官网草稿拒绝、未知结果不重放、切会话不绑定、原复制/回填/引用/附件保护。浏览器读写均为 fixture 替身。390×740 下弹层边界 left=18/right=378/top=276/bottom≈728，原生顶层 popover，未裁切。截图 `%TEMP%/Reflex-ADR0002-plugin-narrow.png`。Chrome DevTools 首次连接超时，改用可用 IAB 完成；未改浏览器配置。 |
| 包白名单 | 12 个运行文件 | `npm pack --dry-run --json`，新增 handoff.js，未包含测试数据/依赖/profile；0.2.0 未发布。 |
| 生产宿主 WebView N02/N03/N04 | **环境阻塞，未通过** | `run-boundary-tests.ps1 -ChatModeOnly` 三次（Reflex-ADR0002-bridge、-r2、-r3）均保留原 `production host initializes before waiting for isolated navigation` 断言并失败；未跳过或降低要求。 |

### 首轮初始化失败（成因已由下文恢复验收纠正）

第三次隔离测试 FirstChance 只记录异常类型与代码栈，定位到 Microsoft SDK 内部 `CoreWebView2Controller.get__nativeICoreWebView2Controller2` → `set_DefaultBackgroundColor` → `WinForms.WebView2.InitCoreWebView2Async` 的 NotImplementedException。独立基础 WebView fixture 的模式断言可运行，但生产 ChatWindow 初始化未成功，因此桥接到实际官网编辑器的断言尚未执行。

另从未修改的 HEAD `7a5902d` 导出原始源码，在独立目录复用同一 SDK/loader，只运行旧 ChatModeOnly，**同一生产宿主初始化断言仍失败**。证据 `%TEMP%/Reflex-ADR0002-baseline-5c41774b1f294c03922820e395cbd086/baseline.log`。当时据此记录为当前环境下既有兼容失败；基线复现只证明并非新桥接代码特有，不能证明 runtime 不支持该接口。具体成因由下文线程证据纠正。未升级运行时或修改个人环境，此前 S1 全套通过保留为当时事实。

当时恢复条件：修复并独立验证该宿主初始化兼容故障，重跑 N02/N03/N04（包括 HTTP→helper→pipe→实际 WebView）。该条件已由下文满足。回复自动读取、官网登录往返、个人 DSH profile 集成及完整物理混合 DPI 矩阵仍是另行验收。本轮原生版本/manifest 未改，开发构建并非新发行包。

前轮收尾：相对文档链接与 `git diff --check` 通过；暂存区为空。源码/文档均留在当前主工作树，未提交推送。三个本轮浏览器预览监听端口与自建标签页已关闭，未发现本轮临时 EXE 遗留进程；测试证据保留。未重启用户已停止的 3081 DSH 试用实例。

## 2026-10-19 恢复：线程根因与完整带入验收

用户明确授权继续修复兼容故障并补齐带入验收；基线、工作树和受检 ADR/合同字节均未变化。本次新增源码修复仅在 `webview-host/tests/BoundaryTests.cs`，没有修改生产初始化、SDK、loader 或安装环境。

### 根因与有界修复

1. 追加异常/线程诊断后，入口确为线程 1 / STA，但 `SynchronizationContext` 是基类；SDK 的 DefaultBackgroundColor 异常发生在线程 3 / MTA、无同步上下文。错误为 E_NOINTERFACE（ICoreWebView2Controller2），SDK 包装为 NotImplementedException。证据 `%TEMP%/Reflex-ADR0002-thread-diagnostic.log`。
2. 临时复制源码、优先运行生产宿主而跳过独立 WebView 的探针仍失败，排除前一个 WebView 释放顺序的假设。文件仅在 `%TEMP%/Reflex-ADR0002-order-probe-5fe4523cd9974f2e919296226e784b2f`，未引入仓库选项。此前诊断日志 `%TEMP%/Reflex-ADR0002-resume-diagnostic` 保留。
3. 测试原本仅用 DoEvents 驱动；短期窗体关闭后，同步上下文可能恢复为基类，await 续体转到线程池。新增隐藏 Control 的 BeginInvoke，将所有桌面分支串行放入真正的 `Application.Run` 消息循环；finally 退出循环，异常经 ExceptionDispatchInfo 保留堆栈重抛。core-only 分支不启动 UI。新增 STA + WindowsFormsSynchronizationContext 断言，原生产初始化、背景色及草稿等断言全部保留。符合 [WebView2 官方线程模型](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/threading-model)。
4. 原 SDK 1.0.4258.31、runtime 155.0.4283.62 下直接通过，不升级、不屏蔽 DefaultBackgroundColor，也没有为生产代码加异常降级。因此纠正此前“runtime 兼容性”的归因为隔离测试驱动线程问题。

专项独立 reviewer `compatibility_fix_review` 结论 PASS；核验线程日志、原断言保留及真实消息循环正常/异常两条路径。对实际编译的 RunDesktop 反射探针验证 STA、WinForms 上下文、退出与异常堆栈：首次被机器既有非法 LIB 路径阻断，第 2 次仅在探针子进程清空 LIB 后通过。未修改文件或操作个人窗口。本专项不重置前述 S2 实现审查的三次计数。

```text
webview-host/tests/BoundaryTests.cs sha256:8e7ddbfef837f1ec764cdcb46955ec0355e41d29338c4ed0ae325bb53cb37559
```

### 当前验收结果

| 层级/合同 | 结果与尝试 | 证据与边界 |
| --- | --- | --- |
| 初始化与完整带入 N02/N03/N04 | 修复后专项首次通过，44 项原生检查 + 11 草稿 + 20 模式 + 10 焦点 | `%TEMP%/Reflex-ADR0002-sta-fixed.log`；设 `REFLEX_TEST_TRANSPORT_SCRIPT=plugins/dsh-reflex/tests/native-transport.mjs`。真实 HTTP→固定 stdin helper→鉴权管道→生产 ChatWindow/WebView，随后直接核对编辑器内容和 sent.length===0。 |
| 完整 Boundary N02–N05 | 首次通过，114 项 + 11/20/10 | `%TEMP%/Reflex-ADR0002-resume-boundary-all.log`；未跳过默认桌面分支，含实际屏幕间唤起及保留同一 WebView/草稿/模式。计数会因 Windows 是否授予前台焦点而有一项差异，日志明确记录。 |
| 原生全套与离线构建 N05 | 本次共 2 次：第 1 次默认 291 通过，高 DPI 在既有 `tray-bound popup really opens` 失败；检查原测试后，第 2 次完整命令通过，默认/高 DPI 各 290、各 10 焦点、旧启动器参数转发、Boundary 115、草稿 11、模式 20、焦点 10 | 首次 `%TEMP%/Reflex-ADR0002-resume-full.log` 原样保留。最终 `%TEMP%/Reflex-ADR0002-resume-full-r2.log` 退出码 0；原托盘断言未变、更未降低要求。两处托盘前台切换因 Windows 未授予焦点，走原有 SKIP 分支，不能宣称这两处已验收。首次托盘瞬时失败的确切原因未单独证实，不能仅凭重跑通过声称已修复托盘问题。 |
| 独立实现审查 | 专项及收尾文档核对 PASS，原 S1/S2 审查仍有效 | reviewer 对照最终日志确认 290/290、两处既有 SKIP、真实 HTTP→WebView 内容和零发送、Boundary 115 及 11/20/10，源文件指纹一致。新增修复仅为测试消息循环；前述生产/插件实现与已通过 Node 16、真实传输 7、新旧 API 浏览器各 24、打包 12 文件证据一致，无需以重复运行代替范围核对。 |

带入专项证明：空编辑器成功填入原文和所选模式；只加一次润色前缀，不改变持久翻译设置；已有草稿/工具草稿、加载中和模态状态拒绝；不会自动发送；用户在 fixture 显式发送或清空后，后续输入恢复持续模式。含前导空行的普通模式材料原样保留。页面内容为官方 HTTPS origin 下拦截的自有 HTML，WebView profile 与偏好均在临时目录，未读取个人官网对话或执行真实模型请求。

S2 的 N02–N05、T01–T02、P01–P02 按上述分层证据满足，解除初始化 ENVIRONMENT_HOLD 并记录 PASSED；中英文说明、插件使用说明和验证记录已同步。这个状态仅代表 ADR 本地开发验收，不代表正式 DSH 0.2.0 集成、官网往返或发布完成。系统剪贴板权限、官网登录往返、恶意服务抢占管道名的反向拒绝及完整物理混合 DPI 矩阵仍未实测。

收尾核验：5 份更新文档的 39 个本地链接与 `git diff --check` 通过；暂存区为空，HEAD 仍为 `7a5902d`。保留所有既有未提交改动与诊断证据；本次未提交、推送、发布、向个人 DSH 安装 0.2.0 或替换 v1.0.17。没有启动新的浏览器预览/DSH 服务，未发现本轮临时 EXE/Node 残留；原 3081 试用与三个预览端口均未监听。ADR 与阶段测试合同指纹未改。
