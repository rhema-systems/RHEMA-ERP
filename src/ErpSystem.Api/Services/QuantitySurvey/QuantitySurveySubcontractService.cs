using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveySubcontractService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters,
    IVendorInvoiceService vendorInvoices,
    ITaxCalculationEngine taxEngine,
    IControlledFileUploadService controlledFiles,
    ICentralDocumentRepositoryFileService centralDocuments) : IQuantitySurveySubcontractService
{
    private const long MaximumEvidenceBytes = 52_428_800;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveySubcontractWorkspaceDto> GetWorkspaceAsync(Guid projectId, bool external,
        CancellationToken token = default)
    {
        ExternalActor? actor = external ? await RequireExternalActorAsync(projectId, false, null, token) : null;
        if (!external) await RequireProjectAsync(projectId);
        var contracts = await ContractQuery(projectId).OrderBy(value => value.ContractNumber).Select(value =>
            new QuantitySurveySubcontractContractLookupDto(value.Id, value.ContractNumber, value.ContractTitle,
                value.ContractValue, value.Currency, value.RetentionPercentage, value.SubcontractPaymentTermId!.Value)).ToListAsync(token);
        var partners = external ? [] : await PartnerQuery().OrderBy(value => value.PartnerName).Select(value =>
            new QuantitySurveySubcontractPartnerLookupDto(value.Id, value.PartnerCode, value.PartnerName)).ToListAsync(token);
        var terms = external ? [] : await db.PaymentTerms.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.IsActive)
            .OrderBy(value => value.Name).Select(value => new QuantitySurveySubcontractPaymentTermLookupDto(value.Id, value.Code, value.Name)).ToListAsync(token);
        var rows = await Query().Where(value => value.ProjectId == projectId &&
            (!actor.HasValue || value.SubcontractorBusinessPartnerId == actor.Value.BusinessPartnerId))
            .OrderByDescending(value => value.CreatedAt).ToListAsync(token);
        return new() { Contracts = contracts, Subcontractors = partners, PaymentTerms = terms,
            Subcontracts = rows.Select(Map).ToList(), ExternalBusinessPartnerId = actor?.BusinessPartnerId };
    }

    public async Task<QuantitySurveySubcontractDto> SaveAsync(Guid projectId,
        SaveQuantitySurveySubcontractRequest request, string correlationId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var title = RequiredText(request.Title, 3, 200, "Title");
        var scope = RequiredText(request.Scope, 10, 4000, "Scope");
        var contract = await ContractQuery(projectId).SingleOrDefaultAsync(value => value.Id == request.ContractId, token)
            ?? throw Validation("Select an active Procurement Works contract for this project with approved subcontract terms.");
        var partner = await PartnerQuery().SingleOrDefaultAsync(value => value.Id == request.SubcontractorBusinessPartnerId, token)
            ?? throw Validation("Select an active approved supplier or contractor.");
        var term = await db.PaymentTerms.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.PaymentTermId && !value.IsDeleted && value.IsActive, token)
            ?? throw Validation("Select an active Finance payment term.");
        if (contract.SubcontractPaymentTermId != term.Id)
            throw Validation("The payment term must match the controlled subcontract term on the parent Works contract.");
        var policy = await ResolvePolicyAsync(token);
        var issues = QuantitySurveySubcontractRules.ValidateContract(contract.ContractValue, request.SubcontractValue,
            request.RetentionPercentage, request.StartDate, request.EndDate, contract.AllowSubcontracting,
            policy.ContractControls.ControlSubcontracts, true, true);
        if (issues.Count > 0) throw Validation(string.Join(" ", issues));
        var requestHash = Hash(new { projectId, request.ContractId, request.SubcontractorBusinessPartnerId,
            request.PaymentTermId, title, scope, request.SubcontractValue, request.RetentionPercentage,
            request.StartDate, request.EndDate, policy.PolicyHash });
        var retry = await db.QuantitySurveySubcontracts.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (!FixedEquals(retry.RequestHash, requestHash) || retry.Id != request.Id.GetValueOrDefault(retry.Id)) throw RetryConflict();
            return Map(await RequiredAsync(retry.Id, false, token));
        }
        var strategy = db.Database.CreateExecutionStrategy();
        Guid id = Guid.Empty;
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            QuantitySurveySubcontract value;
            object? before = null;
            var action = QuantitySurveyAuditEventMap.CreateSubcontract;
            if (request.Id.HasValue)
            {
                value = await RequiredAsync(request.Id.Value, true, token);
                if (value.ProjectId != projectId) throw NotFound("The subcontract was not found in this project.");
                if (value.Status is not (QuantitySurveySubcontractStatuses.Draft or QuantitySurveySubcontractStatuses.Rejected))
                    throw Conflict("Only a Draft or Rejected subcontract can be amended.");
                ApplyRowVersion(value.RowVersion, request.RowVersion, "subcontract");
                before = Snapshot(value); action = QuantitySurveyAuditEventMap.UpdateSubcontract;
                value.LastMutationClientRequestId = request.ClientRequestId;
                value.LastMutationRequestHash = requestHash;
            }
            else
            {
                value = new QuantitySurveySubcontract
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId,
                    ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
                    SubcontractNumber = await NextNumberAsync("SUB", db.QuantitySurveySubcontracts.IgnoreQueryFilters(), token),
                    PreparedById = UserId, PreparedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
                };
                db.QuantitySurveySubcontracts.Add(value);
            }
            value.ContractId = contract.Id; value.SubcontractorBusinessPartnerId = partner.Id; value.PaymentTermId = term.Id;
            value.Title = title; value.Scope = scope; value.SubcontractValue = Round(request.SubcontractValue);
            value.Currency = contract.Currency.ToUpperInvariant(); value.RetentionPercentage = Round(request.RetentionPercentage);
            value.StartDate = request.StartDate.Date; value.EndDate = request.EndDate?.Date;
            value.Status = QuantitySurveySubcontractStatuses.Draft; value.ApprovalStatus = "Draft";
            value.ConfigurationProfileId = policy.ProfileId; value.ContractControlsDecisionId = policy.ContractDecisionId;
            value.ApprovalWorkflowDefinitionId = policy.SubcontractWorkflowId; value.PolicyHash = policy.PolicyHash;
            value.WorkflowInstanceId = null; value.SubmittedById = null; value.SubmittedAt = null; value.ApprovedById = null;
            value.ApprovedAt = null; value.RejectionReason = null; value.ClosedById = null; value.ClosedAt = null; value.ClosureNote = null;
            value.CorrelationId = Correlation(correlationId); value.UpdatedAt = DateTime.UtcNow; value.UpdatedBy = UserName; value.LastModifiedById = UserId;
            AddRevision(value, null, request.ClientRequestId, requestHash, action, before, Snapshot(value), action == QuantitySurveyAuditEventMap.CreateSubcontract ? "Subcontract prepared." : "Subcontract amended.", correlationId, null);
            AddAudit(value.Id, nameof(QuantitySurveySubcontract), action, before, Snapshot(value), correlationId);
            await SaveAsync(token); await transaction.CommitAsync(token); id = value.Id;
        });
        db.ChangeTracker.Clear(); return Map(await RequiredAsync(id, false, token));
    }

    public Task<QuantitySurveySubcontractDto> SubmitSubcontractAsync(Guid id, QuantitySurveySubcontractActionRequest request,
        string correlationId, CancellationToken token = default) => MutateSubcontractAsync(id, request,
        QuantitySurveyAuditEventMap.SubmitSubcontract, correlationId, token, async value =>
        {
            await RequireProjectAsync(value.ProjectId);
            if (value.Status is not (QuantitySurveySubcontractStatuses.Draft or QuantitySurveySubcontractStatuses.Rejected))
                throw Conflict("Only a Draft or Rejected subcontract can be submitted.");
            if (value.Evidence.All(item => item.IsDeleted || item.ValuationId != null))
                throw Conflict("Attach at least one clean central-DMS subcontract agreement before submission.");
            await ValidateFrozenSubcontractAsync(value, token);
            var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.Subcontract, value.Id, value.ApprovalWorkflowDefinitionId);
            if (!result.ExecutionResult.Success || result.Outcome != WorkflowOutcome.Pending)
                throw Conflict(result.ExecutionResult.Message ?? "The subcontract workflow must stop at an independent approval step.");
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Subcontract).ApplySubmitOutcome(value, result, UserId);
            value.Status = QuantitySurveySubcontractStatuses.PendingApproval; value.ApprovalStatus = "Pending";
            value.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId; value.SubmittedById = UserId; value.SubmittedAt = DateTime.UtcNow;
        });

    public Task<QuantitySurveySubcontractDto> DecideSubcontractAsync(Guid id, QuantitySurveySubcontractActionRequest request,
        bool approve, string correlationId, CancellationToken token = default) => MutateSubcontractAsync(id, request,
        approve ? QuantitySurveyAuditEventMap.ApproveSubcontract : QuantitySurveyAuditEventMap.RejectSubcontract,
        correlationId, token, async value =>
        {
            await RequireProjectAsync(value.ProjectId);
            if (value.Status != QuantitySurveySubcontractStatuses.PendingApproval || !value.WorkflowInstanceId.HasValue)
                throw Conflict("The subcontract is not pending workflow approval.");
            if (value.PreparedById == UserId || value.SubmittedById == UserId)
                throw Conflict("The subcontract preparer or submitter cannot decide the same record.");
            await ValidateFrozenSubcontractAsync(value, token);
            var outcome = await ResolveWorkflowOutcomeAsync(QuantitySurveyWorkflowBindingRegistry.Subcontract, value.Id,
                value.WorkflowInstanceId.Value, approve, request.Reason, token);
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Subcontract)
                .ApplyApprovalOutcome(value, outcome, UserId, approve ? null : RequiredText(request.Reason, 5, 2000, "Rejection reason"));
            value.Status = approve ? QuantitySurveySubcontractStatuses.Approved : QuantitySurveySubcontractStatuses.Rejected;
            value.ApprovalStatus = approve ? "Approved" : "Rejected";
        });

    public async Task<QuantitySurveySubcontractValuationDto> SaveValuationAsync(Guid subcontractId,
        SaveQuantitySurveySubcontractValuationRequest request, bool external, string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var subcontract = await RequiredAsync(subcontractId, false, token);
        Guid? actorPartnerId = null;
        if (external)
        {
            var actor = await RequireExternalActorAsync(subcontract.ProjectId, true, subcontract.Id, token);
            if (actor.BusinessPartnerId != subcontract.SubcontractorBusinessPartnerId)
                throw new UnauthorizedAccessException("The subcontract is not assigned to the linked business partner.");
            actorPartnerId = actor.BusinessPartnerId;
        }
        else await RequireProjectAsync(subcontract.ProjectId);
        if (subcontract.Status != QuantitySurveySubcontractStatuses.Approved)
            throw Conflict("Valuations require an approved open subcontract.");
        if (request.ValuationDate == default || request.PeriodEndDate == default || request.PeriodEndDate.Date > request.ValuationDate.Date)
            throw Validation("Select a valuation date on or after the period-end date.");
        if (request.PeriodEndDate.Date < subcontract.StartDate.Date ||
            (subcontract.EndDate.HasValue && request.PeriodEndDate.Date > subcontract.EndDate.Value.Date))
            throw Validation("The valuation period must fall within the approved subcontract dates.");
        var note = Normalize(request.SubmissionNote);
        var policy = await ResolvePolicyAsync(token);
        var requestHash = Hash(new { subcontractId, request.ValuationDate, request.PeriodEndDate,
            request.ClaimedToDateAmount, request.IsFinal, note, policy.PolicyHash });
        var retry = await db.QuantitySurveySubcontractValuations.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId, token);
        if (retry is not null)
        {
            if (retry.SubcontractId != subcontractId || !FixedEquals(retry.RequestHash, requestHash) || retry.Id != request.Id.GetValueOrDefault(retry.Id))
                throw RetryConflict();
            return MapValuation(await RequiredValuationAsync(retry.Id, false, token));
        }
        var previous = await PreviousApprovedValuationAsync(subcontractId, request.Id, token);
        var previousCertified = previous?.AssessedToDateAmount ?? 0m;
        var claimed = Round(request.ClaimedToDateAmount);
        if (claimed <= previousCertified || claimed > subcontract.SubcontractValue)
            throw Validation("Claimed-to-date must exceed the previous approved value and cannot exceed the subcontract value.");
        var strategy = db.Database.CreateExecutionStrategy(); Guid id = Guid.Empty;
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            QuantitySurveySubcontractValuation value; object? before = null;
            var action = QuantitySurveyAuditEventMap.CreateSubcontractValuation;
            if (request.Id.HasValue)
            {
                value = await RequiredValuationAsync(request.Id.Value, true, token);
                if (value.SubcontractId != subcontractId) throw NotFound("The valuation was not found in this subcontract.");
                if (value.Status is not (QuantitySurveySubcontractValuationStatuses.Draft or QuantitySurveySubcontractValuationStatuses.Rejected))
                    throw Conflict("Only a Draft or Rejected valuation can be amended.");
                ApplyRowVersion(value.RowVersion, request.RowVersion, "subcontract valuation"); before = Snapshot(value);
                action = QuantitySurveyAuditEventMap.UpdateSubcontractValuation;
                value.LastMutationClientRequestId = request.ClientRequestId; value.LastMutationRequestHash = requestHash;
            }
            else
            {
                value = new QuantitySurveySubcontractValuation
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, SubcontractId = subcontractId,
                    ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
                    ValuationNumber = await NextNumberAsync("SUBVAL", db.QuantitySurveySubcontractValuations.IgnoreQueryFilters(), token),
                    CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
                };
                db.QuantitySurveySubcontractValuations.Add(value);
            }
            value.ValuationDate = request.ValuationDate.Date; value.PeriodEndDate = request.PeriodEndDate.Date;
            value.ClaimedToDateAmount = claimed; value.AssessedToDateAmount = null;
            value.PreviouslyCertifiedAmount = previousCertified; value.CurrentCertifiedAmount = 0;
            value.RetentionHeldAmount = 0; value.RetentionReleasedAmount = 0;
            value.ApprovedBackChargeAmount = 0; value.ApprovedContraChargeAmount = 0; value.TaxAmount = 0; value.NetCertifiedAmount = 0;
            value.IsFinal = request.IsFinal; value.Status = QuantitySurveySubcontractValuationStatuses.Draft; value.ApprovalStatus = "Draft";
            value.SubmissionNote = note; value.AssessmentNote = null;
            value.ConfigurationProfileId = policy.ProfileId; value.ValuationDecisionId = policy.ValuationDecisionId;
            value.ApprovalWorkflowDefinitionId = policy.CertificateWorkflowId; value.EvidenceMetadataTemplateId = policy.EvidenceMetadataTemplateId;
            value.PolicyHash = policy.PolicyHash; value.WorkflowInstanceId = null; value.SubmittedById = null;
            value.SubmittedBusinessPartnerId = actorPartnerId; value.SubmittedAt = null; value.AssessedById = null; value.AssessedAt = null;
            value.ApprovedById = null; value.ApprovedAt = null; value.RejectionReason = null; value.PaymentCertificateId = null;
            value.CorrelationId = Correlation(correlationId); value.UpdatedAt = DateTime.UtcNow; value.UpdatedBy = UserName; value.LastModifiedById = UserId;
            AddRevision(subcontract, value, request.ClientRequestId, requestHash, action, before, Snapshot(value),
                action == QuantitySurveyAuditEventMap.CreateSubcontractValuation ? "Valuation prepared." : "Valuation amended.", correlationId, actorPartnerId);
            AddAudit(value.Id, nameof(QuantitySurveySubcontractValuation), action, before, Snapshot(value), correlationId);
            await SaveAsync(token); await transaction.CommitAsync(token); id = value.Id;
        });
        db.ChangeTracker.Clear(); return MapValuation(await RequiredValuationAsync(id, false, token));
    }

    public async Task<QuantitySurveySubcontractEvidenceDto> UploadEvidenceAsync(Guid subcontractId, Guid? valuationId,
        Guid clientRequestId, string title, string fileName, string contentType, long fileSize, Func<Stream> openRead,
        bool external, string correlationId, CancellationToken token = default)
    {
        var subcontract = await RequiredAsync(subcontractId, false, token); Guid? actorPartnerId = null;
        if (external)
        {
            var actor = await RequireExternalActorAsync(subcontract.ProjectId, true, subcontract.Id, token);
            if (actor.BusinessPartnerId != subcontract.SubcontractorBusinessPartnerId)
                throw new UnauthorizedAccessException("The subcontract is not assigned to the linked business partner.");
            actorPartnerId = actor.BusinessPartnerId;
        }
        else await RequireProjectAsync(subcontract.ProjectId);
        QuantitySurveySubcontractValuation? valuation = null;
        if (valuationId.HasValue)
        {
            valuation = await RequiredValuationAsync(valuationId.Value, false, token);
            if (valuation.SubcontractId != subcontractId) throw NotFound("The valuation was not found in this subcontract.");
            if (valuation.Status is not (QuantitySurveySubcontractValuationStatuses.Draft or QuantitySurveySubcontractValuationStatuses.Rejected))
                throw Conflict("Evidence can be added only to a Draft or Rejected valuation.");
        }
        else if (external || subcontract.Status is not (QuantitySurveySubcontractStatuses.Draft or QuantitySurveySubcontractStatuses.Rejected))
            throw Conflict("Agreement evidence can be added only to a Draft or Rejected subcontract by an internal user.");
        if (clientRequestId == Guid.Empty || fileSize <= 0 || fileSize > MaximumEvidenceBytes)
            throw Validation("Select an evidence file up to 50 MB.");
        var safeTitle = RequiredText(title, 3, 200, "Evidence title"); var safeName = Path.GetFileName(fileName);
        await using var source = openRead(); using var memory = new MemoryStream(); await source.CopyToAsync(memory, token);
        var bytes = memory.ToArray(); if (bytes.LongLength != fileSize) throw Validation("The evidence file size changed during upload.");
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var requestHash = Hash(new { subcontractId, valuationId, safeTitle, safeName, contentType, fileSize, checksum });
        var retry = await db.QuantitySurveySubcontractEvidence.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ClientRequestId == clientRequestId, token);
        if (retry is not null)
        {
            if (retry.SubcontractId != subcontractId || retry.ValuationId != valuationId || !FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
            return MapEvidence(retry);
        }
        var metadataId = valuation?.EvidenceMetadataTemplateId ?? (await ResolvePolicyAsync(token)).EvidenceMetadataTemplateId;
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == metadataId && !value.IsDeleted && value.IsActive && value.PublishedAt != null, token)
            ?? throw Conflict("The governed subcontract evidence DMS template is unavailable.");
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
                SourceModule = "QuantitySurvey", SourceLabel = "Quantity Survey subcontract evidence",
                SourceEntityType = nameof(QuantitySurveySubcontractEvidence), SourceRecordId = id,
                SourceRecordReference = valuation?.ValuationNumber ?? subcontract.SubcontractNumber,
                Title = safeTitle, DocumentType = valuationId.HasValue ? "SubcontractValuationEvidence" : "SubcontractAgreement",
                MetadataTemplateCode = template.TemplateCode, AccessProfile = template.AccessProfile, VersionStatus = "Submitted",
                ChangeSummary = "Clean scanned subcontract evidence retained in the central DMS.", RequirePublishedGovernance = true,
                MetadataValues = [new("subcontractId", "Subcontract ID", subcontractId.ToString(), "guid"),
                    new("valuationId", "Valuation ID", valuationId?.ToString(), "guid"),
                    new("checksumSha256", "Checksum SHA-256", checksum)]
            }, token);
        }
        catch { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw; }
        var evidence = new QuantitySurveySubcontractEvidence
        {
            Id = id, TenantId = TenantId, SubcontractId = subcontractId, ValuationId = valuationId,
            ClientRequestId = clientRequestId, RequestHash = requestHash, EvidenceType = valuationId.HasValue ? "Valuation" : "Agreement",
            Title = safeTitle, OriginalFileName = upload.Record.OriginalFileName, ContentType = contentType,
            FileSize = bytes.LongLength, ChecksumSha256 = checksum, FileUploadRecordId = document.FileUploadRecordId,
            CentralDocumentRecordId = document.DocumentRecordId, CentralDocumentVersionId = document.DocumentVersionId,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        };
        db.QuantitySurveySubcontractEvidence.Add(evidence);
        AddAudit(valuationId ?? subcontractId, valuationId.HasValue ? nameof(QuantitySurveySubcontractValuation) : nameof(QuantitySurveySubcontract),
            QuantitySurveyAuditEventMap.AttachSubcontractEvidence, null,
            new { evidence.Id, evidence.EvidenceType, evidence.Title, evidence.ChecksumSha256, actorPartnerId }, correlationId);
        try { await SaveAsync(token); }
        catch { await centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, token); throw; }
        return MapEvidence(evidence);
    }

    public async Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(Guid subcontractId, Guid evidenceId,
        bool external, CancellationToken token = default)
    {
        var subcontract = await RequiredAsync(subcontractId, false, token);
        if (external)
        {
            var actor = await RequireExternalActorAsync(subcontract.ProjectId, false, subcontract.Id, token);
            if (actor.BusinessPartnerId != subcontract.SubcontractorBusinessPartnerId)
                throw new UnauthorizedAccessException("The subcontract is not assigned to the linked business partner.");
        }
        else await RequireProjectAsync(subcontract.ProjectId);
        var evidence = subcontract.Evidence.SingleOrDefault(value => value.Id == evidenceId && !value.IsDeleted)
            ?? throw NotFound("The subcontract evidence was not found.");
        return await centralDocuments.OpenAsync(TenantId, evidence.CentralDocumentRecordId, evidence.CentralDocumentVersionId, token)
               ?? throw Conflict("The central-DMS evidence content is unavailable.");
    }

    public async Task<QuantitySurveySubcontractValuationDto> SubmitValuationAsync(Guid id,
        QuantitySurveySubcontractActionRequest request, bool external, string correlationId,
        CancellationToken token = default)
    {
        var current = await RequiredValuationAsync(id, false, token); Guid? actorPartnerId = null;
        if (external)
        {
            var actor = await RequireExternalActorAsync(current.Subcontract.ProjectId, true, current.SubcontractId, token);
            if (actor.BusinessPartnerId != current.Subcontract.SubcontractorBusinessPartnerId)
                throw new UnauthorizedAccessException("The valuation is not assigned to the linked business partner.");
            actorPartnerId = actor.BusinessPartnerId;
        }
        else await RequireProjectAsync(current.Subcontract.ProjectId);
        return await MutateValuationAsync(id, request, QuantitySurveyAuditEventMap.SubmitSubcontractValuation,
            correlationId, token, actorPartnerId, async value =>
            {
                if (value.Status is not (QuantitySurveySubcontractValuationStatuses.Draft or QuantitySurveySubcontractValuationStatuses.Rejected))
                    throw Conflict("Only a Draft or Rejected valuation can be submitted.");
                if (value.Evidence.All(item => item.IsDeleted))
                    throw Conflict("Attach at least one clean central-DMS valuation evidence file before submission.");
                await ValidateFrozenValuationAsync(value, token);
                value.Status = QuantitySurveySubcontractValuationStatuses.Submitted; value.ApprovalStatus = "Draft";
                value.SubmittedById = UserId; value.SubmittedBusinessPartnerId = actorPartnerId; value.SubmittedAt = DateTime.UtcNow;
                value.AssessedById = null; value.AssessedAt = null; value.AssessedToDateAmount = null; value.AssessmentNote = null;
            });
    }

    public Task<QuantitySurveySubcontractValuationDto> AssessValuationAsync(Guid id,
        AssessQuantitySurveySubcontractValuationRequest request, string correlationId,
        CancellationToken token = default) => MutateValuationAsync(id, request,
        QuantitySurveyAuditEventMap.AssessSubcontractValuation, correlationId, token, null, async value =>
        {
            await RequireProjectAsync(value.Subcontract.ProjectId);
            if (value.Status != QuantitySurveySubcontractValuationStatuses.Submitted)
                throw Conflict("Only a submitted subcontractor valuation can be assessed.");
            if (value.SubmittedById == UserId) throw Conflict("The valuation submitter cannot assess the same record.");
            var policy = await ValidateFrozenValuationAsync(value, token);
            var previous = await PreviousApprovedValuationAsync(value.SubcontractId, value.Id, token);
            var previousCertified = previous?.AssessedToDateAmount ?? 0m;
            if (previousCertified != value.PreviouslyCertifiedAmount)
                throw Conflict("The previous approved subcontract certificate changed. Refresh this valuation before assessment.");
            var chargeIds = (request.ChargeNoticeIds ?? [])
                .Where(chargeId => chargeId != Guid.Empty)
                .Distinct()
                .ToArray();
            if (chargeIds.Length != (request.ChargeNoticeIds?.Count ?? 0))
                throw Validation("Select each approved charge notice only once.");
            var charges = chargeIds.Length == 0
                ? []
                : await db.QuantitySurveySubcontractChargeNotices.Where(charge =>
                    charge.TenantId == TenantId && !charge.IsDeleted &&
                    charge.SubcontractId == value.SubcontractId && chargeIds.Contains(charge.Id)).ToListAsync(token);
            if (charges.Count != chargeIds.Length)
                throw Validation("One or more selected charge notices do not belong to this subcontract.");
            if (charges.Any(charge => charge.Status != QuantitySurveySubcontractChargeStatuses.Approved ||
                                      charge.AppliedValuationId.HasValue || !charge.ApprovedAmount.HasValue))
                throw Conflict("Only independently approved, unapplied charge notices can be allocated to a valuation.");
            if ((!policy.ContractControls.ControlBackCharges && charges.Any(charge => charge.ChargeType == QuantitySurveySubcontractChargeTypes.BackCharge)) ||
                (!policy.ContractControls.ControlContraCharges && charges.Any(charge => charge.ChargeType == QuantitySurveySubcontractChargeTypes.ContraCharge)))
                throw Conflict("The effective contract controls no longer permit one or more selected charge types.");
            var chargeAmounts = QuantitySurveySubcontractChargeRules.SumApproved(charges.Select(charge =>
                (charge.ChargeType, charge.ApprovedAmount!.Value)));
            var backCharge = chargeAmounts.BackCharge;
            var contraCharge = chargeAmounts.ContraCharge;
            var assessed = Round(request.AssessedToDateAmount);
            var preTax = Round(assessed - previousCertified -
                Round((assessed - previousCertified) * value.Subcontract.RetentionPercentage / 100m) +
                Round(request.RetentionReleasedAmount) - backCharge - contraCharge);
            if (preTax < 0) throw Validation("Retention and approved charges cannot exceed the current certified value.");
            var taxResult = await taxEngine.CalculateTaxesAsync(new TaxCalculationRequestDto
            {
                BaseAmount = preTax, TaxGroupId = policy.Valuation.CertificateTaxGroupId,
                TransactionDate = value.ValuationDate, TransactionType = TaxTransactionType.PurchaseOfServices
            }, token);
            var tax = taxResult.TotalTaxAmount;
            if (policy.Valuation.TaxHandling == QuantitySurveyTaxHandling.Inclusive && tax > 0m && preTax > 0m)
            { var rate = tax / preTax; tax = Round(preTax * rate / (1m + rate)); }
            QuantitySurveySubcontractValuationAmounts amounts;
            try
            {
                amounts = QuantitySurveySubcontractRules.Calculate(value.Subcontract.SubcontractValue,
                    value.ClaimedToDateAmount, assessed, previousCertified, value.Subcontract.RetentionPercentage,
                    request.RetentionReleasedAmount, backCharge, contraCharge, tax,
                    policy.Valuation.TaxHandling == QuantitySurveyTaxHandling.Inclusive);
            }
            catch (ArgumentOutOfRangeException) { throw Validation("The assessed, retention, charge, or tax amounts do not reconcile."); }
            var assessmentNote = RequiredText(request.Reason, 5, 2000, "Assessment note");
            var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.PaymentCertificate, value.Id, value.ApprovalWorkflowDefinitionId);
            if (!result.ExecutionResult.Success || result.Outcome != WorkflowOutcome.Pending)
                throw Conflict(result.ExecutionResult.Message ?? "The subcontract certificate workflow must stop at an independent approval step.");
            foreach (var charge in charges)
            {
                var chargeBefore = SnapshotCharge(charge);
                charge.Status = QuantitySurveySubcontractChargeStatuses.Allocated;
                charge.AppliedValuationId = value.Id;
                charge.AllocatedAt = DateTime.UtcNow;
                charge.AppliedAt = null;
                charge.LastMutationClientRequestId = request.ClientRequestId;
                charge.LastMutationRequestHash = Hash(new { ValuationId = value.Id, ChargeId = charge.Id, Action = QuantitySurveyAuditEventMap.AllocateSubcontractCharge });
                charge.CorrelationId = Correlation(correlationId);
                charge.UpdatedAt = DateTime.UtcNow;
                charge.UpdatedBy = UserName;
                charge.LastModifiedById = UserId;
                AddChargeRevision(charge, request.ClientRequestId, charge.LastMutationRequestHash,
                    QuantitySurveyAuditEventMap.AllocateSubcontractCharge, chargeBefore, SnapshotCharge(charge),
                    request.Reason, correlationId);
                AddAudit(charge.Id, nameof(QuantitySurveySubcontractChargeNotice),
                    QuantitySurveyAuditEventMap.AllocateSubcontractCharge, chargeBefore, SnapshotCharge(charge), correlationId);
            }
            // Persist the charge allocation first inside the same serializable transaction. The
            // valuation SQL gate can then reconcile the financial deduction to concrete notices.
            if (charges.Count > 0) await SaveAsync(token);
            value.AssessedToDateAmount = assessed; value.PreviouslyCertifiedAmount = amounts.PreviouslyCertified;
            value.CurrentCertifiedAmount = amounts.CurrentCertified; value.RetentionHeldAmount = amounts.RetentionHeld;
            value.RetentionReleasedAmount = amounts.RetentionReleased; value.ApprovedBackChargeAmount = amounts.BackCharge;
            value.ApprovedContraChargeAmount = amounts.ContraCharge; value.TaxAmount = amounts.Tax; value.NetCertifiedAmount = amounts.Net;
            value.AssessmentNote = assessmentNote; value.AssessedById = UserId; value.AssessedAt = DateTime.UtcNow;
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.PaymentCertificate).ApplySubmitOutcome(value, result, UserId);
            value.Status = QuantitySurveySubcontractValuationStatuses.PendingApproval; value.ApprovalStatus = "Pending";
            value.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
        });

    public Task<QuantitySurveySubcontractValuationDto> DecideValuationAsync(Guid id,
        QuantitySurveySubcontractActionRequest request, bool approve, string correlationId,
        CancellationToken token = default) => MutateValuationAsync(id, request,
        approve ? QuantitySurveyAuditEventMap.ApproveSubcontractValuation : QuantitySurveyAuditEventMap.RejectSubcontractValuation,
        correlationId, token, null, async value =>
        {
            await RequireProjectAsync(value.Subcontract.ProjectId);
            if (value.Status != QuantitySurveySubcontractValuationStatuses.PendingApproval || !value.WorkflowInstanceId.HasValue)
                throw Conflict("The subcontract valuation is not pending workflow approval.");
            if (value.SubmittedById == UserId || value.AssessedById == UserId)
                throw Conflict("The valuation submitter or QS assessor cannot decide the same record.");
            var policy = await ValidateFrozenValuationAsync(value, token);
            var outcome = await ResolveWorkflowOutcomeAsync(QuantitySurveyWorkflowBindingRegistry.PaymentCertificate,
                value.Id, value.WorkflowInstanceId.Value, approve, request.Reason, token);
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.PaymentCertificate)
                .ApplyApprovalOutcome(value, outcome, UserId, approve ? null : RequiredText(request.Reason, 5, 2000, "Rejection reason"));
            value.Status = approve ? QuantitySurveySubcontractValuationStatuses.Approved : QuantitySurveySubcontractValuationStatuses.Rejected;
            value.ApprovalStatus = approve ? "Approved" : "Rejected";
            var allocatedCharges = await db.QuantitySurveySubcontractChargeNotices.Where(charge =>
                charge.TenantId == TenantId && !charge.IsDeleted && charge.SubcontractId == value.SubcontractId &&
                charge.AppliedValuationId == value.Id && charge.Status == QuantitySurveySubcontractChargeStatuses.Allocated).ToListAsync(token);
            var allocatedAmounts = QuantitySurveySubcontractChargeRules.SumApproved(allocatedCharges.Select(charge =>
                (charge.ChargeType, charge.ApprovedAmount ?? throw Conflict("An allocated charge has no approved amount."))));
            if (allocatedAmounts.BackCharge != value.ApprovedBackChargeAmount || allocatedAmounts.ContraCharge != value.ApprovedContraChargeAmount)
                throw Conflict("The valuation charge totals no longer reconcile to their approved charge notices.");
            if (!approve)
            {
                foreach (var charge in allocatedCharges)
                {
                    var chargeBefore = SnapshotCharge(charge);
                    charge.Status = QuantitySurveySubcontractChargeStatuses.Approved;
                    charge.AppliedValuationId = null;
                    charge.AllocatedAt = null;
                    charge.AppliedAt = null;
                    charge.LastMutationClientRequestId = request.ClientRequestId;
                    charge.LastMutationRequestHash = Hash(new { ValuationId = value.Id, ChargeId = charge.Id, Action = QuantitySurveyAuditEventMap.ReleaseSubcontractChargeAllocation });
                    charge.CorrelationId = Correlation(correlationId);
                    charge.UpdatedAt = DateTime.UtcNow;
                    charge.UpdatedBy = UserName;
                    charge.LastModifiedById = UserId;
                    AddChargeRevision(charge, request.ClientRequestId, charge.LastMutationRequestHash,
                        QuantitySurveyAuditEventMap.ReleaseSubcontractChargeAllocation, chargeBefore, SnapshotCharge(charge),
                        request.Reason, correlationId);
                    AddAudit(charge.Id, nameof(QuantitySurveySubcontractChargeNotice),
                        QuantitySurveyAuditEventMap.ReleaseSubcontractChargeAllocation, chargeBefore, SnapshotCharge(charge), correlationId);
                }
                return;
            }
            if (!value.AssessedToDateAmount.HasValue) throw Conflict("The approved valuation has no QS assessment.");
            if (value.PaymentCertificateId.HasValue) return;
            var certificate = new ProjectPaymentCertificate
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = value.Subcontract.ProjectId,
                ContractId = value.Subcontract.ContractId, QuantitySurveySubcontractValuationId = value.Id,
                SubcontractorBusinessPartnerId = value.Subcontract.SubcontractorBusinessPartnerId,
                ClientRequestId = Guid.NewGuid(), CertificateNumber = await NextCertificateNumberAsync(token),
                Title = $"{value.Subcontract.SubcontractNumber} · {value.ValuationNumber}", Status = ProjectPaymentCertificateStatuses.Approved,
                ApprovalStatus = "Approved", IssueDate = DateTime.UtcNow.Date, Currency = value.Subcontract.Currency,
                CertifiedToDateAmount = value.AssessedToDateAmount.Value, PreviouslyCertifiedAmount = value.PreviouslyCertifiedAmount,
                GrossCertifiedAmount = value.CurrentCertifiedAmount, RetentionHeldAmount = value.RetentionHeldAmount,
                RetentionReleasedAmount = value.RetentionReleasedAmount, OtherDeductionsAmount = value.ApprovedBackChargeAmount + value.ApprovedContraChargeAmount,
                TaxAmount = value.TaxAmount, NetCertifiedAmount = value.NetCertifiedAmount, Notes = value.AssessmentNote,
                ConfigurationProfileId = policy.ProfileId, ValuationDecisionId = policy.ValuationDecisionId,
                ApprovalWorkflowDefinitionId = policy.CertificateWorkflowId, WorkflowInstanceId = value.WorkflowInstanceId,
                CertificateTemplateId = policy.Valuation.CertificateTemplateId,
                CertificateTemplateVersionSnapshot = policy.CertificateTemplateVersion,
                CertificateMetadataTemplateId = policy.Valuation.CertificateMetadataTemplateId,
                CertificateMetadataTemplateCodeSnapshot = policy.CertificateMetadataTemplateCode,
                PolicyHash = policy.PolicyHash, ExpenseAccountId = policy.Valuation.CertificateExpenseAccountId,
                AccountsPayableAccountId = policy.Valuation.CertificateAccountsPayableAccountId,
                PaymentTermId = value.Subcontract.PaymentTermId, TaxGroupId = policy.Valuation.CertificateTaxGroupId,
                WithholdingTaxId = policy.Valuation.CertificateWithholdingTaxId, TaxHandling = policy.Valuation.TaxHandling.ToString(),
                RetentionApplied = value.Subcontract.RetentionPercentage > 0, PreparedById = value.AssessedById,
                PreparedAt = value.AssessedAt ?? DateTime.UtcNow, SubmittedById = value.SubmittedById,
                SubmittedAt = value.SubmittedAt, ApprovedById = UserId, ApprovedAt = DateTime.UtcNow,
                ApHandoffStatus = ProjectPaymentCertificateApHandoffStatuses.Ready, PaymentStatusSnapshot = "NotInvoiced",
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId,
                UpdatedAt = DateTime.UtcNow, UpdatedBy = UserName, LastModifiedById = UserId
            };
            certificate.RequestHash = Hash(new { value.Id, certificate.CertificateNumber, certificate.NetCertifiedAmount,
                certificate.PolicyHash, certificate.SubcontractorBusinessPartnerId });
            db.Set<ProjectPaymentCertificate>().Add(certificate);
            // The valuation already exists, so EF can insert the certificate before updating this source link.
            value.PaymentCertificateId = certificate.Id;
            foreach (var charge in allocatedCharges)
            {
                var chargeBefore = SnapshotCharge(charge);
                charge.Status = QuantitySurveySubcontractChargeStatuses.Applied;
                charge.AppliedAt = DateTime.UtcNow;
                charge.LastMutationClientRequestId = request.ClientRequestId;
                charge.LastMutationRequestHash = Hash(new { ValuationId = value.Id, CertificateId = certificate.Id, ChargeId = charge.Id, Action = QuantitySurveyAuditEventMap.ApplySubcontractCharge });
                charge.CorrelationId = Correlation(correlationId);
                charge.UpdatedAt = DateTime.UtcNow;
                charge.UpdatedBy = UserName;
                charge.LastModifiedById = UserId;
                AddChargeRevision(charge, request.ClientRequestId, charge.LastMutationRequestHash,
                    QuantitySurveyAuditEventMap.ApplySubcontractCharge, chargeBefore, SnapshotCharge(charge),
                    request.Reason, correlationId);
                AddAudit(charge.Id, nameof(QuantitySurveySubcontractChargeNotice),
                    QuantitySurveyAuditEventMap.ApplySubcontractCharge, chargeBefore, SnapshotCharge(charge), correlationId);
            }
        });

    public async Task<QuantitySurveySubcontractValuationDto> HandoffToApAsync(Guid id,
        QuantitySurveySubcontractActionRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var reason = RequiredText(request.Reason, 5, 2000, "AP handoff reason"); var hash = Hash(new { id, reason, Action = "HandoffToAp" });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var value = await RequiredValuationAsync(id, true, token); await RequireProjectAsync(value.Subcontract.ProjectId);
            if (value.LastMutationClientRequestId == request.ClientRequestId)
            {
                if (!FixedEquals(value.LastMutationRequestHash, hash)) throw RetryConflict();
                await transaction.CommitAsync(token); return;
            }
            ApplyRowVersion(value.RowVersion, request.RowVersion, "subcontract valuation");
            if (await db.QuantitySurveySubcontractRevisions.AsNoTracking().AnyAsync(item =>
                    item.TenantId == TenantId && item.ClientRequestId == request.ClientRequestId, token))
                throw RetryConflict();
            if (value.Status is not (QuantitySurveySubcontractValuationStatuses.Approved or QuantitySurveySubcontractValuationStatuses.Paid) || !value.PaymentCertificateId.HasValue)
                throw Conflict("Only an approved subcontract certificate can be handed to Finance AP.");
            var certificate = await db.Set<ProjectPaymentCertificate>().SingleAsync(item => item.TenantId == TenantId && item.Id == value.PaymentCertificateId && !item.IsDeleted, token);
            if (certificate.VendorInvoiceId.HasValue) { await transaction.CommitAsync(token); return; }
            await ValidateFrozenValuationAsync(value, token);
            var before = Snapshot(value);
            try
            {
                var taxableBase = Round(certificate.NetCertifiedAmount - certificate.TaxAmount);
                if (taxableBase < 0) throw Conflict("The certificate tax exceeds the payable amount.");
                var invoice = await vendorInvoices.CreateAsync(new VendorInvoiceCreateDto
                {
                    BusinessPartnerId = value.Subcontract.SubcontractorBusinessPartnerId,
                    SupplierInvoiceNumber = certificate.CertificateNumber, InvoiceDate = certificate.IssueDate,
                    ReceivedDate = DateTime.UtcNow, CurrencyCode = certificate.Currency, ExchangeRate = 1m,
                    PaymentTermId = certificate.PaymentTermId, WithholdingTaxId = certificate.WithholdingTaxId,
                    MatchingType = InvoiceMatchingType.None, ExpenseAccountId = certificate.ExpenseAccountId,
                    AcceptedSupplyKind = ProcurementAcceptedSupplyKind.WorksPaymentCertificate,
                    AcceptedSupplySourceId = certificate.Id,
                    IsTrustedAcceptedSupplyHandoff = true,
                    ApAccountId = certificate.AccountsPayableAccountId,
                    Notes = $"Generated from approved QS subcontract certificate {certificate.CertificateNumber}. {reason}",
                    Reference = $"QS-SUBCERT:{certificate.Id:N}",
                    LineItems = [new VendorInvoiceLineItemCreateDto
                    {
                        LineItemType = "Service", GLAccountId = certificate.ExpenseAccountId,
                        Description = certificate.Title, Quantity = 1m, UnitPrice = taxableBase,
                        TaxGroupId = certificate.TaxGroupId, TaxTreatment = TaxTreatment.Standard, Unit = "Certificate"
                    }]
                }, token);
                if (Round(invoice.TotalAmount) != Round(certificate.NetCertifiedAmount))
                    throw Conflict("Finance AP recalculated a different invoice total. Review the frozen tax setup before retrying.");
                certificate.VendorInvoiceId = invoice.Id; certificate.ApHandoffStatus = ProjectPaymentCertificateApHandoffStatuses.Created;
                certificate.ApHandoffAt = DateTime.UtcNow; certificate.PaymentStatusSnapshot = invoice.Status.ToString();
                certificate.PaymentStatusUpdatedAt = DateTime.UtcNow; certificate.UpdatedAt = DateTime.UtcNow;
                certificate.UpdatedBy = UserName; certificate.LastModifiedById = UserId;
                value.LastMutationClientRequestId = request.ClientRequestId; value.LastMutationRequestHash = hash;
                AddRevision(value.Subcontract, value, request.ClientRequestId, hash,
                    QuantitySurveyAuditEventMap.HandoffSubcontractCertificateToAp, before, Snapshot(value), reason, correlationId, null);
                AddAudit(value.Id, nameof(QuantitySurveySubcontractValuation), QuantitySurveyAuditEventMap.HandoffSubcontractCertificateToAp,
                    before, new { Valuation = Snapshot(value), certificate.VendorInvoiceId, certificate.PaymentStatusSnapshot }, correlationId);
                await SaveAsync(token); await transaction.CommitAsync(token);
            }
            catch (QuantitySurveySubcontractException) { throw; }
            catch (InvalidOperationException exception) { throw Conflict(exception.Message); }
        });
        db.ChangeTracker.Clear(); return MapValuation(await RequiredValuationAsync(id, false, token));
    }

    public async Task<QuantitySurveySubcontractValuationDto> RefreshPaymentAsync(Guid id, string correlationId,
        CancellationToken token = default)
    {
        var value = await RequiredValuationAsync(id, true, token); await RequireProjectAsync(value.Subcontract.ProjectId);
        if (!value.PaymentCertificateId.HasValue) throw Conflict("The subcontract valuation has no approved certificate.");
        var certificate = await db.Set<ProjectPaymentCertificate>().Include(item => item.VendorInvoice).SingleAsync(item =>
            item.TenantId == TenantId && item.Id == value.PaymentCertificateId && !item.IsDeleted, token);
        if (!certificate.VendorInvoiceId.HasValue) throw Conflict("The subcontract certificate has not been handed to Finance AP.");
        var invoice = await vendorInvoices.GetByIdAsync(certificate.VendorInvoiceId.Value, token)
            ?? throw Conflict("The linked Finance AP invoice is unavailable.");
        var before = Snapshot(value); certificate.PaymentStatusSnapshot = invoice.Status.ToString(); certificate.PaymentStatusUpdatedAt = DateTime.UtcNow;
        if (invoice.Status == VendorInvoiceStatus.Paid) value.Status = QuantitySurveySubcontractValuationStatuses.Paid;
        AddAudit(value.Id, nameof(QuantitySurveySubcontractValuation), QuantitySurveyAuditEventMap.RefreshSubcontractPaymentStatus,
            before, new { Valuation = Snapshot(value), certificate.PaymentStatusSnapshot }, correlationId);
        await SaveAsync(token); return MapValuation(await RequiredValuationAsync(id, false, token));
    }

    public Task<QuantitySurveySubcontractDto> CloseAsync(Guid id, QuantitySurveySubcontractActionRequest request,
        string correlationId, CancellationToken token = default) => MutateSubcontractAsync(id, request,
        QuantitySurveyAuditEventMap.CloseSubcontractFinalAccount, correlationId, token, async value =>
        {
            await RequireProjectAsync(value.ProjectId);
            if (value.Status != QuantitySurveySubcontractStatuses.Approved) throw Conflict("Only an approved open subcontract can be closed.");
            var approved = value.Valuations.Where(item => !item.IsDeleted && item.Status is
                (QuantitySurveySubcontractValuationStatuses.Approved or QuantitySurveySubcontractValuationStatuses.Paid)).ToList();
            var final = approved.OrderByDescending(item => item.ValuationDate).FirstOrDefault(item => item.IsFinal)
                ?? throw Conflict("Approve a final subcontract valuation before final settlement.");
            if (final.AssessedToDateAmount != value.SubcontractValue)
                throw Conflict("The final assessed-to-date amount must equal the approved subcontract value.");
            if (approved.Any(item => item.Status != QuantitySurveySubcontractValuationStatuses.Paid))
                throw Conflict("Every approved subcontract certificate must be fully paid in Finance before final settlement.");
            var retentionBalance = Round(approved.Sum(item => item.RetentionHeldAmount) - approved.Sum(item => item.RetentionReleasedAmount));
            if (retentionBalance != 0) throw Conflict("Release the full subcontract retention balance before final settlement.");
            value.Status = QuantitySurveySubcontractStatuses.Closed; value.ClosedById = UserId; value.ClosedAt = DateTime.UtcNow;
            value.ClosureNote = RequiredText(request.Reason, 5, 2000, "Final settlement note");
        });

    public async Task<IReadOnlyList<QuantitySurveySubcontractRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default)
    {
        var value = await RequiredAsync(id, false, token); await RequireProjectAsync(value.ProjectId);
        return await db.QuantitySurveySubcontractRevisions.AsNoTracking().Where(item => item.TenantId == TenantId && item.SubcontractId == id)
            .OrderByDescending(item => item.CreatedAt).Select(item => new QuantitySurveySubcontractRevisionDto
            { Id = item.Id, ValuationId = item.ValuationId, Action = item.Action, ActorName = item.ActorName,
                ActorRoles = item.ActorRoles, CorrelationId = item.CorrelationId, Reason = item.Reason,
                BeforeJson = item.BeforeJson, AfterJson = item.AfterJson, CreatedAt = item.CreatedAt }).ToListAsync(token);
    }

    private async Task<QuantitySurveySubcontractDto> MutateSubcontractAsync(Guid id,
        QuantitySurveySubcontractActionRequest request, string action, string correlationId, CancellationToken token,
        Func<QuantitySurveySubcontract, Task> mutation)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var reason = RequiredText(request.Reason, 5, 2000, "Action reason"); var requestHash = Hash(new { id, action, reason });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var value = await RequiredAsync(id, true, token);
            if (value.LastMutationClientRequestId == request.ClientRequestId)
            { if (!FixedEquals(value.LastMutationRequestHash, requestHash)) throw RetryConflict(); await transaction.CommitAsync(token); return; }
            ApplyRowVersion(value.RowVersion, request.RowVersion, "subcontract");
            if (await db.QuantitySurveySubcontractRevisions.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.ClientRequestId == request.ClientRequestId, token)) throw RetryConflict();
            var before = Snapshot(value); await mutation(value);
            value.LastMutationClientRequestId = request.ClientRequestId; value.LastMutationRequestHash = requestHash;
            value.CorrelationId = Correlation(correlationId); value.UpdatedAt = DateTime.UtcNow; value.UpdatedBy = UserName; value.LastModifiedById = UserId;
            AddRevision(value, null, request.ClientRequestId, requestHash, action, before, Snapshot(value), reason, correlationId, null);
            AddAudit(value.Id, nameof(QuantitySurveySubcontract), action, before, Snapshot(value), correlationId);
            await SaveAsync(token); await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear(); return Map(await RequiredAsync(id, false, token));
    }

    private async Task<QuantitySurveySubcontractValuationDto> MutateValuationAsync(Guid id,
        QuantitySurveySubcontractActionRequest request, string action, string correlationId, CancellationToken token,
        Guid? actorPartnerId, Func<QuantitySurveySubcontractValuation, Task> mutation)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var reason = RequiredText(request.Reason, 5, 2000, "Action reason"); var requestHash = Hash(new { id, action, reason, request });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var value = await RequiredValuationAsync(id, true, token);
            if (value.LastMutationClientRequestId == request.ClientRequestId)
            { if (!FixedEquals(value.LastMutationRequestHash, requestHash)) throw RetryConflict(); await transaction.CommitAsync(token); return; }
            ApplyRowVersion(value.RowVersion, request.RowVersion, "subcontract valuation");
            if (await db.QuantitySurveySubcontractRevisions.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.ClientRequestId == request.ClientRequestId, token)) throw RetryConflict();
            var before = Snapshot(value); await mutation(value);
            value.LastMutationClientRequestId = request.ClientRequestId; value.LastMutationRequestHash = requestHash;
            value.CorrelationId = Correlation(correlationId); value.UpdatedAt = DateTime.UtcNow; value.UpdatedBy = UserName; value.LastModifiedById = UserId;
            AddRevision(value.Subcontract, value, request.ClientRequestId, requestHash, action, before, Snapshot(value), reason, correlationId, actorPartnerId);
            AddAudit(value.Id, nameof(QuantitySurveySubcontractValuation), action, before, Snapshot(value), correlationId);
            await SaveAsync(token); await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear(); return MapValuation(await RequiredValuationAsync(id, false, token));
    }

    private async Task<WorkflowOutcome> ResolveWorkflowOutcomeAsync(string type, Guid entityId, Guid instanceId,
        bool approve, string reason, CancellationToken token)
    {
        var status = await db.WorkflowInstances.AsNoTracking().Where(item => item.TenantId == TenantId && item.Id == instanceId && item.EntityId == entityId)
            .Select(item => (WorkflowInstanceStatus?)item.Status).SingleOrDefaultAsync(token);
        WorkflowOutcome outcome;
        if (approve && status == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
        else if (!approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) outcome = WorkflowOutcome.Rejected;
        else
        {
            if (!approve && status == WorkflowInstanceStatus.Completed) throw Conflict("A completed workflow is approved and cannot be rejected.");
            if (approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) throw Conflict("The workflow ended without approval.");
            if (!await workflow.CanUserApproveAsync(type, entityId, UserId))
                throw new UnauthorizedAccessException("You are not assigned to the current approval step.");
            var result = await workflow.ProcessApprovalAsync(type, entityId, UserId, approve ? "Approve" : "Reject",
                RequiredText(reason, 5, 2000, "Decision reason"));
            if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The workflow decision failed.");
            outcome = result.Outcome;
        }
        if (outcome != (approve ? WorkflowOutcome.Approved : WorkflowOutcome.Rejected))
            throw Conflict("The shared workflow has not reached the requested final outcome.");
        return outcome;
    }

    private async Task<Policy> ResolvePolicyAsync(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var profiles = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted &&
            value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && value.PublishedAt != null && value.EffectiveFrom <= now &&
            (!value.EffectiveTo.HasValue || value.EffectiveTo >= now)).OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No Published quantity-survey configuration is effective for this date.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one quantity-survey configuration is effective for this date.");
        var profile = profiles[0];
        var decisions = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().Where(value => value.TenantId == TenantId &&
            value.ProfileId == profile.Id && !value.IsDeleted && (value.DecisionKey == "QS-DEC-008" || value.DecisionKey == "QS-DEC-012") &&
            value.Status == QuantitySurveyConfigurationDecisionStatus.Approved && value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved &&
            value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified && (!value.EffectiveFrom.HasValue || value.EffectiveFrom <= now) &&
            (!value.EffectiveTo.HasValue || value.EffectiveTo >= now)).ToListAsync(token);
        var valuationDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-008")
            ?? throw Validation("The effective QS-DEC-008 valuation-and-certificate decision is unavailable.");
        var contractDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-012")
            ?? throw Validation("The effective QS-DEC-012 subcontract-controls decision is unavailable.");
        QsValuationCertificateValue valuation; QsContractControlsValue controls;
        try
        {
            valuation = JsonSerializer.Deserialize<QsValuationCertificateValue>(valuationDecision.ValueJson, JsonOptions) ?? throw new JsonException();
            controls = JsonSerializer.Deserialize<QsContractControlsValue>(contractDecision.ValueJson, JsonOptions) ?? throw new JsonException();
        }
        catch (JsonException exception) { throw Validation($"The effective QS subcontract policy is invalid: {exception.Message}"); }
        if (!controls.ControlSubcontracts || controls.SubcontractWorkflowDefinitionId == Guid.Empty)
            throw Validation("QS-DEC-012 must enable subcontract controls and select a subcontract workflow.");
        var subcontractWorkflow = await RequiredWorkflowAsync(controls.SubcontractWorkflowDefinitionId, QuantitySurveyWorkflowBindingRegistry.Subcontract, token);
        var certificateWorkflow = await RequiredWorkflowAsync(valuation.CertificateWorkflowDefinitionId, QuantitySurveyWorkflowBindingRegistry.PaymentCertificate, token);
        var metadata = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId &&
            value.Id == valuation.ValuationEvidenceMetadataTemplateId && !value.IsDeleted && value.IsActive && value.PublishedAt != null, token)
            ?? throw Validation("The configured Published subcontract valuation DMS template is unavailable.");
        var report = await db.ReportTemplates.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId &&
            value.Id == valuation.CertificateTemplateId && !value.IsDeleted && (value.Status == "Published" || value.Status == "published"), token)
            ?? throw Validation("The configured Published payment-certificate template is unavailable.");
        _ = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == valuation.CertificateExpenseAccountId &&
                !value.IsDeleted && value.Status == AccountStatus.Active && value.AllowDirectPosting && value.AccountType == AccountType.Expense, token)
            ?? throw Validation("The configured certificate project-cost account is unavailable.");
        _ = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == valuation.CertificateAccountsPayableAccountId &&
                !value.IsDeleted && value.Status == AccountStatus.Active && value.IsControlAccount && value.AccountType == AccountType.Liability, token)
            ?? throw Validation("The configured certificate AP account is unavailable.");
        _ = await db.TaxGroups.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == valuation.CertificateTaxGroupId &&
                !value.IsDeleted && value.IsActive && (value.Applicability == TaxApplicability.Purchases || value.Applicability == TaxApplicability.Both), token)
            ?? throw Validation("The configured certificate purchase tax group is unavailable.");
        var certMetadata = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId &&
            value.Id == valuation.CertificateMetadataTemplateId && !value.IsDeleted && value.IsActive && value.PublishedAt != null, token)
            ?? throw Validation("The configured Published certificate DMS template is unavailable.");
        var hash = Hash(new { Profile = profile.Id, profile.Version, ValuationDecision = valuationDecision.Id,
            ValuationValueJson = valuationDecision.ValueJson, ContractDecision = contractDecision.Id,
            ContractValueJson = contractDecision.ValueJson, SubcontractWorkflow = subcontractWorkflow.Id,
            CertificateWorkflow = certificateWorkflow.Id, Metadata = metadata.Id, Report = report.Id, ReportVersion = report.Version,
            CertificateMetadata = certMetadata.Id });
        return new(profile.Id, valuationDecision.Id, contractDecision.Id, controls, valuation,
            subcontractWorkflow.Id, certificateWorkflow.Id, metadata.Id, report.Version, certMetadata.TemplateCode, hash);
    }

    private async Task<WorkflowDefinition> RequiredWorkflowAsync(Guid id, string type, CancellationToken token)
    {
        var value = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).Include(item => item.Steps)
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted && item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published, token)
            ?? throw Validation($"The configured Published {type} workflow is unavailable.");
        if (!string.Equals(value.EntityType.Code, type, StringComparison.OrdinalIgnoreCase) ||
            !value.Steps.Any(step => !step.IsDeleted && step.IsRequired && step.StepType == WorkflowStepType.Approval))
            throw Validation($"The configured workflow must target {type} and contain a required approval step.");
        return value;
    }

    private async Task ValidateFrozenSubcontractAsync(QuantitySurveySubcontract value, CancellationToken token)
    {
        var policy = await ResolvePolicyAsync(token);
        var contract = await ContractQuery(value.ProjectId).SingleOrDefaultAsync(item => item.Id == value.ContractId, token)
            ?? throw Conflict("The parent active Works contract or its subcontract terms are no longer eligible.");
        if (!await PartnerQuery().AnyAsync(item => item.Id == value.SubcontractorBusinessPartnerId, token) ||
            !await db.PaymentTerms.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.Id == value.PaymentTermId && !item.IsDeleted && item.IsActive, token))
            throw Conflict("The subcontractor or payment term is no longer active.");
        if (contract.SubcontractPaymentTermId != value.PaymentTermId || value.ConfigurationProfileId != policy.ProfileId ||
            value.ContractControlsDecisionId != policy.ContractDecisionId || value.ApprovalWorkflowDefinitionId != policy.SubcontractWorkflowId ||
            !FixedEquals(value.PolicyHash, policy.PolicyHash))
            throw Conflict("The subcontract's frozen contract or policy lineage changed. Return it to Draft and refresh.");
    }

    private async Task<Policy> ValidateFrozenValuationAsync(QuantitySurveySubcontractValuation value, CancellationToken token)
    {
        await ValidateFrozenSubcontractAsync(value.Subcontract, token); var policy = await ResolvePolicyAsync(token);
        if (value.ConfigurationProfileId != policy.ProfileId || value.ValuationDecisionId != policy.ValuationDecisionId ||
            value.ApprovalWorkflowDefinitionId != policy.CertificateWorkflowId || value.EvidenceMetadataTemplateId != policy.EvidenceMetadataTemplateId ||
            !FixedEquals(value.PolicyHash, policy.PolicyHash))
            throw Conflict("The valuation's frozen policy lineage changed. Return it to Draft and refresh.");
        return policy;
    }

    private IQueryable<Contract> ContractQuery(Guid projectId) => db.Contracts.AsNoTracking()
        .Include(value => value.Tender).ThenInclude(value => value.SourcePurchaseRequisition)
        .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.ContractType == "Works" && value.Status == "Active" &&
            value.AllowSubcontracting && value.SubcontractPaymentTermId != null && value.Tender.SourcePurchaseRequisition != null &&
            value.Tender.SourcePurchaseRequisition.ProjectId == projectId);

    private IQueryable<BusinessPartner> PartnerQuery() => db.BusinessPartners.AsNoTracking().Where(value => value.TenantId == TenantId &&
        !value.IsDeleted && value.IsActive && !value.IsBlacklisted &&
        (value.RegistrationStatus == BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus ||
         value.RegistrationStatus == BusinessPartnerLifecyclePolicy.LegacyApprovedRegistrationStatus) &&
        (value.PartnerType == "Supplier" || value.PartnerType == "Contractor" || value.PartnerType == "Both"));

    private IQueryable<QuantitySurveySubcontract> Query(bool tracked = false)
    {
        var query = db.QuantitySurveySubcontracts.Include(value => value.Contract)
            .Include(value => value.SubcontractorBusinessPartner).Include(value => value.Evidence)
            .Include(value => value.Valuations).ThenInclude(value => value.Evidence)
            .Include(value => value.Valuations).ThenInclude(value => value.PaymentCertificate).ThenInclude(value => value!.VendorInvoice)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<QuantitySurveySubcontract> RequiredAsync(Guid id, bool tracked, CancellationToken token) =>
        await Query(tracked).SingleOrDefaultAsync(value => value.Id == id, token)
        ?? throw NotFound("The governed subcontract was not found.");

    private async Task<QuantitySurveySubcontractValuation> RequiredValuationAsync(Guid id, bool tracked, CancellationToken token)
    {
        var query = db.QuantitySurveySubcontractValuations.Include(value => value.Subcontract).ThenInclude(value => value.Contract)
            .Include(value => value.Subcontract).ThenInclude(value => value.SubcontractorBusinessPartner)
            .Include(value => value.Evidence).Include(value => value.PaymentCertificate).ThenInclude(value => value!.VendorInvoice)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(value => value.Id == id, token)
               ?? throw NotFound("The governed subcontract valuation was not found.");
    }

    private Task<QuantitySurveySubcontractValuation?> PreviousApprovedValuationAsync(Guid subcontractId, Guid? excludeId,
        CancellationToken token) => db.QuantitySurveySubcontractValuations.AsNoTracking().Where(value => value.TenantId == TenantId &&
            value.SubcontractId == subcontractId && value.Id != excludeId && !value.IsDeleted &&
            (value.Status == QuantitySurveySubcontractValuationStatuses.Approved || value.Status == QuantitySurveySubcontractValuationStatuses.Paid) &&
            value.ApprovalStatus == "Approved").OrderByDescending(value => value.ValuationDate).ThenByDescending(value => value.CreatedAt)
        .FirstOrDefaultAsync(token);

    private async Task RequireProjectAsync(Guid projectId)
    { if (!await projectService.HasProjectAccessAsync(projectId)) throw new UnauthorizedAccessException("You are not permitted to access the selected project."); }

    private async Task<ExternalActor> RequireExternalActorAsync(Guid projectId, bool requireUpload, Guid? subcontractId,
        CancellationToken token)
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
            (value.ArtifactType == "Project" || (subcontractId.HasValue && value.ArtifactType == "Subcontract" && value.ArtifactId == subcontractId))).ToListAsync(token);
        if (project.BusinessPartnerId != link.BusinessPartnerId && (policies.Count == 0 || (requireUpload && !policies.Any(value => value.CanUpload))))
            throw new UnauthorizedAccessException("The project external-access policy does not permit this subcontract action.");
        return new(link.BusinessPartnerId, link.BusinessPartner.PartnerName);
    }

    private async Task<string> NextNumberAsync<T>(string prefix, IQueryable<T> query, CancellationToken token) where T : TenantEntity
    { var year = DateTime.UtcNow.Year; var count = await query.CountAsync(value => value.TenantId == TenantId && value.CreatedAt.Year == year, token); return $"{prefix}-{year}-{count + 1:00000}"; }

    private async Task<string> NextCertificateNumberAsync(CancellationToken token)
    { var year = DateTime.UtcNow.Year; var count = await db.Set<ProjectPaymentCertificate>().IgnoreQueryFilters().CountAsync(value => value.TenantId == TenantId && value.CreatedAt.Year == year, token); return $"SUBCERT-{year}-{count + 1:00000}"; }

    private void AddRevision(QuantitySurveySubcontract subcontract, QuantitySurveySubcontractValuation? valuation,
        Guid clientRequestId, string requestHash, string action, object? before, object after, string reason,
        string correlationId, Guid? actorPartnerId) => db.QuantitySurveySubcontractRevisions.Add(new()
    {
        Id = Guid.NewGuid(), TenantId = TenantId, SubcontractId = subcontract.Id, ValuationId = valuation?.Id,
        ClientRequestId = clientRequestId, RequestHash = requestHash, Action = action, ActorUserId = UserId,
        ActorBusinessPartnerId = actorPartnerId, ActorName = UserName, ActorRoles = ActorRoles,
        CorrelationId = Correlation(correlationId), Reason = reason,
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
        CreatedBy = UserName, CreatedById = UserId
    });

    private void AddChargeRevision(QuantitySurveySubcontractChargeNotice charge, Guid clientRequestId,
        string requestHash, string action, object? before, object after, string reason,
        string correlationId) => db.QuantitySurveySubcontractChargeRevisions.Add(new()
    {
        Id = Guid.NewGuid(), TenantId = TenantId, ChargeNoticeId = charge.Id,
        ClientRequestId = clientRequestId, RequestHash = requestHash, Action = action,
        ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles,
        CorrelationId = Correlation(correlationId), Reason = reason,
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
        CreatedBy = UserName, CreatedById = UserId
    });

    private void AddAudit(Guid resourceId, string resource, string action, object? before, object after, string correlationId)
        => db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = resource, ResourceId = resourceId.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
            IpAddress = "api", UserAgent = "QuantitySurvey", Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The subcontract record changed. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("IX_", StringComparison.OrdinalIgnoreCase) == true ||
            exception.InnerException?.Message.Contains("QS0521", StringComparison.OrdinalIgnoreCase) == true ||
            exception.InnerException?.Message.Contains("subcontract", StringComparison.OrdinalIgnoreCase) == true)
        { throw Conflict("The subcontract request conflicts with an existing governed source or lifecycle rule."); }
    }

    private static QuantitySurveySubcontractDto Map(QuantitySurveySubcontract value)
    {
        var approved = value.Valuations.Where(item => !item.IsDeleted && item.Status is
            (QuantitySurveySubcontractValuationStatuses.Approved or QuantitySurveySubcontractValuationStatuses.Paid)).ToList();
        var certified = approved.Count == 0 ? 0m : approved.Max(item => item.AssessedToDateAmount ?? 0m);
        var paid = approved.Where(item => item.Status == QuantitySurveySubcontractValuationStatuses.Paid).Sum(item => item.NetCertifiedAmount);
        var retention = Round(approved.Sum(item => item.RetentionHeldAmount) - approved.Sum(item => item.RetentionReleasedAmount));
        return new()
        {
            Id = value.Id, ProjectId = value.ProjectId, ContractId = value.ContractId,
            ContractNumber = value.Contract?.ContractNumber ?? string.Empty,
            SubcontractorBusinessPartnerId = value.SubcontractorBusinessPartnerId,
            SubcontractorName = value.SubcontractorBusinessPartner?.PartnerName ?? string.Empty,
            PaymentTermId = value.PaymentTermId, PaymentTermName = string.Empty,
            SubcontractNumber = value.SubcontractNumber, Title = value.Title, Scope = value.Scope,
            SubcontractValue = value.SubcontractValue, Currency = value.Currency, RetentionPercentage = value.RetentionPercentage,
            StartDate = value.StartDate, EndDate = value.EndDate, Status = value.Status, ApprovalStatus = value.ApprovalStatus,
            WorkflowInstanceId = value.WorkflowInstanceId, ClosedAt = value.ClosedAt, ClosureNote = value.ClosureNote,
            CertifiedToDateAmount = certified, PaidToDateAmount = paid, RetentionBalance = retention,
            OutstandingBalance = Round(value.SubcontractValue - certified), RowVersion = Convert.ToBase64String(value.RowVersion),
            Evidence = value.Evidence.Where(item => !item.IsDeleted && item.ValuationId == null).OrderBy(item => item.CreatedAt).Select(MapEvidence).ToList(),
            Valuations = value.Valuations.Where(item => !item.IsDeleted).OrderByDescending(item => item.ValuationDate).Select(MapValuation).ToList()
        };
    }

    private static QuantitySurveySubcontractValuationDto MapValuation(QuantitySurveySubcontractValuation value) => new()
    {
        Id = value.Id, SubcontractId = value.SubcontractId, ValuationNumber = value.ValuationNumber,
        ValuationDate = value.ValuationDate, PeriodEndDate = value.PeriodEndDate, ClaimedToDateAmount = value.ClaimedToDateAmount,
        AssessedToDateAmount = value.AssessedToDateAmount, PreviouslyCertifiedAmount = value.PreviouslyCertifiedAmount,
        CurrentCertifiedAmount = value.CurrentCertifiedAmount, RetentionHeldAmount = value.RetentionHeldAmount,
        RetentionReleasedAmount = value.RetentionReleasedAmount, ApprovedBackChargeAmount = value.ApprovedBackChargeAmount,
        ApprovedContraChargeAmount = value.ApprovedContraChargeAmount, TaxAmount = value.TaxAmount,
        NetCertifiedAmount = value.NetCertifiedAmount, IsFinal = value.IsFinal, Status = value.Status,
        ApprovalStatus = value.ApprovalStatus, SubmissionNote = value.SubmissionNote, AssessmentNote = value.AssessmentNote,
        WorkflowInstanceId = value.WorkflowInstanceId, PaymentCertificateId = value.PaymentCertificateId,
        CertificateNumber = value.PaymentCertificate?.CertificateNumber, VendorInvoiceId = value.PaymentCertificate?.VendorInvoiceId,
        PaymentStatus = value.PaymentCertificate?.PaymentStatusSnapshot ?? "NotInvoiced",
        ApHandoffStatus = value.PaymentCertificate?.ApHandoffStatus ?? ProjectPaymentCertificateApHandoffStatuses.NotReady,
        RowVersion = Convert.ToBase64String(value.RowVersion),
        Evidence = value.Evidence.Where(item => !item.IsDeleted).OrderBy(item => item.CreatedAt).Select(MapEvidence).ToList()
    };

    private static QuantitySurveySubcontractEvidenceDto MapEvidence(QuantitySurveySubcontractEvidence value) => new()
    { Id = value.Id, ValuationId = value.ValuationId, EvidenceType = value.EvidenceType, Title = value.Title,
        FileName = value.OriginalFileName, FileSize = value.FileSize, ChecksumSha256 = value.ChecksumSha256,
        CentralDocumentRecordId = value.CentralDocumentRecordId, CentralDocumentVersionId = value.CentralDocumentVersionId };

    private static object Snapshot(QuantitySurveySubcontract value) => new
    { value.Id, value.ProjectId, value.ContractId, value.SubcontractorBusinessPartnerId, value.PaymentTermId,
        value.SubcontractNumber, value.Title, value.Scope, value.SubcontractValue, value.Currency, value.RetentionPercentage,
        value.StartDate, value.EndDate, value.Status, value.ApprovalStatus, value.ConfigurationProfileId,
        value.ContractControlsDecisionId, value.ApprovalWorkflowDefinitionId, value.PolicyHash, value.WorkflowInstanceId,
        value.SubmittedById, value.SubmittedAt, value.ApprovedById, value.ApprovedAt, value.ClosedById, value.ClosedAt, value.ClosureNote };

    private static object Snapshot(QuantitySurveySubcontractValuation value) => new
    { value.Id, value.SubcontractId, value.ValuationNumber, value.ValuationDate, value.PeriodEndDate,
        value.ClaimedToDateAmount, value.AssessedToDateAmount, value.PreviouslyCertifiedAmount, value.CurrentCertifiedAmount,
        value.RetentionHeldAmount, value.RetentionReleasedAmount, value.ApprovedBackChargeAmount, value.ApprovedContraChargeAmount,
        value.TaxAmount, value.NetCertifiedAmount, value.IsFinal, value.Status, value.ApprovalStatus,
        value.ConfigurationProfileId, value.ValuationDecisionId, value.ApprovalWorkflowDefinitionId,
        value.EvidenceMetadataTemplateId, value.PolicyHash, value.WorkflowInstanceId, value.SubmittedById,
        value.SubmittedBusinessPartnerId, value.SubmittedAt, value.AssessedById, value.AssessedAt,
        value.ApprovedById, value.ApprovedAt, value.PaymentCertificateId };

    private static object SnapshotCharge(QuantitySurveySubcontractChargeNotice value) => new
    {
        value.Id, value.SubcontractId, value.AppliedValuationId, value.NoticeNumber,
        value.ChargeType, value.ProposedAmount, value.ApprovedAmount, value.Status,
        value.ApprovalStatus, value.ResponseStatus, value.AllocatedAt, value.AppliedAt,
        value.PolicyHash, value.WorkflowInstanceId
    };

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
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right)
    { if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false; var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right); return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); }
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static QuantitySurveySubcontractNotFoundException NotFound(string value) => new(value);
    private static QuantitySurveySubcontractValidationException Validation(string value) => new(value);
    private static QuantitySurveySubcontractConflictException Conflict(string value) => new(value);
    private static QuantitySurveySubcontractConflictException RetryConflict() => Conflict("This client request identifier is already bound to different subcontract inputs.");
    private readonly record struct ExternalActor(Guid BusinessPartnerId, string Name);
    private sealed record Policy(Guid ProfileId, Guid ValuationDecisionId, Guid ContractDecisionId,
        QsContractControlsValue ContractControls, QsValuationCertificateValue Valuation,
        Guid SubcontractWorkflowId, Guid CertificateWorkflowId, Guid EvidenceMetadataTemplateId,
        int CertificateTemplateVersion, string CertificateMetadataTemplateCode, string PolicyHash);
}
