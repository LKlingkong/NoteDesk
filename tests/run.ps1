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

$interactionExe=Join-Path $qaRoot 'InteractionTests.exe'
& $compiler /nologo /target:exe /main:InteractionTests /codepage:65001 /resource:"$projectRoot\assets\CloudsWallpaper.png",NoteDesk.CloudsWallpaper.png /out:$interactionExe $refs $sources (Join-Path $PSScriptRoot 'InteractionTests.cs')
if($LASTEXITCODE -ne 0){throw 'Interaction test compilation failed'}
& $interactionExe
if($LASTEXITCODE -ne 0){throw 'Interaction tests failed'}

$opacityExe=Join-Path $qaRoot 'NoteTransparencyTests.exe'
& $compiler /nologo /target:exe /main:NoteTransparencyTests /codepage:65001 /resource:"$projectRoot\assets\CloudsWallpaper.png",NoteDesk.CloudsWallpaper.png /out:$opacityExe $refs $sources (Join-Path $PSScriptRoot 'NoteTransparencyTests.cs')
if($LASTEXITCODE -ne 0){throw 'Note transparency test compilation failed'}
& $opacityExe
if($LASTEXITCODE -ne 0){throw 'Note transparency tests failed'}

$designExe=Join-Path $qaRoot 'NoteDesignTests.exe'
& $compiler /nologo /target:exe /main:NoteDesignTests /codepage:65001 /resource:"$projectRoot\assets\CloudsWallpaper.png",NoteDesk.CloudsWallpaper.png /out:$designExe $refs $sources (Join-Path $PSScriptRoot 'NoteDesignTests.cs')
if($LASTEXITCODE -ne 0){throw 'Note design test compilation failed'}
& $designExe
if($LASTEXITCODE -ne 0){throw 'Note design tests failed'}
