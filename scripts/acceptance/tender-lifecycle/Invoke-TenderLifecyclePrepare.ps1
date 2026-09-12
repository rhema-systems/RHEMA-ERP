[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$ApiBaseUrl,
    [string]$RunId,
    [string]$OutputPath = (Join-Path $PSScriptRoot 'tender-lifecycle.fixture.json'),
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'TenderLifecycle.Common.ps1')

if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = Get-TenderFixtureEnvironmentValue -Name 'TENDER_E2E_RUN_ID' -Required
}
if ($RunId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{7,63}$') {
    throw 'TENDER_E2E_RUN_ID must be 8-64 characters using letters, digits, dot, underscore, or hyphen.'
}

function Read-One {
    param([System.Data.DataTable]$Table,[string]$Message)
    Assert-TenderFixtureCondition ($Table.Rows.Count -eq 1) $Message
    $Table.Rows[0]
}

function Read-Actor {
    param([string]$EnvironmentName,[string]$DefaultUsername)
    $username = Get-TenderFixtureEnvironmentValue -Name $EnvironmentName -Default $DefaultUsername
    $row = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,UserName,FirstName,LastName,IsActive
FROM dbo.Users WHERE TenantId=@tenantId AND UserName=@username;
'@ -Parameters @{tenantId=$tenantId;username=$username}) "Actor '$username' was not found exactly once."
    Assert-TenderFixtureCondition ([bool]$row.IsActive) "Actor '$username' is inactive."
    [pscustomobject]@{
        Id=[Guid]$row.Id
        Username=[string]$row.UserName
        DisplayName=("$($row.FirstName) $($row.LastName)").Trim()
    }
}

function Invoke-SourcePreparation {
    param([Guid]$RequisitionId,[string]$Label,[string]$OfficerToken)
    $readiness = Invoke-TenderFixtureApi -Method GET `
        -Path "/api/PurchaseRequisitions/$RequisitionId/sourcing-readiness" `
        -Token $OfficerToken -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl
    if (-not [bool]$readiness.isReleased) {
        [void](Invoke-TenderFixtureApi -Method POST `
            -Path "/api/PurchaseRequisitions/$RequisitionId/sourcing-release" `
            -Token $OfficerToken `
            -Body @{reason="Release $Label for disposable tender browser acceptance."} `
            -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl)
    }
    $case = Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT TOP(1) Id,SourcingReleaseId,SelectedMethod FROM dbo.ProcurementSourcingCases
WHERE TenantId=@tenantId AND PurchaseRequisitionId=@requisitionId AND IsDeleted=0
ORDER BY CreatedAt DESC;
'@ -Parameters @{tenantId=$tenantId;requisitionId=$RequisitionId}
    if ($case.Rows.Count -eq 1) {
        return [pscustomobject]@{
            CaseId=[Guid]$case.Rows[0].Id
            ReleaseId=[Guid]$case.Rows[0].SourcingReleaseId
        }
    }

    $caseReadiness = Invoke-TenderFixtureApi -Method GET `
        -Path "/api/procurement/sourcing-cases/readiness/$RequisitionId" `
        -Token $OfficerToken -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl
    if (-not [bool]$caseReadiness.canCreate) {
        throw "$Label sourcing-case readiness is blocked: $($caseReadiness.decisionCode) - $($caseReadiness.message)"
    }
    $lineIds = @($caseReadiness.lines | ForEach-Object {[Guid]$_.id})
    Assert-TenderFixtureCondition ($lineIds.Count -ge 2) "$Label must retain at least two active lines."
    $created = Invoke-TenderFixtureApi -Method POST -Path '/api/procurement/sourcing-cases' `
        -Token $OfficerToken -Body @{
            requisitionId=$RequisitionId
            selectedMethod=$caseReadiness.recommendedMethod
            justification="Deterministic $Label sourcing case for real SQL/browser acceptance."
            lots=@(@{
                lotCode='LOT-01';title="$Label governed scope"
                description='Exact approved requisition scope.'
                purchaseRequisitionItemIds=$lineIds
            })
        } -ExpectedStatus @(201) -ApiBaseUrl $ApiBaseUrl
    $createdLineage = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT TOP(1) Id,SourcingReleaseId FROM dbo.ProcurementSourcingCases
WHERE TenantId=@tenantId AND PurchaseRequisitionId=@requisitionId AND IsDeleted=0
ORDER BY CreatedAt DESC;
'@ -Parameters @{tenantId=$tenantId;requisitionId=$RequisitionId}) `
        "$Label authoritative sourcing case was not retained after API creation."
    [pscustomobject]@{
        CaseId=[Guid]$createdLineage.Id
        ReleaseId=[Guid]$createdLineage.SourcingReleaseId
    }
}

function Invoke-ReleaseOnlyPreparation {
    param([Guid]$RequisitionId,[string]$Label,[string]$OfficerToken)
    $readiness = Invoke-TenderFixtureApi -Method GET `
        -Path "/api/PurchaseRequisitions/$RequisitionId/sourcing-readiness" `
        -Token $OfficerToken -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl
    if (-not [bool]$readiness.isReleased) {
        [void](Invoke-TenderFixtureApi -Method POST `
            -Path "/api/PurchaseRequisitions/$RequisitionId/sourcing-release" `
            -Token $OfficerToken `
            -Body @{reason="Release $Label for alternate approved-award conversion acceptance."} `
            -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl)
    }
    $release = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT TOP(1) Id FROM dbo.ProcurementRequisitionSourcingReleases
WHERE TenantId=@tenantId AND PurchaseRequisitionId=@requisitionId AND IsDeleted=0
ORDER BY AttemptNumber DESC,ReleasedAtUtc DESC;
'@ -Parameters @{tenantId=$tenantId;requisitionId=$RequisitionId}) `
        "$Label current immutable release was not retained exactly once."
    $caseCount = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.ProcurementSourcingCases
WHERE TenantId=@tenantId AND PurchaseRequisitionId=@requisitionId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;requisitionId=$RequisitionId})
    Assert-TenderFixtureCondition ($caseCount -eq 0) `
        "$Label must remain release-only until the real purchase-order conversion recovers its sourcing case."
    [Guid]$release.Id
}

function Read-Scenario {
    param([string]$Marker,[switch]$RequireBid,[switch]$RequireTwoBids)
    $tender = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,TenderNumber,Status,SubmissionDeadline,OpeningDate
FROM dbo.Tenders WHERE TenantId=@tenantId AND Notes=@marker AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;marker=$Marker}) "Authoritative scenario '$Marker' was not prepared exactly once."
    $bids = Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,BidNumber,Status,BusinessPartnerId FROM dbo.TenderBids
WHERE TenantId=@tenantId AND TenderId=@tenderId AND IsDeleted=0 ORDER BY CreatedAt,Id;
'@ -Parameters @{tenantId=$tenantId;tenderId=[Guid]$tender.Id}
    $minimum = if($RequireTwoBids){2}elseif($RequireBid){1}else{0}
    Assert-TenderFixtureCondition ($bids.Rows.Count -ge $minimum) "Scenario '$Marker' requires at least $minimum bid(s)."
    [pscustomobject]@{Tender=$tender;Bids=$bids}
}

function Connect-FixtureScenarioLineage {
    param(
        [Guid]$TenderId,
        [Guid]$RequisitionId,
        [Guid]$ReleaseId,
        [Guid]$ExpectedCaseId,
        [string]$EvaluatorToken,
        [string]$Label
    )
    # This is the only direct fixture-state transition after seeding. The exact
    # disposable-database guard has already passed, and both lineage identifiers
    # came from the real release/case APIs above. The case remains null initially
    # so the real recovery boundary registers the source request itself.
    [void](Invoke-TenderFixtureNonQuery -Connection $connection -Sql @'
UPDATE dbo.Tenders
SET SourcePurchaseRequisitionId=@requisitionId,
    SourcingReleaseId=@releaseId,
    SourcingCaseId=NULL
WHERE TenantId=@tenantId AND Id=@tenderId AND IsDeleted=0
  AND SourcePurchaseRequisitionId IS NULL;
'@ -Parameters @{
        tenantId=$tenantId;tenderId=$TenderId;requisitionId=$RequisitionId;releaseId=$ReleaseId
    })
    [void](Invoke-TenderFixtureApi -Method GET `
        -Path "/api/procurement/evaluation-committees/readiness?sourceType=Tender&sourceId=$TenderId" `
        -Token $EvaluatorToken -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl)
    $linkedCaseId = Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT SourcingCaseId FROM dbo.Tenders
WHERE TenantId=@tenantId AND Id=@tenderId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;tenderId=$TenderId}
    Assert-TenderFixtureCondition ($null -ne $linkedCaseId -and [Guid]$linkedCaseId -eq $ExpectedCaseId) `
        "$Label did not retain the exact API-created sourcing case after recovery."
}

function Get-TenderFixtureSha256 {
    param([Parameter(Mandatory)][string]$Value)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash($bytes)
        ([BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Initialize-ControlledCompetition {
    param(
        [Guid]$TenderId,
        [Guid]$ExpectedCaseId,
        [Guid]$OfficerId,
        [string]$Label
    )

    $lineage = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT sc.Id AS CaseId,sc.SelectedMethod,sc.MethodRuleId,sc.MethodRuleCode,
       sc.AuthorityRouteId,sc.AuthorityRouteReference,mr.WorkflowDefinitionId
FROM dbo.ProcurementSourcingCases sc
JOIN dbo.ProcurementPolicyMethodRules mr ON mr.Id=sc.MethodRuleId AND mr.TenantId=sc.TenantId
WHERE sc.TenantId=@tenantId AND sc.Id=@caseId AND sc.IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;caseId=$ExpectedCaseId}) `
        "$Label exact NCT sourcing lineage is unavailable."
    Assert-TenderFixtureCondition ([Guid]$lineage.CaseId -eq $ExpectedCaseId) `
        "$Label sourcing case changed before controlled finalization."
    Assert-TenderFixtureCondition ([int]$lineage.SelectedMethod -eq 1) `
        "$Label must use NationalCompetitiveTendering, not a legacy restricted/RFQ route."
    Assert-TenderFixtureCondition ($null -ne $lineage.AuthorityRouteId -and
        -not [string]::IsNullOrWhiteSpace([string]$lineage.AuthorityRouteReference)) `
        "$Label requires the advanced authority route captured by the real release API."
    Assert-TenderFixtureCondition ($null -ne $lineage.WorkflowDefinitionId) `
        "$Label NCT method rule requires the published award-approval workflow."

    $existing = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.ProcurementTenderControls
WHERE TenantId=@tenantId AND TenderId=@tenderId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;tenderId=$TenderId})
    if ($existing -eq 1) { return }
    Assert-TenderFixtureCondition ($existing -eq 0) `
        "$Label must have zero or one statutory tender control."

    $bids = Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT b.Id,b.BidNumber,b.BusinessPartnerId,b.TotalBidAmount,b.Currency,
       bp.PartnerName,bp.PrimaryEmail
FROM dbo.TenderBids b
JOIN dbo.BusinessPartners bp ON bp.Id=b.BusinessPartnerId AND bp.TenantId=b.TenantId
WHERE b.TenantId=@tenantId AND b.TenderId=@tenderId AND b.IsDeleted=0
  AND b.Status=N'Submitted'
ORDER BY b.CreatedAt,b.Id;
'@ -Parameters @{tenantId=$tenantId;tenderId=$TenderId}
    Assert-TenderFixtureCondition ($bids.Rows.Count -eq 2) `
        "$Label requires exactly two sealed Submitted bids before controlled opening."

    $now = [DateTime]::UtcNow
    $deadline = $now.AddHours(-4)
    $opening = $now.AddHours(-3)
    $controlId = [Guid]::NewGuid()
    $lifecycle = [ordered]@{
        schemaVersion='tdc.nct-ict-control.v1';id=$controlId;tenderId=$TenderId
        sourcingCaseId=[Guid]$lineage.CaseId;methodRuleId=[Guid]$lineage.MethodRuleId
        authorityRouteId=[Guid]$lineage.AuthorityRouteId;method=1
        methodRuleCode=[string]$lineage.MethodRuleCode
        authorityRouteReference=[string]$lineage.AuthorityRouteReference
        status=0;submissionDeadlineUtc=$deadline;openingScheduledAtUtc=$opening
    } | ConvertTo-Json -Compress
    $lifecycleHash = Get-TenderFixtureSha256 -Value $lifecycle

    [void](Invoke-TenderFixtureNonQuery -Connection $connection -Sql @'
INSERT dbo.ProcurementTenderControls
    (Id,TenderId,SourcingCaseId,MethodRuleId,AuthorityRouteId,Method,MethodRuleCode,
     AuthorityRouteReference,Status,AdvertisementReference,PublicationChannel,
     TenderDocumentReference,TenderDocumentVersion,DocumentFee,
     AdvertisementEvidenceReference,AdvertisedAtUtc,SubmissionDeadlineUtc,
     OpeningScheduledAtUtc,ApprovalActorsJson,WorkflowDefinitionId,
     LifecycleSnapshotJson,IntegrityHash,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
VALUES
    (@id,@tenderId,@caseId,@methodRuleId,@authorityRouteId,1,@methodRuleCode,
     @authorityRouteReference,0,@advertisementReference,@publicationChannel,
     @documentReference,N'1',0,@advertisementEvidence,@advertisedAt,@deadline,
     @opening,N'[]',@workflowDefinitionId,@snapshot,@integrityHash,
     @createdAt,N'Tender E2E controlled prerequisite',@officerId,0,@tenantId);
UPDATE dbo.Tenders
SET Status=N'Published',PublishDate=@advertisedAt,PublishedById=@officerId,
    SubmissionDeadline=@deadline,OpeningDate=@opening
WHERE TenantId=@tenantId AND Id=@tenderId AND IsDeleted=0;
'@ -Parameters @{
        id=$controlId;tenderId=$TenderId;caseId=[Guid]$lineage.CaseId
        methodRuleId=[Guid]$lineage.MethodRuleId;authorityRouteId=[Guid]$lineage.AuthorityRouteId
        methodRuleCode=[string]$lineage.MethodRuleCode
        authorityRouteReference=[string]$lineage.AuthorityRouteReference
        advertisementReference="TE2E-$RunId-NCT-ADVERT"
        publicationChannel='Guarded browser acceptance channel'
        documentReference="TE2E-$RunId-NCT-DOC"
        advertisementEvidence="TE2E-$RunId-NCT-ADVERT-EVIDENCE"
        advertisedAt=$now.AddDays(-1);deadline=$deadline;opening=$opening
        workflowDefinitionId=[Guid]$lineage.WorkflowDefinitionId
        snapshot=$lifecycle;integrityHash=$lifecycleHash;createdAt=$now.AddDays(-1)
        officerId=$OfficerId;tenantId=$tenantId
    })

    for($index=0;$index -lt $bids.Rows.Count;$index++) {
        $bid = $bids.Rows[$index]
        $sequence = $index + 1
        $receivedAt = $deadline.AddMinutes(-30 + $sequence)
        $sealed = [ordered]@{
            schemaVersion='tdc.nct-ict-sealed-submission.v1';tenderId=$TenderId
            tenderBidId=[Guid]$bid.Id;businessPartnerId=[Guid]$bid.BusinessPartnerId
            receivedAtUtc=$receivedAt;submissionDeadlineUtc=$deadline;disposition=0
            totalBidAmount=[decimal]$bid.TotalBidAmount;currency=[string]$bid.Currency
        } | ConvertTo-Json -Compress
        $sealedHash = Get-TenderFixtureSha256 -Value $sealed
        $issueSnapshot = "$controlId|$($bid.BusinessPartnerId)|$sequence|$RunId"
        $issueHash = Get-TenderFixtureSha256 -Value $issueSnapshot
        [void](Invoke-TenderFixtureNonQuery -Connection $connection -Sql @'
INSERT dbo.ProcurementTenderDocumentIssues
    (Id,TenderControlId,BusinessPartnerId,RecipientName,RecipientEmail,AmountPaid,
     IssueReceiptNumber,IssuedAtUtc,IssuedByUserId,EvidenceReference,IntegrityHash,
     CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
VALUES
    (@issueId,@controlId,@partnerId,@partnerName,@partnerEmail,0,
     @issueReceipt,@issuedAt,@officerId,@issueEvidence,@issueHash,
     @createdAt,N'Tender E2E controlled prerequisite',@officerId,0,@tenantId);
INSERT dbo.ProcurementTenderSubmissionReceipts
    (Id,TenderControlId,TenderBidId,BusinessPartnerId,ReceiptNumber,ReceivedAtUtc,
     SubmissionDeadlineUtc,Disposition,SealedSnapshotJson,IntegrityHash,
     CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
VALUES
    (@receiptId,@controlId,@bidId,@partnerId,@receiptNumber,@receivedAt,
     @deadline,0,@sealedSnapshot,@sealedHash,
     @createdAt,N'Tender E2E controlled prerequisite',@officerId,0,@tenantId);
'@ -Parameters @{
            issueId=[Guid]::NewGuid();controlId=$controlId;partnerId=[Guid]$bid.BusinessPartnerId
            partnerName=[string]$bid.PartnerName;partnerEmail=[string]$bid.PrimaryEmail
            issueReceipt="TE2E-$RunId-DOC-$sequence";issuedAt=$deadline.AddHours(-1)
            officerId=$OfficerId;issueEvidence="TE2E-$RunId-DOC-EVIDENCE-$sequence"
            issueHash=$issueHash;receiptId=[Guid]::NewGuid();bidId=[Guid]$bid.Id
            receiptNumber="TE2E-$RunId-BID-$sequence";receivedAt=$receivedAt
            deadline=$deadline;sealedSnapshot=$sealed;sealedHash=$sealedHash
            createdAt=$receivedAt;tenantId=$tenantId
        })
    }
}

function Initialize-ControlledSupplierLifecycle {
    param(
        [Guid]$TenderId,
        [Guid]$ExpectedCaseId,
        [string]$OfficerToken,
        [Guid]$SupplierId,
        [string]$SupplierName,
        [string]$SupplierEmail,
        [string]$Label
    )

    $source = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT t.Id,t.Status,t.SubmissionDeadline,t.OpeningDate,t.Currency,t.SourcingCaseId,
       sc.SelectedMethod
FROM dbo.Tenders t
JOIN dbo.ProcurementSourcingCases sc
  ON sc.Id=t.SourcingCaseId AND sc.TenantId=t.TenantId AND sc.IsDeleted=0
WHERE t.TenantId=@tenantId AND t.Id=@tenderId AND t.IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;tenderId=$TenderId}) `
        "$Label tender and exact sourcing lineage are unavailable."
    Assert-TenderFixtureCondition ([Guid]$source.SourcingCaseId -eq $ExpectedCaseId) `
        "$Label did not retain the exact API-created sourcing case."
    Assert-TenderFixtureCondition ([int]$source.SelectedMethod -eq 1) `
        "$Label must use the controlled NationalCompetitiveTendering lifecycle."
    Assert-TenderFixtureCondition ([string]$source.Status -eq 'Approved') `
        "$Label must start Approved so the production advertisement boundary is exercised."
    $deadline = [DateTime]$source.SubmissionDeadline
    $opening = [DateTime]$source.OpeningDate
    Assert-TenderFixtureCondition ($deadline -gt [DateTime]::UtcNow -and $opening -ge $deadline) `
        "$Label requires a future submission deadline and valid opening schedule."

    $readiness = Invoke-TenderFixtureApi -Method GET `
        -Path "/api/procurement/tender-document-register/readiness?sourceType=Tender&sourceId=$TenderId" `
        -Token $OfficerToken -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl
    Assert-TenderFixtureCondition ([bool]$readiness.ready -and
        -not [string]::IsNullOrWhiteSpace([string]$readiness.effectiveTemplateVersionId)) `
        "$Label has no exact Published NCT tender-document template."

    [void](Invoke-TenderFixtureApi -Method POST `
        -Path '/api/procurement/tender-document-register/bind' `
        -Token $OfficerToken -ExpectedStatus @(201) -ApiBaseUrl $ApiBaseUrl -Body @{
            sourceType='Tender';sourceId=$TenderId
            templateVersionId=[Guid]$readiness.effectiveTemplateVersionId
            submissionDeadlineUtc=$deadline.ToUniversalTime().ToString('o')
            openingScheduledAtUtc=$opening.ToUniversalTime().ToString('o')
            bidValidityUntilUtc=$deadline.ToUniversalTime().AddDays(30).ToString('o')
            feeMode='Free';feeAmount=0;currencyCode=[string]$source.Currency
        })

    [void](Invoke-TenderFixtureApi -Method POST `
        -Path "/api/procurement/tenders/$TenderId/controls/advertise" `
        -Token $OfficerToken -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl -Body @{
            advertisementReference="TE2E-$RunId-SUPPLIER-ADVERT"
            publicationChannel='Guarded browser acceptance channel'
            tenderDocumentReference="TE2E-$RunId-SUPPLIER-DOC"
            tenderDocumentVersion='1'
            documentFee=0
            advertisementEvidenceReference="TE2E-$RunId-SUPPLIER-ADVERT-EVIDENCE"
            submissionDeadlineUtc=$deadline.ToUniversalTime().ToString('o')
            openingScheduledAtUtc=$opening.ToUniversalTime().ToString('o')
        })

    [void](Invoke-TenderFixtureApi -Method POST `
        -Path "/api/procurement/tenders/$TenderId/controls/document-issues" `
        -Token $OfficerToken -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl -Body @{
            businessPartnerId=$SupplierId;recipientName=$SupplierName
            recipientEmail=$SupplierEmail;amountPaid=0
            issueReceiptNumber="TE2E-$RunId-SUPPLIER-ISSUE"
            evidenceReference="TE2E-$RunId-SUPPLIER-ISSUE-EVIDENCE"
        })

    $effective = Invoke-TenderFixtureApi -Method GET `
        -Path "/api/procurement/tender-document-register/readiness?sourceType=Tender&sourceId=$TenderId" `
        -Token $OfficerToken -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl
    Assert-TenderFixtureCondition ([bool]$effective.ready -and [int]$effective.issuanceCount -eq 1) `
        "$Label controlled document was not issued exactly once to Supplier A."
}

$connection = New-TenderFixtureSqlConnection -ConnectionString $ConnectionString
try {
    Assert-TenderFixtureSchema -Connection $connection
    $databaseName = Get-TenderFixtureDatabaseName -Connection $connection
    $tenant = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,Name,Code FROM dbo.Tenants WHERE Code=N'DEFAULT' AND IsDeleted=0;
'@) 'The DEFAULT tenant was not found exactly once.'
    $tenantId = [Guid]$tenant.Id
    $actors = [ordered]@{
        requester=Read-Actor 'TENDER_E2E_REQUESTER_USERNAME' 'manager'
        prApprover=Read-Actor 'TENDER_E2E_PR_APPROVER_USERNAME' 'finance.clerk'
        officer=Read-Actor 'TENDER_E2E_PROCUREMENT_OFFICER_USERNAME' 'accounts.officer'
        evaluator=Read-Actor 'TENDER_E2E_EVALUATOR_USERNAME' 'helpdesk.agent'
        evaluatorB=Read-Actor 'TENDER_E2E_EVALUATOR_B_USERNAME' 'helpdesk.supervisor'
        approver=Read-Actor 'TENDER_E2E_APPROVER_USERNAME' 'financial.controller'
        etcApprover=Read-Actor 'TENDER_E2E_ETC_APPROVER_USERNAME' 'tdc.tender.etc-approver'
        contractApprover=Read-Actor 'TENDER_E2E_CONTRACT_APPROVER_USERNAME' 'chief.accountant'
        supplier=Read-Actor 'TENDER_E2E_SUPPLIER_USERNAME' 'external'
        supplierB=Read-Actor 'TENDER_E2E_SUPPLIER_B_USERNAME' 'tdc.tender.supplier-b'
        unauthorized=Read-Actor 'TENDER_E2E_UNAUTHORIZED_USERNAME' 'employee'
    }
    $actorIds = @($actors.Values | ForEach-Object {$_.Id} | Sort-Object -Unique)
    Assert-TenderFixtureCondition ($actorIds.Count -eq $actors.Count) 'Tender lifecycle actors must be distinct users.'

    if (-not $Apply) {
        Write-Output "TENDER-LIFECYCLE-PREFLIGHT|$databaseName|$($tenant.Code)|$RunId"
        Write-Output 'Dry-run completed. No SQL or API mutations were made.'
        return
    }
    [void](Assert-TenderFixtureDisposableDatabase -Connection $connection)
    if ([string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
        $ApiBaseUrl = Get-TenderFixtureEnvironmentValue -Name 'TENDER_E2E_API_BASE_URL' -Required
    }

    $officer = Connect-TenderFixtureActor `
        -UsernameEnvironmentName 'TENDER_E2E_PROCUREMENT_OFFICER_USERNAME' `
        -PasswordEnvironmentName 'TENDER_E2E_PROCUREMENT_OFFICER_PASSWORD' `
        -DefaultUsername 'accounts.officer' -ApiBaseUrl $ApiBaseUrl
    $evaluatorSession = Connect-TenderFixtureActor `
        -UsernameEnvironmentName 'TENDER_E2E_EVALUATOR_USERNAME' `
        -PasswordEnvironmentName 'TENDER_E2E_EVALUATOR_PASSWORD' `
        -DefaultUsername 'helpdesk.agent' -ApiBaseUrl $ApiBaseUrl
    $etcApproverSession = Connect-TenderFixtureActor `
        -UsernameEnvironmentName 'TENDER_E2E_ETC_APPROVER_USERNAME' `
        -PasswordEnvironmentName 'TENDER_E2E_ETC_APPROVER_PASSWORD' `
        -DefaultUsername 'tdc.tender.etc-approver' -ApiBaseUrl $ApiBaseUrl
    # The guarded Testing-only seeder owns deterministic users, suppliers, budgets,
    # approved PRs, reference configuration, and explicitly fixture-assisted tender,
    # bid, and committee states. Release/case identities and source-request
    # registration always come from the real APIs; evaluation and award remain API-owned.
    $preMarker = "TENDER-E2E:${RunId}:PREPUBLICATION"
    $lifeMarker = "TENDER-E2E:${RunId}:LIFECYCLE-SOURCE"
    $preSource = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,RequisitionNumber,TotalAmount,Currency FROM dbo.PurchaseRequisitions
WHERE TenantId=@tenantId AND Notes=@marker AND Status=N'Approved' AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;marker=$preMarker}) `
        "The guarded seeder did not create approved source '$preMarker'."
    $lifeSource = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,RequisitionNumber,TotalAmount,Currency FROM dbo.PurchaseRequisitions
WHERE TenantId=@tenantId AND Notes=@marker AND Status=N'Approved' AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;marker=$lifeMarker}) `
        "The guarded seeder did not create approved source '$lifeMarker'."
    $alternateMarker = "TENDER-E2E:${RunId}:ALTERNATE-HANDOFF"
    $alternateSource = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,RequisitionNumber,TotalAmount,Currency FROM dbo.PurchaseRequisitions
WHERE TenantId=@tenantId AND Notes=@marker AND Status=N'Approved' AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;marker=$alternateMarker}) `
        "The guarded seeder did not create approved source '$alternateMarker'."
    $contractMarker = "TENDER-E2E:${RunId}:CONTRACT-HANDOFF"
    $contractSource = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,RequisitionNumber,TotalAmount,Currency FROM dbo.PurchaseRequisitions
WHERE TenantId=@tenantId AND Notes=@marker AND Status=N'Approved' AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;marker=$contractMarker}) `
        "The guarded seeder did not create approved source '$contractMarker'."
    # PREPUBLICATION deliberately starts as an approved, unreleased PR. The
    # browser must cross the real PR -> Tender entry boundary, which creates
    # the immutable release and sourcing case. Do not fabricate that lineage
    # in fixture preparation or the test ceases to prove the user journey.
    $preReadiness = Invoke-TenderFixtureApi -Method GET `
        -Path "/api/PurchaseRequisitions/$($preSource.Id)/sourcing-readiness" `
        -Token $officer.Token -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl
    Assert-TenderFixtureCondition (-not [bool]$preReadiness.isReleased) `
        'Pre-publication source must be unreleased before browser acceptance starts.'
    $preExistingLineage = Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT
    (SELECT COUNT(*) FROM dbo.ProcurementRequisitionSourcingReleases
     WHERE TenantId=@tenantId AND PurchaseRequisitionId=@requisitionId) AS ReleaseCount,
    (SELECT COUNT(*) FROM dbo.ProcurementSourcingCases
     WHERE TenantId=@tenantId AND PurchaseRequisitionId=@requisitionId AND IsDeleted=0) AS CaseCount;
'@ -Parameters @{tenantId=$tenantId;requisitionId=[Guid]$preSource.Id}
    Assert-TenderFixtureCondition (
        [int]$preExistingLineage.Rows[0].ReleaseCount -eq 0 -and
        [int]$preExistingLineage.Rows[0].CaseCount -eq 0
    ) 'Pre-publication source unexpectedly has sourcing lineage before browser acceptance.'
    [void](Invoke-SourcePreparation -RequisitionId ([Guid]$lifeSource.Id) `
        -Label 'lifecycle PR' -OfficerToken $officer.Token)
    $alternateLineage = Invoke-SourcePreparation `
        -RequisitionId ([Guid]$alternateSource.Id) `
        -Label 'alternate handoff PR' -OfficerToken $officer.Token
    $alternateCandidate = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT t.Id AS TenderId,t.TenderNumber,b.Id AS TenderBidId,b.BusinessPartnerId,b.TotalBidAmount
FROM dbo.Tenders t
JOIN dbo.TenderBids b ON b.TenderId=t.Id AND b.TenantId=t.TenantId AND b.IsDeleted=0
WHERE t.TenantId=@tenantId AND t.Notes=@marker AND t.Status=N'Evaluated'
  AND b.Status=N'Evaluated' AND t.IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;marker=$alternateMarker}) `
        'The alternate evaluated Tender/Bid prerequisite is missing.'
    $linkedAlternate = [int](Invoke-TenderFixtureNonQuery -Connection $connection -Sql @'
UPDATE dbo.Tenders
SET SourcingReleaseId=@releaseId
WHERE TenantId=@tenantId AND Id=@tenderId AND SourcePurchaseRequisitionId=@requisitionId
  AND SourcingReleaseId IS NULL AND SourcingCaseId IS NULL AND IsDeleted=0;
'@ -Parameters @{
        tenantId=$tenantId;tenderId=[Guid]$alternateCandidate.TenderId
        requisitionId=[Guid]$alternateSource.Id;releaseId=[Guid]$alternateLineage.ReleaseId
    })
    Assert-TenderFixtureCondition ($linkedAlternate -eq 1) `
        'The alternate tender did not retain the exact API-created sourcing release.'
    try {
        [void](Invoke-TenderFixtureApi -Method GET `
            -Path "/api/procurement/evaluation-committees/readiness?sourceType=Tender&sourceId=$($alternateCandidate.TenderId)" `
            -Token $evaluatorSession.Token -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl)
    } catch {
        $diagnostic = Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT TOP(1) ExceptionType,FullMessage,StackTrace
FROM dbo.SystemExceptionLogs
WHERE TenantId=@tenantId
  AND RequestPath=N'/api/procurement/evaluation-committees/readiness'
ORDER BY CreatedAt DESC;
'@ -Parameters @{tenantId=$tenantId}
        if ($diagnostic.Rows.Count -eq 1) {
            $row = $diagnostic.Rows[0]
            throw "Alternate lineage recovery failed: $($row.ExceptionType)`n$($row.FullMessage)`n$($row.StackTrace)"
        }
        throw
    }
    $recoveredAlternateCaseId = Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT SourcingCaseId FROM dbo.Tenders
WHERE TenantId=@tenantId AND Id=@tenderId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;tenderId=[Guid]$alternateCandidate.TenderId}
    Assert-TenderFixtureCondition (
        $null -ne $recoveredAlternateCaseId -and
        [Guid]$recoveredAlternateCaseId -eq [Guid]$alternateLineage.CaseId) `
        'The alternate tender did not recover the exact API-created sourcing case.'
    # NCT/ICT/QBS/QCBS must not use the legacy TenderAward-to-PO shortcut. The
    # browser suite proves that fail-closed boundary against the independently
    # approved contract award below, then exercises the supported contract path.

    $contractLineage = Invoke-SourcePreparation `
        -RequisitionId ([Guid]$contractSource.Id) `
        -Label 'contract handoff PR' -OfficerToken $officer.Token
    $contractAward = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT a.Id AS AwardId,a.TenderId,a.TenderBidId,a.BusinessPartnerId,a.Status,
       a.AwardedAmount,a.Currency,a.CreatedById,a.AwardedById,t.TenderNumber
FROM dbo.TenderAwards a
JOIN dbo.Tenders t ON t.Id=a.TenderId AND t.TenantId=a.TenantId AND t.IsDeleted=0
WHERE a.TenantId=@tenantId AND t.Notes=@marker AND a.IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;marker=$contractMarker}) `
        'The independently approved TenderAward prerequisite for real contract handoff is missing.'
    Assert-TenderFixtureCondition ([string]$contractAward.Status -eq 'Awarded') `
        'The real contract-handoff TenderAward prerequisite is not finalized.'
    Assert-TenderFixtureCondition (
        $contractAward.CreatedById -ne [DBNull]::Value -and
        $contractAward.AwardedById -ne [DBNull]::Value -and
        [Guid]$contractAward.CreatedById -ne [Guid]$contractAward.AwardedById) `
        'The real contract-handoff TenderAward must retain a distinct recommendation maker and approver.'
    $linkedContract = [int](Invoke-TenderFixtureNonQuery -Connection $connection -Sql @'
UPDATE dbo.Tenders
SET SourcingReleaseId=@releaseId
WHERE TenantId=@tenantId AND Id=@tenderId AND SourcePurchaseRequisitionId=@requisitionId
  AND SourcingReleaseId IS NULL AND SourcingCaseId IS NULL AND IsDeleted=0;
'@ -Parameters @{
        tenantId=$tenantId;tenderId=[Guid]$contractAward.TenderId
        requisitionId=[Guid]$contractSource.Id;releaseId=[Guid]$contractLineage.ReleaseId
    })
    Assert-TenderFixtureCondition ($linkedContract -eq 1) `
        'The contract-handoff tender did not retain the exact API-created sourcing release.'

    $crossTenantTender = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT t.Id,t.TenantId,t.TenderNumber,t.Status,t.Title,t.UpdatedAt
FROM dbo.Tenders t
JOIN dbo.Tenants x ON x.Id=t.TenantId AND x.IsDeleted=0
WHERE t.TenantId<>@tenantId AND t.Notes=@marker AND t.IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;marker="TENDER-E2E:${RunId}:CROSS-TENANT"}) `
        'The guarded seeder did not create exactly one foreign-tenant Tender isolation fixture.'

    $scenarioSources = @{}
    $scenarioLineages = @{}
    foreach($scenarioName in @('SUPPLIER-LIFECYCLE','COMMITTEE-ABSENT','COMMITTEE-DRAFT','COMMITTEE-ACTIVE')) {
        $scenarioMarker = "TENDER-E2E:${RunId}:$scenarioName"
        $source = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,RequisitionNumber,TotalAmount,Currency FROM dbo.PurchaseRequisitions
WHERE TenantId=@tenantId AND Notes=@marker AND Status=N'Approved' AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;marker=$scenarioMarker}) `
            "The guarded seeder did not create approved scenario source '$scenarioMarker'."
        $scenarioSources[$scenarioName] = $source
        $scenarioLineages[$scenarioName] = Invoke-SourcePreparation `
            -RequisitionId ([Guid]$source.Id) -Label "$scenarioName PR" -OfficerToken $officer.Token
    }

    # These four scenario families are fixture-assisted prerequisites created by
    # the guarded Testing-only seeder. Preparation fails closed if any is absent;
    # it never synthesizes tender, bid, or committee rows with ad-hoc SQL.
    $supplierLifecycle = Read-Scenario "TENDER-E2E:${RunId}:SUPPLIER-LIFECYCLE"
    $absent = Read-Scenario "TENDER-E2E:${RunId}:COMMITTEE-ABSENT" -RequireBid
    $draft = Read-Scenario "TENDER-E2E:${RunId}:COMMITTEE-DRAFT" -RequireBid
    $active = Read-Scenario "TENDER-E2E:${RunId}:COMMITTEE-ACTIVE" -RequireTwoBids
    Assert-TenderFixtureCondition ($supplierLifecycle.Bids.Rows.Count -eq 0) `
        'Supplier lifecycle must start without a Supplier A bid so the browser owns bid initiation.'

    $scenarioRows = @{
        'SUPPLIER-LIFECYCLE'=$supplierLifecycle
        'COMMITTEE-ABSENT'=$absent
        'COMMITTEE-DRAFT'=$draft
        'COMMITTEE-ACTIVE'=$active
    }
    foreach($scenarioName in $scenarioRows.Keys) {
        Connect-FixtureScenarioLineage `
            -TenderId ([Guid]$scenarioRows[$scenarioName].Tender.Id) `
            -RequisitionId ([Guid]$scenarioSources[$scenarioName].Id) `
            -ReleaseId ([Guid]$scenarioLineages[$scenarioName].ReleaseId) `
            -ExpectedCaseId ([Guid]$scenarioLineages[$scenarioName].CaseId) `
            -EvaluatorToken $evaluatorSession.Token -Label $scenarioName
    }
    $supplierA = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,PartnerName,PrimaryEmail FROM dbo.BusinessPartners
WHERE TenantId=@tenantId AND Notes=@marker AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;marker="TENDER-E2E:${RunId}:SUPPLIER-A"}) `
        'Supplier A business-partner fixture is missing.'
    Initialize-ControlledSupplierLifecycle `
        -TenderId ([Guid]$supplierLifecycle.Tender.Id) `
        -ExpectedCaseId ([Guid]$scenarioLineages['SUPPLIER-LIFECYCLE'].CaseId) `
        -OfficerToken $officer.Token `
        -SupplierId ([Guid]$supplierA.Id) `
        -SupplierName ([string]$supplierA.PartnerName) `
        -SupplierEmail ([string]$supplierA.PrimaryEmail) `
        -Label 'SUPPLIER-LIFECYCLE'
    Initialize-ControlledCompetition `
        -TenderId ([Guid]$active.Tender.Id) `
        -ExpectedCaseId ([Guid]$scenarioLineages['COMMITTEE-ACTIVE'].CaseId) `
        -OfficerId ([Guid]$actors.officer.Id) `
        -Label 'COMMITTEE-ACTIVE'

    $supplierLifecycleStatus = [string](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT Status FROM dbo.Tenders WHERE TenantId=@tenantId AND Id=@tenderId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;tenderId=[Guid]$supplierLifecycle.Tender.Id})
    Assert-TenderFixtureCondition ($supplierLifecycleStatus -eq 'Published') `
        'Supplier lifecycle tender must be Published.'
    Assert-TenderFixtureCondition ([DateTime]$supplierLifecycle.Tender.SubmissionDeadline -gt [DateTime]::UtcNow) `
        'Supplier lifecycle tender deadline must remain in the future.'
    Assert-TenderFixtureCondition ([string]$absent.Bids.Rows[0].Status -eq 'Opened') `
        'Absent-committee bid must be formally Opened.'
    Assert-TenderFixtureCondition ([string]$draft.Bids.Rows[0].Status -eq 'Opened') `
        'Draft-committee bid must be formally Opened.'
    foreach($bid in $active.Bids.Rows){
        Assert-TenderFixtureCondition ([string]$bid.Status -eq 'Submitted') `
            'Active competition bids must remain sealed Submitted records for browser-controlled opening.'
    }
    $draftControlCount = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.ProcurementEvaluationCommitteeControls
WHERE TenantId=@tenantId AND SourceType=0 AND SourceId=@sourceId AND Status=0 AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;sourceId=[Guid]$draft.Tender.Id})
    Assert-TenderFixtureCondition ($draftControlCount -eq 1) 'Draft scenario must have exactly one Draft committee control.'
    $activeReady = Invoke-TenderFixtureApi -Method GET `
        -Path "/api/procurement/evaluation-committees/readiness?sourceType=Tender&sourceId=$($active.Tender.Id)" `
        -Token $evaluatorSession.Token -ExpectedStatus @(200) -ApiBaseUrl $ApiBaseUrl
    Assert-TenderFixtureCondition ([bool]$activeReady.compositionReady -and [bool]$activeReady.quorumMet) `
        'Active committee composition and signed quorum must both be ready.'

    $lot = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT TOP(1) Id,LotCode FROM dbo.TenderLots
WHERE TenantId=@tenantId AND TenderId=@tenderId AND IsDeleted=0 ORDER BY DisplayOrder,CreatedAt;
'@ -Parameters @{tenantId=$tenantId;tenderId=[Guid]$supplierLifecycle.Tender.Id}) 'Supplier lifecycle lot is missing.'
    $item = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT TOP(1) Id,Description,Quantity FROM dbo.TenderItems
WHERE TenantId=@tenantId AND TenderId=@tenderId AND IsDeleted=0 ORDER BY LineNumber,CreatedAt;
'@ -Parameters @{tenantId=$tenantId;tenderId=[Guid]$supplierLifecycle.Tender.Id}) 'Supplier lifecycle item is missing.'
    $activeItem = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT TOP(1) Id,Description,Quantity,LotId FROM dbo.TenderItems
WHERE TenantId=@tenantId AND TenderId=@tenderId AND IsDeleted=0 ORDER BY LineNumber,CreatedAt;
'@ -Parameters @{tenantId=$tenantId;tenderId=[Guid]$active.Tender.Id}) 'Competition item is missing.'
    $preItems = Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT ItemDescription FROM dbo.PurchaseRequisitionItems
WHERE TenantId=@tenantId AND RequisitionId=@id AND IsDeleted=0 ORDER BY CreatedAt,Id;
'@ -Parameters @{tenantId=$tenantId;id=[Guid]$preSource.Id}
    Assert-TenderFixtureCondition ($preItems.Rows.Count -ge 2) 'Pre-publication PR requires at least two lines.'
    $template = Read-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT TOP(1) Id,TemplateName FROM dbo.EvaluationTemplates
WHERE TenantId=@tenantId AND Category=N'Goods' AND TenderType=N'ITB' AND IsActive=1 AND IsDeleted=0
ORDER BY IsDefault DESC,CreatedAt DESC;
'@ -Parameters @{tenantId=$tenantId}) 'An active Goods/ITB evaluation template is required.'

    $fixture = [ordered]@{
        schemaVersion=1;runId=$RunId;tenantCode=[string]$tenant.Code
        prePublication=[ordered]@{
            approvedRequisitionId=[string]$preSource.Id;requisitionNumber=[string]$preSource.RequisitionNumber
            title="Tender E2E publication $RunId";description='Real API approval and controlled publication.'
            tenderType='ITB';evaluationTemplateName=[string]$template.TemplateName
            expectedItemDescriptions=@($preItems.Rows|ForEach-Object{[string]$_.ItemDescription})
            submissionDeadline=[DateTime]::UtcNow.AddHours(4).ToString('yyyy-MM-ddTHH:mm')
            openingDate=[DateTime]::UtcNow.AddHours(4).AddMinutes(15).ToString('yyyy-MM-ddTHH:mm')
            publication=[ordered]@{
                advertisementReference="TE2E-$RunId-ADVERT";publicationChannel='GHANEPS acceptance channel'
                tenderDocumentReference="TE2E-$RunId-DOC";tenderDocumentVersion='1.0'
                advertisementEvidenceReference="TE2E-$RunId-ADVERT-EVIDENCE"
                invitedSupplier=[ordered]@{
                    businessPartnerId=[string]$supplierA.Id
                    name=[string]$supplierA.PartnerName
                    email=[string]$supplierA.PrimaryEmail
                }
            }
        }
        supplierLifecycle=[ordered]@{
            tenderId=[string]$supplierLifecycle.Tender.Id;tenderNumber=[string]$supplierLifecycle.Tender.TenderNumber
            lotId=[string]$lot.Id;lotLabel=[string]$lot.LotCode;expectedItemDescription=[string]$item.Description
            offeredQuantity=[decimal]$item.Quantity;unitPrice=90;deliveryDays=14;brand='SupplierA';model='A-100'
            technicalProposal=('Technical proposal retained by real browser persistence. ' * 3)
            commercialProposal=('Commercial proposal retained by real browser persistence. ' * 3)
            paymentReference="TE2E-$RunId-PAYMENT";requiredDocumentPaths=@{}
        }
        committee=[ordered]@{
            absent=[ordered]@{tenderId=[string]$absent.Tender.Id;tenderNumber=[string]$absent.Tender.TenderNumber;bidId=[string]$absent.Bids.Rows[0].Id;bidNumber=[string]$absent.Bids.Rows[0].BidNumber}
            draft=[ordered]@{tenderId=[string]$draft.Tender.Id;tenderNumber=[string]$draft.Tender.TenderNumber;bidId=[string]$draft.Bids.Rows[0].Id;bidNumber=[string]$draft.Bids.Rows[0].BidNumber}
            active=[ordered]@{tenderId=[string]$active.Tender.Id;tenderNumber=[string]$active.Tender.TenderNumber;bidId=[string]$active.Bids.Rows[0].Id;bidNumber=[string]$active.Bids.Rows[0].BidNumber}
        }
        competition=[ordered]@{
            tenderId=[string]$active.Tender.Id;tenderNumber=[string]$active.Tender.TenderNumber
            firstSupplierBidId=[string]$active.Bids.Rows[0].Id;firstSupplierBidNumber=[string]$active.Bids.Rows[0].BidNumber
            secondSupplierBidId=[string]$active.Bids.Rows[1].Id;secondSupplierBidNumber=[string]$active.Bids.Rows[1].BidNumber
            expectedWinnerBidNumber=[string]$active.Bids.Rows[0].BidNumber
            opening=[ordered]@{mode='Controlled';evidenceReference="TE2E-$RunId-OPENING";officerSignatureReference="TE2E-$RunId-OFFICER-SIGN";observerName='Independent Observer';observerSignatureReference="TE2E-$RunId-OBSERVER-SIGN"}
            tenderItemId=[string]$activeItem.Id;lotId=[string]$activeItem.LotId
            offeredQuantity=[decimal]$activeItem.Quantity;unitPrice=110;deliveryDays=14;brand='SupplierB';model='B-200'
        }
        evaluation=[ordered]@{signatureReference="TE2E-$RunId-EVAL-SIGN";evidenceReference="TE2E-$RunId-EVAL-EVIDENCE";technicalComments='Technically responsive.';commercialComments='Commercially responsive.';overallComments='Recommend the highest ranked bid.';recommendation='Recommend award subject to independent approval.'}
        award=[ordered]@{justification='Highest ranked responsive bid within approved budget.';approvalNotes='Independent award approval.'}
        handoff=[ordered]@{
            mode='Contract'
            alternateApprovedAwardId=[string]$contractAward.AwardId
            alternateMode='PO'
            contractApprovedAwardId=[string]$contractAward.AwardId
            contractAwardAmount=[decimal]$contractAward.AwardedAmount
            contractAwardCurrency=[string]$contractAward.Currency
            contractStartDate=[DateTime]::UtcNow.AddDays(1).ToString('yyyy-MM-dd')
            contractEndDate=[DateTime]::UtcNow.AddYears(1).ToString('yyyy-MM-dd')
        }
        regression=[ordered]@{
            tenderAmendmentDescription='Clarify delivery evidence.'
            tenderAmendmentChanges='Add signed delivery schedule evidence.'
            crossTenantTenderId=[string]$crossTenantTender.Id
        }
        metadata=[ordered]@{databaseName=$databaseName;tenantId=[string]$tenantId;sourcePurchaseRequisitionId=[string]$preSource.Id}
    }
    [void](Write-TenderFixtureJson -Value $fixture -Path $OutputPath)
    Write-Output "TENDER-LIFECYCLE-PREPARED|$databaseName|$($tenant.Code)|$RunId"
} finally {
    $connection.Dispose()
}
