# ADR-0005 阶段合同

2026-11-05。

| 阶段 | 范围 | 门禁 |
| --- | --- | --- |
| S1 模式声明头 | Directive 常量、Prefix/Compose 接线、chat-modes.md 同步 | `run-tests.ps1` 全量；新增断言：三条链（Prefix/取词 Compose/注入 catalog）首行为同一 Directive、normal 非空头、头不随 modes.json 覆盖或自定义变化、`All` 六模式文本与 DSH 提取指纹不变、空草稿零注入保持、模式切换 marker 前缀替换回归；chat-mode-tests.cjs 增 normal 带头与切换替换用例；DSH 插件 `npm test` 兼容回归；真机验收 FR-03：总结数轮→切普通对话发一条闲聊，回复不带总结腔且可引用上文；翻译→总结交叉同样验收 |

通用约束（同 ADR-0004）：隔离偏好目录（测试脚本既有 root 重定向）；不动个人安装；真机验收截图留档 `docs/testing/`。2026-11-06 验证约束更新：失败必须定位到实现、环境或夹具并保留证据；必要检查不允许仅凭重跑变绿判定通过。真机验收不发送真实材料之外的敏感内容，验收会话用后即弃。
