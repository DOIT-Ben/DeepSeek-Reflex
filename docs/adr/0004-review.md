# ADR-0004 审查与实施记录

2026-11-05。用户已批准实施计划（模式自定义=内置可改+新增自定义；界面整改=全应用统一）。

## 实施内容

以下保留 2026-11-05 的实施与验证快照；其中填充式视觉已被次日白底和完整输入描边方案替代。当前验收以[2026-11-06 功能边界报告](../testing/2026-11-06-functional-boundaries.md)为准。

S1 与 S2 当时已实施于 main 工作区：

- `ModeCatalog.cs` 新建；`ChatModes` 增加 Effective/Apply/Reset；`ChatModeInjector` catalog 改读 Effective；`ChatWindow` 启动加载、菜单入口、`ReloadChatModes` 删除回退、`ApplyWebProfileTheme`（PreferredColorScheme，初始化+ApplyTheme 双接线，try/catch 容错）。
- `SettingsDialog` 第三页「聊天模式」（normal 固定行/内置编辑+恢复默认/自定义增删改）+ 动作行「聊天模式」按钮 + 模式菜单「自定义提示词…」入口 + footer 三态与未保存拦截。
- 填充式整改（`SettingsPanelControls`/`FeedbackDialog`/`SettingsDialog`：删 Outline、PanelChoices/PanelCard 去描边、输入 Well 底色、编辑卡铺满）见 `docs/ui-style-guide.md` 新章节。

## 验证证据

| 层 | 结果 |
| --- | --- |
| `run-tests.ps1` default DPI 全轮 | 通过（含 TestModeCatalog 全部新增断言：存储往返/normal 不可覆盖/重名校验/上限/坏文件降级/版本拒绝/未知 id 丢弃、Effective 目录层、覆盖措辞进 Prefix 与取词 Compose、模式页打开/列表/内置锁定名编辑/保存回调路由/落盘内容） |
| BoundaryTests + 3 个 cjs | 通过；含新增"覆盖指令进注入 catalog""自定义模式按自身 id 进 catalog"cjs 用例（overridden/custom 两条 PASS），chat-mode 22 / draft 11 / focus 10 |
| DSH 插件 `npm test` / `npm run check` | 19/19 通过；client.js 产物一致（build.mjs 提取的六模式与 UI token 未受影响） |
| 全量套件多轮 | 本机存在与改动无关的 UI 时序偶发：改动前基线 A/B（stash 后连跑两轮）第 2 轮即挂"modal guide return preserves geometry"；改动后各轮分别偶发"pill swallow""Escape focus""physical click high-dpi"，失败点不重复且集中于物理光标/焦点类断言（当时推测与前台键鼠干扰有关，未逐项定位，不能据此判通过）。功能面断言无失败 |

## 真机验收（2026-11-05）

隔离 harness（未入仓库，临时目录 `%TEMP%\reflex-0004-acceptance\`）：真实 WebView2（隔离 profile、about:blank、断网）断言 light/dark/往返三轮 `matchMedia('prefers-color-scheme')` 全过；12 张截图（浅/深 × 六窗口）经两轮像素级审查：

- 首轮发现 3 个问题并全部修复：①编辑卡上页面色标签块与空错误槽横带（标签改 Transparent）；②模式列表默认视图出现未主题化系统滚动条（列表 196 DIP + 24/28 行距，默认 7 行恰好不滚）；③内置模式编辑时只读名称框与卡面同色不可辨（保持 Well 底 + Muted 灰字）。另补齐 6 个编辑器输入框的聚焦环接线。
- 复验轮：三个问题像素级归零（Back 色块 0、滚动条竖带 0、名称框 Well 占比 69.8%），动作行 6 按钮均分与全窗口零描边回归通过。
- 布局修复后 TestModeCatalog 全部断言对最终布局复跑通过。

## 实施后精简（2026-11-05 同日）

应用户要求对本轮代码去重（行为零变化，全部反射测试锁定的私有成员名保持）：`SettingsDialog` 把两页逐字重复的编辑卡提炼为 `BuildEditorCard`+`InputWell`（输入井、聚焦环、点击聚焦只写一份），页切换三胞胎合并为 `ActivatePage`，子页布局二胞胎合并为 `ArrangeSubpage`，列表行按钮统一 `RowAction`，删除失去用途的 `modeNameFrame` 字段；`ChatWindow` 的 `SwitchChatMode`/`ReloadChatModes` 共享 `ApplyChatMode` 尾部；`ModeCatalog` 校验收敛为单一 `ValidateInstruction`。精简后两套子页功能套件（TestModeCatalog、TestEdgeDockAndCommands）与像素复验（卡面零异色块、无滚动条带、Well 层次）全部重跑通过。

## D4 决策点关闭（2026-11-05 真机探针）

隔离 WebView 只读访问 chat.deepseek.com（未登录落地页）实测：

- 站点主题偏好存于 `localStorage["__appKit_@deepseek/chat_themePreference"]`，默认 `{"value":"system"}`，且**只在页面启动时**读取 `prefers-color-scheme`——深色启动探针页面直接渲染深底 `rgb(21,21,23)`；页面启动后再翻转 scheme，body 背景保持白色不跟。
- 结论：宿主的 boot-time 接线（`ApplyWebProfileTheme` 在 `Navigate` 之前调用）与站点机制正好契合，**无需注入官网主题同步脚本**；中途切主题在下次页面加载（刷新/新对话导航）时生效，不自动刷新以保护会话草稿。此边界记入 chat-modes.md 与用户文档口径。

## 遗留

- 官网主题中途切换需刷新页面才生效（D4 实证边界，非缺陷）。
- 深色模式下自定义模式较多（列表超出可视区）时仍会出现系统滚动条（WinForms AutoScroll 不随应用主题），属已知限制，记录于 ui-style-guide。
- 目标语言选择（如翻译模式指定目标语言）仍留待后续。
