$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
Push-Location $projectRoot
try {
    & (Join-Path $projectRoot 'build.ps1')
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
        $process=Start-Process -FilePath (Join-Path $projectRoot 'dist\NoteDesk.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit(30000)) {Stop-Process -Id $process.Id;throw ($case.Name+' timed out')}
        $resultFile=Join-Path $dataRoot $case.Result
        $result=Get-Content -LiteralPath $resultFile -Raw
        if($process.ExitCode -ne 0 -or -not $result.StartsWith('PASS:')) {throw ($case.Name+': '+$result)}
        $results+=($case.Name+': '+$result.Trim())
    }
    $results | Set-Content -LiteralPath (Join-Path $projectRoot 'dist\validation.txt') -Encoding UTF8
    $results
} finally {Pop-Location}
