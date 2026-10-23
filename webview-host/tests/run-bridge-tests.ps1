param([string]$OutputDirectory=(Join-Path ([IO.Path]::GetTempPath()) ('Reflex-transport-'+[Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
$SourceRoot=Split-Path -Parent $PSScriptRoot
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$Compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$TestExe=Join-Path $OutputDirectory 'BridgeTransportTests.exe'
$PriorLib=$env:LIB
try {
 $env:LIB=''
 & $Compiler /nologo /target:winexe /platform:x64 /warnaserror+ /reference:System.dll /reference:System.Core.dll /reference:System.Runtime.Serialization.dll "/out:$TestExe" (Join-Path $SourceRoot 'ReflexBridge.cs') (Join-Path $SourceRoot 'ChatModes.cs') (Join-Path $PSScriptRoot 'BridgeTransportTests.cs')
 if($LASTEXITCODE -ne 0){throw 'Transport test compilation failed'}
}finally{$env:LIB=$PriorLib}
$Script=[IO.Path]::GetFullPath((Join-Path $SourceRoot '../plugins/dsh-reflex/tests/native-transport.mjs'))
$Log=Join-Path $OutputDirectory 'transport.log'
$Child=Start-Process -FilePath $TestExe -ArgumentList @('"'+$Script+'"') -WindowStyle Hidden -RedirectStandardOutput $Log -RedirectStandardError (Join-Path $OutputDirectory 'stderr.log') -PassThru
try{if(-not $Child.WaitForExit(30000)){$Child.Kill();throw 'Transport test timed out'};Get-Content -LiteralPath $Log;if($Child.ExitCode -ne 0){throw 'Transport test failed'}}finally{$Child.Dispose()}
