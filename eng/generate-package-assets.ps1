<#
.SYNOPSIS
    Draws the five images the MSIX manifest points at.

.DESCRIPTION
    The images are versioned because the package needs them, but a binary nobody can regenerate is a
    binary nobody can review. This script is the source: it draws from the product's own design
    tokens, so the tile a user sees and the application behind it are the same two colours.

    Rerun it after changing a token and commit the result; the packaging script never calls it,
    because a release must not be able to change its own icons.
#>
[CmdletBinding()]
param(
    [string]$Output = 'src/ApSolutions.LocalMedia.Windows.Package/Assets'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $Output))
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

# Theme/DesignTokens.axaml: the dark surface and the accent that reads on it.
$surface = [System.Drawing.ColorTranslator]::FromHtml('#111827')
$accent = [System.Drawing.ColorTranslator]::FromHtml('#7CC4FF')

# Square44x44 has to survive being drawn at 16 pixels on a taskbar, so the mark is one shape: a
# rounded frame in the accent with the play triangle punched out of it in the surface colour.
function New-Mark {
    param(
        [System.Drawing.Graphics]$Graphics,
        [single]$X,
        [single]$Y,
        [single]$Size
    )

    $radius = $Size * 0.22
    $frame = New-Object System.Drawing.Drawing2D.GraphicsPath
    $frame.AddArc($X, $Y, $radius * 2, $radius * 2, 180, 90)
    $frame.AddArc($X + $Size - ($radius * 2), $Y, $radius * 2, $radius * 2, 270, 90)
    $frame.AddArc($X + $Size - ($radius * 2), $Y + $Size - ($radius * 2), $radius * 2, $radius * 2, 0, 90)
    $frame.AddArc($X, $Y + $Size - ($radius * 2), $radius * 2, $radius * 2, 90, 90)
    $frame.CloseFigure()

    $accentBrush = New-Object System.Drawing.SolidBrush $accent
    $Graphics.FillPath($accentBrush, $frame)

    $triangle = @(
        (New-Object System.Drawing.PointF(($X + $Size * 0.38), ($Y + $Size * 0.28))),
        (New-Object System.Drawing.PointF(($X + $Size * 0.38), ($Y + $Size * 0.72))),
        (New-Object System.Drawing.PointF(($X + $Size * 0.72), ($Y + $Size * 0.50)))
    )
    $surfaceBrush = New-Object System.Drawing.SolidBrush $surface
    $Graphics.FillPolygon($surfaceBrush, $triangle)

    $accentBrush.Dispose()
    $surfaceBrush.Dispose()
    $frame.Dispose()
}

function New-Asset {
    param(
        [string]$Name,
        [int]$Width,
        [int]$Height,
        [switch]$WithWordmark
    )

    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $graphics.Clear($surface)

    if ($WithWordmark) {
        $markSize = [single]($Height * 0.56)
        $left = [single]($Width * 0.08)
        $top = [single](($Height - $markSize) / 2)
        New-Mark -Graphics $graphics -X $left -Y $top -Size $markSize

        $font = New-Object System.Drawing.Font('Segoe UI Semibold', ($Height * 0.20), [System.Drawing.GraphicsUnit]::Pixel)
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        $format = New-Object System.Drawing.StringFormat
        $format.LineAlignment = [System.Drawing.StringAlignment]::Center
        $graphics.DrawString(
            'AP Reelume',
            $font,
            $brush,
            (New-Object System.Drawing.RectangleF(($left + $markSize + ($Width * 0.05)), 0, ($Width * 0.75), $Height)),
            $format)
        $font.Dispose()
        $brush.Dispose()
        $format.Dispose()
    }
    else {
        $markSize = [single]([Math]::Min($Width, $Height) * 0.76)
        New-Mark -Graphics $graphics `
            -X ([single](($Width - $markSize) / 2)) `
            -Y ([single](($Height - $markSize) / 2)) `
            -Size $markSize
    }

    $path = Join-Path $outputRoot $Name
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Output "$Name ($Width x $Height)"
}

New-Asset -Name 'Square44x44Logo.png' -Width 44 -Height 44
New-Asset -Name 'Square150x150Logo.png' -Width 150 -Height 150
New-Asset -Name 'StoreLogo.png' -Width 50 -Height 50
New-Asset -Name 'Wide310x150Logo.png' -Width 310 -Height 150 -WithWordmark
New-Asset -Name 'SplashScreen.png' -Width 620 -Height 300 -WithWordmark

Write-Output "Assets written to $outputRoot"
