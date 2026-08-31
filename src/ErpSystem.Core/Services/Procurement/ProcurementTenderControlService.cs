using ErpSystem.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementTenderControlService : IProcurementTenderControlService
{
    private const string SourceType = "Tender";
    private const string ApprovalSourceType = "TenderAward";
    private const string ManagePermission = "procurement.tender.administer";
    private const string EvaluatePermission = "procurement.tender.evaluate";
    private const string ApprovePermission = "procurement.tender.approve";
    private const string ContractPermission = "procurement.contract.manage";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IProcurementSourcingCaseService _sourcingCases;
    private readonly IWorkflowService _workflowService;
    private readonly ISupplierValidationService _supplierValidation;
    private readonly IProcurementTenderDocumentControlService _tenderDocumentControlService;
    private readonly IProcurementEvaluationCommitteeControlService _evaluationCommittee;
    private readonly IProcurementAwardReadinessService _awardReadiness;

    public ProcurementTenderControlService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IProcurementSourcingCaseService sourcingCases,
        IWorkflowService workflowService,
        ISupplierValidationService supplierValidation,
        IProcurementTenderDocumentControlService tenderDocumentControlService,
        IProcurementEvaluationCommitteeControlService evaluationCommittee,
        IProcurementAwardReadinessService awardReadiness)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _sourcingCases = sourcingCases;
        _workflowService = workflowService;
        _supplierValidation = supplierValidation;
        _tenderDocumentControlService = tenderDocumentControlService;
        _evaluationCommittee = evaluationCommittee;
        _awardReadiness = awardReadiness;
    }

    private IGenericRepository<Tender> Tenders => _unitOfWork.Repository<Tender>();
    private IGenericRepository<TenderBid> Bids => _unitOfWork.Repository<TenderBid>();
    private IGenericRepository<ProcurementSourcingCase> Cases => _unitOfWork.Repository<ProcurementSourcingCase>();
    private IGenericRepository<ProcurementPolicyMethodRule> MethodRules => _unitOfWork.Repository<ProcurementPolicyMethodRule>();
    private IGenericRepository<ProcurementTenderControl> Controls => _unitOfWork.Repository<ProcurementTenderControl>();
    private IGenericRepository<ProcurementTenderDocumentIssue> Issues => _unitOfWork.Repository<ProcurementTenderDocumentIssue>();
    private IGenericRepository<ProcurementTenderSubmissionReceipt> Receipts => _unitOfWork.Repository<ProcurementTenderSubmissionReceipt>();
    private IGenericRepository<TenderFee> TenderFees => _unitOfWork.Repository<TenderFee>();
    private IGenericRepository<TenderPayment> TenderPayments => _unitOfWork.Repository<TenderPayment>();

    public async Task<bool> IsNctOrIctAsync(Guid tenderId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        return await Tenders.GetQueryable(item => item.Id == tenderId && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Where(item => item.SourcingCaseId.HasValue)
            .Join(Cases.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted),
                tender => tender.SourcingCaseId, sourcingCase => sourcingCase.Id, (_, sourcingCase) => sourcingCase.SelectedMethod)
            .AnyAsync(method => method == ProcurementMethodType.NationalCompetitiveTendering ||
                                method == ProcurementMethodType.InternationalCompetitiveTendering, cancellationToken);
    }

    public async Task<bool> IsControlledTenderMethodAsync(Guid tenderId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        return await Tenders.GetQueryable(item => item.Id == tenderId && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Where(item => item.SourcingCaseId.HasValue)
            .Join(Cases.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted),
                tender => tender.SourcingCaseId, sourcingCase => sourcingCase.Id, (_, sourcingCase) => sourcingCase.SelectedMethod)
            .AnyAsync(method => method == ProcurementMethodType.NationalCompetitiveTendering ||
                                method == ProcurementMethodType.InternationalCompetitiveTendering ||
                                method == ProcurementMethodType.QualityBasedSelection ||
                                method == ProcurementMethodType.QualityAndCostBasedSelection, cancellationToken);
    }

    public async Task<bool> ShouldConcealFinancialProposalAsync(
        Guid tenderId,
        Guid bidId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var state = await Controls.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.TenderId == tenderId && !item.IsDeleted)
            .AsNoTracking()
            .Select(item => new
            {
                item.Method,
                item.Status,
                item.RecommendedBidId,
                item.TechnicalEvaluationSnapshotJson
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            var selectedMethod = await Tenders.GetQueryable(item => item.Id == tenderId && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .Where(item => item.SourcingCaseId.HasValue)
                .Join(Cases.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted),
                    tender => tender.SourcingCaseId,
                    sourcingCase => sourcingCase.Id,
                    (_, sourcingCase) => (ProcurementMethodType?)sourcingCase.SelectedMethod)
                .SingleOrDefaultAsync(cancellationToken);

            return selectedMethod.HasValue && IsQualitySelection(selectedMethod.Value);
        }

        if (!IsQualitySelection(state.Method)) return false;
        if (state.Status < ProcurementTenderControlStatus.FinancialEvaluated) return true;
        if (state.Method == ProcurementMethodType.QualityBasedSelection)
            return state.RecommendedBidId != bidId;
        if (string.IsNullOrWhiteSpace(state.TechnicalEvaluationSnapshotJson)) return true;
        try
        {
            using var document = JsonDocument.Parse(state.TechnicalEvaluationSnapshotJson);
            if (!document.RootElement.TryGetProperty("scores", out var scores) ||
                scores.ValueKind != JsonValueKind.Array)
                return true;

            foreach (var item in scores.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("bidId", out var bidIdValue) ||
                    !bidIdValue.TryGetGuid(out var evaluatedBidId) ||
                    !item.TryGetProperty("qualified", out var qualifiedValue) ||
                    (qualifiedValue.ValueKind != JsonValueKind.True && qualifiedValue.ValueKind != JsonValueKind.False))
                    continue;

                if (evaluatedBidId == bidId)
                    return !qualifiedValue.GetBoolean();
            }

            return true;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    public async Task<ProcurementTenderControlDto> GetAsync(Guid tenderId, CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var control = await LoadControlAsync(tenderId, tracked: false, cancellationToken);
        return Map(control);
    }

    public async Task<ProcurementTenderControlDto> PublishAsync(
        Guid tenderId,
        PublishProcurementTenderRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var tender = await LoadTenderAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, tender.TenderNumber, correlation, cancellationToken);
        var lineage = await RevalidateAsync(tender, correlation, cancellationToken);
        if (!lineage.Case.AuthorityRouteId.HasValue || string.IsNullOrWhiteSpace(lineage.Case.AuthorityRouteReference))
            throw Validation("TENDER_ADVANCED_AUTHORITY_ROUTE_REQUIRED",
                "This statutory tender-control stage requires an advanced authority route. The sourcing case remains valid for the standard approved-PR tender workflow.");
        if (!string.Equals(tender.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            throw Conflict("TENDER_APPROVAL_REQUIRED", "The tender must complete its document approval workflow before advertisement.");
        if (lineage.Case.SelectedMethod == ProcurementMethodType.QualityAndCostBasedSelection)
        {
            if (!tender.UseQCBSEvaluation)
                throw Validation("QCBS_CONFIGURATION_REQUIRED", "A QCBS sourcing case must use the controlled QCBS evaluation configuration.");
            EnsureQcbsWeights(tender);
        }
        else if (lineage.Case.SelectedMethod == ProcurementMethodType.QualityBasedSelection)
        {
            if (tender.UseQCBSEvaluation)
                throw Validation("QBS_CONFIGURATION_CONFLICT", "A QBS sourcing case cannot enable the QCBS price-weighted evaluation mode.");
            EnsureTechnicalThreshold(tender);
        }
        if (await Controls.ExistsAsync(item => item.TenantId == _currentUser.TenantId && item.TenderId == tenderId && !item.IsDeleted))
            throw Conflict("TENDER_ALREADY_ADVERTISED", "The statutory advertisement record already exists.");
        var deadline = EnsureUtc(request.SubmissionDeadlineUtc);
        var opening = EnsureUtc(request.OpeningScheduledAtUtc);
        if (deadline <= DateTime.UtcNow)
            throw Validation("TENDER_DEADLINE_INVALID", "The submission deadline must be in the future.");
        if (opening < deadline)
            throw Validation("TENDER_OPENING_INVALID", "The public opening cannot be scheduled before the submission deadline.");
        var effectiveDocument = await _tenderDocumentControlService.EnsurePublicationReadyAsync(
            ProcurementTenderDocumentSourceType.Tender, tenderId, deadline, correlation, cancellationToken);
        var documentRegister = await _tenderDocumentControlService.GetRegisterAsync(
            ProcurementTenderDocumentSourceType.Tender, tenderId, cancellationToken);
        if (request.DocumentFee != documentRegister.FeeAmount)
            throw Validation("TENDER_DOCUMENT_FEE_MISMATCH",
                $"The exact controlled document fee is {documentRegister.FeeAmount:0.00} {documentRegister.CurrencyCode}.");
        Require(request.AdvertisementReference, "TENDER_ADVERTISEMENT_REFERENCE_REQUIRED", "Advertisement reference is required.");
        Require(request.PublicationChannel, "TENDER_PUBLICATION_CHANNEL_REQUIRED", "Publication channel is required.");
        Require(request.AdvertisementEvidenceReference, "TENDER_ADVERTISEMENT_EVIDENCE_REQUIRED", "Advertisement evidence is required.");
        if (!lineage.MethodRule.WorkflowDefinitionId.HasValue)
            throw Validation("TENDER_AWARD_WORKFLOW_REQUIRED", "The locked controlled method rule must select the shared award-approval workflow.");

        var now = DateTime.UtcNow;
        var control = new ProcurementTenderControl
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            TenderId = tender.Id,
            SourcingCaseId = lineage.Case.Id,
            MethodRuleId = lineage.MethodRule.Id,
            AuthorityRouteId = lineage.Case.AuthorityRouteId.Value,
            Method = lineage.Case.SelectedMethod,
            MethodRuleCode = lineage.MethodRule.RuleCode,
            AuthorityRouteReference = lineage.Case.AuthorityRouteReference,
            Status = ProcurementTenderControlStatus.Advertised,
            AdvertisementReference = request.AdvertisementReference.Trim(),
            PublicationChannel = request.PublicationChannel.Trim(),
            TenderDocumentReference = effectiveDocument.EffectiveTemplateReference,
            TenderDocumentVersion = effectiveDocument.EffectiveTemplateVersionId.ToString("D"),
            DocumentFee = documentRegister.FeeAmount,
            AdvertisementEvidenceReference = request.AdvertisementEvidenceReference.Trim(),
            AdvertisedAtUtc = now,
            SubmissionDeadlineUtc = deadline,
            OpeningScheduledAtUtc = opening,
            WorkflowDefinitionId = lineage.MethodRule.WorkflowDefinitionId,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        Capture(control);
        await Controls.AddAsync(control);
        tender.Status = "Published";
        tender.PublishDate = now;
        tender.SubmissionDeadline = deadline;
        tender.OpeningDate = opening;
        tender.PublishedById = _currentUser.UserId;
        tender.UpdatedAt = now;
        tender.LastModifiedById = _currentUser.UserId;
        await Tenders.UpdateAsync(tender);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "TenderAdvertised", ProcurementControlEventResult.Allowed,
            new
            {
                request.AdvertisementReference,
                request.PublicationChannel,
                RequestedTenderDocumentReference = request.TenderDocumentReference,
                RequestedTenderDocumentVersion = request.TenderDocumentVersion,
                RequestedDocumentFee = request.DocumentFee,
                ControlledTemplateReference = effectiveDocument.EffectiveTemplateReference,
                ControlledTemplateVersionId = effectiveDocument.EffectiveTemplateVersionId,
                ControlledDocumentFee = documentRegister.FeeAmount
            },
            new { control.Id, control.Method, control.SubmissionDeadlineUtc, control.OpeningScheduledAtUtc, control.IntegrityHash },
            correlation, cancellationToken, External(control.AdvertisementEvidenceReference, "Tender advertisement", "SRC-006"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementTenderDocumentIssueDto> IssueDocumentAsync(
        Guid tenderId,
        IssueProcurementTenderDocumentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, correlation, cancellationToken);
        if (control.Status != ProcurementTenderControlStatus.Advertised || DateTime.UtcNow > control.SubmissionDeadlineUtc)
            throw Conflict("TENDER_DOCUMENT_ISSUE_CLOSED", "Tender documents can be issued only while the advertised submission window is open.");
        Require(request.RecipientName, "TENDER_DOCUMENT_RECIPIENT_REQUIRED", "Document recipient is required.");
        Require(request.IssueReceiptNumber, "TENDER_DOCUMENT_RECEIPT_REQUIRED", "An issue or sale receipt number is required.");
        Require(request.EvidenceReference, "TENDER_DOCUMENT_EVIDENCE_REQUIRED", "Document issue evidence is required.");
        if (request.AmountPaid != control.DocumentFee)
            throw Validation("TENDER_DOCUMENT_FEE_MISMATCH", $"The configured document fee is {control.DocumentFee:0.00}.");
        if (control.DocumentFee > 0 && string.IsNullOrWhiteSpace(request.PaymentReference))
            throw Validation("TENDER_DOCUMENT_PAYMENT_REQUIRED", "Paid tender documents require a payment reference.");
        if (request.BusinessPartnerId.HasValue)
        {
            var validation = await _supplierValidation.ValidateForTenderAsync(
                request.BusinessPartnerId.Value, control.Tender.RequiresPrequalification, control.Tender.MinimumPerformanceRating);
            if (!validation.IsValid)
                throw Validation("TENDER_DOCUMENT_RECIPIENT_INELIGIBLE", string.Join("; ", validation.Errors));
        }
        var activeIssue = await _tenderDocumentControlService.IssueTenderCompatibilityAsync(
            tenderId, request, correlation, cancellationToken);
        var existingIssue = await Issues.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.TenderControlId == control.Id &&
                item.IssueReceiptNumber == request.IssueReceiptNumber.Trim() && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (existingIssue is not null)
            return MapIssue(existingIssue);

        var now = DateTime.UtcNow;
        var issue = new ProcurementTenderDocumentIssue
        {
            Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, TenderControlId = control.Id,
            BusinessPartnerId = request.BusinessPartnerId, RecipientName = request.RecipientName.Trim(),
            RecipientEmail = NullIfWhiteSpace(request.RecipientEmail), RecipientPhone = NullIfWhiteSpace(request.RecipientPhone),
            AmountPaid = request.AmountPaid, PaymentReference = NullIfWhiteSpace(request.PaymentReference),
            IssueReceiptNumber = activeIssue.ReceiptNumber, IssuedAtUtc = activeIssue.IssuedAtUtc,
            IssuedByUserId = activeIssue.IssuedByUserId, EvidenceReference = activeIssue.EvidenceReference,
            CreatedAt = now, CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId
        };
        issue.IntegrityHash = ComputeHash(JsonSerializer.Serialize(new
        {
            issue.TenderControlId, issue.BusinessPartnerId, issue.RecipientName, issue.RecipientEmail,
            issue.RecipientPhone, issue.AmountPaid, issue.PaymentReference, issue.IssueReceiptNumber,
            issue.IssuedAtUtc, issue.IssuedByUserId, issue.EvidenceReference,
            control.TenderDocumentReference, control.TenderDocumentVersion
        }, JsonOptions));
        await Issues.AddAsync(issue);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, control.DocumentFee > 0 ? "TenderDocumentSold" : "TenderDocumentIssued",
            ProcurementControlEventResult.Allowed,
            new { issue.BusinessPartnerId, issue.RecipientName, issue.AmountPaid, issue.PaymentReference },
            new { issue.Id, issue.IssueReceiptNumber, issue.IntegrityHash }, correlation, cancellationToken,
            External(issue.EvidenceReference, "Tender document issue or sale", "SRC-006"));
        return MapIssue(issue);
    }

    public async Task<ProcurementTenderSubmissionDisposition?> RecordSubmissionAsync(
        TenderBid bid,
        DateTime receivedAtUtc,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        if (bid.TenantId != _currentUser.TenantId)
            throw new ProcurementTenderControlAuthorizationException("The bid is outside the current tenant.");
        if (!await IsControlledTenderMethodAsync(bid.TenderId, cancellationToken)) return null;
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(bid.TenderId, tracked: true, cancellationToken);
        await RevalidateAsync(control.Tender, correlation, cancellationToken);
        if (control.Status != ProcurementTenderControlStatus.Advertised)
            throw Conflict("TENDER_SUBMISSION_WINDOW_CLOSED", "The NCT/ICT tender is not accepting sealed submissions.");
        if (await Receipts.ExistsAsync(item => item.TenantId == _currentUser.TenantId &&
                item.TenderControlId == control.Id && item.TenderBidId == bid.Id && !item.IsDeleted))
            throw Conflict("TENDER_SUBMISSION_DUPLICATE", "This bid already has a statutory receipt.");
        if (!await Issues.ExistsAsync(item => item.TenantId == _currentUser.TenantId &&
                item.TenderControlId == control.Id && item.BusinessPartnerId == bid.BusinessPartnerId && !item.IsDeleted))
            throw Validation("TENDER_DOCUMENT_ISSUE_REQUIRED", "The bidder must have a controlled tender-document issue or sale record.");
        var validation = await _supplierValidation.ValidateForTenderAsync(
            bid.BusinessPartnerId, control.Tender.RequiresPrequalification, control.Tender.MinimumPerformanceRating);
        if (!validation.IsValid)
            throw Validation("TENDER_BIDDER_INELIGIBLE", string.Join("; ", validation.Errors));
        var received = EnsureUtc(receivedAtUtc);
        var disposition = received <= control.SubmissionDeadlineUtc
            ? ProcurementTenderSubmissionDisposition.OnTimeAccepted
            : ProcurementTenderSubmissionDisposition.LateRejected;
        var sequence = await Receipts.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId && item.TenderControlId == control.Id)
            .CountAsync(cancellationToken) + 1;
        var snapshot = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.nct-ict-sealed-bid.v1",
            bid.Id, bid.BidNumber, bid.TenderId, bid.BusinessPartnerId, bid.TotalBidAmount, bid.Currency,
            receivedAtUtc = received, control.SubmissionDeadlineUtc,
            items = bid.Items.Where(item => !item.IsDeleted).OrderBy(item => item.TenderItemId)
                .Select(item => new { item.TenderItemId, item.OfferedQuantity, item.UnitPrice, item.TotalPrice }).ToArray(),
            documents = bid.Documents.Where(item => !item.IsDeleted).OrderBy(item => item.DocumentName)
                .Select(item => new { item.DocumentName, item.DocumentType, item.FilePath, item.FileSize }).ToArray()
        }, JsonOptions);
        var receipt = new ProcurementTenderSubmissionReceipt
        {
            Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, TenderControlId = control.Id,
            TenderBidId = bid.Id, BusinessPartnerId = bid.BusinessPartnerId,
            ReceiptNumber = $"{control.Tender.TenderNumber}-B{sequence:0000}", ReceivedAtUtc = received,
            SubmissionDeadlineUtc = control.SubmissionDeadlineUtc, Disposition = disposition,
            SealedSnapshotJson = snapshot, IntegrityHash = ComputeHash(snapshot),
            CreatedAt = received, CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId
        };
        await Receipts.AddAsync(receipt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control,
            disposition == ProcurementTenderSubmissionDisposition.OnTimeAccepted ? "TenderBidReceivedAndSealed" : "LateTenderBidRejected",
            disposition == ProcurementTenderSubmissionDisposition.OnTimeAccepted ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Rejected,
            new { bid.Id, bid.BusinessPartnerId, receivedAtUtc = received },
            new { receipt.Id, receipt.ReceiptNumber, receipt.Disposition, receipt.IntegrityHash },
            correlation, cancellationToken, External(receipt.ReceiptNumber, "Sealed tender submission receipt", "SRC-003"));
        return disposition;
    }

    public async Task<ProcurementTenderControlDto> CompleteOpeningAsync(
        Guid tenderId,
        CompleteProcurementTenderOpeningRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, control.Tender.TenderNumber, correlation, cancellationToken);
        var lineage = await RevalidateAsync(control.Tender, correlation, cancellationToken);
        EnsureStatus(control, ProcurementTenderControlStatus.Advertised, "TENDER_OPENING_NOT_READY");
        if (DateTime.UtcNow < control.SubmissionDeadlineUtc)
            throw Conflict("TENDER_OPENING_BEFORE_DEADLINE", "Public opening cannot start before the submission deadline.");
        var onTime = control.SubmissionReceipts.Where(item => !item.IsDeleted &&
            item.Disposition == ProcurementTenderSubmissionDisposition.OnTimeAccepted).OrderBy(item => item.ReceivedAtUtc).ToList();
        var paymentAdmissions = await AssessPaymentAdmissionsAsync(
            control.TenderId,
            onTime.Select(item => item.TenderBid).ToList(),
            cancellationToken);
        var pendingAdmission = paymentAdmissions.Values.FirstOrDefault(item =>
            item.BlockingStatus is TenderBidPaymentAdmissionStatus.PendingVerification or
                TenderBidPaymentAdmissionStatus.PendingProviderConfirmation);
        if (pendingAdmission is not null)
        {
            await RecordAsync(control, "TenderOpeningPaymentAdmissionDenied",
                ProcurementControlEventResult.Denied,
                new
                {
                    PendingBidIds = paymentAdmissions
                        .Where(item => !item.Value.CanOpenOrEvaluate)
                        .Select(item => item.Key)
                        .ToArray()
                },
                new { pendingAdmission.Code, pendingAdmission.Message },
                correlation, cancellationToken);
            throw Validation(pendingAdmission.Code, pendingAdmission.Message);
        }

        var excludedAdmissions = paymentAdmissions
            .Where(item => !item.Value.CanOpenOrEvaluate)
            .ToDictionary(item => item.Key, item => item.Value);
        foreach (var receipt in onTime.Where(item => excludedAdmissions.ContainsKey(item.TenderBidId)))
        {
            var admission = excludedAdmissions[receipt.TenderBidId];
            receipt.TenderBid.Status = "Rejected";
            receipt.TenderBid.RejectionReason = admission.Message;
            receipt.TenderBid.UpdatedAt = DateTime.UtcNow;
            await Bids.UpdateAsync(receipt.TenderBid);
        }
        if (excludedAdmissions.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordAsync(control, "TenderBidPaymentAdmissionExcluded",
                ProcurementControlEventResult.Rejected,
                new { BidIds = excludedAdmissions.Keys.ToArray() },
                new
                {
                    Exclusions = excludedAdmissions.Select(item => new
                    {
                        BidId = item.Key,
                        item.Value.Code,
                        item.Value.Message
                    }).ToArray()
                },
                correlation, cancellationToken);
        }

        var admitted = onTime
            .Where(item => paymentAdmissions[item.TenderBidId].CanOpenOrEvaluate)
            .ToList();
        if (lineage.MethodRule.MinimumQuotationCount <= 0 || admitted.Count < lineage.MethodRule.MinimumQuotationCount)
            throw Conflict("TENDER_MINIMUM_COMPETITION_NOT_MET",
                $"At least {lineage.MethodRule.MinimumQuotationCount} payment-admitted on-time submissions are required; {admitted.Count} are eligible for opening.");
        var participants = request.Participants.Where(item => !string.IsNullOrWhiteSpace(item.Name)).ToList();
        if (participants.Count < 2 || !participants.Any(item => item.IsObserver) || !participants.Any(item => !item.IsObserver))
            throw Validation("TENDER_OPENING_PARTICIPANTS_INVALID", "A signed opening officer and independent observer are required.");
        if (!participants.Any(item => item.UserId == _currentUser.UserId) ||
            participants.Any(item => string.IsNullOrWhiteSpace(item.Role) || string.IsNullOrWhiteSpace(item.SignatureReference)))
            throw Validation("TENDER_OPENING_SIGNATURES_REQUIRED", "Every participant must sign, and the current opening officer must be included.");
        if (participants.Where(item => item.UserId.HasValue).GroupBy(item => item.UserId).Any(group => group.Count() > 1) ||
            participants.GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw Validation("TENDER_OPENING_PARTICIPANT_DUPLICATE", "Opening participants cannot be duplicated.");
        Require(request.EvidenceReference, "TENDER_OPENING_EVIDENCE_REQUIRED", "Public-opening evidence is required.");

        var now = DateTime.UtcNow;
        var qualitySelection = IsQualitySelection(control.Method);
        var snapshot = JsonSerializer.Serialize(new
        {
            schemaVersion = qualitySelection
                ? "tdc.quality-selection-technical-opening.v1"
                : "tdc.nct-ict-public-opening.v1",
            control.TenderId, method = control.Method, openedAtUtc = now,
            evidenceReference = request.EvidenceReference.Trim(),
            participants = participants.Select(item => new { item.UserId, name = item.Name.Trim(), role = item.Role.Trim(), item.IsObserver, signatureReference = item.SignatureReference.Trim() }).ToArray(),
            entries = control.SubmissionReceipts.Where(item => !item.IsDeleted).OrderBy(item => item.ReceivedAtUtc).Select(item => new
            {
                item.TenderBidId, item.BusinessPartnerId, item.ReceiptNumber, item.ReceivedAtUtc, item.Disposition,
                paymentAdmission = paymentAdmissions.TryGetValue(item.TenderBidId, out var admission)
                    ? admission.BlockingStatus.ToString()
                    : "NotApplicable",
                declaredAmount = qualitySelection ? (decimal?)null : item.TenderBid.TotalBidAmount,
                currency = qualitySelection ? null : item.TenderBid.Currency,
                submissionIntegrityHash = item.IntegrityHash
            }).ToArray()
        }, JsonOptions);
        control.OpenedAtUtc = now;
        control.OpeningEvidenceReference = request.EvidenceReference.Trim();
        control.OpeningSnapshotJson = snapshot;
        control.OpeningIntegrityHash = ComputeHash(snapshot);
        control.Status = ProcurementTenderControlStatus.Opened;
        var admittedBidIds = admitted.Select(item => item.TenderBidId).ToHashSet();
        foreach (var receipt in control.SubmissionReceipts.Where(item => !item.IsDeleted))
        {
            var isAdmitted = admittedBidIds.Contains(receipt.TenderBidId);
            receipt.OpenedAtUtc = isAdmitted ? now : null;
            receipt.TenderBid.Status = isAdmitted ? "Opened" : "Rejected";
            receipt.TenderBid.OpenedDate = isAdmitted ? now : null;
            receipt.TenderBid.OpenedById = isAdmitted ? _currentUser.UserId : null;
            receipt.TenderBid.UpdatedAt = now;
            await Receipts.UpdateAsync(receipt);
            await Bids.UpdateAsync(receipt.TenderBid);
        }
        Touch(control, now);
        await Controls.UpdateAsync(control);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, qualitySelection ? "TechnicalProposalOpeningCompleted" : "PublicOpeningCompleted",
            ProcurementControlEventResult.Allowed,
            new { ParticipantCount = participants.Count, ReceiptCount = control.SubmissionReceipts.Count },
            new { control.OpeningIntegrityHash, OnTimeCount = admitted.Count, ExcludedCount = excludedAdmissions.Count }, correlation, cancellationToken,
            External(control.OpeningEvidenceReference,
                qualitySelection ? "Signed technical-proposal opening register" : "Signed public opening register", "SRC-004"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public Task<ProcurementTenderControlDto> SaveTechnicalEvaluationAsync(
        Guid tenderId, SaveProcurementTenderTechnicalEvaluationRequest request, string correlationId,
        CancellationToken cancellationToken = default) =>
        SaveEvaluationAsync(tenderId, request.RowVersion, request.EvidenceReference, request.Scores,
            technical: true, recommendedBidId: null, recommendationReason: null, correlationId, cancellationToken);

    public Task<ProcurementTenderControlDto> SaveFinancialEvaluationAsync(
        Guid tenderId, SaveProcurementTenderFinancialEvaluationRequest request, string correlationId,
        CancellationToken cancellationToken = default) =>
        SaveEvaluationAsync(tenderId, request.RowVersion, request.EvidenceReference, request.Scores,
            technical: false, request.RecommendedBidId, request.RecommendationReason, correlationId, cancellationToken);

    private async Task<ProcurementTenderControlDto> SaveEvaluationAsync(
        Guid tenderId, string rowVersion, string evidenceReference, object scores, bool technical,
        Guid? recommendedBidId, string? recommendationReason, string correlationId, CancellationToken cancellationToken)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(EvaluatePermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, correlation, cancellationToken);
        var scorerEligibility = await EnsureCommitteeScorerAsync(
            tenderId,
            technical ? ProcurementEvaluationPhase.Technical : ProcurementEvaluationPhase.Financial,
            correlation,
            cancellationToken);
        EnsureRowVersion(control.RowVersion, rowVersion);
        Require(evidenceReference, "TENDER_EVALUATION_EVIDENCE_REQUIRED", "Signed evaluation evidence is required.");
        var onTimeBids = control.SubmissionReceipts.Where(item => !item.IsDeleted &&
                item.Disposition == ProcurementTenderSubmissionDisposition.OnTimeAccepted &&
                (item.TenderBid.Status == "Opened" || item.TenderBid.Status == "UnderEvaluation" ||
                 item.TenderBid.Status == "Evaluated"))
            .Select(item => item.TenderBidId).Distinct().ToHashSet();
        var now = DateTime.UtcNow;
        string snapshot;
        if (technical)
        {
            if (scorerEligibility.AuthorizedAttempt == 1)
                EnsureStatus(control, ProcurementTenderControlStatus.Opened, "TENDER_TECHNICAL_EVALUATION_NOT_READY");
            else
                EnsureStatus(control, ProcurementTenderControlStatus.TechnicalEvaluated, "TENDER_TECHNICAL_RECALL_WINDOW_CLOSED");
            var typed = (List<ProcurementTenderTechnicalScoreRequest>)scores;
            if (typed.Count != onTimeBids.Count || typed.Select(item => item.BidId).Distinct().Count() != typed.Count ||
                typed.Any(item => !onTimeBids.Contains(item.BidId)))
                throw Validation("TENDER_TECHNICAL_COVERAGE_INVALID", "Technical evaluation must cover every on-time opened bid exactly once.");
            if (IsQualitySelection(control.Method))
            {
                EnsureTechnicalThreshold(control.Tender);
                if (typed.Any(item => item.Qualified != (item.Score >= control.Tender.MinimumTechnicalScore)))
                    throw Validation("TENDER_TECHNICAL_THRESHOLD_MISMATCH",
                        $"QBS/QCBS qualification must be derived from the configured {control.Tender.MinimumTechnicalScore:0.##} technical threshold.");
            }
            snapshot = JsonSerializer.Serialize(new
            {
                schemaVersion = IsQualitySelection(control.Method)
                    ? "tdc.quality-selection-technical-evaluation.v1"
                    : "tdc.nct-ict-technical-evaluation.v1",
                method = control.Method,
                minimumTechnicalScore = IsQualitySelection(control.Method) ? (decimal?)control.Tender.MinimumTechnicalScore : null,
                evaluatedAtUtc = now,
                evaluatorUserId = _currentUser.UserId, evaluatorName = ActorName(), evidenceReference = evidenceReference.Trim(),
                scores = typed.OrderBy(item => item.BidId).Select(item => new { item.BidId, item.Score, item.Qualified, item.Reason }).ToArray()
            }, JsonOptions);
        }
        else
        {
            if (scorerEligibility.AuthorizedAttempt == 1)
                EnsureStatus(control, ProcurementTenderControlStatus.TechnicalEvaluated, "TENDER_FINANCIAL_EVALUATION_NOT_READY");
            else
                EnsureStatus(control, ProcurementTenderControlStatus.FinancialEvaluated, "TENDER_FINANCIAL_RECALL_WINDOW_CLOSED");
            var technicalEvaluators = SnapshotUserIds(
                control.TechnicalEvaluationSnapshotJson, "evaluatorUserId").Distinct().ToList();
            if (technicalEvaluators.Count != 0)
            {
                var separation = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
                {
                    ControlCode = "SOD-TENDER-TECHNICAL-FINANCIAL-EVALUATOR",
                    SourceType = SourceType,
                    SourceReference = control.Tender.TenderNumber,
                    ProhibitedActorUserIds = technicalEvaluators
                }, correlation, cancellationToken);
                if (!separation.Allowed)
                    throw new ProcurementTenderControlAuthorizationException(separation.Message);
            }
            var qualified = QualifiedBidIds(control);
            var typed = (List<ProcurementTenderFinancialScoreRequest>)scores;
            if (qualified.Count == 0 ||
                (control.Method != ProcurementMethodType.QualityBasedSelection && typed.Count != qualified.Count) ||
                typed.Select(item => item.BidId).Distinct().Count() != typed.Count ||
                typed.Any(item => !qualified.Contains(item.BidId)) || !recommendedBidId.HasValue || !qualified.Contains(recommendedBidId.Value))
                throw Validation("TENDER_FINANCIAL_COVERAGE_INVALID", "Financial evaluation and recommendation must cover only every technically qualified bid.");
            Require(recommendationReason, "TENDER_RECOMMENDATION_REASON_REQUIRED", "Award recommendation reason is required.");
            if (control.Method == ProcurementMethodType.QualityBasedSelection)
            {
                var ranked = TechnicalScores(control).Where(item => qualified.Contains(item.BidId))
                    .OrderByDescending(item => item.Score).ThenBy(item => item.BidId).ToList();
                if (ranked.Count > 1 && ranked[0].Score == ranked[1].Score)
                    throw Validation("QBS_TECHNICAL_RANKING_TIED", "The highest-ranked QBS technical result is tied and must be resolved by the controlled committee before financial opening.");
                var selected = ranked[0].BidId;
                if (recommendedBidId.Value != selected || typed.Count != 1 || typed[0].BidId != selected)
                    throw Validation("QBS_RECOMMENDATION_INVALID", "QBS financial review and recommendation must be limited to the uniquely highest-ranked technically qualified bid.");
                snapshot = JsonSerializer.Serialize(new
                {
                    schemaVersion = "tdc.qbs-financial-negotiation.v1", method = control.Method, evaluatedAtUtc = now,
                    evaluatorUserId = _currentUser.UserId, evaluatorName = ActorName(), evidenceReference = evidenceReference.Trim(),
                    recommendedBidId = selected, recommendationReason = recommendationReason!.Trim(),
                    scores = typed.Select(item => new { item.BidId, TechnicalRank = 1, item.EvaluatedAmount, item.Reason }).ToArray()
                }, JsonOptions);
            }
            else if (control.Method == ProcurementMethodType.QualityAndCostBasedSelection)
            {
                EnsureQcbsWeights(control.Tender);
                if (typed.Any(item => item.EvaluatedAmount <= 0))
                    throw Validation("QCBS_EVALUATED_AMOUNT_INVALID", "Every QCBS qualified bid requires a positive evaluated financial amount.");
                var technicalScores = TechnicalScores(control).ToDictionary(item => item.BidId, item => item.Score);
                var lowestAmount = typed.Min(item => item.EvaluatedAmount);
                var calculated = typed.Select(item =>
                {
                    var financialScore = decimal.Round(lowestAmount / item.EvaluatedAmount * 100m, 4, MidpointRounding.AwayFromZero);
                    var combinedScore = decimal.Round(
                        technicalScores[item.BidId] * control.Tender.TechnicalWeight / 100m +
                        financialScore * control.Tender.FinancialWeight / 100m,
                        4, MidpointRounding.AwayFromZero);
                    return new { item.BidId, TechnicalScore = technicalScores[item.BidId], FinancialScore = financialScore, CombinedScore = combinedScore, item.EvaluatedAmount, item.Reason };
                }).OrderByDescending(item => item.CombinedScore).ThenBy(item => item.BidId).ToList();
                if (calculated.Count > 1 && calculated[0].CombinedScore == calculated[1].CombinedScore)
                    throw Validation("QCBS_COMBINED_SCORE_TIED", "The highest QCBS combined score is tied and must be resolved by the controlled committee before recommendation.");
                if (recommendedBidId.Value != calculated[0].BidId)
                    throw Validation("QCBS_RECOMMENDATION_INVALID", "The recommended QCBS bid must have the highest server-calculated combined technical and financial score.");
                snapshot = JsonSerializer.Serialize(new
                {
                    schemaVersion = "tdc.qcbs-combined-evaluation.v1", method = control.Method, evaluatedAtUtc = now,
                    minimumTechnicalScore = control.Tender.MinimumTechnicalScore,
                    technicalWeight = control.Tender.TechnicalWeight, financialWeight = control.Tender.FinancialWeight,
                    evaluatorUserId = _currentUser.UserId, evaluatorName = ActorName(), evidenceReference = evidenceReference.Trim(),
                    recommendedBidId, recommendationReason = recommendationReason!.Trim(), scores = calculated
                }, JsonOptions);
            }
            else
            {
                snapshot = JsonSerializer.Serialize(new
                {
                    schemaVersion = "tdc.nct-ict-financial-evaluation.v1", method = control.Method, evaluatedAtUtc = now,
                    evaluatorUserId = _currentUser.UserId, evaluatorName = ActorName(), evidenceReference = evidenceReference.Trim(),
                    recommendedBidId, recommendationReason = recommendationReason!.Trim(),
                    scores = typed.OrderBy(item => item.BidId).Select(item => new { item.BidId, item.Score, item.EvaluatedAmount, item.Reason }).ToArray()
                }, JsonOptions);
            }
        }
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await LockCommitteeScoreSheetAsync(
                    tenderId,
                    technical ? ProcurementEvaluationPhase.Technical : ProcurementEvaluationPhase.Financial,
                    "ProcurementTenderControl",
                    tenderId,
                    snapshot,
                    evidenceReference.Trim(),
                    correlation,
                    cancellationToken);
                if (technical)
                {
                    control.TechnicalEvaluatedAtUtc = now;
                    control.TechnicalEvaluationEvidenceReference = evidenceReference.Trim();
                    control.TechnicalEvaluationSnapshotJson = snapshot;
                    control.TechnicalEvaluationIntegrityHash = ComputeHash(snapshot);
                    control.Status = ProcurementTenderControlStatus.TechnicalEvaluated;
                }
                else
                {
                    control.FinancialEvaluatedAtUtc = now;
                    control.FinancialEvaluationEvidenceReference = evidenceReference.Trim();
                    control.FinancialEvaluationSnapshotJson = snapshot;
                    control.FinancialEvaluationIntegrityHash = ComputeHash(snapshot);
                    control.RecommendedBidId = recommendedBidId;
                    control.Status = ProcurementTenderControlStatus.FinancialEvaluated;
                }
                Touch(control, now);
                await Controls.UpdateAsync(control);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
        await RecordAsync(control, technical ? "TechnicalEvaluationSigned" : "FinancialProposalOpenedEvaluatedAndRecommended",
            ProcurementControlEventResult.Allowed, new { EvidenceReference = evidenceReference, EvaluatorUserId = _currentUser.UserId },
            new { control.Status, Hash = technical ? control.TechnicalEvaluationIntegrityHash : control.FinancialEvaluationIntegrityHash, control.RecommendedBidId },
            correlation, cancellationToken, External(evidenceReference.Trim(), technical ? "Technical evaluation" :
                IsQualitySelection(control.Method) ? "Financial proposal opening, evaluation, and recommendation" : "Financial evaluation", "SRC-007"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementTenderControlDto> SubmitApprovalAsync(
        Guid tenderId,
        SubmitProcurementTenderApprovalRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(EvaluatePermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, correlation, cancellationToken);
        EnsureStatus(control, ProcurementTenderControlStatus.FinancialEvaluated, "TENDER_APPROVAL_NOT_READY");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        await EnsureCommitteeDecisionReadyAsync(control, correlation, cancellationToken);
        if (!control.WorkflowDefinitionId.HasValue)
            throw Validation("TENDER_AWARD_WORKFLOW_REQUIRED", "The locked method rule has no award approval workflow.");
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var workflow = await _workflowService.StartApprovalWorkflowAsync(
                    ApprovalSourceType, control.TenderId, control.WorkflowDefinitionId.Value);
                if (!workflow.Success || !workflow.WorkflowInstanceId.HasValue)
                    throw Conflict("TENDER_AWARD_WORKFLOW_START_FAILED", workflow.Message ?? "The exact award workflow could not be started.");

                var now = DateTime.UtcNow;
                control.Status = ProcurementTenderControlStatus.PendingApproval;
                control.SubmittedForApprovalAtUtc = now;
                control.SubmittedForApprovalById = _currentUser.UserId;
                control.ApprovalActorsJson = JsonSerializer.Serialize(new[] { _currentUser.UserId }, JsonOptions);
                control.WorkflowInstanceId = workflow.WorkflowInstanceId;
                Touch(control, now);
                await Controls.UpdateAsync(control);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
        await RecordAsync(control, "TenderRecommendationSubmitted", ProcurementControlEventResult.Allowed,
            new { control.RecommendedBidId, control.SubmittedForApprovalById },
            new { control.WorkflowDefinitionId, control.WorkflowInstanceId }, correlation, cancellationToken,
            External($"workflow:{control.WorkflowInstanceId:N}", "Tender award workflow", "SRC-009"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementTenderControlDto> DecideApprovalAsync(
        Guid tenderId,
        DecideProcurementTenderApprovalRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, control.Tender.TenderNumber, correlation, cancellationToken);
        var lineage = await RevalidateAsync(control.Tender, correlation, cancellationToken);
        EnsureStatus(control, ProcurementTenderControlStatus.PendingApproval, "TENDER_APPROVAL_NOT_PENDING");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        await EnsureCommitteeDecisionReadyAsync(control, correlation, cancellationToken);
        if (!control.WorkflowInstanceId.HasValue || !await _workflowService.CanUserApproveAsync(ApprovalSourceType, control.TenderId, _currentUser.UserId))
            throw new ProcurementTenderControlAuthorizationException("The current user is not assigned to the active tender-award workflow.");
        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not "approve" and not "reject")
            throw Validation("TENDER_APPROVAL_ACTION_INVALID", "Action must be Approve or Reject.");
        Require(request.AuthorityApprovalReference, "TENDER_AUTHORITY_APPROVAL_REQUIRED", "The exact authority-route approval reference is required.");
        var ppaRequired = IsPpaRequired(lineage.Case.AuthorityRoute);
        if (ppaRequired && string.IsNullOrWhiteSpace(request.PpaApprovalReference))
            throw Validation("TENDER_PPA_APPROVAL_REQUIRED", "The locked authority route requires a PPA or central-review approval reference.");

        var prohibited = new HashSet<Guid> { control.Tender.CreatedById ?? Guid.Empty, control.SubmittedForApprovalById ?? Guid.Empty };
        foreach (var id in SnapshotUserIds(control.OpeningSnapshotJson, "userId")) prohibited.Add(id);
        foreach (var id in SnapshotUserIds(control.TechnicalEvaluationSnapshotJson, "evaluatorUserId")) prohibited.Add(id);
        foreach (var id in SnapshotUserIds(control.FinancialEvaluationSnapshotJson, "evaluatorUserId")) prohibited.Add(id);
        prohibited.Remove(Guid.Empty);
        if (prohibited.Count != 0)
        {
            var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
            {
                ControlCode = "SOD-TENDER-EVALUATOR-AWARD-APPROVER",
                SourceType = ApprovalSourceType,
                SourceReference = control.Tender.TenderNumber,
                ProhibitedActorUserIds = prohibited.ToList()
            }, correlation, cancellationToken);
            if (!sod.Allowed) throw new ProcurementTenderControlAuthorizationException(sod.Message);
        }
        var result = await _workflowService.ProcessApprovalStepAsync(
            ApprovalSourceType, control.TenderId, _currentUser.UserId, action, request.Comments);
        if (!result.Success)
            throw Conflict("TENDER_AWARD_WORKFLOW_DECISION_FAILED", result.Message ?? "Tender award workflow decision failed.");
        var now = DateTime.UtcNow;
        var actors = JsonSerializer.Deserialize<List<Guid>>(control.ApprovalActorsJson, JsonOptions) ?? new();
        if (!actors.Contains(_currentUser.UserId)) actors.Add(_currentUser.UserId);
        control.ApprovalActorsJson = JsonSerializer.Serialize(actors, JsonOptions);
        control.AuthorityApprovalReference = request.AuthorityApprovalReference.Trim();
        control.PpaApprovalReference = NullIfWhiteSpace(request.PpaApprovalReference);
        if (action == "approve" && result.Status == WorkflowInstanceStatus.Completed)
        {
            control.Status = ProcurementTenderControlStatus.Approved;
            control.ApprovedAtUtc = now;
            control.ApprovedById = _currentUser.UserId;
        }
        else if (action == "reject")
        {
            if (result.Status is not WorkflowInstanceStatus.Cancelled and not WorkflowInstanceStatus.Failed)
                throw Conflict("TENDER_REJECTION_NOT_FINAL", "A rejection is final only when the shared workflow is Cancelled or Failed.");
            control.Status = ProcurementTenderControlStatus.Rejected;
        }
        Touch(control, now);
        await Controls.UpdateAsync(control);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, control.Status == ProcurementTenderControlStatus.Approved ? "TenderAwardApproved" :
                control.Status == ProcurementTenderControlStatus.Rejected ? "TenderAwardRejected" : "TenderApprovalStepCompleted",
            control.Status == ProcurementTenderControlStatus.Rejected ? ProcurementControlEventResult.Rejected : ProcurementControlEventResult.Allowed,
            new { action, request.AuthorityApprovalReference, request.PpaApprovalReference, ActorUserId = _currentUser.UserId },
            new { control.Status, WorkflowStatus = result.Status }, correlation, cancellationToken,
            External($"workflow:{control.WorkflowInstanceId:N}", "Tender award workflow", "SRC-009"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementTenderControlDto> RecordAwardAsync(
        Guid tenderId,
        RecordProcurementTenderAwardRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, correlation, cancellationToken);
        EnsureStatus(control, ProcurementTenderControlStatus.Approved, "TENDER_AWARD_NOT_READY");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        await EnsureCommitteeDecisionReadyAsync(control, correlation, cancellationToken);
        if (!control.RecommendedBidId.HasValue || request.BidId != control.RecommendedBidId.Value)
            throw Validation("TENDER_AWARD_RECOMMENDATION_MISMATCH", "The award must use the approved recommended bid.");
        Require(request.AwardReference, "TENDER_AWARD_REFERENCE_REQUIRED", "Award reference is required.");
        Require(request.EvidenceReference, "TENDER_AWARD_EVIDENCE_REQUIRED", "Award evidence is required.");
        var bid = control.SubmissionReceipts.Where(item => !item.IsDeleted &&
                item.Disposition == ProcurementTenderSubmissionDisposition.OnTimeAccepted)
            .Select(item => item.TenderBid).SingleOrDefault(item => item.Id == request.BidId)
            ?? throw Validation("TENDER_AWARD_BID_INVALID", "The recommended on-time opened bid was not found.");
        var supplier = await _supplierValidation.EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = bid.BusinessPartnerId,
            Boundary = SupplierEligibilityBoundary.Award,
            RequiresPrequalification = control.Tender.RequiresPrequalification,
            MinimumPerformanceRating = control.Tender.MinimumPerformanceRating
        }, cancellationToken);
        if (!supplier.IsValid) throw Validation("TENDER_AWARD_SUPPLIER_INELIGIBLE", string.Join("; ", supplier.Errors));
        await _awardReadiness.EnsureAwardReadyAsync(
            ProcurementAwardReadinessSourceType.Tender,
            tenderId,
            ProcurementAwardReadinessGateRequestFactory.Create(
                ProcurementAwardReadinessSourceType.Tender,
                tenderId,
                correlation,
                [bid.Id],
                [bid.BusinessPartnerId]),
            correlation,
            cancellationToken);
        var now = DateTime.UtcNow;
        control.AwardBidId = bid.Id;
        control.AwardReference = request.AwardReference.Trim();
        control.AwardEvidenceReference = request.EvidenceReference.Trim();
        control.AwardedAtUtc = now;
        control.Status = ProcurementTenderControlStatus.Awarded;
        control.Tender.Status = "Awarded";
        control.Tender.AwardDate = now;
        control.Tender.AwardedById = _currentUser.UserId;
        bid.Status = "Accepted";
        bid.UpdatedAt = now;
        Touch(control, now);
        await Controls.UpdateAsync(control);
        await Tenders.UpdateAsync(control.Tender);
        await Bids.UpdateAsync(bid);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "TenderAwardRecorded", ProcurementControlEventResult.Allowed,
            new { request.BidId, request.AwardReference }, new { control.Status, control.AwardedAtUtc },
            correlation, cancellationToken, External(control.AwardEvidenceReference, "Tender award record", "SRC-009"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementTenderControlDto> RecordContractAsync(
        Guid tenderId,
        RecordProcurementTenderContractRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ContractPermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, correlation, cancellationToken);
        EnsureStatus(control, ProcurementTenderControlStatus.Awarded, "TENDER_CONTRACT_NOT_READY");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        Require(request.ContractReference, "TENDER_CONTRACT_REFERENCE_REQUIRED", "Executed contract reference is required.");
        Require(request.EvidenceReference, "TENDER_CONTRACT_EVIDENCE_REQUIRED", "Executed contract evidence is required.");
        var now = DateTime.UtcNow;
        control.ContractReference = request.ContractReference.Trim();
        control.ContractEvidenceReference = request.EvidenceReference.Trim();
        control.ContractedAtUtc = now;
        control.Status = ProcurementTenderControlStatus.Contracted;
        Touch(control, now);
        await Controls.UpdateAsync(control);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "TenderContractRecorded", ProcurementControlEventResult.Allowed,
            new { request.ContractReference }, new { control.Status, control.ContractedAtUtc },
            correlation, cancellationToken, External(control.ContractEvidenceReference, "Executed procurement contract", "CON-001"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementTenderControlDto> RecordAcceptanceAsync(
        Guid tenderId,
        RecordProcurementTenderAcceptanceRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ContractPermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, correlation, cancellationToken);
        EnsureStatus(control, ProcurementTenderControlStatus.Contracted, "TENDER_ACCEPTANCE_NOT_READY");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        Require(request.AcceptanceReference, "TENDER_ACCEPTANCE_REFERENCE_REQUIRED", "Successful bidder acceptance reference is required.");
        Require(request.EvidenceReference, "TENDER_ACCEPTANCE_EVIDENCE_REQUIRED", "Successful bidder acceptance evidence is required.");
        var now = DateTime.UtcNow;
        control.BidderAcceptanceReference = request.AcceptanceReference.Trim();
        control.BidderAcceptanceEvidenceReference = request.EvidenceReference.Trim();
        control.AcceptedAtUtc = now;
        control.Status = ProcurementTenderControlStatus.Accepted;
        Touch(control, now);
        await Controls.UpdateAsync(control);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "SuccessfulBidderAcceptanceRecorded", ProcurementControlEventResult.Allowed,
            new { request.AcceptanceReference }, new { control.Status, control.AcceptedAtUtc, control.IntegrityHash },
            correlation, cancellationToken, External(control.BidderAcceptanceEvidenceReference, "Bidder acceptance", "CON-002"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    private async Task<Tender> LoadTenderAsync(Guid tenderId, bool tracked, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        IQueryable<Tender> query = Tenders.GetQueryable(item =>
                item.Id == tenderId && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Bids).ThenInclude(item => item.Items)
            .Include(item => item.Bids).ThenInclude(item => item.Documents);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("TENDER_NOT_FOUND", "The tender was not found in the current tenant.");
    }

    private async Task<ProcurementTenderControl> LoadControlAsync(Guid tenderId, bool tracked, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        IQueryable<ProcurementTenderControl> query = Controls.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.TenderId == tenderId && !item.IsDeleted)
            .Include(item => item.Tender)
            .Include(item => item.SourcingCase).ThenInclude(item => item.AuthorityRoute).ThenInclude(item => item.Steps)
            .Include(item => item.MethodRule)
            .Include(item => item.DocumentIssues).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.SubmissionReceipts).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.SubmissionReceipts).ThenInclude(item => item.TenderBid).ThenInclude(item => item.Items)
            .Include(item => item.SubmissionReceipts).ThenInclude(item => item.TenderBid).ThenInclude(item => item.Documents);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("TENDER_CONTROL_NOT_FOUND", "Advertise this NCT/ICT tender through the statutory control before continuing.");
    }

    private async Task<(ProcurementSourcingCase Case, ProcurementPolicyMethodRule MethodRule)> RevalidateAsync(
        Tender tender, string correlationId, CancellationToken cancellationToken)
    {
        if (!tender.SourcePurchaseRequisitionId.HasValue || !tender.SourcingReleaseId.HasValue || !tender.SourcingCaseId.HasValue)
            throw Validation("TENDER_SOURCE_LINEAGE_REQUIRED", "The tender has no complete immutable requisition, release, and sourcing-case lineage.");
        var sourcingCase = await Cases.GetQueryable(item => item.Id == tender.SourcingCaseId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.AuthorityRoute).ThenInclude(item => item.Steps)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("TENDER_SOURCING_CASE_NOT_FOUND", "The locked sourcing case is unavailable.");
        if (!IsControlledTenderMethod(sourcingCase.SelectedMethod))
            throw Validation("TENDER_METHOD_NOT_CONTROLLED", "This control is available only for NCT, ICT, QBS, or QCBS sourcing cases.");
        ProcurementSourcingCaseEntryGateDto gate;
        try
        {
            gate = await _sourcingCases.RevalidateSourceEntryAsync(
                tender.SourcePurchaseRequisitionId.Value, tender.SourcingReleaseId.Value, sourcingCase.Id,
                sourcingCase.SelectedMethod, SourceType, tender.Id, tender.TenderNumber, correlationId, cancellationToken);
        }
        catch (ProcurementRequisitionSourcingValidationException exception)
        {
            throw Validation(exception.Code, exception.Message);
        }
        var rule = await MethodRules.GetQueryableIncludingDeleted(item =>
                item.Id == sourcingCase.MethodRuleId && item.TenantId == _currentUser.TenantId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("TENDER_METHOD_RULE_NOT_FOUND", "The exact method rule locked by the sourcing case no longer exists.");
        if (rule.IsDeleted || !rule.IsEnabled || !rule.IsAllowed || !IsControlledTenderMethod(rule.Method) ||
            gate.MethodRuleId != rule.Id || gate.EstimatedValue != tender.EstimatedValue ||
            !string.Equals(gate.CurrencyCode, tender.Currency, StringComparison.OrdinalIgnoreCase))
            throw Validation("TENDER_SOURCE_LINEAGE_STALE", "The tender no longer matches its current controlled method, value, currency, or rule lineage.");
        return (sourcingCase, rule);
    }

    private async Task EnsureCapabilityAsync(string permissionCode, string reference, string correlationId, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (HasPlatformSuperAdministratorBypass()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permissionCode, SourceType = SourceType, SourceReference = reference
        }, correlationId, cancellationToken);
        if (!decision.Allowed) throw new ProcurementTenderControlAuthorizationException(decision.Message);
    }

    private async Task RecordAsync(
        ProcurementTenderControl control, string action, ProcurementControlEventResult result,
        object input, object output, string correlationId, CancellationToken cancellationToken,
        params ProcurementControlEventEvidenceReference[] evidence)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("tender-statutory-control", control.TenantId, control.TenderId, action, correlationId),
            EventType = "ProcurementTenderStatutoryControl", Action = action, Result = result,
            RuleCode = control.MethodRuleCode, RuleId = control.MethodRuleId,
            DecisionKeys = Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToList(),
            SourceType = SourceType, SourceId = control.TenderId, SourceReference = control.Tender.TenderNumber,
            Reason = action, InputValues = input, ResultValues = output, Evidence = evidence.ToList(),
            CorrelationId = correlationId, OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private async Task<Dictionary<Guid, TenderBidPaymentAdmissionDecision>> AssessPaymentAdmissionsAsync(
        Guid tenderId,
        IReadOnlyCollection<TenderBid> bids,
        CancellationToken cancellationToken)
    {
        var fees = await TenderFees.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderId == tenderId &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (fees.All(item => !item.IsMandatory || item.Amount <= 0m))
            return bids.ToDictionary(
                bid => bid.Id,
                bid => TenderBidPaymentRules.Assess(bid, fees, Array.Empty<TenderPayment>()));

        var partnerIds = bids.Select(item => item.BusinessPartnerId).Distinct().ToList();
        var feeIds = fees.Select(item => item.Id).ToList();
        var payments = await TenderPayments.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                partnerIds.Contains(item.BusinessPartnerId) &&
                feeIds.Contains(item.TenderFeeId) &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return bids.ToDictionary(
            bid => bid.Id,
            bid => TenderBidPaymentRules.Assess(bid, fees, payments));
    }

    private async Task<ProcurementEvaluationScorerEligibilityDto> EnsureCommitteeScorerAsync(
        Guid tenderId,
        ProcurementEvaluationPhase phase,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _evaluationCommittee.EnsureScoreSubjectEligibleAsync(
                ProcurementEvaluationSourceType.Tender,
                tenderId,
                phase,
                "ProcurementTenderControl",
                tenderId,
                correlationId,
                cancellationToken);
            if (!result.Allowed)
            {
                var message = string.Join(" ", result.BlockedReasons);
                if (result.BlockedReasons.Any(reason =>
                        reason.Contains("not appointed", StringComparison.OrdinalIgnoreCase)))
                    throw new ProcurementTenderControlAuthorizationException(message);
                throw Conflict("EVALUATION_SCORER_INELIGIBLE", message);
            }
            return result;
        }
        catch (ProcurementEvaluationCommitteeAuthorizationException exception)
        {
            throw new ProcurementTenderControlAuthorizationException(exception.Message);
        }
        catch (ProcurementEvaluationCommitteeNotFoundException exception)
        {
            throw Conflict(exception.Code, exception.Message);
        }
        catch (ProcurementEvaluationCommitteeConflictException exception)
        {
            throw Conflict(exception.Code, exception.Message);
        }
        catch (ProcurementEvaluationCommitteeValidationException exception)
        {
            throw Validation(exception.Code, exception.Message);
        }
    }

    private async Task LockCommitteeScoreSheetAsync(
        Guid tenderId,
        ProcurementEvaluationPhase phase,
        string subjectType,
        Guid subjectId,
        string scoreSnapshotJson,
        string evidenceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var eligibility = await _evaluationCommittee.EnsureScoreSubjectEligibleAsync(
                ProcurementEvaluationSourceType.Tender,
                tenderId,
                phase,
                subjectType,
                subjectId,
                correlationId,
                cancellationToken);
            if (!eligibility.Allowed)
            {
                var message = string.Join(" ", eligibility.BlockedReasons);
                if (eligibility.BlockedReasons.Any(reason =>
                        reason.Contains("not appointed", StringComparison.OrdinalIgnoreCase)))
                    throw new ProcurementTenderControlAuthorizationException(message);
                throw Conflict("EVALUATION_SCORER_INELIGIBLE", message);
            }
            var committee = await _evaluationCommittee.GetAsync(
                ProcurementEvaluationSourceType.Tender,
                tenderId,
                cancellationToken);
            var appointment = committee.Members.Single(item => item.Id == eligibility.AppointmentId);
            var meeting = committee.Meetings.Single(item => item.Id == eligibility.MeetingId);
            var idempotencyKey = $"TDC0208-{ComputeHash(
                $"{tenderId:N}|{phase}|{subjectId:N}|{_currentUser.UserId:N}|{eligibility.AuthorizedAttempt}")[..32]}";

            await _evaluationCommittee.LockScoreSheetAsync(
                new LockProcurementEvaluationScoreSheetRequest
                {
                    SourceType = ProcurementEvaluationSourceType.Tender,
                    SourceId = tenderId,
                    Phase = phase,
                    ScoreSubjectType = subjectType,
                    ScoreSubjectId = subjectId,
                    MeetingId = meeting.Id,
                    AppointmentId = appointment.Id,
                    CommitteeRowVersion = committee.RowVersion,
                    MeetingRowVersion = meeting.RowVersion,
                    AppointmentRowVersion = appointment.RowVersion,
                    ScoreSnapshotJson = scoreSnapshotJson,
                    SignatureReference = evidenceReference,
                    EvidenceReference = evidenceReference,
                    IdempotencyKey = idempotencyKey
                },
                correlationId,
                cancellationToken);
        }
        catch (ProcurementEvaluationCommitteeAuthorizationException exception)
        {
            throw new ProcurementTenderControlAuthorizationException(exception.Message);
        }
        catch (ProcurementEvaluationCommitteeNotFoundException exception)
        {
            throw Conflict(exception.Code, exception.Message);
        }
        catch (ProcurementEvaluationCommitteeConflictException exception)
        {
            throw Conflict(exception.Code, exception.Message);
        }
        catch (ProcurementEvaluationCommitteeValidationException exception)
        {
            throw Validation(exception.Code, exception.Message);
        }
    }

    private async Task EnsureCommitteeDecisionReadyAsync(
        ProcurementTenderControl control,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var committee = await _evaluationCommittee.GetAsync(
                ProcurementEvaluationSourceType.Tender,
                control.TenderId,
                cancellationToken);
            if (!committee.CompositionReady || !committee.QuorumMet)
                throw Conflict(
                    "EVALUATION_COMMITTEE_NOT_READY",
                    "Committee composition and signed quorum must remain complete before recommendation approval or award.");

            foreach (var phase in new[]
                     {
                         ProcurementEvaluationPhase.Technical,
                         ProcurementEvaluationPhase.Financial
                     })
            {
                var sheets = committee.ScoreSheets.Where(item =>
                    item.Phase == phase &&
                    item.ScoreSubjectType == "ProcurementTenderControl" &&
                    item.ScoreSubjectId == control.TenderId).ToList();
                var latest = sheets
                    .GroupBy(item => new { item.AppointmentId, item.Phase, item.ScoreSubjectType })
                    .Select(group => group
                        .OrderByDescending(item => item.Attempt)
                        .ThenByDescending(item => item.SubmittedAtUtc)
                        .First())
                    .ToList();
                if (latest.Count == 0 || latest.Any(item => item.Status != ProcurementEvaluationScoreSheetStatus.Locked))
                    throw Conflict(
                        "EVALUATION_SCORE_SHEETS_NOT_CURRENT",
                        $"A current locked {phase} score-sheet attempt is required; recalled attempts cannot satisfy approval readiness.");
                var expectedSnapshot = phase == ProcurementEvaluationPhase.Technical
                    ? control.TechnicalEvaluationSnapshotJson
                    : control.FinancialEvaluationSnapshotJson;
                var normalizedExpectedSnapshot = NormalizeJson(expectedSnapshot);
                if (latest.Any(item => NormalizeJson(item.ScoreSnapshotJson) != normalizedExpectedSnapshot))
                    throw Conflict(
                        "EVALUATION_SCORE_PROJECTION_MISMATCH",
                        $"The current locked {phase} score-sheet snapshot does not match the exact retained tender-control projection.");
                var currentIds = latest.Select(item => item.Id).ToHashSet();
                if (committee.Recalls.Any(item =>
                        currentIds.Contains(item.ScoreSheetId) &&
                        item.Status is ProcurementEvaluationScoreRecallStatus.PendingApproval or
                            ProcurementEvaluationScoreRecallStatus.Approved))
                    throw Conflict(
                        "EVALUATION_SCORE_RECALL_UNRESOLVED",
                        $"The {phase} evaluation cannot proceed while the current score sheet has a pending recall or an approved recall without its authorized locked replacement attempt.");
            }
        }
        catch (ProcurementEvaluationCommitteeAuthorizationException exception)
        {
            throw new ProcurementTenderControlAuthorizationException(exception.Message);
        }
        catch (ProcurementEvaluationCommitteeNotFoundException exception)
        {
            throw Conflict(exception.Code, exception.Message);
        }
        catch (ProcurementEvaluationCommitteeConflictException exception)
        {
            throw Conflict(exception.Code, exception.Message);
        }
        catch (ProcurementEvaluationCommitteeValidationException exception)
        {
            throw Validation(exception.Code, exception.Message);
        }
    }

    private static void Touch(ProcurementTenderControl control, DateTime now)
    {
        control.UpdatedAt = now;
        Capture(control);
    }

    private static void Capture(ProcurementTenderControl control)
    {
        control.LifecycleSnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.nct-ict-control.v1", control.Id, control.TenderId, control.SourcingCaseId,
            control.MethodRuleId, control.AuthorityRouteId, control.Method, control.MethodRuleCode,
            control.AuthorityRouteReference, control.Status, control.AdvertisementReference, control.PublicationChannel,
            control.TenderDocumentReference, control.TenderDocumentVersion, control.DocumentFee,
            control.AdvertisementEvidenceReference, control.AdvertisedAtUtc, control.SubmissionDeadlineUtc,
            control.OpeningScheduledAtUtc, control.OpenedAtUtc, control.OpeningIntegrityHash,
            control.TechnicalEvaluatedAtUtc, control.TechnicalEvaluationIntegrityHash,
            control.FinancialEvaluatedAtUtc, control.FinancialEvaluationIntegrityHash, control.RecommendedBidId,
            control.WorkflowDefinitionId, control.WorkflowInstanceId, control.AuthorityApprovalReference,
            control.PpaApprovalReference, control.ApprovalActorsJson, control.ApprovedAtUtc, control.ApprovedById,
            control.AwardBidId, control.AwardReference, control.AwardedAtUtc, control.ContractReference,
            control.ContractedAtUtc, control.BidderAcceptanceReference, control.AcceptedAtUtc
        }, JsonOptions);
        control.IntegrityHash = ComputeHash(control.LifecycleSnapshotJson);
    }

    private static string NormalizeJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(document.RootElement, JsonOptions);
        }
        catch (JsonException)
        {
            return value.Trim();
        }
    }

    private static HashSet<Guid> QualifiedBidIds(ProcurementTenderControl control)
    {
        return TechnicalScores(control).Where(item => item.Qualified).Select(item => item.BidId).ToHashSet();
    }

    private static List<TechnicalScoreSnapshot> TechnicalScores(ProcurementTenderControl control)
    {
        if (string.IsNullOrWhiteSpace(control.TechnicalEvaluationSnapshotJson) ||
            ComputeHash(control.TechnicalEvaluationSnapshotJson) != control.TechnicalEvaluationIntegrityHash)
            throw Conflict("TENDER_TECHNICAL_INTEGRITY_FAILED", "The signed technical evaluation failed integrity verification.");
        using var document = JsonDocument.Parse(control.TechnicalEvaluationSnapshotJson);
        return document.RootElement.GetProperty("scores").EnumerateArray()
            .Select(item => new TechnicalScoreSnapshot(
                item.GetProperty("bidId").GetGuid(),
                item.GetProperty("score").GetDecimal(),
                item.GetProperty("qualified").GetBoolean()))
            .ToList();
    }

    private static void EnsureTechnicalThreshold(Tender tender)
    {
        if (tender.MinimumTechnicalScore is < 0m or > 100m)
            throw Validation("QUALITY_SELECTION_TECHNICAL_THRESHOLD_INVALID", "QBS/QCBS minimum technical score must be between 0 and 100.");
    }

    private static void EnsureQcbsWeights(Tender tender)
    {
        EnsureTechnicalThreshold(tender);
        if (tender.TechnicalWeight <= 0m || tender.FinancialWeight <= 0m ||
            tender.TechnicalWeight + tender.FinancialWeight != 100m)
            throw Validation("QCBS_WEIGHTS_INVALID", "QCBS technical and financial weights must both be positive and total exactly 100.");
    }

    private static IEnumerable<Guid> SnapshotUserIds(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json)) yield break;
        using var document = JsonDocument.Parse(json);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String && property.Value.TryGetGuid(out var direct)) yield return direct;
            if (property.Value.ValueKind == JsonValueKind.Array)
                foreach (var item in property.Value.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(propertyName, out var value) &&
                        value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var nested)) yield return nested;
        }
    }

    private static bool IsPpaRequired(ProcurementRequisitionAuthorityRoute route) =>
        route.Steps.Any(item => item.AuthorityName.Contains("PPA", StringComparison.OrdinalIgnoreCase) ||
                                item.AuthorityRole.Contains("PPA", StringComparison.OrdinalIgnoreCase) ||
                                item.AuthorityName.Contains("Central", StringComparison.OrdinalIgnoreCase) ||
                                item.AuthorityRole.Contains("Central", StringComparison.OrdinalIgnoreCase));

    private static ProcurementTenderControlDto Map(ProcurementTenderControl control)
    {
        var sealedRegister = control.Status == ProcurementTenderControlStatus.Advertised;
        var firstIssue = control.DocumentIssues.Where(item => !item.IsDeleted)
            .OrderBy(item => item.IssuedAtUtc).FirstOrDefault();
        var firstOnTime = control.SubmissionReceipts.Where(item => !item.IsDeleted &&
                item.Disposition == ProcurementTenderSubmissionDisposition.OnTimeAccepted)
            .OrderBy(item => item.ReceivedAtUtc).FirstOrDefault();
        var firstLate = control.SubmissionReceipts.Where(item => !item.IsDeleted &&
                item.Disposition == ProcurementTenderSubmissionDisposition.LateRejected)
            .OrderBy(item => item.ReceivedAtUtc).FirstOrDefault();
        var ppaRequired = IsPpaRequired(control.SourcingCase.AuthorityRoute);

        var technicalResults = string.IsNullOrWhiteSpace(control.TechnicalEvaluationSnapshotJson)
            ? new List<TechnicalScoreSnapshot>()
            : TechnicalScores(control);
        var qualitySelection = IsQualitySelection(control.Method);
        var financialStageCompleted = control.FinancialEvaluatedAtUtc.HasValue;
        var qualifiedBidIds = technicalResults.Where(item => item.Qualified).Select(item => item.BidId).ToHashSet();
        bool RevealAmount(ProcurementTenderSubmissionReceipt item) =>
            !sealedRegister && (!qualitySelection ||
                financialStageCompleted && (control.Method == ProcurementMethodType.QualityAndCostBasedSelection
                    ? qualifiedBidIds.Contains(item.TenderBidId)
                    : control.RecommendedBidId == item.TenderBidId));
        return new ProcurementTenderControlDto
        {
        TenderId = control.TenderId, TenderNumber = control.Tender.TenderNumber, TenderTitle = control.Tender.Title,
        Method = control.Method, MethodRuleCode = control.MethodRuleCode,
        AuthorityRouteReference = control.AuthorityRouteReference,
        PpaApprovalRequired = ppaRequired, Status = control.Status,
        AdvertisementReference = control.AdvertisementReference, PublicationChannel = control.PublicationChannel,
        TenderDocumentReference = control.TenderDocumentReference, TenderDocumentVersion = control.TenderDocumentVersion,
        DocumentFee = control.DocumentFee, AdvertisementEvidenceReference = control.AdvertisementEvidenceReference,
        AdvertisedAtUtc = control.AdvertisedAtUtc, SubmissionDeadlineUtc = control.SubmissionDeadlineUtc,
        OpeningScheduledAtUtc = control.OpeningScheduledAtUtc, OpenedAtUtc = control.OpenedAtUtc,
        TechnicalEvaluatedAtUtc = control.TechnicalEvaluatedAtUtc, FinancialEvaluatedAtUtc = control.FinancialEvaluatedAtUtc,
        MinimumTechnicalScore = control.Tender.MinimumTechnicalScore,
        TechnicalWeight = control.Tender.TechnicalWeight, FinancialWeight = control.Tender.FinancialWeight,
        RecommendedBidId = control.RecommendedBidId, WorkflowInstanceId = control.WorkflowInstanceId,
        AuthorityApprovalReference = control.AuthorityApprovalReference, PpaApprovalReference = control.PpaApprovalReference,
        AwardBidId = control.AwardBidId, AwardReference = control.AwardReference,
        ContractReference = control.ContractReference, BidderAcceptanceReference = control.BidderAcceptanceReference,
        IntegrityHash = control.IntegrityHash, RowVersion = Convert.ToBase64String(control.RowVersion),
        DocumentIssues = control.DocumentIssues.Where(item => !item.IsDeleted).OrderBy(item => item.IssuedAtUtc).Select(MapIssue).ToList(),
        SubmissionReceipts = control.SubmissionReceipts.Where(item => !item.IsDeleted).OrderBy(item => item.ReceivedAtUtc).Select(item => new ProcurementTenderSubmissionReceiptDto
        {
            Id = item.Id, TenderBidId = sealedRegister ? Guid.Empty : item.TenderBidId,
            BusinessPartnerId = sealedRegister ? Guid.Empty : item.BusinessPartnerId,
            BusinessPartnerName = sealedRegister ? "Sealed bidder" : item.BusinessPartner?.PartnerName ?? string.Empty,
            ReceiptNumber = item.ReceiptNumber,
            ReceivedAtUtc = item.ReceivedAtUtc, Disposition = item.Disposition, OpenedAtUtc = item.OpenedAtUtc,
            BidAmount = RevealAmount(item) ? item.TenderBid.TotalBidAmount : 0m,
            Currency = RevealAmount(item) ? item.TenderBid.Currency : string.Empty,
            IntegrityHash = item.IntegrityHash
        }).ToList(),
        TechnicalResults = technicalResults.Select(item => new ProcurementTenderTechnicalResultDto
        {
            BidId = item.BidId, Score = item.Score, Qualified = item.Qualified
        }).ToList(),
        Milestones =
        [
            Milestone("DEC-001", "Current sourcing-case lineage", control.AdvertisedAtUtc, control.AuthorityRouteReference),
            Milestone("DEC-002", "Approved advertisement", control.AdvertisedAtUtc, control.AdvertisementReference),
            Milestone("DEC-003", "Controlled document issue or sale", firstIssue?.IssuedAtUtc, firstIssue?.IssueReceiptNumber),
            Milestone("DEC-004", "Sealed submission receipt", firstOnTime?.ReceivedAtUtc, firstOnTime?.ReceiptNumber),
            Milestone("DEC-005", "Late submission classification",
                firstLate?.ReceivedAtUtc ?? control.OpenedAtUtc,
                firstLate?.ReceiptNumber ?? (control.OpenedAtUtc.HasValue ? "No late submissions" : null)),
            Milestone("DEC-006", "Signed public opening", control.OpenedAtUtc, control.OpeningEvidenceReference),
            Milestone("DEC-007", "Technical evaluation", control.TechnicalEvaluatedAtUtc, control.TechnicalEvaluationEvidenceReference),
            Milestone("DEC-008", "Financial evaluation", control.FinancialEvaluatedAtUtc, control.FinancialEvaluationEvidenceReference),
            Milestone("DEC-009", "Exact authority approval", control.ApprovedAtUtc, control.AuthorityApprovalReference),
            Milestone("DEC-010", "PPA or central approval", control.ApprovedAtUtc,
                ppaRequired ? control.PpaApprovalReference : control.ApprovedAtUtc.HasValue ? "Not required by locked route" : null),
            Milestone("DEC-011", "Approved recommendation", control.ApprovedAtUtc, control.RecommendedBidId?.ToString()),
            Milestone("DEC-012", "Award record", control.AwardedAtUtc, control.AwardReference),
            Milestone("DEC-013", "Executed contract", control.ContractedAtUtc, control.ContractReference),
            Milestone("DEC-014", "Successful bidder acceptance", control.AcceptedAtUtc, control.BidderAcceptanceReference)
        ]
        };
    }

    private static ProcurementTenderDocumentIssueDto MapIssue(ProcurementTenderDocumentIssue issue) => new()
    {
        Id = issue.Id, BusinessPartnerId = issue.BusinessPartnerId, RecipientName = issue.RecipientName,
        AmountPaid = issue.AmountPaid, PaymentReference = issue.PaymentReference,
        IssueReceiptNumber = issue.IssueReceiptNumber, IssuedAtUtc = issue.IssuedAtUtc,
        EvidenceReference = issue.EvidenceReference, IntegrityHash = issue.IntegrityHash
    };

    private static ProcurementTenderMilestoneDto Milestone(string code, string label, DateTime? completed, string? reference) =>
        new() { Code = code, Label = label, CompletedAtUtc = completed, Reference = reference };

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (HasPlatformSuperAdministratorBypass() ||
            _currentUser.HasRegisteredProcurementPermission("procurement.records.read")) return;
        throw new ProcurementTenderControlAuthorizationException("The procurement records read permission is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementTenderControlAuthorizationException("An authenticated tenant context is required.");
    }

    private static void EnsureStatus(ProcurementTenderControl control, ProcurementTenderControlStatus expected, string code)
    {
        if (control.Status != expected) throw Conflict(code, $"This action requires {expected}; current status is {control.Status}.");
    }

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Validation("TENDER_ROW_VERSION_INVALID", "RowVersion must be valid base64."); }
        if (!current.SequenceEqual(parsed)) throw Conflict("TENDER_VERSION_CONFLICT", "The statutory record changed. Reload before continuing.");
    }

    private bool HasPlatformSuperAdministratorBypass() => _currentUser.HasRole(Constants.Roles.SuperAdmin);
    private string ActorName() => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);
    private static bool IsNctOrIct(ProcurementMethodType method) =>
        method is ProcurementMethodType.NationalCompetitiveTendering or ProcurementMethodType.InternationalCompetitiveTendering;
    private static bool IsQualitySelection(ProcurementMethodType method) =>
        method is ProcurementMethodType.QualityBasedSelection or ProcurementMethodType.QualityAndCostBasedSelection;
    private static bool IsControlledTenderMethod(ProcurementMethodType method) =>
        IsNctOrIct(method) || IsQualitySelection(method);
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : Truncate(value.Trim(), 100);
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string ComputeHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void Require(string? value, string code, string message) { if (string.IsNullOrWhiteSpace(value)) throw Validation(code, message); }
    private static ProcurementTenderControlNotFoundException NotFound(string code, string message) => new(code, message);
    private static ProcurementTenderControlConflictException Conflict(string code, string message) => new(code, message);
    private static ProcurementTenderControlValidationException Validation(string code, string message) => new(code, message);
    private static ProcurementControlEventEvidenceReference External(string reference, string label, string requirement) => new()
    {
        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
        Reference = reference,
        Label = label,
        RequirementKey = requirement
    };

    private sealed record TechnicalScoreSnapshot(Guid BidId, decimal Score, bool Qualified);
}
