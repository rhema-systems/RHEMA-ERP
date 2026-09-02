using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyMaterialReconciliationService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : IQuantitySurveyMaterialReconciliationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private Guid TenantId => currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
        ? tenantId : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var userId) && userId != Guid.Empty
        ? userId : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyMaterialReconciliationWorkspaceDto> GetWorkspaceAsync(
        Guid projectId, bool external = false, CancellationToken token = default)
    {
        Guid? partnerId = null;
        if (external) partnerId = (await RequireExternalProjectAsync(projectId, false, null, token)).BusinessPartnerId;
        else await RequireProjectAsync(projectId);

        var valuationQuery = db.QuantitySurveyValuationWorksheets.AsNoTracking()
            .Include(value => value.ProjectInterimValuation).ThenInclude(value => value.Contract).ThenInclude(value => value!.BusinessPartner)
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                            value.ProjectInterimValuation.ContractId != null && value.ContractorBusinessPartnerId != null);
        if (partnerId.HasValue) valuationQuery = valuationQuery.Where(value => value.ContractorBusinessPartnerId == partnerId.Value);
        var valuations = await valuationQuery.OrderByDescending(value => value.ProjectInterimValuation.ValuationDate)
            .Select(value => new QuantitySurveyMaterialValuationLookupDto(
                value.Id, value.ProjectInterimValuationId, value.ProjectInterimValuation.ContractId!.Value,
                (value.ProjectInterimValuation.ValuationNumber ?? "Valuation") + " - " + value.ProjectInterimValuation.Title,
                value.ProjectInterimValuation.Contract!.ContractNumber,
                value.ProjectInterimValuation.Contract.BusinessPartner.PartnerName,
                value.ProjectInterimValuation.Currency, value.Status)).ToListAsync(token);

        var currencyCodes = valuations.Select(value => value.Currency).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var today = DateTime.UtcNow;
        var rateRows = await db.QuantitySurveyRateLibraryRates.AsNoTracking()
            .Include(value => value.RateLibraryItem).ThenInclude(value => value.InventoryItem)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                            value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published &&
                            value.EffectiveFrom <= today && (!value.EffectiveTo.HasValue || value.EffectiveTo >= today) &&
                            value.RateLibraryItem.InventoryItemId != null && value.RateLibraryItem.IsActive &&
                            !value.RateLibraryItem.IsDeleted && value.RateLibraryItem.InventoryItem != null &&
                            !value.RateLibraryItem.InventoryItem.IsDeleted && value.RateLibraryItem.InventoryItem.Status == ItemStatus.Active)
            .OrderByDescending(value => value.Version).ToListAsync(token);
        var materialSources = rateRows.Where(value => currencyCodes.Count == 0 || currencyCodes.Contains(value.CurrencyCodeSnapshot, StringComparer.OrdinalIgnoreCase))
            .GroupBy(value => new { value.RateLibraryItem.InventoryItemId, Currency = value.CurrencyCodeSnapshot.ToUpperInvariant() })
            .Select(group => group.First()).Select(value => new QuantitySurveyMaterialSourceLookupDto(
                value.RateLibraryItem.InventoryItemId!.Value,
                value.RateLibraryItem.InventoryItem!.ItemCode, value.RateLibraryItem.InventoryItem.Name,
                value.RateLibraryItem.InventoryItem.UnitOfMeasure, value.Id, value.UnitRate, value.CurrencyCodeSnapshot))
            .OrderBy(value => value.ItemCode).ToList();

        var inventoryIssues = await db.Set<InventoryIssueVoucherLine>().AsNoTracking()
            .Include(value => value.InventoryIssueVoucher).Include(value => value.InventoryItem)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                            value.InventoryIssueVoucher.TenantId == TenantId && !value.InventoryIssueVoucher.IsDeleted &&
                            value.InventoryIssueVoucher.ProjectId == projectId &&
                            value.InventoryIssueVoucher.Status == InventoryIssueVoucherStatus.Acknowledged)
            .OrderByDescending(value => value.InventoryIssueVoucher.AcknowledgedAtUtc)
            .Select(value => new QuantitySurveyMaterialIssueLookupDto(value.Id, value.InventoryItemId,
                value.InventoryIssueVoucher.VoucherNumber, value.InventoryItem.ItemCode, value.InventoryItem.Name,
                value.UnitOfMeasure ?? value.InventoryItem.UnitOfMeasure, value.Quantity, value.UnitCost,
                value.TotalValue, value.IntegrityHash)).ToListAsync(token);

        var worksheetIds = valuations.Select(value => value.WorksheetId).ToList();
        var evidence = await db.QuantitySurveyValuationWorksheetEvidence.AsNoTracking()
            .Where(value => value.TenantId == TenantId && worksheetIds.Contains(value.WorksheetId) && !value.IsDeleted &&
                            (value.EvidenceType == QuantitySurveyValuationEvidenceType.MaterialOnSite ||
                             value.EvidenceType == QuantitySurveyValuationEvidenceType.MaterialOffSite))
            .OrderBy(value => value.Title).Select(value => new QuantitySurveyMaterialEvidenceLookupDto(
                value.Id, value.WorksheetId, value.EvidenceType, value.Title, value.OriginalFileName,
                value.ChecksumSha256)).ToListAsync(token);
        var reconciliations = await Query().Where(value => value.ProjectId == projectId).OrderByDescending(value => value.PreparedAt).ToListAsync(token);
        return new()
        {
            Valuations = valuations, MaterialSources = materialSources, InventoryIssues = inventoryIssues,
            Evidence = evidence, Reconciliations = reconciliations.Select(Map).ToList()
        };
    }

    public async Task<QuantitySurveyMaterialReconciliationDto> GetAsync(
        Guid id, bool external = false, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        if (external)
        {
            var actor = await RequireExternalProjectAsync(entity.ProjectId, false, entity.ValuationWorksheetId, token);
            if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId)
                throw new UnauthorizedAccessException("This material reconciliation is assigned to a different contractor.");
        }
        else await RequireProjectAsync(entity.ProjectId);
        return Map(entity);
    }

    public async Task<QuantitySurveyMaterialReconciliationDto> SaveAsync(
        Guid projectId, SaveQuantitySurveyMaterialReconciliationRequest request, string correlationId,
        CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || request.ValuationWorksheetId == Guid.Empty || request.Lines.Count == 0)
            throw Validation("Select an interim valuation and at least one controlled material source.");
        await RequireProjectAsync(projectId);
        var reason = RequiredReason(request.Reason);
        var requestHash = Hash(new { projectId, request.ValuationWorksheetId, request.Notes, reason, request.Lines });
        Guid id = Guid.Empty;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var retry = await db.QuantitySurveyMaterialReconciliationRevisions.AsNoTracking()
                .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
            if (retry is not null)
            {
                if (!FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
                id = retry.ReconciliationId; await transaction.CommitAsync(token); return;
            }

            var worksheet = await db.QuantitySurveyValuationWorksheets
                .Include(value => value.ProjectInterimValuation).ThenInclude(value => value.Contract).ThenInclude(value => value!.BusinessPartner)
                .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.ValuationWorksheetId &&
                                               value.ProjectId == projectId && !value.IsDeleted, token)
                ?? throw Validation("Select an interim valuation worksheet from this project.");
            var contract = worksheet.ProjectInterimValuation.Contract;
            if (contract is null || worksheet.ContractorBusinessPartnerId != contract.BusinessPartnerId ||
                contract.ContractType != "Works" || contract.Status != "Active")
                throw Conflict("The valuation must be linked to its active Works contract and contractor.");
            var policy = await ResolvePolicyAsync(token);
            var existing = await db.QuantitySurveyMaterialReconciliations.Include(value => value.Lines)
                .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.ValuationWorksheetId == worksheet.Id && !value.IsDeleted, token);
            if (existing is not null && existing.Status is not (QuantitySurveyMaterialReconciliationStatuses.Draft or QuantitySurveyMaterialReconciliationStatuses.Rejected))
                throw Conflict("Only a Draft or Rejected material reconciliation can be amended.");
            if (existing is not null && !string.IsNullOrWhiteSpace(request.RowVersion)) ApplyRowVersion(existing, request.RowVersion);

            var before = existing is null ? null : Snapshot(existing);
            var entity = existing ?? new QuantitySurveyMaterialReconciliation
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ContractId = contract.Id,
                ValuationWorksheetId = worksheet.Id, ContractorBusinessPartnerId = contract.BusinessPartnerId,
                ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
                ReconciliationNumber = await NextNumberAsync(token), PreparedById = UserId, PreparedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
            };
            if (existing is null) db.QuantitySurveyMaterialReconciliations.Add(entity);
            else db.QuantitySurveyMaterialReconciliationLines.RemoveRange(entity.Lines);
            entity.ProjectId = projectId; entity.ContractId = contract.Id; entity.ValuationWorksheetId = worksheet.Id;
            entity.ContractorBusinessPartnerId = contract.BusinessPartnerId;
            entity.ContractNumberSnapshot = contract.ContractNumber; entity.ContractorNameSnapshot = contract.BusinessPartner.PartnerName;
            entity.CurrencyCodeSnapshot = worksheet.ProjectInterimValuation.Currency.ToUpperInvariant();
            entity.ConfigurationProfileId = policy.ProfileId; entity.MaterialDecisionId = policy.DecisionId;
            entity.ApprovalWorkflowDefinitionId = policy.WorkflowDefinitionId; entity.PolicyHash = policy.PolicyHash;
            entity.ValuationBasis = policy.Value.ValuationBasis;
            entity.InventoryReconciliationRequired = policy.Value.RequireInventoryReconciliation;
            entity.Status = QuantitySurveyMaterialReconciliationStatuses.Draft; entity.ApprovalStatus = "Draft";
            entity.ContractorConfirmedById = null; entity.ContractorConfirmedAt = null; entity.ContractorConfirmationHash = null;
            entity.SubmittedById = null; entity.SubmittedAt = null; entity.ApprovedById = null; entity.ApprovedAt = null;
            entity.WorkflowInstanceId = null; entity.RejectionReason = null; entity.Notes = Clean(request.Notes);
            entity.CorrelationId = Correlation(correlationId); entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;

            var lines = await BuildLinesAsync(entity, request.Lines, policy, token);
            entity.Lines = lines;
            entity.MaterialOnSiteAmount = Round(lines.Where(value => value.LineType == QuantitySurveyMaterialLineType.MaterialOnSite).Sum(value => value.TotalValue));
            entity.MaterialOffSiteAmount = Round(lines.Where(value => value.LineType == QuantitySurveyMaterialLineType.MaterialOffSite).Sum(value => value.TotalValue));
            entity.TdcSuppliedDeductionAmount = Round(lines.Where(value => value.LineType == QuantitySurveyMaterialLineType.TdcSuppliedMaterial).Sum(value => value.TotalValue));
            id = entity.Id;
            AddRevision(entity, request.ClientRequestId, requestHash, QuantitySurveyAuditEventMap.PrepareMaterialReconciliation,
                reason, before, Snapshot(entity), correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.PrepareMaterialReconciliation, before, Snapshot(entity), correlationId);
            await SaveChangesAsync(token); await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear(); return await GetAsync(id, false, token);
    }

    public async Task<QuantitySurveyMaterialReconciliationDto> ConfirmAsync(
        Guid id, QuantitySurveyMaterialContractorConfirmationRequest request, bool external, string correlationId,
        CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token);
        var actor = external
            ? await RequireExternalProjectAsync(entity.ProjectId, true, entity.ValuationWorksheetId, token)
            : throw new UnauthorizedAccessException("Contractor confirmation must be completed by the assigned external contractor.");
        if (actor.BusinessPartnerId != entity.ContractorBusinessPartnerId)
            throw new UnauthorizedAccessException("Only the assigned contractor can confirm this reconciliation.");
        var attestation = RequiredText(request.Attestation, 10, 1000, "Contractor attestation");
        await MutateAsync(entity, request, QuantitySurveyAuditEventMap.ConfirmMaterialReconciliation, correlationId, token, async value =>
        {
            if (value.Status != QuantitySurveyMaterialReconciliationStatuses.Draft)
                throw Conflict("Only a Draft material reconciliation can be confirmed by the contractor.");
            await ValidateFrozenAsync(value, token);
            value.Status = QuantitySurveyMaterialReconciliationStatuses.ContractorConfirmed;
            value.ApprovalStatus = "Draft"; value.ContractorConfirmedById = UserId;
            value.ContractorConfirmedAt = DateTime.UtcNow;
            value.ContractorConfirmationHash = Hash(new { value.Id, value.PolicyHash, Lines = value.Lines.Select(line => line.SourceHash), attestation, actor.BusinessPartnerId });
        });
        return await GetAsync(id, true, token);
    }

    public async Task<QuantitySurveyMaterialReconciliationDto> SubmitAsync(
        Guid id, QuantitySurveyMaterialReconciliationActionRequest request, string correlationId, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token); await RequireProjectAsync(entity.ProjectId);
        await MutateAsync(entity, request, QuantitySurveyAuditEventMap.SubmitMaterialReconciliation, correlationId, token, async value =>
        {
            if (value.Status != QuantitySurveyMaterialReconciliationStatuses.ContractorConfirmed)
                throw Conflict("The assigned contractor must confirm the reconciliation before submission.");
            await ValidateFrozenAsync(value, token);
            var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.MaterialDeduction, value.Id, value.ApprovalWorkflowDefinitionId);
            if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The material-deduction workflow could not be started.");
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.MaterialDeduction).ApplySubmitOutcome(value, result.Outcome, UserId);
            value.Status = QuantitySurveyMaterialReconciliationStatuses.PendingApproval; value.ApprovalStatus = "Pending";
            value.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId; value.SubmittedById = UserId; value.SubmittedAt = DateTime.UtcNow;
        });
        return await GetAsync(id, false, token);
    }

    public Task<QuantitySurveyMaterialReconciliationDto> ApproveAsync(Guid id,
        QuantitySurveyMaterialReconciliationActionRequest request, string correlationId, CancellationToken token = default)
        => DecideAsync(id, request, true, correlationId, token);

    public Task<QuantitySurveyMaterialReconciliationDto> RejectAsync(Guid id,
        QuantitySurveyMaterialReconciliationActionRequest request, string correlationId, CancellationToken token = default)
        => DecideAsync(id, request, false, correlationId, token);

    public async Task<IReadOnlyList<QuantitySurveyMaterialReconciliationRevisionDto>> HistoryAsync(
        Guid id, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token); await RequireProjectAsync(entity.ProjectId);
        return await db.QuantitySurveyMaterialReconciliationRevisions.AsNoTracking().Where(value =>
                value.TenantId == TenantId && value.ReconciliationId == id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt).Select(value => new QuantitySurveyMaterialReconciliationRevisionDto
            {
                Id = value.Id, Action = value.Action, ActorName = value.ActorName, ActorRoles = value.ActorRoles,
                CorrelationId = value.CorrelationId, Reason = value.Reason, BeforeJson = value.BeforeJson,
                AfterJson = value.AfterJson, CreatedAt = value.CreatedAt
            }).ToListAsync(token);
    }

    private async Task<QuantitySurveyMaterialReconciliationDto> DecideAsync(
        Guid id, QuantitySurveyMaterialReconciliationActionRequest request, bool approve, string correlationId,
        CancellationToken token)
    {
        var entity = await RequiredAsync(id, true, token); await RequireProjectAsync(entity.ProjectId);
        await MutateAsync(entity, request,
            approve ? QuantitySurveyAuditEventMap.ApproveMaterialReconciliation : QuantitySurveyAuditEventMap.RejectMaterialReconciliation,
            correlationId, token, async value =>
            {
                if (value.Status != QuantitySurveyMaterialReconciliationStatuses.PendingApproval)
                    throw Conflict("The material reconciliation must be PendingApproval before a decision.");
                if (value.PreparedById == UserId || value.SubmittedById == UserId || value.ContractorConfirmedById == UserId)
                    throw Conflict("Maker-checker control prevents a preparer, contractor confirmer, or submitter from deciding the reconciliation.");
                await ValidateFrozenAsync(value, token);
                if (!value.WorkflowInstanceId.HasValue) throw Conflict("The shared workflow instance is missing.");
                var status = await db.WorkflowInstances.AsNoTracking().Where(item => item.TenantId == TenantId &&
                        item.Id == value.WorkflowInstanceId.Value && !item.IsDeleted)
                    .Select(item => (WorkflowInstanceStatus?)item.Status).SingleOrDefaultAsync(token)
                    ?? throw Conflict("The shared workflow instance is unavailable.");
                WorkflowOutcome outcome;
                if (approve && status == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
                else if (!approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) outcome = WorkflowOutcome.Rejected;
                else
                {
                    if (!approve && status == WorkflowInstanceStatus.Completed) throw Conflict("A completed workflow is approved and cannot be rejected.");
                    if (approve && status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) throw Conflict("The workflow ended without approval.");
                    if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.MaterialDeduction, value.Id, UserId))
                        throw new UnauthorizedAccessException("You are not assigned to the current material-deduction approval step.");
                    var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.MaterialDeduction,
                        value.Id, UserId, approve ? "Approve" : "Reject", RequiredReason(request.Reason));
                    if (!result.ExecutionResult.Success) throw Conflict(result.ExecutionResult.Message ?? "The material-deduction workflow decision failed.");
                    outcome = result.Outcome;
                }
                if (outcome != (approve ? WorkflowOutcome.Approved : WorkflowOutcome.Rejected))
                    throw Conflict("The shared workflow has not reached the requested final outcome.");
                workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.MaterialDeduction)
                    .ApplyApprovalOutcome(value, outcome, UserId, approve ? null : RequiredReason(request.Reason));
                value.Status = approve ? QuantitySurveyMaterialReconciliationStatuses.Approved : QuantitySurveyMaterialReconciliationStatuses.Rejected;
                value.ApprovalStatus = approve ? "Approved" : "Rejected";
                value.ApprovedById = approve ? UserId : null; value.ApprovedAt = approve ? DateTime.UtcNow : null;
                value.RejectionReason = approve ? null : RequiredReason(request.Reason);
            });
        return await GetAsync(id, false, token);
    }

    private async Task MutateAsync(QuantitySurveyMaterialReconciliation entity,
        QuantitySurveyMaterialReconciliationActionRequest request, string action, string correlationId,
        CancellationToken token, Func<QuantitySurveyMaterialReconciliation, Task> mutate)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var reason = RequiredReason(request.Reason);
        var requestHash = Hash(new
        {
            action,
            reason,
            Attestation = request is QuantitySurveyMaterialContractorConfirmationRequest confirmation
                ? confirmation.Attestation
                : null
        });
        var entityId = entity.Id;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var current = await RequiredAsync(entityId, true, token);
            if (current.LastMutationClientRequestId == request.ClientRequestId)
            {
                if (!FixedEquals(current.LastMutationRequestHash, requestHash)) throw RetryConflict();
                await transaction.CommitAsync(token);
                return;
            }
            ApplyRowVersion(current, request.RowVersion);
            var duplicate = await db.QuantitySurveyMaterialReconciliationRevisions.AsNoTracking().FirstOrDefaultAsync(value =>
                value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
            if (duplicate is not null) throw RetryConflict();
            var before = Snapshot(current); await mutate(current);
            current.LastMutationClientRequestId = request.ClientRequestId; current.LastMutationRequestHash = requestHash;
            current.CorrelationId = Correlation(correlationId); current.UpdatedAt = DateTime.UtcNow;
            current.UpdatedBy = UserName; current.LastModifiedById = UserId;
            AddRevision(current, request.ClientRequestId, requestHash, action, reason, before, Snapshot(current), correlationId);
            AddAudit(current, action, before, Snapshot(current), correlationId);
            await SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        });
    }

    private async Task<List<QuantitySurveyMaterialReconciliationLine>> BuildLinesAsync(
        QuantitySurveyMaterialReconciliation entity,
        IReadOnlyList<SaveQuantitySurveyMaterialReconciliationLineRequest> requests,
        Policy policy, CancellationToken token)
    {
        if (requests.Count > 500) throw Validation("A material reconciliation cannot contain more than 500 lines.");
        if (requests.GroupBy(value => new { value.LineType, value.InventoryIssueVoucherLineId, value.ValuationEvidenceId }).Any(group => group.Count() > 1))
            throw Validation("A controlled material source can appear only once in a reconciliation.");
        var itemIds = requests.Select(value => value.InventoryItemId).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Where(value => value.TenantId == TenantId &&
            itemIds.Contains(value.Id) && !value.IsDeleted && value.Status == ItemStatus.Active).ToDictionaryAsync(value => value.Id, token);
        if (items.Count != itemIds.Count) throw Validation("Every selected material must be an active Inventory item in this tenant.");
        var rateIds = requests.Where(value => value.ApprovedRateId.HasValue).Select(value => value.ApprovedRateId!.Value).Distinct().ToList();
        var rates = await db.QuantitySurveyRateLibraryRates.AsNoTracking().Include(value => value.RateLibraryItem)
            .Where(value => value.TenantId == TenantId && rateIds.Contains(value.Id) && !value.IsDeleted &&
                            value.LifecycleStatus == QuantitySurveyRateLifecycleStatus.Published && value.EffectiveFrom <= DateTime.UtcNow &&
                            (!value.EffectiveTo.HasValue || value.EffectiveTo >= DateTime.UtcNow))
            .ToDictionaryAsync(value => value.Id, token);
        var evidenceIds = requests.Where(value => value.ValuationEvidenceId.HasValue).Select(value => value.ValuationEvidenceId!.Value).Distinct().ToList();
        var evidence = await db.QuantitySurveyValuationWorksheetEvidence.AsNoTracking().Where(value => value.TenantId == TenantId &&
            evidenceIds.Contains(value.Id) && value.WorksheetId == entity.ValuationWorksheetId && !value.IsDeleted).ToDictionaryAsync(value => value.Id, token);
        var issueIds = requests.Where(value => value.InventoryIssueVoucherLineId.HasValue).Select(value => value.InventoryIssueVoucherLineId!.Value).Distinct().ToList();
        var issues = await db.Set<InventoryIssueVoucherLine>().AsNoTracking().Include(value => value.InventoryIssueVoucher)
            .Where(value => value.TenantId == TenantId && issueIds.Contains(value.Id) && !value.IsDeleted &&
                            value.InventoryIssueVoucher.TenantId == TenantId && !value.InventoryIssueVoucher.IsDeleted &&
                            value.InventoryIssueVoucher.ProjectId == entity.ProjectId &&
                            value.InventoryIssueVoucher.Status == InventoryIssueVoucherStatus.Acknowledged)
            .ToDictionaryAsync(value => value.Id, token);

        var lines = new List<QuantitySurveyMaterialReconciliationLine>();
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var item = items[request.InventoryItemId];
            QuantitySurveyRateLibraryRate? rate = null;
            if (request.ApprovedRateId.HasValue)
            {
                if (!rates.TryGetValue(request.ApprovedRateId.Value, out rate) || rate.RateLibraryItem.InventoryItemId != item.Id ||
                    !string.Equals(rate.CurrencyCodeSnapshot, entity.CurrencyCodeSnapshot, StringComparison.OrdinalIgnoreCase))
                    throw Validation($"Line {index + 1} must use a current approved rate for the selected Inventory item and contract currency.");
            }
            decimal quantity; decimal? deliveredCost; Guid? evidenceId = null; Guid? issueId = null;
            string? issueNumber = null; string? issueHash = null; Guid? docRecord = null; Guid? docVersion = null; string? checksum = null;
            if (request.LineType == QuantitySurveyMaterialLineType.TdcSuppliedMaterial)
            {
                if (!policy.Value.DeductTdcSuppliedMaterials) throw Validation("The effective QS policy does not permit TDC-supplied material deductions.");
                if (!request.InventoryIssueVoucherLineId.HasValue || !issues.TryGetValue(request.InventoryIssueVoucherLineId.Value, out var issue) ||
                    issue.InventoryItemId != item.Id)
                    throw Validation($"Line {index + 1} must select an acknowledged Inventory issue for this project and item.");
                quantity = issue.Quantity; deliveredCost = issue.UnitCost; issueId = issue.Id;
                issueNumber = issue.InventoryIssueVoucher.VoucherNumber; issueHash = issue.IntegrityHash;
            }
            else
            {
                var requiredType = request.LineType == QuantitySurveyMaterialLineType.MaterialOnSite
                    ? QuantitySurveyValuationEvidenceType.MaterialOnSite : QuantitySurveyValuationEvidenceType.MaterialOffSite;
                if (requiredType == QuantitySurveyValuationEvidenceType.MaterialOnSite && !policy.Value.AllowMaterialsOnSite)
                    throw Validation("The effective QS policy does not permit materials on site.");
                if (requiredType == QuantitySurveyValuationEvidenceType.MaterialOffSite && !policy.Value.AllowOffSiteMaterials)
                    throw Validation("The effective QS policy does not permit off-site materials.");
                if (!request.ValuationEvidenceId.HasValue || !evidence.TryGetValue(request.ValuationEvidenceId.Value, out var proof) || proof.EvidenceType != requiredType)
                    throw Validation($"Line {index + 1} must select matching, centrally governed valuation evidence.");
                quantity = request.Quantity; deliveredCost = request.DeliveredUnitCost; evidenceId = proof.Id;
                docRecord = proof.CentralDocumentRecordId; docVersion = proof.CentralDocumentVersionId; checksum = proof.ChecksumSha256;
            }
            decimal appliedRate;
            try { appliedRate = QuantitySurveyMaterialReconciliationRules.AppliedRate(policy.Value.ValuationBasis, deliveredCost, rate?.UnitRate); }
            catch (InvalidOperationException exception) { throw Validation($"Line {index + 1}: {exception.Message}"); }
            var total = QuantitySurveyMaterialReconciliationRules.LineValue(quantity, appliedRate);
            var sourceHash = Hash(new { entity.ProjectId, entity.ContractId, entity.ValuationWorksheetId, request.LineType,
                item.Id, item.ItemCode, quantity, deliveredCost, RateId = rate?.Id, ApprovedRate = rate?.UnitRate,
                issueId, issueNumber, issueHash, evidenceId, docRecord, docVersion, checksum, appliedRate, total });
            lines.Add(new()
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ReconciliationId = entity.Id, Sequence = index + 1,
                LineType = request.LineType, InventoryItemId = item.Id, InventoryItemCodeSnapshot = item.ItemCode,
                InventoryItemNameSnapshot = item.Name, UnitOfMeasureSnapshot = item.UnitOfMeasure,
                Quantity = quantity, DeliveredUnitCost = deliveredCost, ApprovedRateId = rate?.Id,
                ApprovedUnitRateSnapshot = rate?.UnitRate, AppliedUnitRate = appliedRate, TotalValue = total,
                InventoryIssueVoucherLineId = issueId, IssueVoucherNumberSnapshot = issueNumber,
                IssueVoucherIntegrityHashSnapshot = issueHash, ValuationEvidenceId = evidenceId,
                CentralDocumentRecordIdSnapshot = docRecord, CentralDocumentVersionIdSnapshot = docVersion,
                EvidenceChecksumSnapshot = checksum, SourceHash = sourceHash,
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
            });
        }
        if (policy.Value.RequireInventoryReconciliation && !lines.Any(value => value.LineType == QuantitySurveyMaterialLineType.TdcSuppliedMaterial))
            throw Validation("The effective QS policy requires at least one acknowledged Inventory issue line for TDC-supplied material reconciliation.");
        return lines;
    }

    private async Task ValidateFrozenAsync(QuantitySurveyMaterialReconciliation entity, CancellationToken token)
    {
        var policy = await ResolvePolicyAsync(token);
        if (policy.ProfileId != entity.ConfigurationProfileId || policy.DecisionId != entity.MaterialDecisionId ||
            policy.WorkflowDefinitionId != entity.ApprovalWorkflowDefinitionId || !FixedEquals(policy.PolicyHash, entity.PolicyHash))
            throw Conflict("The effective material-deduction policy changed. Amend and reconfirm the reconciliation.");
        var worksheet = await db.QuantitySurveyValuationWorksheets.AsNoTracking().Include(value => value.ProjectInterimValuation)
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == entity.ValuationWorksheetId &&
                                           value.ProjectId == entity.ProjectId && !value.IsDeleted, token);
        if (worksheet?.ProjectInterimValuation.ContractId != entity.ContractId || worksheet.ContractorBusinessPartnerId != entity.ContractorBusinessPartnerId)
            throw Conflict("The valuation, Works contract, or contractor lineage changed. Amend and reconfirm the reconciliation.");
        var requests = entity.Lines.OrderBy(value => value.Sequence).Select(value => new SaveQuantitySurveyMaterialReconciliationLineRequest
        {
            LineType = value.LineType, InventoryItemId = value.InventoryItemId, Quantity = value.Quantity,
            DeliveredUnitCost = value.DeliveredUnitCost, ApprovedRateId = value.ApprovedRateId,
            InventoryIssueVoucherLineId = value.InventoryIssueVoucherLineId, ValuationEvidenceId = value.ValuationEvidenceId
        }).ToList();
        var rebuilt = await BuildLinesAsync(entity, requests, policy, token);
        if (rebuilt.Count != entity.Lines.Count || rebuilt.Zip(entity.Lines.OrderBy(value => value.Sequence))
            .Any(pair => !FixedEquals(pair.First.SourceHash, pair.Second.SourceHash)))
            throw Conflict("A governed Inventory, rate, or DMS source changed. Amend and reconfirm the reconciliation.");
    }

    private async Task<Policy> ResolvePolicyAsync(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var profiles = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId &&
                !value.IsDeleted && value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && value.PublishedAt != null &&
                value.EffectiveFrom <= now && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).ToListAsync(token);
        if (profiles.Count == 0) throw Validation("No Published quantity-survey configuration is effective for this date.");
        if (profiles.Count > 1 && profiles[0].IsDefault == profiles[1].IsDefault)
            throw Conflict("More than one quantity-survey configuration is effective for this date.");
        var profile = profiles[0];
        var decision = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ProfileId == profile.Id && value.DecisionKey == "QS-DEC-010" && !value.IsDeleted, token)
            ?? throw Validation("The effective configuration has no QS-DEC-010 material-deduction decision.");
        if (decision.Status != QuantitySurveyConfigurationDecisionStatus.Approved ||
            decision.ApprovalStatus != QuantitySurveyConfigurationApprovalStatus.Approved ||
            decision.EvidenceStatus != QuantitySurveyConfigurationEvidenceStatus.Verified ||
            (decision.EffectiveFrom.HasValue && decision.EffectiveFrom > now) ||
            (decision.EffectiveTo.HasValue && decision.EffectiveTo < now))
            throw Validation("QS-DEC-010 is not approved, verified, and effective for this date.");
        QsMaterialDeductionValue value;
        try { value = JsonSerializer.Deserialize<QsMaterialDeductionValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Conflict("QS-DEC-010 contains invalid material-deduction policy data."); }
        if (value.ApprovalWorkflowDefinitionId == Guid.Empty) throw Validation("QS-DEC-010 has no material-deduction workflow.");
        var workflowValid = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).AnyAsync(item =>
            item.TenantId == TenantId && item.Id == value.ApprovalWorkflowDefinitionId && !item.IsDeleted && item.IsActive &&
            item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !item.EntityType.IsDeleted && item.EntityType.IsActive &&
            item.EntityType.Code == QuantitySurveyWorkflowBindingRegistry.MaterialDeduction, token);
        if (!workflowValid) throw Validation("The QS-DEC-010 workflow must be active, Published, and bound to QS_MATERIAL_DEDUCTION.");
        return new(profile.Id, decision.Id, value.ApprovalWorkflowDefinitionId, value,
            Hash(new { Profile = profile.Id, profile.Version, Decision = decision.Id, decision.ValueJson, value.ApprovalWorkflowDefinitionId }));
    }

    private IQueryable<QuantitySurveyMaterialReconciliation> Query(bool tracked = false)
    {
        var query = db.QuantitySurveyMaterialReconciliations.Include(value => value.Lines)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }
    private async Task<QuantitySurveyMaterialReconciliation> RequiredAsync(Guid id, bool tracked, CancellationToken token) =>
        await Query(tracked).SingleOrDefaultAsync(value => value.Id == id, token)
        ?? throw new QuantitySurveyMaterialReconciliationNotFoundException("The governed material reconciliation was not found.");
    private async Task RequireProjectAsync(Guid projectId)
    {
        if (!await projectService.HasProjectAccessAsync(projectId))
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }
    private async Task<ExternalActor> RequireExternalProjectAsync(Guid projectId, bool requireApprove, Guid? worksheetId, CancellationToken token)
    {
        var link = await db.BusinessPartnerUsers.IgnoreQueryFilters().AsNoTracking().Include(value => value.BusinessPartner).Include(value => value.User)
            .SingleOrDefaultAsync(value => value.UserId == UserId && value.TenantId == TenantId && value.IsActive && !value.IsDeleted &&
                                           value.User.TenantId == TenantId && value.User.IsActive &&
                                           value.BusinessPartner.TenantId == TenantId && !value.BusinessPartner.IsDeleted, token)
            ?? throw new UnauthorizedAccessException("No active business-partner identity is linked to this portal user.");
        if (!link.BusinessPartner.IsActive || !BusinessPartnerLifecyclePolicy.IsOperationalRegistration(link.BusinessPartner.RegistrationStatus))
            throw new UnauthorizedAccessException("The linked business partner is not active.");
        _ = await projectService.GetExternalProjectByIdAsync(projectId, UserId)
            ?? throw new UnauthorizedAccessException("You do not have access to the selected project.");
        var project = await db.Projects.AsNoTracking().SingleAsync(value => value.TenantId == TenantId && value.Id == projectId, token);
        if (!project.ExternalPortalAccessEnabled || !project.ExternalCollaborationEnabled)
            throw new UnauthorizedAccessException("External collaboration is not enabled for this project.");
        if (project.BusinessPartnerId != link.BusinessPartnerId)
        {
            var allowed = await db.ProjectExternalAccessPolicies.AsNoTracking().AnyAsync(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && value.BusinessPartnerId == link.BusinessPartnerId && !value.IsDeleted &&
                (!requireApprove || value.CanApprove) && (value.ArtifactType == "Project" ||
                 (worksheetId.HasValue && value.ArtifactType == "InterimValuation" && value.ArtifactId == worksheetId)), token);
            if (!allowed) throw new UnauthorizedAccessException("The project external-access policy does not permit this material action.");
        }
        return new(link.BusinessPartnerId, link.BusinessPartner.PartnerName);
    }

    private async Task<string> NextNumberAsync(CancellationToken token)
    {
        var year = DateTime.UtcNow.Year;
        var count = await db.QuantitySurveyMaterialReconciliations.IgnoreQueryFilters()
            .CountAsync(value => value.TenantId == TenantId && value.PreparedAt.Year == year, token);
        return $"MATREC-{year}-{count + 1:00000}";
    }
    private void AddRevision(QuantitySurveyMaterialReconciliation entity, Guid clientRequestId, string requestHash,
        string action, string reason, object? before, object after, string correlationId) =>
        db.QuantitySurveyMaterialReconciliationRevisions.Add(new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ReconciliationId = entity.Id, ClientRequestId = clientRequestId,
            RequestHash = requestHash, Action = action, ActorUserId = UserId, ActorName = UserName,
            ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName, CreatedById = UserId
        });
    private void AddAudit(QuantitySurveyMaterialReconciliation entity, string action, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = nameof(QuantitySurveyMaterialReconciliation), ResourceId = entity.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
            IpAddress = "api", UserAgent = "QuantitySurvey", Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });
    private async Task SaveChangesAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The material reconciliation changed. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("IX_", StringComparison.OrdinalIgnoreCase) == true)
        { throw Conflict("A selected material source or client request is already governed by another reconciliation."); }
    }

    private static QuantitySurveyMaterialReconciliationDto Map(QuantitySurveyMaterialReconciliation value) => new()
    {
        Id = value.Id, ProjectId = value.ProjectId, ContractId = value.ContractId,
        ValuationWorksheetId = value.ValuationWorksheetId, ReconciliationNumber = value.ReconciliationNumber,
        Status = value.Status, ApprovalStatus = value.ApprovalStatus, ContractNumber = value.ContractNumberSnapshot,
        ContractorName = value.ContractorNameSnapshot, Currency = value.CurrencyCodeSnapshot,
        ValuationBasis = value.ValuationBasis, MaterialOnSiteAmount = value.MaterialOnSiteAmount,
        MaterialOffSiteAmount = value.MaterialOffSiteAmount, TdcSuppliedDeductionAmount = value.TdcSuppliedDeductionAmount,
        ContractorConfirmedAt = value.ContractorConfirmedAt, WorkflowInstanceId = value.WorkflowInstanceId,
        Notes = value.Notes, RowVersion = Convert.ToBase64String(value.RowVersion),
        Lines = value.Lines.Where(line => !line.IsDeleted).OrderBy(line => line.Sequence).Select(line => new QuantitySurveyMaterialReconciliationLineDto
        {
            Id = line.Id, Sequence = line.Sequence, LineType = line.LineType, InventoryItemId = line.InventoryItemId,
            ItemCode = line.InventoryItemCodeSnapshot, ItemName = line.InventoryItemNameSnapshot,
            UnitOfMeasure = line.UnitOfMeasureSnapshot, Quantity = line.Quantity,
            DeliveredUnitCost = line.DeliveredUnitCost, ApprovedRateId = line.ApprovedRateId,
            ApprovedUnitRate = line.ApprovedUnitRateSnapshot, AppliedUnitRate = line.AppliedUnitRate,
            TotalValue = line.TotalValue, InventoryIssueVoucherLineId = line.InventoryIssueVoucherLineId,
            IssueVoucherNumber = line.IssueVoucherNumberSnapshot, ValuationEvidenceId = line.ValuationEvidenceId,
            SourceHash = line.SourceHash
        }).ToList()
    };
    private static object Snapshot(QuantitySurveyMaterialReconciliation value) => new
    {
        value.Id, value.ProjectId, value.ContractId, value.ValuationWorksheetId, value.ContractorBusinessPartnerId,
        value.ReconciliationNumber, value.Status, value.ApprovalStatus, value.ValuationBasis,
        value.MaterialOnSiteAmount, value.MaterialOffSiteAmount, value.TdcSuppliedDeductionAmount,
        value.ConfigurationProfileId, value.MaterialDecisionId, value.ApprovalWorkflowDefinitionId,
        value.PolicyHash, value.PreparedById, value.ContractorConfirmedById, value.SubmittedById,
        value.ApprovedById, value.WorkflowInstanceId, value.RejectionReason,
        Lines = value.Lines.Where(line => !line.IsDeleted).OrderBy(line => line.Sequence).Select(line => new
        { line.Id, line.Sequence, line.LineType, line.InventoryItemId, line.Quantity, line.AppliedUnitRate,
          line.TotalValue, line.InventoryIssueVoucherLineId, line.ValuationEvidenceId, line.SourceHash })
    };
    private static void ApplyRowVersion(QuantitySurveyMaterialReconciliation value, string encoded)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw Validation("The material reconciliation row version is invalid. Refresh and retry."); }
        if (expected.Length != value.RowVersion.Length || !CryptographicOperations.FixedTimeEquals(expected, value.RowVersion))
            throw Conflict("The material reconciliation changed. Refresh and retry.");
    }
    private static string RequiredReason(string? value) => RequiredText(value, 5, 2000, "Reason");
    private static string RequiredText(string? value, int min, int max, string label)
        => string.IsNullOrWhiteSpace(value) || value.Trim().Length < min || value.Trim().Length > max
            ? throw Validation($"{label} must contain {min} to {max} characters.") : value.Trim();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value)
        ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static QuantitySurveyMaterialReconciliationValidationException Validation(string message) => new(message);
    private static QuantitySurveyMaterialReconciliationConflictException Conflict(string message) => new(message);
    private static QuantitySurveyMaterialReconciliationConflictException RetryConflict() =>
        Conflict("This client request identifier is already bound to different material-reconciliation inputs.");
    private sealed record Policy(Guid ProfileId, Guid DecisionId, Guid WorkflowDefinitionId,
        QsMaterialDeductionValue Value, string PolicyHash);
    private sealed record ExternalActor(Guid BusinessPartnerId, string Name);
}
