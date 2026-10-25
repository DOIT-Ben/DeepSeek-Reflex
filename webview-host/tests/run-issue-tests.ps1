param(
    [ValidateSet('Menu','Settings','Feedback','Regression')][string]$Case='Menu',
    [ValidateSet('default','high-dpi')][string]$Dpi='high-dpi',
    [string]$SourceOverride,
    [string]$OutputDirectory=(Join-Path ([IO.Path]::GetTempPath()) ('Reflex-issue-tests-'+[Guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference='Stop'
$hostSources=Split-Path -Parent $PSScriptRoot
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh isolated output directory'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$prefsRoot=Join-Path $OutputDirectory 'isolated-preferences'
New-Item -ItemType Directory -Path $prefsRoot | Out-Null
$prefs=[IO.File]::ReadAllText((Join-Path $hostSources 'Preferences.cs'))
$initializer='internal static readonly string Root = EnsureRoot();'
if(-not $prefs.Contains($initializer)){throw 'Review changed Preferences initializer before running tests'}
$literal=ConvertTo-Json -InputObject $prefsRoot -Compress
$prefs=$prefs.Replace($initializer,'internal static readonly string Root = '+$literal+';')
$isolatedSource=Join-Path $OutputDirectory 'Preferences.cs'
[IO.File]::WriteAllText($isolatedSource,$prefs,[Text.UTF8Encoding]::new($false))
$sources=@(Get-ChildItem -LiteralPath $hostSources -Filter '*.cs' -File | Where-Object Name -ne 'Preferences.cs' | ForEach-Object FullName)
if($SourceOverride){
    $expected=if($Case -eq 'Menu'){'TrayMenu.cs'}elseif($Case -eq 'Settings'){'SettingsDialog.cs'}elseif($Case -eq 'Feedback'){'FeedbackDialog.cs'}else{''}
    if(-not $expected -or [IO.Path]::GetFileName($SourceOverride) -ne $expected){throw 'Unsupported baseline override'}
    $name=[IO.Path]::GetFileName($SourceOverride)
    $sources=@($sources | Where-Object {[IO.Path]::GetFileName($_) -ne $name})+@([IO.Path]::GetFullPath($SourceOverride))
}
$sources+=@($isolatedSource,(Join-Path $PSScriptRoot 'IssueRegressionTests.cs'))
$main='DeepSeekFloat.IssueRegressionTests'
if($Case -eq 'Regression'){$sources+=Join-Path $PSScriptRoot 'SettingsTests.cs';$main='DeepSeekFloat.SettingsTests'}
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$refs=@('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Runtime.Serialization.dll')
foreach($name in @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll')){$refs+='/reference:'+(Join-Path $framework ('WPF/'+$name))}
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){
    $path=Join-Path $hostSources ('vendor/WebView2/lib/net462/'+$name)
    $refs+='/reference:'+$path;Copy-Item -LiteralPath $path -Destination $OutputDirectory
}
Copy-Item -LiteralPath (Join-Path $hostSources 'vendor/WebView2/runtimes/win-x64/native/WebView2Loader.dll') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $hostSources 'assets/icon.ico') -Destination $OutputDirectory
$manifest=@();if($Dpi -eq 'high-dpi'){$manifest=@('/win32manifest:'+(Join-Path $hostSources 'app.manifest'))}
$exe=Join-Path $OutputDirectory 'IssueRegressionTests.exe'
$priorLib=$env:LIB
try{
    $env:LIB=''
    & (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /warnaserror+ ('/main:'+$main) ('/out:'+$exe) $manifest $refs $sources > (Join-Path $OutputDirectory 'compile.log') 2>&1
    if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $OutputDirectory 'compile.log');throw 'Isolated test compilation failed'}
}finally{$env:LIB=$priorLib}
$start=[Diagnostics.ProcessStartInfo]::new($exe)
$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
$start.ArgumentList.Add($OutputDirectory)
if($Case -ne 'Regression'){$start.ArgumentList.Add($Case)}
$child=[Diagnostics.Process]::Start($start)
try{
    $stdout=$child.StandardOutput.ReadToEndAsync();$stderr=$child.StandardError.ReadToEndAsync()
    if(-not $child.WaitForExit(90000)){$child.Kill();throw 'Isolated native test timeout'}
    $output=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'test.log'),$output)
    Write-Output $output
    if($child.ExitCode -ne 0){throw 'Isolated native assertion failed'}
}finally{$child.Dispose()}
Write-Output "PASS isolated $Case / $Dpi; Preferences initialization never invokes personal migration: $OutputDirectory"
