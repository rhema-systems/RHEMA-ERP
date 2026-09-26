using ErpSystem.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementPrequalificationService : IProcurementPrequalificationService
{
    private const string SourceType = "ProcurementPrequalification";
    private const string ApprovalSourceType = "ProcurementSourcing";
    private const string ManagePermission = "procurement.sourcing.manage";
    private const string EvaluatePermission = "procurement.tender.evaluate";
    private const string ApprovePermission = "procurement.sourcing.approve";
    private const string RuleCode = "TDC-PREQUALIFICATION";
    private const string ActionUrl = "/procurement/prequalification";
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
    private readonly IWorkflowService _workflowService;
    private readonly ISupplierValidationService _supplierValidation;
    private readonly INotificationService _notifications;
    private readonly IWorkflowIntegrationService _workflowIntegration;

    public ProcurementPrequalificationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IWorkflowService workflowService,
        ISupplierValidationService supplierValidation,
        INotificationService notifications,
        IWorkflowIntegrationService workflowIntegration)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _workflowService = workflowService;
        _supplierValidation = supplierValidation;
        _notifications = notifications;
        _workflowIntegration = workflowIntegration;
    }

    private IGenericRepository<ProcurementPrequalificationExercise> Exercises => _unitOfWork.Repository<ProcurementPrequalificationExercise>();
    private IGenericRepository<ProcurementPrequalificationCriterion> Criteria => _unitOfWork.Repository<ProcurementPrequalificationCriterion>();
    private IGenericRepository<ProcurementPrequalificationApplication> Applications => _unitOfWork.Repository<ProcurementPrequalificationApplication>();
    private IGenericRepository<ProcurementPrequalificationScore> Scores => _unitOfWork.Repository<ProcurementPrequalificationScore>();
    private IGenericRepository<ProcurementQualifiedListEntry> QualifiedEntries => _unitOfWork.Repository<ProcurementQualifiedListEntry>();
    private IGenericRepository<PartnerCategory> Categories => _unitOfWork.Repository<PartnerCategory>();
    private IGenericRepository<BusinessPartner> Suppliers => _unitOfWork.Repository<BusinessPartner>();
    private IGenericRepository<BusinessPartnerUser> PartnerUsers => _unitOfWork.Repository<BusinessPartnerUser>();
    private IGenericRepository<ProcurementPolicySet> PolicySets => _unitOfWork.Repository<ProcurementPolicySet>();
    private IGenericRepository<WorkflowDefinition> Workflows => _unitOfWork.Repository<WorkflowDefinition>();

    public async Task<IReadOnlyList<ProcurementPrequalificationSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var exercises = await Exercises.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Applications)
            .Include(item => item.QualifiedEntries)
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        if (_currentUser.IsExternalUser)
            exercises = exercises.Where(item => item.Status != ProcurementPrequalificationStatus.Draft).ToList();
        return exercises.Select(MapSummary).ToList();
    }

    public async Task<ProcurementPrequalificationReadinessDto> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var categories = await Categories.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.IsActive && !item.IsDeleted)
            .OrderBy(item => item.CategoryName).AsNoTracking().ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var policies = await PolicySets.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published && !item.IsDeleted &&
                item.EffectiveFrom <= now && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now))
            .OrderBy(item => item.Name).ThenByDescending(item => item.Version)
            .AsNoTracking().ToListAsync(cancellationToken);
        var workflows = await Workflows.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.IsActive && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.IsDeleted)
            .Include(item => item.EntityType)
            .Where(item => item.EntityType.Code == "PROCUREMENT_SOURCING" ||
                           item.EntityType.Name == "Procurement Sourcing" ||
                           item.EntityType.Name == "ProcurementSourcing")
            .OrderBy(item => item.Name).ThenByDescending(item => item.Version)
            .AsNoTracking().ToListAsync(cancellationToken);
        var suppliers = await Suppliers.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.IsActive &&
                !item.IsBlacklisted && !item.IsDeleted &&
                BusinessPartnerRoles.ProcurementTypes.Contains(item.PartnerType))
            .OrderBy(item => item.PartnerName).AsNoTracking().ToListAsync(cancellationToken);
        if (_currentUser.IsExternalUser)
        {
            var partnerIds = await PartnerUsers.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.UserId == _currentUser.UserId && item.IsActive && !item.IsDeleted)
                .Select(item => item.BusinessPartnerId)
                .ToListAsync(cancellationToken);
            suppliers = suppliers.Where(item => partnerIds.Contains(item.Id)).ToList();
        }
        return new ProcurementPrequalificationReadinessDto
        {
            ApprovalRequired = await _workflowIntegration.HasActiveApprovalWorkflowAsync(ApprovalSourceType),
            Categories = categories.Select(item => new ProcurementPrequalificationCategoryDto
                { Id = item.Id, Code = item.CategoryCode, Name = item.CategoryName }).ToList(),
            Policies = policies.Select(item => new ProcurementPrequalificationPolicyDto
            {
                Id = item.Id, Code = item.Code, Name = item.Name, Version = item.Version,
                SourceConfigurationProfileId = item.SourceConfigurationProfileId
            }).ToList(),
            Workflows = workflows.Select(item => new ProcurementPrequalificationWorkflowDto
                { Id = item.Id, Name = item.Name, Version = item.Version }).ToList(),
            Suppliers = suppliers.Select(item => new ProcurementPrequalificationSupplierDto
                { Id = item.Id, Code = item.PartnerCode, Name = item.PartnerName }).ToList()
        };
    }

    public async Task<ProcurementPrequalificationExerciseDto> GetAsync(Guid exerciseId, CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var dto = await MapAsync(await LoadExerciseAsync(exerciseId, false, cancellationToken), cancellationToken);
        if (_currentUser.IsExternalUser)
        {
            var partnerIds = await PartnerUsers.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.UserId == _currentUser.UserId && item.IsActive && !item.IsDeleted)
                .Select(item => item.BusinessPartnerId).ToListAsync(cancellationToken);
            dto.Applications = dto.Applications.Where(item => partnerIds.Contains(item.BusinessPartnerId)).ToList();
            dto.QualifiedEntries = dto.QualifiedEntries.Where(item => partnerIds.Contains(item.BusinessPartnerId)).ToList();
            dto.ApplicationCount = dto.Applications.Count;
            dto.QualifiedCount = dto.QualifiedEntries.Count(item => item.Status == ProcurementQualifiedListEntryStatus.Active &&
                item.ExpiresAtUtc > DateTime.UtcNow);
        }
        return dto;
    }

    public async Task<ProcurementPrequalificationExerciseDto> CreateAsync(
        CreateProcurementPrequalificationExerciseRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(ManagePermission, request.Reference, correlation, cancellationToken);
        Require(request.Reference, "PREQUAL_REFERENCE_REQUIRED", "A unique prequalification reference is required.");
        Require(request.Title, "PREQUAL_TITLE_REQUIRED", "A prequalification title is required.");
        Require(request.Description, "PREQUAL_DESCRIPTION_REQUIRED", "A detailed prequalification description is required.");
        var reference = request.Reference.Trim().ToUpperInvariant();
        if (await Exercises.ExistsAsync(item => item.TenantId == _currentUser.TenantId && item.Reference == reference && !item.IsDeleted))
            throw Conflict("PREQUAL_REFERENCE_EXISTS", "The prequalification reference already exists in this tenant.");

        var opensAt = EnsureUtc(request.OpensAtUtc);
        var closesAt = EnsureUtc(request.ClosesAtUtc);
        if (closesAt <= opensAt)
            throw Validation("PREQUAL_DATE_RANGE_INVALID", "The submission closing date must be after the opening date.");
        if (closesAt <= DateTime.UtcNow)
            throw Validation("PREQUAL_CLOSE_DATE_PAST", "A new prequalification exercise requires a future closing date.");
        if (request.ValidityMonths is < 1 or > 60)
            throw Validation("PREQUAL_VALIDITY_INVALID", "Qualified-list validity must be between 1 and 60 months.");
        if (request.PassingScore is < 1 or > 100)
            throw Validation("PREQUAL_PASSING_SCORE_INVALID", "The overall passing score must be between 1 and 100.");

        var categoryIds = request.CategoryIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (categoryIds.Count == 0) throw Validation("PREQUAL_CATEGORY_REQUIRED", "At least one active category is required.");
        var categories = await Categories.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                categoryIds.Contains(item.Id) && item.IsActive && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (categories.Count != categoryIds.Count)
            throw Validation("PREQUAL_CATEGORY_INVALID", "One or more selected categories are unavailable in this tenant.");

        var criterionRequests = request.Criteria.OrderBy(item => item.SortOrder).ToList();
        if (criterionRequests.Count == 0)
            throw Validation("PREQUAL_CRITERIA_REQUIRED", "At least one evaluation criterion is required.");
        var codes = criterionRequests.Select(item => item.Code.Trim().ToUpperInvariant()).ToList();
        if (codes.Any(string.IsNullOrWhiteSpace) || codes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != codes.Count)
            throw Validation("PREQUAL_CRITERIA_DUPLICATE", "Criterion codes are required and must be unique.");
        if (Math.Abs(criterionRequests.Sum(item => item.Weight) - 100m) > 0.01m)
            throw Validation("PREQUAL_CRITERIA_WEIGHT_INVALID", "Criterion weights must total exactly 100.");
        if (criterionRequests.Any(item => item.Weight <= 0 || item.Weight > 100 ||
                item.MinimumScore < 0 || item.MinimumScore > 100))
            throw Validation("PREQUAL_CRITERION_RANGE_INVALID", "Criterion weights and minimum scores must be within the allowed range.");
        if (!criterionRequests.Any(item => item.IsMandatory))
            throw Validation("PREQUAL_MANDATORY_CRITERION_REQUIRED", "At least one criterion must be mandatory.");

        var policy = await PolicySets.GetQueryable(item => item.Id == request.PolicySetId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var policyMoment = DateTime.UtcNow;
        if (policy is null || policy.LifecycleStatus != ProcurementPolicyLifecycleStatus.Published ||
            policy.EffectiveFrom > policyMoment ||
            (policy.EffectiveTo.HasValue && policy.EffectiveTo.Value < policyMoment))
            throw Validation("PREQUAL_POLICY_INVALID", "Select one current Published procurement policy set.");

        var approvalRequired = await _workflowIntegration.HasActiveApprovalWorkflowAsync(ApprovalSourceType);
        var workflow = request.WorkflowDefinitionId.HasValue
            ? await Workflows.GetQueryable(item => item.Id == request.WorkflowDefinitionId.Value &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .Include(item => item.EntityType).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            : null;
        if ((approvalRequired && workflow is null) || (request.WorkflowDefinitionId.HasValue &&
            (workflow is null || !workflow.IsActive || workflow.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published ||
             workflow.EntityType.IsDeleted || !workflow.EntityType.IsActive ||
             !(workflow.EntityType.Code.Equals("PROCUREMENT_SOURCING", StringComparison.OrdinalIgnoreCase) ||
               workflow.EntityType.Name.Replace(" ", string.Empty).Equals("ProcurementSourcing", StringComparison.OrdinalIgnoreCase)))))
            throw Validation("PREQUAL_WORKFLOW_INVALID", "Select one active Published Procurement Sourcing workflow definition.");

        var now = DateTime.UtcNow;
        var exercise = new ProcurementPrequalificationExercise
        {
            Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, Reference = reference,
            Title = request.Title.Trim(), Description = request.Description.Trim(),
            Status = ProcurementPrequalificationStatus.Draft,
            CategoryIdsJson = SerializeGuidList(categoryIds), OpensAtUtc = opensAt, ClosesAtUtc = closesAt,
            ValidityMonths = request.ValidityMonths, PassingScore = request.PassingScore,
            PolicySetId = policy.Id, PolicySetCode = policy.Code, PolicySetVersion = policy.Version,
            SourceConfigurationProfileId = policy.SourceConfigurationProfileId,
            WorkflowDefinitionId = workflow?.Id, CreatedAt = now, CreatedBy = ActorName(), CreatedById = _currentUser.UserId
        };
        foreach (var item in criterionRequests)
            exercise.Criteria.Add(new ProcurementPrequalificationCriterion
            {
                Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, ExerciseId = exercise.Id,
                Code = item.Code.Trim().ToUpperInvariant(), Name = item.Name.Trim(),
                Description = NullIfWhiteSpace(item.Description), Weight = item.Weight,
                MinimumScore = item.MinimumScore, IsMandatory = item.IsMandatory,
                RequiresEvidence = item.RequiresEvidence, SortOrder = item.SortOrder,
                CreatedAt = now, CreatedBy = ActorName(), CreatedById = _currentUser.UserId
            });
        CaptureExercise(exercise);
        await Exercises.AddAsync(exercise);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(exercise, "PrequalificationDraftCreated", ProcurementControlEventResult.Allowed,
            new { categoryIds, request.ValidityMonths, request.PassingScore, CriterionCount = criterionRequests.Count },
            new { exercise.Id, exercise.Reference, exercise.Status, exercise.IntegrityHash }, correlation, cancellationToken);
        return await GetAsync(exercise.Id, cancellationToken);
    }

    public async Task<ProcurementPrequalificationExerciseDto> AdvertiseAsync(
        Guid exerciseId,
        AdvertiseProcurementPrequalificationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var exercise = await LoadExerciseAsync(exerciseId, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, exercise.Reference, correlation, cancellationToken);
        EnsureStatus(exercise, ProcurementPrequalificationStatus.Draft, "PREQUAL_ADVERTISEMENT_NOT_DRAFT");
        EnsureRowVersion(exercise.RowVersion, request.RowVersion);
        await EnsureCurrentPolicyLineageAsync(exercise, cancellationToken);
        Require(request.AdvertisementReference, "PREQUAL_ADVERTISEMENT_REFERENCE_REQUIRED", "The approved advertisement reference is required.");
        Require(request.AdvertisementEvidenceReference, "PREQUAL_ADVERTISEMENT_EVIDENCE_REQUIRED", "Advertisement publication evidence is required.");
        if (exercise.ClosesAtUtc <= DateTime.UtcNow)
            throw Conflict("PREQUAL_ADVERTISEMENT_WINDOW_CLOSED", "The configured submission window has already closed.");
        var now = DateTime.UtcNow;
        exercise.AdvertisementReference = request.AdvertisementReference.Trim();
        exercise.AdvertisementEvidenceReference = request.AdvertisementEvidenceReference.Trim();
        exercise.AdvertisedAtUtc = now;
        exercise.AdvertisedById = _currentUser.UserId;
        exercise.Status = ProcurementPrequalificationStatus.Advertised;
        TouchExercise(exercise, now);
        await Exercises.UpdateAsync(exercise);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(exercise, "PrequalificationAdvertised", ProcurementControlEventResult.Allowed,
            new { request.AdvertisementReference }, new { exercise.Status, exercise.AdvertisedAtUtc },
            correlation, cancellationToken,
            External(exercise.AdvertisementEvidenceReference, "Prequalification advertisement", "SRC-005"));
        return await GetAsync(exerciseId, cancellationToken);
    }

    public async Task<ProcurementPrequalificationApplicationDto> SubmitApplicationAsync(
        Guid exerciseId,
        SubmitProcurementPrequalificationApplicationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        EnsureAuthenticatedTenant();
        var exercise = await LoadExerciseAsync(exerciseId, true, cancellationToken);
        EnsureStatus(exercise, ProcurementPrequalificationStatus.Advertised, "PREQUAL_APPLICATION_WINDOW_CLOSED");
        var now = DateTime.UtcNow;
        if (now < exercise.OpensAtUtc || now > exercise.ClosesAtUtc)
            throw Conflict("PREQUAL_APPLICATION_OUTSIDE_WINDOW", "Applications are accepted only during the advertised submission window.");
        if (request.BusinessPartnerId == Guid.Empty)
            throw Validation("PREQUAL_SUPPLIER_REQUIRED", "A supplier or contractor is required.");
        await EnsureSupplierActorAsync(request.BusinessPartnerId, exercise.Reference, correlation, cancellationToken);
        if (await Applications.ExistsAsync(item => item.TenantId == _currentUser.TenantId &&
                item.ExerciseId == exerciseId && item.BusinessPartnerId == request.BusinessPartnerId && !item.IsDeleted))
            throw Conflict("PREQUAL_APPLICATION_EXISTS", "This supplier already has an application for the exercise.");
        var supplier = await Suppliers.GetQueryable(item => item.Id == request.BusinessPartnerId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Categories)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("PREQUAL_SUPPLIER_NOT_FOUND", "The supplier is unavailable in this tenant.");
        var supplierValidation = await _supplierValidation.ValidateForPurchaseOrderAsync(supplier.Id);
        if (!supplierValidation.IsValid)
            throw Validation("PREQUAL_SUPPLIER_INELIGIBLE", string.Join("; ", supplierValidation.Errors));

        var exerciseCategories = DeserializeGuidList(exercise.CategoryIdsJson);
        var selectedCategories = request.CategoryIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (selectedCategories.Count == 0 || selectedCategories.Except(exerciseCategories).Any())
            throw Validation("PREQUAL_APPLICATION_CATEGORY_INVALID", "Select at least one category advertised by this exercise.");
        var supplierCategoryIds = supplier.Categories.Where(item => selectedCategories.Contains(item.CategoryId))
            .Select(item => item.CategoryId).Distinct().ToList();
        if (supplierCategoryIds.Count != selectedCategories.Count)
            throw Validation("PREQUAL_SUPPLIER_CATEGORY_MISMATCH", "The supplier profile does not include every selected category.");

        var evidence = request.Evidence.Where(item => !string.IsNullOrWhiteSpace(item.CriterionCode))
            .GroupBy(item => item.CriterionCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        if (evidence.Values.Any(items => items.Count != 1))
            throw Validation("PREQUAL_EVIDENCE_DUPLICATE", "Each criterion evidence item may be supplied only once.");
        var snapshots = new List<EvidenceSnapshot>();
        foreach (var criterion in exercise.Criteria.OrderBy(item => item.SortOrder))
        {
            if (!criterion.RequiresEvidence) continue;
            if (!evidence.TryGetValue(criterion.Code, out var supplied))
                throw Validation("PREQUAL_EVIDENCE_MISSING", $"Evidence is required for criterion '{criterion.Name}'.");
            var item = supplied[0];
            Require(item.EvidenceReference, "PREQUAL_EVIDENCE_REFERENCE_REQUIRED", $"Evidence reference is required for '{criterion.Name}'.");
            Require(item.VerificationReference, "PREQUAL_EVIDENCE_VERIFICATION_REQUIRED", $"Verified evidence reference is required for '{criterion.Name}'.");
            snapshots.Add(new EvidenceSnapshot(criterion.Code, item.EvidenceReference.Trim(), item.VerificationReference.Trim()));
        }
        if (evidence.Keys.Except(exercise.Criteria.Where(item => item.RequiresEvidence).Select(item => item.Code), StringComparer.OrdinalIgnoreCase).Any())
            throw Validation("PREQUAL_EVIDENCE_UNKNOWN", "The application contains evidence for an unknown or non-evidence criterion.");

        var sequence = await Applications.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.ExerciseId == exerciseId)
            .CountAsync(cancellationToken) + 1;
        var application = new ProcurementPrequalificationApplication
        {
            Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, ExerciseId = exercise.Id,
            BusinessPartnerId = supplier.Id, ApplicationNumber = $"{exercise.Reference}-APP-{sequence:0000}",
            CategoryIdsJson = SerializeGuidList(selectedCategories),
            EvidenceJson = JsonSerializer.Serialize(snapshots, JsonOptions),
            Status = ProcurementPrequalificationApplicationStatus.Submitted,
            SubmittedAtUtc = now, SubmittedById = _currentUser.UserId,
            CreatedAt = now, CreatedBy = ActorName(), CreatedById = _currentUser.UserId
        };
        CaptureApplication(application);
        await Applications.AddAsync(application);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(exercise, "PrequalificationApplicationSubmitted", ProcurementControlEventResult.Allowed,
            new { application.BusinessPartnerId, selectedCategories },
            new { application.Id, application.ApplicationNumber, application.Status, application.IntegrityHash },
            correlation, cancellationToken,
            snapshots.Select(item => External(item.EvidenceReference, $"Application evidence {item.CriterionCode}", item.CriterionCode)).ToArray());
        return MapApplication(await LoadApplicationAsync(exerciseId, application.Id, false, cancellationToken));
    }

    public async Task<ProcurementPrequalificationExerciseDto> CloseAsync(
        Guid exerciseId,
        CloseProcurementPrequalificationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var exercise = await LoadExerciseAsync(exerciseId, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, exercise.Reference, correlation, cancellationToken);
        EnsureStatus(exercise, ProcurementPrequalificationStatus.Advertised, "PREQUAL_CLOSE_NOT_ADVERTISED");
        EnsureRowVersion(exercise.RowVersion, request.RowVersion);
        if (DateTime.UtcNow < exercise.ClosesAtUtc)
            throw Conflict("PREQUAL_CLOSE_TOO_EARLY", "The advertised application deadline has not elapsed.");
        if (!exercise.Applications.Any(item => !item.IsDeleted))
            throw Conflict("PREQUAL_NO_APPLICATIONS", "The exercise cannot close without at least one submitted application.");
        var now = DateTime.UtcNow;
        exercise.Status = ProcurementPrequalificationStatus.Closed;
        exercise.ClosedAtUtc = now;
        exercise.ClosedById = _currentUser.UserId;
        TouchExercise(exercise, now);
        await Exercises.UpdateAsync(exercise);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(exercise, "PrequalificationSubmissionsClosed", ProcurementControlEventResult.Allowed,
            new { exercise.ClosesAtUtc }, new { exercise.Status, ApplicationCount = exercise.Applications.Count(item => !item.IsDeleted) },
            correlation, cancellationToken);
        return await GetAsync(exerciseId, cancellationToken);
    }

    public async Task<ProcurementPrequalificationApplicationDto> EvaluateAsync(
        Guid exerciseId,
        Guid applicationId,
        EvaluateProcurementPrequalificationApplicationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var exercise = await LoadExerciseAsync(exerciseId, true, cancellationToken);
        await EnsureCapabilityAsync(EvaluatePermission, exercise.Reference, correlation, cancellationToken);
        if (exercise.Status is not ProcurementPrequalificationStatus.Closed and not ProcurementPrequalificationStatus.UnderEvaluation)
            throw Conflict("PREQUAL_EVALUATION_NOT_OPEN", "Evaluation requires a closed prequalification exercise.");
        var application = exercise.Applications.SingleOrDefault(item => item.Id == applicationId && !item.IsDeleted)
            ?? throw NotFound("PREQUAL_APPLICATION_NOT_FOUND", "The application is unavailable in this exercise.");
        if (application.Status != ProcurementPrequalificationApplicationStatus.Submitted)
            throw Conflict("PREQUAL_APPLICATION_ALREADY_EVALUATED", "Each application can be evaluated only once.");
        EnsureRowVersion(application.RowVersion, request.RowVersion);
        if (exercise.CreatedById == _currentUser.UserId || application.SubmittedById == _currentUser.UserId)
            throw new ProcurementPrequalificationAuthorizationException("The exercise preparer or application submitter cannot evaluate the application.");
        var prohibited = new[] { exercise.CreatedById ?? Guid.Empty, application.SubmittedById }.Where(id => id != Guid.Empty).Distinct().ToList();
        if (prohibited.Count > 0)
        {
            var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
            {
                ControlCode = "SOD-TENDER-TECHNICAL-FINANCIAL-EVALUATOR",
                SourceType = SourceType, SourceReference = exercise.Reference,
                ProhibitedActorUserIds = prohibited
            }, correlation, cancellationToken);
            if (!sod.Allowed) throw new ProcurementPrequalificationAuthorizationException(sod.Message);
        }
        Require(request.Remarks, "PREQUAL_EVALUATION_REMARKS_REQUIRED", "Evaluation remarks are required.");
        Require(request.RecommendationEvidenceReference, "PREQUAL_RECOMMENDATION_EVIDENCE_REQUIRED", "Signed evaluation/recommendation evidence is required.");
        var supplied = request.Scores.GroupBy(item => item.CriterionId).ToDictionary(group => group.Key, group => group.ToList());
        if (supplied.Count != exercise.Criteria.Count || supplied.Values.Any(items => items.Count != 1) ||
            exercise.Criteria.Any(criterion => !supplied.ContainsKey(criterion.Id)))
            throw Validation("PREQUAL_SCORECARD_INCOMPLETE", "Exactly one score is required for every locked criterion.");

        var now = DateTime.UtcNow;
        var scoreRows = new List<ProcurementPrequalificationScore>();
        foreach (var criterion in exercise.Criteria.OrderBy(item => item.SortOrder))
        {
            var item = supplied[criterion.Id][0];
            if (item.Score is < 0 or > 100)
                throw Validation("PREQUAL_SCORE_RANGE_INVALID", "Criterion scores must be between 0 and 100.");
            Require(item.Reason, "PREQUAL_SCORE_REASON_REQUIRED", $"A score reason is required for '{criterion.Name}'.");
            if (criterion.RequiresEvidence && string.IsNullOrWhiteSpace(item.EvidenceReference))
                throw Validation("PREQUAL_SCORE_EVIDENCE_REQUIRED", $"Evaluation evidence is required for '{criterion.Name}'.");
            scoreRows.Add(new ProcurementPrequalificationScore
            {
                Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, ApplicationId = application.Id,
                CriterionId = criterion.Id, Score = item.Score, MeetsRequirement = item.MeetsRequirement,
                Reason = item.Reason.Trim(), EvidenceReference = NullIfWhiteSpace(item.EvidenceReference),
                EvaluatedAtUtc = now, EvaluatedById = _currentUser.UserId,
                CreatedAt = now, CreatedBy = ActorName(), CreatedById = _currentUser.UserId
            });
        }
        var total = Math.Round(scoreRows.Sum(score =>
            score.Score * exercise.Criteria.Single(criterion => criterion.Id == score.CriterionId).Weight / 100m), 2);
        var mandatoryPassed = scoreRows.All(score =>
        {
            var criterion = exercise.Criteria.Single(item => item.Id == score.CriterionId);
            return !criterion.IsMandatory || (score.MeetsRequirement && score.Score >= criterion.MinimumScore);
        });
        var passed = mandatoryPassed && total >= exercise.PassingScore;
        foreach (var score in scoreRows)
            application.Scores.Add(score);
        await Scores.AddRangeAsync(scoreRows);
        application.TotalScore = total;
        application.Passed = passed;
        application.Status = passed
            ? ProcurementPrequalificationApplicationStatus.EvaluatedQualified
            : ProcurementPrequalificationApplicationStatus.EvaluatedRejected;
        application.EvaluationRemarks = request.Remarks.Trim();
        application.RecommendationEvidenceReference = request.RecommendationEvidenceReference.Trim();
        application.EvaluatedAtUtc = now;
        application.EvaluatedById = _currentUser.UserId;
        CaptureApplication(application);
        exercise.Status = ProcurementPrequalificationStatus.UnderEvaluation;
        TouchExercise(exercise, now);
        await Applications.UpdateAsync(application);
        await Exercises.UpdateAsync(exercise);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(exercise, "PrequalificationApplicationEvaluated",
            passed ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.ReviewRequired,
            new { application.Id, EvaluatorUserId = _currentUser.UserId },
            new { application.Status, application.TotalScore, application.Passed, application.IntegrityHash },
            correlation, cancellationToken,
            External(application.RecommendationEvidenceReference, "Signed prequalification evaluation", "SRC-005"));
        return MapApplication(await LoadApplicationAsync(exerciseId, applicationId, false, cancellationToken));
    }

    public async Task<ProcurementPrequalificationExerciseDto> SubmitDecisionAsync(
        Guid exerciseId,
        SubmitProcurementPrequalificationDecisionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var exercise = await LoadExerciseAsync(exerciseId, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, exercise.Reference, correlation, cancellationToken);
        EnsureStatus(exercise, ProcurementPrequalificationStatus.UnderEvaluation, "PREQUAL_DECISION_NOT_READY");
        EnsureRowVersion(exercise.RowVersion, request.RowVersion);
        var applications = exercise.Applications.Where(item => !item.IsDeleted).ToList();
        if (applications.Count == 0 || applications.Any(item => item.Status == ProcurementPrequalificationApplicationStatus.Submitted))
            throw Conflict("PREQUAL_EVALUATIONS_INCOMPLETE", "Every submitted application must have a complete immutable evaluation before approval.");

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var workflow = exercise.WorkflowDefinitionId.HasValue
                    ? await _workflowIntegration.SubmitAsync(ApprovalSourceType, exercise.Id, exercise.WorkflowDefinitionId.Value)
                    : await _workflowIntegration.SubmitAsync(ApprovalSourceType, exercise.Id);
                if (!workflow.ExecutionResult.Success || (workflow.ApprovalRequired &&
                    (!exercise.WorkflowDefinitionId.HasValue || !workflow.ExecutionResult.WorkflowInstanceId.HasValue ||
                     workflow.Outcome != WorkflowOutcome.Pending)))
                    throw Conflict("PREQUAL_WORKFLOW_START_FAILED", workflow.ExecutionResult.Message ?? "The exact prequalification workflow could not be started.");
                var now = DateTime.UtcNow;
                exercise.ApprovalRequired = workflow.ApprovalRequired;
                exercise.WorkflowInstanceId = workflow.ExecutionResult.WorkflowInstanceId;
                exercise.Status = ProcurementPrequalificationStatus.PendingApproval;
                exercise.SubmittedForApprovalAtUtc = now;
                exercise.SubmittedForApprovalById = _currentUser.UserId;
                if (!workflow.ApprovalRequired)
                {
                    RequireDecisionEvidence(request.DecisionReference, request.DecisionEvidenceReference, request.Reason);
                    exercise.WorkflowInstanceId = null;
                    exercise.DecisionReference = request.DecisionReference!.Trim();
                    exercise.DecisionEvidenceReference = request.DecisionEvidenceReference!.Trim();
                    exercise.DecisionReason = request.Reason!.Trim();
                    exercise.DecidedAtUtc = now;
                    exercise.DecidedById = _currentUser.UserId;
                    // Qualified-list insert guards read the persisted exercise state.
                    // Save the validated direct decision first, inside this same owner
                    // transaction, then finalize applications and list entries atomically.
                    exercise.Status = ProcurementPrequalificationStatus.Approved;
                    TouchExercise(exercise, now);
                    await Exercises.UpdateAsync(exercise);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await CompleteQualificationAsync(exercise, now, cancellationToken);
                }
                TouchExercise(exercise, now);
                await Exercises.UpdateAsync(exercise);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
        if (!exercise.ApprovalRequired) await NotifyApplicantsAsync(exercise, cancellationToken);
        await RecordAsync(exercise, exercise.ApprovalRequired ? "PrequalificationDecisionSubmitted" : "PrequalificationCompleted", ProcurementControlEventResult.Allowed,
            new { exercise.SubmittedForApprovalById },
            new { exercise.WorkflowDefinitionId, exercise.WorkflowInstanceId, exercise.Status },
            correlation, cancellationToken,
            exercise.ApprovalRequired
                ? External($"workflow:{exercise.WorkflowInstanceId:N}", "Prequalification approval workflow", "DEC-003")
                : External(exercise.DecisionEvidenceReference, "Prequalification decision", "DEC-003"));
        return await GetAsync(exerciseId, cancellationToken);
    }

    private static void RequireDecisionEvidence(string? reference, string? evidence, string? reason)
    {
        Require(reference, "PREQUAL_DECISION_REFERENCE_REQUIRED", "The decision reference is required.");
        Require(evidence, "PREQUAL_DECISION_EVIDENCE_REQUIRED", "Signed decision evidence is required.");
        Require(reason, "PREQUAL_DECISION_REASON_REQUIRED", "A decision reason is required.");
    }

    private async Task CompleteQualificationAsync(ProcurementPrequalificationExercise exercise,
        DateTime now, CancellationToken cancellationToken)
    {
        var applications = exercise.Applications.Where(item => !item.IsDeleted).ToList();
        if (applications.Count == 0 || applications.Any(item => item.Status is not
                ProcurementPrequalificationApplicationStatus.EvaluatedQualified and not
                ProcurementPrequalificationApplicationStatus.EvaluatedRejected))
            throw Conflict("PREQUAL_EVALUATIONS_INCOMPLETE", "Every application must retain its completed evaluation before finalizing the decision.");
        exercise.Status = ProcurementPrequalificationStatus.Approved;
        foreach (var application in applications)
        {
            var qualified = application.Status == ProcurementPrequalificationApplicationStatus.EvaluatedQualified;
            application.Status = qualified
                ? ProcurementPrequalificationApplicationStatus.Approved
                : ProcurementPrequalificationApplicationStatus.Rejected;
            CaptureApplication(application);
            await Applications.UpdateAsync(application);
            if (!qualified) continue;
            foreach (var categoryId in DeserializeGuidList(application.CategoryIdsJson))
            {
                var entry = new ProcurementQualifiedListEntry
                {
                    Id = Guid.NewGuid(), TenantId = _currentUser.TenantId, ExerciseId = exercise.Id,
                    ApplicationId = application.Id, BusinessPartnerId = application.BusinessPartnerId,
                    CategoryId = categoryId, ValidFromUtc = now, ExpiresAtUtc = now.AddMonths(exercise.ValidityMonths),
                    Status = ProcurementQualifiedListEntryStatus.Active,
                    // These legacy column names retain the actual signed business decision,
                    // including direct completion; they do not manufacture an internal approver.
                    ApprovalReference = exercise.DecisionReference!,
                    ApprovalEvidenceReference = exercise.DecisionEvidenceReference!,
                    CreatedAt = now, CreatedBy = ActorName(), CreatedById = _currentUser.UserId
                };
                CaptureEntry(entry);
                await QualifiedEntries.AddAsync(entry);
            }
        }
    }

    public async Task<ProcurementPrequalificationExerciseDto> DecideAsync(
        Guid exerciseId,
        DecideProcurementPrequalificationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var exercise = await LoadExerciseAsync(exerciseId, true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, exercise.Reference, correlation, cancellationToken);
        EnsureStatus(exercise, ProcurementPrequalificationStatus.PendingApproval, "PREQUAL_DECISION_NOT_PENDING");
        EnsureRowVersion(exercise.RowVersion, request.RowVersion);
        RequireDecisionEvidence(request.DecisionReference, request.DecisionEvidenceReference, request.Reason);
        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not "approve" and not "reject")
            throw Validation("PREQUAL_DECISION_ACTION_INVALID", "Action must be Approve or Reject.");
        if (!exercise.WorkflowInstanceId.HasValue ||
            !await _workflowService.CanUserApproveAsync(ApprovalSourceType, exercise.Id, _currentUser.UserId))
            throw new ProcurementPrequalificationAuthorizationException("The current user is not assigned to the active prequalification workflow.");

        var prohibited = exercise.Applications.Where(item => !item.IsDeleted)
            .SelectMany(item => new[] { item.SubmittedById, item.EvaluatedById ?? Guid.Empty })
            .Append(exercise.CreatedById ?? Guid.Empty)
            .Append(exercise.SubmittedForApprovalById ?? Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().ToList();
        if (prohibited.Count > 0)
        {
            var sod = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
            {
                ControlCode = "SOD-INITIATOR-APPROVER", SourceType = SourceType,
                SourceReference = exercise.Reference, ProhibitedActorUserIds = prohibited
            }, correlation, cancellationToken);
            if (!sod.Allowed) throw new ProcurementPrequalificationAuthorizationException(sod.Message);
        }

        var result = await _workflowService.ProcessApprovalStepAsync(
            ApprovalSourceType, exercise.Id, _currentUser.UserId, action, request.Reason);
        if (!result.Success)
            throw Conflict("PREQUAL_WORKFLOW_DECISION_FAILED", result.Message ?? "The prequalification workflow decision failed.");
        var now = DateTime.UtcNow;
        exercise.DecisionReference = request.DecisionReference.Trim();
        exercise.DecisionEvidenceReference = request.DecisionEvidenceReference.Trim();
        exercise.DecisionReason = request.Reason.Trim();
        exercise.DecidedAtUtc = now;
        exercise.DecidedById = _currentUser.UserId;
        if (action == "approve" && result.Status == WorkflowInstanceStatus.Completed)
        {
            await CompleteQualificationAsync(exercise, now, cancellationToken);
        }
        else if (action == "reject")
        {
            if (result.Status is not WorkflowInstanceStatus.Cancelled and not WorkflowInstanceStatus.Failed)
                throw Conflict("PREQUAL_REJECTION_NOT_FINAL", "A rejection is final only when the shared workflow is Cancelled or Failed.");
            exercise.Status = ProcurementPrequalificationStatus.Rejected;
            foreach (var application in exercise.Applications.Where(item => !item.IsDeleted))
            {
                application.Status = ProcurementPrequalificationApplicationStatus.Rejected;
                CaptureApplication(application);
                await Applications.UpdateAsync(application);
            }
        }
        TouchExercise(exercise, now);
        await Exercises.UpdateAsync(exercise);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (exercise.Status is ProcurementPrequalificationStatus.Approved or ProcurementPrequalificationStatus.Rejected)
            await NotifyApplicantsAsync(exercise, cancellationToken);
        await RecordAsync(exercise,
            exercise.Status == ProcurementPrequalificationStatus.Approved ? "PrequalificationApproved" :
            exercise.Status == ProcurementPrequalificationStatus.Rejected ? "PrequalificationRejected" :
            "PrequalificationApprovalStepCompleted",
            exercise.Status == ProcurementPrequalificationStatus.Rejected
                ? ProcurementControlEventResult.Rejected : ProcurementControlEventResult.Allowed,
            new { action, ActorUserId = _currentUser.UserId, request.DecisionReference },
            new { exercise.Status, WorkflowStatus = result.Status, QualifiedCount = exercise.Applications.Count(item => item.Passed == true) },
            correlation, cancellationToken,
            External(exercise.DecisionEvidenceReference, "Prequalification decision", "DEC-003"),
            External($"workflow:{exercise.WorkflowInstanceId:N}", "Prequalification approval workflow", "DEC-003"));
        return await GetAsync(exerciseId, cancellationToken);
    }

    public async Task<ProcurementPrequalificationExerciseDto> ExpireAsync(
        Guid exerciseId,
        ExpireProcurementPrequalificationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var exercise = await LoadExerciseAsync(exerciseId, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, exercise.Reference, correlation, cancellationToken);
        EnsureStatus(exercise, ProcurementPrequalificationStatus.Approved, "PREQUAL_EXPIRY_NOT_APPROVED");
        EnsureRowVersion(exercise.RowVersion, request.RowVersion);
        var now = DateTime.UtcNow;
        var due = exercise.QualifiedEntries.Where(item => !item.IsDeleted &&
            item.Status == ProcurementQualifiedListEntryStatus.Active && item.ExpiresAtUtc <= now).ToList();
        if (due.Count == 0)
            throw Conflict("PREQUAL_EXPIRY_NOT_DUE", "No active qualified-list entry has reached its expiry date.");
        foreach (var entry in due)
        {
            entry.Status = ProcurementQualifiedListEntryStatus.Expired;
            entry.ExpiredAtUtc = now;
            CaptureEntry(entry);
            await QualifiedEntries.UpdateAsync(entry);
        }
        if (!exercise.QualifiedEntries.Any(item => !item.IsDeleted &&
                item.Status == ProcurementQualifiedListEntryStatus.Active && item.ExpiresAtUtc > now))
            exercise.Status = ProcurementPrequalificationStatus.Expired;
        TouchExercise(exercise, now);
        await Exercises.UpdateAsync(exercise);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordAsync(exercise, "PrequalificationEntriesExpired", ProcurementControlEventResult.Allowed,
            new { EvaluatedAtUtc = now }, new { ExpiredCount = due.Count, exercise.Status },
            correlation, cancellationToken);
        return await GetAsync(exerciseId, cancellationToken);
    }

    public async Task<ProcurementSupplierEligibilityDto> CheckEligibilityAsync(
        Guid businessPartnerId,
        Guid categoryId,
        DateTime? atUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        if (_currentUser.IsExternalUser && !await PartnerUsers.ExistsAsync(item => item.TenantId == _currentUser.TenantId &&
                item.UserId == _currentUser.UserId && item.BusinessPartnerId == businessPartnerId &&
                item.IsActive && !item.IsDeleted))
            throw new ProcurementPrequalificationAuthorizationException("External users may check eligibility only for their linked supplier account.");
        var moment = EnsureUtc(atUtc ?? DateTime.UtcNow);
        var supplierExists = await Suppliers.ExistsAsync(item => item.Id == businessPartnerId &&
            item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        if (!supplierExists) throw NotFound("PREQUAL_SUPPLIER_NOT_FOUND", "The supplier is unavailable in this tenant.");
        var categoryExists = await Categories.ExistsAsync(item => item.Id == categoryId &&
            item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        if (!categoryExists) throw NotFound("PREQUAL_CATEGORY_NOT_FOUND", "The category is unavailable in this tenant.");
        var entry = await QualifiedEntries.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == businessPartnerId && item.CategoryId == categoryId &&
                item.Status == ProcurementQualifiedListEntryStatus.Active &&
                item.ValidFromUtc <= moment && item.ExpiresAtUtc > moment && !item.IsDeleted)
            .Include(item => item.BusinessPartner).Include(item => item.Category)
            .AsNoTracking().OrderByDescending(item => item.ExpiresAtUtc).FirstOrDefaultAsync(cancellationToken);
        return new ProcurementSupplierEligibilityDto
        {
            BusinessPartnerId = businessPartnerId, CategoryId = categoryId, EvaluatedAtUtc = moment,
            Eligible = entry is not null,
            Code = entry is null ? "NOT_PREQUALIFIED" : "PREQUALIFIED",
            Message = entry is null
                ? "No active, approved, unexpired qualified-list entry exists for this supplier and category."
                : $"Qualified through {entry.ExpiresAtUtc:yyyy-MM-dd}.",
            Entry = entry is null ? null : MapEntry(entry)
        };
    }

    private async Task<ProcurementPrequalificationExercise> LoadExerciseAsync(
        Guid exerciseId, bool tracked, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        IQueryable<ProcurementPrequalificationExercise> query = Exercises.GetQueryable(item => item.Id == exerciseId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Criteria.Where(criterion => !criterion.IsDeleted))
            .Include(item => item.Applications.Where(application => !application.IsDeleted))
                .ThenInclude(application => application.BusinessPartner)
            .Include(item => item.Applications.Where(application => !application.IsDeleted))
                .ThenInclude(application => application.Scores.Where(score => !score.IsDeleted))
                    .ThenInclude(score => score.Criterion)
            .Include(item => item.QualifiedEntries.Where(entry => !entry.IsDeleted))
                .ThenInclude(entry => entry.BusinessPartner)
            .Include(item => item.QualifiedEntries.Where(entry => !entry.IsDeleted))
                .ThenInclude(entry => entry.Category);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("PREQUAL_EXERCISE_NOT_FOUND", "The prequalification exercise is unavailable in this tenant.");
    }

    private async Task<ProcurementPrequalificationApplication> LoadApplicationAsync(
        Guid exerciseId, Guid applicationId, bool tracked, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        IQueryable<ProcurementPrequalificationApplication> query = Applications.GetQueryable(item => item.Id == applicationId && item.ExerciseId == exerciseId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.BusinessPartner)
            .Include(item => item.Scores.Where(score => !score.IsDeleted)).ThenInclude(score => score.Criterion);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("PREQUAL_APPLICATION_NOT_FOUND", "The application is unavailable in this tenant.");
    }

    private async Task<ProcurementPrequalificationExerciseDto> MapAsync(
        ProcurementPrequalificationExercise exercise, CancellationToken cancellationToken)
    {
        var categoryIds = DeserializeGuidList(exercise.CategoryIdsJson);
        var categories = await Categories.GetQueryable(item => item.TenantId == exercise.TenantId &&
                categoryIds.Contains(item.Id) && !item.IsDeleted)
            .AsNoTracking().OrderBy(item => item.CategoryName).ToListAsync(cancellationToken);
        var summary = MapSummary(exercise);
        return new ProcurementPrequalificationExerciseDto
        {
            Id = summary.Id, Reference = summary.Reference, Title = summary.Title, Status = summary.Status,
            ApprovalRequired = exercise.ApprovalRequired,
            OpensAtUtc = summary.OpensAtUtc, ClosesAtUtc = summary.ClosesAtUtc,
            ApplicationCount = summary.ApplicationCount, QualifiedCount = summary.QualifiedCount,
            ExpiresAtUtc = summary.ExpiresAtUtc, Description = exercise.Description,
            ValidityMonths = exercise.ValidityMonths, PassingScore = exercise.PassingScore,
            PolicySetId = exercise.PolicySetId, PolicySetCode = exercise.PolicySetCode,
            PolicySetVersion = exercise.PolicySetVersion,
            SourceConfigurationProfileId = exercise.SourceConfigurationProfileId,
            WorkflowDefinitionId = exercise.WorkflowDefinitionId, WorkflowInstanceId = exercise.WorkflowInstanceId,
            AdvertisementReference = exercise.AdvertisementReference,
            AdvertisementEvidenceReference = exercise.AdvertisementEvidenceReference,
            AdvertisedAtUtc = exercise.AdvertisedAtUtc, ClosedAtUtc = exercise.ClosedAtUtc,
            SubmittedForApprovalAtUtc = exercise.SubmittedForApprovalAtUtc,
            DecisionReference = exercise.DecisionReference,
            DecisionEvidenceReference = exercise.DecisionEvidenceReference,
            DecisionReason = exercise.DecisionReason, DecidedAtUtc = exercise.DecidedAtUtc,
            IntegrityHash = exercise.IntegrityHash, RowVersion = Convert.ToBase64String(exercise.RowVersion),
            Categories = categories.Select(item => new ProcurementPrequalificationCategoryDto
                { Id = item.Id, Code = item.CategoryCode, Name = item.CategoryName }).ToList(),
            Criteria = exercise.Criteria.OrderBy(item => item.SortOrder).Select(MapCriterion).ToList(),
            Applications = exercise.Applications.OrderBy(item => item.SubmittedAtUtc).Select(MapApplication).ToList(),
            QualifiedEntries = exercise.QualifiedEntries.OrderBy(item => item.BusinessPartner.PartnerName)
                .ThenBy(item => item.Category.CategoryName).Select(MapEntry).ToList(),
            Milestones =
            [
                Milestone("DEC-001", "Prequalification scope and criteria", exercise.CreatedAt, exercise.Reference),
                Milestone("DEC-002", "Published advertisement", exercise.AdvertisedAtUtc, exercise.AdvertisementReference),
                Milestone("DEC-003", "Controlled supplier submissions closed", exercise.ClosedAtUtc, $"{exercise.Applications.Count} applications"),
                Milestone("DEC-004", "Criterion-level evaluation complete",
                    exercise.Applications.Count > 0 && exercise.Applications.All(item => item.EvaluatedAtUtc.HasValue)
                        ? exercise.Applications.Max(item => item.EvaluatedAtUtc) : null,
                    $"{exercise.Applications.Count(item => item.Passed == true)} recommended"),
                Milestone("DEC-005", exercise.ApprovalRequired ? "Exact shared approval submitted" : "Decision completed", exercise.SubmittedForApprovalAtUtc, exercise.WorkflowInstanceId?.ToString()),
                Milestone("DEC-006", "Qualified-list decision", exercise.DecidedAtUtc, exercise.DecisionReference),
                Milestone("DEC-007", "Reusable eligibility and expiry", exercise.DecidedAtUtc,
                    exercise.QualifiedEntries.Count > 0 ? exercise.QualifiedEntries.Max(item => item.ExpiresAtUtc).ToString("O") : null)
            ]
        };
    }

    private static ProcurementPrequalificationSummaryDto MapSummary(ProcurementPrequalificationExercise exercise) => new()
    {
        Id = exercise.Id, Reference = exercise.Reference, Title = exercise.Title, Status = exercise.Status,
        ApprovalRequired = exercise.ApprovalRequired,
        OpensAtUtc = exercise.OpensAtUtc, ClosesAtUtc = exercise.ClosesAtUtc,
        ApplicationCount = exercise.Applications.Count(item => !item.IsDeleted),
        QualifiedCount = exercise.QualifiedEntries.Count(item => !item.IsDeleted &&
            item.Status == ProcurementQualifiedListEntryStatus.Active && item.ExpiresAtUtc > DateTime.UtcNow),
        ExpiresAtUtc = exercise.QualifiedEntries.Where(item => !item.IsDeleted)
            .Select(item => (DateTime?)item.ExpiresAtUtc).Max()
    };

    private static ProcurementPrequalificationCriterionDto MapCriterion(ProcurementPrequalificationCriterion item) => new()
    {
        Id = item.Id, Code = item.Code, Name = item.Name, Description = item.Description,
        Weight = item.Weight, MinimumScore = item.MinimumScore, IsMandatory = item.IsMandatory,
        RequiresEvidence = item.RequiresEvidence, SortOrder = item.SortOrder
    };

    private static ProcurementPrequalificationApplicationDto MapApplication(ProcurementPrequalificationApplication item) => new()
    {
        Id = item.Id, ApplicationNumber = item.ApplicationNumber, BusinessPartnerId = item.BusinessPartnerId,
        SupplierName = item.BusinessPartner?.PartnerName ?? string.Empty, Status = item.Status,
        SubmittedAtUtc = item.SubmittedAtUtc, EvaluatedAtUtc = item.EvaluatedAtUtc,
        TotalScore = item.TotalScore, Passed = item.Passed, EvaluationRemarks = item.EvaluationRemarks,
        RecommendationEvidenceReference = item.RecommendationEvidenceReference,
        RowVersion = Convert.ToBase64String(item.RowVersion), CategoryIds = DeserializeGuidList(item.CategoryIdsJson),
        Evidence = EvidenceFrom(item).Select(evidence => new ProcurementPrequalificationEvidenceDto
        {
            CriterionCode = evidence.CriterionCode, EvidenceReference = evidence.EvidenceReference,
            VerificationReference = evidence.VerificationReference
        }).ToList(),
        Scores = item.Scores.OrderBy(score => score.Criterion.SortOrder).Select(score => new ProcurementPrequalificationScoreDto
        {
            CriterionId = score.CriterionId, CriterionCode = score.Criterion.Code,
            CriterionName = score.Criterion.Name, Score = score.Score,
            MeetsRequirement = score.MeetsRequirement, Reason = score.Reason,
            EvidenceReference = score.EvidenceReference, EvaluatedAtUtc = score.EvaluatedAtUtc,
            EvaluatedById = score.EvaluatedById
        }).ToList()
    };

    private static ProcurementQualifiedListEntryDto MapEntry(ProcurementQualifiedListEntry item) => new()
    {
        Id = item.Id, BusinessPartnerId = item.BusinessPartnerId,
        SupplierName = item.BusinessPartner?.PartnerName ?? string.Empty,
        CategoryId = item.CategoryId, CategoryCode = item.Category?.CategoryCode ?? string.Empty,
        CategoryName = item.Category?.CategoryName ?? string.Empty, ValidFromUtc = item.ValidFromUtc,
        ExpiresAtUtc = item.ExpiresAtUtc, Status = item.Status,
        ApprovalReference = item.ApprovalReference, IntegrityHash = item.IntegrityHash
    };

    private async Task EnsureCurrentPolicyLineageAsync(
        ProcurementPrequalificationExercise exercise,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var policy = await PolicySets.GetQueryable(item => item.Id == exercise.PolicySetId &&
                item.TenantId == exercise.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (policy is null ||
            policy.LifecycleStatus != ProcurementPolicyLifecycleStatus.Published ||
            policy.EffectiveFrom > now ||
            (policy.EffectiveTo.HasValue && policy.EffectiveTo.Value < now) ||
            !policy.Code.Equals(exercise.PolicySetCode, StringComparison.Ordinal) ||
            policy.Version != exercise.PolicySetVersion ||
            policy.SourceConfigurationProfileId != exercise.SourceConfigurationProfileId)
            throw Conflict(
                "PREQUAL_POLICY_STALE",
                "The exact source policy is no longer current or its immutable lineage no longer matches. Create a new controlled exercise.");
    }

    private async Task EnsureSupplierActorAsync(
        Guid businessPartnerId, string reference, string correlationId, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.IsExternalUser)
        {
            await EnsureCapabilityAsync(ManagePermission, reference, correlationId, cancellationToken);
            return;
        }
        if (!await PartnerUsers.ExistsAsync(item => item.TenantId == _currentUser.TenantId &&
                item.BusinessPartnerId == businessPartnerId && item.UserId == _currentUser.UserId &&
                item.IsActive && !item.IsDeleted))
            throw new ProcurementPrequalificationAuthorizationException("External users may submit only for their linked supplier account.");
    }

    private async Task EnsureCapabilityAsync(
        string permissionCode, string reference, string correlationId, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (HasPlatformSuperAdministratorBypass()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permissionCode, SourceType = SourceType, SourceReference = reference
        }, correlationId, cancellationToken);
        if (!decision.Allowed) throw new ProcurementPrequalificationAuthorizationException(decision.Message);
    }

    private async Task NotifyApplicantsAsync(
        ProcurementPrequalificationExercise exercise, CancellationToken cancellationToken)
    {
        var partnerIds = exercise.Applications.Where(item => !item.IsDeleted)
            .Select(item => item.BusinessPartnerId).Distinct().ToList();
        var recipients = await PartnerUsers.GetQueryable(item => item.TenantId == exercise.TenantId &&
                partnerIds.Contains(item.BusinessPartnerId) && item.IsActive && !item.IsDeleted)
            .Select(item => item.UserId).Distinct().ToListAsync(cancellationToken);
        foreach (var recipientId in recipients)
            await _notifications.CreateNotificationAsync(new CreateNotificationDto
            {
                RecipientId = recipientId, Type = "ProcurementPrequalification",
                Title = $"Prequalification {exercise.Reference} decision",
                Message = $"The prequalification exercise is {exercise.Status}. Open the retained record for your application result.",
                Priority = "High", EntityType = nameof(ProcurementPrequalificationExercise),
                EntityId = exercise.Id, ActionUrl = $"{ActionUrl}/{exercise.Id}",
                Metadata = new Dictionary<string, object>
                {
                    ["reference"] = exercise.Reference,
                    ["status"] = exercise.Status.ToString(),
                    ["decisionReference"] = exercise.DecisionReference ?? string.Empty
                }
            }, _currentUser.UserId, exercise.TenantId);
    }

    private async Task RecordAsync(
        ProcurementPrequalificationExercise exercise,
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
            EventKey = ProcurementControlEventKey.Create("prequalification", exercise.TenantId, exercise.Id, action, correlationId),
            EventType = "ProcurementPrequalification", Action = action, Result = result,
            RuleCode = RuleCode, DecisionKeys = Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToList(),
            SourceType = SourceType, SourceId = exercise.Id, SourceReference = exercise.Reference,
            Reason = action, InputValues = input, ResultValues = output, Evidence = evidence.ToList(),
            CorrelationId = correlationId, OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private static void CaptureExercise(ProcurementPrequalificationExercise item)
    {
        item.LifecycleSnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = item.ApprovalRequired ? "tdc.prequalification.v1" : "tdc.prequalification.v2.no-approval",
            item.ApprovalRequired, item.Id, item.Reference, item.Title, item.Description,
            item.Status, item.CategoryIdsJson, item.OpensAtUtc, item.ClosesAtUtc, item.ValidityMonths,
            item.PassingScore, item.PolicySetId, item.PolicySetCode, item.PolicySetVersion,
            item.SourceConfigurationProfileId, item.WorkflowDefinitionId, item.WorkflowInstanceId,
            criteria = item.Criteria.OrderBy(criterion => criterion.SortOrder).Select(criterion => new
            {
                criterion.Id, criterion.Code, criterion.Name, criterion.Description, criterion.Weight,
                criterion.MinimumScore, criterion.IsMandatory, criterion.RequiresEvidence, criterion.SortOrder
            }),
            item.AdvertisementReference, item.AdvertisementEvidenceReference, item.AdvertisedAtUtc,
            item.ClosedAtUtc, item.SubmittedForApprovalAtUtc, item.DecisionReference,
            item.DecisionEvidenceReference, item.DecisionReason, item.DecidedAtUtc, item.DecidedById
        }, JsonOptions);
        item.IntegrityHash = ComputeHash(item.LifecycleSnapshotJson);
    }

    private static void CaptureApplication(ProcurementPrequalificationApplication item)
    {
        item.EvaluationSnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.prequalification.application.v1", item.Id, item.ExerciseId,
            item.BusinessPartnerId, item.ApplicationNumber, item.CategoryIdsJson, item.EvidenceJson,
            item.Status, item.SubmittedAtUtc, item.SubmittedById, item.EvaluatedAtUtc,
            item.EvaluatedById, item.TotalScore, item.Passed, item.EvaluationRemarks,
            item.RecommendationEvidenceReference,
            scores = item.Scores.OrderBy(score => score.CriterionId).Select(score => new
            {
                score.CriterionId, score.Score, score.MeetsRequirement, score.Reason,
                score.EvidenceReference, score.EvaluatedAtUtc, score.EvaluatedById
            })
        }, JsonOptions);
        item.IntegrityHash = ComputeHash(item.EvaluationSnapshotJson);
    }

    private static void CaptureEntry(ProcurementQualifiedListEntry item)
    {
        item.LifecycleSnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.qualified-list-entry.v1", item.Id, item.ExerciseId, item.ApplicationId,
            item.BusinessPartnerId, item.CategoryId, item.ValidFromUtc, item.ExpiresAtUtc, item.Status,
            item.ApprovalReference, item.ApprovalEvidenceReference, item.ExpiredAtUtc,
            item.RevokedAtUtc, item.RevocationReason
        }, JsonOptions);
        item.IntegrityHash = ComputeHash(item.LifecycleSnapshotJson);
    }

    private static void TouchExercise(ProcurementPrequalificationExercise item, DateTime now)
    {
        item.UpdatedAt = now;
        CaptureExercise(item);
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser || HasPlatformSuperAdministratorBypass() ||
            _currentUser.HasRegisteredProcurementPermission("procurement.records.read")) return;
        throw new ProcurementPrequalificationAuthorizationException("The procurement records read permission is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementPrequalificationAuthorizationException("An authenticated tenant context is required.");
    }

    private static void EnsureStatus(
        ProcurementPrequalificationExercise exercise, ProcurementPrequalificationStatus expected, string code)
    {
        if (exercise.Status != expected)
            throw Conflict(code, $"This action requires {expected}; current status is {exercise.Status}.");
    }

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw Validation("PREQUAL_ROW_VERSION_INVALID", "RowVersion must be valid base64."); }
        if (!current.SequenceEqual(parsed))
            throw Conflict("PREQUAL_VERSION_CONFLICT", "The prequalification record changed. Reload before continuing.");
    }

    private bool HasPlatformSuperAdministratorBypass() => _currentUser.HasRole(Constants.Roles.SuperAdmin);
    private string ActorName() => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string SerializeGuidList(IEnumerable<Guid> values) => JsonSerializer.Serialize(values.Distinct().OrderBy(value => value).ToList(), JsonOptions);
    private static List<Guid> DeserializeGuidList(string? json) => string.IsNullOrWhiteSpace(json)
        ? new() : JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? new();
    private static List<EvidenceSnapshot> EvidenceFrom(ProcurementPrequalificationApplication item) =>
        JsonSerializer.Deserialize<List<EvidenceSnapshot>>(item.EvidenceJson, JsonOptions) ?? new();
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : Truncate(value.Trim(), 100);
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string ComputeHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void Require(string? value, string code, string message) { if (string.IsNullOrWhiteSpace(value)) throw Validation(code, message); }
    private static ProcurementPrequalificationMilestoneDto Milestone(string code, string label, DateTime? completedAt, string? reference) =>
        new() { Code = code, Label = label, CompletedAtUtc = completedAt, Reference = reference };
    private static ProcurementPrequalificationNotFoundException NotFound(string code, string message) => new(code, message);
    private static ProcurementPrequalificationConflictException Conflict(string code, string message) => new(code, message);
    private static ProcurementPrequalificationValidationException Validation(string code, string message) => new(code, message);
    private static ProcurementControlEventEvidenceReference External(string reference, string label, string requirement) => new()
    {
        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
        Reference = reference, Label = label, RequirementKey = requirement
    };

    private sealed record EvidenceSnapshot(string CriterionCode, string EvidenceReference, string VerificationReference);
}
