# vatSys ATOP Plugin

vatSys plugin to provide ATOP-like strips, labels, and additional functionality into the
[vatSys](https://virtualairtrafficsystem.com/) controller client for VATSIM.

### Contributing

Please use the issues functionality to suggest new functionality or report bugs.

If you want to contribute code, you'll have to create a new branch and then submit a pull request.

### Development Environment Setup

This project uses .NET Framework 4.8, so you will need to have the SDK installed. Additionally, you will need vatSys to
be installed.

The project requires `vatSys.exe` as a reference. You can do this by adding the `bin` folder of your vatSys install to
the reference paths setup within your IDE (e.g. `C:\Program Files (x86)\vatSys\bin\`).

### PCF Bitmap Font Conversion (Web + C#)

This repository includes a helper script at [tools/convert-pcf-font.ps1](tools/convert-pcf-font.ps1) to convert X11
bitmap `.pcf` / `.pcf.gz` fonts into assets usable by both the web UI and the C# plugin.

Requirements:

- `fontforge` available in `PATH`
- Optional: `woff2_compress` for `.woff2` generation

Example usage from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\convert-pcf-font.ps1 `
	-InputPcf .\fonts\myfont.pcf.gz `
	-FamilyName "My Bitmap Font" `
	-OutDir webfonts `
	-GenerateCSharpHelper `
	-CSharpNamespace "AtopPlugin.Fonts"
```

Outputs:

- `webfonts/<name>.woff` and/or `webfonts/<name>.woff2` (preferred for web)
- optional `webfonts/<name>.ttf` (depends on source font capabilities)
- `webfonts/<name>.css` with generated `@font-face`
- optional `webfonts/<ClassName>.cs` loader helper for C# (generated when TTF is available)

Batch-convert all MEARTS PCF fonts:

```powershell
Get-ChildItem -Path .\UI\MEARTS Fonts -Filter *.pcf | ForEach-Object {
	.\tools\convert-pcf-font.ps1 -InputPcf $_.FullName -FamilyName ("MEARTS " + $_.BaseName) -OutDir 'webfonts/mearts'
}
```