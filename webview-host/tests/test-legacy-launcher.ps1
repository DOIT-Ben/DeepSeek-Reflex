param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$HostSources = Split-Path -Parent $PSScriptRoot
$Fixture = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'legacy launcher with spaces'
if (Test-Path -LiteralPath $Fixture) { throw 'Legacy fixture must be new' }
New-Item -ItemType Directory -Path $Fixture -Force | Out-Null
$Source = Join-Path $Fixture 'Receiver.cs'
@'
using System;
using System.IO;
using System.Text;
class Receiver {
 static void Main(string[] args) {
  File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"received.txt"),Array.ConvertAll(args,value=>Convert.ToBase64String(Encoding.UTF8.GetBytes(value))));
 }
}
'@ | Set-Content -LiteralPath $Source -Encoding utf8
$Compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$PriorLib = $env:LIB
try {
    $env:LIB = ''
    & $Compiler /nologo /target:winexe /platform:x64 /warnaserror+ ("/out:$Fixture\DeepSeekFloat.exe") $Source
    if ($LASTEXITCODE -ne 0) { throw 'Legacy receiver compilation failed' }
} finally { $env:LIB = $PriorLib }
$Launcher = Join-Path $Fixture 'DeepSeek.exe'
Copy-Item -LiteralPath (Join-Path $HostSources 'dist\DeepSeek.exe') -Destination $Launcher
$Expected = @('--background','words with spaces','中文路径\','a"quoted"word','','C:\a directory\')
$Start = [Diagnostics.ProcessStartInfo]::new($Launcher)
$Start.UseShellExecute = $false
foreach ($Argument in $Expected) { $Start.ArgumentList.Add($Argument) }
$Process = [Diagnostics.Process]::Start($Start)
try {
    if (-not $Process.WaitForExit(5000)) { $Process.Kill(); throw 'Legacy launcher timeout' }
    if ($Process.ExitCode -ne 0) { throw 'Legacy launcher failed' }
} finally { $Process.Dispose() }
$ReceivedPath = Join-Path $Fixture 'received.txt'
$Deadline = [DateTime]::UtcNow.AddSeconds(3)
while (-not (Test-Path -LiteralPath $ReceivedPath) -and [DateTime]::UtcNow -lt $Deadline) { Start-Sleep -Milliseconds 20 }
if (-not (Test-Path -LiteralPath $ReceivedPath)) { throw 'Legacy host did not receive arguments' }
$Actual = @([IO.File]::ReadAllLines($ReceivedPath) | ForEach-Object { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($_)) })
if ($Actual.Count -ne $Expected.Count) { throw 'Legacy argument count changed' }
for ($Index=0; $Index -lt $Expected.Count; $Index++) {
    if ($Actual[$Index] -cne $Expected[$Index]) { throw "Legacy argument changed at $Index" }
}
Write-Output 'PASS real legacy launcher forwards spaces, quotes, Unicode, empty arguments and trailing backslashes without opening a website'
