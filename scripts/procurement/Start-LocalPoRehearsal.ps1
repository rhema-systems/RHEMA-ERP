param([switch]$ApiOnly, [string]$ApiRuntimePath)
$ErrorActionPreference = 'Stop'
$rehearsalRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$rehearsalRoot = Join-Path $rehearsalRepo 'local-artifacts/po-rehearsal-20260909'
$rehearsalDatabase = 'RhemaERP_PO_Rehearsal_20260909'
$rehearsalProductionApi = Join-Path $rehearsalRepo 'local-artifacts/rehearsal-production-20260910/api'
$rehearsalCurrentCountApi = Join-Path $env:LOCALAPPDATA 'Temp/tdc-count-current-20260911/api'
$rehearsalCountReviewApi = Join-Path $env:LOCALAPPDATA 'Temp/tdc-count-review-20260911/api'
$rehearsalCountAbcApi = Join-Path $env:LOCALAPPDATA 'Temp/tdc-count-abc-20260911/api'
$rehearsalManualCountApi = Join-Path $env:LOCALAPPDATA 'Temp/tdc-count-manual-20260911/api'
$rehearsalScopeCountApi = Join-Path $env:LOCALAPPDATA 'Temp/tdc-count-scope-20260911/api'
$rehearsalDefaultBinApi = Join-Path $env:LOCALAPPDATA 'Temp/tdc-warehouse-defaults-20260911/api'
$rehearsalValuationApi = Join-Path $env:LOCALAPPDATA 'Temp/tdc-count-valuation-20260912/api'
$rehearsalCountE2EApi = Join-Path $env:LOCALAPPDATA 'Temp/tdc-count-e2e-20260912/api'
$rehearsalOptionalApprovalApi = Join-Path $env:LOCALAPPDATA 'Temp/tdc-optional-approval-20260912/api'
$rehearsalOptionalApprovalPhase2Api = Join-Path $env:LOCALAPPDATA 'Temp/tdc-optional-approval-phase2-20260912/api'
$rehearsalInventoryFinalApi = Join-Path $env:LOCALAPPDATA 'Temp/tdc-inventory-final-20260912/api'
$rehearsalTransferAutoCompleteApi = Join-Path $env:LOCALAPPDATA 'Temp/tdc-inventory-transfer-auto-complete-20260912/api'
$rehearsalApiRuntime = if ($ApiRuntimePath) {
    (Resolve-Path -LiteralPath $ApiRuntimePath).Path
} elseif (Test-Path -LiteralPath (Join-Path $rehearsalCountE2EApi 'e2e-runtime-ready.txt')) {
    (Resolve-Path -LiteralPath $rehearsalCountE2EApi).Path
} elseif (Test-Path -LiteralPath (Join-Path $rehearsalValuationApi 'valuation-runtime-ready.txt')) {
    (Resolve-Path -LiteralPath $rehearsalValuationApi).Path
} elseif (Test-Path -LiteralPath (Join-Path $rehearsalDefaultBinApi 'default-bin-runtime-ready.txt')) {
    (Resolve-Path -LiteralPath $rehearsalDefaultBinApi).Path
} elseif (Test-Path -LiteralPath (Join-Path $rehearsalScopeCountApi 'scope-runtime-ready.txt')) {
    (Resolve-Path -LiteralPath $rehearsalScopeCountApi).Path
} elseif (Test-Path -LiteralPath (Join-Path $rehearsalManualCountApi 'manual-runtime-ready.txt')) {
    (Resolve-Path -LiteralPath $rehearsalManualCountApi).Path
} elseif (Test-Path -LiteralPath (Join-Path $rehearsalCountAbcApi 'abc-runtime-ready.txt')) {
    (Resolve-Path -LiteralPath $rehearsalCountAbcApi).Path
} elseif (Test-Path -LiteralPath (Join-Path $rehearsalCountReviewApi 'review-runtime-ready.txt')) {
    (Resolve-Path -LiteralPath $rehearsalCountReviewApi).Path
} elseif (Test-Path -LiteralPath (Join-Path $rehearsalCurrentCountApi 'ErpSystem.Api.exe')) {
    (Resolve-Path -LiteralPath $rehearsalCurrentCountApi).Path
} elseif (Test-Path -LiteralPath (Join-Path $rehearsalProductionApi 'ErpSystem.Api.exe')) {
    (Resolve-Path -LiteralPath $rehearsalProductionApi).Path
} else { Join-Path $rehearsalRoot 'api' }
$rehearsalAllowedRuntimes = @(
    [IO.Path]::GetFullPath((Join-Path $rehearsalRoot 'api')),
    [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Temp/tdc-count-current-20260911/api')),
    [IO.Path]::GetFullPath($rehearsalCountReviewApi),
    [IO.Path]::GetFullPath($rehearsalCountAbcApi),
    [IO.Path]::GetFullPath($rehearsalManualCountApi),
    [IO.Path]::GetFullPath($rehearsalScopeCountApi),
    [IO.Path]::GetFullPath($rehearsalDefaultBinApi),
    [IO.Path]::GetFullPath($rehearsalValuationApi),
    [IO.Path]::GetFullPath($rehearsalCountE2EApi),
    [IO.Path]::GetFullPath($rehearsalOptionalApprovalApi),
    [IO.Path]::GetFullPath($rehearsalOptionalApprovalPhase2Api),
    [IO.Path]::GetFullPath($rehearsalInventoryFinalApi),
    [IO.Path]::GetFullPath($rehearsalTransferAutoCompleteApi),
    [IO.Path]::GetFullPath((Join-Path $rehearsalRepo 'local-artifacts/physical-count-current-20260911/api')),
    [IO.Path]::GetFullPath((Join-Path $rehearsalRepo 'local-artifacts/rehearsal-production-20260910/api'))
)
if ($rehearsalApiRuntime -notin $rehearsalAllowedRuntimes -or -not (Test-Path -LiteralPath (Join-Path $rehearsalApiRuntime 'ErpSystem.Api.exe'))) {
    throw 'The API runtime must be one of the prepared isolated rehearsal directories.'
}
$rehearsalPortsToCheck = if ($ApiOnly) { @(5002) } else { @(3002,5002,5519) }
foreach ($rehearsalPort in $rehearsalPortsToCheck) {
    if (Get-NetTCPConnection -LocalPort $rehearsalPort -State Listen -ErrorAction SilentlyContinue) { throw "Port $rehearsalPort is already occupied; no existing processes will be stopped." }
}
& (Join-Path $PSScriptRoot 'Start-LocalFileScanner.ps1')
$rehearsalApiProject = Join-Path $rehearsalRepo 'src/ErpSystem.Api/ErpSystem.Api.csproj'
$rehearsalRawSecrets = @(dotnet user-secrets list --project $rehearsalApiProject --json)
if ($LASTEXITCODE -ne 0) { throw 'Could not load existing local configuration.' }
$rehearsalJsonStart = [Array]::FindIndex($rehearsalRawSecrets,[Predicate[string]]{param($line) $line.Trim() -eq '{'})
$rehearsalJsonEnd = [Array]::FindLastIndex($rehearsalRawSecrets,[Predicate[string]]{param($line) $line.Trim() -eq '}'})
if ($rehearsalJsonStart -lt 0 -or $rehearsalJsonEnd -le $rehearsalJsonStart) { throw 'Local secret configuration is unavailable.' }
$rehearsalSecrets = (($rehearsalRawSecrets[$rehearsalJsonStart..$rehearsalJsonEnd]) -join "`n") | ConvertFrom-Json
Add-Type -AssemblyName System.Data
$rehearsalConnectionBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$rehearsalSecrets.'ConnectionStrings:DefaultConnection')
if ($rehearsalConnectionBuilder['Initial Catalog'] -ne 'RhemaERP') { throw 'Unexpected source database configuration.' }
$rehearsalConnectionBuilder['Initial Catalog'] = $rehearsalDatabase
$rehearsalProbe = [System.Data.SqlClient.SqlConnection]::new($rehearsalConnectionBuilder.ConnectionString)
try {
    $rehearsalProbe.Open()
    $rehearsalCheck = $rehearsalProbe.CreateCommand()
    if ($rehearsalApiRuntime -eq [IO.Path]::GetFullPath($rehearsalTransferAutoCompleteApi)) {
        . (Join-Path $PSScriptRoot 'LocalTransferRuntimeReadiness.ps1')
        $null=Get-LocalTransferRuntimeReadiness -ApiRuntimePath $rehearsalApiRuntime
    }
    if ($rehearsalApiRuntime -eq [IO.Path]::GetFullPath($rehearsalInventoryFinalApi)) {
        $rehearsalCheck.CommandText = "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId IN (N'20260912230000_InventoryTransferOptionalApproval',N'20260912231000_PurchaseReturnOptionalApproval')"
        if ([int]$rehearsalCheck.ExecuteScalar() -ne 2) { throw 'The reviewed inventory schema is not applied; this API will not be started.' }
        if (-not (Test-Path -LiteralPath (Join-Path $rehearsalApiRuntime 'inventory-runtime-ready.json'))) { throw 'The inventory API verification manifest is missing.' }
    }
    $rehearsalCheck.CommandText = "SELECT CASE WHEN DB_NAME()='$rehearsalDatabase' AND CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='RHEMA-MICHAEL\SQL2017' AND NOT EXISTS(SELECT 1 FROM dbo.EmailSettings WHERE NULLIF(SmtpHost,'') IS NOT NULL) AND NOT EXISTS(SELECT 1 FROM dbo.SmsSettings WHERE TwilioEnabled=1 OR GhanaGatewayEnabled=1) THEN 1 ELSE 0 END"
    if ([int]$rehearsalCheck.ExecuteScalar() -ne 1) { throw 'Rehearsal database/outbound isolation verification failed.' }
    if ($rehearsalApiRuntime -eq [IO.Path]::GetFullPath($rehearsalOptionalApprovalPhase2Api)) {
        $rehearsalCheck.CommandText = "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId IN (N'20260912190000_VendorPaymentOptionalApproval',N'20260912200000_JournalBatchOptionalWorkflow',N'20260912210000_OptionalExceptionalAndPrequalificationApproval',N'20260912220000_VendorPaymentDirectEvidence')"
        if ([int]$rehearsalCheck.ExecuteScalar() -ne 4) { throw 'The reviewed Phase 2 schema is not applied; this API will not be started.' }
    }
} finally { $rehearsalProbe.Dispose() }

$rehearsalBaseConfig = Get-Content (Join-Path $rehearsalRepo 'src/ErpSystem.Api/appsettings.json') -Raw | ConvertFrom-Json
$rehearsalEncryptionKey = [string]$rehearsalSecrets.'Security:EncryptionKey'
if (-not $rehearsalEncryptionKey) { $rehearsalEncryptionKey = [string]$rehearsalBaseConfig.Security.EncryptionKey }
if (-not $rehearsalEncryptionKey) { throw 'Existing local encryption configuration is unavailable.' }
$rehearsalEnvironment = @{
    'ASPNETCORE_ENVIRONMENT'='Rehearsal'; 'DOTNET_ENVIRONMENT'='Rehearsal';
    'ASPNETCORE_URLS'='http://127.0.0.1:5002'; 'SkipStartupInitialization'='true';
    'BackgroundServices__Enabled'='false'; 'Database__Provider'='SqlServer';
    'ConnectionStrings__DefaultConnection'=$rehearsalConnectionBuilder.ConnectionString;
    'ConnectionStrings__Redis'=''; 'Security__EncryptionKey'=$rehearsalEncryptionKey;
    'JwtSettings__SecretKey'=([Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N'));
    'JwtSettings__PortalSecretKey'=([Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N'));
    'JwtSettings__Issuer'='RhemaERP.PO.Rehearsal'; 'JwtSettings__Audience'='RhemaERP.PO.Rehearsal.Client';
    'JwtSettings__PortalAudience'='RhemaERP.PO.Rehearsal.Portal'; 'JwtSettings__ExpiryInMinutes'='480';
    'ALLOWED_ORIGINS'='http://127.0.0.1:3002'; 'FrontendUrl'='http://127.0.0.1:3002';
    'CandidatePortal__PortalUrl'='http://127.0.0.1:3002';
    'Application__EnvironmentName'='Rehearsal';
    'FileStorage__Provider'='Local'; 'FileStorage__Local__UseWebRoot'='false';
    'FileStorage__Local__BasePath'=(Join-Path $rehearsalRoot 'api/uploads');
    'FileStorage__Local__PrivateBasePath'=(Join-Path $rehearsalRoot 'api/secure-file-storage');
    'FileStorage__Local__BaseUrl'='http://127.0.0.1:5002/uploads';
    'Sms__Twilio__Enabled'='false'; 'Sms__GhanaGateway__Enabled'='false';
    'HTTP_PROXY'='http://127.0.0.1:5519'; 'HTTPS_PROXY'='http://127.0.0.1:5519'; 'ALL_PROXY'='http://127.0.0.1:5519'; 'NO_PROXY'='';
}
foreach ($rehearsalLicenseName in @('Syncfusion:LicenseKey','SyncfusionLicenseKey')) {
    $rehearsalLicenseValue = [string]$rehearsalSecrets.$rehearsalLicenseName
    if ($rehearsalLicenseValue) { $rehearsalEnvironment[$rehearsalLicenseName.Replace(':','__')] = $rehearsalLicenseValue }
}
# Environment values exist only in this process and its child. No credentials are written to disk or command lines.
$rehearsalPriorEnvironment = @{}
try {
    foreach ($entry in $rehearsalEnvironment.GetEnumerator()) {
        $rehearsalPriorEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key,'Process')
        [Environment]::SetEnvironmentVariable($entry.Key,[string]$entry.Value,'Process')
    }
    $rehearsalNode = (Get-Command node).Source
    if (-not $ApiOnly) {
        $rehearsalBlocker = Start-Process -FilePath $rehearsalNode -ArgumentList 'deny-outbound.cjs' -WorkingDirectory $rehearsalRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $rehearsalRoot 'outbound.stdout.log') -RedirectStandardError (Join-Path $rehearsalRoot 'outbound.stderr.log')
    } elseif (-not (Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort 5519 -State Listen -ErrorAction SilentlyContinue)) {
        throw 'The rehearsal outbound blocker must be running before the API starts.'
    }
    $rehearsalApi = Start-Process -FilePath (Join-Path $rehearsalApiRuntime 'ErpSystem.Api.exe') -ArgumentList '--urls','http://127.0.0.1:5002','--SkipStartupInitialization','true' -WorkingDirectory $rehearsalApiRuntime -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $rehearsalRoot 'api.stdout.log') -RedirectStandardError (Join-Path $rehearsalRoot 'api.stderr.log')
} finally {
    foreach ($entry in $rehearsalPriorEnvironment.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key,$entry.Value,'Process') }
}
if ($ApiOnly) {
    [pscustomobject]@{Database=$rehearsalDatabase;ApiProcessId=$rehearsalApi.Id;OriginalServersUntouched=$true} | ConvertTo-Json -Compress
    return
}
if ($rehearsalApiRuntime -eq [IO.Path]::GetFullPath($rehearsalProductionApi)) {
    $rehearsalReady = $false
    for ($attempt = 0; $attempt -lt 12; $attempt++) {
        try {
            if ((Invoke-WebRequest 'http://127.0.0.1:5002/health/live' -TimeoutSec 5 -UseBasicParsing).StatusCode -eq 200) { $rehearsalReady = $true; break }
        } catch { Start-Sleep -Seconds 2 }
    }
    if (-not $rehearsalReady) { throw 'The prepared rehearsal API did not become healthy; frontend was not started.' }
    & (Join-Path $PSScriptRoot 'Start-LocalPoRehearsalProduction.ps1')
    return
}
$rehearsalOldPort = $env:PORT
$rehearsalOldHostname = $env:HOSTNAME
try {
    $env:PORT='3002'; $env:HOSTNAME='127.0.0.1'
    $rehearsalFrontendProcess = Start-Process -FilePath (Get-Command node).Source -ArgumentList '--require','../frontend-guard.cjs','server.js' -WorkingDirectory (Join-Path $rehearsalRoot 'frontend') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $rehearsalRoot 'frontend.stdout.log') -RedirectStandardError (Join-Path $rehearsalRoot 'frontend.stderr.log')
} finally { $env:PORT=$rehearsalOldPort; $env:HOSTNAME=$rehearsalOldHostname }
[pscustomobject]@{Database=$rehearsalDatabase;Url='http://127.0.0.1:3002';ApiProcessId=$rehearsalApi.Id;FrontendProcessId=$rehearsalFrontendProcess.Id;OutboundBlockerProcessId=$rehearsalBlocker.Id;OriginalServersUntouched=$true} | ConvertTo-Json -Compress
