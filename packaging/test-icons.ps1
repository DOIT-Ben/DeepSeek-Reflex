param([Parameter(Mandatory)][string]$IconPath,[Parameter(Mandatory)][string[]]$BinaryPaths)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('ReflexIconCheck' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ReflexIconCheck {
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] public static extern uint ExtractIconEx(string path,int index,[Out] IntPtr[] large,[Out] IntPtr[] small,uint count);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
}
'@
}
function Get-IconPixels([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing icon input: $Path" }
    $large = [IntPtr[]]::new(1); $small = [IntPtr[]]::new(1)
    try {
        if ([ReflexIconCheck]::ExtractIconEx($Path,0,$large,$small,1) -lt 1 -or $large[0] -eq [IntPtr]::Zero) { throw "No icon resource: $Path" }
        $icon = [Drawing.Icon]::FromHandle($large[0])
        try {
            $bitmap = $icon.ToBitmap()
            try {
                $pixels = [Collections.Generic.List[int]]::new()
                for ($y=0;$y -lt $bitmap.Height;$y++) { for($x=0;$x -lt $bitmap.Width;$x++) { $pixels.Add($bitmap.GetPixel($x,$y).ToArgb()) } }
                return [pscustomobject]@{Width=$bitmap.Width;Height=$bitmap.Height;Pixels=($pixels -join ',')}
            } finally { $bitmap.Dispose() }
        } finally { $icon.Dispose() }
    } finally {
        if($large[0] -ne [IntPtr]::Zero){[ReflexIconCheck]::DestroyIcon($large[0])|Out-Null}
        if($small[0] -ne [IntPtr]::Zero){[ReflexIconCheck]::DestroyIcon($small[0])|Out-Null}
    }
}
$bytes=[IO.File]::ReadAllBytes($IconPath)
if($bytes.Length -lt 6 -or [BitConverter]::ToUInt16($bytes,2) -ne 1){throw 'Invalid ICO header'}
$count=[BitConverter]::ToUInt16($bytes,4)
if($bytes.Length -lt 6+16*$count){throw 'Truncated ICO directory'}
$sizes=@(for($i=0;$i -lt $count;$i++) { $size=[int]$bytes[6+16*$i];if($size -eq 0){256}else{$size} })
if(($sizes -join ',') -ne '16,24,32,48,64,128,256'){throw 'ICO sizes mismatch'}
Write-Output ('PASS multi-resolution ICO sizes: '+($sizes -join ', '))
$baseline = Get-IconPixels $IconPath
foreach($path in $BinaryPaths) {
    $actual=Get-IconPixels $path
    if($actual.Width -ne $baseline.Width -or $actual.Height -ne $baseline.Height -or $actual.Pixels -ne $baseline.Pixels) { throw "Embedded icon mismatch: $path" }
    Write-Output "PASS embedded icon pixels match source: $path"
}
