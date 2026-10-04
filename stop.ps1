$ErrorActionPreference = 'Stop'
$Exe = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\DeepSeekFloat\DeepSeekFloat.exe'))
# Only stop this installation, never other processes with a similar name.
Get-Process -Name 'DeepSeekFloat' -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and [IO.Path]::GetFullPath($_.Path) -eq $Exe
} | ForEach-Object {
    Stop-Process -Id $_.Id -ErrorAction Stop
    if (-not $_.WaitForExit(10000)) { throw 'DeepSeek 小窗未能及时退出。' }
}
Write-Output 'DeepSeek 小窗已退出。'
