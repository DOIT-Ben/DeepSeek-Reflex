param([string]$OutputDirectory=(Join-Path ([IO.Path]::GetTempPath()) ('Reflex-boundary-'+[Guid]::NewGuid().ToString('N'))),[switch]$CoreOnly)
$ErrorActionPreference='Stop'
$HostSources=Split-Path -Parent $PSScriptRoot
$Framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$Compiler=Join-Path $Framework 'csc.exe'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$References=@('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Runtime.Serialization.dll')
foreach($Name in @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll')){$References+="/reference:$Framework\WPF\$Name"}
$ClipboardExe=Join-Path $OutputDirectory 'ClipboardBoundary.exe'
$SavedLib=$env:LIB
try{
 $env:LIB=''
 & $Compiler /nologo /target:exe /platform:x64 /warnaserror+ "/out:$ClipboardExe" $References (Join-Path $HostSources 'SelectionCapture.cs') (Join-Path $PSScriptRoot 'ClipboardBoundary.cs')
 if($LASTEXITCODE -ne 0){throw 'Clipboard test compilation failed'}
}finally{$env:LIB=$SavedLib}
& $ClipboardExe
if($LASTEXITCODE -ne 0){throw 'Clipboard boundary failed'}
foreach($Name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){
 $References+="/reference:$HostSources\vendor\WebView2\lib\net462\$Name"
 Copy-Item -LiteralPath (Join-Path $HostSources "dist/$Name") -Destination $OutputDirectory
}
foreach($Name in @('WebView2Loader.dll','icon.ico')){Copy-Item -LiteralPath (Join-Path $HostSources "dist/$Name") -Destination $OutputDirectory}
$Exe=Join-Path $OutputDirectory 'BoundaryTests.exe'
$Sources=@(Get-ChildItem -LiteralPath $HostSources -Filter '*.cs' -File | ForEach-Object FullName)
$SavedLib=$env:LIB
try{
 $env:LIB=''
 & $Compiler /nologo /target:exe /platform:x64 /warnaserror+ /main:DeepSeekFloat.BoundaryTests "/win32manifest:$HostSources\app.manifest" "/out:$Exe" $References $Sources (Join-Path $PSScriptRoot 'BoundaryTests.cs')
 if($LASTEXITCODE -ne 0){throw 'Boundary test compilation failed'}
}finally{$env:LIB=$SavedLib}
$Arguments=@($OutputDirectory);if($CoreOnly){$Arguments+='--core'}
& $Exe @Arguments
if($LASTEXITCODE -ne 0){throw 'Boundary cases failed'}
node (Join-Path $PSScriptRoot 'prompt-tests.cjs') $OutputDirectory
if($LASTEXITCODE -ne 0){throw 'Draft boundary failed'}
node (Join-Path $PSScriptRoot 'focus-tests.cjs') $OutputDirectory
if($LASTEXITCODE -ne 0){throw 'Focus regression failed'}
Write-Output "Boundary evidence: $OutputDirectory"
