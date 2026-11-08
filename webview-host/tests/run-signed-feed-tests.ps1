param([Parameter(Mandatory)][string]$OutputDirectory,[switch]$Live)
$ErrorActionPreference='Stop'
$dir=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $dir){throw 'Use a fresh signed-feed evidence directory'}
New-Item -ItemType Directory -Path $dir | Out-Null
$source=Split-Path -Parent $PSScriptRoot
$transaction=[IO.File]::ReadAllText((Join-Path $source 'UpdateTransaction.cs'))
$transaction=$transaction.Replace('Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DeepSeek-Reflex","updates")',('"'+$dir.Replace('\','\\')+'\\state"'))
$isolated=Join-Path $dir 'UpdateTransaction.cs';[IO.File]::WriteAllText($isolated,$transaction,[Text.UTF8Encoding]::new($false))
$sources=@('UpdateFeed.cs','SignedUpdateFeed.cs','UpdateSigningKey.cs','UpdatePackage.cs','UpdateCoordinator.cs') | ForEach-Object {Join-Path $source $_}
$sources+=@($isolated,(Join-Path $PSScriptRoot 'SignedFeedTests.cs'))
$fw=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$exe=Join-Path $dir 'SignedFeedTests.exe';$prior=$env:LIB
try{$env:LIB=''; & (Join-Path $fw 'csc.exe') /nologo /platform:x64 /target:exe /warnaserror+ /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/out:$exe" $sources;if($LASTEXITCODE -ne 0){throw 'Signed-feed compilation failed'}}finally{$env:LIB=$prior}
$arguments=@($dir);if($Live){$arguments+='--live'}
& $exe @arguments | Tee-Object -FilePath (Join-Path $dir 'signed-feed.log')
if($LASTEXITCODE -ne 0){throw 'Signed-feed checks failed'}
