# ADR-0007 状态

方案R1待独立审查。实施S1 PROPOSED。用户授权沿用本聊天目标，已验证HK HTTPS及独立静态路由可行；尚无远程写入。

R1 REQUEST_CHANGES（Kuhn）：新协议首包身份、续期闭环、现有网站身份。已修订为1.0.21唯一构建、明确人工续期及验证、现有反馈域名即HK站点。R2送审前同一主工具调用已先Get-FileHash固定63a58bd63d66abb7530550b6ccfbb08233ec324e3101e6cdc3ab2900b5f852bc，但消息漏传。R2审查确认三项均处理，仅因未传预期hash返回REQUEST_CHANGES；现补全原R2行政输入，未修改ADR字节，不重置轮次，待其完成比对。PHASE/TEST embedded-in-ADR。未进入源码实施或远程写入。

R2最终PASS，已比对预先固定hash一致，无剩余方案阻断。用户授权有效；进入S1实现。工作树仅本聊天已归属变更，沿用活动树，保留ADR0006全部工作。

S1实现R1 REQUEST_CHANGES：发布目录中断恢复及并发stable回退。已使用目录整体原子rename和fcntl全事务锁修复，HK隔离6项通过。R2 REQUEST_CHANGES：同版本续期可缩短到期日。补入expires严格延后及相等/倒退负例，HK隔离7项全部通过，deployment-qa-final.log。R3最终复核待结论。签名26、worker28、General两套各76、UpdateUI两套各89通过；均为本轮实际执行。尚未部署live路由及清单。

S1独立实现R3 PASS（Mencius），续期签发和到期时间均严格递增，7项隔离部署故障通过。已部署HK独立静态路由、固定1.0.21包及签名清单，live/signed-feed.log真实无凭据Check、下载、21项包及SHA/品牌验证通过；同版同摘要实际续签并在原高水位状态上重验通过renewal-live.log。HK nginx-t通过，现有根页200、feedback GET404与前一致，私有暂存HTTP404。运维PS状态命令在本机环境代理下TLS EOF；单次NoProxy诊断成功，未更改环境代理或系统配置；正式.NET客户端无需改变代理即可真实验证。用户已确认处理草稿，保护性本机安装进行中。

S1 PASSED。本机保护性安装1.0.21完成，14项替换前后保护文件不变，实际UI在线检查显示当前/最新1.0.21且已最新，登录页保留。live-comparison生产协调器以隔离current20读取真实signed21进入Available。包及签名摘要一致。所有预定逻辑验收完成，人工续期条件见docs/operations/update-feed.md与docs/testing/2026-11-07-signed-static-updates.md；无GitHub Release/commit/push或无人值守任务。
