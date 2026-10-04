param([string]$ImagePath,[string]$IconPath)
$ErrorActionPreference = 'Stop'
$Project = Split-Path -Parent $PSScriptRoot
if (-not $ImagePath) { $ImagePath = Join-Path $Project 'docs\assets\logo.png' }
if (-not $IconPath) { $IconPath = Join-Path $Project 'webview-host\assets\icon.ico' }
Add-Type -AssemblyName System.Drawing
$Image = [Drawing.Image]::FromFile([IO.Path]::GetFullPath($ImagePath))
$Sizes = @(16,24,32,48,64,128,256)
$Entries = @()
try {
    foreach ($Size in $Sizes) {
        $Bitmap = [Drawing.Bitmap]::new($Size,$Size)
        $Graphics = [Drawing.Graphics]::FromImage($Bitmap)
        $Stream = [IO.MemoryStream]::new()
        try {
            $Graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $Graphics.DrawImage($Image,0,0,$Size,$Size)
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
