using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed class QuantitySurveyEscalationFormulaService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : IQuantitySurveyEscalationFormulaService
{
    private static readonly Regex CodePattern = new("^[A-Z0-9][A-Z0-9_-]{2,39}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value
        : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value
        : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyEscalationLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        var policy = await RequirePoliciesAsync(DateTime.UtcNow, cancellationToken);
        var accessible = (await projectService.LookupProjectsAsync(take: 5000)).Select(value => value.Id).Distinct().ToList();
        var projects = await db.Projects.AsNoTracking()
            .Where(value => value.TenantId == TenantId && accessible.Contains(value.Id) && !value.IsDeleted)
            .OrderBy(value => value.ProjectCode)
            .Select(value => Option(value.Id, value.ProjectCode + " · " + value.Title, value.Status))
            .ToListAsync(cancellationToken);
        var contractLinks = await db.Projects.AsNoTracking()
            .Where(value => value.TenantId == TenantId && accessible.Contains(value.Id) && !value.IsDeleted && value.ContractId.HasValue)
            .Select(value => new { ProjectId = value.Id, ContractId = value.ContractId!.Value })
            .Concat(db.ProjectPackages.AsNoTracking().Where(value => value.TenantId == TenantId && accessible.Contains(value.ProjectId) && !value.IsDeleted && value.ContractId.HasValue).Select(value => new { value.ProjectId, ContractId = value.ContractId!.Value }))
            .Distinct().ToListAsync(cancellationToken);
        var contractIds = contractLinks.Select(value => value.ContractId).Distinct().ToList();
        var contractValues = await db.Contracts.AsNoTracking()
            .Where(value => value.TenantId == TenantId && contractIds.Contains(value.Id) && !value.IsDeleted && value.ContractType == "Works")
            .OrderBy(value => value.ContractNumber)
            .ToListAsync(cancellationToken);
        var contractById = contractValues.ToDictionary(value => value.Id);
        var contracts = contractLinks
            .Where(value => contractById.ContainsKey(value.ContractId))
            .Select(value => Option(value.ContractId, contractById[value.ContractId].ContractNumber + " · " + contractById[value.ContractId].ContractTitle, value.ProjectId.ToString()))
            .OrderBy(value => value.Label)
            .ToList();
        var evidence = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord)
            .Where(value => value.TenantId == TenantId).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .OrderByDescending(value => value.PublishedAt)
            .Where(value => value.DocumentRecord.SourceRecordId.HasValue && contractIds.Contains(value.DocumentRecord.SourceRecordId.Value))
            .Select(value => Option(value.Id, value.DocumentRecord.DocumentReference + " · " + value.DocumentRecord.Title + " · " + value.VersionNumber, value.DocumentRecord.SourceRecordId!.Value.ToString()))
            .Take(500).ToListAsync(cancellationToken);
        var families = await db.QuantitySurveyPriceIndexFamilies.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IsActive && policy.Escalation.IndexSources.Contains(value.Source))
            .OrderBy(value => value.Source).ThenBy(value => value.Code)
            .Select(value => Option(value.Id, value.Code + " · " + value.Name, value.Source.ToString()))
            .ToListAsync(cancellationToken);
        var roles = await db.Roles.AsNoTracking().Where(value => policy.Authority.ApproverRoleIds.Contains(value.Id) && value.Name != null)
            .OrderBy(value => value.Name).Select(value => Option(value.Id, value.Name!, "QS approver authority")).ToListAsync(cancellationToken);
        var formulaTypes = new[] { policy.Escalation.Formula }.Select(value => new QuantitySurveyLookupOptionDto { Value = value.ToString(), Label = SplitWords(value.ToString()), Group = "QS-DEC-006" }).ToList();
        var indexSources = policy.Escalation.IndexSources.Select(value => new QuantitySurveyLookupOptionDto { Value = value.ToString(), Label = SplitWords(value.ToString()), Group = "QS-DEC-006" }).ToList();
        return new QuantitySurveyEscalationLookupsDto
        {
            Sources = new Dictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>>(StringComparer.OrdinalIgnoreCase)
            {
                ["projects"] = projects, ["contracts"] = contracts, ["evidenceDocuments"] = evidence,
                ["indexFamilies"] = families, ["authorityRoles"] = roles, ["formulaTypes"] = formulaTypes,
                ["indexSources"] = indexSources
            },
            Policy = new QuantitySurveyEscalationPolicyDto
            {
                ConfigurationProfileId = policy.Profile.Id,
                ConfigurationDecisionId = policy.Decision.Id,
                FormulaType = policy.Escalation.Formula,
                MaterialCoefficient = policy.Escalation.MaterialCoefficient,
                LabourCoefficient = policy.Escalation.LabourCoefficient,
                PlantCoefficient = policy.Escalation.PlantCoefficient,
                OtherCoefficient = policy.Escalation.OtherCoefficient,
                ImportFormat = policy.Escalation.ImportFormat
            }
        };
    }

    public async Task<IReadOnlyList<QuantitySurveyIndexFamilyDto>> GetIndexFamiliesAsync(QuantitySurveyIndexFamilyListRequest request, CancellationToken cancellationToken = default)
    {
        var query = db.QuantitySurveyPriceIndexFamilies.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted);
        if (!request.IncludeInactive) query = query.Where(value => value.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(value => value.Code.Contains(search) || value.Name.Contains(search) || value.Publisher.Contains(search));
        }
        return await query.OrderBy(value => value.Source).ThenBy(value => value.Code).Select(value => MapFamily(value)).ToListAsync(cancellationToken);
    }

    public async Task<QuantitySurveyIndexFamilyDto> CreateIndexFamilyAsync(SaveQuantitySurveyIndexFamilyRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var policy = await RequirePoliciesAsync(DateTime.UtcNow, cancellationToken);
        var code = NormalizeCode(request.Code);
        var reason = RequireText(request.Reason, "Change reason", 1000);
        if (!policy.Escalation.IndexSources.Contains(request.Source)) throw new QuantitySurveyEscalationValidationException("The selected index source is not permitted by the effective QS-DEC-006 policy.");
        var name = RequireText(request.Name, "Index family name", 160);
        var publisher = RequireText(request.Publisher, "Index publisher", 160);
        var description = Clean(request.Description, 1000);
        var existing = await db.QuantitySurveyPriceIndexFamilies.IgnoreQueryFilters().FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Code == code, cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsDeleted && existing.Name == name && existing.Source == request.Source && existing.Publisher == publisher && existing.Description == description && existing.IsActive == request.IsActive) return MapFamily(existing);
            throw new QuantitySurveyEscalationConflictException("An index family with this code already exists with a different payload.");
        }
        var entity = new QuantitySurveyPriceIndexFamily
        {
            Id = Guid.NewGuid(), TenantId = TenantId, Code = code, Name = name, Source = request.Source,
            Publisher = publisher, Description = description, IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        };
        db.QuantitySurveyPriceIndexFamilies.Add(entity);
        AddAudit(entity.Id, nameof(QuantitySurveyPriceIndexFamily), QuantitySurveyAuditEventMap.CreatePriceIndexFamily, null, new { reason, value = FamilySnapshot(entity) }, correlationId);
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.QuantitySurveyPriceIndexFamilies.IgnoreQueryFilters()
                .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Code == code, cancellationToken);
            if (concurrent is not null && !concurrent.IsDeleted && concurrent.Name == name && concurrent.Source == request.Source && concurrent.Publisher == publisher && concurrent.Description == description && concurrent.IsActive == request.IsActive)
            {
                return MapFamily(concurrent);
            }

            throw new QuantitySurveyEscalationConflictException("An index family with this code was created concurrently with a different payload. Refresh and try again.");
        }
        return MapFamily(entity);
    }

    public async Task<QuantitySurveyIndexFamilyDto> UpdateIndexFamilyAsync(Guid id, SaveQuantitySurveyIndexFamilyRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var policy = await RequirePoliciesAsync(DateTime.UtcNow, cancellationToken);
        var entity = await db.QuantitySurveyPriceIndexFamilies.FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, cancellationToken)
            ?? throw new QuantitySurveyEscalationNotFoundException("The index family was not found.");
        CheckVersion(entity.RowVersion, request.RowVersion, "index family");
        if (!policy.Escalation.IndexSources.Contains(request.Source)) throw new QuantitySurveyEscalationValidationException("The selected index source is not permitted by the effective QS-DEC-006 policy.");
        var code = NormalizeCode(request.Code);
        var reason = RequireText(request.Reason, "Change reason", 1000);
        if (await db.QuantitySurveyPriceIndexFamilies.IgnoreQueryFilters().AnyAsync(value => value.TenantId == TenantId && value.Code == code && value.Id != id, cancellationToken)) throw new QuantitySurveyEscalationConflictException("An index family with this code already exists.");
        if (!request.IsActive && await db.QuantitySurveyEscalationFormulaComponents.AnyAsync(value => value.TenantId == TenantId && value.IndexFamilyId == id && !value.IsDeleted && !value.Formula.IsDeleted && value.Formula.Status != "Retired", cancellationToken))
            throw new QuantitySurveyEscalationConflictException("An index family used by a current formula cannot be deactivated.");
        var before = FamilySnapshot(entity);
        entity.Code = code; entity.Name = RequireText(request.Name, "Index family name", 160); entity.Source = request.Source;
        entity.Publisher = RequireText(request.Publisher, "Index publisher", 160); entity.Description = Clean(request.Description, 1000); entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
        AddAudit(entity.Id, nameof(QuantitySurveyPriceIndexFamily), QuantitySurveyAuditEventMap.UpdatePriceIndexFamily, before, new { reason, value = FamilySnapshot(entity) }, correlationId);
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new QuantitySurveyEscalationConflictException("An index family with this code was created concurrently. Refresh and choose another code.");
        }
        return MapFamily(entity);
    }

    public async Task<QuantitySurveyEscalationFormulaPageDto> GetFormulasAsync(QuantitySurveyEscalationFormulaListRequest request, CancellationToken cancellationToken = default)
    {
        var accessible = (await projectService.LookupProjectsAsync(take: 5000)).Select(value => value.Id).Distinct().ToList();
        var query = FormulaQuery().Where(value => accessible.Contains(value.ProjectId));
        if (request.ProjectId.HasValue) query = query.Where(value => value.ProjectId == request.ProjectId.Value);
        if (request.ContractId.HasValue) query = query.Where(value => value.ContractId == request.ContractId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(value => value.Status == request.Status.Trim());
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim(); query = query.Where(value => value.Code.Contains(search) || value.Name.Contains(search) || value.ContractClauseReference.Contains(search)); }
        var page = Math.Max(1, request.Page); var size = Math.Clamp(request.PageSize, 1, 100); var count = await query.CountAsync(cancellationToken);
        var values = await query.OrderBy(value => value.Code).ThenByDescending(value => value.Version).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new QuantitySurveyEscalationFormulaPageDto { Items = values.Select(MapFormula).ToList(), Page = page, PageSize = size, TotalCount = count };
    }

    public async Task<QuantitySurveyEscalationFormulaDto> GetFormulaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequiredFormulaAsync(id, false, cancellationToken);
        await RequireProjectAccessAsync(entity.ProjectId);
        return MapFormula(entity);
    }

    public async Task<QuantitySurveyEscalationFormulaDto> CreateFormulaAsync(CreateQuantitySurveyEscalationFormulaRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw new QuantitySurveyEscalationValidationException("A client request ID is required for safe retry.");
        var normalized = await ValidateFormulaAsync(request, cancellationToken);
        var requestHash = Hash(JsonSerializer.Serialize(new { request.ClientRequestId, normalized.Code, normalized.Name, request.ProjectId, request.ContractId, normalized.Clause, request.FormulaType, BaseDate = request.BaseDate.Date, EffectiveFrom = request.EffectiveFrom.Date, EffectiveTo = request.EffectiveTo?.Date, request.AuthorityRoleId, request.CentralDocumentVersionId, request.SourceFormulaId, Components = normalized.Components.Select(value => new { value.Component, value.Coefficient, value.Family.Id }), normalized.Reason }, JsonOptions));
        var existing = await FormulaQuery().FirstOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal)) throw new QuantitySurveyEscalationConflictException("This client request ID was already used with a different formula payload.");
            return MapFormula(existing);
        }
        QuantitySurveyEscalationFormulaDefinition? source = null;
        var formulaKey = Guid.NewGuid(); var version = 1;
        if (request.SourceFormulaId.HasValue)
        {
            source = await RequiredFormulaAsync(request.SourceFormulaId.Value, false, cancellationToken);
            await RequireProjectAccessAsync(source.ProjectId);
            if (source.Status != "Approved") throw new QuantitySurveyEscalationConflictException("Only an Approved formula can seed a revision.");
            RequireStableRevisionLineage(source, request.ProjectId, request.ContractId, normalized.Code);
            if (await db.QuantitySurveyEscalationFormulas.AnyAsync(value => value.TenantId == TenantId && value.FormulaKey == source.FormulaKey && !value.IsDeleted && (value.Status == "Draft" || value.Status == "PendingApproval"), cancellationToken)) throw new QuantitySurveyEscalationConflictException("This formula family already has an active draft or pending revision.");
            formulaKey = source.FormulaKey;
            var currentMaximum = await db.QuantitySurveyEscalationFormulas.IgnoreQueryFilters().Where(value => value.TenantId == TenantId && value.FormulaKey == formulaKey).MaxAsync(value => (int?)value.Version, cancellationToken);
            version = (currentMaximum ?? 0) + 1;
        }
        var now = DateTime.UtcNow;
        var entity = new QuantitySurveyEscalationFormulaDefinition
        {
            Id = Guid.NewGuid(), TenantId = TenantId, FormulaKey = formulaKey, Code = normalized.Code, Name = normalized.Name, Version = version,
            ProjectId = request.ProjectId, ContractId = request.ContractId, ContractClauseReference = normalized.Clause, FormulaType = request.FormulaType,
            BaseDate = request.BaseDate.Date, EffectiveFrom = request.EffectiveFrom.Date, EffectiveTo = request.EffectiveTo?.Date,
            AuthorityRoleId = normalized.Role.Id, AuthorityRoleNameSnapshot = normalized.Role.Name!, ConfigurationProfileId = normalized.Policy.Profile.Id,
            ConfigurationDecisionId = normalized.Policy.Decision.Id, ApprovalWorkflowDefinitionId = normalized.Policy.Escalation.ApprovalWorkflowDefinitionId,
            CentralDocumentRecordId = normalized.Evidence.DocumentRecordId, CentralDocumentVersionId = normalized.Evidence.Id, SupersedesFormulaId = source?.Id,
            ClientRequestId = request.ClientRequestId, RequestHash = requestHash, Status = "Draft", ApprovalStatus = "Draft", PreparedById = UserId,
            PreparedAt = now, AuditAction = QuantitySurveyAuditEventMap.CreateEscalationFormula, CorrelationId = NormalizeCorrelation(correlationId), ChangeReason = normalized.Reason,
            ActorRoles = ActorRoles, CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        entity.Components = normalized.Components.Select((value, index) => new QuantitySurveyEscalationFormulaComponent
        {
            Id = Guid.NewGuid(), TenantId = TenantId, FormulaId = entity.Id, Sequence = index + 1, Component = value.Component, Coefficient = value.Coefficient,
            IndexFamilyId = value.Family.Id, IndexSourceSnapshot = value.Family.Source, IndexFamilyCodeSnapshot = value.Family.Code, IndexFamilyNameSnapshot = value.Family.Name,
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        }).ToList();
        entity.SnapshotHash = ComputeSnapshot(entity);
        db.QuantitySurveyEscalationFormulas.Add(entity);
        AddRevision(entity, QuantitySurveyAuditEventMap.CreateEscalationFormula, normalized.Reason, null, FormulaSnapshot(entity), correlationId);
        AddAudit(entity.Id, nameof(QuantitySurveyEscalationFormulaDefinition), QuantitySurveyAuditEventMap.CreateEscalationFormula, null, FormulaSnapshot(entity), correlationId);
        try { await SaveAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            db.ChangeTracker.Clear();
            var retry = await FormulaQuery().FirstOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, cancellationToken);
            if (retry is null) throw new QuantitySurveyEscalationConflictException("This formula family already has an active draft or a duplicate version. Refresh and try again.");
            if (!string.Equals(retry.RequestHash, requestHash, StringComparison.Ordinal)) throw new QuantitySurveyEscalationConflictException("This client request ID was already used with a different formula payload.");
            return MapFormula(retry);
        }
        return await GetFormulaAsync(entity.Id, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationFormulaDto> UpdateFormulaAsync(Guid id, UpdateQuantitySurveyEscalationFormulaRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var entity = await RequiredFormulaAsync(id, true, cancellationToken); await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.Status is not ("Draft" or "Rejected")) throw new QuantitySurveyEscalationConflictException("Only a Draft or Rejected formula can be amended.");
        CheckVersion(entity.RowVersion, request.RowVersion, "formula");
        if (request.SourceFormulaId != entity.SupersedesFormulaId) throw new QuantitySurveyEscalationValidationException("The source formula of an existing version cannot be changed.");
        var normalized = await ValidateFormulaAsync(request, cancellationToken); var before = FormulaSnapshot(entity); var now = DateTime.UtcNow;
        if (entity.SupersedesFormulaId.HasValue)
        {
            var source = await RequiredFormulaAsync(entity.SupersedesFormulaId.Value, false, cancellationToken);
            RequireStableRevisionLineage(source, request.ProjectId, request.ContractId, normalized.Code);
        }
        entity.Code = normalized.Code; entity.Name = normalized.Name; entity.ProjectId = request.ProjectId; entity.ContractId = request.ContractId;
        entity.ContractClauseReference = normalized.Clause; entity.FormulaType = request.FormulaType; entity.BaseDate = request.BaseDate.Date; entity.EffectiveFrom = request.EffectiveFrom.Date; entity.EffectiveTo = request.EffectiveTo?.Date;
        entity.AuthorityRoleId = normalized.Role.Id; entity.AuthorityRoleNameSnapshot = normalized.Role.Name!; entity.ConfigurationProfileId = normalized.Policy.Profile.Id; entity.ConfigurationDecisionId = normalized.Policy.Decision.Id;
        entity.ApprovalWorkflowDefinitionId = normalized.Policy.Escalation.ApprovalWorkflowDefinitionId; entity.CentralDocumentRecordId = normalized.Evidence.DocumentRecordId; entity.CentralDocumentVersionId = normalized.Evidence.Id;
        entity.Status = "Draft"; entity.ApprovalStatus = "Draft"; entity.RejectionReason = null; entity.WorkflowInstanceId = null; entity.SubmittedAt = null; entity.SubmittedById = null;
        entity.AuditAction = QuantitySurveyAuditEventMap.UpdateEscalationFormula; entity.CorrelationId = NormalizeCorrelation(correlationId); entity.ChangeReason = normalized.Reason; entity.ActorRoles = ActorRoles; entity.UpdatedAt = now; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
        db.QuantitySurveyEscalationFormulaComponents.RemoveRange(entity.Components);
        entity.Components = normalized.Components.Select((value, index) => new QuantitySurveyEscalationFormulaComponent { Id = Guid.NewGuid(), TenantId = TenantId, FormulaId = entity.Id, Sequence = index + 1, Component = value.Component, Coefficient = value.Coefficient, IndexFamilyId = value.Family.Id, IndexSourceSnapshot = value.Family.Source, IndexFamilyCodeSnapshot = value.Family.Code, IndexFamilyNameSnapshot = value.Family.Name, CreatedAt = now, CreatedBy = UserName, CreatedById = UserId }).ToList();
        entity.SnapshotHash = ComputeSnapshot(entity); AddRevision(entity, QuantitySurveyAuditEventMap.UpdateEscalationFormula, normalized.Reason, before, FormulaSnapshot(entity), correlationId); AddAudit(entity.Id, nameof(QuantitySurveyEscalationFormulaDefinition), QuantitySurveyAuditEventMap.UpdateEscalationFormula, before, FormulaSnapshot(entity), correlationId);
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new QuantitySurveyEscalationConflictException("The formula update conflicts with an existing formula family or version. Refresh and try again.");
        }
        return await GetFormulaAsync(entity.Id, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationFormulaDto> SubmitFormulaAsync(Guid id, QuantitySurveyEscalationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var entity = await RequiredFormulaAsync(id, true, cancellationToken); await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.Status is "PendingApproval" or "Approved") return MapFormula(entity);
        CheckVersion(entity.RowVersion, request.RowVersion, "formula");
        if (entity.Status is not ("Draft" or "Rejected")) throw new QuantitySurveyEscalationConflictException("Only a Draft or Rejected formula can be submitted.");
        var reason = RequireText(request.Reason, "Submission reason", 1000); await ValidateStoredAsync(entity, cancellationToken); var before = FormulaSnapshot(entity);
        var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, entity.ApprovalWorkflowDefinitionId);
        if (!result.ExecutionResult.Success) throw new QuantitySurveyEscalationConflictException(result.ExecutionResult.Message ?? "The configured escalation workflow could not be started.");
        workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Escalation).ApplySubmitOutcome(entity, result.Outcome, UserId);
        if (result.Outcome == WorkflowOutcome.Approved) { entity.Status = "PendingApproval"; entity.ApprovalStatus = "Pending"; entity.ApprovedById = null; entity.ApprovedAt = null; }
        entity.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId; entity.SubmittedById = UserId; entity.SubmittedAt = DateTime.UtcNow; Touch(entity, QuantitySurveyAuditEventMap.SubmitEscalationFormula, correlationId, reason);
        AddRevision(entity, QuantitySurveyAuditEventMap.SubmitEscalationFormula, reason, before, FormulaSnapshot(entity), correlationId); AddAudit(entity.Id, nameof(QuantitySurveyEscalationFormulaDefinition), QuantitySurveyAuditEventMap.SubmitEscalationFormula, before, FormulaSnapshot(entity), correlationId); await SaveAsync(cancellationToken);
        return await GetFormulaAsync(id, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationFormulaDto> ApproveFormulaAsync(Guid id, QuantitySurveyEscalationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var entity = await RequiredFormulaAsync(id, true, cancellationToken); await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.Status == "Approved") return MapFormula(entity);
        CheckVersion(entity.RowVersion, request.RowVersion, "formula");
        if (entity.Status != "PendingApproval") throw new QuantitySurveyEscalationConflictException("The formula must be PendingApproval before approval.");
        if (entity.PreparedById == UserId) throw new QuantitySurveyEscalationConflictException("Maker-checker control prevents the formula preparer from approving it.");
        var reason = RequireText(request.Reason, "Approval reason", 1000); await ValidateStoredAsync(entity, cancellationToken); var status = await WorkflowStatusAsync(entity, cancellationToken); WorkflowOutcome outcome;
        if (status == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
        else
        {
            if (status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) throw new QuantitySurveyEscalationConflictException("The formula workflow ended without approval.");
            if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId)) throw new UnauthorizedAccessException("You are not assigned to the current formula approval step.");
            var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId, "Approve", reason);
            if (!result.ExecutionResult.Success) throw new QuantitySurveyEscalationConflictException(result.ExecutionResult.Message ?? "The formula approval could not be processed."); outcome = result.Outcome;
        }
        if (outcome == WorkflowOutcome.Approved) await FinalizeApprovalAsync(entity, reason, correlationId, cancellationToken);
        return await GetFormulaAsync(id, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationFormulaDto> RejectFormulaAsync(Guid id, QuantitySurveyEscalationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var entity = await RequiredFormulaAsync(id, true, cancellationToken); await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.Status == "Rejected") return MapFormula(entity);
        CheckVersion(entity.RowVersion, request.RowVersion, "formula");
        if (entity.Status != "PendingApproval") throw new QuantitySurveyEscalationConflictException("The formula must be PendingApproval before rejection.");
        var reason = RequireText(request.Reason, "Rejection reason", 1000); var status = await WorkflowStatusAsync(entity, cancellationToken);
        if (status == WorkflowInstanceStatus.Completed) throw new QuantitySurveyEscalationConflictException("The completed formula workflow is approved and cannot be rejected.");
        WorkflowOutcome outcome;
        if (status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) outcome = WorkflowOutcome.Rejected;
        else { if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId)) throw new UnauthorizedAccessException("You are not assigned to the current formula approval step."); var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId, "Reject", reason); if (!result.ExecutionResult.Success) throw new QuantitySurveyEscalationConflictException(result.ExecutionResult.Message ?? "The formula rejection could not be processed."); outcome = result.Outcome; }
        if (outcome != WorkflowOutcome.Rejected) throw new QuantitySurveyEscalationConflictException("The shared workflow did not return a rejected outcome.");
        var before = FormulaSnapshot(entity); workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Escalation).ApplyApprovalOutcome(entity, outcome, UserId, reason); Touch(entity, QuantitySurveyAuditEventMap.RejectEscalationFormula, correlationId, reason);
        AddRevision(entity, QuantitySurveyAuditEventMap.RejectEscalationFormula, reason, before, FormulaSnapshot(entity), correlationId); AddAudit(entity.Id, nameof(QuantitySurveyEscalationFormulaDefinition), QuantitySurveyAuditEventMap.RejectEscalationFormula, before, FormulaSnapshot(entity), correlationId); await SaveAsync(cancellationToken); return await GetFormulaAsync(id, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationFormulaDto> RetireFormulaAsync(Guid id, QuantitySurveyEscalationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var entity = await RequiredFormulaAsync(id, true, cancellationToken); await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.Status == "Retired") return MapFormula(entity);
        CheckVersion(entity.RowVersion, request.RowVersion, "formula");
        if (entity.Status != "Approved") throw new QuantitySurveyEscalationConflictException("Only an Approved formula can be retired."); if (entity.PreparedById == UserId) throw new QuantitySurveyEscalationConflictException("Maker-checker control prevents the formula preparer from retiring it."); RequireAuthorityRole(entity);
        var reason = RequireText(request.Reason, "Retirement reason", 1000); var before = FormulaSnapshot(entity); entity.Status = "Retired"; entity.RetiredById = UserId; entity.RetiredAt = DateTime.UtcNow; Touch(entity, QuantitySurveyAuditEventMap.RetireEscalationFormula, correlationId, reason);
        AddRevision(entity, QuantitySurveyAuditEventMap.RetireEscalationFormula, reason, before, FormulaSnapshot(entity), correlationId); AddAudit(entity.Id, nameof(QuantitySurveyEscalationFormulaDefinition), QuantitySurveyAuditEventMap.RetireEscalationFormula, before, FormulaSnapshot(entity), correlationId); await SaveAsync(cancellationToken); return await GetFormulaAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<QuantitySurveyEscalationRevisionDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequiredFormulaAsync(id, false, cancellationToken); await RequireProjectAccessAsync(entity.ProjectId);
        return await db.QuantitySurveyEscalationFormulaRevisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.FormulaId == id && !value.IsDeleted).OrderByDescending(value => value.CreatedAt)
            .Select(value => new QuantitySurveyEscalationRevisionDto { Id = value.Id, Action = value.Action, ActorUserId = value.ActorUserId, ActorName = value.ActorName, ActorRoles = value.ActorRoles, CorrelationId = value.CorrelationId, Reason = value.Reason, BeforeJson = value.BeforeJson, AfterJson = value.AfterJson, CreatedAt = value.CreatedAt }).ToListAsync(cancellationToken);
    }

    private async Task FinalizeApprovalAsync(QuantitySurveyEscalationFormulaDefinition entity, string reason, string correlationId, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var current = await RequiredFormulaAsync(entity.Id, true, cancellationToken);
            if (current.Status == "Approved") { await transaction.CommitAsync(cancellationToken); return; }
            if (current.PreparedById == UserId) throw new QuantitySurveyEscalationConflictException("Maker-checker control prevents the formula preparer from approving it.");
            RequireAuthorityRole(current);
            await ValidateStoredAsync(current, cancellationToken);
            var otherFamiliesOverlap = await db.QuantitySurveyEscalationFormulas.AnyAsync(value => value.TenantId == TenantId && !value.IsDeleted && value.Id != current.Id && value.FormulaKey != current.FormulaKey && value.ProjectId == current.ProjectId && value.ContractId == current.ContractId && value.Code == current.Code && value.Status == "Approved" && value.EffectiveFrom <= (current.EffectiveTo ?? DateTime.MaxValue) && (value.EffectiveTo ?? DateTime.MaxValue) >= current.EffectiveFrom, cancellationToken);
            if (otherFamiliesOverlap) throw new QuantitySurveyEscalationConflictException("Another Approved formula with this code overlaps the selected project, contract, and effective period.");
            var before = FormulaSnapshot(current); workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Escalation).ApplyApprovalOutcome(current, WorkflowOutcome.Approved, UserId, null); Touch(current, QuantitySurveyAuditEventMap.ApproveEscalationFormula, correlationId, reason);
            AddRevision(current, QuantitySurveyAuditEventMap.ApproveEscalationFormula, reason, before, FormulaSnapshot(current), correlationId); AddAudit(current.Id, nameof(QuantitySurveyEscalationFormulaDefinition), QuantitySurveyAuditEventMap.ApproveEscalationFormula, before, FormulaSnapshot(current), correlationId); await SaveAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task<ValidatedFormula> ValidateFormulaAsync(CreateQuantitySurveyEscalationFormulaRequest request, CancellationToken cancellationToken)
    {
        var policy = await RequirePoliciesAsync(request.EffectiveFrom, cancellationToken); var reason = RequireText(request.Reason, "Change reason", 1000); var code = NormalizeCode(request.Code); var name = RequireText(request.Name, "Formula name", 200); var clause = RequireText(request.ContractClauseReference, "Contract clause reference", 120);
        if (request.ProjectId == Guid.Empty || request.ContractId == Guid.Empty) throw new QuantitySurveyEscalationValidationException("Select a project and its controlled contract.");
        await RequireProjectAccessAsync(request.ProjectId);
        var linked = await db.Projects.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == request.ProjectId && !value.IsDeleted && value.ContractId == request.ContractId, cancellationToken)
            || await db.ProjectPackages.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == request.ProjectId && !value.IsDeleted && value.ContractId == request.ContractId, cancellationToken);
        if (!linked) throw new QuantitySurveyEscalationValidationException("The selected contract is not linked to the selected project or one of its work packages.");
        var contract = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.ContractId && !value.IsDeleted, cancellationToken) ?? throw new QuantitySurveyEscalationValidationException("The selected contract is not available in this tenant.");
        if (!string.Equals(contract.ContractType, "Works", StringComparison.OrdinalIgnoreCase)) throw new QuantitySurveyEscalationValidationException("Price-adjustment formulas can be registered only for a Works contract.");
        if (request.FormulaType != policy.Escalation.Formula) throw new QuantitySurveyEscalationValidationException("The formula type does not match the effective QS-DEC-006 policy.");
        if (request.BaseDate == default || request.EffectiveFrom == default || request.BaseDate.Date > request.EffectiveFrom.Date) throw new QuantitySurveyEscalationValidationException("Base date must be on or before the effective date.");
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value.Date < request.EffectiveFrom.Date) throw new QuantitySurveyEscalationValidationException("Effective-to date cannot precede effective-from date.");
        if (request.EffectiveFrom.Date < policy.Profile.EffectiveFrom.Date || (policy.Profile.EffectiveTo.HasValue && (request.EffectiveTo ?? request.EffectiveFrom).Date > policy.Profile.EffectiveTo.Value.Date)) throw new QuantitySurveyEscalationValidationException("The formula effective period must remain inside the effective QS profile period.");
        if (!policy.Authority.ApproverRoleIds.Contains(request.AuthorityRoleId)) throw new QuantitySurveyEscalationValidationException("Select an approval authority permitted by QS-DEC-001.");
        var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(value => value.Id == request.AuthorityRoleId && value.Name != null, cancellationToken) ?? throw new QuantitySurveyEscalationValidationException("The selected approval authority role is unavailable.");
        var evidence = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(value => value.TenantId == TenantId && value.Id == request.CentralDocumentVersionId).Where(CentralDocumentEvidenceRules.CurrentPublished()).SingleOrDefaultAsync(cancellationToken) ?? throw new QuantitySurveyEscalationValidationException("Select a current Published central-DMS contract document version.");
        if (evidence.DocumentRecord.SourceRecordId != request.ContractId) throw new QuantitySurveyEscalationValidationException("The selected DMS evidence is not linked to the selected contract.");
        var componentError = QuantitySurveyEscalationFormulaRules.ValidatePolicyComponents(request.Components.Select(value => (value.Component, value.Coefficient)), policy.Escalation);
        if (componentError is not null) throw new QuantitySurveyEscalationValidationException(componentError);
        var familyIds = request.Components.Select(value => value.IndexFamilyId).Distinct().ToList(); if (familyIds.Any(value => value == Guid.Empty)) throw new QuantitySurveyEscalationValidationException("Select a controlled index family for every component.");
        var families = await db.QuantitySurveyPriceIndexFamilies.AsNoTracking().Where(value => value.TenantId == TenantId && familyIds.Contains(value.Id) && !value.IsDeleted && value.IsActive).ToDictionaryAsync(value => value.Id, cancellationToken);
        if (families.Count != familyIds.Count) throw new QuantitySurveyEscalationValidationException("One or more selected index families are inactive or outside the tenant.");
        var components = request.Components.OrderBy(value => (int)value.Component).Select(value => { var family = families[value.IndexFamilyId]; if (!policy.Escalation.IndexSources.Contains(family.Source)) throw new QuantitySurveyEscalationValidationException($"Index source {family.Source} is not permitted by QS-DEC-006."); return new ValidatedComponent(value.Component, value.Coefficient, family); }).ToList();
        return new ValidatedFormula(code, name, clause, reason, policy, role, evidence, components);
    }

    private async Task ValidateStoredAsync(QuantitySurveyEscalationFormulaDefinition entity, CancellationToken cancellationToken)
    {
        var policy = await RequirePoliciesAsync(entity.EffectiveFrom, cancellationToken); if (entity.ConfigurationProfileId != policy.Profile.Id || entity.ConfigurationDecisionId != policy.Decision.Id || entity.ApprovalWorkflowDefinitionId != policy.Escalation.ApprovalWorkflowDefinitionId) throw new QuantitySurveyEscalationConflictException("The effective QS escalation policy changed. Amend the draft before submitting or approving it.");
        var linked = await db.Projects.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == entity.ProjectId && !value.IsDeleted && value.ContractId == entity.ContractId, cancellationToken)
            || await db.ProjectPackages.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == entity.ProjectId && !value.IsDeleted && value.ContractId == entity.ContractId, cancellationToken);
        if (!linked) throw new QuantitySurveyEscalationConflictException("The formula contract is no longer linked to the selected project or work package.");
        var contractCurrent = await db.Contracts.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == entity.ContractId && !value.IsDeleted && value.ContractType == "Works", cancellationToken);
        if (!contractCurrent) throw new QuantitySurveyEscalationConflictException("The selected Works contract is no longer available.");
        if (!policy.Authority.ApproverRoleIds.Contains(entity.AuthorityRoleId) || !await db.Roles.AsNoTracking().AnyAsync(value => value.Id == entity.AuthorityRoleId && value.Name == entity.AuthorityRoleNameSnapshot, cancellationToken)) throw new QuantitySurveyEscalationConflictException("The formula approval authority is no longer permitted by QS-DEC-001.");
        var evidenceCurrent = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(value => value.TenantId == TenantId && value.Id == entity.CentralDocumentVersionId && value.DocumentRecord.SourceRecordId == entity.ContractId).Where(CentralDocumentEvidenceRules.CurrentPublished()).AnyAsync(cancellationToken); if (!evidenceCurrent) throw new QuantitySurveyEscalationConflictException("The formula contract evidence is no longer the current Published DMS version linked to the contract.");
        var componentError = QuantitySurveyEscalationFormulaRules.ValidatePolicyComponents(entity.Components.Select(value => (value.Component, value.Coefficient)), policy.Escalation);
        if (componentError is not null || !string.Equals(entity.SnapshotHash, ComputeSnapshot(entity), StringComparison.Ordinal)) throw new QuantitySurveyEscalationConflictException(componentError ?? "The formula snapshot failed its integrity check.");
        var familyIds = entity.Components.Select(value => value.IndexFamilyId).Distinct().ToList();
        var families = await db.QuantitySurveyPriceIndexFamilies.AsNoTracking().Where(value => value.TenantId == TenantId && familyIds.Contains(value.Id) && !value.IsDeleted && value.IsActive).ToDictionaryAsync(value => value.Id, cancellationToken);
        if (families.Count != familyIds.Count || entity.Components.Any(value => !families.TryGetValue(value.IndexFamilyId, out var family) || family.Source != value.IndexSourceSnapshot || !policy.Escalation.IndexSources.Contains(family.Source))) throw new QuantitySurveyEscalationConflictException("An index family or source no longer matches QS-DEC-006.");
    }

    private async Task<PolicyContext> RequirePoliciesAsync(DateTime effectiveAt, CancellationToken cancellationToken)
    {
        var date = effectiveAt.Date;
        var profile = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && value.EffectiveFrom <= date && (!value.EffectiveTo.HasValue || value.EffectiveTo >= date)).OrderByDescending(value => value.EffectiveFrom).ThenByDescending(value => value.Version).FirstOrDefaultAsync(cancellationToken)
            ?? throw new QuantitySurveyEscalationValidationException("No Published QS configuration profile is effective for the selected date.");
        var decisions = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProfileId == profile.Id && !value.IsDeleted && (value.DecisionKey == "QS-DEC-001" || value.DecisionKey == "QS-DEC-006") && value.Status == QuantitySurveyConfigurationDecisionStatus.Approved && value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved && value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified).ToListAsync(cancellationToken);
        var authorityDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-001") ?? throw new QuantitySurveyEscalationValidationException("The effective profile has no approved QS-DEC-001 authority decision.");
        var escalationDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-006") ?? throw new QuantitySurveyEscalationValidationException("The effective profile has no approved QS-DEC-006 escalation decision.");
        var authority = JsonSerializer.Deserialize<QsRolesAuthorityValue>(authorityDecision.ValueJson, JsonOptions) ?? throw new QuantitySurveyEscalationValidationException("QS-DEC-001 could not be read.");
        var escalation = JsonSerializer.Deserialize<QsEscalationValue>(escalationDecision.ValueJson, JsonOptions) ?? throw new QuantitySurveyEscalationValidationException("QS-DEC-006 could not be read.");
        var workflowDefinitionValid = await db.WorkflowDefinitions.AsNoTracking().Include(value => value.EntityType).AnyAsync(value => value.TenantId == TenantId && value.Id == escalation.ApprovalWorkflowDefinitionId && !value.IsDeleted && value.IsActive && value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !value.EntityType.IsDeleted && value.EntityType.IsActive && value.EntityType.Code == QuantitySurveyWorkflowBindingRegistry.Escalation, cancellationToken);
        if (!workflowDefinitionValid) throw new QuantitySurveyEscalationValidationException("QS-DEC-006 must reference an active Published QS escalation workflow definition.");
        return new PolicyContext(profile, escalationDecision, escalation, authority);
    }

    private IQueryable<QuantitySurveyEscalationFormulaDefinition> FormulaQuery() => db.QuantitySurveyEscalationFormulas.AsNoTracking().Include(value => value.Project).Include(value => value.Contract).Include(value => value.CentralDocumentVersion).ThenInclude(value => value.DocumentRecord).Include(value => value.Components).Where(value => value.TenantId == TenantId && !value.IsDeleted);
    private async Task<QuantitySurveyEscalationFormulaDefinition> RequiredFormulaAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.QuantitySurveyEscalationFormulas.AsTracking() : db.QuantitySurveyEscalationFormulas.AsNoTracking();
        return await query.Include(value => value.Project).Include(value => value.Contract).Include(value => value.CentralDocumentVersion).ThenInclude(value => value.DocumentRecord).Include(value => value.Components).FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, cancellationToken) ?? throw new QuantitySurveyEscalationNotFoundException("The escalation formula was not found.");
    }
    private async Task RequireProjectAccessAsync(Guid projectId) { if (!await projectService.HasProjectAccessAsync(projectId)) throw new UnauthorizedAccessException("You are not permitted to access the selected project."); }
    private void RequireAuthorityRole(QuantitySurveyEscalationFormulaDefinition entity) { if (!currentUser.Roles.Any(value => string.Equals(value, entity.AuthorityRoleNameSnapshot, StringComparison.OrdinalIgnoreCase))) throw new UnauthorizedAccessException("Your assigned roles do not include the configured formula approval authority."); }
    private async Task<WorkflowInstanceStatus?> WorkflowStatusAsync(QuantitySurveyEscalationFormulaDefinition entity, CancellationToken cancellationToken) => !entity.WorkflowInstanceId.HasValue ? null : (await db.WorkflowInstances.AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == entity.WorkflowInstanceId && value.EntityId == entity.Id, cancellationToken))?.Status;
    private void AddRevision(QuantitySurveyEscalationFormulaDefinition entity, string action, string? reason, object? before, object? after, string correlationId) => db.QuantitySurveyEscalationFormulaRevisions.Add(new QuantitySurveyEscalationFormulaRevision { Id = Guid.NewGuid(), TenantId = TenantId, FormulaId = entity.Id, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = NormalizeCorrelation(correlationId), Reason = Clean(reason, 1000), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private void AddAudit(Guid id, string resource, string action, object? before, object? after, string correlationId) => db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = resource, ResourceId = id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = after is null ? null : JsonSerializer.Serialize(new { correlationId = NormalizeCorrelation(correlationId), value = after }, JsonOptions), IpAddress = "api", UserAgent = "QS-0301", Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private void Touch(QuantitySurveyEscalationFormulaDefinition entity, string action, string correlationId, string reason) { entity.AuditAction = action; entity.CorrelationId = NormalizeCorrelation(correlationId); entity.ChangeReason = reason; entity.ActorRoles = ActorRoles; entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId; }
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new QuantitySurveyEscalationConflictException("The record changed after it was loaded. Refresh and try again.");
        }
        catch (DbUpdateException exception) when (TryGetSqlException(exception, out var sqlException) && sqlException.Number is >= 51020 and <= 51029)
        {
            throw new QuantitySurveyEscalationConflictException(DatabaseConflictMessage(sqlException.Number));
        }
    }

    private static void RequireStableRevisionLineage(QuantitySurveyEscalationFormulaDefinition source, Guid projectId, Guid contractId, string code)
    {
        if (source.ProjectId != projectId || source.ContractId != contractId || !string.Equals(source.Code, code, StringComparison.Ordinal))
        {
            throw new QuantitySurveyEscalationValidationException("A formula revision must retain the source formula's project, Works contract, and code.");
        }
    }

    private static QuantitySurveyLookupOptionDto Option(Guid id, string label, string? group) => new() { Value = id.ToString(), Label = label, Group = group };
    private static QuantitySurveyIndexFamilyDto MapFamily(QuantitySurveyPriceIndexFamily value) => new() { Id = value.Id, Code = value.Code, Name = value.Name, Source = value.Source, Publisher = value.Publisher, Description = value.Description, IsActive = value.IsActive, RowVersion = Convert.ToBase64String(value.RowVersion) };
    private static QuantitySurveyEscalationFormulaDto MapFormula(QuantitySurveyEscalationFormulaDefinition value) => new() { Id = value.Id, FormulaKey = value.FormulaKey, Code = value.Code, Name = value.Name, Version = value.Version, ProjectId = value.ProjectId, ProjectCode = value.Project.ProjectCode, ProjectName = value.Project.Title, ContractId = value.ContractId, ContractNumber = value.Contract.ContractNumber, ContractTitle = value.Contract.ContractTitle, ContractClauseReference = value.ContractClauseReference, FormulaType = value.FormulaType, BaseDate = value.BaseDate, EffectiveFrom = value.EffectiveFrom, EffectiveTo = value.EffectiveTo, AuthorityRoleId = value.AuthorityRoleId, AuthorityRoleName = value.AuthorityRoleNameSnapshot, ConfigurationProfileId = value.ConfigurationProfileId, ConfigurationDecisionId = value.ConfigurationDecisionId, ApprovalWorkflowDefinitionId = value.ApprovalWorkflowDefinitionId, CentralDocumentRecordId = value.CentralDocumentRecordId, CentralDocumentVersionId = value.CentralDocumentVersionId, EvidenceLabel = value.CentralDocumentVersion.DocumentRecord.DocumentReference + " · " + value.CentralDocumentVersion.DocumentRecord.Title + " · " + value.CentralDocumentVersion.VersionNumber, SupersedesFormulaId = value.SupersedesFormulaId, Status = value.Status, ApprovalStatus = value.ApprovalStatus, PreparedById = value.PreparedById, PreparedAt = value.PreparedAt, SubmittedById = value.SubmittedById, SubmittedAt = value.SubmittedAt, ApprovedById = value.ApprovedById, ApprovedAt = value.ApprovedAt, RejectionReason = value.RejectionReason, SnapshotHash = value.SnapshotHash, RowVersion = Convert.ToBase64String(value.RowVersion), Components = value.Components.OrderBy(item => item.Sequence).Select(item => new QuantitySurveyEscalationFormulaComponentDto { Id = item.Id, Sequence = item.Sequence, Component = item.Component, Coefficient = item.Coefficient, IndexFamilyId = item.IndexFamilyId, IndexSource = item.IndexSourceSnapshot, IndexFamilyCode = item.IndexFamilyCodeSnapshot, IndexFamilyName = item.IndexFamilyNameSnapshot }).ToList() };
    private static object FamilySnapshot(QuantitySurveyPriceIndexFamily value) => new { value.Id, value.Code, value.Name, value.Source, value.Publisher, value.Description, value.IsActive };
    private static object FormulaSnapshot(QuantitySurveyEscalationFormulaDefinition value) => new { value.Id, value.FormulaKey, value.Code, value.Name, value.Version, value.ProjectId, value.ContractId, value.ContractClauseReference, value.FormulaType, value.BaseDate, value.EffectiveFrom, value.EffectiveTo, value.AuthorityRoleId, value.ConfigurationProfileId, value.ConfigurationDecisionId, value.ApprovalWorkflowDefinitionId, value.CentralDocumentVersionId, value.SupersedesFormulaId, value.Status, value.ApprovalStatus, value.PreparedById, value.SubmittedById, value.ApprovedById, value.ApprovedAt, value.SnapshotHash, Components = value.Components.OrderBy(item => item.Sequence).Select(item => new { item.Sequence, item.Component, item.Coefficient, item.IndexFamilyId, item.IndexSourceSnapshot, item.IndexFamilyCodeSnapshot, item.IndexFamilyNameSnapshot }) };
    private static object IntegritySnapshot(QuantitySurveyEscalationFormulaDefinition value) => new { value.FormulaKey, value.Code, value.Name, value.Version, value.ProjectId, value.ContractId, value.ContractClauseReference, value.FormulaType, value.BaseDate, value.EffectiveFrom, value.EffectiveTo, value.AuthorityRoleId, value.AuthorityRoleNameSnapshot, value.ConfigurationProfileId, value.ConfigurationDecisionId, value.ApprovalWorkflowDefinitionId, value.CentralDocumentRecordId, value.CentralDocumentVersionId, value.SupersedesFormulaId, Components = value.Components.OrderBy(item => item.Sequence).Select(item => new { item.Sequence, item.Component, item.Coefficient, item.IndexFamilyId, item.IndexSourceSnapshot, item.IndexFamilyCodeSnapshot, item.IndexFamilyNameSnapshot }) };
    private static string ComputeSnapshot(QuantitySurveyEscalationFormulaDefinition value) => Hash(JsonSerializer.Serialize(IntegritySnapshot(value), JsonOptions));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static bool IsUniqueConstraintViolation(DbUpdateException exception) => exception.InnerException is SqlException { Number: 2601 or 2627 };
    private static bool TryGetSqlException(DbUpdateException exception, out SqlException sqlException)
    {
        sqlException = exception.InnerException as SqlException ?? null!;
        return sqlException is not null;
    }
    private static string DatabaseConflictMessage(int number) => number switch
    {
        51020 => "A price-adjustment formula must begin as a Draft.",
        51021 => "The requested formula lifecycle transition is not permitted.",
        51022 => "The formula no longer matches its tenant, project, Works contract, policy, authority, workflow, DMS evidence, or revision lineage. Refresh and review the controlled selections.",
        51023 => "The formula must contain the four current policy coefficients and controlled index families before submission or approval.",
        51024 => "Another Approved formula overlaps this project, Works contract, code, and effective period.",
        51025 => "Approved formula inputs are immutable. Retire the formula and create a governed revision.",
        51026 => "The formula components no longer match an editable same-tenant formula and active controlled index families.",
        51027 => "An index family used by a current formula cannot be deleted or deactivated.",
        51028 => "Formula revision history is append-only.",
        51029 => "Formula revision history must remain in the same tenant as its formula.",
        _ => "The database rejected the formula change because a governed control changed. Refresh and try again."
    };
    private static string NormalizeCode(string value) { var code = RequireText(value, "Code", 40).ToUpperInvariant(); if (!CodePattern.IsMatch(code)) throw new QuantitySurveyEscalationValidationException("Code must contain 3-40 uppercase letters, digits, hyphens, or underscores."); return code; }
    private static string RequireText(string? value, string label, int max) { var result = value?.Trim(); if (string.IsNullOrWhiteSpace(result)) throw new QuantitySurveyEscalationValidationException(label + " is required."); if (result.Length > max) throw new QuantitySurveyEscalationValidationException(label + $" cannot exceed {max} characters."); return result; }
    private static string? Clean(string? value, int max) { var result = value?.Trim(); if (string.IsNullOrEmpty(result)) return null; return result.Length <= max ? result : result[..max]; }
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static void CheckVersion(byte[] current, string? supplied, string label) { byte[] parsed; try { parsed = Convert.FromBase64String(supplied ?? string.Empty); } catch { throw new QuantitySurveyEscalationValidationException($"A valid {label} row version is required."); } if (!current.SequenceEqual(parsed)) throw new QuantitySurveyEscalationConflictException($"The {label} changed after it was loaded. Refresh and try again."); }
    private static string SplitWords(string value) => Regex.Replace(value, "(?<!^)([A-Z])", " $1");
    private static JsonSerializerOptions CreateJsonOptions() { var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true }; options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)); return options; }

    private sealed record PolicyContext(QuantitySurveyConfigurationProfile Profile, QuantitySurveyConfigurationDecision Decision, QsEscalationValue Escalation, QsRolesAuthorityValue Authority);
    private sealed record ValidatedComponent(QuantitySurveyEscalationComponentType Component, decimal Coefficient, QuantitySurveyPriceIndexFamily Family);
    private sealed record ValidatedFormula(string Code, string Name, string Clause, string Reason, PolicyContext Policy, ApplicationRole Role, ErpSystem.Core.Entities.DocumentManagement.CentralDocumentVersion Evidence, IReadOnlyList<ValidatedComponent> Components);
}
