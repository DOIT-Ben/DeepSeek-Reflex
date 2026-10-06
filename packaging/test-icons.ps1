param([Parameter(Mandatory)][string]$IconPath,[Parameter(Mandatory)][string[]]$BinaryPaths)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('ReflexIconResources' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ReflexIconResources {
    private delegate bool Callback(IntPtr module,IntPtr type,IntPtr name,IntPtr parameter);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
    [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern bool EnumResourceNames(IntPtr module,IntPtr type,Callback callback,IntPtr parameter);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr FindResource(IntPtr module,IntPtr name,IntPtr type);
    [DllImport("kernel32.dll")] private static extern uint SizeofResource(IntPtr module,IntPtr resource);
    [DllImport("kernel32.dll")] private static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
    [DllImport("kernel32.dll")] private static extern IntPtr LockResource(IntPtr resource);
    private static byte[] Read(IntPtr module,IntPtr name,int type) {
        var resource=FindResource(module,name,new IntPtr(type));
        if(resource==IntPtr.Zero)throw new Exception("Missing icon resource");
        var bytes=new byte[SizeofResource(module,resource)];
        Marshal.Copy(LockResource(LoadResource(module,resource)),bytes,0,bytes.Length);return bytes;
    }
    public static byte[][] Frames(string path) {
        // Read the executable as data, without running application code.
        var module=LoadLibraryEx(path,IntPtr.Zero,2);
        if(module==IntPtr.Zero)throw new Exception("Cannot inspect icon resources");
        try {
            byte[] group=null;
            Callback callback=(m,t,n,p)=>{group=Read(module,n,14);return false;};
            EnumResourceNames(module,new IntPtr(14),callback,IntPtr.Zero);
            if(group==null||group.Length<6)throw new Exception("No main icon group");
            int count=BitConverter.ToUInt16(group,4);
            if(group.Length<6+14*count)throw new Exception("Truncated icon group");
            var frames=new byte[count][];
            for(int i=0;i<count;i++)frames[i]=Read(module,new IntPtr(BitConverter.ToUInt16(group,6+14*i+12)),3);
            return frames;
        }finally{FreeLibrary(module);}
    }
}
'@
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
# Compare every encoded frame directly: shell extraction may choose different
# source sizes for ICO and EXE at high DPI even with identical embedded bytes.
$baseline=@(for($i=0;$i -lt $count;$i++){
    $length=[BitConverter]::ToUInt32($bytes,6+16*$i+8)
    $offset=[BitConverter]::ToUInt32($bytes,6+16*$i+12)
    [Convert]::ToBase64String($bytes,[int]$offset,[int]$length)
})
foreach($path in $BinaryPaths) {
    if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing icon input: $path"}
    $frames=[ReflexIconResources]::Frames([IO.Path]::GetFullPath($path))
    $actual=@($frames | ForEach-Object {[Convert]::ToBase64String($_)})
    if($actual.Count -ne $baseline.Count -or (Compare-Object $baseline $actual)){throw "Embedded icon mismatch: $path"}
    Write-Output "PASS all seven embedded icon frames match source bytes: $path"
}
