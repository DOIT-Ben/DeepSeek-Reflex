param([Parameter(Mandatory)][string]$OutputDirectory,[switch]$Live,[switch]$NegativeStartupRace,[string]$MetadataFile)
$ErrorActionPreference='Stop'
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh updater test directory'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$source=Split-Path -Parent $PSScriptRoot
$fw=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$csc=Join-Path $fw 'csc.exe'
$refs=@('/reference:System.dll','/reference:System.Core.dll','/reference:System.Windows.Forms.dll','/reference:System.Runtime.Serialization.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll')
$transaction=[IO.File]::ReadAllText((Join-Path $source 'UpdateTransaction.cs'))
$unique=[Guid]::NewGuid().ToString('N')
$transaction=$transaction.Replace('Local\DeepSeek-Reflex.Host',"Local\Reflex-Test-$unique-Host").Replace('Local\DeepSeek-Reflex.Update',"Local\Reflex-Test-$unique-Update").Replace('30000','1800')
$transaction=$transaction.Replace('Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DeepSeek-Reflex","updates")',('"'+$OutputDirectory.Replace('\','\\')+'\\tasks"'))
$isolated=Join-Path $OutputDirectory 'UpdateTransaction.cs'
[IO.File]::WriteAllText($isolated,$transaction,[Text.UTF8Encoding]::new($false))
$sources=@((Join-Path $source 'UpdateFeed.cs'),(Join-Path $source 'SignedUpdateFeed.cs'),(Join-Path $source 'UpdateSigningKey.cs'),(Join-Path $source 'UpdatePackage.cs'),$isolated)
$fixture=Join-Path $PSScriptRoot 'UpdateFixture.cs'
if($NegativeStartupRace){$fixture=Join-Path $OutputDirectory 'UpdateFixture-before.cs';$s=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'UpdateFixture.cs'));$s=$s.Replace('if(UpdateTransaction.Bootstrap(dir))return 0;'+"`n"+'                    File.AppendAllText','if(UpdateTransaction.BarrierActive())return 0;'+"`n"+'                    File.AppendAllText');[IO.File]::WriteAllText($fixture,$s,[Text.UTF8Encoding]::new($false))}
$priorLib=$env:LIB
try {
 $env:LIB=''
 foreach($v in @('1.0.19','1.0.20')) {
  $dir=Join-Path $OutputDirectory $v;New-Item -ItemType Directory -Path $dir | Out-Null
  $assembly=Join-Path $dir 'Assembly.cs'
  [IO.File]::WriteAllText($assembly,('[assembly:System.Reflection.AssemblyProduct("DeepSeek-Reflex")][assembly:System.Reflection.AssemblyFileVersion("'+$v+'.0")][assembly:System.Reflection.AssemblyInformationalVersion("'+$v+'")]'))
  & $csc /nologo /target:exe /platform:x64 /warnaserror+ /main:DeepSeekFloat.UpdateFixture "/out:$dir/DeepSeek-Reflex.exe" $refs $sources $fixture $assembly
  if($LASTEXITCODE -ne 0){throw 'Fixture build failed'}
 }
 $exe=Join-Path $OutputDirectory 'UpdateTests.exe'
 & $csc /nologo /target:exe /platform:x64 /warnaserror+ /main:DeepSeekFloat.UpdateTests "/out:$exe" $refs $sources (Join-Path $PSScriptRoot 'UpdateTests.cs')
 if($LASTEXITCODE -ne 0){throw 'Update tests build failed'}
 $argsList=@((Join-Path $OutputDirectory 'cases'),(Join-Path $OutputDirectory '1.0.19'),(Join-Path $OutputDirectory '1.0.20'));if($Live){$argsList+='--live'};if($NegativeStartupRace){$argsList+='--race-only'}
 if($MetadataFile){if(-not $Live){throw 'MetadataFile requires Live'};$argsList+=(Resolve-Path -LiteralPath $MetadataFile).Path}
 & $exe @argsList | Tee-Object -FilePath (Join-Path $OutputDirectory 'updates.log')
 if($LASTEXITCODE -ne 0){throw 'Updater test failure'}
} finally {$env:LIB=$priorLib}
