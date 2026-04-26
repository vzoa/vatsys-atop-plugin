param(
    [Parameter(Mandatory = $true)]
    [string]$InputPcf,

    [string]$FamilyName,

    [string]$OutDir = "webfonts",

    [switch]$GenerateCSharpHelper,

    [string]$CSharpNamespace = "AtopPlugin.Fonts",

    [string]$CSharpClassName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ToolPath {
    param([Parameter(Mandatory = $true)][string]$Name)

    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $cmd) {
        if ($Name -eq "fontforge") {
            $known = @(
                "C:\\Program Files\\FontForgeBuilds\\bin\\fontforge.exe",
                "C:\\Program Files\\FontForgeBuilds\\fontforge.exe",
                "C:\\Program Files\\FontForge\\bin\\fontforge.exe",
                "C:\\Program Files\\FontForge\\fontforge.exe"
            )

            $resolved = $known | Where-Object { Test-Path $_ } | Select-Object -First 1
            if ($resolved) {
                return $resolved
            }
        }

        throw "Required tool '$Name' was not found in PATH. Install it first and retry."
    }
    return $cmd.Source
}

function Get-SafeName {
    param([Parameter(Mandatory = $true)][string]$Name)

    $safe = $Name -replace "[^A-Za-z0-9_-]", "_"
    if ([string]::IsNullOrWhiteSpace($safe)) {
        return "bitmap_font"
    }
    return $safe
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$resolvedInput = (Resolve-Path $InputPcf).Path

if (-not (Test-Path $resolvedInput)) {
    throw "Input file does not exist: $InputPcf"
}

$leaf = [System.IO.Path]::GetFileName($resolvedInput).ToLowerInvariant()
if (-not ($leaf.EndsWith(".pcf") -or $leaf.EndsWith(".pcf.gz"))) {
    throw "Input must be a .pcf or .pcf.gz file."
}

$fontForge = Get-ToolPath -Name "fontforge"
$woff2Compress = Get-Command "woff2_compress" -ErrorAction SilentlyContinue

$baseName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedInput)
if ($leaf.EndsWith(".pcf.gz")) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($baseName)
}

if ([string]::IsNullOrWhiteSpace($FamilyName)) {
    $FamilyName = $baseName
}

$safeBase = Get-SafeName -Name $baseName
$safeFamily = Get-SafeName -Name $FamilyName

$outPath = Join-Path $projectRoot $OutDir
New-Item -ItemType Directory -Path $outPath -Force | Out-Null

$ttfPath = Join-Path $outPath ("$safeBase.ttf")
$woffPath = Join-Path $outPath ("$safeBase.woff")
$woff2Path = Join-Path $outPath ("$safeBase.woff2")
$cssPath = Join-Path $outPath ("$safeBase.css")

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("pcf-convert-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    $ffScriptPath = Join-Path $tempDir "convert.py"
    $escapedInput = $resolvedInput.Replace("\\", "\\\\")
    $escapedTtf = $ttfPath.Replace("\\", "\\\\")
    $escapedWoff = $woffPath.Replace("\\", "\\\\")
    $escapedWoff2 = $woff2Path.Replace("\\", "\\\\")

    $ffScript = @"
import fontforge

font = fontforge.open(r"$escapedInput")
font.familyname = "$safeFamily"
font.fullname = "$safeFamily"
font.fontname = "$safeFamily"
font.generate(r"$escapedTtf")
font.generate(r"$escapedWoff")
try:
    font.generate(r"$escapedWoff2")
except Exception:
    pass
"@

    Set-Content -Path $ffScriptPath -Value $ffScript -Encoding ASCII

    Write-Host "Converting PCF to TTF with FontForge..."
    & $fontForge -script $ffScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw "FontForge failed with exit code $LASTEXITCODE"
    }

    $hasTtf = Test-Path $ttfPath
    $hasWoff = Test-Path $woffPath
    $hasWoff2 = Test-Path $woff2Path

    if (-not ($hasTtf -or $hasWoff -or $hasWoff2)) {
        throw "Conversion failed: FontForge did not produce usable output for '$resolvedInput'."
    }

    if (-not $hasWoff2 -and $woff2Compress -and $hasTtf) {
        Write-Host "Creating WOFF2..."
        & $woff2Compress.Source $ttfPath
        if ($LASTEXITCODE -ne 0) {
            throw "woff2_compress failed with exit code $LASTEXITCODE"
        }
        $hasWoff2 = Test-Path $woff2Path
    }
    elseif (-not $hasWoff2 -and -not $hasTtf) {
        Write-Warning "WOFF2 not generated for this bitmap font."
    }
    elseif (-not $hasWoff2) {
        Write-Warning "woff2_compress not found. Only TTF will be generated."
    }

    $fontSources = @()
    if (Test-Path $woff2Path) { $fontSources += "url('./$safeBase.woff2') format('woff2')" }
    if (Test-Path $woffPath) { $fontSources += "url('./$safeBase.woff') format('woff')" }
    if (Test-Path $ttfPath) { $fontSources += "url('./$safeBase.ttf') format('truetype')" }
    if ($fontSources.Count -eq 0) {
        throw "No web font outputs found for '$safeBase'."
    }
    $fontSourceText = $fontSources -join ",`n       "

    $css = @"
/* Generated from $leaf */
@font-face {
  font-family: '$FamilyName';
  src: $fontSourceText;
  font-display: swap;
}
"@

    Set-Content -Path $cssPath -Value $css -Encoding UTF8

    if ($GenerateCSharpHelper) {
        if (-not (Test-Path $ttfPath)) {
            Write-Warning "Skipping C# helper generation because no TTF was produced for '$safeBase'."
        }
        else {
        if ([string]::IsNullOrWhiteSpace($CSharpClassName)) {
            $CSharpClassName = (Get-SafeName -Name $safeFamily) + "FontLoader"
        }

        $csPath = Join-Path $outPath ("$CSharpClassName.cs")
        $cs = @"
using System;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace $CSharpNamespace;

public static class $CSharpClassName
{
    public const string FamilyName = "$FamilyName";

    public static PrivateFontCollection LoadFromFile(string ttfPath)
    {
        if (string.IsNullOrWhiteSpace(ttfPath) || !File.Exists(ttfPath))
            throw new FileNotFoundException("TTF file not found", ttfPath);

        var fonts = new PrivateFontCollection();
        fonts.AddFontFile(ttfPath);
        return fonts;
    }

    public static PrivateFontCollection LoadFromEmbeddedResource(string resourceName, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource '{resourceName}' not found.");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        var handle = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, handle, bytes.Length);
            var fonts = new PrivateFontCollection();
            fonts.AddMemoryFont(handle, bytes.Length);
            return fonts;
        }
        finally
        {
            Marshal.FreeCoTaskMem(handle);
        }
    }
}
"@

        Set-Content -Path $csPath -Value $cs -Encoding UTF8
        Write-Host "Generated C# helper: $csPath"
        }
    }

    Write-Host "Done."
    if (Test-Path $ttfPath) { Write-Host "TTF:   $ttfPath" }
    if (Test-Path $woffPath) { Write-Host "WOFF:  $woffPath" }
    if (Test-Path $woff2Path) { Write-Host "WOFF2: $woff2Path" }
    Write-Host "CSS:   $cssPath"
}
finally {
    if (Test-Path $tempDir) {
        Remove-Item -LiteralPath $tempDir -Recurse -Force
    }
}
