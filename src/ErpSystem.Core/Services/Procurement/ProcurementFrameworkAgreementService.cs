using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementFrameworkAgreementService :
    IProcurementFrameworkAgreementService
{
    private const string SourceTypeName = "ProcurementFrameworkAgreement";
    private const string EventType = "ProcurementFrameworkAgreementControl";
    private const string ManagePermission = "procurement.contract.manage";
    private const string ApprovePermission = "procurement.contract.approve";
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToArray();
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IWorkflowInstanceService _workflowInstances;
    private readonly ISupplierValidationService _supplierValidation;
    private readonly ICentralDocumentRepositoryFileService _documents;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly ILogger<ProcurementFrameworkAgreementService> _logger;

    public ProcurementFrameworkAgreementService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IWorkflowInstanceService workflowInstances,
        ISupplierValidationService supplierValidation,
        ICentralDocumentRepositoryFileService documents,
        INotificationTopicPublisher notificationTopics,
        ILogger<ProcurementFrameworkAgreementService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _workflowInstances = workflowInstances;
        _supplierValidation = supplierValidation;
        _documents = documents;
        _notificationTopics = notificationTopics;
        _logger = logger;
    }

    private IGenericRepository<ProcurementFrameworkAgreement> Agreements =>
        _unitOfWork.Repository<ProcurementFrameworkAgreement>();
    private IGenericRepository<ProcurementFrameworkAgreementCategory> Categories =>
        _unitOfWork.Repository<ProcurementFrameworkAgreementCategory>();
    private IGenericRepository<ProcurementFrameworkPriceListLine> PriceLines =>
        _unitOfWork.Repository<ProcurementFrameworkPriceListLine>();
    private IGenericRepository<ProcurementFrameworkCallOffAuthority> Authorities =>
        _unitOfWork.Repository<ProcurementFrameworkCallOffAuthority>();
    private IGenericRepository<ProcurementFrameworkAgreementDocument> AgreementDocuments =>
        _unitOfWork.Repository<ProcurementFrameworkAgreementDocument>();
    private IGenericRepository<ProcurementFrameworkAgreementExtension> Extensions =>
        _unitOfWork.Repository<ProcurementFrameworkAgreementExtension>();
    private IGenericRepository<ProcurementFrameworkBalanceMovement> BalanceMovements =>
        _unitOfWork.Repository<ProcurementFrameworkBalanceMovement>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions =>
        _unitOfWork.Repository<WorkflowDefinition>();
    private IGenericRepository<WorkflowInstance> WorkflowInstances =>
        _unitOfWork.Repository<WorkflowInstance>();

    public async Task<ProcurementFrameworkAgreementSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        var query = Agreements.GetQueryable(item =>
            item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        var all = await query.AsNoTracking()
            .Include(item => item.Extensions)
            .ToListAsync(cancellationToken);
        var movements = await BalanceMovements.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var effectiveFamilies = ProcurementFrameworkCallOffCommercialRules
            .SummarizeFamilies(
                all.Select(ToAgreementRevisionState),
                movements.Select(item =>
                    new ProcurementFrameworkCallOffCommercialRules.MovementState(
                        item.AgreementId,
                        item.MovementType,
                        item.Amount)),
                now)
            .Where(item => item.IsEffective)
            .ToList();
        var byCurrency = effectiveFamilies.GroupBy(item => item.CurrencyCode)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.CeilingAmount));
        var availableByCurrency = effectiveFamilies
            .GroupBy(item => item.CurrencyCode)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.AvailableAmount));
        return new ProcurementFrameworkAgreementSummaryDto
        {
            TotalVersions = all.Count,
            DraftCount = all.Count(item => item.Status == ProcurementFrameworkAgreementStatus.Draft),
            PendingApprovalCount = all.Count(item =>
                item.Status == ProcurementFrameworkAgreementStatus.PendingApproval),
            PublishedCount = all.Count(item =>
                item.Status == ProcurementFrameworkAgreementStatus.Published),
            EffectiveCount = effectiveFamilies.Count,
            ExpiringWithin90DaysCount = effectiveFamilies.Count(item =>
                item.EffectiveEndUtc.HasValue &&
                item.EffectiveEndUtc.Value <= now.AddDays(90)),
            PendingExtensionCount = all.Sum(item => item.Extensions.Count(extension =>
                !extension.IsDeleted &&
                extension.Status == ProcurementFrameworkExtensionStatus.PendingApproval)),
            TotalEffectiveCeiling = byCurrency.Count == 1 ? byCurrency.Values.Single() : 0m,
            TotalEffectiveAvailable = availableByCurrency.Count == 1
                ? availableByCurrency.Values.Single()
                : 0m,
            EffectiveCeilingByCurrency = byCurrency,
            EffectiveAvailableByCurrency = availableByCurrency
        };
    }

    public async Task<ProcurementFrameworkAgreementPageDto> SearchAsync(
        ProcurementFrameworkAgreementSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        request.Page = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 200);
        var now = DateTime.UtcNow;
        var query = Agreements.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.AgreementNumber.Contains(search) ||
                item.Title.Contains(search) ||
                item.SourceReference.Contains(search) ||
                item.BusinessPartner.PartnerCode.Contains(search) ||
                item.BusinessPartner.PartnerName.Contains(search));
        }
        if (request.BusinessPartnerId.HasValue)
            query = query.Where(item => item.BusinessPartnerId == request.BusinessPartnerId.Value);
        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);
        if (request.SourceType.HasValue)
            query = query.Where(item => item.SourceType == request.SourceType.Value);
        if (request.EffectiveOnly == true)
        {
            query = query.Where(item =>
                item.Status == ProcurementFrameworkAgreementStatus.Published &&
                item.EffectiveFromUtc <= now &&
                (item.EffectiveToUtc > now ||
                 item.Extensions.Any(extension =>
                     !extension.IsDeleted &&
                     extension.Status ==
                     ProcurementFrameworkExtensionStatus.Approved &&
                     extension.ProposedEndUtc > now)));
        }

        var total = await query.CountAsync(cancellationToken);
        var pageIds = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Version)
            .Select(item => item.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var pageOrder = pageIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);
        var pageRows = pageIds.Count == 0
            ? []
            : (await AgreementQuery()
                .AsNoTracking()
                .Where(item => pageIds.Contains(item.Id))
                .AsSplitQuery()
                .ToListAsync(cancellationToken))
            .OrderBy(item => pageOrder[item.Id])
            .ToList();
        var familySummaries = await ResolveFamilySummariesAsync(
            pageRows,
            now,
            cancellationToken);
        return new ProcurementFrameworkAgreementPageDto
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total,
            Items = pageRows
                .Select(item => MapList(
                    item,
                    now,
                    familySummaries.GetValueOrDefault(item.Id)))
                .ToList()
        };
    }

    public async Task<ProcurementFrameworkAgreementDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        var agreement = await LoadAsync(id, false, cancellationToken);
        var familySummaries = await ResolveFamilySummariesAsync(
            [agreement],
            now,
            cancellationToken);
        return Map(
            agreement,
            now,
            familySummaries.GetValueOrDefault(agreement.Id));
    }

    public async Task<IReadOnlyList<ProcurementFrameworkWorkflowOptionDto>>
        GetWorkflowOptionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ThenByDescending(item => item.Version)
            .Select(item => new ProcurementFrameworkWorkflowOptionDto
            {
                Id = item.Id,
                Name = item.Name,
                Version = item.Version
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementFrameworkCategoryOptionDto>>
        GetCategoryOptionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return await _unitOfWork.Repository<PartnerCategory>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.IsActive)
            .AsNoTracking()
            .OrderBy(item => item.CategoryCode)
            .Select(item => new ProcurementFrameworkCategoryOptionDto
            {
                Id = item.Id,
                Code = item.CategoryCode,
                Name = item.CategoryName
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementFrameworkItemOptionDto>>
        GetItemOptionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.Status == ItemStatus.Active)
            .AsNoTracking()
            .OrderBy(item => item.ItemCode)
            .Select(item => new ProcurementFrameworkItemOptionDto
            {
                Id = item.Id,
                Code = item.ItemCode,
                Name = item.Name,
                UnitOfMeasure = item.UnitOfMeasure
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementFrameworkSourceOptionDto>>
        GetSourceOptionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var decisions = await _unitOfWork.Repository<ProcurementAwardReadinessDecision>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.Status == ProcurementAwardReadinessDecisionStatus.Ready)
            .AsNoTracking()
            .OrderByDescending(item => item.EvaluatedAtUtc)
            .ToListAsync(cancellationToken);
        var options = new List<ProcurementFrameworkSourceOptionDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var decision in decisions)
        {
            foreach (var supplierId in ParseIds(decision.RecommendedBusinessPartnerIdsJson))
            {
                var key = $"{decision.SourceType}:{decision.SourceId:N}:{supplierId:N}";
                if (!seen.Add(key)) continue;
                try
                {
                    var source = await ResolveSourceAsync(
                        decision.SourceType, decision.SourceId, supplierId, cancellationToken);
                    options.Add(new ProcurementFrameworkSourceOptionDto
                    {
                        SourceType = source.SourceType,
                        SourceId = source.SourceId,
                        SourceReference = source.SourceReference,
                        BusinessPartnerId = supplierId,
                        SupplierCode = source.Supplier.PartnerCode,
                        SupplierName = source.Supplier.PartnerName,
                        AwardAmount = source.AwardAmount,
                        CurrencyCode = source.CurrencyCode,
                        AwardedAtUtc = source.AwardedAtUtc,
                        AwardReadinessDecisionId = source.Readiness.Id
                    });
                }
                catch (ProcurementFrameworkAgreementConflictException)
                {
                    // Stale readiness is intentionally omitted from selectable source options.
                }
                catch (ProcurementFrameworkAgreementNotFoundException)
                {
                    // Deleted source/supplier lineage is not selectable.
                }
            }
        }
        return options.OrderByDescending(item => item.AwardedAtUtc)
            .ThenBy(item => item.SourceReference)
            .ToList();
    }

    public async Task<ProcurementFrameworkAgreementDto> CreateAsync(
        CreateProcurementFrameworkAgreementRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(ManagePermission, "NEW", correlation, cancellationToken);
        var replay = await AgreementQuery().AsNoTracking().SingleOrDefaultAsync(item =>
            item.CreationCorrelationId == correlation, cancellationToken);
        if (replay is not null) return Map(replay, DateTime.UtcNow);
        ValidateCommercialRequest(request.Title, request.CeilingAmount, request.CurrencyCode,
            request.EffectiveFromUtc, request.EffectiveToUtc, request.CategoryIds,
            request.PriceLines, request.CallOffAuthorities);
        var source = await ResolveSourceAsync(
            request.SourceType, request.SourceId, request.BusinessPartnerId, cancellationToken);
        EnsureSourceCurrency(source, request.CurrencyCode);
        EnsureSourceCeiling(source, request.CeilingAmount);
        var workflow = await ValidateWorkflowAsync(request.WorkflowDefinitionId, cancellationToken);
        await ValidateCurrencyAsync(request.CurrencyCode, cancellationToken);
        var categoryRows = await ResolveCategoriesAsync(request.CategoryIds, cancellationToken);
        var priceRows = await ResolvePriceLinesAsync(request.PriceLines, cancellationToken);
        var authorityRows = ResolveAuthorities(
            request.CallOffAuthorities, request.EffectiveFromUtc, request.EffectiveToUtc);
        await ValidateAuthorityUsersAsync(authorityRows, cancellationToken);
        var eligibility = await EnforceSupplierAsync(
            request.BusinessPartnerId, request.CategoryIds, source, correlation, cancellationToken);
        var now = DateTime.UtcNow;
        var family = Guid.NewGuid();
        var agreement = new ProcurementFrameworkAgreement
        {
            TenantId = _currentUser.TenantId,
            AgreementKey = family,
            AgreementNumber = $"FA-{now:yyyy}-{family:N}"[..20].ToUpperInvariant(),
            Title = request.Title.Trim(),
            Version = 1,
            Status = ProcurementFrameworkAgreementStatus.Draft,
            BusinessPartnerId = source.Supplier.Id,
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            SourceReference = source.SourceReference,
            AwardReadinessDecisionId = source.Readiness.Id,
            SourceIntegrityHash = source.Readiness.IntegrityHash,
            SupplierEligibilityDecisionHash = eligibility.DecisionHash,
            PriceListReference = $"FPL-{now:yyyy}-{family:N}"[..21].ToUpperInvariant(),
            PriceListVersion = 1,
            CeilingAmount = RoundMoney(request.CeilingAmount),
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc),
            EffectiveToUtc = EnsureUtc(request.EffectiveToUtc),
            WorkflowDefinitionId = workflow.Id,
            Description = Trim(request.Description, 1000),
            TermsSummary = Trim(request.TermsSummary, 1000),
            CreationCorrelationId = correlation,
            LastOperationCorrelationId = correlation,
            LastOperation = "Created",
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        AddChildren(agreement, categoryRows, priceRows, authorityRows, now);
        Capture(agreement);
        await ExecuteAsync(async () =>
        {
            if (await Agreements.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.SourceType == request.SourceType &&
                    item.SourceId == request.SourceId &&
                    item.BusinessPartnerId == request.BusinessPartnerId &&
                    !item.IsDeleted &&
                    item.Status != ProcurementFrameworkAgreementStatus.Rejected &&
                    item.Status != ProcurementFrameworkAgreementStatus.Terminated)
                .AnyAsync(cancellationToken))
            {
                throw Conflict("FRAMEWORK_AGREEMENT_SOURCE_ALREADY_REGISTERED",
                    "This supplier and approved source already have a framework-agreement family.");
            }
            await Agreements.AddAsync(agreement);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(
                agreement, "Created", ProcurementControlEventResult.Succeeded,
                null, Snapshot(agreement), request.Description, [],
                correlation, now, cancellationToken);
        }, cancellationToken, IsolationLevel.Serializable);
        return Map(await LoadAsync(agreement.Id, false, cancellationToken), now);
    }

    public async Task<ProcurementFrameworkAgreementDto> UpdateAsync(
        Guid id,
        UpdateProcurementFrameworkAgreementRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var agreement = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, agreement.AgreementNumber,
            correlation, cancellationToken);
        if (IsReplay(agreement, "Updated", correlation))
            return Map(agreement, DateTime.UtcNow);
        EnsureRowVersion(agreement.RowVersion, request.RowVersion);
        EnsureStatus(agreement, ProcurementFrameworkAgreementStatus.Draft,
            "Only a Draft framework agreement can be edited.");
        ValidateCommercialRequest(request.Title, request.CeilingAmount, request.CurrencyCode,
            request.EffectiveFromUtc, request.EffectiveToUtc, request.CategoryIds,
            request.PriceLines, request.CallOffAuthorities);
        var source = await ResolveSourceAsync(
            agreement.SourceType, agreement.SourceId, agreement.BusinessPartnerId,
            cancellationToken);
        EnsureSourceCurrency(source, request.CurrencyCode);
        EnsureSourceCeiling(source, request.CeilingAmount);
        var workflow = await ValidateWorkflowAsync(request.WorkflowDefinitionId, cancellationToken);
        await ValidateCurrencyAsync(request.CurrencyCode, cancellationToken);
        var categoryRows = await ResolveCategoriesAsync(request.CategoryIds, cancellationToken);
        var priceRows = await ResolvePriceLinesAsync(request.PriceLines, cancellationToken);
        var authorityRows = ResolveAuthorities(
            request.CallOffAuthorities, request.EffectiveFromUtc, request.EffectiveToUtc);
        await ValidateAuthorityUsersAsync(authorityRows, cancellationToken);
        var eligibility = await EnforceSupplierAsync(
            agreement.BusinessPartnerId, request.CategoryIds, source, correlation, cancellationToken);
        var before = Snapshot(agreement);
        var now = DateTime.UtcNow;
        RetireDraftChildren(agreement, now);
        agreement.Title = request.Title.Trim();
        agreement.CeilingAmount = RoundMoney(request.CeilingAmount);
        agreement.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        agreement.EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc);
        agreement.EffectiveToUtc = EnsureUtc(request.EffectiveToUtc);
        agreement.WorkflowDefinitionId = workflow.Id;
        agreement.AwardReadinessDecisionId = source.Readiness.Id;
        agreement.SourceIntegrityHash = source.Readiness.IntegrityHash;
        agreement.SupplierEligibilityDecisionHash = eligibility.DecisionHash;
        agreement.Description = Trim(request.Description, 1000);
        agreement.TermsSummary = Trim(request.TermsSummary, 1000);
        AddChildren(agreement, categoryRows, priceRows, authorityRows, now);
        Touch(agreement, "Updated", correlation, now);
        Capture(agreement);
        await ExecuteAsync(async () =>
        {
            await Agreements.UpdateAsync(agreement);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(
                agreement, "Updated", ProcurementControlEventResult.Succeeded,
                before, Snapshot(agreement), request.Description, [],
                correlation, now, cancellationToken);
        }, cancellationToken);
        return Map(await LoadAsync(agreement.Id, false, cancellationToken), now);
    }

    public async Task<ProcurementFrameworkAgreementDto> CloneAsync(
        Guid id,
        CloneProcurementFrameworkAgreementRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var sourceAgreement = await LoadAsync(id, false, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, sourceAgreement.AgreementNumber,
            correlation, cancellationToken);
        EnsureRowVersion(sourceAgreement.RowVersion, request.RowVersion);
        if (sourceAgreement.Status is not (
                ProcurementFrameworkAgreementStatus.Published or
                ProcurementFrameworkAgreementStatus.Superseded or
                ProcurementFrameworkAgreementStatus.Expired))
            throw Conflict("FRAMEWORK_AGREEMENT_CLONE_STATUS_INVALID",
                "Only a Published, Superseded, or Expired framework agreement can be revised.");
        if (await Agreements.GetQueryable(item =>
                item.TenantId == sourceAgreement.TenantId &&
                item.AgreementKey == sourceAgreement.AgreementKey &&
                !item.IsDeleted &&
                (item.Status == ProcurementFrameworkAgreementStatus.Draft ||
                 item.Status == ProcurementFrameworkAgreementStatus.PendingApproval))
            .AnyAsync(cancellationToken))
            throw Conflict("FRAMEWORK_AGREEMENT_OPEN_REVISION_EXISTS",
                "This framework family already has an open Draft or PendingApproval revision.");
        ValidateDates(request.EffectiveFromUtc, request.EffectiveToUtc);
        var workflow = await ValidateWorkflowAsync(request.WorkflowDefinitionId, cancellationToken);
        var maximumVersion = await Agreements.GetQueryableIncludingDeleted(item =>
                item.TenantId == sourceAgreement.TenantId &&
                item.AgreementKey == sourceAgreement.AgreementKey)
            .MaxAsync(item => (int?)item.Version, cancellationToken);
        var nextVersion = (maximumVersion ?? 0) + 1;
        var replay = await AgreementQuery().AsNoTracking().SingleOrDefaultAsync(item =>
            item.CreationCorrelationId == correlation, cancellationToken);
        if (replay is not null) return Map(replay, DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var clone = new ProcurementFrameworkAgreement
        {
            TenantId = sourceAgreement.TenantId,
            AgreementKey = sourceAgreement.AgreementKey,
            AgreementNumber = sourceAgreement.AgreementNumber,
            Title = sourceAgreement.Title,
            Version = nextVersion,
            Status = ProcurementFrameworkAgreementStatus.Draft,
            BusinessPartnerId = sourceAgreement.BusinessPartnerId,
            SourceType = sourceAgreement.SourceType,
            SourceId = sourceAgreement.SourceId,
            SourceReference = sourceAgreement.SourceReference,
            AwardReadinessDecisionId = sourceAgreement.AwardReadinessDecisionId,
            SourceIntegrityHash = sourceAgreement.SourceIntegrityHash,
            SupplierEligibilityDecisionHash = sourceAgreement.SupplierEligibilityDecisionHash,
            PriceListReference = sourceAgreement.PriceListReference,
            PriceListVersion = nextVersion,
            CeilingAmount = sourceAgreement.CeilingAmount,
            CurrencyCode = sourceAgreement.CurrencyCode,
            EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc),
            EffectiveToUtc = EnsureUtc(request.EffectiveToUtc),
            WorkflowDefinitionId = workflow.Id,
            SupersedesAgreementId = sourceAgreement.Id,
            Description = sourceAgreement.Description,
            TermsSummary = Trim(request.ChangeSummary, 1000),
            CreationCorrelationId = correlation,
            LastOperationCorrelationId = correlation,
            LastOperation = "Cloned",
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        foreach (var item in sourceAgreement.Categories.Where(item => !item.IsDeleted))
            clone.Categories.Add(Clone(item, clone, now));
        foreach (var item in sourceAgreement.PriceLines.Where(item => !item.IsDeleted))
            clone.PriceLines.Add(Clone(item, clone, now));
        foreach (var item in sourceAgreement.CallOffAuthorities.Where(item => !item.IsDeleted))
            clone.CallOffAuthorities.Add(Clone(item, clone, now));
        Capture(clone);
        await ExecuteAsync(async () =>
        {
            await Agreements.AddAsync(clone);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(
                clone, "Cloned", ProcurementControlEventResult.Succeeded,
                Snapshot(sourceAgreement), Snapshot(clone), request.ChangeSummary, [],
                correlation, now, cancellationToken);
        }, cancellationToken);
        return Map(await LoadAsync(clone.Id, false, cancellationToken), now);
    }

    public Task<ProcurementFrameworkAgreementDto> SubmitAsync(
        Guid id,
        ProcurementFrameworkAgreementLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        SubmitCoreAsync(id, request, NormalizeCorrelation(correlationId), cancellationToken);

    public Task<ProcurementFrameworkAgreementDto> ApproveAsync(
        Guid id,
        ProcurementFrameworkAgreementLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        DecideCoreAsync(id, request, true, NormalizeCorrelation(correlationId), cancellationToken);

    public Task<ProcurementFrameworkAgreementDto> RejectAsync(
        Guid id,
        ProcurementFrameworkAgreementLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        DecideCoreAsync(id, request, false, NormalizeCorrelation(correlationId), cancellationToken);

    public async Task<ProcurementFrameworkAgreementDto> TerminateAsync(
        Guid id,
        ProcurementFrameworkAgreementLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var agreement = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, agreement.AgreementNumber,
            correlation, cancellationToken);
        if (IsReplay(agreement, "Terminated", correlation))
            return Map(agreement, DateTime.UtcNow);
        EnsureRowVersion(agreement.RowVersion, request.RowVersion);
        EnsureStatus(agreement, ProcurementFrameworkAgreementStatus.Published,
            "Only a Published framework agreement can be terminated.");
        EnsureLifecycleEvidence(request.Evidence);
        await EnsureIndependentActorAsync(
            agreement.PublishedById, agreement.AgreementNumber, correlation, cancellationToken);
        var now = DateTime.UtcNow;
        var before = Snapshot(agreement);
        agreement.Status = ProcurementFrameworkAgreementStatus.Terminated;
        agreement.TerminatedById = _currentUser.UserId;
        agreement.TerminatedAtUtc = now;
        agreement.TerminationReason = Trim(request.Comment, 1000);
        Touch(agreement, "Terminated", correlation, now);
        Capture(agreement);
        await ExecuteAsync(async () =>
        {
            await Agreements.UpdateAsync(agreement);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(
                agreement, "Terminated", ProcurementControlEventResult.Rejected,
                before, Snapshot(agreement), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
        }, cancellationToken);
        await PublishNotificationAsync(
            "procurement.framework-agreement.terminated", agreement, cancellationToken);
        return Map(await LoadAsync(agreement.Id, false, cancellationToken), now);
    }

    public async Task<ProcurementFrameworkAgreementDocumentDto> AddDocumentAsync(
        Guid id,
        AddProcurementFrameworkAgreementDocumentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var agreement = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, agreement.AgreementNumber,
            correlation, cancellationToken);
        EnsureStatus(agreement, ProcurementFrameworkAgreementStatus.Draft,
            "Documents can be attached only to a Draft framework agreement.");
        if (request.FileUploadRecordId == Guid.Empty)
            throw Validation("FRAMEWORK_AGREEMENT_UPLOAD_REQUIRED",
                "A clean controlled upload is required.");
        var upload = await _unitOfWork.Repository<FileUploadRecord>()
            .GetQueryable(item =>
                item.TenantId == agreement.TenantId &&
                item.Id == request.FileUploadRecordId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("FRAMEWORK_AGREEMENT_UPLOAD_NOT_FOUND",
                "The controlled upload was not found in the current tenant.");
        if (upload.VirusScanStatus != FileVirusScanStatus.Clean)
            throw Validation("FRAMEWORK_AGREEMENT_UPLOAD_NOT_CLEAN",
                "Framework documents require a Clean malware-scan result.");
        if (upload.CreatedById != _currentUser.UserId)
            throw new ProcurementFrameworkAgreementAuthorizationException(
                "Only the actor who uploaded this controlled file can attach it.");
        if (await AgreementDocuments.GetQueryable(item =>
                item.TenantId == agreement.TenantId &&
                item.FileUploadRecordId == upload.Id && !item.IsDeleted)
            .AnyAsync(cancellationToken))
            throw Conflict("FRAMEWORK_AGREEMENT_UPLOAD_ALREADY_ATTACHED",
                "The controlled upload is already attached to a framework agreement.");
        var now = DateTime.UtcNow;
        CentralDocumentRepositoryLink? link = null;
        ProcurementFrameworkAgreementDocument? document = null;
        try
        {
            await ExecuteAsync(async () =>
            {
                link = await _documents.RegisterAsync(new CentralDocumentRepositoryRegistration
                {
                    TenantId = agreement.TenantId,
                    ActorUserId = _currentUser.UserId,
                    ActorName = ActorName,
                    FileUploadRecordId = upload.Id,
                    SourceModule = "Procurement",
                    SourceLabel = $"Framework agreement {agreement.AgreementNumber} v{agreement.Version}",
                    SourceEntityType = SourceTypeName,
                    SourceRecordId = agreement.Id,
                    SourceRecordReference = agreement.AgreementNumber,
                    Title = request.Title,
                    DocumentType = request.DocumentType,
                    AccessProfile = "Procurement contract restricted",
                    VersionNumber = $"v{agreement.Version}.0",
                    VersionStatus = "Submitted",
                    Notes = $"Framework agreement document; required={request.IsRequired}"
                }, cancellationToken);
                document = new ProcurementFrameworkAgreementDocument
                {
                    TenantId = agreement.TenantId,
                    AgreementId = agreement.Id,
                    DocumentType = Required(request.DocumentType, 100),
                    Title = Required(request.Title, 250),
                    FileUploadRecordId = upload.Id,
                    CentralDocumentRecordId = link.DocumentRecordId,
                    CentralDocumentVersionId = link.DocumentVersionId,
                    DmsReference = link.DocumentReference,
                    IsRequired = request.IsRequired,
                    IsCurrent = true,
                    CreatedAt = now,
                    CreatedBy = ActorName,
                    CreatedById = _currentUser.UserId
                };
                Capture(document);
                await AgreementDocuments.AddAsync(document);
                Touch(agreement, "DocumentAdded", correlation, now);
                Capture(agreement);
                await Agreements.UpdateAsync(agreement);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync(
                    agreement, "DocumentAdded",
                    ProcurementControlEventResult.Succeeded, null,
                    new { document.Id, document.DocumentType, document.DmsReference },
                    request.Title, [], correlation, now, cancellationToken);
            }, cancellationToken);
            return Map(document!);
        }
        catch
        {
            if (link is not null)
            {
                try
                {
                    await _documents.DeleteAsync(
                        agreement.TenantId, link.DocumentRecordId,
                        _currentUser.UserId, cancellationToken);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogWarning(cleanupException,
                        "Failed to compensate central-DMS registration {DocumentRecordId}",
                        link.DocumentRecordId);
                }
            }
            throw;
        }
    }

    public async Task<ProcurementFrameworkAgreementDocumentDto> RetireDocumentAsync(
        Guid id,
        Guid documentId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var agreement = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, agreement.AgreementNumber,
            correlation, cancellationToken);
        EnsureStatus(agreement, ProcurementFrameworkAgreementStatus.Draft,
            "Documents can be retired only from a Draft framework agreement.");
        var document = agreement.Documents.SingleOrDefault(item =>
            item.Id == documentId && !item.IsDeleted)
            ?? throw NotFound("FRAMEWORK_AGREEMENT_DOCUMENT_NOT_FOUND",
                "The framework document was not found.");
        if (!document.IsCurrent) return Map(document);
        var now = DateTime.UtcNow;
        document.IsCurrent = false;
        document.RetiredAtUtc = now;
        document.RetiredById = _currentUser.UserId;
        document.UpdatedAt = now;
        document.UpdatedBy = ActorName;
        document.LastModifiedById = _currentUser.UserId;
        Capture(document);
        Touch(agreement, "DocumentRetired", correlation, now);
        Capture(agreement);
        await ExecuteAsync(async () =>
        {
            await AgreementDocuments.UpdateAsync(document);
            await Agreements.UpdateAsync(agreement);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(
                agreement, "DocumentRetired",
                ProcurementControlEventResult.Succeeded, null,
                new { document.Id, document.DocumentType, document.DmsReference },
                "Draft document retired from the current agreement revision.",
                [], correlation, now, cancellationToken);
        }, cancellationToken);
        return Map(document);
    }

    public async Task<CentralDocumentRepositoryContent?> OpenDocumentAsync(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var agreement = await LoadAsync(id, false, cancellationToken);
        var document = agreement.Documents.SingleOrDefault(item =>
            item.Id == documentId && !item.IsDeleted)
            ?? throw NotFound("FRAMEWORK_AGREEMENT_DOCUMENT_NOT_FOUND",
                "The framework document was not found.");
        return await _documents.OpenAsync(
            agreement.TenantId, document.CentralDocumentRecordId,
            document.CentralDocumentVersionId, cancellationToken);
    }

    public async Task<ProcurementFrameworkAgreementExtensionDto> RequestExtensionAsync(
        Guid id,
        RequestProcurementFrameworkAgreementExtension request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var agreement = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, agreement.AgreementNumber,
            correlation, cancellationToken);
        EnsureRowVersion(agreement.RowVersion, request.AgreementRowVersion);
        EnsureStatus(agreement, ProcurementFrameworkAgreementStatus.Published,
            "Only a Published framework agreement can be extended.");
        EnsureLifecycleEvidence(request.Evidence);
        var currentEnd = ProcurementFrameworkAgreementRules.EffectiveEnd(agreement);
        var proposed = EnsureUtc(request.ProposedEndUtc);
        if (proposed <= currentEnd)
            throw Validation("FRAMEWORK_AGREEMENT_EXTENSION_END_INVALID",
                "The proposed extension end must be later than the current effective end.");
        if (await Extensions.GetQueryable(item =>
                item.TenantId == agreement.TenantId &&
                item.AgreementId == agreement.Id && !item.IsDeleted &&
                item.Status == ProcurementFrameworkExtensionStatus.PendingApproval)
            .AnyAsync(cancellationToken))
            throw Conflict("FRAMEWORK_AGREEMENT_EXTENSION_PENDING",
                "This agreement already has a pending extension.");
        var workflow = await ValidateWorkflowAsync(request.WorkflowDefinitionId, cancellationToken);
        var maximumSequence = await Extensions.GetQueryableIncludingDeleted(item =>
                item.TenantId == agreement.TenantId && item.AgreementId == agreement.Id)
            .MaxAsync(item => (int?)item.SequenceNumber, cancellationToken);
        var sequence = (maximumSequence ?? 0) + 1;
        var now = DateTime.UtcNow;
        var extension = new ProcurementFrameworkAgreementExtension
        {
            TenantId = agreement.TenantId,
            AgreementId = agreement.Id,
            SequenceNumber = sequence,
            Status = ProcurementFrameworkExtensionStatus.PendingApproval,
            PreviousEndUtc = currentEnd,
            ProposedEndUtc = proposed,
            Reason = Required(request.Reason, 1000),
            WorkflowDefinitionId = workflow.Id,
            SubmittedById = _currentUser.UserId,
            SubmittedByName = ActorName,
            SubmittedAtUtc = now,
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        Capture(extension);
        await ExecuteAsync(async () =>
        {
            await Extensions.AddAsync(extension);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var instance = await _workflowInstances.StartWorkflowAsync(
                workflow.Id, workflow.EntityTypeId, extension.Id.ToString(),
                _currentUser.UserId, new
                {
                    agreement.Id,
                    agreement.AgreementNumber,
                    agreement.Version,
                    extension.SequenceNumber,
                    extension.PreviousEndUtc,
                    extension.ProposedEndUtc,
                    extension.Reason
                }, cancellationToken);
            extension.WorkflowInstanceId = instance.Id;
            Capture(extension);
            await Extensions.UpdateAsync(extension);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(
                agreement, "ExtensionSubmitted",
                ProcurementControlEventResult.ReviewRequired, null, Snapshot(extension),
                request.Reason, request.Evidence, correlation, now, cancellationToken);
        }, cancellationToken);
        await PublishNotificationAsync(
            "procurement.framework-agreement.extension-submitted", agreement, cancellationToken);
        return Map(extension);
    }

    public async Task<ProcurementFrameworkAgreementExtensionDto> DecideExtensionAsync(
        Guid id,
        Guid extensionId,
        DecideProcurementFrameworkAgreementExtension request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var agreement = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, agreement.AgreementNumber,
            correlation, cancellationToken);
        EnsureLifecycleEvidence(request.Evidence);
        var extension = agreement.Extensions.SingleOrDefault(item =>
            item.Id == extensionId && !item.IsDeleted)
            ?? throw NotFound("FRAMEWORK_AGREEMENT_EXTENSION_NOT_FOUND",
                "The framework extension was not found.");
        EnsureRowVersion(extension.RowVersion, request.RowVersion);
        if (extension.Status != ProcurementFrameworkExtensionStatus.PendingApproval)
            return Map(extension);
        if (request.Approve)
            EnsureStatus(
                agreement,
                ProcurementFrameworkAgreementStatus.Published,
                "Only a Published framework agreement can receive an approved extension.");
        await EnsureIndependentActorAsync(
            extension.SubmittedById, agreement.AgreementNumber, correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(
            extension.WorkflowDefinitionId, extension.WorkflowInstanceId,
            extension.Id, request.Approve, cancellationToken);
        var now = DateTime.UtcNow;
        extension.Status = request.Approve
            ? ProcurementFrameworkExtensionStatus.Approved
            : ProcurementFrameworkExtensionStatus.Rejected;
        extension.DecidedById = _currentUser.UserId;
        extension.DecidedByName = ActorName;
        extension.DecidedAtUtc = now;
        extension.DecisionComment = Trim(request.Comment, 1000);
        extension.UpdatedAt = now;
        extension.UpdatedBy = ActorName;
        extension.LastModifiedById = _currentUser.UserId;
        Capture(extension);
        await ExecuteAsync(async () =>
        {
            await Extensions.UpdateAsync(extension);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(
                agreement,
                request.Approve ? "ExtensionApproved" : "ExtensionRejected",
                request.Approve
                    ? ProcurementControlEventResult.Succeeded
                    : ProcurementControlEventResult.Rejected,
                null, Snapshot(extension), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
        }, cancellationToken);
        await PublishNotificationAsync(
            request.Approve
                ? "procurement.framework-agreement.extension-approved"
                : "procurement.framework-agreement.extension-rejected",
            agreement, cancellationToken);
        return Map(extension);
    }

    public async Task<int> ProcessLifecycleAsync(
        Guid? tenantId = null,
        DateTime? atUtc = null,
        CancellationToken cancellationToken = default)
    {
        var system = tenantId.HasValue;
        var targetTenant = tenantId ?? _currentUser.TenantId;
        if (targetTenant == Guid.Empty)
            throw new ProcurementFrameworkAgreementAuthorizationException(
                "A tenant context is required.");
        if (!system)
            await EnsureCapabilityAsync(
                ManagePermission, "LIFECYCLE", Guid.NewGuid().ToString("N"), cancellationToken);
        var now = EnsureUtc(atUtc ?? DateTime.UtcNow);
        var published = await Agreements.GetQueryable(item =>
                item.TenantId == targetTenant && !item.IsDeleted &&
                item.Status == ProcurementFrameworkAgreementStatus.Published)
            .Include(item => item.Extensions)
            .OrderBy(item => item.AgreementKey)
            .ThenByDescending(item => item.Version)
            .ToListAsync(cancellationToken);
        var changed = new List<ProcurementFrameworkAgreement>();
        foreach (var family in published.GroupBy(item => item.AgreementKey))
        {
            var effective = family.Where(item =>
                    item.EffectiveFromUtc <= now &&
                    ProcurementFrameworkAgreementRules.EffectiveEnd(item) > now)
                .OrderByDescending(item => item.Version)
                .FirstOrDefault();
            foreach (var item in family)
            {
                if (effective is not null && item.Id != effective.Id &&
                    item.EffectiveFromUtc <= now && item.Version < effective.Version)
                {
                    item.Status = ProcurementFrameworkAgreementStatus.Superseded;
                    item.SupersededByAgreementId = effective.Id;
                }
                else if (ProcurementFrameworkAgreementRules.EffectiveEnd(item) <= now)
                {
                    item.Status = ProcurementFrameworkAgreementStatus.Expired;
                }
                else
                {
                    continue;
                }
                TouchSystem(item, "LifecycleProcessed", now);
                Capture(item);
                changed.Add(item);
            }
        }
        if (changed.Count != 0)
        {
            await ExecuteAsync(async () =>
            {
                foreach (var item in changed)
                    await Agreements.UpdateAsync(item);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                foreach (var item in changed)
                {
                    var request = BuildEvent(
                        item, "LifecycleProcessed",
                        ProcurementControlEventResult.Succeeded, null, Snapshot(item),
                        "Scheduled framework lifecycle processing.", [],
                        $"framework-lifecycle-{item.Id:N}-{now:yyyyMMddHHmm}", now);
                    if (system)
                        await _controlEvents.RecordSystemAsync(
                            targetTenant, "Framework lifecycle processor",
                            request, cancellationToken);
                    else
                        await _controlEvents.RecordAsync(request, cancellationToken);
                }
            }, cancellationToken);
        }
        return changed.Count;
    }

    private async Task<ProcurementFrameworkAgreementDto> SubmitCoreAsync(
        Guid id,
        ProcurementFrameworkAgreementLifecycleRequest request,
        string correlation,
        CancellationToken cancellationToken)
    {
        var agreement = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, agreement.AgreementNumber,
            correlation, cancellationToken);
        if (IsReplay(agreement, "Submitted", correlation))
            return Map(agreement, DateTime.UtcNow);
        EnsureRowVersion(agreement.RowVersion, request.RowVersion);
        EnsureStatus(agreement, ProcurementFrameworkAgreementStatus.Draft,
            "Only a Draft framework agreement can be submitted.");
        EnsureLifecycleEvidence(request.Evidence);
        EnsureCompleteDraft(agreement);
        var source = await ResolveSourceAsync(
            agreement.SourceType, agreement.SourceId, agreement.BusinessPartnerId,
            cancellationToken);
        EnsureSourceCurrency(source, agreement.CurrencyCode);
        EnsureSourceCeiling(source, agreement.CeilingAmount);
        await EnsureFamilyCommitmentCeilingAsync(
            agreement,
            cancellationToken);
        var eligibility = await EnforceSupplierAsync(
            agreement.BusinessPartnerId,
            agreement.Categories.Where(item => !item.IsDeleted)
                .Select(item => item.PartnerCategoryId).ToList(),
            source, correlation, cancellationToken);
        var workflow = await ValidateWorkflowAsync(
            agreement.WorkflowDefinitionId, cancellationToken);
        var now = DateTime.UtcNow;
        var before = Snapshot(agreement);
        await ExecuteAsync(async () =>
        {
            agreement.Status = ProcurementFrameworkAgreementStatus.PendingApproval;
            agreement.AwardReadinessDecisionId = source.Readiness.Id;
            agreement.SourceIntegrityHash = source.Readiness.IntegrityHash;
            agreement.SupplierEligibilityDecisionHash = eligibility.DecisionHash;
            agreement.SubmittedById = _currentUser.UserId;
            agreement.SubmittedByName = ActorName;
            agreement.SubmittedAtUtc = now;
            agreement.ReviewComment = Trim(request.Comment, 1000);
            Touch(agreement, "Submitted", correlation, now);
            Capture(agreement);
            await Agreements.UpdateAsync(agreement);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var instance = await _workflowInstances.StartWorkflowAsync(
                workflow.Id, workflow.EntityTypeId, agreement.Id.ToString(),
                _currentUser.UserId, new
                {
                    agreement.AgreementNumber,
                    agreement.Version,
                    agreement.BusinessPartnerId,
                    agreement.SourceType,
                    agreement.SourceId,
                    agreement.SourceReference,
                    agreement.CeilingAmount,
                    agreement.CurrencyCode,
                    agreement.EffectiveFromUtc,
                    agreement.EffectiveToUtc,
                    CategoryIds = agreement.Categories
                        .Where(item => !item.IsDeleted)
                        .Select(item => item.PartnerCategoryId),
                    agreement.PriceListReference,
                    agreement.PriceListVersion
                }, cancellationToken);
            agreement.WorkflowInstanceId = instance.Id;
            Capture(agreement);
            await Agreements.UpdateAsync(agreement);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(
                agreement, "Submitted",
                ProcurementControlEventResult.ReviewRequired,
                before, Snapshot(agreement), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
        }, cancellationToken);
        await PublishNotificationAsync(
            "procurement.framework-agreement.submitted", agreement, cancellationToken);
        return Map(await LoadAsync(agreement.Id, false, cancellationToken), now);
    }

    private async Task<ProcurementFrameworkAgreementDto> DecideCoreAsync(
        Guid id,
        ProcurementFrameworkAgreementLifecycleRequest request,
        bool approve,
        string correlation,
        CancellationToken cancellationToken)
    {
        var agreement = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, agreement.AgreementNumber,
            correlation, cancellationToken);
        var operation = approve ? "Published" : "Rejected";
        if (IsReplay(agreement, operation, correlation))
            return Map(agreement, DateTime.UtcNow);
        EnsureRowVersion(agreement.RowVersion, request.RowVersion);
        EnsureStatus(agreement, ProcurementFrameworkAgreementStatus.PendingApproval,
            "Only a PendingApproval framework agreement can be decided.");
        EnsureLifecycleEvidence(request.Evidence);
        await EnsureIndependentActorAsync(
            agreement.SubmittedById, agreement.AgreementNumber, correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(
            agreement.WorkflowDefinitionId, agreement.WorkflowInstanceId,
            agreement.Id, approve, cancellationToken);
        var now = DateTime.UtcNow;
        var before = Snapshot(agreement);
        if (approve)
        {
            EnsureCompleteDraft(agreement);
            var source = await ResolveSourceAsync(
                agreement.SourceType, agreement.SourceId, agreement.BusinessPartnerId,
                cancellationToken);
            EnsureSourceCurrency(source, agreement.CurrencyCode);
            EnsureSourceCeiling(source, agreement.CeilingAmount);
            var eligibility = await EnforceSupplierAsync(
                agreement.BusinessPartnerId,
                agreement.Categories.Where(item => !item.IsDeleted)
                    .Select(item => item.PartnerCategoryId).ToList(),
                source, correlation, cancellationToken);
            agreement.Status = ProcurementFrameworkAgreementStatus.Published;
            agreement.AwardReadinessDecisionId = source.Readiness.Id;
            agreement.SourceIntegrityHash = source.Readiness.IntegrityHash;
            agreement.SupplierEligibilityDecisionHash = eligibility.DecisionHash;
            agreement.PublishedById = _currentUser.UserId;
            agreement.PublishedByName = ActorName;
            agreement.PublishedAtUtc = now;
        }
        else
        {
            agreement.Status = ProcurementFrameworkAgreementStatus.Rejected;
            agreement.RejectedById = _currentUser.UserId;
            agreement.RejectedByName = ActorName;
            agreement.RejectedAtUtc = now;
        }
        agreement.ReviewComment = Trim(request.Comment, 1000);
        Touch(agreement, operation, correlation, now);
        Capture(agreement);
        await ExecuteAsync(async () =>
        {
            if (approve)
            {
                await EnsureFamilyCommitmentCeilingAsync(
                    agreement,
                    cancellationToken);
            }
            await Agreements.UpdateAsync(agreement);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (approve && agreement.EffectiveFromUtc <= now)
            {
                var prior = await Agreements.GetQueryable(item =>
                        item.TenantId == agreement.TenantId &&
                        item.AgreementKey == agreement.AgreementKey &&
                        item.Id != agreement.Id && !item.IsDeleted &&
                        item.Status == ProcurementFrameworkAgreementStatus.Published &&
                        item.Version < agreement.Version)
                    .OrderByDescending(item => item.Version)
                    .FirstOrDefaultAsync(cancellationToken);
                if (prior is not null)
                {
                    prior.Status = ProcurementFrameworkAgreementStatus.Superseded;
                    prior.SupersededByAgreementId = agreement.Id;
                    Touch(prior, "Superseded", correlation, now);
                    Capture(prior);
                    await Agreements.UpdateAsync(prior);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
            await RecordEventAsync(
                agreement, operation,
                approve
                    ? ProcurementControlEventResult.Succeeded
                    : ProcurementControlEventResult.Rejected,
                before, Snapshot(agreement), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
        }, cancellationToken, IsolationLevel.Serializable);
        await PublishNotificationAsync(
            approve
                ? "procurement.framework-agreement.published"
                : "procurement.framework-agreement.rejected",
            agreement, cancellationToken);
        return Map(await LoadAsync(agreement.Id, false, cancellationToken), now);
    }

    private IQueryable<ProcurementFrameworkAgreement> AgreementQuery() =>
        Agreements.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.BusinessPartner)
            .Include(item => item.Categories)
            .Include(item => item.PriceLines)
            .Include(item => item.CallOffAuthorities)
            .Include(item => item.Documents)
            .Include(item => item.Extensions)
            .Include(item => item.Balance);

    private async Task<ProcurementFrameworkAgreement> LoadAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = AgreementQuery();
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFound("FRAMEWORK_AGREEMENT_NOT_FOUND",
                "The framework agreement was not found in the current tenant.");
    }

    private async Task<IReadOnlyDictionary<Guid,
        ProcurementFrameworkCallOffCommercialRules.FamilySummary>>
        ResolveFamilySummariesAsync(
            IEnumerable<ProcurementFrameworkAgreement> agreements,
            DateTime atUtc,
            CancellationToken cancellationToken)
    {
        var familyKeys = agreements
            .Select(item => item.AgreementKey)
            .Distinct()
            .ToArray();
        if (familyKeys.Length == 0)
        {
            return new Dictionary<Guid,
                ProcurementFrameworkCallOffCommercialRules.FamilySummary>();
        }

        var revisions = await Agreements.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                familyKeys.Contains(item.AgreementKey) &&
                !item.IsDeleted)
            .Include(item => item.Extensions.Where(child => !child.IsDeleted))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var agreementIds = revisions.Select(item => item.Id).ToArray();
        var movements = await BalanceMovements.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                agreementIds.Contains(item.AgreementId) &&
                !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ProcurementFrameworkCallOffCommercialRules
            .SummarizeRevisionFamilies(
                revisions.Select(ToAgreementRevisionState),
                movements.Select(item =>
                    new ProcurementFrameworkCallOffCommercialRules.MovementState(
                        item.AgreementId,
                        item.MovementType,
                        item.Amount)),
                atUtc);
    }

    private async Task EnsureFamilyCommitmentCeilingAsync(
        ProcurementFrameworkAgreement agreement,
        CancellationToken cancellationToken)
    {
        var familyAgreementIds = await Agreements.GetQueryable(item =>
                item.TenantId == agreement.TenantId &&
                item.AgreementKey == agreement.AgreementKey &&
                !item.IsDeleted)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var movements = await BalanceMovements.GetQueryable(item =>
                item.TenantId == agreement.TenantId &&
                familyAgreementIds.Contains(item.AgreementId) &&
                !item.IsDeleted &&
                (item.MovementType ==
                     ProcurementFrameworkBalanceMovementType.Commitment ||
                 item.MovementType ==
                     ProcurementFrameworkBalanceMovementType.Release))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var committed = decimal.Round(
            Math.Max(
                0m,
                movements.Sum(item =>
                    item.MovementType ==
                    ProcurementFrameworkBalanceMovementType.Commitment
                        ? item.Amount
                        : -item.Amount)),
            2,
            MidpointRounding.AwayFromZero);
        if (decimal.Round(
                agreement.CeilingAmount,
                2,
                MidpointRounding.AwayFromZero) < committed)
        {
            throw Conflict(
                "FRAMEWORK_AGREEMENT_CEILING_BELOW_COMMITMENTS",
                $"The proposed ceiling cannot be below the agreement family's committed amount of {committed:0.00} {agreement.CurrencyCode}.");
        }
    }

    private static ProcurementFrameworkCallOffCommercialRules
        .AgreementRevisionState ToAgreementRevisionState(
            ProcurementFrameworkAgreement agreement) =>
        new(
            agreement.Id,
            agreement.AgreementKey,
            agreement.Version,
            agreement.CurrencyCode,
            agreement.CeilingAmount,
            agreement.EffectiveFromUtc,
            ProcurementFrameworkAgreementRules.EffectiveEnd(agreement),
            agreement.Status == ProcurementFrameworkAgreementStatus.Published);

    private async Task<SourceResolution> ResolveSourceAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        Guid businessPartnerId,
        CancellationToken cancellationToken)
    {
        if (sourceId == Guid.Empty || businessPartnerId == Guid.Empty)
            throw Validation("FRAMEWORK_AGREEMENT_SOURCE_REQUIRED",
                "An approved source and awarded supplier are required.");
        var supplier = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == businessPartnerId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("FRAMEWORK_AGREEMENT_SUPPLIER_NOT_FOUND",
                "The awarded supplier was not found in the current tenant.");
        var readiness = await _unitOfWork.Repository<ProcurementAwardReadinessDecision>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.SourceType == sourceType && item.SourceId == sourceId &&
                !item.IsDeleted)
            .AsNoTracking()
            .OrderByDescending(item => item.DecisionSequence)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw Conflict("FRAMEWORK_AGREEMENT_AWARD_READINESS_REQUIRED",
                "The source has no current Ready award-readiness decision.");
        if (readiness.Status != ProcurementAwardReadinessDecisionStatus.Ready)
        {
            throw Conflict("FRAMEWORK_AGREEMENT_AWARD_READINESS_REQUIRED",
                $"The latest source readiness decision is {readiness.Status}; a current Ready decision is required.");
        }
        if (!ParseIds(readiness.RecommendedBusinessPartnerIdsJson).Contains(businessPartnerId))
            throw Conflict("FRAMEWORK_AGREEMENT_SUPPLIER_NOT_RECOMMENDED",
                "The supplier is not in the exact approved recommendation lineage.");
        return sourceType switch
        {
            ProcurementAwardReadinessSourceType.RequestForQuotation =>
                await ResolveRfqSourceAsync(sourceId, supplier, readiness, cancellationToken),
            ProcurementAwardReadinessSourceType.Tender =>
                await ResolveTenderSourceAsync(sourceId, supplier, readiness, false, cancellationToken),
            ProcurementAwardReadinessSourceType.ExceptionalSourcing =>
                await ResolveTenderSourceAsync(sourceId, supplier, readiness, true, cancellationToken),
            _ => throw Validation("FRAMEWORK_AGREEMENT_SOURCE_TYPE_INVALID",
                "Unsupported framework source type.")
        };
    }

    private async Task<SourceResolution> ResolveRfqSourceAsync(
        Guid sourceId,
        BusinessPartner supplier,
        ProcurementAwardReadinessDecision readiness,
        CancellationToken cancellationToken)
    {
        var rfq = await _unitOfWork.Repository<RequestForQuotation>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == sourceId && !item.IsDeleted)
            .AsNoTracking()
            .Include(item => item.AwardLines)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("FRAMEWORK_AGREEMENT_RFQ_NOT_FOUND",
                "The RFQ source was not found in the current tenant.");
        var lines = rfq.AwardLines.Where(item =>
            !item.IsDeleted && item.BusinessPartnerId == supplier.Id).ToList();
        if (!string.Equals(rfq.Status, "Awarded", StringComparison.OrdinalIgnoreCase) ||
            !rfq.AwardedAt.HasValue || lines.Count == 0)
            throw Conflict("FRAMEWORK_AGREEMENT_RFQ_NOT_AWARDED",
                "The RFQ has no retained award for this supplier.");
        if (rfq.AwardedBusinessPartnerId.HasValue &&
            rfq.AwardedBusinessPartnerId != supplier.Id)
            throw Conflict("FRAMEWORK_AGREEMENT_RFQ_AWARD_CONTRADICTION",
                "The RFQ header contradicts the selected award-line supplier.");
        return new SourceResolution(
            ProcurementAwardReadinessSourceType.RequestForQuotation,
            rfq.Id, rfq.RfqNumber, supplier, readiness,
            lines.Sum(item => item.LineTotal), rfq.Currency.Trim().ToUpperInvariant(),
            EnsureUtc(rfq.AwardedAt.Value));
    }

    private async Task<SourceResolution> ResolveTenderSourceAsync(
        Guid sourceId,
        BusinessPartner supplier,
        ProcurementAwardReadinessDecision readiness,
        bool exceptional,
        CancellationToken cancellationToken)
    {
        var tender = await _unitOfWork.Repository<Tender>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == sourceId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("FRAMEWORK_AGREEMENT_TENDER_NOT_FOUND",
                "The tender source was not found in the current tenant.");
        Guid? bidId;
        DateTime? awardedAt;
        string? reference;
        decimal? exceptionalAmount = null;
        if (exceptional)
        {
            var control = await _unitOfWork.Repository<ProcurementExceptionalSourcingControl>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.TenderId == tender.Id && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("FRAMEWORK_AGREEMENT_EXCEPTIONAL_CONTROL_NOT_FOUND",
                    "The exceptional-sourcing control was not found.");
            if (control.Status < ProcurementExceptionalSourcingControlStatus.Awarded ||
                control.Status == ProcurementExceptionalSourcingControlStatus.Rejected ||
                !control.AwardBidId.HasValue || !control.AwardedAtUtc.HasValue)
                throw Conflict("FRAMEWORK_AGREEMENT_EXCEPTIONAL_SOURCE_NOT_AWARDED",
                    "The exceptional source has no retained approved award.");
            bidId = control.AwardBidId;
            awardedAt = control.AwardedAtUtc;
            reference = control.AwardReference;
            exceptionalAmount = control.NegotiatedAmount;
        }
        else
        {
            var control = await _unitOfWork.Repository<ProcurementTenderControl>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.TenderId == tender.Id && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("FRAMEWORK_AGREEMENT_TENDER_CONTROL_NOT_FOUND",
                    "The formal tender control was not found.");
            if (control.Status < ProcurementTenderControlStatus.Awarded ||
                control.Status == ProcurementTenderControlStatus.Rejected ||
                !control.AwardBidId.HasValue || !control.AwardedAtUtc.HasValue)
                throw Conflict("FRAMEWORK_AGREEMENT_TENDER_NOT_AWARDED",
                    "The tender has no retained approved award.");
            bidId = control.AwardBidId;
            awardedAt = control.AwardedAtUtc;
            reference = control.AwardReference;
        }
        var bid = await _unitOfWork.Repository<TenderBid>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == bidId!.Value && item.TenderId == tender.Id &&
                !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Conflict("FRAMEWORK_AGREEMENT_AWARD_BID_NOT_FOUND",
                "The retained award bid is unavailable.");
        if (bid.BusinessPartnerId != supplier.Id)
            throw Conflict("FRAMEWORK_AGREEMENT_AWARD_SUPPLIER_MISMATCH",
                "The selected supplier does not own the retained award bid.");
        var award = await _unitOfWork.Repository<TenderAward>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.TenderId == tender.Id &&
                item.BusinessPartnerId == supplier.Id &&
                item.TenderBidId == bid.Id && !item.IsDeleted &&
                item.Status != "Cancelled")
            .AsNoTracking()
            .OrderByDescending(item => item.AwardDate)
            .FirstOrDefaultAsync(cancellationToken);
        var amount = exceptionalAmount ?? award?.AwardedAmount ?? bid.TotalBidAmount;
        return new SourceResolution(
            exceptional
                ? ProcurementAwardReadinessSourceType.ExceptionalSourcing
                : ProcurementAwardReadinessSourceType.Tender,
            tender.Id,
            string.IsNullOrWhiteSpace(reference) ? tender.TenderNumber : reference.Trim(),
            supplier, readiness, amount,
            (award?.Currency ?? tender.Currency ?? "GHS").Trim().ToUpperInvariant(),
            EnsureUtc(awardedAt!.Value));
    }

    private async Task<SupplierValidationResult> EnforceSupplierAsync(
        Guid businessPartnerId,
        List<Guid> categoryIds,
        SourceResolution source,
        string correlationId,
        CancellationToken cancellationToken) =>
        await _supplierValidation.EnforceEligibilityAsync(
            new SupplierEligibilityEvaluationRequest
            {
                BusinessPartnerId = businessPartnerId,
                Boundary = SupplierEligibilityBoundary.Contract,
                CategoryIds = categoryIds.Distinct().ToList(),
                RequiresLicenses = false,
                IncludeFinancialWarnings = true,
                RecordAudit = true,
                SourceType = SourceTypeName,
                SourceId = source.SourceId,
                SourceReference = source.SourceReference,
                CorrelationId = correlationId
            }, cancellationToken);

    private async Task<WorkflowDefinition> ValidateWorkflowAsync(
        Guid workflowDefinitionId,
        CancellationToken cancellationToken)
    {
        if (workflowDefinitionId == Guid.Empty)
            throw Validation("FRAMEWORK_AGREEMENT_WORKFLOW_REQUIRED",
                "A Published shared workflow definition is required.");
        return await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == workflowDefinitionId && !item.IsDeleted &&
                item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("FRAMEWORK_AGREEMENT_WORKFLOW_INVALID",
                "The selected shared workflow is unavailable or not Published.");
    }

    private async Task ValidateCurrencyAsync(
        string currencyCode,
        CancellationToken cancellationToken)
    {
        var code = currencyCode.Trim().ToUpperInvariant();
        if (!await _unitOfWork.Repository<Currency>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.CurrencyCode == code && item.IsActive && !item.IsDeleted)
                .AnyAsync(cancellationToken))
            throw Validation("FRAMEWORK_AGREEMENT_CURRENCY_INVALID",
                "The framework currency is not active in Finance for this tenant.");
    }

    private static void EnsureSourceCurrency(
        SourceResolution source,
        string currencyCode)
    {
        var normalizedCurrency = currencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(
                normalizedCurrency,
                source.CurrencyCode,
                StringComparison.Ordinal))
        {
            throw Conflict(
                "FRAMEWORK_AGREEMENT_CURRENCY_SOURCE_MISMATCH",
                $"The framework currency must match the approved award currency {source.CurrencyCode}.");
        }
    }

    private static void EnsureSourceCeiling(
        SourceResolution source,
        decimal ceilingAmount)
    {
        var normalizedCeiling = RoundMoney(ceilingAmount);
        var authorizedAmount = RoundMoney(source.AwardAmount);
        if (normalizedCeiling > authorizedAmount)
        {
            throw Conflict(
                "FRAMEWORK_AGREEMENT_CEILING_EXCEEDS_AWARD",
                $"The framework ceiling {normalizedCeiling:0.00} {source.CurrencyCode} exceeds the approved award amount {authorizedAmount:0.00} {source.CurrencyCode}.");
        }
    }

    private async Task<List<CategoryInput>> ResolveCategoriesAsync(
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        var ids = categoryIds.Where(item => item != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            throw Validation("FRAMEWORK_AGREEMENT_CATEGORY_REQUIRED",
                "At least one supplier category is required.");
        var rows = await _unitOfWork.Repository<PartnerCategory>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                ids.Contains(item.Id) && !item.IsDeleted && item.IsActive)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (rows.Count != ids.Count)
            throw Validation("FRAMEWORK_AGREEMENT_CATEGORY_INVALID",
                "Every framework category must be active in the current tenant.");
        return rows.Select(item =>
            new CategoryInput(item.Id, item.CategoryCode, item.CategoryName)).ToList();
    }

    private async Task<List<PriceInput>> ResolvePriceLinesAsync(
        IReadOnlyCollection<SaveProcurementFrameworkPriceListLineRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            throw Validation("FRAMEWORK_AGREEMENT_PRICE_LINE_REQUIRED",
                "At least one governed price-list line is required.");
        var ids = requests.Select(item => item.InventoryItemId)
            .Where(item => item != Guid.Empty).Distinct().ToList();
        if (ids.Count != requests.Count)
            throw Validation("FRAMEWORK_AGREEMENT_PRICE_ITEM_DUPLICATE",
                "Each active inventory item can appear only once in a price-list revision.");
        var items = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                ids.Contains(item.Id) && !item.IsDeleted &&
                item.Status == ItemStatus.Active)
            .AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
        if (items.Count != ids.Count)
            throw Validation("FRAMEWORK_AGREEMENT_PRICE_ITEM_INVALID",
                "Every governed price line requires an active tenant inventory item.");
        var result = new List<PriceInput>();
        foreach (var request in requests)
        {
            if (request.UnitPrice <= 0 || request.MinimumQuantity <= 0 ||
                request.MaximumQuantity.HasValue &&
                request.MaximumQuantity.Value < request.MinimumQuantity)
                throw Validation("FRAMEWORK_AGREEMENT_PRICE_LINE_INVALID",
                    "Price, minimum quantity, and optional maximum quantity are invalid.");
            var item = items[request.InventoryItemId];
            result.Add(new PriceInput(
                item.Id, item.ItemCode, item.Name, item.UnitOfMeasure,
                decimal.Round(request.UnitPrice, 4, MidpointRounding.AwayFromZero),
                decimal.Round(request.MinimumQuantity, 4, MidpointRounding.AwayFromZero),
                request.MaximumQuantity.HasValue
                    ? decimal.Round(request.MaximumQuantity.Value, 4, MidpointRounding.AwayFromZero)
                    : null,
                request.LeadTimeDays, Trim(request.Specifications, 500)));
        }
        return result;
    }

    private static List<AuthorityInput> ResolveAuthorities(
        IReadOnlyCollection<SaveProcurementFrameworkCallOffAuthorityRequest> requests,
        DateTime agreementStart,
        DateTime agreementEnd)
    {
        if (requests.Count == 0)
            throw Validation("FRAMEWORK_AGREEMENT_AUTHORITY_REQUIRED",
                "At least one call-off authority configuration is required.");
        var start = EnsureUtc(agreementStart);
        var end = EnsureUtc(agreementEnd);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<AuthorityInput>();
        foreach (var request in requests)
        {
            var value = Required(request.AuthorityValue, 200);
            var validFrom = EnsureUtc(request.ValidFromUtc);
            var validTo = EnsureUtc(request.ValidToUtc);
            if (validFrom < start || validTo > end || validTo <= validFrom)
                throw Validation("FRAMEWORK_AGREEMENT_AUTHORITY_PERIOD_INVALID",
                    "Call-off authority validity must fall within the agreement period.");
            if (request.MaximumCallOffAmount.HasValue &&
                request.MaximumCallOffAmount.Value <= 0)
                throw Validation("FRAMEWORK_AGREEMENT_AUTHORITY_LIMIT_INVALID",
                    "A call-off authority limit must be greater than zero.");
            if (request.AuthorityKind == ProcurementFrameworkAuthorityKind.User &&
                (!request.AuthorityUserId.HasValue || request.AuthorityUserId == Guid.Empty))
                throw Validation("FRAMEWORK_AGREEMENT_AUTHORITY_USER_REQUIRED",
                    "A user authority requires the server identifier of that user.");
            if (request.AuthorityKind != ProcurementFrameworkAuthorityKind.User &&
                request.AuthorityUserId.HasValue)
                throw Validation("FRAMEWORK_AGREEMENT_AUTHORITY_USER_INVALID",
                    "Only a User authority can carry a user identifier.");
            if (!seen.Add($"{request.AuthorityKind}:{value}"))
                throw Validation("FRAMEWORK_AGREEMENT_AUTHORITY_DUPLICATE",
                    "Duplicate call-off authority entries are not allowed.");
            result.Add(new AuthorityInput(
                request.AuthorityKind, request.AuthorityUserId, value,
                Required(request.DisplayName, 300),
                request.MaximumCallOffAmount.HasValue
                    ? RoundMoney(request.MaximumCallOffAmount.Value)
                    : null,
                validFrom, validTo, request.IsActive));
        }
        return result;
    }

    private async Task ValidateAuthorityUsersAsync(
        IReadOnlyCollection<AuthorityInput> authorities,
        CancellationToken cancellationToken)
    {
        var userIds = authorities
            .Where(item => item.Kind == ProcurementFrameworkAuthorityKind.User)
            .Select(item => item.UserId!.Value)
            .Distinct()
            .ToArray();
        if (userIds.Length == 0) return;

        var now = DateTime.UtcNow;
        var activeUserIds = await _unitOfWork.Repository<UserTenant>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                userIds.Contains(item.UserId) &&
                !item.IsDeleted &&
                item.Status == UserTenantStatus.Active &&
                (!item.ExpiresAt.HasValue || item.ExpiresAt > now))
            .Select(item => item.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (activeUserIds.Count != userIds.Length)
            throw Validation(
                "FRAMEWORK_AGREEMENT_AUTHORITY_USER_INVALID",
                "Every user authority must have active access to the current tenant.");
    }

    private static void AddChildren(
        ProcurementFrameworkAgreement agreement,
        IEnumerable<CategoryInput> categories,
        IEnumerable<PriceInput> prices,
        IEnumerable<AuthorityInput> authorities,
        DateTime now)
    {
        foreach (var input in categories)
        {
            var item = new ProcurementFrameworkAgreementCategory
            {
                TenantId = agreement.TenantId,
                AgreementId = agreement.Id,
                PartnerCategoryId = input.Id,
                CategoryCode = input.Code,
                CategoryName = input.Name,
                CreatedAt = now,
                CreatedBy = agreement.CreatedBy,
                CreatedById = agreement.CreatedById
            };
            Capture(item);
            agreement.Categories.Add(item);
        }
        foreach (var input in prices)
        {
            var item = new ProcurementFrameworkPriceListLine
            {
                TenantId = agreement.TenantId,
                AgreementId = agreement.Id,
                InventoryItemId = input.Id,
                ItemCode = input.Code,
                ItemName = input.Name,
                UnitOfMeasure = input.UnitOfMeasure,
                UnitPrice = input.UnitPrice,
                MinimumQuantity = input.MinimumQuantity,
                MaximumQuantity = input.MaximumQuantity,
                LeadTimeDays = input.LeadTimeDays,
                Specifications = input.Specifications,
                CreatedAt = now,
                CreatedBy = agreement.CreatedBy,
                CreatedById = agreement.CreatedById
            };
            Capture(item);
            agreement.PriceLines.Add(item);
        }
        foreach (var input in authorities)
        {
            var item = new ProcurementFrameworkCallOffAuthority
            {
                TenantId = agreement.TenantId,
                AgreementId = agreement.Id,
                AuthorityKind = input.Kind,
                AuthorityUserId = input.UserId,
                AuthorityValue = input.Value,
                DisplayName = input.DisplayName,
                MaximumCallOffAmount = input.MaximumAmount,
                ValidFromUtc = input.ValidFromUtc,
                ValidToUtc = input.ValidToUtc,
                IsActive = input.IsActive,
                CreatedAt = now,
                CreatedBy = agreement.CreatedBy,
                CreatedById = agreement.CreatedById
            };
            Capture(item);
            agreement.CallOffAuthorities.Add(item);
        }
    }

    private static void RetireDraftChildren(
        ProcurementFrameworkAgreement agreement,
        DateTime now)
    {
        foreach (var item in agreement.Categories.Where(item => !item.IsDeleted))
            Retire(item, now);
        foreach (var item in agreement.PriceLines.Where(item => !item.IsDeleted))
            Retire(item, now);
        foreach (var item in agreement.CallOffAuthorities.Where(item => !item.IsDeleted))
            Retire(item, now);
    }

    private static void Retire(BaseEntity item, DateTime now)
    {
        item.IsDeleted = true;
        item.DeletedAt = now;
        item.DeletedBy = "Draft revised";
        item.UpdatedAt = now;
    }

    private static ProcurementFrameworkAgreementCategory Clone(
        ProcurementFrameworkAgreementCategory source,
        ProcurementFrameworkAgreement agreement,
        DateTime now)
    {
        var clone = new ProcurementFrameworkAgreementCategory
        {
            TenantId = agreement.TenantId,
            AgreementId = agreement.Id,
            PartnerCategoryId = source.PartnerCategoryId,
            CategoryCode = source.CategoryCode,
            CategoryName = source.CategoryName,
            CreatedAt = now,
            CreatedBy = agreement.CreatedBy,
            CreatedById = agreement.CreatedById
        };
        Capture(clone);
        return clone;
    }

    private static ProcurementFrameworkPriceListLine Clone(
        ProcurementFrameworkPriceListLine source,
        ProcurementFrameworkAgreement agreement,
        DateTime now)
    {
        var clone = new ProcurementFrameworkPriceListLine
        {
            TenantId = agreement.TenantId,
            AgreementId = agreement.Id,
            InventoryItemId = source.InventoryItemId,
            ItemCode = source.ItemCode,
            ItemName = source.ItemName,
            UnitOfMeasure = source.UnitOfMeasure,
            UnitPrice = source.UnitPrice,
            MinimumQuantity = source.MinimumQuantity,
            MaximumQuantity = source.MaximumQuantity,
            LeadTimeDays = source.LeadTimeDays,
            Specifications = source.Specifications,
            CreatedAt = now,
            CreatedBy = agreement.CreatedBy,
            CreatedById = agreement.CreatedById
        };
        Capture(clone);
        return clone;
    }

    private static ProcurementFrameworkCallOffAuthority Clone(
        ProcurementFrameworkCallOffAuthority source,
        ProcurementFrameworkAgreement agreement,
        DateTime now)
    {
        var clone = new ProcurementFrameworkCallOffAuthority
        {
            TenantId = agreement.TenantId,
            AgreementId = agreement.Id,
            AuthorityKind = source.AuthorityKind,
            AuthorityUserId = source.AuthorityUserId,
            AuthorityValue = source.AuthorityValue,
            DisplayName = source.DisplayName,
            MaximumCallOffAmount = source.MaximumCallOffAmount,
            ValidFromUtc = agreement.EffectiveFromUtc,
            ValidToUtc = agreement.EffectiveToUtc,
            IsActive = source.IsActive,
            CreatedAt = now,
            CreatedBy = agreement.CreatedBy,
            CreatedById = agreement.CreatedById
        };
        Capture(clone);
        return clone;
    }

    private static void EnsureCompleteDraft(ProcurementFrameworkAgreement agreement)
    {
        if (!agreement.Categories.Any(item => !item.IsDeleted))
            throw Validation("FRAMEWORK_AGREEMENT_CATEGORY_REQUIRED",
                "At least one supplier category is required.");
        if (!agreement.PriceLines.Any(item => !item.IsDeleted))
            throw Validation("FRAMEWORK_AGREEMENT_PRICE_LINE_REQUIRED",
                "At least one governed price-list line is required.");
        if (!agreement.CallOffAuthorities.Any(item => !item.IsDeleted && item.IsActive))
            throw Validation("FRAMEWORK_AGREEMENT_AUTHORITY_REQUIRED",
                "At least one active call-off authority is required.");
        if (!agreement.Documents.Any(item => !item.IsDeleted && item.IsCurrent))
            throw Validation("FRAMEWORK_AGREEMENT_DOCUMENT_REQUIRED",
                "At least one current central-DMS agreement document is required.");
        if (!agreement.Documents.Any(item =>
                !item.IsDeleted && item.IsCurrent && item.IsRequired))
            throw Validation("FRAMEWORK_AGREEMENT_REQUIRED_DOCUMENT_MISSING",
                "At least one current document must be classified as required.");
    }

    private static void ValidateCommercialRequest(
        string title,
        decimal ceiling,
        string currency,
        DateTime start,
        DateTime end,
        IReadOnlyCollection<Guid> categories,
        IReadOnlyCollection<SaveProcurementFrameworkPriceListLineRequest> prices,
        IReadOnlyCollection<SaveProcurementFrameworkCallOffAuthorityRequest> authorities)
    {
        Required(title, 200);
        if (ceiling <= 0)
            throw Validation("FRAMEWORK_AGREEMENT_CEILING_INVALID",
                "The framework ceiling must be greater than zero.");
        var code = Required(currency, 3).ToUpperInvariant();
        if (code.Length != 3 || !code.All(char.IsLetter))
            throw Validation("FRAMEWORK_AGREEMENT_CURRENCY_INVALID",
                "A three-letter ISO currency code is required.");
        ValidateDates(start, end);
        if (categories.Count == 0 || prices.Count == 0 || authorities.Count == 0)
            throw Validation("FRAMEWORK_AGREEMENT_CONTENT_INCOMPLETE",
                "Categories, governed price lines, and call-off authorities are required.");
    }

    private static void ValidateDates(DateTime start, DateTime end)
    {
        if (EnsureUtc(end) <= EnsureUtc(start))
            throw Validation("FRAMEWORK_AGREEMENT_PERIOD_INVALID",
                "The agreement end must be later than its effective start.");
    }

    private async Task EnsureWorkflowOutcomeAsync(
        Guid workflowDefinitionId,
        Guid? workflowInstanceId,
        Guid entityId,
        bool approving,
        CancellationToken cancellationToken)
    {
        if (!workflowInstanceId.HasValue)
            throw Conflict("FRAMEWORK_AGREEMENT_WORKFLOW_INSTANCE_REQUIRED",
                "The shared workflow instance was not recorded.");
        var instance = await WorkflowInstances.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == workflowInstanceId.Value && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Conflict("FRAMEWORK_AGREEMENT_WORKFLOW_INSTANCE_NOT_FOUND",
                "The shared workflow instance is unavailable.");
        if (instance.WorkflowDefinitionId != workflowDefinitionId ||
            instance.EntityId != entityId)
            throw Conflict("FRAMEWORK_AGREEMENT_WORKFLOW_BINDING_INVALID",
                "The workflow instance does not belong to this framework decision.");
        if (approving && instance.Status != WorkflowInstanceStatus.Completed)
            throw Conflict("FRAMEWORK_AGREEMENT_WORKFLOW_NOT_APPROVED",
                "Only a Completed shared workflow permits approval.");
        if (!approving &&
            instance.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            throw Conflict("FRAMEWORK_AGREEMENT_WORKFLOW_NOT_REJECTED",
                "Only a Cancelled or Failed shared workflow permits rejection.");
    }

    private async Task EnsureIndependentActorAsync(
        Guid? prohibitedActor,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!prohibitedActor.HasValue || prohibitedActor == Guid.Empty)
            throw Conflict("FRAMEWORK_AGREEMENT_INITIATOR_NOT_RECORDED",
                "The submitting actor was not recorded.");
        var decision = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = SourceTypeName,
            SourceReference = sourceReference,
            ProhibitedActorUserIds = [prohibitedActor.Value]
        }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementFrameworkAgreementAuthorizationException(decision.Message);
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementFrameworkAgreementAuthorizationException(
                "External portal users cannot administer framework agreements.");
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                SourceType = SourceTypeName,
                SourceReference = sourceReference
            }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementFrameworkAgreementAuthorizationException(decision.Message);
    }

    private void EnsureInternalReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementFrameworkAgreementAuthorizationException(
                "External portal users cannot access framework administration.");
        if (IsAdministrator() ||
            _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role =>
                ProcurementAccessControlRegistry.FindRole(role) is not null))
            return;
        throw new ProcurementFrameworkAgreementAuthorizationException(
            "A TDC procurement or internal-audit role is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw new ProcurementFrameworkAgreementAuthorizationException(
                "An authenticated tenant context is required.");
    }

    private bool IsAdministrator() =>
        _currentUser.HasRole("Admin") ||
        _currentUser.HasRole("Administrator") ||
        _currentUser.HasRole("SuperAdmin") ||
        _currentUser.HasRole("TenantAdmin");

    private async Task RecordEventAsync(
        ProcurementFrameworkAgreement agreement,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlationId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(BuildEvent(
            agreement, action, result, before, after, reason,
            evidence, correlationId, occurredAtUtc), cancellationToken);

    private static ProcurementControlEventWriteRequest BuildEvent(
        ProcurementFrameworkAgreement agreement,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlationId,
        DateTime occurredAtUtc) => new()
    {
        EventKey = ProcurementControlEventKey.Create(
            "framework-agreement", agreement.TenantId, agreement.Id,
            $"{action}-{correlationId}"),
        EventType = EventType,
        Action = action,
        Result = result,
        RuleCode = agreement.SourceType.ToString(),
        RuleId = agreement.AwardReadinessDecisionId,
        RuleVersion = agreement.SourceIntegrityHash,
        DecisionKeys = DecisionKeys.ToList(),
        SourceType = SourceTypeName,
        SourceId = agreement.Id,
        SourceReference = $"{agreement.AgreementNumber}/v{agreement.Version}",
        Reason = Trim(reason, 1000),
        InputValues = new
        {
            agreement.BusinessPartnerId,
            agreement.SourceType,
            agreement.SourceId,
            agreement.SourceReference,
            agreement.AwardReadinessDecisionId,
            agreement.WorkflowDefinitionId
        },
        ResultValues = new
        {
            agreement.Status,
            agreement.CeilingAmount,
            agreement.CurrencyCode,
            agreement.EffectiveFromUtc,
            EffectiveEndUtc = ProcurementFrameworkAgreementRules.EffectiveEnd(agreement),
            agreement.PriceListReference,
            agreement.PriceListVersion,
            agreement.SupplierEligibilityDecisionHash,
            agreement.IntegrityHash
        },
        Before = before,
        After = after,
        CorrelationId = correlationId,
        CausationId = correlationId,
        OccurredAtUtc = occurredAtUtc,
        Evidence = evidence.Select(item => new ProcurementControlEventEvidenceReference
        {
            ReferenceKind = item.ReferenceKind,
            ReferenceId = item.ReferenceId,
            Reference = item.Reference,
            Label = item.Label,
            RequirementKey = item.RequirementKey
        }).ToList()
    };

    private async Task PublishNotificationAsync(
        string topic,
        ProcurementFrameworkAgreement agreement,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationTopics.PublishAsync(new NotificationTopicEvent
            {
                TenantId = agreement.TenantId,
                TopicKey = topic,
                NotificationType = "ProcurementFrameworkAgreementControl",
                EntityType = SourceTypeName,
                EntityId = agreement.Id,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["agreementNumber"] = agreement.AgreementNumber,
                    ["version"] = agreement.Version,
                    ["supplierId"] = agreement.BusinessPartnerId,
                    ["status"] = agreement.Status.ToString(),
                    ["ceilingAmount"] = agreement.CeilingAmount,
                    ["currencyCode"] = agreement.CurrencyCode,
                    ["effectiveFromUtc"] = agreement.EffectiveFromUtc,
                    ["effectiveEndUtc"] = ProcurementFrameworkAgreementRules.EffectiveEnd(agreement)
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish framework notification {Topic} for {AgreementId}",
                topic, agreement.Id);
        }
    }

    private static ProcurementFrameworkAgreementListItemDto MapList(
        ProcurementFrameworkAgreement item,
        DateTime now,
        ProcurementFrameworkCallOffCommercialRules.FamilySummary?
            familySummary = null)
    {
        var end = ProcurementFrameworkAgreementRules.EffectiveEnd(item);
        return new ProcurementFrameworkAgreementListItemDto
        {
            Id = item.Id,
            AgreementKey = item.AgreementKey,
            AgreementNumber = item.AgreementNumber,
            Title = item.Title,
            Version = item.Version,
            Status = item.Status,
            BusinessPartnerId = item.BusinessPartnerId,
            SupplierCode = item.BusinessPartner?.PartnerCode ?? string.Empty,
            SupplierName = item.BusinessPartner?.PartnerName ?? string.Empty,
            SourceType = item.SourceType,
            SourceId = item.SourceId,
            SourceReference = item.SourceReference,
            PriceListReference = item.PriceListReference,
            PriceListVersion = item.PriceListVersion,
            CeilingAmount = item.CeilingAmount,
            AvailableCeiling = familySummary is not null
                ? ProcurementFrameworkCallOffCommercialRules
                    .EvaluateFamilyCapacity(
                        familySummary.CeilingAmount,
                        familySummary.CommittedAmount,
                        0m)
                    .AvailableAmount
                : item.Balance?.AvailableAmount ?? item.CeilingAmount,
            CurrencyCode = item.CurrencyCode,
            EffectiveFromUtc = item.EffectiveFromUtc,
            EffectiveToUtc = item.EffectiveToUtc,
            EffectiveEndUtc = end,
            IsEffective = ProcurementFrameworkAgreementRules.IsEffective(item, now),
            CategoryCount = item.Categories.Count(child => !child.IsDeleted),
            PriceLineCount = item.PriceLines.Count(child => !child.IsDeleted),
            AuthorityCount = item.CallOffAuthorities.Count(child => !child.IsDeleted && child.IsActive),
            DocumentCount = item.Documents.Count(child =>
                !child.IsDeleted && child.IsCurrent),
            AllowedActions = ProcurementFrameworkAgreementRules.AllowedActions(item),
            RowVersion = Convert.ToBase64String(item.RowVersion ?? Array.Empty<byte>())
        };
    }

    private static ProcurementFrameworkAgreementDto Map(
        ProcurementFrameworkAgreement item,
        DateTime now,
        ProcurementFrameworkCallOffCommercialRules.FamilySummary?
            familySummary = null)
    {
        var list = MapList(item, now, familySummary);
        return new ProcurementFrameworkAgreementDto
        {
            Id = list.Id,
            AgreementKey = list.AgreementKey,
            AgreementNumber = list.AgreementNumber,
            Title = list.Title,
            Version = list.Version,
            Status = list.Status,
            BusinessPartnerId = list.BusinessPartnerId,
            SupplierCode = list.SupplierCode,
            SupplierName = list.SupplierName,
            SourceType = list.SourceType,
            SourceId = list.SourceId,
            SourceReference = list.SourceReference,
            PriceListReference = list.PriceListReference,
            PriceListVersion = list.PriceListVersion,
            CeilingAmount = list.CeilingAmount,
            AvailableCeiling = list.AvailableCeiling,
            CurrencyCode = list.CurrencyCode,
            EffectiveFromUtc = list.EffectiveFromUtc,
            EffectiveToUtc = list.EffectiveToUtc,
            EffectiveEndUtc = list.EffectiveEndUtc,
            IsEffective = list.IsEffective,
            CategoryCount = list.CategoryCount,
            PriceLineCount = list.PriceLineCount,
            AuthorityCount = list.AuthorityCount,
            DocumentCount = list.DocumentCount,
            AllowedActions = list.AllowedActions,
            RowVersion = list.RowVersion,
            AwardReadinessDecisionId = item.AwardReadinessDecisionId,
            SourceIntegrityHash = item.SourceIntegrityHash,
            SupplierEligibilityDecisionHash = item.SupplierEligibilityDecisionHash,
            WorkflowDefinitionId = item.WorkflowDefinitionId,
            WorkflowInstanceId = item.WorkflowInstanceId,
            SupersedesAgreementId = item.SupersedesAgreementId,
            SupersededByAgreementId = item.SupersededByAgreementId,
            Description = item.Description,
            TermsSummary = item.TermsSummary,
            ReviewComment = item.ReviewComment,
            SubmittedById = item.SubmittedById,
            SubmittedByName = item.SubmittedByName,
            SubmittedAtUtc = item.SubmittedAtUtc,
            PublishedById = item.PublishedById,
            PublishedByName = item.PublishedByName,
            PublishedAtUtc = item.PublishedAtUtc,
            RejectedById = item.RejectedById,
            RejectedByName = item.RejectedByName,
            RejectedAtUtc = item.RejectedAtUtc,
            TerminatedById = item.TerminatedById,
            TerminatedAtUtc = item.TerminatedAtUtc,
            TerminationReason = item.TerminationReason,
            IntegrityHash = item.IntegrityHash,
            Categories = item.Categories.Where(child => !child.IsDeleted)
                .OrderBy(child => child.CategoryCode)
                .Select(Map).ToList(),
            PriceLines = item.PriceLines.Where(child => !child.IsDeleted)
                .OrderBy(child => child.ItemCode)
                .Select(Map).ToList(),
            CallOffAuthorities = item.CallOffAuthorities.Where(child => !child.IsDeleted)
                .OrderBy(child => child.AuthorityKind)
                .ThenBy(child => child.DisplayName)
                .Select(Map).ToList(),
            Documents = item.Documents.Where(child => !child.IsDeleted)
                .OrderByDescending(child => child.IsCurrent)
                .ThenBy(child => child.DocumentType)
                .Select(Map).ToList(),
            Extensions = item.Extensions.Where(child => !child.IsDeleted)
                .OrderByDescending(child => child.SequenceNumber)
                .Select(Map).ToList()
        };
    }

    private static ProcurementFrameworkAgreementCategoryDto Map(
        ProcurementFrameworkAgreementCategory item) => new()
    {
        Id = item.Id,
        PartnerCategoryId = item.PartnerCategoryId,
        CategoryCode = item.CategoryCode,
        CategoryName = item.CategoryName,
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementFrameworkPriceListLineDto Map(
        ProcurementFrameworkPriceListLine item) => new()
    {
        Id = item.Id,
        InventoryItemId = item.InventoryItemId,
        ItemCode = item.ItemCode,
        ItemName = item.ItemName,
        UnitOfMeasure = item.UnitOfMeasure,
        UnitPrice = item.UnitPrice,
        MinimumQuantity = item.MinimumQuantity,
        MaximumQuantity = item.MaximumQuantity,
        LeadTimeDays = item.LeadTimeDays,
        Specifications = item.Specifications,
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementFrameworkCallOffAuthorityDto Map(
        ProcurementFrameworkCallOffAuthority item) => new()
    {
        Id = item.Id,
        AuthorityKind = item.AuthorityKind,
        AuthorityUserId = item.AuthorityUserId,
        AuthorityValue = item.AuthorityValue,
        DisplayName = item.DisplayName,
        MaximumCallOffAmount = item.MaximumCallOffAmount,
        ValidFromUtc = item.ValidFromUtc,
        ValidToUtc = item.ValidToUtc,
        IsActive = item.IsActive,
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementFrameworkAgreementDocumentDto Map(
        ProcurementFrameworkAgreementDocument item) => new()
    {
        Id = item.Id,
        DocumentType = item.DocumentType,
        Title = item.Title,
        FileUploadRecordId = item.FileUploadRecordId,
        CentralDocumentRecordId = item.CentralDocumentRecordId,
        CentralDocumentVersionId = item.CentralDocumentVersionId,
        DmsReference = item.DmsReference,
        IsRequired = item.IsRequired,
        IsCurrent = item.IsCurrent,
        RetiredAtUtc = item.RetiredAtUtc,
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementFrameworkAgreementExtensionDto Map(
        ProcurementFrameworkAgreementExtension item) => new()
    {
        Id = item.Id,
        SequenceNumber = item.SequenceNumber,
        Status = item.Status,
        PreviousEndUtc = item.PreviousEndUtc,
        ProposedEndUtc = item.ProposedEndUtc,
        Reason = item.Reason,
        WorkflowDefinitionId = item.WorkflowDefinitionId,
        WorkflowInstanceId = item.WorkflowInstanceId,
        SubmittedById = item.SubmittedById,
        SubmittedByName = item.SubmittedByName,
        SubmittedAtUtc = item.SubmittedAtUtc,
        DecidedById = item.DecidedById,
        DecidedByName = item.DecidedByName,
        DecidedAtUtc = item.DecidedAtUtc,
        DecisionComment = item.DecisionComment,
        IntegrityHash = item.IntegrityHash,
        RowVersion = Convert.ToBase64String(item.RowVersion ?? Array.Empty<byte>())
    };

    private static object Snapshot(ProcurementFrameworkAgreement item) => new
    {
        item.Id,
        item.TenantId,
        item.AgreementKey,
        item.AgreementNumber,
        item.Title,
        item.Version,
        item.Status,
        item.BusinessPartnerId,
        item.SourceType,
        item.SourceId,
        item.SourceReference,
        item.AwardReadinessDecisionId,
        item.SourceIntegrityHash,
        item.SupplierEligibilityDecisionHash,
        item.PriceListReference,
        item.PriceListVersion,
        item.CeilingAmount,
        item.CurrencyCode,
        item.EffectiveFromUtc,
        item.EffectiveToUtc,
        EffectiveEndUtc = ProcurementFrameworkAgreementRules.EffectiveEnd(item),
        item.WorkflowDefinitionId,
        item.WorkflowInstanceId,
        item.SupersedesAgreementId,
        item.SupersededByAgreementId,
        Categories = item.Categories.Where(child => !child.IsDeleted)
            .OrderBy(child => child.CategoryCode)
            .Select(child => new
            {
                child.PartnerCategoryId,
                child.CategoryCode,
                child.CategoryName,
                child.IntegrityHash
            }),
        PriceLines = item.PriceLines.Where(child => !child.IsDeleted)
            .OrderBy(child => child.ItemCode)
            .Select(child => new
            {
                child.InventoryItemId,
                child.ItemCode,
                child.ItemName,
                child.UnitOfMeasure,
                child.UnitPrice,
                child.MinimumQuantity,
                child.MaximumQuantity,
                child.LeadTimeDays,
                child.IntegrityHash
            }),
        Authorities = item.CallOffAuthorities.Where(child => !child.IsDeleted)
            .OrderBy(child => child.AuthorityKind)
            .ThenBy(child => child.AuthorityValue)
            .Select(child => new
            {
                child.AuthorityKind,
                child.AuthorityUserId,
                child.AuthorityValue,
                child.MaximumCallOffAmount,
                child.ValidFromUtc,
                child.ValidToUtc,
                child.IsActive,
                child.IntegrityHash
            }),
        Documents = item.Documents.Where(child => !child.IsDeleted)
            .OrderBy(child => child.DocumentType)
            .Select(child => new
            {
                child.DocumentType,
                child.CentralDocumentRecordId,
                child.CentralDocumentVersionId,
                child.DmsReference,
                child.IsRequired,
                child.IsCurrent,
                child.IntegrityHash
            }),
        Extensions = item.Extensions.Where(child => !child.IsDeleted)
            .OrderBy(child => child.SequenceNumber)
            .Select(child => new
            {
                child.SequenceNumber,
                child.Status,
                child.PreviousEndUtc,
                child.ProposedEndUtc,
                child.IntegrityHash
            }),
        item.LastOperation,
        item.LastOperationCorrelationId
    };

    private static object Snapshot(ProcurementFrameworkAgreementExtension item) => new
    {
        item.Id,
        item.AgreementId,
        item.SequenceNumber,
        item.Status,
        item.PreviousEndUtc,
        item.ProposedEndUtc,
        item.Reason,
        item.WorkflowDefinitionId,
        item.WorkflowInstanceId,
        item.SubmittedById,
        item.SubmittedAtUtc,
        item.DecidedById,
        item.DecidedAtUtc,
        item.CorrelationId,
        item.IntegrityHash
    };

    private static void Capture(ProcurementFrameworkAgreement item)
    {
        item.SnapshotJson = Serialize(Snapshot(item));
        item.IntegrityHash = Hash(item.SnapshotJson);
    }

    private static void Capture(ProcurementFrameworkAgreementCategory item) =>
        item.IntegrityHash = Hash(Serialize(new
        {
            item.TenantId,
            item.AgreementId,
            item.PartnerCategoryId,
            item.CategoryCode,
            item.CategoryName
        }));

    private static void Capture(ProcurementFrameworkPriceListLine item) =>
        item.IntegrityHash = Hash(Serialize(new
        {
            item.TenantId,
            item.AgreementId,
            item.InventoryItemId,
            item.ItemCode,
            item.ItemName,
            item.UnitOfMeasure,
            item.UnitPrice,
            item.MinimumQuantity,
            item.MaximumQuantity,
            item.LeadTimeDays,
            item.Specifications
        }));

    private static void Capture(ProcurementFrameworkCallOffAuthority item) =>
        item.IntegrityHash = Hash(Serialize(new
        {
            item.TenantId,
            item.AgreementId,
            item.AuthorityKind,
            item.AuthorityUserId,
            item.AuthorityValue,
            item.DisplayName,
            item.MaximumCallOffAmount,
            item.ValidFromUtc,
            item.ValidToUtc,
            item.IsActive
        }));

    private static void Capture(ProcurementFrameworkAgreementDocument item) =>
        item.IntegrityHash = Hash(Serialize(new
        {
            item.TenantId,
            item.AgreementId,
            item.DocumentType,
            item.Title,
            item.FileUploadRecordId,
            item.CentralDocumentRecordId,
            item.CentralDocumentVersionId,
            item.DmsReference,
            item.IsRequired,
            item.IsCurrent,
            item.RetiredAtUtc,
            item.RetiredById
        }));

    private static void Capture(ProcurementFrameworkAgreementExtension item)
    {
        item.SnapshotJson = Serialize(Snapshot(item));
        item.IntegrityHash = Hash(item.SnapshotJson);
    }

    private void Touch(
        ProcurementFrameworkAgreement item,
        string operation,
        string correlationId,
        DateTime now)
    {
        item.LastOperation = operation;
        item.LastOperationCorrelationId = correlationId;
        item.UpdatedAt = now;
        item.UpdatedBy = ActorName;
        item.LastModifiedById = _currentUser.UserId;
    }

    private static void TouchSystem(
        ProcurementFrameworkAgreement item,
        string operation,
        DateTime now)
    {
        item.LastOperation = operation;
        item.LastOperationCorrelationId =
            $"framework-lifecycle-{item.Id:N}-{now:yyyyMMddHHmm}";
        item.UpdatedAt = now;
        item.UpdatedBy = "Framework lifecycle processor";
    }

    private static bool IsReplay(
        ProcurementFrameworkAgreement item,
        string operation,
        string correlationId) =>
        string.Equals(item.LastOperation, operation, StringComparison.Ordinal) &&
        string.Equals(item.LastOperationCorrelationId, correlationId, StringComparison.Ordinal);

    private static void EnsureStatus(
        ProcurementFrameworkAgreement item,
        ProcurementFrameworkAgreementStatus expected,
        string message)
    {
        if (item.Status != expected)
            throw Conflict("FRAMEWORK_AGREEMENT_STATUS_INVALID", message);
    }

    private static void EnsureLifecycleEvidence(
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence)
    {
        if (evidence.Count == 0 || evidence.Any(item =>
                string.IsNullOrWhiteSpace(item.Reference) &&
                (!item.ReferenceId.HasValue || item.ReferenceId == Guid.Empty)))
            throw Validation("FRAMEWORK_AGREEMENT_LIFECYCLE_EVIDENCE_REQUIRED",
                "Submission and decisions require a shared evidence reference.");
    }

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied ?? string.Empty); }
        catch (FormatException)
        {
            throw Validation("FRAMEWORK_AGREEMENT_ROW_VERSION_INVALID",
                "The row version is invalid.");
        }
        if (!current.SequenceEqual(parsed))
            throw Conflict("FRAMEWORK_AGREEMENT_CONCURRENCY_CONFLICT",
                "The framework record changed; reload it before continuing.");
    }

    private async Task ExecuteAsync(
        Func<Task> action,
        CancellationToken cancellationToken,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(
                isolationLevel,
                cancellationToken);
            try
            {
                await action();
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private string ActorName => string.IsNullOrWhiteSpace(_currentUser.FullName)
        ? _currentUser.Username
        : _currentUser.FullName;

    private static string NormalizeCorrelation(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw Validation("FRAMEWORK_AGREEMENT_CORRELATION_REQUIRED",
                "X-Correlation-ID is required.");
        return correlationId.Trim().Length <= 100
            ? correlationId.Trim()
            : Hash(correlationId);
    }

    private static HashSet<Guid> ParseIds(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? [])
                .Where(item => item != Guid.Empty).ToHashSet();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string Serialize(object value) =>
        JsonSerializer.Serialize(value, JsonOptions);
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null :
        value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
    private static string Required(string? value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result))
            throw Validation("FRAMEWORK_AGREEMENT_REQUIRED_VALUE_MISSING",
                "A required framework value is missing.");
        return result.Length <= max ? result : result[..max];
    }

    private static ProcurementFrameworkAgreementNotFoundException NotFound(
        string code,
        string message) => new(code, message);
    private static ProcurementFrameworkAgreementValidationException Validation(
        string code,
        string message) => new(code, message);
    private static ProcurementFrameworkAgreementConflictException Conflict(
        string code,
        string message) => new(code, message);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private sealed record SourceResolution(
        ProcurementAwardReadinessSourceType SourceType,
        Guid SourceId,
        string SourceReference,
        BusinessPartner Supplier,
        ProcurementAwardReadinessDecision Readiness,
        decimal AwardAmount,
        string CurrencyCode,
        DateTime AwardedAtUtc);

    private sealed record CategoryInput(Guid Id, string Code, string Name);
    private sealed record PriceInput(
        Guid Id,
        string Code,
        string Name,
        string UnitOfMeasure,
        decimal UnitPrice,
        decimal MinimumQuantity,
        decimal? MaximumQuantity,
        int LeadTimeDays,
        string? Specifications);
    private sealed record AuthorityInput(
        ProcurementFrameworkAuthorityKind Kind,
        Guid? UserId,
        string Value,
        string DisplayName,
        decimal? MaximumAmount,
        DateTime ValidFromUtc,
        DateTime ValidToUtc,
        bool IsActive);
}

public static class ProcurementFrameworkAgreementRules
{
    public static DateTime EffectiveEnd(ProcurementFrameworkAgreement agreement) =>
        agreement.Extensions
            .Where(item =>
                !item.IsDeleted &&
                item.Status == ProcurementFrameworkExtensionStatus.Approved)
            .Select(item => item.ProposedEndUtc)
            .Append(agreement.EffectiveToUtc)
            .Max();

    public static bool IsEffective(
        ProcurementFrameworkAgreement agreement,
        DateTime atUtc)
    {
        var at = atUtc.Kind == DateTimeKind.Utc
            ? atUtc
            : atUtc.ToUniversalTime();
        return agreement.Status == ProcurementFrameworkAgreementStatus.Published &&
               agreement.EffectiveFromUtc <= at &&
               EffectiveEnd(agreement) > at;
    }

    public static IReadOnlyList<string> AllowedActions(
        ProcurementFrameworkAgreement agreement) =>
        agreement.Status switch
        {
            ProcurementFrameworkAgreementStatus.Draft =>
                ["edit", "add-document", "retire-document", "submit"],
            ProcurementFrameworkAgreementStatus.PendingApproval =>
                ["approve", "reject"],
            ProcurementFrameworkAgreementStatus.Published =>
                ["clone", "request-extension", "terminate", "download-document"],
            ProcurementFrameworkAgreementStatus.Superseded or
            ProcurementFrameworkAgreementStatus.Expired =>
                ["clone", "download-document"],
            _ => ["download-document"]
        };
}
