$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# A simple geometric keyboard, rendered separately at each Windows icon size.
function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc(($x + $w - $d), $y, $d, $d, 270, 90)
    $path.AddArc(($x + $w - $d), ($y + $h - $d), $d, $d, 0, 90)
    $path.AddArc($x, ($y + $h - $d), $d, $d, 90, 90)
    $path.CloseFigure()
    return ,$path
}

$images = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.ScaleTransform(($size / 256.0), ($size / 256.0))
    $body = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(30, 43, 58))
    $keys = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(231, 242, 248))
    $accent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(65, 211, 182))
    $border = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(65, 211, 182), 9)
    $path = New-RoundedPath 10 45 236 166 20
    $graphics.FillPath($body, $path)
    $graphics.DrawPath($border, $path)
    $path.Dispose()
    for ($row = 0; $row -lt 3; $row++) {
        for ($col = 0; $col -lt 7; $col++) {
            $path = New-RoundedPath (28 + $col * 29) (66 + $row * 32) 21 22 3
            $graphics.FillPath($keys, $path)
            $path.Dispose()
        }
    }
    foreach ($rect in @(@(28, 165, 28, 23), @(65, 165, 126, 23), @(200, 165, 28, 23))) {
        $path = New-RoundedPath $rect[0] $rect[1] $rect[2] $rect[3] 4
        $graphics.FillPath($accent, $path)
        $path.Dispose()
    }
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += [pscustomobject]@{ Size = $size; Data = $stream.ToArray() }
    if ($size -eq 256) { $bitmap.Save((Join-Path $PSScriptRoot 'keyboard-preview.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose()
    $border.Dispose(); $body.Dispose(); $keys.Dispose(); $accent.Dispose()
    $graphics.Dispose(); $bitmap.Dispose()
}

$file = [System.IO.File]::Create((Join-Path $PSScriptRoot 'keyboard.ico'))
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($entry in $images) {
        $dimension = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$entry.Data.Length); $writer.Write([uint32]$offset)
        $offset += $entry.Data.Length
    }
    foreach ($entry in $images) { $writer.Write([byte[]]$entry.Data) }
} finally { $writer.Dispose(); $file.Dispose() }
