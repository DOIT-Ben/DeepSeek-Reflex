# 已知问题与缺陷档案

初始登记日期：2026-10-06。原始受检版本 v1.0.14，提交 `c1475a9a7cb57f01f51aaa4086a217e0535dcb05`；2026-10-13 补充 v1.0.17 发布前验证发现的两项问题。发现快照保留。

共 39 项：P1 3 项、P2 33 项、P3 3 项。37 项在各档案列明的范围内已修复并验收；BUG-007 保留完整实体混合 DPI 验收缺口，BUG-019 保留个人安装反馈页复核。新增功能审计的本地结果见[2026-11-06 功能边界报告](../testing/2026-11-06-functional-boundaries.md)，不等同于线上部署。

| 编号 | 优先级 | 标题 | 状态 | 影响面 |
| --- | --- | --- | --- | --- |
| [BUG-001](BUG-001-guide-tray-wake.md) | P1 | 引导被托盘收起后，正常唤起链路无法恢复 | 已修复关闭 | 窗口唤起、首次引导和使用帮助 |
| [BUG-002](BUG-002-webview-process-recovery.md) | P2 | WebView 进程故障没有分类恢复 | 已修复关闭 | 官网浏览区、故障提示与刷新 |
| [BUG-003](BUG-003-maximized-wake-state.md) | P2 | 最大化状态在收起后唤回时丢失 | 已修复关闭 | 快捷键和托盘恢复窗口布局 |
| [BUG-004](BUG-004-settings-access-denied-rollback.md) | P2 | 设置保存被拒绝时，实际热键未回滚 | 已修复关闭 | 偏好保存、快捷键与界面状态一致性 |
| [BUG-005](BUG-005-clipboard-cancel-restore.md) | P2 | 取词取消路径未恢复本次复制改写的剪贴板 | 已修复关闭 | Ctrl+C 回退取词与剪贴板保全 |
| [BUG-006](BUG-006-draft-ownership-equality.md) | P2 | 仅凭文本相同误判草稿所有权 | 已修复关闭 | 取词填入与用户草稿保护 |
| [BUG-007](BUG-007-dynamic-dpi-layout.md) | P2 | 跨显示器 DPI 变化未统一重算布局 | 已修复待验收 | 布局、圆角缓存、命中区域与设置面板 |
| [BUG-008](BUG-008-switch-accessible-state.md) | P2 | 自绘开关开启时无障碍接口仍报告 Off | 已修复关闭 | 读屏与辅助自动化 |
| [BUG-009](BUG-009-ci-behavior-gates.md) | P2 | 发布 CI 未接入已有核心行为回归 | 已修复关闭 | 构建门禁与发行可信度 |
| [BUG-010](BUG-010-packaged-readme-links.md) | P3 | 发行包中英文 README 的本地链接缺失 | 已修复关闭 | 解压后阅读说明、离线帮助 |
| [BUG-011](BUG-011-popup-frame-consistency.md) | P3 | 登录与外链弹窗没有复用统一外框 | 已修复关闭 | 弹窗视觉、导航与关闭反馈 |
| [BUG-012](BUG-012-redundant-webview-background-interface.md) | P2 | 重复设置默认背景色请求不支持的 WebView 接口 | 已修复关闭 | 主窗、重建、弹窗与初始化兼容性 |
| [BUG-013](BUG-013-synthetic-dpi-monitor-race.md) | P3 | 模拟 DPI 消息与真实跨屏缩放竞争导致假失败 | 已修复关闭 | 本机回归可信度与发布门禁 |
| [BUG-014](BUG-014-settings-nested-surfaces.md) | P2 | 设置与反馈的输入表面叠了多层灰底 | 已修复关闭 | 设置聊天模式、快捷指令、快捷键与反馈输入 |
| [BUG-015](BUG-015-subpage-form-discoverability.md) | P2 | 两个子页的表单没有标题、按钮语义靠悬停才可发现 | 已修复关闭 | 设置聊天模式与快捷指令子页 |
| [BUG-016](BUG-016-mode-list-null-draft-crash.md) | P1 | 未进过聊天模式页时 DPI 缩放触发模式列表空引用崩溃 | 已修复关闭 | 设置对话框 DPI 变更与子页布局 |
| [BUG-017](BUG-017-anchor-autoscroll-field-collapse.md) | P1 | 高 DPI 下子页输入字段渲染成发丝线（锚定+滚动容器布局损坏） | 已修复关闭 | 设置两个子页的字段与布局重算 |
| [BUG-018](BUG-018-settings-toggle-session.md) | P2 | 设置打开后唤出键失效，不能保留草稿整组收起 | 已修复关闭 | 设置主页、快捷指令、聊天模式及热键注册 |
| [BUG-019](BUG-019-feedback-placeholder-and-submit.md) | P2 | 反馈提示被文本框遮挡，发送中可重新启用提交 | 已修复并更新本机，实机反馈页待复核 | 反馈页层级、焦点和提交状态 |
| [BUG-020](BUG-020-catalog-validation.md) | P2 | 模式与指令目录可载入重复、保留或超限条目 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-021](BUG-021-atomic-preference-writes.md) | P2 | 并发偏好写入争抢同一个临时文件 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-022](BUG-022-pending-editor-save.md) | P2 | 子页最终保存跳过正在编辑的条目 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-023](BUG-023-native-close-draft.md) | P2 | 原生关闭绕过未保存确认 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-024](BUG-024-late-selection-settings.md) | P2 | 设置打开后延迟完成的取词仍填入输入框 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-025](BUG-025-feedback-store-cap.md) | P2 | 反馈文件可被最后一次追加写穿容量上限 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-026](BUG-026-feedback-http-errors.md) | P2 | 反馈编码或存储失败直接断开 HTTP 连接 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-027](BUG-027-feedback-read-timeout.md) | P2 | 不完整反馈请求长期占住工作线程 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-028](BUG-028-helper-response-integrity.md) | P2 | 辅助进程异常退出或坏编码仍可能被当成成功 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-029](BUG-029-aborted-upload-error.md) | P2 | 上传中断后的 socket 错误可成为未处理异常 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-030](BUG-030-bridge-test-dependencies.md) | P2 | 桥接回归入口缺少模式目录编译依赖 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-031](BUG-031-legacy-installer-files.md) | P2 | PowerShell 升级脚本递归删除旧安装目录中的非托管文件 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-032](BUG-032-bridge-log-failure.md) | P2 | 诊断日志失败会终止唯一桥接服务循环 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-033](BUG-033-deleted-mode-settings.md) | P2 | 主页旧快照会写回已删除的当前模式 | 已修复，本地验收通过 | 本轮功能与边界审计 |
| [BUG-034](BUG-034-feedback-short-work-area.md) | P2 | 反馈布局重新撑大已适配短屏的窗口 | 已修复，本地验收通过 | 短工作区、反馈错误提示与重试可达性 |
| [BUG-035](BUG-035-editor-first-focus-selection.md) | P2 | 预填输入框首次聚焦会自动全选 | 已修复，全页专项验收通过 | 设置、模式、指令、反馈与快捷键录入 |
| [BUG-036](BUG-036-settings-owner-placement.md) | P2 | 设置窗口首次打开固定在屏幕左上侧 | 已修复，本地验收通过 | 设置及其子页的所有者定位 |
| [BUG-037](BUG-037-feedback-rate-limit-body-close.md) | P2 | 反馈限流在请求体到达前关闭连接 | 已修复，本地验收通过 | 反馈接收端的并发与分段请求 |
| [BUG-038](BUG-038-owned-popup-placement.md) | P2 | 自有弹窗没有统一首次定位 | 已修复，本地验收通过 | 设置、反馈、帮助、登录与外链 |
| [BUG-039](BUG-039-popup-initial-dpi.md) | P2 | 登录／外链窗口首次跨屏时重复放大 | 已修复，本地验收通过 | 新建窗口的 DPI 与逻辑尺寸 |

两个尚未证实的场景另列于 [待复验候选](CANDIDATES.md)，不计入确认问题数量。验证范围、对照及脱敏诊断见 [证据汇总](evidence/2026-10-06-audit.md)。

每条档案独立维护现象、复现、源码快照、根因、修复建议和验收标准。修复后更新同一档案及本索引，避免平行记录；源码证据保留发现时的固定提交链接。

2026-11-06 补充 BUG-017 的确定性根因：全宽字段标签遮挡兄弟输入窗口，100% 即可复现。修改前失败、修改后两套编译各 158 项通过；高 DPI 全量运行的模态停顿限制见 [字段布局验收](../testing/2026-11-06-settings-editor-layout.md)。

本轮证据与尚未覆盖的场景见[v1.0.16 回归说明](../testing/1.0.16-regressions.md)。关闭仅针对所登记的缺陷与验证范围，不代表全部应用和设备已通过兼容性验收。

v1.0.17 的初始化修复、DPI 负例与全量门禁见 [发布验证](../testing/1.0.17-release.md)。
