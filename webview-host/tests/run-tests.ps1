param(
    [string]$OutputDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('DeepSeekFloat-tests-'+[Guid]::NewGuid().ToString('N'))),
    [string]$NodePath = 'node'
)
$ErrorActionPreference = 'Stop'
$HostSources = Split-Path -Parent $PSScriptRoot
$Compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$Node = (Get-Command $NodePath -ErrorAction Stop).Source
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& (Join-Path $HostSources 'build.ps1') -Offline
$Refs = @('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Runtime.Serialization.dll')
foreach ($Name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')) {
    $Refs += "/reference:$HostSources\vendor\WebView2\lib\net462\$Name"
}
foreach ($Name in @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll')) {
    $Refs += "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\$Name"
}
$Sources = @(Get-ChildItem -LiteralPath $HostSources -Filter '*.cs' -File | ForEach-Object FullName)
foreach ($Dpi in @('default','high-dpi')) {
    $RunDirectory = Join-Path $OutputDirectory $Dpi
    New-Item -ItemType Directory -Path $RunDirectory -Force | Out-Null
    $TestExe = Join-Path $RunDirectory 'SettingsTests.exe'
    $Manifest = @()
    if ($Dpi -eq 'high-dpi') { $Manifest = @("/win32manifest:$HostSources\app.manifest") }
    $PriorLib = $env:LIB
    try {
        $env:LIB = ''
        & $Compiler /nologo /target:exe /platform:x64 /warnaserror+ /main:DeepSeekFloat.SettingsTests "/out:$TestExe" $Manifest $Refs $Sources "$PSScriptRoot\SettingsTests.cs"
        if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
    } finally { $env:LIB = $PriorLib }
    foreach ($Name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','icon.ico')) {
        Copy-Item -LiteralPath (Join-Path $HostSources "dist\$Name") -Destination $RunDirectory -Force
    }
    & $TestExe $RunDirectory | Tee-Object -FilePath (Join-Path $RunDirectory 'regression.log')
    if ($LASTEXITCODE -ne 0) { throw "Window regression failed: $Dpi" }
    & $Node "$PSScriptRoot\focus-tests.cjs" $RunDirectory | Tee-Object -FilePath (Join-Path $RunDirectory 'focus.log')
    if ($LASTEXITCODE -ne 0) { throw "Focus script regression failed: $Dpi" }
}
& (Join-Path $PSScriptRoot 'test-legacy-launcher.ps1') -OutputDirectory $OutputDirectory
Write-Output "Test evidence: $OutputDirectory"
