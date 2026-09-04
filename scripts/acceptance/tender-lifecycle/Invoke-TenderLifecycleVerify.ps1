[CmdletBinding()]
param(
    [string]$ConnectionString,
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$ResultPath,
    [ValidateSet('Preflight','AfterBid','AfterOpening','AfterEvaluation','AfterAward','AfterHandoff','Complete')]
    [string]$Stage = 'Complete'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'TenderLifecycle.Common.ps1')

function Read-JsonFile {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$Label)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    Assert-TenderFixtureCondition (Test-Path -LiteralPath $fullPath -PathType Leaf) "$Label was not found at '$fullPath'."
    try { Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json -Depth 50 }
    catch { throw "$Label is not valid JSON: $($_.Exception.Message)" }
}

function Require-Guid {
    param($Object,[string]$Name,[string]$Label)
    ConvertTo-TenderFixtureGuid -Value (Get-TenderFixtureObjectValue -Object $Object -Name $Name) -Label "$Label.$Name"
}

function Require-One {
    param([System.Data.DataTable]$Table,[string]$Message)
    Assert-TenderFixtureCondition ($Table.Rows.Count -eq 1) $Message
    $Table.Rows[0]
}

function Read-Tender {
    param([Guid]$Id,[string]$Number,[Guid]$TenantId,[string]$Label)
    $row = Require-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT
    t.Id,t.TenderNumber,t.Status,t.SourcePurchaseRequisitionId,
    t.SourcingReleaseId,t.SourcingCaseId,
    r.PurchaseRequisitionId AS ReleaseRequisitionId,
    r.IntegrityHash AS ReleaseIntegrityHash,
    sc.PurchaseRequisitionId AS CaseRequisitionId,
    sc.SourcingReleaseId AS CaseReleaseId,
    sc.IntegrityHash AS CaseIntegrityHash,
    (
        SELECT COUNT(1)
        FROM dbo.ProcurementSourcingCaseSourceRequests sr
        WHERE sr.TenantId=t.TenantId
          AND sr.SourcingCaseId=t.SourcingCaseId
          AND sr.SourceEntityId=t.Id
          AND sr.SourceType=N'Tender'
          AND sr.IsDeleted=0
    ) AS SourceRegistrationCount
FROM dbo.Tenders t
JOIN dbo.ProcurementRequisitionSourcingReleases r
  ON r.Id=t.SourcingReleaseId AND r.TenantId=t.TenantId AND r.IsDeleted=0
JOIN dbo.ProcurementSourcingCases sc
  ON sc.Id=t.SourcingCaseId AND sc.TenantId=t.TenantId AND sc.IsDeleted=0
WHERE t.Id=@id AND t.TenantId=@tenantId AND t.IsDeleted=0;
'@ -Parameters @{id=$Id;tenantId=$TenantId}) "$Label does not exist in the fixture tenant."
    Assert-TenderFixtureCondition ([string]$row.TenderNumber -eq $Number) "$Label number does not match its database row."
    $numberCount = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.Tenders
WHERE TenantId=@tenantId AND TenderNumber=@number AND IsDeleted=0;
'@ -Parameters @{tenantId=$TenantId;number=$Number})
    Assert-TenderFixtureCondition ($numberCount -eq 1) "$Label number is not unique in the fixture tenant."
    Assert-TenderFixtureCondition ($row.SourcePurchaseRequisitionId -ne [DBNull]::Value) "$Label has no approved requisition source."
    Assert-TenderFixtureCondition (
        [Guid]$row.ReleaseRequisitionId -eq [Guid]$row.SourcePurchaseRequisitionId -and
        [Guid]$row.CaseRequisitionId -eq [Guid]$row.SourcePurchaseRequisitionId -and
        [Guid]$row.CaseReleaseId -eq [Guid]$row.SourcingReleaseId) `
        "$Label release, sourcing-case, and requisition lineage do not agree."
    Assert-TenderFixtureCondition (
        -not [string]::IsNullOrWhiteSpace([string]$row.ReleaseIntegrityHash) -and
        -not [string]::IsNullOrWhiteSpace([string]$row.CaseIntegrityHash)) `
        "$Label release or sourcing-case integrity hash is missing."
    Assert-TenderFixtureCondition ([int]$row.SourceRegistrationCount -eq 1) `
        "$Label must have exactly one sourcing-case source registration."
    $row
}

function Read-Bid {
    param([Guid]$Id,[Guid]$TenderId,[string]$Number,[Guid]$TenantId,[string]$Label)
    $row = Require-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,TenderId,BidNumber,BusinessPartnerId,Status,SubmittedDate,OpenedDate,TotalBidAmount
FROM dbo.TenderBids
WHERE Id=@id AND TenderId=@tenderId AND TenantId=@tenantId AND IsDeleted=0;
'@ -Parameters @{id=$Id;tenderId=$TenderId;tenantId=$TenantId}) "$Label does not exist under the expected tender and tenant."
    Assert-TenderFixtureCondition ([string]$row.BidNumber -eq $Number) "$Label number does not match its database row."
    $numberCount = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.TenderBids
WHERE TenantId=@tenantId AND TenderId=@tenderId AND BidNumber=@number AND IsDeleted=0;
'@ -Parameters @{tenantId=$TenantId;tenderId=$TenderId;number=$Number})
    Assert-TenderFixtureCondition ($numberCount -eq 1) "$Label number is not unique under its tender."
    $row
}

function Assert-PassedCheckpoint {
    param([Parameter(Mandatory)][string]$Name)
    $property = $result.checkpoints.PSObject.Properties[$Name]
    Assert-TenderFixtureCondition ($null -ne $property) "Required lifecycle checkpoint '$Name' was not recorded."
    Assert-TenderFixtureCondition ([string]$property.Value -eq 'Passed') `
        "Required lifecycle checkpoint '$Name' is '$($property.Value)', not Passed."
}

function Test-StageAtLeast {
    param([string]$Required)
    $order = @('Preflight','AfterBid','AfterOpening','AfterEvaluation','AfterAward','AfterHandoff','Complete')
    [Array]::IndexOf($order,$Stage) -ge [Array]::IndexOf($order,$Required)
}

$manifest = Read-JsonFile -Path $ManifestPath -Label 'Tender lifecycle fixture manifest'
$result = Read-JsonFile -Path $ResultPath -Label 'Tender lifecycle result'
Assert-TenderFixtureCondition ($manifest.schemaVersion -eq 1) 'Fixture manifest schemaVersion must be 1.'
Assert-TenderFixtureCondition ($result.schemaVersion -eq 1) 'Lifecycle result schemaVersion must be 1.'
Assert-TenderFixtureCondition (-not [string]::IsNullOrWhiteSpace([string]$manifest.runId)) 'Fixture manifest runId is required.'
Assert-TenderFixtureCondition ([string]$manifest.runId -match '^[A-Za-z0-9][A-Za-z0-9._-]{7,63}$') 'Fixture manifest runId is invalid.'
Assert-TenderFixtureCondition ([string]$result.runId -eq [string]$manifest.runId) 'Result runId does not match the fixture manifest.'
if (-not [string]::IsNullOrWhiteSpace([string]$result.tenantCode)) {
    Assert-TenderFixtureCondition ([string]$result.tenantCode -eq [string]$manifest.tenantCode) 'Result tenantCode does not match the fixture manifest.'
}

$connection = New-TenderFixtureSqlConnection -ConnectionString $ConnectionString
try {
    Assert-TenderFixtureSchema -Connection $connection
    $databaseName = Assert-TenderFixtureDisposableDatabase -Connection $connection
    if ($null -ne $manifest.metadata -and -not [string]::IsNullOrWhiteSpace([string]$manifest.metadata.databaseName)) {
        Assert-TenderFixtureCondition ([string]$manifest.metadata.databaseName -eq $databaseName) 'Fixture manifest targets a different database.'
    }
    $tenant = Require-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,Code FROM dbo.Tenants WHERE Code=@code AND IsDeleted=0;
'@ -Parameters @{code=[string]$manifest.tenantCode}) 'Fixture tenant was not found exactly once.'
    $tenantId = [Guid]$tenant.Id

    $manifestTenantIdValue = Get-TenderFixtureObjectValue $manifest.metadata 'tenantId'
    if (-not [string]::IsNullOrWhiteSpace([string]$manifestTenantIdValue)) {
        $manifestTenantId = ConvertTo-TenderFixtureGuid $manifestTenantIdValue 'metadata.tenantId'
        Assert-TenderFixtureCondition ($manifestTenantId -eq $tenantId) 'Fixture manifest tenant ID does not match its tenant code.'
    }

    $sourceRequisitionId = Require-Guid $manifest.prePublication 'approvedRequisitionId' 'prePublication'
    $metadataSourceRequisitionId = Require-Guid $manifest.metadata 'sourcePurchaseRequisitionId' 'metadata'
    Assert-TenderFixtureCondition ($sourceRequisitionId -eq $metadataSourceRequisitionId) `
        'Pre-publication and metadata source requisition IDs do not agree.'
    $sourceRequisition = Require-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,RequisitionNumber,Status
FROM dbo.PurchaseRequisitions
WHERE Id=@id AND TenantId=@tenantId AND IsDeleted=0;
'@ -Parameters @{id=$sourceRequisitionId;tenantId=$tenantId}) 'Approved source requisition is missing from the fixture tenant.'
    Assert-TenderFixtureCondition ([string]$sourceRequisition.RequisitionNumber -eq [string]$manifest.prePublication.requisitionNumber) `
        'Approved source requisition number does not match its database row.'
    Assert-TenderFixtureCondition ([string]$sourceRequisition.Status -eq 'Approved') `
        'Tender lifecycle source requisition is not Approved.'

    $supplierTenderId = Require-Guid $manifest.supplierLifecycle 'tenderId' 'supplierLifecycle'
    $supplierTender = Read-Tender -Id $supplierTenderId -Number ([string]$manifest.supplierLifecycle.tenderNumber) -TenantId $tenantId -Label 'Supplier lifecycle tender'
    Assert-TenderFixtureCondition ([string]$supplierTender.Status -in @('Published','Closed','Awarded')) 'Supplier lifecycle tender has an unexpected status.'

    $competitionTenderId = Require-Guid $manifest.competition 'tenderId' 'competition'
    [void](Read-Tender -Id $competitionTenderId -Number ([string]$manifest.competition.tenderNumber) -TenantId $tenantId -Label 'Competition tender')
    $firstBidId = Require-Guid $manifest.competition 'firstSupplierBidId' 'competition'
    $secondBidId = Require-Guid $manifest.competition 'secondSupplierBidId' 'competition'
    Assert-TenderFixtureCondition ($firstBidId -ne $secondBidId) 'Competition bids must be distinct.'
    $firstBid = Read-Bid -Id $firstBidId -TenderId $competitionTenderId -Number ([string]$manifest.competition.firstSupplierBidNumber) -TenantId $tenantId -Label 'First competition bid'
    $secondBid = Read-Bid -Id $secondBidId -TenderId $competitionTenderId -Number ([string]$manifest.competition.secondSupplierBidNumber) -TenantId $tenantId -Label 'Second competition bid'
    Assert-TenderFixtureCondition ([Guid]$firstBid.BusinessPartnerId -ne [Guid]$secondBid.BusinessPartnerId) 'Competition bids must belong to different suppliers.'

    $absentTenderId = Require-Guid $manifest.committee.absent 'tenderId' 'committee.absent'
    $absentControls = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.ProcurementEvaluationCommitteeControls
WHERE TenantId=@tenantId AND SourceType=0 AND SourceId=@sourceId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;sourceId=$absentTenderId})
    Assert-TenderFixtureCondition ($absentControls -eq 0) 'Absent-committee scenario unexpectedly has a committee control.'

    $draftTenderId = Require-Guid $manifest.committee.draft 'tenderId' 'committee.draft'
    $draftControls = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.ProcurementEvaluationCommitteeControls
WHERE TenantId=@tenantId AND SourceType=0 AND SourceId=@sourceId AND Status=0 AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;sourceId=$draftTenderId})
    Assert-TenderFixtureCondition ($draftControls -eq 1) 'Draft-committee scenario must retain exactly one Draft control.'

    $activeControls = Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,RequiredQuorum,Status FROM dbo.ProcurementEvaluationCommitteeControls
WHERE TenantId=@tenantId AND SourceType=0 AND SourceId=@sourceId AND Status=1 AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;sourceId=$competitionTenderId}
    $activeControl = Require-One $activeControls 'Competition tender must have exactly one Active committee control.'
    $activeAppointments = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.ProcurementEvaluationCommitteeAppointments
WHERE TenantId=@tenantId AND CommitteeControlId=@controlId AND Status=1 AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;controlId=[Guid]$activeControl.Id})
    Assert-TenderFixtureCondition ($activeAppointments -ge [int]$activeControl.RequiredQuorum) 'Active committee does not have enough accepted appointments for quorum.'

    $statutoryControl = Require-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT Id,TenderId,SourcingCaseId,MethodRuleId,AuthorityRouteId,Method,Status,
       WorkflowDefinitionId,OpenedAtUtc,OpeningSnapshotJson,OpeningIntegrityHash,
       TechnicalEvaluatedAtUtc,TechnicalEvaluationSnapshotJson,TechnicalEvaluationIntegrityHash,
       FinancialEvaluatedAtUtc,FinancialEvaluationSnapshotJson,FinancialEvaluationIntegrityHash,
       RecommendedBidId,WorkflowInstanceId,SubmittedForApprovalAtUtc,SubmittedForApprovalById,
       ApprovedAtUtc,ApprovedById,AwardBidId,AwardReference,AwardEvidenceReference,AwardedAtUtc,
       ContractReference,ContractEvidenceReference,ContractedAtUtc,
       BidderAcceptanceReference,BidderAcceptanceEvidenceReference,AcceptedAtUtc,
       LifecycleSnapshotJson,IntegrityHash
FROM dbo.ProcurementTenderControls
WHERE TenantId=@tenantId AND TenderId=@tenderId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;tenderId=$competitionTenderId}) `
        'Competition tender must have exactly one statutory NCT tender control.'
    Assert-TenderFixtureCondition ([Guid]$statutoryControl.SourcingCaseId -eq [Guid](
        Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT SourcingCaseId FROM dbo.Tenders
WHERE TenantId=@tenantId AND Id=@tenderId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;tenderId=$competitionTenderId})) `
        'Statutory tender control does not retain the tender sourcing case.'
    Assert-TenderFixtureCondition (
        [int]$statutoryControl.Method -eq 1 -and
        $statutoryControl.MethodRuleId -ne [DBNull]::Value -and
        $statutoryControl.AuthorityRouteId -ne [DBNull]::Value -and
        $statutoryControl.WorkflowDefinitionId -ne [DBNull]::Value) `
        'Statutory tender control does not retain its exact NCT rule, authority route, and workflow.'
    Assert-TenderFixtureCondition (
        -not [string]::IsNullOrWhiteSpace([string]$statutoryControl.LifecycleSnapshotJson) -and
        ([string]$statutoryControl.IntegrityHash).Length -eq 64) `
        'Statutory tender control has no immutable lifecycle snapshot or valid integrity hash.'
    $statutoryIssueCount = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.ProcurementTenderDocumentIssues
WHERE TenantId=@tenantId AND TenderControlId=@controlId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;controlId=[Guid]$statutoryControl.Id})
    $statutoryReceiptCount = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.ProcurementTenderSubmissionReceipts
WHERE TenantId=@tenantId AND TenderControlId=@controlId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;controlId=[Guid]$statutoryControl.Id})
    Assert-TenderFixtureCondition ($statutoryIssueCount -eq 2 -and $statutoryReceiptCount -eq 2) `
        'Controlled competition must retain exactly two document issues and two sealed submission receipts.'
    if ($Stage -eq 'Preflight') {
        Assert-TenderFixtureCondition ([int]$statutoryControl.Status -eq 0) `
            'Preflight statutory control must remain Advertised with sealed submissions.'
    }

    $scenarioTenderIds = @($supplierTenderId,$competitionTenderId,$absentTenderId,$draftTenderId) |
        Sort-Object -Unique
    Assert-TenderFixtureCondition ($scenarioTenderIds.Count -eq 4) `
        'Supplier lifecycle, competition, absent-committee, and draft-committee scenarios must use distinct tenders.'

    if (Test-StageAtLeast 'AfterBid') {
        $resultSourceRequisitionId = Require-Guid $result.records 'sourcePurchaseRequisitionId' 'result.records'
        Assert-TenderFixtureCondition ($resultSourceRequisitionId -eq $sourceRequisitionId) `
            'Browser result source requisition does not match the fixture source.'
        $prePublicationTenderId = Require-Guid $result.records 'prePublicationTenderId' 'result.records'
        Assert-TenderFixtureCondition ($prePublicationTenderId -notin $scenarioTenderIds) `
            'The PR-to-tender browser path reused a seeded scenario instead of creating a new tender.'
        $prePublicationTender = Read-Tender -Id $prePublicationTenderId `
            -Number ([string]$result.records.prePublicationTenderNumber) -TenantId $tenantId `
            -Label 'Browser-created pre-publication tender'
        Assert-TenderFixtureCondition ([Guid]$prePublicationTender.SourcePurchaseRequisitionId -eq $sourceRequisitionId) `
            'Browser-created tender is not linked to the approved source requisition.'
        Assert-TenderFixtureCondition ([string]$prePublicationTender.Status -eq 'Published') `
            'Browser-created tender did not complete independent approval and publication.'
        $prePublicationItems = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.TenderItems
WHERE TenantId=@tenantId AND TenderId=@tenderId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;tenderId=$prePublicationTenderId})
        Assert-TenderFixtureCondition ($prePublicationItems -ge $manifest.prePublication.expectedItemDescriptions.Count) `
            'Browser-created tender did not retain all expected approved requisition lines.'
        $publicationSupplierId = Require-Guid $manifest.prePublication.publication.invitedSupplier `
            'businessPartnerId' 'prePublication.publication.invitedSupplier'
        $publicationEvidence = Require-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT r.Id AS RegisterId,i.Id AS IssuanceId,ti.Id AS InvitationId,c.Id AS ControlId,
       t.SubmissionDeadline,t.OpeningDate,
       r.OriginalSubmissionDeadlineUtc,r.OpeningScheduledAtUtc,r.IntegrityHash AS RegisterIntegrityHash,
       c.Status AS ControlStatus,c.AdvertisementReference,c.PublicationChannel,
       c.TenderDocumentReference,c.TenderDocumentVersion,c.AdvertisementEvidenceReference,
       c.SubmissionDeadlineUtc AS ControlSubmissionDeadlineUtc,
       c.OpeningScheduledAtUtc AS ControlOpeningScheduledAtUtc,c.IntegrityHash AS ControlIntegrityHash
FROM dbo.Tenders t
JOIN dbo.ProcurementTenderDocumentRegisters r
  ON r.TenderId=t.Id AND r.TenantId=t.TenantId AND r.IsDeleted=0
JOIN dbo.ProcurementTenderDocumentIssuances i
  ON i.RegisterId=r.Id AND i.TenantId=r.TenantId AND i.BusinessPartnerId=@supplierId AND i.IsDeleted=0
JOIN dbo.TenderInvitations ti
  ON ti.TenderId=t.Id AND ti.TenantId=t.TenantId AND ti.BusinessPartnerId=@supplierId AND ti.IsDeleted=0
JOIN dbo.ProcurementTenderControls c
  ON c.TenderId=t.Id AND c.TenantId=t.TenantId AND c.IsDeleted=0
WHERE t.Id=@tenderId AND t.TenantId=@tenantId AND t.IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;tenderId=$prePublicationTenderId;supplierId=$publicationSupplierId}) `
            'Browser-created tender publication must retain one controlled register, supplier issuance, invitation, and statutory control.'
        Assert-TenderFixtureCondition (
            [string]$publicationEvidence.AdvertisementReference -eq [string]$manifest.prePublication.publication.advertisementReference -and
            [string]$publicationEvidence.PublicationChannel -eq [string]$manifest.prePublication.publication.publicationChannel -and
            [string]$publicationEvidence.AdvertisementEvidenceReference -eq [string]$manifest.prePublication.publication.advertisementEvidenceReference) `
            'Browser-created tender statutory publication references do not match the browser request.'
        Assert-TenderFixtureCondition (
            [int]$publicationEvidence.ControlStatus -eq 0 -and
            -not [string]::IsNullOrWhiteSpace([string]$publicationEvidence.TenderDocumentReference) -and
            -not [string]::IsNullOrWhiteSpace([string]$publicationEvidence.TenderDocumentVersion) -and
            ([string]$publicationEvidence.RegisterIntegrityHash).Length -eq 64 -and
            ([string]$publicationEvidence.ControlIntegrityHash).Length -eq 64) `
            'Browser-created tender lacks an authoritative advertised control or immutable controlled-document lineage.'
        Assert-TenderFixtureCondition (
            [DateTime]$publicationEvidence.OriginalSubmissionDeadlineUtc -eq [DateTime]$publicationEvidence.SubmissionDeadline -and
            [DateTime]$publicationEvidence.OpeningScheduledAtUtc -eq [DateTime]$publicationEvidence.OpeningDate -and
            [DateTime]$publicationEvidence.ControlSubmissionDeadlineUtc -eq [DateTime]$publicationEvidence.SubmissionDeadline -and
            [DateTime]$publicationEvidence.ControlOpeningScheduledAtUtc -eq [DateTime]$publicationEvidence.OpeningDate) `
            'Browser-created tender dates diverged across the tender, document register, and statutory control.'

        $supplierBidId = Require-Guid $result.records 'supplierBidId' 'result.records'
        $supplierBidNumber = [string](Get-TenderFixtureObjectValue $result.records 'supplierBidNumber')
        $supplierBid = Read-Bid -Id $supplierBidId -TenderId $supplierTenderId -Number $supplierBidNumber -TenantId $tenantId -Label 'Supplier lifecycle bid'
        Assert-TenderFixtureCondition ([string]$supplierBid.Status -in @('Submitted','Opened','UnderEvaluation','Accepted','Rejected')) 'Supplier lifecycle bid was not submitted.'
        Assert-TenderFixtureCondition ([decimal]$supplierBid.TotalBidAmount -gt 0) 'Supplier lifecycle bid total was not persisted.'
        $bidLines = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.TenderBidItems WHERE TenantId=@tenantId AND TenderBidId=@bidId AND IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;bidId=$supplierBidId})
        Assert-TenderFixtureCondition ($bidLines -gt 0) 'Supplier lifecycle bid has no persisted bid lines.'
        $supplierDocumentLineage = Require-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT r.Id AS RegisterId,i.Id AS IssuanceId,c.Id AS ControlId,di.Id AS StatutoryIssueId,
       sr.Id AS SubmissionReceiptId
FROM dbo.ProcurementTenderDocumentRegisters r
JOIN dbo.ProcurementTenderDocumentIssuances i
  ON i.RegisterId=r.Id AND i.TenantId=r.TenantId AND i.IsDeleted=0
JOIN dbo.ProcurementTenderControls c
  ON c.TenderId=r.TenderId AND c.TenantId=r.TenantId AND c.IsDeleted=0
JOIN dbo.ProcurementTenderDocumentIssues di
  ON di.TenderControlId=c.Id AND di.BusinessPartnerId=i.BusinessPartnerId
 AND di.TenantId=c.TenantId AND di.IsDeleted=0
JOIN dbo.ProcurementTenderSubmissionReceipts sr
  ON sr.TenderControlId=c.Id AND sr.TenderBidId=@bidId
 AND sr.BusinessPartnerId=i.BusinessPartnerId AND sr.TenantId=c.TenantId AND sr.IsDeleted=0
WHERE r.TenantId=@tenantId AND r.TenderId=@tenderId AND r.IsDeleted=0;
'@ -Parameters @{tenantId=$tenantId;tenderId=$supplierTenderId;bidId=$supplierBidId}) `
            'Supplier submission does not retain one exact central-document issuance and statutory sealed receipt.'
        Assert-TenderFixtureCondition (
            $supplierDocumentLineage.RegisterId -ne [DBNull]::Value -and
            $supplierDocumentLineage.IssuanceId -ne [DBNull]::Value -and
            $supplierDocumentLineage.ControlId -ne [DBNull]::Value -and
            $supplierDocumentLineage.StatutoryIssueId -ne [DBNull]::Value -and
            $supplierDocumentLineage.SubmissionReceiptId -ne [DBNull]::Value) `
            'Supplier controlled-document or sealed-submission lineage contains an empty key.'
    }

    if (Test-StageAtLeast 'AfterOpening') {
        $supplierBidId = Require-Guid $result.records 'supplierBidId' 'result.records'
        $supplierPayment = Require-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT p.Id,p.Status,p.VerifiedDate,p.VerifiedById,p.PostingEventId,p.JournalEntryId,p.PostedAtUtc
FROM dbo.TenderPayments p
JOIN dbo.TenderFees f
  ON f.Id=p.TenderFeeId AND f.TenantId=p.TenantId AND f.IsDeleted=0
JOIN dbo.TenderBids b
  ON b.TenderId=f.TenderId AND b.BusinessPartnerId=p.BusinessPartnerId
 AND b.TenantId=p.TenantId AND b.IsDeleted=0
WHERE p.TenantId=@tenantId AND b.Id=@bidId
  AND p.PaymentReference=@paymentReference AND p.IsDeleted=0;
'@ -Parameters @{
            tenantId=$tenantId
            bidId=$supplierBidId
            paymentReference=[string]$manifest.supplierLifecycle.paymentReference
        }) 'Supplier tender-fee payment was not persisted exactly once.'
        Assert-TenderFixtureCondition ([string]$supplierPayment.Status -eq 'Completed') `
            'Supplier tender-fee payment was not verified as Completed.'
        Assert-TenderFixtureCondition (
            $supplierPayment.VerifiedDate -ne [DBNull]::Value -and
            $supplierPayment.VerifiedById -ne [DBNull]::Value) `
            'Supplier tender-fee payment has no verifier or verification timestamp.'
        Assert-TenderFixtureCondition (
            $supplierPayment.PostingEventId -ne [DBNull]::Value -and
            $supplierPayment.JournalEntryId -ne [DBNull]::Value -and
            $supplierPayment.PostedAtUtc -ne [DBNull]::Value) `
            'Verified tender-fee payment has incomplete Finance posting lineage.'

        foreach ($bid in @($firstBid,$secondBid)) {
            Assert-TenderFixtureCondition ([string]$bid.Status -in @('Opened','UnderEvaluation','Accepted','Rejected')) 'A competition bid was not formally opened.'
            Assert-TenderFixtureCondition ($bid.OpenedDate -ne [DBNull]::Value) 'A competition bid has no opening timestamp.'
        }
        Assert-TenderFixtureCondition (
            [int]$statutoryControl.Status -ge 1 -and
            $statutoryControl.OpenedAtUtc -ne [DBNull]::Value -and
            -not [string]::IsNullOrWhiteSpace([string]$statutoryControl.OpeningSnapshotJson) -and
            ([string]$statutoryControl.OpeningIntegrityHash).Length -eq 64) `
            'Controlled public opening was not retained immutably.'
        Assert-PassedCheckpoint 'formal-opening-complete'
    } else {
        foreach ($bid in @($firstBid,$secondBid)) {
            Assert-TenderFixtureCondition ([string]$bid.Status -eq 'Submitted') 'Preflight competition bids must remain sealed Submitted records.'
        }
    }

    if (Test-StageAtLeast 'AfterEvaluation') {
        foreach ($phase in @(0,1)) {
            $lockedSheets = [int](Invoke-TenderFixtureScalar -Connection $connection -Sql @'
SELECT COUNT(1) FROM dbo.ProcurementEvaluationScoreSheets
WHERE TenantId=@tenantId AND CommitteeControlId=@controlId
  AND ScoreSubjectType=N'ProcurementTenderControl' AND ScoreSubjectId=@tenderId
  AND Phase=@phase AND Status=0 AND IsDeleted=0;
'@ -Parameters @{
                tenantId=$tenantId;controlId=[Guid]$activeControl.Id
                tenderId=$competitionTenderId;phase=$phase
            })
            Assert-TenderFixtureCondition ($lockedSheets -eq 1) `
                "Controlled competition must retain exactly one current locked $(@('technical','financial')[$phase]) score sheet."
        }
        Assert-TenderFixtureCondition (
            [int]$statutoryControl.Status -ge 4 -and
            $statutoryControl.TechnicalEvaluatedAtUtc -ne [DBNull]::Value -and
            ([string]$statutoryControl.TechnicalEvaluationIntegrityHash).Length -eq 64 -and
            $statutoryControl.FinancialEvaluatedAtUtc -ne [DBNull]::Value -and
            ([string]$statutoryControl.FinancialEvaluationIntegrityHash).Length -eq 64 -and
            [Guid]$statutoryControl.RecommendedBidId -eq $firstBidId -and
            $statutoryControl.WorkflowInstanceId -ne [DBNull]::Value -and
            $statutoryControl.SubmittedForApprovalAtUtc -ne [DBNull]::Value -and
            $statutoryControl.SubmittedForApprovalById -ne [DBNull]::Value) `
            'Controlled technical, financial, recommendation, or authority-workflow evidence is incomplete.'
        foreach ($checkpoint in @(
            'controlled-technical-evaluation',
            'controlled-financial-recommendation',
            'controlled-authority-workflow-submitted',
            'controlled-evaluation-retry-denied')) {
            Assert-PassedCheckpoint $checkpoint
        }
    }

    if (Test-StageAtLeast 'AfterAward') {
        Assert-TenderFixtureCondition (
            [int]$statutoryControl.Status -ge 7 -and
            $statutoryControl.ApprovedAtUtc -ne [DBNull]::Value -and
            $statutoryControl.ApprovedById -ne [DBNull]::Value -and
            [Guid]$statutoryControl.SubmittedForApprovalById -ne [Guid]$statutoryControl.ApprovedById -and
            [Guid]$statutoryControl.AwardBidId -eq $firstBidId -and
            -not [string]::IsNullOrWhiteSpace([string]$statutoryControl.AwardReference) -and
            -not [string]::IsNullOrWhiteSpace([string]$statutoryControl.AwardEvidenceReference) -and
            $statutoryControl.AwardedAtUtc -ne [DBNull]::Value) `
            'Independent approval or controlled award evidence is incomplete.'
        foreach ($checkpoint in @(
            'controlled-authority-approved',
            'controlled-award-readiness-current',
            'controlled-award-recorded')) {
            Assert-PassedCheckpoint $checkpoint
        }
    }

    if (Test-StageAtLeast 'AfterHandoff') {
        Assert-TenderFixtureCondition (
            [int]$statutoryControl.Status -ge 8 -and
            -not [string]::IsNullOrWhiteSpace([string]$statutoryControl.ContractReference) -and
            -not [string]::IsNullOrWhiteSpace([string]$statutoryControl.ContractEvidenceReference) -and
            $statutoryControl.ContractedAtUtc -ne [DBNull]::Value) `
            'Controlled executed-contract evidence was not retained.'
        Assert-PassedCheckpoint 'controlled-contract-recorded'
    }

    if ($Stage -eq 'Complete') {
        Assert-TenderFixtureCondition (
            [int]$statutoryControl.Status -eq 9 -and
            -not [string]::IsNullOrWhiteSpace([string]$statutoryControl.BidderAcceptanceReference) -and
            -not [string]::IsNullOrWhiteSpace([string]$statutoryControl.BidderAcceptanceEvidenceReference) -and
            $statutoryControl.AcceptedAtUtc -ne [DBNull]::Value) `
            'Successful-bidder acceptance did not complete the statutory tender control.'
        Assert-PassedCheckpoint 'controlled-bidder-acceptance'
        Assert-PassedCheckpoint 'controlled-lifecycle-refresh'
        foreach ($checkpoint in @(
            'actor-isolation',
            'pr-tender-draft-created',
            'tender-draft-reopened',
            'tender-independent-approval',
            'supplier-bid-submitted',
            'payment-verified-premature-opening-denied',
            'committee-absent-gate',
            'committee-draft-gate',
            'formal-opening-complete',
            'controlled-technical-evaluation',
            'second-supplier-bid-visible',
            'controlled-financial-recommendation',
            'controlled-authority-workflow-submitted',
            'tender-amendment',
            'controlled-evaluation-retry-denied',
            'unauthorized-evaluation-server-denied',
            'unauthorized-api-nonmutation',
            'controlled-authority-approved',
            'controlled-award-readiness-current',
            'controlled-award-recorded',
            'controlled-contract-recorded')) {
            Assert-PassedCheckpoint $checkpoint
        }

        $crossTenantTenderId = Require-Guid $manifest.regression 'crossTenantTenderId' 'regression'
        $foreignTender = Require-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT t.Id,t.TenantId,t.Status,t.Title,t.UpdatedAt,
       (SELECT COUNT(1) FROM dbo.TenderRevisions r
        WHERE r.TenderId=t.Id AND r.TenantId=t.TenantId AND r.IsDeleted=0) AS RevisionCount
FROM dbo.Tenders t
WHERE t.Id=@id AND t.TenantId<>@tenantId AND t.IsDeleted=0;
'@ -Parameters @{id=$crossTenantTenderId;tenantId=$tenantId}) `
            'The real foreign-tenant Tender isolation fixture is missing.'
        Assert-TenderFixtureCondition ([string]$foreignTender.Status -eq 'Draft') `
            'The foreign-tenant Tender status changed during the isolation probes.'
        Assert-TenderFixtureCondition ([int]$foreignTender.RevisionCount -eq 0) `
            'A cross-tenant Tender revision mutation was persisted.'
        Assert-PassedCheckpoint 'cross-tenant-api-isolation'

        $alternateAwardId = Require-Guid $result.records 'awardId' 'records'
        Assert-TenderFixtureCondition ([string]$manifest.handoff.alternateMode -eq 'PO') `
            'This controlled-contract lifecycle requires a direct-PO bypass denial probe.'
        $directPoState = Require-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT a.Id AS AwardId,a.Status AS AwardStatus,a.PurchaseOrderId,
       (SELECT COUNT(1) FROM dbo.PurchaseOrders po
        WHERE po.TenantId=a.TenantId AND po.IsDeleted=0
          AND po.ProcurementSourceType=1 AND po.ProcurementSourceId=a.Id) AS DirectPoCount
FROM dbo.TenderAwards a
WHERE a.Id=@awardId AND a.TenantId=@tenantId AND a.IsDeleted=0;
'@ -Parameters @{awardId=$alternateAwardId;tenantId=$tenantId}) `
            'The independently approved controlled award for the direct-PO denial probe is missing.'
        Assert-TenderFixtureCondition (
            [string]$directPoState.AwardStatus -in @('Awarded','ContractSigned') -and
            $directPoState.PurchaseOrderId -eq [DBNull]::Value -and
            [int]$directPoState.DirectPoCount -eq 0) `
            'A controlled tender award bypassed the required contract lifecycle and created a direct PO.'
        Assert-PassedCheckpoint 'controlled-direct-po-bypass-denied'

        $contractAwardId = Require-Guid $result.records 'awardId' 'records'
        $contractId = Require-Guid $result.records 'contractId' 'records'
        $contractActivationId = Require-Guid $result.records 'contractActivationId' 'records'
        $contractHandoff = Require-One (Invoke-TenderFixtureQuery -Connection $connection -Sql @'
SELECT c.Id,c.Status,c.TenderAwardId,c.TenderId,c.TenderBidId,c.BusinessPartnerId,
       c.ContractValue,c.Currency,c.ActivatedAt,a.AwardedAmount,a.Currency AS AwardCurrency,
       ca.Id AS ActivationId,ca.Status AS ActivationStatus,ca.WorkflowDefinitionId,
       ca.WorkflowInstanceId,ca.AwardReadinessDecisionId,ca.SubmittedById,ca.DecidedById,
       ca.ActivatedById,ca.DecidedAtUtc,ca.ActivatedAtUtc,ca.ContractSnapshotJson,
       ca.ContractSnapshotHash,ca.ReadinessSnapshotJson,ca.IntegrityHash,
       wi.Status AS WorkflowStatus
FROM dbo.Contracts c
JOIN dbo.TenderAwards a ON a.Id=c.TenderAwardId AND a.TenantId=c.TenantId AND a.IsDeleted=0
JOIN dbo.ProcurementContractActivations ca ON ca.Id=@activationId
  AND ca.ContractId=c.Id AND ca.TenantId=c.TenantId AND ca.IsDeleted=0
JOIN dbo.WorkflowInstances wi ON wi.Id=ca.WorkflowInstanceId
  AND wi.TenantId=ca.TenantId AND wi.IsDeleted=0
WHERE c.Id=@contractId AND c.TenderAwardId=@awardId
  AND c.TenantId=@tenantId AND c.IsDeleted=0;
'@ -Parameters @{
            tenantId=$tenantId;awardId=$contractAwardId
            contractId=$contractId;activationId=$contractActivationId
        }) 'The real TenderAward-to-Contract activation output is missing.'
        Assert-TenderFixtureCondition (
            [string]$contractHandoff.Status -eq 'Active' -and
            [int]$contractHandoff.ActivationStatus -eq 3 -and
            [decimal]$contractHandoff.ContractValue -eq [decimal]$contractHandoff.AwardedAmount -and
            [string]$contractHandoff.Currency -eq [string]$contractHandoff.AwardCurrency) `
            'The activated contract does not retain the exact approved award commercials.'
        Assert-TenderFixtureCondition (
            $contractHandoff.SubmittedById -ne [DBNull]::Value -and
            $contractHandoff.DecidedById -ne [DBNull]::Value -and
            [Guid]$contractHandoff.SubmittedById -ne [Guid]$contractHandoff.DecidedById -and
            $contractHandoff.ActivatedAtUtc -ne [DBNull]::Value -and
            ([string]$contractHandoff.ContractSnapshotHash).Length -eq 64 -and
            ([string]$contractHandoff.IntegrityHash).Length -eq 64 -and
            -not [string]::IsNullOrWhiteSpace([string]$contractHandoff.ContractSnapshotJson) -and
            -not [string]::IsNullOrWhiteSpace([string]$contractHandoff.ReadinessSnapshotJson)) `
            'The real contract lacks independent approval, workflow, activation, or immutable snapshot evidence.'
        Assert-TenderFixtureCondition ([int]$contractHandoff.WorkflowStatus -eq 2) `
            'The contract activation workflow did not complete.'
        Assert-PassedCheckpoint 'real-contract-handoff-activated'

        $pendingCoverage = [System.Collections.Generic.List[string]]::new()
        if ([string]::IsNullOrWhiteSpace([string]$manifest.handoff.alternateApprovedAwardId) -or
            [string]::IsNullOrWhiteSpace([string]$manifest.handoff.alternateMode)) {
            $pendingCoverage.Add('alternate PO/contract award handoff')
        } else {
            $alternateCheckpoint = if ([string]$manifest.handoff.alternateMode -eq 'PO') {
                'controlled-direct-po-bypass-denied'
            } else {
                'alternate-contract-handoff-created'
            }
            Assert-PassedCheckpoint $alternateCheckpoint
        }
        if ([string]::IsNullOrWhiteSpace([string]$manifest.regression.crossTenantTenderId)) {
            $pendingCoverage.Add('cross-tenant tender isolation')
        } else {
            Assert-PassedCheckpoint 'cross-tenant-api-isolation'
        }
        Assert-TenderFixtureCondition ($pendingCoverage.Count -eq 0) `
            "Lifecycle coverage remains Pending: $($pendingCoverage -join '; '). Complete verification cannot claim a pass."
        Assert-TenderFixtureCondition ([string]$result.status -eq 'Passed') 'Lifecycle result is not marked Passed.'
        Assert-TenderFixtureCondition ($result.checkpoints.PSObject.Properties.Count -gt 0) 'Lifecycle result contains no passed checkpoints.'
        foreach ($checkpoint in $result.checkpoints.PSObject.Properties) {
            Assert-TenderFixtureCondition ([string]$checkpoint.Value -eq 'Passed') "Checkpoint '$($checkpoint.Name)' is not Passed."
        }
    }

    Write-Output "TENDER-LIFECYCLE-VERIFIED|$Stage|$($manifest.runId)"
} finally {
    $connection.Dispose()
}
