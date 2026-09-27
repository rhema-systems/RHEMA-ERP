using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderService : ITenderService
{
    private const string TenderEvaluatePermission = "procurement.tender.evaluate";

    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderItemRepository _itemRepository;
    private readonly ITenderDocumentRepository _documentRepository;
    private readonly ITenderInvitationRepository _invitationRepository;
    private readonly ITenderFeeRepository _feeRepository;
    private readonly ITenderEvaluatorRepository _evaluatorRepository;
    private readonly ITenderClarificationRepository _clarificationRepository;
    private readonly ITenderRevisionRepository _revisionRepository;
    private readonly ITenderViewLogRepository _viewLogRepository;
    private readonly ITenderLotRepository _lotRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly ITenderNotificationService _notificationService;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<TenderService> _logger;
    private readonly IAppEventBus _appEventBus;
    private readonly IProcurementSourcingCaseService _sourcingCaseService;
    private readonly IProcurementTenderControlService _tenderControlService;
    private readonly IProcurementTenderDocumentControlService _tenderDocumentControlService;
    private readonly IProcurementExceptionalSourcingControlService _exceptionalSourcingControlService;
    private readonly IProcurementEvaluationCommitteeControlService _evaluationCommittee;

    public TenderService(
        ITenderRepository tenderRepository,
        ITenderItemRepository itemRepository,
        ITenderDocumentRepository documentRepository,
        ITenderInvitationRepository invitationRepository,
        ITenderFeeRepository feeRepository,
        ITenderEvaluatorRepository evaluatorRepository,
        ITenderClarificationRepository clarificationRepository,
        ITenderRevisionRepository revisionRepository,
        ITenderViewLogRepository viewLogRepository,
        ITenderLotRepository lotRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        ITenderNotificationService notificationService,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ICurrentUserProvider currentUserProvider,
        IAppEventBus appEventBus,
        IProcurementSourcingCaseService sourcingCaseService,
        IProcurementTenderControlService tenderControlService,
        IProcurementTenderDocumentControlService tenderDocumentControlService,
        IProcurementExceptionalSourcingControlService exceptionalSourcingControlService,
        IProcurementEvaluationCommitteeControlService evaluationCommittee,
        ILogger<TenderService> logger)
    {
        _tenderRepository = tenderRepository;
        _itemRepository = itemRepository;
        _documentRepository = documentRepository;
        _invitationRepository = invitationRepository;
        _feeRepository = feeRepository;
        _evaluatorRepository = evaluatorRepository;
        _clarificationRepository = clarificationRepository;
        _revisionRepository = revisionRepository;
        _viewLogRepository = viewLogRepository;
        _lotRepository = lotRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _notificationService = notificationService;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _roleManager = roleManager;
        _currentUserProvider = currentUserProvider;
        _appEventBus = appEventBus;
        _sourcingCaseService = sourcingCaseService;
        _tenderControlService = tenderControlService;
        _tenderDocumentControlService = tenderDocumentControlService;
        _exceptionalSourcingControlService = exceptionalSourcingControlService;
        _evaluationCommittee = evaluationCommittee;
        _logger = logger;
    }

    public async Task<TenderDetailDto?> GetTenderByIdAsync(Guid id)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(id);
            if (tender == null) return null;

            var items = await _itemRepository.GetByTenderIdAsync(id);
            var lots = await _lotRepository.GetByTenderIdAsync(id);
            var documents = await _documentRepository.GetByTenderIdAsync(id);
            var fees = await _feeRepository.GetByTenderIdAsync(id);
            var invitations = await _invitationRepository.GetByTenderIdAsync(id);
            var clarifications = await _clarificationRepository.GetByTenderIdAsync(id);
            var evaluators = await GetTenderEvaluatorsAsync(id);
            var bidRepository = _unitOfWork.Repository<TenderBid>();
            var bids = await bidRepository.FindAsync(b => b.TenderId == id && !b.IsDeleted);

            var result = MapToDetailDto(tender, lots, items, documents, fees, invitations, clarifications, evaluators, bids);
            result.SourcingMethod = await GetSourcingMethodAsync(tender);
            result.UsesControlledTenderLifecycle = await _tenderControlService.IsControlledTenderMethodAsync(tender.Id);
            return await ProtectBidSummariesAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tender {TenderId}", id);
            throw;
        }
    }

    public async Task<TenderDetailDto?> GetTenderByNumberAsync(string tenderNumber)
    {
        try
        {
            var tender = await _tenderRepository.GetByTenderNumberAsync(tenderNumber);
            if (tender == null) return null;

            var items = await _itemRepository.GetByTenderIdAsync(tender.Id);
            var lots = await _lotRepository.GetByTenderIdAsync(tender.Id);
            var documents = await _documentRepository.GetByTenderIdAsync(tender.Id);
            var fees = await _feeRepository.GetByTenderIdAsync(tender.Id);
            var invitations = await _invitationRepository.GetByTenderIdAsync(tender.Id);
            var clarifications = await _clarificationRepository.GetByTenderIdAsync(tender.Id);
            var evaluators = await GetTenderEvaluatorsAsync(tender.Id);
            var bidRepository = _unitOfWork.Repository<TenderBid>();
            var bids = await bidRepository.FindAsync(b => b.TenderId == tender.Id && !b.IsDeleted);

            var result = MapToDetailDto(tender, lots, items, documents, fees, invitations, clarifications, evaluators, bids);
            result.SourcingMethod = await GetSourcingMethodAsync(tender);
            result.UsesControlledTenderLifecycle = await _tenderControlService.IsControlledTenderMethodAsync(tender.Id);
            return await ProtectBidSummariesAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tender by number {TenderNumber}", tenderNumber);
            throw;
        }
    }

    public async Task<PagedResult<TenderDto>> GetTendersAsync(int page, int pageSize, string? search = null, string? status = null, string? tenderType = null)
    {
        try
        {
            var result = await _tenderRepository.GetTendersAsync(page, pageSize, search, status, tenderType);

            return new PagedResult<TenderDto>
            {
                Items = result.Items.Select(MapToDto).ToList(),
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenders");
            throw;
        }
    }

    public async Task<IEnumerable<TenderDto>> GetPublishedTendersAsync()
    {
        try
        {
            var tenders = await _tenderRepository.GetPublishedTendersAsync();
            return tenders.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving published tenders");
            throw;
        }
    }

    public async Task<IEnumerable<TenderDto>> GetActiveTendersAsync()
    {
        try
        {
            var tenders = await _tenderRepository.GetActiveTendersAsync();
            return tenders.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active tenders");
            throw;
        }
    }

    public async Task<TenderDetailDto> CreateTenderAsync(CreateTenderDto dto)
    {
        try
        {
            ValidateTenderSchedule(dto.SubmissionDeadline, dto.OpeningDate);
            _ = ValidateBidValidityPeriod(dto.BidValidityPeriodDays);
            await TenderEvaluationConfiguration.ValidateAsync(_unitOfWork, _currentUserProvider.TenantId,
                dto.EvaluationTemplateId, dto.UseQCBSEvaluation, dto.TechnicalWeight,
                dto.FinancialWeight, dto.MinimumTechnicalScore);
            if (dto.SourcePurchaseRequisitionId == Guid.Empty)
                throw new ProcurementRequisitionSourcingValidationException(
                    "TENDER_SOURCE_REQUISITION_REQUIRED", "A tender must be created from a released purchase requisition.");
            var requestForQuotation = IsRequestForQuotation(dto.TenderType);
            var sourceType = requestForQuotation ? "RequestForQuotation" : "Tender";
            var gate = await _sourcingCaseService.EnforceSourceEntryAsync(
                dto.SourcePurchaseRequisitionId,
                requestForQuotation ? ProcurementMethodType.RequestForQuotation : null,
                sourceType, $"new-tender:{dto.Title}", Guid.NewGuid().ToString("N"));
            if (gate.SelectedMethod == ProcurementMethodType.PettyPurchase)
            {
                if (dto.TenderType != "PettyPurchase" || dto.SubmissionDeadline.HasValue || dto.OpeningDate.HasValue ||
                    dto.EvaluationTemplateId.HasValue || dto.UseQCBSEvaluation || dto.AllowPartialBids)
                    throw new ProcurementRequisitionSourcingValidationException("PETTY_PREPARATION_REQUIRED",
                        "Use Petty Purchase preparation. Public bidding dates, scoring templates and partial bids do not apply.");
                // The client cannot substitute items or quantities on the small-purchase route.
                var sourceItems = await _unitOfWork.Repository<PurchaseRequisitionItem>().GetQueryable(item =>
                    item.RequisitionId == dto.SourcePurchaseRequisitionId && item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted)
                    .Include(item => item.InventoryItem).OrderBy(item => item.Id).ToListAsync();
                if (sourceItems.Count == 0)
                    throw new ProcurementRequisitionSourcingValidationException("PETTY_SOURCE_ITEMS_REQUIRED", "The approved requisition has no current items.");
                dto.Items = sourceItems.Select((item, index) => new CreateTenderItemDto
                {
                    LineNumber = index + 1, ItemCode = item.InventoryItem?.ItemCode,
                    Description = item.ItemDescription, Quantity = item.Quantity, UnitOfMeasure = item.UnitOfMeasure,
                    Specifications = item.Specifications, RequiredDeliveryDate = item.RequiredDate
                }).ToList();
            }
            else if (dto.TenderType == "PettyPurchase")
                throw new ProcurementRequisitionSourcingValidationException("PETTY_METHOD_MISMATCH", "The locked sourcing case does not select Petty Purchase.");
            if (dto.SourceProcurementPlanItemId.HasValue &&
                gate.SourcePlanItemId != dto.SourceProcurementPlanItemId.Value)
                throw new ProcurementRequisitionSourcingValidationException(
                    "TENDER_PLAN_ITEM_LINEAGE_MISMATCH", "The selected requisition was not released for this procurement-plan item.");
            if (dto.EstimatedValue.HasValue && dto.EstimatedValue.Value != gate.EstimatedValue)
                throw new ProcurementRequisitionSourcingValidationException("TENDER_CASE_VALUE_MISMATCH", "Tender value must equal the locked sourcing-case value.");
            if (!string.IsNullOrWhiteSpace(dto.Currency) && !string.Equals(dto.Currency.Trim(), gate.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new ProcurementRequisitionSourcingValidationException("TENDER_CASE_CURRENCY_MISMATCH", "Tender currency must equal the locked sourcing-case currency.");
            // Generate tender number
            var tenderNumber = await _tenderRepository.GenerateTenderNumberAsync();

            var tender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderNumber = tenderNumber,
                Title = dto.Title,
                Description = dto.Description,
                TenderType = dto.TenderType,
                Status = "Draft",
                SubmissionDeadline = dto.SubmissionDeadline,
                OpeningDate = dto.OpeningDate,
                EstimatedValue = gate.EstimatedValue,
                Currency = gate.CurrencyCode,
                SourcePurchaseRequisitionId = dto.SourcePurchaseRequisitionId,
                SourcingReleaseId = gate.SourcingReleaseId,
                SourcingCaseId = gate.SourcingCaseId,
                MinimumPerformanceRating = dto.MinimumPerformanceRating,
                RequiresPrequalification = dto.RequiresPrequalification,
                AllowPartialBids = dto.AllowPartialBids,
                PriceWeightage = dto.PriceWeightage,
                QualityWeightage = dto.QualityWeightage,
                DeliveryWeightage = dto.DeliveryWeightage,
                ExperienceWeightage = dto.ExperienceWeightage,
                EvaluationCriteriaJson = dto.EvaluationCriteriaJson,
                // QCBS Configuration
                UseQCBSEvaluation = dto.UseQCBSEvaluation,
                MinimumTechnicalScore = dto.MinimumTechnicalScore,
                TechnicalWeight = dto.TechnicalWeight,
                FinancialWeight = dto.FinancialWeight,
                TermsAndConditions = dto.TermsAndConditions,
                BidValidityPeriodDays = ValidateBidValidityPeriod(dto.BidValidityPeriodDays),
                RequiredDocuments = dto.RequiredDocuments,
                RequiresAcceptanceDeclaration = dto.RequiresAcceptanceDeclaration,
                EvaluationTemplateId = dto.EvaluationTemplateId,
                Notes = dto.Notes,
                CreatedById = _currentUserProvider.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _tenderRepository.CreateAsync(tender);

            // Add tender items if provided
            var items = new List<TenderItem>();
            if (dto.Items != null && dto.Items.Any())
            {
                foreach (var itemDto in dto.Items)
                {
                    var item = new TenderItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _currentUserProvider.TenantId,
                        TenderId = tender.Id,
                        LineNumber = itemDto.LineNumber,
                        ItemCode = itemDto.ItemCode,
                        Description = itemDto.Description,
                        Quantity = itemDto.Quantity,
                        UnitOfMeasure = itemDto.UnitOfMeasure,
                        Specifications = itemDto.Specifications,
                        RequiredDeliveryDate = itemDto.RequiredDeliveryDate,
                        DeliveryLocation = itemDto.DeliveryLocation,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _itemRepository.CreateAsync(item);
                    items.Add(item);
                }
            }

            if (gate.SourcingCaseId.HasValue)
                await _sourcingCaseService.RegisterSourceRequestAsync(gate.SourcingCaseId.Value, sourceType,
                    tender.Id, tender.TenderNumber, Guid.NewGuid().ToString("N"));

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created tender {TenderId}", tender.Id);

            // Publish event for admin-configurable notification topics (best-effort).
            try
            {
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = tender.TenantId,
                    EntityType = "Tender",
                    Activity = "Created",
                    Audience = "Internal",
                    EntityId = tender.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>
                    {
                        ["TenderId"] = tender.Id,
                        ["TenderNumber"] = tender.TenderNumber ?? string.Empty,
                        ["Title"] = tender.Title ?? string.Empty,
                        ["Status"] = tender.Status ?? string.Empty,
                        ["TenderType"] = tender.TenderType ?? string.Empty,
                        ["SubmissionDeadline"] = tender.SubmissionDeadline?.ToString("o") ?? string.Empty,
                        ["OpeningDate"] = tender.OpeningDate?.ToString("o") ?? string.Empty
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish Tender.Created entity activity event for tender {TenderId}", tender.Id);
            }

            return MapToDetailDto(tender, new List<TenderLot>(), items, new List<TenderDocument>(), new List<TenderFee>(), new List<TenderInvitation>(), new List<TenderClarification>(), new List<TenderEvaluatorDto>(), new List<TenderBid>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tender");
            throw;
        }
    }

    public async Task<TenderDetailDto> UpdateTenderAsync(Guid id, UpdateTenderDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Tender with ID {id} not found");
            Guid? sourceCaseIdToRegister = null;
            string? sourceTypeToRegister = null;

            if (tender.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft tenders can be updated");
            }
            ValidateTenderSchedule(dto.SubmissionDeadline, dto.OpeningDate);
            _ = ValidateBidValidityPeriod(dto.BidValidityPeriodDays);
            await TenderEvaluationConfiguration.ValidateAsync(_unitOfWork, tender.TenantId,
                dto.EvaluationTemplateId, dto.UseQCBSEvaluation, dto.TechnicalWeight,
                dto.FinancialWeight, dto.MinimumTechnicalScore);
            if (tender.SourcePurchaseRequisitionId.HasValue)
            {
                var requestForQuotation = IsRequestForQuotation(tender.TenderType);
                var gate = await _sourcingCaseService.EnforceSourceEntryAsync(tender.SourcePurchaseRequisitionId.Value,
                    requestForQuotation ? ProcurementMethodType.RequestForQuotation : null,
                    requestForQuotation ? "RequestForQuotation" : "Tender", tender.TenderNumber, Guid.NewGuid().ToString("N"));
                EnsureSourceLineage(tender.SourcingReleaseId, tender.SourcingCaseId, gate);
                tender.SourcingReleaseId = gate.SourcingReleaseId;
                tender.SourcingCaseId = gate.SourcingCaseId;
                sourceCaseIdToRegister = gate.SourcingCaseId;
                sourceTypeToRegister = requestForQuotation ? "RequestForQuotation" : "Tender";
                if (dto.EstimatedValue.HasValue && dto.EstimatedValue.Value != gate.EstimatedValue)
                    throw new ProcurementRequisitionSourcingValidationException("TENDER_CASE_VALUE_MISMATCH", "Tender value must remain equal to the locked sourcing-case value.");
                if (!string.IsNullOrWhiteSpace(dto.Currency) && !string.Equals(dto.Currency.Trim(), gate.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                    throw new ProcurementRequisitionSourcingValidationException("TENDER_CASE_CURRENCY_MISMATCH", "Tender currency must remain equal to the locked sourcing-case currency.");
            }

            tender.Title = dto.Title;
            tender.Description = dto.Description;
            tender.SubmissionDeadline = dto.SubmissionDeadline;
            tender.OpeningDate = dto.OpeningDate;
            tender.EstimatedValue = dto.EstimatedValue;
            tender.Currency = dto.Currency;
            tender.MinimumPerformanceRating = dto.MinimumPerformanceRating;
            tender.RequiresPrequalification = dto.RequiresPrequalification;
            tender.AllowPartialBids = dto.AllowPartialBids;
            tender.PriceWeightage = dto.PriceWeightage;
            tender.QualityWeightage = dto.QualityWeightage;
            tender.DeliveryWeightage = dto.DeliveryWeightage;
            tender.ExperienceWeightage = dto.ExperienceWeightage;
            tender.EvaluationCriteriaJson = dto.EvaluationCriteriaJson;
            // QCBS Configuration
            tender.UseQCBSEvaluation = dto.UseQCBSEvaluation;
            tender.MinimumTechnicalScore = dto.MinimumTechnicalScore;
            tender.TechnicalWeight = dto.TechnicalWeight;
            tender.FinancialWeight = dto.FinancialWeight;
            tender.TermsAndConditions = dto.TermsAndConditions;
            tender.BidValidityPeriodDays = ValidateBidValidityPeriod(dto.BidValidityPeriodDays);
            tender.RequiredDocuments = dto.RequiredDocuments;
            tender.RequiresAcceptanceDeclaration = dto.RequiresAcceptanceDeclaration;
            tender.EvaluationTemplateId = dto.EvaluationTemplateId;
            tender.Notes = dto.Notes;
            tender.UpdatedAt = DateTime.UtcNow;

            await _tenderRepository.UpdateAsync(tender);
            if (sourceCaseIdToRegister.HasValue)
            {
                await _sourcingCaseService.RegisterSourceRequestAsync(
                    sourceCaseIdToRegister.Value,
                    sourceTypeToRegister!,
                    tender.Id,
                    tender.TenderNumber,
                    Guid.NewGuid().ToString("N"));
            }
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated tender {TenderId}", id);

            var items = await _itemRepository.GetByTenderIdAsync(id);
            var lots = await _lotRepository.GetByTenderIdAsync(id);
            var documents = await _documentRepository.GetByTenderIdAsync(id);
            var fees = await _feeRepository.GetByTenderIdAsync(id);
            var invitations = await _invitationRepository.GetByTenderIdAsync(id);
            var clarifications = await _clarificationRepository.GetByTenderIdAsync(id);
            var evaluators = await GetTenderEvaluatorsAsync(id);
            var bidRepository = _unitOfWork.Repository<TenderBid>();
            var bids = await bidRepository.FindAsync(b => b.TenderId == id && !b.IsDeleted);

            return await ProtectBidSummariesAsync(MapToDetailDto(tender, lots, items, documents, fees, invitations, clarifications, evaluators, bids));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender {TenderId}", id);
            throw;
        }
    }

    public async Task SubmitTenderForApprovalAsync(Guid id, Guid userId)
    {
        var tender = await _tenderRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Tender with ID {id} not found");

        if (!string.Equals(tender.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Tender must be in Draft status to submit for approval (current status: '{tender.Status}')");
        }

        await TenderEvaluationConfiguration.ValidateAsync(_unitOfWork, tender);
        var workflowResult = await _workflowIntegrationService.SubmitAsync("Tender", id);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start workflow");
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("Tender");
        tender.ApprovalRequired = workflowResult.ApprovalRequired;
        statusAdapter.ApplySubmitOutcome(tender, workflowResult, userId);
        tender.UpdatedAt = DateTime.UtcNow;

        await _tenderRepository.UpdateAsync(tender);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ApproveTenderAsync(Guid id, Guid userId, string? comments = null)
    {
        var tender = await _tenderRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Tender with ID {id} not found");

        if (!string.Equals(tender.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Tender must be in Submitted status to approve (current status: '{tender.Status}')");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync("Tender", id, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        await TenderEvaluationConfiguration.ValidateAsync(_unitOfWork, tender);
        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            "Tender",
            id,
            userId,
            "Approve",
            comments);

        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process approval");
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("Tender");
        statusAdapter.ApplyApprovalOutcome(tender, workflowResult.Outcome, userId);
        tender.UpdatedAt = DateTime.UtcNow;

        await _tenderRepository.UpdateAsync(tender);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RejectTenderAsync(Guid id, Guid userId, string reason, string? comments = null)
    {
        var tender = await _tenderRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Tender with ID {id} not found");

        if (!string.Equals(tender.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Tender must be in Submitted status to reject (current status: '{tender.Status}')");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync("Tender", id, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        var rejectionText = !string.IsNullOrWhiteSpace(comments) ? comments : reason;
        if (string.IsNullOrWhiteSpace(rejectionText))
        {
            throw new InvalidOperationException("Rejection reason is required");
        }

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            "Tender",
            id,
            userId,
            "Reject",
            rejectionText);

        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process rejection");
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("Tender");
        statusAdapter.ApplyApprovalOutcome(tender, workflowResult.Outcome, userId, rejectionText);
        tender.UpdatedAt = DateTime.UtcNow;

        await _tenderRepository.UpdateAsync(tender);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<TenderDetailDto> PublishTenderAsync(Guid id, PublishTenderDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Tender with ID {id} not found");

            if (!string.Equals(tender.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Tender cannot be published in current status: {tender.Status}");
            }
            await TenderEvaluationConfiguration.ValidateAsync(_unitOfWork, tender);
            foreach (var recipientId in (dto.InvitedBusinessPartnerIds ?? []).Where(value => value != Guid.Empty).Distinct())
            {
                var recipient = await _businessPartnerRepository.GetByIdAsync(recipientId);
                if (recipient is null || recipient.IsDeleted ||
                    recipient.TenantId != _currentUserProvider.TenantId || recipient.TenantId != tender.TenantId ||
                    !recipient.IsActive || recipient.IsBlacklisted ||
                    !(BusinessPartnerRoles.CanProcure(recipient.PartnerType)))
                    throw new ProcurementRequisitionSourcingValidationException(
                        "TENDER_PUBLICATION_RECIPIENT_INVALID",
                        "Publication notices require an active, non-blacklisted supplier record in the current tenant.");
            }
            ValidateTenderSchedule(dto.SubmissionDeadline, dto.OpeningDate);
            if (!tender.SourcePurchaseRequisitionId.HasValue)
                throw new ProcurementRequisitionSourcingValidationException(
                    "TENDER_SOURCE_REQUISITION_REQUIRED", "A tender cannot be published without a source purchase requisition and current sourcing release.");
            var requestForQuotation = IsRequestForQuotation(tender.TenderType);
            var gate = await _sourcingCaseService.EnforceSourceEntryAsync(tender.SourcePurchaseRequisitionId.Value,
                requestForQuotation ? ProcurementMethodType.RequestForQuotation : null,
                requestForQuotation ? "RequestForQuotation" : "Tender", tender.TenderNumber, Guid.NewGuid().ToString("N"));
            EnsureSourceLineage(tender.SourcingReleaseId, tender.SourcingCaseId, gate);
            tender.SourcingReleaseId = gate.SourcingReleaseId;
            tender.SourcingCaseId = gate.SourcingCaseId;
            if (tender.EstimatedValue != gate.EstimatedValue || !string.Equals(tender.Currency, gate.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new ProcurementRequisitionSourcingValidationException("TENDER_CASE_VALUE_MISMATCH", "Tender value and currency no longer match the locked sourcing case.");

            if (gate.SelectedMethod is ProcurementMethodType.RestrictedTendering or ProcurementMethodType.SingleSource or ProcurementMethodType.PettyPurchase)
            {
                throw new ProcurementExceptionalSourcingConflictException(
                    "EXCEPTIONAL_CONTROL_REQUIRED",
                    "Restricted Tendering, Single Source, and Petty Purchase cases must be prepared, approved, and released through the dedicated noncompetitive-sourcing control.");
            }

            if (UsesAdvancedSourcingControls(gate))
            {
                var documentCorrelationId = Guid.NewGuid().ToString("N");
                await _tenderDocumentControlService.EnsurePublicationReadyAsync(
                    ProcurementTenderDocumentSourceType.Tender, tender.Id, dto.SubmissionDeadline,
                    documentCorrelationId);
                // Tender publication is an announcement, not document issuance.
                // Legacy RFQ publication does attach a document package, so that
                // separate dispatch boundary must still verify recorded access.
                if (requestForQuotation)
                    await _tenderDocumentControlService.EnsureDispatchReadyAsync(
                        ProcurementTenderDocumentSourceType.Tender, tender.Id,
                        (dto.InvitedBusinessPartnerIds ?? []).Where(item => item != Guid.Empty).Distinct().ToList(),
                        (dto.ExternalRecipientEmails ?? []).Where(item => !string.IsNullOrWhiteSpace(item)).ToList(),
                        documentCorrelationId);
            }
            if (!RequiresControlledPublication(gate))
            {
                ValidateReleaseOnlyPublication(tender, dto, DateTime.UtcNow);
            }
            if (RequiresControlledPublication(gate))
            {
                if (!dto.OpeningDate.HasValue)
                    throw new ProcurementTenderControlValidationException("TENDER_OPENING_REQUIRED", "Controlled tender publication requires an opening date.");
                await _tenderControlService.PublishAsync(id, new PublishProcurementTenderRequest
                {
                    AdvertisementReference = dto.AdvertisementReference ?? string.Empty,
                    PublicationChannel = dto.PublicationChannel ?? string.Empty,
                    TenderDocumentReference = dto.TenderDocumentReference ?? string.Empty,
                    TenderDocumentVersion = dto.TenderDocumentVersion ?? string.Empty,
                    DocumentFee = dto.DocumentFee,
                    AdvertisementEvidenceReference = dto.AdvertisementEvidenceReference ?? string.Empty,
                    SubmissionDeadlineUtc = dto.SubmissionDeadline,
                    OpeningScheduledAtUtc = dto.OpeningDate.Value
                }, Guid.NewGuid().ToString("N"));
            }
            tender.Status = "Published";
            tender.PublishDate = DateTime.UtcNow; // Always publish immediately
            tender.SubmissionDeadline = dto.SubmissionDeadline;
            tender.OpeningDate = dto.OpeningDate;
            tender.PublishedById = _currentUserProvider.UserId;
            tender.UpdatedAt = DateTime.UtcNow;

            await _tenderRepository.UpdateAsync(tender);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Published tender {TenderId}", id);

            // Publish events for admin-configurable notification topics (best-effort).
            try
            {
                var baseData = new Dictionary<string, object>
                {
                    ["TenderId"] = tender.Id,
                    ["TenderNumber"] = tender.TenderNumber ?? string.Empty,
                    ["Title"] = tender.Title ?? string.Empty,
                    ["Status"] = tender.Status ?? string.Empty,
                    ["SubmissionDeadline"] = tender.SubmissionDeadline?.ToString("o") ?? string.Empty,
                    ["OpeningDate"] = tender.OpeningDate?.ToString("o") ?? string.Empty,
                    ["PublishedById"] = tender.PublishedById ?? Guid.Empty
                };

                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = tender.TenantId,
                    EntityType = "Tender",
                    Activity = "Published",
                    Audience = "Internal",
                    EntityId = tender.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>(baseData)
                });

                if (dto.InvitedBusinessPartnerIds != null)
                {
                    foreach (var bpId in dto.InvitedBusinessPartnerIds.Where(x => x != Guid.Empty).Distinct())
                    {
                        var supplierData = new Dictionary<string, object>(baseData)
                        {
                            ["BusinessPartnerId"] = bpId
                        };

                        await _appEventBus.PublishAsync(new EntityActivityEvent
                        {
                            TenantId = tender.TenantId,
                            EntityType = "Tender",
                            Activity = "Published",
                            Audience = "Supplier",
                            EntityId = tender.Id,
                            TriggeredByUserId = _currentUserProvider.UserId,
                            Data = supplierData
                        });
                    }
                }

                if (dto.ExternalRecipientEmails != null && dto.ExternalRecipientEmails.Any())
                {
                    var supplierData = new Dictionary<string, object>(baseData)
                    {
                        ["Emails"] = string.Join(",", dto.ExternalRecipientEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()))
                    };

                    await _appEventBus.PublishAsync(new EntityActivityEvent
                    {
                        TenantId = tender.TenantId,
                        EntityType = "Tender",
                        Activity = "Published",
                        Audience = "Supplier",
                        EntityId = tender.Id,
                        TriggeredByUserId = _currentUserProvider.UserId,
                        Data = supplierData
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish Tender.Published entity activity event for tender {TenderId}", id);
            }

            // Send notifications to invited business partners
            if (dto.InvitedBusinessPartnerIds != null && dto.InvitedBusinessPartnerIds.Any())
            {
                await _notificationService.SendTenderPublishedNotificationAsync(id, dto.InvitedBusinessPartnerIds, dto.ExternalRecipientEmails);
            }
            else if (dto.ExternalRecipientEmails != null && dto.ExternalRecipientEmails.Any())
            {
                // Allow publish notifications for "public" recipients even if no business partners were invited.
                await _notificationService.SendTenderPublishedNotificationAsync(id, new List<Guid>(), dto.ExternalRecipientEmails);
            }

            var items = await _itemRepository.GetByTenderIdAsync(id);
            var lots = await _lotRepository.GetByTenderIdAsync(id);
            var documents = await _documentRepository.GetByTenderIdAsync(id);
            var fees = await _feeRepository.GetByTenderIdAsync(id);
            var invitations = await _invitationRepository.GetByTenderIdAsync(id);
            var clarifications = await _clarificationRepository.GetByTenderIdAsync(id);
            var evaluators = await GetTenderEvaluatorsAsync(id);
            var bidRepository = _unitOfWork.Repository<TenderBid>();
            var bids = await bidRepository.FindAsync(b => b.TenderId == id && !b.IsDeleted);

            return await ProtectBidSummariesAsync(MapToDetailDto(tender, lots, items, documents, fees, invitations, clarifications, evaluators, bids));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing tender {TenderId}", id);
            throw;
        }
    }

    public async Task<TenderDto> CloseTenderAsync(Guid id)
    {
        var tender = await _tenderRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Tender with ID {id} not found");

        if (!string.Equals(tender.Status, "Published", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Only a published tender can be closed (current status: '{tender.Status}').");
        if (await _tenderControlService.IsControlledTenderMethodAsync(tender.Id))
            throw new ProcurementTenderControlConflictException(
                "TENDER_STATUTORY_OPENING_REQUIRED",
                "NCT, ICT, QBS, and QCBS tenders must close and open through the signed public-opening control.");
        if (!tender.SubmissionDeadline.HasValue)
            throw new InvalidOperationException(
                "A published tender must retain its submission deadline before it can be closed.");
        if (DateTime.UtcNow < tender.SubmissionDeadline.Value)
            throw new InvalidOperationException(
                "The tender cannot be closed before its submission deadline.");

        tender.Status = "Closed";
        tender.UpdatedAt = DateTime.UtcNow;
        await _tenderRepository.UpdateAsync(tender);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Closed tender {TenderId}", id);
        return MapToDto(tender);
    }

    public async Task DeleteTenderAsync(Guid id)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Tender with ID {id} not found");

            if (tender.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft tenders can be deleted");
            }

            await _tenderRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted tender {TenderId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender {TenderId}", id);
            throw;
        }
    }

    // Tender Items
    public async Task<TenderItemDto> AddTenderItemAsync(Guid tenderId, CreateTenderItemDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);

            var item = new TenderItem
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                LotId = dto.LotId,
                LineNumber = dto.LineNumber,
                ItemCode = dto.ItemCode,
                Description = dto.Description,
                Quantity = dto.Quantity,
                UnitOfMeasure = dto.UnitOfMeasure,
                Specifications = dto.Specifications,
                RequiredDeliveryDate = dto.RequiredDeliveryDate,
                DeliveryLocation = dto.DeliveryLocation,
                CreatedAt = DateTime.UtcNow
            };

            await _itemRepository.CreateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Added item {ItemId} to tender {TenderId}", item.Id, tenderId);

            return MapItemToDto(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderItemDto> UpdateTenderItemAsync(Guid itemId, CreateTenderItemDto dto)
    {
        try
        {
            var item = await _itemRepository.GetByIdAsync(itemId)
                ?? throw new InvalidOperationException($"Item with ID {itemId} not found");
            var tender = await _tenderRepository.GetByIdAsync(item.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {item.TenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);

            item.LineNumber = dto.LineNumber;
            item.ItemCode = dto.ItemCode;
            item.Description = dto.Description;
            item.Quantity = dto.Quantity;
            item.UnitOfMeasure = dto.UnitOfMeasure;
            item.Specifications = dto.Specifications;
            item.RequiredDeliveryDate = dto.RequiredDeliveryDate;
            item.DeliveryLocation = dto.DeliveryLocation;
            item.UpdatedAt = DateTime.UtcNow;

            await _itemRepository.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated tender item {ItemId}", itemId);

            return MapItemToDto(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender item {ItemId}", itemId);
            throw;
        }
    }

    public async Task DeleteTenderItemAsync(Guid itemId)
    {
        try
        {
            var item = await _itemRepository.GetByIdAsync(itemId)
                ?? throw new InvalidOperationException($"Item with ID {itemId} not found");
            var tender = await _tenderRepository.GetByIdAsync(item.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {item.TenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);
            await _itemRepository.DeleteAsync(itemId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted tender item {ItemId}", itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender item {ItemId}", itemId);
            throw;
        }
    }

    // Tender Documents
    public async Task<TenderDocumentDto> UploadTenderDocumentAsync(Guid tenderId, UploadTenderDocumentDto dto,
        string logicalFileReference, string? fileType, long? fileSize, Guid fileUploadRecordId,
        Guid centralDocumentRecordId, Guid centralDocumentVersionId)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);

            var document = new TenderDocument
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                DocumentName = dto.DocumentName,
                DocumentType = dto.DocumentType,
                FilePath = logicalFileReference,
                FileType = fileType,
                FileSize = fileSize,
                IsPublic = dto.IsPublic,
                UploadedDate = DateTime.UtcNow,
                UploadedById = _currentUserProvider.UserId,
                FileUploadRecordId = fileUploadRecordId,
                CentralDocumentRecordId = centralDocumentRecordId,
                CentralDocumentVersionId = centralDocumentVersionId,
                CreatedAt = DateTime.UtcNow
            };

            await _documentRepository.CreateAsync(document);

            // If this is an acceptance declaration, update the tender entity
            if (dto.DocumentType == "AcceptanceDeclaration")
            {
                tender.AcceptanceDeclarationDocumentPath = logicalFileReference;
                tender.AcceptanceDeclarationDocumentName = dto.DocumentName;
                tender.UpdatedAt = DateTime.UtcNow;
                await _tenderRepository.UpdateAsync(tender);
                _logger.LogInformation("Updated tender {TenderId} with acceptance declaration document", tenderId);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Uploaded document {DocumentId} to tender {TenderId}", document.Id, tenderId);

            return MapDocumentToDto(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document to tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task DeleteTenderDocumentAsync(Guid documentId)
    {
        try
        {
            var document = await _documentRepository.GetByIdAsync(documentId)
                ?? throw new InvalidOperationException($"Document with ID {documentId} not found");
            var tender = await _tenderRepository.GetByIdAsync(document.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {document.TenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);
            await _documentRepository.DeleteAsync(documentId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted tender document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderDocumentDto>> GetTenderDocumentsAsync(Guid tenderId, bool includeInternal = false)
    {
        try
        {
            var documents = await _documentRepository.GetByTenderIdAsync(tenderId);

            if (!includeInternal)
            {
                documents = documents.Where(d => d.IsPublic).ToList();
            }

            return documents.Select(MapDocumentToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents for tender {TenderId}", tenderId);
            throw;
        }
    }

    // Tender Invitations
    public async Task InviteTenderersAsync(Guid tenderId, InviteTenderersDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");
            if (await _exceptionalSourcingControlService.IsExceptionalAsync(tenderId))
                throw new ProcurementExceptionalSourcingConflictException(
                    "EXCEPTIONAL_INVITATION_CONTROL_REQUIRED",
                    "Controlled noncompetitive suppliers are invited only through the dedicated approved sourcing lifecycle.");

            foreach (var businessPartnerId in dto.BusinessPartnerIds)
            {
                var invitation = new TenderInvitation
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUserProvider.TenantId,
                    TenderId = tenderId,
                    BusinessPartnerId = businessPartnerId,
                    InvitedDate = DateTime.UtcNow,
                    InvitedById = _currentUserProvider.UserId,
                    Status = "Invited",
                    CreatedAt = DateTime.UtcNow
                };

                await _invitationRepository.CreateAsync(invitation);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Invited {Count} business partners to tender {TenderId}", dto.BusinessPartnerIds.Count, tenderId);

            // Send notifications
            if (dto.SendNotifications)
            {
                await _notificationService.SendTenderPublishedNotificationAsync(tenderId, dto.BusinessPartnerIds, dto.ExternalRecipientEmails);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting tenderers to tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderInvitationDto>> GetTenderInvitationsAsync(Guid tenderId)
    {
        try
        {
            var invitations = await _invitationRepository.GetByTenderIdAsync(tenderId);
            return invitations.Select(i => new TenderInvitationDto
            {
                Id = i.Id,
                TenderId = i.TenderId,
                BusinessPartnerId = i.BusinessPartnerId,
                BusinessPartnerName = string.Empty, // Would need to fetch from BusinessPartner entity
                InvitedDate = i.InvitedDate,
                InvitedByName = string.Empty, // Would need to fetch from User entity
                Status = i.Status,
                ViewedDate = i.ViewedDate,
                ResponseDate = i.ResponseDate,
                DeclineReason = i.DeclineReason
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invitations for tender {TenderId}", tenderId);
            throw;
        }
    }

    // Tender Fees
    public async Task<TenderFeeDto> AddTenderFeeAsync(Guid tenderId, CreateTenderFeeDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);
            await ValidateTenderFeePostingAccountsAsync(dto);

            var fee = new TenderFee
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                FeeType = dto.FeeType,
                Amount = dto.Amount,
                Currency = dto.Currency,
                PaymentMethod = dto.PaymentMethod,
                ReceivingAccountId = dto.ReceivingAccountId,
                RevenueAccountId = dto.RevenueAccountId,
                IsMandatory = dto.IsMandatory,
                DueDate = dto.DueDate,
                Description = dto.Description,
                BankAccountDetails = dto.BankAccountDetails,
                CreatedAt = DateTime.UtcNow
            };

            await _feeRepository.CreateAsync(fee);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Added fee {FeeId} to tender {TenderId}", fee.Id, tenderId);

            return MapFeeToDto(fee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding fee to tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderFeeDto> UpdateTenderFeeAsync(Guid feeId, CreateTenderFeeDto dto)
    {
        try
        {
            var fee = await _feeRepository.GetByIdAsync(feeId)
                ?? throw new InvalidOperationException($"Fee with ID {feeId} not found");
            var tender = await _tenderRepository.GetByIdAsync(fee.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {fee.TenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);
            await ValidateTenderFeePostingAccountsAsync(dto);

            fee.FeeType = dto.FeeType;
            fee.Amount = dto.Amount;
            fee.Currency = dto.Currency;
            fee.PaymentMethod = dto.PaymentMethod;
            fee.ReceivingAccountId = dto.ReceivingAccountId;
            fee.RevenueAccountId = dto.RevenueAccountId;
            fee.IsMandatory = dto.IsMandatory;
            fee.DueDate = dto.DueDate;
            fee.Description = dto.Description;
            fee.BankAccountDetails = dto.BankAccountDetails;
            fee.UpdatedAt = DateTime.UtcNow;

            await _feeRepository.UpdateAsync(fee);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated tender fee {FeeId}", feeId);

            return MapFeeToDto(fee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender fee {FeeId}", feeId);
            throw;
        }
    }

    public async Task DeleteTenderFeeAsync(Guid feeId)
    {
        try
        {
            var fee = await _feeRepository.GetByIdAsync(feeId)
                ?? throw new InvalidOperationException($"Fee with ID {feeId} not found");
            var tender = await _tenderRepository.GetByIdAsync(fee.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {fee.TenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);
            await _feeRepository.DeleteAsync(feeId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted tender fee {FeeId}", feeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender fee {FeeId}", feeId);
            throw;
        }
    }

    // Tender Evaluators
    public async Task AssignEvaluatorsAsync(Guid tenderId, AssignEvaluatorsDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");
            await EnsureStandaloneEvaluatorAssignmentMutableAsync(tender);

            if (dto.Evaluators.Count == 0)
                throw new InvalidOperationException("Select at least one authorised tender evaluator.");

            var selectedUserIds = dto.Evaluators.Select(item => item.UserId).ToList();
            if (selectedUserIds.Any(userId => userId == Guid.Empty) ||
                selectedUserIds.Distinct().Count() != selectedUserIds.Count)
                throw new InvalidOperationException("Each evaluator must be a different valid user.");

            var eligibleCandidates = (await GetEligibleEvaluatorCandidatesAsync())
                .ToDictionary(item => item.UserId);
            var ineligibleUserIds = selectedUserIds
                .Where(userId => !eligibleCandidates.ContainsKey(userId))
                .ToList();
            if (ineligibleUserIds.Count > 0)
            {
                throw new InvalidOperationException(
                    "Only active users in the current tenant whose Security role grants " +
                    $"'{TenderEvaluatePermission}' can be assigned as tender evaluators.");
            }

            var evaluatorIds = new List<Guid>();

            foreach (var evaluatorDto in dto.Evaluators)
            {
                var evaluator = new TenderEvaluator
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUserProvider.TenantId,
                    TenderId = tenderId,
                    UserId = evaluatorDto.UserId,
                    Role = evaluatorDto.Role,
                    WeightagePercentage = evaluatorDto.WeightagePercentage,
                    AssignedDate = DateTime.UtcNow,
                    AssignedById = _currentUserProvider.UserId,
                    Status = "Assigned",
                    CreatedAt = DateTime.UtcNow
                };

                await _evaluatorRepository.CreateAsync(evaluator);
                evaluatorIds.Add(evaluatorDto.UserId);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Assigned {Count} evaluators to tender {TenderId}", dto.Evaluators.Count, tenderId);

            // Send notifications
            await _notificationService.SendEvaluationAssignedNotificationAsync(tenderId, evaluatorIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning evaluators to tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderEvaluatorCandidateDto>> GetEvaluatorCandidatesAsync(Guid tenderId)
    {
        var tender = await _tenderRepository.GetByIdAsync(tenderId)
            ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");
        if (tender.TenantId != _currentUserProvider.TenantId)
            throw new InvalidOperationException("The tender is not available in the current tenant.");

        var assignedUserIds = (await _evaluatorRepository.GetByTenderIdAsync(tenderId))
            .Select(item => item.UserId)
            .ToHashSet();

        return (await GetEligibleEvaluatorCandidatesAsync())
            .Where(item => !assignedUserIds.Contains(item.UserId))
            .OrderBy(item => item.FullName)
            .ThenBy(item => item.UserName)
            .ToList();
    }

    private async Task<List<TenderEvaluatorCandidateDto>> GetEligibleEvaluatorCandidatesAsync()
    {
        var permission = await _unitOfWork.Repository<Permission>().FirstOrDefaultAsync(
            item => !item.IsDeleted && item.Name == TenderEvaluatePermission,
            item => item.RolePermissions);
        if (permission is null)
            return new List<TenderEvaluatorCandidateDto>();

        var candidates = new Dictionary<Guid, TenderEvaluatorCandidateDto>();
        foreach (var roleId in permission.RolePermissions.Select(item => item.RoleId).Distinct())
        {
            var role = await _roleManager.FindByIdAsync(roleId.ToString());
            if (role?.Name is not { Length: > 0 } roleName)
                continue;

            var users = await _userManager.GetUsersInRoleAsync(roleName);
            foreach (var user in users.Where(item =>
                         item.IsActive && item.TenantId == _currentUserProvider.TenantId))
            {
                if (!candidates.TryGetValue(user.Id, out var candidate))
                {
                    candidate = new TenderEvaluatorCandidateDto
                    {
                        UserId = user.Id,
                        UserName = user.UserName ?? string.Empty,
                        FullName = user.FullName,
                        Email = user.Email ?? string.Empty
                    };
                    candidates.Add(user.Id, candidate);
                }

                if (!candidate.RoleNames.Contains(roleName, StringComparer.OrdinalIgnoreCase))
                    candidate.RoleNames.Add(roleName);
            }
        }

        foreach (var candidate in candidates.Values)
            candidate.RoleNames.Sort(StringComparer.OrdinalIgnoreCase);

        return candidates.Values.ToList();
    }

    public async Task<IEnumerable<TenderEvaluatorDto>> GetTenderEvaluatorsAsync(Guid tenderId)
    {
        try
        {
            var evaluators = await _evaluatorRepository.GetByTenderIdAsync(tenderId);
            var result = new List<TenderEvaluatorDto>();
            var evaluationRepository = _unitOfWork.Repository<TenderEvaluation>();

            foreach (var evaluator in evaluators)
            {
                var user = await _userManager.FindByIdAsync(evaluator.UserId.ToString());
                var assignedByUser = evaluator.AssignedById.HasValue ? await _userManager.FindByIdAsync(evaluator.AssignedById.Value.ToString()) : null;
                var evaluationCount = await evaluationRepository.FindAsync(e => e.TenderEvaluatorId == evaluator.Id && !e.IsDeleted);

                result.Add(new TenderEvaluatorDto
                {
                    Id = evaluator.Id,
                    TenderId = evaluator.TenderId,
                    UserId = evaluator.UserId,
                    UserName = user?.FullName ?? string.Empty,
                    Role = evaluator.Role,
                    AssignedDate = evaluator.AssignedDate,
                    AssignedByName = assignedByUser?.FullName ?? string.Empty,
                    Status = evaluator.Status,
                    AcceptedDate = evaluator.AcceptedDate,
                    CompletedDate = evaluator.CompletedDate,
                    WeightagePercentage = evaluator.WeightagePercentage,
                    EvaluationCount = evaluationCount.Count()
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluators for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task RemoveEvaluatorAsync(Guid evaluatorId)
    {
        try
        {
            var evaluator = await _evaluatorRepository.GetByIdAsync(evaluatorId)
                ?? throw new InvalidOperationException($"Evaluator with ID {evaluatorId} not found");
            await EnsureStandaloneEvaluatorAssignmentMutableAsync(evaluator.TenderId);
            await _evaluatorRepository.DeleteAsync(evaluatorId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Removed evaluator {EvaluatorId}", evaluatorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing evaluator {EvaluatorId}", evaluatorId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderDto>> GetMyAssignedTendersAsync()
    {
        try
        {
            // Get all TenderEvaluator records for the current user
            var evaluators = await _evaluatorRepository.GetByUserIdAsync(_currentUserProvider.UserId);

            if (!evaluators.Any())
            {
                return new List<TenderDto>();
            }

            // Get unique tenders from evaluator assignments
            var tenderIds = evaluators.Select(e => e.TenderId).Distinct().ToList();

            // Fetch tender details for each tender
            var tenders = new List<Tender>();
            foreach (var tenderId in tenderIds)
            {
                var tender = await _tenderRepository.GetByIdAsync(tenderId);
                if (tender != null)
                {
                    tenders.Add(tender);
                }
            }

            // Map to DTOs
            return tenders.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assigned tenders for user {UserId}", _currentUserProvider.UserId);
            throw;
        }
    }

    // Tender Clarifications
    public async Task<TenderClarificationDto> CreateClarificationAsync(Guid tenderId, CreateClarificationDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            // Get the business partner for the current user
            var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId)
                ?? throw new InvalidOperationException("No business partner found for the current user. Please complete your business partner registration first.");

            var clarification = new TenderClarification
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                BusinessPartnerId = businessPartner.Id,
                Question = dto.Question,
                QuestionDate = DateTime.UtcNow,
                QuestionById = _currentUserProvider.UserId,
                Status = "Pending",
                IsPublic = dto.IsPublic,
                Category = dto.Category,
                CreatedAt = DateTime.UtcNow
            };

            await _clarificationRepository.CreateAsync(clarification);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created clarification {ClarificationId} for tender {TenderId}", clarification.Id, tenderId);

            // Send notification
            await _notificationService.SendClarificationNotificationAsync(clarification.Id, false);

            return MapClarificationToDto(clarification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating clarification for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderClarificationDto> AnswerClarificationAsync(Guid clarificationId, AnswerClarificationDto dto)
    {
        try
        {
            var clarification = await _clarificationRepository.GetByIdAsync(clarificationId)
                ?? throw new InvalidOperationException($"Clarification with ID {clarificationId} not found");

            clarification.Answer = dto.Answer;
            clarification.AnswerDate = DateTime.UtcNow;
            clarification.AnsweredById = _currentUserProvider.UserId;
            clarification.Status = "Answered";
            clarification.IsPublic = dto.IsPublic;
            clarification.UpdatedAt = DateTime.UtcNow;

            await _clarificationRepository.UpdateAsync(clarification);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Answered clarification {ClarificationId}", clarificationId);

            // Send notification
            await _notificationService.SendClarificationNotificationAsync(clarificationId, true);

            return MapClarificationToDto(clarification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering clarification {ClarificationId}", clarificationId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderClarificationDto>> GetTenderClarificationsAsync(Guid tenderId, bool publicOnly = false)
    {
        try
        {
            var clarifications = await _clarificationRepository.GetByTenderIdAsync(tenderId);

            if (publicOnly)
            {
                clarifications = clarifications.Where(c => c.IsPublic).ToList();
            }

            return clarifications.Select(MapClarificationToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clarifications for tender {TenderId}", tenderId);
            throw;
        }
    }

    // Tender Revisions
    public async Task<TenderRevisionDto> CreateRevisionAsync(Guid tenderId, CreateRevisionDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");
            if (tender.TenantId != _currentUserProvider.TenantId)
                throw new UnauthorizedAccessException("The tender does not belong to the current tenant.");
            if (!string.Equals(tender.Status, "Published", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Tender revisions can only be issued while the tender is published (current status: '{tender.Status}').");
            var revisionType = dto.RevisionType?.Trim();
            if (revisionType is not ("Amendment" or "Addendum" or "Corrigendum" or "DeadlineExtension"))
                throw new InvalidOperationException(
                    "Revision type must be Amendment, Addendum, Corrigendum, or DeadlineExtension.");
            if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Trim().Length < 10)
                throw new InvalidOperationException(
                    "A tender revision requires a clear description of at least 10 characters.");
            if (revisionType == "DeadlineExtension" && !dto.NewSubmissionDeadline.HasValue)
                throw new InvalidOperationException(
                    "A deadline-extension revision requires a new submission deadline.");
            if (dto.NewSubmissionDeadline.HasValue &&
                dto.NewSubmissionDeadline.Value <= DateTime.UtcNow)
                throw new InvalidOperationException(
                    "A revised submission deadline must be in the future.");
            if (dto.NewSubmissionDeadline.HasValue && tender.SubmissionDeadline.HasValue &&
                dto.NewSubmissionDeadline.Value <= tender.SubmissionDeadline.Value)
                throw new InvalidOperationException(
                    "A revised submission deadline must extend the current deadline.");
            ValidateTenderSchedule(
                dto.NewSubmissionDeadline,
                tender.OpeningDate,
                "TENDER_REVISION_OPENING_BEFORE_DEADLINE",
                "The revised submission deadline cannot be later than the scheduled tender opening. Choose a deadline at or before the opening schedule.");

            // Get current revision number
            var revisions = await _revisionRepository.GetByTenderIdAsync(tenderId);
            var revisionCount = revisions.Count() + 1;
            var revisionNumber = $"REV-{revisionCount:D3}";

            var revision = new TenderRevision
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                RevisionNumber = revisionNumber,
                RevisionDate = DateTime.UtcNow,
                RevisedById = _currentUserProvider.UserId,
                RevisionType = revisionType,
                Description = dto.Description.Trim(),
                Changes = string.IsNullOrWhiteSpace(dto.Changes) ? null : dto.Changes.Trim(),
                NewSubmissionDeadline = dto.NewSubmissionDeadline,
                RequiresRebid = dto.RequiresRebid,
                NotificationSent = false,
                CreatedAt = DateTime.UtcNow
            };

            await _revisionRepository.CreateAsync(revision);

            // Update tender if submission deadline changed
            if (dto.NewSubmissionDeadline.HasValue)
            {
                tender.SubmissionDeadline = dto.NewSubmissionDeadline.Value;
                await _tenderRepository.UpdateAsync(tender);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created revision {RevisionId} for tender {TenderId}", revision.Id, tenderId);

            // Send notifications
            if (dto.SendNotifications)
            {
                try
                {
                    await _notificationService.SendRevisionNotificationAsync(revision.Id);
                    revision.NotificationSent = true;
                    revision.NotificationSentDate = DateTime.UtcNow;
                    await _revisionRepository.UpdateAsync(revision);
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (Exception notificationError)
                {
                    _logger.LogWarning(notificationError,
                        "Revision {RevisionId} was saved but its notification could not be sent",
                        revision.Id);
                }
            }

            return MapRevisionToDto(revision);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating revision for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderRevisionDto>> GetTenderRevisionsAsync(Guid tenderId)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");
            if (tender.TenantId != _currentUserProvider.TenantId)
                throw new UnauthorizedAccessException("The tender does not belong to the current tenant.");
            var revisions = await _revisionRepository.GetByTenderIdAsync(tenderId);
            return revisions.Select(MapRevisionToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving revisions for tender {TenderId}", tenderId);
            throw;
        }
    }

    // Tender Statistics
    public async Task<int> GetTotalViewsAsync(Guid tenderId)
    {
        try
        {
            var viewLogs = await _viewLogRepository.GetByTenderIdAsync(tenderId);
            return viewLogs.Count(v => v.ActionType == "View");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving total views for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<int> GetTotalDownloadsAsync(Guid tenderId)
    {
        try
        {
            var viewLogs = await _viewLogRepository.GetByTenderIdAsync(tenderId);
            return viewLogs.Count(v => v.ActionType == "Download");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving total downloads for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task LogTenderViewAsync(Guid tenderId, Guid? businessPartnerId, string actionType)
    {
        try
        {
            var viewLog = new TenderViewLog
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                BusinessPartnerId = businessPartnerId,
                UserId = _currentUserProvider.UserId,
                ActionType = actionType,
                ActionDate = DateTime.UtcNow,
                IpAddress = string.Empty, // Would need to get from HTTP context
                CreatedAt = DateTime.UtcNow
            };

            await _viewLogRepository.CreateAsync(viewLog);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Logged {ActionType} for tender {TenderId}", actionType, tenderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging view for tender {TenderId}", tenderId);
            throw;
        }
    }

    // Mapping methods
    private TenderDto MapToDto(Tender tender)
    {
        return new TenderDto
        {
            ApprovalRequired = tender.ApprovalRequired,
            Id = tender.Id,
            TenderNumber = tender.TenderNumber,
            Title = tender.Title,
            TenderType = tender.TenderType,
            Status = tender.Status,
            PublishDate = tender.PublishDate,
            SubmissionDeadline = tender.SubmissionDeadline,
            EstimatedValue = _currentUserProvider.IsExternalUser ? null : tender.EstimatedValue,
            Currency = tender.Currency,
            SourcePurchaseRequisitionId = tender.SourcePurchaseRequisitionId,
            SourcingReleaseId = tender.SourcingReleaseId,
            SourcingCaseId = tender.SourcingCaseId,
            SourcingMethod = tender.SourcingCase?.SelectedMethod,
            BidCount = tender.Bids?.Count(b => !b.IsDeleted && b.Status != "Draft") ?? 0,
            InvitationCount = tender.Invitations?.Count(i => !i.IsDeleted) ?? 0,
            CreatedAt = tender.CreatedAt,
            CreatedByName = tender.CreatedBy != null ? $"{tender.CreatedBy.FirstName} {tender.CreatedBy.LastName}".Trim() : string.Empty
        };
    }

    private TenderDetailDto MapToDetailDto(Tender tender, IEnumerable<TenderLot> lots, IEnumerable<TenderItem> items, IEnumerable<TenderDocument> documents, IEnumerable<TenderFee> fees, IEnumerable<TenderInvitation> invitations, IEnumerable<TenderClarification> clarifications, IEnumerable<TenderEvaluatorDto> evaluators, IEnumerable<TenderBid> bids)
    {
        return new TenderDetailDto
        {
            ApprovalRequired = tender.ApprovalRequired,
            Id = tender.Id,
            TenderNumber = tender.TenderNumber,
            Title = tender.Title,
            Description = tender.Description,
            TenderType = tender.TenderType,
            Status = tender.Status,
            PublishDate = tender.PublishDate,
            SubmissionDeadline = tender.SubmissionDeadline,
            OpeningDate = tender.OpeningDate,
            AwardDate = tender.AwardDate,
            EstimatedValue = _currentUserProvider.IsExternalUser ? null : tender.EstimatedValue,
            Currency = tender.Currency,
            SourcePurchaseRequisitionId = tender.SourcePurchaseRequisitionId,
            SourcingReleaseId = tender.SourcingReleaseId,
            SourcingCaseId = tender.SourcingCaseId,
            MinimumPerformanceRating = tender.MinimumPerformanceRating,
            RequiresPrequalification = tender.RequiresPrequalification,
            AllowPartialBids = tender.AllowPartialBids,
            PriceWeightage = tender.PriceWeightage,
            QualityWeightage = tender.QualityWeightage,
            DeliveryWeightage = tender.DeliveryWeightage,
            ExperienceWeightage = tender.ExperienceWeightage,
            EvaluationCriteriaJson = tender.EvaluationCriteriaJson,
            // QCBS Configuration
            UseQCBSEvaluation = tender.UseQCBSEvaluation,
            MinimumTechnicalScore = tender.MinimumTechnicalScore,
            TechnicalWeight = tender.TechnicalWeight,
            FinancialWeight = tender.FinancialWeight,
            TermsAndConditions = tender.TermsAndConditions,
            BidValidityPeriodDays = tender.BidValidityPeriodDays,
            RequiredDocuments = tender.RequiredDocuments,
            RequiresAcceptanceDeclaration = tender.RequiresAcceptanceDeclaration,
            AcceptanceDeclarationDocumentPath = tender.AcceptanceDeclarationDocumentPath,
            AcceptanceDeclarationDocumentName = tender.AcceptanceDeclarationDocumentName,
            EvaluationTemplateId = tender.EvaluationTemplateId,
            EvaluationTemplateName = tender.EvaluationTemplate?.TemplateName,
            Notes = tender.Notes,
            CreatedAt = tender.CreatedAt,
            Lots = lots.Select(MapToLotDto).ToList(),
            Items = items.Select(MapItemToDto).ToList(),
            Documents = documents.Select(MapDocumentToDto).ToList(),
            Fees = fees.Select(MapFeeToDto).ToList(),
            Invitations = invitations.Select(MapInvitationToDto).ToList(),
            Clarifications = clarifications.Select(MapClarificationToDto).ToList(),
            Evaluators = evaluators.ToList(),
            Bids = bids.Select(b => MapBidToSummaryDto(b, tender)).ToList(),
            BidCount = bids.Count(b => !string.Equals(b.Status, "Draft", StringComparison.OrdinalIgnoreCase) &&
                                       !string.Equals(b.Status, "Withdrawn", StringComparison.OrdinalIgnoreCase))
        };
    }

    private async Task<ProcurementMethodType?> GetSourcingMethodAsync(Tender tender)
    {
        if (!tender.SourcingCaseId.HasValue) return null;
        return await _unitOfWork.Repository<ProcurementSourcingCase>()
            .GetQueryable(item => item.Id == tender.SourcingCaseId.Value &&
                                  item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted)
            .Select(item => (ProcurementMethodType?)item.SelectedMethod)
            .SingleOrDefaultAsync();
    }

    private static TenderItemDto MapItemToDto(TenderItem item)
    {
        return new TenderItemDto
        {
            Id = item.Id,
            TenderId = item.TenderId,
            LineNumber = item.LineNumber,
            ItemCode = item.ItemCode,
            Description = item.Description,
            Quantity = item.Quantity,
            UnitOfMeasure = item.UnitOfMeasure,
            Specifications = item.Specifications,
            RequiredDeliveryDate = item.RequiredDeliveryDate,
            DeliveryLocation = item.DeliveryLocation
        };
    }

    private async Task EnsureStandaloneEvaluatorAssignmentMutableAsync(Guid tenderId)
    {
        var tender = await _tenderRepository.GetByIdAsync(tenderId)
            ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");
        await EnsureStandaloneEvaluatorAssignmentMutableAsync(tender);
    }

    private async Task EnsureStandaloneEvaluatorAssignmentMutableAsync(Tender tender)
    {
        if (!tender.SourcePurchaseRequisitionId.HasValue)
            throw new ProcurementRequisitionSourcingValidationException(
                "TENDER_SOURCE_REQUISITION_REQUIRED",
                "The tender must retain its approved purchase-requisition source before evaluators can be assigned.");

        var requestForQuotation = IsRequestForQuotation(tender.TenderType);
        var gate = await _sourcingCaseService.EnforceSourceEntryAsync(
            tender.SourcePurchaseRequisitionId.Value,
            requestForQuotation ? ProcurementMethodType.RequestForQuotation : null,
            requestForQuotation ? "RequestForQuotation" : "Tender",
            tender.TenderNumber,
            Guid.NewGuid().ToString("N"));
        EnsureSourceLineage(tender.SourcingReleaseId, tender.SourcingCaseId, gate);

        var lineageChanged = tender.SourcingReleaseId != gate.SourcingReleaseId ||
                             tender.SourcingCaseId != gate.SourcingCaseId;
        tender.SourcingReleaseId = gate.SourcingReleaseId;
        tender.SourcingCaseId = gate.SourcingCaseId;
        if (lineageChanged)
        {
            tender.UpdatedAt = DateTime.UtcNow;
            await _tenderRepository.UpdateAsync(tender);
            await _unitOfWork.SaveChangesAsync();
        }

        // A current release is sufficient for the simplified approved-PR route. The
        // source-specific committee is mandatory only when the tenant has explicitly
        // created the advanced immutable sourcing case that owns that committee.
        if (!UsesAdvancedSourcingControls(gate))
            return;

        var readiness = await _evaluationCommittee.GetReadinessAsync(
            ProcurementEvaluationSourceType.Tender,
            tender.Id,
            CancellationToken.None);
        if (readiness.HasControl)
        {
            throw new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_COMMITTEE_MEMBERSHIP_REQUIRED",
                "This source is bound to the controlled evaluation committee. Manage evaluator membership through that exact committee; standalone evaluator assignments cannot grant or remove scoring access.");
        }
    }

    private static bool IsRequestForQuotation(string? tenderType) =>
        string.Equals(tenderType?.Trim(), "RFQ", StringComparison.OrdinalIgnoreCase);

    internal static bool UsesAdvancedSourcingControls(ProcurementSourcingCaseEntryGateDto gate) =>
        gate.SourcingCaseId.HasValue && gate.SourcingCaseId.Value != Guid.Empty;

    internal static bool RequiresControlledPublication(ProcurementSourcingCaseEntryGateDto gate) =>
        UsesAdvancedSourcingControls(gate) &&
        ProcurementTenderRouting.RequiresControlledLifecycle(gate.SelectedMethod, gate.HasAdvancedAuthorityRoute);

    internal static void ValidateReleaseOnlyPublication(
        Tender tender,
        PublishTenderDto request,
        DateTime nowUtc)
    {
        if (!tender.SourcePurchaseRequisitionId.HasValue || !tender.SourcingReleaseId.HasValue)
            throw new ProcurementRequisitionSourcingValidationException(
                "TENDER_SOURCE_LINEAGE_REQUIRED",
                "The tender must retain its approved requisition and immutable sourcing-release lineage before publication.");
        if (request.SubmissionDeadline <= nowUtc)
            throw new ProcurementRequisitionSourcingValidationException(
                "TENDER_DEADLINE_PASSED",
                "The tender submission deadline must be in the future when it is published.");
        ValidateTenderSchedule(request.SubmissionDeadline, request.OpeningDate);
    }

    private static int? ValidateBidValidityPeriod(int? days)
    {
        if (days.HasValue && (days.Value <= 0 || days.Value > (DateTime.MaxValue - DateTime.UtcNow).TotalDays))
            throw new InvalidOperationException("Enter a positive bid-validity period in calendar days from the tender terms.");
        return days;
    }

    internal static void ValidateTenderSchedule(
        DateTime? submissionDeadline,
        DateTime? openingDate,
        string errorCode = "TENDER_OPENING_BEFORE_DEADLINE",
        string errorMessage = "The scheduled tender opening cannot be before the submission deadline.")
    {
        if (submissionDeadline.HasValue && openingDate.HasValue && openingDate.Value < submissionDeadline.Value)
            throw new ProcurementRequisitionSourcingValidationException(errorCode, errorMessage);
    }

    private static void EnsureSourceLineage(Guid? releaseId, Guid? caseId, ProcurementSourcingCaseEntryGateDto gate)
    {
        if (releaseId.HasValue && releaseId.Value != gate.SourcingReleaseId)
            throw new ProcurementRequisitionSourcingValidationException("TENDER_RELEASE_LINEAGE_MISMATCH", "Tender sourcing-release lineage cannot be replaced.");
        if (caseId.HasValue && caseId.Value != gate.SourcingCaseId)
            throw new ProcurementRequisitionSourcingValidationException("TENDER_CASE_LINEAGE_MISMATCH", "Tender sourcing-case lineage cannot be replaced.");
    }

    private static TenderDocumentDto MapDocumentToDto(TenderDocument document)
    {
        return new TenderDocumentDto
        {
            Id = document.Id,
            TenderId = document.TenderId,
            DocumentName = document.DocumentName,
            DocumentType = document.DocumentType,
            FilePath = document.FilePath,
            FileType = document.FileType,
            FileSize = document.FileSize,
            IsPublic = document.IsPublic,
            UploadedDate = document.UploadedDate,
            UploadedByName = string.Empty, // Would need to fetch from User entity
            FileUploadRecordId = document.FileUploadRecordId,
            CentralDocumentRecordId = document.CentralDocumentRecordId,
            CentralDocumentVersionId = document.CentralDocumentVersionId
        };
    }

    private static TenderFeeDto MapFeeToDto(TenderFee fee)
    {
        return new TenderFeeDto
        {
            Id = fee.Id,
            TenderId = fee.TenderId,
            FeeType = fee.FeeType,
            Amount = fee.Amount,
            Currency = fee.Currency,
            PaymentMethod = fee.PaymentMethod,
            ReceivingAccountId = fee.ReceivingAccountId,
            RevenueAccountId = fee.RevenueAccountId,
            IsMandatory = fee.IsMandatory,
            DueDate = fee.DueDate,
            Description = fee.Description,
            BankAccountDetails = fee.BankAccountDetails,
            PaymentCount = 0 // Would need to count from Payments collection
        };
    }

    private static TenderInvitationDto MapInvitationToDto(TenderInvitation invitation)
    {
        return new TenderInvitationDto
        {
            Id = invitation.Id,
            TenderId = invitation.TenderId,
            BusinessPartnerId = invitation.BusinessPartnerId,
            BusinessPartnerName = invitation.BusinessPartner?.PartnerName ?? string.Empty,
            InvitedDate = invitation.InvitedDate,
            InvitedByName = string.Empty, // Would need to fetch from User entity
            Status = invitation.Status,
            ViewedDate = invitation.ViewedDate,
            ResponseDate = invitation.ResponseDate,
            DeclineReason = invitation.DeclineReason
        };
    }

    private static TenderClarificationDto MapClarificationToDto(TenderClarification clarification)
    {
        return new TenderClarificationDto
        {
            Id = clarification.Id,
            TenderId = clarification.TenderId,
            BusinessPartnerId = clarification.BusinessPartnerId,
            BusinessPartnerName = clarification.BusinessPartner?.PartnerName ?? string.Empty,
            Question = clarification.Question,
            QuestionDate = clarification.QuestionDate,
            QuestionByName = clarification.QuestionBy != null
                ? $"{clarification.QuestionBy.FirstName} {clarification.QuestionBy.LastName}".Trim()
                : string.Empty,
            Answer = clarification.Answer,
            AnswerDate = clarification.AnswerDate,
            AnsweredByName = clarification.AnsweredBy != null
                ? $"{clarification.AnsweredBy.FirstName} {clarification.AnsweredBy.LastName}".Trim()
                : string.Empty,
            Status = clarification.Status,
            IsPublic = clarification.IsPublic,
            Category = clarification.Category
        };
    }

    private static TenderRevisionDto MapRevisionToDto(TenderRevision revision)
    {
        return new TenderRevisionDto
        {
            Id = revision.Id,
            TenderId = revision.TenderId,
            RevisionNumber = revision.RevisionNumber,
            RevisionDate = revision.RevisionDate,
            RevisedByName = string.Empty, // Would need to fetch from User entity
            RevisionType = revision.RevisionType,
            Description = revision.Description,
            Changes = revision.Changes,
            NewSubmissionDeadline = revision.NewSubmissionDeadline,
            RequiresRebid = revision.RequiresRebid,
            NotificationSent = revision.NotificationSent
        };
    }

    internal static TenderBidSummaryDto MapBidToSummaryDto(TenderBid bid, Tender? tender = null)
    {
        var result = new TenderBidSummaryDto
        {
            Id = bid.Id,
            TenderId = bid.TenderId,
            TenderNumber = tender?.TenderNumber ?? string.Empty,
            TenderTitle = tender?.Title ?? string.Empty,
            BusinessPartnerId = bid.BusinessPartnerId,
            BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
            BidNumber = bid.BidNumber,
            SubmittedDate = bid.SubmittedDate,
            Status = bid.Status,
            TotalBidAmount = bid.TotalBidAmount,
            Currency = bid.Currency,
            TotalScore = bid.TotalScore,
            Rank = bid.Rank,
            IsCompliant = bid.IsCompliant,
            HasPaidFees = false, // Payment info not available in this context
            // QCBS Scores
            TechnicalScore = bid.TechnicalScore,
            FinancialScore = bid.FinancialScore,
            CombinedScore = bid.CombinedScore,
            IsQualifiedTechnically = bid.IsQualifiedTechnically,
            DisqualificationReason = bid.DisqualificationReason
        };
        return bid.OpenedDate.HasValue ? result : ProcurementBidDisclosure.Seal(result);
    }

    private async Task<TenderDetailDto> ProtectBidSummariesAsync(TenderDetailDto result)
    {
        foreach (var bid in result.Bids)
        {
            // Tender-wide views are not the supplier's own-bid access path.
            if (bid.IsSealed || !await _tenderControlService.ShouldConcealFinancialProposalAsync(result.Id, bid.Id)) continue;
            bid.IsFinancialProposalSealed = true;
            bid.TotalBidAmount = 0m;
            bid.Currency = null;
            bid.FinancialScore = null;
            bid.CombinedScore = null;
            bid.TotalScore = null;
            bid.Rank = null;
        }
        return result;
    }

    #region Tender LOT Methods

    public async Task<TenderLotDto> AddTenderLotAsync(Guid tenderId, CreateTenderLotDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId);
            if (tender == null)
                throw new InvalidOperationException($"Tender with ID {tenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);

            var lotCode = await _lotRepository.GenerateLotCodeAsync(tenderId);

            var lot = new TenderLot
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                LotNumber = dto.LotNumber,
                LotCode = dto.LotCode ?? lotCode,
                Title = dto.Title,
                Description = dto.Description,
                EstimatedValue = dto.EstimatedValue,
                Currency = dto.Currency ?? tender.Currency,
                RequiredDeliveryDate = dto.RequiredDeliveryDate,
                DeliveryLocation = dto.DeliveryLocation,
                Specifications = dto.Specifications,
                Notes = dto.Notes,
                DisplayOrder = dto.DisplayOrder,
                Status = "Active"
            };

            await _lotRepository.CreateAsync(lot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created tender lot {LotCode} for tender {TenderId}", lot.LotCode, tenderId);

            return MapToLotDto(lot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tender lot for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderLotDto> UpdateTenderLotAsync(Guid lotId, UpdateTenderLotDto dto)
    {
        try
        {
            var lot = await _lotRepository.GetByIdAsync(lotId);
            if (lot == null)
                throw new InvalidOperationException($"Tender lot with ID {lotId} not found");
            var tender = await _tenderRepository.GetByIdAsync(lot.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {lot.TenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);

            lot.LotCode = dto.LotCode ?? lot.LotCode;
            lot.Title = dto.Title ?? lot.Title;
            lot.Description = dto.Description ?? lot.Description;
            lot.EstimatedValue = dto.EstimatedValue ?? lot.EstimatedValue;
            lot.Currency = dto.Currency ?? lot.Currency;
            lot.RequiredDeliveryDate = dto.RequiredDeliveryDate ?? lot.RequiredDeliveryDate;
            lot.DeliveryLocation = dto.DeliveryLocation ?? lot.DeliveryLocation;
            lot.Specifications = dto.Specifications ?? lot.Specifications;
            lot.Notes = dto.Notes ?? lot.Notes;
            lot.DisplayOrder = dto.DisplayOrder;

            await _lotRepository.UpdateAsync(lot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated tender lot {LotId}", lotId);

            return MapToLotDto(lot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender lot {LotId}", lotId);
            throw;
        }
    }

    public async Task DeleteTenderLotAsync(Guid lotId)
    {
        try
        {
            var lot = await _lotRepository.GetByIdWithItemsAsync(lotId);
            if (lot == null)
                throw new InvalidOperationException($"Tender lot with ID {lotId} not found");
            var tender = await _tenderRepository.GetByIdAsync(lot.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {lot.TenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);

            // Remove lot assignment from items
            if (lot.Items != null)
            {
                foreach (var item in lot.Items)
                {
                    item.LotId = null;
                    await _itemRepository.UpdateAsync(item);
                }
            }

            await _lotRepository.DeleteAsync(lotId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted tender lot {LotId}", lotId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender lot {LotId}", lotId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderLotDto>> GetTenderLotsAsync(Guid tenderId)
    {
        try
        {
            var lots = await _lotRepository.GetByTenderIdAsync(tenderId);
            return lots.Select(MapToLotDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tender lots for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderLotDto?> GetTenderLotByIdAsync(Guid lotId)
    {
        try
        {
            var lot = await _lotRepository.GetByIdWithItemsAsync(lotId);
            return lot != null ? MapToLotDto(lot) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tender lot {LotId}", lotId);
            throw;
        }
    }

    public async Task AssignItemToLotAsync(Guid itemId, Guid lotId)
    {
        try
        {
            var item = await _itemRepository.GetByIdAsync(itemId);
            if (item == null)
                throw new InvalidOperationException($"Tender item with ID {itemId} not found");

            var lot = await _lotRepository.GetByIdAsync(lotId);
            if (lot == null)
                throw new InvalidOperationException($"Tender lot with ID {lotId} not found");

            if (item.TenderId != lot.TenderId)
                throw new InvalidOperationException("Item and lot must belong to the same tender");
            var tender = await _tenderRepository.GetByIdAsync(item.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {item.TenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);

            item.LotId = lotId;
            await _itemRepository.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Assigned item {ItemId} to lot {LotId}", itemId, lotId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning item {ItemId} to lot {LotId}", itemId, lotId);
            throw;
        }
    }

    public async Task RemoveItemFromLotAsync(Guid itemId)
    {
        try
        {
            var item = await _itemRepository.GetByIdAsync(itemId);
            if (item == null)
                throw new InvalidOperationException($"Tender item with ID {itemId} not found");
            var tender = await _tenderRepository.GetByIdAsync(item.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {item.TenderId} not found");
            await EnsureStatutoryStructureMutableAsync(tender);

            item.LotId = null;
            await _itemRepository.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Removed item {ItemId} from lot", itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item {ItemId} from lot", itemId);
            throw;
        }
    }

    private async Task EnsureStatutoryStructureMutableAsync(Tender tender)
    {
        if (string.Equals(tender.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            return;
        if (tender.SourcingCaseId.HasValue || await _tenderControlService.IsControlledTenderMethodAsync(tender.Id))
            throw new ProcurementTenderControlConflictException(
                "TENDER_STATUTORY_TERMS_LOCKED",
                "NCT, ICT, QBS, and QCBS tender terms are locked after document approval. Use the controlled tender lifecycle.");
        if (await _exceptionalSourcingControlService.IsExceptionalAsync(tender.Id))
            throw new ProcurementExceptionalSourcingConflictException(
                "EXCEPTIONAL_TENDER_TERMS_LOCKED",
                "Restricted, single-source, and petty-purchase terms are locked after approval. Use the dedicated noncompetitive-sourcing control.");
    }

    private async Task ValidateTenderFeePostingAccountsAsync(CreateTenderFeeDto dto)
    {
        if (!dto.ReceivingAccountId.HasValue || dto.ReceivingAccountId == Guid.Empty)
            throw new InvalidOperationException(
                "Select the receiving GL account for this tender fee.");
        if (!dto.RevenueAccountId.HasValue || dto.RevenueAccountId == Guid.Empty)
            throw new InvalidOperationException(
                "Select the fee revenue GL account for this tender fee.");
        if (dto.ReceivingAccountId == dto.RevenueAccountId)
            throw new InvalidOperationException(
                "The receiving and fee revenue GL accounts must be different.");

        var accountIds = new[]
        {
            dto.ReceivingAccountId.Value,
            dto.RevenueAccountId.Value
        };
        var accounts = await _unitOfWork.Repository<Account>()
            .GetQueryable(account =>
                account.TenantId == _currentUserProvider.TenantId &&
                accountIds.Contains(account.Id) &&
                !account.IsDeleted)
            .AsNoTracking()
            .ToListAsync();
        if (accounts.Count != accountIds.Length)
            throw new InvalidOperationException(
                "One or more selected tender-fee GL accounts do not belong to this tenant.");

        var receiving = accounts.Single(account => account.Id == dto.ReceivingAccountId.Value);
        if (receiving.Status != AccountStatus.Active ||
            receiving.AccountType != AccountType.Asset ||
            !receiving.AllowDirectPosting ||
            receiving.IsControlAccount)
            throw new InvalidOperationException(
                "The receiving GL account must be an active, direct-posting, non-control Asset account.");

        var revenue = accounts.Single(account => account.Id == dto.RevenueAccountId.Value);
        if (revenue.Status != AccountStatus.Active ||
            revenue.AccountType != AccountType.Revenue ||
            !revenue.AllowDirectPosting ||
            revenue.IsControlAccount)
            throw new InvalidOperationException(
                "The fee revenue GL account must be an active, direct-posting, non-control Revenue account.");
    }

    private TenderLotDto MapToLotDto(TenderLot lot)
    {
        return new TenderLotDto
        {
            Id = lot.Id,
            TenderId = lot.TenderId,
            LotNumber = lot.LotNumber,
            LotCode = lot.LotCode,
            Title = lot.Title,
            Description = lot.Description,
            EstimatedValue = _currentUserProvider.IsExternalUser ? null : lot.EstimatedValue,
            Currency = lot.Currency,
            Status = lot.Status,
            RequiredDeliveryDate = lot.RequiredDeliveryDate,
            DeliveryLocation = lot.DeliveryLocation,
            Specifications = lot.Specifications,
            Notes = lot.Notes,
            DisplayOrder = lot.DisplayOrder,
            ItemCount = lot.Items?.Count ?? 0,
            BidCount = lot.BidLots?.Count ?? 0,
            IsAwarded = lot.Awards?.Any() ?? false,
            AwardedToPartnerName = lot.Awards?.FirstOrDefault()?.BusinessPartner?.PartnerName,
            Items = lot.Items?.Select(i => new TenderItemDto
            {
                Id = i.Id,
                TenderId = i.TenderId,
                LotId = i.LotId,
                LotCode = lot.LotCode,
                LotTitle = lot.Title,
                LineNumber = i.LineNumber,
                ItemCode = i.ItemCode,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitOfMeasure = i.UnitOfMeasure,
                Specifications = i.Specifications,
                RequiredDeliveryDate = i.RequiredDeliveryDate,
                DeliveryLocation = i.DeliveryLocation
            }).ToList() ?? new List<TenderItemDto>(),
            CreatedAt = lot.CreatedAt
        };
    }

    #endregion
}
