param([switch]$Background)
$ErrorActionPreference = 'Stop'
$Exe = Join-Path $env:LOCALAPPDATA 'Programs\DeepSeekFloat\DeepSeekFloat.exe'
if (-not (Test-Path -LiteralPath $Exe -PathType Leaf)) { throw 'DeepSeek 小窗尚未安装；请先运行构建与 install.ps1。' }
$Launch = @{FilePath=$Exe; WorkingDirectory=(Split-Path -Parent $Exe); WindowStyle='Hidden'}
if ($Background) { $Launch.ArgumentList = '--background' }
Start-Process @Launch
