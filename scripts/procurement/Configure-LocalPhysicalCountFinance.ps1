#requires -Version 7.0
param(
    [ValidateSet('RhemaERP_PO_Rehearsal_20260909','RhemaERP')][string]$Database='RhemaERP_PO_Rehearsal_20260909',
    [switch]$Apply,
    # Explicitly approved setup only: Finance Reviewer and employee Internal Audit,
    # restricted to Project Demo Warehouse, with all locations in that warehouse.
    [switch]$ConfigureReviewers
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data
Add-Type -AssemblyName System.Net.Http
$countFinanceRepo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$countFinanceRuntime='C:/Users/micha/AppData/Local/Temp/tdc-count-e2e-20260912/api'
$countFinanceExe=[IO.Path]::GetFullPath((Join-Path $countFinanceRuntime 'ErpSystem.Api.exe'))
$countFinanceTenant='00000000-0000-0000-0000-000000000001'
$countFinanceConnection=$null
$countFinanceProcess=$null
$countFinanceClient=$null
$countFinancePassword=$null
$countFinanceLogin=$null
$countFinancePhase='Read-only preflight'
$countFinanceNewAssignmentIds=@()
$countFinanceReviewers=@()

function Invoke-CountFinanceSql([string]$Query,[hashtable]$Parameters=@{}) {
    $countFinanceCommand=$countFinanceConnection.CreateCommand()
    $countFinanceCommand.CommandTimeout=180
    $countFinanceCommand.CommandText=$Query
    foreach($countFinanceParameter in $Parameters.GetEnumerator()) {
        [void]$countFinanceCommand.Parameters.AddWithValue('@'+$countFinanceParameter.Key,$countFinanceParameter.Value)
    }
    try {
        $countFinanceReader=$countFinanceCommand.ExecuteReader()
        try {
            $countFinanceRows=@()
            while($countFinanceReader.Read()) {
                $countFinanceFields=[ordered]@{}
                for($countFinanceField=0;$countFinanceField -lt $countFinanceReader.FieldCount;$countFinanceField++) {
                    $countFinanceFields[$countFinanceReader.GetName($countFinanceField)]=if($countFinanceReader.IsDBNull($countFinanceField)){$null}else{$countFinanceReader.GetValue($countFinanceField)}
                }
                $countFinanceRows += [pscustomobject]$countFinanceFields
            }
            return $countFinanceRows
        } finally {$countFinanceReader.Dispose()}
    } finally {$countFinanceCommand.Dispose()}
}

function Invoke-CountFinanceApi([string]$Method,[string]$Path,[object]$Body=$null) {
    $countFinanceRequest=[System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method),'http://127.0.0.1:5003'+$Path)
    try {
        if($null -ne $Body) {$countFinanceRequest.Content=[System.Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 12 -Compress),[Text.Encoding]::UTF8,'application/json')}
        $countFinanceResponse=$countFinanceClient.SendAsync($countFinanceRequest).GetAwaiter().GetResult()
        try {
            $countFinanceResponseText=$countFinanceResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if(-not $countFinanceResponse.IsSuccessStatusCode) {
                $countFinanceCode=''
                if($Path -ne '/api/Auth/login') {try {$countFinanceCode=[string](($countFinanceResponseText | ConvertFrom-Json).code)}catch{}}
                throw "Central API $Method $Path failed with HTTP $([int]$countFinanceResponse.StatusCode) $countFinanceCode. No automatic retry or credential reset was attempted."
            }
            if([string]::IsNullOrWhiteSpace($countFinanceResponseText)){return $null}
            return $countFinanceResponseText | ConvertFrom-Json
        } finally {$countFinanceResponse.Dispose()}
    } finally {$countFinanceRequest.Dispose()}
}

function Get-CountFinanceBaseline {
    $countFinanceBaseline=[ordered]@{}
    foreach($countFinanceTable in @('PhysicalCounts','PhysicalCountItems','StockAdjustments','StockAdjustmentItems','StockMovements','InventoryItems','InventoryLocations','InventoryBalances','JournalEntries','AccountTransactions','FinancePostingEvents')) {
        $countFinanceHash=@(Invoke-CountFinanceSql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.[$countFinanceTable] WHERE TenantId=@tenant ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES))),2) ValueHash" @{tenant=[Guid]$countFinanceTenant})[0].ValueHash
        $countFinanceBaseline[$countFinanceTable]=$countFinanceHash
    }
    $countFinanceBaseline['Migrations']=@(Invoke-CountFinanceSql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.__EFMigrationsHistory ORDER BY MigrationId FOR JSON PATH))),2) ValueHash")[0].ValueHash
    if(-not $ConfigureReviewers) {
        $countFinanceBaseline['UserRoles']=@(Invoke-CountFinanceSql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.UserRoles ORDER BY UserId,RoleId FOR JSON PATH))),2) ValueHash")[0].ValueHash
        $countFinanceBaseline['Responsibilities']=@(Invoke-CountFinanceSql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.ProcurementResponsibilityAssignments WHERE TenantId=@tenant ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES))),2) ValueHash" @{tenant=[Guid]$countFinanceTenant})[0].ValueHash
    }
    return $countFinanceBaseline | ConvertTo-Json -Compress
}

function Get-CountFinanceSettingsInvariant {
    # Exclude only the two authorized mappings and automatic modification metadata.
    $countFinanceInvariantSql=@'
DECLARE @columns nvarchar(max)=STUFF((SELECT N','+QUOTENAME(c.name) FROM sys.columns c
 WHERE c.object_id=OBJECT_ID(N'dbo.FinanceSettings')
 AND c.name NOT IN(N'WriteOffExpenseAccountId',N'WriteOffRecoveryAccountId',N'UpdatedAt',N'UpdatedBy',N'LastModifiedById',N'RowVersion')
 ORDER BY c.column_id FOR XML PATH(''),TYPE).value('.','nvarchar(max)'),1,1,N'');
DECLARE @sql nvarchar(max)=N'SELECT '+@columns+N' FROM dbo.FinanceSettings WHERE TenantId=@tenant AND IsDeleted=0 FOR JSON PATH,INCLUDE_NULL_VALUES';
EXEC sys.sp_executesql @sql,N'@tenant uniqueidentifier',@tenant;
'@
    $countFinanceInvariantRows=@(Invoke-CountFinanceSql $countFinanceInvariantSql @{tenant=[Guid]$countFinanceTenant})
    return ($countFinanceInvariantRows | ForEach-Object {$_.PSObject.Properties.Value}) -join ''
}

function Get-CountFinanceReviewerInvariant {
    # Exclude only the two explicitly approved role links and newly created scopes.
    # Every prior assignment and its history remain covered by the exact hash.
    $countFinanceSecurityParameters=@{
        tenant=[Guid]$countFinanceTenant;financeUser=$countFinanceReviewers[0].UserId;financeRole=$countFinanceReviewers[0].RoleId;
        auditUser=$countFinanceReviewers[1].UserId;auditRole=$countFinanceReviewers[1].RoleId;
        newIds=(ConvertTo-Json -InputObject @($countFinanceNewAssignmentIds | ForEach-Object {[string]$_}) -Compress)
    }
    $countFinanceSecuritySql=@'
SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),
 (SELECT UserId,RoleId FROM dbo.UserRoles WHERE NOT(UserId=@financeUser AND RoleId=@financeRole)
 AND NOT(UserId=@auditUser AND RoleId=@auditRole) ORDER BY UserId,RoleId FOR JSON PATH))),2) Roles,
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),
 (SELECT Id,UserName,Email,PhoneNumber,FirstName,LastName,IsActive,EmployeeId,TenantId,PasswordHash
 FROM dbo.Users WHERE Id IN(@financeUser,@auditUser) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES))),2) Profiles,
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),
 (SELECT * FROM dbo.ProcurementResponsibilityAssignments WHERE TenantId=@tenant
 AND Id NOT IN(SELECT CONVERT(uniqueidentifier,[value]) FROM OPENJSON(@newIds)) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES))),2) Assignments,
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),
 (SELECT * FROM dbo.ProcurementResponsibilityWarehouses WHERE TenantId=@tenant
 AND AssignmentId NOT IN(SELECT CONVERT(uniqueidentifier,[value]) FROM OPENJSON(@newIds)) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES))),2) WarehouseScopes,
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),
 (SELECT * FROM dbo.ProcurementResponsibilityLocations WHERE TenantId=@tenant
 AND AssignmentId NOT IN(SELECT CONVERT(uniqueidentifier,[value]) FROM OPENJSON(@newIds)) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES))),2) LocationScopes;
'@
    return @(Invoke-CountFinanceSql $countFinanceSecuritySql $countFinanceSecurityParameters)[0] | ConvertTo-Json -Compress
}

function Get-CountFinanceActiveReviewerAssignments([hashtable]$Reviewer) {
    return @(Invoke-CountFinanceSql @'
SELECT a.Id,a.WarehouseScopeMode,a.LocationScopeMode,a.EffectiveFrom,a.EffectiveTo,
 (SELECT COUNT(*) FROM dbo.ProcurementResponsibilityWarehouses w WHERE w.AssignmentId=a.Id AND w.TenantId=@tenant AND w.IsDeleted=0) WarehouseCount,
 (SELECT COUNT(*) FROM dbo.ProcurementResponsibilityWarehouses w WHERE w.AssignmentId=a.Id AND w.TenantId=@tenant AND w.IsDeleted=0 AND w.WarehouseId=@warehouse) ExactWarehouseCount,
 (SELECT COUNT(*) FROM dbo.ProcurementResponsibilityLocations l WHERE l.AssignmentId=a.Id AND l.TenantId=@tenant AND l.IsDeleted=0) LocationCount
FROM dbo.ProcurementResponsibilityAssignments a
WHERE a.TenantId=@tenant AND a.UserId=@user AND a.RoleName=@role AND a.IsActive=1 AND a.IsDeleted=0;
'@ @{tenant=[Guid]$countFinanceTenant;user=$Reviewer.UserId;role=$Reviewer.Role;warehouse=$countFinanceWarehouse.Id})
}

function Assert-CountFinanceReviewerAssignment([object]$Assignment) {
    if($Assignment.WarehouseScopeMode -ne 2 -or $Assignment.LocationScopeMode -ne 1 -or $Assignment.WarehouseCount -ne 1 -or $Assignment.ExactWarehouseCount -ne 1 -or $Assignment.LocationCount -ne 0 -or $Assignment.EffectiveFrom -gt [DateTime]::UtcNow -or ($Assignment.EffectiveTo -and $Assignment.EffectiveTo -le [DateTime]::UtcNow)) {
        throw 'An active reviewer assignment differs from the approved current Project Demo Warehouse / all-locations scope. No existing assignment will be overwritten.'
    }
}

try {
    $countFinanceConnection=[System.Data.SqlClient.SqlConnection]::new("Server=RHEMA-MICHAEL\SQL2017;Database=$Database;Integrated Security=True;Application Name=PhysicalCountFinanceSetup")
    $countFinanceConnection.Open()
    $countFinanceIdentity=@(Invoke-CountFinanceSql "SELECT DB_NAME() DatabaseName,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) ServerName")[0]
    if($countFinanceIdentity.DatabaseName -ne $Database -or $countFinanceIdentity.ServerName -ne 'RHEMA-MICHAEL\SQL2017'){throw 'The exact approved local database was not verified.'}
    $countFinanceSettings=@(Invoke-CountFinanceSql 'SELECT Id,CoaType,BaseCurrency,AccountSeparator,WriteOffExpenseAccountId,WriteOffRecoveryAccountId FROM dbo.FinanceSettings WHERE TenantId=@tenant AND IsDeleted=0' @{tenant=[Guid]$countFinanceTenant})
    if($countFinanceSettings.Count -ne 1 -or $countFinanceSettings[0].CoaType -ne 'Segmented' -or $countFinanceSettings[0].BaseCurrency -ne 'GHS' -or $countFinanceSettings[0].AccountSeparator -ne '-'){throw 'Finance settings differ from the reviewed segmented GHS configuration.'}
    $countFinanceSegments=@(Invoke-CountFinanceSql 'SELECT Id,SegmentName,SegmentPosition,SegmentLength,LookupTableRequired FROM dbo.AccountSegmentStructures WHERE TenantId=@tenant AND IsDeleted=0 AND IsActive=1 ORDER BY SegmentPosition' @{tenant=[Guid]$countFinanceTenant})
    if($countFinanceSegments.Count -ne 3 -or $countFinanceSegments[0].SegmentName -ne 'Department' -or $countFinanceSegments[0].SegmentLength -ne 3 -or $countFinanceSegments[1].SegmentName -ne 'Natural Account' -or $countFinanceSegments[1].SegmentLength -ne 4 -or $countFinanceSegments[1].LookupTableRequired -or $countFinanceSegments[2].SegmentName -ne 'Project' -or $countFinanceSegments[2].SegmentLength -ne 4){throw 'The reviewed three-part account structure changed.'}
    $countFinanceDepartment=@(Invoke-CountFinanceSql 'SELECT Id FROM dbo.SegmentLookupValues WHERE TenantId=@tenant AND SegmentStructureId=@segment AND SegmentValue=N''000'' AND IsActive=1 AND IsDeleted=0' @{tenant=[Guid]$countFinanceTenant;segment=$countFinanceSegments[0].Id})
    $countFinanceProject=@(Invoke-CountFinanceSql 'SELECT Id FROM dbo.SegmentLookupValues WHERE TenantId=@tenant AND SegmentStructureId=@segment AND SegmentValue=N''0000'' AND IsActive=1 AND IsDeleted=0' @{tenant=[Guid]$countFinanceTenant;segment=$countFinanceSegments[2].Id})
    if($countFinanceDepartment.Count -ne 1 -or $countFinanceProject.Count -ne 1){throw 'The exact General Department / No Project segment values were not found.'}
    $countFinanceSpecs=@(
        @{Code='000-6800-0000';Name='Stock Count Losses';Type='Expense';TypeNumber=5;Category='Other Expenses';Natural='6800';Setting='WriteOffExpenseAccountId'},
        @{Code='000-4940-0000';Name='Stock Count Gains';Type='Revenue';TypeNumber=4;Category='Other Income';Natural='4940';Setting='WriteOffRecoveryAccountId'}
    )
    foreach($countFinanceSpec in $countFinanceSpecs) {
        $countFinanceExisting=@(Invoke-CountFinanceSql 'SELECT Id,AccountCode,AccountNumber,AccountName,AccountType,CurrencyCode,Status,AllowDirectPosting,IsControlAccount,IsDeleted FROM dbo.Accounts WHERE TenantId=@tenant AND (AccountCode=@code OR AccountNumber=@code)' @{tenant=[Guid]$countFinanceTenant;code=$countFinanceSpec.Code})
        if($countFinanceExisting.Count -gt 1){throw 'The requested account code is ambiguous.'}
        if($countFinanceExisting.Count -eq 1) {
            $countFinanceAccount=$countFinanceExisting[0]
            if($countFinanceAccount.AccountCode -ne $countFinanceSpec.Code -or $countFinanceAccount.AccountNumber -ne $countFinanceSpec.Code -or $countFinanceAccount.AccountName -ne $countFinanceSpec.Name -or $countFinanceAccount.AccountType -ne $countFinanceSpec.TypeNumber -or $countFinanceAccount.CurrencyCode -ne 'GHS' -or $countFinanceAccount.Status -ne 1 -or -not $countFinanceAccount.AllowDirectPosting -or $countFinanceAccount.IsControlAccount -or $countFinanceAccount.IsDeleted){throw 'An existing requested code differs from the exact dedicated account. It will not be overwritten.'}
            $countFinanceSpec.Id=$countFinanceAccount.Id
        }
        $countFinanceExistingMapping=$countFinanceSettings[0].($countFinanceSpec.Setting)
        if($countFinanceExistingMapping -and (!$countFinanceSpec.Id -or $countFinanceExistingMapping -ne $countFinanceSpec.Id)){throw 'A mapping is already assigned elsewhere. Existing Finance configuration will not be overwritten.'}
    }
    if($ConfigureReviewers) {
        $countFinanceWarehouses=@(Invoke-CountFinanceSql 'SELECT Id,Name,Code FROM dbo.Warehouses WHERE TenantId=@tenant AND Code=N''DEMO-PM'' AND Name=N''Project Demo Warehouse'' AND IsActive=1 AND IsDeleted=0' @{tenant=[Guid]$countFinanceTenant})
        if($countFinanceWarehouses.Count -ne 1){throw 'The exact approved active Project Demo Warehouse was not found.'}
        $countFinanceWarehouse=$countFinanceWarehouses[0]
        $countFinanceReviewers=@(
            @{Username='financereviewer';Role='TDC_FINANCE_REVIEWER'},
            @{Username='employee';Role='TDC_INTERNAL_AUDIT'}
        )
        foreach($countFinanceReviewer in $countFinanceReviewers) {
            $countFinanceUsers=@(Invoke-CountFinanceSql 'SELECT u.Id FROM dbo.Users u WHERE u.UserName=@username AND u.IsActive=1 AND EXISTS(SELECT 1 FROM dbo.UserTenants ut WHERE ut.UserId=u.Id AND ut.TenantId=@tenant AND ut.IsDeleted=0 AND ut.Status=0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt>GETUTCDATE()))' @{username=$countFinanceReviewer.Username;tenant=[Guid]$countFinanceTenant})
            $countFinanceRoles=@(Invoke-CountFinanceSql 'SELECT Id FROM dbo.AspNetRoles WHERE Name=@role' @{role=$countFinanceReviewer.Role})
            if($countFinanceUsers.Count -ne 1 -or $countFinanceRoles.Count -ne 1){throw 'An exact approved active reviewer or provisioned security role was not found.'}
            $countFinanceReviewer.UserId=$countFinanceUsers[0].Id;$countFinanceReviewer.RoleId=$countFinanceRoles[0].Id
            $countFinanceReviewer.HasSecurityRole=@(Invoke-CountFinanceSql 'SELECT UserId FROM dbo.UserRoles WHERE UserId=@user AND RoleId=@role' @{user=$countFinanceReviewer.UserId;role=$countFinanceReviewer.RoleId}).Count -eq 1
            $countFinanceExistingAssignments=@(Get-CountFinanceActiveReviewerAssignments $countFinanceReviewer)
            if($countFinanceExistingAssignments.Count -gt 1){throw 'Multiple active assignments require separate administrator review.'}
            if($countFinanceExistingAssignments.Count -eq 1){Assert-CountFinanceReviewerAssignment $countFinanceExistingAssignments[0];$countFinanceReviewer.AssignmentId=$countFinanceExistingAssignments[0].Id}
        }
        $countFinanceReviewerInvariantBefore=Get-CountFinanceReviewerInvariant
    }
    # Exercise all preservation queries in preview too, before any backup or API write.
    $countFinanceBaselineBefore=Get-CountFinanceBaseline
    $countFinanceInvariantBefore=Get-CountFinanceSettingsInvariant
    [pscustomobject]@{Database=$Database;Tenant=$countFinanceTenant;PlannedAccounts=@($countFinanceSpecs | ForEach-Object { [pscustomobject]@{Code=$_.Code;Name=$_.Name;AlreadyExists=[bool]$_.Id}});Reviewers=@($countFinanceReviewers | ForEach-Object {[pscustomobject]@{Username=$_.Username;Role=$_.Role;SecurityRoleExists=$_.HasSecurityRole;AssignmentExists=[bool]$_.AssignmentId;Warehouse='Project Demo Warehouse';Locations='All'}});Applied=$false} | ConvertTo-Json -Depth 5
    if(-not $Apply){return}
    if(-not(Test-Path -LiteralPath $countFinanceExe) -or -not(Test-Path -LiteralPath (Join-Path $countFinanceRuntime 'e2e-runtime-ready.txt'))){throw 'The verified updated maintenance API runtime is not ready.'}
    if(Get-NetTCPConnection -LocalPort 5003 -State Listen -ErrorAction SilentlyContinue){throw 'Port 5003 is occupied; no existing process will be replaced.'}
    if(-not(Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort 5519 -State Listen -ErrorAction SilentlyContinue)){throw 'The local outbound blocker on port 5519 is unavailable.'}
    $countFinancePhase='Verified pre-change backup'
    $countFinanceBackupDirectory='C:/Program Files/Microsoft SQL Server/MSSQL15.SQL2017/MSSQL/Backup'
    if(-not(Test-Path -LiteralPath $countFinanceBackupDirectory -PathType Container)){throw 'Expected SQL Server backup directory is unavailable.'}
    $countFinanceBackupPath=Join-Path $countFinanceBackupDirectory ($Database+'_before_count_finance_'+[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss_fff')+'.bak')
    [void](Invoke-CountFinanceSql "BACKUP DATABASE [$Database] TO DISK=@backup WITH COPY_ONLY,CHECKSUM,COMPRESSION; RESTORE VERIFYONLY FROM DISK=@backup WITH CHECKSUM;" @{backup=$countFinanceBackupPath})
    Write-Output "Verified COPY_ONLY backup: $countFinanceBackupPath"
    $countFinancePassword=Read-Host 'Existing admin password (not saved)' -AsSecureString
    $countFinancePhase='Loading isolated runtime configuration'
    $countFinanceSecretsRaw=@(dotnet user-secrets list --project (Join-Path $countFinanceRepo 'src/ErpSystem.Api/ErpSystem.Api.csproj') --json)
    if($LASTEXITCODE -ne 0){throw 'Could not load existing local configuration.'}
    $countFinanceJsonStart=[Array]::FindIndex($countFinanceSecretsRaw,[Predicate[string]]{param($line)$line.Trim() -eq '{'})
    $countFinanceJsonEnd=[Array]::FindLastIndex($countFinanceSecretsRaw,[Predicate[string]]{param($line)$line.Trim() -eq '}'})
    if($countFinanceJsonStart -lt 0 -or $countFinanceJsonEnd -le $countFinanceJsonStart){throw 'Existing local configuration format is unavailable.'}
    $countFinanceSecrets=(($countFinanceSecretsRaw[$countFinanceJsonStart..$countFinanceJsonEnd]) -join "`n") | ConvertFrom-Json
    $countFinanceBuilder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$countFinanceSecrets.'ConnectionStrings:DefaultConnection')
    if($countFinanceBuilder.InitialCatalog -ne 'RhemaERP' -or $countFinanceBuilder.DataSource -notin @('RHEMA-MICHAEL\SQL2017','.\SQL2017','localhost\SQL2017')){throw 'Existing runtime database configuration is outside the exact local allowlist.'}
    # PowerShell adapts this IDictionary setter by key: use the SQL keyword with
    # its space, not InitialCatalog (which is rejected as an unknown keyword).
    $countFinanceBuilder['Initial Catalog']=$Database
    if($countFinanceBuilder.InitialCatalog -ne $Database){throw 'The isolated API database target was not retained.'}
    $countFinanceBase=Get-Content -LiteralPath (Join-Path $countFinanceRepo 'src/ErpSystem.Api/appsettings.json') -Raw | ConvertFrom-Json -AsHashtable
    $countFinanceEncryption=[string]$countFinanceSecrets.'Security:EncryptionKey'
    if(-not $countFinanceEncryption){$countFinanceEncryption=[string]$countFinanceBase.Security.EncryptionKey}
    if(-not $countFinanceEncryption){throw 'Existing encryption configuration is unavailable.'}
    $countFinanceEnvironment=@{
        ASPNETCORE_ENVIRONMENT='CountFinanceMaintenance';DOTNET_ENVIRONMENT='CountFinanceMaintenance';ASPNETCORE_URLS='http://127.0.0.1:5003';
        SkipStartupInitialization='true';BackgroundServices__Enabled='false';Database__Provider='SqlServer';ConnectionStrings__DefaultConnection=$countFinanceBuilder.ConnectionString;ConnectionStrings__Redis='';
        Security__EncryptionKey=$countFinanceEncryption;JwtSettings__SecretKey=([Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N'));
        JwtSettings__PortalSecretKey=([Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N'));JwtSettings__Issuer='RhemaERP.Local.CountFinanceMaintenance';
        JwtSettings__Audience='RhemaERP.Local.CountFinanceMaintenance.Client';JwtSettings__PortalAudience='RhemaERP.Local.CountFinanceMaintenance.Portal';JwtSettings__ExpiryInMinutes='15';
        CandidatePortal__PortalUrl='http://127.0.0.1:3002';FrontendUrl='http://127.0.0.1:3002';ALLOWED_ORIGINS='http://127.0.0.1:3002';Application__EnvironmentName='CountFinanceMaintenance';
        Sms__Twilio__Enabled='false';Sms__GhanaGateway__Enabled='false';Serilog__MinimumLevel__Default='Warning';Logging__LogLevel__Default='Warning';
        HTTP_PROXY='http://127.0.0.1:5519';HTTPS_PROXY='http://127.0.0.1:5519';ALL_PROXY='http://127.0.0.1:5519';NO_PROXY=''
    }
    $countFinancePrior=@{}
    foreach($countFinanceEntry in $countFinanceEnvironment.GetEnumerator()){$countFinancePrior[$countFinanceEntry.Key]=[Environment]::GetEnvironmentVariable($countFinanceEntry.Key,'Process');[Environment]::SetEnvironmentVariable($countFinanceEntry.Key,[string]$countFinanceEntry.Value,'Process')}
    try {
        $countFinancePhase='Starting isolated maintenance API'
        $countFinanceLogRoot=Split-Path -Parent $countFinanceRuntime
        $countFinanceProcess=Start-Process -FilePath $countFinanceExe -ArgumentList '--urls','http://127.0.0.1:5003','--SkipStartupInitialization','true','--BackgroundServices:Enabled','false' -WorkingDirectory $countFinanceRuntime -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $countFinanceLogRoot ($Database+'-finance-api.stdout.log')) -RedirectStandardError (Join-Path $countFinanceLogRoot ($Database+'-finance-api.stderr.log'))
    } finally {foreach($countFinanceEntry in $countFinancePrior.GetEnumerator()){[Environment]::SetEnvironmentVariable($countFinanceEntry.Key,$countFinanceEntry.Value,'Process')}}
    $countFinanceHandler=[System.Net.Http.HttpClientHandler]::new();$countFinanceHandler.UseProxy=$false
    # A fresh maintenance API builds the large EF model on its first login.
    $countFinanceClient=[System.Net.Http.HttpClient]::new($countFinanceHandler);$countFinanceClient.Timeout=[TimeSpan]::FromSeconds(300)
    $countFinanceReady=$false
    for($countFinanceAttempt=0;$countFinanceAttempt -lt 30;$countFinanceAttempt++) {
        if($countFinanceProcess.HasExited){throw 'Temporary API stopped before readiness.'}
        try {$countFinanceHealth=$countFinanceClient.GetAsync('http://127.0.0.1:5003/health/live').GetAwaiter().GetResult();$countFinanceReady=$countFinanceHealth.IsSuccessStatusCode;$countFinanceHealth.Dispose();if($countFinanceReady){break}}catch{}
        Start-Sleep -Milliseconds 500
    }
    if(-not $countFinanceReady){throw 'Temporary API readiness failed.'}
    $countFinanceListeners=@(Get-NetTCPConnection -LocalPort 5003 -State Listen -ErrorAction Stop)
    if($countFinanceListeners.Count -ne 1 -or $countFinanceListeners[0].OwningProcess -ne $countFinanceProcess.Id){throw 'Temporary API port ownership was not verified; authentication was not attempted.'}
    $countFinancePointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($countFinancePassword)
    try {$countFinancePhase='Authenticating supplied administrator';$countFinanceLogin=Invoke-CountFinanceApi 'POST' '/api/Auth/login' @{username='admin';password=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($countFinancePointer);tenantCode='DEFAULT'}}
    finally {[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($countFinancePointer)}
    if(-not $countFinanceLogin.token -or $countFinanceLogin.user.currentTenantId -ne $countFinanceTenant){throw 'Authentication did not return the exact approved tenant.'}
    $countFinanceClient.DefaultRequestHeaders.Authorization=[System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',[string]$countFinanceLogin.token)
    $countFinanceApiSettings=Invoke-CountFinanceApi 'GET' '/api/finance/settings'
    if([string]$countFinanceApiSettings.id -ne [string]$countFinanceSettings[0].Id){throw 'API settings row differs from the verified SQL target database.'}
    $countFinancePhase='Creating exact dedicated accounts through central COA API'
    foreach($countFinanceSpec in $countFinanceSpecs) {
        if($countFinanceSpec.Id){continue}
        $countFinancePayload=@{
            accountCode=$countFinanceSpec.Code;accountNumber=$countFinanceSpec.Code;accountName=$countFinanceSpec.Name;accountType=$countFinanceSpec.Type;accountCategory=$countFinanceSpec.Category;
            currencyCode='GHS';isSegmented=$true;isMultiCurrency=$false;isControlAccount=$false;isPostingAllowed=$true;isIFRSClassified=$true;isBaseFrameworkClassified=$true;isLocalFrameworkClassified=$true;
            segmentValues=@(
                @{segmentStructureId=$countFinanceSegments[0].Id;segmentPosition=1;segmentValue='000';segmentLookupValueId=$countFinanceDepartment[0].Id},
                @{segmentStructureId=$countFinanceSegments[1].Id;segmentPosition=2;segmentValue=$countFinanceSpec.Natural;segmentLookupValueId=$null},
                @{segmentStructureId=$countFinanceSegments[2].Id;segmentPosition=3;segmentValue='0000';segmentLookupValueId=$countFinanceProject[0].Id}
            )
        }
        $countFinanceCreated=Invoke-CountFinanceApi 'POST' '/api/finance/accounts' $countFinancePayload
        if(-not $countFinanceCreated.id -or $countFinanceCreated.accountCode -ne $countFinanceSpec.Code){throw 'Central account creation returned an unexpected identity.'}
        $countFinanceSpec.Id=[Guid]$countFinanceCreated.id
    }
    $countFinancePhase='Saving only loss and gain mappings through central Finance Settings API'
    $countFinanceUpdated=Invoke-CountFinanceApi 'PUT' '/api/finance/settings' @{writeOffExpenseAccountId=$countFinanceSpecs[0].Id;writeOffRecoveryAccountId=$countFinanceSpecs[1].Id}
    if([string]$countFinanceUpdated.writeOffExpenseAccountId -ne [string]$countFinanceSpecs[0].Id -or [string]$countFinanceUpdated.writeOffRecoveryAccountId -ne [string]$countFinanceSpecs[1].Id){throw 'Updated API did not retain both requested mappings. No SQL fallback was attempted.'}
    if($ConfigureReviewers) {
        $countFinancePhase='Configuring the two explicitly approved independent reviewers through central APIs'
        foreach($countFinanceReviewer in $countFinanceReviewers) {
            $countFinanceProfile=Invoke-CountFinanceApi 'GET' ('/api/User/'+$countFinanceReviewer.UserId)
            if([string]$countFinanceProfile.id -ne [string]$countFinanceReviewer.UserId -or $countFinanceProfile.username -ne $countFinanceReviewer.Username -or -not $countFinanceProfile.isActive){throw 'The reviewer API profile differs from the verified SQL identity.'}
            $countFinanceProfileBefore=$countFinanceProfile | Select-Object id,username,email,phoneNumber,firstName,lastName,isActive,employeeId,tenantId | ConvertTo-Json -Compress
            $countFinanceExpectedRoles=@(@($countFinanceProfile.roles)+@($countFinanceReviewer.Role) | Sort-Object -Unique)
            if($countFinanceProfile.roles -notcontains $countFinanceReviewer.Role) {
                [void](Invoke-CountFinanceApi 'PUT' ('/api/User/'+$countFinanceReviewer.UserId) @{
                    username=$countFinanceProfile.username;email=$countFinanceProfile.email;phoneNumber=$countFinanceProfile.phoneNumber;
                    firstName=$countFinanceProfile.firstName;lastName=$countFinanceProfile.lastName;isActive=$countFinanceProfile.isActive;roles=$countFinanceExpectedRoles
                })
            }
            $countFinanceProfileAfter=Invoke-CountFinanceApi 'GET' ('/api/User/'+$countFinanceReviewer.UserId)
            if(($countFinanceProfileAfter | Select-Object id,username,email,phoneNumber,firstName,lastName,isActive,employeeId,tenantId | ConvertTo-Json -Compress) -ne $countFinanceProfileBefore -or (@($countFinanceProfileAfter.roles | Sort-Object -Unique) -join '|') -ne ($countFinanceExpectedRoles -join '|')){throw 'Reviewer profile or preserved-role verification failed.'}
            if(-not $countFinanceReviewer.AssignmentId) {
                $countFinanceNewAssignment=Invoke-CountFinanceApi 'POST' '/api/procurement/access-controls/assignments' @{
                    userId=$countFinanceReviewer.UserId;roleName=$countFinanceReviewer.Role;
                    warehouseScopeMode=2;warehouseIds=@($countFinanceWarehouse.Id);locationScopeMode=1;locationIds=@();
                    effectiveFrom=[DateTime]::UtcNow.ToString('o');effectiveTo=$null;isActive=$true;
                    reason='User-authorized physical-count reviewer setup for Project Demo Warehouse, all locations. Existing security roles and expired responsibility history retained. No count approval or posting performed by setup.'
                }
                if(-not $countFinanceNewAssignment.id -or [string]$countFinanceNewAssignment.userId -ne [string]$countFinanceReviewer.UserId -or $countFinanceNewAssignment.roleName -ne $countFinanceReviewer.Role){throw 'The new responsibility assignment returned an unexpected identity.'}
                $countFinanceReviewer.AssignmentId=[Guid]$countFinanceNewAssignment.id
                $countFinanceNewAssignmentIds += $countFinanceReviewer.AssignmentId
            }
            $countFinanceVerifiedAssignments=@(Get-CountFinanceActiveReviewerAssignments $countFinanceReviewer)
            if($countFinanceVerifiedAssignments.Count -ne 1 -or $countFinanceVerifiedAssignments[0].Id -ne $countFinanceReviewer.AssignmentId){throw 'Exact active reviewer assignment SQL verification failed.'}
            Assert-CountFinanceReviewerAssignment $countFinanceVerifiedAssignments[0]
            if(@(Invoke-CountFinanceSql 'SELECT UserId FROM dbo.UserRoles WHERE UserId=@user AND RoleId=@role' @{user=$countFinanceReviewer.UserId;role=$countFinanceReviewer.RoleId}).Count -ne 1){throw 'Required reviewer security role was not saved.'}
        }
        if($countFinanceReviewerInvariantBefore -ne (Get-CountFinanceReviewerInvariant)){throw 'An unrelated security role, reviewer profile, or historical responsibility assignment changed.'}
    }
    $countFinancePhase='Verifying mappings and preserved operational baselines'
    $countFinanceAfter=@(Invoke-CountFinanceSql 'SELECT WriteOffExpenseAccountId,WriteOffRecoveryAccountId FROM dbo.FinanceSettings WHERE TenantId=@tenant AND IsDeleted=0' @{tenant=[Guid]$countFinanceTenant})[0]
    if($countFinanceAfter.WriteOffExpenseAccountId -ne $countFinanceSpecs[0].Id -or $countFinanceAfter.WriteOffRecoveryAccountId -ne $countFinanceSpecs[1].Id){throw 'SQL mapping verification failed.'}
    foreach($countFinanceSpec in $countFinanceSpecs) {
        $countFinanceVerified=@(Invoke-CountFinanceSql 'SELECT Id FROM dbo.Accounts WHERE Id=@id AND TenantId=@tenant AND AccountCode=@code AND AccountNumber=@code AND AccountName=@name AND AccountType=@type AND CurrencyCode=N''GHS'' AND Status=1 AND AllowDirectPosting=1 AND IsControlAccount=0 AND IsDeleted=0' @{id=$countFinanceSpec.Id;tenant=[Guid]$countFinanceTenant;code=$countFinanceSpec.Code;name=$countFinanceSpec.Name;type=$countFinanceSpec.TypeNumber})
        if($countFinanceVerified.Count -ne 1){throw 'Dedicated account SQL verification failed.'}
    }
    if($countFinanceInvariantBefore -ne (Get-CountFinanceSettingsInvariant)){throw 'An unrelated Finance setting changed. Inspect before continuing.'}
    if($countFinanceBaselineBefore -ne (Get-CountFinanceBaseline)){throw 'A count, movement, GL, inventory, role, responsibility or migration baseline changed. Inspect before continuing.'}
    [pscustomobject]@{Database=$Database;LossAccount=$countFinanceSpecs[0].Code;GainAccount=$countFinanceSpecs[1].Code;LossAccountId=$countFinanceSpecs[0].Id;GainAccountId=$countFinanceSpecs[1].Id;VerifiedBackup=$countFinanceBackupPath;OnlyTwoFinanceMappingsChanged=$true;OperationalBaselinesUnchanged=$true;UnrelatedRolesAndAssignmentHistoryUnchanged=$true;ReviewersConfigured=[bool]$ConfigureReviewers;ReviewerSignInRequired=[bool]$ConfigureReviewers;Applied=$true} | ConvertTo-Json -Compress
} catch {
    $countFinanceErrorParameter=[string]$_.Exception.ParamName
    if(-not $countFinanceErrorParameter -and $_.Exception.InnerException){$countFinanceErrorParameter=[string]$_.Exception.InnerException.ParamName}
    if($countFinanceErrorParameter -notmatch '^[A-Za-z][A-Za-z0-9_. ]{0,64}$'){$countFinanceErrorParameter='not available'}
    $countFinanceSafeDiagnostics=' ScriptLineNumber='+$_.InvocationInfo.ScriptLineNumber+'; ExceptionType='+$_.Exception.GetType().Name+'; ParamName='+$countFinanceErrorParameter+'.'
    $countFinanceSafeError=if($_.Exception.Message -like 'Central API *'){$_.Exception.Message+$countFinanceSafeDiagnostics}else{$countFinancePhase+' failed.'+$countFinanceSafeDiagnostics+' Sensitive diagnostics suppressed; no automatic rollback of already completed central API writes.'}
    Write-Output $countFinanceSafeError
    exit 1
} finally {
    if($countFinanceClient){$countFinanceClient.Dispose()}
    if($countFinanceConnection){$countFinanceConnection.Dispose()}
    if($countFinancePassword){$countFinancePassword.Dispose()}
    $countFinanceLogin=$null;$countFinanceSecrets=$null;$countFinanceSecretsRaw=$null;$countFinanceEncryption=$null;$countFinanceEnvironment=$null;$countFinanceBuilder=$null
    if($countFinanceProcess -and -not $countFinanceProcess.HasExited) {
        $countFinanceCurrent=Get-Process -Id $countFinanceProcess.Id -ErrorAction SilentlyContinue
        if($countFinanceCurrent -and $countFinanceCurrent.Path -eq $countFinanceExe -and $countFinanceCurrent.StartTime -eq $countFinanceProcess.StartTime){Stop-Process -Id $countFinanceProcess.Id -ErrorAction Stop;[void]$countFinanceCurrent.WaitForExit(10000)}
        else {Write-Output 'Temporary process identity changed; manual inspection is required.'}
    }
    if($Apply){Write-Output ('TemporaryPort5003Stopped='+(-not [bool](Get-NetTCPConnection -LocalPort 5003 -State Listen -ErrorAction SilentlyContinue)))}
}
