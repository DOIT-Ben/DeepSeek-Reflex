param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$Project = Split-Path -Parent $Root
$AppVersion = (Get-Content -LiteralPath (Join-Path $Project 'VERSION') -Raw).Trim()
if ($AppVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain a semantic release version' }
[xml]$Manifest = Get-Content -LiteralPath (Join-Path $Root 'app.manifest') -Raw
if ($Manifest.assembly.assemblyIdentity.version -ne "$AppVersion.0") { throw 'Application manifest version must match VERSION' }
$Version = '1.0.4258.31'
$Vendor = Join-Path $Root 'vendor\WebView2'
$SdkFiles = @('lib/net462/Microsoft.Web.WebView2.Core.dll','lib/net462/Microsoft.Web.WebView2.WinForms.dll','runtimes/win-x64/native/WebView2Loader.dll','LICENSE.txt','NOTICE.txt')
$Missing = @($SdkFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $Vendor $_) -PathType Leaf) })
if ($Missing.Count -gt 0) {
    if ($Offline) { throw 'WebView2 SDK not cached; run build.ps1 once online' }
    $Package = Join-Path ([IO.Path]::GetTempPath()) ('DeepSeekFloat-WebView2-'+[Guid]::NewGuid().ToString('N')+'.nupkg')
    try {
        Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$Version/microsoft.web.webview2.$Version.nupkg" -OutFile $Package -TimeoutSec 60
        $Archive = [IO.Compression.ZipFile]::OpenRead($Package)
        try {
            foreach ($Name in $SdkFiles) {
                $Entry = $Archive.GetEntry($Name)
                if ($null -eq $Entry) { throw "SDK package missing $Name" }
                $Destination = Join-Path $Vendor $Name
                New-Item -ItemType Directory -Path (Split-Path -Parent $Destination) -Force | Out-Null
                [IO.Compression.ZipFileExtensions]::ExtractToFile($Entry,$Destination,$true)
            }
        } finally { $Archive.Dispose() }
    } finally { if (Test-Path -LiteralPath $Package) { Remove-Item -LiteralPath $Package } }
}
$Compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $Compiler)) { throw '.NET Framework C# compiler unavailable' }
$Output = Join-Path $Root 'dist'
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$VersionSource = Join-Path $Output 'Version.g.cs'
Set-Content -LiteralPath $VersionSource -Encoding utf8 -Value ('[assembly: System.Reflection.AssemblyVersion("'+$AppVersion+'.0")][assembly: System.Reflection.AssemblyFileVersion("'+$AppVersion+'.0")][assembly: System.Reflection.AssemblyInformationalVersion("'+$AppVersion+'")]')
$Core = Join-Path $Vendor 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$Forms = Join-Path $Vendor 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$Sources = @(Get-ChildItem -LiteralPath $Root -Filter '*.cs' -File | ForEach-Object FullName) + @($VersionSource)
$PreviousLib = $env:LIB
try {
    # This managed build does not use stale machine-wide MSVC library paths.
    $env:LIB = ''
    & $Compiler /nologo /target:winexe /platform:x64 /optimize+ /warnaserror+ "/out:$Output\DeepSeekFloat.exe" "/win32icon:$Root\assets\icon.ico" "/win32manifest:$Root\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" "/reference:$Core" "/reference:$Forms" $Sources
} finally { $env:LIB = $PreviousLib }
if ($LASTEXITCODE -ne 0) { throw "C# build failed: $LASTEXITCODE" }
$PreviousLib = $env:LIB
try {
    $env:LIB = ''
    & $Compiler /nologo /target:winexe /platform:x64 /optimize+ /warnaserror+ "/out:$Output\DeepSeek.exe" "/win32icon:$Root\assets\icon.ico" "/win32manifest:$Root\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll (Join-Path $Root 'compat\LegacyLauncher.cs') (Join-Path $Root 'ShellIdentity.cs') (Join-Path $Root 'AssemblyInfo.cs') $VersionSource
} finally { $env:LIB = $PreviousLib }
if ($LASTEXITCODE -ne 0) { throw "Legacy launcher build failed: $LASTEXITCODE" }
foreach ($File in @($Core,$Forms,(Join-Path $Vendor 'runtimes\win-x64\native\WebView2Loader.dll'))) {
    Copy-Item -LiteralPath $File -Destination $Output -Force
}
Copy-Item -LiteralPath (Join-Path $Root 'app.config') -Destination (Join-Path $Output 'DeepSeekFloat.exe.config') -Force
Copy-Item -LiteralPath (Join-Path $Vendor 'LICENSE.txt') -Destination (Join-Path $Output 'WebView2-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $Vendor 'NOTICE.txt') -Destination (Join-Path $Output 'WebView2-NOTICE.txt') -Force
Copy-Item -LiteralPath (Join-Path $Root 'assets\icon.ico') -Destination $Output -Force
foreach ($Name in @('LICENSE','THIRD-PARTY-NOTICES.md','VERSION')) {
    Copy-Item -LiteralPath (Join-Path $Project $Name) -Destination $Output -Force
}
Write-Output "built: $Output\DeepSeekFloat.exe"
