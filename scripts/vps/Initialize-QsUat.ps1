[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^RhemaERP_VpsTest_[A-Za-z0-9_]+$')][string]$ExpectedDatabase,
    [string]$OutputDirectory,
    [switch]$AutoApproveQsUat,
    [switch]$ReconcileUnapprovedQsDrafts
)
# Explicit, additive Test VPS setup. No service restart or production target.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'FreshDatabaseProvisioning.ps1')
function Read-QsBootstrapPassword {
    while($true){
        $secureSecret=$null;$pointer=[IntPtr]::Zero
        try{
            $secureSecret=Read-Host 'Shared UAT password for NEW QS contractor, consultant and engineer accounts (existing passwords preserved)' -AsSecureString
            if($null -eq $secureSecret -or !$secureSecret.Length){
                Write-Warning 'The password cannot be empty. Enter it again, or press Ctrl+C to cancel QS preparation.'
                continue
            }
            $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureSecret)
            $value=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
            if([string]::IsNullOrWhiteSpace($value)){
                Write-Warning 'The password cannot contain only whitespace. Enter it again, or press Ctrl+C to cancel QS preparation.'
                continue
            }
            return $value
        }finally{
            if($pointer -ne [IntPtr]::Zero){[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)}
            if($secureSecret){$secureSecret.Dispose()}
        }
    }
}
if($ReconcileUnapprovedQsDrafts -and !$AutoApproveQsUat){throw 'ReconcileUnapprovedQsDrafts requires AutoApproveQsUat.'}
if([string]::IsNullOrWhiteSpace($OutputDirectory)){$OutputDirectory=Join-Path $PSScriptRoot '..\..\artifacts\qs-uat'}
$values=@{};$secret=$null;$connectionString=$null
try {
    $xmlPath='C:\RhemaERP\services\api\RhemaERPAPI.xml'
    if(Test-Path -LiteralPath $xmlPath){
        [xml]$service=Get-Content -LiteralPath $xmlPath
        foreach($node in @($service.service.env)){$values[[string]$node.name]=[string]$node.value}
    }else{
        $service=Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Services\RhemaERPAPI\Parameters'
        foreach($entry in @($service.AppEnvironment)+@($service.AppEnvironmentExtra)){
            if($entry -and $entry.IndexOf('=') -gt 0){$at=$entry.IndexOf('=');$values[$entry.Substring(0,$at)]=$entry.Substring($at+1)}
        }
    }
    $connectionString=[string]$values['ConnectionStrings__DefaultConnection']
    if([string]::IsNullOrWhiteSpace($connectionString)){throw 'Configured API database unavailable.'}
    $target=New-Object System.Data.SqlClient.SqlConnectionStringBuilder $connectionString
    if($target.InitialCatalog -cne $ExpectedDatabase){throw 'Configured database differs from explicit QS test target.'}
    $secret=[Environment]::GetEnvironmentVariable('UatBootstrap__SharedPassword','Process')
    if([string]::IsNullOrWhiteSpace($secret)){$secret=[string]$values['UatBootstrap__SharedPassword']}
    $stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6)
    $work=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ('preparation-'+$stamp)
    [void][IO.Directory]::CreateDirectory($work)
    $settings=@{Logging=@{LogLevel=@{Default='Warning'}};Serilog=@{MinimumLevel=@{Default='Warning'};WriteTo=@(@{Name='Console'})}}
    [IO.File]::WriteAllText((Join-Path $work 'appsettings.json'),($settings|ConvertTo-Json -Depth 5))
    $invokePreparation={
        Invoke-RhemaFreshApiCli -ApiExecutable 'C:\RhemaERP\api\ErpSystem.Api.exe' -ContentRoot $work `
            -ConnectionString $connectionString -Command 'seed-qs-uat' -ExpectedQsDatabase $ExpectedDatabase `
            -OperationalUatPassword $secret -TimeoutSeconds 1200 -AutoApproveQsUat:$AutoApproveQsUat `
            -ReconcileUnapprovedQsDrafts:$ReconcileUnapprovedQsDrafts
    }
    try {
        try{$result=& $invokePreparation}
        catch{
            $firstEvidence=$_.Exception.Data['SafeCliEvidence']
            $passwordRequired=[string]::IsNullOrWhiteSpace($secret) -and $firstEvidence -and
                @($firstEvidence.GuardCodes) -contains 'QS_UAT_PASSWORD_REQUIRED'
            if(!$passwordRequired){throw}
            Write-Output 'QS_UAT_PASSWORD|REQUIRED_FOR_MISSING_ACTORS'
            $secret=Read-QsBootstrapPassword
            $result=& $invokePreparation
        }
    } catch {
        $safeEvidence=$_.Exception.Data['SafeCliEvidence']
        if($safeEvidence){
            $safeEvidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $work 'command-evidence.json') -Encoding UTF8
            $summary=[ordered]@{ExitCode=$safeEvidence.ExitCode;GuardCodes=@($safeEvidence.GuardCodes);
                QsDecisionCodes=@($safeEvidence.QsDecisionCodes);QsStages=@($safeEvidence.QsStages);
                MissingServices=@($safeEvidence.MissingServices);SqlErrorNumbers=@($safeEvidence.SqlErrorNumbers);
                ExceptionTypes=@($safeEvidence.ExceptionTypes)}
            Write-Output ('QS_UAT_FAILURE|'+($summary|ConvertTo-Json -Compress -Depth 5))
        }
        Write-Output "QS_UAT_PREPARATION_EVIDENCE|$work"
        throw 'QS preparation stopped. Review the sanitized command evidence; raw process output and credentials were not persisted.'
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $work 'command-evidence.json') -Encoding UTF8
    $report=Join-Path $work 'qs-uat-preparation.json'
    if(!(Test-Path -LiteralPath $report)){throw 'QS preparation returned without its required evidence report.'}
    Write-Output "QS_UAT_PREPARATION_REPORT|$report"
    if($AutoApproveQsUat){
        $prepared=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        if($prepared.Database -cne $ExpectedDatabase -or !$prepared.Preparation.ProfileId -or
            $prepared.Preparation.IndependentConfigurationReviewRequired -ne $false -or
            @($prepared.Preparation.Unresolved).Count -ne 0 -or
            @($prepared.Preparation.PreparedDecisions | Select-Object -Unique).Count -ne 17) {
            throw 'QS auto-approval did not verify all 17 decisions and profile publication. Review the preparation report.'
        }
        Write-Output 'QS_CONFIGURATION|AUTO_APPROVED_TEST_ONLY'
        Write-Output 'Preparation completed with explicit test-only QS decision auto-approval and profile publication.'
    }else{
        Write-Output 'QS_CONFIGURATION|INDEPENDENT_REVIEW_REQUIRED'
        Write-Output 'Preparation completed. Review the proposed decisions and complete their evidenced approval before publishing the QS profile.'
    }
}finally{
    $secret=$null;$connectionString=$null;$target=$null;$service=$null;$values=$null
}
