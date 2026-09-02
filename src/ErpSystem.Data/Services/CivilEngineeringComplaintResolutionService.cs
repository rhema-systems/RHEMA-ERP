using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

/// <summary>
/// A read-only Civil complaint-resolution register. It composes the existing Helpdesk complaint,
/// Civil assessment, owner-maintained execution and Finance-direction records without taking
/// ownership of any of their lifecycles.
/// </summary>
public sealed class CivilEngineeringComplaintResolutionService(
    ApplicationDbContext db,
    ICurrentUserService currentUser) : ICivilEngineeringComplaintResolutionService
{
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A valid tenant context is required.");

    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("An authenticated user is required.");

    public async Task<IReadOnlyList<CivilEngineeringComplaintResolutionDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var intakes = await VisibleComplaintIntakesAsync(cancellationToken);
        return await MapAsync(intakes, cancellationToken);
    }

    public async Task<IReadOnlyList<CivilEngineeringComplaintResolutionTimelineEntryDto>> GetTimelineAsync(
        Guid helpdeskTicketId,
        CancellationToken cancellationToken = default)
    {
        if (helpdeskTicketId == Guid.Empty)
            throw new ArgumentException("A Helpdesk complaint ticket is required.", nameof(helpdeskTicketId));

        var intake = (await VisibleComplaintIntakesAsync(cancellationToken))
            .FirstOrDefault(value => value.HelpdeskTicketId == helpdeskTicketId)
            ?? throw new KeyNotFoundException("The Civil-linked Helpdesk complaint was not found within your assigned scope.");

        var assessment = await db.CivilEngineeringMaintenanceAssessments.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IntakeId == intake.Id)
            .OrderByDescending(value => value.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var handoff = assessment is null ? null : await db.CivilEngineeringMaintenanceCostingHandoffs.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.AssessmentId == assessment.Id)
            .OrderByDescending(value => value.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var execution = handoff is null ? null : await db.CivilEngineeringMaintenanceExecutionLinks.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.HandoffId == handoff.Id)
            .OrderByDescending(value => value.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var completion = execution is null ? null : await db.CivilEngineeringMaintenanceCompletionControls.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.ExecutionLinkId == execution.Id)
            .OrderByDescending(value => value.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var entries = new List<CivilEngineeringComplaintResolutionTimelineEntryDto>();
        var helpdeskHistory = await db.EhcTicketStatusHistories.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.TicketId == helpdeskTicketId)
            .Select(value => new { value.CreatedAt, value.ToStatus })
            .ToListAsync(cancellationToken);
        entries.AddRange(helpdeskHistory.Select(value => new CivilEngineeringComplaintResolutionTimelineEntryDto
        {
            OccurredAt = value.CreatedAt,
            Source = "Helpdesk",
            Action = "Complaint status updated",
            Stage = value.ToStatus.ToString()
        }));

        entries.AddRange((await db.CivilEngineeringMaintenanceIntakeRevisions.AsNoTracking()
                .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IntakeId == intake.Id)
                .Select(value => new { value.CreatedAt, value.Action, value.ActorName })
                .ToListAsync(cancellationToken))
            .Select(value => Entry(value.CreatedAt, "Civil intake", value.Action, intake.Status, value.ActorName)));

        if (assessment is not null)
            entries.AddRange((await db.CivilEngineeringMaintenanceAssessmentRevisions.AsNoTracking()
                    .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.AssessmentId == assessment.Id)
                    .Select(value => new { value.CreatedAt, value.Action, value.ToStage, value.ActorName })
                    .ToListAsync(cancellationToken))
                .Select(value => Entry(value.CreatedAt, "Civil assessment", value.Action, value.ToStage, value.ActorName)));

        if (handoff is not null)
            entries.AddRange((await db.CivilEngineeringMaintenanceCostingHandoffRevisions.AsNoTracking()
                    .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.HandoffId == handoff.Id)
                    .Select(value => new { value.CreatedAt, value.Action, value.ToStage, value.ActorName })
                    .ToListAsync(cancellationToken))
                .Select(value => Entry(value.CreatedAt, "Costing and award", value.Action, value.ToStage, value.ActorName)));

        if (execution is not null)
            entries.AddRange((await db.CivilEngineeringMaintenanceExecutionLinkRevisions.AsNoTracking()
                    .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.ExecutionLinkId == execution.Id)
                    .Select(value => new { value.CreatedAt, value.Action, value.ToStage, value.ActorName })
                    .ToListAsync(cancellationToken))
                .Select(value => Entry(value.CreatedAt, "Maintenance execution link", value.Action, value.ToStage, value.ActorName)));

        if (completion is not null)
            entries.AddRange((await db.CivilEngineeringMaintenanceCompletionRevisions.AsNoTracking()
                    .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.CompletionControlId == completion.Id)
                    .Select(value => new { value.CreatedAt, value.Action, value.ToStage, value.ActorName })
                    .ToListAsync(cancellationToken))
                .Select(value => Entry(value.CreatedAt, "Civil completion control", value.Action, value.ToStage, value.ActorName)));

        return entries.OrderByDescending(value => value.OccurredAt).ToList();
    }

    private async Task<List<CivilEngineeringMaintenanceIntake>> VisibleComplaintIntakesAsync(CancellationToken token)
    {
        var projectIds = await db.ProjectMembers.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.IsActive && !value.IsDeleted && value.UserId == UserId)
            .Select(value => value.ProjectId)
            .Distinct()
            .ToListAsync(token);
        var assignedIntakeIds = await db.CivilEngineeringMaintenanceAssessments.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                (value.HodUserId == UserId || value.SupervisingCivilEngineerUserId == UserId || value.CivilEngineerUserId == UserId || value.CurrentAssigneeUserId == UserId))
            .Select(value => value.IntakeId)
            .Distinct()
            .ToListAsync(token);
        var hasManagementRole = currentUser.Roles.Any(value =>
            string.Equals(value, CivilEngineeringAccessControlRegistry.HeadRole, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, StringComparison.OrdinalIgnoreCase));

        return await db.CivilEngineeringMaintenanceIntakes.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                value.WorkClassification == CivilEngineeringWorkClassification.AssetComplaintResolution &&
                value.HelpdeskTicketId.HasValue &&
                (hasManagementRole || value.RequesterUserId == UserId || assignedIntakeIds.Contains(value.Id) ||
                    (value.ProjectId.HasValue && projectIds.Contains(value.ProjectId.Value))))
            .OrderByDescending(value => value.CreatedAt)
            .Take(500)
            .ToListAsync(token);
    }

    private async Task<IReadOnlyList<CivilEngineeringComplaintResolutionDto>> MapAsync(
        IReadOnlyCollection<CivilEngineeringMaintenanceIntake> intakes,
        CancellationToken token)
    {
        if (intakes.Count == 0) return [];

        var intakeIds = intakes.Select(value => value.Id).ToList();
        var ticketIds = intakes.Where(value => value.HelpdeskTicketId.HasValue).Select(value => value.HelpdeskTicketId!.Value).Distinct().ToList();
        var tickets = await db.EhcTickets.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && ticketIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, token);
        var assessments = await db.CivilEngineeringMaintenanceAssessments.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && intakeIds.Contains(value.IntakeId))
            .OrderByDescending(value => value.CreatedAt).ToListAsync(token);
        var assessmentByIntake = assessments.GroupBy(value => value.IntakeId).ToDictionary(value => value.Key, value => value.First());
        var assessmentIds = assessments.Select(value => value.Id).ToList();
        var handoffs = assessmentIds.Count == 0 ? [] : await db.CivilEngineeringMaintenanceCostingHandoffs.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && assessmentIds.Contains(value.AssessmentId))
            .OrderByDescending(value => value.CreatedAt).ToListAsync(token);
        var handoffByAssessment = handoffs.GroupBy(value => value.AssessmentId).ToDictionary(value => value.Key, value => value.First());
        var handoffIds = handoffs.Select(value => value.Id).ToList();
        var executions = handoffIds.Count == 0 ? [] : await db.CivilEngineeringMaintenanceExecutionLinks.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && handoffIds.Contains(value.HandoffId))
            .OrderByDescending(value => value.CreatedAt).ToListAsync(token);
        var executionByHandoff = executions.GroupBy(value => value.HandoffId).ToDictionary(value => value.Key, value => value.First());
        var executionIds = executions.Select(value => value.Id).ToList();
        var completions = executionIds.Count == 0 ? [] : await db.CivilEngineeringMaintenanceCompletionControls.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && executionIds.Contains(value.ExecutionLinkId))
            .OrderByDescending(value => value.CreatedAt).ToListAsync(token);
        var completionByExecution = completions.GroupBy(value => value.ExecutionLinkId).ToDictionary(value => value.Key, value => value.First());
        var workOrderIds = executions.Where(value => value.WorkOrderId.HasValue).Select(value => value.WorkOrderId!.Value).Distinct().ToList();
        var workOrders = workOrderIds.Count == 0 ? new Dictionary<Guid, ErpSystem.Core.Entities.Maintenance.WorkOrder>() : await db.WorkOrders.AsNoTracking()
            .Where(value => value.TenantId == TenantId && workOrderIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);

        return intakes.Select(intake =>
        {
            tickets.TryGetValue(intake.HelpdeskTicketId!.Value, out var ticket);
            assessmentByIntake.TryGetValue(intake.Id, out var assessment);
            if (assessment is null) return Result(intake, ticket, null, null, null, null, null);
            handoffByAssessment.TryGetValue(assessment.Id, out var handoff);
            if (handoff is null) return Result(intake, ticket, assessment, null, null, null, null);
            executionByHandoff.TryGetValue(handoff.Id, out var execution);
            if (execution is null) return Result(intake, ticket, assessment, handoff, null, null, null);
            completionByExecution.TryGetValue(execution.Id, out var completion);
            workOrders.TryGetValue(execution.WorkOrderId ?? Guid.Empty, out var workOrder);
            return Result(intake, ticket, assessment, handoff, execution, completion, workOrder);
        }).ToList();
    }

    private static CivilEngineeringComplaintResolutionDto Result(
        CivilEngineeringMaintenanceIntake intake,
        ErpSystem.Core.Entities.Ehc.EhcTicket? ticket,
        CivilEngineeringMaintenanceAssessment? assessment,
        CivilEngineeringMaintenanceCostingHandoff? handoff,
        CivilEngineeringMaintenanceExecutionLink? execution,
        CivilEngineeringMaintenanceCompletionControl? completion,
        ErpSystem.Core.Entities.Maintenance.WorkOrder? workOrder)
    {
        var state = CivilEngineeringComplaintResolutionProjectionPolicy.Derive(
            assessment?.Stage, handoff?.Stage, execution?.Stage, completion?.Stage,
            completion?.InspectionStatus, completion?.PaymentDirectionStatus);
        return new CivilEngineeringComplaintResolutionDto
        {
            HelpdeskTicketId = intake.HelpdeskTicketId!.Value,
            ComplaintTicketNumber = ticket?.TicketNumber ?? "Unavailable complaint",
            ComplaintSubject = ticket?.Subject,
            HelpdeskStatus = ticket?.Status.ToString() ?? "Unavailable",
            ComplaintLoggedAt = ticket?.CreatedAt ?? intake.CreatedAt,
            IntakeId = intake.Id,
            IntakeNumber = intake.IntakeNumber,
            IntakeStatus = intake.Status,
            AssessmentId = assessment?.Id,
            AssessmentStage = assessment?.Stage,
            CostingHandoffId = handoff?.Id,
            CostingStage = handoff?.Stage,
            ExecutionLinkId = execution?.Id,
            ExecutionStage = execution?.Stage,
            WorkOrderNumber = workOrder?.WorkOrderNumber,
            WorkOrderStatus = workOrder?.Status,
            CompletionControlId = completion?.Id,
            CompletionStage = completion?.Stage,
            InspectionStatus = completion?.InspectionStatus,
            PaymentDirectionStatus = completion?.PaymentDirectionStatus,
            ResolutionStage = state.Stage,
            ResolutionLabel = state.Label,
            ReadyForHelpdeskResolution = state.ReadyForHelpdeskResolution,
            LastActivityAt = Latest(ticket?.UpdatedAt, intake.UpdatedAt, assessment?.UpdatedAt, handoff?.UpdatedAt, execution?.UpdatedAt, completion?.UpdatedAt, completion?.ClosedAt, ticket?.CreatedAt, intake.CreatedAt)
        };
    }

    private static CivilEngineeringComplaintResolutionTimelineEntryDto Entry(DateTime occurredAt, string source, string action, string? stage, string? actorName) => new()
    {
        OccurredAt = occurredAt,
        Source = source,
        Action = action,
        Stage = stage,
        ActorName = actorName
    };

    private static DateTime Latest(params DateTime?[] values) => values.Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty(DateTime.UnixEpoch).Max();
}
