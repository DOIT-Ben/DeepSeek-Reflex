param([switch]$Offline,[string]$MakeNsisPath,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$Project = Split-Path -Parent $PSScriptRoot
$Version = (Get-Content -LiteralPath (Join-Path $Project 'VERSION') -Raw).Trim()
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release VERSION' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $Project 'release' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $MakeNsisPath) {
    $Command = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($Command) { $MakeNsisPath = $Command.Source }
    else { $MakeNsisPath = Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe' }
}
if (-not (Test-Path -LiteralPath $MakeNsisPath -PathType Leaf)) { throw 'Install NSIS 3, or specify -MakeNsisPath' }
& (Join-Path $Project 'webview-host\build.ps1') -Offline:$Offline
$Dist = Join-Path $Project 'webview-host\dist'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$Stage = Join-Path $OutputDirectory ('stage-'+[Guid]::NewGuid().ToString('N'))
$Payload = Join-Path $Stage 'DeepSeek-Reflex'
New-Item -ItemType Directory -Path $Payload -Force | Out-Null
$Files = @('DeepSeekFloat.exe','DeepSeekFloat.exe.config','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','icon.ico','LICENSE','THIRD-PARTY-NOTICES.md','WebView2-LICENSE.txt','WebView2-NOTICE.txt','VERSION')
try {
    foreach ($Name in $Files) { Copy-Item -LiteralPath (Join-Path $Dist $Name) -Destination $Payload }
    Copy-Item -LiteralPath (Join-Path $Project 'README.md') -Destination (Join-Path $Payload 'README.md')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'QUICKSTART.txt') -Destination $Payload
    $Info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $Payload 'DeepSeekFloat.exe'))
    if ($Info.ProductVersion -ne $Version -or $Info.ProductName -ne 'DeepSeek-Reflex') { throw 'Executable version does not match release metadata' }
    $PayloadHashes = @(Get-ChildItem -LiteralPath $Payload -File | Sort-Object Name | ForEach-Object {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant(),$_.Name
    })
    $PayloadHashes | Set-Content -LiteralPath (Join-Path $Payload 'SHA256SUMS.txt') -Encoding utf8
    $Zip = Join-Path $OutputDirectory ("DeepSeek-Reflex-$Version-Windows-x64.zip")
    Compress-Archive -LiteralPath $Payload -DestinationPath $Zip -Force
    Get-ChildItem -LiteralPath $Payload -File | Sort-Object Name | ForEach-Object {
        'Delete "$INSTDIR\'+$_.Name+'"'
    } | Set-Content -LiteralPath (Join-Path $Stage 'uninstall-files.nsh') -Encoding utf8
    $Setup = Join-Path $OutputDirectory ("DeepSeek-Reflex-$Version-Setup-x64.exe")
    & $MakeNsisPath /WX /INPUTCHARSET UTF8 ("/DAPP_VERSION=$Version") ("/DPAYLOAD_DIR=$Payload") ("/DOUTPUT_FILE=$Setup") (Join-Path $PSScriptRoot 'setup.nsi')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
    $Setup = Join-Path $OutputDirectory ("DeepSeek-Reflex-$Version-Setup-x64.exe")
    @($Setup,$Zip) | ForEach-Object {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_).Hash.ToLowerInvariant(),(Split-Path -Leaf $_)
    } | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding utf8
} finally {
    # The only recursively removed path is this freshly created stage under the chosen output.
    $Resolved = [IO.Path]::GetFullPath($Stage)
    if (-not $Resolved.StartsWith($OutputDirectory+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid release stage path' }
    Remove-Item -LiteralPath $Resolved -Recurse -Force
}
Write-Output "Release $Version prepared: $OutputDirectory"
