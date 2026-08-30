using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Projects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>
/// Tenant-safe Civil intake overlay. Maintenance schedules/job cards, Estate properties,
/// Helpdesk complaints and central-DMS documents remain authoritative in their own modules.
/// </summary>
public sealed class CivilEngineeringMaintenanceIntakeService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService) : ICivilEngineeringMaintenanceIntakeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringMaintenanceIntakeLookupsDto> GetLookupsAsync(CancellationToken token = default)
    {
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        return new CivilEngineeringMaintenanceIntakeLookupsDto
        {
            Sources = policy.Value.AllowedRequestSources.Distinct().OrderBy(value => value).ToList(),
            Urgencies = policy.Value.AllowedUrgencies.Distinct().OrderBy(value => value).ToList(),
            WorkClassifications = [CivilEngineeringWorkClassification.ScheduledMaintenance, CivilEngineeringWorkClassification.BreakdownMaintenance, CivilEngineeringWorkClassification.AssetComplaintResolution],
            Projects = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted)
                .OrderBy(value => value.ProjectCode).Select(value => new CivilEngineeringMaintenanceIntakeLookupOptionDto { Id = value.Id, Label = value.ProjectCode + " - " + value.Title }).Take(250).ToListAsync(token),
            MaintenanceAssets = await db.MaintenanceAssets.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.Status == AssetStatus.Active)
                .OrderBy(value => value.AssetNumber).ThenBy(value => value.Name).Select(value => new CivilEngineeringMaintenanceIntakeLookupOptionDto { Id = value.Id, Label = (value.AssetNumber ?? "Asset") + " - " + value.Name + (string.IsNullOrWhiteSpace(value.Building) ? string.Empty : " (" + value.Building + ")") }).Take(250).ToListAsync(token),
            BuildingsOrProperties = await db.EstateManagedAssets.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted)
                .OrderBy(value => value.AssetCode).ThenBy(value => value.Name).Select(value => new CivilEngineeringMaintenanceIntakeLookupOptionDto { Id = value.Id, Label = value.AssetCode + " - " + value.Name + (string.IsNullOrWhiteSpace(value.Location) ? string.Empty : " (" + value.Location + ")") }).Take(250).ToListAsync(token),
            MaintenanceSchedules = await db.MaintenanceSchedules.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IsActive)
                .OrderBy(value => value.Code).Select(value => new CivilEngineeringMaintenanceIntakeLookupOptionDto { Id = value.Id, Label = value.Code + " - " + value.Name }).Take(250).ToListAsync(token),
            ComplaintTickets = await db.EhcTickets.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.TicketType == EhcTicketType.Complaint && value.Status != EhcTicketStatus.Closed)
                .OrderByDescending(value => value.CreatedAt).Select(value => new CivilEngineeringMaintenanceIntakeLookupOptionDto { Id = value.Id, Label = value.TicketNumber + " - " + (value.Subject ?? "Complaint") }).Take(250).ToListAsync(token),
            Requesters = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && value.IsActive)
                .OrderBy(value => value.FirstName).ThenBy(value => value.LastName).Select(value => new CivilEngineeringMaintenanceIntakeLookupOptionDto { Id = value.Id, Label = Name(value.FirstName, value.LastName, value.UserName) }).Take(250).ToListAsync(token),
            Priorities = await db.PriorityLevels.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IsActive)
                .OrderBy(value => value.Level).ThenBy(value => value.Name).Select(value => new CivilEngineeringMaintenanceIntakeLookupOptionDto { Id = value.Id, Label = value.Name }).ToListAsync(token),
            Documents = await DocumentsAsync(policy.Template.TemplateCode, token)
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringMaintenanceIntakeDto>> ListAsync(CancellationToken token = default) =>
        await MapAsync(await Intakes(false).OrderByDescending(value => value.CreatedAt).Take(500).ToListAsync(token), token);

    public async Task<CivilEngineeringMaintenanceIntakeDto> CreateAsync(CreateCivilEngineeringMaintenanceIntakeRequest request, string correlationId, CancellationToken token = default)
    {
        var policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        var errors = CivilEngineeringMaintenanceIntakePolicy.ValidateCreate(request, policy.Value);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new
        {
            request.WorkClassification, request.Source, request.Urgency, title = request.Title.Trim(), description = request.Description.Trim(),
            request.ProjectId, request.MaintenanceAssetId, request.EstateManagedAssetId, request.MaintenanceScheduleId, request.HelpdeskTicketId,
            request.RequesterUserId, request.PriorityLevelId, request.CentralDocumentRecordId, request.CentralDocumentVersionId
        });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await Intakes(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different intake values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([retry], token)).Single();
        }

        policy = await ResolvePolicyAsync(DateTime.UtcNow, token);
        errors = CivilEngineeringMaintenanceIntakePolicy.ValidateCreate(request, policy.Value);
        if (errors.Count > 0) throw Validation(errors);
        var resolvedProjectId = await RequireTargetsAsync(request, token);
        await RequireRequesterAsync(request.RequesterUserId, token);
        await RequirePriorityAsync(request.PriorityLevelId, token);
        var evidence = await RequireEvidenceAsync(request.CentralDocumentRecordId, request.CentralDocumentVersionId, policy.Template.TemplateCode, token);
        var now = DateTime.UtcNow;
        var sequence = await Intakes(true).CountAsync(value => value.CreatedAt >= new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), token) + 1;
        var intake = new CivilEngineeringMaintenanceIntake
        {
            Id = Guid.NewGuid(), TenantId = TenantId, IntakeNumber = $"CIV-MNT-{now:yyyy}-{sequence:D5}", WorkClassification = request.WorkClassification,
            Source = request.Source, Urgency = request.Urgency, Title = RequiredText(request.Title, 300, "Intake title"), Description = RequiredText(request.Description, 4000, "Intake description"),
            ProjectId = resolvedProjectId, MaintenanceAssetId = request.MaintenanceAssetId, EstateManagedAssetId = request.EstateManagedAssetId,
            MaintenanceScheduleId = request.MaintenanceScheduleId, HelpdeskTicketId = request.HelpdeskTicketId, RequesterUserId = request.RequesterUserId,
            PriorityLevelId = request.PriorityLevelId, CentralDocumentRecordId = evidence.DocumentRecordId, CentralDocumentVersionId = evidence.Id,
            ConfigurationProfileId = policy.ProfileId, ConfigurationDecisionId = policy.DecisionId, WorkflowDefinitionId = policy.WorkflowDefinitionId,
            EvidenceMetadataTemplateId = policy.Template.Id, EvidenceMetadataTemplateCodeSnapshot = policy.Template.TemplateCode, PolicyHash = policy.PolicyHash,
            Status = CivilEngineeringMaintenanceIntakeStatuses.Logged, ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
            CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.CivilEngineeringMaintenanceIntakes.Add(intake);
        AddRevision(intake, CivilEngineeringAuditEventMap.CreateCivilWorkIntake, null, Snapshot(intake), correlationId);
        AddAudit(intake, CivilEngineeringAuditEventMap.CreateCivilWorkIntake, null, Snapshot(intake), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([intake], token)).Single();
    }

    public async Task<IReadOnlyList<CivilEngineeringMaintenanceIntakeRevisionDto>> GetHistoryAsync(Guid intakeId, CancellationToken token = default)
    {
        if (!await Intakes(false).AnyAsync(value => value.Id == intakeId, token)) throw new CivilEngineeringMaintenanceIntakeNotFoundException("The Civil maintenance intake was not found.");
        return await db.CivilEngineeringMaintenanceIntakeRevisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.IntakeId == intakeId && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt).Select(value => new CivilEngineeringMaintenanceIntakeRevisionDto { Id = value.Id, Action = value.Action, ActorName = value.ActorName, ActorRoles = value.ActorRoles, CorrelationId = value.CorrelationId, CreatedAt = value.CreatedAt }).ToListAsync(token);
    }

    private IQueryable<CivilEngineeringMaintenanceIntake> Intakes(bool tracked) =>
        (tracked ? db.CivilEngineeringMaintenanceIntakes : db.CivilEngineeringMaintenanceIntakes.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<Guid?> RequireTargetsAsync(CreateCivilEngineeringMaintenanceIntakeRequest request, CancellationToken token)
    {
        Guid? inferredProjectId = null;
        if (request.MaintenanceAssetId.HasValue)
        {
            var asset = await db.MaintenanceAssets.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.MaintenanceAssetId && !value.IsDeleted && value.Status == AssetStatus.Active, token)
                ?? throw Validation("The selected maintenance asset is not active in this tenant.");
            inferredProjectId = asset.CurrentProjectId;
        }
        if (request.EstateManagedAssetId.HasValue)
        {
            var asset = await db.EstateManagedAssets.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.EstateManagedAssetId && !value.IsDeleted, token)
                ?? throw Validation("The selected building/property is not active in this tenant.");
            inferredProjectId = asset.ProjectId;
        }
        if (request.MaintenanceScheduleId.HasValue)
        {
            var schedule = await db.MaintenanceSchedules.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.MaintenanceScheduleId && !value.IsDeleted && value.IsActive, token)
                ?? throw Validation("The selected maintenance schedule is not active in this tenant.");
            if (schedule.AssetId != request.MaintenanceAssetId) throw Validation("The selected schedule does not belong to the selected maintenance asset.");
        }
        if (request.HelpdeskTicketId.HasValue && !await db.EhcTickets.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == request.HelpdeskTicketId && !value.IsDeleted && value.TicketType == EhcTicketType.Complaint && value.Status != EhcTicketStatus.Closed, token))
            throw Validation("The selected Helpdesk complaint is unavailable for Civil intake.");

        var projectId = request.ProjectId ?? inferredProjectId;
        if (projectId.HasValue)
        {
            if (request.ProjectId.HasValue && inferredProjectId.HasValue && request.ProjectId != inferredProjectId) throw Validation("The selected project does not match the selected asset or building/property.");
            if (await projectService.GetProjectByIdAsync(projectId.Value) is null) throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
        }
        return projectId;
    }

    private async Task RequireRequesterAsync(Guid userId, CancellationToken token)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == userId && value.IsActive, token)) throw Validation("Select an active requester from this tenant.");
    }

    private async Task RequirePriorityAsync(Guid priorityId, CancellationToken token)
    {
        if (!await db.PriorityLevels.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == priorityId && !value.IsDeleted && value.IsActive, token)) throw Validation("Select an active maintenance priority from this tenant.");
    }

    private async Task<CentralDocumentVersion> RequireEvidenceAsync(Guid recordId, Guid versionId, string templateCode, CancellationToken token)
    {
        var version = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == versionId, token) ?? throw Validation("Select a current Published central-DMS evidence document.");
        if (version.DocumentRecordId != recordId) throw Validation("The selected DMS version does not belong to the selected document.");
        if (!string.Equals(version.DocumentRecord.MetadataTemplateCode, templateCode, StringComparison.OrdinalIgnoreCase)) throw Validation($"The intake evidence must use DMS template {templateCode}.");
        return version;
    }

    private async Task<Policy> ResolvePolicyAsync(DateTime at, CancellationToken token)
    {
        var profiles = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted
                && value.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Published && value.EffectiveFrom <= at && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).Take(2).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No effective published Civil Engineering configuration profile exists.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one Civil Engineering configuration profile is effective.");
        var profile = profiles[0];
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profile.Id && value.ConfigurationKey == "CIV-CFG-007" && !value.IsDeleted, token)
            ?? throw Validation("The effective Civil Engineering configuration has no CIV-CFG-007 maintenance-assessment decision.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified)
            throw Validation("CIV-CFG-007 is not approved and verified.");
        CivilEngineeringMaintenanceAssessmentValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringMaintenanceAssessmentValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("CIV-CFG-007 contains invalid maintenance-assessment control data."); }
        if (value.WorkflowDefinitionId == Guid.Empty || value.MetadataTemplateId == Guid.Empty || value.AllowedRequestSources.Count == 0 || value.AllowedUrgencies.Count == 0)
            throw Validation("CIV-CFG-007 must select its assessment workflow, DMS template, allowed sources and allowed urgency levels.");
        var workflow = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.WorkflowDefinitionId && !item.IsDeleted && item.IsActive
            && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.EntityType.IsDeleted && item.EntityType.IsActive && item.EntityType.Code == CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment, token);
        if (workflow is null || !await db.WorkflowSteps.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.WorkflowDefinitionId == value.WorkflowDefinitionId && !item.IsDeleted, token))
            throw Validation($"The CIV-CFG-007 assessment workflow must be active, Published, contain a review step, and be bound to {CivilEngineeringWorkflowBindingRegistry.MaintenanceAssessment}.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.MetadataTemplateId && item.IsActive && item.PublishedAt.HasValue && !item.IsDeleted, token)
            ?? throw Validation("The CIV-CFG-007 DMS assessment-evidence template is unavailable.");
        return new Policy(profile.Id, decision.Id, value.WorkflowDefinitionId, value, template, Hash(decision.ValueJson));
    }

    private async Task<IReadOnlyList<CivilEngineeringMaintenanceIntakeDocumentLookupDto>> DocumentsAsync(string templateCode, CancellationToken token) =>
        await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished())
            .Where(value => value.TenantId == TenantId && value.DocumentRecord.MetadataTemplateCode == templateCode).OrderBy(value => value.DocumentRecord.DocumentReference).ThenByDescending(value => value.CreatedAt)
            .Select(value => new CivilEngineeringMaintenanceIntakeDocumentLookupDto { CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id, DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber }).Take(250).ToListAsync(token);

    private async Task<IReadOnlyList<CivilEngineeringMaintenanceIntakeDto>> MapAsync(IReadOnlyCollection<CivilEngineeringMaintenanceIntake> values, CancellationToken token)
    {
        var projectIds = values.Where(value => value.ProjectId.HasValue).Select(value => value.ProjectId!.Value).Distinct().ToList();
        var assetIds = values.Where(value => value.MaintenanceAssetId.HasValue).Select(value => value.MaintenanceAssetId!.Value).Distinct().ToList();
        var propertyIds = values.Where(value => value.EstateManagedAssetId.HasValue).Select(value => value.EstateManagedAssetId!.Value).Distinct().ToList();
        var scheduleIds = values.Where(value => value.MaintenanceScheduleId.HasValue).Select(value => value.MaintenanceScheduleId!.Value).Distinct().ToList();
        var ticketIds = values.Where(value => value.HelpdeskTicketId.HasValue).Select(value => value.HelpdeskTicketId!.Value).Distinct().ToList();
        var userIds = values.Select(value => value.RequesterUserId).Distinct().ToList();
        var priorityIds = values.Select(value => value.PriorityLevelId).Distinct().ToList();
        var documentIds = values.Select(value => value.CentralDocumentRecordId).Distinct().ToList();
        var projects = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && projectIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.ProjectCode + " - " + value.Title, token);
        var assets = await db.MaintenanceAssets.AsNoTracking().Where(value => value.TenantId == TenantId && assetIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => (value.AssetNumber ?? "Asset") + " - " + value.Name, token);
        var properties = await db.EstateManagedAssets.AsNoTracking().Where(value => value.TenantId == TenantId && propertyIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.AssetCode + " - " + value.Name, token);
        var schedules = await db.MaintenanceSchedules.AsNoTracking().Where(value => value.TenantId == TenantId && scheduleIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Code + " - " + value.Name, token);
        var tickets = await db.EhcTickets.AsNoTracking().Where(value => value.TenantId == TenantId && ticketIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.TicketNumber, token);
        var users = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && userIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => Name(value.FirstName, value.LastName, value.UserName), token);
        var priorities = await db.PriorityLevels.AsNoTracking().Where(value => value.TenantId == TenantId && priorityIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.Name, token);
        var documents = await db.CentralDocumentRecords.AsNoTracking().Where(value => value.TenantId == TenantId && documentIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => value.DocumentReference, token);
        return values.Select(value => new CivilEngineeringMaintenanceIntakeDto
        {
            Id = value.Id, IntakeNumber = value.IntakeNumber, WorkClassification = value.WorkClassification, Source = value.Source, Urgency = value.Urgency, Title = value.Title, Description = value.Description,
            ProjectId = value.ProjectId, ProjectName = value.ProjectId.HasValue ? projects.GetValueOrDefault(value.ProjectId.Value) : null,
            MaintenanceAssetId = value.MaintenanceAssetId, MaintenanceAssetName = value.MaintenanceAssetId.HasValue ? assets.GetValueOrDefault(value.MaintenanceAssetId.Value) : null,
            EstateManagedAssetId = value.EstateManagedAssetId, BuildingOrPropertyName = value.EstateManagedAssetId.HasValue ? properties.GetValueOrDefault(value.EstateManagedAssetId.Value) : null,
            MaintenanceScheduleId = value.MaintenanceScheduleId, MaintenanceScheduleName = value.MaintenanceScheduleId.HasValue ? schedules.GetValueOrDefault(value.MaintenanceScheduleId.Value) : null,
            HelpdeskTicketId = value.HelpdeskTicketId, ComplaintTicketNumber = value.HelpdeskTicketId.HasValue ? tickets.GetValueOrDefault(value.HelpdeskTicketId.Value) : null,
            RequesterUserId = value.RequesterUserId, RequesterName = users.GetValueOrDefault(value.RequesterUserId, "Unavailable requester"), PriorityLevelId = value.PriorityLevelId,
            PriorityName = priorities.GetValueOrDefault(value.PriorityLevelId, "Unavailable priority"), CentralDocumentRecordId = value.CentralDocumentRecordId, CentralDocumentVersionId = value.CentralDocumentVersionId,
            EvidenceReference = documents.GetValueOrDefault(value.CentralDocumentRecordId), Status = value.Status, WorkflowDefinitionId = value.WorkflowDefinitionId, CreatedAt = value.CreatedAt, RowVersion = Convert.ToBase64String(value.RowVersion)
        }).ToList();
    }

    private void AddRevision(CivilEngineeringMaintenanceIntake intake, string action, object? before, object after, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.CivilEngineeringMaintenanceIntakeRevisions.Add(new CivilEngineeringMaintenanceIntakeRevision { Id = Guid.NewGuid(), TenantId = TenantId, IntakeId = intake.Id, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    }

    private void AddAudit(CivilEngineeringMaintenanceIntake intake, string action, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(CivilEngineeringMaintenanceIntake), ResourceId = intake.Id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The Civil maintenance intake changed concurrently. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52200 and <= 52209) { throw Conflict(sql.Message); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting Civil maintenance intake was detected. Refresh and retry."); }
    }

    private static object Snapshot(CivilEngineeringMaintenanceIntake value) => new { value.Id, value.IntakeNumber, value.WorkClassification, value.Source, value.Urgency, value.Title, value.ProjectId, value.MaintenanceAssetId, value.EstateManagedAssetId, value.MaintenanceScheduleId, value.HelpdeskTicketId, value.RequesterUserId, value.PriorityLevelId, value.CentralDocumentRecordId, value.CentralDocumentVersionId, value.Status, value.ConfigurationProfileId, value.ConfigurationDecisionId, value.WorkflowDefinitionId, value.PolicyHash };
    private static string RequiredText(string? value, int max, string label) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) throw Validation($"{label} is required."); return normalized.Length <= max ? normalized : throw Validation($"{label} cannot exceed {max} characters."); }
    private static string Name(string? first, string? last, string? fallback) { var value = string.Join(' ', new[] { first, last }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim(); return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value; }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value[..Math.Min(100, value.Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static CivilEngineeringMaintenanceIntakeValidationException Validation(string value) => new(value);
    private static CivilEngineeringMaintenanceIntakeValidationException Validation(IEnumerable<string> values) => new(string.Join(" ", values));
    private static CivilEngineeringMaintenanceIntakeConflictException Conflict(string value) => new(value);
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId, CivilEngineeringMaintenanceAssessmentValue Value, CentralDocumentMetadataTemplate Template, string PolicyHash);
}
