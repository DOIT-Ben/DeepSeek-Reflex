param([Parameter(Mandatory)][string]$OutputDirectory,[switch]$Live,[string]$CandidateDirectory)
$ErrorActionPreference='Stop'
$source=Split-Path -Parent $PSScriptRoot
if(-not $CandidateDirectory){$CandidateDirectory=Join-Path $source 'dist'}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a new client QA directory'}
$dir=(New-Item -ItemType Directory -Path $OutputDirectory).FullName
foreach($name in @('DeepSeek-Reflex.exe','DeepSeek-Reflex.exe.config','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','icon.ico')){
    Copy-Item -LiteralPath (Join-Path $CandidateDirectory $name) -Destination $dir
}
$app=Join-Path $dir 'DeepSeek-Reflex.exe'
$driver=Join-Path $dir 'ClientSwitchTests.exe'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$oldLib=$env:LIB
try{
    $env:LIB=''
    & (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /warnaserror+ "/out:$driver" "/win32manifest:$source\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$dir\Microsoft.Web.WebView2.Core.dll" "/reference:$dir\Microsoft.Web.WebView2.WinForms.dll" (Join-Path $PSScriptRoot 'ClientSwitchTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Client test driver compilation failed'}
}finally{$env:LIB=$oldLib}
"Candidate SHA256 "+(Get-FileHash -LiteralPath $app).Hash
$jobs=if($Live){@('live')}else{@('login','restart')}
foreach($run in $jobs){
    & $driver $app (Join-Path $dir 'preferences') $run 2>&1 | Tee-Object -FilePath (Join-Path $dir ($run+'.log'))
    if($LASTEXITCODE -ne 0){throw "Client desktop acceptance failed in $run"}
}
