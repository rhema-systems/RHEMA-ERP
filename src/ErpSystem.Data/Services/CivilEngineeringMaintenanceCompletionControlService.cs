using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
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
/// Holds Civil's review/direction evidence around completed Maintenance work. It does not write a
/// Maintenance job card/work order, project-wide closure, inspection owner record or Finance posting.
/// </summary>
public sealed class CivilEngineeringMaintenanceCompletionControlService(
    ApplicationDbContext db,
    ICurrentUserService currentUser) : ICivilEngineeringMaintenanceCompletionControlService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private Guid TenantId => currentUser.TenantId is { } id && id != Guid.Empty ? id : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<CivilEngineeringMaintenanceCompletionLookupsDto> GetLookupsAsync(CancellationToken token = default)
    {
        var projectIds = await VisibleProjectIdsAsync(token);
        if (projectIds.Count == 0) return new CivilEngineeringMaintenanceCompletionLookupsDto();
        var links = await db.CivilEngineeringMaintenanceExecutionLinks.AsNoTracking()
            .Include(value => value.Handoff).ThenInclude(value => value.Assessment).ThenInclude(value => value.Intake)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && projectIds.Contains(value.ProjectId)
                && value.Stage == CivilEngineeringMaintenanceExecutionLinkStages.Completed
                && value.Status == CivilEngineeringMaintenanceExecutionLinkStatuses.Completed)
            .OrderByDescending(value => value.LastRevalidatedAt).Take(250).ToListAsync(token);
        var templates = links.Select(value => value.Handoff.Assessment.Intake.EvidenceMetadataTemplateCodeSnapshot).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var policy = links.Count == 0 ? null : await ResolvePolicyAsync(links[0].Handoff.Assessment.Intake, token);
        return new CivilEngineeringMaintenanceCompletionLookupsDto
        {
            CompletedExecutionLinks = links.Select(value => new CivilEngineeringMaintenanceExecutionLookupOptionDto { Id = value.Id, Label = $"{value.Handoff.Assessment.Intake.IntakeNumber} - completed Maintenance work", Status = value.LastOwnerStatusSummary ?? value.Status, MaintenanceAssetId = value.MaintenanceAssetId }).ToList(),
            Documents = await DocumentLookupsAsync(templates, token),
            RequireInspectionBeforeClosure = policy?.RequireInspectionBeforeClosure ?? true,
            RequireClosureEvidence = policy?.RequireClosureEvidence ?? true
        };
    }

    public async Task<IReadOnlyList<CivilEngineeringMaintenanceCompletionControlDto>> ListAsync(CancellationToken token = default)
    {
        var projectIds = await VisibleProjectIdsAsync(token);
        if (projectIds.Count == 0) return [];
        return await MapAsync(await Controls(false).Where(value => projectIds.Contains(value.ProjectId)).OrderByDescending(value => value.CreatedAt).Take(500).ToListAsync(token), token);
    }

    public async Task<CivilEngineeringMaintenanceCompletionControlDto> CreateAsync(CreateCivilEngineeringMaintenanceCompletionControlRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringMaintenanceCompletionControlPolicy.ValidateCreate(request);
        if (errors.Count > 0) throw Validation(errors);
        var hash = Hash(new { request.ExecutionLinkId, summary = request.CompletionSummary.Trim(), request.CompletionDocumentRecordId, request.CompletionDocumentVersionId });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var retry = await Controls(true).SingleOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!SameHash(retry.RequestHash, hash)) throw Conflict("This client request identifier was already used with different completion-report values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([retry], token)).Single();
        }

        var link = await LoadExecutionLinkAsync(request.ExecutionLinkId, true, token);
        var policy = await ResolvePolicyAsync(link.Handoff.Assessment.Intake, token);
        await RequireCompletedOwnerWorkAsync(link, token);
        await RequireCivilEngineerAsync(link, token);
        if (await Controls(true).AnyAsync(value => value.ExecutionLinkId == link.Id, token)) throw Conflict("This Maintenance execution link already has a Civil completion control.");
        if (!await db.ProjectCivilWeeklySupervisionReports.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && !value.IsDeleted && value.ProjectId == link.ProjectId && value.CreatedAt >= link.CreatedAt && value.Status == "Approved" && value.ApprovalStatus == "Approved", token))
            throw Validation("At least one approved governed weekly supervision report is required before submitting the Civil completion report.");
        var evidence = await RequireEvidenceAsync(request.CompletionDocumentRecordId, request.CompletionDocumentVersionId, policy.TemplateCode, token);
        var assessment = link.Handoff.Assessment;
        if (!assessment.CivilEngineerUserId.HasValue) throw Conflict("The approved Civil assessment has no assigned Civil Engineer for the completion report.");
        var now = DateTime.UtcNow;
        var control = new CivilEngineeringMaintenanceCompletionControl
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ExecutionLinkId = link.Id, ProjectId = link.ProjectId, MaintenanceAssetId = link.MaintenanceAssetId,
            JobCardId = link.JobCardId, WorkOrderId = ResolveWorkOrderId(link), CivilEngineerUserId = assessment.CivilEngineerUserId.Value,
            SupervisingCivilEngineerUserId = assessment.SupervisingCivilEngineerUserId, HodUserId = assessment.HodUserId,
            CompletionSummary = request.CompletionSummary.Trim(), CompletionDocumentRecordId = evidence.DocumentRecordId, CompletionDocumentVersionId = evidence.Id,
            CompletionReportedAt = now, ClientRequestId = request.ClientRequestId, RequestHash = hash, CorrelationId = Correlation(correlationId),
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.CivilEngineeringMaintenanceCompletionControls.Add(control);
        AddRevision(control, CivilEngineeringAuditEventMap.SubmitCivilCompletionReport, null, null, Snapshot(control), correlationId);
        AddAudit(control, CivilEngineeringAuditEventMap.SubmitCivilCompletionReport, null, Snapshot(control), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([control], token)).Single();
    }

    public async Task<CivilEngineeringMaintenanceCompletionControlDto> ProcessAsync(Guid completionControlId, ProcessCivilEngineeringMaintenanceCompletionControlRequest request, string correlationId, CancellationToken token = default)
    {
        var errors = CivilEngineeringMaintenanceCompletionControlPolicy.ValidateProcess(request);
        if (errors.Count > 0) throw Validation(errors);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var control = await Controls(true).Include(value => value.ExecutionLink).ThenInclude(value => value.Handoff).ThenInclude(value => value.Assessment).ThenInclude(value => value.Intake).SingleOrDefaultAsync(value => value.Id == completionControlId, token)
            ?? throw new CivilEngineeringMaintenanceCompletionControlNotFoundException("The Civil Maintenance completion control was not found.");
        await RequireProjectMemberAsync(control.ProjectId, token);
        var hash = Hash(new { completionControlId, request.Action, note = request.Note?.Trim(), request.CentralDocumentRecordId, request.CentralDocumentVersionId, request.RowVersion });
        if (control.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!SameHash(control.LastMutationRequestHash ?? string.Empty, hash)) throw Conflict("This client request identifier was already used with different completion-control values.");
            await transaction.CommitAsync(token);
            return (await MapAsync([control], token)).Single();
        }
        ApplyRowVersion(control, request.RowVersion);
        if (CivilEngineeringMaintenanceCompletionControlPolicy.IsTerminal(control.Stage)) throw Conflict("A closed Civil Maintenance completion control cannot be changed.");
        await RequireCompletedOwnerWorkAsync(control.ExecutionLink, token);
        var policy = await ResolvePolicyAsync(control.ExecutionLink.Handoff.Assessment.Intake, token);
        var before = Snapshot(control);
        var fromStage = control.Stage;
        var evidence = request.CentralDocumentVersionId.HasValue ? await RequireEvidenceAsync(request.CentralDocumentRecordId!.Value, request.CentralDocumentVersionId.Value, policy.TemplateCode, token) : null;
        var action = ApplyAction(control, request, evidence, policy);
        control.LastMutationClientRequestId = request.ClientRequestId;
        control.LastMutationRequestHash = hash;
        control.CorrelationId = Correlation(correlationId);
        control.UpdatedAt = DateTime.UtcNow;
        control.UpdatedBy = UserName;
        control.LastModifiedById = UserId;
        AddRevision(control, action, fromStage, request.Note?.Trim(), Snapshot(control), correlationId, before);
        AddAudit(control, action, before, Snapshot(control), correlationId);
        await SaveAsync(token);
        await transaction.CommitAsync(token);
        return (await MapAsync([control], token)).Single();
    }

    public async Task<IReadOnlyList<CivilEngineeringMaintenanceCompletionRevisionDto>> GetHistoryAsync(Guid completionControlId, CancellationToken token = default)
    {
        var projectIds = await VisibleProjectIdsAsync(token);
        if (!await Controls(false).AnyAsync(value => value.Id == completionControlId && projectIds.Contains(value.ProjectId), token)) throw new CivilEngineeringMaintenanceCompletionControlNotFoundException("The Civil Maintenance completion control was not found.");
        return await db.CivilEngineeringMaintenanceCompletionRevisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.CompletionControlId == completionControlId && !value.IsDeleted).OrderBy(value => value.CreatedAt)
            .Select(value => new CivilEngineeringMaintenanceCompletionRevisionDto { Id = value.Id, Action = value.Action, FromStage = value.FromStage, ToStage = value.ToStage, ActorName = value.ActorName, ActorRoles = value.ActorRoles, Reason = value.Reason, CorrelationId = value.CorrelationId, CreatedAt = value.CreatedAt }).ToListAsync(token);
    }

    private string ApplyAction(CivilEngineeringMaintenanceCompletionControl control, ProcessCivilEngineeringMaintenanceCompletionControlRequest request, CentralDocumentVersion? evidence, Policy policy)
    {
        switch (request.Action)
        {
            case CivilEngineeringMaintenanceCompletionAction.SubmitToHod when control.Stage == CivilEngineeringMaintenanceCompletionStages.SceReview:
                RequireActor(control, control.SupervisingCivilEngineerUserId, CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, "Only the assigned Supervising Civil Engineer can submit this completion report to the Head.");
                control.Stage = CivilEngineeringMaintenanceCompletionStages.HodReview; control.Status = CivilEngineeringMaintenanceCompletionStatuses.Pending; control.SceReviewedById = UserId; control.SceReviewedAt = DateTime.UtcNow;
                return CivilEngineeringAuditEventMap.SubmitCivilCompletionReport;
            case CivilEngineeringMaintenanceCompletionAction.ReturnToCivilEngineer when control.Stage == CivilEngineeringMaintenanceCompletionStages.SceReview:
                RequireActor(control, control.SupervisingCivilEngineerUserId, CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, "Only the assigned Supervising Civil Engineer can return this completion report.");
                control.Stage = CivilEngineeringMaintenanceCompletionStages.Returned; control.Status = CivilEngineeringMaintenanceCompletionStatuses.Returned; control.SceReviewedById = UserId; control.SceReviewedAt = DateTime.UtcNow;
                return CivilEngineeringAuditEventMap.RejectCivilCompletionReport;
            case CivilEngineeringMaintenanceCompletionAction.SubmitToHod when control.Stage == CivilEngineeringMaintenanceCompletionStages.Returned:
                RequireActor(control, control.CivilEngineerUserId, CivilEngineeringAccessControlRegistry.CivilEngineerRole, "Only the assigned Civil Engineer can resubmit a returned completion report.");
                if (evidence is null) throw Validation("Select current published completion evidence when resubmitting a returned report.");
                control.CompletionDocumentRecordId = evidence.DocumentRecordId; control.CompletionDocumentVersionId = evidence.Id; control.CompletionReportedAt = DateTime.UtcNow;
                control.Stage = CivilEngineeringMaintenanceCompletionStages.SceReview; control.Status = CivilEngineeringMaintenanceCompletionStatuses.Pending;
                return CivilEngineeringAuditEventMap.SubmitCivilCompletionReport;
            case CivilEngineeringMaintenanceCompletionAction.ApproveCompletion when control.Stage == CivilEngineeringMaintenanceCompletionStages.HodReview:
                RequireActor(control, control.HodUserId, CivilEngineeringAccessControlRegistry.HeadRole, "Only the assigned Head of Civil Engineering can approve the completion report.");
                control.Stage = CivilEngineeringMaintenanceCompletionStages.AwaitingInspectionDirection; control.Status = CivilEngineeringMaintenanceCompletionStatuses.Active; control.HodReviewedById = UserId; control.HodReviewedAt = DateTime.UtcNow;
                return CivilEngineeringAuditEventMap.ApproveCivilCompletionReport;
            case CivilEngineeringMaintenanceCompletionAction.DirectInspection when control.Stage == CivilEngineeringMaintenanceCompletionStages.AwaitingInspectionDirection:
                RequireActor(control, control.HodUserId, CivilEngineeringAccessControlRegistry.HeadRole, "Only the assigned Head of Civil Engineering can direct an inspection.");
                if (evidence is null) throw Validation("Select inspection-direction evidence.");
                control.InspectionStatus = CivilEngineeringMaintenanceInspectionStatuses.Directed; control.InspectionDirectionDocumentRecordId = evidence.DocumentRecordId; control.InspectionDirectionDocumentVersionId = evidence.Id;
                control.Stage = CivilEngineeringMaintenanceCompletionStages.InspectionInProgress; control.Status = CivilEngineeringMaintenanceCompletionStatuses.Active;
                return CivilEngineeringAuditEventMap.CreateCivilInspection;
            case CivilEngineeringMaintenanceCompletionAction.RecordInspectionPassed when control.Stage == CivilEngineeringMaintenanceCompletionStages.InspectionInProgress:
                RequireActor(control, control.SupervisingCivilEngineerUserId, CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, "Only the assigned Supervising Civil Engineer can record the directed inspection outcome.");
                if (evidence is null) throw Validation("Select passed-inspection evidence.");
                control.InspectionStatus = CivilEngineeringMaintenanceInspectionStatuses.Passed; control.InspectionOutcomeDocumentRecordId = evidence.DocumentRecordId; control.InspectionOutcomeDocumentVersionId = evidence.Id; control.InspectionOutcomeNote = request.Note!.Trim(); control.InspectionRecordedById = UserId; control.InspectionRecordedAt = DateTime.UtcNow;
                control.Stage = CivilEngineeringMaintenanceCompletionStages.AwaitingPaymentDirection; control.Status = CivilEngineeringMaintenanceCompletionStatuses.Active;
                return CivilEngineeringAuditEventMap.ApproveCivilInspection;
            case CivilEngineeringMaintenanceCompletionAction.RecordInspectionFailed when control.Stage == CivilEngineeringMaintenanceCompletionStages.InspectionInProgress:
                RequireActor(control, control.SupervisingCivilEngineerUserId, CivilEngineeringAccessControlRegistry.SupervisingEngineerRole, "Only the assigned Supervising Civil Engineer can record the directed inspection outcome.");
                if (evidence is null) throw Validation("Select failed-inspection evidence.");
                control.InspectionStatus = CivilEngineeringMaintenanceInspectionStatuses.Failed; control.InspectionOutcomeDocumentRecordId = evidence.DocumentRecordId; control.InspectionOutcomeDocumentVersionId = evidence.Id; control.InspectionOutcomeNote = request.Note!.Trim(); control.InspectionRecordedById = UserId; control.InspectionRecordedAt = DateTime.UtcNow;
                control.Stage = CivilEngineeringMaintenanceCompletionStages.RemediationRequired; control.Status = CivilEngineeringMaintenanceCompletionStatuses.Blocked;
                return CivilEngineeringAuditEventMap.RejectCivilInspection;
            case CivilEngineeringMaintenanceCompletionAction.DirectPayment when control.Stage == CivilEngineeringMaintenanceCompletionStages.AwaitingPaymentDirection:
                RequireActor(control, control.HodUserId, CivilEngineeringAccessControlRegistry.HeadRole, "Only the assigned Head of Civil Engineering can direct payment review.");
                if (control.InspectionStatus != CivilEngineeringMaintenanceInspectionStatuses.Passed || evidence is null) throw Validation("A passed inspection and DMS payment-direction evidence are required before Finance can be directed.");
                control.PaymentDirectionStatus = CivilEngineeringMaintenancePaymentDirectionStatuses.Directed; control.PaymentDirectionDocumentRecordId = evidence.DocumentRecordId; control.PaymentDirectionDocumentVersionId = evidence.Id; control.PaymentDirectionNote = request.Note!.Trim(); control.PaymentDirectedById = UserId; control.PaymentDirectedAt = DateTime.UtcNow;
                control.Stage = CivilEngineeringMaintenanceCompletionStages.AwaitingClosure; control.Status = CivilEngineeringMaintenanceCompletionStatuses.Active;
                return CivilEngineeringAuditEventMap.SubmitPaymentRecommendation;
            case CivilEngineeringMaintenanceCompletionAction.Close when control.Stage == CivilEngineeringMaintenanceCompletionStages.AwaitingClosure:
                RequireActor(control, control.HodUserId, CivilEngineeringAccessControlRegistry.HeadRole, "Only the assigned Head of Civil Engineering can close this work-level completion control.");
                if (policy.RequireInspectionBeforeClosure && control.InspectionStatus != CivilEngineeringMaintenanceInspectionStatuses.Passed) throw Validation("A passed Civil inspection is required before closure by CIV-CFG-007.");
                if (control.PaymentDirectionStatus != CivilEngineeringMaintenancePaymentDirectionStatuses.Directed) throw Validation("A controlled Finance payment direction is required before closure.");
                if (policy.RequireClosureEvidence && evidence is null) throw Validation("CIV-CFG-007 requires central-DMS closure evidence.");
                control.ClosureDocumentRecordId = evidence?.DocumentRecordId; control.ClosureDocumentVersionId = evidence?.Id; control.ClosedById = UserId; control.ClosedAt = DateTime.UtcNow; control.Stage = CivilEngineeringMaintenanceCompletionStages.Closed; control.Status = CivilEngineeringMaintenanceCompletionStatuses.Closed;
                return CivilEngineeringAuditEventMap.ApproveCivilWorkClosure;
            default:
                throw Conflict("The requested completion-control action is not valid for the current Civil lifecycle stage.");
        }
    }

    private IQueryable<CivilEngineeringMaintenanceCompletionControl> Controls(bool tracked) => (tracked ? db.CivilEngineeringMaintenanceCompletionControls : db.CivilEngineeringMaintenanceCompletionControls.AsNoTracking()).Where(value => value.TenantId == TenantId && !value.IsDeleted);
    private async Task<List<Guid>> VisibleProjectIdsAsync(CancellationToken token) => await db.ProjectMembers.AsNoTracking().Where(value => value.TenantId == TenantId && value.UserId == UserId && value.IsActive && !value.IsDeleted).Select(value => value.ProjectId).Distinct().ToListAsync(token);
    private async Task RequireProjectMemberAsync(Guid projectId, CancellationToken token)
    {
        if (!await db.ProjectMembers.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.UserId == UserId && value.IsActive && !value.IsDeleted, token)) throw new UnauthorizedAccessException("You are not an active member of this Civil work's project.");
    }
    private void RequireActor(CivilEngineeringMaintenanceCompletionControl control, Guid expectedUserId, string expectedRole, string error)
    {
        if (expectedUserId != UserId || !currentUser.Roles.Any(value => string.Equals(value, expectedRole, StringComparison.OrdinalIgnoreCase))) throw new UnauthorizedAccessException(error);
    }
    private async Task RequireCivilEngineerAsync(CivilEngineeringMaintenanceExecutionLink link, CancellationToken token)
    {
        await RequireProjectMemberAsync(link.ProjectId, token);
        var assigned = link.Handoff.Assessment.CivilEngineerUserId;
        if (!assigned.HasValue || assigned.Value != UserId || !currentUser.Roles.Any(value => string.Equals(value, CivilEngineeringAccessControlRegistry.CivilEngineerRole, StringComparison.OrdinalIgnoreCase))) throw new UnauthorizedAccessException("Only the Civil Engineer assigned to the approved assessment can submit its completion report.");
    }
    private async Task<CivilEngineeringMaintenanceExecutionLink> LoadExecutionLinkAsync(Guid id, bool tracked, CancellationToken token) => await (tracked ? db.CivilEngineeringMaintenanceExecutionLinks : db.CivilEngineeringMaintenanceExecutionLinks.AsNoTracking())
        .Include(value => value.Handoff).ThenInclude(value => value.Assessment).ThenInclude(value => value.Intake)
        .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, token)
        ?? throw new CivilEngineeringMaintenanceExecutionLinkNotFoundException("The completed Civil Maintenance execution link was not found.");
    private async Task RequireCompletedOwnerWorkAsync(CivilEngineeringMaintenanceExecutionLink link, CancellationToken token)
    {
        await RequireProjectMemberAsync(link.ProjectId, token);
        if (!link.WorkOrderId.HasValue) throw Validation("Synchronize the Civil Maintenance execution link after the authoritative job card creates its work order before submitting a completion report.");
        var jobCard = link.JobCardId.HasValue ? await db.JobCards.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == link.JobCardId && !value.IsDeleted, token) : null;
        var workOrder = await db.WorkOrders.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == link.WorkOrderId.Value && !value.IsDeleted, token);
        if (jobCard is not null && jobCard.AssetId != link.MaintenanceAssetId || workOrder is not null && workOrder.AssetId != link.MaintenanceAssetId) throw Conflict("The authoritative Maintenance owner no longer matches the frozen Civil asset lineage.");
        if (workOrder is null || !string.Equals(workOrder.Status, "Completed", StringComparison.OrdinalIgnoreCase) && !string.Equals(workOrder.Status, "Closed", StringComparison.OrdinalIgnoreCase)) throw Validation("The authoritative Maintenance work order must be completed or closed before a Civil completion report can be submitted.");
        if (link.Handoff.Stage != CivilEngineeringMaintenanceCostingHandoffStages.Awarded || link.Handoff.Status != CivilEngineeringMaintenanceCostingHandoffStatuses.Awarded || link.Handoff.ApprovalStatus != CivilEngineeringMaintenanceCostingHandoffApprovalStatuses.Approved) throw Conflict("The Civil costing handoff is no longer awarded.");
    }
    private async Task<CentralDocumentVersion> RequireEvidenceAsync(Guid recordId, Guid versionId, string templateCode, CancellationToken token) => await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == versionId && value.DocumentRecordId == recordId && value.DocumentRecord.MetadataTemplateCode == templateCode, token)
        ?? throw Validation("Select current published DMS evidence from the maintenance-assessment evidence template.");
    private async Task<IReadOnlyList<CivilEngineeringMaintenanceIntakeDocumentLookupDto>> DocumentLookupsAsync(IReadOnlyCollection<string> templateCodes, CancellationToken token) => templateCodes.Count == 0 ? [] : await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(CentralDocumentEvidenceRules.CurrentPublished()).Where(value => value.TenantId == TenantId && templateCodes.Contains(value.DocumentRecord.MetadataTemplateCode))
        .OrderBy(value => value.DocumentRecord.DocumentReference).ThenByDescending(value => value.CreatedAt).Select(value => new CivilEngineeringMaintenanceIntakeDocumentLookupDto { CentralDocumentRecordId = value.DocumentRecordId, CentralDocumentVersionId = value.Id, DocumentReference = value.DocumentRecord.DocumentReference, Title = value.DocumentRecord.Title, VersionNumber = value.VersionNumber }).Take(250).ToListAsync(token);
    private async Task<Policy> ResolvePolicyAsync(CivilEngineeringMaintenanceIntake intake, CancellationToken token)
    {
        var profile = await db.CivilEngineeringConfigurationProfiles.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == intake.ConfigurationProfileId && !value.IsDeleted, token) ?? throw Validation("The frozen Civil maintenance configuration profile is unavailable.");
        var decision = await db.CivilEngineeringConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == intake.ConfigurationDecisionId && value.ProfileId == profile.Id && value.ConfigurationKey == "CIV-CFG-007" && !value.IsDeleted, token) ?? throw Validation("The frozen CIV-CFG-007 maintenance decision is unavailable.");
        if (decision.Status != CivilEngineeringConfigurationDecisionStatus.Approved || decision.ApprovalStatus != CivilEngineeringConfigurationApprovalStatus.Approved || decision.EvidenceStatus != CivilEngineeringConfigurationEvidenceStatus.Verified) throw Validation("CIV-CFG-007 is not approved and verified.");
        CivilEngineeringMaintenanceAssessmentValue value;
        try { value = JsonSerializer.Deserialize<CivilEngineeringMaintenanceAssessmentValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); } catch (JsonException) { throw Conflict("The frozen CIV-CFG-007 maintenance controls are invalid."); }
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == intake.EvidenceMetadataTemplateId && item.IsActive && item.PublishedAt.HasValue && !item.IsDeleted, token) ?? throw Validation("The frozen CIV-CFG-007 DMS evidence template is unavailable.");
        if (!string.Equals(template.TemplateCode, intake.EvidenceMetadataTemplateCodeSnapshot, StringComparison.OrdinalIgnoreCase)) throw Conflict("The frozen Civil DMS evidence-template lineage no longer matches CIV-CFG-007.");
        return new Policy(value.RequireInspectionBeforeClosure, value.RequireClosureEvidence, template.TemplateCode);
    }
    private async Task<IReadOnlyList<CivilEngineeringMaintenanceCompletionControlDto>> MapAsync(IReadOnlyCollection<CivilEngineeringMaintenanceCompletionControl> values, CancellationToken token)
    {
        if (values.Count == 0) return [];
        var linkIds = values.Select(value => value.ExecutionLinkId).Distinct().ToList(); var projectIds = values.Select(value => value.ProjectId).Distinct().ToList(); var assetIds = values.Select(value => value.MaintenanceAssetId).Distinct().ToList();
        var jobIds = values.Where(value => value.JobCardId.HasValue).Select(value => value.JobCardId!.Value).Distinct().ToList(); var workOrderIds = values.Where(value => value.WorkOrderId.HasValue).Select(value => value.WorkOrderId!.Value).Distinct().ToList(); var users = values.SelectMany(value => new[] { value.CivilEngineerUserId, value.SupervisingCivilEngineerUserId, value.HodUserId }).Distinct().ToList(); var documentVersionIds = values.Select(value => value.CompletionDocumentVersionId).Distinct().ToList();
        var links = await db.CivilEngineeringMaintenanceExecutionLinks.AsNoTracking().Include(value => value.Handoff).ThenInclude(value => value.Assessment).ThenInclude(value => value.Intake).Where(value => value.TenantId == TenantId && linkIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var projects = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && projectIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => $"{value.ProjectCode} - {value.Title}", token);
        var assets = await db.MaintenanceAssets.AsNoTracking().Where(value => value.TenantId == TenantId && assetIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => $"{value.AssetNumber} - {value.Name}", token);
        var jobCards = jobIds.Count == 0 ? new Dictionary<Guid, JobCard>() : await db.JobCards.AsNoTracking().Where(value => value.TenantId == TenantId && jobIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var workOrders = workOrderIds.Count == 0 ? new Dictionary<Guid, WorkOrder>() : await db.WorkOrders.AsNoTracking().Where(value => value.TenantId == TenantId && workOrderIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        var userNames = await db.Users.AsNoTracking().Where(value => value.TenantId == TenantId && users.Contains(value.Id)).ToDictionaryAsync(value => value.Id, value => Name(value.FirstName, value.LastName, value.UserName), token);
        var documents = await db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord).Where(value => value.TenantId == TenantId && documentVersionIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, token);
        return values.Select(value => { links.TryGetValue(value.ExecutionLinkId, out var link); jobCards.TryGetValue(value.JobCardId ?? Guid.Empty, out var jobCard); workOrders.TryGetValue(value.WorkOrderId ?? Guid.Empty, out var workOrder); documents.TryGetValue(value.CompletionDocumentVersionId, out var document); return new CivilEngineeringMaintenanceCompletionControlDto { Id = value.Id, ExecutionLinkId = value.ExecutionLinkId, IntakeNumber = link?.Handoff.Assessment.Intake.IntakeNumber ?? "Unavailable intake", ProjectId = value.ProjectId, ProjectLabel = projects.GetValueOrDefault(value.ProjectId, "Unavailable project"), MaintenanceAssetId = value.MaintenanceAssetId, MaintenanceAssetLabel = assets.GetValueOrDefault(value.MaintenanceAssetId, "Unavailable asset"), JobCardId = value.JobCardId, JobCardNumber = jobCard?.JobCardNumber, WorkOrderId = value.WorkOrderId, WorkOrderNumber = workOrder?.WorkOrderNumber, WorkOrderStatus = workOrder?.Status, CivilEngineerUserId = value.CivilEngineerUserId, CivilEngineerName = userNames.GetValueOrDefault(value.CivilEngineerUserId, "Unavailable Civil Engineer"), SupervisingCivilEngineerUserId = value.SupervisingCivilEngineerUserId, SupervisingCivilEngineerName = userNames.GetValueOrDefault(value.SupervisingCivilEngineerUserId, "Unavailable SCE"), HodUserId = value.HodUserId, HodName = userNames.GetValueOrDefault(value.HodUserId, "Unavailable HOD"), Stage = value.Stage, Status = value.Status, CompletionSummary = value.CompletionSummary, CompletionDocumentRecordId = value.CompletionDocumentRecordId, CompletionDocumentVersionId = value.CompletionDocumentVersionId, CompletionEvidenceReference = document?.DocumentRecord.DocumentReference ?? "Unavailable evidence", CompletionReportedAt = value.CompletionReportedAt, InspectionStatus = value.InspectionStatus, PaymentDirectionStatus = value.PaymentDirectionStatus, ClosedAt = value.ClosedAt, RowVersion = Convert.ToBase64String(value.RowVersion) }; }).ToList();
    }
    private void AddRevision(CivilEngineeringMaintenanceCompletionControl control, string action, string? fromStage, string? reason, object after, string correlationId, object? before = null) { CivilEngineeringAuditEventMap.GetRequired(action); db.CivilEngineeringMaintenanceCompletionRevisions.Add(new CivilEngineeringMaintenanceCompletionRevision { Id = Guid.NewGuid(), TenantId = TenantId, CompletionControlId = control.Id, Action = action, FromStage = fromStage, ToStage = control.Stage, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, Reason = Clean(reason, 2000), CorrelationId = Correlation(correlationId), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId }); }
    private void AddAudit(CivilEngineeringMaintenanceCompletionControl control, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(CivilEngineeringMaintenanceCompletionControl), ResourceId = control.Id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions), IpAddress = currentUser.IpAddress ?? string.Empty, UserAgent = currentUser.UserAgent, Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private async Task SaveAsync(CancellationToken token) { try { await db.SaveChangesAsync(token); } catch (DbUpdateConcurrencyException) { throw Conflict("The completion control changed concurrently. Refresh and retry."); } catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is >= 52240 and <= 52246) { throw Conflict(sql.Message); } catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("A duplicate or conflicting Civil completion control was detected. Refresh and retry."); } }
    private static Guid? ResolveWorkOrderId(CivilEngineeringMaintenanceExecutionLink link) => link.WorkOrderId;
    private static void ApplyRowVersion(CivilEngineeringMaintenanceCompletionControl value, string rowVersion) { try { value.RowVersion = Convert.FromBase64String(rowVersion); } catch (FormatException) { throw Validation("The completion-control concurrency version is invalid."); } }
    private static object Snapshot(CivilEngineeringMaintenanceCompletionControl value) => new { value.Id, value.ExecutionLinkId, value.ProjectId, value.MaintenanceAssetId, value.JobCardId, value.WorkOrderId, value.Stage, value.Status, value.CompletionDocumentVersionId, value.InspectionStatus, value.PaymentDirectionStatus, value.ClosedAt };
    private static string Name(string? first, string? last, string? user) => string.Join(' ', new[] { first, last }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim() is { Length: > 0 } name ? name : user ?? "Unavailable";
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(max, value.Trim().Length)];
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value[..Math.Min(100, value.Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool SameHash(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static CivilEngineeringMaintenanceCompletionControlValidationException Validation(string value) => new(value);
    private static CivilEngineeringMaintenanceCompletionControlValidationException Validation(IEnumerable<string> values) => new(string.Join(" ", values));
    private static CivilEngineeringMaintenanceCompletionControlConflictException Conflict(string value) => new(value);
    private sealed record Policy(bool RequireInspectionBeforeClosure, bool RequireClosureEvidence, string TemplateCode);
}
