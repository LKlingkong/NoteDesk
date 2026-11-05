param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$refs = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','WPF\WindowsBase.dll','System.Xaml.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputExe = Join-Path $OutputDirectory "NoteDesk.exe"
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /win32icon:"$PSScriptRoot\assets\NoteDesk.ico" /resource:"$PSScriptRoot\assets\CloudsWallpaper.png",NoteDesk.CloudsWallpaper.png /out:"$outputExe" $refs $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$inputHashes = [ordered]@{}
foreach ($inputPath in @($sources) + @((Join-Path $PSScriptRoot 'assets\NoteDesk.ico'), (Join-Path $PSScriptRoot 'assets\CloudsWallpaper.png'), $PSCommandPath)) {
    $relative = $inputPath.Substring($PSScriptRoot.Length + 1).Replace('\','/')
    $inputHashes[$relative] = (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash
}
[ordered]@{version=(Get-Item -LiteralPath $outputExe).VersionInfo.FileVersion; executableSha256=(Get-FileHash -LiteralPath $outputExe -Algorithm SHA256).Hash; inputs=$inputHashes} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'build-manifest.json') -Encoding UTF8
Write-Output "Built $outputExe"

