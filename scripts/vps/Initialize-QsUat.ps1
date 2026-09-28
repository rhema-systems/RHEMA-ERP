[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^RhemaERP_VpsTest_[A-Za-z0-9_]+$')][string]$ExpectedDatabase,
    [string]$OutputDirectory
)
# Explicit, additive Test VPS setup. No service restart or production target.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'FreshDatabaseProvisioning.ps1')
if([string]::IsNullOrWhiteSpace($OutputDirectory)){$OutputDirectory=Join-Path $PSScriptRoot '..\..\artifacts\qs-uat'}
$values=@{};$secret=$null;$secureSecret=$null;$connectionString=$null
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
    if([string]::IsNullOrWhiteSpace($secret)){
        $secureSecret=Read-Host 'Shared UAT password for NEW QS contractor, consultant and engineer accounts (existing passwords preserved)' -AsSecureString
        if(!$secureSecret.Length){throw 'An initial password for missing UAT accounts is required.'}
        $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureSecret)
        try{$secret=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)}finally{[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)}
    }
    $stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6)
    $work=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ('preparation-'+$stamp)
    [void][IO.Directory]::CreateDirectory($work)
    $settings=@{Logging=@{LogLevel=@{Default='Warning'}};Serilog=@{MinimumLevel=@{Default='Warning'};WriteTo=@(@{Name='Console'})}}
    [IO.File]::WriteAllText((Join-Path $work 'appsettings.json'),($settings|ConvertTo-Json -Depth 5))
    try {
        $result=Invoke-RhemaFreshApiCli -ApiExecutable 'C:\RhemaERP\api\ErpSystem.Api.exe' -ContentRoot $work `
            -ConnectionString $connectionString -Command 'seed-qs-uat' -ExpectedQsDatabase $ExpectedDatabase `
            -OperationalUatPassword $secret -TimeoutSeconds 1200
    } catch {
        $safeEvidence=$_.Exception.Data['SafeCliEvidence']
        if($safeEvidence){$safeEvidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $work 'command-evidence.json') -Encoding UTF8}
        Write-Output "QS_UAT_PREPARATION_EVIDENCE|$work"
        throw 'QS preparation stopped. Review the sanitized command evidence; raw process output and credentials were not persisted.'
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $work 'command-evidence.json') -Encoding UTF8
    $report=Join-Path $work 'qs-uat-preparation.json'
    if(!(Test-Path -LiteralPath $report)){throw 'QS preparation returned without its required evidence report.'}
    Write-Output "QS_UAT_PREPARATION_REPORT|$report"
    Write-Output 'QS_CONFIGURATION|INDEPENDENT_REVIEW_REQUIRED'
    Write-Output 'Preparation completed. Review the proposed decisions and complete their evidenced approval before publishing the QS profile.'
}finally{
    if($secureSecret){$secureSecret.Dispose()}
    $secret=$null;$connectionString=$null;$target=$null;$service=$null;$values=$null
}
