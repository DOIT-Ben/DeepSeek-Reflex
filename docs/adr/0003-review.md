# ADR-0003 审查与实施记录

2026-10-25，基线 `main@ac23e84a9e8a80cfe1e9670317594e0c6b2437b2`，起点工作树干净。用户授权本地按 ADR 逐个修复 #1–#5，不含提交、推送、发布、替换安装和 GitHub 关单。

## 方案门禁

第1轮独立 `adr_reviewer / adr0003_plan_review`：PASS，无阻断意见；审查者未参与撰写和影响检查，复算下列原始字节一致。主线实施前再次复算一致，重大待决项为零，记录可实施门禁。本文件不属于受检字节。

```text
ADR=docs/adr/0003-reviewed-issue-remediation.md@sha256:dfbf2fc3a5878c3b659d83c1038a996ef860022bacf5658bb637a0d4e29be79c
PHASE=docs/adr/0003-stage-tests.md@sha256:3ce3c3d047bfd19943a439be05236632558f186a50751235f0cec456be7506b9
TEST=docs/adr/0003-stage-tests.md@sha256:3ce3c3d047bfd19943a439be05236632558f186a50751235f0cec456be7506b9
```

沿用已在干净起点选定的活动主工作树。进入源码实施前仅新增本任务S0的四份ADR文档，无来源不明改动，不需要再次选择工作树或提交这些文件。

## 唯一阶段状态

| 阶段 | 状态 | 证据 |
| --- | --- | --- |
| S0 | PASSED | 第1轮方案PASS，实施前指纹一致 |
| I1 / #1 | PASSED | P01验红转绿17项，独立实现第1轮PASS，依赖产物复核完整 |
| I2 / #2 | PASSED | 验红转绿39项，独立实现第1轮PASS |
| I3 / #3 | PASSED | 第2轮独立PASS，负例及非空绘制154项通过 |
| I4 / #4 | PASSED | REWORK_1后专项32项、第2轮独立PASS、原模态断言重验通过 |
| I5 / #5 | EVIDENCE_GAP | 修复/专项18项/实现审查PASS，N06完整原生回归尚未稳定通过 |

## 技能与环境

用户点名 ADR Skills。应用中的五个 ADR 符号链接仍指向兼容入口，兼容入口的 F 盘目标不可用；本轮直接读取唯一主库 D:\Skills 中对应 Skill，不修改开关或链接。源码身份和 Git 远程已核对为 DOIT-Ben/DeepSeek-Reflex；NSIS 现有本机编译器可用，不需要安装新依赖。

## I1 / Issue #1

实现仅删除setup.nsi的旧目录自动递归清理，明确保留旧程序文件。新增`packaging/tests/test-installer-flow.ps1`提取真实必需安装段，由现有NSIS编译器执行；实际文件操作保留，注册表写被剔除、注册进程为替身，LOCALAPPDATA/开始菜单目录重定向至新临时目录。

共3次有界执行：第1次fixture因SetAutoClose放在节外编译失败（`%TEMP%/Reflex-ADR0003-I1-red.log`），去掉无用指令；第2次在未修源码上执行，`same-old preserves new executable`如期验红（`-red2.log`）；第3次修复后17个保全断言通过（`-green.log`）。同旧目录、旧目录子目录、新旧独立、旧Float存在及用户哨兵均覆盖。没有启动真实安装器或写个人安装/注册表。

独立实现审查`adr0003_impl_review`第1轮PASS，无阻断意见；另只读核对四个场景dependency.txt完整。受检setup.nsi指纹`afb0cf41362ea114b4ac3fde111663859c723414c28fcf04ba8bf538ed2ccd3b`，fixture指纹`840bd839b5771811319ba368c8bb580a19f41ed4888fd23c8c7c3d55765b01eb`。P01文档、实现、验证、审查均完成，允许进入I2。依赖文件自动断言作为I2扩展补齐。

## I2 / Issue #2

在旧启动器FileClose后显式Goto install_files，原三个占用失败路径不变。fixture新增Upgrade阶段并补齐P01依赖内容断言。共2次执行：`%TEMP%/Reflex-ADR0003-I2-red.log`在old-launcher permits installation验红；`-green.log`的P01+P02共39项通过，包含三种真实写访问锁、未锁旧启动器/两种旧EXE及新装。

独立实现第1轮PASS，未修改文件或运行测试。setup.nsi指纹`6778335bda7ee8a141ee330a960f59244b656af76307228bccb9a26ef7dce415`，fixture指纹`9032c843b6f974c3d52b00ab1f4d6ab778cc720a4d390aded178e0f440e78b3d`。这是隔离NSIS逻辑验收，不冒充正式用户升级；允许进入I3。

## I3 / Issue #3

TrayMenu量测与文字绘制统一NoPadding/NoPrefix基础标志，保留原省略、勾选及菜单尺寸策略。新增隔离原生runner与IssueRegressionTests：编译真实host源码，仅生成Preferences初始化直接指向测试目录的副本，不执行个人APPDATA迁移；STA真实消息循环，进程有界退出。

共2次执行：`%TEMP%/Reflex-ADR0003-I3-red/test.log`原代码在首项普通对话完整绘制比较失败；`-I3-green/test.log`修复后103项通过。真实renderer与完整文字参考逐像素比较，含六模式、字面A&B、超长名称、1/1.5/2倍率及checked/unchecked，另查勾选槽与宽度上限。初次子进程中文默认编码乱码只影响日志显示，已在测试Main设置UTF8。

独立实现第1轮REQUEST_CHANGES：超长100字令Side过大、比较矩形负宽，EqualPixels空循环返回true。103项结果中该部分失效，不能据此过阶段。生产修复本身未发现问题。测试已改为拒绝空/负/越界区域并加入三项负例；超长样例改为仍有正文字区的32字，额外检查实际/参考均有字形且确实达到宽度上限。待第3次执行与第2轮复审。

未开始I4/I5源码。相关原SettingsTests将通过同样隔离初始化的Regression入口在收尾执行，未以本专项冒充完整原生回归。

I3第3次执行`%TEMP%/Reflex-ADR0003-I3-final/test.log`154项通过。独立第2轮PASS，已查看真实超长省略图，原假绿关闭；生产TrayMenu指纹仍`0895e81a7ca887adece6934001f9b814026e9b2538d7adb859727c9c3d98d42a`，测试类指纹`d8bcc7c2bcffbc598f055fe9b8f6c78b2d7fe93d4c343d2b0689bed42ca3b2dd`。共3次执行、2轮审查，门禁满足后进入I4。

## I4 / Issue #4

ApplyDialogHeight对请求矩形先复用FitBounds，工作区来自当前窗口屏幕，沿用当前scale；没有改共享拟合算法或持久化。原生case增加实际页面导航、有限空间、固定按钮、滚动、编辑稿、不漂移及保存指令返回/直接指令启动。

共2次执行：`%TEMP%/Reflex-ADR0003-I4-red/test.log`在自有窗口2.736倍参数下，返回general高度1581超出实际1368工作区，断言验红；`-I4-green/test.log`31项通过。测试仅调用该窗ApplyDpi，不修改Windows显示设置；另有100/150/200%合成负坐标拟合用例。仍用编译时隔离Preferences，未读写个人资料。第1轮独立审查已委派，未开始I5。

I4第1轮独立PASS，构造/两种页面/保存返回共享入口及控件初始化顺序已核对。SettingsDialog指纹`92db00ff4cda0d0d4215d5137038f75e6af6e866da184dd242a9d97b9fde3aa5`，测试类`a7aa11046c0c2a76e381f2a49aaf3d09664c78191e62c4fbdc08522fe463ce91`；允许进入I5。

收尾时撤销上述I4当前通过状态：原SettingsTests默认/高DPI均在模态topmost断言失败。完整HEAD基线在更早的原生灰边断言失败，不能单凭此认定modal历史失败。临时聚焦探针直接调用原SettingsTests的ModalCornersAbove谓词和真实ChatWindow.ShowSettings，HEAD基线三轮全部通过，旧I4实现的置顶轮却为expectedTop=True、actualTop=False（`%TEMP%/Reflex-ADR0003-modal-probe/baseline.log`、`default.log`）。

根因是ApplyDialogHeight在构造中调用Screen.FromControl，提前创建尚无owner的HWND。REWORK_1只在IsHandleCreated为false时改用Screen.FromRectangle(Bounds)，已有句柄仍用FromControl；所有入口继续共享FitBounds，ADR产品决定和冻结输入不变。新增构造不创建HWND断言。I4第3次专项执行`%TEMP%/Reflex-ADR0003-I4-rework/test.log`32项通过，等待第2轮独立审查及原模态回归重验。探针初次编译因机器LIB含失效MSVC路径失败，按现有build/runner的做法临时清空并恢复LIB后，第2次编译成功；未修改系统环境。

## I5 / Issue #5

Send在await前捕获trim后的submittedContact，BuildJson及成功写偏好使用同一快照，包括空字符串；失败、关闭保护和本地存储失败时的成功语义保留。共2次专项执行：`%TEMP%/Reflex-ADR0003-I5-red/test.log`在清空记忆验红，`-I5-green/test.log`18项通过，全部请求为mock。涵盖重开回填、清空、失败不写、await期间编辑、存储写失败不重发、关闭窗口的延续保护。

第1轮独立实现PASS，无阻断发现；FeedbackDialog指纹`1059250c176b2726d10dc95450bec869d906ec5cf67e9d141f4a0aa71d87ee08`，当时测试类`d03afabdc900e6a03510f6b7df47b29408ffa326b566bb49fee2ba7fd800dba5`，runner `e2b32ae6adb2ff416b73a7d7c52356e9b74b8f02945f9361a49099c8971acd9f`。专项及审查有效，但I4被重开，I5保持BLOCKED_BY_I4，不以专项PASS冒充全部结项。

## 收尾重验 / N06

I4第2轮独立审查PASS。修正源码指纹`d6f844c978756c7e88c2c602872cae9b577b1c7e9e1b37ade6977d8fc0610001`；测试类最终`cbebf37fc6001f862ef6be77103b95cd14d885ad1f5d66d68b69f7a4bf662b60`。聚焦原模态谓词的修正版本三轮全部通过（`%TEMP%/Reflex-ADR0003-modal-probe/fixed.log`）；修正后的完整高DPI运行也通过原settings modal聚合断言。I4恢复PASSED，解除其上游阻塞，I5因下述完整回归证据缺口保留EVIDENCE_GAP。

| 检查 | 尝试及结果 | 证据 |
| --- | --- | --- |
| 离线生产编译 | 首次通过；I4重修后第2次也通过 | `%TEMP%/Reflex-ADR0003-build.log`、`Reflex-ADR0003-build-final.log` |
| 原完整SettingsTests / default | 2次失败：重修前modal native band；重修后原生直边像素断言。后者日志先打印edges=True，随后相同谓词失败，存在采样时序不稳定 | `Reflex-ADR0003-regression-default/test.log`、`-default-rework/test.log` |
| 原完整SettingsTests / high-dpi | 2次失败：重修前modal native band；重修后已通过modal，失败在mode pill关闭判定 | `Reflex-ADR0003-regression-highdpi/test.log`、`-highdpi-rework/test.log` |
| 原完整HEAD基线 / default | 1次，失败在更早的native deactivation直边像素断言，未到达modal；不能代替modal基线 | `Reflex-ADR0003-regression-baseline/test.log` |
| 原commands-only / HEAD | 1次，150项通过 | `Reflex-ADR0003-baseline-commands/test.log` |
| 原commands-only / 本轮 | 第1次在mode pill断言失败；I4重修后第2次仍同处失败；第3次仅在临时测试副本添加2条Console日志，保留原assert、输入和DoEvents，150项通过 | `Reflex-ADR0003-existing-commands/test.log`、`-existing-commands-rework/test.log`、`-click-diagnostic-run/test.log` |

上述日志路径均相对`%TEMP%/`。第3次诊断打印目标点=Cursor.Position=Control.MousePosition，位于pill且不位于menu；关闭返回true且openMenu为空。这只证明该次运行有效通过，不能把增加日志后的绿灯扩大成无日志稳定通过，也不能断言此前pill失败已由本次修复导致或排除。仓库内原SettingsTests.cs原始字节未修改，指纹`25928237b6017c8d1ec8308b6475ac47cf2486ff43b0eda578539c227d716709`。保留所有失败，不降低断言；不把完整套件标绿。

P01/P02最终39项、N03最终154项、N04重修32项、N05最终18项，共243项专项检查通过；其生产源码与对应检查函数未被后续无关阶段覆盖。第5项联系方式修复及第1轮独立实现审查仍有效，但N06闭环未完成，不能宣称五阶段全部通过。未解决项是原生直边采样及mode pill无日志点击回归的稳定性/因果；恢复条件为不放宽原断言的有效完整回归证据，必要时另走明确缺陷范围，不静默扩展本轮五个Issue。

全部测试使用自有隔离目录及窗口，Preferences编译时初始化即隔离；反馈均为mock，无个人资料迁移、官网对话或真实反馈。当前版本未变，不提交、不推送、不发布、不替换安装，不评论或关闭远端Issue。旧程序文件有意保留，后续自动清理需独立迁移方案。

最终只读核验：四份本轮ADR文档的本地链接均存在；`git diff --check`通过，新增文本逐行尾空白检查通过。暂存路径0，HEAD仍为起点ac23e84a9e8a80cfe1e9670317594e0c6b2437b2，origin仍为已核对的DOIT-Ben/DeepSeek-Reflex；工作树只含本轮四个生产文件、四份ADR文档和三个测试文件。自有`Reflex-ADR0003-`测试进程0，隔离证据保留。冻结ADR/合同指纹再次复算一致。

独立审查者最后只读核对证据边界：PASS，不计新增实现审查轮次；同意I4恢复PASSED、I5维持EVIDENCE_GAP，强调诊断150项不能替代完整稳定回归，未将pill问题归类为已证实历史故障或环境跳过。本轮五个Issue均已有代码修复及各自专项/实现审查，但完整ADR交付仍未完成，不声明五阶段全部验收通过。

## 后续提交推送授权 / 2026-10-25

用户随后明确要求“提交推送吧。兄弟。”，授权提交本轮11个源码、测试及ADR文件并推送至已核对的`DOIT-Ben/DeepSeek-Reflex`仓库`main`。前述不含提交推送、暂存0、HEAD起点均为实施验收结束时的历史快照。本次交付保留I5的EVIDENCE_GAP及完整原生回归未通过的事实，不发布、不替换安装、不评论或关闭Issue；提交说明仅关联Issue，不触发自动关单。

提交前再次核对：有效push地址唯一且为`https://github.com/DOIT-Ben/DeepSeek-Reflex.git`；fetch后本地main与远端main均为ac23e84a9e8a80cfe1e9670317594e0c6b2437b2，无待推送旧提交；有效hooks目录无启用脚本。冻结ADR/合同指纹不变，七个新增文件经独立只读核对均为小型UTF-8文本，未发现意外个人资料或凭据。推送会触发既有构建CI；该工作流只有contents:read及构建产物上传，不创建GitHub Release。
