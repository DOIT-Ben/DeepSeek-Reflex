param([string]$SourceDirectory=(Split-Path -Parent $PSScriptRoot),[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$sourceRoot=[IO.Path]::GetFullPath($SourceDirectory)
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh isolated audit directory'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$preferences=[IO.File]::ReadAllText((Join-Path $sourceRoot 'Preferences.cs'))
$initializer='internal static readonly string Root = EnsureRoot();'
if(-not $preferences.Contains($initializer)){throw 'Review changed preference initialization'}
$fixtureRoot=Join-Path $OutputDirectory 'preferences'
$literal=ConvertTo-Json -InputObject $fixtureRoot -Compress
$preferences=$preferences.Replace($initializer,'internal static readonly string Root = '+$literal+';')
$isolated=Join-Path $OutputDirectory 'Preferences.cs'
[IO.File]::WriteAllText($isolated,$preferences,[Text.UTF8Encoding]::new($false))
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$exe=Join-Path $OutputDirectory 'AuditBoundaryTests.exe'
$refs=@('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Runtime.Serialization.dll')
foreach($name in @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll')){$refs+='/reference:'+(Join-Path $framework ('WPF/'+$name))}
$hostRoot=Split-Path -Parent $PSScriptRoot
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){$refs+='/reference:'+(Join-Path $hostRoot ('vendor/WebView2/lib/net462/'+$name))}
$sources=@(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File | Where-Object Name -ne 'Preferences.cs' | ForEach-Object FullName)+@($isolated,(Join-Path $PSScriptRoot 'AuditBoundaryTests.cs'))
$priorLib=$env:LIB
try{$env:LIB=''; & (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /warnaserror+ /main:DeepSeekFloat.AuditBoundaryTests "/out:$exe" $refs $sources;if($LASTEXITCODE -ne 0){throw 'Audit compilation failed'}}finally{$env:LIB=$priorLib}
& $exe | Tee-Object -FilePath (Join-Path $OutputDirectory 'audit.log')
if($LASTEXITCODE -ne 0){throw 'Audit boundaries failed'}
