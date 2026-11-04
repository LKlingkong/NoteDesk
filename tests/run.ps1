$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
$qaRoot=Join-Path $projectRoot '.qa'
New-Item -ItemType Directory -Force -Path $qaRoot | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework=Split-Path $compiler
$refs=@('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','WPF\WindowsBase.dll','System.Xaml.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:'+(Join-Path $framework $_) }
$sources=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$testExe=Join-Path $qaRoot 'UpgradeTests.exe'
& $compiler /nologo /target:exe /main:UpgradeTests /codepage:65001 /resource:"$projectRoot\assets\CloudsWallpaper.png",NoteDesk.CloudsWallpaper.png /out:$testExe $refs $sources (Join-Path $PSScriptRoot 'UpgradeTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& $testExe @args
if($LASTEXITCODE -ne 0){throw 'Upgrade tests failed'}
$wallpaperExe=Join-Path $qaRoot 'WallpaperTests.exe'
& $compiler /nologo /target:exe /main:WallpaperTests /codepage:65001 /resource:"$projectRoot\assets\CloudsWallpaper.png",NoteDesk.CloudsWallpaper.png /out:$wallpaperExe $refs $sources (Join-Path $PSScriptRoot 'WallpaperTests.cs')
if($LASTEXITCODE -ne 0){throw 'Wallpaper test compilation failed'}
& $wallpaperExe
if($LASTEXITCODE -ne 0){throw 'Wallpaper tests failed'}
