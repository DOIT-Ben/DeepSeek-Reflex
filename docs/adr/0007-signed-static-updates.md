# ADR-0007 签名静态更新源

PROPOSED，2026-11-07。基线 HEAD fbbcee8284346806e40100b1da37393dd422f6c6 加本聊天未提交的 ADR0006 更新器及布局工作；保留并复用这些已归属本任务的修改，沿用活动树，不提交、推送或创建公开 Release。

来源：用户已同意静态版本清单、数字签名、自动生成发布流程，并要求“那你来试试啊兄弟，如果不行用我们hk服务器吧？”；授权实现和在既有 HK 站点验证更新服务。不得改其他 HK 业务、代理、数据库、登录资料，不新增收费资源或代码签名证书。

## 影响证据

UpdateFeed 当前固定请求 GitHub latest REST，无凭据共享出口多次返回403 remaining=0。UpdatePackage 验证完整21项ZIP，UpdateTransaction只写11项程序文件、独立worker原子替换及恢复；这些职责不变。UpdateCoordinator只依赖Check返回UpdateRelease，继续复用，不将签名或部署堆入ChatWindow。本项目现有网站入口只有已部署反馈服务的test.doitbenai.cloud（server/reflex-feedback/README.md），README下载入口是GitHub Releases，未发现独立官网静态托管。这个现有项目域名本身就在HK，故优先复用既有网站与用户允许HK是同一落点，并非跳过已知独立官网。已有HK nginx test.doitbenai.cloud 443 有效证书，/reflex/feedback独立路由，其余根路由代理现有业务。现场nginx -t通过，磁盘可用4GB。新增签名验证独立模块，UpdateFeed保留旧GitHub解析作为测试/兼容代码，生产检查切换静态源，不在失败时回落到未签名来源。

## 合同与选择

FR1 客户端无凭据读取 https://test.doitbenai.cloud/reflex/updates/stable.json，验证RSA2048/SHA256 PKCS1签名后才能信任清单。公钥内置，私钥只留本机专用用户目录，限定当前用户和SYSTEM读取，不进入仓库、服务端、安装包或日志。应用不接受公钥/地址命令行覆盖。首版单钥，换钥需新版客户端。

FR2 外层JSON是payload和signature两个Base64字符串；签名覆盖payload原始UTF8字节，不重新序列化。外层最大1MiB，payload最大512KiB、签名256字节。payload含schema=1、issued/expires UTC Unix秒和release（现有稳定版tag/body/assets结构）。有效期最长30日；未来签发允许5分钟时钟偏差，过期/无效/重复JSON成员拒绝，错误不得显示成没有更新。本机时间错误提示校对时间。已保存最高有效版本，清单降级或同版本换摘要拒绝；写入防回放状态失败时阻止下载，文件单独在LocalAppData，不改既有用户配置。

FR3 仍严格vX.Y.Z、size>0且<=64MiB、sha256及唯一x64包；只接受精确同站 /reflex/updates/releases/X.Y.Z/DeepSeek-Reflex-X.Y.Z-Windows-x64.zip。所有重定向继续HTTPS白名单、最多5次，镜像URL路径受签名且精确核对，不扩大任意host。完整ZIP及二次worker校验、版本不降级、未保存保护保持。

FR4 发布脚本验证版本和ZIP21项完整性后，生成限期签名清单与只读版本目录。先上传本次新版本到HK独立暂存目录，服务端核对ZIP SHA/字节，禁止覆盖不同摘要的既有版本；包发布成功后原子替换stable.json。文件公开，无Cookie/用户数据。限制单包64MiB、最多保留5个非活动版本，自动部署不删除历史，超过预算停止并报告。私钥不传HK。清单续期与每次发版使用同一脚本；发布负责人为项目维护者（用户或本聊天受托执行者），每14日续期一次，最迟到期前7日执行。发布脚本提供只读-status检查和明确非零失败退出，保存脱敏续期收据；客户端清单剩余不足7日时提示维护即将到期并保留公告入口。过期会拒绝更新而不影响聊天；负责人须用原私钥重新签署同版本同摘要并原子替换，客户端接受新有效期而不接受不同包摘要。本轮验证一次同版本同摘要续期（issued递增、expiry延后）及过期拒绝和恢复。自动发版脚本可调用不代表已经建立无人值守续期；人工14日维护是明确运行条件，交付时告知并登记项目运营待办，不创建未授权的定时任务。无CI私钥凭据授权时不自动写GitHub Secrets，而交付可自动调用的脚本及手动运行入口。首次新协议客户端使用新版本1.0.21，构建完成后固定ZIP摘要再发布，绝不重打1.0.20。旧1.0.20保留历史身份且不在首次稳定清单发布。本机通过已授权的保护性手动安装引导到1.0.21；随后当前版查询应诚实Current，隔离1.0.20比较器应Available。包内exe、VERSION、清单和本机版本均须1.0.21且摘要对应同一构建。

FR5 仅在当前HTTPS server block增加独立 /reflex/updates/ 静态location，目录列表关闭、只读GET/HEAD、清单no-cache、版本包immutable。不反代GitHub API，不依赖其匿名额度。不新增常驻进程。不将nginx解析正文或静态SHA冒充密码学签名。备份配置，nginx -t后graceful reload，失败恢复精确配置并重验；不重启业务应用。

替代：GitHub Pages/对象存储需额外站点或账号部署权限，当前现成HK HTTPS目录可满足；仅加Token不能安全地把发布者凭据放客户端，排除。HK不是CDN，不承诺全球加速；后续可迁移同一签名协议，不在本轮接入新收费CDN。

## 单阶段实施与验收

S1映射FR1–FR5：签名模块、Feed适配、独立防回放状态、签名/发布脚本、nginx隔离路由与文档。先独立ADR审查PASS，实施后独立代码审查，最多3结论2次修复；方案最多2轮。状态唯一记录0007-review.md。

测试：签名正确/错误公钥/更改payload/签名短长/JSON重复字段/畸形Base64/超限/schema/时间边界/过期/未来/版本回放/同版摘要变化/保存失败；包来源/路径/大小/摘要旧门禁；保留原GitHub解析及安装28项、设置焦点回归。旧签名或篡改版本必须失败。使用真实签名fixture独立临时密钥，不读发布私钥做日志。网络使用真实HK GET下载、签名、ZIP验证和诚实的Current状态；隔离旧版本比较检测Available，不发布虚构版本。nginx前后现有根页与反馈GET结果不变；故障切换清单及回退只在测试目录，稳定清单发布保持真实版本。独立审查通过后更新本机，保全配置/Cookies、验证设置入口与真实在线检查；不发送聊天或反馈。实际断电等未测物理边界不计通过。
