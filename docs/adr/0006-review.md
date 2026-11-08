# ADR-0006 审查与阶段记录

基线 fbbcee8284346806e40100b1da37393dd422f6c6。用户明确授权完整 ADR 实施和本机安装，不含新公开 Release。方案、阶段和测试合同在 0006-one-click-updates.md 内嵌；本文是状态与证据唯一所有者。

方案第 1 轮：待独立审查。S1、S2：PROPOSED。

## 方案第 1 轮

独立审查者 Confucius / 01a1b41b-da22-7732-9d92-72184ef4bb61，REQUEST_CHANGES。指纹 ADR=docs/adr/0006-one-click-updates.md@sha256:597fed66f63e709a5fe8548df327364fac11ef7f5460b944e19601675eefe820；PHASE/TEST=embedded-in-ADR。

三项高优先级意见：退出到 Host mutex 交接空窗；崩溃后独立恢复入口及验收不足；21 项 ZIP 与历史 11 项安装写集的文件归属不一致。已分别补入跨退出屏障／双重启动检查、先于 UI JIT 的恢复引导／写前日志及中断进程验收、11 项保留写集与其余同名文件保全。未将可选偏好当作用户批准；注册版本仅在安装路径匹配时尽力同步，便携副本不影响个人安装。

## 方案第 2 轮输入

ADR=docs/adr/0006-one-click-updates.md@sha256:2c3a088236cc938eb67c55fdf53e7b2695ecce871d1065b5e3c63797a9402e4e
PHASE=embedded-in-ADR
TEST=embedded-in-ADR

待独立复审；实施尚未开始。

## 方案门禁
第 2 轮独立审查 PASS，Confucius 已独立复算上述指纹，三个 R1 阻断项关闭。当前 ADR 原始字节未改变；PHASE/TEST 内嵌。用户完整实施和本机安装授权有效，S1 IN_PROGRESS，S2 PROPOSED。进入源码修改前核对：HEAD 与基线一致，仅三份本任务 ADR 未跟踪，无无关变更。

## S1 实施审查 R1 输入
累计基线 fbbcee8284346806e40100b1da37393dd422f6c6；当前未提交 UpdateFeed/UpdatePackage/UpdateTransaction、Program 早期入口与编译引用、隔离更新测试。s1-final/updates.log：24 passed, 0 failed。首轮 s1-r1 已捕获恢复引导进程过早退出导致身份校验失败，补入 recovery-ready 身份握手后 s1-r2、s1-final 通过。独立审查待结论；S1 仍 IN_PROGRESS。真实 GitHub 无凭据请求当前 403，响应明确 rate limit remaining=0，UTC 04:37:04 重置；不借用登录 Token，不把受限请求记为通过，待额度恢复验证。

S1 R1 实现 REQUEST_CHANGES（Beauvoir）：首次检查之后、部分替换后强杀 worker 的启动竞争。R2 已修并 PASS；startup-negative 旧逻辑失败、s1-reviewed 最终 28 组通过。另补真实 GitHub 证据：当前无凭据 API 因共享出口限流失败；仅 QA 用现有 gh CLI 取官方 v1.0.19 元数据快照（未取出或传入 Token），生产解析器校验该真实响应，再由无凭据 HttpWebRequest 下载官方 ZIP 并验证 GitHub digest、21 项、内置 SHA、品牌和版本，live-official-tls/updates.log PASS。测试曝光 Download 单独调用时旧 .NET 默认 TLS 不足，TLS12 移到公共 Read 入口；最后 R3 只复核此行及真实证据。客户端无凭据完整 Check 仍待额度恢复后 S2 本机安装前核对，不能由 CLI 快照冒充。

## S1 阶段收口／S2 开始
S1 实现审查 R3 PASS（Beauvoir），最终 s1-tls-final 为28/0，官方元数据＋无凭据真实 ZIP 下载验证已完成。S1 协议、事务、隔离进程及正式包验收 PASSED。无凭据完整 Check 的出口限流明确保留为 S2 最终交付门禁，不与已验证的正式包或局部请求行为混同；未完成前不安装本机或宣称完整交付。方案hash未变，S2 IN_PROGRESS，授权仍为本地实现、测试、本机安装。

后续交付：S2实现已独立R2 PASS，布局/焦点/草稿门禁、本机20预览安装已实际验收。原D1 GitHub无凭据Check持续限流从未计为通过；该发布源决策由ADR0007替代，签名静态源在21客户端真实Check/下载/包校验、本机UI均通过，详0007-review.md，不回写历史失败为成功。
