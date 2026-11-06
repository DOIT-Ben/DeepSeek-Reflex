# 2026-11-05 自定义模式提示词与主题接线验收

对应 ADR-0004（S1/S2）与填充式界面整改。全部在隔离偏好目录与断网 WebView（about:blank）完成，未触碰个人安装、个人官网登录资料，未发送真实模型请求。

## 功能与主题

| 项 | 证据 |
| --- | --- |
| 模式提示词存储/校验/容错/目录层 | `run-tests.ps1` default 轮 SettingsTests 全绿（TestModeCatalog 21 项：往返、normal 不可覆盖、重名/长度/上限、坏文件降级保原文件、版本拒绝、未知 id 丢弃、Effective/Prefix/Compose 跟随、Reset 回退）；布局修整后经 harness 复跑同套断言再次全绿 |
| 注入器消费覆盖与自定义模式 | BoundaryTests 新增断言 + chat-mode-tests.cjs 22 项（含 overridden/custom 两条新用例）、prompt 11、focus 10 |
| 设置「聊天模式」页 | harness 截图 + TestModeCatalog 页面冒烟（打开/列表/锁定名编辑/保存路由/落盘） |
| 网页主题接线 | 真实隔离 WebView2：light→`prefers-color-scheme: light`、切 dark→dark 且 light 翻转、回 light 恢复，四断言全过（`Profile.PreferredColorScheme`） |
| DSH 插件兼容 | `npm test` 19/19、`npm run check` 产物一致（六模式与 UI token 提取不受影响） |

## 界面像素验收

12 张截图（浅/深 × 常规/模式/模式编辑/指令/反馈/引导）两轮像素级审查（脚本与 dump 留存 `%TEMP%\reflex-0004-verify-scripts\`）：首轮发现编辑卡异色标签块/空错误槽横带、模式列表系统滚动条、只读名称框不可辨三问题，修复后复验全部归零；动作行 6 按钮均分、全窗口零静止描边、Back→Card→Well 层次链两主题下成立。截图目录：`%TEMP%\reflex-0004-acceptance\`。

## 环境限制与遗留

- ~~全量绿轮~~ **已收官（2026-11-05 08:26 轮）**：ADR-0005 声明头实施后，`run-tests.ps1` default 419 + high-dpi 418 双轮零失败（证据目录 `%TEMP%\DeepSeek-Reflex-tests-4518f732…`），boundary 120+11+22+10，DSH 插件 19/19。此前的物理鼠标类偶发在无并行操作时未复现。
- **官网主题 D4 探针（已闭环）**：站点主题存 `localStorage themePreference` 默认 `system`，仅页面启动时读取 `prefers-color-scheme`；深色启动实测页面直接深底 `rgb(21,21,23)`，启动后翻转不跟。宿主 boot-time 接线契合，无需注入同步脚本；中途切主题下次页面加载生效。
- **BUG-014 已修复关闭**（同日）：InputFrame 单一 Well 填充、HotkeyBox 同色、标签透明、子页按钮不再拉伸；100%/150% 截图（`settings-modes-150.png` 等 19 张）零静止描边、四级填充层次齐全。
- **ADR-0005 已实施**：每条模式链消息带当前模式声明头；注入器 normal 早退守卫同步修正（有材料即带头）；真机行为验收（FR-03）待用户登录态实测。
- BUG-007 保持"已修复待验收"：唯一缺口是实体双屏 DPI 往返，本机单屏无法闭环。
- 深色下自定义模式过多时列表仍会出现系统滚动条（已知限制，见 ui-style-guide）。
