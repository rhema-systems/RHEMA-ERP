[CmdletBinding()]
param(
    [ValidatePattern('^RhemaERP_[A-Za-z0-9_]{1,119}$')]
    [string]$ExpectedDatabase='RhemaERP_VpsTest_20260926_173800',
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$ExpectedCommit,
    [uri]$PublicBaseUrl='https://63.141.230.56',
    [string]$ArtifactDirectory,
    [switch]$UpdateSource,
    [switch]$CleanBuild,
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$ReuseFrontendBuildFromCommit,
    [switch]$LegacyFullBuild,
    [switch]$PrepareQsUat,
    [switch]$AutoApproveQsUat,
    [switch]$ReconcileUnapprovedQsDrafts
)
# Run this file on the VPS release checkout after updating master. The existing
# deployer performs its own preflight, backups, migration and release checks.
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path -Parent $PSScriptRoot
$deploymentPassed=$false
$preparationRunning=$false
$locationPushed=$false
$releaseCommit=$null
$releaseArtifactDirectory=$null
$runStartedUtc=[DateTime]::UtcNow
$timings=[System.Collections.Generic.List[object]]::new()

function Invoke-QsTimedStep {
    param([string]$Name,[scriptblock]$Operation)
    $started=[DateTime]::UtcNow
    $watch=[Diagnostics.Stopwatch]::StartNew()
    try {
        & $Operation
        $watch.Stop()
        $completed=[DateTime]::UtcNow
        $timings.Add([ordered]@{
            name=$Name;scope='QS wrapper';status='Passed';startedUtc=$started.ToString('o')
            completedUtc=$completed.ToString('o')
            durationSeconds=[Math]::Round($watch.Elapsed.TotalSeconds,2)
        })
    } catch {
        $watch.Stop()
        $completed=[DateTime]::UtcNow
        $timings.Add([ordered]@{
            name=$Name;scope='QS wrapper';status='Failed';startedUtc=$started.ToString('o')
            completedUtc=$completed.ToString('o')
            durationSeconds=[Math]::Round($watch.Elapsed.TotalSeconds,2)
            error=$_.Exception.Message
        })
        throw
    }
}

function Write-QsTimingEvidence {
    $completed=[DateTime]::UtcNow
    $outputRoot=Join-Path $repositoryRoot 'artifacts\qs-uat'
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    $path=Join-Path $outputRoot ("deployment-timings-{0}.json" -f $runStartedUtc.ToString('yyyyMMdd-HHmmss'))
    $evidence=[ordered]@{
        schemaVersion=1
        startedUtc=$runStartedUtc.ToString('o')
        completedUtc=$completed.ToString('o')
        totalDurationSeconds=[Math]::Round(($completed-$runStartedUtc).TotalSeconds,2)
        steps=@($timings)
        slowestSteps=@($timings|Sort-Object durationSeconds -Descending)
    }
    [IO.File]::WriteAllText($path,($evidence|ConvertTo-Json -Depth 8),
        (New-Object Text.UTF8Encoding($false)))
    Write-Output "QS_DEPLOYMENT_TIMING_EVIDENCE|$path"
    Write-Output 'QS DEPLOYMENT TIMING SUMMARY (slowest first)'
    foreach($step in @($timings|Sort-Object durationSeconds -Descending)) {
        Write-Output ("{0,10:n1}s  {1,-6}  {2}" -f [double]$step.durationSeconds,$step.status,$step.name)
    }
}
try {
    if($LegacyFullBuild -and ($ArtifactDirectory -or $CleanBuild -or $ReuseFrontendBuildFromCommit)) {
        throw 'LegacyFullBuild cannot be combined with ArtifactDirectory, CleanBuild, or ReuseFrontendBuildFromCommit.'
    }
    if($ArtifactDirectory -and ($CleanBuild -or $ReuseFrontendBuildFromCommit)) {
        throw 'ArtifactDirectory already identifies a completed build and cannot be combined with build options.'
    }
    if($CleanBuild -and $ReuseFrontendBuildFromCommit) {
        throw 'CleanBuild cannot be combined with ReuseFrontendBuildFromCommit.'
    }
    if($AutoApproveQsUat -and !$PrepareQsUat){throw 'AutoApproveQsUat requires PrepareQsUat.'}
    if($ReconcileUnapprovedQsDrafts -and !$AutoApproveQsUat){throw 'ReconcileUnapprovedQsDrafts requires AutoApproveQsUat.'}
    if($PrepareQsUat -and $ExpectedDatabase -cnotmatch '^RhemaERP_VpsTest_[A-Za-z0-9_]+$') {
        throw 'QS preparation requires an explicitly selected RhemaERP_VpsTest database.'
    }
    if(-not $PublicBaseUrl.IsAbsoluteUri -or $PublicBaseUrl.Scheme -notin @('https','http') -or
        $PublicBaseUrl.UserInfo -or $PublicBaseUrl.Query -or $PublicBaseUrl.Fragment -or $PublicBaseUrl.AbsolutePath -ne '/') {
        throw 'Public URL must be an HTTP(S) origin.'
    }
    $publicOrigin=$PublicBaseUrl.GetLeftPart([UriPartial]::Authority)
    $powershell=(Get-Command powershell.exe -ErrorAction Stop).Source
    Push-Location -LiteralPath $repositoryRoot
    $locationPushed=$true
    if($UpdateSource) {
        Invoke-QsTimedStep 'Validate and optionally update source checkout' {
            $changes=@(& git status --porcelain)
            if($LASTEXITCODE -ne 0){throw 'Could not read the release checkout status.'}
            if($changes.Count){throw 'The release checkout has local changes. Review them before deployment.'}
            & git checkout master
            if($LASTEXITCODE -ne 0){throw 'Could not select the master branch.'}
            & git pull --ff-only origin master
            if($LASTEXITCODE -ne 0){throw 'The fast-forward source update failed.'}
            $head=(& git rev-parse HEAD).Trim()
            if($LASTEXITCODE -ne 0 -or $head -notmatch '^[0-9a-f]{40}$') {
                throw 'Could not resolve the release commit.'
            }
            if($ExpectedCommit -and -not $head.StartsWith($ExpectedCommit,[StringComparison]::OrdinalIgnoreCase)) {
                throw "Release checkout $head does not match ExpectedCommit $ExpectedCommit."
            }
            $script:releaseCommit=$head
        }
    } else {
        $now=[DateTime]::UtcNow.ToString('o')
        $timings.Add([ordered]@{
            name='Source checkout update';scope='QS wrapper';status='NotRequested'
            startedUtc=$now;completedUtc=$now;durationSeconds=0
        })
        if($ExpectedCommit) {
            $releaseCommit=$ExpectedCommit
        } else {
            $releaseCommit=(& git rev-parse HEAD).Trim()
            if($LASTEXITCODE -ne 0 -or $releaseCommit -notmatch '^[0-9a-f]{40}$') {
                throw 'Could not resolve the release commit.'
            }
        }
    }

    if(-not $releaseCommit){throw 'Could not resolve the release commit.'}

    if(-not $LegacyFullBuild) {
        $preflightArguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',
            (Join-Path $PSScriptRoot 'Deploy-RhemaVps.ps1'),
            '-Environment','Test','-LocalVps','-PreflightOnly',
            '-PublicBaseUrl',$publicOrigin,'-ExpectedCommit',$releaseCommit)
        Invoke-QsTimedStep 'Validate VPS before release build' {
            & $powershell @preflightArguments
            if($LASTEXITCODE -ne 0){throw 'VPS preflight failed before release build.'}
        }
    }

    if($LegacyFullBuild) {
        $now=[DateTime]::UtcNow.ToString('o')
        $timings.Add([ordered]@{
            name='Build immutable release once';scope='QS wrapper';status='LegacyDeployer'
            startedUtc=$now;completedUtc=$now;durationSeconds=0
        })
    } elseif($ArtifactDirectory) {
        $releaseArtifactDirectory=[IO.Path]::GetFullPath($ArtifactDirectory)
        if(-not (Test-Path -LiteralPath (Join-Path $releaseArtifactDirectory 'release-manifest.json'))) {
            throw "Release manifest was not found in ArtifactDirectory: $releaseArtifactDirectory"
        }
        $now=[DateTime]::UtcNow.ToString('o')
        $timings.Add([ordered]@{
            name='Build immutable release once';scope='QS wrapper';status='Reused'
            startedUtc=$now;completedUtc=$now;durationSeconds=0
        })
        Write-Output "RELEASE_ARTIFACT_REUSED|$releaseArtifactDirectory"
    } else {
        $buildParameters=@{
            Environment='Test'
            PublicBaseUrl=[uri]$publicOrigin
            ExpectedCommit=$releaseCommit
            ArchiveCompressionLevel='Fastest'
        }
        if($CleanBuild){$buildParameters.CleanBuild=$true}
        if($ReuseFrontendBuildFromCommit) {
            $buildParameters.ReuseFrontendBuildFromCommit=$ReuseFrontendBuildFromCommit
        }
        $buildOutput=@(Invoke-QsTimedStep 'Build immutable release once' {
            & (Join-Path $PSScriptRoot 'Build-RhemaRelease.ps1') @buildParameters
        })
        $buildOutput | Out-Host
        $artifactMarkers=@($buildOutput | ForEach-Object {[string]$_} |
            Where-Object {$_ -like 'RELEASE_ARTIFACT_DIRECTORY|*'})
        if($artifactMarkers.Count -ne 1) {
            throw 'Release build did not return exactly one artifact directory.'
        }
        $releaseArtifactDirectory=[IO.Path]::GetFullPath(
            $artifactMarkers[0].Substring('RELEASE_ARTIFACT_DIRECTORY|'.Length))
        if(-not (Test-Path -LiteralPath (Join-Path $releaseArtifactDirectory 'release-manifest.json'))) {
            throw 'Release build marker points to a directory without a manifest.'
        }
    }

    $deployArguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',
        (Join-Path $PSScriptRoot 'Deploy-RhemaVps.ps1'),
        '-Environment','Test','-LocalVps','-PublicBaseUrl',$publicOrigin)
    $deployArguments+=@('-ExpectedCommit',$releaseCommit)
    if($PrepareQsUat){$deployArguments+='-PrepareOperationalUat'}
    if(-not $LegacyFullBuild) {
        $deployArguments+=@('-DeployOnly','-ArtifactDirectory',$releaseArtifactDirectory)
    }
    # Keep the child interactive so its existing secure credential prompt works.
    Invoke-QsTimedStep 'Activate verified ERP release' {
        & $powershell @deployArguments
        if($LASTEXITCODE -ne 0){throw 'Deployment command failed.'}
    }
    $deploymentPassed=$true

    if($PrepareQsUat) {
        $preparationRunning=$true
        $prepareArguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',
            (Join-Path $PSScriptRoot 'vps\Initialize-QsUat.ps1'),'-ExpectedDatabase',$ExpectedDatabase)
        if($AutoApproveQsUat){$prepareArguments+='-AutoApproveQsUat'}
        if($ReconcileUnapprovedQsDrafts){$prepareArguments+='-ReconcileUnapprovedQsDrafts'}
        Invoke-QsTimedStep 'Prepare QS UAT data and decisions' {
            & $powershell @prepareArguments
            if($LASTEXITCODE -ne 0){throw 'QS test preparation failed; retain its evidence before retrying.'}
        }
        $preparationRunning=$false
    }

    Invoke-QsTimedStep 'Generate QS UAT readiness evidence' {
        & $powershell -NoProfile -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'vps\Get-QsUatReadiness.ps1') `
            -ExpectedDatabase $ExpectedDatabase -PublicBaseUrl $publicOrigin
        if($LASTEXITCODE -ne 0){throw 'QS prerequisite report command failed.'}
    }

    Write-Output 'QS_RELEASE_DEPLOYMENT|PASS'
    Write-Output 'Deployment completed and QS prerequisite reports generated. Review artifacts\qs-uat before UAT.'
    exit 0
} catch {
    if($preparationRunning) {
        Write-Error 'Deployment passed, but QS test preparation stopped. The readiness report was not run. Keep preparation evidence and resolve the reported blocker before retrying Initialize-QsUat.ps1.' -ErrorAction Continue
    } elseif($deploymentPassed) {
        Write-Error 'Deployment passed, but the QS prerequisite report failed. Inspect the reported target/configuration error and deployment evidence before retrying the report.' -ErrorAction Continue
    } else {
        Write-Error 'Deployment stopped. The QS prerequisite report was not run. Keep the deployment evidence and resolve the reported blocker before retrying.' -ErrorAction Continue
    }
    exit 1
} finally {
    Write-QsTimingEvidence
    if($locationPushed){Pop-Location}
}
