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
    [IO.File]::WriteAllText((Join-Path $copiedScripts 'Deploy-RhemaVps.ps1'),@'
[CmdletBinding()]
param([string]$Environment,[switch]$LocalVps,[string]$ExpectedCommit,[string]$PublicBaseUrl)
[pscustomobject]@{Step='Deploy';Environment=$Environment;LocalVps=$LocalVps.IsPresent;Commit=$ExpectedCommit;PublicBaseUrl=$PublicBaseUrl;WorkingDirectory=(Get-Location).Path} |
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
        @{Name='deployment-failure';DeployExit=19;ReportExit=0;ExpectedExit=1;ExpectedSteps=1;Message='Deployment stopped. The QS prerequisite report was not run.'},
        @{Name='report-failure';DeployExit=0;ReportExit=23;ExpectedExit=1;ExpectedSteps=2;Message='Deployment passed, but the QS prerequisite report failed.'},
        @{Name='success';DeployExit=0;ReportExit=0;ExpectedExit=0;ExpectedSteps=2;Message='QS_RELEASE_DEPLOYMENT|PASS'}
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
        Assert-Test ($steps[0].Step -eq 'Deploy' -and $steps[0].Environment -eq 'Test' -and $steps[0].LocalVps -and $steps[0].Commit -ceq $expectedCommit) ($case.Name+': deployment parameters changed.')
        foreach($step in $steps) {
            Assert-Test ($step.WorkingDirectory -ceq $testRoot -and $step.PublicBaseUrl -ceq 'https://63.141.230.56') ($case.Name+': child did not receive repository working directory or normalized public URL.')
        }
        if($steps.Count -eq 2) {
            Assert-Test ($steps[1].Step -eq 'Report' -and $steps[1].Database -ceq 'RhemaERP_VpsTest_20260926_173800') ($case.Name+': report target or sequencing changed.')
        }
        Assert-Test ($output.Contains($case.Message)) ($case.Name+': accurate completion/failure message missing.')
        if($case.ExpectedExit -ne 0) {
            Assert-Test (-not $output.Contains('QS_RELEASE_DEPLOYMENT|PASS') -and -not $output.Contains('Deployment completed and QS prerequisite reports generated.')) ($case.Name+': failure incorrectly announced success.')
        }
        Write-Output ('PASS|Windows PowerShell deployment wrapper: '+$case.Name)
    }
    $source=[IO.File]::ReadAllText($wrapper)
    Assert-Test (-not $source.Contains("'-NonInteractive'") -and -not $source.Contains('-NonInteractive ') -and -not $source.Contains('-DryRun')) 'Wrapper must retain interactive secure prompting and use the actual deployer preflight only.'
} finally {
    if(Test-Path -LiteralPath $testRoot) {
        $resolvedRoot=[IO.Path]::GetFullPath($testRoot)
        $expectedPrefix=Join-Path ([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) 'rhema-qs-deployment-test-'
        Assert-Test ($resolvedRoot.StartsWith($expectedPrefix,[StringComparison]::OrdinalIgnoreCase)) 'Refusing cleanup outside the test temporary directory.'
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
