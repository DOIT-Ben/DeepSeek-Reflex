param(
    [ValidateSet('Preserve','Upgrade')][string]$Stage='Preserve',
    [string]$SetupPath=(Join-Path $PSScriptRoot '../setup.nsi'),
    [string]$OutputDirectory=(Join-Path ([IO.Path]::GetTempPath()) ('Reflex-installer-flow-'+[Guid]::NewGuid().ToString('N'))),
    [string]$MakeNsisPath='C:\Program Files (x86)\NSIS\makensis.exe'
)
$ErrorActionPreference='Stop'
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh output directory'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
function Assert-InSandbox([string]$Path){
    $full=[IO.Path]::GetFullPath($Path)
    if(-not $full.StartsWith($OutputDirectory+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Outside sandbox: $full"}
    return $full
}
function Check([bool]$Value,[string]$Name){if(-not $Value){throw $Name};Write-Output "PASS $Name"}
function NsisLiteral([string]$Value){return $Value.Replace('$','$$').Replace('"','$\"')}
$payload=Assert-InSandbox (Join-Path $OutputDirectory 'payload with spaces')
New-Item -ItemType Directory -Path $payload | Out-Null
[IO.File]::WriteAllText((Join-Path $payload 'DeepSeek-Reflex.exe'),'synthetic-new-payload')
[IO.File]::WriteAllText((Join-Path $payload 'dependency.txt'),'synthetic-dependency')

# Execute the production required section, with every external namespace redirected.
# Never run the real installer or its shortcut-registration EXE from this harness.
$source=[IO.File]::ReadAllText([IO.Path]::GetFullPath($SetupPath))
$match=[regex]::Match($source,'(?ms)^Section "DeepSeek-Reflex[^\r\n]*\r?\n(?<body>.*?)^SectionEnd[ \t]*\r?$')
if(-not $match.Success){throw 'Required installation section not found'}
$body=$match.Groups['body'].Value.Replace('$LOCALAPPDATA','$TestLocalData').Replace('$SMPROGRAMS','$TestStartMenu')
$safeLines=[Collections.Generic.List[string]]::new()
$allowed=@('SectionIn','IfFileExists','ClearErrors','FileOpen','IfErrors','FileClose','Goto','MessageBox','SetErrorLevel','Abort','SetOutPath','SetOverwrite','File','WriteUninstaller','CreateDirectory','CreateShortcut','StrCpy','Call','RMDir')
foreach($raw in [regex]::Split($body,'\r?\n')){
    $line=$raw.Trim()
    if($line -match '^WriteReg(Str|DWORD)\b'){$safeLines.Add('; registry write suppressed by isolated fixture');continue}
    if($line -eq '' -or $line.StartsWith(';') -or $line -match '^[A-Za-z_]\w*:$'){$safeLines.Add($raw);continue}
    $command=($line -split '\s+',2)[0]
    if($command -notin $allowed){throw "Unexpected instruction: $command"}
    if($line -match '"(?:[A-Za-z]:|\\\\)'){throw 'Literal external file path in fixture section'}
    foreach($variable in [regex]::Matches($line,'\$([A-Za-z_]\w*)')){
        if($variable.Groups[1].Value -notin @('INSTDIR','TestLocalData','TestStartMenu')){throw "Unmapped variable: $($variable.Value)"}
    }
    foreach($macro in [regex]::Matches($line,'\$\{([^}]+)\}')){if($macro.Groups[1].Value -ne 'PAYLOAD_DIR'){throw 'Unmapped compile macro'}}
    if($command -eq 'Call' -and $line -ne 'Call RegisterTaskbarShortcut'){throw 'Unexpected external call'}
    if($command -eq 'StrCpy' -and $line -notmatch '^StrCpy \$0 '){throw 'Fixture namespace mutation'}
    $safeLines.Add($raw)
}
$fixture=Assert-InSandbox (Join-Path $OutputDirectory 'fixture.nsi')
$exe=Assert-InSandbox (Join-Path $OutputDirectory 'fixture.exe')
$header=@(
    'Unicode true','Name "Reflex isolated installer flow"',
    ('OutFile "'+(NsisLiteral $exe)+'"'),
    ('InstallDir "'+(NsisLiteral (Join-Path $OutputDirectory 'unused'))+'"'),
    ('!define PAYLOAD_DIR "'+(NsisLiteral $payload)+'"'),
    'RequestExecutionLevel user','SilentInstall silent',
    'Var TestLocalData','Var TestStartMenu','Function .onInit',
    ' ReadEnvStr $TestLocalData "REFLEX_INSTALLER_TEST_LOCAL"',
    ' StrCmp $TestLocalData "" 0 +2',' Abort',
    ' StrCpy $TestStartMenu "$TestLocalData\shortcuts"','FunctionEnd',
    'Section "Required"'
)
$footer=@('SectionEnd','Function RegisterTaskbarShortcut',' Return','FunctionEnd','Section "Uninstall"',' Quit','SectionEnd')
[IO.File]::WriteAllText($fixture,($header+$safeLines.ToArray()+$footer -join "`r`n"),[Text.UTF8Encoding]::new($false))
& $MakeNsisPath /WX /V2 /INPUTCHARSET UTF8 $fixture > (Join-Path $OutputDirectory 'compile.log') 2>&1
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $OutputDirectory 'compile.log');throw 'NSIS fixture compilation failed'}

function Invoke-Fixture([string]$Local,[string]$Target){
    $Local=Assert-InSandbox $Local;$Target=Assert-InSandbox $Target
    $start=[Diagnostics.ProcessStartInfo]::new($exe)
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
    # NSIS /D consumes the remaining command line, including unquoted spaces.
    $start.Arguments='/S /D='+$Target
    $start.Environment['REFLEX_INSTALLER_TEST_LOCAL']=$Local
    $child=[Diagnostics.Process]::Start($start)
    try{if(-not $child.WaitForExit(15000)){$child.Kill();throw 'Installer fixture timeout'};return $child.ExitCode}finally{$child.Dispose()}
}
foreach($case in @('same-old','child-of-old','separate','separate-with-float')){
    $root=Assert-InSandbox (Join-Path $OutputDirectory $case)
    $local=Assert-InSandbox (Join-Path $root 'local')
    $legacy=Assert-InSandbox (Join-Path $local 'Programs/DeepSeekFloat')
    $target=if($case -eq 'same-old'){$legacy}elseif($case -eq 'child-of-old'){Join-Path $legacy 'new install'}else{Join-Path $local 'Programs/DeepSeek-Reflex'}
    $target=Assert-InSandbox $target
    New-Item -ItemType Directory -Path $legacy -Force | Out-Null
    $note=Join-Path $legacy 'user-note.txt';[IO.File]::WriteAllText($note,'preserve this synthetic user file')
    if($case -eq 'separate-with-float'){[IO.File]::WriteAllText((Join-Path $legacy 'DeepSeekFloat.exe'),'old synthetic payload')}
    $code=Invoke-Fixture $local $target
    Check ($code -eq 0) "$case exits successfully"
    Check ((Test-Path -LiteralPath (Join-Path $target 'DeepSeek-Reflex.exe')) -and [IO.File]::ReadAllText((Join-Path $target 'DeepSeek-Reflex.exe')) -eq 'synthetic-new-payload') "$case preserves new executable"
    Check (Test-Path -LiteralPath (Join-Path $target 'uninstall.exe')) "$case preserves new uninstaller"
    Check ((Test-Path -LiteralPath (Join-Path $target 'dependency.txt')) -and [IO.File]::ReadAllText((Join-Path $target 'dependency.txt')) -eq 'synthetic-dependency') "$case preserves new dependency"
    Check ((Test-Path -LiteralPath $note) -and [IO.File]::ReadAllText($note) -eq 'preserve this synthetic user file') "$case preserves unmanaged sentinel"
    if($case -eq 'separate-with-float'){Check (Test-Path -LiteralPath (Join-Path $legacy 'DeepSeekFloat.exe')) "$case retains old managed file by policy"}
}
if($Stage -eq 'Upgrade'){
    foreach($case in @('fresh','old-launcher','both-old','locked-current','locked-float','locked-launcher')){
        $local=Assert-InSandbox (Join-Path $OutputDirectory ($case+'/local'))
        $legacy=Assert-InSandbox (Join-Path $local 'Programs/DeepSeekFloat')
        $target=Assert-InSandbox (Join-Path $local 'Programs/DeepSeek-Reflex')
        New-Item -ItemType Directory -Path $legacy,$target -Force | Out-Null
        $current=Join-Path $target 'DeepSeek-Reflex.exe';$oldFloat=Join-Path $legacy 'DeepSeekFloat.exe';$oldLauncher=Join-Path $legacy 'DeepSeek.exe'
        if($case -in @('old-launcher','both-old','locked-launcher')){[IO.File]::WriteAllText($oldLauncher,'old launcher')}
        if($case -in @('both-old','locked-float')){[IO.File]::WriteAllText($oldFloat,'old float')}
        $lockedPath=if($case -eq 'locked-current'){$current}elseif($case -eq 'locked-float'){$oldFloat}elseif($case -eq 'locked-launcher'){$oldLauncher}else{$null}
        $lock=$null
        try{
            if($lockedPath){[IO.File]::WriteAllText($lockedPath,'locked sentinel');$lock=[IO.File]::Open($lockedPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)}
            $code=Invoke-Fixture $local $target
        }finally{if($lock){$lock.Dispose()}}
        if($lockedPath){
            Check ($code -ne 0) "$case rejects an actual write lock"
            Check ([IO.File]::ReadAllText($lockedPath) -eq 'locked sentinel') "$case does not overwrite locked payload"
            Check (-not (Test-Path -LiteralPath (Join-Path $target 'dependency.txt'))) "$case does not partially install"
        }else{
            Check ($code -eq 0) "$case permits installation"
            Check ((Test-Path -LiteralPath $current) -and [IO.File]::ReadAllText($current) -eq 'synthetic-new-payload') "$case installs the new payload"
            Check (Test-Path -LiteralPath (Join-Path $target 'uninstall.exe')) "$case writes the uninstaller"
        }
    }
}
Write-Output "PASS installer $Stage; only generated sandbox files were accessed: $OutputDirectory"
