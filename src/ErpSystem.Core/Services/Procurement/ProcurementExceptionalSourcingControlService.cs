using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementExceptionalSourcingControlService : IProcurementExceptionalSourcingControlService
{
    private const string SourceType = "Tender";
    private const string ApprovalSourceType = "TenderException";
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
    private readonly ITenderNotificationService _notifications;
    private readonly IProcurementTenderDocumentControlService _tenderDocumentControlService;
    private readonly IProcurementAwardReadinessService _awardReadiness;

    public ProcurementExceptionalSourcingControlService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IProcurementSourcingCaseService sourcingCases,
        IWorkflowService workflowService,
        ISupplierValidationService supplierValidation,
        ITenderNotificationService notifications,
        IProcurementTenderDocumentControlService tenderDocumentControlService,
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
        _notifications = notifications;
        _tenderDocumentControlService = tenderDocumentControlService;
        _awardReadiness = awardReadiness;
    }

    private IGenericRepository<Tender> Tenders => _unitOfWork.Repository<Tender>();
    private IGenericRepository<TenderBid> Bids => _unitOfWork.Repository<TenderBid>();
    private IGenericRepository<TenderInvitation> Invitations => _unitOfWork.Repository<TenderInvitation>();
    private IGenericRepository<TenderNegotiation> Negotiations => _unitOfWork.Repository<TenderNegotiation>();
    private IGenericRepository<BusinessPartner> Suppliers => _unitOfWork.Repository<BusinessPartner>();
    private IGenericRepository<ProcurementSourcingCase> Cases => _unitOfWork.Repository<ProcurementSourcingCase>();
    private IGenericRepository<ProcurementPolicyMethodRule> MethodRules => _unitOfWork.Repository<ProcurementPolicyMethodRule>();
    private IGenericRepository<ProcurementPolicyExceptionRule> ExceptionRules => _unitOfWork.Repository<ProcurementPolicyExceptionRule>();
    private IGenericRepository<ProcurementPolicyEvidenceRule> EvidenceRules => _unitOfWork.Repository<ProcurementPolicyEvidenceRule>();
    private IGenericRepository<ProcurementExceptionalSourcingControl> Controls => _unitOfWork.Repository<ProcurementExceptionalSourcingControl>();

    public async Task<bool> IsExceptionalAsync(Guid tenderId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        return await Tenders.GetQueryable(item => item.Id == tenderId && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Where(item => item.SourcingCaseId.HasValue)
            .Join(Cases.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted),
                tender => tender.SourcingCaseId, sourcingCase => sourcingCase.Id, (_, sourcingCase) => sourcingCase.SelectedMethod)
            .AnyAsync(IsExceptionalExpression(), cancellationToken);
    }

    public async Task<ProcurementExceptionalSourcingControlDto> GetAsync(Guid tenderId, CancellationToken cancellationToken = default)
    {
        EnsureReader();
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementExceptionalSourcingReadinessDto> GetReadinessAsync(Guid tenderId, CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var tender = await LoadTenderAsync(tenderId, tracked: false, cancellationToken);
        var lineage = await RevalidateAsync(tender, null, Guid.NewGuid().ToString("N"), cancellationToken);
        var minimum = lineage.Case.SelectedMethod is ProcurementMethodType.SingleSource or ProcurementMethodType.PettyPurchase
            ? 1 : Math.Max(2, lineage.MethodRule.RequiresCompetition ? lineage.MethodRule.MinimumQuotationCount : 2);
        var suppliers = await Suppliers.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.IsActive && !item.IsBlacklisted && (item.PartnerType == "Supplier" || item.PartnerType == "Both" || item.PartnerType == "Contractor"))
            .OrderBy(item => item.PartnerName).AsNoTracking().ToListAsync(cancellationToken);
        return new ProcurementExceptionalSourcingReadinessDto
        {
            TenderId = tender.Id, TenderNumber = tender.TenderNumber, TenderTitle = tender.Title,
            TenderStatus = tender.Status, Method = lineage.Case.SelectedMethod,
            MethodRuleCode = lineage.MethodRule.RuleCode, ExceptionRuleCode = lineage.ExceptionRule.RuleCode,
            AuthorityRouteReference = lineage.Case.AuthorityRouteReference, MinimumSupplierCount = minimum,
            BoardApprovalRequired = lineage.BoardRequired,
            ManagingDirectorApprovalRequired = lineage.ManagingDirectorRequired,
            PpaApprovalRequired = lineage.PpaRequired,
            JustificationRequired = lineage.MethodRule.JustificationRequired,
            EvidenceRequired = lineage.ExceptionRule.EvidenceRequired,
            PostAwardFilingRequired = lineage.ExceptionRule.PostAwardFilingRequired,
            EvidenceRequirements = lineage.EvidenceRules.Select(rule => new ProcurementExceptionalEvidenceRequirementDto
            {
                EvidenceRuleId = rule.Id, RuleCode = rule.RuleCode,
                RequirementKey = NullIfWhiteSpace(rule.SharedRequirementKey) ?? rule.RuleCode,
                EvidenceName = rule.EvidenceName, RequiresVerification = rule.RequiresVerification
            }).ToList(),
            SupplierOptions = suppliers.Select(item => new ProcurementExceptionalSupplierOptionDto
            {
                BusinessPartnerId = item.Id, PartnerCode = item.PartnerCode,
                SupplierName = item.PartnerName, RegistrationStatus = item.RegistrationStatus
            }).ToList()
        };
    }

    public async Task<ProcurementExceptionalSourcingControlDto> PrepareAsync(
        Guid tenderId, PrepareProcurementExceptionalSourcingRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var tender = await LoadTenderAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, tender.TenderNumber, correlation, cancellationToken);
        if (!string.Equals(tender.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            throw Conflict("EXCEPTIONAL_TENDER_APPROVAL_REQUIRED", "The tender document must be Approved before the controlled noncompetitive case is prepared.");
        if (!tender.SubmissionDeadline.HasValue || EnsureUtc(tender.SubmissionDeadline.Value) <= DateTime.UtcNow)
            throw Validation("EXCEPTIONAL_TENDER_DEADLINE_INVALID", "The approved tender requires a future supplier-submission deadline.");
        if (await Controls.ExistsAsync(item => item.TenantId == _currentUser.TenantId && item.TenderId == tenderId && !item.IsDeleted))
            throw Conflict("EXCEPTIONAL_CONTROL_EXISTS", "The controlled noncompetitive sourcing record already exists.");
        var lineage = await RevalidateAsync(tender, null, correlation, cancellationToken);
        if (!lineage.Case.AuthorityRouteId.HasValue || string.IsNullOrWhiteSpace(lineage.Case.AuthorityRouteReference))
            throw Validation("EXCEPTIONAL_ADVANCED_AUTHORITY_ROUTE_REQUIRED",
                "This exceptional statutory-control stage requires an advanced authority route. The sourcing case remains valid for the standard approved-PR tender workflow.");
        if (lineage.MethodRule.JustificationRequired)
        {
            Require(request.Justification, "EXCEPTIONAL_JUSTIFICATION_REQUIRED", "The configured sourcing rule requires a detailed justification.");
            Require(request.JustificationEvidenceReference, "EXCEPTIONAL_JUSTIFICATION_EVIDENCE_REQUIRED", "The configured sourcing rule requires justification evidence.");
        }
        Require(request.SupplierSelectionEvidenceReference, "EXCEPTIONAL_SUPPLIER_EVIDENCE_REQUIRED", "Controlled supplier-selection evidence is required.");
        var supplierIds = request.BusinessPartnerIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var minimum = lineage.Case.SelectedMethod is ProcurementMethodType.SingleSource or ProcurementMethodType.PettyPurchase
            ? 1
            : Math.Max(2, lineage.MethodRule.RequiresCompetition ? lineage.MethodRule.MinimumQuotationCount : 2);
        if (lineage.Case.SelectedMethod == ProcurementMethodType.SingleSource && supplierIds.Count != 1)
            throw Validation("SINGLE_SOURCE_SUPPLIER_COUNT", "Single-source procurement requires exactly one identified supplier.");
        if (lineage.Case.SelectedMethod == ProcurementMethodType.PettyPurchase && supplierIds.Count != 1)
            throw Validation("PETTY_PURCHASE_SUPPLIER_COUNT", "Petty-purchase procurement requires exactly one identified supplier.");
        if (lineage.Case.SelectedMethod == ProcurementMethodType.RestrictedTendering && supplierIds.Count < minimum)
            throw Validation("RESTRICTED_SUPPLIER_MINIMUM", $"Restricted tendering requires at least {minimum} eligible invited suppliers under the locked method rule.");

        var suppliers = await Suppliers.GetQueryable(item => item.TenantId == _currentUser.TenantId && supplierIds.Contains(item.Id) && !item.IsDeleted)
            .OrderBy(item => item.PartnerName).AsNoTracking().ToListAsync(cancellationToken);
        if (suppliers.Count != supplierIds.Count)
            throw Validation("EXCEPTIONAL_SUPPLIER_NOT_FOUND", "One or more selected suppliers are unavailable in the current tenant.");
        foreach (var supplier in suppliers)
        {
            var validation = await _supplierValidation.ValidateForTenderAsync(
                supplier.Id, tender.RequiresPrequalification, tender.MinimumPerformanceRating);
            if (!validation.IsValid)
                throw Validation("EXCEPTIONAL_SUPPLIER_INELIGIBLE", $"{supplier.PartnerName}: {string.Join("; ", validation.Errors)}");
        }

        var supplied = request.EvidenceChecklist
            .Where(item => !string.IsNullOrWhiteSpace(item.RequirementKey))
            .GroupBy(item => item.RequirementKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        if (supplied.Values.Any(group => group.Count != 1))
            throw Validation("EXCEPTIONAL_EVIDENCE_DUPLICATE", "Each mandatory evidence requirement may be supplied only once.");
        var checklist = new List<EvidenceSnapshot>();
        foreach (var rule in lineage.EvidenceRules)
        {
            var key = NullIfWhiteSpace(rule.SharedRequirementKey) ?? rule.RuleCode;
            if (!supplied.TryGetValue(key, out var matches))
                throw Validation("EXCEPTIONAL_EVIDENCE_MISSING", $"Mandatory evidence '{rule.EvidenceName}' is missing for requirement '{key}'.");
            var item = matches[0];
            Require(item.EvidenceReference, "EXCEPTIONAL_EVIDENCE_REFERENCE_REQUIRED", $"Evidence reference is required for '{rule.EvidenceName}'.");
            if (rule.RequiresVerification)
                Require(item.VerificationReference, "EXCEPTIONAL_EVIDENCE_VERIFICATION_REQUIRED", $"Verified shared-evidence reference is required for '{rule.EvidenceName}'.");
            checklist.Add(new EvidenceSnapshot(rule.Id, rule.RuleCode, key, rule.EvidenceName,
                item.EvidenceReference.Trim(), item.VerificationReference.Trim()));
        }
        if (supplied.Keys.Except(checklist.Select(item => item.RequirementKey), StringComparer.OrdinalIgnoreCase).Any())
            throw Validation("EXCEPTIONAL_EVIDENCE_UNKNOWN", $"The checklist contains evidence that is not part of the current {DecisionKey(lineage.Case.SelectedMethod)} policy.");

        var now = DateTime.UtcNow;
        var control = new ProcurementExceptionalSourcingControl
        {
            Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, TenderId = tender.Id,
            SourcingCaseId = lineage.Case.Id, MethodRuleId = lineage.MethodRule.Id,
            ExceptionRuleId = lineage.ExceptionRule.Id, AuthorityRouteId = lineage.Case.AuthorityRouteId.Value,
            Method = lineage.Case.SelectedMethod, MethodRuleCode = lineage.MethodRule.RuleCode,
            ExceptionRuleCode = lineage.ExceptionRule.RuleCode,
            AuthorityRouteReference = lineage.Case.AuthorityRouteReference,
            Status = ProcurementExceptionalSourcingControlStatus.Prepared,
            Justification = NullIfWhiteSpace(request.Justification) ?? "Not required by the locked sourcing rule.",
            JustificationEvidenceReference = NullIfWhiteSpace(request.JustificationEvidenceReference) ?? "not-required-by-policy",
            SupplierSelectionEvidenceReference = request.SupplierSelectionEvidenceReference.Trim(),
            SupplierSnapshotJson = JsonSerializer.Serialize(suppliers.Select(item => new SupplierSnapshot(item.Id, item.PartnerName)).ToList(), JsonOptions),
            EvidenceChecklistJson = JsonSerializer.Serialize(checklist, JsonOptions),
            PreparedAtUtc = now, PreparedById = _currentUser.UserId,
            BoardApprovalRequired = lineage.BoardRequired,
            ManagingDirectorApprovalRequired = lineage.ManagingDirectorRequired,
            PpaApprovalRequired = lineage.PpaRequired,
            WorkflowDefinitionId = lineage.WorkflowDefinitionId,
            CreatedAt = now, CreatedBy = ActorName(), CreatedById = _currentUser.UserId,
            Tender = tender, SourcingCase = lineage.Case, MethodRule = lineage.MethodRule,
            ExceptionRule = lineage.ExceptionRule, AuthorityRoute = lineage.Case.AuthorityRoute
        };
        Capture(control);
        await Controls.AddAsync(control);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "ExceptionalSourcingPrepared", ProcurementControlEventResult.Allowed,
            new { control.Method, SupplierCount = suppliers.Count, EvidenceCount = checklist.Count },
            new { control.Status, control.ExceptionRuleCode, control.IntegrityHash }, correlation, cancellationToken,
            BuildPreparationEvidence(control, checklist).ToArray());
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementExceptionalSourcingControlDto> SubmitApprovalAsync(
        Guid tenderId, SubmitProcurementExceptionalApprovalRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, control, correlation, cancellationToken);
        EnsureStatus(control, ProcurementExceptionalSourcingControlStatus.Prepared, "EXCEPTIONAL_APPROVAL_NOT_READY");
        EnsureRowVersion(control.RowVersion, request.RowVersion);

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var workflow = await _workflowService.StartApprovalWorkflowAsync(
                    ApprovalSourceType, control.TenderId, control.WorkflowDefinitionId);
                if (!workflow.Success || !workflow.WorkflowInstanceId.HasValue)
                    throw Conflict("EXCEPTIONAL_WORKFLOW_START_FAILED", workflow.Message ?? "The exact DEC-006 approval workflow could not be started.");
                var now = DateTime.UtcNow;
                control.Status = ProcurementExceptionalSourcingControlStatus.PendingApproval;
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
        await RecordAsync(control, "ExceptionalApprovalSubmitted", ProcurementControlEventResult.Allowed,
            new { control.SubmittedForApprovalById }, new { control.WorkflowDefinitionId, control.WorkflowInstanceId },
            correlation, cancellationToken, External($"workflow:{control.WorkflowInstanceId:N}", "Exceptional sourcing workflow", "DEC-006"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementExceptionalSourcingControlDto> DecideApprovalAsync(
        Guid tenderId, DecideProcurementExceptionalApprovalRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, control, correlation, cancellationToken);
        EnsureStatus(control, ProcurementExceptionalSourcingControlStatus.PendingApproval, "EXCEPTIONAL_APPROVAL_NOT_PENDING");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        if (!control.WorkflowInstanceId.HasValue ||
            !await _workflowService.CanUserApproveAsync(ApprovalSourceType, control.TenderId, _currentUser.UserId))
            throw new ProcurementExceptionalSourcingAuthorizationException("The current user is not assigned to the active exceptional-sourcing workflow.");
        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not "approve" and not "reject")
            throw Validation("EXCEPTIONAL_APPROVAL_ACTION_INVALID", "Action must be Approve or Reject.");

        var boardReference = NullIfWhiteSpace(request.BoardApprovalReference) ?? control.BoardApprovalReference;
        var mdReference = NullIfWhiteSpace(request.ManagingDirectorApprovalReference) ?? control.ManagingDirectorApprovalReference;
        var ppaReference = NullIfWhiteSpace(request.PpaApprovalReference) ?? control.PpaApprovalReference;
        if (action == "approve")
        {
            if (control.BoardApprovalRequired) Require(boardReference, "EXCEPTIONAL_BOARD_APPROVAL_REQUIRED", "The configured Board approval reference is required.");
            if (control.ManagingDirectorApprovalRequired) Require(mdReference, "EXCEPTIONAL_MD_APPROVAL_REQUIRED", "The configured Managing Director approval reference is required.");
            if (control.PpaApprovalRequired) Require(ppaReference, "EXCEPTIONAL_PPA_APPROVAL_REQUIRED", "The mandatory PPA approval reference is required.");
        }

        var actors = JsonSerializer.Deserialize<List<Guid>>(control.ApprovalActorsJson, JsonOptions) ?? new();
        var prohibited = new HashSet<Guid>(actors)
        {
            control.Tender.CreatedById ?? Guid.Empty,
            control.PreparedById,
            control.SubmittedForApprovalById ?? Guid.Empty,
            control.SourcingCase.StartedById ?? Guid.Empty
        };
        foreach (var actor in DeserializeGuidList(control.SourcingCase.MethodOverrideApprovalActorsJson)) prohibited.Add(actor);
        prohibited.Remove(Guid.Empty);
        if (prohibited.Count > 0)
        {
            var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
            {
                ControlCode = "SOD-EXCEPTION-INITIATOR-APPROVER", SourceType = ApprovalSourceType,
                SourceReference = control.Tender.TenderNumber, ProhibitedActorUserIds = prohibited.ToList()
            }, correlation, cancellationToken);
            if (!sod.Allowed) throw new ProcurementExceptionalSourcingAuthorizationException(sod.Message);
        }

        if (action == "approve")
        {
            var supplierIds = SuppliersFrom(control).Select(item => item.BusinessPartnerId).ToList();
            await _tenderDocumentControlService.EnsurePublicationReadyAsync(
                ProcurementTenderDocumentSourceType.Tender, control.TenderId,
                control.Tender.SubmissionDeadline!.Value, correlation, cancellationToken);
            await _tenderDocumentControlService.EnsureDispatchReadyAsync(
                ProcurementTenderDocumentSourceType.Tender, control.TenderId,
                supplierIds, [], correlation, cancellationToken);
        }
        var result = await _workflowService.ProcessApprovalStepAsync(
            ApprovalSourceType, control.TenderId, _currentUser.UserId, action, request.Comments);
        if (!result.Success)
            throw Conflict("EXCEPTIONAL_WORKFLOW_DECISION_FAILED", result.Message ?? "Exceptional-sourcing workflow decision failed.");
        var now = DateTime.UtcNow;
        if (!actors.Contains(_currentUser.UserId)) actors.Add(_currentUser.UserId);
        control.ApprovalActorsJson = JsonSerializer.Serialize(actors, JsonOptions);
        control.BoardApprovalReference = boardReference;
        control.ManagingDirectorApprovalReference = mdReference;
        control.PpaApprovalReference = ppaReference;
        if (action == "approve" && result.Status == WorkflowInstanceStatus.Completed)
        {
            var supplierIds = SuppliersFrom(control).Select(item => item.BusinessPartnerId).ToList();
            control.Status = ProcurementExceptionalSourcingControlStatus.Approved;
            control.ApprovedAtUtc = now;
            control.ApprovedById = _currentUser.UserId;
            control.SuppliersInvitedAtUtc = now;
            control.Tender.Status = "Published";
            control.Tender.PublishDate = now;
            control.Tender.PublishedById = _currentUser.UserId;
            control.Tender.UpdatedAt = now;
            var existing = await Invitations.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.TenderId == control.TenderId && supplierIds.Contains(item.BusinessPartnerId) && !item.IsDeleted)
                .Select(item => item.BusinessPartnerId).ToListAsync(cancellationToken);
            foreach (var supplierId in supplierIds.Except(existing))
                await Invitations.AddAsync(new TenderInvitation
                {
                    Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, TenderId = control.TenderId,
                    BusinessPartnerId = supplierId, InvitedDate = now, InvitedById = _currentUser.UserId,
                    Status = "Invited", CreatedAt = now, CreatedBy = ActorName(), CreatedById = _currentUser.UserId
                });
            await Tenders.UpdateAsync(control.Tender);
        }
        else if (action == "reject")
        {
            if (result.Status is not WorkflowInstanceStatus.Cancelled and not WorkflowInstanceStatus.Failed)
                throw Conflict("EXCEPTIONAL_REJECTION_NOT_FINAL", "A rejection is final only when the shared workflow is Cancelled or Failed.");
            control.Status = ProcurementExceptionalSourcingControlStatus.Rejected;
        }
        Touch(control, now);
        await Controls.UpdateAsync(control);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (control.Status == ProcurementExceptionalSourcingControlStatus.Approved)
            await _notifications.SendTenderPublishedNotificationAsync(control.TenderId,
                SuppliersFrom(control).Select(item => item.BusinessPartnerId).ToList(), new List<string>());
        await RecordAsync(control, control.Status == ProcurementExceptionalSourcingControlStatus.Approved
                ? "ExceptionalSourcingApproved" : control.Status == ProcurementExceptionalSourcingControlStatus.Rejected
                    ? "ExceptionalSourcingRejected" : "ExceptionalApprovalStepCompleted",
            control.Status == ProcurementExceptionalSourcingControlStatus.Rejected
                ? ProcurementControlEventResult.Rejected : ProcurementControlEventResult.Allowed,
            new { action, request.BoardApprovalReference, request.ManagingDirectorApprovalReference, request.PpaApprovalReference, ActorUserId = _currentUser.UserId },
            new { control.Status, WorkflowStatus = result.Status, control.SuppliersInvitedAtUtc }, correlation, cancellationToken,
            External($"workflow:{control.WorkflowInstanceId:N}", "Exceptional sourcing workflow", "DEC-006"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task EnsureBidSupplierAllowedAsync(Guid tenderId, Guid businessPartnerId, CancellationToken cancellationToken = default)
    {
        if (!await IsExceptionalAsync(tenderId, cancellationToken)) return;
        var control = await LoadControlAsync(tenderId, tracked: false, cancellationToken);
        if (control.Status != ProcurementExceptionalSourcingControlStatus.Approved)
            throw Conflict("EXCEPTIONAL_BID_WINDOW_CLOSED", "Supplier bids are accepted only after the exact exceptional-sourcing approval and before negotiation begins.");
        if (!SuppliersFrom(control).Any(item => item.BusinessPartnerId == businessPartnerId))
            throw new ProcurementExceptionalSourcingAuthorizationException("Only a supplier recorded in the approved restricted or single-source shortlist may submit this bid.");
    }

    public async Task EnsureNegotiationAllowedAsync(Guid tenderId, Guid bidId, CancellationToken cancellationToken = default)
    {
        if (!await IsExceptionalAsync(tenderId, cancellationToken)) return;
        var control = await LoadControlAsync(tenderId, tracked: false, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, control.Tender.TenderNumber,
            Guid.NewGuid().ToString("N"), cancellationToken);
        if (control.Status != ProcurementExceptionalSourcingControlStatus.Approved)
            throw Conflict("EXCEPTIONAL_NEGOTIATION_NOT_READY", "Negotiation can start only after exact Board/MD/PPA approval and before a negotiation is recorded.");
        var bid = await Bids.GetQueryable(item => item.Id == bidId && item.TenderId == tenderId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("EXCEPTIONAL_NEGOTIATION_BID_NOT_FOUND", "The negotiation bid was not found in the current tender and tenant.");
        if (!SuppliersFrom(control).Any(item => item.BusinessPartnerId == bid.BusinessPartnerId))
            throw Validation("EXCEPTIONAL_NEGOTIATION_SUPPLIER_INVALID", "Negotiation must use a supplier in the approved exceptional-sourcing shortlist.");
    }

    public async Task<ProcurementExceptionalSourcingControlDto> RecordNegotiationAsync(
        Guid tenderId, RecordProcurementExceptionalNegotiationRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, control, correlation, cancellationToken);
        EnsureStatus(control, ProcurementExceptionalSourcingControlStatus.Approved, "EXCEPTIONAL_NEGOTIATION_NOT_READY");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        Require(request.PlanReference, "EXCEPTIONAL_NEGOTIATION_PLAN_REQUIRED", "The approved negotiation plan reference is required.");
        Require(request.MinutesEvidenceReference, "EXCEPTIONAL_NEGOTIATION_MINUTES_REQUIRED", "Signed negotiation minutes evidence is required.");
        Require(request.OutcomeReference, "EXCEPTIONAL_NEGOTIATION_OUTCOME_REQUIRED", "The negotiation outcome reference is required.");
        var negotiation = await Negotiations.GetQueryable(item => item.Id == request.NegotiationId &&
                item.TenderId == tenderId && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Items).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("EXCEPTIONAL_NEGOTIATION_NOT_FOUND", "The negotiation was not found in the current tender and tenant.");
        if (!string.Equals(negotiation.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
            !negotiation.CompletedDate.HasValue || !negotiation.NegotiatedAmount.HasValue)
            throw Validation("EXCEPTIONAL_NEGOTIATION_INCOMPLETE", "Complete the existing tender negotiation, including item outcomes and final amount, before recording it.");
        if (!SuppliersFrom(control).Any(item => item.BusinessPartnerId == negotiation.BusinessPartnerId))
            throw Validation("EXCEPTIONAL_NEGOTIATION_SUPPLIER_INVALID", "The completed negotiation supplier is outside the approved shortlist.");
        var now = DateTime.UtcNow;
        control.NegotiationId = negotiation.Id;
        control.NegotiationPlanReference = request.PlanReference.Trim();
        control.NegotiationMinutesEvidenceReference = request.MinutesEvidenceReference.Trim();
        control.NegotiationOutcomeReference = request.OutcomeReference.Trim();
        control.NegotiatedAmount = negotiation.NegotiatedAmount;
        control.NegotiatedAtUtc = negotiation.CompletedDate.Value;
        control.Status = ProcurementExceptionalSourcingControlStatus.Negotiated;
        Touch(control, now);
        await Controls.UpdateAsync(control);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "ExceptionalNegotiationRecorded", ProcurementControlEventResult.Allowed,
            new { request.NegotiationId, request.PlanReference, request.OutcomeReference },
            new { control.Status, control.NegotiatedAmount, control.NegotiatedAtUtc }, correlation, cancellationToken,
            External(control.NegotiationMinutesEvidenceReference, "Signed negotiation minutes", "E2E-005"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementExceptionalSourcingControlDto> RecordRecommendationAsync(
        Guid tenderId, RecordProcurementExceptionalRecommendationRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(EvaluatePermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, control, correlation, cancellationToken);
        EnsureStatus(control, ProcurementExceptionalSourcingControlStatus.Negotiated, "EXCEPTIONAL_RECOMMENDATION_NOT_READY");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        Require(request.Reason, "EXCEPTIONAL_RECOMMENDATION_REASON_REQUIRED", "The negotiated award recommendation reason is required.");
        Require(request.EvidenceReference, "EXCEPTIONAL_RECOMMENDATION_EVIDENCE_REQUIRED", "Signed recommendation evidence is required.");
        var negotiation = await Negotiations.GetQueryable(item => item.Id == control.NegotiationId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted).AsNoTracking().SingleAsync(cancellationToken);
        if (negotiation.TenderBidId != request.BidId)
            throw Validation("EXCEPTIONAL_RECOMMENDATION_BID_MISMATCH", "The recommendation must use the bid whose negotiation was completed.");
        var bid = await LoadBidAsync(tenderId, request.BidId, cancellationToken);
        var supplier = await _supplierValidation.EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = bid.BusinessPartnerId,
            Boundary = SupplierEligibilityBoundary.Award,
            RequiresPrequalification = control.Tender.RequiresPrequalification,
            MinimumPerformanceRating = control.Tender.MinimumPerformanceRating
        }, cancellationToken);
        if (!supplier.IsValid) throw Validation("EXCEPTIONAL_RECOMMENDATION_SUPPLIER_INELIGIBLE", string.Join("; ", supplier.Errors));
        var now = DateTime.UtcNow;
        control.RecommendedBidId = bid.Id;
        control.RecommendationReason = request.Reason.Trim();
        control.RecommendationEvidenceReference = request.EvidenceReference.Trim();
        control.RecommendedAtUtc = now;
        control.RecommendedById = _currentUser.UserId;
        control.Status = ProcurementExceptionalSourcingControlStatus.Recommended;
        Touch(control, now);
        await Controls.UpdateAsync(control);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "ExceptionalAwardRecommended", ProcurementControlEventResult.Allowed,
            new { request.BidId, request.Reason }, new { control.Status, control.RecommendedAtUtc }, correlation, cancellationToken,
            External(control.RecommendationEvidenceReference, "Exceptional award recommendation", "SRC-009"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementExceptionalSourcingControlDto> RecordAwardAsync(
        Guid tenderId, RecordProcurementTenderAwardRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, control, correlation, cancellationToken);
        EnsureStatus(control, ProcurementExceptionalSourcingControlStatus.Recommended, "EXCEPTIONAL_AWARD_NOT_READY");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        if (!control.RecommendedBidId.HasValue || request.BidId != control.RecommendedBidId.Value)
            throw Validation("EXCEPTIONAL_AWARD_RECOMMENDATION_MISMATCH", "The award must use the negotiated, approved recommendation.");
        Require(request.AwardReference, "EXCEPTIONAL_AWARD_REFERENCE_REQUIRED", "Award reference is required.");
        Require(request.EvidenceReference, "EXCEPTIONAL_AWARD_EVIDENCE_REQUIRED", "Award evidence is required.");
        var prohibited = new[] { control.PreparedById, control.SubmittedForApprovalById ?? Guid.Empty, control.RecommendedById ?? Guid.Empty }
            .Where(id => id != Guid.Empty).Distinct().ToList();
        var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-TENDER-EVALUATOR-AWARD-APPROVER", SourceType = SourceType,
            SourceReference = control.Tender.TenderNumber, ProhibitedActorUserIds = prohibited
        }, correlation, cancellationToken);
        if (!sod.Allowed) throw new ProcurementExceptionalSourcingAuthorizationException(sod.Message);
        var bid = await LoadBidAsync(tenderId, request.BidId, cancellationToken);
        var supplier = await _supplierValidation.EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = bid.BusinessPartnerId,
            Boundary = SupplierEligibilityBoundary.Award,
            RequiresPrequalification = control.Tender.RequiresPrequalification,
            MinimumPerformanceRating = control.Tender.MinimumPerformanceRating
        }, cancellationToken);
        if (!supplier.IsValid) throw Validation("EXCEPTIONAL_AWARD_SUPPLIER_INELIGIBLE", string.Join("; ", supplier.Errors));
        await _awardReadiness.EnsureAwardReadyAsync(
            ProcurementAwardReadinessSourceType.ExceptionalSourcing,
            tenderId,
            ProcurementAwardReadinessGateRequestFactory.Create(
                ProcurementAwardReadinessSourceType.ExceptionalSourcing,
                tenderId,
                correlation,
                [bid.Id],
                [bid.BusinessPartnerId]),
            correlation,
            cancellationToken);
        var now = DateTime.UtcNow;
        control.AwardBidId = bid.Id; control.AwardReference = request.AwardReference.Trim();
        control.AwardEvidenceReference = request.EvidenceReference.Trim(); control.AwardedAtUtc = now;
        control.Status = ProcurementExceptionalSourcingControlStatus.Awarded;
        control.Tender.Status = "Awarded"; control.Tender.AwardDate = now; control.Tender.AwardedById = _currentUser.UserId;
        bid.Status = "Accepted"; bid.UpdatedAt = now;
        Touch(control, now);
        await Controls.UpdateAsync(control); await Tenders.UpdateAsync(control.Tender); await Bids.UpdateAsync(bid);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "ExceptionalAwardRecorded", ProcurementControlEventResult.Allowed,
            new { request.BidId, request.AwardReference }, new { control.Status, control.AwardedAtUtc }, correlation, cancellationToken,
            External(control.AwardEvidenceReference, "Exceptional procurement award", "SRC-009"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementExceptionalSourcingControlDto> RecordContractAsync(
        Guid tenderId, RecordProcurementTenderContractRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ContractPermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, control, correlation, cancellationToken);
        EnsureStatus(control, ProcurementExceptionalSourcingControlStatus.Awarded, "EXCEPTIONAL_CONTRACT_NOT_READY");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        Require(request.ContractReference, "EXCEPTIONAL_CONTRACT_REFERENCE_REQUIRED", "Executed contract reference is required.");
        Require(request.EvidenceReference, "EXCEPTIONAL_CONTRACT_EVIDENCE_REQUIRED", "Executed contract evidence is required.");
        var now = DateTime.UtcNow;
        control.ContractReference = request.ContractReference.Trim(); control.ContractEvidenceReference = request.EvidenceReference.Trim();
        control.ContractedAtUtc = now; control.Status = ProcurementExceptionalSourcingControlStatus.Contracted;
        Touch(control, now); await Controls.UpdateAsync(control); await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "ExceptionalContractRecorded", ProcurementControlEventResult.Allowed,
            new { request.ContractReference }, new { control.Status, control.ContractedAtUtc }, correlation, cancellationToken,
            External(control.ContractEvidenceReference, "Executed procurement contract", "CON-001"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementExceptionalSourcingControlDto> RecordAcceptanceAsync(
        Guid tenderId, RecordProcurementTenderAcceptanceRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ContractPermission, control.Tender.TenderNumber, correlation, cancellationToken);
        await RevalidateAsync(control.Tender, control, correlation, cancellationToken);
        EnsureStatus(control, ProcurementExceptionalSourcingControlStatus.Contracted, "EXCEPTIONAL_ACCEPTANCE_NOT_READY");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        Require(request.AcceptanceReference, "EXCEPTIONAL_ACCEPTANCE_REFERENCE_REQUIRED", "Successful supplier acceptance reference is required.");
        Require(request.EvidenceReference, "EXCEPTIONAL_ACCEPTANCE_EVIDENCE_REQUIRED", "Successful supplier acceptance evidence is required.");
        var now = DateTime.UtcNow;
        control.BidderAcceptanceReference = request.AcceptanceReference.Trim();
        control.BidderAcceptanceEvidenceReference = request.EvidenceReference.Trim();
        control.AcceptedAtUtc = now; control.Status = ProcurementExceptionalSourcingControlStatus.Accepted;
        Touch(control, now); await Controls.UpdateAsync(control); await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "ExceptionalSupplierAcceptanceRecorded", ProcurementControlEventResult.Allowed,
            new { request.AcceptanceReference }, new { control.Status, control.AcceptedAtUtc }, correlation, cancellationToken,
            External(control.BidderAcceptanceEvidenceReference, "Successful supplier acceptance", "CON-002"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    public async Task<ProcurementExceptionalSourcingControlDto> RecordPostAwardFilingAsync(
        Guid tenderId, RecordProcurementPostAwardFilingRequest request, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var control = await LoadControlAsync(tenderId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ContractPermission, control.Tender.TenderNumber, correlation, cancellationToken);
        var lineage = await RevalidateAsync(control.Tender, control, correlation, cancellationToken);
        EnsureStatus(control, ProcurementExceptionalSourcingControlStatus.Accepted, "EXCEPTIONAL_FILING_NOT_READY");
        EnsureRowVersion(control.RowVersion, request.RowVersion);
        if (!lineage.ExceptionRule.PostAwardFilingRequired)
            throw Validation("EXCEPTIONAL_FILING_POLICY_INVALID", "The exact DEC-006 exception rule must require post-award filing.");
        Require(request.FilingReference, "EXCEPTIONAL_FILING_REFERENCE_REQUIRED", "PPA post-award filing reference is required.");
        Require(request.FilingEvidenceReference, "EXCEPTIONAL_FILING_EVIDENCE_REQUIRED", "PPA filing evidence is required.");
        Require(request.ExceptionReportReference, "EXCEPTIONAL_REPORT_REFERENCE_REQUIRED", "Exception report reference is required.");
        Require(request.ExceptionReportEvidenceReference, "EXCEPTIONAL_REPORT_EVIDENCE_REQUIRED", "Exception report evidence is required.");
        var now = DateTime.UtcNow;
        control.PostAwardFilingReference = request.FilingReference.Trim();
        control.PostAwardFilingEvidenceReference = request.FilingEvidenceReference.Trim();
        control.ExceptionReportReference = request.ExceptionReportReference.Trim();
        control.ExceptionReportEvidenceReference = request.ExceptionReportEvidenceReference.Trim();
        control.FiledAtUtc = now; control.FiledById = _currentUser.UserId;
        control.Status = ProcurementExceptionalSourcingControlStatus.Filed;
        Touch(control, now); await Controls.UpdateAsync(control); await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(control, "ExceptionalPostAwardFiled", ProcurementControlEventResult.Allowed,
            new { request.FilingReference, request.ExceptionReportReference }, new { control.Status, control.FiledAtUtc, control.IntegrityHash },
            correlation, cancellationToken,
            External(control.PostAwardFilingEvidenceReference, "PPA post-award filing", "DEC-006"),
            External(control.ExceptionReportEvidenceReference, "Exceptional procurement report", "E2E-005"));
        return Map(await LoadControlAsync(tenderId, tracked: false, cancellationToken));
    }

    private async Task<Tender> LoadTenderAsync(Guid tenderId, bool tracked, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        IQueryable<Tender> query = Tenders.GetQueryable(item => item.Id == tenderId && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Bids).ThenInclude(item => item.Items);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("EXCEPTIONAL_TENDER_NOT_FOUND", "The tender was not found in the current tenant.");
    }

    private async Task<TenderBid> LoadBidAsync(Guid tenderId, Guid bidId, CancellationToken cancellationToken) =>
        await Bids.GetQueryable(item => item.Id == bidId && item.TenderId == tenderId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Items).SingleOrDefaultAsync(cancellationToken)
        ?? throw Validation("EXCEPTIONAL_BID_NOT_FOUND", "The bid was not found in the current tender and tenant.");

    private async Task<ProcurementExceptionalSourcingControl> LoadControlAsync(Guid tenderId, bool tracked, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        IQueryable<ProcurementExceptionalSourcingControl> query = Controls.GetQueryable(item => item.TenderId == tenderId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Tender).ThenInclude(item => item.Bids).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.SourcingCase).ThenInclude(item => item.AuthorityRoute).ThenInclude(item => item.Steps)
            .Include(item => item.MethodRule).Include(item => item.ExceptionRule)
            .Include(item => item.Negotiation);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("EXCEPTIONAL_CONTROL_NOT_FOUND", "Prepare this Restricted Tendering or Single Source tender through the exceptional-sourcing control first.");
    }

    private async Task<Lineage> RevalidateAsync(Tender tender, ProcurementExceptionalSourcingControl? control,
        string correlationId, CancellationToken cancellationToken)
    {
        if (!tender.SourcePurchaseRequisitionId.HasValue || !tender.SourcingReleaseId.HasValue || !tender.SourcingCaseId.HasValue)
            throw Validation("EXCEPTIONAL_SOURCE_LINEAGE_REQUIRED", "The tender has no complete immutable requisition, release, and sourcing-case lineage.");
        var sourcingCase = await Cases.GetQueryable(item => item.Id == tender.SourcingCaseId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.AuthorityRoute).ThenInclude(item => item.Steps)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("EXCEPTIONAL_SOURCING_CASE_NOT_FOUND", "The locked sourcing case is unavailable.");
        if (!IsExceptional(sourcingCase.SelectedMethod))
            throw Validation("EXCEPTIONAL_METHOD_REQUIRED", "This control is available only for Restricted Tendering, Single Source, or Petty Purchase sourcing cases.");
        ProcurementSourcingCaseEntryGateDto gate;
        try
        {
            gate = await _sourcingCases.RevalidateSourceEntryAsync(tender.SourcePurchaseRequisitionId.Value,
                tender.SourcingReleaseId.Value, sourcingCase.Id, sourcingCase.SelectedMethod, SourceType,
                tender.Id, tender.TenderNumber, correlationId, cancellationToken);
        }
        catch (ProcurementRequisitionSourcingValidationException exception)
        {
            throw Validation(exception.Code, exception.Message);
        }
        var methodRule = await MethodRules.GetQueryableIncludingDeleted(item => item.Id == sourcingCase.MethodRuleId &&
                item.TenantId == _currentUser.TenantId).SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("EXCEPTIONAL_METHOD_RULE_NOT_FOUND", "The exact method rule locked by the sourcing case no longer exists.");
        if (methodRule.IsDeleted || !methodRule.IsEnabled || !methodRule.IsAllowed ||
            methodRule.Method != sourcingCase.SelectedMethod || gate.MethodRuleId != methodRule.Id ||
            gate.EstimatedValue != tender.EstimatedValue || !string.Equals(gate.CurrencyCode, tender.Currency, StringComparison.OrdinalIgnoreCase))
            throw Validation("EXCEPTIONAL_SOURCE_LINEAGE_STALE", "The tender no longer matches its current exceptional method, value, currency, or rule lineage.");

        var decisionKey = DecisionKey(sourcingCase.SelectedMethod);
        var evidenceStage = sourcingCase.SelectedMethod == ProcurementMethodType.PettyPurchase
            ? ProcurementEvidenceStage.Requisition
            : ProcurementEvidenceStage.Sourcing;
        ProcurementPolicyExceptionRule exceptionRule;
        if (control is not null)
        {
            exceptionRule = await ExceptionRules.GetQueryableIncludingDeleted(item => item.Id == control.ExceptionRuleId &&
                    item.TenantId == _currentUser.TenantId).SingleOrDefaultAsync(cancellationToken)
                ?? throw Validation("EXCEPTIONAL_RULE_NOT_FOUND", "The exact DEC-006 exception rule no longer exists.");
        }
        else if (sourcingCase.ApprovedExceptionRuleId.HasValue)
        {
            exceptionRule = await ExceptionRules.GetQueryableIncludingDeleted(item => item.Id == sourcingCase.ApprovedExceptionRuleId.Value &&
                    item.TenantId == _currentUser.TenantId).SingleOrDefaultAsync(cancellationToken)
                ?? throw Validation("EXCEPTIONAL_RULE_NOT_FOUND", "The sourcing case's approved exception rule no longer exists.");
        }
        else
        {
            var currentRules = await ExceptionRules.GetQueryable(item => item.PolicySetId == sourcingCase.PolicySetId &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsEnabled &&
                    item.SourceDecisionKey == decisionKey && item.Method == sourcingCase.SelectedMethod &&
                    (!item.Category.HasValue || item.Category == sourcingCase.Category) && item.EffectiveFrom <= DateTime.UtcNow &&
                    (!item.EffectiveTo.HasValue || item.EffectiveTo >= DateTime.UtcNow))
                .AsNoTracking().ToListAsync(cancellationToken);
            if (currentRules.Count == 0)
                throw Validation("EXCEPTIONAL_RULE_NOT_FOUND", $"No current {decisionKey} exception rule covers the selected method.");
            if (currentRules.Count > 1)
                throw Validation("EXCEPTIONAL_RULE_AMBIGUOUS", $"More than one current {decisionKey} exception rule covers the selected method.");
            exceptionRule = currentRules[0];
        }
        var now = DateTime.UtcNow;
        if (exceptionRule.IsDeleted || !exceptionRule.IsEnabled || exceptionRule.PolicySetId != sourcingCase.PolicySetId ||
            exceptionRule.Method != sourcingCase.SelectedMethod || exceptionRule.SourceDecisionKey != decisionKey ||
            exceptionRule.EffectiveFrom > now || (exceptionRule.EffectiveTo.HasValue && exceptionRule.EffectiveTo < now) ||
            exceptionRule.Disposition != ProcurementExceptionDisposition.ApprovalRequired)
            throw Validation("EXCEPTIONAL_RULE_STALE", $"The exact {decisionKey} exception rule is missing, expired, prohibited, or no longer matches the sourcing case.");
        if (sourcingCase.SelectedMethod == ProcurementMethodType.PettyPurchase)
        {
            if (exceptionRule.PostAwardFilingRequired)
                throw Validation("PETTY_PURCHASE_POLICY_INVALID", "DEC-005 petty-purchase controls cannot require the DEC-006 post-award filing lifecycle.");
        }
        else if (!exceptionRule.EvidenceRequired || !exceptionRule.PostAwardFilingRequired)
        {
            throw Validation("EXCEPTIONAL_POLICY_INCOMPLETE", "The DEC-006 exception rule must require verified evidence, approval, and post-award filing. Sourcing justification is governed by the matched Method rule.");
        }
        var workflowDefinitionId = exceptionRule.WorkflowDefinitionId ?? methodRule.WorkflowDefinitionId;
        if (!workflowDefinitionId.HasValue)
            throw Validation("EXCEPTIONAL_WORKFLOW_REQUIRED", $"The exact {decisionKey} or method rule must select a shared sourcing-approval workflow.");
        if (control is not null && (control.SourcingCaseId != sourcingCase.Id || control.MethodRuleId != methodRule.Id ||
                control.ExceptionRuleId != exceptionRule.Id || control.WorkflowDefinitionId != workflowDefinitionId.Value ||
                control.Method != sourcingCase.SelectedMethod))
            throw Validation("EXCEPTIONAL_CONTROL_LINEAGE_MISMATCH", "The statutory record no longer matches its immutable case, rule, method, or workflow lineage.");

        var evidenceRules = await EvidenceRules.GetQueryable(item => item.PolicySetId == sourcingCase.PolicySetId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsEnabled && item.IsMandatory &&
                item.SourceDecisionKey == decisionKey && item.Stage == evidenceStage &&
                (!item.Category.HasValue || item.Category == sourcingCase.Category) &&
                (!item.Method.HasValue || item.Method == sourcingCase.SelectedMethod) && item.EffectiveFrom <= now &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo >= now))
            .OrderBy(item => item.RuleCode).AsNoTracking().ToListAsync(cancellationToken);
        if (evidenceRules.Count == 0)
            throw Validation("EXCEPTIONAL_EVIDENCE_POLICY_INCOMPLETE", $"The current {decisionKey} policy has no mandatory evidence checklist.");
        if (control is not null)
        {
            var captured = EvidenceFrom(control);
            if (evidenceRules.Any(rule => !captured.Any(item => item.EvidenceRuleId == rule.Id &&
                    !string.IsNullOrWhiteSpace(item.EvidenceReference) && !string.IsNullOrWhiteSpace(item.VerificationReference))))
                throw Validation("EXCEPTIONAL_EVIDENCE_STALE", $"The immutable sourcing record does not cover every current mandatory {decisionKey} evidence rule.");
        }
        var authorityText = string.Join(" ", sourcingCase.AuthorityRoute.Steps.Select(item => $"{item.AuthorityName} {item.AuthorityRole}").Append(exceptionRule.ApproverRole));
        var boardRequired = authorityText.Contains("board", StringComparison.OrdinalIgnoreCase);
        var managingDirectorRequired = authorityText.Contains("managing director", StringComparison.OrdinalIgnoreCase) ||
                                      authorityText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(item => string.Equals(item, "MD", StringComparison.OrdinalIgnoreCase));
        var ppaRequired = authorityText.Contains("PPA", StringComparison.OrdinalIgnoreCase) ||
                          authorityText.Contains("public procurement", StringComparison.OrdinalIgnoreCase) ||
                          authorityText.Contains("central tender", StringComparison.OrdinalIgnoreCase);
        if (sourcingCase.SelectedMethod != ProcurementMethodType.PettyPurchase &&
            (!ppaRequired || (!boardRequired && !managingDirectorRequired)))
            throw Validation("EXCEPTIONAL_AUTHORITY_POLICY_INCOMPLETE", "The locked route must include PPA and at least Board or Managing Director authority for restricted/single-source procurement.");
        return new Lineage(sourcingCase, methodRule, exceptionRule, evidenceRules,
            workflowDefinitionId.Value, boardRequired, managingDirectorRequired, ppaRequired);
    }

    private async Task EnsureCapabilityAsync(string permissionCode, string reference, string correlationId, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permissionCode, SourceType = SourceType, SourceReference = reference
        }, correlationId, cancellationToken);
        if (!decision.Allowed) throw new ProcurementExceptionalSourcingAuthorizationException(decision.Message);
    }

    private async Task RecordAsync(ProcurementExceptionalSourcingControl control, string action,
        ProcurementControlEventResult result, object input, object output, string correlationId,
        CancellationToken cancellationToken, params ProcurementControlEventEvidenceReference[] evidence)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("exceptional-sourcing-control", control.TenantId, control.TenderId, action, correlationId),
            EventType = "ProcurementExceptionalSourcingControl", Action = action, Result = result,
            RuleCode = control.ExceptionRuleCode, RuleId = control.ExceptionRuleId,
            DecisionKeys = Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToList(),
            SourceType = SourceType, SourceId = control.TenderId, SourceReference = control.Tender.TenderNumber,
            Reason = action, InputValues = input, ResultValues = output, Evidence = evidence.ToList(),
            CorrelationId = correlationId, OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private static IEnumerable<ProcurementControlEventEvidenceReference> BuildPreparationEvidence(
        ProcurementExceptionalSourcingControl control, IEnumerable<EvidenceSnapshot> checklist)
    {
        if (!string.Equals(control.JustificationEvidenceReference, "not-required-by-policy", StringComparison.OrdinalIgnoreCase))
            yield return External(control.JustificationEvidenceReference, "Controlled sourcing justification", DecisionKey(control.Method));
        yield return External(control.SupplierSelectionEvidenceReference, "Exceptional supplier selection", "E2E-005");
        foreach (var item in checklist)
            yield return External(item.EvidenceReference, item.EvidenceName, item.RequirementKey);
    }

    private static void Touch(ProcurementExceptionalSourcingControl control, DateTime now)
    {
        control.UpdatedAt = now;
        Capture(control);
    }

    private static void Capture(ProcurementExceptionalSourcingControl control)
    {
        control.LifecycleSnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.noncompetitive-sourcing-control.v2", control.Id, control.TenderId,
            control.SourcingCaseId, control.MethodRuleId, control.ExceptionRuleId, control.AuthorityRouteId,
            control.Method, control.MethodRuleCode, control.ExceptionRuleCode, control.AuthorityRouteReference,
            control.Status, control.Justification, control.JustificationEvidenceReference,
            control.SupplierSelectionEvidenceReference, control.SupplierSnapshotJson, control.EvidenceChecklistJson,
            control.PreparedAtUtc, control.PreparedById, control.SuppliersInvitedAtUtc,
            control.BoardApprovalRequired, control.ManagingDirectorApprovalRequired, control.PpaApprovalRequired,
            control.WorkflowDefinitionId, control.WorkflowInstanceId, control.BoardApprovalReference,
            control.ManagingDirectorApprovalReference, control.PpaApprovalReference, control.ApprovalActorsJson,
            control.ApprovedAtUtc, control.ApprovedById, control.NegotiationId, control.NegotiationPlanReference,
            control.NegotiationMinutesEvidenceReference, control.NegotiationOutcomeReference, control.NegotiatedAmount,
            control.NegotiatedAtUtc, control.RecommendedBidId, control.RecommendationReason,
            control.RecommendationEvidenceReference, control.RecommendedAtUtc, control.RecommendedById,
            control.AwardBidId, control.AwardReference, control.AwardedAtUtc, control.ContractReference,
            control.ContractedAtUtc, control.BidderAcceptanceReference, control.AcceptedAtUtc,
            control.PostAwardFilingReference, control.ExceptionReportReference, control.FiledAtUtc, control.FiledById
        }, JsonOptions);
        control.IntegrityHash = ComputeHash(control.LifecycleSnapshotJson);
    }

    private static ProcurementExceptionalSourcingControlDto Map(ProcurementExceptionalSourcingControl control) => new()
    {
        TenderId = control.TenderId, TenderNumber = control.Tender.TenderNumber, TenderTitle = control.Tender.Title,
        Method = control.Method, MethodRuleCode = control.MethodRuleCode, ExceptionRuleCode = control.ExceptionRuleCode,
        AuthorityRouteReference = control.AuthorityRouteReference, Status = control.Status,
        Justification = control.Justification, JustificationEvidenceReference = control.JustificationEvidenceReference,
        SupplierSelectionEvidenceReference = control.SupplierSelectionEvidenceReference,
        PreparedAtUtc = control.PreparedAtUtc, SuppliersInvitedAtUtc = control.SuppliersInvitedAtUtc,
        BoardApprovalRequired = control.BoardApprovalRequired,
        ManagingDirectorApprovalRequired = control.ManagingDirectorApprovalRequired,
        PpaApprovalRequired = control.PpaApprovalRequired, WorkflowInstanceId = control.WorkflowInstanceId,
        BoardApprovalReference = control.BoardApprovalReference,
        ManagingDirectorApprovalReference = control.ManagingDirectorApprovalReference,
        PpaApprovalReference = control.PpaApprovalReference,
        NegotiationId = control.NegotiationId, NegotiationPlanReference = control.NegotiationPlanReference,
        NegotiationMinutesEvidenceReference = control.NegotiationMinutesEvidenceReference,
        NegotiationOutcomeReference = control.NegotiationOutcomeReference, NegotiatedAmount = control.NegotiatedAmount,
        RecommendedBidId = control.RecommendedBidId, RecommendationReason = control.RecommendationReason,
        AwardReference = control.AwardReference, ContractReference = control.ContractReference,
        BidderAcceptanceReference = control.BidderAcceptanceReference,
        PostAwardFilingReference = control.PostAwardFilingReference,
        ExceptionReportReference = control.ExceptionReportReference,
        IntegrityHash = control.IntegrityHash, RowVersion = Convert.ToBase64String(control.RowVersion),
        Suppliers = SuppliersFrom(control).Select(item => new ProcurementExceptionalSupplierDto
            { BusinessPartnerId = item.BusinessPartnerId, SupplierName = item.SupplierName }).ToList(),
        EvidenceChecklist = EvidenceFrom(control).Select(item => new ProcurementExceptionalEvidenceDto
        {
            EvidenceRuleId = item.EvidenceRuleId, RuleCode = item.RuleCode, RequirementKey = item.RequirementKey,
            EvidenceName = item.EvidenceName, EvidenceReference = item.EvidenceReference,
            VerificationReference = item.VerificationReference
        }).ToList(),
        Bids = control.Tender.Bids.Where(item => !item.IsDeleted).OrderBy(item => item.SubmittedDate).Select(item => new ProcurementExceptionalBidDto
        {
            BidId = item.Id, BidNumber = item.BidNumber, BusinessPartnerId = item.BusinessPartnerId,
            SupplierName = item.BusinessPartner?.PartnerName ?? SuppliersFrom(control).FirstOrDefault(supplier => supplier.BusinessPartnerId == item.BusinessPartnerId)?.SupplierName ?? string.Empty,
            BidAmount = item.TotalBidAmount, Currency = item.Currency ?? control.Tender.Currency, Status = item.Status
        }).ToList(),
        Milestones =
        [
            Milestone("DEC-001", "Current exceptional method lineage", control.PreparedAtUtc, control.MethodRuleCode),
            Milestone("DEC-002", "Statutory justification", control.PreparedAtUtc, control.JustificationEvidenceReference),
            Milestone("DEC-003", "Mandatory evidence checklist", control.PreparedAtUtc, control.ExceptionRuleCode),
            Milestone("DEC-004", "Restricted or sole supplier identity", control.PreparedAtUtc, control.SupplierSelectionEvidenceReference),
            Milestone("DEC-005", "Exact shared approval submitted", control.SubmittedForApprovalAtUtc, control.WorkflowInstanceId?.ToString()),
            Milestone("DEC-006", "Board authority outcome", control.BoardApprovalRequired ? control.ApprovedAtUtc : control.PreparedAtUtc,
                control.BoardApprovalRequired ? control.BoardApprovalReference : "Not required by locked route"),
            Milestone("DEC-007", "Managing Director authority outcome", control.ManagingDirectorApprovalRequired ? control.ApprovedAtUtc : control.PreparedAtUtc,
                control.ManagingDirectorApprovalRequired ? control.ManagingDirectorApprovalReference : "Not required by locked route"),
            Milestone("DEC-008", "PPA approval", control.PpaApprovalRequired ? control.ApprovedAtUtc : control.PreparedAtUtc,
                control.PpaApprovalRequired ? control.PpaApprovalReference : "Not required by locked route"),
            Milestone("DEC-009", "Negotiation plan and minutes", control.NegotiatedAtUtc, control.NegotiationOutcomeReference),
            Milestone("DEC-010", "Negotiated recommendation", control.RecommendedAtUtc, control.RecommendedBidId?.ToString()),
            Milestone("DEC-011", "Award record", control.AwardedAtUtc, control.AwardReference),
            Milestone("DEC-012", "Executed contract", control.ContractedAtUtc, control.ContractReference),
            Milestone("DEC-013", "Supplier acceptance", control.AcceptedAtUtc, control.BidderAcceptanceReference),
            Milestone("DEC-014", "Post-award filing and exception report",
                control.ExceptionRule.PostAwardFilingRequired ? control.FiledAtUtc : control.AcceptedAtUtc,
                control.ExceptionRule.PostAwardFilingRequired ? control.PostAwardFilingReference : "Not required by locked rule")
        ]
    };

    private static List<SupplierSnapshot> SuppliersFrom(ProcurementExceptionalSourcingControl control) =>
        JsonSerializer.Deserialize<List<SupplierSnapshot>>(control.SupplierSnapshotJson, JsonOptions) ?? new();
    private static List<EvidenceSnapshot> EvidenceFrom(ProcurementExceptionalSourcingControl control) =>
        JsonSerializer.Deserialize<List<EvidenceSnapshot>>(control.EvidenceChecklistJson, JsonOptions) ?? new();
    private static List<Guid> DeserializeGuidList(string? json) => string.IsNullOrWhiteSpace(json)
        ? new() : JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? new();
    private static ProcurementExceptionalSourcingMilestoneDto Milestone(string code, string label, DateTime? completedAt, string? reference) =>
        new() { Code = code, Label = label, CompletedAtUtc = completedAt, Reference = reference };

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator() || _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role => ProcurementAccessControlRegistry.FindRole(role) is not null)) return;
        throw new ProcurementExceptionalSourcingAuthorizationException("A TDC procurement or tenant-administration role is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementExceptionalSourcingAuthorizationException("An authenticated tenant context is required.");
    }

    private static void EnsureStatus(ProcurementExceptionalSourcingControl control,
        ProcurementExceptionalSourcingControlStatus expected, string code)
    {
        if (control.Status != expected) throw Conflict(code, $"This action requires {expected}; current status is {control.Status}.");
    }

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Validation("EXCEPTIONAL_ROW_VERSION_INVALID", "RowVersion must be valid base64."); }
        if (!current.SequenceEqual(parsed)) throw Conflict("EXCEPTIONAL_VERSION_CONFLICT", "The statutory record changed. Reload before continuing.");
    }

    private bool IsAdministrator() => _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");
    private string ActorName() => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);
    private static bool IsExceptional(ProcurementMethodType method) =>
        method is ProcurementMethodType.RestrictedTendering or ProcurementMethodType.SingleSource or ProcurementMethodType.PettyPurchase;
    private static System.Linq.Expressions.Expression<Func<ProcurementMethodType, bool>> IsExceptionalExpression() =>
        method => method == ProcurementMethodType.RestrictedTendering || method == ProcurementMethodType.SingleSource ||
                  method == ProcurementMethodType.PettyPurchase;
    private static string DecisionKey(ProcurementMethodType method) =>
        method == ProcurementMethodType.PettyPurchase ? "DEC-005" : "DEC-006";
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : Truncate(value.Trim(), 100);
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string ComputeHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void Require(string? value, string code, string message) { if (string.IsNullOrWhiteSpace(value)) throw Validation(code, message); }
    private static ProcurementExceptionalSourcingNotFoundException NotFound(string code, string message) => new(code, message);
    private static ProcurementExceptionalSourcingConflictException Conflict(string code, string message) => new(code, message);
    private static ProcurementExceptionalSourcingValidationException Validation(string code, string message) => new(code, message);
    private static ProcurementControlEventEvidenceReference External(string reference, string label, string requirement) => new()
    {
        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
        Reference = reference, Label = label, RequirementKey = requirement
    };

    private sealed record SupplierSnapshot(Guid BusinessPartnerId, string SupplierName);
    private sealed record EvidenceSnapshot(Guid EvidenceRuleId, string RuleCode, string RequirementKey,
        string EvidenceName, string EvidenceReference, string VerificationReference);
    private sealed record Lineage(ProcurementSourcingCase Case, ProcurementPolicyMethodRule MethodRule,
        ProcurementPolicyExceptionRule ExceptionRule, IReadOnlyList<ProcurementPolicyEvidenceRule> EvidenceRules,
        Guid WorkflowDefinitionId, bool BoardRequired, bool ManagingDirectorRequired, bool PpaRequired);
}
