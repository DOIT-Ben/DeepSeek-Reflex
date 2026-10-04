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
for($i=0;$i -lt $count;$i++) {
    $length=[BitConverter]::ToUInt32($bytes,6+16*$i+8)
    $offset=[BitConverter]::ToUInt32($bytes,6+16*$i+12)
    if($offset+$length -gt $bytes.Length){throw 'Truncated ICO image'}
    $stream=[IO.MemoryStream]::new($bytes,[int]$offset,[int]$length)
    $bitmap=[Drawing.Bitmap]::new($stream)
    try {
        $size=$sizes[$i];$transparent=0;$visible=0;$left=$size;$right=-1;$top=$size;$bottom=-1
        if($bitmap.Width -ne $size -or $bitmap.Height -ne $size){throw 'ICO image dimensions mismatch'}
        for($y=0;$y -lt $size;$y++){for($x=0;$x -lt $size;$x++){
            $alpha=$bitmap.GetPixel($x,$y).A
            if($alpha -eq 0){$transparent++}
            if($alpha -ge 32){$visible++;$left=[Math]::Min($left,$x);$right=[Math]::Max($right,$x);$top=[Math]::Min($top,$y);$bottom=[Math]::Max($bottom,$y)}
        }}
        foreach($point in @(@(0,0),@(($size-1),0),@(0,($size-1)),@(($size-1),($size-1)))){
            if($bitmap.GetPixel($point[0],$point[1]).A -ne 0){throw "Opaque background in ${size}px ICO"}
        }
        $width=$right-$left+1;$height=$bottom-$top+1
        if($transparent -lt $size*$size*0.25 -or $visible -lt $size*$size*0.20 -or $width -lt $size*0.85){throw "Invisible, undersized or opaque ${size}px ICO"}
        if($height -gt $width*0.75 -or $height -lt $width*0.45){throw "Distorted fish aspect ratio in ${size}px ICO"}
        Write-Output "PASS transparent ${size}px ICO, subject ${width}x${height}"
    } finally {$bitmap.Dispose();$stream.Dispose()}
}
$baseline = Get-IconPixels $IconPath
foreach($path in $BinaryPaths) {
    $actual=Get-IconPixels $path
    if($actual.Width -ne $baseline.Width -or $actual.Height -ne $baseline.Height -or $actual.Pixels -ne $baseline.Pixels) { throw "Embedded icon mismatch: $path" }
    Write-Output "PASS embedded icon pixels match source: $path"
}
