$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$assetDirectory = Join-Path $PSScriptRoot 'MemoPrise\Assets'
$drawing = [Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $assetDirectory 'Caducee.xaml')))
$frames = @()
foreach ($size in @(16,20,24,32,40,48,64,128,256,512)) {
    $visual = New-Object Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $context.DrawImage($drawing, (New-Object Windows.Rect(0,0,$size,$size)))
    $context.Close()
    $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object IO.MemoryStream
    $encoder.Save($stream)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    if ($size -eq 512) { [IO.File]::WriteAllBytes((Join-Path $assetDirectory 'MemoPrise.png'),$bytes) }
    else { $frames += [PSCustomObject]@{Size=$size;Bytes=$bytes} }
}
$output = [IO.File]::Create((Join-Path $assetDirectory 'MemoPrise.ico'))
$writer = New-Object IO.BinaryWriter($output)
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) {0} else {$frame.Size}
        $writer.Write([Byte]$dimension); $writer.Write([Byte]$dimension)
        $writer.Write([Byte]0); $writer.Write([Byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frame.Bytes.Length); $writer.Write([UInt32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([Byte[]]$frame.Bytes) }
} finally { $writer.Dispose(); $output.Dispose() }
