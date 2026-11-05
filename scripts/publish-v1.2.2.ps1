$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$repo='LKlingkong/NoteDesk'
$tag='v1.2.2'
# Work in a fresh checkout: the source snapshot may not contain .git metadata.
$checkout=Join-Path $env:TEMP ('NoteDesk-publish-'+[Guid]::NewGuid().ToString('N'))
& gh api user --silent
if($LASTEXITCODE -ne 0){throw 'GitHub connection or authentication unavailable.'}
& git -c http.sslBackend=schannel clone ('https://github.com/'+$repo+'.git') $checkout
if($LASTEXITCODE -ne 0){throw 'Clone failed.'}
$base=(& git -C $checkout rev-parse HEAD).Trim()
if(-not $base.StartsWith('c387f79')){throw 'Remote source changed since review; inspect it before publishing.'}
foreach($folder in @('src','assets','tests','scripts','docs')){Copy-Item -LiteralPath (Join-Path $project $folder) -Destination $checkout -Recurse -Force}
foreach($file in @('README.md','RELEASE_NOTES.md','build.ps1')){Copy-Item -LiteralPath (Join-Path $project $file) -Destination $checkout -Force}
& git -C $checkout diff --check
if($LASTEXITCODE -ne 0){throw 'Source whitespace check failed.'}
& git -C $checkout add src assets tests scripts docs README.md RELEASE_NOTES.md build.ps1
& git -C $checkout commit -m 'Release v1.2.2: fix note transparency and refine paper UI'
if($LASTEXITCODE -ne 0){throw 'Commit failed; check Git author configuration.'}
& (Join-Path $checkout 'scripts/package-release.ps1') -Version '1.2.2' -BuildDirectory (Join-Path $project 'dist/build-v1.2.2')
$assets=Join-Path $checkout 'dist/release-v1.2.2'
& git -C $checkout tag -a $tag -m 'NoteDesk v1.2.2'
if($LASTEXITCODE -ne 0){throw 'Tag already exists or could not be created.'}
& git -c http.sslBackend=schannel -C $checkout push --atomic origin main ('refs/tags/'+$tag)
if($LASTEXITCODE -ne 0){throw 'Push failed; no Release published.'}
& gh release create $tag (Join-Path $assets 'NoteDesk.exe') (Join-Path $assets 'NoteDesk-Windows-v1.2.2.zip') (Join-Path $assets 'SHA256SUMS.txt') --repo $repo --verify-tag --title 'NoteDesk v1.2.2' --notes-file (Join-Path $checkout 'docs/releases/v1.2.2.md') --draft
if($LASTEXITCODE -ne 0){throw 'Could not create release draft.'}
$release=(& gh release view $tag --repo $repo --json assets | ConvertFrom-Json)
if($LASTEXITCODE -ne 0 -or $release.assets.Count -ne 3){throw 'Release attachment verification failed.'}
foreach($asset in $release.assets){$local=(Get-FileHash -LiteralPath (Join-Path $assets $asset.name) -Algorithm SHA256).Hash.ToLowerInvariant();if($asset.digest -ne ('sha256:'+$local)){throw ('Remote hash mismatch: '+$asset.name)}}
& gh release edit $tag --repo $repo --draft=false --latest
if($LASTEXITCODE -ne 0){throw 'Draft created but publication failed.'}
& gh release view $tag --repo $repo --json url,isDraft
