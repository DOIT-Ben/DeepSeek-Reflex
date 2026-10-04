param([string]$ImagePath,[string]$IconPath)
$ErrorActionPreference = 'Stop'
$Project = Split-Path -Parent $PSScriptRoot
if (-not $ImagePath) { $ImagePath = Join-Path $Project 'docs\assets\logo.png' }
if (-not $IconPath) { $IconPath = Join-Path $Project 'webview-host\assets\icon.ico' }
Add-Type -AssemblyName System.Drawing
$Image = [Drawing.Bitmap]::new([IO.Path]::GetFullPath($ImagePath))
$Bounds = [Drawing.Rectangle]::Empty
if (-not ('ReflexAlphaBounds' -as [type])) {
    Add-Type -ReferencedAssemblies ([Drawing.Bitmap].Assembly.Location),([Drawing.Rectangle].Assembly.Location) @'
using System.Drawing;
public static class ReflexAlphaBounds {
    public static Rectangle Find(Bitmap image) {
        int left=image.Width,top=image.Height,right=-1,bottom=-1;
        bool transparent=false;
        for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) {
            int alpha=image.GetPixel(x,y).A;
            if(alpha==0)transparent=true;
            if(alpha>=16){left=System.Math.Min(left,x);top=System.Math.Min(top,y);right=System.Math.Max(right,x);bottom=System.Math.Max(bottom,y);}
        }
        if(!transparent||right<left)throw new System.InvalidOperationException("Logo must contain a visible subject and real transparent background");
        return Rectangle.FromLTRB(System.Math.Max(0,left-2),System.Math.Max(0,top-2),System.Math.Min(image.Width,right+3),System.Math.Min(image.Height,bottom+3));
    }
}
'@
}
$Sizes = @(16,24,32,48,64,128,256)
$Entries = @()
try {
    $Bounds = [ReflexAlphaBounds]::Find($Image)
    foreach ($Size in $Sizes) {
        $Bitmap = [Drawing.Bitmap]::new($Size,$Size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $Graphics = [Drawing.Graphics]::FromImage($Bitmap)
        $Stream = [IO.MemoryStream]::new()
        try {
            $Graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $Graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $Graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $Graphics.Clear([Drawing.Color]::Transparent)
            # Fit the alpha bounds, not the square source canvas. Preserve the fish's aspect ratio.
            $Padding = [Math]::Max(0.5,$Size*0.02)
            $Scale = ($Size-2*$Padding)/[Math]::Max($Bounds.Width,$Bounds.Height)
            $Width = [float]($Bounds.Width*$Scale); $Height = [float]($Bounds.Height*$Scale)
            $Target = [Drawing.RectangleF]::new([float](($Size-$Width)/2),[float](($Size-$Height)/2),$Width,$Height)
            $Source = [Drawing.RectangleF]::new($Bounds.X,$Bounds.Y,$Bounds.Width,$Bounds.Height)
            $Graphics.DrawImage($Image,$Target,$Source,[Drawing.GraphicsUnit]::Pixel)
            $Bitmap.Save($Stream,[Drawing.Imaging.ImageFormat]::Png)
            $Entries += ,([pscustomobject]@{Size=$Size;Bytes=$Stream.ToArray()})
        } finally { $Stream.Dispose(); $Graphics.Dispose(); $Bitmap.Dispose() }
    }
} finally { $Image.Dispose() }
$Output = [IO.File]::Create([IO.Path]::GetFullPath($IconPath))
$Writer = [IO.BinaryWriter]::new($Output)
try {
    $Writer.Write([uint16]0); $Writer.Write([uint16]1); $Writer.Write([uint16]$Entries.Count)
    $Offset = 6 + 16*$Entries.Count
    foreach ($Entry in $Entries) {
        $Dimension = if ($Entry.Size -eq 256) { 0 } else { $Entry.Size }
        $Writer.Write([byte]$Dimension); $Writer.Write([byte]$Dimension)
        $Writer.Write([byte]0); $Writer.Write([byte]0)
        $Writer.Write([uint16]1); $Writer.Write([uint16]32)
        $Writer.Write([uint32]$Entry.Bytes.Length); $Writer.Write([uint32]$Offset)
        $Offset += $Entry.Bytes.Length
    }
    foreach ($Entry in $Entries) { $Writer.Write([byte[]]$Entry.Bytes) }
} finally { $Writer.Dispose() }
Write-Output "Exported multi-resolution ICO: $IconPath"
Write-Output "Transparent subject bounds: $Bounds"
