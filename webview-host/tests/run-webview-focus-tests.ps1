param([Parameter(Mandatory=$true)][string]$OutputDirectory,[string]$SourceDirectory,[switch]$HighDpi)
$ErrorActionPreference='Stop'
$hostRoot=Split-Path -Parent $PSScriptRoot
if(-not $SourceDirectory){$SourceDirectory=$hostRoot}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object {'/reference:'+$_}
foreach($name in @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll')){$refs+='/reference:'+(Join-Path $framework "WPF/$name")}
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){$refs+='/reference:'+(Join-Path $hostRoot "vendor/WebView2/lib/net462/$name")}
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','icon.ico')){Copy-Item (Join-Path $hostRoot "dist/$name") $OutputDirectory}
$sources=@(Get-ChildItem $SourceDirectory -Filter '*.cs' -File | ForEach-Object FullName)
$exe=Join-Path $OutputDirectory 'WebViewFocusTests.exe'
$manifest=@();if($HighDpi){$manifest=@('/win32manifest:'+(Join-Path $hostRoot 'app.manifest'))}
$savedLib=$env:LIB
try{$env:LIB=''; & (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /main:DeepSeekFloat.WebViewFocusTests ('/out:'+$exe) $manifest $refs $sources (Join-Path $PSScriptRoot 'WebViewFocusTests.cs');if($LASTEXITCODE -ne 0){throw 'Native focus tests did not compile'}}finally{$env:LIB=$savedLib}
& $exe (Join-Path $OutputDirectory 'preferences') | Tee-Object -FilePath (Join-Path $OutputDirectory 'regression.log')
if($LASTEXITCODE -ne 0){throw 'Native focus regression failed'}
