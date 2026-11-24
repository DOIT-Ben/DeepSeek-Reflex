# 签名更新源运维

决策与验收：ADR-0007。客户端从 `https://test.doitbenai.cloud/reflex/updates/stable.json` 获取限期签名清单，版本包位于同一站点 `releases/<version>/`。该站在既有 HK 主机，无新增后端进程或数据库；不是 CDN，不承诺跨区域加速。

客户端内置 RSA2048 公钥，对原始 payload 做 SHA256/PKCS1 验签。私钥只在发布者本机 `%LOCALAPPDATA%/DeepSeek-Reflex/publishing/`，当前用户和 SYSTEM 专属 ACL；不得上传 HK、GitHub、安装包或日志。保管私钥与其受控备份；丢失或泄漏需更换客户端内置公钥并人工引导更新。清单签名不等于 Windows Authenticode 代码签名。

## 发布及续期

在项目根目录 PowerShell 7 执行。先构建新的唯一版本；不要用同一版本重打包：

```powershell
./packaging/build-release.ps1 -Offline -OutputDirectory <发行目录>
./packaging/sign-update-feed.ps1 -ZipPath <已验证ZIP> -OutputDirectory <新的签名输出目录>
./packaging/publish-update-feed.ps1 -SignedDirectory <签名输出目录>
./packaging/sign-update-feed.ps1 -Status
```

签名脚本检验21项包及摘要；发布脚本先用客户端公钥核对签名与收据，私钥不参与远程命令。服务器互斥校验包、原子发布整个版本目录，再原子替换 stable；拒绝降级、同版不同摘要和过旧续签。已有不完整目录或未知内容保留并报错，不自动清理。最多6个版本目录，达到预算即停止，人工确认保留范围后再处理。

签名时从 `docs/releases/<包内版本>.md` 读取该版本的更新说明，客户端会显示同一段文字。缺少说明文件的历史版本显示产品名和版本号。

清单最长有效30天。**负责人：项目维护者或明确受托的发布执行者；每14天续期一次，最迟到期前7天。** 续期使用相同已发布ZIP和原私钥重新执行签名及发布命令，必须保持版本和包摘要，签发时间须递增。无需重打安装包。脚本 `-Status` 返回到期日期，不足7天以非零状态提示维护；客户端也会提示。状态脚本读出的远程字段不代替客户端签名验证。

每次发布或续期后，从最新签名收据的签发时间起计算下一次人工续期时间，并用 `-Status` 核对线上到期日期。

清单过期或被篡改时客户端拒绝更新，聊天和已安装版本仍可使用。发布者续签同版同包可恢复；不能靠关闭验签、删除高水位或回落未签名来源修复。旧版本客户端首次需要手动安装一次1.0.21以获得新协议。

若本机 PowerShell 的环境代理出现 TLS EOF，先核对网络；本轮客户端 .NET Framework 路径已真实通过。仅在确认直连可达时，可对这一次状态诊断指定 `Invoke-RestMethod -NoProxy`（例如在独立 PowerShell 进程中设置对应的默认参数），不要修改系统或持久环境代理。状态查询失败必须如实报错，不能据此关闭清单验签。

## HK 路由与恢复

仅增加 `/etc/nginx/reflex-updates.conf` 对应的静态路径，不改变现有根代理、反馈和私有路由。`server/reflex-updates/install-route.py` 在复制到 `/opt/reflex-updates/` 后执行；对既有站点配置备份、原子修改，`nginx -t` 后 graceful reload，失败恢复原文件并重验。不要用重启应用代替 nginx reload。

静态根 `/var/www/reflex-updates/`；只开放清单与版本包，禁止目录列表，任务暂存和发布脚本不可通过 HTTP 访问。保留原 stable 备份用于核对，但不要向有高水位客户端发布较旧版本；应修复原包或使用更高的新版本。回滚路由必须使用本次 `site-before-*` 精确备份，并检查根页与反馈 GET 状态与变更前一致。
