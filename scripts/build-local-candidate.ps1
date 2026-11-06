$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot
$version = '1.3.0'
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$buildDirectory = Join-Path $project ('dist\build-v' + $version + '-' + $runId)
$candidateDirectory = Join-Path $project ('dist\candidate-v' + $version + '-' + $runId)

# Run the existing regression suite before producing a test package.
& (Join-Path $project 'tests\verify.ps1') -BuildDirectory $buildDirectory -SkipDesktop
$executable = Join-Path $buildDirectory 'NoteDesk.exe'
$validation = Join-Path $buildDirectory 'validation.txt'
if (-not (Test-Path -LiteralPath $validation -PathType Leaf)) {
    throw 'Validation report missing. Candidate packaging stopped.'
}
if ((Get-Item -LiteralPath $executable).VersionInfo.FileVersion -ne ($version + '.0')) {
    throw 'Executable version does not match the candidate version.'
}

New-Item -ItemType Directory -Path $candidateDirectory | Out-Null
$files = @(
    $executable
    $validation
    (Join-Path $project ('docs\releases\v' + $version + '.md'))
)
Copy-Item -LiteralPath $files -Destination $candidateDirectory
$archive = Join-Path $candidateDirectory ('NoteDesk-Windows-v' + $version + '-test.zip')
$packageFiles = @(Get-ChildItem -LiteralPath $candidateDirectory -File | ForEach-Object FullName)
Compress-Archive -LiteralPath $packageFiles -DestinationPath $archive

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    $entry = $zip.GetEntry('NoteDesk.exe')
    if ($null -eq $entry) {
        throw 'Executable missing from candidate archive.'
    }
    $stream = $entry.Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $packedHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
    } finally {
        $stream.Dispose()
        $sha.Dispose()
    }
    if ($packedHash -ne (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash) {
        throw 'Candidate archive contains a different executable.'
    }
} finally {
    $zip.Dispose()
}

Get-FileHash -LiteralPath (Join-Path $candidateDirectory 'NoteDesk.exe'), $archive -Algorithm SHA256 |
    ForEach-Object { $_.Hash + '  ' + (Split-Path $_.Path -Leaf) } |
    Set-Content -LiteralPath (Join-Path $candidateDirectory 'SHA256SUMS.txt') -Encoding ASCII
Write-Output ('Local candidate: ' + $candidateDirectory)
