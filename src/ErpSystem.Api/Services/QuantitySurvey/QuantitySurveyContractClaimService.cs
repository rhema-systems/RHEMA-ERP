using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyContractClaimService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters,
    IControlledFileUploadService controlledFiles,
    ICentralDocumentRepositoryFileService centralDocuments) : IQuantitySurveyContractClaimService
{
    private const int MaximumEvidenceBytes = 50 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyContractClaimWorkspaceDto> GetWorkspaceAsync(Guid projectId, bool external, CancellationToken token = default)
    {
        ExternalActor? actor = external ? await RequireExternalActorAsync(projectId, false, false, null, token) : null;
        if (!external) await RequireProjectAsync(projectId);
        var contractsQuery = db.Contracts.AsNoTracking().Include(value => value.BusinessPartner)
            .Include(value => value.Tender).ThenInclude(value => value.SourcePurchaseRequisition)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.ContractType == "Works" && value.Status == "Active" &&
                value.Tender.SourcePurchaseRequisition != null && value.Tender.SourcePurchaseRequisition.ProjectId == projectId);
        if (actor.HasValue) contractsQuery = contractsQuery.Where(value => value.BusinessPartnerId == actor.Value.BusinessPartnerId);
        var contracts = await contractsQuery.OrderBy(value => value.ContractNumber).Select(value =>
            new QuantitySurveyClaimContractLookupDto(value.Id, value.ContractNumber, value.ContractTitle, value.BusinessPartnerId,
                value.BusinessPartner.PartnerName, value.ContractValue, value.Currency)).ToListAsync(token);
        var contractIds = contracts.Select(value => value.Id).ToList();
        var variations = await db.ProjectVariationOrders.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId &&
                value.ContractId.HasValue && contractIds.Contains(value.ContractId.Value) && value.IsQuantitySurveyGoverned && !value.IsDeleted &&
                value.Status == ProjectVariationOrderStatuses.Approved)
            .OrderByDescending(value => value.ApprovedAt).Select(value => new QuantitySurveyClaimVariationLookupDto(value.Id,
                value.ReferenceNumber ?? value.Id.ToString(), value.Title, value.VariationType, value.ApprovedAmount ?? 0m)).ToListAsync(token);
        var extensions = await db.ProjectExtensionOfTimeRequests.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId &&
                value.ContractId.HasValue && contractIds.Contains(value.ContractId.Value) && !value.IsDeleted &&
                (value.Status == ProjectExtensionOfTimeStatuses.Submitted || value.Status == ProjectExtensionOfTimeStatuses.UnderReview ||
                 value.Status == ProjectExtensionOfTimeStatuses.Approved || value.Status == ProjectExtensionOfTimeStatuses.Implemented))
            .OrderByDescending(value => value.RequestedDate).Select(value => new QuantitySurveyClaimExtensionLookupDto(value.Id,
                value.ReferenceNumber ?? value.Id.ToString(), value.Title, value.DaysRequested, value.DaysApproved, value.Status)).ToListAsync(token);
        var boqs = await db.ProjectBoqVersions.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                value.ApprovalStatus == "Approved" && value.PublishedAt != null && value.Status != "Retired")
            .OrderByDescending(value => value.VersionNumber).Select(value => new QuantitySurveyClaimBoqLookupDto(value.Id,
                value.VersionNumber.ToString(), value.Status)).ToListAsync(token);
        var claimsQuery = Query().Where(value => value.ProjectId == projectId);
        if (actor.HasValue) claimsQuery = claimsQuery.Where(value => value.ContractorBusinessPartnerId == actor.Value.BusinessPartnerId);
        var claims = (await claimsQuery.OrderByDescending(value => value.CreatedAt).ToListAsync(token)).Select(Map).ToList();
        return new() { Contracts = contracts, Variations = variations, ExtensionsOfTime = extensions, ApprovedBoqVersions = boqs, Claims = claims };
    }

    public async Task<QuantitySurveyContractClaimDto> GetAsync(Guid id, bool external, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        if (external)
        {
            var actor = await RequireExternalActorAsync(entity.ProjectId, false, false, entity.Id, token);
            if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId)
                throw new UnauthorizedAccessException("The claim is not assigned to the linked contractor.");
        }
        else await RequireProjectAsync(entity.ProjectId);
        return Map(entity);
    }

    public async Task<QuantitySurveyContractClaimDto> SaveExternalAsync(Guid projectId, SaveQuantitySurveyContractClaimRequest request,
        string correlationId, CancellationToken token = default)
    {
        var actor = await RequireExternalActorAsync(projectId, true, false, request.Id, token);
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (!QuantitySurveyContractClaimRules.HasValidSource(request.ClaimType, request.VariationOrderId, request.ExtensionOfTimeId))
            throw Validation("Select the required controlled source for the selected claim type.");
        var title = RequiredText(request.Title, 3, 200, "Claim title");
        var basis = RequiredText(request.Basis, 10, 4000, "Claim basis");
        var amount = Round(request.ClaimedAmount);
        if (amount <= 0) throw Validation("The claimed amount must be greater than zero.");
        var contract = await RequiredContractAsync(projectId, request.ContractId, actor.BusinessPartnerId, token);
        await ValidateSourcesAsync(projectId, contract.Id, request, token);
        var policy = await ResolvePolicyAsync(token);
        var requestHash = Hash(new { projectId, request.ContractId, request.ApprovedBoqVersionId, request.VariationOrderId,
            request.ExtensionOfTimeId, request.ClaimType, title, basis, amount, actor.BusinessPartnerId, policy.PolicyHash });
        if (request.Id.HasValue)
        {
            var retryRevision = await db.QuantitySurveyContractClaimRevisions.AsNoTracking().FirstOrDefaultAsync(value =>
                value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId, token);
            if (retryRevision is not null)
            {
                if (retryRevision.ContractClaimId != request.Id.Value || !FixedEquals(retryRevision.RequestHash, requestHash)) throw RetryConflict();
                return await GetAsync(retryRevision.ContractClaimId, true, token);
            }
        }
        else
        {
            var retry = await db.QuantitySurveyContractClaims.AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId &&
                value.ClientRequestId == request.ClientRequestId, token);
            if (retry is not null)
            {
                if (!FixedEquals(retry.RequestHash, requestHash) || retry.ProjectId != projectId) throw RetryConflict();
                return await GetAsync(retry.Id, true, token);
            }
        }
        var strategy = db.Database.CreateExecutionStrategy();
        Guid id = Guid.Empty;
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            QuantitySurveyContractClaim entity;
            object? before = null;
            var action = QuantitySurveyAuditEventMap.CreateContractClaim;
            if (request.Id.HasValue)
            {
                entity = await RequiredAsync(request.Id.Value, true, token);
                if (entity.ProjectId != projectId || entity.ContractorBusinessPartnerId != actor.BusinessPartnerId)
                    throw new UnauthorizedAccessException("The claim is not assigned to the linked contractor.");
                if (!QuantitySurveyContractClaimRules.CanEdit(entity.Status)) throw Conflict("Only a Draft or Rejected claim can be amended.");
                ApplyRowVersion(entity, request.RowVersion);
                before = Snapshot(entity); action = QuantitySurveyAuditEventMap.UpdateContractClaim;
                entity.LastMutationClientRequestId = request.ClientRequestId;
                entity.LastMutationRequestHash = requestHash;
            }
            else
            {
                entity = new QuantitySurveyContractClaim
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId,
                    ClaimNumber = await NextNumberAsync(token), ClientRequestId = request.ClientRequestId,
                    CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
                };
                db.QuantitySurveyContractClaims.Add(entity);
            }
            entity.ContractId = contract.Id; entity.ContractorBusinessPartnerId = actor.BusinessPartnerId;
            entity.ApprovedBoqVersionId = request.ApprovedBoqVersionId; entity.VariationOrderId = request.VariationOrderId;
            entity.ExtensionOfTimeId = request.ExtensionOfTimeId; entity.ClaimType = request.ClaimType;
            entity.Title = title; entity.Basis = basis; entity.ClaimedAmount = amount; entity.Currency = contract.Currency.ToUpperInvariant();
            entity.Status = QuantitySurveyContractClaimStatuses.Draft; entity.ApprovalStatus = "Draft";
            entity.QsAssessedAmount = null; entity.ApprovedAmount = null; entity.RejectedAmount = null; entity.QsReviewNote = null;
            entity.DisputeStatus = QuantitySurveyClaimDisputeStatus.None; entity.DisputeReason = null; entity.DisputeResolution = null;
            entity.SettlementStatus = QuantitySurveyClaimSettlementStatus.NotApplicable; entity.SettledAmount = 0; entity.SettlementReference = null; entity.SettlementDate = null;
            entity.ConfigurationProfileId = policy.ProfileId; entity.VariationDecisionId = policy.DecisionId;
            entity.ApprovalWorkflowDefinitionId = policy.WorkflowDefinitionId; entity.EvidenceMetadataTemplateId = policy.EvidenceTemplateId;
            entity.PolicyHash = policy.PolicyHash;
            if (!request.Id.HasValue) entity.RequestHash = requestHash;
            entity.CorrelationId = Correlation(correlationId);
            entity.SubmittedById = null; entity.SubmittedBusinessPartnerId = actor.BusinessPartnerId; entity.SubmittedAt = null;
            entity.QsVettedById = null; entity.QsVettedAt = null; entity.ApprovedById = null; entity.ApprovedAt = null;
            entity.WorkflowInstanceId = null; entity.RejectionReason = null; entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
            AddRevision(entity, request.ClientRequestId, requestHash, action, basis, before, Snapshot(entity), correlationId, actor.BusinessPartnerId);
            AddAudit(entity, action, before, Snapshot(entity), correlationId);
            await SaveChangesAsync(token); id = entity.Id; await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear(); return await GetAsync(id, true, token);
    }

    public async Task<QuantitySurveyContractClaimEvidenceDto> UploadEvidenceAsync(Guid id, Guid clientRequestId, string title,
        string fileName, string contentType, long fileSize, Func<Stream> openRead, bool external, string correlationId,
        CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        Guid? actorPartnerId = null;
        if (external)
        {
            var actor = await RequireExternalActorAsync(entity.ProjectId, true, false, entity.Id, token);
            if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId) throw new UnauthorizedAccessException("The claim is not assigned to the linked contractor.");
            actorPartnerId = actor.BusinessPartnerId;
        }
        else await RequireProjectAsync(entity.ProjectId);
        if (!QuantitySurveyContractClaimRules.CanEdit(entity.Status)) throw Conflict("Evidence can be added only to a Draft or Rejected claim.");
        if (clientRequestId == Guid.Empty || fileSize <= 0 || fileSize > MaximumEvidenceBytes) throw Validation("Select an evidence file up to 50 MB.");
        var safeTitle = RequiredText(title, 3, 200, "Evidence title"); var safeName = Path.GetFileName(fileName);
        await using var source = openRead(); using var memory = new MemoryStream(); await source.CopyToAsync(memory, token);
        var bytes = memory.ToArray(); if (bytes.LongLength != fileSize) throw Validation("The evidence file size changed during upload.");
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var requestHash = Hash(new { id, safeTitle, safeName, contentType, fileSize, checksum });
        var retry = await db.QuantitySurveyContractClaimEvidence.AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == clientRequestId, token);
        if (retry is not null) { if (retry.ContractClaimId != id || !FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict(); return MapEvidence(retry); }
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId &&
            value.Id == entity.EvidenceMetadataTemplateId && !value.IsDeleted && value.IsActive && value.PublishedAt != null, token)
            ?? throw Conflict("The frozen claim-evidence DMS template is no longer active and Published.");
        var upload = await controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
            Category = ControlledFileUploadCategories.QuantitySurveyClaimEvidence, FileName = safeName,
            ContentType = contentType, FileSize = bytes.LongLength, OpenReadStream = () => new MemoryStream(bytes, false)
        }, token);
        if (upload.Record.VirusScanStatus != FileVirusScanStatus.Clean || !FixedEquals(upload.ChecksumSha256, checksum))
        { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw Conflict("The centrally scanned evidence did not pass its integrity check."); }
        var evidenceId = Guid.NewGuid(); CentralDocumentRepositoryLink document;
        try
        {
            document = await centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = TenantId, ActorUserId = UserId, ActorName = UserName, FileUploadRecordId = upload.Record.Id,
                SourceModule = "QuantitySurvey", SourceLabel = "Quantity Survey contractor claim evidence",
                SourceEntityType = nameof(QuantitySurveyContractClaimEvidence), SourceRecordId = evidenceId,
                SourceRecordReference = entity.ClaimNumber, Title = safeTitle, DocumentType = "ContractClaimEvidence",
                MetadataTemplateCode = template.TemplateCode, AccessProfile = template.AccessProfile, VersionStatus = "Submitted",
                ChangeSummary = "Clean scanned contractor-claim evidence retained in the central DMS.", RequirePublishedGovernance = true,
                MetadataValues = [new("contractClaimId", "Contract claim ID", entity.Id.ToString(), "guid"),
                    new("projectId", "Project ID", entity.ProjectId.ToString(), "guid"),
                    new("checksumSha256", "Checksum SHA-256", checksum)]
            }, token);
        }
        catch { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw; }
        var evidence = new QuantitySurveyContractClaimEvidence
        {
            Id = evidenceId, TenantId = TenantId, ContractClaimId = id, ClientRequestId = clientRequestId, RequestHash = requestHash,
            Title = safeTitle, OriginalFileName = upload.Record.OriginalFileName, ContentType = contentType, FileSize = bytes.LongLength,
            ChecksumSha256 = checksum, FileUploadRecordId = document.FileUploadRecordId,
            CentralDocumentRecordId = document.DocumentRecordId, CentralDocumentVersionId = document.DocumentVersionId,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        };
        db.QuantitySurveyContractClaimEvidence.Add(evidence);
        AddAudit(entity, QuantitySurveyAuditEventMap.AttachContractClaimEvidence, null,
            new { evidence.Id, evidence.Title, evidence.ChecksumSha256, actorPartnerId }, correlationId);
        try { await SaveChangesAsync(token); }
        catch { await centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, token); throw; }
        return MapEvidence(evidence);
    }

    public async Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(
        Guid id, Guid evidenceId, bool external, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        if (external)
        {
            var actor = await RequireExternalActorAsync(entity.ProjectId, false, false, entity.Id, token);
            if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId)
                throw new UnauthorizedAccessException("The claim is not assigned to the linked contractor.");
        }
        else await RequireProjectAsync(entity.ProjectId);
        var evidence = entity.Evidence.SingleOrDefault(value => value.Id == evidenceId && !value.IsDeleted)
            ?? throw new QuantitySurveyContractClaimNotFoundException("The contractor-claim evidence was not found.");
        return await centralDocuments.OpenAsync(TenantId, evidence.CentralDocumentRecordId,
                   evidence.CentralDocumentVersionId, token)
               ?? throw Conflict("The central-DMS evidence content is unavailable.");
    }

    public async Task<QuantitySurveyContractClaimDto> SubmitExternalAsync(Guid id, QuantitySurveyContractClaimActionRequest request,
        string correlationId, CancellationToken token = default)
    {
        var actor = await ExternalForClaimAsync(id, true, false, token);
        return await MutateAsync(id, request, QuantitySurveyAuditEventMap.SubmitContractClaim, correlationId, token, actor.BusinessPartnerId, async value =>
        {
            if (!QuantitySurveyContractClaimRules.CanEdit(value.Status)) throw Conflict("Only a Draft or Rejected claim can be submitted.");
            if (value.Evidence.All(item => item.IsDeleted)) throw Conflict("Attach at least one clean centrally governed evidence file before submission.");
            await ValidateFrozenAsync(value, token);
            value.Status = QuantitySurveyContractClaimStatuses.Submitted; value.ApprovalStatus = "Draft";
            value.SubmittedById = UserId; value.SubmittedBusinessPartnerId = actor.BusinessPartnerId; value.SubmittedAt = DateTime.UtcNow;
            value.QsVettedById = null; value.QsVettedAt = null; value.QsAssessedAmount = null; value.QsReviewNote = null;
        });
    }

    public Task<QuantitySurveyContractClaimDto> VetAsync(Guid id, VetQuantitySurveyContractClaimRequest request,
        string correlationId, CancellationToken token = default) => MutateAsync(id, request,
        QuantitySurveyAuditEventMap.VetContractClaim, correlationId, token, null, async value =>
        {
            await RequireProjectAsync(value.ProjectId);
            if (value.Status != QuantitySurveyContractClaimStatuses.Submitted) throw Conflict("Only a submitted contractor claim can be QS-vetted.");
            if (value.SubmittedById == UserId) throw Conflict("The contractor submitter cannot vet the same claim.");
            var assessed = Round(request.AssessedAmount);
            if (assessed < 0 || assessed > value.ClaimedAmount) throw Validation("The assessed amount must be between zero and the claimed amount.");
            await ValidateFrozenAsync(value, token);
            value.QsAssessedAmount = assessed; value.RejectedAmount = QuantitySurveyContractClaimRules.RejectedAmount(value.ClaimedAmount, assessed);
            value.QsReviewNote = RequiredText(request.Reason, 5, 2000, "QS review note"); value.QsVettedById = UserId;
            value.QsVettedAt = DateTime.UtcNow; value.Status = QuantitySurveyContractClaimStatuses.Vetted;
        });

    public Task<QuantitySurveyContractClaimDto> SubmitApprovalAsync(Guid id, QuantitySurveyContractClaimActionRequest request,
        string correlationId, CancellationToken token = default) => MutateAsync(id, request,
        QuantitySurveyAuditEventMap.SubmitContractClaimApproval, correlationId, token, null, async value =>
        {
            await RequireProjectAsync(value.ProjectId);
            if (value.Status != QuantitySurveyContractClaimStatuses.Vetted || !value.QsAssessedAmount.HasValue)
                throw Conflict("The claim must be QS-vetted before workflow submission.");
            await ValidateFrozenAsync(value, token);
            var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.Claim, value.Id, value.ApprovalWorkflowDefinitionId);
            if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The claim workflow could not be started.");
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Claim).ApplySubmitOutcome(value, result, UserId);
            if (result.Outcome != WorkflowOutcome.Pending) throw Conflict("The claim workflow must stop at an independent approval step.");
            value.Status = QuantitySurveyContractClaimStatuses.PendingApproval; value.ApprovalStatus = "Pending";
            value.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
        });

    public Task<QuantitySurveyContractClaimDto> DecideAsync(Guid id, QuantitySurveyContractClaimActionRequest request, bool approve,
        string correlationId, CancellationToken token = default) => MutateAsync(id, request,
        approve ? QuantitySurveyAuditEventMap.ApproveContractClaim : QuantitySurveyAuditEventMap.RejectContractClaim,
        correlationId, token, null, async value =>
        {
            await RequireProjectAsync(value.ProjectId);
            if (value.Status != QuantitySurveyContractClaimStatuses.PendingApproval || !value.WorkflowInstanceId.HasValue)
                throw Conflict("The claim is not pending workflow approval.");
            if (value.SubmittedById == UserId || value.QsVettedById == UserId)
                throw Conflict("The contractor submitter or QS vetter cannot decide the same claim.");
            await ValidateFrozenAsync(value, token);
            var status = await db.WorkflowInstances.AsNoTracking().Where(item => item.TenantId == TenantId &&
                item.Id == value.WorkflowInstanceId && item.EntityId == value.Id).Select(item => (WorkflowInstanceStatus?)item.Status).SingleOrDefaultAsync(token);
            WorkflowOutcome outcome;
            if (approve && status == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
            else if (!approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) outcome = WorkflowOutcome.Rejected;
            else
            {
                if (!approve && status == WorkflowInstanceStatus.Completed) throw Conflict("A completed workflow is approved and cannot be rejected.");
                if (approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) throw Conflict("The workflow ended without approval.");
                if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Claim, value.Id, UserId))
                    throw new UnauthorizedAccessException("You are not assigned to the current claim approval step.");
                var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Claim, value.Id, UserId,
                    approve ? "Approve" : "Reject", RequiredText(request.Reason, 5, 2000, "Decision reason"));
                if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The claim workflow decision failed.");
                outcome = result.Outcome;
            }
            if (outcome != (approve ? WorkflowOutcome.Approved : WorkflowOutcome.Rejected))
                throw Conflict("The shared workflow has not reached the requested final outcome.");
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Claim)
                .ApplyApprovalOutcome(value, outcome, UserId, approve ? null : RequiredText(request.Reason, 5, 2000, "Rejection reason"));
            value.Status = approve ? QuantitySurveyContractClaimStatuses.Approved : QuantitySurveyContractClaimStatuses.Rejected;
            value.ApprovalStatus = approve ? "Approved" : "Rejected";
            value.ApprovedAmount = approve ? value.QsAssessedAmount : 0m;
            value.RejectedAmount = QuantitySurveyContractClaimRules.RejectedAmount(value.ClaimedAmount, value.ApprovedAmount ?? 0m);
            value.SettlementStatus = approve && value.ApprovedAmount > 0 ? QuantitySurveyClaimSettlementStatus.Pending : QuantitySurveyClaimSettlementStatus.NotApplicable;
        });

    public async Task<QuantitySurveyContractClaimDto> OpenDisputeExternalAsync(Guid id, QuantitySurveyContractClaimActionRequest request,
        string correlationId, CancellationToken token = default)
    {
        var actor = await ExternalForClaimAsync(id, true, false, token);
        return await MutateAsync(id, request, QuantitySurveyAuditEventMap.OpenContractClaimDispute, correlationId, token,
            actor.BusinessPartnerId, value =>
            {
                if (value.Status is not (QuantitySurveyContractClaimStatuses.Approved or QuantitySurveyContractClaimStatuses.Rejected))
                    throw Conflict("Only a decided claim can be disputed.");
                if (value.DisputeStatus != QuantitySurveyClaimDisputeStatus.None) throw Conflict("The claim already has a dispute outcome.");
                value.DisputeStatus = QuantitySurveyClaimDisputeStatus.Open;
                value.DisputeReason = RequiredText(request.Reason, 5, 2000, "Dispute reason"); value.DisputeResolution = null;
                return Task.CompletedTask;
            });
    }

    public Task<QuantitySurveyContractClaimDto> ResolveDisputeAsync(Guid id, QuantitySurveyContractClaimActionRequest request, bool accepted,
        string correlationId, CancellationToken token = default) => MutateAsync(id, request,
        QuantitySurveyAuditEventMap.ResolveContractClaimDispute, correlationId, token, null, async value =>
        {
            await RequireProjectAsync(value.ProjectId);
            if (value.DisputeStatus != QuantitySurveyClaimDisputeStatus.Open) throw Conflict("The claim has no open dispute.");
            if (value.SubmittedById == UserId || value.QsVettedById == UserId) throw Conflict("The claim submitter or QS vetter cannot resolve its dispute.");
            value.DisputeStatus = accepted ? QuantitySurveyClaimDisputeStatus.ResolvedAccepted : QuantitySurveyClaimDisputeStatus.ResolvedRejected;
            value.DisputeResolution = RequiredText(request.Reason, 5, 2000, "Dispute resolution");
        });

    public Task<QuantitySurveyContractClaimDto> SettleAsync(Guid id, SettleQuantitySurveyContractClaimRequest request,
        string correlationId, CancellationToken token = default) => MutateAsync(id, request,
        QuantitySurveyAuditEventMap.SettleContractClaim, correlationId, token, null, async value =>
        {
            await RequireProjectAsync(value.ProjectId);
            if (value.Status != QuantitySurveyContractClaimStatuses.Approved || !value.ApprovedAmount.HasValue || value.ApprovedAmount <= 0)
                throw Conflict("Only an approved positive claim can be settled.");
            if (value.DisputeStatus == QuantitySurveyClaimDisputeStatus.Open) throw Conflict("Resolve the open dispute before settlement.");
            var total = Round(request.Amount);
            if (total < value.SettledAmount || total > value.ApprovedAmount.Value)
                throw Validation("The settlement total cannot decrease or exceed the approved claim amount.");
            if (request.SettlementDate == default || request.SettlementDate > DateTime.UtcNow.AddDays(1)) throw Validation("Select a valid settlement date.");
            value.SettledAmount = total; value.SettlementStatus = QuantitySurveyContractClaimRules.SettlementStatus(value.ApprovedAmount.Value, total);
            value.SettlementReference = RequiredText(request.SettlementReference, 2, 100, "Settlement reference");
            value.SettlementDate = request.SettlementDate.ToUniversalTime();
        });

    public async Task<IReadOnlyList<QuantitySurveyContractClaimRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token); await RequireProjectAsync(entity.ProjectId);
        return await db.QuantitySurveyContractClaimRevisions.AsNoTracking().Where(value => value.TenantId == TenantId && value.ContractClaimId == id)
            .OrderByDescending(value => value.CreatedAt).Select(value => new QuantitySurveyContractClaimRevisionDto
            { Id = value.Id, Action = value.Action, ActorName = value.ActorName, ActorRoles = value.ActorRoles,
                CorrelationId = value.CorrelationId, Reason = value.Reason, BeforeJson = value.BeforeJson, AfterJson = value.AfterJson,
                CreatedAt = value.CreatedAt }).ToListAsync(token);
    }

    private async Task<QuantitySurveyContractClaimDto> MutateAsync(Guid id, QuantitySurveyContractClaimActionRequest request, string action,
        string correlationId, CancellationToken token, Guid? actorPartnerId, Func<QuantitySurveyContractClaim, Task> mutation)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var reason = RequiredText(request.Reason, 5, 2000, "Action reason");
        var requestHash = Hash(new { id, action, reason, request });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var value = await RequiredAsync(id, true, token);
            if (value.LastMutationClientRequestId == request.ClientRequestId)
            { if (!FixedEquals(value.LastMutationRequestHash, requestHash)) throw RetryConflict(); await transaction.CommitAsync(token); return; }
            ApplyRowVersion(value, request.RowVersion);
            if (await db.QuantitySurveyContractClaimRevisions.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.ClientRequestId == request.ClientRequestId, token)) throw RetryConflict();
            var before = Snapshot(value); await mutation(value);
            value.LastMutationClientRequestId = request.ClientRequestId; value.LastMutationRequestHash = requestHash;
            value.CorrelationId = Correlation(correlationId); value.UpdatedAt = DateTime.UtcNow; value.UpdatedBy = UserName; value.LastModifiedById = UserId;
            AddRevision(value, request.ClientRequestId, requestHash, action, reason, before, Snapshot(value), correlationId, actorPartnerId);
            AddAudit(value, action, before, Snapshot(value), correlationId); await SaveChangesAsync(token); await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear(); return await GetAsync(id, actorPartnerId.HasValue, token);
    }

    private async Task ValidateSourcesAsync(Guid projectId, Guid contractId, SaveQuantitySurveyContractClaimRequest request, CancellationToken token)
    {
        if (request.ApprovedBoqVersionId.HasValue && !await db.ProjectBoqVersions.AsNoTracking().AnyAsync(value => value.TenantId == TenantId &&
            value.Id == request.ApprovedBoqVersionId && value.ProjectId == projectId && !value.IsDeleted && value.ApprovalStatus == "Approved" && value.PublishedAt != null, token))
            throw Validation("Select a published Approved BoQ version for this project.");
        if (request.VariationOrderId.HasValue)
        {
            var variation = await db.ProjectVariationOrders.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId &&
                value.Id == request.VariationOrderId && value.ProjectId == projectId && value.ContractId == contractId && value.IsQuantitySurveyGoverned &&
                !value.IsDeleted && value.Status == ProjectVariationOrderStatuses.Approved, token)
                ?? throw Validation("Select an approved governed variation for this Works contract.");
            if (request.ClaimType == QuantitySurveyContractClaimType.Daywork && variation.VariationType != ProjectVariationOrderTypes.Daywork)
                throw Validation("A daywork claim must link to an approved Daywork record.");
            if (request.ClaimType == QuantitySurveyContractClaimType.AdditionalWork && variation.VariationType != ProjectVariationOrderTypes.AdditionalWork)
                throw Validation("An additional-work claim must link to an approved Additional Work record.");
        }
        if (request.ExtensionOfTimeId.HasValue && !await db.ProjectExtensionOfTimeRequests.AsNoTracking().AnyAsync(value => value.TenantId == TenantId &&
            value.Id == request.ExtensionOfTimeId && value.ProjectId == projectId && value.ContractId == contractId && !value.IsDeleted &&
            (value.Status == ProjectExtensionOfTimeStatuses.Submitted || value.Status == ProjectExtensionOfTimeStatuses.UnderReview ||
             value.Status == ProjectExtensionOfTimeStatuses.Approved || value.Status == ProjectExtensionOfTimeStatuses.Implemented), token))
            throw Validation("Select a submitted extension-of-time record for this Works contract.");
    }

    private async Task ValidateFrozenAsync(QuantitySurveyContractClaim value, CancellationToken token)
    {
        var contract = await RequiredContractAsync(value.ProjectId, value.ContractId, value.ContractorBusinessPartnerId, token);
        if (!string.Equals(contract.Currency, value.Currency, StringComparison.OrdinalIgnoreCase)) throw Conflict("The frozen contract currency changed. Prepare a new claim revision.");
        var request = new SaveQuantitySurveyContractClaimRequest { ContractId = value.ContractId, ApprovedBoqVersionId = value.ApprovedBoqVersionId,
            VariationOrderId = value.VariationOrderId, ExtensionOfTimeId = value.ExtensionOfTimeId, ClaimType = value.ClaimType,
            Title = value.Title, Basis = value.Basis, ClaimedAmount = value.ClaimedAmount };
        await ValidateSourcesAsync(value.ProjectId, value.ContractId, request, token);
        var policy = await ResolvePolicyAsync(token);
        if (policy.ProfileId != value.ConfigurationProfileId || policy.DecisionId != value.VariationDecisionId ||
            policy.WorkflowDefinitionId != value.ApprovalWorkflowDefinitionId || policy.EvidenceTemplateId != value.EvidenceMetadataTemplateId ||
            !FixedEquals(policy.PolicyHash, value.PolicyHash)) throw Conflict("The effective claim policy changed. Return to Draft and refresh the claim.");
    }

    private async Task<Policy> ResolvePolicyAsync(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var profiles = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted &&
            value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && value.PublishedAt != null && value.EffectiveFrom <= now &&
            (!value.EffectiveTo.HasValue || value.EffectiveTo >= now)).OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No Published quantity-survey configuration is effective for this date.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one quantity-survey configuration is effective for this date.");
        var profile = profiles[0]; var decision = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId &&
            value.ProfileId == profile.Id && value.DecisionKey == "QS-DEC-011" && !value.IsDeleted, token)
            ?? throw Validation("The effective configuration has no QS-DEC-011 variations-and-claims decision.");
        if (decision.Status != QuantitySurveyConfigurationDecisionStatus.Approved || decision.ApprovalStatus != QuantitySurveyConfigurationApprovalStatus.Approved ||
            decision.EvidenceStatus != QuantitySurveyConfigurationEvidenceStatus.Verified || (decision.EffectiveFrom.HasValue && decision.EffectiveFrom > now) ||
            (decision.EffectiveTo.HasValue && decision.EffectiveTo < now)) throw Validation("QS-DEC-011 is not approved, verified, and effective for this date.");
        QsVariationClaimsValue value; try { value = JsonSerializer.Deserialize<QsVariationClaimsValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("QS-DEC-011 contains invalid variations-and-claims policy data."); }
        if (value.ClaimWorkflowDefinitionId == Guid.Empty || value.VariationEvidenceMetadataTemplateId == Guid.Empty ||
            !value.AllowedTypes.Contains("Claim", StringComparer.OrdinalIgnoreCase))
            throw Validation("QS-DEC-011 must allow Claim and select its workflow and evidence DMS template.");
        var workflowValid = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).AnyAsync(item => item.TenantId == TenantId &&
            item.Id == value.ClaimWorkflowDefinitionId && !item.IsDeleted && item.IsActive && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
            !item.EntityType.IsDeleted && item.EntityType.IsActive && item.EntityType.Code == QuantitySurveyWorkflowBindingRegistry.Claim, token);
        if (!workflowValid) throw Validation("The QS-DEC-011 claim workflow must be active, Published, and bound to QS_CLAIM.");
        var dmsValid = await db.CentralDocumentMetadataTemplates.AsNoTracking().AnyAsync(item => item.TenantId == TenantId &&
            item.Id == value.VariationEvidenceMetadataTemplateId && !item.IsDeleted && item.IsActive && item.PublishedAt != null, token);
        if (!dmsValid) throw Validation("The QS-DEC-011 claim evidence DMS template must be active and Published.");
        return new(profile.Id, decision.Id, value.ClaimWorkflowDefinitionId, value.VariationEvidenceMetadataTemplateId,
            Hash(new { Profile = profile.Id, profile.Version, Decision = decision.Id, decision.ValueJson,
                value.ClaimWorkflowDefinitionId, value.VariationEvidenceMetadataTemplateId }));
    }

    private async Task<ErpSystem.Core.Entities.Procurement.Contract> RequiredContractAsync(Guid projectId, Guid contractId,
        Guid contractorId, CancellationToken token) => await db.Contracts.AsNoTracking().Include(value => value.Tender).ThenInclude(value => value.SourcePurchaseRequisition)
        .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == contractId && value.BusinessPartnerId == contractorId &&
            !value.IsDeleted && value.ContractType == "Works" && value.Status == "Active" && value.Tender.SourcePurchaseRequisition != null &&
            value.Tender.SourcePurchaseRequisition.ProjectId == projectId, token)
        ?? throw Validation("Select an active Procurement Works contract assigned to this contractor and project.");

    private async Task<ExternalActor> ExternalForClaimAsync(Guid id, bool requireApprove, bool requireUpload, CancellationToken token)
    {
        var entity = await RequiredAsync(id, false, token);
        var actor = await RequireExternalActorAsync(entity.ProjectId, requireUpload, requireApprove, entity.Id, token);
        if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId) throw new UnauthorizedAccessException("The claim is not assigned to the linked contractor.");
        return actor;
    }

    private async Task<ExternalActor> RequireExternalActorAsync(Guid projectId, bool requireUpload, bool requireApprove,
        Guid? claimId, CancellationToken token)
    {
        var link = await db.BusinessPartnerUsers.IgnoreQueryFilters().AsNoTracking().Include(value => value.BusinessPartner).Include(value => value.User)
            .FirstOrDefaultAsync(value => value.UserId == UserId && value.IsActive && !value.IsDeleted && !value.BusinessPartner.IsDeleted &&
                value.BusinessPartner.TenantId == TenantId && value.User.TenantId == TenantId && value.User.IsActive, token)
            ?? throw new UnauthorizedAccessException("No active business-partner identity is linked to this portal user.");
        if (!link.BusinessPartner.IsActive || !BusinessPartnerLifecyclePolicy.IsOperationalRegistration(link.BusinessPartner.RegistrationStatus))
            throw new UnauthorizedAccessException("The linked business partner is not active.");
        _ = await projectService.GetExternalProjectByIdAsync(projectId, UserId)
            ?? throw new UnauthorizedAccessException("You do not have access to the selected project.");
        var project = await db.Projects.AsNoTracking().SingleAsync(value => value.TenantId == TenantId && value.Id == projectId, token);
        if (!project.ExternalPortalAccessEnabled || !project.ExternalCollaborationEnabled)
            throw new UnauthorizedAccessException("External collaboration is not enabled for this project.");
        var policies = await db.ProjectExternalAccessPolicies.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId &&
            value.BusinessPartnerId == link.BusinessPartnerId && !value.IsDeleted &&
            (value.ArtifactType == "Project" || (claimId.HasValue && value.ArtifactType == "ContractClaim" && value.ArtifactId == claimId))).ToListAsync(token);
        if (project.BusinessPartnerId != link.BusinessPartnerId && (policies.Count == 0 || (requireUpload && !policies.Any(value => value.CanUpload)) ||
            (requireApprove && !policies.Any(value => value.CanApprove))))
            throw new UnauthorizedAccessException("The project external-access policy does not permit this contractor-claim action.");
        return new(link.BusinessPartnerId, link.BusinessPartner.PartnerName);
    }

    private IQueryable<QuantitySurveyContractClaim> Query(bool tracked = false)
    {
        var query = db.QuantitySurveyContractClaims.Include(value => value.Contract).ThenInclude(value => value.BusinessPartner)
            .Include(value => value.Evidence).Where(value => value.TenantId == TenantId && !value.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }
    private async Task<QuantitySurveyContractClaim> RequiredAsync(Guid id, bool tracked, CancellationToken token)
        => await Query(tracked).SingleOrDefaultAsync(value => value.Id == id, token)
           ?? throw new QuantitySurveyContractClaimNotFoundException("The governed contractor claim was not found.");
    private async Task RequireProjectAsync(Guid projectId)
    { if (!await projectService.HasProjectAccessAsync(projectId)) throw new UnauthorizedAccessException("You are not permitted to access the selected project."); }
    private async Task<string> NextNumberAsync(CancellationToken token)
    { var year = DateTime.UtcNow.Year; var count = await db.QuantitySurveyContractClaims.IgnoreQueryFilters().CountAsync(value => value.TenantId == TenantId && value.CreatedAt.Year == year, token); return $"CLM-{year}-{count + 1:00000}"; }

    private void AddRevision(QuantitySurveyContractClaim value, Guid clientRequestId, string requestHash, string action, string reason,
        object? before, object after, string correlationId, Guid? actorPartnerId) => db.QuantitySurveyContractClaimRevisions.Add(new()
        { Id = Guid.NewGuid(), TenantId = TenantId, ContractClaimId = value.Id, ClientRequestId = clientRequestId, RequestHash = requestHash,
            Action = action, ActorUserId = UserId, ActorBusinessPartnerId = actorPartnerId, ActorName = UserName, ActorRoles = ActorRoles,
            CorrelationId = Correlation(correlationId), Reason = reason, BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private void AddAudit(QuantitySurveyContractClaim value, string action, object? before, object after, string correlationId)
        => db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = nameof(QuantitySurveyContractClaim), ResourceId = value.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
            IpAddress = "api", UserAgent = "QuantitySurvey", Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private async Task SaveChangesAsync(CancellationToken token)
    { try { await db.SaveChangesAsync(token); } catch (DbUpdateConcurrencyException) { throw Conflict("The claim changed. Refresh and retry."); }
      catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("IX_", StringComparison.OrdinalIgnoreCase) == true ||
          exception.InnerException?.Message.Contains("5190", StringComparison.OrdinalIgnoreCase) == true)
      { throw Conflict("The claim conflicts with an existing governed source or lifecycle rule."); } }

    private static QuantitySurveyContractClaimDto Map(QuantitySurveyContractClaim value) => new()
    {
        Id = value.Id, ProjectId = value.ProjectId, ContractId = value.ContractId, ContractorBusinessPartnerId = value.ContractorBusinessPartnerId,
        ApprovedBoqVersionId = value.ApprovedBoqVersionId, VariationOrderId = value.VariationOrderId, ExtensionOfTimeId = value.ExtensionOfTimeId,
        ClaimNumber = value.ClaimNumber, ClaimType = value.ClaimType, Title = value.Title, Basis = value.Basis, Status = value.Status,
        ApprovalStatus = value.ApprovalStatus, ContractNumber = value.Contract?.ContractNumber ?? string.Empty,
        ContractorName = value.Contract?.BusinessPartner?.PartnerName ?? string.Empty, Currency = value.Currency,
        ClaimedAmount = value.ClaimedAmount, QsAssessedAmount = value.QsAssessedAmount, ApprovedAmount = value.ApprovedAmount,
        RejectedAmount = value.RejectedAmount, QsReviewNote = value.QsReviewNote, DisputeStatus = value.DisputeStatus,
        DisputeReason = value.DisputeReason, DisputeResolution = value.DisputeResolution, SettlementStatus = value.SettlementStatus,
        SettledAmount = value.SettledAmount, SettlementReference = value.SettlementReference, SettlementDate = value.SettlementDate,
        WorkflowInstanceId = value.WorkflowInstanceId, RejectionReason = value.RejectionReason,
        RowVersion = Convert.ToBase64String(value.RowVersion), Evidence = value.Evidence.Where(item => !item.IsDeleted).OrderBy(item => item.CreatedAt).Select(MapEvidence).ToList()
    };
    private static QuantitySurveyContractClaimEvidenceDto MapEvidence(QuantitySurveyContractClaimEvidence value) => new()
    { Id = value.Id, Title = value.Title, FileName = value.OriginalFileName, ContentType = value.ContentType, FileSize = value.FileSize,
        ChecksumSha256 = value.ChecksumSha256, CentralDocumentRecordId = value.CentralDocumentRecordId, CentralDocumentVersionId = value.CentralDocumentVersionId };
    private static object Snapshot(QuantitySurveyContractClaim value) => new
    { value.Id, value.ProjectId, value.ContractId, value.ContractorBusinessPartnerId, value.ApprovedBoqVersionId, value.VariationOrderId,
        value.ExtensionOfTimeId, value.ClaimNumber, value.ClaimType, value.Title, value.Basis, value.Status, value.ApprovalStatus,
        value.Currency, value.ClaimedAmount, value.QsAssessedAmount, value.ApprovedAmount, value.RejectedAmount, value.QsReviewNote,
        value.DisputeStatus, value.DisputeReason, value.DisputeResolution, value.SettlementStatus, value.SettledAmount,
        value.SettlementReference, value.SettlementDate, value.ConfigurationProfileId, value.VariationDecisionId,
        value.ApprovalWorkflowDefinitionId, value.EvidenceMetadataTemplateId, value.PolicyHash, value.SubmittedById,
        value.SubmittedBusinessPartnerId, value.SubmittedAt, value.QsVettedById, value.QsVettedAt, value.ApprovedById,
        value.ApprovedAt, value.WorkflowInstanceId, value.RejectionReason };
    private static void ApplyRowVersion(QuantitySurveyContractClaim value, string? encoded)
    { if (string.IsNullOrWhiteSpace(encoded)) throw Validation("A claim row version is required. Refresh and retry."); byte[] expected;
      try { expected = Convert.FromBase64String(encoded); } catch (FormatException) { throw Validation("The claim row version is invalid. Refresh and retry."); }
      if (expected.Length != value.RowVersion.Length || !CryptographicOperations.FixedTimeEquals(expected, value.RowVersion)) throw Conflict("The claim changed. Refresh and retry."); }
    private static string RequiredText(string? value, int min, int max, string label)
        => string.IsNullOrWhiteSpace(value) || value.Trim().Length < min || value.Trim().Length > max
            ? throw Validation($"{label} must contain {min} to {max} characters.") : value.Trim();
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right)
    { if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false; var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right); return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); }
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static QuantitySurveyContractClaimValidationException Validation(string message) => new(message);
    private static QuantitySurveyContractClaimConflictException Conflict(string message) => new(message);
    private static QuantitySurveyContractClaimConflictException RetryConflict() => Conflict("This client request identifier is already bound to different claim inputs.");
    private readonly record struct ExternalActor(Guid BusinessPartnerId, string Name);
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId, Guid EvidenceTemplateId, string PolicyHash);
}
