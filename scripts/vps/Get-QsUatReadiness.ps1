[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^RhemaERP_[A-Za-z0-9_]{1,119}$')][string]$ExpectedDatabase,
    [string]$OutputDirectory,
    [uri]$PublicBaseUrl='https://63.141.230.56'
)
# Run on the VPS. Reads only the existing API service configuration and database.
# Does not create users, change passwords, modify business records or restart services.
$ErrorActionPreference='Stop'
# Windows PowerShell 5.1 does not populate PSScriptRoot while binding -File
# parameter defaults. Resolve the default only after the script body starts.
if([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory=Join-Path $PSScriptRoot '..\..\artifacts\qs-uat'
}
$connection=$null;$command=$null
try {
    $serviceValues=@{}
    $serviceXml='C:\RhemaERP\services\api\RhemaERPAPI.xml'
    if(Test-Path -LiteralPath $serviceXml) {
        [xml]$configuration=Get-Content -LiteralPath $serviceXml
        foreach($node in @($configuration.service.env)){$serviceValues[[string]$node.name]=[string]$node.value}
    } else {
        $configuration=Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Services\RhemaERPAPI\Parameters'
        foreach($entry in @($configuration.AppEnvironment)+@($configuration.AppEnvironmentExtra)) {
            if($entry -and $entry.IndexOf('=') -gt 0){$at=$entry.IndexOf('=');$serviceValues[$entry.Substring(0,$at)]=$entry.Substring($at+1)}
        }
    }
    $value=[string]$serviceValues['ConnectionStrings__DefaultConnection']
    if([string]::IsNullOrWhiteSpace($value)){throw 'Configured database unavailable.'}
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder $value
    if($builder.InitialCatalog -cne $ExpectedDatabase){throw 'Configured database does not match explicit target.'}
    . (Join-Path $PSScriptRoot 'OperationalUatVerification.ps1')
    $operational=Get-RhemaOperationalSeedSnapshot -ConnectionString $value -DatabaseName $ExpectedDatabase
    $connection=New-Object System.Data.SqlClient.SqlConnection $value
    $connection.Open();$command=$connection.CreateCommand();$command.CommandTimeout=120
    $command.CommandText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'QsUatPrerequisiteInventory.sql'))
    [void]$command.Parameters.AddWithValue('@ExpectedDatabase',$ExpectedDatabase)
    $table=New-Object System.Data.DataTable
    $reader=$command.ExecuteReader()
    try{$table.Load($reader)}finally{$reader.Dispose()}
    $rows=@($table.Rows | ForEach-Object {[pscustomobject]@{Category=[string]$_.Category;Reference=[string]$_.Reference;Finding=[string]$_.Finding}})
    $output=[IO.Path]::GetFullPath($OutputDirectory);[void][IO.Directory]::CreateDirectory($output)
    $stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $report=Join-Path $output ('qs-prerequisites-'+$stamp+'.json')
    [pscustomobject]@{
        Database=$ExpectedDatabase;CapturedUtc=[DateTime]::UtcNow.ToString('o');ReadOnly=$true
        OperationalBaseline=$operational;Prerequisites=$rows;QsEndToEndVerified=$false
        Pending=@('Verify actual stage permissions and project/contract assignments','Confirm approved budget, accounting mappings and open Finance period','Confirm current rates, authority limits and QS decision bindings','Verify scanner/DMS upload and download','Execute fresh A-E records and the authorised F Finance boundary in the HTML walkthrough')
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $report -Encoding UTF8
    if($PublicBaseUrl.Scheme -notin @('https','http') -or $PublicBaseUrl.UserInfo -or $PublicBaseUrl.Query -or $PublicBaseUrl.Fragment){throw 'Public URL must be an HTTP(S) origin without credentials, query or fragment.'}
    $html=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\..\docs\TDC_QS_END_TO_END_UAT_WALKTHROUGH.html'))
    $html=$html.Replace('http://localhost:3000',$PublicBaseUrl.GetLeftPart([UriPartial]::Authority))
    $notice='<aside class="callout"><strong>VPS UAT:</strong> Follow the fresh A-F run. Historical project IDs in demonstration links belong to the local fixture and are not verified on this VPS. Use the register to select records created for this run. This copy is navigation guidance, not a passed UAT report.</aside>'
    $html=$html.Replace('<main>','<main>'+$notice)
    $walkthrough=Join-Path $output ('QS-VPS-Walkthrough-'+$stamp+'.html')
    [IO.File]::WriteAllText($walkthrough,$html)
    $rows | Format-Table -AutoSize -Wrap
    Write-Output ('OPERATIONAL_BASELINE|'+$(if($operational.Ready){'PASS'}else{'NEEDS_SETUP'}))
    Write-Output "QS_PREREQUISITE_REPORT|$report"
    Write-Output "QS_VPS_WALKTHROUGH|$walkthrough"
    Write-Output 'QS_END_TO_END|NOT_YET_VERIFIED'
} catch {
    throw 'QS prerequisite inventory stopped. Check the explicit database, API service configuration and deployed schema. Connection details were not logged.'
} finally {
    if($command){$command.Dispose()};if($connection){$connection.Dispose()}
    $value=$null;$builder=$null;$configuration=$null;$serviceValues=$null
}
