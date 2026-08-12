using System.Data;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Services.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed partial class QuantitySurveyValuationWorksheetService
{
    private const int MaximumValuationEvidenceFiles = 30;
    private const string ContractorAttestation =
        "I confirm that this interim valuation claim is complete, accurate, and supported by the submitted evidence.";
    private const string ConsultantAttestation =
        "I confirm that I independently reviewed the QS-vetted interim valuation and endorse it for approval.";
    private static readonly JsonSerializerOptions ValuationPolicyJsonOptions = CreateValuationPolicyJsonOptions();

    public async Task<QuantitySurveyValuationLookupsDto> GetExternalLookupsAsync(
        Guid projectId, CancellationToken token = default)
    {
        var actor = await RequireExternalValuationProjectAsync(projectId, false, null, token);
        var lookups = await BuildValuationLookupsAsync(projectId, actor.BusinessPartnerId, token);
        return lookups;
    }

    public async Task<IReadOnlyList<QuantitySurveyValuationWorksheetDto>> GetExternalProjectValuationsAsync(
        Guid projectId, CancellationToken token = default)
    {
        var actor = await RequireExternalValuationProjectAsync(projectId, false, null, token);
        return (await Query().Where(value => value.ProjectId == projectId &&
                (value.ContractorBusinessPartnerId == actor.BusinessPartnerId ||
                 value.ConsultantBusinessPartnerId == actor.BusinessPartnerId))
            .OrderByDescending(value => value.ProjectInterimValuation.ValuationDate)
            .ThenByDescending(value => value.CreatedAt).ToListAsync(token)).Select(Map).ToList();
    }

    public async Task<QuantitySurveyValuationWorksheetDto> GetExternalAsync(
        Guid worksheetId, CancellationToken token = default)
    {
        var entity = await RequiredWorksheetAsync(worksheetId, false, token);
        await RequireExternalValuationAsync(entity, false, false, token);
        return Map(entity);
    }

    public async Task<QuantitySurveyValuationWorksheetDto> SaveContractorClaimAsync(
        Guid worksheetId, SaveQuantitySurveyValuationWorksheetRequest request, string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var entity = await RequiredWorksheetAsync(worksheetId, true, token);
        var actor = await RequireExternalValuationAsync(entity, false, false, token);
        if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId)
            throw new UnauthorizedAccessException("Only the assigned contractor can prepare this valuation claim.");
        if (entity.Status != QuantitySurveyValuationWorkflowStatuses.Draft)
            throw Conflict("Only a Draft interim valuation claim can be amended by the contractor.");
        ValidateFrozenValuationPolicy(entity, await ResolveValuationPolicyAsync(DateTime.UtcNow, token));
        ApplyRowVersion(entity, request.RowVersion);
        var source = await BuildSourceAsync(entity.ProjectInterimValuation, entity.ProjectBoqVersionId, token);
        var inputs = request.Lines.ToDictionary(value => value.ProjectBoqVersionLineId);
        if (inputs.Count != request.Lines.Count || inputs.Count != source.Lines.Count ||
            source.Lines.Any(value => !inputs.ContainsKey(value.ProjectBoqVersionLineId)))
            throw Validation("Submit exactly one controlled claim row for every item in the approved BoQ.");
        var retention = decimal.Round(request.RetentionPercentage, 4, MidpointRounding.AwayFromZero);
        var calculated = source.Lines.Select(value =>
        {
            var input = inputs[value.ProjectBoqVersionLineId];
            return QuantitySurveyValuationWorksheetRules.Calculate(value, input.CurrentClaimedQuantity,
                value.PreviouslyCertifiedQuantity, retention, null, requireDisputeReviewNote: false);
        }).ToList();
        var totals = QuantitySurveyValuationWorksheetRules.Total(calculated);
        var mutationHash = Hash(new
        {
            Action = "ContractorClaim", worksheetId, RetentionPercentage = retention,
            Lines = calculated.Select(value => new { value.Source.ProjectBoqVersionLineId, value.CurrentClaimedQuantity })
        });
        if (IsValuationMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        var before = Snapshot(entity);
        Apply(entity, retention, totals, calculated, QuantitySurveyAuditEventMap.UpdateValuationWorksheet, correlationId);
        TouchValuation(entity, QuantitySurveyAuditEventMap.UpdateValuationWorksheet, correlationId,
            request.ClientRequestId, mutationHash);
        AddValuationRevision(entity, QuantitySurveyAuditEventMap.UpdateValuationWorksheet,
            "Contractor claim amended.", before, Snapshot(entity), correlationId, actor.BusinessPartnerId);
        AddValuationAudit(entity, QuantitySurveyAuditEventMap.UpdateValuationWorksheet, before, Snapshot(entity), correlationId);
        await SaveChangesAsync(token);
        return Map(await RequiredWorksheetAsync(entity.Id, false, token));
    }

    public async Task<QuantitySurveyValuationWorksheetDto> SubmitContractorClaimAsync(
        Guid worksheetId, QuantitySurveyValuationEndorsementRequest request, string correlationId,
        CancellationToken token = default)
    {
        var entity = await RequiredWorksheetAsync(worksheetId, true, token);
        var actor = await RequireExternalValuationAsync(entity, true, false, token);
        if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId)
            throw new UnauthorizedAccessException("Only the assigned contractor can submit this valuation claim.");
        var mutationHash = Hash(new { Action = "ContractorSubmit", request.Notes, request.Signature, Actor = UserId });
        if (IsValuationMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        ApplyRowVersion(entity, request.RowVersion);
        if (entity.Status != QuantitySurveyValuationWorkflowStatuses.Draft)
            throw Conflict("Only a Draft interim valuation claim can be submitted.");
        // This action creates the contractor submission lineage. Requiring that
        // lineage here would make the first governed submission impossible.
        await ValidateValuationReadinessAsync(entity, false, false, token);
        if (entity.CurrentClaimedValue <= entity.PreviouslyCertifiedValue)
            throw Conflict("The contractor claim must contain a positive current-period value.");
        var signatureHash = ValidateValuationSignature(request.Signature, ContractorAttestation,
            entity.ExternalSignatureRequired, entity.Id, actor.BusinessPartnerId);
        var before = Snapshot(entity);
        QuantitySurveyValuationWorksheetRules.RequireTransition(
            entity.Status, QuantitySurveyValuationWorkflowStatuses.ContractorSubmitted);
        entity.Status = QuantitySurveyValuationWorkflowStatuses.ContractorSubmitted;
        entity.ContractorSubmittedById = UserId; entity.ContractorSubmittedByName = UserName;
        entity.ContractorSubmittedAt = DateTime.UtcNow; entity.ContractorAttestation = ContractorAttestation;
        entity.ContractorSignatureHash = signatureHash;
        SyncParentValuation(entity, ProjectInterimValuationStatuses.Submitted);
        TouchValuation(entity, QuantitySurveyAuditEventMap.SubmitContractorValuation, correlationId,
            request.ClientRequestId, mutationHash);
        AddValuationRevision(entity, QuantitySurveyAuditEventMap.SubmitContractorValuation,
            CleanValuationText(request.Notes, 2000) ?? "Contractor claim submitted.", before,
            Snapshot(entity), correlationId, actor.BusinessPartnerId);
        AddValuationAudit(entity, QuantitySurveyAuditEventMap.SubmitContractorValuation, before, Snapshot(entity), correlationId);
        await SaveChangesAsync(token);
        return Map(await RequiredWorksheetAsync(entity.Id, false, token));
    }

    public async Task<QuantitySurveyValuationWorksheetDto> VetAsync(
        Guid worksheetId, QuantitySurveyValuationLifecycleRequest request, string correlationId,
        CancellationToken token = default)
    {
        var entity = await RequiredWorksheetAsync(worksheetId, true, token);
        await RequireProjectAccessAsync(entity.ProjectId);
        var reason = RequiredValuationText(request.Reason, 5, 2000, "QS review note");
        var mutationHash = Hash(new { Action = "Vet", Reason = reason });
        if (IsValuationMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        ApplyRowVersion(entity, request.RowVersion);
        var permitted = entity.ContractorSubmissionRequired
            ? entity.Status is QuantitySurveyValuationWorkflowStatuses.ContractorSubmitted or QuantitySurveyValuationWorkflowStatuses.UnderQsReview
            : entity.Status is QuantitySurveyValuationWorkflowStatuses.Draft or QuantitySurveyValuationWorkflowStatuses.UnderQsReview;
        if (!permitted) throw Conflict("The interim valuation is not ready for QS vetting.");
        await ValidateValuationReadinessAsync(entity, entity.ContractorSubmissionRequired, false, token);
        var before = Snapshot(entity);
        QuantitySurveyValuationWorksheetRules.RequireTransition(
            entity.Status, QuantitySurveyValuationWorkflowStatuses.QsVetted);
        entity.Status = QuantitySurveyValuationWorkflowStatuses.QsVetted;
        entity.QsVettedById = UserId; entity.QsVettedAt = DateTime.UtcNow; entity.QsReviewNote = reason;
        SyncParentValuation(entity, ProjectInterimValuationStatuses.UnderReview);
        TouchValuation(entity, QuantitySurveyAuditEventMap.VetValuation, correlationId,
            request.ClientRequestId, mutationHash);
        AddValuationRevision(entity, QuantitySurveyAuditEventMap.VetValuation, reason, before,
            Snapshot(entity), correlationId);
        AddValuationAudit(entity, QuantitySurveyAuditEventMap.VetValuation, before, Snapshot(entity), correlationId);
        await SaveChangesAsync(token);
        return Map(await RequiredWorksheetAsync(entity.Id, false, token));
    }

    public async Task<QuantitySurveyValuationWorksheetDto> EndorseConsultantAsync(
        Guid worksheetId, QuantitySurveyValuationEndorsementRequest request, string correlationId,
        CancellationToken token = default)
    {
        var entity = await RequiredWorksheetAsync(worksheetId, true, token);
        var actor = await RequireExternalValuationAsync(entity, true, false, token);
        if (actor.BusinessPartnerId != entity.ConsultantBusinessPartnerId)
            throw new UnauthorizedAccessException("Only the assigned consultant can endorse this valuation.");
        var mutationHash = Hash(new { Action = "ConsultantEndorse", request.Notes, request.Signature, Actor = UserId });
        if (IsValuationMutationRetry(entity, request.ClientRequestId, mutationHash)) return Map(entity);
        ApplyRowVersion(entity, request.RowVersion);
        if (entity.Status != QuantitySurveyValuationWorkflowStatuses.QsVetted)
            throw Conflict("The valuation must be QS-vetted before consultant endorsement.");
        await ValidateValuationReadinessAsync(entity, entity.ContractorSubmissionRequired, false, token);
        if (entity.QsVettedById == UserId || entity.ContractorSubmittedById == UserId)
            throw Conflict("The contractor submitter or QS reviewer cannot provide the consultant endorsement.");
        var signatureHash = ValidateValuationSignature(request.Signature, ConsultantAttestation,
            true, entity.Id, actor.BusinessPartnerId);
        var before = Snapshot(entity);
        QuantitySurveyValuationWorksheetRules.RequireTransition(
            entity.Status, QuantitySurveyValuationWorkflowStatuses.ConsultantEndorsed);
        entity.Status = QuantitySurveyValuationWorkflowStatuses.ConsultantEndorsed;
        entity.ConsultantEndorsedById = UserId; entity.ConsultantEndorsedByName = UserName;
        entity.ConsultantEndorsedAt = DateTime.UtcNow; entity.ConsultantAttestation = ConsultantAttestation;
        entity.ConsultantSignatureHash = signatureHash;
        TouchValuation(entity, QuantitySurveyAuditEventMap.EndorseValuation, correlationId,
            request.ClientRequestId, mutationHash);
        AddValuationRevision(entity, QuantitySurveyAuditEventMap.EndorseValuation,
            CleanValuationText(request.Notes, 2000) ?? "Consultant endorsement recorded.", before,
            Snapshot(entity), correlationId, actor.BusinessPartnerId);
        AddValuationAudit(entity, QuantitySurveyAuditEventMap.EndorseValuation, before, Snapshot(entity), correlationId);
        await SaveChangesAsync(token);
        return Map(await RequiredWorksheetAsync(entity.Id, false, token));
    }

    public async Task<QuantitySurveyValuationWorksheetDto> SubmitApprovalAsync(
        Guid worksheetId, QuantitySurveyValuationLifecycleRequest request, string correlationId,
        CancellationToken token = default)
    {
        var reason = RequiredValuationText(request.Reason, 5, 2000, "Approval submission reason");
        var mutationHash = Hash(new { Action = "SubmitApproval", Reason = reason });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var entity = await RequiredWorksheetAsync(worksheetId, true, token);
            await RequireProjectAccessAsync(entity.ProjectId);
            if (IsValuationMutationRetry(entity, request.ClientRequestId, mutationHash))
            {
                await transaction.CommitAsync(token);
                return;
            }
            ApplyRowVersion(entity, request.RowVersion);
            var requiredStatus = entity.ConsultantEndorsementRequired
                ? QuantitySurveyValuationWorkflowStatuses.ConsultantEndorsed
                : QuantitySurveyValuationWorkflowStatuses.QsVetted;
            if (entity.Status != requiredStatus)
                throw Conflict(entity.ConsultantEndorsementRequired
                    ? "The assigned consultant must endorse the QS-vetted valuation before approval submission."
                    : "The valuation must be QS-vetted before approval submission.");
            await ValidateValuationReadinessAsync(entity, entity.ContractorSubmissionRequired,
                entity.ConsultantEndorsementRequired, token);
            var before = Snapshot(entity);
            var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.Valuation, entity.Id,
                entity.ApprovalWorkflowDefinitionId ?? throw Conflict("The frozen QS valuation workflow is unavailable."));
            if (!result.ExecutionResult.Success)
                throw Conflict(result.ExecutionResult.Message ?? "The configured QS valuation workflow could not be started.");
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Valuation)
                .ApplySubmitOutcome(entity, result.Outcome, UserId);
            if (result.Outcome != WorkflowOutcome.Pending)
                throw Conflict("The QS valuation workflow must stop at an independent approval step; automatic terminal outcomes are not permitted.");
            QuantitySurveyValuationWorksheetRules.RequireTransition(
                entity.Status, QuantitySurveyValuationWorkflowStatuses.PendingApproval);
            entity.Status = QuantitySurveyValuationWorkflowStatuses.PendingApproval;
            entity.ApprovalStatus = "Pending";
            entity.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
            TouchValuation(entity, QuantitySurveyAuditEventMap.SubmitValuationApproval, correlationId,
                request.ClientRequestId, mutationHash);
            AddValuationRevision(entity, QuantitySurveyAuditEventMap.SubmitValuationApproval, reason, before,
                Snapshot(entity), correlationId);
            AddValuationAudit(entity, QuantitySurveyAuditEventMap.SubmitValuationApproval, before, Snapshot(entity), correlationId);
            await SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear();
        return Map(await RequiredWorksheetAsync(worksheetId, false, token));
    }

    public Task<QuantitySurveyValuationWorksheetDto> ApproveAsync(
        Guid worksheetId, QuantitySurveyValuationLifecycleRequest request, string correlationId,
        CancellationToken token = default) => CompleteApprovalAsync(worksheetId, request, true, correlationId, token);

    public Task<QuantitySurveyValuationWorksheetDto> RejectAsync(
        Guid worksheetId, QuantitySurveyValuationLifecycleRequest request, string correlationId,
        CancellationToken token = default) => CompleteApprovalAsync(worksheetId, request, false, correlationId, token);

    private async Task<QuantitySurveyValuationWorksheetDto> CompleteApprovalAsync(
        Guid worksheetId, QuantitySurveyValuationLifecycleRequest request, bool approve, string correlationId,
        CancellationToken token)
    {
        var reason = RequiredValuationText(request.Reason, 5, 2000, approve ? "Approval reason" : "Rejection reason");
        var action = approve ? "Approve" : "Reject";
        var mutationHash = Hash(new { Action = action, Reason = reason });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var entity = await RequiredWorksheetAsync(worksheetId, true, token);
            await RequireProjectAccessAsync(entity.ProjectId);
            if (IsValuationMutationRetry(entity, request.ClientRequestId, mutationHash))
            {
                await transaction.CommitAsync(token);
                return;
            }
            ApplyRowVersion(entity, request.RowVersion);
            if (entity.Status != QuantitySurveyValuationWorkflowStatuses.PendingApproval)
                throw Conflict("The valuation must be PendingApproval before this decision.");
            try
            {
                QuantitySurveyValuationWorksheetRules.RequireIndependentApprover(
                    UserId, entity.PreparedById, entity.ContractorSubmittedById,
                    entity.QsVettedById, entity.ConsultantEndorsedById);
            }
            catch (QuantitySurveyValuationWorksheetValidationException exception)
            {
                throw Conflict(exception.Message);
            }
            var status = await ValuationWorkflowStatusAsync(entity, token);
            if (!approve && status == WorkflowInstanceStatus.Completed)
                throw Conflict("A completed QS valuation workflow is approved and cannot be rejected.");
            WorkflowOutcome outcome;
            if (approve && status == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
            else if (!approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                outcome = WorkflowOutcome.Rejected;
            else
            {
                if (status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                    throw Conflict("The QS valuation workflow ended without approval.");
                if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Valuation, entity.Id, UserId))
                    throw new UnauthorizedAccessException("You are not assigned to the current QS valuation approval step.");
                var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Valuation,
                    entity.Id, UserId, action, reason);
                if (!result.ExecutionResult.Success)
                    throw Conflict(result.ExecutionResult.Message ?? $"The QS valuation {action.ToLowerInvariant()} could not be processed.");
                outcome = result.Outcome;
            }
            var expected = approve ? WorkflowOutcome.Approved : WorkflowOutcome.Rejected;
            if (outcome != expected)
            {
                if (!approve) throw Conflict("The shared workflow did not return a rejected outcome.");
                await transaction.CommitAsync(token);
                return;
            }
            var before = Snapshot(entity);
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Valuation)
                .ApplyApprovalOutcome(entity, outcome, UserId, approve ? null : reason);
            QuantitySurveyValuationWorksheetRules.RequireTransition(
                entity.Status, approve ? QuantitySurveyValuationWorkflowStatuses.Approved : QuantitySurveyValuationWorkflowStatuses.Rejected);
            entity.Status = approve ? QuantitySurveyValuationWorkflowStatuses.Approved : QuantitySurveyValuationWorkflowStatuses.Rejected;
            entity.ApprovalStatus = approve ? "Approved" : "Rejected";
            entity.CertificateReady = approve && QuantitySurveyValuationWorksheetRules.IsCertificateReady(
                entity.Status, entity.ApprovalStatus, entity.ContractorSubmissionRequired,
                entity.ContractorSubmittedById, entity.ConsultantEndorsementRequired,
                entity.ConsultantEndorsedById, entity.SupportingEvidenceRequired,
                entity.Evidence.Count, entity.WorkflowInstanceId, entity.ApprovedById);
            if (approve && !entity.CertificateReady)
                throw Conflict("The approved valuation is not ready for certificate generation because a configured control is incomplete.");
            entity.CertificateReadyAt = approve ? DateTime.UtcNow : null;
            if (!approve) entity.RejectionReason = reason;
            SyncParentValuation(entity, approve ? ProjectInterimValuationStatuses.Certified : ProjectInterimValuationStatuses.Rejected);
            var auditAction = approve ? QuantitySurveyAuditEventMap.ApproveValuation : QuantitySurveyAuditEventMap.RejectValuation;
            TouchValuation(entity, auditAction, correlationId, request.ClientRequestId, mutationHash);
            AddValuationRevision(entity, auditAction, reason, before, Snapshot(entity), correlationId);
            AddValuationAudit(entity, auditAction, before, Snapshot(entity), correlationId);
            await SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear();
        return Map(await RequiredWorksheetAsync(worksheetId, false, token));
    }

    public async Task<QuantitySurveyValuationEvidenceDto> AddEvidenceAsync(
        Guid worksheetId, Stream stream, string fileName, string contentType,
        AddQuantitySurveyValuationEvidenceRequest request, bool external, string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || !Enum.IsDefined(request.EvidenceType))
            throw Validation("Select a valid evidence type and provide a client request identifier.");
        var entity = await RequiredWorksheetAsync(worksheetId, true, token);
        ExternalValuationActor? actor = null;
        if (external) actor = await RequireExternalValuationAsync(entity, false, true, token);
        else await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.Status is QuantitySurveyValuationWorkflowStatuses.PendingApproval or
            QuantitySurveyValuationWorkflowStatuses.Approved or QuantitySurveyValuationWorkflowStatuses.Rejected)
            throw Conflict("Evidence cannot be changed after the valuation enters approval or reaches a terminal state.");
        if (entity.Evidence.Count >= MaximumValuationEvidenceFiles)
            throw Validation($"An interim valuation can contain at most {MaximumValuationEvidenceFiles} evidence files.");
        var policy = await ResolveValuationPolicyAsync(DateTime.UtcNow, token);
        ValidateFrozenValuationPolicy(entity, policy);
        var safeName = Path.GetFileName(fileName?.Trim());
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 260) throw Validation("Select a file with a valid name.");
        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        if (!policy.External.AllowedFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw Validation("The selected file type is not allowed by the effective external-submission policy.");
        await using var memory = new MemoryStream(); await stream.CopyToAsync(memory, token);
        var maximumBytes = policy.External.MaximumFileSizeMb * 1024L * 1024L;
        if (memory.Length is < 1 || memory.Length > maximumBytes)
            throw Validation($"Evidence files must be between 1 byte and {policy.External.MaximumFileSizeMb} MB.");
        var bytes = memory.ToArray();
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var title = RequiredValuationText(request.Title, 3, 200, "Evidence title");
        var canonicalType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim().ToLowerInvariant();
        var requestHash = Hash(new { WorksheetId = worksheetId, request.EvidenceType, Title = title, safeName, canonicalType, checksum });
        var retry = await db.QuantitySurveyValuationWorksheetEvidence.AsNoTracking()
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
        if (retry is not null)
        {
            if (retry.WorksheetId != worksheetId || !FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
            return MapValuationEvidence(retry);
        }
        var upload = await controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
            Category = ControlledFileUploadCategories.QuantitySurveyValuationEvidence,
            FileName = safeName, ContentType = canonicalType, FileSize = bytes.LongLength,
            OpenReadStream = () => new MemoryStream(bytes, false)
        }, token);
        if (upload.Record.VirusScanStatus != FileVirusScanStatus.Clean || !FixedEquals(upload.ChecksumSha256, checksum))
        {
            await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token);
            throw Conflict("The centrally scanned evidence did not pass its integrity check.");
        }
        var evidenceId = Guid.NewGuid();
        CentralDocumentRepositoryLink document;
        try
        {
            document = await centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
                FileUploadRecordId = upload.Record.Id, SourceModule = "QuantitySurvey",
                SourceLabel = "Quantity Survey interim valuation evidence",
                SourceEntityType = nameof(QuantitySurveyValuationWorksheetEvidence), SourceRecordId = evidenceId,
                SourceRecordReference = entity.ProjectInterimValuation.ValuationNumber,
                Title = $"{ValuationLabel(entity.ProjectInterimValuation)} · {title}",
                DocumentType = entity.EvidenceMetadataTemplate?.DocumentType
                    ?? throw Conflict("The frozen interim-valuation DMS template is unavailable."),
                MetadataTemplateCode = entity.EvidenceMetadataTemplateCodeSnapshot,
                AccessProfile = entity.EvidenceMetadataTemplate?.AccessProfile ?? "Module restricted",
                VersionStatus = "Validated", RequirePublishedGovernance = true,
                ChangeSummary = "Clean scanned interim-valuation evidence retained in the central DMS.",
                MetadataValues =
                [
                    new("valuationWorksheetId", "Valuation worksheet ID", entity.Id.ToString(), "guid"),
                    new("projectId", "Project ID", entity.ProjectId.ToString(), "guid"),
                    new("interimValuationId", "Interim valuation ID", entity.ProjectInterimValuationId.ToString(), "guid"),
                    new("evidenceType", "Evidence type", request.EvidenceType.ToString()),
                    new("checksumSha256", "Checksum SHA-256", checksum)
                ]
            }, token);
        }
        catch (Exception failure)
        {
            await CleanupFailedValuationUploadAsync(upload.Record.Id, failure, token);
            throw new InvalidOperationException("Unreachable after preserving the evidence-registration failure.");
        }
        try
        {
            // The shared upload and DMS owners save through this request-scoped DbContext.
            // Reload the aggregate so its SQL rowversion cannot be stale when we touch it below.
            db.ChangeTracker.Clear();
            entity = await RequiredWorksheetAsync(worksheetId, true, token);
            if (external)
                actor = await RequireExternalValuationAsync(entity, false, true, token);
            else
                await RequireProjectAccessAsync(entity.ProjectId);
            if (entity.Status is QuantitySurveyValuationWorkflowStatuses.PendingApproval or
                QuantitySurveyValuationWorkflowStatuses.Approved or QuantitySurveyValuationWorkflowStatuses.Rejected)
                throw Conflict("Evidence cannot be changed after the valuation enters approval or reaches a terminal state.");
            if (entity.Evidence.Count >= MaximumValuationEvidenceFiles)
                throw Validation($"An interim valuation can contain at most {MaximumValuationEvidenceFiles} evidence files.");
            ValidateFrozenValuationPolicy(entity, policy);

            var before = Snapshot(entity);
            var evidence = new QuantitySurveyValuationWorksheetEvidence
            {
                Id = evidenceId, TenantId = TenantId, WorksheetId = entity.Id,
                ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
                EvidenceType = request.EvidenceType, Title = title,
                OriginalFileName = upload.Record.OriginalFileName, ContentType = canonicalType,
                FileSize = bytes.LongLength, ChecksumSha256 = checksum,
                FileUploadRecordId = document.FileUploadRecordId,
                CentralDocumentRecordId = document.DocumentRecordId,
                CentralDocumentVersionId = document.DocumentVersionId,
                UploadedById = UserId, UploadedByName = UserName, UploadedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
            };
            db.QuantitySurveyValuationWorksheetEvidence.Add(evidence);
            AddValuationRevision(entity, QuantitySurveyAuditEventMap.AttachValuationEvidence, title,
                before, Snapshot(entity), correlationId, actor?.BusinessPartnerId);
            AddValuationAudit(entity, QuantitySurveyAuditEventMap.AttachValuationEvidence, before, Snapshot(entity), correlationId);
            await SaveChangesAsync(token);
            return MapValuationEvidence(evidence);
        }
        catch (Exception failure)
        {
            await CleanupFailedValuationDocumentAsync(document.DocumentRecordId, failure, token);
            throw new InvalidOperationException("Unreachable after preserving the valuation-evidence failure.");
        }
    }

    private async Task CleanupFailedValuationUploadAsync(
        Guid uploadId, Exception originalFailure, CancellationToken token)
    {
        db.ChangeTracker.Clear();
        try
        {
            await controlledFiles.DeleteAsync(TenantId, uploadId, UserId, token);
        }
        catch (Exception cleanupFailure)
        {
            logger.LogError(cleanupFailure,
                "Failed to compensate controlled QS valuation upload {UploadId} after {FailureType}.",
                uploadId, originalFailure.GetType().Name);
        }

        ExceptionDispatchInfo.Capture(originalFailure).Throw();
    }

    private async Task CleanupFailedValuationDocumentAsync(
        Guid documentRecordId, Exception originalFailure, CancellationToken token)
    {
        db.ChangeTracker.Clear();
        try
        {
            await centralDocuments.DeleteAsync(TenantId, documentRecordId, UserId, token);
        }
        catch (Exception cleanupFailure)
        {
            logger.LogError(cleanupFailure,
                "Failed to compensate central QS valuation document {DocumentRecordId} after {FailureType}.",
                documentRecordId, originalFailure.GetType().Name);
        }

        ExceptionDispatchInfo.Capture(originalFailure).Throw();
    }

    public async Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(
        Guid worksheetId, Guid evidenceId, bool external, CancellationToken token = default)
    {
        var entity = await RequiredWorksheetAsync(worksheetId, false, token);
        if (external) await RequireExternalValuationAsync(entity, false, false, token);
        else await RequireProjectAccessAsync(entity.ProjectId);
        var evidence = entity.Evidence.SingleOrDefault(value => value.Id == evidenceId && !value.IsDeleted)
            ?? throw new QuantitySurveyValuationWorksheetNotFoundException("The valuation evidence was not found.");
        return await centralDocuments.OpenAsync(TenantId, evidence.CentralDocumentRecordId,
                   evidence.CentralDocumentVersionId, token)
               ?? throw Conflict("The central-DMS evidence content is unavailable.");
    }

    private async Task<ValuationPolicyContext> ResolveValuationPolicyAsync(DateTime at, CancellationToken token)
    {
        var profiles = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(value =>
                value.TenantId == TenantId && !value.IsDeleted &&
                value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && value.PublishedAt != null &&
                value.EffectiveFrom <= at && (!value.EffectiveTo.HasValue || value.EffectiveTo >= at))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No Published quantity-survey configuration is effective for this date.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault)
            throw Conflict("More than one quantity-survey configuration is effective for this date.");
        var profile = profiles[0];
        var decisions = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().Where(value =>
                value.TenantId == TenantId && value.ProfileId == profile.Id && !value.IsDeleted &&
                (value.DecisionKey == "QS-DEC-008" || value.DecisionKey == "QS-DEC-013"))
            .ToListAsync(token);
        var valuationDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-008")
            ?? throw Validation("The effective configuration has no QS-DEC-008 valuation decision.");
        var externalDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-013")
            ?? throw Validation("The effective configuration has no QS-DEC-013 external-submission decision.");
        ValidateValuationDecision(valuationDecision, at, "QS-DEC-008");
        ValidateValuationDecision(externalDecision, at, "QS-DEC-013");
        QsValuationCertificateValue valuation; QsExternalSubmissionValue external;
        try
        {
            valuation = JsonSerializer.Deserialize<QsValuationCertificateValue>(valuationDecision.ValueJson,
                ValuationPolicyJsonOptions) ?? new();
            external = JsonSerializer.Deserialize<QsExternalSubmissionValue>(externalDecision.ValueJson,
                ValuationPolicyJsonOptions) ?? new();
        }
        catch (JsonException) { throw Conflict("The effective QS valuation or external-submission policy contains invalid data."); }
        if (valuation.ValuationWorkflowDefinitionId == Guid.Empty ||
            valuation.ValuationEvidenceMetadataTemplateId == Guid.Empty)
            throw Conflict("QS-DEC-008 is incomplete for governed interim valuations.");
        if (external.Channels.Count == 0 || external.AllowedFileExtensions.Count == 0 || external.MaximumFileSizeMb < 1)
            throw Conflict("QS-DEC-013 is incomplete for external interim-valuation submissions.");
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == valuation.ValuationEvidenceMetadataTemplateId && !value.IsDeleted, token)
            ?? throw Validation("The QS interim-valuation evidence DMS template is unavailable.");
        if (!template.IsActive || template.PublishedAt is null)
            throw Validation("The QS interim-valuation evidence DMS template must be active and published.");
        var definition = await db.WorkflowDefinitions.AsNoTracking().Include(value => value.EntityType)
            .Include(value => value.Steps).FirstOrDefaultAsync(value => value.TenantId == TenantId &&
                value.Id == valuation.ValuationWorkflowDefinitionId && !value.IsDeleted && value.IsActive &&
                value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published, token);
        if (definition?.EntityType is null || !string.Equals(definition.EntityType.Code,
                QuantitySurveyWorkflowBindingRegistry.Valuation, StringComparison.OrdinalIgnoreCase))
            throw Conflict("QS-DEC-008 must reference an active Published QS_VALUATION workflow.");
        if (!definition.Steps.Any(step => !step.IsDeleted && step.IsRequired && step.StepType == WorkflowStepType.Approval))
            throw Conflict("The QS_VALUATION workflow must contain an independent approval step.");
        var policyHash = Hash(new
        {
            Profile = profile.Id, profile.Version, Valuation = valuationDecision.Id,
            ValuationValueJson = valuationDecision.ValueJson, External = externalDecision.Id,
            ExternalValueJson = externalDecision.ValueJson, Template = template.Id,
            template.TemplateCode, Workflow = definition.Id
        });
        return new(profile, valuationDecision, externalDecision, valuation, external, template, policyHash);
    }

    private async Task ValidateValuationReadinessAsync(QuantitySurveyValuationWorksheet entity,
        bool requireContractor, bool requireConsultant, CancellationToken token)
    {
        ValidateFrozenValuationPolicy(entity, await ResolveValuationPolicyAsync(DateTime.UtcNow, token));
        if (entity.Lines.Count == 0 || entity.Lines.Any(value => value.IsDeleted))
            throw Conflict("The valuation requires a complete controlled BoQ-line worksheet.");
        if (requireContractor && (!entity.ContractorSubmittedAt.HasValue ||
                                  string.IsNullOrWhiteSpace(entity.ContractorSignatureHash)))
            throw Conflict("The assigned contractor must submit the valuation claim.");
        if (requireConsultant && (!entity.ConsultantEndorsedAt.HasValue ||
                                  string.IsNullOrWhiteSpace(entity.ConsultantSignatureHash)))
            throw Conflict("The assigned consultant must endorse the QS-vetted valuation.");
        if (entity.SupportingEvidenceRequired && entity.Evidence.Count == 0)
            throw Conflict("Add the central-DMS supporting evidence required by the effective valuation policy.");
        if (entity.Evidence.Any(value => value.CentralDocumentRecord.TenantId != TenantId ||
                                         value.CentralDocumentVersion.TenantId != TenantId ||
                                         value.CentralDocumentVersion.DocumentRecordId != value.CentralDocumentRecordId ||
                                         value.CentralDocumentVersion.Status != "Validated"))
            throw Conflict("Valuation evidence no longer satisfies central-DMS lineage controls.");
    }

    private async Task<QuantitySurveyValuationLookupsDto> BuildValuationLookupsAsync(
        Guid projectId, Guid? actorPartnerId, CancellationToken token)
    {
        var versions = await db.ProjectBoqVersions.AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && !value.IsDeleted && value.Status == ProjectBoqVersionStatuses.Approved &&
                value.PublishedAt != null).OrderByDescending(value => value.VersionNumber)
            .Select(value => new QuantitySurveyValuationBoqLookupDto
            {
                Id = value.Id, VersionNumber = value.VersionNumber,
                Label = $"Approved BoQ v{value.VersionNumber} · {value.LineCount} lines · {value.PublishedAt:dd MMM yyyy}",
                LineCount = value.LineCount
            }).ToListAsync(token);
        var policy = await ResolveValuationPolicyAsync(DateTime.UtcNow, token);
        var partnerIds = await AccessibleValuationPartnerIdsAsync(projectId, token);
        if (actorPartnerId.HasValue) partnerIds = [actorPartnerId.Value];
        var partners = await db.BusinessPartners.AsNoTracking().Where(value => value.TenantId == TenantId &&
                partnerIds.Contains(value.Id) && !value.IsDeleted && value.IsActive && !value.IsBlacklisted &&
                (value.RegistrationStatus == BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus ||
                 value.RegistrationStatus == BusinessPartnerLifecyclePolicy.LegacyApprovedRegistrationStatus))
            .OrderBy(value => value.PartnerName)
            .Select(value => new { value.Id, value.PartnerName, value.PartnerCode, value.PartnerType }).ToListAsync(token);
        var portalIds = await db.BusinessPartnerUsers.IgnoreQueryFilters().AsNoTracking().Where(value =>
                value.TenantId == TenantId && partnerIds.Contains(value.BusinessPartnerId) && value.IsActive &&
                !value.IsDeleted && value.User.TenantId == TenantId && value.User.IsActive)
            .Select(value => value.BusinessPartnerId).Distinct().ToListAsync(token);
        var contractors = partners.Where(value => value.PartnerType is "Contractor" or "Both")
            .Where(value => (!policy.External.RequirePortalIdentity && !policy.Valuation.RequireContractorSubmission) ||
                            portalIds.Contains(value.Id))
            .Select(value => new QuantitySurveyValuationPartnerLookupDto
            { Id = value.Id, Label = value.PartnerName, Description = value.PartnerCode }).ToList();
        var consultants = partners.Where(value => !contractors.Any(item => item.Id == value.Id))
            .Where(value => (!policy.External.RequirePortalIdentity && !policy.Valuation.RequireConsultantEndorsement) ||
                            portalIds.Contains(value.Id))
            .Select(value => new QuantitySurveyValuationPartnerLookupDto
            { Id = value.Id, Label = value.PartnerName, Description = $"{value.PartnerCode} · {value.PartnerType}" }).ToList();
        return new() { ApprovedBoqVersions = versions, Contractors = contractors, Consultants = consultants };
    }

    private async Task<ExternalValuationActor> RequireExternalValuationPartnerAsync(CancellationToken token)
    {
        var link = await db.BusinessPartnerUsers.IgnoreQueryFilters().AsNoTracking()
            .Include(value => value.BusinessPartner).Include(value => value.User)
            .FirstOrDefaultAsync(value => value.UserId == UserId && value.IsActive && !value.IsDeleted &&
                !value.BusinessPartner.IsDeleted && value.BusinessPartner.TenantId == TenantId &&
                value.User.TenantId == TenantId && value.User.IsActive, token)
            ?? throw new UnauthorizedAccessException("No active business-partner identity is linked to this portal user.");
        if (!link.BusinessPartner.IsActive ||
            !BusinessPartnerLifecyclePolicy.IsOperationalRegistration(link.BusinessPartner.RegistrationStatus))
            throw new UnauthorizedAccessException("The linked business partner is not active.");
        return new(link.BusinessPartnerId, link.BusinessPartner.PartnerName);
    }

    private async Task<ExternalValuationActor> RequireExternalValuationProjectAsync(
        Guid projectId, bool requireUpload, Guid? worksheetId, CancellationToken token)
    {
        var actor = await RequireExternalValuationPartnerAsync(token);
        _ = await projectService.GetExternalProjectByIdAsync(projectId, UserId)
            ?? throw new UnauthorizedAccessException("You do not have access to the selected project.");
        var project = await db.Projects.AsNoTracking().FirstAsync(value => value.TenantId == TenantId && value.Id == projectId, token);
        if (!project.ExternalPortalAccessEnabled || !project.ExternalCollaborationEnabled)
            throw new UnauthorizedAccessException("External collaboration is not enabled for this project.");
        if (project.BusinessPartnerId == actor.BusinessPartnerId) return actor;
        var policies = await db.ProjectExternalAccessPolicies.AsNoTracking().Where(value => value.TenantId == TenantId &&
            value.ProjectId == projectId && value.BusinessPartnerId == actor.BusinessPartnerId && !value.IsDeleted &&
            (value.ArtifactType == "Project" || (worksheetId.HasValue &&
                value.ArtifactType == "InterimValuation" && value.ArtifactId == worksheetId))).ToListAsync(token);
        if (policies.Count == 0 || (requireUpload && !policies.Any(value => value.CanUpload)))
            throw new UnauthorizedAccessException("The project external-access policy does not permit this valuation action.");
        return actor;
    }

    private async Task<ExternalValuationActor> RequireExternalValuationAsync(
        QuantitySurveyValuationWorksheet entity, bool requireApprove, bool requireUpload, CancellationToken token)
    {
        var actor = await RequireExternalValuationProjectAsync(entity.ProjectId, requireUpload, entity.Id, token);
        if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId &&
            actor.BusinessPartnerId != entity.ConsultantBusinessPartnerId)
            throw new UnauthorizedAccessException("The linked business partner is not assigned to this interim valuation.");
        if (requireApprove)
        {
            var projectPartner = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.Id == entity.ProjectId).Select(value => value.BusinessPartnerId).SingleAsync(token);
            if (projectPartner != actor.BusinessPartnerId)
            {
                var allowed = await db.ProjectExternalAccessPolicies.AsNoTracking().AnyAsync(value =>
                    value.TenantId == TenantId && value.ProjectId == entity.ProjectId &&
                    value.BusinessPartnerId == actor.BusinessPartnerId && !value.IsDeleted && value.CanApprove &&
                    (value.ArtifactType == "Project" || (value.ArtifactType == "InterimValuation" &&
                                                         value.ArtifactId == entity.Id)), token);
                if (!allowed) throw new UnauthorizedAccessException("The external-access policy does not permit valuation submission or endorsement.");
            }
        }
        return actor;
    }

    private async Task<List<Guid>> AccessibleValuationPartnerIdsAsync(Guid projectId, CancellationToken token)
    {
        var projectPartner = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && value.Id == projectId)
            .Select(value => value.BusinessPartnerId).FirstOrDefaultAsync(token);
        var values = await db.ProjectExternalAccessPolicies.AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && !value.IsDeleted && value.ArtifactType == "Project")
            .Select(value => value.BusinessPartnerId).Distinct().ToListAsync(token);
        if (projectPartner.HasValue) values.Add(projectPartner.Value);
        return values.Distinct().ToList();
    }

    private async Task<QuantitySurveyValuationWorksheet> RequiredWorksheetAsync(
        Guid id, bool tracking, CancellationToken token)
        => await Query(tracking).FirstOrDefaultAsync(value => value.Id == id, token)
           ?? throw new QuantitySurveyValuationWorksheetNotFoundException("The interim valuation worksheet was not found.");

    private async Task<WorkflowInstanceStatus?> ValuationWorkflowStatusAsync(
        QuantitySurveyValuationWorksheet entity, CancellationToken token)
        => !entity.WorkflowInstanceId.HasValue ? null :
            (await db.WorkflowInstances.AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId &&
                value.Id == entity.WorkflowInstanceId && value.EntityId == entity.Id, token))?.Status;

    private static void ValidateValuationDecision(QuantitySurveyConfigurationDecision value, DateTime at, string key)
    {
        if (value.Status != QuantitySurveyConfigurationDecisionStatus.Approved ||
            value.ApprovalStatus != QuantitySurveyConfigurationApprovalStatus.Approved ||
            value.EvidenceStatus != QuantitySurveyConfigurationEvidenceStatus.Verified ||
            (value.EffectiveFrom.HasValue && value.EffectiveFrom > at) ||
            (value.EffectiveTo.HasValue && value.EffectiveTo < at))
            throw Validation($"{key} is not approved, verified, and effective for this date.");
    }

    private static void ValidateFrozenValuationPolicy(
        QuantitySurveyValuationWorksheet entity, ValuationPolicyContext policy)
    {
        if (entity.ConfigurationProfileId != policy.Profile.Id || entity.ValuationDecisionId != policy.ValuationDecision.Id ||
            entity.ExternalSubmissionDecisionId != policy.ExternalDecision.Id ||
            entity.ApprovalWorkflowDefinitionId != policy.Valuation.ValuationWorkflowDefinitionId ||
            entity.EvidenceMetadataTemplateId != policy.Template.Id || !FixedEquals(entity.PolicyHash, policy.PolicyHash))
            throw Conflict("The governing QS configuration changed. Create a new interim valuation worksheet.");
    }

    private static bool IsLegacyDraftValuationPolicy(QuantitySurveyValuationWorksheet entity) =>
        entity.Status == QuantitySurveyValuationWorkflowStatuses.Draft &&
        entity.ConfigurationProfileId is null && entity.ValuationDecisionId is null &&
        entity.ExternalSubmissionDecisionId is null && entity.ApprovalWorkflowDefinitionId is null &&
        entity.EvidenceMetadataTemplateId is null && entity.PolicyHash is null;

    private static void FreezeValuationPolicy(
        QuantitySurveyValuationWorksheet entity, ValuationPolicyContext policy)
    {
        if (!IsLegacyDraftValuationPolicy(entity))
            throw Conflict("Only a legacy Draft valuation can adopt the effective QS policy.");
        entity.ConfigurationProfileId = policy.Profile.Id;
        entity.ValuationDecisionId = policy.ValuationDecision.Id;
        entity.ExternalSubmissionDecisionId = policy.ExternalDecision.Id;
        entity.ApprovalWorkflowDefinitionId = policy.Valuation.ValuationWorkflowDefinitionId;
        entity.EvidenceMetadataTemplateId = policy.Template.Id;
        entity.EvidenceMetadataTemplateCodeSnapshot = policy.Template.TemplateCode;
        entity.PolicyHash = policy.PolicyHash;
        entity.ContractorSubmissionRequired = policy.Valuation.RequireContractorSubmission;
        entity.ConsultantEndorsementRequired = policy.Valuation.RequireConsultantEndorsement;
        entity.SupportingEvidenceRequired = policy.Valuation.RequireSupportingEvidence || policy.External.RequireEvidence;
        entity.PortalIdentityRequired = policy.External.RequirePortalIdentity;
        entity.ExternalSignatureRequired = policy.External.RequireSignature;
    }

    private static string ValidateValuationSignature(WorkflowSignatureSubmissionDto signature,
        string attestation, bool required, Guid worksheetId, Guid businessPartnerId)
    {
        if (!required) return Hash(new { worksheetId, businessPartnerId, Optional = true });
        var policy = new WorkflowSignaturePolicyDto
        {
            IsRequired = true, Method = signature.Method,
            RequireValidCertificateChain = signature.Method == WorkflowSignatureMethod.DigitalCertificate,
            AttestationText = attestation
        };
        var errors = WorkflowSignatureValidator.Validate(policy, [], new { signature }, DateTime.UtcNow);
        if (errors.Count > 0) throw Validation(string.Join(" ", errors));
        var certificate = signature.Method == WorkflowSignatureMethod.DigitalCertificate
            ? WorkflowSignatureValidator.InspectCertificate(signature.CertificateBase64, DateTime.UtcNow) : null;
        return Hash(new { worksheetId, businessPartnerId, signature.Method, attestation,
            certificate?.Thumbprint, signature.ExternalReference, signature.SignedAt });
    }

    private static bool IsValuationMutationRetry(
        QuantitySurveyValuationWorksheet entity, Guid clientRequestId, string requestHash)
    {
        if (clientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (entity.LastMutationClientRequestId != clientRequestId) return false;
        if (!FixedEquals(entity.LastMutationRequestHash, requestHash)) throw RetryConflict();
        return true;
    }

    private void TouchValuation(QuantitySurveyValuationWorksheet entity, string action, string correlationId,
        Guid? clientRequestId = null, string? requestHash = null)
    {
        entity.AuditAction = action; entity.CorrelationId = Correlation(correlationId); entity.ActorRoles = ActorRoles;
        if (clientRequestId.HasValue)
        {
            entity.LastMutationClientRequestId = clientRequestId;
            entity.LastMutationRequestHash = requestHash;
        }
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
    }

    private void AddValuationRevision(QuantitySurveyValuationWorksheet entity, string action, string? reason,
        object? before, object after, string correlationId, Guid? actorPartnerId = null)
        => db.QuantitySurveyValuationWorksheetRevisions.Add(new QuantitySurveyValuationWorksheetRevision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, WorksheetId = entity.Id, Action = action,
            ActorUserId = UserId, ActorBusinessPartnerId = actorPartnerId, ActorName = UserName,
            ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(new { reason = CleanValuationText(reason, 2000), value = after }, JsonOptions),
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });

    private void AddValuationAudit(QuantitySurveyValuationWorksheet entity, string action,
        object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog
    {
        TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
        Resource = nameof(QuantitySurveyValuationWorksheet), ResourceId = entity.Id.ToString(),
        OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
        IpAddress = "api", UserAgent = "QS-0502", Timestamp = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
    });

    private void SyncParentValuation(QuantitySurveyValuationWorksheet entity, string status)
    {
        var parent = entity.ProjectInterimValuation;
        parent.Status = status; parent.GrossWorkValue = entity.CurrentPeriodCertifiedValue;
        parent.RetentionPercentage = entity.RetentionPercentage; parent.RetentionAmount = entity.CurrentRetentionValue;
        parent.PreviousCertifiedAmount = entity.PreviouslyCertifiedValue; parent.NetValuationAmount = entity.NetCurrentValue;
        parent.UpdatedAt = DateTime.UtcNow; parent.UpdatedBy = UserName; parent.LastModifiedById = UserId;
    }

    private static QuantitySurveyValuationEvidenceDto MapValuationEvidence(QuantitySurveyValuationWorksheetEvidence value) => new()
    {
        Id = value.Id, EvidenceType = value.EvidenceType, Title = value.Title,
        OriginalFileName = value.OriginalFileName, ContentType = value.ContentType,
        FileSize = value.FileSize, ChecksumSha256 = value.ChecksumSha256,
        CentralDocumentRecordId = value.CentralDocumentRecordId,
        CentralDocumentVersionId = value.CentralDocumentVersionId,
        UploadedByName = value.UploadedByName, UploadedAt = value.UploadedAt
    };

    private static string RequiredValuationText(string? value, int min, int max, string label)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean) || clean.Length < min || clean.Length > max)
            throw Validation($"{label} must contain between {min} and {max} characters.");
        return clean;
    }

    private static string? CleanValuationText(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max
            ? value.Trim() : throw Validation($"Text cannot exceed {max} characters.");

    private static JsonSerializerOptions CreateValuationPolicyJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private readonly record struct ExternalValuationActor(Guid BusinessPartnerId, string PartnerName);
    private sealed record ValuationPolicyContext(
        QuantitySurveyConfigurationProfile Profile,
        QuantitySurveyConfigurationDecision ValuationDecision,
        QuantitySurveyConfigurationDecision ExternalDecision,
        QsValuationCertificateValue Valuation,
        QsExternalSubmissionValue External,
        ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate Template,
        string PolicyHash);
}
