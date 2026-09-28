[CmdletBinding()]
param(
    [string]$SqlServer,
    [ValidatePattern('^RhemaERP_(InventoryWorkflowsUpgrade|ReceiptFresh)_[A-Za-z0-9_]+$')]
    [string]$VerificationDatabase
)
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$reportScript=Join-Path $PSScriptRoot 'Get-QsUatReadiness.ps1'
$powershell=(Get-Command powershell.exe -ErrorAction Stop).Source
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('rhema-qs-report-test-'+[guid]::NewGuid().ToString('N'))
function Assert-Test { param([bool]$Condition,[string]$Message) if(-not $Condition){throw $Message} }
function Invoke-TestChild {
    param([string]$ScriptPath,[string[]]$ScriptArguments)
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=$powershell
    $arguments=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$ScriptPath)+$ScriptArguments
    foreach($argument in $arguments){Assert-Test (-not $argument.Contains('"')) 'Unexpected quote in test argument.'}
    $start.Arguments=($arguments | ForEach-Object {'"'+$_+'"'}) -join ' '
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $process=New-Object Diagnostics.Process
    $process.StartInfo=$start
    try {
        [void]$process.Start()
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(180000)){$process.Kill();throw 'QS report test child timed out.'}
        [pscustomobject]@{ExitCode=$process.ExitCode;Output=($stdout.Result+"`n"+$stderr.Result)}
    } finally {$process.Dispose()}
}

# Exercise the shipped file through the same Windows PowerShell 5.1 -File
# entry point used on the VPS. Omit OutputDirectory intentionally. A random
# unmatched target stops before SQL access even if this host has an API service.
$probeDatabase='RhemaERP_QsBindingProbe_'+[guid]::NewGuid().ToString('N')
$probe=Invoke-TestChild -ScriptPath $reportScript -ScriptArguments @('-ExpectedDatabase',$probeDatabase)
Assert-Test ($probe.ExitCode -ne 0) 'Unconfigured or mismatched target must stop the report.'
Assert-Test ($probe.Output.Contains('QS prerequisite inventory stopped.')) 'The real -File entry point failed before its guarded script body.'
Assert-Test (-not $probe.Output.Contains('ParameterArgumentValidationErrorEmptyStringNotAllowed')) 'OutputDirectory still resolves before PSScriptRoot is available.'
Assert-Test (-not $probe.Output.Contains('ConnectionStrings__DefaultConnection')) 'Configuration details appeared in the failure output.'
Write-Output 'PASS|Windows PowerShell -File with omitted OutputDirectory reaches guarded target validation.'

Assert-Test ([string]::IsNullOrWhiteSpace($SqlServer) -eq [string]::IsNullOrWhiteSpace($VerificationDatabase)) 'Supply both SqlServer and VerificationDatabase for the optional read-only integration check.'
if([string]::IsNullOrWhiteSpace($VerificationDatabase)) {
    Write-Output 'SKIP|Read-only report integration: specify an existing isolated migration verification database.'
    exit 0
}

try {
    # Copy the unchanged shipping script and its read-only dependencies into a
    # temporary repository shape. This keeps default output away from real UAT
    # artifacts. Only service configuration reads are mocked, never SQL results.
    $copiedScripts=Join-Path $testRoot 'scripts\vps'
    $copiedDocs=Join-Path $testRoot 'docs'
    [void][IO.Directory]::CreateDirectory($copiedScripts)
    [void][IO.Directory]::CreateDirectory($copiedDocs)
    foreach($file in @('Get-QsUatReadiness.ps1','OperationalUatVerification.ps1','QsUatPrerequisiteInventory.sql','QsUatReadinessSummary.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $copiedScripts $file)
    }
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\TDC_QS_END_TO_END_UAT_WALKTHROUGH.html') -Destination $copiedDocs
    $harnessPath=Join-Path $testRoot 'Invoke-QsReportHarness.ps1'
    [IO.File]::WriteAllText($harnessPath,@'
param([string]$SqlServer,[string]$VerificationDatabase,[string]$TargetDatabase,[string]$ReportPath)
$ErrorActionPreference='Stop'
function Get-ItemProperty {
    param([string]$LiteralPath)
    if($LiteralPath -cne 'HKLM:\SYSTEM\CurrentControlSet\Services\RhemaERPAPI\Parameters'){throw 'Unexpected configuration read.'}
    $builder=New-Object Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source']=$SqlServer;$builder['Initial Catalog']=$VerificationDatabase
    $builder['Integrated Security']=$true;$builder['TrustServerCertificate']=$true
    [pscustomobject]@{AppEnvironment=@();AppEnvironmentExtra=@('ConnectionStrings__DefaultConnection='+$builder.ConnectionString)}
}
function Test-Path {
    param([string]$LiteralPath)
    if($LiteralPath -cne 'C:\RhemaERP\services\api\RhemaERPAPI.xml'){throw 'Unexpected service-file read.'}
    return $false
}
# OutputDirectory remains omitted to exercise the real default and file paths.
& $ReportPath -ExpectedDatabase $TargetDatabase -PublicBaseUrl 'https://vps-uat.example.test'
'@)
    $copiedReport=Join-Path $copiedScripts 'Get-QsUatReadiness.ps1'
    $child=Invoke-TestChild -ScriptPath $harnessPath -ScriptArguments @('-SqlServer',$SqlServer,'-VerificationDatabase',$VerificationDatabase,'-TargetDatabase',$VerificationDatabase,'-ReportPath',$copiedReport)
    Assert-Test ($child.ExitCode -eq 0) 'Read-only QS report integration failed. Connection details were not logged.'
    $outputDirectory=Join-Path $testRoot 'artifacts\qs-uat'
    $reports=@(Get-ChildItem -LiteralPath $outputDirectory -Filter 'qs-prerequisites-*.json')
    $walkthroughs=@(Get-ChildItem -LiteralPath $outputDirectory -Filter 'QS-VPS-Walkthrough-*.html')
    Assert-Test ($reports.Count -eq 1 -and $walkthroughs.Count -eq 1) 'Default output must contain one report and one VPS walkthrough.'
    $report=Get-Content -LiteralPath $reports[0].FullName -Raw | ConvertFrom-Json
    Assert-Test ($report.ReadOnly -eq $true -and $report.QsEndToEndVerified -eq $false -and $report.Database -ceq $VerificationDatabase) 'Report misrepresented the target or UAT readiness.'
    Assert-Test (@($report.Prerequisites).Count -ge 13 -and $null -ne $report.OperationalBaseline) 'Report omitted actor prerequisites or operational baseline findings.'
    Assert-Test ($report.ConfigurationReviewRequired -and $null -ne $report.ReadyLandCount -and @($report.Prerequisites | Where-Object Category -eq 'Actor').Count -eq 15) 'Report omitted configuration review, ready-land count or expanded UAT actors.'
    $html=Get-Content -LiteralPath $walkthroughs[0].FullName -Raw
    Assert-Test (-not $html.Contains('http://localhost:3000') -and $html.Contains('https://vps-uat.example.test') -and $html.Contains('Historical project IDs')) 'Walkthrough did not preserve the VPS-link and historical-fixture boundaries.'
    Assert-Test ($child.Output.Contains('QS_END_TO_END|NOT_YET_VERIFIED')) 'Console output must not claim completed UAT.'
    $reportHash=(Get-FileHash -LiteralPath $reports[0].FullName -Algorithm SHA256).Hash
    $mismatch=Invoke-TestChild -ScriptPath $harnessPath -ScriptArguments @('-SqlServer',$SqlServer,'-VerificationDatabase',$VerificationDatabase,'-TargetDatabase',$probeDatabase,'-ReportPath',$copiedReport)
    Assert-Test ($mismatch.ExitCode -ne 0 -and $mismatch.Output.Contains('QS prerequisite inventory stopped.')) 'Wrong target was accepted.'
    Assert-Test ((Get-FileHash -LiteralPath $reports[0].FullName -Algorithm SHA256).Hash -ceq $reportHash -and @(Get-ChildItem -LiteralPath $outputDirectory -Filter 'qs-prerequisites-*.json').Count -eq 1) 'Wrong-target attempt changed report output.'
    Write-Output 'PASS|Read-only isolated database report, default output, VPS walkthrough, and wrong-target rejection. No VPS readiness is inferred.'
} finally {
    if(Test-Path -LiteralPath $testRoot) {
        $resolvedRoot=[IO.Path]::GetFullPath($testRoot)
        $expectedPrefix=Join-Path ([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) 'rhema-qs-report-test-'
        Assert-Test ($resolvedRoot.StartsWith($expectedPrefix,[StringComparison]::OrdinalIgnoreCase)) 'Refusing cleanup outside the test temporary directory.'
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
