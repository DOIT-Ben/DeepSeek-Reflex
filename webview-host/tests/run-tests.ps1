param(
    [string]$OutputDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('DeepSeek-Reflex-tests-'+[Guid]::NewGuid().ToString('N'))),
    [string]$NodePath = 'node',
    [switch]$InteractionOnly,
    [switch]$EditorOnly,
    [switch]$GeneralOnly,
    [switch]$SurfacesOnly,
    [switch]$SettingsSessionOnly,
    [switch]$FeedbackOnly,
    [switch]$AuditUiOnly,
    [switch]$InputFocusOnly,
    [string]$SourceDirectory
)
$ErrorActionPreference = 'Stop'
if(@($InteractionOnly,$EditorOnly,$GeneralOnly,$SurfacesOnly,$SettingsSessionOnly,$FeedbackOnly,$AuditUiOnly,$InputFocusOnly).Where({$_}).Count -gt 1){throw 'Choose only one focused test mode'}
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
if(-not $SourceDirectory){$SourceDirectory=$HostSources}
$Sources = @(Get-ChildItem -LiteralPath $SourceDirectory -Filter '*.cs' -File | ForEach-Object FullName)
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
    $TestArgs=@($RunDirectory);if($InteractionOnly){$TestArgs+='--interaction-only'}
    if($EditorOnly){$TestArgs+='--editor-layout-only'}
    if($GeneralOnly){$TestArgs+='--general-layout-only'}
    if($SurfacesOnly){$TestArgs+='--surfaces-only'}
    if($SettingsSessionOnly){$TestArgs+='--settings-session-only'}
    if($FeedbackOnly){$TestArgs+='--feedback-only'}
    if($AuditUiOnly){$TestArgs+='--audit-ui-only'}
    if($InputFocusOnly){$TestArgs+='--input-focus-only'}
    & $TestExe @TestArgs | Tee-Object -FilePath (Join-Path $RunDirectory 'regression.log')
    if ($LASTEXITCODE -ne 0) { throw "Window regression failed: $Dpi" }
    if(-not ($InteractionOnly -or $EditorOnly -or $GeneralOnly -or $SurfacesOnly -or $SettingsSessionOnly -or $FeedbackOnly -or $AuditUiOnly -or $InputFocusOnly)){
        & $Node "$PSScriptRoot\focus-tests.cjs" $RunDirectory | Tee-Object -FilePath (Join-Path $RunDirectory 'focus.log')
        if ($LASTEXITCODE -ne 0) { throw "Focus script regression failed: $Dpi" }
    }
}
if($InteractionOnly){Write-Output "Focused interaction evidence: $OutputDirectory";return}
if($EditorOnly){Write-Output "Focused editor evidence: $OutputDirectory";return}
if($GeneralOnly){Write-Output "Focused general settings evidence: $OutputDirectory";return}
if($SurfacesOnly){Write-Output "Surface review evidence: $OutputDirectory";return}
if($SettingsSessionOnly){Write-Output "Settings session evidence: $OutputDirectory";return}
if($FeedbackOnly){Write-Output "Feedback evidence: $OutputDirectory";return}
if($InputFocusOnly){Write-Output "Input focus evidence: $OutputDirectory";return}
if($AuditUiOnly){Write-Output "Audit UI evidence: $OutputDirectory";return}
& (Join-Path $PSScriptRoot 'run-boundary-tests.ps1') -OutputDirectory (Join-Path $OutputDirectory 'boundary')
Write-Output "Test evidence: $OutputDirectory"
