using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyVariationService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters,
    IControlledFileUploadService controlledFiles,
    ICentralDocumentRepositoryFileService centralDocuments) : IQuantitySurveyVariationService
{
    private const int MaximumEvidenceBytes = 50 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(',', currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyVariationWorkspaceDto> GetWorkspaceAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        var contracts = await db.Contracts.AsNoTracking().Include(value => value.BusinessPartner)
            .Include(value => value.Tender).ThenInclude(value => value.SourcePurchaseRequisition)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.ContractType == "Works" && value.Status == "Active" &&
                            value.Tender.SourcePurchaseRequisition != null &&
                            value.Tender.SourcePurchaseRequisition.ProjectId == projectId)
            .OrderBy(value => value.ContractNumber).Select(value => new QuantitySurveyVariationContractLookupDto(
                value.Id, value.ContractNumber, value.ContractTitle, value.BusinessPartnerId,
                value.BusinessPartner.PartnerName, value.ContractValue, value.Currency)).ToListAsync(token);
        var instructions = await db.ProjectSiteInstructions.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                value.Status != ProjectSiteInstructionStatuses.Draft && value.Status != ProjectSiteInstructionStatuses.Cancelled)
            .OrderByDescending(value => value.IssuedDate).Select(value => new QuantitySurveyVariationSourceLookupDto(value.Id,
                value.ReferenceNumber ?? value.Id.ToString(), value.Title, value.Status, value.EstimatedCostImpact, value.ScheduleImpactDays)).ToListAsync(token);
        var changes = await db.ProjectChangeRequests.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted && value.Status == "Approved")
            .OrderByDescending(value => value.CreatedAt).Select(value => new QuantitySurveyVariationSourceLookupDto(value.Id,
                value.Id.ToString(), value.Title, value.Status, value.CostImpact, value.ScheduleImpactDays)).ToListAsync(token);
        var versions = await db.ProjectBoqVersions.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                value.Status == ProjectBoqVersionStatuses.Approved && value.PublishedAt != null).Select(value => value.Id).ToListAsync(token);
        var lines = await db.ProjectBoqVersionLines.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                versions.Contains(value.ProjectBoqVersionId) && value.ItemType == ProjectBoqItemTypes.Item)
            .OrderBy(value => value.SortOrder).Select(value => new QuantitySurveyVariationBoqLineLookupDto(value.Id,
                value.ProjectBoqVersionId, value.LineKey, value.LineNumber ?? value.ItemCode ?? value.Id.ToString(), value.Description,
                value.UnitOfMeasure, value.Quantity, value.UnitRate ?? 0m, value.Currency)).ToListAsync(token);
        var variations = await Query().Where(value => value.ProjectId == projectId).OrderByDescending(value => value.RequestedDate).ToListAsync(token);
        return new() { Contracts = contracts, SiteInstructions = instructions, ChangeRequests = changes, BoqLines = lines, Variations = variations.Select(Map).ToList() };
    }

    public async Task<QuantitySurveyVariationDto> GetAsync(Guid id, CancellationToken token = default)
    {
        var value = await RequiredAsync(id, false, token); await RequireProjectAsync(value.ProjectId); return Map(value);
    }

    public async Task<QuantitySurveyVariationDto> SaveAsync(Guid projectId, SaveQuantitySurveyVariationRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || request.ContractId == Guid.Empty || request.ApprovedBoqVersionId == Guid.Empty || request.Lines.Count == 0)
            throw Validation("Select an active Works contract, an approved BoQ and at least one controlled BoQ line.");
        await RequireProjectAsync(projectId);
        var reason = RequiredText(request.Reason, 10, 4000, "Variation reason");
        var title = RequiredText(request.Title, 3, 200, "Variation title");
        var requestHash = Hash(new { projectId, request.Id, request.ContractId, request.ApprovedBoqVersionId, request.SourceType, request.SiteInstructionId, request.ChangeRequestId, title, reason, request.VariationType, request.ScheduleImpactDays, request.Lines });
        Guid id = Guid.Empty;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var retry = await db.QuantitySurveyVariationRevisions.AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId, token);
            if (retry is not null)
            {
                if (!FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
                id = retry.VariationOrderId; await transaction.CommitAsync(token); return;
            }
            var contract = await db.Contracts.Include(value => value.BusinessPartner)
                .Include(value => value.Tender).ThenInclude(value => value.SourcePurchaseRequisition)
                .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.ContractId && !value.IsDeleted && value.ContractType == "Works" && value.Status == "Active", token)
                ?? throw Validation("Select an active Works contract.");
            if (contract.Tender.SourcePurchaseRequisition?.ProjectId != projectId)
                throw Conflict("The selected Works contract is not linked to this project through its governed procurement requisition.");
            var version = await db.ProjectBoqVersions.SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.ApprovedBoqVersionId && value.ProjectId == projectId && !value.IsDeleted && value.Status == ProjectBoqVersionStatuses.Approved && value.PublishedAt != null, token)
                ?? throw Validation("Select a published Approved BoQ version from this project.");
            await ValidateSourceAsync(projectId, request, token);
            var variationType = NormalizeType(request.VariationType);
            if (!QuantitySurveyVariationRules.IsSourceCompatibleWithRecordType(request.SourceType, variationType))
                throw Validation("The selected site-instruction or change-order record type must use its matching governed source type.");
            var policy = await ResolvePolicyAsync(token);
            if (!policy.Value.AllowedTypes.Contains(QuantitySurveyVariationRules.PolicyRecordType(variationType), StringComparer.OrdinalIgnoreCase))
                throw Validation("The selected variation, change-order, daywork, additional-work or site-instruction type is not allowed by QS-DEC-011.");
            var existing = request.Id.HasValue
                ? await db.ProjectVariationOrders.Include(value => value.ValuationLines).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.Id && value.ProjectId == projectId && value.IsQuantitySurveyGoverned && !value.IsDeleted, token)
                    ?? throw new QuantitySurveyVariationNotFoundException("The governed variation draft was not found.")
                : await db.ProjectVariationOrders.Include(value => value.ValuationLines).SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
            if (existing is not null && existing.Status is not (ProjectVariationOrderStatuses.Draft or ProjectVariationOrderStatuses.Rejected))
                throw Conflict("Only a Draft or Rejected variation can be amended.");
            if (existing is not null && !string.IsNullOrWhiteSpace(request.RowVersion)) ApplyRowVersion(existing, request.RowVersion);
            var before = existing is null ? null : Snapshot(existing);
            var entity = existing ?? new ProjectVariationOrder
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ClientRequestId = request.ClientRequestId,
                ReferenceNumber = await NextNumberAsync(token), RequestedDate = DateTime.UtcNow, PreparedById = UserId,
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId, IsQuantitySurveyGoverned = true
            };
            if (existing is null) db.ProjectVariationOrders.Add(entity);
            else entity.ValuationLines = await db.QuantitySurveyVariationValuationLines.IgnoreQueryFilters()
                .Where(line => line.TenantId == TenantId && line.VariationOrderId == entity.Id).ToListAsync(token);
            entity.ContractId = contract.Id; entity.ContractorBusinessPartnerId = contract.BusinessPartnerId;
            entity.ApprovedBoqVersionId = version.Id; entity.VariationSourceType = request.SourceType;
            entity.SiteInstructionId = request.SiteInstructionId; entity.ChangeRequestId = request.ChangeRequestId;
            entity.Title = title; entity.Description = reason; entity.VariationType = variationType;
            entity.Status = ProjectVariationOrderStatuses.Draft; entity.ApprovalStatus = ProjectVariationOrderStatuses.Draft;
            entity.RequestHash = requestHash; entity.Currency = contract.Currency.ToUpperInvariant(); entity.ScheduleImpactDays = request.ScheduleImpactDays;
            entity.OriginalContractSumSnapshot = contract.ContractValue; entity.ConfigurationProfileId = policy.ProfileId;
            entity.VariationDecisionId = policy.DecisionId; entity.ApprovalWorkflowDefinitionId = policy.WorkflowDefinitionId;
            entity.EvidenceMetadataTemplateId = policy.EvidenceTemplateId; entity.PolicyHash = policy.PolicyHash;
            entity.UpdateContractSumOnApplication = policy.Value.UpdateContractSum; entity.UpdateBudgetOnApplication = policy.Value.UpdateBudget;
            entity.UpdateForecastOnApplication = policy.Value.UpdateForecast; entity.UpdateCertificateOnApplication = policy.Value.UpdateCertificate;
            entity.SubmittedById = null; entity.SubmittedAt = null; entity.ApprovedById = null; entity.ApprovedAt = null;
            entity.ApprovedAmount = null; entity.RevisedContractSumSnapshot = null; entity.WorkflowInstanceId = null; entity.RejectionReason = null;
            entity.CorrelationId = Correlation(correlationId); entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
            var lines = await BuildLinesAsync(entity, request.Lines, version.Id, contract.Currency, token);
            ReconcileValuationLines(db, entity, lines, UserId, UserName);
            entity.EstimatedAmount = Round(lines.Sum(value => value.Amount));
            entity.BudgetImpactAmount = policy.Value.UpdateBudget ? entity.EstimatedAmount : null;
            entity.ForecastImpactAmount = policy.Value.UpdateForecast ? entity.EstimatedAmount : null;
            id = entity.Id;
            AddRevision(entity, request.ClientRequestId, requestHash, "PrepareVariation", reason, before, Snapshot(entity), correlationId);
            AddAudit(entity, "PrepareVariation", before, Snapshot(entity), correlationId);
            await SaveChangesAsync(token); await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear(); return await GetAsync(id, token);
    }

    public async Task<QuantitySurveyVariationEvidenceDto> UploadEvidenceAsync(Guid id, Guid clientRequestId, string title, string fileName, string contentType, long fileSize, Func<Stream> openRead, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token); await RequireProjectAsync(entity.ProjectId);
        if (entity.Status is not (ProjectVariationOrderStatuses.Draft or ProjectVariationOrderStatuses.Rejected)) throw Conflict("Evidence can be added only to a Draft or Rejected variation.");
        if (clientRequestId == Guid.Empty || fileSize <= 0 || fileSize > MaximumEvidenceBytes) throw Validation("Select an evidence file up to 50 MB.");
        var safeTitle = RequiredText(title, 3, 200, "Evidence title");
        var safeName = Path.GetFileName(fileName);
        await using var source = openRead(); using var memory = new MemoryStream(); await source.CopyToAsync(memory, token);
        var bytes = memory.ToArray(); if (bytes.LongLength != fileSize) throw Validation("The evidence file size changed during upload.");
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var requestHash = Hash(new { id, safeTitle, safeName, contentType, fileSize, checksum });
        var retry = await db.QuantitySurveyVariationEvidence.AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == clientRequestId, token);
        if (retry is not null) { if (retry.VariationOrderId != id || !FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict(); return MapEvidence(retry); }
        var template = await db.CentralDocumentMetadataTemplates.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == entity.EvidenceMetadataTemplateId && !value.IsDeleted && value.IsActive && value.PublishedAt != null, token)
            ?? throw Conflict("The frozen variation evidence DMS template is no longer active and Published.");
        var upload = await controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = TenantId, ActorUserId = UserId, ActorName = UserName, Category = ControlledFileUploadCategories.QuantitySurveyVariationEvidence,
            FileName = safeName, ContentType = contentType, FileSize = bytes.LongLength, OpenReadStream = () => new MemoryStream(bytes, false)
        }, token);
        if (upload.Record.VirusScanStatus != FileVirusScanStatus.Clean || !FixedEquals(upload.ChecksumSha256, checksum))
        { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw Conflict("The centrally scanned evidence did not pass its integrity check."); }
        var evidenceId = Guid.NewGuid(); CentralDocumentRepositoryLink document;
        try
        {
            document = await centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = TenantId, ActorUserId = UserId, ActorName = UserName, FileUploadRecordId = upload.Record.Id,
                SourceModule = "QuantitySurvey", SourceLabel = "Quantity Survey variation evidence", SourceEntityType = nameof(QuantitySurveyVariationEvidence),
                SourceRecordId = evidenceId, SourceRecordReference = entity.ReferenceNumber, Title = safeTitle, DocumentType = template.DocumentType,
                MetadataTemplateCode = template.TemplateCode, AccessProfile = template.AccessProfile, VersionStatus = "Submitted",
                ChangeSummary = "Clean scanned variation evidence retained in the central DMS.", RequirePublishedGovernance = true,
                MetadataValues = [new("variationOrderId", "Variation order ID", entity.Id.ToString(), "guid"), new("projectId", "Project ID", entity.ProjectId.ToString(), "guid"),
                    new("contractId", "Contract ID", entity.ContractId?.ToString(), "guid"),
                    new("recordReference", "Variation reference", entity.ReferenceNumber ?? entity.Id.ToString()),
                    new("evidenceDate", "Evidence date", DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), "date"),
                    new("checksumSha256", "Checksum SHA-256", checksum)]
            }, token);
        }
        catch { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw; }
        var evidence = new QuantitySurveyVariationEvidence
        {
            Id = evidenceId, TenantId = TenantId, VariationOrderId = id, ClientRequestId = clientRequestId, RequestHash = requestHash,
            Title = safeTitle, OriginalFileName = upload.Record.OriginalFileName, ContentType = contentType, FileSize = bytes.LongLength,
            ChecksumSha256 = checksum, FileUploadRecordId = document.FileUploadRecordId, CentralDocumentRecordId = document.DocumentRecordId,
            CentralDocumentVersionId = document.DocumentVersionId, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        };
        db.QuantitySurveyVariationEvidence.Add(evidence); AddAudit(entity, "AttachVariationEvidence", null, new { evidence.Id, evidence.Title, evidence.ChecksumSha256 }, entity.CorrelationId ?? string.Empty);
        try { await SaveChangesAsync(token); } catch { await centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, token); throw; }
        return MapEvidence(evidence);
    }

    public Task<QuantitySurveyVariationDto> SubmitAsync(Guid id, QuantitySurveyVariationActionRequest request, string correlationId, CancellationToken token = default)
        => MutateAsync(id, request, "SubmitVariation", correlationId, token, async value =>
        {
            if (value.Status is not (ProjectVariationOrderStatuses.Draft or ProjectVariationOrderStatuses.Rejected)) throw Conflict("Only a Draft or Rejected variation can be submitted.");
            if (value.VariationEvidence.All(evidence => evidence.IsDeleted)) throw Conflict("Attach at least one clean centrally governed evidence file before submission.");
            await ValidateFrozenAsync(value, token);
            if (QuantitySurveyDayworkRules.IsSupportedVariationType(value.VariationType))
            {
                var sheets = await db.QuantitySurveyDayworkSheets.AsNoTracking().Where(sheet => sheet.TenantId == TenantId &&
                    sheet.VariationOrderId == value.Id && !sheet.IsDeleted).ToListAsync(token);
                if (sheets.Count == 0 || sheets.Any(sheet => sheet.Status != QuantitySurveyDayworkSheetStatus.Verified))
                    throw Conflict("Every Daywork or Additional Work variation requires at least one independently Verified detail sheet before submission.");
                if (Round(sheets.Sum(sheet => sheet.TotalAmount)) != Round(value.EstimatedAmount ?? 0m))
                    throw Conflict("The Verified daywork-sheet total must reconcile exactly to the parent variation valuation.");
            }
            var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.Variation, value.Id, value.ApprovalWorkflowDefinitionId!.Value);
            if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The variation workflow could not be started.");
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Variation).ApplySubmitOutcome(value, result, UserId);
            value.Status = ProjectVariationOrderStatuses.PendingApproval; value.ApprovalStatus = "Pending";
            value.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId; value.SubmittedById = UserId; value.SubmittedAt = DateTime.UtcNow;
        });

    public Task<QuantitySurveyVariationDto> ApproveAsync(Guid id, QuantitySurveyVariationActionRequest request, string correlationId, CancellationToken token = default)
        => DecideAsync(id, request, true, correlationId, token);
    public Task<QuantitySurveyVariationDto> RejectAsync(Guid id, QuantitySurveyVariationActionRequest request, string correlationId, CancellationToken token = default)
        => DecideAsync(id, request, false, correlationId, token);

    public Task<QuantitySurveyVariationDto> ApplyAsync(Guid id, QuantitySurveyVariationActionRequest request, string correlationId, CancellationToken token = default)
        => MutateAsync(id, request, QuantitySurveyAuditEventMap.ApplyApprovedVariation, correlationId, token, async value =>
        {
            if (value.Status != ProjectVariationOrderStatuses.Approved)
                throw Conflict("Only an Approved variation can be applied to downstream commercial records.");
            await ApplyApprovedVariationAsync(value, request.ClientRequestId, request.Reason, correlationId, token);
        });

    public async Task<IReadOnlyList<QuantitySurveyVariationRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default)
    {
        var value = await RequiredAsync(id, false, token); await RequireProjectAsync(value.ProjectId);
        return await db.QuantitySurveyVariationRevisions.AsNoTracking().Where(item => item.TenantId == TenantId && item.VariationOrderId == id && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt).Select(item => new QuantitySurveyVariationRevisionDto { Id = item.Id, Action = item.Action, ActorName = item.ActorName, ActorRoles = item.ActorRoles, CorrelationId = item.CorrelationId, Reason = item.Reason, BeforeJson = item.BeforeJson, AfterJson = item.AfterJson, CreatedAt = item.CreatedAt }).ToListAsync(token);
    }

    public async Task<bool> IsGovernedAsync(Guid? variationId, CancellationToken token = default)
    {
        if (variationId.HasValue && await db.ProjectVariationOrders.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == variationId && value.IsQuantitySurveyGoverned && !value.IsDeleted, token)) return true;
        var now = DateTime.UtcNow;
        return await db.QuantitySurveyConfigurationProfiles.AsNoTracking().AnyAsync(profile => profile.TenantId == TenantId && !profile.IsDeleted && profile.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && profile.EffectiveFrom <= now && (!profile.EffectiveTo.HasValue || profile.EffectiveTo >= now) &&
            db.QuantitySurveyConfigurationDecisions.Any(decision => decision.TenantId == TenantId && decision.ProfileId == profile.Id && !decision.IsDeleted && decision.DecisionKey == "QS-DEC-011" && decision.Status == QuantitySurveyConfigurationDecisionStatus.Approved && decision.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved && decision.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified), token);
    }

    private async Task<QuantitySurveyVariationDto> DecideAsync(Guid id, QuantitySurveyVariationActionRequest request, bool approve, string correlationId, CancellationToken token) =>
        await MutateAsync(id, request, approve ? "ApproveVariation" : "RejectVariation", correlationId, token, async value =>
        {
            if (value.Status != ProjectVariationOrderStatuses.PendingApproval) throw Conflict("The variation must be PendingApproval before a decision.");
            if (value.PreparedById == UserId || value.SubmittedById == UserId) throw Conflict("Maker-checker control prevents the preparer or submitter from deciding this variation.");
            await ValidateFrozenAsync(value, token);
            if (!value.WorkflowInstanceId.HasValue) throw Conflict("The shared workflow instance is missing.");
            var status = await db.WorkflowInstances.AsNoTracking().Where(item => item.TenantId == TenantId && item.Id == value.WorkflowInstanceId && !item.IsDeleted).Select(item => (WorkflowInstanceStatus?)item.Status).SingleOrDefaultAsync(token)
                ?? throw Conflict("The shared workflow instance is unavailable.");
            WorkflowOutcome outcome;
            if (approve && status == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
            else if (!approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) outcome = WorkflowOutcome.Rejected;
            else
            {
                if (!approve && status == WorkflowInstanceStatus.Completed) throw Conflict("A completed workflow is approved and cannot be rejected.");
                if (approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) throw Conflict("The workflow ended without approval.");
                if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Variation, value.Id, UserId)) throw new UnauthorizedAccessException("You are not assigned to the current variation approval step.");
                var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Variation, value.Id, UserId, approve ? "Approve" : "Reject", RequiredText(request.Reason, 5, 2000, "Decision reason"));
                if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The variation workflow decision failed."); outcome = result.Outcome;
            }
            // Commit successful intermediate reviews; only the final outcome applies commercial changes.
            if (approve && outcome == WorkflowOutcome.Pending) return;
            if (outcome != (approve ? WorkflowOutcome.Approved : WorkflowOutcome.Rejected)) throw Conflict("The shared workflow has not reached the requested final outcome.");
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Variation).ApplyApprovalOutcome(value, outcome, UserId, approve ? null : request.Reason.Trim());
            value.Status = approve ? ProjectVariationOrderStatuses.Approved : ProjectVariationOrderStatuses.Rejected;
            value.ApprovalStatus = approve ? "Approved" : "Rejected"; value.ApprovedById = approve ? UserId : null;
            value.ApprovedAt = approve ? DateTime.UtcNow : null; value.ApprovedDate = approve ? DateTime.UtcNow : null;
            value.ApprovedByName = approve ? UserName : null; value.ApprovedAmount = approve ? value.EstimatedAmount : null;
            value.RevisedContractSumSnapshot = approve ? QuantitySurveyVariationRules.CalculateRevisedContractSum(value.OriginalContractSumSnapshot!.Value, value.ApprovedAmount!.Value) : null;
            value.RejectionReason = approve ? null : request.Reason.Trim();
            if (approve)
                await ApplyApprovedVariationAsync(value, request.ClientRequestId, request.Reason, correlationId, token);
        });

    private async Task ApplyApprovedVariationAsync(ProjectVariationOrder value, Guid clientRequestId, string reason, string correlationId, CancellationToken token)
    {
        if (value.DownstreamApplicationStatus != ProjectVariationApplicationStatuses.NotApplied)
        {
            if (value.ApplicationClientRequestId == clientRequestId && value.ApplicationRequestHash is not null) return;
            throw Conflict("The approved variation has already been applied to downstream commercial records.");
        }
        if (value.Status != ProjectVariationOrderStatuses.Approved || !value.ApprovedAmount.HasValue)
            throw Conflict("The variation must be Approved with a frozen approved amount before downstream application.");

        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.ApplyApprovedVariation);
        var applicationReason = RequiredText(reason, 5, 2000, "Application reason");
        var requestHash = Hash(new { Action = QuantitySurveyAuditEventMap.ApplyApprovedVariation, value.Id, value.ApprovedAmount, applicationReason });
        var contract = await db.Contracts.Include(item => item.Milestones).SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == value.ContractId && !item.IsDeleted && item.Status == "Active" && item.ContractType == "Works", token)
            ?? throw Conflict("The controlled Works contract is no longer active.");
        if (contract.ContractValue != value.OriginalContractSumSnapshot)
            throw Conflict("The Works contract value changed after the variation was prepared. Rebase the variation before applying it.");
        if (contract.Currency != value.Currency || contract.BusinessPartnerId != value.ContractorBusinessPartnerId)
            throw Conflict("The Works contract currency or contractor no longer matches the approved variation.");

        var sourceVersion = await db.ProjectBoqVersions.Include(item => item.Lines).SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == value.ApprovedBoqVersionId && item.ProjectId == value.ProjectId && !item.IsDeleted &&
                item.VersionType == QuantitySurveyBoqVersionType.Approved && item.Status == ProjectBoqVersionStatuses.Approved && item.PublishedAt != null, token)
            ?? throw Conflict("The approved BoQ publication used by the variation is unavailable.");
        var currentPublicationId = await db.ProjectBoqVersions.AsNoTracking().Where(item => item.TenantId == TenantId && item.ProjectId == value.ProjectId &&
                !item.IsDeleted && item.VersionType == QuantitySurveyBoqVersionType.Approved && item.Status == ProjectBoqVersionStatuses.Approved && item.PublishedAt != null)
            .OrderByDescending(item => item.PublishedAt).ThenByDescending(item => item.VersionNumber).Select(item => item.Id).FirstOrDefaultAsync(token);
        if (currentPublicationId != sourceVersion.Id)
            throw Conflict("The approved BoQ publication changed after the variation was prepared. Rebase the variation before applying it.");
        if (await db.ProjectBoqVersions.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.ProjectId == value.ProjectId && !item.IsDeleted &&
                item.VersionType != QuantitySurveyBoqVersionType.Approved &&
                (item.Status == ProjectBoqVersionStatuses.Draft || item.Status == ProjectBoqVersionStatuses.PendingApproval || item.Status == ProjectBoqVersionStatuses.Rejected), token))
            throw Conflict("Complete the existing BoQ candidate lifecycle before applying this variation.");

        var now = DateTime.UtcNow;
        var amendmentSequence = await db.ContractAmendments.Where(item => item.TenantId == TenantId && item.ContractId == contract.Id && !item.IsDeleted)
            .Select(item => (int?)item.SequenceNumber).MaxAsync(token) ?? 0;
        var revisedContractValue = value.UpdateContractSumOnApplication
            ? QuantitySurveyVariationRules.CalculateNonNegativeDownstreamValue(contract.ContractValue, value.ApprovedAmount.Value, "the Works contract")
            : contract.ContractValue;
        var amendment = new ContractAmendment
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ContractId = contract.Id,
            AmendmentNumber = $"AMD-QS-{amendmentSequence + 1:000}", SequenceNumber = amendmentSequence + 1,
            AmendmentType = value.UpdateContractSumOnApplication ? "ValueChange" : value.ScheduleImpactDays != 0 ? "TimelineExtension" : "ScopeChange",
            Reason = applicationReason[..Math.Min(applicationReason.Length, 500)], Description = $"Approved QS variation {value.ReferenceNumber}: {value.Title}",
            PreviousValue = contract.ContractValue, NewValue = revisedContractValue, ValueChange = value.UpdateContractSumOnApplication ? value.ApprovedAmount : 0m,
            PreviousEndDate = contract.EndDate, NewEndDate = contract.EndDate?.AddDays(value.ScheduleImpactDays ?? 0), DaysExtended = value.ScheduleImpactDays,
            ScopeChanges = value.Description ?? value.Title, Status = "Approved", RequestedDate = value.SubmittedAt ?? now,
            RequestedById = value.SubmittedById, ApprovedDate = now, ApprovedById = UserId,
            ApprovalNotes = $"Applied from governed QS variation {value.ReferenceNumber}.", Notes = $"QS-0511:{value.Id:N}",
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        db.ContractAmendments.Add(amendment);
        if (value.UpdateContractSumOnApplication)
        {
            contract.ContractValue = revisedContractValue;
            foreach (var milestone in contract.Milestones.Where(item => !item.IsDeleted))
            {
                milestone.PaymentAmount = Round(revisedContractValue * milestone.PaymentPercentage / 100m);
                milestone.UpdatedAt = now; milestone.UpdatedBy = UserName; milestone.LastModifiedById = UserId;
            }
        }
        if (value.ScheduleImpactDays is not null and not 0 && contract.EndDate.HasValue)
        {
            contract.EndDate = contract.EndDate.Value.AddDays(value.ScheduleImpactDays.Value);
            if (contract.StartDate.HasValue) contract.DurationDays = (int)(contract.EndDate.Value - contract.StartDate.Value).TotalDays;
        }
        contract.UpdatedAt = now; contract.UpdatedBy = UserName; contract.LastModifiedById = UserId;

        var revisedLines = BuildRevisedBoqLines(value, sourceVersion, now);
        var nextBoqVersion = await db.ProjectBoqVersions.Where(item => item.TenantId == TenantId && item.ProjectId == value.ProjectId && !item.IsDeleted)
            .Select(item => (int?)item.VersionNumber).MaxAsync(token) ?? 0;
        var revisedBoq = new ProjectBoqVersion
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = value.ProjectId, SourceVersionId = sourceVersion.Id,
            VersionNumber = nextBoqVersion + 1, VersionType = QuantitySurveyBoqVersionType.Revised,
            Status = ProjectBoqVersionStatuses.Draft, ApprovalStatus = ProjectBoqVersionStatuses.Draft,
            ChangeSummary = $"Approved variation {value.ReferenceNumber}: {value.Title}", AuditAction = QuantitySurveyAuditEventMap.CreateBoqVersion,
            LineCount = revisedLines.Count, SnapshotAt = now, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId),
            CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        };
        foreach (var line in revisedLines) line.ProjectBoqVersionId = revisedBoq.Id;
        revisedBoq.SnapshotHash = ComputeBoqSnapshotHash(revisedLines);
        db.ProjectBoqVersions.Add(revisedBoq); db.ProjectBoqVersionLines.AddRange(revisedLines);

        ProjectBudgetRevision? budgetRevision = null;
        var project = await db.Projects.SingleAsync(item => item.TenantId == TenantId && item.Id == value.ProjectId && !item.IsDeleted, token);
        var originalEstimatedBudget = project.EstimatedBudget;
        if (value.UpdateBudgetOnApplication)
        {
            if (await db.ProjectBudgetRevisions.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.ProjectId == value.ProjectId && !item.IsDeleted &&
                    (item.Status == "Draft" || item.Status == "PendingApproval"), token))
                throw Conflict("Complete the current project budget-revision workflow before applying this variation.");
            var latestBudget = await db.ProjectBudgetRevisions.AsNoTracking().Where(item => item.TenantId == TenantId && item.ProjectId == value.ProjectId && !item.IsDeleted && item.Status == "Approved")
                .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(token);
            var nextBudgetVersion = await db.ProjectBudgetRevisions.Where(item => item.TenantId == TenantId && item.ProjectId == value.ProjectId && !item.IsDeleted)
                .Select(item => (int?)item.VersionNumber).MaxAsync(token) ?? 0;
            decimal estimatedBudget; decimal approvedBudget;
            try
            {
                estimatedBudget = QuantitySurveyVariationRules.CalculateNonNegativeDownstreamValue(project.EstimatedBudget ?? latestBudget?.EstimatedBudget ?? 0m, value.ApprovedAmount.Value, "the estimated project budget");
                approvedBudget = QuantitySurveyVariationRules.CalculateNonNegativeDownstreamValue(project.ApprovedBudget ?? latestBudget?.ApprovedBudget ?? 0m, value.ApprovedAmount.Value, "the approved project budget");
            }
            catch (InvalidOperationException exception) { throw Conflict(exception.Message); }
            budgetRevision = new ProjectBudgetRevision
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = value.ProjectId, VersionNumber = nextBudgetVersion + 1,
                RevisionName = $"QS variation {value.ReferenceNumber}", RevisionType = "ApprovedVariation",
                EstimatedBudget = estimatedBudget, ApprovedBudget = approvedBudget,
                CommittedCost = latestBudget?.CommittedCost ?? 0m, ForecastCost = latestBudget?.ForecastCost ?? 0m,
                ThresholdWarningPercent = latestBudget?.ThresholdWarningPercent ?? 75m, ThresholdCriticalPercent = latestBudget?.ThresholdCriticalPercent ?? 90m,
                Status = "Approved", EffectiveDate = now.Date, SubmittedAt = value.SubmittedAt ?? now, ApprovedAt = now, ApprovedById = UserId,
                ChangeReason = applicationReason, Notes = $"QS-0511:{value.Id:N}", CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            };
            db.ProjectBudgetRevisions.Add(budgetRevision);
            project.EstimatedBudget = estimatedBudget; project.ApprovedBudget = approvedBudget; project.BudgetStatus = "Approved";
        }

        ProjectForecastVersion? forecastVersion = null;
        if (value.UpdateForecastOnApplication)
        {
            var forecasts = await db.ProjectForecastVersions.Where(item => item.TenantId == TenantId && item.ProjectId == value.ProjectId && !item.IsDeleted).ToListAsync(token);
            var active = forecasts.Where(item => item.IsActive).OrderByDescending(item => item.VersionNumber).FirstOrDefault();
            foreach (var item in forecasts.Where(item => item.IsActive)) { item.IsActive = false; item.UpdatedAt = now; item.UpdatedBy = UserName; item.LastModifiedById = UserId; }
            decimal forecastCost; decimal estimateAtCompletion;
            try
            {
                (forecastCost, estimateAtCompletion) = CalculateForecastVariation(active, originalEstimatedBudget, value.ApprovedAmount.Value);
            }
            catch (InvalidOperationException exception) { throw Conflict(exception.Message); }
            var forecastRevenue = active?.ForecastRevenue ?? revisedContractValue;
            forecastVersion = new ProjectForecastVersion
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = value.ProjectId,
                VersionNumber = forecasts.Select(item => item.VersionNumber).DefaultIfEmpty(0).Max() + 1,
                VersionName = $"QS variation {value.ReferenceNumber}", AsOfDate = now.Date,
                ForecastCost = forecastCost, EstimateAtCompletion = estimateAtCompletion,
                ForecastRevenue = forecastRevenue, ForecastMargin = Round(forecastRevenue - estimateAtCompletion), IsActive = true,
                Notes = $"QS-0511:{value.Id:N}", CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            };
            db.ProjectForecastVersions.Add(forecastVersion);
        }
        project.UpdatedAt = now; project.UpdatedBy = UserName; project.LastModifiedById = UserId;

        value.DownstreamApplicationStatus = ProjectVariationApplicationStatuses.AppliedPendingBoqApproval;
        value.ApplicationClientRequestId = clientRequestId; value.ApplicationRequestHash = requestHash;
        value.AppliedById = UserId; value.AppliedAt = now; value.ImplementedDate = now;
        value.ContractAmendmentId = amendment.Id; value.RevisedBoqVersionId = revisedBoq.Id;
        value.BudgetRevisionId = budgetRevision?.Id; value.ForecastVersionId = forecastVersion?.Id;
        value.ApplicationHash = Hash(new
        {
            value.Id, ApprovedAmount = Round(value.ApprovedAmount.Value), ContractAmendmentId = amendment.Id,
            ContractValue = revisedContractValue, RevisedBoqVersionId = revisedBoq.Id, revisedBoq.SnapshotHash,
            BudgetRevisionId = budgetRevision?.Id, ForecastVersionId = forecastVersion?.Id,
            value.UpdateContractSumOnApplication, value.UpdateBudgetOnApplication, value.UpdateForecastOnApplication, value.UpdateCertificateOnApplication
        });
    }

    private async Task<QuantitySurveyVariationDto> MutateAsync(Guid id, QuantitySurveyVariationActionRequest request, string action, string correlationId, CancellationToken token, Func<ProjectVariationOrder, Task> mutation)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var reason = RequiredText(request.Reason, 5, 2000, "Reason"); var requestHash = Hash(new { action, reason });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var value = await RequiredAsync(id, true, token); await RequireProjectAsync(value.ProjectId);
            if (value.LastMutationClientRequestId == request.ClientRequestId) { if (!FixedEquals(value.LastMutationRequestHash, requestHash)) throw RetryConflict(); await transaction.CommitAsync(token); return; }
            ApplyRowVersion(value, request.RowVersion);
            if (await db.QuantitySurveyVariationRevisions.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.ClientRequestId == request.ClientRequestId, token)) throw RetryConflict();
            var applicationWasPending = value.DownstreamApplicationStatus == ProjectVariationApplicationStatuses.NotApplied;
            var before = Snapshot(value); await mutation(value); value.LastMutationClientRequestId = request.ClientRequestId; value.LastMutationRequestHash = requestHash;
            value.CorrelationId = Correlation(correlationId); value.UpdatedAt = DateTime.UtcNow; value.UpdatedBy = UserName; value.LastModifiedById = UserId;
            AddRevision(value, request.ClientRequestId, requestHash, action, reason, before, Snapshot(value), correlationId); AddAudit(value, action, before, Snapshot(value), correlationId);
            var applicationTransition = applicationWasPending && value.DownstreamApplicationStatus != ProjectVariationApplicationStatuses.NotApplied;
            try
            {
                if (applicationTransition) await SetApplicationContextAsync(value, token);
                await SaveChangesAsync(token); await transaction.CommitAsync(token);
            }
            finally
            {
                if (applicationTransition) await ClearApplicationContextAsync(token);
            }
        });
        db.ChangeTracker.Clear(); return await GetAsync(id, token);
    }

    internal static string ResolveApplicationStatus(ProjectVariationOrder value) =>
        value.DownstreamApplicationStatus == ProjectVariationApplicationStatuses.AppliedPendingBoqApproval &&
        value.RevisedBoqVersion is { IsDeleted: false, Status: ProjectBoqVersionStatuses.Approved } revised &&
        revised.TenantId == value.TenantId && revised.ProjectId == value.ProjectId
            ? ProjectVariationApplicationStatuses.Applied : value.DownstreamApplicationStatus;

    internal static (decimal ForecastCost, decimal EstimateAtCompletion) CalculateForecastVariation(
        ProjectForecastVersion? active, decimal? originalEstimatedBudget, decimal approvedAmount) => (
        QuantitySurveyVariationRules.CalculateNonNegativeDownstreamValue(active?.ForecastCost ?? originalEstimatedBudget ?? 0m,
            approvedAmount, "the active project forecast"),
        QuantitySurveyVariationRules.CalculateNonNegativeDownstreamValue(active?.EstimateAtCompletion ?? active?.ForecastCost ?? originalEstimatedBudget ?? 0m,
            approvedAmount, "the estimate at completion"));

    internal static void ReconcileValuationLines(ApplicationDbContext context, ProjectVariationOrder entity,
        IReadOnlyList<QuantitySurveyVariationValuationLine> desired, Guid actorId, string actorName)
    {
        // The source-line key is unique even for soft-deleted rows. Preserve its identity
        // when editing or restoring a line, and explicitly add only genuinely new rows.
        var existing = entity.ValuationLines.ToDictionary(line => line.ProjectBoqVersionLineId);
        var selected = desired.Select(line => line.ProjectBoqVersionLineId).ToHashSet();
        var now = DateTime.UtcNow;
        foreach (var line in entity.ValuationLines.Where(line => !line.IsDeleted && !selected.Contains(line.ProjectBoqVersionLineId)))
        {
            line.IsDeleted = true; line.DeletedAt = now; line.DeletedBy = actorName;
            line.UpdatedAt = now; line.UpdatedBy = actorName; line.LastModifiedById = actorId;
        }
        foreach (var source in desired)
        {
            if (!existing.TryGetValue(source.ProjectBoqVersionLineId, out var line))
            {
                entity.ValuationLines.Add(source);
                context.QuantitySurveyVariationValuationLines.Add(source);
                continue;
            }
            line.BoqLineKey = source.BoqLineKey; line.Sequence = source.Sequence;
            line.LineReferenceSnapshot = source.LineReferenceSnapshot; line.DescriptionSnapshot = source.DescriptionSnapshot;
            line.UnitSnapshot = source.UnitSnapshot; line.QuantityChange = source.QuantityChange;
            line.UnitRate = source.UnitRate; line.Amount = source.Amount;
            line.ValuationReason = source.ValuationReason; line.SourceHash = source.SourceHash;
            line.IsDeleted = false; line.DeletedAt = null; line.DeletedBy = null;
            line.UpdatedAt = now; line.UpdatedBy = actorName; line.LastModifiedById = actorId;
        }
    }

    private async Task<List<QuantitySurveyVariationValuationLine>> BuildLinesAsync(ProjectVariationOrder entity, IReadOnlyList<SaveQuantitySurveyVariationLineRequest> requests, Guid versionId, string currency, CancellationToken token)
    {
        if (requests.Count > 500 || requests.GroupBy(value => value.ProjectBoqVersionLineId).Any(group => group.Count() > 1)) throw Validation("Select between 1 and 500 unique approved BoQ lines.");
        var ids = requests.Select(value => value.ProjectBoqVersionLineId).ToList();
        var sources = await db.ProjectBoqVersionLines.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == entity.ProjectId && value.ProjectBoqVersionId == versionId && ids.Contains(value.Id) && !value.IsDeleted && value.ItemType == ProjectBoqItemTypes.Item).ToDictionaryAsync(value => value.Id, token);
        if (sources.Count != ids.Count) throw Validation("Every valuation line must come from the selected published Approved BoQ.");
        var result = new List<QuantitySurveyVariationValuationLine>(); var sequence = 0;
        foreach (var request in requests)
        {
            var source = sources[request.ProjectBoqVersionLineId]; if (request.QuantityChange == 0) throw Validation("Variation quantity changes cannot be zero.");
            var rate = request.UnitRate ?? source.UnitRate ?? throw Validation($"BoQ line {source.LineNumber ?? source.ItemCode} has no controlled rate."); if (rate < 0) throw Validation("Variation rates cannot be negative.");
            var amount = QuantitySurveyVariationRules.CalculateAmount(request.QuantityChange, rate); var valuationReason = RequiredText(request.ValuationReason, 5, 1000, "Line valuation reason");
            result.Add(new QuantitySurveyVariationValuationLine { Id = Guid.NewGuid(), TenantId = TenantId, VariationOrderId = entity.Id, ProjectBoqVersionLineId = source.Id, BoqLineKey = source.LineKey, Sequence = ++sequence,
                LineReferenceSnapshot = source.LineNumber ?? source.ItemCode ?? source.Id.ToString(), DescriptionSnapshot = source.Description, UnitSnapshot = source.UnitOfMeasure,
                QuantityChange = request.QuantityChange, UnitRate = rate, Amount = amount, ValuationReason = valuationReason,
                SourceHash = Hash(new { source.Id, source.ProjectBoqVersionId, source.LineKey, source.Description, source.UnitOfMeasure, request.QuantityChange, rate, amount, currency }), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
        }
        return result;
    }

    private List<ProjectBoqVersionLine> BuildRevisedBoqLines(ProjectVariationOrder variation, ProjectBoqVersion sourceVersion, DateTime now)
    {
        var sourceLines = sourceVersion.Lines.Where(item => !item.IsDeleted).OrderBy(item => item.SortOrder).ThenBy(item => item.LineNumber).ToList();
        if (sourceLines.Count == 0) throw Conflict("The approved BoQ publication has no controlled lines.");
        var revised = sourceLines.Select(item => new ProjectBoqVersionLine
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = item.ProjectId, LineKey = item.LineKey,
            SourceBoqItemId = item.SourceBoqItemId, ProjectPackageId = item.ProjectPackageId,
            PackageCode = item.PackageCode, PackageName = item.PackageName, SectionCode = item.SectionCode, SectionName = item.SectionName,
            TradeCode = item.TradeCode, TradeName = item.TradeName, CostCode = item.CostCode, CostCodeName = item.CostCodeName,
            MeasurementStandard = item.MeasurementStandard, MeasurementCode = item.MeasurementCode, MeasurementRule = item.MeasurementRule,
            LineNumber = item.LineNumber, ItemCode = item.ItemCode, ItemType = item.ItemType, Description = item.Description,
            Quantity = item.Quantity, UnitOfMeasure = item.UnitOfMeasure, UnitRate = item.UnitRate, LineAmount = item.LineAmount,
            Currency = item.Currency, SortOrder = item.SortOrder, CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
        }).ToList();
        var sourceById = sourceLines.ToDictionary(item => item.Id);
        var revisedByKey = revised.ToDictionary(item => item.LineKey);
        var nextSort = revised.Select(item => item.SortOrder).DefaultIfEmpty(0).Max();
        var sequence = 0;
        foreach (var impact in variation.ValuationLines.Where(item => !item.IsDeleted).OrderBy(item => item.Sequence))
        {
            if (!sourceById.TryGetValue(impact.ProjectBoqVersionLineId, out var source) || !revisedByKey.TryGetValue(source.LineKey, out var target))
                throw Conflict("The approved variation contains a BoQ line outside its frozen publication.");
            var sourceRate = source.UnitRate;
            var rateMatches = sourceRate.HasValue &&
                decimal.Round(sourceRate.Value, 6, MidpointRounding.AwayFromZero) ==
                decimal.Round(impact.UnitRate, 6, MidpointRounding.AwayFromZero);
            if (impact.QuantityChange < 0m || rateMatches)
            {
                if (!rateMatches) throw Conflict("A negative variation quantity must use the approved BoQ line rate.");
                target.Quantity = decimal.Round(target.Quantity + impact.QuantityChange, 4, MidpointRounding.AwayFromZero);
                if (target.Quantity < 0m) throw Conflict($"Variation line {impact.LineReferenceSnapshot} would reduce the revised BoQ quantity below zero.");
                target.LineAmount = target.UnitRate.HasValue ? Round(target.Quantity * target.UnitRate.Value) : null;
                continue;
            }

            var suffix = $"-V{++sequence:00}";
            var reference = (source.LineNumber ?? source.ItemCode ?? "VAR");
            reference = reference[..Math.Min(reference.Length, 50 - suffix.Length)] + suffix;
            revised.Add(new ProjectBoqVersionLine
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = source.ProjectId, LineKey = Guid.NewGuid(),
                SourceBoqItemId = source.SourceBoqItemId, ProjectPackageId = source.ProjectPackageId,
                PackageCode = source.PackageCode, PackageName = source.PackageName, SectionCode = source.SectionCode, SectionName = source.SectionName,
                TradeCode = source.TradeCode, TradeName = source.TradeName, CostCode = source.CostCode, CostCodeName = source.CostCodeName,
                MeasurementStandard = source.MeasurementStandard, MeasurementCode = source.MeasurementCode, MeasurementRule = source.MeasurementRule,
                LineNumber = reference, ItemCode = source.ItemCode, ItemType = source.ItemType,
                Description = $"{source.Description} (Variation {variation.ReferenceNumber})", Quantity = impact.QuantityChange,
                UnitOfMeasure = source.UnitOfMeasure, UnitRate = impact.UnitRate, LineAmount = Round(impact.QuantityChange * impact.UnitRate),
                Currency = source.Currency, SortOrder = ++nextSort, CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            });
        }
        var sourceValue = Round(sourceLines.Sum(item => item.LineAmount ?? 0m));
        var revisedValue = Round(revised.Sum(item => item.LineAmount ?? 0m));
        if (Round(revisedValue - sourceValue) != Round(variation.ApprovedAmount ?? 0m))
            throw Conflict("The revised BoQ does not reconcile exactly to the approved variation amount.");
        return revised;
    }

    private async Task ValidateSourceAsync(Guid projectId, SaveQuantitySurveyVariationRequest request, CancellationToken token)
    {
        if (!QuantitySurveyVariationRules.HasValidSourceSelection(request.SourceType, request.SiteInstructionId, request.ChangeRequestId))
            throw Validation("Select exactly one governed source for a site instruction/change request, or no source for a direct variation/change order.");

        var valid = request.SourceType switch
        {
            QuantitySurveyVariationSourceType.SiteInstruction => request.SiteInstructionId.HasValue && !request.ChangeRequestId.HasValue && await db.ProjectSiteInstructions.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.Id == request.SiteInstructionId && !value.IsDeleted && value.Status != ProjectSiteInstructionStatuses.Draft && value.Status != ProjectSiteInstructionStatuses.Cancelled, token),
            QuantitySurveyVariationSourceType.ChangeRequest => request.ChangeRequestId.HasValue && !request.SiteInstructionId.HasValue && await db.ProjectChangeRequests.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.Id == request.ChangeRequestId && !value.IsDeleted && value.Status == "Approved", token),
            QuantitySurveyVariationSourceType.DirectVariation or QuantitySurveyVariationSourceType.ChangeOrder => !request.SiteInstructionId.HasValue && !request.ChangeRequestId.HasValue,
            _ => false
        };
        if (!valid) throw Validation("Select a valid governed site instruction/change request, or choose a direct variation/change order without a source record.");
    }

    private async Task ValidateFrozenAsync(ProjectVariationOrder value, CancellationToken token)
    {
        var contract = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.ContractId && !item.IsDeleted && item.Status == "Active" && item.ContractType == "Works", token)
            ?? throw Conflict("The frozen Works contract is no longer active.");
        if (contract.BusinessPartnerId != value.ContractorBusinessPartnerId || contract.Currency != value.Currency || contract.ContractValue != value.OriginalContractSumSnapshot) throw Conflict("The frozen contract or contractor values changed. Prepare a new variation revision.");
        var policy = await ResolvePolicyAsync(token);
        if (policy.ProfileId != value.ConfigurationProfileId || policy.DecisionId != value.VariationDecisionId || policy.WorkflowDefinitionId != value.ApprovalWorkflowDefinitionId || policy.EvidenceTemplateId != value.EvidenceMetadataTemplateId || !FixedEquals(policy.PolicyHash, value.PolicyHash)) throw Conflict("The effective variation policy changed. Return to Draft and refresh the variation.");
        var lineIds = value.ValuationLines.Where(line => !line.IsDeleted).Select(line => line.ProjectBoqVersionLineId).ToList();
        var sources = await db.ProjectBoqVersionLines.AsNoTracking().Where(line => line.TenantId == TenantId && line.ProjectBoqVersionId == value.ApprovedBoqVersionId && lineIds.Contains(line.Id) && !line.IsDeleted).ToDictionaryAsync(line => line.Id, token);
        if (sources.Count != lineIds.Count || value.ValuationLines.Any(line => !line.IsDeleted && (!sources.TryGetValue(line.ProjectBoqVersionLineId, out var source) || source.LineKey != line.BoqLineKey))) throw Conflict("The approved BoQ lineage changed. Prepare a new variation revision.");
    }

    private async Task<Policy> ResolvePolicyAsync(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var profiles = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted && value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && value.PublishedAt != null && value.EffectiveFrom <= now && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now)).OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No Published quantity-survey configuration is effective for this date.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault) throw Conflict("More than one quantity-survey configuration is effective for this date.");
        var profile = profiles[0]; var decision = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ProfileId == profile.Id && value.DecisionKey == "QS-DEC-011" && !value.IsDeleted, token) ?? throw Validation("The effective configuration has no QS-DEC-011 variation decision.");
        if (decision.Status != QuantitySurveyConfigurationDecisionStatus.Approved || decision.ApprovalStatus != QuantitySurveyConfigurationApprovalStatus.Approved || decision.EvidenceStatus != QuantitySurveyConfigurationEvidenceStatus.Verified || (decision.EffectiveFrom.HasValue && decision.EffectiveFrom > now) || (decision.EffectiveTo.HasValue && decision.EffectiveTo < now)) throw Validation("QS-DEC-011 is not approved, verified, and effective for this date.");
        QsVariationClaimsValue value; try { value = JsonSerializer.Deserialize<QsVariationClaimsValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); } catch (JsonException) { throw Conflict("QS-DEC-011 contains invalid variation policy data."); }
        if (value.VariationWorkflowDefinitionId == Guid.Empty || value.VariationEvidenceMetadataTemplateId == Guid.Empty) throw Validation("QS-DEC-011 must select the variation workflow and evidence DMS template.");
        var workflowValid = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).AnyAsync(item => item.TenantId == TenantId && item.Id == value.VariationWorkflowDefinitionId && !item.IsDeleted && item.IsActive && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.EntityType.IsDeleted && item.EntityType.IsActive && item.EntityType.Code == QuantitySurveyWorkflowBindingRegistry.Variation, token);
        if (!workflowValid) throw Validation("The QS-DEC-011 workflow must be active, Published, and bound to QS_VARIATION.");
        var dmsValid = await db.CentralDocumentMetadataTemplates.AsNoTracking().AnyAsync(item => item.TenantId == TenantId && item.Id == value.VariationEvidenceMetadataTemplateId && !item.IsDeleted && item.IsActive && item.PublishedAt != null, token);
        if (!dmsValid) throw Validation("The QS-DEC-011 variation evidence DMS template must be active and Published.");
        return new(profile.Id, decision.Id, value.VariationWorkflowDefinitionId, value.VariationEvidenceMetadataTemplateId, value, Hash(new { Profile = profile.Id, profile.Version, Decision = decision.Id, decision.ValueJson, value.VariationWorkflowDefinitionId, value.VariationEvidenceMetadataTemplateId }));
    }

    private IQueryable<ProjectVariationOrder> Query(bool tracked = false) { var query = db.ProjectVariationOrders.Include(value => value.Contract).ThenInclude(value => value!.BusinessPartner).Include(value => value.RevisedBoqVersion).Include(value => value.ValuationLines).Include(value => value.VariationEvidence).Where(value => value.TenantId == TenantId && value.IsQuantitySurveyGoverned && !value.IsDeleted); return tracked ? query : query.AsNoTracking(); }
    private async Task<ProjectVariationOrder> RequiredAsync(Guid id, bool tracked, CancellationToken token) => await Query(tracked).SingleOrDefaultAsync(value => value.Id == id, token) ?? throw new QuantitySurveyVariationNotFoundException("The governed variation was not found.");
    private async Task RequireProjectAsync(Guid projectId) { if (!await projectService.HasProjectAccessAsync(projectId)) throw new UnauthorizedAccessException("You are not permitted to access the selected project."); }
    private async Task<string> NextNumberAsync(CancellationToken token) { var year = DateTime.UtcNow.Year; var count = await db.ProjectVariationOrders.IgnoreQueryFilters().CountAsync(value => value.TenantId == TenantId && value.IsQuantitySurveyGoverned && value.RequestedDate.Year == year, token); return $"VO-{year}-{count + 1:00000}"; }
    private void AddRevision(ProjectVariationOrder value, Guid clientRequestId, string requestHash, string action, string reason, object? before, object after, string correlationId) => db.QuantitySurveyVariationRevisions.Add(new() { Id = Guid.NewGuid(), TenantId = TenantId, VariationOrderId = value.Id, ClientRequestId = clientRequestId, RequestHash = requestHash, Action = action, ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = reason, BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private void AddAudit(ProjectVariationOrder value, string action, object? before, object after, string correlationId) => db.AuditLogs.Add(new AuditLog { TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(ProjectVariationOrder), ResourceId = value.Id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions), IpAddress = "api", UserAgent = "QuantitySurvey", Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId });
    private async Task SaveChangesAsync(CancellationToken token) { try { await db.SaveChangesAsync(token); } catch (DbUpdateConcurrencyException) { throw Conflict("The variation changed. Refresh and retry."); } catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("IX_", StringComparison.OrdinalIgnoreCase) == true || exception.InnerException?.Message.Contains("5189", StringComparison.OrdinalIgnoreCase) == true) { throw Conflict("The variation conflicts with an existing governed source or lifecycle rule."); } }
    private static QuantitySurveyVariationDto Map(ProjectVariationOrder value) => new() { Id = value.Id, ProjectId = value.ProjectId, ContractId = value.ContractId!.Value, ReferenceNumber = value.ReferenceNumber ?? string.Empty, Title = value.Title, VariationType = value.VariationType, SourceType = value.VariationSourceType, SiteInstructionId = value.SiteInstructionId, ChangeRequestId = value.ChangeRequestId, Status = value.Status, ApprovalStatus = value.ApprovalStatus, ContractNumber = value.Contract?.ContractNumber ?? string.Empty, ContractorName = value.Contract?.BusinessPartner?.PartnerName ?? string.Empty, Currency = value.Currency, ValuedAmount = value.EstimatedAmount ?? 0m, ApprovedAmount = value.ApprovedAmount, OriginalContractSum = value.OriginalContractSumSnapshot ?? 0m, RevisedContractSum = value.RevisedContractSumSnapshot, DownstreamApplicationStatus = ResolveApplicationStatus(value), ContractAmendmentId = value.ContractAmendmentId, RevisedBoqVersionId = value.RevisedBoqVersionId, RevisedBoqVersionNumber = value.RevisedBoqVersion?.VersionNumber, RevisedBoqStatus = value.RevisedBoqVersion?.Status, BudgetRevisionId = value.BudgetRevisionId, ForecastVersionId = value.ForecastVersionId, AppliedAt = value.AppliedAt, CertificateEligible = value.UpdateCertificateOnApplication && value.RevisedBoqVersion?.Status == ProjectBoqVersionStatuses.Approved, ScheduleImpactDays = value.ScheduleImpactDays ?? 0, WorkflowInstanceId = value.WorkflowInstanceId, RejectionReason = value.RejectionReason, RowVersion = Convert.ToBase64String(value.RowVersion), Lines = value.ValuationLines.Where(line => !line.IsDeleted).OrderBy(line => line.Sequence).Select(line => new QuantitySurveyVariationLineDto { Id = line.Id, ProjectBoqVersionLineId = line.ProjectBoqVersionLineId, Reference = line.LineReferenceSnapshot, Description = line.DescriptionSnapshot, Unit = line.UnitSnapshot, QuantityChange = line.QuantityChange, UnitRate = line.UnitRate, Amount = line.Amount, ValuationReason = line.ValuationReason }).ToList(), Evidence = value.VariationEvidence.Where(item => !item.IsDeleted).OrderBy(item => item.CreatedAt).Select(MapEvidence).ToList() };
    private static QuantitySurveyVariationEvidenceDto MapEvidence(QuantitySurveyVariationEvidence value) => new() { Id = value.Id, Title = value.Title, FileName = value.OriginalFileName, ContentType = value.ContentType, FileSize = value.FileSize, ChecksumSha256 = value.ChecksumSha256, CentralDocumentRecordId = value.CentralDocumentRecordId, CentralDocumentVersionId = value.CentralDocumentVersionId };
    private static object Snapshot(ProjectVariationOrder value) => new { value.Id, value.ProjectId, value.ContractId, value.ReferenceNumber, value.Title, value.VariationType, value.VariationSourceType, value.SiteInstructionId, value.ChangeRequestId, value.Status, value.ApprovalStatus, value.EstimatedAmount, value.ApprovedAmount, value.ScheduleImpactDays, value.OriginalContractSumSnapshot, value.RevisedContractSumSnapshot, value.DownstreamApplicationStatus, value.ApplicationClientRequestId, value.ApplicationHash, value.AppliedById, value.AppliedAt, value.ContractAmendmentId, value.RevisedBoqVersionId, value.BudgetRevisionId, value.ForecastVersionId, value.ConfigurationProfileId, value.VariationDecisionId, value.ApprovalWorkflowDefinitionId, value.EvidenceMetadataTemplateId, value.PolicyHash, value.SubmittedById, value.ApprovedById, value.WorkflowInstanceId, Lines = value.ValuationLines.Where(line => !line.IsDeleted).OrderBy(line => line.Sequence).Select(line => new { line.ProjectBoqVersionLineId, line.BoqLineKey, line.QuantityChange, line.UnitRate, line.Amount, line.SourceHash }) };

    private static string ComputeBoqSnapshotHash(IEnumerable<ProjectBoqVersionLine> lines)
    {
        var canonical = lines.OrderBy(item => item.SortOrder).ThenBy(item => item.LineNumber, StringComparer.Ordinal).ThenBy(item => item.LineKey).Select(item => new
        {
            item.LineKey, item.SourceBoqItemId, item.ProjectPackageId, item.PackageCode, item.PackageName, item.SectionCode, item.SectionName,
            item.TradeCode, item.TradeName, item.CostCode, item.CostCodeName, item.MeasurementStandard, item.MeasurementCode,
            item.MeasurementRule, item.LineNumber, item.ItemCode, item.ItemType, item.Description, item.Quantity, item.UnitOfMeasure,
            item.UnitRate, item.LineAmount, item.Currency, item.SortOrder
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
    }
    private async Task SetApplicationContextAsync(ProjectVariationOrder value, CancellationToken token)
    {
        await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'qs_variation_application_id', @value={0};", [value.Id.ToString()], token);
        await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'qs_variation_application_actor', @value={0};", [UserId.ToString()], token);
        await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'qs_variation_application_hash', @value={0};", [value.ApplicationHash ?? string.Empty], token);
    }
    private async Task ClearApplicationContextAsync(CancellationToken token)
    {
        await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'qs_variation_application_id', @value=NULL;", token);
        await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'qs_variation_application_actor', @value=NULL;", token);
        await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'qs_variation_application_hash', @value=NULL;", token);
    }
    private static void ApplyRowVersion(ProjectVariationOrder value, string encoded) { byte[] expected; try { expected = Convert.FromBase64String(encoded); } catch (FormatException) { throw Validation("The variation row version is invalid. Refresh and retry."); } if (expected.Length != value.RowVersion.Length || !CryptographicOperations.FixedTimeEquals(expected, value.RowVersion)) throw Conflict("The variation changed. Refresh and retry."); }
    private static string NormalizeType(string value) => value.Trim() switch { ProjectVariationOrderTypes.ScopeChange => ProjectVariationOrderTypes.ScopeChange, ProjectVariationOrderTypes.QuantityAdjustment => ProjectVariationOrderTypes.QuantityAdjustment, ProjectVariationOrderTypes.ProvisionalSum => ProjectVariationOrderTypes.ProvisionalSum, ProjectVariationOrderTypes.RateChange => ProjectVariationOrderTypes.RateChange, ProjectVariationOrderTypes.Omission => ProjectVariationOrderTypes.Omission, ProjectVariationOrderTypes.Daywork => ProjectVariationOrderTypes.Daywork, ProjectVariationOrderTypes.AdditionalWork => ProjectVariationOrderTypes.AdditionalWork, ProjectVariationOrderTypes.SiteInstruction => ProjectVariationOrderTypes.SiteInstruction, ProjectVariationOrderTypes.ChangeOrder => ProjectVariationOrderTypes.ChangeOrder, _ => ProjectVariationOrderTypes.Other };
    private static string RequiredText(string? value, int min, int max, string label) => string.IsNullOrWhiteSpace(value) || value.Trim().Length < min || value.Trim().Length > max ? throw Validation($"{label} must contain {min} to {max} characters.") : value.Trim();
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right) { if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false; var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right); return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); }
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static QuantitySurveyVariationValidationException Validation(string message) => new(message);
    private static QuantitySurveyVariationConflictException Conflict(string message) => new(message);
    private static QuantitySurveyVariationConflictException RetryConflict() => Conflict("This client request identifier is already bound to different variation inputs.");
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId, Guid EvidenceTemplateId, QsVariationClaimsValue Value, string PolicyHash);
}
