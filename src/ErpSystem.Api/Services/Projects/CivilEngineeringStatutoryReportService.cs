using System.Globalization;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Projects;

/// <summary>
/// Read-only Civil Engineering report provider.  It composes authoritative project, Civil,
/// Maintenance and DMS-control references through the shared report engine; it never owns or
/// changes those source lifecycles.
/// </summary>
public sealed class CivilEngineeringStatutoryReportService(
    ApplicationDbContext db,
    ICurrentUserProvider currentUser,
    IProjectService projects,
    IHttpContextAccessor httpContextAccessor,
    IAuthorizationService authorization) : ICivilEngineeringStatutoryReportService
{
    public bool CanHandle(string? reportQuery) => CivilEngineeringStatutoryReportCatalogue.Resolve(reportQuery) is not null;

    public bool OwnsIdentifier(string? reportQuery) =>
        !string.IsNullOrWhiteSpace(reportQuery) &&
        reportQuery.StartsWith(CivilEngineeringStatutoryReportCatalogue.QueryPrefix, StringComparison.OrdinalIgnoreCase);

    public string? ResolveCode(string? reportQuery) => CivilEngineeringStatutoryReportCatalogue.Resolve(reportQuery)?.Code;

    public async Task<bool> CanReadAsync(bool isAdministrator, CancellationToken cancellationToken = default)
    {
        try
        {
            await RequirePermissionAsync(CivilEngineeringStatutoryReportCatalogue.ReadPermission, isAdministrator);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task AuthorizeExportAsync(string reportQuery, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        _ = CivilEngineeringStatutoryReportCatalogue.Resolve(reportQuery)
            ?? throw new InvalidOperationException("The Civil Engineering system report is not registered.");
        await RequirePermissionAsync(CivilEngineeringStatutoryReportCatalogue.ExportPermission, isAdministrator);
    }

    public async Task<ReportResultDto> ExecuteAsync(
        string reportQuery,
        ExecuteReportDto request,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var definition = CivilEngineeringStatutoryReportCatalogue.Resolve(reportQuery)
            ?? throw new InvalidOperationException("The Civil Engineering system report is not registered.");
        await RequirePermissionAsync(CivilEngineeringStatutoryReportCatalogue.ReadPermission, isAdministrator);
        var filters = ReportFilters.Parse(request);
        var tenantId = EnsureTenant();
        var project = await projects.GetProjectByIdAsync(filters.ProjectId)
            ?? throw new KeyNotFoundException("The selected project was not found or is outside your assigned project scope.");

        var rows = definition.EffectiveSourceCode switch
        {
            CivilEngineeringStatutoryReportCatalogue.DesignBacklogCode => await DesignBacklogAsync(project, tenantId, cancellationToken),
            CivilEngineeringStatutoryReportCatalogue.FieldTaskRegisterCode => await FieldTasksAsync(project, tenantId, cancellationToken),
            CivilEngineeringStatutoryReportCatalogue.SupervisionControlCode => await SupervisionControlsAsync(project, tenantId, cancellationToken),
            CivilEngineeringStatutoryReportCatalogue.MaintenanceComplaintCode => await MaintenanceAndComplaintsAsync(project, tenantId, cancellationToken),
            CivilEngineeringStatutoryReportCatalogue.PermittingWatchCode => await PermittingWatchAsync(project, tenantId, cancellationToken),
            CivilEngineeringStatutoryReportCatalogue.CompletionHandoverCode => await CompletionAndHandoverAsync(project, tenantId, cancellationToken),
            CivilEngineeringStatutoryReportCatalogue.EngineeringWorkRegisterCode => await EngineeringWorkRegisterAsync(project, tenantId, cancellationToken),
            CivilEngineeringStatutoryReportCatalogue.CompletionCertificateReportCode => await CompletionCertificateReportAsync(project, tenantId, cancellationToken),
            CivilEngineeringStatutoryReportCatalogue.ProjectDashboardCode => await ProjectDashboardAsync(project, tenantId, cancellationToken),
            CivilEngineeringStatutoryReportCatalogue.EngineeringAuditTrailCode => await EngineeringAuditTrailAsync(project, tenantId, cancellationToken),
            _ => throw new InvalidOperationException("The Civil Engineering system report is not implemented.")
        };
        rows = FilterArchitectureRows(definition.Code, rows);

        return Page(rows.Where(row => filters.InRange(row.EffectiveAt)).ToList(), definition, request, filters);
    }

    private async Task<List<CivilReportRow>> EngineeringWorkRegisterAsync(ProjectDetailDto project, Guid tenantId, CancellationToken token)
    {
        var design = await DesignBacklogAsync(project, tenantId, token);
        var maintenance = await MaintenanceAndComplaintsAsync(project, tenantId, token);
        return design.Concat(maintenance).OrderByDescending(value => value.EffectiveAt).ToList();
    }

    private async Task<List<CivilReportRow>> CompletionCertificateReportAsync(ProjectDetailDto project, Guid tenantId, CancellationToken token)
    {
        var completion = await CompletionAndHandoverAsync(project, tenantId, token);
        var maintenance = await MaintenanceAndComplaintsAsync(project, tenantId, token);
        return completion.Concat(maintenance.Where(value => RecordType(value) == "Completion control"))
            .OrderByDescending(value => value.EffectiveAt).ToList();
    }

    private async Task<List<CivilReportRow>> ProjectDashboardAsync(ProjectDetailDto project, Guid tenantId, CancellationToken token)
    {
        var areas = new (string Name, List<CivilReportRow> Rows)[]
        {
            ("Design", await DesignBacklogAsync(project, tenantId, token)),
            ("Field tasks", await FieldTasksAsync(project, tenantId, token)),
            ("Supervision", await SupervisionControlsAsync(project, tenantId, token)),
            ("Maintenance and complaints", await MaintenanceAndComplaintsAsync(project, tenantId, token)),
            ("Permitting", await PermittingWatchAsync(project, tenantId, token)),
            ("Completion and handover", await CompletionAndHandoverAsync(project, tenantId, token))
        };

        return areas.Select(area =>
        {
            var lastActivity = area.Rows.Count == 0 ? DateTime.UtcNow : area.Rows.Max(value => value.EffectiveAt);
            var overdue = area.Rows.Count(value => BooleanValue(value, "IsOverdue"));
            var pending = area.Rows.Count(value => IsPending(value));
            return Row(lastActivity,
                ("ProjectCode", project.ProjectCode), ("Area", area.Name), ("TotalRecords", area.Rows.Count),
                ("OverdueRecords", overdue), ("PendingRecords", pending), ("LastActivityAt", lastActivity));
        }).ToList();
    }

    private async Task<List<CivilReportRow>> EngineeringAuditTrailAsync(ProjectDetailDto project, Guid tenantId, CancellationToken token)
    {
        var actions = CivilEngineeringAuditEventMap.Definitions.Select(value => value.Action).Distinct().ToArray();
        var projectId = project.Id.ToString("D");
        var logs = await db.AuditLogs.AsNoTracking().Where(value =>
                value.TenantId == tenantId && !value.IsDeleted && actions.Contains(value.Action) &&
                ((value.NewValues != null && value.NewValues.Contains(projectId)) ||
                 (value.OldValues != null && value.OldValues.Contains(projectId)) ||
                 (value.Resource == nameof(Project) && value.ResourceId == projectId)))
            .OrderByDescending(value => value.Timestamp)
            .Select(value => new
            {
                value.Action, value.Resource, value.ResourceId, value.Username, value.Timestamp, value.NewValues
            }).ToListAsync(token);

        return logs.Select(value => Row(value.Timestamp,
            ("ProjectCode", project.ProjectCode), ("Action", value.Action), ("Resource", value.Resource),
            ("ResourceId", value.ResourceId ?? string.Empty), ("Actor", value.Username), ("OccurredAt", value.Timestamp),
            ("CorrelationId", CorrelationId(value.NewValues)))).ToList();
    }

    private static List<CivilReportRow> FilterArchitectureRows(string reportCode, List<CivilReportRow> rows) => reportCode switch
    {
        CivilEngineeringStatutoryReportCatalogue.InspectionReportCode => rows
            .Where(value => RecordType(value) == "Civil inspection").ToList(),
        CivilEngineeringStatutoryReportCatalogue.SiteInstructionLogCode => rows
            .Where(value => RecordType(value) == "Site instruction").ToList(),
        CivilEngineeringStatutoryReportCatalogue.ProgressReportCode => rows
            .Where(value => RecordType(value) == "Weekly supervision").ToList(),
        CivilEngineeringStatutoryReportCatalogue.DefectReportCode => rows
            .Where(value => RecordType(value) is "Snag" or "Defect" or "Warranty defect").ToList(),
        _ => rows
    };

    private static string RecordType(CivilReportRow row) =>
        row.Values.TryGetValue("RecordType", out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    private static bool BooleanValue(CivilReportRow row, string key) =>
        row.Values.TryGetValue(key, out var value) && value is bool flag && flag;

    private static bool IsPending(CivilReportRow row)
    {
        var status = row.Values.TryGetValue("Status", out var statusValue)
            ? Convert.ToString(statusValue, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;
        var approval = row.Values.TryGetValue("ApprovalStatus", out var approvalValue)
            ? Convert.ToString(approvalValue, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;
        return status.Contains("Pending", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("Draft", StringComparison.OrdinalIgnoreCase) ||
               approval.Contains("Pending", StringComparison.OrdinalIgnoreCase) ||
               approval.Contains("Not started", StringComparison.OrdinalIgnoreCase);
    }

    private static string CorrelationId(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("correlationId", out var direct)
                ? direct.ToString()
                : document.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Object &&
                  value.TryGetProperty("correlationId", out var nested)
                    ? nested.ToString()
                    : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private async Task<List<CivilReportRow>> DesignBacklogAsync(ProjectDetailDto project, Guid tenantId, CancellationToken token)
    {
        var cases = await db.ProjectCivilDesignCases.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new
            {
                value.ReferenceNumber, value.Title, value.Stage, value.Status, value.ApprovalStatus,
                value.CurrentAssigneeUserId, value.CurrentDueAt, value.CreatedAt
            }).ToListAsync(token);
        var documents = await db.ProjectCivilEngineeringDocuments.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.DesignCase.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new
            {
                value.DocumentReferenceSnapshot, value.DocumentTitleSnapshot, value.FileCategory, value.Status,
                value.OwnerUserId, value.ReviewerUserId, value.ReviewedAt, value.CreatedAt
            }).ToListAsync(token);
        var userNames = await UserNamesAsync(
            cases.Where(value => value.CurrentAssigneeUserId.HasValue).Select(value => value.CurrentAssigneeUserId!.Value)
                .Concat(documents.Select(value => value.OwnerUserId)).Concat(documents.Select(value => value.ReviewerUserId)), tenantId, token);
        var now = DateTime.UtcNow;
        var rows = new List<CivilReportRow>();

        rows.AddRange(cases.Select(value => Row(value.CreatedAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", "Design case"), ("Reference", value.ReferenceNumber),
            ("Title", value.Title), ("Stage", value.Stage), ("Status", value.Status), ("ApprovalStatus", value.ApprovalStatus),
            ("Assignee", UserName(userNames, value.CurrentAssigneeUserId)), ("DueDate", value.CurrentDueAt),
            ("IsOverdue", IsOverdue(value.CurrentDueAt, value.Status, "Approved", "Rejected")), ("RecordedAt", value.CreatedAt))));
        rows.AddRange(documents.Select(value => Row(value.CreatedAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", $"Engineering document ({value.FileCategory})"),
            ("Reference", value.DocumentReferenceSnapshot), ("Title", value.DocumentTitleSnapshot), ("Stage", "Versioned review"),
            ("Status", value.Status.ToString()), ("ApprovalStatus", value.ReviewedAt.HasValue ? "Reviewed" : "Pending"),
            ("Assignee", UserName(userNames, value.ReviewedAt.HasValue ? value.ReviewerUserId : value.OwnerUserId)),
            ("DueDate", null), ("IsOverdue", false), ("RecordedAt", value.CreatedAt))));

        return rows.OrderByDescending(value => value.EffectiveAt).ToList();
    }

    private async Task<List<CivilReportRow>> FieldTasksAsync(ProjectDetailDto project, Guid tenantId, CancellationToken token)
    {
        var tasks = await db.ProjectCivilDirectTaskControls.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new
            {
                value.WorkItemId, value.AssignedToUserId, value.AssignedRoleName, value.Urgency, value.Status,
                value.ApprovalStatus, value.ProgressPercent, value.DueDate, value.CreatedAt
            }).ToListAsync(token);
        var workItemIds = tasks.Select(value => value.WorkItemId).Distinct().ToList();
        var workItemNames = workItemIds.Count == 0 ? new Dictionary<Guid, string>() : await db.ProjectWorkItems.AsNoTracking()
            .Where(value => value.TenantId == tenantId && workItemIds.Contains(value.Id) && !value.IsDeleted)
            .Select(value => new { value.Id, value.Title }).ToDictionaryAsync(value => value.Id, value => value.Title, token);
        var userNames = await UserNamesAsync(tasks.Select(value => value.AssignedToUserId), tenantId, token);

        return tasks.Select(value => Row(value.CreatedAt,
                ("ProjectCode", project.ProjectCode), ("TaskTitle", workItemNames.GetValueOrDefault(value.WorkItemId, "Unavailable project task")),
                ("AssignedTo", UserName(userNames, value.AssignedToUserId)), ("AssignedRole", value.AssignedRoleName),
                ("Urgency", value.Urgency.ToString()), ("Status", value.Status), ("ApprovalStatus", value.ApprovalStatus),
                ("ProgressPercent", value.ProgressPercent), ("DueDate", value.DueDate),
                ("IsOverdue", IsOverdue(value.DueDate, value.Status, "Accepted", "Returned")), ("RecordedAt", value.CreatedAt)))
            .OrderByDescending(value => value.EffectiveAt).ToList();
    }

    private async Task<List<CivilReportRow>> SupervisionControlsAsync(ProjectDetailDto project, Guid tenantId, CancellationToken token)
    {
        var rfis = await db.ProjectCivilRfiRoutings.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new
            {
                value.ProjectRfi.ReferenceNumber, value.ProjectRfi.Subject, value.Status, value.ApprovalStatus,
                value.ProjectRfi.ResponseDueDate, EvidenceCount = value.Evidence.Count, value.CreatedAt
            }).ToListAsync(token);
        var instructions = await db.ProjectCivilSiteInstructionRoutings.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new
            {
                value.ProjectSiteInstruction.ReferenceNumber, value.ProjectSiteInstruction.Title, value.Status, value.ApprovalStatus,
                value.ProjectSiteInstruction.EffectiveDate, EvidenceCount = value.Evidence.Count, value.CreatedAt
            }).ToListAsync(token);
        var weeklyReports = await db.ProjectCivilWeeklySupervisionReports.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new { value.WeekStart, value.WeekEnd, value.Status, value.ApprovalStatus, value.DueAt, value.EscalatedAt, EvidenceCount = value.Evidence.Count, value.CreatedAt })
            .ToListAsync(token);
        var qualityTests = await db.ProjectCivilQualityTestReports.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.TestedAt)
            .Select(value => new { value.ReportReference, value.ResultSummary, value.TestedAt, value.ResultStatus, value.Status, value.ApprovalStatus, value.AcceptanceBlocked, value.CreatedAt })
            .ToListAsync(token);
        var endorsements = await db.ProjectCivilIpcEndorsements.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.SubmittedAt)
            .Select(value => new { value.ProjectPaymentCertificateId, value.Status, value.SubmittedAt, value.ReviewedAt, value.RequiresDmsEvidence, value.EvidenceDocumentRecordId, value.CreatedAt })
            .ToListAsync(token);
        var certificateIds = endorsements.Select(value => value.ProjectPaymentCertificateId).Distinct().ToList();
        Dictionary<Guid, (string Reference, string Title, DateTime? DueAt)> certificateNames;
        if (certificateIds.Count == 0)
            certificateNames = [];
        else
            certificateNames = await db.ProjectPaymentCertificates.AsNoTracking()
                .Where(value => value.TenantId == tenantId && certificateIds.Contains(value.Id) && !value.IsDeleted)
                .Select(value => new { value.Id, value.CertificateNumber, value.Title, value.PaymentDueDate })
                .ToDictionaryAsync(value => value.Id, value => (Reference: value.CertificateNumber ?? "Certificate", Title: value.Title, DueAt: value.PaymentDueDate), token);
        var rows = new List<CivilReportRow>();

        rows.AddRange(rfis.Select(value => Row(value.CreatedAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", "RFI"), ("Reference", value.ReferenceNumber), ("Title", value.Subject),
            ("Status", value.Status), ("ApprovalStatus", value.ApprovalStatus), ("DueDate", value.ResponseDueDate),
            ("IsOverdue", IsOverdue(value.ResponseDueDate, value.Status, CivilEngineeringRfiRoutingStatuses.Answered, CivilEngineeringRfiRoutingStatuses.Closed)),
            ("EvidenceStatus", value.EvidenceCount > 0 ? "Linked" : "Missing"), ("RecordedAt", value.CreatedAt))));
        rows.AddRange(instructions.Select(value => Row(value.CreatedAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", "Site instruction"), ("Reference", value.ReferenceNumber), ("Title", value.Title),
            ("Status", value.Status), ("ApprovalStatus", value.ApprovalStatus), ("DueDate", value.EffectiveDate),
            ("IsOverdue", IsOverdue(value.EffectiveDate, value.Status, CivilEngineeringSiteInstructionRoutingStatuses.Closed, CivilEngineeringSiteInstructionRoutingStatuses.Rejected)),
            ("EvidenceStatus", value.EvidenceCount > 0 ? "Linked" : "Missing"), ("RecordedAt", value.CreatedAt))));
        rows.AddRange(weeklyReports.Select(value => Row(value.CreatedAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", "Weekly supervision"), ("Reference", $"{value.WeekStart:yyyy-MM-dd} to {value.WeekEnd:yyyy-MM-dd}"),
            ("Title", "Civil weekly supervision report"), ("Status", value.Status), ("ApprovalStatus", value.ApprovalStatus), ("DueDate", value.DueAt),
            ("IsOverdue", IsOverdue(value.DueAt, value.Status, "Approved", "Rejected")),
            ("EvidenceStatus", value.EvidenceCount > 0 ? "Linked" : "Missing"), ("RecordedAt", value.CreatedAt))));
        rows.AddRange(qualityTests.Select(value => Row(value.TestedAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", "Quality test"), ("Reference", value.ReportReference), ("Title", value.ResultSummary),
            ("Status", value.Status), ("ApprovalStatus", value.ApprovalStatus), ("DueDate", null), ("IsOverdue", false),
            ("EvidenceStatus", value.AcceptanceBlocked ? "Acceptance blocked" : "Linked"), ("RecordedAt", value.TestedAt))));
        rows.AddRange(endorsements.Select(value =>
        {
            (string Reference, string Title, DateTime? DueAt) certificate = certificateNames.GetValueOrDefault(
                value.ProjectPaymentCertificateId,
                (Reference: "Unavailable certificate", Title: "Unavailable certificate", DueAt: (DateTime?)null));
            return Row(value.SubmittedAt,
                ("ProjectCode", project.ProjectCode), ("RecordType", "IPC endorsement"), ("Reference", certificate.Reference), ("Title", certificate.Title),
                ("Status", value.Status), ("ApprovalStatus", value.ReviewedAt.HasValue ? "Reviewed" : "Pending"), ("DueDate", certificate.DueAt),
                ("IsOverdue", IsOverdue(certificate.DueAt, value.Status, CivilEngineeringIpcEndorsementStatuses.Endorsed)),
                ("EvidenceStatus", value.RequiresDmsEvidence ? value.EvidenceDocumentRecordId.HasValue ? "Linked" : "Missing" : "Not required"),
                ("RecordedAt", value.SubmittedAt));
        }));

        return rows.OrderByDescending(value => value.EffectiveAt).ToList();
    }

    private async Task<List<CivilReportRow>> MaintenanceAndComplaintsAsync(ProjectDetailDto project, Guid tenantId, CancellationToken token)
    {
        var intakes = await db.CivilEngineeringMaintenanceIntakes.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new { value.Id, value.IntakeNumber, value.Title, value.WorkClassification, value.Status, value.CreatedAt })
            .ToListAsync(token);
        var intakeIds = intakes.Select(value => value.Id).ToList();
        var assessments = await db.CivilEngineeringMaintenanceAssessments.AsNoTracking().Where(value =>
                value.TenantId == tenantId && intakeIds.Contains(value.IntakeId) && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new { value.Id, value.IntakeId, value.Stage, value.Status, value.ApprovalStatus, value.CurrentDueAt, value.CreatedAt })
            .ToListAsync(token);
        var assessmentIds = assessments.Select(value => value.Id).ToList();
        var handoffs = await db.CivilEngineeringMaintenanceCostingHandoffs.AsNoTracking().Where(value =>
                value.TenantId == tenantId && assessmentIds.Contains(value.AssessmentId) && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new { value.Id, value.AssessmentId, value.Stage, value.Status, value.ApprovalStatus, value.CreatedAt })
            .ToListAsync(token);
        var handoffIds = handoffs.Select(value => value.Id).ToList();
        var executions = await db.CivilEngineeringMaintenanceExecutionLinks.AsNoTracking().Where(value =>
                value.TenantId == tenantId && handoffIds.Contains(value.HandoffId) && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new { value.Id, value.HandoffId, value.Stage, value.Status, value.CreatedAt })
            .ToListAsync(token);
        var executionIds = executions.Select(value => value.Id).ToList();
        var completions = await db.CivilEngineeringMaintenanceCompletionControls.AsNoTracking().Where(value =>
                value.TenantId == tenantId && executionIds.Contains(value.ExecutionLinkId) && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new { value.ExecutionLinkId, value.Stage, value.Status, value.CompletionReportedAt, value.ClosedAt, value.CreatedAt })
            .ToListAsync(token);
        var intakeById = intakes.ToDictionary(value => value.Id);
        var assessmentById = assessments.ToDictionary(value => value.Id);
        var handoffById = handoffs.ToDictionary(value => value.Id);
        var executionById = executions.ToDictionary(value => value.Id);
        var rows = new List<CivilReportRow>();

        rows.AddRange(intakes.Select(value => Row(value.CreatedAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", value.WorkClassification.ToString().Contains("Complaint", StringComparison.Ordinal) ? "Complaint intake" : "Maintenance intake"),
            ("Reference", value.IntakeNumber), ("Title", value.Title), ("Stage", "Intake"), ("Status", value.Status), ("ApprovalStatus", "Not applicable"),
            ("DueDate", null), ("IsOverdue", false), ("RecordedAt", value.CreatedAt))));
        rows.AddRange(assessments.Select(value =>
        {
            var intake = intakeById.GetValueOrDefault(value.IntakeId);
            return Row(value.CreatedAt,
                ("ProjectCode", project.ProjectCode), ("RecordType", "Maintenance assessment"), ("Reference", intake?.IntakeNumber ?? "Unavailable intake"),
                ("Title", intake?.Title ?? "Unavailable maintenance intake"), ("Stage", value.Stage), ("Status", value.Status), ("ApprovalStatus", value.ApprovalStatus),
                ("DueDate", value.CurrentDueAt), ("IsOverdue", IsOverdue(value.CurrentDueAt, value.Status, "Approved", "Rejected")), ("RecordedAt", value.CreatedAt));
        }));
        rows.AddRange(handoffs.Select(value =>
        {
            var intake = assessmentById.TryGetValue(value.AssessmentId, out var assessment) ? intakeById.GetValueOrDefault(assessment.IntakeId) : null;
            return Row(value.CreatedAt,
                ("ProjectCode", project.ProjectCode), ("RecordType", "Costing and award handoff"), ("Reference", intake?.IntakeNumber ?? "Unavailable intake"),
                ("Title", intake?.Title ?? "Unavailable maintenance intake"), ("Stage", value.Stage), ("Status", value.Status), ("ApprovalStatus", value.ApprovalStatus),
                ("DueDate", null), ("IsOverdue", false), ("RecordedAt", value.CreatedAt));
        }));
        rows.AddRange(executions.Select(value =>
        {
            var intake = handoffById.TryGetValue(value.HandoffId, out var handoff) && assessmentById.TryGetValue(handoff.AssessmentId, out var assessment)
                ? intakeById.GetValueOrDefault(assessment.IntakeId) : null;
            return Row(value.CreatedAt,
                ("ProjectCode", project.ProjectCode), ("RecordType", "Maintenance execution link"), ("Reference", intake?.IntakeNumber ?? "Unavailable intake"),
                ("Title", intake?.Title ?? "Unavailable maintenance intake"), ("Stage", value.Stage), ("Status", value.Status), ("ApprovalStatus", "Owner lifecycle"),
                ("DueDate", null), ("IsOverdue", false), ("RecordedAt", value.CreatedAt));
        }));
        rows.AddRange(completions.Select(value =>
        {
            var intake = executionById.TryGetValue(value.ExecutionLinkId, out var execution) && handoffById.TryGetValue(execution.HandoffId, out var handoff) &&
                         assessmentById.TryGetValue(handoff.AssessmentId, out var assessment)
                ? intakeById.GetValueOrDefault(assessment.IntakeId) : null;
            return Row(value.CompletionReportedAt,
                ("ProjectCode", project.ProjectCode), ("RecordType", "Completion control"), ("Reference", intake?.IntakeNumber ?? "Unavailable intake"),
                ("Title", intake?.Title ?? "Unavailable maintenance intake"), ("Stage", value.Stage), ("Status", value.Status), ("ApprovalStatus", value.ClosedAt.HasValue ? "Closed" : "In review"),
                ("DueDate", null), ("IsOverdue", false), ("RecordedAt", value.CompletionReportedAt));
        }));

        return rows.OrderByDescending(value => value.EffectiveAt).ToList();
    }

    private async Task<List<CivilReportRow>> PermittingWatchAsync(ProjectDetailDto project, Guid tenantId, CancellationToken token)
    {
        var files = await db.ProjectCivilDevelopmentApprovalFiles.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new { value.Id, value.FileNumber, value.ApplicationReference, value.CurrentSection, value.DueDate, value.Status, value.CreatedAt })
            .ToListAsync(token);
        var fileIds = files.Select(value => value.Id).ToList();
        var handoffs = await db.ProjectCivilDevelopmentApprovalFileHandoffs.AsNoTracking().Where(value =>
                value.TenantId == tenantId && fileIds.Contains(value.DevelopmentApprovalFileId) && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new { value.Id, value.DevelopmentApprovalFileId, value.FromSection, value.ToSection, value.DueDate, value.CreatedAt })
            .ToListAsync(token);
        var reviews = await db.ProjectCivilDevelopmentApprovalEngineeringReviews.AsNoTracking().Where(value =>
                value.TenantId == tenantId && fileIds.Contains(value.DevelopmentApprovalFileId) && !value.IsDeleted)
            .OrderByDescending(value => value.ReviewedAt)
            .Select(value => new { value.Id, value.DevelopmentApprovalFileId, value.Stage, value.Status, value.ApprovalStatus, value.ReviewedAt, value.CreatedAt })
            .ToListAsync(token);
        var reviewIds = reviews.Select(value => value.Id).ToList();
        var decisions = await db.ProjectCivilDevelopmentApprovalEngineeringReviewDecisions.AsNoTracking().Where(value =>
                value.TenantId == tenantId && reviewIds.Contains(value.EngineeringReviewId) && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new { value.EngineeringReviewId, value.Outcome, value.WorkflowOutcome, value.CreatedAt })
            .ToListAsync(token);
        var fileById = files.ToDictionary(value => value.Id);
        var reviewById = reviews.ToDictionary(value => value.Id);
        var rows = new List<CivilReportRow>();

        rows.AddRange(files.Select(value => Row(value.CreatedAt,
            ("ProjectCode", project.ProjectCode), ("FileNumber", value.FileNumber), ("ApplicationReference", value.ApplicationReference),
            ("RecordType", "Development approval file"), ("CurrentSection", value.CurrentSection), ("Stage", value.Status.ToString()), ("Status", value.Status.ToString()),
            ("DueDate", value.DueDate), ("IsOverdue", IsOverdue(value.DueDate, value.Status.ToString(), "Completed")), ("RecordedAt", value.CreatedAt))));
        rows.AddRange(handoffs.Select(value =>
        {
            var file = fileById.GetValueOrDefault(value.DevelopmentApprovalFileId);
            return Row(value.CreatedAt,
                ("ProjectCode", project.ProjectCode), ("FileNumber", file?.FileNumber ?? "Unavailable file"), ("ApplicationReference", file?.ApplicationReference ?? "Unavailable"),
                ("RecordType", "Section handoff"), ("CurrentSection", value.ToSection.ToString()), ("Stage", $"{value.FromSection} to {value.ToSection}"), ("Status", "Routed"),
                ("DueDate", value.DueDate), ("IsOverdue", IsOverdue(value.DueDate, "Routed")), ("RecordedAt", value.CreatedAt));
        }));
        rows.AddRange(reviews.Select(value =>
        {
            var file = fileById.GetValueOrDefault(value.DevelopmentApprovalFileId);
            return Row(value.ReviewedAt,
                ("ProjectCode", project.ProjectCode), ("FileNumber", file?.FileNumber ?? "Unavailable file"), ("ApplicationReference", file?.ApplicationReference ?? "Unavailable"),
                ("RecordType", "Engineering review"), ("CurrentSection", "Civil Engineering"), ("Stage", value.Stage.ToString()), ("Status", value.Status),
                ("DueDate", file?.DueDate), ("IsOverdue", IsOverdue(file?.DueDate, value.Status, "HodApproved", "HodRejected")), ("RecordedAt", value.ReviewedAt));
        }));
        rows.AddRange(decisions.Select(value =>
        {
            var file = reviewById.TryGetValue(value.EngineeringReviewId, out var review) ? fileById.GetValueOrDefault(review.DevelopmentApprovalFileId) : null;
            return Row(value.CreatedAt,
                ("ProjectCode", project.ProjectCode), ("FileNumber", file?.FileNumber ?? "Unavailable file"), ("ApplicationReference", file?.ApplicationReference ?? "Unavailable"),
                ("RecordType", "HOD decision"), ("CurrentSection", "Head of Department"), ("Stage", value.Outcome.ToString()), ("Status", value.WorkflowOutcome),
                ("DueDate", null), ("IsOverdue", false), ("RecordedAt", value.CreatedAt));
        }));

        return rows.OrderByDescending(value => value.EffectiveAt).ToList();
    }

    private async Task<List<CivilReportRow>> CompletionAndHandoverAsync(ProjectDetailDto project, Guid tenantId, CancellationToken token)
    {
        var handoverItems = await db.ProjectHandoverItems.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.CompletedDate ?? value.CreatedAt)
            .Select(value => new { value.HandoverType, value.ReferenceNumber, value.Title, value.Status, value.TargetDate, value.CompletedDate, value.CreatedAt })
            .ToListAsync(token);
        var snags = await db.ProjectSnagItems.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.ReportedDate)
            .Select(value => new { value.Title, value.Severity, value.Status, value.ReportedDate, value.TargetClosureDate, value.ClosedDate })
            .ToListAsync(token);
        var defects = await db.ProjectDefectLiabilityCases.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.ReportedDate)
            .Select(value => new { value.Title, value.Status, value.ReportedDate, value.TargetResolutionDate, value.ResolvedDate, value.IsWarrantyRelated, value.WarrantyExpiryDate })
            .ToListAsync(token);
        var inspections = await db.ProjectCivilInspectionControls.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.ScheduledAt)
            .Select(value => new
            {
                value.Purpose, value.Stage, value.Status, value.ScheduledAt, value.InspectedAt, value.ReinspectedAt, value.ClosedAt,
                value.InspectionDocumentRecordId, value.ReinspectionDocumentRecordId, value.ClosureDocumentRecordId
            }).ToListAsync(token);
        var drawings = await db.ProjectDrawings.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && value.IsAsBuilt && !value.IsDeleted)
            .OrderByDescending(value => value.ApprovedDate ?? value.IssuedDate ?? value.CreatedAt)
            .Select(value => new { value.DrawingNumber, value.Title, value.Discipline, value.Revision, value.Status, value.ApprovedDate, value.IssuedDate, value.CreatedAt })
            .ToListAsync(token);
        var closeouts = await db.ProcurementWorksCloseoutActions.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.DecidedAtUtc ?? value.SubmittedAtUtc)
            .Select(value => new
            {
                value.Sequence, value.ActionType, value.Status, value.SubmittedAtUtc, value.DecidedAtUtc, value.DefectsLiabilityEndsAtUtc,
                value.WorkflowInstanceId, AsBuiltLinked = value.Evidence.Any(evidence => evidence.RequirementKey == "as-built-drawing-current-version" && !evidence.IsDeleted)
            }).ToListAsync(token);
        var closure = await db.ProjectClosures.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .Select(value => new { value.Status, value.SubmittedAt, value.ApprovedAt, value.AssetsReconciled, value.OpenItemsDisposed, value.CreatedAt })
            .FirstOrDefaultAsync(token);
        var estateAssets = await db.EstateManagedAssets.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .Select(value => new { value.AssetCode, value.Name, value.Status, value.IsPublishedFromProject, value.PublishedFromProjectAt, value.CreatedAt })
            .ToListAsync(token);
        var links = await db.ProjectAssetLinks.AsNoTracking().Where(value =>
                value.TenantId == tenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .Select(value => new { value.MaintenanceAssetId, value.FixedAssetId, value.CompanyAssetId, value.JobCardId, value.LinkType, value.Status, value.ReconciliationKey, value.CreatedAt })
            .ToListAsync(token);
        var fixedAssetIds = links.Where(value => value.FixedAssetId.HasValue).Select(value => value.FixedAssetId!.Value).Distinct().ToList();
        var fixedAssets = fixedAssetIds.Count == 0 ? new Dictionary<Guid, string>() : await db.FixedAssets.AsNoTracking()
            .Where(value => value.TenantId == tenantId && fixedAssetIds.Contains(value.Id) && !value.IsDeleted)
            .Select(value => new { value.Id, Label = value.AssetCode + " - " + value.Name })
            .ToDictionaryAsync(value => value.Id, value => value.Label, token);
        var rows = new List<CivilReportRow>();

        rows.AddRange(handoverItems.Select(value => Row(value.CompletedDate ?? value.CreatedAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", "Handover item"), ("Reference", value.ReferenceNumber ?? value.HandoverType),
            ("Title", value.Title), ("Stage", value.HandoverType), ("Status", value.Status), ("ApprovalStatus", "Projects lifecycle"),
            ("DueDate", value.TargetDate), ("IsOverdue", IsOverdue(value.TargetDate, value.Status, ProjectHandoverItemStatuses.Completed, ProjectHandoverItemStatuses.Waived)),
            ("EvidenceStatus", "Tracked by governed closeout where required"), ("RecordedAt", value.CompletedDate ?? value.CreatedAt))));
        rows.AddRange(snags.Select(value => Row(value.ClosedDate ?? value.ReportedDate,
            ("ProjectCode", project.ProjectCode), ("RecordType", "Snag"), ("Reference", value.Severity), ("Title", value.Title),
            ("Stage", "Snag clearance"), ("Status", value.Status), ("ApprovalStatus", "Projects lifecycle"), ("DueDate", value.TargetClosureDate),
            ("IsOverdue", IsOverdue(value.TargetClosureDate, value.Status, ProjectSnagStatuses.Closed)), ("EvidenceStatus", "Projects record"),
            ("RecordedAt", value.ClosedDate ?? value.ReportedDate))));
        rows.AddRange(defects.Select(value => Row(value.ResolvedDate ?? value.ReportedDate,
            ("ProjectCode", project.ProjectCode), ("RecordType", value.IsWarrantyRelated ? "Warranty defect" : "Defect"),
            ("Reference", value.WarrantyExpiryDate?.ToString("yyyy-MM-dd") ?? "Defect liability"), ("Title", value.Title),
            ("Stage", value.IsWarrantyRelated ? "Defects liability" : "Rectification"), ("Status", value.Status), ("ApprovalStatus", "Projects/Maintenance lifecycle"),
            ("DueDate", value.TargetResolutionDate), ("IsOverdue", IsOverdue(value.TargetResolutionDate, value.Status, ProjectDefectLiabilityStatuses.Resolved, ProjectDefectLiabilityStatuses.Closed, ProjectDefectLiabilityStatuses.WarrantyExpired)),
            ("EvidenceStatus", "Projects and linked Maintenance history"), ("RecordedAt", value.ResolvedDate ?? value.ReportedDate))));
        rows.AddRange(inspections.Select(value => Row(value.ClosedAt ?? value.ReinspectedAt ?? value.InspectedAt ?? value.ScheduledAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", "Civil inspection"), ("Reference", value.Stage), ("Title", value.Purpose),
            ("Stage", value.Stage), ("Status", value.Status), ("ApprovalStatus", "Independent inspection control"), ("DueDate", value.ScheduledAt),
            ("IsOverdue", IsOverdue(value.ScheduledAt, value.Status, CivilEngineeringInspectionStatuses.Passed, CivilEngineeringInspectionStatuses.Closed)),
            ("EvidenceStatus", value.ClosureDocumentRecordId.HasValue || value.ReinspectionDocumentRecordId.HasValue || value.InspectionDocumentRecordId.HasValue ? "Central DMS linked" : "Plan evidence only"),
            ("RecordedAt", value.ClosedAt ?? value.ReinspectedAt ?? value.InspectedAt ?? value.ScheduledAt))));
        rows.AddRange(drawings.Select(value => Row(value.ApprovedDate ?? value.IssuedDate ?? value.CreatedAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", "As-built drawing"), ("Reference", value.DrawingNumber), ("Title", value.Title),
            ("Stage", value.Discipline), ("Status", value.Status), ("ApprovalStatus", value.Status == ProjectDrawingStatuses.ApprovedAsBuilt ? "Approved as-built" : "Requires closeout validation"),
            ("DueDate", null), ("IsOverdue", false), ("EvidenceStatus", "Validated by Works Closeout before final closure"), ("RecordedAt", value.ApprovedDate ?? value.IssuedDate ?? value.CreatedAt))));
        rows.AddRange(closeouts.Select(value => Row(value.DecidedAtUtc ?? value.SubmittedAtUtc,
            ("ProjectCode", project.ProjectCode), ("RecordType", "Works closeout"), ("Reference", $"{value.ActionType} #{value.Sequence}"),
            ("Title", "Governed Works closeout action"), ("Stage", value.ActionType.ToString()), ("Status", value.Status.ToString()),
            ("ApprovalStatus", value.WorkflowInstanceId.HasValue ? "Shared workflow" : "Not started"), ("DueDate", value.DefectsLiabilityEndsAtUtc),
            ("IsOverdue", false), ("EvidenceStatus", value.AsBuiltLinked ? "Current as-built DMS evidence linked" : "As-built evidence not applicable or pending"),
            ("RecordedAt", value.DecidedAtUtc ?? value.SubmittedAtUtc))));
        rows.AddRange(estateAssets.Select(value => Row(value.PublishedFromProjectAt ?? value.CreatedAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", "Property history link"), ("Reference", value.AssetCode), ("Title", value.Name),
            ("Stage", "Estate Managed Asset"), ("Status", value.Status.ToString()), ("ApprovalStatus", value.IsPublishedFromProject ? "Published from project" : "Project linked"),
            ("DueDate", null), ("IsOverdue", false), ("EvidenceStatus", "Authoritative Property record"), ("RecordedAt", value.PublishedFromProjectAt ?? value.CreatedAt))));
        rows.AddRange(links.Select(value => Row(value.CreatedAt,
            ("ProjectCode", project.ProjectCode), ("RecordType", "Asset reconciliation link"),
            ("Reference", value.FixedAssetId.HasValue ? fixedAssets.GetValueOrDefault(value.FixedAssetId.Value, "Unavailable fixed asset") : value.JobCardId?.ToString() ?? value.MaintenanceAssetId?.ToString() ?? value.CompanyAssetId?.ToString() ?? "Project asset link"),
            ("Title", value.LinkType), ("Stage", "Project reconciliation"), ("Status", value.Status), ("ApprovalStatus", value.ReconciliationKey is { Length: 64 } ? "Idempotent controlled link" : "Legacy link"),
            ("DueDate", null), ("IsOverdue", false), ("EvidenceStatus", "Authoritative owner linked"), ("RecordedAt", value.CreatedAt))));
        if (closure != null)
        {
            rows.Add(Row(closure.ApprovedAt ?? closure.SubmittedAt ?? closure.CreatedAt,
                ("ProjectCode", project.ProjectCode), ("RecordType", "Project closure"), ("Reference", "Project closure"), ("Title", "Approved project closure control"),
                ("Stage", "Closure"), ("Status", closure.Status), ("ApprovalStatus", closure.ApprovedAt.HasValue ? "Shared workflow approved" : "Pending"),
                ("DueDate", null), ("IsOverdue", false), ("EvidenceStatus", closure.AssetsReconciled && closure.OpenItemsDisposed ? "Asset and open-item reconciliation confirmed" : "Reconciliation incomplete"),
                ("RecordedAt", closure.ApprovedAt ?? closure.SubmittedAt ?? closure.CreatedAt)));
        }

        return rows.OrderByDescending(value => value.EffectiveAt).ToList();
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> userIds, Guid tenantId, CancellationToken token)
    {
        var ids = userIds.Where(value => value != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return [];
        return await db.Users.AsNoTracking().Where(value => value.TenantId == tenantId && ids.Contains(value.Id))
            .Select(value => new { value.Id, value.FirstName, value.LastName, value.UserName })
            .ToDictionaryAsync(value => value.Id, value => string.IsNullOrWhiteSpace($"{value.FirstName} {value.LastName}".Trim()) ? value.UserName ?? "Unavailable user" : $"{value.FirstName} {value.LastName}".Trim(), token);
    }

    private async Task RequirePermissionAsync(string permission, bool isAdministrator)
    {
        if (isAdministrator) return;
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("Authentication is required to access Civil Engineering reports.");
        if (!(await authorization.AuthorizeAsync(principal, permission)).Succeeded)
            throw new UnauthorizedAccessException($"Permission '{permission}' is required.");
    }

    private Guid EnsureTenant()
    {
        if (!currentUser.IsAuthenticated || currentUser.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("A valid tenant context is required to access Civil Engineering reports.");
        return currentUser.TenantId;
    }

    private static string UserName(IReadOnlyDictionary<Guid, string> names, Guid? userId) =>
        userId.HasValue && names.TryGetValue(userId.Value, out var name) ? name : "Unassigned";

    private static bool IsOverdue(DateTime? dueAt, string? status, params string[] terminalStatuses) =>
        dueAt.HasValue && dueAt.Value < DateTime.UtcNow && !terminalStatuses.Contains(status ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private static CivilReportRow Row(DateTime effectiveAt, params (string Key, object? Value)[] values) =>
        new(effectiveAt, values.ToDictionary(value => value.Key, value => value.Value!));

    private static ReportResultDto Page(
        IReadOnlyList<CivilReportRow> rows,
        CivilEngineeringSystemReportDefinition definition,
        ExecuteReportDto request,
        ReportFilters filters)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 1000);
        if (request.MaxRows is > 0) pageSize = Math.Min(pageSize, Math.Clamp(request.MaxRows.Value, 1, 1000));
        var totalPages = rows.Count == 0 ? 0 : (int)Math.Ceiling(rows.Count / (double)pageSize);
        return new ReportResultDto
        {
            TotalRows = rows.Count,
            Columns = definition.Columns.Select((column, index) => new ReportColumnDto
            {
                Name = column.Name, DisplayName = column.DisplayName, DataType = column.DataType,
                Format = column.Format, IsVisible = column.IsVisible, Order = index, AggregationType = column.AggregationType
            }).ToList(),
            Data = rows.Skip((page - 1) * pageSize).Take(pageSize).Select(value => value.Values).ToList(),
            CurrentPage = page, PageSize = pageSize, TotalPages = totalPages,
            HasNextPage = page < totalPages, HasPreviousPage = page > 1 && totalPages > 0,
            Metadata = new ReportMetadataDto
            {
                Parameters = filters.ToMetadata(), Query = definition.Query, DataAsOf = DateTime.UtcNow,
                DataSource = "Authoritative tenant and assigned-project Civil Engineering records",
                Statistics = new Dictionary<string, object> { ["systemCode"] = definition.Code, ["totalRows"] = rows.Count }
            }
        };
    }

    private sealed record CivilReportRow(DateTime EffectiveAt, Dictionary<string, object> Values);

    private sealed record ReportFilters(Guid ProjectId, DateTime? StartUtc, DateTime? EndExclusiveUtc)
    {
        public static ReportFilters Parse(ExecuteReportDto request)
        {
            var projectId = GetGuid(request.Parameters, "projectId");
            if (!projectId.HasValue || projectId.Value == Guid.Empty)
                throw new InvalidOperationException("Select a project before running the Civil Engineering report.");
            var start = request.StartDate ?? GetDate(request.Parameters, "startDate");
            var end = request.EndDate ?? GetDate(request.Parameters, "endDate");
            start = start.HasValue ? DateTime.SpecifyKind(start.Value.Date, DateTimeKind.Utc) : null;
            var endExclusive = end.HasValue ? DateTime.SpecifyKind(end.Value.Date.AddDays(1), DateTimeKind.Utc) : (DateTime?)null;
            if (start.HasValue && endExclusive.HasValue && start.Value >= endExclusive.Value)
                throw new InvalidOperationException("Start date must be on or before end date.");
            return new ReportFilters(projectId.Value, start, endExclusive);
        }

        public bool InRange(DateTime value) => (!StartUtc.HasValue || value >= StartUtc.Value) &&
                                               (!EndExclusiveUtc.HasValue || value < EndExclusiveUtc.Value);

        public Dictionary<string, object> ToMetadata()
        {
            var values = new Dictionary<string, object> { ["projectId"] = ProjectId };
            if (StartUtc.HasValue) values["startDate"] = StartUtc.Value;
            if (EndExclusiveUtc.HasValue) values["endDate"] = EndExclusiveUtc.Value.AddDays(-1);
            return values;
        }

        private static string? GetString(Dictionary<string, object>? values, string key)
        {
            if (values is null || !values.TryGetValue(key, out var value) || value is null) return null;
            var text = value is JsonElement element ? element.ToString() : Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static DateTime? GetDate(Dictionary<string, object>? values, string key) =>
            DateTime.TryParse(GetString(values, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;

        private static Guid? GetGuid(Dictionary<string, object>? values, string key) =>
            Guid.TryParse(GetString(values, key), out var value) ? value : null;
    }
}
