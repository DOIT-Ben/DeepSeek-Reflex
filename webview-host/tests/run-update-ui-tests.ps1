param([Parameter(Mandatory)][string]$OutputDirectory,[switch]$NegativeDraft)
$ErrorActionPreference='Stop'
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh update UI directory'}
$source=Split-Path -Parent $PSScriptRoot
$fw=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$refs=@('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Runtime.Serialization.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll')
foreach($n in @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll')){$refs+='/reference:'+(Join-Path $fw ('WPF/'+$n))}
foreach($n in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){$refs+='/reference:'+(Join-Path $source ('vendor/WebView2/lib/net462/'+$n))}
$priorLib=$env:LIB
try {
 $env:LIB=''
 foreach($mode in @('default','high-dpi')) {
  $dir=Join-Path $OutputDirectory $mode;New-Item -ItemType Directory -Path $dir -Force | Out-Null
  $prefs=[IO.File]::ReadAllText((Join-Path $source 'Preferences.cs')).Replace('internal static readonly string Root = EnsureRoot();',('internal static readonly string Root = "'+(Join-Path $dir 'preferences').Replace('\','\\')+'";'))
  $p=Join-Path $dir 'Preferences.cs';[IO.File]::WriteAllText($p,$prefs,[Text.UTF8Encoding]::new($false))
  $sources=@(Get-ChildItem $source -Filter '*.cs' -File | Where-Object Name -ne 'Preferences.cs' | ForEach-Object FullName)+@($p,(Join-Path $PSScriptRoot 'UpdateUiTests.cs'))
  if($NegativeDraft){
   $chat=[IO.File]::ReadAllText((Join-Path $source 'ChatWindow.cs')).Replace("`r`n","`n")
   $fixed="if(editors.length!==1)return 'unknown';`nreturn String(editors[0].value===undefined?editors[0].textContent:editors[0].value).length>0?'dirty':'clean';"
   $old="return editors.some(e=>String(e.value===undefined?e.textContent:e.value).trim().length>0)?'dirty':'clean';"
   if(-not $chat.Contains($fixed)){throw 'Review changed draft probe before generating negative'}
   $chatPath=Join-Path $dir 'ChatWindow-before.cs';[IO.File]::WriteAllText($chatPath,$chat.Replace($fixed,$old),[Text.UTF8Encoding]::new($false))
   $sources=@($sources | Where-Object {[IO.Path]::GetFileName($_) -ne 'ChatWindow.cs'})+@($chatPath)
  }
  $exe=Join-Path $dir 'UpdateUiTests.exe';$manifest=@();if($mode -eq 'high-dpi'){$manifest=@('/win32manifest:'+(Join-Path $source 'app.manifest'))}
  & (Join-Path $fw 'csc.exe') /nologo /target:exe /platform:x64 /warnaserror+ /main:DeepSeekFloat.UpdateUiTests "/out:$exe" $manifest $refs $sources
  if($LASTEXITCODE -ne 0){throw 'Update UI test build failed'}
  Copy-Item (Join-Path $source 'app.config') ($exe+'.config')
  foreach($n in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','icon.ico')){Copy-Item (Join-Path $source "dist/$n") $dir}
  & $exe $dir | Tee-Object -FilePath (Join-Path $dir 'update-ui.log')
  if($LASTEXITCODE -ne 0){throw "Update UI failed: $mode"}
 }
}finally{$env:LIB=$priorLib}
