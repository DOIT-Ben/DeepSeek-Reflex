param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$BuildDir = Join-Path $Root 'webview-host\dist'
$ExeSrc = Join-Path $BuildDir 'DeepSeekFloat.exe'
$IconSrc = Join-Path $Root 'webview-host\assets\icon.ico'
$InstallDir = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\DeepSeekFloat'))
$ExeDst = Join-Path $InstallDir 'DeepSeekFloat.exe'
if (-not (Test-Path -LiteralPath $ExeSrc -PathType Leaf)) { throw "release exe not found: $ExeSrc" }
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
$ManagedFiles = @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','DeepSeekFloat.exe.config','WebView2-LICENSE.txt','WebView2-NOTICE.txt','LICENSE','THIRD-PARTY-NOTICES.md','VERSION','icon.ico','DeepSeekFloat.exe')
$StageDir = Join-Path $InstallDir ('.update-'+[Guid]::NewGuid().ToString('N'))
$RollbackDir = Join-Path $StageDir 'rollback'
New-Item -ItemType Directory -Path $RollbackDir -Force | Out-Null
$Existing = @()
foreach ($Name in $ManagedFiles) {
    $Source = if ($Name -eq 'icon.ico') { $IconSrc } else { Join-Path $BuildDir $Name }
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) { throw "missing build output: $Name" }
    $Staged = Join-Path $StageDir $Name
    Copy-Item -LiteralPath $Source -Destination $Staged -Force
    if ((Get-FileHash -LiteralPath $Staged).Hash -ne (Get-FileHash -LiteralPath $Source).Hash) { throw "staged hash mismatch: $Name" }
    $Destination = Join-Path $InstallDir $Name
    if (Test-Path -LiteralPath $Destination -PathType Leaf) {
        Copy-Item -LiteralPath $Destination -Destination (Join-Path $RollbackDir $Name)
        $Existing += $Name
    }
}
# All managed files are verified before stopping this exact installed application.
Get-Process -Name 'DeepSeekFloat' -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and [IO.Path]::GetFullPath($_.Path) -eq $ExeDst
} | ForEach-Object {
    $HostProcess = $_
    Stop-Process -Id $HostProcess.Id -ErrorAction Stop
    if (-not $HostProcess.WaitForExit(10000)) { throw 'Installed host did not exit in time' }
}
try {
    # Publish the entry executable last. Retain a bounded rollback directory.
    foreach ($Name in $ManagedFiles) {
        $Staged = Join-Path $StageDir $Name
        $Destination = Join-Path $InstallDir $Name
        if (Test-Path -LiteralPath $Destination -PathType Leaf) {
            # Preserve identical dependencies and atomically replace only changed managed files.
            if ((Get-FileHash -LiteralPath $Staged).Hash -eq (Get-FileHash -LiteralPath $Destination).Hash) { continue }
            [IO.File]::Replace($Staged,$Destination,(Join-Path $StageDir ($Name+'.replaced')))
        } else { [IO.File]::Move($Staged,$Destination) }
    }
} catch {
    foreach ($Name in $ManagedFiles) {
        $Destination = Join-Path $InstallDir $Name
        if ($Name -in $Existing) { Copy-Item -LiteralPath (Join-Path $RollbackDir $Name) -Destination $Destination -Force }
        elseif (Test-Path -LiteralPath $Destination -PathType Leaf) { Remove-Item -LiteralPath $Destination }
    }
    throw
}
$Desktop = [Environment]::GetFolderPath('Desktop')
$StartDir = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'
$StartupDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup'
New-Item -ItemType Directory -Path $StartupDir -Force | Out-Null
$DesktopLnk = Join-Path $Desktop 'DeepSeek.lnk'
$StartLnk = Join-Path $StartDir 'DeepSeek.lnk'
$StartupLnk = Join-Path $StartupDir 'DeepSeek.lnk'
$shell = New-Object -ComObject WScript.Shell
foreach ($shortcutPath in @($DesktopLnk,$StartLnk,$StartupLnk)) {
    if (-not (Test-Path -LiteralPath (Split-Path -Parent $shortcutPath))) { continue }
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $ExeDst
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.WindowStyle = 1
    $shortcut.Arguments = if ($shortcutPath -eq $StartupLnk) { '--background' } else { '' }
    $shortcut.Description = 'DeepSeek-Reflex：快捷键唤出 / 收起，设置面板调整快捷键'
    $shortcut.Hotkey = ''
    $shortcut.IconLocation = Join-Path $InstallDir 'icon.ico'
    $shortcut.Save()
    $registration=Start-Process -FilePath $ExeDst -ArgumentList ('"--register-shortcut='+$shortcutPath+'"') -WindowStyle Hidden -Wait -PassThru
    if($registration.ExitCode -ne 0){throw "Shortcut taskbar identity could not be saved: $shortcutPath"}
}
# Notify only the application's changed resources and links; do not clear the
# machine-wide icon cache or restart Explorer to refresh one application.
if (-not ('ReflexShellRefresh' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ReflexShellRefresh {
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
    public static extern void SHChangeNotify(uint events,uint flags,string path,IntPtr unused);
}
'@
}
foreach ($changedPath in @($ExeDst,(Join-Path $InstallDir 'icon.ico'),$DesktopLnk,$StartLnk,$StartupLnk)) {
    if(Test-Path -LiteralPath $changedPath){[ReflexShellRefresh]::SHChangeNotify(0x2000,0x2005,$changedPath,[IntPtr]::Zero)}
}
if (-not $NoLaunch) { Start-Process -FilePath $ExeDst -WindowStyle Hidden }
Write-Output "installed: $ExeDst"
Write-Output "rollback files: $RollbackDir"
