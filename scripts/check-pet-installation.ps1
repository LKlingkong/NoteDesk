param(
    [string]$DataRoot = (Join-Path $env:LOCALAPPDATA 'NoteDesk')
)

$ErrorActionPreference = 'Stop'

$running = @(Get-Process -Name NoteDesk -ErrorAction SilentlyContinue)
if ($running.Count -eq 0) {
    Write-Output 'PROCESS: NoteDesk is not running.'
}
foreach ($process in $running) {
    if ($process.Path) {
        $version = (Get-Item -LiteralPath $process.Path).VersionInfo.FileVersion
        Write-Output ('PROCESS: ' + $version + ' | ' + $process.Path)
    }
}

$petRoot = Join-Path $DataRoot 'pets'
if (-not (Test-Path -LiteralPath $petRoot -PathType Container)) {
    Write-Output 'RESULT: NO_PET_RESOURCES'
    return
}

$settingsFile = Join-Path $petRoot 'settings.json'
$settings = $null
if (Test-Path -LiteralPath $settingsFile -PathType Leaf) {
    try {
        $settings = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        Write-Output 'RESULT: INVALID_PET_SETTINGS'
        return
    }
}

$installed = @(foreach ($file in (Get-ChildItem -LiteralPath $petRoot -File -Filter '*.pet.json')) {
    try {
        $pet = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($pet.Key -match '^[a-f0-9]{64}$' -and
            (Test-Path -LiteralPath (Join-Path $petRoot ($pet.Key + '.image')) -PathType Leaf)) {
            $pet
        }
    } catch {
        Write-Warning ('Unreadable descriptor: ' + $file.Name)
    }
})
Write-Output ('INSTALLED: ' + $installed.Count)
$selected = $installed | Where-Object { $_.Key -eq $settings.Selected } | Select-Object -First 1
if ($null -eq $selected) {
    $selected = $installed | Where-Object { $_.Id -eq 'xiaolemi' } | Select-Object -First 1
}
if ($null -eq $selected) {
    Write-Output 'RESULT: NO_SELECTED_PET'
    return
}

Write-Output ('SELECTED: ' + $selected.Name)
Write-Output ('DISPLAY_ENABLED: ' + ($settings.Enabled -eq $true))
Add-Type -AssemblyName WindowsBase,PresentationCore
$asset = [IO.File]::OpenRead((Join-Path $petRoot ($selected.Key + '.image')))
try {
    $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create(
        $asset,
        [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
        [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
    )
    $image = $decoder.Frames[0]
    Write-Output ('IMAGE: ' + $image.PixelWidth + 'x' + $image.PixelHeight)
    if ($selected.Animated -and ($image.PixelWidth -ne 1536 -or $image.PixelHeight -ne 2288)) {
        Write-Output 'RESULT: INVALID_ATLAS_DIMENSIONS'
        return
    }
} catch {
    Write-Output ('RESULT: IMAGE_DECODE_FAILED | ' + $_.Exception.GetBaseException().Message)
    return
} finally {
    $asset.Dispose()
}

if ($settings.Enabled -ne $true) {
    Write-Output 'RESULT: PET_DISPLAY_DISABLED'
} else {
    Write-Output 'RESULT: ASSETS_AND_SETTINGS_VALID'
}
