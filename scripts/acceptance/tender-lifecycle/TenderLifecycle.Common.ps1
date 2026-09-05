Set-StrictMode -Version Latest

$script:TenderFixtureDatabasePrefix = 'RhemaERP_TenderBrowser_'

function Get-TenderFixtureEnvironmentValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Name,
        [string]$Default,
        [switch]$Required
    )

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) { $value = $Default }
    if ($Required -and [string]::IsNullOrWhiteSpace($value)) {
        throw "Required environment variable '$Name' is not configured."
    }
    $value
}

function New-TenderFixtureSqlConnection {
    [CmdletBinding()]
    param([string]$ConnectionString)

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        $ConnectionString = Get-TenderFixtureEnvironmentValue `
            -Name 'TENDER_E2E_SQL_CONNECTION'
        if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
            $ConnectionString = Get-TenderFixtureEnvironmentValue `
                -Name 'TDC_TENDER_SQL_CONNECTION' -Required
        }
    }

    Add-Type -AssemblyName System.Data
    $connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
    $connection.Open()
    $connection
}

function Add-TenderFixtureSqlParameters {
    param(
        [Parameter(Mandatory)][System.Data.SqlClient.SqlCommand]$Command,
        [hashtable]$Parameters = @{}
    )

    foreach ($entry in $Parameters.GetEnumerator()) {
        $value = if ($null -eq $entry.Value) { [DBNull]::Value } else { $entry.Value }
        [void]$Command.Parameters.AddWithValue("@$($entry.Key)", $value)
    }
}

function Invoke-TenderFixtureQuery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory)][string]$Sql,
        [hashtable]$Parameters = @{},
        [System.Data.SqlClient.SqlTransaction]$Transaction
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 120
    if ($null -ne $Transaction) { $command.Transaction = $Transaction }
    Add-TenderFixtureSqlParameters -Command $command -Parameters $Parameters
    $table = [System.Data.DataTable]::new()
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($command)
    [void]$adapter.Fill($table)
    Write-Output -NoEnumerate $table
}

function Invoke-TenderFixtureScalar {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory)][string]$Sql,
        [hashtable]$Parameters = @{},
        [System.Data.SqlClient.SqlTransaction]$Transaction
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 120
    if ($null -ne $Transaction) { $command.Transaction = $Transaction }
    Add-TenderFixtureSqlParameters -Command $command -Parameters $Parameters
    $command.ExecuteScalar()
}

function Invoke-TenderFixtureNonQuery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory)][string]$Sql,
        [hashtable]$Parameters = @{},
        [System.Data.SqlClient.SqlTransaction]$Transaction
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 120
    if ($null -ne $Transaction) { $command.Transaction = $Transaction }
    Add-TenderFixtureSqlParameters -Command $command -Parameters $Parameters
    $command.ExecuteNonQuery()
}

function Get-TenderFixtureDatabaseName {
    param([Parameter(Mandatory)][System.Data.SqlClient.SqlConnection]$Connection)
    [string](Invoke-TenderFixtureScalar -Connection $Connection -Sql 'SELECT DB_NAME();')
}

function Assert-TenderFixtureDisposableDatabase {
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.Data.SqlClient.SqlConnection]$Connection)

    $databaseName = Get-TenderFixtureDatabaseName -Connection $Connection
    $approvedPattern = '^RhemaERP_TenderBrowser_[0-9a-f]{32}$'
    if ($databaseName -notmatch $approvedPattern) {
        throw "Fixture writes are refused for database '$databaseName'. The database name must start with '$script:TenderFixtureDatabasePrefix'."
    }
    if ($databaseName -in @('master', 'model', 'msdb', 'tempdb')) {
        throw 'Fixture writes are never permitted against a SQL Server system database.'
    }
    $databaseName
}

function New-TenderFixtureDeterministicGuid {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RunId,
        [Parameter(Mandatory)][string]$Label
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha256.ComputeHash(
            [System.Text.Encoding]::UTF8.GetBytes("tdc-tender-e2e|$RunId|$Label"))
    }
    finally {
        $sha256.Dispose()
    }
    $guidBytes = [byte[]]::new(16)
    [Array]::Copy($bytes, $guidBytes, 16)
    $guidBytes[7] = ($guidBytes[7] -band 0x0f) -bor 0x50
    $guidBytes[8] = ($guidBytes[8] -band 0x3f) -bor 0x80
    [Guid]::new($guidBytes)
}

function Get-TenderFixtureSha256 {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Value)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value))
        ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Invoke-TenderFixtureApi {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('GET','POST','PUT','DELETE')][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [string]$Token,
        $Body,
        [int[]]$ExpectedStatus = @(200),
        [string]$ApiBaseUrl
    )

    if ([string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
        $ApiBaseUrl = Get-TenderFixtureEnvironmentValue `
            -Name 'TENDER_E2E_API_BASE_URL' -Required
    }
    $headers = @{ 'X-Correlation-ID' = "TENDER-E2E-$([Guid]::NewGuid().ToString('N'))" }
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers.Authorization = "Bearer $Token"
    }
    $parameters = @{
        Uri = "$($ApiBaseUrl.TrimEnd('/'))$Path"
        Method = $Method
        Headers = $headers
        UseBasicParsing = $true
        # The first authenticated request compiles the full EF model on a fresh
        # disposable database. Keep the lifecycle bounded, but allow that cold
        # start to complete on resource-constrained acceptance hosts.
        TimeoutSec = 300
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 40 -Compress
    }
    try {
        $response = Invoke-WebRequest @parameters
        $statusCode = [int]$response.StatusCode
        $content = if ($response.Content -is [byte[]]) {
            [Text.Encoding]::UTF8.GetString($response.Content)
        } else { [string]$response.Content }
    }
    catch {
        # Windows PowerShell 5.1 has no Invoke-WebRequest -SkipHttpErrorCheck.
        # Preserve controlled 4xx/5xx payloads so the caller can assert the exact
        # expected status without making the harness PowerShell-version dependent.
        $errorResponse = $_.Exception.Response
        if ($null -eq $errorResponse) {
            throw "$Method $Path failed before an HTTP response was received: $($_.Exception.Message)"
        }
        $statusCode = [int]$errorResponse.StatusCode
        $stream = $errorResponse.GetResponseStream()
        if ($null -eq $stream) {
            $content = ''
        }
        else {
            $reader = [System.IO.StreamReader]::new($stream)
            try { $content = $reader.ReadToEnd() }
            finally {
                $reader.Dispose()
                $stream.Dispose()
            }
        }
        $response = [pscustomobject]@{ StatusCode = $statusCode; Content = $content }
    }
    if ($statusCode -notin $ExpectedStatus) {
        $safeContent = if ($content.Length -gt 2000) { $content.Substring(0, 2000) } else { $content }
        throw "$Method $Path returned HTTP ${statusCode}: $safeContent"
    }
    if ([string]::IsNullOrWhiteSpace($content)) { return $null }
    $content | ConvertFrom-Json
}

function Connect-TenderFixtureActor {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$UsernameEnvironmentName,
        [Parameter(Mandatory)][string]$PasswordEnvironmentName,
        [Parameter(Mandatory)][string]$DefaultUsername,
        [string]$ApiBaseUrl
    )

    $username = Get-TenderFixtureEnvironmentValue `
        -Name $UsernameEnvironmentName -Default $DefaultUsername
    $password = Get-TenderFixtureEnvironmentValue `
        -Name $PasswordEnvironmentName -Required
    $result = Invoke-TenderFixtureApi -Method POST -Path '/api/auth/login' `
        -Body @{ username=$username; password=$password; rememberMe=$false } `
        -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl
    if ([string]::IsNullOrWhiteSpace([string]$result.token)) {
        throw "Authentication did not issue a token for acceptance actor '$username'."
    }
    [pscustomobject]@{ Username=$username; Token=[string]$result.token }
}

function Assert-TenderFixtureSchema {
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.Data.SqlClient.SqlConnection]$Connection)

    $requiredTables = @(
        'Tenants', 'Users', 'AspNetRoles', 'UserRoles', 'Permissions', 'RolePermissions',
        'ProcurementResponsibilityAssignments', 'ProcurementCommittees',
        'ProcurementCommitteeMembers', 'BusinessPartners', 'BusinessPartnerUsers',
        'PartnerCategories', 'BusinessPartnerCategories', 'BusinessPartnerDocuments',
        'ProcurementBudgets', 'PurchaseRequisitions', 'PurchaseRequisitionItems',
        'ProcurementRequisitionSourcingReleases', 'ProcurementSourcingCases',
        'EvaluationCriteria', 'EvaluationTemplates', 'EvaluationTemplateCriteria',
        'Tenders', 'TenderLots', 'TenderItems', 'TenderInvitations', 'TenderFees',
        'TenderPayments', 'TenderBids', 'TenderBidLots', 'TenderBidItems',
        'TenderEvaluators', 'TenderEvaluations', 'TenderAwards', 'PurchaseOrders', 'Contracts',
        'ProcurementTenderDocumentTemplateVersions', 'ProcurementTenderDocumentTemplateMethods',
        'ProcurementTenderDocumentRegisters', 'ProcurementTenderDocumentIssuances',
        'ProcurementTenderControls', 'ProcurementTenderDocumentIssues',
        'ProcurementTenderSubmissionReceipts', 'ProcurementSourcingCaseSourceRequests',
        'ProcurementEvaluationCommitteeControls',
        'ProcurementEvaluationCommitteeAppointments',
        'ProcurementEvaluationConflictDeclarations', 'ProcurementEvaluationMeetings',
        'ProcurementEvaluationAttendanceRecords', 'ProcurementEvaluationScoreSheets'
    )
    $missing = [System.Collections.Generic.List[string]]::new()
    foreach ($table in $requiredTables) {
        $exists = Invoke-TenderFixtureScalar -Connection $Connection -Sql @'
SELECT COUNT(1) FROM sys.tables WHERE object_id=OBJECT_ID(@qualifiedName);
'@ -Parameters @{ qualifiedName = "dbo.$table" }
        if ([int]$exists -ne 1) { $missing.Add($table) }
    }
    if ($missing.Count -gt 0) {
        throw "Current-model tender fixture tables are missing: $($missing -join ', '). Apply all migrations before preparing acceptance data."
    }

    $requiredColumns = @(
        @{ Table = 'Tenders'; Column = 'SourcingReleaseId' },
        @{ Table = 'Tenders'; Column = 'SourcingCaseId' },
        @{ Table = 'ProcurementTenderControls'; Column = 'MethodRuleId' },
        @{ Table = 'ProcurementTenderControls'; Column = 'AuthorityRouteId' },
        @{ Table = 'ProcurementTenderControls'; Column = 'WorkflowDefinitionId' },
        @{ Table = 'ProcurementTenderControls'; Column = 'LifecycleSnapshotJson' },
        @{ Table = 'ProcurementTenderControls'; Column = 'IntegrityHash' },
        @{ Table = 'ProcurementTenderControls'; Column = 'RowVersion' },
        @{ Table = 'ProcurementTenderSubmissionReceipts'; Column = 'SealedSnapshotJson' },
        @{ Table = 'ProcurementTenderSubmissionReceipts'; Column = 'IntegrityHash' },
        @{ Table = 'TenderPayments'; Column = 'PostingEventId' },
        @{ Table = 'TenderPayments'; Column = 'JournalEntryId' },
        @{ Table = 'TenderPayments'; Column = 'PostedAtUtc' },
        @{ Table = 'PurchaseOrders'; Column = 'SourceIntegrityHash' },
        @{ Table = 'PurchaseOrders'; Column = 'SourcingReleaseId' },
        @{ Table = 'PurchaseOrders'; Column = 'SourcingCaseId' }
    )
    $missingColumns = [System.Collections.Generic.List[string]]::new()
    foreach ($item in $requiredColumns) {
        $exists = Invoke-TenderFixtureScalar -Connection $Connection -Sql @'
SELECT COUNT(1)
FROM sys.columns
WHERE object_id=OBJECT_ID(@qualifiedName) AND name=@columnName;
'@ -Parameters @{
            qualifiedName = "dbo.$($item.Table)"
            columnName = $item.Column
        }
        if ([int]$exists -ne 1) { $missingColumns.Add("$($item.Table).$($item.Column)") }
    }
    if ($missingColumns.Count -gt 0) {
        throw "Current-model tender fixture columns are missing: $($missingColumns -join ', '). Migration parity is required."
    }
}

function Assert-TenderFixtureCondition {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )
    if (-not $Condition) { throw $Message }
}

function ConvertTo-TenderFixtureGuid {
    [CmdletBinding()]
    param(
        $Value,
        [Parameter(Mandatory)][string]$Label,
        [switch]$AllowEmpty
    )

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        if ($AllowEmpty) { return [Guid]::Empty }
        throw "$Label is required."
    }
    $parsed = [Guid]::Empty
    if (-not [Guid]::TryParse([string]$Value, [ref]$parsed) -or $parsed -eq [Guid]::Empty) {
        throw "$Label must be a non-empty GUID."
    }
    $parsed
}

function Get-TenderFixtureObjectValue {
    param(
        $Object,
        [Parameter(Mandatory)][string]$Name
    )
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    $property.Value
}

function Write-TenderFixtureJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Value,
        [Parameter(Mandatory)][string]$Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $directory = [System.IO.Path]::GetDirectoryName($fullPath)
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }
    [System.IO.File]::WriteAllText(
        $fullPath,
        ($Value | ConvertTo-Json -Depth 20),
        [System.Text.UTF8Encoding]::new($false))
    $fullPath
}
