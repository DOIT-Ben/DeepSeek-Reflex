# ADR-0004：自定义聊天模式提示词与网页主题接线

**ACCEPTED · 2026-11-04 立项，2026-11-05 实施完成并验收**（S1/S2 证据见 [审查与实施记录](0004-review.md)）。基线 main（1.0.18 之后）。

来源、影响与阶段/测试合同见 [来源与影响](0004-impact.md)、[阶段合同](0004-stage-tests.md)。方案门禁和实施进度的唯一所有者是 [审查与实施记录](0004-review.md)。本 ADR 不修改 ADR-0001/0002 的受检内容。

## 目标与边界

用户要求：①每种聊天模式可自定义嵌入提示词，并能新增自己的模式；②设置里的主题开关要能作用到内嵌网页（此前只重绘宿主）；③设置界面按填充式风格整改（另行记录于 `docs/ui-style-guide.md`，不属本 ADR 决策范围）。

| 标识 | 来源 | 可观察结果 | 决策/阶段 |
| --- | --- | --- | --- |
| FR-01 | 用户 | 内置五种带提示词模式的指令可编辑、可逐个恢复出厂文案；「普通对话」保持原文语义不可编辑 | D1/S1 |
| FR-02 | 用户 | 可新增自定义模式（名称+提示词，上限 10 个），出现在模式菜单，可编辑、可删除 | D1/S1 |
| FR-03 | 用户 | 覆盖与自定义模式对持续注入、取词三动作、DSH 桥组合一致生效 | D2/S1 |
| FR-04 | 用户 | 删除正使用的自定义模式后当前模式回退普通对话并重注入 | D3/S1 |
| FR-05 | 用户 | 主题切换同时驱动内嵌网页深浅色；跟随系统时与宿主同源解析 | D4/S2 |
| TR-01 | 仓库边界 | 无新增网络调用、无正文外发；modes.json 只存本机偏好目录 | D1 |
| TR-02 | 技术设计 | C# 5/.NET 4.8；存储沿用 Preferences 原子写与坏文件降级约定（同 commands.json） | D1 |
| TR-03 | 实施约束 | DSH 插件不重建不发布；未知 mode id 一律回退普通对话 | D2 |

## D1：mode 覆盖的存储形态

新文件 `webview-host/ModeCatalog.cs` 管理 `%APPDATA%\cloud.doitbenai.deepseek-reflex\modes.json`：`{Version:1, Overrides:[{Id,Instruction}], Custom:[{Id,Name,Instruction}]}`。只存差异：没有覆盖条目的内置模式不落盘；「normal」永远不可覆盖（加载与保存双侧过滤）。名称非空≤16 字且不与任何内置或既有自定义重名；指令非空≤2000 字；自定义上限 10 个。文件不可读或版本未知时只加载内置并在设置页提示，原文件保持不动——与 commands.json 完全相同的容错合同。

`ChatModes.All` 保持出厂六模式不动（DSH 插件 `build.mjs` 在构建期从该文件正则提取，改动会破坏指纹校验）。新增 `ChatModes.Effective`：内置按覆盖替换指令、追加自定义模式；`Find` 改查 Effective，未知 id 回退「普通对话」。应用启动时与设置保存后由宿主 `ChatModes.Apply(ModeCatalog.Load())` 重建。

设置对话框新增「聊天模式」页：列表（普通对话固定行、内置行带编辑/恢复默认、自定义行带编辑/删除）+ 名称/提示词编辑卡 +「新增模式」。入口两处：设置主页动作行「聊天模式」按钮；模式菜单底部「自定义提示词…」。未保存离开沿用指令页的确认拦截。

## D2：三条消费链与插件边界

- 持续注入：`ChatModeInjector.Script` 的 catalog 改由 `ChatModes.Effective` 生成，覆盖与自定义模式的前缀随之生效。
- 一次性取词：`PromptInserter.Compose` 经 `ChatModes.Find` 间接消费 Effective——内置翻译/解释/提问被覆盖后，取词栏措辞与模式栏保持一字不差（延续 ADR-0002 的"单一措辞"约定）。
- DSH 桥：`handoff.js` 的六 id 白名单与插件菜单不变；自定义模式只出现在宿主小窗。桥传内置 id 时由宿主 `ChatModes.Prefix` 组合，覆盖文本自动生效；未知 id 宿主侧回退普通对话，天然兼容旧插件。

## D3：删除与回退语义

设置保存后宿主重载目录；若 `window-settings.json` 记录的 ChatMode 已不存在（自定义模式被删），宿主把当前模式写成「普通对话」并重新注入，不弹错误。`WindowSettings.Load` 的归一化经 `ChatModes.Find` 天然继承该回退。

## D4：网页主题接线

WebView2 初始化后与宿主 `ApplyTheme` 时设置 `CoreWebView2Profile.PreferredColorScheme`（Dark/Light，跟随系统时先经现有 `ResolveTheme` 解析，保证外壳与网页永不打架）。弹窗与快捷答案窗共享同一 profile 自动跟随。接口调用失败（旧 Runtime/浏览器进程退出）仅放弃本次设置，不中断初始化——沿用 BUG-012 的教训：不为视觉增强冒初始化风险。若真机验收发现官网自身主题设置钉死而不响应 prefers-color-scheme，再评估是否注入官网主题同步脚本（本轮不做）。
