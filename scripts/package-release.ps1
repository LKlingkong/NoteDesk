param([Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,[string]$BuildDirectory='')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
if([string]::IsNullOrWhiteSpace($BuildDirectory)){$BuildDirectory=Join-Path $projectRoot 'dist'}
elseif(-not [IO.Path]::IsPathRooted($BuildDirectory)){$BuildDirectory=Join-Path $projectRoot $BuildDirectory}
Push-Location $projectRoot
try {
    $status=@(& git status --porcelain)
    if($LASTEXITCODE -ne 0){throw 'Run packaging from a Git checkout.'}
    if($status.Count -gt 0){throw 'Commit project changes before packaging.'}
    $sourceCommit=(& git rev-parse HEAD).Trim()
    $exe=Join-Path $BuildDirectory 'NoteDesk.exe'
    $manifestPath=Join-Path $BuildDirectory 'build-manifest.json'
    $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne ($Version+'.0')){throw 'EXE version does not match release version.'}
    if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $manifest.executableSha256){throw 'EXE differs from its build manifest.'}
    $expectedInputs=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName.Substring($projectRoot.Length+1).Replace('\','/')})+@('assets/NoteDesk.ico','assets/CloudsWallpaper.png','build.ps1')
    if(@(Compare-Object -ReferenceObject @($expectedInputs | Sort-Object) -DifferenceObject @($manifest.inputs.PSObject.Properties.Name | Sort-Object)).Count -gt 0){throw 'Rebuild required: compiler input set changed.'}
    foreach($inputHash in $manifest.inputs.PSObject.Properties) {
        if((Get-FileHash -LiteralPath (Join-Path $projectRoot $inputHash.Name) -Algorithm SHA256).Hash -ne $inputHash.Value){throw ('Rebuild required: '+$inputHash.Name)}
    }
    $releaseNotes=Join-Path $projectRoot ('docs\releases\v'+$Version+'.md')
    if(-not (Test-Path -LiteralPath $releaseNotes)){throw 'Missing versioned release notes.'}
    foreach($name in @('validation.txt','preview-light.png','preview-dark.png')) {
        if(-not (Test-Path -LiteralPath (Join-Path $BuildDirectory $name))){throw ('Missing verified artifact: '+$name)}
    }
    $output=Join-Path $projectRoot ('dist\release-v'+$Version)
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Copy-Item -LiteralPath $exe -Destination (Join-Path $output 'NoteDesk.exe') -Force
    Copy-Item -LiteralPath $releaseNotes -Destination (Join-Path $output 'release-notes.md') -Force
    $qaRoot=Join-Path $projectRoot '.qa'
    New-Item -ItemType Directory -Path $qaRoot -Force | Out-Null
    $stage=Join-Path $qaRoot ('release-stage-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    try {
        Copy-Item -LiteralPath $exe -Destination (Join-Path $stage 'NoteDesk.exe')
        foreach($directory in @('src','assets','tests','scripts','docs')){Copy-Item -LiteralPath (Join-Path $projectRoot $directory) -Destination (Join-Path $stage $directory) -Recurse}
        foreach($name in @('README.md','RELEASE_NOTES.md','build.ps1')){Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination (Join-Path $stage $name)}
        foreach($name in @('validation.txt','build-manifest.json','preview-light.png','preview-dark.png')){Copy-Item -LiteralPath (Join-Path $BuildDirectory $name) -Destination (Join-Path $stage $name)}
        [ordered]@{version=$Version; sourceCommit=$sourceCommit; executableSha256=$manifest.executableSha256} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'release-manifest.json') -Encoding UTF8
        $archive=Join-Path $output ('NoteDesk-Windows-v'+$Version+'.zip')
        Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -Force
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip=[IO.Compression.ZipFile]::OpenRead($archive)
        try {
            $entry=@($zip.Entries | Where-Object {$_.FullName -eq 'NoteDesk.exe'})
            if($entry.Count -ne 1){throw 'Archive must contain exactly one root executable.'}
            $stream=$entry[0].Open();$sha=[Security.Cryptography.SHA256]::Create()
            try{$zipExeHash=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','')}finally{$sha.Dispose();$stream.Dispose()}
            if($zipExeHash -ne $manifest.executableSha256){throw 'Archive executable hash mismatch.'}
            if(@($zip.Entries | Where-Object {$_.FullName -match '(^|[/\\])(\.git|\.qa|dist)([/\\]|$)|(^|[/\\])notes\.json(\.bak)?$'}).Count -gt 0){throw 'Unexpected build/test data inside archive.'}
            if(-not ($zip.Entries.FullName -contains 'assets/CloudsWallpaper.png' -or $zip.Entries.FullName -contains 'assets\CloudsWallpaper.png')){throw 'Source wallpaper missing from complete package.'}
        }finally{$zip.Dispose()}
        Get-FileHash -LiteralPath (Join-Path $output 'NoteDesk.exe'),$archive -Algorithm SHA256 | ForEach-Object {$_.Hash+'  '+(Split-Path $_.Path -Leaf)} | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
        Write-Output ('Release: '+$output)
        Write-Output ('Source: '+$sourceCommit)
    }finally {
        $resolvedStage=[IO.Path]::GetFullPath($stage)
        $resolvedQa=[IO.Path]::GetFullPath($qaRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
        if(-not $resolvedStage.StartsWith($resolvedQa,[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing to remove staging outside project QA directory.'}
        if(Test-Path -LiteralPath $resolvedStage){Remove-Item -LiteralPath $resolvedStage -Recurse -Force}
    }
}finally {Pop-Location}
