using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveySubcontractChargeService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IQuantitySurveyConfigurationService configuration,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters,
    IControlledFileUploadService controlledFiles,
    ICentralDocumentRepositoryFileService centralDocuments,
    INotificationTopicPublisher notifications,
    ILogger<QuantitySurveySubcontractChargeService> logger) : IQuantitySurveySubcontractChargeService
{
    private const long MaximumEvidenceBytes = 52_428_800;
    private const string IssueTopic = "QuantitySurvey.SubcontractChargeNoticeIssued";
    private const string DecisionTopic = "QuantitySurvey.SubcontractChargeDecision";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<IReadOnlyList<QuantitySurveySubcontractChargeDto>> GetAsync(Guid projectId, Guid subcontractId,
        bool external, CancellationToken token = default)
    {
        var subcontract = await RequiredSubcontractAsync(subcontractId, false, token);
        if (subcontract.ProjectId != projectId) throw NotFound("The subcontract was not found in this project.");
        Guid? partnerId = external ? (await RequireExternalActorAsync(projectId, false, subcontractId, token)).BusinessPartnerId : null;
        if (!external) await RequireProjectAsync(projectId);
        if (partnerId.HasValue && partnerId != subcontract.SubcontractorBusinessPartnerId)
            throw new UnauthorizedAccessException("The subcontract is not assigned to the linked business partner.");
        var query = Query().Where(value => value.SubcontractId == subcontractId);
        if (external) query = query.Where(value => value.Status != QuantitySurveySubcontractChargeStatuses.Draft);
        var values = await query.OrderByDescending(value => value.NoticeDate)
            .ThenByDescending(value => value.CreatedAt)
            .ToListAsync(token);
        return values.Select(Map).ToList();
    }

    public async Task<QuantitySurveySubcontractChargeDto> SaveAsync(Guid subcontractId,
        SaveQuantitySurveySubcontractChargeRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var subcontract = await RequiredSubcontractAsync(subcontractId, false, token); await RequireProjectAsync(subcontract.ProjectId);
        if (subcontract.Status != QuantitySurveySubcontractStatuses.Approved)
            throw Conflict("Charge notices require an approved open subcontract.");
        var title = RequiredText(request.Title, 3, 200, "Title");
        var reason = RequiredText(request.Reason, 10, 4000, "Charge reason");
        var policy = await ResolvePolicyAsync(subcontract, token);
        var issues = QuantitySurveySubcontractChargeRules.ValidateDraft(request.ChargeType, request.ProposedAmount,
            request.NoticeDate, request.ResponseDueDate, subcontract.SubcontractValue,
            policy.Controls.ControlBackCharges, policy.Controls.ControlContraCharges);
        if (issues.Count > 0) throw Validation(string.Join(" ", issues));
        var chargeType = request.ChargeType.Trim();
        var requestHash = Hash(new { subcontractId, chargeType, title, reason, request.NoticeDate, request.ResponseDueDate,
            ProposedAmount = Round(request.ProposedAmount), subcontract.PolicyHash, policy.EvidenceMetadataTemplateId });
        var retry = await db.QuantitySurveySubcontractChargeNotices.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (retry.SubcontractId != subcontractId || retry.Id != request.Id.GetValueOrDefault(retry.Id) || !FixedEquals(retry.RequestHash, requestHash))
                throw RetryConflict();
            return Map(await RequiredAsync(retry.Id, false, token));
        }

        var strategy = db.Database.CreateExecutionStrategy(); Guid id = Guid.Empty;
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var parent = await RequiredSubcontractAsync(subcontractId, true, token);
            await ValidateFrozenAsync(parent, policy, token);
            QuantitySurveySubcontractChargeNotice value; object? before = null;
            var action = QuantitySurveyAuditEventMap.CreateSubcontractCharge;
            if (request.Id.HasValue)
            {
                value = await RequiredAsync(request.Id.Value, true, token);
                if (value.SubcontractId != subcontractId) throw NotFound("The charge notice was not found in this subcontract.");
                if (value.Status is not (QuantitySurveySubcontractChargeStatuses.Draft or QuantitySurveySubcontractChargeStatuses.Rejected))
                    throw Conflict("Only a Draft or Rejected charge notice can be amended.");
                ApplyRowVersion(value.RowVersion, request.RowVersion, "charge notice"); before = Snapshot(value);
                value.LastMutationClientRequestId = request.ClientRequestId; value.LastMutationRequestHash = requestHash;
                action = QuantitySurveyAuditEventMap.UpdateSubcontractCharge;
            }
            else
            {
                value = new QuantitySurveySubcontractChargeNotice
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, SubcontractId = subcontractId,
                    ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
                    NoticeNumber = await NextNumberAsync(token), PreparedById = UserId, PreparedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
                };
                db.QuantitySurveySubcontractChargeNotices.Add(value);
            }
            value.ChargeType = chargeType; value.Title = title; value.Reason = reason;
            value.NoticeDate = request.NoticeDate.Date; value.ResponseDueDate = request.ResponseDueDate.Date;
            value.ProposedAmount = Round(request.ProposedAmount); value.ApprovedAmount = null; value.Currency = parent.Currency;
            value.Status = QuantitySurveySubcontractChargeStatuses.Draft; value.ApprovalStatus = "Draft";
            value.ResponseStatus = QuantitySurveySubcontractChargeResponses.Pending; value.ResponseNote = null;
            value.RespondedByBusinessPartnerId = null; value.RespondedAt = null;
            value.ConfigurationProfileId = parent.ConfigurationProfileId; value.ContractControlsDecisionId = parent.ContractControlsDecisionId;
            value.ApprovalWorkflowDefinitionId = parent.ApprovalWorkflowDefinitionId;
            value.EvidenceMetadataTemplateId = policy.EvidenceMetadataTemplateId; value.PolicyHash = parent.PolicyHash;
            value.WorkflowInstanceId = null; value.IssuedById = null; value.IssuedAt = null; value.SubmittedById = null;
            value.SubmittedAt = null; value.ApprovedById = null; value.ApprovedAt = null; value.RejectionReason = null;
            value.AppliedValuationId = null; value.AllocatedAt = null; value.AppliedAt = null;
            value.CommunicationStatus = "NotRequested"; value.CommunicationRequestedAt = null;
            value.CommunicationRequestCount = 0; value.LastCommunicationTopic = null;
            value.CorrelationId = Correlation(correlationId); value.UpdatedAt = DateTime.UtcNow;
            value.UpdatedBy = UserName; value.LastModifiedById = UserId;
            AddRevision(value, request.ClientRequestId, requestHash, action, before, Snapshot(value),
                action == QuantitySurveyAuditEventMap.CreateSubcontractCharge ? "Charge notice prepared." : "Charge notice amended.", correlationId, null);
            AddAudit(value.Id, action, before, Snapshot(value), correlationId);
            await SaveChangesAsync(token); await transaction.CommitAsync(token); id = value.Id;
        });
        db.ChangeTracker.Clear(); return Map(await RequiredAsync(id, false, token));
    }

    public async Task<QuantitySurveySubcontractChargeEvidenceDto> UploadEvidenceAsync(Guid chargeNoticeId,
        Guid clientRequestId, string title, string fileName, string contentType, long fileSize, Func<Stream> openRead,
        bool external, string correlationId, CancellationToken token = default)
    {
        var charge = await RequiredAsync(chargeNoticeId, false, token); Guid? actorPartnerId = null;
        if (external)
        {
            var actor = await RequireExternalActorAsync(charge.Subcontract.ProjectId, true, charge.SubcontractId, token);
            if (actor.BusinessPartnerId != charge.Subcontract.SubcontractorBusinessPartnerId)
                throw new UnauthorizedAccessException("The charge notice is not assigned to the linked business partner.");
            if (charge.Status != QuantitySurveySubcontractChargeStatuses.Issued)
                throw Conflict("Response evidence can be added only while the issued notice is awaiting a response.");
            actorPartnerId = actor.BusinessPartnerId;
        }
        else
        {
            await RequireProjectAsync(charge.Subcontract.ProjectId);
            if (charge.Status is not (QuantitySurveySubcontractChargeStatuses.Draft or QuantitySurveySubcontractChargeStatuses.Rejected))
                throw Conflict("Notice evidence can be added only to a Draft or Rejected charge notice.");
        }
        if (clientRequestId == Guid.Empty || fileSize <= 0 || fileSize > MaximumEvidenceBytes)
            throw Validation("Select an evidence file up to 50 MB.");
        var safeTitle = RequiredText(title, 3, 200, "Evidence title"); var safeName = Path.GetFileName(fileName);
        await using var source = openRead(); using var memory = new MemoryStream(); await source.CopyToAsync(memory, token);
        var bytes = memory.ToArray(); if (bytes.LongLength != fileSize) throw Validation("The evidence file size changed during upload.");
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var requestHash = Hash(new { chargeNoticeId, safeTitle, safeName, contentType, fileSize, checksum, actorPartnerId });
        var retry = await db.QuantitySurveySubcontractChargeEvidence.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ClientRequestId == clientRequestId, token);
        if (retry is not null)
        {
            if (retry.ChargeNoticeId != chargeNoticeId || !FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
            return MapEvidence(retry);
        }
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == charge.EvidenceMetadataTemplateId && !value.IsDeleted && value.IsActive && value.PublishedAt != null, token)
            ?? throw Conflict("The governed Published charge-notice DMS template is unavailable.");
        var upload = await controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
            Category = ControlledFileUploadCategories.QuantitySurveySubcontractEvidence,
            FileName = safeName, ContentType = contentType, FileSize = bytes.LongLength,
            OpenReadStream = () => new MemoryStream(bytes, false)
        }, token);
        if (upload.Record.VirusScanStatus != FileVirusScanStatus.Clean || !FixedEquals(upload.ChecksumSha256, checksum))
        { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw Conflict("The centrally scanned evidence did not pass its integrity check."); }
        var id = Guid.NewGuid(); CentralDocumentRepositoryLink document;
        try
        {
            document = await centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = TenantId, ActorUserId = UserId, ActorName = UserName, FileUploadRecordId = upload.Record.Id,
                SourceModule = "QuantitySurvey", SourceLabel = "Quantity Survey subcontract charge evidence",
                SourceEntityType = nameof(QuantitySurveySubcontractChargeEvidence), SourceRecordId = id,
                SourceRecordReference = charge.NoticeNumber, Title = safeTitle,
                DocumentType = external ? "SubcontractChargeResponseEvidence" : "SubcontractChargeNoticeEvidence",
                MetadataTemplateCode = template.TemplateCode, AccessProfile = template.AccessProfile, VersionStatus = "Submitted",
                ChangeSummary = "Clean scanned subcontract charge evidence retained in the central DMS.", RequirePublishedGovernance = true,
                MetadataValues = [new("subcontractId", "Subcontract ID", charge.SubcontractId.ToString(), "guid"),
                    new("chargeNoticeId", "Charge notice ID", charge.Id.ToString(), "guid"),
                    new("chargeType", "Charge type", charge.ChargeType), new("checksumSha256", "Checksum SHA-256", checksum)]
            }, token);
        }
        catch { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw; }
        var evidence = new QuantitySurveySubcontractChargeEvidence
        {
            Id = id, TenantId = TenantId, ChargeNoticeId = charge.Id, ClientRequestId = clientRequestId,
            RequestHash = requestHash, Title = safeTitle, OriginalFileName = upload.Record.OriginalFileName,
            ContentType = contentType, FileSize = bytes.LongLength, ChecksumSha256 = checksum,
            FileUploadRecordId = document.FileUploadRecordId, CentralDocumentRecordId = document.DocumentRecordId,
            CentralDocumentVersionId = document.DocumentVersionId, CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName, CreatedById = UserId
        };
        db.QuantitySurveySubcontractChargeEvidence.Add(evidence);
        AddAudit(charge.Id, QuantitySurveyAuditEventMap.AttachSubcontractChargeEvidence, null,
            new { evidence.Id, evidence.Title, evidence.ChecksumSha256, actorPartnerId }, correlationId);
        try { await SaveChangesAsync(token); }
        catch { await centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, token); throw; }
        return MapEvidence(evidence);
    }

    public async Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(Guid chargeNoticeId, Guid evidenceId,
        bool external, CancellationToken token = default)
    {
        var charge = await RequiredAsync(chargeNoticeId, false, token);
        if (external)
        {
            var actor = await RequireExternalActorAsync(charge.Subcontract.ProjectId, false, charge.SubcontractId, token);
            if (actor.BusinessPartnerId != charge.Subcontract.SubcontractorBusinessPartnerId || charge.Status == QuantitySurveySubcontractChargeStatuses.Draft)
                throw new UnauthorizedAccessException("The charge evidence is not available to the linked business partner.");
        }
        else await RequireProjectAsync(charge.Subcontract.ProjectId);
        var evidence = charge.Evidence.SingleOrDefault(value => value.Id == evidenceId && !value.IsDeleted)
            ?? throw NotFound("The charge evidence was not found.");
        return await centralDocuments.OpenAsync(TenantId, evidence.CentralDocumentRecordId, evidence.CentralDocumentVersionId, token)
               ?? throw Conflict("The central-DMS evidence content is unavailable.");
    }

    public async Task<QuantitySurveySubcontractChargeDto> IssueAsync(Guid id, QuantitySurveySubcontractActionRequest request,
        string correlationId, CancellationToken token = default)
    {
        var value = await MutateInternalAsync(id, request, QuantitySurveyAuditEventMap.IssueSubcontractCharge,
            correlationId, token, async charge =>
            {
                if (charge.Status != QuantitySurveySubcontractChargeStatuses.Draft)
                    throw Conflict("Only a Draft charge notice can be issued.");
                if (charge.Evidence.All(item => item.IsDeleted))
                    throw Conflict("Attach at least one clean central-DMS notice evidence file before issue.");
                await ValidateFrozenAsync(charge.Subcontract, await ResolvePolicyAsync(charge.Subcontract, token), token);
                charge.Status = QuantitySurveySubcontractChargeStatuses.Issued; charge.ApprovalStatus = "Draft";
                charge.IssuedById = UserId; charge.IssuedAt = DateTime.UtcNow;
                RequestCommunication(charge, IssueTopic, correlationId);
            });
        await PublishAsync(value, IssueTopic, token); return Map(await RequiredAsync(id, false, token));
    }

    public async Task<QuantitySurveySubcontractChargeDto> RespondAsync(Guid id,
        RespondQuantitySurveySubcontractChargeRequest request, string correlationId, CancellationToken token = default)
    {
        var current = await RequiredAsync(id, false, token);
        var actor = await RequireExternalActorAsync(current.Subcontract.ProjectId, true, current.SubcontractId, token);
        if (actor.BusinessPartnerId != current.Subcontract.SubcontractorBusinessPartnerId)
            throw new UnauthorizedAccessException("The charge notice is not assigned to the linked business partner.");
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var response = request.ResponseStatus?.Trim();
        if (response is not (QuantitySurveySubcontractChargeResponses.Accepted or QuantitySurveySubcontractChargeResponses.Disputed))
            throw Validation("Select Accepted or Disputed.");
        var note = RequiredText(request.Reason, 5, 2000, "Response note");
        var hash = Hash(new { id, response, note, Action = "Respond" });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var charge = await RequiredAsync(id, true, token);
            if (charge.LastMutationClientRequestId == request.ClientRequestId)
            { if (!FixedEquals(charge.LastMutationRequestHash, hash)) throw RetryConflict(); await transaction.CommitAsync(token); return; }
            ApplyRowVersion(charge.RowVersion, request.RowVersion, "charge notice");
            if (charge.Status != QuantitySurveySubcontractChargeStatuses.Issued)
                throw Conflict("Only an issued charge notice awaiting response can be answered.");
            var before = Snapshot(charge); charge.Status = QuantitySurveySubcontractChargeStatuses.Responded;
            charge.ResponseStatus = response; charge.ResponseNote = note; charge.RespondedByBusinessPartnerId = actor.BusinessPartnerId;
            charge.RespondedAt = DateTime.UtcNow; charge.LastMutationClientRequestId = request.ClientRequestId;
            charge.LastMutationRequestHash = hash; charge.CorrelationId = Correlation(correlationId);
            charge.UpdatedAt = DateTime.UtcNow; charge.UpdatedBy = UserName; charge.LastModifiedById = UserId;
            AddRevision(charge, request.ClientRequestId, hash, QuantitySurveyAuditEventMap.RespondSubcontractCharge,
                before, Snapshot(charge), note, correlationId, actor.BusinessPartnerId);
            AddAudit(charge.Id, QuantitySurveyAuditEventMap.RespondSubcontractCharge, before, Snapshot(charge), correlationId);
            await SaveChangesAsync(token); await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear(); return Map(await RequiredAsync(id, false, token));
    }

    public Task<QuantitySurveySubcontractChargeDto> SubmitAsync(Guid id, QuantitySurveySubcontractActionRequest request,
        string correlationId, CancellationToken token = default) => MutateInternalAsync(id, request,
        QuantitySurveyAuditEventMap.SubmitSubcontractCharge, correlationId, token, async charge =>
        {
            if (charge.Status is not (QuantitySurveySubcontractChargeStatuses.Issued or QuantitySurveySubcontractChargeStatuses.Responded))
                throw Conflict("Only an issued or responded charge notice can be submitted for approval.");
            if (charge.Status == QuantitySurveySubcontractChargeStatuses.Issued)
            {
                if (DateTime.UtcNow.Date <= charge.ResponseDueDate.Date)
                    throw Conflict("Wait for the subcontractor response or the response due date before approval submission.");
                charge.ResponseStatus = QuantitySurveySubcontractChargeResponses.NoResponse;
            }
            await ValidateFrozenAsync(charge.Subcontract, await ResolvePolicyAsync(charge.Subcontract, token), token);
            var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.Subcontract,
                charge.Id, charge.ApprovalWorkflowDefinitionId);
            if (!result.ExecutionResult.Success || result.Outcome != WorkflowOutcome.Pending)
                throw Conflict(result.ExecutionResult.Message ?? "The charge workflow must stop at an independent approval step.");
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Subcontract).ApplySubmitOutcome(charge, result.Outcome, UserId);
            charge.Status = QuantitySurveySubcontractChargeStatuses.PendingApproval; charge.ApprovalStatus = "Pending";
            charge.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId; charge.SubmittedById = UserId; charge.SubmittedAt = DateTime.UtcNow;
        });

    public async Task<QuantitySurveySubcontractChargeDto> DecideAsync(Guid id,
        DecideQuantitySurveySubcontractChargeRequest request, bool approve, string correlationId,
        CancellationToken token = default)
    {
        var result = await MutateInternalAsync(id, request,
            approve ? QuantitySurveyAuditEventMap.ApproveSubcontractCharge : QuantitySurveyAuditEventMap.RejectSubcontractCharge,
            correlationId, token, async charge =>
            {
                if (charge.Status != QuantitySurveySubcontractChargeStatuses.PendingApproval || !charge.WorkflowInstanceId.HasValue)
                    throw Conflict("The charge notice is not pending workflow approval.");
                if (charge.PreparedById == UserId || charge.IssuedById == UserId || charge.SubmittedById == UserId)
                    throw Conflict("The charge preparer, issuer or submitter cannot decide the same notice.");
                await ValidateFrozenAsync(charge.Subcontract, await ResolvePolicyAsync(charge.Subcontract, token), token);
                var outcome = await ResolveWorkflowOutcomeAsync(charge.Id, charge.WorkflowInstanceId.Value, approve, request.Reason, token);
                workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Subcontract)
                    .ApplyApprovalOutcome(charge, outcome, UserId, approve ? null : RequiredText(request.Reason, 5, 2000, "Rejection reason"));
                charge.Status = approve ? QuantitySurveySubcontractChargeStatuses.Approved : QuantitySurveySubcontractChargeStatuses.Rejected;
                charge.ApprovalStatus = approve ? "Approved" : "Rejected";
                charge.ApprovedAmount = approve
                    ? ValidateApproved(request.ApprovedAmount, charge.ProposedAmount)
                    : null;
                RequestCommunication(charge, DecisionTopic, correlationId);
            });
        await PublishAsync(result, DecisionTopic, token); return Map(await RequiredAsync(id, false, token));
    }

    public async Task<QuantitySurveySubcontractChargeDto> RetryCommunicationAsync(Guid id, string correlationId,
        CancellationToken token = default)
    {
        var current = await RequiredAsync(id, false, token); await RequireProjectAsync(current.Subcontract.ProjectId);
        if (current.Status is QuantitySurveySubcontractChargeStatuses.Draft or QuantitySurveySubcontractChargeStatuses.PendingApproval)
            throw Conflict("This charge notice has no issued or decided communication to retry.");
        var topic = current.Status is QuantitySurveySubcontractChargeStatuses.Approved or
            QuantitySurveySubcontractChargeStatuses.Rejected or QuantitySurveySubcontractChargeStatuses.Allocated or
            QuantitySurveySubcontractChargeStatuses.Applied ? DecisionTopic : IssueTopic;
        var clientRequestId = Guid.NewGuid(); var hash = Hash(new { id, topic, current.CommunicationRequestCount, Action = "RetryCommunication" });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var charge = await RequiredAsync(id, true, token); var before = Snapshot(charge);
            RequestCommunication(charge, topic, correlationId); charge.UpdatedAt = DateTime.UtcNow;
            charge.UpdatedBy = UserName; charge.LastModifiedById = UserId;
            AddRevision(charge, clientRequestId, hash, QuantitySurveyAuditEventMap.CommunicateSubcontractCharge,
                before, Snapshot(charge), "Charge communication requested again.", correlationId, null);
            AddAudit(charge.Id, QuantitySurveyAuditEventMap.CommunicateSubcontractCharge, before, Snapshot(charge), correlationId);
            await SaveChangesAsync(token); await transaction.CommitAsync(token);
        });
        await PublishAsync(Map(await RequiredAsync(id, false, token)), topic, token);
        db.ChangeTracker.Clear(); return Map(await RequiredAsync(id, false, token));
    }

    public async Task<IReadOnlyList<QuantitySurveySubcontractChargeRevisionDto>> HistoryAsync(Guid id,
        CancellationToken token = default)
    {
        var value = await RequiredAsync(id, false, token); await RequireProjectAsync(value.Subcontract.ProjectId);
        return await db.QuantitySurveySubcontractChargeRevisions.AsNoTracking().Where(item => item.TenantId == TenantId && item.ChargeNoticeId == id)
            .OrderByDescending(item => item.CreatedAt).Select(item => new QuantitySurveySubcontractChargeRevisionDto
            {
                Id = item.Id, Action = item.Action, ActorName = item.ActorName, ActorRoles = item.ActorRoles,
                CorrelationId = item.CorrelationId, Reason = item.Reason, BeforeJson = item.BeforeJson,
                AfterJson = item.AfterJson, CreatedAt = item.CreatedAt
            }).ToListAsync(token);
    }

    private async Task<QuantitySurveySubcontractChargeDto> MutateInternalAsync(Guid id,
        QuantitySurveySubcontractActionRequest request, string action, string correlationId, CancellationToken token,
        Func<QuantitySurveySubcontractChargeNotice, Task> mutate)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var reason = RequiredText(request.Reason, 5, 2000, "Action reason");
        var approved = request is DecideQuantitySurveySubcontractChargeRequest decision ? decision.ApprovedAmount : (decimal?)null;
        var hash = Hash(new { id, action, reason, approved }); var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var value = await RequiredAsync(id, true, token); await RequireProjectAsync(value.Subcontract.ProjectId);
            if (value.LastMutationClientRequestId == request.ClientRequestId)
            { if (!FixedEquals(value.LastMutationRequestHash, hash)) throw RetryConflict(); await transaction.CommitAsync(token); return; }
            ApplyRowVersion(value.RowVersion, request.RowVersion, "charge notice"); var before = Snapshot(value);
            await mutate(value); value.LastMutationClientRequestId = request.ClientRequestId; value.LastMutationRequestHash = hash;
            value.CorrelationId = Correlation(correlationId); value.UpdatedAt = DateTime.UtcNow;
            value.UpdatedBy = UserName; value.LastModifiedById = UserId;
            AddRevision(value, request.ClientRequestId, hash, action, before, Snapshot(value), reason, correlationId, null);
            AddAudit(value.Id, action, before, Snapshot(value), correlationId);
            await SaveChangesAsync(token); await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear(); return Map(await RequiredAsync(id, false, token));
    }

    private async Task<WorkflowOutcome> ResolveWorkflowOutcomeAsync(Guid entityId, Guid instanceId, bool approve,
        string reason, CancellationToken token)
    {
        var status = await db.WorkflowInstances.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.Id == instanceId && value.EntityId == entityId)
            .Select(value => (WorkflowInstanceStatus?)value.Status)
            .SingleOrDefaultAsync(token);
        if (!status.HasValue) throw Conflict("The charge workflow instance is unavailable.");
        WorkflowOutcome outcome;
        if (approve && status == WorkflowInstanceStatus.Completed)
            outcome = WorkflowOutcome.Approved;
        else if (!approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
            outcome = WorkflowOutcome.Rejected;
        else
        {
            if (!approve && status == WorkflowInstanceStatus.Completed)
                throw Conflict("A completed workflow is approved and cannot be rejected.");
            if (approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                throw Conflict("The workflow ended without approval.");
            if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Subcontract, entityId, UserId))
                throw new UnauthorizedAccessException("You are not assigned to the current approval step.");
            var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Subcontract,
                entityId, UserId, approve ? "Approve" : "Reject", RequiredText(reason, 5, 2000, "Decision reason"));
            if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The workflow decision failed.");
            outcome = result.Outcome;
        }
        if (outcome != (approve ? WorkflowOutcome.Approved : WorkflowOutcome.Rejected))
            throw Conflict("The shared workflow has not reached the requested final outcome.");
        return outcome;
    }

    private async Task<Policy> ResolvePolicyAsync(QuantitySurveySubcontract subcontract, CancellationToken token)
    {
        var profile = await configuration.GetEffectiveProfileAsync(DateTime.UtcNow, token)
            ?? throw Validation("No Published quantity-survey configuration is effective for this date.");
        var controlsDecision = profile.Decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-012" &&
            value.Status == QuantitySurveyConfigurationDecisionStatus.Approved &&
            value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved &&
            value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified)
            ?? throw Validation("The effective approved and evidence-verified QS-DEC-012 subcontract-controls decision is unavailable.");
        var valuationDecision = profile.Decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-008" &&
            value.Status == QuantitySurveyConfigurationDecisionStatus.Approved &&
            value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved &&
            value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified)
            ?? throw Validation("The effective approved and evidence-verified QS-DEC-008 valuation decision is unavailable.");
        QsContractControlsValue controls; QsValuationCertificateValue valuation;
        try
        {
            controls = controlsDecision.Value.Deserialize<QsContractControlsValue>(JsonOptions) ?? throw new JsonException();
            valuation = valuationDecision.Value.Deserialize<QsValuationCertificateValue>(JsonOptions) ?? throw new JsonException();
        }
        catch (JsonException exception) { throw Validation($"The effective QS charge policy is invalid: {exception.Message}"); }
        if (!controls.ControlSubcontracts || (!controls.ControlBackCharges && !controls.ControlContraCharges))
            throw Validation("QS-DEC-012 must enable subcontract and back/contra-charge controls.");
        if (profile.Id != subcontract.ConfigurationProfileId || controlsDecision.Id != subcontract.ContractControlsDecisionId ||
            controls.SubcontractWorkflowDefinitionId != subcontract.ApprovalWorkflowDefinitionId)
            throw Conflict("The subcontract's frozen charge-control lineage differs from the current effective policy.");
        var workflowDefinition = await db.WorkflowDefinitions.AsNoTracking().Include(value => value.EntityType).Include(value => value.Steps)
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == subcontract.ApprovalWorkflowDefinitionId &&
                !value.IsDeleted && value.IsActive && value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published, token)
            ?? throw Validation("The configured Published subcontract workflow is unavailable.");
        if (!string.Equals(workflowDefinition.EntityType.Code, QuantitySurveyWorkflowBindingRegistry.Subcontract, StringComparison.OrdinalIgnoreCase) ||
            !workflowDefinition.Steps.Any(value => !value.IsDeleted && value.IsRequired && value.StepType == WorkflowStepType.Approval))
            throw Validation("The configured subcontract workflow must contain a required independent approval step.");
        _ = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId &&
            value.Id == valuation.ValuationEvidenceMetadataTemplateId && !value.IsDeleted && value.IsActive && value.PublishedAt != null, token)
            ?? throw Validation("The configured Published subcontract evidence DMS template is unavailable.");
        return new(controls, valuation.ValuationEvidenceMetadataTemplateId);
    }

    private async Task ValidateFrozenAsync(QuantitySurveySubcontract subcontract, Policy policy, CancellationToken token)
    {
        if (subcontract.Status != QuantitySurveySubcontractStatuses.Approved)
            throw Conflict("The parent subcontract is no longer approved and open.");
        if (!await db.Contracts.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == subcontract.ContractId &&
                !value.IsDeleted && value.ContractType == "Works" && value.Status == "Active" && value.AllowSubcontracting, token))
            throw Conflict("The parent active Procurement Works contract no longer allows subcontracting.");
        if (!await db.BusinessPartners.AsNoTracking().AnyAsync(value => value.TenantId == TenantId &&
                value.Id == subcontract.SubcontractorBusinessPartnerId && !value.IsDeleted && value.IsActive && !value.IsBlacklisted &&
                BusinessPartnerLifecyclePolicy.IsOperationalRegistration(value.RegistrationStatus), token))
            throw Conflict("The controlled subcontractor is no longer operational.");
        if (policy.EvidenceMetadataTemplateId == Guid.Empty || string.IsNullOrWhiteSpace(subcontract.PolicyHash))
            throw Conflict("The frozen subcontract charge policy lineage is incomplete.");
    }

    private async Task PublishAsync(QuantitySurveySubcontractChargeDto charge, string topic, CancellationToken token)
    {
        try
        {
            var decision = charge.Status is QuantitySurveySubcontractChargeStatuses.Approved or
                QuantitySurveySubcontractChargeStatuses.Allocated or QuantitySurveySubcontractChargeStatuses.Applied
                ? "approved" : charge.Status == QuantitySurveySubcontractChargeStatuses.Rejected ? "rejected" : "issued";
            await notifications.PublishAsync(new NotificationTopicEvent
            {
                TenantId = TenantId, TopicKey = topic, NotificationType = "QuantitySurveySubcontractCharge",
                EntityType = nameof(QuantitySurveySubcontractChargeNotice), EntityId = charge.Id, TriggeredByUserId = UserId,
                Data = new Dictionary<string, object>
                {
                    ["BusinessPartnerId"] = charge.SubcontractorBusinessPartnerId,
                    ["Title"] = $"Subcontract {charge.ChargeType} notice {charge.NoticeNumber}",
                    ["Message"] = $"Charge notice {charge.NoticeNumber} has been {decision}. Amount: {charge.Currency} {(charge.ApprovedAmount ?? charge.ProposedAmount):N2}.",
                    ["ActionUrl"] = $"/external-portal/projects/{charge.ProjectId}",
                    ["NoticeNumber"] = charge.NoticeNumber,
                    ["ChargeType"] = charge.ChargeType,
                    ["Decision"] = decision,
                    ["Amount"] = (charge.ApprovedAmount ?? charge.ProposedAmount).ToString("N2"),
                    ["Currency"] = charge.Currency
                },
                Metadata = new Dictionary<string, object> { ["correlationId"] = charge.Id.ToString("N"), ["ruleCode"] = "CON-003", ["ruleVersion"] = "QS-0522" },
                Email = new NotificationTopicEmailOptions
                {
                    SubjectTemplateOverride = "Subcontract {{ChargeType}} notice {{NoticeNumber}}",
                    HtmlBodyTemplateOverride = $"<p>A subcontract charge notice has been {WebUtility.HtmlEncode(decision)}.</p><p><strong>Notice:</strong> {WebUtility.HtmlEncode(charge.NoticeNumber)}<br/><strong>Type:</strong> {WebUtility.HtmlEncode(charge.ChargeType)}<br/><strong>Amount:</strong> {WebUtility.HtmlEncode(charge.Currency)} {(charge.ApprovedAmount ?? charge.ProposedAmount):N2}</p><p>Sign in to the external project portal to review the notice and evidence.</p>",
                    TextBodyTemplateOverride = "Charge notice {{NoticeNumber}} has been {{Decision}}. Amount: {{Currency}} {{Amount}}. Sign in to the external project portal to review it."
                }
            }, token);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to publish QS subcontract charge communication {Topic} for {ChargeId}", topic, charge.Id);
        }
    }

    private void RequestCommunication(QuantitySurveySubcontractChargeNotice charge, string topic, string correlationId)
    {
        charge.CommunicationStatus = "Requested"; charge.CommunicationRequestedAt = DateTime.UtcNow;
        charge.CommunicationRequestCount += 1; charge.LastCommunicationTopic = topic; charge.CorrelationId = Correlation(correlationId);
    }

    private IQueryable<QuantitySurveySubcontractChargeNotice> Query(bool tracked = false)
    {
        var query = db.QuantitySurveySubcontractChargeNotices.Include(value => value.Subcontract)
            .ThenInclude(value => value.SubcontractorBusinessPartner).Include(value => value.Evidence)
            .Include(value => value.AppliedValuation).Where(value => value.TenantId == TenantId && !value.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<QuantitySurveySubcontractChargeNotice> RequiredAsync(
        Guid id,
        bool tracked,
        CancellationToken token)
    {
        return await Query(tracked).SingleOrDefaultAsync(value => value.Id == id, token)
            ?? throw NotFound("The governed subcontract charge notice was not found.");
    }

    private async Task<QuantitySurveySubcontract> RequiredSubcontractAsync(Guid id, bool tracked, CancellationToken token)
    {
        var query = db.QuantitySurveySubcontracts.Include(value => value.SubcontractorBusinessPartner)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(value => value.Id == id, token)
               ?? throw NotFound("The governed subcontract was not found.");
    }

    private async Task RequireProjectAsync(Guid projectId)
    { if (projectId == Guid.Empty || await projectService.GetProjectByIdAsync(projectId) is null) throw new UnauthorizedAccessException("You are not permitted to access the selected project."); }

    private async Task<ExternalActor> RequireExternalActorAsync(Guid projectId, bool requireUpload, Guid subcontractId, CancellationToken token)
    {
        var link = await db.BusinessPartnerUsers.IgnoreQueryFilters().AsNoTracking().Include(value => value.BusinessPartner).Include(value => value.User)
            .FirstOrDefaultAsync(value => value.UserId == UserId && value.IsActive && !value.IsDeleted && !value.BusinessPartner.IsDeleted &&
                value.BusinessPartner.TenantId == TenantId && value.User.TenantId == TenantId && value.User.IsActive, token)
            ?? throw new UnauthorizedAccessException("No active business-partner identity is linked to this portal user.");
        _ = await projectService.GetExternalProjectByIdAsync(projectId, UserId)
            ?? throw new UnauthorizedAccessException("You do not have access to the selected project.");
        var project = await db.Projects.AsNoTracking().SingleAsync(value => value.TenantId == TenantId && value.Id == projectId, token);
        var allowed = project.BusinessPartnerId == link.BusinessPartnerId || await db.ProjectExternalAccessPolicies.AsNoTracking().AnyAsync(value =>
            value.TenantId == TenantId && value.ProjectId == projectId && value.BusinessPartnerId == link.BusinessPartnerId && !value.IsDeleted &&
            ((value.ArtifactType == "Project") || (value.ArtifactType == "Subcontract" && value.ArtifactId == subcontractId)) &&
            (!requireUpload || value.CanUpload), token);
        if (!project.ExternalPortalAccessEnabled || !project.ExternalCollaborationEnabled || !allowed)
            throw new UnauthorizedAccessException("The project external-access policy does not permit this charge-notice action.");
        return new(link.BusinessPartnerId);
    }

    private async Task<string> NextNumberAsync(CancellationToken token)
    {
        var year = DateTime.UtcNow.Year;
        var count = await db.QuantitySurveySubcontractChargeNotices.IgnoreQueryFilters().CountAsync(value =>
            value.TenantId == TenantId && value.CreatedAt.Year == year, token);
        return $"SUBCHG-{year}-{count + 1:00000}";
    }

    private void AddRevision(QuantitySurveySubcontractChargeNotice value, Guid clientRequestId, string requestHash,
        string action, object? before, object after, string reason, string correlationId, Guid? actorPartnerId) =>
        db.QuantitySurveySubcontractChargeRevisions.Add(new QuantitySurveySubcontractChargeRevision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ChargeNoticeId = value.Id, ClientRequestId = clientRequestId,
            RequestHash = requestHash, Action = action, ActorUserId = UserId, ActorBusinessPartnerId = actorPartnerId,
            ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName, CreatedById = UserId
        });

    private void AddAudit(Guid resourceId, string action, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = nameof(QuantitySurveySubcontractChargeNotice), ResourceId = resourceId.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
            IpAddress = "api", UserAgent = "QuantitySurvey", Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });

    private async Task SaveChangesAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The charge notice changed. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("QS0522", StringComparison.OrdinalIgnoreCase) == true ||
            exception.InnerException?.Message.Contains("SubcontractCharge", StringComparison.OrdinalIgnoreCase) == true ||
            exception.InnerException?.Message.Contains("IX_", StringComparison.OrdinalIgnoreCase) == true)
        { throw Conflict("The charge request conflicts with an existing governed source or lifecycle rule."); }
    }

    private static QuantitySurveySubcontractChargeDto Map(QuantitySurveySubcontractChargeNotice value) => new()
    {
        Id = value.Id, SubcontractId = value.SubcontractId, ProjectId = value.Subcontract.ProjectId,
        SubcontractorBusinessPartnerId = value.Subcontract.SubcontractorBusinessPartnerId,
        AppliedValuationId = value.AppliedValuationId, AppliedValuationNumber = value.AppliedValuation?.ValuationNumber,
        NoticeNumber = value.NoticeNumber, ChargeType = value.ChargeType, Title = value.Title, Reason = value.Reason,
        NoticeDate = value.NoticeDate, ResponseDueDate = value.ResponseDueDate, ProposedAmount = value.ProposedAmount,
        ApprovedAmount = value.ApprovedAmount, Currency = value.Currency, Status = value.Status,
        ApprovalStatus = value.ApprovalStatus, ResponseStatus = value.ResponseStatus, ResponseNote = value.ResponseNote,
        RespondedAt = value.RespondedAt, WorkflowInstanceId = value.WorkflowInstanceId, IssuedAt = value.IssuedAt,
        ApprovedAt = value.ApprovedAt, AppliedAt = value.AppliedAt, CommunicationStatus = value.CommunicationStatus,
        CommunicationRequestedAt = value.CommunicationRequestedAt, CommunicationRequestCount = value.CommunicationRequestCount,
        RowVersion = Convert.ToBase64String(value.RowVersion), Evidence = value.Evidence.Where(item => !item.IsDeleted)
            .OrderBy(item => item.CreatedAt).Select(MapEvidence).ToList()
    };

    private static QuantitySurveySubcontractChargeEvidenceDto MapEvidence(QuantitySurveySubcontractChargeEvidence value) => new()
    {
        Id = value.Id, ChargeNoticeId = value.ChargeNoticeId, Title = value.Title, FileName = value.OriginalFileName,
        FileSize = value.FileSize, ChecksumSha256 = value.ChecksumSha256,
        CentralDocumentRecordId = value.CentralDocumentRecordId, CentralDocumentVersionId = value.CentralDocumentVersionId
    };

    private static object Snapshot(QuantitySurveySubcontractChargeNotice value) => new
    {
        value.Id, value.SubcontractId, value.AppliedValuationId, value.NoticeNumber, value.ChargeType, value.Title,
        value.Reason, value.NoticeDate, value.ResponseDueDate, value.ProposedAmount, value.ApprovedAmount,
        value.Currency, value.Status, value.ApprovalStatus, value.ResponseStatus, value.ResponseNote,
        value.RespondedByBusinessPartnerId, value.RespondedAt, value.ConfigurationProfileId,
        value.ContractControlsDecisionId, value.ApprovalWorkflowDefinitionId, value.EvidenceMetadataTemplateId,
        value.PolicyHash, value.WorkflowInstanceId, value.IssuedById, value.IssuedAt, value.SubmittedById,
        value.SubmittedAt, value.ApprovedById, value.ApprovedAt, value.AllocatedAt, value.AppliedAt,
        value.CommunicationStatus, value.CommunicationRequestedAt, value.CommunicationRequestCount, value.LastCommunicationTopic
    };

    private static decimal ValidateApproved(decimal approved, decimal proposed)
    { try { return QuantitySurveySubcontractChargeRules.ValidateApprovedAmount(proposed, approved); }
      catch (ArgumentOutOfRangeException exception) { throw Validation(exception.Message); } }
    private static void ApplyRowVersion(byte[] current, string? encoded, string label)
    {
        if (string.IsNullOrWhiteSpace(encoded)) throw Validation($"A {label} row version is required. Refresh and retry.");
        byte[] expected; try { expected = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw Validation($"The {label} row version is invalid. Refresh and retry."); }
        if (expected.Length != current.Length || !CryptographicOperations.FixedTimeEquals(expected, current))
            throw Conflict($"The {label} changed. Refresh and retry.");
    }
    private static string RequiredText(string? value, int min, int max, string label) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length < min || value.Trim().Length > max
            ? throw Validation($"{label} must contain {min} to {max} characters.") : value.Trim();
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right)
    { if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false; var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right); return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); }
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static QuantitySurveySubcontractNotFoundException NotFound(string value) => new(value);
    private static QuantitySurveySubcontractValidationException Validation(string value) => new(value);
    private static QuantitySurveySubcontractConflictException Conflict(string value) => new(value);
    private static QuantitySurveySubcontractConflictException RetryConflict() => Conflict("This client request identifier is already bound to different charge inputs.");
    private readonly record struct ExternalActor(Guid BusinessPartnerId);
    private sealed record Policy(QsContractControlsValue Controls, Guid EvidenceMetadataTemplateId);
}
