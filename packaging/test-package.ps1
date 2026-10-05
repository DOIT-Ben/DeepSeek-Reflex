param([Parameter(Mandatory)][string]$ZipPath,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) ('DeepSeek-Reflex-package-test-'+[Guid]::NewGuid().ToString('N')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Package test output must be a new directory' }
$Expected = @('DeepSeekFloat.exe','DeepSeek.exe','DeepSeekFloat.exe.config','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','icon.ico','LICENSE','THIRD-PARTY-NOTICES.md','WebView2-LICENSE.txt','WebView2-NOTICE.txt','VERSION','README.md','README.en.md','QUICKSTART.txt','SHA256SUMS.txt')
$Expected += @('docs/assets/logo.png','docs/assets/screenshots/compact.png','docs/assets/screenshots/reading.png','docs/assets/screenshots/selected-text.png','docs/assets/screenshots/settings.png','docs/assets/screenshots/getting-started.png')
$Archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ZipPath))
try {
    $Directories = @('DeepSeek-Reflex/docs/','DeepSeek-Reflex/docs/assets/','DeepSeek-Reflex/docs/assets/screenshots/')
    if (@($Archive.Entries.FullName | Select-Object -Unique).Count -ne $Archive.Entries.Count) { throw 'Duplicate ZIP entries' }
    $Names = @($Archive.Entries | ForEach-Object {
        if ($_.FullName.EndsWith('/')) {
            if ($_.FullName -notin $Directories -or $_.Length -ne 0) { throw "Unexpected ZIP directory: $($_.FullName)" }
            return
        }
        if ($_.FullName -notmatch '^DeepSeek-Reflex/(.+)$' -or $Matches[1] -notin $Expected) { throw "Unexpected ZIP entry: $($_.FullName)" }
        $Matches[1]
    })
    if ($Names.Count -ne $Expected.Count -or @($Names | Select-Object -Unique).Count -ne $Expected.Count) { throw 'Missing or duplicate ZIP entries' }
} finally { $Archive.Dispose() }
Expand-Archive -LiteralPath $ZipPath -DestinationPath $OutputDirectory
$Payload = Join-Path $OutputDirectory 'DeepSeek-Reflex'
$Lines = @(Get-Content -LiteralPath (Join-Path $Payload 'SHA256SUMS.txt'))
if ($Lines.Count -ne $Expected.Count-1) { throw 'Incorrect payload hash count' }
$Hashed = @()
foreach ($Line in $Lines) {
    if ($Line -notmatch '^([a-f0-9]{64})  ([^\\]+)$') { throw 'Invalid payload hash line' }
    $Hash=$Matches[1]; $Name=$Matches[2]
    if ($Name -notin $Expected -or $Name -eq 'SHA256SUMS.txt' -or $Name -in $Hashed) { throw 'Unexpected or duplicate payload hash name' }
    if ((Get-FileHash -LiteralPath (Join-Path $Payload $Name)).Hash.ToLowerInvariant() -ne $Hash) { throw "Payload hash mismatch: $Name" }
    $Hashed += $Name
}
$Version = (Get-Content -LiteralPath (Join-Path $Payload 'VERSION') -Raw).Trim()
$Info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $Payload 'DeepSeekFloat.exe'))
if ($Info.ProductVersion -ne $Version -or $Info.FileVersion -ne "$Version.0" -or $Info.ProductName -ne 'DeepSeek-Reflex') { throw 'ZIP executable version mismatch' }
$LegacyInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $Payload 'DeepSeek.exe'))
if ($LegacyInfo.ProductVersion -ne $Version -or $LegacyInfo.FileVersion -ne "$Version.0" -or $LegacyInfo.ProductName -ne 'DeepSeek-Reflex') { throw 'ZIP legacy launcher version mismatch' }
foreach ($Name in @('README.md','README.en.md')) {
    $Readme = Get-Content -LiteralPath (Join-Path $Payload $Name) -Raw
    if ($Readme -match 'C:\\Users\\HB|E:\\Codex-worksapce|AppData\\Roaming\\[^%]') { throw "Local machine information found in public $Name" }
    $Images = @([regex]::Matches($Readme,'<img\s+[^>]*src="(docs/assets/[^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    if ($Images.Count -ne 6) { throw "README must display the logo and five screenshots: $Name" }
    foreach ($Image in $Images) {
        if ($Image -notin $Expected -or -not (Test-Path -LiteralPath (Join-Path $Payload $Image) -PathType Leaf)) { throw "Missing packaged README image: $Image" }
    }
}
Write-Output "PASS package allowlist ($($Expected.Count) entries), all payload hashes and executable version $Version"
