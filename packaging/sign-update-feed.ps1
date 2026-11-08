param([string]$ZipPath,[string]$OutputDirectory,[string]$PrivateKeyPath=(Join-Path $env:LOCALAPPDATA 'DeepSeek-Reflex/publishing/update-signing-key.xml'),[int]$ValidDays=30,[switch]$Status)
$ErrorActionPreference='Stop'
if($Status){
 $r=Invoke-RestMethod 'https://test.doitbenai.cloud/reflex/updates/stable.json' -TimeoutSec 20
 $d=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($r.payload)) | ConvertFrom-Json
 $remaining=[DateTimeOffset]::FromUnixTimeSeconds($d.expires)-[DateTimeOffset]::UtcNow
 [pscustomobject]@{Version=$d.release.tag_name;Expires=[DateTimeOffset]::FromUnixTimeSeconds($d.expires).ToString('o');DaysRemaining=[math]::Floor($remaining.TotalDays)} | ConvertTo-Json
 if($remaining.TotalDays -lt 7){throw 'Signed feed needs renewal; status information is not a substitute for client signature verification'}
 return
}
if($ValidDays -lt 1 -or $ValidDays -gt 30){throw 'Validity must be 1..30 days'}
if(-not $ZipPath -or -not $OutputDirectory){throw 'ZipPath and OutputDirectory required when signing'}
$zip=(Resolve-Path -LiteralPath $ZipPath).Path
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Use a new signing output directory'}
New-Item -ItemType Directory -Path $output | Out-Null
& (Join-Path $PSScriptRoot 'test-package.ps1') -ZipPath $zip -OutputDirectory (Join-Path $output 'verified')
$version=([IO.File]::ReadAllText((Join-Path $output 'verified/DeepSeek-Reflex/VERSION'))).Trim()
if([IO.Path]::GetFileName($zip) -ne "DeepSeek-Reflex-$version-Windows-x64.zip"){throw 'Archive name does not match payload version'}
$size=(Get-Item -LiteralPath $zip).Length
if($size -le 0 -or $size -gt 64MB){throw 'Package size outside updater limits'}
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$issued=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$payload=[ordered]@{schema=1;issued=$issued;expires=($issued+86400L*$ValidDays);release=[ordered]@{tag_name="v$version";draft=$false;prerelease=$false;body="新增签名静态更新源，修复共享网络下更新检查受限流的问题。保留现有设置和登录状态。";assets=@([ordered]@{name=[IO.Path]::GetFileName($zip);size=$size;digest="sha256:$hash";browser_download_url="https://test.doitbenai.cloud/reflex/updates/releases/$version/$([IO.Path]::GetFileName($zip))"})}}
$bytes=[Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json -Depth 8 -Compress))
$rsa=[Security.Cryptography.RSA]::Create()
try {
 $rsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $PrivateKeyPath)))
 if($rsa.KeySize -ne 2048){throw 'Unexpected signing key size'}
 $signature=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
 $envelope=@{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($signature)} | ConvertTo-Json -Compress
 [IO.File]::WriteAllText((Join-Path $output 'stable.json'),$envelope,[Text.UTF8Encoding]::new($false))
 $release=Join-Path $output "releases/$version";New-Item -ItemType Directory -Path $release -Force | Out-Null
 Copy-Item -LiteralPath $zip -Destination $release
 [ordered]@{Version=$version;Sha256=$hash;Bytes=$size;Issued=$issued;Expires=$payload.expires} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'receipt.json') -Encoding utf8
 'PASS complete package verified and manifest signed; private key stayed local'
}finally{$rsa.Dispose()}
