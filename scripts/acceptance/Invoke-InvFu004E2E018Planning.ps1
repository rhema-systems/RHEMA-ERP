[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5100')

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$tenantId = [Guid]'00000000-0000-0000-0000-000000000001'
$planId = [Guid]'3bbb2346-84cd-42f2-a88d-513f3ff7bfb1'
$planItemId = [Guid]'b33f0115-4221-4b63-b1e6-49d8faf8bac4'
$inventoryItemId = [Guid]'00000008-0000-0000-0000-000000000003'
$departmentId = [Guid]'11111111-1111-4111-8111-111111111111'
$supplierId = [Guid]'0da4dee7-20f2-4b5c-8734-7351eb3beece'

$actors = @(
    @{ Id=[Guid]'58cafd8b-42ce-4f67-0dbb-08de862e82ee'; UserName='admin' },
    @{ Id=[Guid]'062fe3a7-4c95-4e6e-8480-f0a056e797c0'; UserName='proc-plan-submitter' },
    @{ Id=[Guid]'088b60a2-cdb3-4c78-b553-202a02e70b65'; UserName='proc-plan-dept-approver' },
    @{ Id=[Guid]'45eacad1-fff9-40f4-91fb-a0e72e2fa7f7'; UserName='proc-plan-procurement-approver' },
    @{ Id=[Guid]'3285bf61-ca0f-471f-800b-db23ba3585c4'; UserName='proc-plan-finance-approver' },
    @{ Id=[Guid]'e6457120-5070-440b-9143-8b3b02896c82'; UserName='proc-plan-final-approver' }
)

function Add-DbParameter {
    param([System.Data.SqlClient.SqlCommand]$Command, [string]$Name, $Value)
    $parameter = $Command.Parameters.AddWithValue($Name, $(if ($null -eq $Value) { [DBNull]::Value } else { $Value }))
    return $parameter
}

function Invoke-DbTable {
    param([System.Data.SqlClient.SqlConnection]$Connection, [string]$Sql, [hashtable]$Parameters = @{})
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 60
    foreach ($entry in $Parameters.GetEnumerator()) { $null = Add-DbParameter $command "@$($entry.Key)" $entry.Value }
    $table = [System.Data.DataTable]::new()
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($command)
    $null = $adapter.Fill($table)
    Write-Output -NoEnumerate $table
}

function Invoke-DbScalar {
    param([System.Data.SqlClient.SqlConnection]$Connection, [string]$Sql, [hashtable]$Parameters = @{})
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 60
    foreach ($entry in $Parameters.GetEnumerator()) { $null = Add-DbParameter $command "@$($entry.Key)" $entry.Value }
    return $command.ExecuteScalar()
}

function Invoke-DbNonQuery {
    param([System.Data.SqlClient.SqlConnection]$Connection, [string]$Sql, [hashtable]$Parameters = @{})
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    foreach ($entry in $Parameters.GetEnumerator()) { $null = Add-DbParameter $command "@$($entry.Key)" $entry.Value }
    return $command.ExecuteNonQuery()
}

function Invoke-JsonApi {
    param([string]$Method, [string]$Path, [string]$Token, $Body, [int[]]$ExpectedStatus = @(200))
    $headers = @{}
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    $headers['X-Correlation-ID'] = "INV-FU-004-E2E018-$([Guid]::NewGuid().ToString('N'))"
    $parameters = @{ Uri="$ApiBaseUrl$Path"; Method=$Method; Headers=$headers; SkipHttpErrorCheck=$true; TimeoutSec=120 }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 30 -Compress
    }
    $response = Invoke-WebRequest @parameters
    $content = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { [string]$response.Content }
    if ($response.StatusCode -notin $ExpectedStatus) {
        throw "$Method $Path returned $($response.StatusCode): $content"
    }
    if ([string]::IsNullOrWhiteSpace($content)) { return $null }
    return $content | ConvertFrom-Json
}

function Login-Actor([string]$UserName, [string]$Password) {
    $value = Invoke-JsonApi POST '/api/auth/login' '' @{ username=$UserName; password=$Password; rememberMe=$false }
    if ([string]::IsNullOrWhiteSpace($value.token)) { throw "Login did not issue a token for $UserName." }
    return [string]$value.token
}

function Test-ApiStatus {
    param($Value, [string]$Name, [int]$NumericValue)
    if ($null -eq $Value) { return $false }
    $text = [string]$Value
    return $text.Equals($Name, [StringComparison]::OrdinalIgnoreCase) -or $text -eq [string]$NumericValue
}

$secretRows = dotnet user-secrets list --project $apiProject --json | ConvertFrom-Json
$secretObject = $secretRows | Where-Object {
    $_ -and $_.PSObject.Properties.Name -contains 'ConnectionStrings:DefaultConnection'
} | Select-Object -First 1
if (-not $secretObject) { throw 'The configured API database connection was not found in user secrets.' }

Add-Type -AssemblyName System.Data
$connection = [System.Data.SqlClient.SqlConnection]::new([string]$secretObject.'ConnectionStrings:DefaultConnection')
$connection.Open()

$identityAssembly = Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App' -Recurse -Filter Microsoft.Extensions.Identity.Core.dll |
    Where-Object { $_.Directory.Name -like '8.*' } |
    Sort-Object { [version]$_.Directory.Name } -Descending | Select-Object -First 1
if (-not $identityAssembly) { throw 'Microsoft.Extensions.Identity.Core.dll was not found.' }
try { Add-Type -Path $identityAssembly.FullName -ErrorAction Stop } catch [System.Management.Automation.RuntimeException] { }
$passwordHasher = [Microsoft.AspNetCore.Identity.PasswordHasher[object]]::new()
$temporaryPassword = "Inv!$([Guid]::NewGuid().ToString('N'))aA7"
$temporaryHash = $passwordHasher.HashPassword([object]::new(), $temporaryPassword)
$passwordBackups = @{}
$addedRoleMemberships = [System.Collections.Generic.List[object]]::new()

try {
    foreach ($actor in $actors) {
        $row = Invoke-DbTable $connection @'
SELECT PasswordHash,AccessFailedCount,LockoutEnd FROM Users WHERE Id=@id AND TenantId=@tenantId AND IsActive=1;
'@ @{ id=$actor.Id; tenantId=$tenantId }
        if ($row.Rows.Count -ne 1) { throw "Acceptance actor $($actor.UserName) is unavailable." }
        $passwordBackups[$actor.Id] = @{
            PasswordHash=if ($row.Rows[0].IsNull('PasswordHash')) { $null } else { [string]$row.Rows[0].PasswordHash }
            AccessFailedCount=[int]$row.Rows[0].AccessFailedCount
            LockoutEnd=if ($row.Rows[0].IsNull('LockoutEnd')) { $null } else { $row.Rows[0].LockoutEnd }
        }
        $null = Invoke-DbNonQuery $connection @'
UPDATE Users SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL WHERE Id=@id;
'@ @{ id=$actor.Id; hash=$temporaryHash }
    }

    foreach ($grant in @(
        @{ UserId=[Guid]'088b60a2-cdb3-4c78-b553-202a02e70b65'; RoleName='Manager' },
        @{ UserId=[Guid]'45eacad1-fff9-40f4-91fb-a0e72e2fa7f7'; RoleName='TDC_HEAD_OF_PROCUREMENT' }
    )) {
        $roleId = Invoke-DbScalar $connection 'SELECT Id FROM AspNetRoles WHERE Name=@name;' @{ name=$grant.RoleName }
        if (-not $roleId) { throw "Required role $($grant.RoleName) is unavailable." }
        $exists = [int](Invoke-DbScalar $connection 'SELECT COUNT(1) FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{
            userId=$grant.UserId; roleId=[Guid]$roleId
        })
        if ($exists -eq 0) {
            $null = Invoke-DbNonQuery $connection 'INSERT INTO UserRoles(UserId,RoleId) VALUES(@userId,@roleId);' @{
                userId=$grant.UserId; roleId=[Guid]$roleId
            }
            $addedRoleMemberships.Add([pscustomobject]@{ UserId=$grant.UserId; RoleId=[Guid]$roleId })
        }
    }

    $tokens = @{}
    foreach ($actor in $actors) { $tokens[$actor.UserName] = Login-Actor $actor.UserName $temporaryPassword }
    $adminToken = $tokens['admin']
    $finalToken = $tokens['proc-plan-final-approver']

    $budgetRow = Invoke-DbTable $connection @'
SELECT TOP (1) Id,Status FROM ProcurementBudgets
WHERE TenantId=@tenantId AND IsDeleted=0 AND Title='INV-REQ-FU-004 E2E-018 Air Filter Budget'
ORDER BY CreatedAt DESC;
'@ @{ tenantId=$tenantId }
    if ($budgetRow.Rows.Count -eq 0) {
        $budget = Invoke-JsonApi POST '/api/procurement/ProcurementBudgets' $adminToken @{
            title='INV-REQ-FU-004 E2E-018 Air Filter Budget'
            description='Retained governed budget for receipt-to-stock, valuation, GL and AP acceptance.'
            departmentId=$departmentId
            fiscalYear=2026
            allocatedAmount=10000
            currency='GHS'
            controlLevel='Strict'
            warningThresholdPercent=80
            effectiveDate=(Get-Date).ToUniversalTime().Date
            expiryDate=[DateTime]'2026-12-31T23:59:59Z'
            notes='INV-REQ-FU-004 E2E-018 runtime evidence'
            allocations=@(@{ categoryName='Goods'; categoryDescription='Air-filter replenishment'; allocatedAmount=10000; notes='Governed Goods allocation' })
        } @(201)
        $budgetId = [Guid]$budget.id
        $budgetStatus = [string]$budget.status
    } else {
        $budgetId = [Guid]$budgetRow.Rows[0].Id
        $budget = Invoke-JsonApi GET "/api/procurement/ProcurementBudgets/$budgetId" $adminToken $null
        $budgetStatus = [string]$budget.status
    }
    if (Test-ApiStatus $budgetStatus 'Draft' 0) {
        $budget = Invoke-JsonApi POST "/api/procurement/ProcurementBudgets/$budgetId/approve" $finalToken $null
        $budgetStatus = [string]$budget.status
    }
    if (-not (Test-ApiStatus $budgetStatus 'Approved' 1) -and -not (Test-ApiStatus $budgetStatus 'Active' 2)) {
        throw "Budget is $budgetStatus, not Approved/Active."
    }

    $plan = Invoke-JsonApi GET "/api/procurement/ProcurementPlans/$planId" $adminToken $null
    if ($plan.status -eq 'Draft') {
        $plan = Invoke-JsonApi POST "/api/procurement/ProcurementPlans/$planId/submit" $tokens['proc-plan-submitter'] @{
            reviewerId=$null; comments='INV-REQ-FU-004 maker submission'
        }
    }
    foreach ($approval in @(
        @{ Actor='proc-plan-dept-approver'; Comment='Department budget and need confirmed.' },
        @{ Actor='proc-plan-procurement-approver'; Comment='Procurement method and specification path confirmed.' },
        @{ Actor='proc-plan-finance-approver'; Comment='Finance budget availability confirmed.' },
        @{ Actor='proc-plan-final-approver'; Comment='Final independent plan approval.' }
    )) {
        $plan = Invoke-JsonApi GET "/api/procurement/ProcurementPlans/$planId" $adminToken $null
        if ($plan.status -eq 'Approved' -or $plan.status -eq 'Active') { break }
        $plan = Invoke-JsonApi POST "/api/procurement/ProcurementPlans/$planId/approve" $tokens[$approval.Actor] @{
            isApproved=$true; approvedBudget=2450; comments=$approval.Comment
            autoGenerateSchedules=$false; budgetId=$budgetId; autoLinkBudget=$true
        }
    }
    $plan = Invoke-JsonApi GET "/api/procurement/ProcurementPlans/$planId" $adminToken $null
    if ($plan.status -eq 'Approved') {
        $plan = Invoke-JsonApi POST "/api/procurement/ProcurementPlans/$planId/publish" $finalToken @{
            comments='Published for INV-REQ-FU-004 E2E-018 execution.'
        }
    }
    if ($plan.status -ne 'Active') { throw "Procurement plan is $($plan.status), not Active." }

    $appRow = Invoke-DbTable $connection @'
SELECT TOP (1) Id,Status FROM ProcurementAppSubmissions
WHERE TenantId=@tenantId AND ProcurementPlanId=@planId AND IsDeleted=0
ORDER BY AttemptNumber DESC,CreatedAt DESC;
'@ @{ tenantId=$tenantId; planId=$planId }
    if ($appRow.Rows.Count -eq 0) {
        $checksum = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
            [Text.Encoding]::UTF8.GetBytes('INV-REQ-FU-004 E2E-018 APP export'))).ToLowerInvariant()
        $app = Invoke-JsonApi POST '/api/procurement/app-submissions/exports' $adminToken @{
            procurementPlanId=$planId; exportFileName='inv-fu-004-e2e018-app.json'; exportFormat='JSON'
            exportTemplateVersion='tdc.app.v1'; exportChecksumSha256=$checksum
            notes='Retained APP exchange evidence'; evidence=@(@{
                referenceKind='ExternalReference'; reference='PPA-APP-E2E018-EXPORT'; label='APP export register'; requirementKey='DEC-009'
            })
        } @(201)
    } else {
        $app = Invoke-JsonApi GET "/api/procurement/app-submissions/$([Guid]$appRow.Rows[0].Id)" $adminToken $null
    }
    if (Test-ApiStatus $app.status 'Exported' 0) {
        $app = Invoke-JsonApi POST "/api/procurement/app-submissions/$($app.id)/submit" $adminToken @{
            externalSubmissionReference='PPA-APP-E2E018-SUBMITTED'; submittedAtUtc=(Get-Date).ToUniversalTime()
            notes='Manual APP exchange for acceptance'; rowVersion=$app.rowVersion; evidence=@(@{
                referenceKind='ExternalReference'; reference='PPA-APP-E2E018-SUBMITTED'; label='APP submission receipt'; requirementKey='DEC-009'
            })
        }
    }
    if (Test-ApiStatus $app.status 'Submitted' 1) {
        $app = Invoke-JsonApi POST "/api/procurement/app-submissions/$($app.id)/acknowledge" $finalToken @{
            acknowledgementReference='PPA-ACK-INV-FU-004-E2E018'; acknowledgedAtUtc=(Get-Date).ToUniversalTime()
            notes='Independent acknowledgement recorded'; rowVersion=$app.rowVersion; evidence=@(@{
                referenceKind='ExternalReference'; reference='PPA-ACK-INV-FU-004-E2E018'; label='PPA acknowledgement'; requirementKey='DEC-009'
            })
        }
    }
    if (-not (Test-ApiStatus $app.status 'Acknowledged' 2)) { throw "APP attempt is $($app.status), not Acknowledged." }

    $specRow = Invoke-DbTable $connection @'
SELECT TOP (1) Id,Status FROM ProcurementSpecificationTemplates
WHERE TenantId=@tenantId AND IsDeleted=0 AND TemplateCode='INV-E2E018-GOODS-SPEC'
ORDER BY Version DESC;
'@ @{ tenantId=$tenantId }
    if ($specRow.Rows.Count -eq 0) {
        $spec = Invoke-JsonApi POST '/api/procurement/specification-templates' $adminToken @{
            templateCode='INV-E2E018-GOODS-SPEC'; name='Air Filter Goods Specification'; description='Governed acceptance specification for Air Filter replenishment.'
            kind='Goods'; isDefault=$false; effectiveFromUtc=(Get-Date).ToUniversalTime().AddMinutes(-5); effectiveToUtc=[DateTime]'2026-12-31T23:59:59Z'
            changeSummary='Initial E2E-018 governed Goods specification'; purpose='Replenish approved heavy-duty air-filter inventory.'
            functionalAndPerformanceRequirements='Heavy-duty air filter compatible with the approved fleet and equipment catalogue.'
            processAndMaterialsRequirements='New OEM-equivalent filter media and casing; no refurbished components.'
            dimensionsAndMarkingRequirements='Each unit must carry manufacturer, model, lot and traceable packaging marks.'
            testingAndInspectionRequirements='Visual, dimensional and fit verification before inventory acceptance.'
            applicableStandards='Manufacturer specification and TDC Stores inspection procedure.'
            deliverables='One hundred filters, waybill, VAT invoice copy and inspection evidence.'
            acceptanceCriteria='Quantity, traceable lot, physical condition and fit verification must pass.'
            workflowDefinitionId=$null
        } @(201)
    } else {
        $spec = Invoke-JsonApi GET "/api/procurement/specification-templates/$([Guid]$specRow.Rows[0].Id)" $adminToken $null
    }
    if (Test-ApiStatus $spec.status 'Draft' 0) {
        $spec = Invoke-JsonApi POST "/api/procurement/specification-templates/$($spec.id)/submit" $adminToken @{
            rowVersion=$spec.rowVersion; comment='Submitted with approved external technical specification evidence.'; evidence=@(@{
                referenceKind='ExternalReference'; reference='INV-E2E018-SPEC-EVIDENCE'; label='Approved technical specification'; requirementKey='DEC-006'
            })
        }
    }
    if (Test-ApiStatus $spec.status 'PendingApproval' 1) {
        $spec = Invoke-JsonApi POST "/api/procurement/specification-templates/$($spec.id)/publish" $finalToken @{
            rowVersion=$spec.rowVersion; comment='Independent publication for E2E-018.'; evidence=@(@{
                referenceKind='ExternalReference'; reference='INV-E2E018-SPEC-APPROVAL'; label='Specification approval'; requirementKey='DEC-006'
            })
        }
    }
    if (-not (Test-ApiStatus $spec.status 'Published' 2)) { throw "Specification is $($spec.status), not Published." }

    $prRow = Invoke-DbTable $connection @'
SELECT TOP (1) Id,Status FROM PurchaseRequisitions
WHERE TenantId=@tenantId AND IsDeleted=0 AND Notes LIKE '%INV-REQ-FU-004 E2E-018 operational retained chain%'
ORDER BY CreatedAt DESC;
'@ @{ tenantId=$tenantId }
    if ($prRow.Rows.Count -eq 0) {
        $pr = Invoke-JsonApi POST '/api/PurchaseRequisitions' $adminToken @{
            requiredDate=(Get-Date).ToUniversalTime().Date.AddDays(14); priority='Normal'; department='Human Resources'
            costCenter='HR-STORES'; justification='Approved replenishment of heavy-duty air filters.'
            notes='INV-REQ-FU-004 E2E-018 operational retained chain'; requestedById=$actors[0].Id
            linkage=@{
                sourcePlanItemId=$planItemId; budgetId=$budgetId; procurementCategory='Goods'; costCenter='HR-STORES'
                requisitionType='StockReplenishment'; specificationTemplateId=$spec.id
            }
            items=@(@{
                inventoryItemId=$inventoryItemId; itemDescription='Air Filter - Heavy Duty'; quantity=10
                unitOfMeasure='EA'; estimatedUnitPrice=24.50; requiredDate=(Get-Date).ToUniversalTime().Date.AddDays(14)
                preferredSupplierId=$supplierId; notes='Receive only after waybill and inspection.'
                specifications='Must satisfy INV-E2E018-GOODS-SPEC.'
            })
        } @(201)
    } else {
        $pr = Invoke-JsonApi GET "/api/PurchaseRequisitions/$([Guid]$prRow.Rows[0].Id)" $adminToken $null
    }
    if ($pr.status -eq 'Draft') {
        $submission = Invoke-JsonApi POST "/api/PurchaseRequisitions/$($pr.id)/submit" $adminToken $null
        $pr = Invoke-JsonApi GET "/api/PurchaseRequisitions/$($pr.id)" $adminToken $null
    }
    if ($pr.status -eq 'Pending Approval' -or $pr.status -eq 'Submitted') {
        $approval = Invoke-JsonApi POST "/api/PurchaseRequisitions/$($pr.id)/approve" $tokens['proc-plan-procurement-approver'] @{
            approved=$true; comments='Independent Head of Procurement approval for governed Goods replenishment.'
        }
        $pr = Invoke-JsonApi GET "/api/PurchaseRequisitions/$($pr.id)" $adminToken $null
    }
    if ($pr.status -ne 'Approved') { throw "Purchase requisition is $($pr.status), not Approved." }
    $release = Invoke-JsonApi POST "/api/PurchaseRequisitions/$($pr.id)/sourcing-release" $tokens['proc-plan-procurement-approver'] @{
        reason='Release approved Goods requisition for governed RFQ sourcing.'
    }

    $budgetReadiness = Invoke-JsonApi GET "/api/PurchaseRequisitions/$($pr.id)/budget-readiness" $adminToken $null
    $authorityReadiness = Invoke-JsonApi GET "/api/PurchaseRequisitions/$($pr.id)/authority-readiness" $adminToken $null
    $sourcingReadiness = Invoke-JsonApi GET "/api/PurchaseRequisitions/$($pr.id)/sourcing-readiness" $adminToken $null
    [pscustomobject]@{
        BudgetId=$budgetId; BudgetStatus=$budgetStatus; PlanNumber=$plan.planNumber; PlanStatus=$plan.status
        AppSubmission=$app.submissionNumber; AppStatus=$app.status; Specification="$($spec.templateCode)/v$($spec.version)"
        SpecificationStatus=$spec.status; RequisitionId=$pr.id; RequisitionNumber=$pr.requisitionNumber
        RequisitionStatus=$pr.status; BudgetDecision=$budgetReadiness.decisionCode
        AuthorityDecision=$authorityReadiness.decisionCode; SourcingDecision=$sourcingReadiness.decisionCode
        SourcingReleaseId=$release.id
    } | ConvertTo-Json -Compress
}
finally {
    foreach ($membership in $addedRoleMemberships) {
        try {
            $null = Invoke-DbNonQuery $connection 'DELETE FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{
                userId=$membership.UserId; roleId=$membership.RoleId
            }
        } catch { }
    }
    foreach ($actorId in $passwordBackups.Keys) {
        $backup = $passwordBackups[$actorId]
        try {
            $null = Invoke-DbNonQuery $connection @'
UPDATE Users SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout WHERE Id=@id;
'@ @{ id=$actorId; hash=$backup.PasswordHash; failed=$backup.AccessFailedCount; lockout=$backup.LockoutEnd }
        } catch { }
    }
    if ($connection.State -eq [System.Data.ConnectionState]::Open) { $connection.Close() }
    $connection.Dispose()
}
