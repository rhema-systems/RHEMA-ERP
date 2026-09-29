# Dot-source only. No operation runs merely by loading this library.
# The returned ConnectionString is sensitive: keep it in memory; never serialize
# the complete result, send it to a transcript, or include it in a release manifest.
function Assert-RhemaFreshDatabaseName {
    param([string]$FreshDatabaseName, [string]$SourceDatabaseName)
    if ($FreshDatabaseName -cnotmatch '^RhemaERP_[A-Za-z0-9_]{1,119}$' -or
        [string]::IsNullOrWhiteSpace($SourceDatabaseName) -or
        $FreshDatabaseName -ieq $SourceDatabaseName -or
        $SourceDatabaseName -iin @('master','model','msdb','tempdb')) {
        throw 'Use a new RhemaERP_ database name containing only letters, numbers and underscores, distinct from the configured application database.'
    }
}

function Get-RhemaFreshConnectionBuilder {
    param([string]$SourceConnectionString, [string]$FreshDatabaseName)
    try { $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $SourceConnectionString }
    catch { throw 'The configured SQL Server connection is invalid; its value was not logged.' }
    Assert-RhemaFreshDatabaseName $FreshDatabaseName $builder.InitialCatalog
    if ([string]::IsNullOrWhiteSpace($builder.DataSource) -or
        -not [string]::IsNullOrWhiteSpace($builder.AttachDBFilename) -or $builder.UserInstance) {
        throw 'Fresh provisioning requires an ordinary SQL Server connection, without AttachDBFilename or User Instance.'
    }
    return $builder
}

function Invoke-RhemaFreshSql {
    param([string]$ConnectionString, [string]$ExpectedDatabase, [string]$Sql)
    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    $command = $null
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 600
        $command.CommandText = 'SELECT DB_NAME();'
        if ([string]$command.ExecuteScalar() -cne $ExpectedDatabase) { throw 'SQL target identity mismatch.' }
        $command.CommandText = $Sql
        $table = New-Object System.Data.DataTable
        $reader = $command.ExecuteReader()
        try { $table.Load($reader) } finally { $reader.Dispose() }
        return ,$table
    } catch {
        $sqlFailure = $_.Exception
        while ($null -ne $sqlFailure.InnerException -and $sqlFailure -isnot [System.Data.SqlClient.SqlException]) { $sqlFailure=$sqlFailure.InnerException }
        $sqlCode = if ($sqlFailure -is [System.Data.SqlClient.SqlException]) { [string][int]$sqlFailure.Number } else { 'unavailable' }
        throw "Fresh database SQL operation failed (SQL number $sqlCode). Existing database data was not modified by this provisioning library; no connection details were logged."
    }
    finally { if ($command) { $command.Dispose() }; $connection.Dispose() }
}

function Test-RhemaFreshDatabaseTarget {
    param([Parameter(Mandatory=$true)][string]$SourceConnectionString,
          [Parameter(Mandatory=$true)][string]$FreshDatabaseName)
    $builder = Get-RhemaFreshConnectionBuilder $SourceConnectionString $FreshDatabaseName
    $builder['Initial Catalog'] = 'master'
    $result = Invoke-RhemaFreshSql $builder.ConnectionString 'master' @"
SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.databases WHERE name=N'$FreshDatabaseName') THEN 1 ELSE 0 END TargetExists,
       CASE WHEN IS_SRVROLEMEMBER(N'sysadmin')=1 OR IS_SRVROLEMEMBER(N'dbcreator')=1
         OR HAS_PERMS_BY_NAME(NULL,NULL,N'CREATE ANY DATABASE')=1
         OR HAS_PERMS_BY_NAME(N'master',N'DATABASE',N'CREATE DATABASE')=1 THEN 1 ELSE 0 END CanCreate,
       CASE WHEN HAS_PERMS_BY_NAME(NULL,NULL,N'VIEW ANY DATABASE')=1 OR IS_SRVROLEMEMBER(N'sysadmin')=1 THEN 1 ELSE 0 END CanInspect;
"@
    if ([int]$result.Rows[0].TargetExists -ne 0) { throw 'The proposed fresh database already exists. Choose a different name; existing databases are never reused or deleted.' }
    if ([int]$result.Rows[0].CanInspect -ne 1) { throw 'The configured SQL principal cannot verify database-name availability. VIEW ANY DATABASE permission is required for this guarded operation.' }
    if ([int]$result.Rows[0].CanCreate -ne 1) { throw 'The configured SQL principal lacks CREATE DATABASE permission. Have the database administrator review provisioning permissions.' }
}

function Invoke-RhemaFreshApiCli {
    param([string]$ApiExecutable, [string]$ContentRoot, [string]$ConnectionString,
          [ValidateSet('apply-migrations','seed-db','seed-deployment-uat','seed-operational-uat','seed-qs-uat')][string]$Command,
          [int]$TimeoutSeconds = 3600,
          [string]$OperationalUatPassword,
          [string]$ExpectedQsDatabase,
          [switch]$AutoApproveQsUat)
    if ($Command -eq 'seed-qs-uat') {
        $qsTarget = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
        if ($ExpectedQsDatabase -cnotmatch '^RhemaERP_(VpsTest|QsUatVerify)_[A-Za-z0-9_]+$' -or
            $qsTarget.InitialCatalog -cne $ExpectedQsDatabase) {
            throw 'QS preparation requires the exact explicitly selected test database.'
        }
        $qsTarget=$null
    }
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $ApiExecutable
    $start.WorkingDirectory = $ContentRoot
    # WorkingDirectory is the clean content root. Environment also explicitly
    # pins it; no credential-bearing command-line arguments are used.
    $start.Arguments = "$Command --migration-command-timeout-seconds 600"
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.EnvironmentVariables.Clear()
    foreach ($name in @('PATH','PATHEXT','SystemRoot','WINDIR','TEMP','TMP','USERPROFILE','APPDATA','LOCALAPPDATA','ProgramData','ProgramFiles','ProgramFiles(x86)','COMSPEC','HOMEDRIVE','HOMEPATH','DOTNET_ROOT','DOTNET_ROOT(x86)')) {
        $value = [Environment]::GetEnvironmentVariable($name,'Process')
        if ($null -ne $value) { $start.EnvironmentVariables[$name] = $value }
    }
    $environment = @{
        ASPNETCORE_ENVIRONMENT='FreshDatabaseProvisioning'; DOTNET_ENVIRONMENT='FreshDatabaseProvisioning';
        ASPNETCORE_CONTENTROOT=$ContentRoot; DOTNET_CONTENTROOT=$ContentRoot;
        ConnectionStrings__DefaultConnection=$ConnectionString; Database__Provider='SqlServer';
        StartupInitialization__AllowDevelopmentDataSeedingOutsideDevelopment='true';
        SkipStartupInitialization='true'; BackgroundServices__Enabled='false'; EstateRecurringBilling__Enabled='false';
        Finance__AccountingEvents__Enabled='false'; Finance__ProducerIntents__Enabled='false'; Finance__ProducerIntentGroups__Enabled='false';
        Sms__Twilio__Enabled='false'; Sms__GhanaGateway__Enabled='false';
        HTTP_PROXY='http://127.0.0.1:9'; HTTPS_PROXY='http://127.0.0.1:9'; ALL_PROXY='http://127.0.0.1:9'; NO_PROXY='';
        DOTNET_CLI_TELEMETRY_OPTOUT='1'; DOTNET_PROCESSOR_COUNT='2'; DOTNET_GCHeapHardLimit='0x100000000'
    }
    foreach ($entry in $environment.GetEnumerator()) { $start.EnvironmentVariables[$entry.Key] = [string]$entry.Value }
    if ($Command -eq 'seed-qs-uat') {
        $start.EnvironmentVariables['ASPNETCORE_ENVIRONMENT']='Test'
        $start.EnvironmentVariables['DOTNET_ENVIRONMENT']='Test'
        $start.EnvironmentVariables['QsUat__Enabled']='true'
        $start.EnvironmentVariables['QsUat__ExpectedDatabase']=$ExpectedQsDatabase
        $start.EnvironmentVariables['QsUat__AutoApprove']= if($AutoApproveQsUat) { 'true' } else { 'false' }
        $start.EnvironmentVariables['DOTNET_GCHeapHardLimit']='0x200000000'
    }
    if (-not [string]::IsNullOrWhiteSpace($OperationalUatPassword)) {
        $start.EnvironmentVariables['UatBootstrap__SharedPassword'] = $OperationalUatPassword
    }
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    $began = [DateTime]::UtcNow
    $evidence = $null
    Write-Host "FRESH_PROGRESS|Starting $Command. Runtime credentials and raw seed output are suppressed."
    try {
        [void]$process.Start()
        $outTask = $process.StandardOutput.ReadToEndAsync()
        $errTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill()
            $process.WaitForExit()
            throw 'CLI timed out; the newly created database is retained for review.'
        }
        $raw = $outTask.GetAwaiter().GetResult() + "`n" + $errTask.GetAwaiter().GetResult()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $digest = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($raw)))).Replace('-','') }
        finally { $sha.Dispose() }
        $evidence = [pscustomobject]@{
            Command=$Command; ExitCode=$process.ExitCode; OutputSha256=$digest;
            Seconds=[math]::Round(([DateTime]::UtcNow-$began).TotalSeconds,1);
            ExceptionTypes=@([regex]::Matches($raw,'\b(?:System|Microsoft)\.[A-Za-z.]+Exception\b') | ForEach-Object Value | Sort-Object -Unique);
            SqlErrorNumbers=@([regex]::Matches($raw,'Error Number:\s*(\d+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique);
            GuardCodes=@([regex]::Matches($raw,'\b(?:CANONICAL|C[1-8]|FINANCE|TDC|AP|AR|PROCUREMENT|ESTATE|QS)_[A-Z0-9_]{3,90}:') | ForEach-Object { $_.Value.TrimEnd(':') } | Sort-Object -Unique)
            QsDecisionCodes=@([regex]::Matches($raw,'\bQS-DEC-[0-9]{3}\b') | ForEach-Object Value | Sort-Object -Unique)
            QsStages=@([regex]::Matches($raw,'QS_UAT_STAGE\|[A-Z_]+') | ForEach-Object Value)
            MissingServices=@([regex]::Matches($raw,"Unable to resolve service for type '([A-Za-z0-9_.`]+)'") | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
        }
        $raw = $null
        if ($process.ExitCode -ne 0) { throw 'CLI failed; raw output was suppressed because it may contain seeded credentials.' }
        Write-Host "FRESH_PROGRESS|Completed $Command in $($evidence.Seconds) seconds."
        return $evidence
    } catch {
        $exitSummary = if ($null -ne $evidence) { [string][int]$evidence.ExitCode } else { 'unavailable' }
        $safeException = New-Object InvalidOperationException "Fresh database CLI $Command failed or timed out (exit code $exitSummary). Its output was not persisted; the new database is retained for review."
        if ($null -ne $evidence) { $safeException.Data['SafeCliEvidence'] = $evidence }
        throw $safeException
    }
    finally { $process.Dispose(); $start.EnvironmentVariables.Clear(); $raw=$null }
}

function Get-RhemaFreshSeedCounts {
    param([string]$ConnectionString, [string]$DatabaseName)
    $table = Invoke-RhemaFreshSql $ConnectionString $DatabaseName @'
SELECT N'BusinessPartners' Name,COUNT_BIG(*) [Count] FROM dbo.BusinessPartners
UNION ALL SELECT N'BusinessPartnerRoles',COUNT_BIG(*) FROM dbo.BusinessPartnerRoles
UNION ALL SELECT N'BusinessPartnerApProfileVersions',COUNT_BIG(*) FROM dbo.BusinessPartnerApProfileVersions
UNION ALL SELECT N'BusinessPartnerArProfileVersions',COUNT_BIG(*) FROM dbo.BusinessPartnerArProfileVersions
UNION ALL SELECT N'BusinessPartnerApWhtDefaults',COUNT_BIG(*) FROM dbo.BusinessPartnerApWhtDefaults
UNION ALL SELECT N'AccountingBooks',COUNT_BIG(*) FROM dbo.AccountingBooks
UNION ALL SELECT N'Suppliers',COUNT_BIG(*) FROM dbo.Suppliers
UNION ALL SELECT N'JournalEntries',COUNT_BIG(*) FROM dbo.JournalEntries;
'@
    $counts = [ordered]@{}
    foreach ($row in $table.Rows) { $counts[[string]$row.Name] = [long]$row.Count }
    return $counts
}

function Invoke-RhemaFreshDatabaseProvisioning {
    param([Parameter(Mandatory=$true)][string]$SourceConnectionString,
          [Parameter(Mandatory=$true)][string]$FreshDatabaseName,
          [Parameter(Mandatory=$true)][string]$StagedApiDirectory,
          [Parameter(Mandatory=$true)][string[]]$ExpectedMigrationIds,
          [Parameter(Mandatory=$true)][string]$WorkDirectory,
          [string]$OperationalUatPassword)
    $builder = Get-RhemaFreshConnectionBuilder $SourceConnectionString $FreshDatabaseName
    $expected = @($ExpectedMigrationIds | Sort-Object -Unique)
    if ($expected.Count -eq 0 -or $expected.Count -ne $ExpectedMigrationIds.Count -or
        @($expected | Where-Object { $_ -cnotmatch '^20\d{12}_[A-Za-z][A-Za-z0-9_]+$' }).Count -gt 0) {
        throw 'Expected migration IDs must be a nonempty unique complete release migration list.'
    }
    $apiRoot = (Resolve-Path -LiteralPath $StagedApiDirectory -ErrorAction Stop).Path
    foreach ($file in @('ErpSystem.Api.exe','ErpSystem.Api.dll','ErpSystem.Data.dll','ErpSystem.Api.runtimeconfig.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $apiRoot $file) -PathType Leaf)) { throw 'Staged published API artifacts are incomplete.' }
    }
    Test-RhemaFreshDatabaseTarget $SourceConnectionString $FreshDatabaseName
    if (Test-Path -LiteralPath $WorkDirectory) { throw 'Fresh provisioning requires a new dedicated work directory.' }
    [void](New-Item -ItemType Directory -Path $WorkDirectory -ErrorAction Stop)
    $contentRoot = (Resolve-Path -LiteralPath $WorkDirectory).Path
    $settings = @{ Serilog=@{ MinimumLevel=@{ Default='Warning' }; WriteTo=@(@{Name='Console'}) }; Logging=@{LogLevel=@{Default='Warning'}} }
    [IO.File]::WriteAllText((Join-Path $contentRoot 'appsettings.json'), ($settings | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false)))
    $builder['Initial Catalog'] = 'master'
    # Strict identifier validation plus a second check makes concurrent creation
    # fail closed. Never attach, overwrite, reset, drop, restore, or stamp history.
    [void](Invoke-RhemaFreshSql $builder.ConnectionString 'master' "IF DB_ID(N'$FreshDatabaseName') IS NOT NULL THROW 51999,'Fresh target already exists.',1; CREATE DATABASE [$FreshDatabaseName];")
    $builder['Initial Catalog'] = $FreshDatabaseName
    $newConnection = $builder.ConnectionString
    $stage = 'ApplyMigrations'
    try {
        $steps = @()
        $exe = Join-Path $apiRoot 'ErpSystem.Api.exe'
        $steps += Invoke-RhemaFreshApiCli $exe $contentRoot $newConnection 'apply-migrations'
        $stage = 'VerifyMigrationHistory'
        $history = Invoke-RhemaFreshSql $newConnection $FreshDatabaseName 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;'
        $actual = @($history.Rows | ForEach-Object { [string]$_.MigrationId })
        if (($actual -join "`n") -cne ($expected -join "`n")) { throw 'Release migration history mismatch.' }
        $stage = 'SeedFirstPass'
        $seedCommand = if ([string]::IsNullOrWhiteSpace($OperationalUatPassword)) { 'seed-db' } else { 'seed-deployment-uat' }
        $steps += Invoke-RhemaFreshApiCli $exe $contentRoot $newConnection $seedCommand -OperationalUatPassword $OperationalUatPassword
        $first = Get-RhemaFreshSeedCounts $newConnection $FreshDatabaseName
        $operationalFirst = $null
        if ($seedCommand -eq 'seed-deployment-uat') {
            $stage = 'VerifyOperationalFirstPass'
            $operationalFirst = Get-RhemaOperationalSeedSnapshot $newConnection $FreshDatabaseName
            Assert-RhemaOperationalSeedReadiness $operationalFirst
        }
        $stage = 'SeedSecondPass'
        $steps += Invoke-RhemaFreshApiCli $exe $contentRoot $newConnection $seedCommand -OperationalUatPassword $OperationalUatPassword
        $second = Get-RhemaFreshSeedCounts $newConnection $FreshDatabaseName
        $operationalSecond = $null
        if ($operationalFirst) {
            $stage = 'VerifyOperationalSecondPass'
            $operationalSecond = Get-RhemaOperationalSeedSnapshot $newConnection $FreshDatabaseName
            Assert-RhemaOperationalSeedReadiness $operationalSecond
            if ($operationalFirst.Fingerprint -cne $operationalSecond.Fingerprint) { throw 'Operational UAT repeat seeding changed governed seed identities or master data.' }
        }
        $stage = 'VerifySeedIdempotency'
        if (($first | ConvertTo-Json -Compress) -cne ($second | ConvertTo-Json -Compress)) { throw 'Second seed pass changed canonical baseline counts.' }
        if ($second.BusinessPartnerRoles -lt 1 -or $second.BusinessPartnerApProfileVersions -lt 1 -or
            $second.AccountingBooks -lt 1 -or $second.Suppliers -ne 0 -or $second.JournalEntries -ne 0) { throw 'Canonical seed readiness check failed.' }
        $stage = 'VerifySchemaAndConstraints'
        $checks = Invoke-RhemaFreshSql $newConnection $FreshDatabaseName @'
SELECT (SELECT COUNT_BIG(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) InvalidForeignKeys,
       (SELECT COUNT_BIG(*) FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1) InvalidChecks,
       CASE WHEN COL_LENGTH(N'dbo.VendorInvoice',N'BusinessPartnerId') IS NOT NULL
         AND COL_LENGTH(N'dbo.VendorInvoice',N'SupplierId') IS NULL
         AND COL_LENGTH(N'dbo.VendorInvoice',N'EstateAcquisitionId') IS NOT NULL
         AND OBJECT_ID(N'dbo.BusinessPartnerApProfileVersions',N'U') IS NOT NULL THEN 1 ELSE 0 END CanonicalSchema;
'@
        if ([long]$checks.Rows[0].InvalidForeignKeys -ne 0 -or [long]$checks.Rows[0].InvalidChecks -ne 0 -or [int]$checks.Rows[0].CanonicalSchema -ne 1) { throw 'Canonical schema or trusted constraint verification failed.' }
        $stage = 'PhysicalIntegrity'
        $dbcc = Invoke-RhemaFreshSql $newConnection $FreshDatabaseName "DBCC CHECKDB (N'$FreshDatabaseName') WITH PHYSICAL_ONLY, NO_INFOMSGS, TABLERESULTS;"
        if ($dbcc.Rows.Count -gt 0) { throw 'Database physical integrity check reported errors.' }
        return [pscustomobject]@{
            ConnectionString=$newConnection; DatabaseName=$FreshDatabaseName; MigrationIds=$actual;
            SeedCounts=$second; CliEvidence=$steps; PhysicalIntegrityPassed=$true; ForeignKeysTrusted=$true
            OperationalSeed=$operationalSecond
        }
    } catch {
        $safeReason = ''
        if ($_.Exception.Message -cmatch '^Fresh database SQL operation failed \(SQL number (-?\d+|unavailable)\)\.') {
            $safeReason = ' SQL error number: ' + $Matches[1] + '.'
        } elseif ($_.Exception.Message -cmatch '^Fresh database CLI (apply-migrations|seed-db|seed-deployment-uat|seed-operational-uat) failed or timed out \(exit code (-?\d+|unavailable)\)\.') {
            $safeReason = ' CLI exit code: ' + $Matches[2] + '.'
        }
        $safeException = New-Object InvalidOperationException "Fresh provisioning failed at $stage.$safeReason Existing application database remains unchanged; new target $FreshDatabaseName is retained for review. No raw command output or connection details were persisted."
        $failureEvidence = [ordered]@{ Stage=$stage; Database=$FreshDatabaseName; Reason=$safeReason }
        if ($stage -eq 'VerifyOperationalFirstPass' -and $null -ne $operationalFirst) {
            $failureEvidence.OperationalFailures = @($operationalFirst.Failures)
        }
        if ($stage -eq 'VerifyOperationalSecondPass' -and $null -ne $operationalSecond) {
            $failureEvidence.OperationalFailures = @($operationalSecond.Failures)
        }
        if ($_.Exception.Data.Contains('SafeCliEvidence')) {
            $diagnostics = $_.Exception.Data['SafeCliEvidence']
            $safeException.Data['SafeCliEvidence'] = $diagnostics
            # Only this explicit allowlist survives the child PowerShell boundary.
            # Never serialize the exception, its full Data dictionary or raw output.
            $failureEvidence.Cli = [ordered]@{
                Command=$diagnostics.Command; ExitCode=$diagnostics.ExitCode; Seconds=$diagnostics.Seconds
                OutputSha256=$diagnostics.OutputSha256; ExceptionTypes=$diagnostics.ExceptionTypes
                SqlErrorNumbers=$diagnostics.SqlErrorNumbers; GuardCodes=$diagnostics.GuardCodes
            }
        }
        try {
            [IO.File]::WriteAllText((Join-Path $contentRoot 'failure.json'), ($failureEvidence | ConvertTo-Json -Depth 5),
                (New-Object Text.UTF8Encoding($false)))
        } catch { } # Preserve the original failure even if evidence cannot be written.
        throw $safeException
    } finally { $newConnection=$null; $builder=$null }
}
