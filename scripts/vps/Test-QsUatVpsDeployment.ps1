[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$powershell=(Get-Command powershell.exe -ErrorAction Stop).Source
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('rhema-qs-deployment-test-'+[guid]::NewGuid().ToString('N'))
function Assert-Test { param([bool]$Condition,[string]$Message) if(-not $Condition){throw $Message} }
try {
    $copiedScripts=Join-Path $testRoot 'scripts'
    $copiedVps=Join-Path $copiedScripts 'vps'
    [void][IO.Directory]::CreateDirectory($copiedVps)
    $wrapper=Join-Path $copiedScripts 'Deploy-QsUatVps.ps1'
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Deploy-QsUatVps.ps1') -Destination $wrapper
    # Only the wrapper is real here. These children record their arguments and
    # exit codes; no production deployment, service access or SQL is performed.
    [IO.File]::WriteAllText((Join-Path $copiedScripts 'Build-RhemaRelease.ps1'),@'
[CmdletBinding()]
param([string]$Environment,[uri]$PublicBaseUrl,[string]$ExpectedCommit,[switch]$CleanBuild,[string]$ReuseFrontendBuildFromCommit)
[pscustomobject]@{Step='Build';Environment=$Environment;Commit=$ExpectedCommit;PublicBaseUrl=$PublicBaseUrl.GetLeftPart([UriPartial]::Authority);WorkingDirectory=(Get-Location).Path} |
 ConvertTo-Json -Compress | Add-Content -LiteralPath $env:RHEMA_QS_WRAPPER_TEST_LOG
if($env:RHEMA_QS_WRAPPER_TEST_BUILD_FAIL -eq '1'){throw 'Synthetic release build failure.'}
$artifact=Join-Path (Split-Path -Parent $PSScriptRoot) 'fake-release'
[void][IO.Directory]::CreateDirectory($artifact)
[IO.File]::WriteAllText((Join-Path $artifact 'release-manifest.json'),'{}')
Write-Output ('RELEASE_ARTIFACT_DIRECTORY|'+$artifact)
'@)
    [IO.File]::WriteAllText((Join-Path $copiedScripts 'Deploy-RhemaVps.ps1'),@'
[CmdletBinding()]
param([string]$Environment,[switch]$LocalVps,[string]$ExpectedCommit,[string]$PublicBaseUrl,[switch]$DeployOnly,[string]$ArtifactDirectory)
[pscustomobject]@{Step='Deploy';Environment=$Environment;LocalVps=$LocalVps.IsPresent;Commit=$ExpectedCommit;PublicBaseUrl=$PublicBaseUrl;DeployOnly=$DeployOnly.IsPresent;ArtifactDirectory=$ArtifactDirectory;WorkingDirectory=(Get-Location).Path} |
 ConvertTo-Json -Compress | Add-Content -LiteralPath $env:RHEMA_QS_WRAPPER_TEST_LOG
exit ([int]$env:RHEMA_QS_WRAPPER_TEST_DEPLOY_EXIT)
'@)
    [IO.File]::WriteAllText((Join-Path $copiedVps 'Get-QsUatReadiness.ps1'),@'
[CmdletBinding()]
param([string]$ExpectedDatabase,[string]$PublicBaseUrl)
[pscustomobject]@{Step='Report';Database=$ExpectedDatabase;PublicBaseUrl=$PublicBaseUrl;WorkingDirectory=(Get-Location).Path} |
 ConvertTo-Json -Compress | Add-Content -LiteralPath $env:RHEMA_QS_WRAPPER_TEST_LOG
exit ([int]$env:RHEMA_QS_WRAPPER_TEST_REPORT_EXIT)
'@)
    $expectedCommit='1111111111111111111111111111111111111111'
    $cases=@(
        @{Name='build-failure';BuildFail=1;DeployExit=0;ReportExit=0;ExpectedExit=1;ExpectedSteps=1;Message='Deployment stopped. The QS prerequisite report was not run.'},
        @{Name='deployment-failure';BuildFail=0;DeployExit=19;ReportExit=0;ExpectedExit=1;ExpectedSteps=2;Message='Deployment stopped. The QS prerequisite report was not run.'},
        @{Name='report-failure';BuildFail=0;DeployExit=0;ReportExit=23;ExpectedExit=1;ExpectedSteps=3;Message='Deployment passed, but the QS prerequisite report failed.'},
        @{Name='success';BuildFail=0;DeployExit=0;ReportExit=0;ExpectedExit=0;ExpectedSteps=3;Message='QS_RELEASE_DEPLOYMENT|PASS'}
    )
    foreach($case in $cases) {
        $log=Join-Path $testRoot ($case.Name+'.jsonl')
        $start=New-Object Diagnostics.ProcessStartInfo
        $start.FileName=$powershell
        $start.Arguments='-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$wrapper+'" -ExpectedCommit '+$expectedCommit
        $start.WorkingDirectory=[IO.Path]::GetTempPath()
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true
        $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
        $start.EnvironmentVariables['RHEMA_QS_WRAPPER_TEST_LOG']=$log
        $start.EnvironmentVariables['RHEMA_QS_WRAPPER_TEST_BUILD_FAIL']=[string]$case.BuildFail
        $start.EnvironmentVariables['RHEMA_QS_WRAPPER_TEST_DEPLOY_EXIT']=[string]$case.DeployExit
        $start.EnvironmentVariables['RHEMA_QS_WRAPPER_TEST_REPORT_EXIT']=[string]$case.ReportExit
        $process=New-Object Diagnostics.Process
        $process.StartInfo=$start
        try {
            [void]$process.Start()
            $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
            if(-not $process.WaitForExit(60000)){$process.Kill();throw 'Deployment wrapper test child timed out.'}
            $output=$stdout.Result+"`n"+$stderr.Result
            Assert-Test ($process.ExitCode -eq $case.ExpectedExit) ($case.Name+': wrong wrapper exit code.')
        } finally {$process.Dispose()}
        $steps=@(Get-Content -LiteralPath $log | ForEach-Object {$_ | ConvertFrom-Json})
        Assert-Test ($steps.Count -eq $case.ExpectedSteps) ($case.Name+': wrong number of child calls.')
        Assert-Test ($steps[0].Step -eq 'Build' -and $steps[0].Environment -eq 'Test' -and $steps[0].Commit -ceq $expectedCommit) ($case.Name+': immutable release build parameters changed.')
        foreach($step in $steps) {
            Assert-Test ($step.WorkingDirectory -ceq $testRoot -and $step.PublicBaseUrl -ceq 'https://63.141.230.56') ($case.Name+': child did not receive repository working directory or normalized public URL.')
        }
        if($steps.Count -ge 2) {
            Assert-Test ($steps[1].Step -eq 'Deploy' -and $steps[1].LocalVps -and $steps[1].DeployOnly -and $steps[1].Commit -ceq $expectedCommit -and $steps[1].ArtifactDirectory -like '*fake-release') ($case.Name+': deploy-only activation parameters changed.')
        }
        if($steps.Count -eq 3) {
            Assert-Test ($steps[2].Step -eq 'Report' -and $steps[2].Database -ceq 'RhemaERP_VpsTest_20260926_173800') ($case.Name+': report target or sequencing changed.')
        }
        Assert-Test ($output.Contains($case.Message)) ($case.Name+': accurate completion/failure message missing.')
        if($case.ExpectedExit -ne 0) {
            Assert-Test (-not $output.Contains('QS_RELEASE_DEPLOYMENT|PASS') -and -not $output.Contains('Deployment completed and QS prerequisite reports generated.')) ($case.Name+': failure incorrectly announced success.')
        }
        Write-Output ('PASS|Windows PowerShell deployment wrapper: '+$case.Name)
    }

    $reuseLog=Join-Path $testRoot 'artifact-reuse.jsonl'
    $reusedArtifact=Join-Path $testRoot 'verified-release'
    [void][IO.Directory]::CreateDirectory($reusedArtifact)
    [IO.File]::WriteAllText((Join-Path $reusedArtifact 'release-manifest.json'),'{}')
    $reuseStart=New-Object Diagnostics.ProcessStartInfo
    $reuseStart.FileName=$powershell
    $reuseStart.Arguments='-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$wrapper+'" -ExpectedCommit '+$expectedCommit+' -ArtifactDirectory "'+$reusedArtifact+'"'
    $reuseStart.WorkingDirectory=[IO.Path]::GetTempPath()
    $reuseStart.UseShellExecute=$false;$reuseStart.CreateNoWindow=$true
    $reuseStart.RedirectStandardOutput=$true;$reuseStart.RedirectStandardError=$true
    $reuseStart.EnvironmentVariables['RHEMA_QS_WRAPPER_TEST_LOG']=$reuseLog
    $reuseStart.EnvironmentVariables['RHEMA_QS_WRAPPER_TEST_BUILD_FAIL']='1'
    $reuseStart.EnvironmentVariables['RHEMA_QS_WRAPPER_TEST_DEPLOY_EXIT']='0'
    $reuseStart.EnvironmentVariables['RHEMA_QS_WRAPPER_TEST_REPORT_EXIT']='0'
    $reuseProcess=New-Object Diagnostics.Process
    $reuseProcess.StartInfo=$reuseStart
    try {
        [void]$reuseProcess.Start()
        $reuseStdout=$reuseProcess.StandardOutput.ReadToEndAsync()
        $reuseStderr=$reuseProcess.StandardError.ReadToEndAsync()
        if(-not $reuseProcess.WaitForExit(60000)){$reuseProcess.Kill();throw 'Artifact reuse wrapper test timed out.'}
        $reuseOutput=$reuseStdout.Result+"`n"+$reuseStderr.Result
        Assert-Test ($reuseProcess.ExitCode -eq 0) 'Artifact reuse wrapper failed.'
    } finally {$reuseProcess.Dispose()}
    $reuseSteps=@(Get-Content -LiteralPath $reuseLog | ForEach-Object {$_ | ConvertFrom-Json})
    Assert-Test ($reuseSteps.Count -eq 2 -and $reuseSteps[0].Step -eq 'Deploy' -and
        $reuseSteps[0].DeployOnly -and $reuseSteps[0].ArtifactDirectory -ceq $reusedArtifact -and
        $reuseSteps[1].Step -eq 'Report') 'Artifact reuse did not skip the build and retain deployment/readiness checks.'
    Assert-Test $reuseOutput.Contains('RELEASE_ARTIFACT_REUSED|') 'Artifact reuse marker was not reported.'
    Write-Output 'PASS|Windows PowerShell deployment wrapper: immutable artifact reuse'

    $source=[IO.File]::ReadAllText($wrapper)
    Assert-Test (-not $source.Contains("'-NonInteractive'") -and -not $source.Contains('-NonInteractive ') -and -not $source.Contains('-DryRun')) 'Wrapper must retain interactive secure prompting and use the actual deployer preflight only.'
    foreach($contract in @('ArtifactDirectory','DeployOnly','LegacyFullBuild','RELEASE_ARTIFACT_REUSED|')) {
        Assert-Test $source.Contains($contract) ('Optimized deployment wrapper is missing contract: '+$contract)
    }
} finally {
    if(Test-Path -LiteralPath $testRoot) {
        $resolvedRoot=[IO.Path]::GetFullPath($testRoot)
        $expectedPrefix=Join-Path ([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) 'rhema-qs-deployment-test-'
        Assert-Test ($resolvedRoot.StartsWith($expectedPrefix,[StringComparison]::OrdinalIgnoreCase)) 'Refusing cleanup outside the test temporary directory.'
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
