param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$refs = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','WPF\WindowsBase.dll','System.Xaml.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputExe = Join-Path $OutputDirectory "NoteDesk.exe"
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /win32icon:"$PSScriptRoot\assets\NoteDesk.ico" /out:"$outputExe" $refs "$PSScriptRoot\src\NoteDesk.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output "Built $outputExe"

