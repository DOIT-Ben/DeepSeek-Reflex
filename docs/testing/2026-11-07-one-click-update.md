# ADR-0006 一键更新验收

代码基线 `fbbcee8284346806e40100b1da37393dd422f6c6`，本地预览版本 1.0.20；不发布新 Release。方案、轮次和阶段结论以 `docs/adr/0006-review.md` 为准。

## 已有证据

产物位于忽略目录 `webview-host/tests/results/update-20261107/`，不包含个人聊天、配置或登录资料。

| 层次 | 结果与证据 |
| --- | --- |
| S1 协议与真实隔离进程 | `s1-tls-final`：28 项通过。父进程身份、准备失败、退出超时、真实锁文件、成功重启、失败回退旧版重启、并发启动、worker 强杀、主 exe 替换前后及提交后的恢复、未知文件保全、注册表路径匹配 |
| 启动竞争负例 | `startup-negative`：旧逻辑失败；修复版本在 `s1-tls-final` 通过 |
| 网络边界 | 实际回环 HTTP 404/429、短包、超限、取消 Abort、摘要不符、失败清理与重试；不将回环源用于正式生产配置 |
| 官方包分层验证 | `live-official-tls`：QA 用现有 CLI 获得官方 v1.0.19 JSON 快照；应用解析器验证该响应，无凭据下载官方 ZIP、核对 GitHub SHA256、21 项文件、内置摘要、品牌与版本通过。没有将 CLI 凭据传给应用 |
| 更新 UI 与协调器 | `ui-core1-final`：普通／高 DPI 各 89 项。失败重试、取消、重复点击、自动检查关闭及持久化失败、三次草稿门禁、帮助反馈所有者、单实例窗口、冻结／恢复热键和控制范围、浅深主题 100%/150%/200%、窄屏滚动、只读字段焦点与选区 |
| 真实 DOM 草稿 | 同上，独立 profile 的真实 WebView Core1 加载本地映射页面；生产探针检测 textarea、contenteditable、纯空白、缺失编辑器。只回传状态，不把文本回传到 C# |
| 草稿负例 | `draft-negative-real`：旧 `some(trim())` 脚本在真实纯空白草稿断言失败；修复后通过 |
| 旧设置布局 | `general-regression`：两套各 70 项通过 |
| 旧输入行为 | 复用上述已编译测试程序，两套各 250 项焦点／选区检查通过 |
| Core 回归 | `core-regression`：23 项核心边界，另有 11 项草稿、22 项模式、10 项聚焦模拟 DOM 检查通过；core-only 明确不执行实体桌面分支 |
| 发行包 | `package-final` 构建 ZIP／Setup；`package-final-check`：21 项白名单、全部摘要、版本 1.0.20 和图标验证通过 |

## 证据边界

完整 WinForms WebView 初始化在本轮测试环境中因 Controller2 `E_NOINTERFACE` 失败，保留 `ui-real` / `ui-real-config` 日志；未修改 SDK、运行时或生产控件来绕过。基础 Core1 DOM 证据不能冒充完整 WinForms 初始化成功。本机已安装应用的实际登录页面另行核对。

无凭据 GitHub API 曾因共享出口额度耗尽返回 403，`live` 与 `live-direct-final` 均保留失败；CLI 元数据快照与直接检查严格区分。最终直接检查和个人安装结果在 ADR 状态记录补充。

强杀 worker 是已执行的中断故障注入，不能替代实际断电、所有 Windows 版本或物理跨屏热插拔验证。本轮不更新线上反馈服务、模型路由或公开 Release；CI 仅增加离线 updater fixture 门禁，不声称已在远程运行。
