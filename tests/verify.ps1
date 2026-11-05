param([string]$BuildDirectory='')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
if([string]::IsNullOrWhiteSpace($BuildDirectory)){$BuildDirectory=Join-Path $projectRoot 'dist'}
elseif(-not [IO.Path]::IsPathRooted($BuildDirectory)){$BuildDirectory=Join-Path $projectRoot $BuildDirectory}
Push-Location $projectRoot
try {
    & (Join-Path $projectRoot 'build.ps1') -OutputDirectory $BuildDirectory
    & (Join-Path $PSScriptRoot 'run.ps1')
    & (Join-Path $PSScriptRoot 'run.ps1') '--desktop'
    $qaRoot=Join-Path $projectRoot '.qa'
    $cases=@(
        @{Name='model';Args=@('--self-test');Result='test-results.txt'},
        @{Name='integration';Args=@('--integration-test','--headless');Result='integration-results.txt'},
        @{Name='light';Args=@('--render-preview','--appearance','light');Result='render-results.txt'},
        @{Name='dark';Args=@('--render-preview','--appearance','dark');Result='render-results.txt'}
    )
    $results=@()
    foreach($case in $cases) {
        $dataRoot=Join-Path $qaRoot $case.Name
        $arguments=@($case.Args)+@('--data-root',('"'+$dataRoot+'"'))
        $process=Start-Process -FilePath (Join-Path $BuildDirectory 'NoteDesk.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit(30000)) {Stop-Process -Id $process.Id;throw ($case.Name+' timed out')}
        $resultFile=Join-Path $dataRoot $case.Result
        $result=Get-Content -LiteralPath $resultFile -Raw
        if($process.ExitCode -ne 0 -or -not $result.StartsWith('PASS:')) {throw ($case.Name+': '+$result)}
        $results+=($case.Name+': '+$result.Trim())
    }
    $results | Set-Content -LiteralPath (Join-Path $BuildDirectory 'validation.txt') -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path $qaRoot 'light\manager.png') -Destination (Join-Path $BuildDirectory 'preview-light.png') -Force
    Copy-Item -LiteralPath (Join-Path $qaRoot 'dark\manager.png') -Destination (Join-Path $BuildDirectory 'preview-dark.png') -Force
    $results
} finally {Pop-Location}
