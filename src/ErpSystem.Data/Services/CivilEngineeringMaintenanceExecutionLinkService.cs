using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>
/// Keeps Civil scope lineage while invoking, and then observing, the existing Maintenance lifecycle.
/// It never approves, starts, completes or otherwise writes the linked Maintenance records.
/// </summary>
public sealed class CivilEngineeringMaintenanceExecutionLinkService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IJobCardService jobCards) : ICivilEngineeringMaintenanceExecutionLinkService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringMaintenanceExecutionLookupsDto> GetLookupsAsync(CancellationToken token = default)
    {
        var projectIds = await VisibleProjectIdsAsync(token);
        if (projectIds.Count == 0) return new CivilEngineeringMaintenanceExecutionLookupsDto();
        var handoffs = await db.CivilEngineeringMaintenanceCostingHandoffs.AsNoTracking()
            .Include(value => value.Assessment).ThenInclude(value => value.Intake)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && projectIds.Contains(value.ProjectId)
                && value.Stage == CivilEngineeringMaintenanceCostingHandoffStages.Awarded && value.Status == CivilEngineeringMaintenanceCostingHandoffStatuses.Awarded
                && value.Assessment.Intake.MaintenanceAssetId.HasValue)
            .OrderByDescending(value => value.ApprovedAt).Take(250).ToListAsync(token);
        var assetIds = handoffs.Select(value => value.Assessment.Intake.MaintenanceAssetId!.Value).Distinct().ToList();
        var jobCards = assetIds.Count == 0 ? [] : await db.JobCards.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && assetIds.Contains(value.AssetId)).OrderByDescending(value => value.CreatedAt).Take(250).ToListAsync(token);
        var workOrders = assetIds.Count == 0 ? [] : await db.WorkOrders.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && assetIds.Contains(value.AssetId)).OrderByDescending(value => value.CreatedAt).Take(250).ToListAsync(token);
        var maintenanceTypes = await db.MaintenanceTypes.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IsActive).OrderBy(value => value.SortOrder).ThenBy(value => value.Name).Take(250).ToListAsync(token);
        var priorities = await db.PriorityLevels.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IsActive).OrderBy(value => value.Level).ThenBy(value => value.Name).Take(250).ToListAsync(token);
        return new CivilEngineeringMaintenanceExecutionLookupsDto
        {
            AwardedHandoffs = handoffs.Select(value => Option(value.Id, $"{value.Assessment.Intake.IntakeNumber} - awarded Civil scope", value.Status, value.Assessment.Intake.MaintenanceAssetId)).ToList(),
            MaintenanceTypes = maintenanceTypes.Select(value => Option(value.Id, value.Name, value.Category)).ToList(),
            PriorityLevels = priorities.Select(value => Option(value.Id, value.Name, value.Level.ToString())).ToList(),
            JobCards = jobCards.Select(value => Option(value.Id, $"{value.JobCardNumber} - {value.Title}", value.JobCardStatus, value.AssetId)).ToList(),
            WorkOrders = workOrders.Select(value => Option(value.Id, $"{value.WorkOrderNumber} - {value.Title}", value.Status, value.AssetId)).ToList()
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringMaintenanceExecutionLinkDto>> ListAsync(CancellationToken token = default)
    {
        var projectIds = await VisibleProjectIdsAsync(token);
        if (projectIds.Count == 0) return [];
        var values = await Links(false).Where(value => projectIds.Contains(value.ProjectId)).OrderByDescending(value => value.CreatedAt).Take(500).ToListAsync(token);
        return await MapAsync(values, token);
    }

    public async Task<CivilEngineeringMaintenanceExecutionLinkDto> CreateAsync(CreateCivilEngineeringMaintenanceExecutionLinkRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringMaintenanceExecutionLinkPolicy.ValidateCreate(request);
        if (errors.Count > 0) throw Validation(errors);
        var requestHash = Hash(new { request.HandoffId, request.LinkMode, request.MaintenanceTypeId, request.PriorityLevelId, request.JobCardId, request.WorkOrderId });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await Links(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash)) throw Conflict("This client request identifier was already used with different Maintenance execution-link values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([retry], token)).Single();
        }

        var handoff = await db.CivilEngineeringMaintenanceCostingHandoffs.AsNoTracking()
            .Include(value => value.Assessment).ThenInclude(value => value.Intake)
            .Include(value => value.QuantitySurveyEstimateVersion)
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.HandoffId && !value.IsDeleted, token)
            ?? throw new CivilEngineeringMaintenanceCostingHandoffNotFoundException("The awarded Civil costing handoff was not found.");
        await RequireExecutionManagerAsync(handoff, token);
        if (handoff.Stage != CivilEngineeringMaintenanceCostingHandoffStages.Awarded || handoff.Status != CivilEngineeringMaintenanceCostingHandoffStatuses.Awarded || handoff.ApprovalStatus != CivilEngineeringMaintenanceCostingHandoffApprovalStatuses.Approved)
            throw Validation("Only an awarded Civil costing handoff can create or link Maintenance execution.");
        var assetId = handoff.Assessment.Intake.MaintenanceAssetId ?? throw Validation("This Civil scope is linked to a building/property rather than a Maintenance asset. Link an eligible Maintenance asset before starting an asset-based job card or work order.");
        if (await Links(true).AnyAsync(value => value.HandoffId == handoff.Id, token)) throw Conflict("This awarded Civil costing handoff already has an execution link.");

        JobCard? jobCard = null;
        WorkOrder? workOrder = null;
        if (string.Equals(request.LinkMode, CivilEngineeringMaintenanceExecutionLinkModes.CreateJobCard, StringComparison.Ordinal))
        {
            var maintenanceType = await db.MaintenanceTypes.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.MaintenanceTypeId && !value.IsDeleted && value.IsActive, token)
                ?? throw Validation("Select an active Maintenance type for the new job card.");
            var priority = await db.PriorityLevels.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.PriorityLevelId && !value.IsDeleted && value.IsActive, token)
                ?? throw Validation("Select an active Maintenance priority for the new job card.");
            var created = await jobCards.CreateJobCardAsync(new CreateJobCardDto
            {
                Title = $"Civil scope - {handoff.Assessment.Intake.Title}",
                Description = BuildMaintenanceDescription(handoff),
                ProblemDescription = BuildProblemDescription(handoff),
                AssetId = assetId,
                MaintenanceTypeId = maintenanceType.Id,
                PriorityLevelId = priority.Id,
                MaintenanceLocation = maintenanceType.Location,
                EstimatedHours = maintenanceType.EstimatedHours,
                EstimatedCost = handoff.QuantitySurveyEstimateVersion.TotalAmount,
                RequiresShutdown = maintenanceType.RequiresShutdown,
                RequiresSafetyPermit = maintenanceType.RequiresSafetyPermit,
                SafetyRequirements = maintenanceType.SafetyRequirements,
                SpecialInstructions = "Created from an awarded Civil Engineering scope. The Civil execution link retains the approved scope and supervision evidence."
            });
            jobCard = await db.JobCards.SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == created.Id && !value.IsDeleted, token)
                ?? throw Conflict("The Maintenance job card was created but could not be read back in the active tenant.");
        }
        else
        {
            jobCard = request.JobCardId.HasValue ? await db.JobCards.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.JobCardId && !value.IsDeleted, token) : null;
            workOrder = request.WorkOrderId.HasValue ? await db.WorkOrders.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.WorkOrderId && !value.IsDeleted, token) : null;
            ValidateExistingOwners(assetId, jobCard, workOrder);
            if (workOrder?.JobCardId is { } workOrderJobCardId && jobCard is null)
                jobCard = await db.JobCards.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == workOrderJobCardId && !value.IsDeleted, token)
                    ?? throw Validation("The selected work order has an unavailable linked job card.");
        }

        var state = CivilEngineeringMaintenanceExecutionLinkPolicy.DeriveOwnerState(jobCard?.JobCardStatus, jobCard?.ApprovalStatus, jobCard?.GeneratedWorkOrderId, workOrder?.Status);
        var now = DateTime.UtcNow;
        var link = new CivilEngineeringMaintenanceExecutionLink
        {
            Id = Guid.NewGuid(), TenantId = TenantId, HandoffId = handoff.Id, ProjectId = handoff.ProjectId, MaintenanceAssetId = assetId,
            JobCardId = jobCard?.Id, WorkOrderId = workOrder?.Id, LinkMode = request.LinkMode.Trim(), Stage = state.Stage, Status = state.Status,
            LastOwnerStatusSummary = state.Summary, LastRevalidatedAt = now, ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
            CorrelationId = Correlation(correlationId), CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.CivilEngineeringMaintenanceExecutionLinks.Add(link);
        AddRevision(link, CivilEngineeringAuditEventMap.LinkMaintenanceWorkOrder, null, null, Snapshot(link), correlationId);
        AddAudit(link, CivilEngineeringAuditEventMap.LinkMaintenanceWorkOrder, null, Snapshot(link), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([link], token)).Single();
    }

    public async Task<CivilEngineeringMaintenanceExecutionLinkDto> RefreshAsync(Guid executionLinkId, RefreshCivilEngineeringMaintenanceExecutionLinkRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) throw Validation("A concurrency version is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var link = await Links(true).Include(value => value.Handoff).ThenInclude(value => value.Assessment).ThenInclude(value => value.Intake).SingleOrDefaultAsync(value => value.Id == executionLinkId, token)
            ?? throw new CivilEngineeringMaintenanceExecutionLinkNotFoundException("The Civil Maintenance execution link was not found.");
        await RequireExecutionManagerAsync(link.Handoff, token);
        ApplyRowVersion(link, request.RowVersion);
        var hash = Hash(new { executionLinkId, Action = "Refresh", request.RowVersion });
        if (link.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(link.LastMutationRequestHash ?? string.Empty, hash)) throw Conflict("This client request identifier was already used with different execution-link refresh values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([link], token)).Single();
        }
        if (link.Handoff.Stage != CivilEngineeringMaintenanceCostingHandoffStages.Awarded || link.Handoff.Status != CivilEngineeringMaintenanceCostingHandoffStatuses.Awarded)
            throw Conflict("The Civil costing handoff is no longer awarded, so Maintenance execution cannot be refreshed.");
        var jobCard = link.JobCardId.HasValue ? await db.JobCards.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == link.JobCardId && !value.IsDeleted, token) : null;
        var workOrderId = link.WorkOrderId ?? jobCard?.GeneratedWorkOrderId;
        var workOrder = workOrderId.HasValue ? await db.WorkOrders.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == workOrderId && !value.IsDeleted, token) : null;
        ValidateExistingOwners(link.MaintenanceAssetId, jobCard, workOrder);
        var fromStage = link.Stage;
        var before = Snapshot(link);
        var state = CivilEngineeringMaintenanceExecutionLinkPolicy.DeriveOwnerState(jobCard?.JobCardStatus, jobCard?.ApprovalStatus, jobCard?.GeneratedWorkOrderId, workOrder?.Status);
        link.WorkOrderId = workOrder?.Id;
        link.Stage = state.Stage;
        link.Status = state.Status;
        link.LastOwnerStatusSummary = state.Summary;
        link.LastRevalidatedAt = DateTime.UtcNow;
        link.LastMutationClientRequestId = request.ClientRequestId;
        link.LastMutationRequestHash = hash;
        link.CorrelationId = Correlation(correlationId);
        link.UpdatedAt = DateTime.UtcNow;
        link.UpdatedBy = UserName;
        link.LastModifiedById = UserId;
        AddRevision(link, CivilEngineeringAuditEventMap.UpdateCivilInterfaceStatus, fromStage, null, Snapshot(link), correlationId);
        AddAudit(link, CivilEngineeringAuditEventMap.UpdateCivilInterfaceStatus, before, Snapshot(link), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([link], token)).Single();
    }

    public async Task<IReadOnlyList<CivilEngineeringMaintenanceExecutionLinkRevisionDto>> GetHistoryAsync(Guid executionLinkId, CancellationToken token = default)
    {
        var projects = await VisibleProjectIdsAsync(token);
        if (!await Links(false).AnyAsync(value => value.Id == executionLinkId && projects.Contains(value.ProjectId), token)) throw new CivilEngineeringMaintenanceExecutionLinkNotFoundException("The Civil Maintenance execution link was not found.");
        return await db.CivilEngineeringMaintenanceExecutionLinkRevisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.ExecutionLinkId == executionLinkId && !value.IsDeleted).OrderBy(value => value.CreatedAt)
            .Select(value => new CivilEngineeringMaintenanceExecutionLinkRevisionDto { Id = value.Id, Action = value.Action, FromStage = value.FromStage, ToStage = value.ToStage, ActorName = value.ActorName, ActorRoles = value.ActorRoles, Reason = value.Reason, CorrelationId = value.CorrelationId, CreatedAt = value.CreatedAt }).ToListAsync(token);
    }

    private IQueryable<CivilEngineeringMaintenanceExecutionLink> Links(bool tracked) => (tracked ? db.CivilEngineeringMaintenanceExecutionLinks : db.CivilEngineeringMaintenanceExecutionLinks.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);
    private async Task<List<Guid>> VisibleProjectIdsAsync(CancellationToken token) => await db.ProjectMembers.AsNoTracking().Where(value => value.TenantId == TenantId && value.UserId == UserId && value.IsActive && !value.IsDeleted).Select(value => value.ProjectId).Distinct().ToListAsync(token);

    private async Task RequireExecutionManagerAsync(CivilEngineeringMaintenanceCostingHandoff handoff, CancellationToken token)
    {
        if (!await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == handoff.ProjectId && value.UserId == UserId && value.IsActive && !value.IsDeleted, token))
            throw new UnauthorizedAccessException("You are not an active member of the Civil scope's project.");
        if (!currentUser.Roles.Any(value => string.Equals(value, CivilEngineeringAccessControlRegistry.HeadRole, StringComparison.OrdinalIgnoreCase) || string.Equals(value, CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Only an assigned Head or Supervising Civil Engineer can create or synchronize Maintenance execution links.");
    }

    private static void ValidateExistingOwners(Guid assetId, JobCard? jobCard, WorkOrder? workOrder)
    {
        if (jobCard is null && workOrder is null) throw Validation("Select an existing Maintenance job card or work order from this tenant.");
        if (jobCard is not null && jobCard.AssetId != assetId) throw Validation("The selected Maintenance job card belongs to a different asset.");
        if (workOrder is not null && workOrder.AssetId != assetId) throw Validation("The selected Maintenance work order belongs to a different asset.");
        if (jobCard is not null && workOrder?.JobCardId is { } workOrderJobCardId && workOrderJobCardId != jobCard.Id) throw Validation("The selected Maintenance work order belongs to a different job card.");
    }

    private async Task<IReadOnlyList<CivilEngineeringMaintenanceExecutionLinkDto>> MapAsync(IReadOnlyCollection<CivilEngineeringMaintenanceExecutionLink> values, CancellationToken token)
    {
        if (values.Count == 0) return [];
        var handoffIds = values.Select(value => value.HandoffId).Distinct().ToList(); var projectIds = values.Select(value => value.ProjectId).Distinct().ToList(); var assetIds = values.Select(value => value.MaintenanceAssetId).Distinct().ToList();
        var jobCardIds = values.Where(value => value.JobCardId.HasValue).Select(value => value.JobCardId!.Value).Distinct().ToList(); var workOrderIds = values.Where(value => value.WorkOrderId.HasValue).Select(value => value.WorkOrderId!.Value).Distinct().ToList();
        var handoffs = await db.CivilEngineeringMaintenanceCostingHandoffs.AsNoTracking().Include(value => value.Assessment).ThenInclude(value => value.Intake).Where(value => value.TenantId == TenantId && handoffIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var projects = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && projectIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => $"{value.ProjectCode} - {value.Title}", token);
        var assets = await db.MaintenanceAssets.AsNoTracking().Where(value => value.TenantId == TenantId && assetIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => $"{value.AssetNumber} - {value.Name}", token);
        var jobCards = jobCardIds.Count == 0 ? new Dictionary<Guid, JobCard>() : await db.JobCards.AsNoTracking().Where(value => value.TenantId == TenantId && jobCardIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var workOrders = workOrderIds.Count == 0 ? new Dictionary<Guid, WorkOrder>() : await db.WorkOrders.AsNoTracking().Where(value => value.TenantId == TenantId && workOrderIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        return values.Select(value =>
        {
            handoffs.TryGetValue(value.HandoffId, out var handoff); jobCards.TryGetValue(value.JobCardId ?? Guid.Empty, out var jobCard); workOrders.TryGetValue(value.WorkOrderId ?? Guid.Empty, out var workOrder);
            return new CivilEngineeringMaintenanceExecutionLinkDto { Id = value.Id, HandoffId = value.HandoffId, IntakeNumber = handoff?.Assessment.Intake.IntakeNumber ?? "Unavailable intake", ProjectId = value.ProjectId, ProjectLabel = projects.GetValueOrDefault(value.ProjectId, "Unavailable project"), MaintenanceAssetId = value.MaintenanceAssetId, MaintenanceAssetLabel = assets.GetValueOrDefault(value.MaintenanceAssetId, "Unavailable asset"), JobCardId = value.JobCardId, JobCardNumber = jobCard?.JobCardNumber, JobCardStatus = jobCard?.JobCardStatus, WorkOrderId = value.WorkOrderId, WorkOrderNumber = workOrder?.WorkOrderNumber, WorkOrderStatus = workOrder?.Status, LinkMode = value.LinkMode, Stage = value.Stage, Status = value.Status, LastOwnerStatusSummary = value.LastOwnerStatusSummary, LastRevalidatedAt = value.LastRevalidatedAt, RowVersion = Convert.ToBase64String(value.RowVersion) };
        }).ToList();
    }

    private void AddRevision(CivilEngineeringMaintenanceExecutionLink link, string action, string? fromStage, string? reason, object after, string correlationId)
    {
        CivilEngineeringAuditEventMap.GetRequired(action);
        db.CivilEngineeringMaintenanceExecutionLinkRevisions.Add(new CivilEngineeringMaintenanceExecutionLinkRevision { Id = Guid.NewGuid(), TenantId = TenantId, ExecutionLinkId = link.Id, Action = action, FromStage = fromStage, ToStage = link.Stage, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, Reason = reason, CorrelationId = Correlation(correlationId), BeforeJson = null, AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    }
    private void AddAudit(CivilEngineeringMaintenanceExecutionLink link, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(CivilEngineeringMaintenanceExecutionLink), ResourceId = link.Id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private async Task SaveAsync(CancellationToken token) { try { await db.SaveChangesAsync(token); } catch (DbUpdateConcurrencyException) { throw Conflict("The Civil Maintenance execution link changed concurrently. Refresh and retry."); } catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52230 and <= 52236) { throw Conflict(sql.Message); } catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting Civil Maintenance execution link was detected. Refresh and retry."); } }
    private static object Snapshot(CivilEngineeringMaintenanceExecutionLink value) => new { value.Id, value.HandoffId, value.ProjectId, value.MaintenanceAssetId, value.JobCardId, value.WorkOrderId, value.LinkMode, value.Stage, value.Status, value.LastOwnerStatusSummary, value.LastRevalidatedAt };
    private static CivilEngineeringMaintenanceExecutionLookupOptionDto Option(Guid id, string label, string status, Guid? assetId = null) => new() { Id = id, Label = label, Status = status, MaintenanceAssetId = assetId };
    private static string BuildMaintenanceDescription(CivilEngineeringMaintenanceCostingHandoff value) => $"Civil Engineering approved scope for {value.Assessment.Intake.IntakeNumber}. {value.Assessment.ScopeRecommendation ?? value.Assessment.RemedyRecommendation ?? value.Assessment.Intake.Description}"[..Math.Min(2000, ($"Civil Engineering approved scope for {value.Assessment.Intake.IntakeNumber}. {value.Assessment.ScopeRecommendation ?? value.Assessment.RemedyRecommendation ?? value.Assessment.Intake.Description}").Length)];
    private static string BuildProblemDescription(CivilEngineeringMaintenanceCostingHandoff value) => (value.Assessment.SiteAssessment ?? value.Assessment.Intake.Description)[..Math.Min(2000, (value.Assessment.SiteAssessment ?? value.Assessment.Intake.Description).Length)];
    private static void ApplyRowVersion(CivilEngineeringMaintenanceExecutionLink value, string rowVersion) { try { value.RowVersion = Convert.FromBase64String(rowVersion); } catch (FormatException) { throw Validation("The execution-link concurrency version is invalid."); } }
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value[..Math.Min(100, value.Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static CivilEngineeringMaintenanceExecutionLinkValidationException Validation(string value) => new(value);
    private static CivilEngineeringMaintenanceExecutionLinkValidationException Validation(IEnumerable<string> values) => new(string.Join(" ", values));
    private static CivilEngineeringMaintenanceExecutionLinkConflictException Conflict(string value) => new(value);
}
