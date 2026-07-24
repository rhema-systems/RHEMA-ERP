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

public sealed class ProcurementRfqControlService : IProcurementRfqControlService
{
    private const string SourceType = "RequestForQuotation";
    private const string ManagePermission = "procurement.sourcing.manage";
    private const string AdministerPermission = "procurement.tender.administer";
    private const string EvaluatePermission = "procurement.tender.evaluate";
    private const string ApprovePermission = "procurement.tender.approve";
    private const string PurchaseOrderPermission = "procurement.purchase-order.create";
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
    private readonly IProcurementSourcingCaseService _sourcingCaseService;
    private readonly IWorkflowService _workflowService;
    private readonly ISupplierValidationService _supplierValidation;

    public ProcurementRfqControlService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IProcurementSourcingCaseService sourcingCaseService,
        IWorkflowService workflowService,
        ISupplierValidationService supplierValidation)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _sourcingCaseService = sourcingCaseService;
        _workflowService = workflowService;
        _supplierValidation = supplierValidation;
    }

    private IGenericRepository<RequestForQuotation> Rfqs => _unitOfWork.Repository<RequestForQuotation>();
    private IGenericRepository<RequestForQuotationQuote> Quotes => _unitOfWork.Repository<RequestForQuotationQuote>();
    private IGenericRepository<ProcurementSourcingCase> SourcingCases => _unitOfWork.Repository<ProcurementSourcingCase>();
    private IGenericRepository<ProcurementPolicyMethodRule> MethodRules => _unitOfWork.Repository<ProcurementPolicyMethodRule>();
    private IGenericRepository<ProcurementRfqReceipt> Receipts => _unitOfWork.Repository<ProcurementRfqReceipt>();
    private IGenericRepository<ProcurementRfqOpeningRegister> OpeningRegisters => _unitOfWork.Repository<ProcurementRfqOpeningRegister>();
    private IGenericRepository<ProcurementRfqOpeningParticipant> OpeningParticipants => _unitOfWork.Repository<ProcurementRfqOpeningParticipant>();
    private IGenericRepository<ProcurementRfqOpeningEntry> OpeningEntries => _unitOfWork.Repository<ProcurementRfqOpeningEntry>();
    private IGenericRepository<ProcurementRfqEvaluation> Evaluations => _unitOfWork.Repository<ProcurementRfqEvaluation>();
    private IGenericRepository<ProcurementRfqEvaluationLine> EvaluationLines => _unitOfWork.Repository<ProcurementRfqEvaluationLine>();

    public async Task<ProcurementRfqControlDto> GetAsync(Guid rfqId, CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var rfq = await LoadRfqAsync(rfqId, tracked: false, cancellationToken);
        var methodRule = await ResolveMethodRuleAsync(rfq, cancellationToken, requireOperational: false);
        var qualifiedInvitationCount = 0;
        foreach (var invitation in rfq.Invitations.Where(item => !item.IsDeleted))
        {
            var validation = await _supplierValidation.ValidateForRfqAsync(invitation.BusinessPartnerId);
            if (validation.IsValid) qualifiedInvitationCount++;
        }
        return MapControl(rfq, methodRule, qualifiedInvitationCount);
    }

    public async Task EnsureDispatchReadyAsync(
        Guid rfqId,
        IReadOnlyCollection<Guid> supplierIds,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var rfq = await LoadRfqAsync(rfqId, tracked: false, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, rfq.RfqNumber, normalizedCorrelation, cancellationToken);
        await RevalidateRfqAsync(rfq, normalizedCorrelation, cancellationToken);
        var rule = await ResolveMethodRuleAsync(rfq, cancellationToken);
        if (!rfq.SubmissionDeadline.HasValue)
            throw Validation("RFQ_DEADLINE_REQUIRED", "A submission deadline is required before the RFQ can be dispatched.");
        if (EnsureUtc(rfq.SubmissionDeadline.Value) <= DateTime.UtcNow)
            throw Validation("RFQ_DEADLINE_PASSED", "The RFQ submission deadline must be in the future at dispatch.");
        if (rule.MinimumQuotationCount <= 0)
            throw Validation("RFQ_MINIMUM_QUOTATIONS_NOT_CONFIGURED", "The exact locked procurement-method rule must configure a positive minimum quotation count.");
        if (!rule.WorkflowDefinitionId.HasValue)
            throw Validation("RFQ_APPROVAL_WORKFLOW_NOT_CONFIGURED", "The exact locked procurement-method rule must select a shared approval workflow before dispatch.");

        var selected = supplierIds.Where(item => item != Guid.Empty).Distinct().ToList();
        if (selected.Count < rule.MinimumQuotationCount)
            throw Validation("RFQ_MINIMUM_SUPPLIERS_NOT_MET",
                $"Invite at least {rule.MinimumQuotationCount} qualified suppliers. Only {selected.Count} were selected; email-only recipients do not count.");

        var failures = new List<string>();
        foreach (var supplierId in selected)
        {
            var result = await _supplierValidation.ValidateForRfqAsync(supplierId);
            if (!result.IsValid)
                failures.Add($"{supplierId}: {string.Join("; ", result.Errors)}");
        }
        if (failures.Count != 0)
            throw Validation("RFQ_SUPPLIER_QUALIFICATION_FAILED", "One or more selected suppliers are not eligible for this RFQ: " + string.Join(" | ", failures));

        await RecordAsync(rfq, rule, "DispatchReadinessPassed", ProcurementControlEventResult.Allowed,
            new { SelectedSupplierCount = selected.Count, rule.MinimumQuotationCount },
            new { QualifiedSupplierCount = selected.Count, rule.WorkflowDefinitionId }, normalizedCorrelation, cancellationToken);
    }

    public async Task<ProcurementRfqReceipt> RecordReceiptAsync(
        Guid rfqId,
        Guid quoteId,
        DateTime receivedAtUtc,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var rfq = await LoadRfqAsync(rfqId, tracked: true, cancellationToken);
        await RevalidateRfqAsync(rfq, NormalizeCorrelation(correlationId), cancellationToken);
        var rule = await ResolveMethodRuleAsync(rfq, cancellationToken);
        var quote = await Quotes.GetQueryable(item => item.Id == quoteId && item.RfqId == rfqId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.BusinessPartner)
            .Include(item => item.Items).ThenInclude(item => item.RfqItem)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("RFQ_QUOTE_NOT_FOUND", "The supplier quote was not found in the current tenant.");
        if (await Receipts.ExistsAsync(item => item.TenantId == _currentUser.TenantId && item.RfqId == rfqId && item.QuoteId == quoteId && !item.IsDeleted))
            throw Conflict("RFQ_QUOTE_ALREADY_RECEIVED", "This supplier has already submitted a sealed quotation for the RFQ.");
        if (!rfq.SubmissionDeadline.HasValue)
            throw Validation("RFQ_DEADLINE_REQUIRED", "The RFQ has no submission deadline.");

        var received = EnsureUtc(receivedAtUtc);
        var deadline = EnsureUtc(rfq.SubmissionDeadline.Value);
        var sequence = (await Receipts.GetQueryableIncludingDeleted(item => item.TenantId == _currentUser.TenantId && item.RfqId == rfqId)
            .Select(item => (int?)item.ReceiptSequence).MaxAsync(cancellationToken) ?? 0) + 1;
        var snapshot = new
        {
            schemaVersion = "tdc.rfq-sealed-quote.v1",
            rfqId,
            rfq.RfqNumber,
            quoteId,
            quote.BusinessPartnerId,
            supplier = quote.BusinessPartner?.PartnerName,
            quote.Notes,
            receivedAtUtc = received,
            submissionDeadlineUtc = deadline,
            items = quote.Items.Where(item => !item.IsDeleted).OrderBy(item => item.RfqItem.LineNumber).Select(item => new
            {
                item.RfqItemId,
                item.RfqItem.LineNumber,
                item.UnitPrice,
                item.LineTotal
            }).ToArray()
        };
        var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        var receipt = new ProcurementRfqReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            RfqId = rfqId,
            QuoteId = quoteId,
            BusinessPartnerId = quote.BusinessPartnerId,
            ReceiptSequence = sequence,
            ReceiptNumber = Truncate($"{rfq.RfqNumber}-R{sequence:0000}", 100),
            SubmissionDeadlineUtc = deadline,
            ReceivedAtUtc = received,
            Disposition = received <= deadline ? ProcurementRfqReceiptDisposition.OnTimeAccepted : ProcurementRfqReceiptDisposition.LateRejected,
            SealedByUserId = quote.SubmittedByUserId ?? _currentUser.UserId,
            SealedAtUtc = received,
            SubmissionSnapshotJson = snapshotJson,
            IntegrityHash = ComputeHash(snapshotJson),
            CreatedAt = received,
            CreatedById = quote.SubmittedByUserId
        };
        await Receipts.AddAsync(receipt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(rfq, rule,
            receipt.Disposition == ProcurementRfqReceiptDisposition.OnTimeAccepted ? "QuotationReceivedAndSealed" : "LateQuotationReceivedAndRejected",
            receipt.Disposition == ProcurementRfqReceiptDisposition.OnTimeAccepted ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Rejected,
            new { quote.BusinessPartnerId, receivedAtUtc = received, submissionDeadlineUtc = deadline },
            new { receipt.Id, receipt.ReceiptNumber, receipt.Disposition, receipt.IntegrityHash },
            NormalizeCorrelation(correlationId), cancellationToken,
            External(receipt.ReceiptNumber, "Sealed quotation receipt", "SRC-003"));
        return receipt;
    }

    public async Task<bool> AreQuotesOpenAsync(Guid rfqId, CancellationToken cancellationToken = default)
    {
        EnsureReader();
        return await OpeningRegisters.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.RfqId == rfqId && !item.IsDeleted)
            .AnyAsync(cancellationToken);
    }

    public async Task<ProcurementRfqOpeningRegisterDto> CompleteOpeningAsync(
        Guid rfqId,
        CompleteProcurementRfqOpeningRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var rfq = await LoadRfqAsync(rfqId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(AdministerPermission, rfq.RfqNumber, normalizedCorrelation, cancellationToken);
        await RevalidateRfqAsync(rfq, normalizedCorrelation, cancellationToken);
        var rule = await ResolveMethodRuleAsync(rfq, cancellationToken);
        if (!rfq.SubmissionDeadline.HasValue || EnsureUtc(rfq.SubmissionDeadline.Value) > DateTime.UtcNow)
            throw Conflict("RFQ_OPENING_BEFORE_DEADLINE", "The controlled opening register cannot be completed before the submission deadline.");
        if (rfq.OpeningRegister is not null)
            throw Conflict("RFQ_OPENING_ALREADY_COMPLETED", "The immutable opening register has already been completed.");
        if (string.IsNullOrWhiteSpace(request.EvidenceReference))
            throw Validation("RFQ_OPENING_EVIDENCE_REQUIRED", "An opening evidence reference is required.");
        var participantSpecs = request.Participants.Where(item => !string.IsNullOrWhiteSpace(item.ParticipantName)).ToList();
        if (participantSpecs.Count < 2 || !participantSpecs.Any(item => item.IsObserver) || !participantSpecs.Any(item => !item.IsObserver))
            throw Validation("RFQ_OPENING_PARTICIPANTS_INVALID", "At least two signed participants, including an opening officer and an observer, are required.");
        if (participantSpecs.Any(item => string.IsNullOrWhiteSpace(item.RoleName) || string.IsNullOrWhiteSpace(item.SignatureReference)))
            throw Validation("RFQ_OPENING_SIGNATURE_REQUIRED", "Every opening participant requires a role and signature reference.");
        if (!participantSpecs.Any(item => item.ParticipantUserId == _currentUser.UserId))
            throw Validation("RFQ_OPENING_OFFICER_SIGNATURE_REQUIRED", "The opening officer must be one of the signed participants.");
        if (participantSpecs.Where(item => item.ParticipantUserId.HasValue).GroupBy(item => item.ParticipantUserId).Any(group => group.Count() > 1) ||
            participantSpecs.GroupBy(item => item.ParticipantName.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw Validation("RFQ_OPENING_PARTICIPANT_DUPLICATE", "Opening participants cannot be duplicated.");

        var receipts = rfq.Receipts.Where(item => !item.IsDeleted).OrderBy(item => item.ReceiptSequence).ToList();
        if (receipts.Count == 0)
            throw Conflict("RFQ_NO_RECEIPTS", "There are no sealed quotation receipts to enter in the opening register.");
        var securitySpecs = request.Securities.Where(item => item.ReceiptId != Guid.Empty).ToList();
        if (securitySpecs.GroupBy(item => item.ReceiptId).Any(group => group.Count() > 1) ||
            securitySpecs.Any(item => receipts.All(receipt => receipt.Id != item.ReceiptId)))
            throw Validation("RFQ_OPENING_SECURITY_INVALID", "Opening security references must identify unique receipts in this RFQ.");
        if (securitySpecs.Any(item => string.IsNullOrWhiteSpace(item.SecurityReference)))
            throw Validation("RFQ_OPENING_SECURITY_REFERENCE_REQUIRED", "Each declared quotation security requires a reference.");
        var securityByReceipt = securitySpecs.ToDictionary(item => item.ReceiptId, item => item.SecurityReference.Trim());
        var now = DateTime.UtcNow;
        var participantSnapshot = participantSpecs.Select(item => new
        {
            item.ParticipantUserId,
            participantName = item.ParticipantName.Trim(),
            roleName = item.RoleName.Trim(),
            item.IsObserver,
            signatureReference = item.SignatureReference.Trim(),
            signedAtUtc = now
        }).ToArray();
        var entrySnapshot = receipts.Select(item => new
        {
            item.Id,
            item.QuoteId,
            item.BusinessPartnerId,
            item.ReceiptNumber,
            item.ReceivedAtUtc,
            item.Disposition,
            declaredAmount = item.Quote.Items.Where(line => !line.IsDeleted).Sum(line => line.LineTotal),
            securityReference = securityByReceipt.GetValueOrDefault(item.Id),
            rejectionReason = item.Disposition == ProcurementRfqReceiptDisposition.LateRejected ? "Received after the locked submission deadline." : null,
            quoteIntegrityHash = item.IntegrityHash
        }).ToArray();
        var participantJson = JsonSerializer.Serialize(participantSnapshot, JsonOptions);
        var registerJson = JsonSerializer.Serialize(entrySnapshot, JsonOptions);
        var register = new ProcurementRfqOpeningRegister
        {
            TenantId = _currentUser.TenantId,
            RfqId = rfqId,
            OpenedAtUtc = now,
            ClosedAtUtc = now,
            OpenedByUserId = _currentUser.UserId,
            OpenedByName = ActorName(),
            EvidenceReference = request.EvidenceReference.Trim(),
            ParticipantSnapshotJson = participantJson,
            RegisterSnapshotJson = registerJson,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        await OpeningRegisters.AddAsync(register);
        var integrityPayload = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.rfq-opening-register.v1",
            registerId = register.Id,
            rfqId,
            openedAtUtc = now,
            evidenceReference = request.EvidenceReference.Trim(),
            participants = participantSnapshot,
            entries = entrySnapshot
        }, JsonOptions);
        register.IntegrityHash = ComputeHash(integrityPayload);
        foreach (var item in participantSpecs)
        {
            var participant = new ProcurementRfqOpeningParticipant
            {
                TenantId = _currentUser.TenantId, OpeningRegisterId = register.Id,
                ParticipantUserId = item.ParticipantUserId,
                ParticipantName = item.ParticipantName.Trim(), RoleName = item.RoleName.Trim(), IsObserver = item.IsObserver,
                SignedAtUtc = now, SignatureReference = item.SignatureReference.Trim(), CreatedAt = now,
                CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId
            };
            register.Participants.Add(participant);
            await OpeningParticipants.AddAsync(participant);
        }
        foreach (var item in receipts)
        {
            var entry = new ProcurementRfqOpeningEntry
            {
                TenantId = _currentUser.TenantId, OpeningRegisterId = register.Id,
                ReceiptId = item.Id, QuoteId = item.QuoteId, BusinessPartnerId = item.BusinessPartnerId,
                ReceiptNumber = item.ReceiptNumber, ReceivedAtUtc = item.ReceivedAtUtc, Disposition = item.Disposition,
                DeclaredAmount = item.Quote.Items.Where(line => !line.IsDeleted).Sum(line => line.LineTotal),
                SecurityReference = securityByReceipt.GetValueOrDefault(item.Id),
                RejectionReason = item.Disposition == ProcurementRfqReceiptDisposition.LateRejected ? "Received after the locked submission deadline." : null,
                QuoteIntegrityHash = item.IntegrityHash, CreatedAt = now, CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId
            };
            register.Entries.Add(entry);
            await OpeningEntries.AddAsync(entry);
            item.OpeningRegisterId = register.Id;
            item.OpenedAtUtc = now;
            await Receipts.UpdateAsync(item);
        }
        rfq.Status = "Evaluation";
        rfq.UpdatedAt = now;
        rfq.LastModifiedById = _currentUser.UserId;
        await Rfqs.UpdateAsync(rfq);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(rfq, rule, "OpeningRegisterCompleted", ProcurementControlEventResult.Allowed,
            new { ReceiptCount = receipts.Count, ParticipantCount = participantSpecs.Count },
            new { register.Id, register.IntegrityHash, OnTimeCount = receipts.Count(item => item.Disposition == ProcurementRfqReceiptDisposition.OnTimeAccepted), LateCount = receipts.Count(item => item.Disposition == ProcurementRfqReceiptDisposition.LateRejected) },
            normalizedCorrelation, cancellationToken,
            External(register.EvidenceReference, "RFQ opening evidence", "SRC-004"));
        return MapOpening(register);
    }

    public async Task<ProcurementRfqEvaluationDto> SaveEvaluationAsync(
        Guid rfqId,
        SaveProcurementRfqEvaluationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var rfq = await LoadRfqAsync(rfqId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(EvaluatePermission, rfq.RfqNumber, normalizedCorrelation, cancellationToken);
        await RevalidateRfqAsync(rfq, normalizedCorrelation, cancellationToken);
        var rule = await ResolveMethodRuleAsync(rfq, cancellationToken);
        var register = rfq.OpeningRegister ?? throw Conflict("RFQ_OPENING_REQUIRED", "Complete the immutable opening register before evaluation.");
        var onTimeReceipts = rfq.Receipts.Where(item => !item.IsDeleted && item.Disposition == ProcurementRfqReceiptDisposition.OnTimeAccepted).ToList();
        if (onTimeReceipts.Count < rule.MinimumQuotationCount)
            throw Validation("RFQ_MINIMUM_QUOTATIONS_NOT_MET", $"At least {rule.MinimumQuotationCount} on-time quotations are required; {onTimeReceipts.Count} were received.");
        if (string.IsNullOrWhiteSpace(request.RecommendationReason) || string.IsNullOrWhiteSpace(request.EvidenceReference))
            throw Validation("RFQ_EVALUATION_EVIDENCE_REQUIRED", "Recommendation rationale and an evidence reference are required.");
        var mode = NormalizeAwardMode(request.AwardMode);
        var rfqItems = rfq.Items.Where(item => !item.IsDeleted).OrderBy(item => item.LineNumber).ToList();
        var lines = request.Lines.Where(item => item.RfqItemId != Guid.Empty && item.QuoteId != Guid.Empty).ToList();
        if (lines.Count != rfqItems.Count || lines.Select(item => item.RfqItemId).Distinct().Count() != rfqItems.Count)
            throw Validation("RFQ_EVALUATION_LINE_COVERAGE", "Evaluation must select exactly one on-time quote for every RFQ line.");
        if (lines.Any(item => item.TechnicalScore is < 0 or > 100 || item.CommercialScore is < 0 or > 100 || item.TotalScore is < 0 or > 100))
            throw Validation("RFQ_EVALUATION_SCORE_RANGE", "Evaluation scores must be between 0 and 100.");
        if (mode == "WinnerTakesAll" && lines.Select(item => item.QuoteId).Distinct().Count() != 1)
            throw Validation("RFQ_WINNER_SELECTION_INVALID", "Winner-takes-all evaluation must select one supplier quote for every line.");
        var validQuoteIds = onTimeReceipts.Select(item => item.QuoteId).ToHashSet();
        if (lines.Any(item => !validQuoteIds.Contains(item.QuoteId)))
            throw Validation("RFQ_LATE_QUOTE_NOT_ELIGIBLE", "Only quotations accepted on time and recorded in the opening register can be evaluated.");
        var evaluatorConflicts = rfq.Quotes.Where(item => !item.IsDeleted && item.SubmittedByUserId.HasValue)
            .Select(item => item.SubmittedByUserId!.Value)
            .Concat(new[] { rfq.CreatedById, rfq.SourcePurchaseRequisition?.RequestedById }
                .Where(item => item.HasValue).Select(item => item!.Value))
            .Where(item => item != Guid.Empty).Distinct().ToList();
        if (evaluatorConflicts.Count != 0)
        {
            var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
            {
                ControlCode = "SOD-RFQ-EVALUATOR-CONFLICT",
                SourceType = SourceType,
                SourceReference = rfq.RfqNumber,
                ProhibitedActorUserIds = evaluatorConflicts
            }, normalizedCorrelation, cancellationToken);
            if (!sod.Allowed) throw new ProcurementRfqControlAuthorizationException(sod.Message);
        }

        var evaluation = rfq.Evaluation;
        var isNewEvaluation = evaluation is null;
        var now = DateTime.UtcNow;
        if (evaluation is null)
        {
            evaluation = new ProcurementRfqEvaluation
            {
                Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, RfqId = rfq.Id,
                OpeningRegisterId = register.Id, Status = ProcurementRfqEvaluationStatus.Draft,
                MethodRuleId = rule.Id, MethodRuleCode = rule.RuleCode,
                WorkflowDefinitionId = rule.WorkflowDefinitionId,
                CreatedAt = now, CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId
            };
            await Evaluations.AddAsync(evaluation);
            rfq.Evaluation = evaluation;
        }
        else
        {
            if (evaluation.Status != ProcurementRfqEvaluationStatus.Draft)
                throw Conflict("RFQ_EVALUATION_LOCKED", "A submitted evaluation recommendation is immutable.");
            EnsureRowVersion(evaluation.RowVersion, request.RowVersion, "RFQ_EVALUATION_VERSION_CONFLICT");
        }
        evaluation.AwardMode = mode;
        evaluation.RecommendationReason = request.RecommendationReason.Trim();
        evaluation.EvidenceReference = request.EvidenceReference.Trim();

        var existingByItem = evaluation.Lines.Where(item => !item.IsDeleted).ToDictionary(item => item.RfqItemId);
        foreach (var input in lines)
        {
            var rfqItem = rfqItems.SingleOrDefault(item => item.Id == input.RfqItemId)
                ?? throw Validation("RFQ_EVALUATION_ITEM_INVALID", "An evaluation line does not belong to this RFQ.");
            var quote = rfq.Quotes.Single(item => item.Id == input.QuoteId);
            var quoteItem = quote.Items.SingleOrDefault(item => !item.IsDeleted && item.RfqItemId == rfqItem.Id)
                ?? throw Validation("RFQ_EVALUATION_QUOTE_LINE_MISSING", $"Selected quote is missing RFQ line {rfqItem.LineNumber}.");
            if (!existingByItem.TryGetValue(rfqItem.Id, out var line))
            {
                line = new ProcurementRfqEvaluationLine
                {
                    Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, EvaluationId = evaluation.Id,
                    RfqItemId = rfqItem.Id, CreatedAt = now, CreatedBy = _currentUser.Username, CreatedById = _currentUser.UserId
                };
                evaluation.Lines.Add(line);
                await EvaluationLines.AddAsync(line);
            }
            line.QuoteId = quote.Id;
            line.BusinessPartnerId = quote.BusinessPartnerId;
            line.UnitPrice = quoteItem.UnitPrice;
            line.LineTotal = quoteItem.LineTotal;
            line.TechnicalScore = input.TechnicalScore;
            line.CommercialScore = input.CommercialScore;
            line.TotalScore = input.TotalScore;
            line.RecommendationReason = NullIfWhiteSpace(input.RecommendationReason);
        }
        CaptureEvaluationSnapshot(evaluation, rfq);
        evaluation.UpdatedAt = now;
        evaluation.LastModifiedById = _currentUser.UserId;
        if (!isNewEvaluation) await Evaluations.UpdateAsync(evaluation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(rfq, rule, "EvaluationRecommendationSaved", ProcurementControlEventResult.Allowed,
            new { mode, LineCount = lines.Count }, new { evaluation.Id, evaluation.IntegrityHash }, normalizedCorrelation, cancellationToken,
            External(evaluation.EvidenceReference, "RFQ evaluation evidence", "SRC-005"));
        return MapEvaluation(evaluation);
    }

    public async Task<ProcurementRfqEvaluationDto> SubmitEvaluationAsync(
        Guid rfqId,
        SubmitProcurementRfqEvaluationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var rfq = await LoadRfqAsync(rfqId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(EvaluatePermission, rfq.RfqNumber, normalizedCorrelation, cancellationToken);
        await RevalidateRfqAsync(rfq, normalizedCorrelation, cancellationToken);
        var rule = await ResolveMethodRuleAsync(rfq, cancellationToken);
        var evaluation = rfq.Evaluation ?? throw Conflict("RFQ_EVALUATION_REQUIRED", "Save an evaluation recommendation before submission.");
        if (evaluation.Status != ProcurementRfqEvaluationStatus.Draft)
            throw Conflict("RFQ_EVALUATION_NOT_DRAFT", "Only a Draft evaluation can be submitted.");
        EnsureRowVersion(evaluation.RowVersion, request.RowVersion, "RFQ_EVALUATION_VERSION_CONFLICT");
        if (!evaluation.WorkflowDefinitionId.HasValue || evaluation.WorkflowDefinitionId != rule.WorkflowDefinitionId)
            throw Validation("RFQ_APPROVAL_WORKFLOW_NOT_CONFIGURED", "The evaluation does not retain the exact shared workflow selected by the locked method rule.");
        if (evaluation.Lines.Count(item => !item.IsDeleted) != rfq.Items.Count(item => !item.IsDeleted))
            throw Validation("RFQ_EVALUATION_LINE_COVERAGE", "Every RFQ line must be evaluated before submission.");

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                evaluation.Status = ProcurementRfqEvaluationStatus.Submitted;
                evaluation.SubmittedAtUtc = now;
                evaluation.SubmittedByUserId = _currentUser.UserId;
                evaluation.SubmittedByName = ActorName();
                evaluation.ApprovalActorsJson = JsonSerializer.Serialize(new[] { _currentUser.UserId }, JsonOptions);
                CaptureEvaluationSnapshot(evaluation, rfq);
                rfq.Status = "PendingApproval";
                rfq.UpdatedAt = now;
                rfq.LastModifiedById = _currentUser.UserId;
                await Evaluations.UpdateAsync(evaluation);
                await Rfqs.UpdateAsync(rfq);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                var workflow = await _workflowService.StartApprovalWorkflowAsync(SourceType, rfq.Id, evaluation.WorkflowDefinitionId.Value);
                if (!workflow.Success || !workflow.WorkflowInstanceId.HasValue)
                    throw Conflict("RFQ_WORKFLOW_START_FAILED", workflow.Message ?? "The exact RFQ approval workflow could not be started.");
                evaluation.WorkflowInstanceId = workflow.WorkflowInstanceId;
                await Evaluations.UpdateAsync(evaluation);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
        await RecordAsync(rfq, rule, "EvaluationSubmittedForApproval", ProcurementControlEventResult.Allowed,
            new { evaluation.Id, evaluation.SubmittedByUserId },
            new { evaluation.WorkflowDefinitionId, evaluation.WorkflowInstanceId }, normalizedCorrelation, cancellationToken,
            External($"workflow:{evaluation.WorkflowInstanceId:N}", "RFQ approval workflow", "SRC-005"));
        return MapEvaluation(evaluation);
    }

    public async Task<ProcurementRfqEvaluationDto> DecideEvaluationAsync(
        Guid rfqId,
        DecideProcurementRfqEvaluationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var rfq = await LoadRfqAsync(rfqId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, rfq.RfqNumber, normalizedCorrelation, cancellationToken);
        await RevalidateRfqAsync(rfq, normalizedCorrelation, cancellationToken);
        var rule = await ResolveMethodRuleAsync(rfq, cancellationToken);
        var evaluation = rfq.Evaluation ?? throw NotFound("RFQ_EVALUATION_NOT_FOUND", "The RFQ evaluation was not found.");
        if (evaluation.Status != ProcurementRfqEvaluationStatus.Submitted || !evaluation.WorkflowInstanceId.HasValue)
            throw Conflict("RFQ_EVALUATION_NOT_PENDING", "Only a submitted evaluation with an active shared workflow can be decided.");
        EnsureRowVersion(evaluation.RowVersion, request.RowVersion, "RFQ_EVALUATION_VERSION_CONFLICT");
        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not "approve" and not "reject")
            throw Validation("RFQ_APPROVAL_ACTION_INVALID", "Action must be Approve or Reject.");
        if (string.IsNullOrWhiteSpace(request.ApprovalReference))
            throw Validation("RFQ_APPROVAL_REFERENCE_REQUIRED", "An approval reference is required.");
        if (!await _workflowService.CanUserApproveAsync(SourceType, rfq.Id, _currentUser.UserId))
            throw new ProcurementRfqControlAuthorizationException("The current user is not assigned to the active RFQ workflow step.");

        var prohibited = new[] { evaluation.SubmittedByUserId, rfq.CreatedById, rfq.SourcePurchaseRequisition?.RequestedById }
            .Where(item => item.HasValue && item.Value != Guid.Empty).Select(item => item!.Value)
            .Concat(rfq.Quotes.Where(item => !item.IsDeleted && item.SubmittedByUserId.HasValue)
                .Select(item => item.SubmittedByUserId!.Value))
            .Distinct().ToList();
        if (prohibited.Count != 0)
        {
            var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
            {
                ControlCode = "SOD-INITIATOR-APPROVER",
                SourceType = SourceType,
                SourceReference = rfq.RfqNumber,
                ProhibitedActorUserIds = prohibited
            }, normalizedCorrelation, cancellationToken);
            if (!sod.Allowed)
                throw new ProcurementRfqControlAuthorizationException(sod.Message);
        }

        var workflow = await _workflowService.ProcessApprovalStepAsync(SourceType, rfq.Id, _currentUser.UserId, action, request.Comments);
        if (!workflow.Success)
            throw Conflict("RFQ_WORKFLOW_DECISION_FAILED", workflow.Message ?? "The shared workflow decision failed.");
        var now = DateTime.UtcNow;
        var actors = JsonSerializer.Deserialize<List<Guid>>(evaluation.ApprovalActorsJson, JsonOptions) ?? new List<Guid>();
        if (!actors.Contains(_currentUser.UserId)) actors.Add(_currentUser.UserId);
        evaluation.ApprovalActorsJson = JsonSerializer.Serialize(actors, JsonOptions);
        evaluation.ApprovalReference = request.ApprovalReference.Trim();
        if (action == "approve" && workflow.Status == WorkflowInstanceStatus.Completed)
        {
            evaluation.Status = ProcurementRfqEvaluationStatus.Approved;
            evaluation.ApprovedAtUtc = now;
            evaluation.ApprovedByUserId = _currentUser.UserId;
            evaluation.ApprovedByName = ActorName();
            rfq.Status = "Approved";
        }
        else if (action == "reject")
        {
            if (workflow.Status is not WorkflowInstanceStatus.Cancelled and not WorkflowInstanceStatus.Failed)
                throw Conflict("RFQ_WORKFLOW_REJECTION_NOT_FINAL", "The rejection path is valid only when the shared workflow is Cancelled or Failed; a Completed workflow is an approved outcome.");
            evaluation.Status = ProcurementRfqEvaluationStatus.Rejected;
            rfq.Status = "Rejected";
        }
        CaptureEvaluationSnapshot(evaluation, rfq);
        evaluation.UpdatedAt = now;
        evaluation.LastModifiedById = _currentUser.UserId;
        rfq.UpdatedAt = now;
        rfq.LastModifiedById = _currentUser.UserId;
        await Evaluations.UpdateAsync(evaluation);
        await Rfqs.UpdateAsync(rfq);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(rfq, rule,
            evaluation.Status == ProcurementRfqEvaluationStatus.Approved ? "EvaluationApproved" : evaluation.Status == ProcurementRfqEvaluationStatus.Rejected ? "EvaluationRejected" : "EvaluationApprovalStepCompleted",
            evaluation.Status == ProcurementRfqEvaluationStatus.Rejected ? ProcurementControlEventResult.Rejected : ProcurementControlEventResult.Allowed,
            new { action, ActorUserId = _currentUser.UserId, request.Comments },
            new { evaluation.Status, WorkflowStatus = workflow.Status, evaluation.ApprovalReference }, normalizedCorrelation, cancellationToken,
            External($"workflow:{evaluation.WorkflowInstanceId:N}", "RFQ approval workflow", "SRC-005"));
        return MapEvaluation(evaluation);
    }

    public async Task<CreatePurchaseOrdersFromRfqDto> GetApprovedAwardAsync(
        Guid rfqId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var rfq = await LoadRfqAsync(rfqId, tracked: false, cancellationToken);
        await EnsureCapabilityAsync(PurchaseOrderPermission, rfq.RfqNumber, normalizedCorrelation, cancellationToken);
        await RevalidateRfqAsync(rfq, normalizedCorrelation, cancellationToken);
        var rule = await ResolveMethodRuleAsync(rfq, cancellationToken);
        var evaluation = rfq.Evaluation ?? throw Conflict("RFQ_APPROVED_EVALUATION_REQUIRED", "An approved evaluation is required before PO or contract handoff.");
        if (evaluation.Status != ProcurementRfqEvaluationStatus.Approved || !string.Equals(rfq.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            throw Conflict("RFQ_APPROVED_EVALUATION_REQUIRED", "The RFQ shared workflow must approve the evaluation before award handoff.");
        if (ComputeHash(evaluation.SnapshotJson) != evaluation.IntegrityHash)
            throw Conflict("RFQ_EVALUATION_INTEGRITY_FAILED", "The retained evaluation recommendation failed its integrity check.");
        var dto = new CreatePurchaseOrdersFromRfqDto { Mode = evaluation.AwardMode };
        if (evaluation.AwardMode == "WinnerTakesAll")
            dto.QuoteId = evaluation.Lines.Where(item => !item.IsDeleted).Select(item => item.QuoteId).Distinct().Single();
        else
            dto.Lines = evaluation.Lines.Where(item => !item.IsDeleted).OrderBy(item => item.RfqItem.LineNumber)
                .Select(item => new RfqSplitAwardLineDto { RfqItemId = item.RfqItemId, QuoteId = item.QuoteId, AwardReason = item.RecommendationReason ?? evaluation.RecommendationReason }).ToList();
        await RecordAsync(rfq, rule, "ApprovedAwardHandoffAllowed", ProcurementControlEventResult.Allowed,
            new { evaluation.Id, evaluation.IntegrityHash }, new { dto.Mode, dto.QuoteId, LineCount = dto.Lines.Count },
            normalizedCorrelation, cancellationToken,
            External(evaluation.EvidenceReference, "Approved RFQ evaluation", "SRC-005"));
        return dto;
    }

    public async Task RecordAwardHandoffAsync(
        Guid rfqId,
        IReadOnlyCollection<CreatedPurchaseOrderFromRfqDto> purchaseOrders,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var rfq = await LoadRfqAsync(rfqId, tracked: false, cancellationToken);
        var rule = await ResolveMethodRuleAsync(rfq, cancellationToken);
        await RecordAsync(rfq, rule, "AwardPurchaseOrdersCreated", ProcurementControlEventResult.Allowed,
            new { EvaluationId = rfq.Evaluation?.Id },
            new { PurchaseOrders = purchaseOrders.Select(item => new { item.PurchaseOrderId, item.OrderNumber, item.BusinessPartnerId, item.TotalAmount }).ToArray() },
            NormalizeCorrelation(correlationId), cancellationToken,
            External($"rfq:{rfq.Id:N}:award", "RFQ award handoff", "SRC-005"));
    }

    private async Task<RequestForQuotation> LoadRfqAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        IQueryable<RequestForQuotation> query = Rfqs.GetQueryable(item => item.Id == id && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Items)
            .Include(item => item.SourcePurchaseRequisition)
            .Include(item => item.Invitations).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.Quotes).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.Quotes).ThenInclude(item => item.Items).ThenInclude(item => item.RfqItem)
            .Include(item => item.Receipts).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.Receipts).ThenInclude(item => item.Quote).ThenInclude(item => item.Items).ThenInclude(item => item.RfqItem)
            .Include(item => item.OpeningRegister).ThenInclude(item => item.Participants)
            .Include(item => item.OpeningRegister).ThenInclude(item => item.Entries).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.Evaluation).ThenInclude(item => item.Lines).ThenInclude(item => item.RfqItem)
            .Include(item => item.Evaluation).ThenInclude(item => item.Lines).ThenInclude(item => item.BusinessPartner);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("RFQ_NOT_FOUND", "The RFQ was not found in the current tenant.");
    }

    private async Task<ProcurementPolicyMethodRule> ResolveMethodRuleAsync(RequestForQuotation rfq, CancellationToken cancellationToken, bool requireOperational = true)
    {
        if (!rfq.SourcingCaseId.HasValue)
            throw Validation("RFQ_SOURCING_CASE_REQUIRED", "The RFQ has no locked sourcing-case lineage.");
        var sourcingCase = await SourcingCases.GetQueryable(item => item.Id == rfq.SourcingCaseId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("RFQ_SOURCING_CASE_NOT_FOUND", "The RFQ locked sourcing case no longer exists.");
        var rule = await MethodRules.GetQueryableIncludingDeleted(item => item.Id == sourcingCase.MethodRuleId &&
                item.TenantId == _currentUser.TenantId)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("RFQ_METHOD_RULE_NOT_FOUND", "The exact method rule locked by the sourcing case no longer exists.");
        if (rule.Method != ProcurementMethodType.RequestForQuotation || (requireOperational && (rule.IsDeleted || !rule.IsAllowed || !rule.IsEnabled)))
            throw Validation("RFQ_METHOD_RULE_INVALID", "The locked method rule does not authorize the RFQ lifecycle.");
        return rule;
    }

    private async Task RevalidateRfqAsync(RequestForQuotation rfq, string correlationId, CancellationToken cancellationToken)
    {
        if (!rfq.SourcePurchaseRequisitionId.HasValue || !rfq.SourcingReleaseId.HasValue || !rfq.SourcingCaseId.HasValue)
            throw Validation("RFQ_SOURCE_LINEAGE_REQUIRED", "The RFQ has no complete immutable requisition, release, and sourcing-case lineage.");
        try
        {
            var gate = await _sourcingCaseService.RevalidateSourceEntryAsync(
                rfq.SourcePurchaseRequisitionId.Value, rfq.SourcingReleaseId.Value, rfq.SourcingCaseId.Value,
                ProcurementMethodType.RequestForQuotation, SourceType, rfq.Id, rfq.RfqNumber,
                correlationId, cancellationToken);
            if (gate.MethodRuleId != (await ResolveMethodRuleAsync(rfq, cancellationToken)).Id ||
                gate.EstimatedValue != rfq.EstimatedValue ||
                !string.Equals(gate.CurrencyCode, rfq.Currency, StringComparison.OrdinalIgnoreCase))
                throw Validation("RFQ_SOURCE_LINEAGE_STALE", "The RFQ no longer matches its current immutable sourcing-case method, value, or currency.");
        }
        catch (ProcurementRequisitionSourcingValidationException exception)
        {
            throw Validation(exception.Code, exception.Message);
        }
        catch (ProcurementSourcingCaseAuthorizationException exception)
        {
            throw new ProcurementRfqControlAuthorizationException(exception.Message);
        }
    }

    private async Task EnsureCapabilityAsync(string permissionCode, string reference, string correlationId, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permissionCode,
            SourceType = SourceType,
            SourceReference = reference
        }, correlationId, cancellationToken);
        if (!decision.Allowed) throw new ProcurementRfqControlAuthorizationException(decision.Message);
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator() || _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role => ProcurementAccessControlRegistry.FindRole(role) is not null)) return;
        throw new ProcurementRfqControlAuthorizationException("A TDC procurement or tenant-administration role is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementRfqControlAuthorizationException("An authenticated tenant context is required.");
    }

    private async Task RecordAsync(
        RequestForQuotation rfq,
        ProcurementPolicyMethodRule rule,
        string action,
        ProcurementControlEventResult result,
        object input,
        object output,
        string correlationId,
        CancellationToken cancellationToken,
        params ProcurementControlEventEvidenceReference[] evidence)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("rfq-statutory-control", rfq.TenantId, rfq.Id, action, correlationId),
            EventType = "ProcurementRfqStatutoryControl",
            Action = action,
            Result = result,
            RuleCode = rule.RuleCode,
            RuleId = rule.Id,
            DecisionKeys = Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToList(),
            SourceType = SourceType,
            SourceId = rfq.Id,
            SourceReference = rfq.RfqNumber,
            Reason = action,
            InputValues = input,
            ResultValues = output,
            Evidence = evidence.ToList(),
            CorrelationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private static void CaptureEvaluationSnapshot(ProcurementRfqEvaluation evaluation, RequestForQuotation rfq)
    {
        var snapshot = new
        {
            schemaVersion = "tdc.rfq-evaluation.v1",
            evaluation.Id,
            evaluation.RfqId,
            evaluation.OpeningRegisterId,
            evaluation.Status,
            evaluation.AwardMode,
            evaluation.RecommendationReason,
            evaluation.EvidenceReference,
            evaluation.MethodRuleId,
            evaluation.MethodRuleCode,
            evaluation.WorkflowDefinitionId,
            evaluation.WorkflowInstanceId,
            evaluation.ApprovalReference,
            evaluation.SubmittedAtUtc,
            evaluation.SubmittedByUserId,
            evaluation.ApprovedAtUtc,
            evaluation.ApprovedByUserId,
            evaluation.ApprovalActorsJson,
            lines = evaluation.Lines.Where(item => !item.IsDeleted).OrderBy(item => rfq.Items.Single(rfqItem => rfqItem.Id == item.RfqItemId).LineNumber)
                .Select(item => new { item.RfqItemId, item.QuoteId, item.BusinessPartnerId, item.UnitPrice, item.LineTotal, item.TechnicalScore, item.CommercialScore, item.TotalScore, item.RecommendationReason }).ToArray()
        };
        evaluation.SnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        evaluation.IntegrityHash = ComputeHash(evaluation.SnapshotJson);
    }

    private static ProcurementRfqControlDto MapControl(RequestForQuotation rfq, ProcurementPolicyMethodRule rule, int qualifiedInvitationCount)
    {
        var onTime = rfq.Receipts.Count(item => !item.IsDeleted && item.Disposition == ProcurementRfqReceiptDisposition.OnTimeAccepted);
        return new ProcurementRfqControlDto
        {
            RfqId = rfq.Id, RfqNumber = rfq.RfqNumber, RfqStatus = rfq.Status,
            MethodRuleId = rule.Id, MethodRuleCode = rule.RuleCode, MinimumQuotationCount = rule.MinimumQuotationCount,
            WorkflowDefinitionId = rule.WorkflowDefinitionId,
            QualifiedInvitationCount = qualifiedInvitationCount,
            OnTimeReceiptCount = onTime,
            LateReceiptCount = rfq.Receipts.Count(item => !item.IsDeleted && item.Disposition == ProcurementRfqReceiptDisposition.LateRejected),
            SubmissionDeadlinePassed = rfq.SubmissionDeadline.HasValue && EnsureUtc(rfq.SubmissionDeadline.Value) <= DateTime.UtcNow,
            QuotesRemainSealed = rfq.OpeningRegister is null,
            MinimumCompetitionMet = rule.MinimumQuotationCount > 0 && onTime >= rule.MinimumQuotationCount,
            Receipts = rfq.Receipts.Where(item => !item.IsDeleted).OrderBy(item => item.ReceiptSequence).Select(MapReceipt).ToList(),
            EvaluationOptions = rfq.OpeningRegister is null ? new List<ProcurementRfqEvaluationOptionDto>() :
                rfq.Receipts.Where(item => !item.IsDeleted && item.Disposition == ProcurementRfqReceiptDisposition.OnTimeAccepted)
                    .SelectMany(item => item.Quote.Items.Where(line => !line.IsDeleted).Select(line => new ProcurementRfqEvaluationOptionDto
                    {
                        RfqItemId = line.RfqItemId,
                        RfqLineNumber = line.RfqItem.LineNumber,
                        ItemDescription = line.RfqItem.Description,
                        QuoteId = item.QuoteId,
                        BusinessPartnerId = item.BusinessPartnerId,
                        BusinessPartnerName = item.BusinessPartner?.PartnerName ?? string.Empty,
                        UnitPrice = line.UnitPrice,
                        LineTotal = line.LineTotal
                    })).OrderBy(item => item.RfqLineNumber).ThenBy(item => item.LineTotal).ToList(),
            OpeningRegister = rfq.OpeningRegister is null ? null : MapOpening(rfq.OpeningRegister),
            Evaluation = rfq.Evaluation is null ? null : MapEvaluation(rfq.Evaluation)
        };
    }

    private static ProcurementRfqReceiptDto MapReceipt(ProcurementRfqReceipt item) => new()
    {
        Id = item.Id, QuoteId = item.QuoteId, BusinessPartnerId = item.BusinessPartnerId,
        BusinessPartnerName = item.BusinessPartner?.PartnerName ?? string.Empty, ReceiptNumber = item.ReceiptNumber,
        SubmissionDeadlineUtc = item.SubmissionDeadlineUtc, ReceivedAtUtc = item.ReceivedAtUtc,
        Disposition = item.Disposition, SealedAtUtc = item.SealedAtUtc, OpenedAtUtc = item.OpenedAtUtc,
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementRfqOpeningRegisterDto MapOpening(ProcurementRfqOpeningRegister item) => new()
    {
        Id = item.Id, OpenedAtUtc = item.OpenedAtUtc, ClosedAtUtc = item.ClosedAtUtc,
        OpenedByUserId = item.OpenedByUserId, OpenedByName = item.OpenedByName,
        EvidenceReference = item.EvidenceReference, IntegrityHash = item.IntegrityHash,
        Participants = item.Participants.Where(child => !child.IsDeleted).OrderBy(child => child.ParticipantName).Select(child => new ProcurementRfqOpeningParticipantDto
        {
            Id = child.Id, ParticipantUserId = child.ParticipantUserId, ParticipantName = child.ParticipantName,
            RoleName = child.RoleName, IsObserver = child.IsObserver, SignedAtUtc = child.SignedAtUtc,
            SignatureReference = child.SignatureReference
        }).ToList(),
        Entries = item.Entries.Where(child => !child.IsDeleted).OrderBy(child => child.ReceivedAtUtc).Select(child => new ProcurementRfqOpeningEntryDto
        {
            Id = child.Id, ReceiptId = child.ReceiptId, QuoteId = child.QuoteId,
            BusinessPartnerId = child.BusinessPartnerId, BusinessPartnerName = child.BusinessPartner?.PartnerName ?? string.Empty,
            ReceiptNumber = child.ReceiptNumber, ReceivedAtUtc = child.ReceivedAtUtc, Disposition = child.Disposition,
            DeclaredAmount = child.DeclaredAmount, SecurityReference = child.SecurityReference,
            RejectionReason = child.RejectionReason, QuoteIntegrityHash = child.QuoteIntegrityHash
        }).ToList()
    };

    private static ProcurementRfqEvaluationDto MapEvaluation(ProcurementRfqEvaluation item) => new()
    {
        Id = item.Id, Status = item.Status, AwardMode = item.AwardMode,
        RecommendationReason = item.RecommendationReason, EvidenceReference = item.EvidenceReference,
        MethodRuleId = item.MethodRuleId, MethodRuleCode = item.MethodRuleCode,
        WorkflowDefinitionId = item.WorkflowDefinitionId, WorkflowInstanceId = item.WorkflowInstanceId,
        ApprovalReference = item.ApprovalReference, SubmittedAtUtc = item.SubmittedAtUtc,
        SubmittedByName = item.SubmittedByName, ApprovedAtUtc = item.ApprovedAtUtc,
        ApprovedByName = item.ApprovedByName, IntegrityHash = item.IntegrityHash,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        Lines = item.Lines.Where(child => !child.IsDeleted).OrderBy(child => child.RfqItem.LineNumber).Select(child => new ProcurementRfqEvaluationLineDto
        {
            Id = child.Id, RfqItemId = child.RfqItemId, RfqLineNumber = child.RfqItem?.LineNumber ?? 0,
            ItemDescription = child.RfqItem?.Description ?? string.Empty, QuoteId = child.QuoteId,
            BusinessPartnerId = child.BusinessPartnerId, BusinessPartnerName = child.BusinessPartner?.PartnerName ?? string.Empty,
            UnitPrice = child.UnitPrice, LineTotal = child.LineTotal, TechnicalScore = child.TechnicalScore,
            CommercialScore = child.CommercialScore, TotalScore = child.TotalScore,
            RecommendationReason = child.RecommendationReason
        }).ToList()
    };

    private static void EnsureRowVersion(byte[] current, string? supplied, string code)
    {
        if (string.IsNullOrWhiteSpace(supplied)) throw Conflict(code, "The current row version is required.");
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Validation("RFQ_ROW_VERSION_INVALID", "RowVersion must be a valid base64 value."); }
        if (!current.SequenceEqual(parsed)) throw Conflict(code, "The RFQ evaluation changed. Reload it before continuing.");
    }

    private bool IsAdministrator() => _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");
    private string ActorName() => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);
    private static string NormalizeAwardMode(string? value) => string.Equals(value?.Trim(), "SplitAward", StringComparison.OrdinalIgnoreCase) ? "SplitAward" : string.Equals(value?.Trim(), "WinnerTakesAll", StringComparison.OrdinalIgnoreCase) ? "WinnerTakesAll" : throw Validation("RFQ_AWARD_MODE_INVALID", "AwardMode must be WinnerTakesAll or SplitAward.");
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : Truncate(value.Trim(), 100);
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string ComputeHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static ProcurementRfqControlNotFoundException NotFound(string code, string message) => new(code, message);
    private static ProcurementRfqControlConflictException Conflict(string code, string message) => new(code, message);
    private static ProcurementRfqControlValidationException Validation(string code, string message) => new(code, message);
    private static ProcurementControlEventEvidenceReference External(string reference, string label, string requirement) => new()
    {
        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
        Reference = reference,
        Label = label,
        RequirementKey = requirement
    };
}
