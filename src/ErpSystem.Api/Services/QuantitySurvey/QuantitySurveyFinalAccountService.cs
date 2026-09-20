using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyFinalAccountService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IVendorInvoiceService vendorInvoices,
    IQuantitySurveyEscalationCalculationService escalationCalculations,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : IQuantitySurveyFinalAccountService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated application user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyFinalAccountWorkspaceDto> GetWorkspaceAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        var contractIds = await LinkedContractIdsAsync(projectId, token);
        var contracts = await db.Set<Contract>().AsNoTracking().Include(value => value.BusinessPartner)
            .Where(value => value.TenantId == TenantId && contractIds.Contains(value.Id) && !value.IsDeleted &&
                            value.ContractType == "Works" &&
                            (value.Status == "Active" || value.Status == "Completed" || value.Status == "Terminated"))
            .OrderBy(value => value.ContractNumber)
            .Select(value => new QuantitySurveyFinalAccountContractLookupDto(value.Id, value.ContractNumber,
                value.ContractTitle, value.BusinessPartner.PartnerName, value.Currency, value.ContractValue))
            .ToListAsync(token);
        var entity = await Query().FirstOrDefaultAsync(value => value.ProjectId == projectId, token);
        return new() { Contracts = contracts, FinalAccount = entity is null ? null : await MapAsync(entity, token) };
    }

    public async Task<QuantitySurveyFinalAccountDto> PrepareAsync(Guid projectId,
        PrepareQuantitySurveyFinalAccountRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || request.ContractId == Guid.Empty)
            throw Validation("Select a controlled Works contract and provide a client request identifier.");
        if (request.SettlementDate == default)
            throw Validation("A settlement date is required.");
        var reason = RequiredReason(request.Reason);
        var requestHash = Hash(new
        {
            projectId,
            request.ContractId,
            SettlementDate = request.SettlementDate.Date,
            Notes = Normalize(request.Notes),
            reason
        });
        await RequireProjectAsync(projectId);
        Guid finalAccountId = Guid.Empty;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var replay = await db.ProjectFinalAccountRevisions.AsNoTracking().FirstOrDefaultAsync(value =>
                value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId, token);
            if (replay is not null)
            {
                if (!FixedEquals(replay.RequestHash, requestHash))
                    throw Conflict("This client request identifier is already bound to different final-account inputs.");
                finalAccountId = replay.ProjectFinalAccountId;
                await transaction.CommitAsync(token);
                return;
            }
            var contract = await RequireContractAsync(projectId, request.ContractId, token);
            var policy = await ResolvePolicyAsync(token);
            var existing = await Query(true).FirstOrDefaultAsync(value => value.ProjectId == projectId, token);
            if (existing is not null && existing.Status is not (ProjectFinalAccountStatuses.Draft or ProjectFinalAccountStatuses.Rejected))
                throw Conflict("Only a Draft or Rejected final account can be refreshed. Submit, approve or close the current lifecycle instead.");
            if (existing is not null) ApplyRowVersion(existing, request.RowVersion);
            if (existing is not null && existing.Status == ProjectFinalAccountStatuses.Draft)
                await escalationCalculations.ApplyApprovedFinalAccountImpactsAsync(existing.Id, correlationId, token);
            var reconciliation = await ReconcileAsync(projectId, contract, existing?.Id, token);
            if (reconciliation.ApprovedBoqVersion is null)
                throw Conflict("An approved Published BoQ is required before the final account can be prepared.");
            if (reconciliation.ApprovedBoqVersion.SnapshotHash?.Length != 64)
                throw Conflict("The approved BoQ does not contain a valid immutable snapshot hash. Re-publish the BoQ before preparing the final account.");
            if (existing is not null && existing.ClientRequestId == request.ClientRequestId)
            {
                if (!FixedEquals(existing.RequestHash, requestHash))
                    throw Conflict("This client request identifier is already bound to different final-account inputs.");
                finalAccountId = existing.Id;
                await transaction.CommitAsync(token);
                return;
            }
            var now = DateTime.UtcNow;
            var before = existing is null ? null : Snapshot(existing);
            var entity = existing ?? new ProjectFinalAccount
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId,
                CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            };
            entity.ContractId = contract.Id;
            entity.ClientRequestId = request.ClientRequestId;
            entity.RequestHash = requestHash;
            entity.LastMutationClientRequestId = null;
            entity.LastMutationRequestHash = null;
            entity.Status = ProjectFinalAccountStatuses.Draft;
            entity.ApprovalStatus = "Draft";
            entity.SettlementDate = request.SettlementDate.Date;
            entity.Currency = contract.Currency.Trim().ToUpperInvariant();
            entity.ConfigurationProfileId = policy.Profile.Id;
            entity.ContractControlsDecisionId = policy.Decision.Id;
            entity.ApprovalWorkflowDefinitionId = policy.Workflow.Id;
            entity.PolicyHash = policy.Hash;
            entity.WorkflowInstanceId = null;
            entity.SubmittedById = null;
            entity.SubmittedAt = null;
            entity.ApprovedById = null;
            entity.ApprovedAt = null;
            entity.RejectionReason = null;
            entity.ClosedById = null;
            entity.ClosedAt = null;
            entity.ClosureReason = null;
            entity.PreparedById = UserId;
            entity.PreparedAt = now;
            entity.Notes = Normalize(request.Notes);
            entity.CorrelationId = Correlation(correlationId);
            ApplyReconciliation(entity, reconciliation);
            entity.UpdatedAt = now;
            entity.UpdatedBy = UserName;
            entity.LastModifiedById = UserId;
            if (existing is null) db.ProjectFinalAccounts.Add(entity);
            AddHistory(entity, request.ClientRequestId, requestHash,
                QuantitySurveyAuditEventMap.PrepareFinalAccount, reason, before, Snapshot(entity), correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.PrepareFinalAccount, before, Snapshot(entity), correlationId);
            await SaveAsync(token);
            finalAccountId = entity.Id;
            await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear();
        return await MapAsync(await RequiredAsync(finalAccountId, false, token), token);
    }

    public Task<QuantitySurveyFinalAccountDto> SubmitAsync(Guid id, QuantitySurveyFinalAccountActionRequest request,
        string correlationId, CancellationToken token = default) =>
        MutateAsync(id, request, QuantitySurveyAuditEventMap.SubmitFinalAccount, correlationId, token, async entity =>
        {
            if (entity.Status != ProjectFinalAccountStatuses.Draft || entity.ApprovalStatus != "Draft")
                throw Conflict("Only a Draft final account can be submitted.");
            await RevalidateCommercialAsync(entity, token);
            var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.FinalAccount, entity.Id,
                entity.ApprovalWorkflowDefinitionId ?? throw Conflict("The frozen final-account workflow is unavailable."));
            if (!result.ExecutionResult.Success)
                throw Conflict(result.ExecutionResult.Message ?? "The configured final-account workflow could not be started.");
            if (result.Outcome != WorkflowOutcome.Pending)
                throw Conflict("The final-account workflow must stop at an independent review and approval step.");
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.FinalAccount)
                .ApplySubmitOutcome(entity, result, UserId);
            entity.Status = ProjectFinalAccountStatuses.PendingApproval;
            entity.ApprovalStatus = "Pending";
            entity.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
            entity.SubmittedById = UserId;
            entity.SubmittedAt = DateTime.UtcNow;
        });

    public Task<QuantitySurveyFinalAccountDto> ApproveAsync(Guid id, QuantitySurveyFinalAccountActionRequest request,
        string correlationId, CancellationToken token = default) =>
        CompleteApprovalAsync(id, request, true, correlationId, token);

    public Task<QuantitySurveyFinalAccountDto> RejectAsync(Guid id, QuantitySurveyFinalAccountActionRequest request,
        string correlationId, CancellationToken token = default) =>
        CompleteApprovalAsync(id, request, false, correlationId, token);

    public Task<QuantitySurveyFinalAccountDto> CloseAsync(Guid id, QuantitySurveyFinalAccountActionRequest request,
        string correlationId, CancellationToken token = default) =>
        MutateAsync(id, request, QuantitySurveyAuditEventMap.CloseFinalAccount, correlationId, token, async entity =>
        {
            if (entity.Status != ProjectFinalAccountStatuses.Approved || entity.ApprovalStatus != "Approved")
                throw Conflict("Only an Approved final account can be closed.");
            var contract = await RequireContractAsync(entity.ProjectId, entity.ContractId ?? Guid.Empty, token);
            var current = await ReconcileAsync(entity.ProjectId, contract, entity.Id, token);
            if (!FixedEquals(entity.ReconciliationHash, current.CommercialHash))
                throw Conflict("The approved commercial sources changed. Closure is blocked until the final account is re-prepared and re-approved.");
            var blockers = QuantitySurveyFinalAccountRules.ClosureBlockers(current.ApprovedBoqVersion is not null,
                current.HasPendingCommercialRecords, current.Amounts.RetentionOutstandingAmount, current.Amounts.FinalPaymentAmount);
            if (blockers.Count > 0) throw Conflict(string.Join(" ", blockers));
            entity.PaidToDateAmount = current.PaidToDateAmount;
            entity.RetentionHeldAmount = current.RetentionHeldAmount;
            entity.RetentionReleasedAmount = current.RetentionReleasedAmount;
            entity.Status = ProjectFinalAccountStatuses.Closed;
            entity.ClosedById = UserId;
            entity.ClosedAt = DateTime.UtcNow;
            entity.ClosureReason = RequiredReason(request.Reason);
        });

    public async Task<IReadOnlyList<QuantitySurveyFinalAccountRevisionDto>> GetHistoryAsync(Guid id, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        await RequireProjectAsync(entity.ProjectId);
        return await db.ProjectFinalAccountRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectFinalAccountId == id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new QuantitySurveyFinalAccountRevisionDto(value.Id, value.Action, value.ActorUserId,
                value.ActorName, value.ActorRoles, value.CorrelationId, value.Reason, value.BeforeJson,
                value.AfterJson, value.CreatedAt)).ToListAsync(token);
    }

    private async Task<QuantitySurveyFinalAccountDto> CompleteApprovalAsync(Guid id,
        QuantitySurveyFinalAccountActionRequest request, bool approve, string correlationId, CancellationToken token)
    {
        var action = approve ? QuantitySurveyAuditEventMap.ApproveFinalAccount : QuantitySurveyAuditEventMap.RejectFinalAccount;
        return await MutateAsync(id, request, action, correlationId, token, async entity =>
        {
            if (entity.Status != ProjectFinalAccountStatuses.PendingApproval || entity.ApprovalStatus != "Pending")
                throw Conflict("The final account must be Pending approval before this decision.");
            QuantitySurveyFinalAccountRules.RequireIndependentApprover(entity.PreparedById ?? Guid.Empty,
                entity.SubmittedById, UserId);
            if (approve) await RevalidateCommercialAsync(entity, token);
            var workflowStatus = await db.WorkflowInstances.AsNoTracking().Where(value => value.TenantId == TenantId &&
                    value.Id == entity.WorkflowInstanceId && value.EntityId == entity.Id)
                .Select(value => (WorkflowInstanceStatus?)value.Status).FirstOrDefaultAsync(token);
            if (!approve && workflowStatus == WorkflowInstanceStatus.Completed)
                throw Conflict("A completed final-account workflow is approved and cannot be rejected.");
            WorkflowOutcome outcome;
            if (approve && workflowStatus == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
            else if (!approve && workflowStatus is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                outcome = WorkflowOutcome.Rejected;
            else
            {
                if (workflowStatus is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                    throw Conflict("The final-account workflow ended without approval.");
                if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.FinalAccount, entity.Id, UserId))
                    throw new UnauthorizedAccessException("You are not assigned to the current final-account approval step.");
                var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.FinalAccount,
                    entity.Id, UserId, approve ? "Approve" : "Reject", RequiredReason(request.Reason));
                if (!result.ExecutionResult.Success)
                    throw Conflict(result.ExecutionResult.Message ?? "The final-account workflow decision could not be processed.");
                outcome = result.Outcome;
            }
            var expected = approve ? WorkflowOutcome.Approved : WorkflowOutcome.Rejected;
            if (outcome != expected)
            {
                if (approve) throw Conflict("The shared workflow has not completed every final-account approval step.");
                throw Conflict("The shared workflow did not return a rejected outcome.");
            }
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.FinalAccount)
                .ApplyApprovalOutcome(entity, outcome, UserId, approve ? null : RequiredReason(request.Reason));
            entity.Status = approve ? ProjectFinalAccountStatuses.Approved : ProjectFinalAccountStatuses.Rejected;
            entity.ApprovalStatus = approve ? "Approved" : "Rejected";
            entity.ApprovedById = approve ? UserId : null;
            entity.ApprovedAt = approve ? DateTime.UtcNow : null;
            entity.RejectionReason = approve ? null : RequiredReason(request.Reason);
        });
    }

    private async Task<QuantitySurveyFinalAccountDto> MutateAsync(Guid id, QuantitySurveyFinalAccountActionRequest request,
        string action, string correlationId, CancellationToken token, Func<ProjectFinalAccount, Task> mutate)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var reason = RequiredReason(request.Reason);
        var mutationHash = Hash(new { action, reason });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var replay = await db.ProjectFinalAccountRevisions.AsNoTracking().FirstOrDefaultAsync(value =>
                value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId, token);
            if (replay is not null)
            {
                if (replay.ProjectFinalAccountId != id || !FixedEquals(replay.RequestHash, mutationHash))
                    throw Conflict("This client request identifier is already bound to a different final-account action.");
                await transaction.CommitAsync(token);
                return;
            }
            var entity = await RequiredAsync(id, true, token);
            await RequireProjectAsync(entity.ProjectId);
            if (entity.LastMutationClientRequestId == request.ClientRequestId)
            {
                if (!FixedEquals(entity.LastMutationRequestHash, mutationHash))
                    throw Conflict("This client request identifier is already bound to a different final-account action.");
                await transaction.CommitAsync(token);
                return;
            }
            ApplyRowVersion(entity, request.RowVersion);
            var before = Snapshot(entity);
            await mutate(entity);
            entity.LastMutationClientRequestId = request.ClientRequestId;
            entity.LastMutationRequestHash = mutationHash;
            entity.CorrelationId = Correlation(correlationId);
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = UserName;
            entity.LastModifiedById = UserId;
            AddHistory(entity, request.ClientRequestId, mutationHash,
                action, reason, before, Snapshot(entity), correlationId);
            AddAudit(entity, action, before, Snapshot(entity), correlationId);
            await SaveAsync(token);
            await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear();
        return await MapAsync(await RequiredAsync(id, false, token), token);
    }

    private async Task RevalidateCommercialAsync(ProjectFinalAccount entity, CancellationToken token)
    {
        var policy = await ResolvePolicyAsync(token);
        if (entity.ConfigurationProfileId != policy.Profile.Id || entity.ContractControlsDecisionId != policy.Decision.Id ||
            entity.ApprovalWorkflowDefinitionId != policy.Workflow.Id || !FixedEquals(entity.PolicyHash, policy.Hash))
            throw Conflict("The effective QS final-account policy changed. Re-prepare the final account under the current policy.");
        var contract = await RequireContractAsync(entity.ProjectId, entity.ContractId ?? Guid.Empty, token);
        var current = await ReconcileAsync(entity.ProjectId, contract, entity.Id, token);
        if (!FixedEquals(entity.ReconciliationHash, current.CommercialHash))
            throw Conflict("The BoQ, variation, claim, escalation, certificate or deduction sources changed. Refresh the Draft final account before submission or approval.");
        if (current.HasPendingCommercialRecords)
            throw Conflict("Pending variations, valuations, certificates, claims or escalation records must be resolved before final-account approval.");
    }

    private async Task<Reconciliation> ReconcileAsync(Guid projectId, Contract contract, Guid? finalAccountId, CancellationToken token)
    {
        var boq = await db.ProjectBoqVersions.AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && !value.IsDeleted && value.VersionType == QuantitySurveyBoqVersionType.Approved &&
                value.Status == ProjectBoqVersionStatuses.Approved && value.PublishedAt != null)
            .OrderByDescending(value => value.PublishedAt).ThenByDescending(value => value.VersionNumber).FirstOrDefaultAsync(token);
        var boqValue = boq is null ? 0m : await db.ProjectBoqVersionLines.AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && value.ProjectBoqVersionId == boq.Id && !value.IsDeleted)
            .SumAsync(value => value.LineAmount ?? 0m, token);
        var variations = await db.ProjectVariationOrders.AsNoTracking().Include(value => value.RevisedBoqVersion).Where(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && value.ContractId == contract.Id && !value.IsDeleted)
            .OrderBy(value => value.RequestedDate).ToListAsync(token);
        var certificates = await db.ProjectPaymentCertificates.AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && value.ContractId == contract.Id && !value.IsDeleted)
            .OrderBy(value => value.IssueDate).ToListAsync(token);
        var worksheets = await db.QuantitySurveyValuationWorksheets.AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && !value.IsDeleted).ToListAsync(token);
        var escalation = finalAccountId.HasValue
            ? await db.QuantitySurveyEscalationCalculationRuns.AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && value.ContractId == contract.Id && value.FinalAccountId == finalAccountId &&
                !value.IsDeleted).ToListAsync(token)
            : [];
        var approvedVariations = variations.Where(value =>
            (value.Status is ProjectVariationOrderStatuses.Approved or
                ProjectVariationOrderStatuses.Implemented or ProjectVariationOrderStatuses.Closed) &&
            value.DownstreamApplicationStatus != ProjectVariationApplicationStatuses.NotApplied).ToList();
        var approvedCertificates = certificates.Where(value => value.Status is ProjectPaymentCertificateStatuses.Approved or
            ProjectPaymentCertificateStatuses.Paid).ToList();
        var approvedEscalation = escalation.Where(value => value.Status == "ApprovedPendingApplication" && value.ApprovalStatus == "Approved" &&
            value.ImpactApplicationStatus == "Applied").ToList();
        decimal paidToDate = 0m;
        foreach (var certificate in approvedCertificates.Where(value => value.VendorInvoiceId.HasValue))
        {
            var invoice = await vendorInvoices.GetByIdAsync(certificate.VendorInvoiceId!.Value, token)
                ?? throw Conflict($"Finance invoice for certificate '{certificate.CertificateNumber}' is unavailable.");
            if (!string.Equals(invoice.CurrencyCode, contract.Currency, StringComparison.OrdinalIgnoreCase))
                throw Conflict($"Finance invoice for certificate '{certificate.CertificateNumber}' uses a different currency.");
            paidToDate += invoice.PaidAmount;
        }
        var certified = Round(approvedCertificates.Sum(value => value.NetCertifiedAmount));
        var retentionHeld = Round(approvedCertificates.Sum(value => value.RetentionHeldAmount));
        var retentionReleased = Round(approvedCertificates.Sum(value => value.RetentionReleasedAmount));
        var advanceRecovery = Round(approvedCertificates.Sum(value => value.AdvanceRecoveryAmount));
        var materialDeduction = Round(approvedCertificates.Sum(value => value.MaterialDeductionAmount));
        var otherDeduction = Round(approvedCertificates.Sum(value => value.OtherDeductionsAmount));
        var variationAmount = Round(approvedVariations.Sum(value => value.ApprovedAmount ?? value.EstimatedAmount ?? 0m));
        var approvedClaimAmount = 0m;
        var escalationAmount = Round(approvedEscalation.Sum(value => value.ApprovedImpactAmount));
        var appliedContractVariationAmount = Round(approvedVariations.Where(value => value.UpdateContractSumOnApplication)
            .Sum(value => value.ApprovedAmount ?? value.EstimatedAmount ?? 0m));
        decimal contractBaseline;
        try { contractBaseline = QuantitySurveyVariationRules.CalculateFinalAccountContractBaseline(contract.ContractValue, appliedContractVariationAmount); }
        catch (InvalidOperationException exception) { throw Conflict(exception.Message); }
        var amounts = QuantitySurveyFinalAccountRules.Calculate(contractBaseline, variationAmount, approvedClaimAmount,
            escalationAmount, advanceRecovery, materialDeduction, otherDeduction, retentionHeld, retentionReleased, Round(paidToDate));
        var pending = variations.Any(value => value.Status is ProjectVariationOrderStatuses.Draft or ProjectVariationOrderStatuses.Submitted or ProjectVariationOrderStatuses.PendingApproval or ProjectVariationOrderStatuses.UnderReview) ||
                      variations.Any(value => value.Status == ProjectVariationOrderStatuses.Approved &&
                          (value.DownstreamApplicationStatus == ProjectVariationApplicationStatuses.NotApplied ||
                           value.RevisedBoqVersion == null || value.RevisedBoqVersion.Status != ProjectBoqVersionStatuses.Approved)) ||
                      certificates.Any(value => value.Status is ProjectPaymentCertificateStatuses.Draft or ProjectPaymentCertificateStatuses.Issued) ||
                      worksheets.Any(value => value.Status is not (QuantitySurveyValuationWorkflowStatuses.Approved or QuantitySurveyValuationWorkflowStatuses.Rejected)) ||
                      worksheets.Any(value => value.Status == QuantitySurveyValuationWorkflowStatuses.Approved && value.DisputedValue > 0m) ||
                      escalation.Any(value => value.Status != "Rejected" &&
                          !(value.Status == "ApprovedPendingApplication" && value.ApprovalStatus == "Approved" &&
                            value.ImpactApplicationStatus == "Applied"));
        var lines = new List<QuantitySurveyFinalAccountReconciliationLineDto>
        {
            new("ApprovedBoQ", "Current approved BoQ publication", boq?.Id, boq is null ? null : $"BoQ v{boq.VersionNumber}", Round(boqValue), "Reference", boq?.Status ?? "Missing"),
            new("Contract", "Reconciled Works contract baseline", contract.Id, contract.ContractNumber, contractBaseline, "Add", contract.Status)
        };
        lines.AddRange(approvedVariations.Select(value => new QuantitySurveyFinalAccountReconciliationLineDto("Variation", value.Title,
            value.Id, value.ReferenceNumber, Round(value.ApprovedAmount ?? value.EstimatedAmount ?? 0m), "Add", value.Status)));
        lines.AddRange(approvedEscalation.Select(value => new QuantitySurveyFinalAccountReconciliationLineDto("Escalation", value.RunReference,
            value.Id, value.RunReference, Round(value.ApprovedImpactAmount), "Add", value.Status)));
        lines.AddRange(approvedCertificates.Select(value => new QuantitySurveyFinalAccountReconciliationLineDto("Certificate", value.Title,
            value.Id, value.CertificateNumber, Round(value.NetCertifiedAmount), "Paid/Certified", value.Status)));
        if (advanceRecovery > 0m) lines.Add(new("AdvanceRecovery", "Advance recovery deductions", null, null, advanceRecovery, "Deduct", "Approved certificates"));
        if (materialDeduction > 0m) lines.Add(new("MaterialDeduction", "Material deductions", null, null, materialDeduction, "Deduct", "Approved certificates"));
        if (otherDeduction > 0m) lines.Add(new("OtherDeduction", "Other certificate deductions", null, null, otherDeduction, "Deduct", "Approved certificates"));
        lines.Add(new("FinancePayment", "Finance-owned AP payments", null, null, Round(paidToDate), "Settle", "Finance read model"));
        var commercialHash = Hash(new
        {
            contract.Id, ContractValue = Round(contract.ContractValue), ContractBaseline = contractBaseline, Currency = contract.Currency.ToUpperInvariant(),
            BoqId = boq?.Id, BoqHash = boq?.SnapshotHash, BoqValue = Round(boqValue),
            Variations = approvedVariations.Select(value => new { value.Id, Amount = Round(value.ApprovedAmount ?? value.EstimatedAmount ?? 0m), value.ApplicationHash, value.RevisedBoqVersionId }),
            Escalation = approvedEscalation.Select(value => new { value.Id, Amount = Round(value.ApprovedImpactAmount) }),
            Certificates = approvedCertificates.Select(value => new { value.Id, value.VendorInvoiceId, Net = Round(value.NetCertifiedAmount),
                RetentionHeld = Round(value.RetentionHeldAmount), RetentionReleased = Round(value.RetentionReleasedAmount),
                Advance = Round(value.AdvanceRecoveryAmount), Material = Round(value.MaterialDeductionAmount), Other = Round(value.OtherDeductionsAmount) })
        });
        return new(boq, Round(boqValue), variationAmount, approvedClaimAmount, escalationAmount, certified,
            retentionHeld, retentionReleased, advanceRecovery, materialDeduction, otherDeduction, Round(paidToDate),
            amounts, pending, lines, commercialHash);
    }

    private static void ApplyReconciliation(ProjectFinalAccount entity, Reconciliation value)
    {
        entity.ApprovedBoqVersionId = value.ApprovedBoqVersion?.Id;
        entity.ApprovedBoqSnapshotHash = value.ApprovedBoqVersion?.SnapshotHash;
        entity.ApprovedBoqValue = value.ApprovedBoqValue;
        entity.OriginalContractValue = value.Amounts.GrossFinalAccountValue - value.ApprovedVariationAmount -
                                      value.ApprovedClaimAmount - value.ApprovedEscalationAmount;
        entity.ApprovedVariationAmount = value.ApprovedVariationAmount;
        entity.ApprovedClaimAmount = value.ApprovedClaimAmount;
        entity.ApprovedEscalationAmount = value.ApprovedEscalationAmount;
        entity.CertifiedToDate = value.CertifiedToDate;
        entity.RetentionHeldAmount = value.RetentionHeldAmount;
        entity.RetentionReleasedAmount = value.RetentionReleasedAmount;
        entity.AdvanceRecoveryAmount = value.AdvanceRecoveryAmount;
        entity.MaterialDeductionAmount = value.MaterialDeductionAmount;
        entity.OtherDeductionAmount = value.OtherDeductionAmount;
        entity.PaidToDateAmount = value.PaidToDateAmount;
        entity.FinalAccountValue = value.Amounts.NetFinalAccountValue;
        entity.ReconciliationHash = value.CommercialHash;
        entity.ReconciliationJson = JsonSerializer.Serialize(value.Lines, JsonOptions);
    }

    private async Task<QuantitySurveyFinalAccountDto> MapAsync(ProjectFinalAccount entity, CancellationToken token)
    {
        var contract = await RequireContractAsync(entity.ProjectId, entity.ContractId ?? Guid.Empty, token);
        var current = await ReconcileAsync(entity.ProjectId, contract, entity.Id, token);
        var blockers = QuantitySurveyFinalAccountRules.ClosureBlockers(current.ApprovedBoqVersion is not null,
            current.HasPendingCommercialRecords, current.Amounts.RetentionOutstandingAmount, current.Amounts.FinalPaymentAmount);
        return new()
        {
            Id = entity.Id, ProjectId = entity.ProjectId, ContractId = contract.Id, ContractNumber = contract.ContractNumber,
            ContractTitle = contract.ContractTitle, Contractor = contract.BusinessPartner.PartnerName,
            Status = entity.Status, ApprovalStatus = entity.ApprovalStatus, SettlementDate = entity.SettlementDate,
            ApprovedBoqVersionId = entity.ApprovedBoqVersionId, ApprovedBoqVersionNumber = current.ApprovedBoqVersion?.VersionNumber,
            ApprovedBoqValue = entity.ApprovedBoqValue, OriginalContractValue = entity.OriginalContractValue,
            ApprovedVariationAmount = entity.ApprovedVariationAmount, ApprovedClaimAmount = entity.ApprovedClaimAmount,
            ApprovedEscalationAmount = entity.ApprovedEscalationAmount,
            GrossFinalAccountValue = Round(entity.OriginalContractValue + entity.ApprovedVariationAmount + entity.ApprovedClaimAmount + entity.ApprovedEscalationAmount),
            AdvanceRecoveryAmount = entity.AdvanceRecoveryAmount, MaterialDeductionAmount = entity.MaterialDeductionAmount,
            OtherDeductionAmount = entity.OtherDeductionAmount,
            TotalDeductionAmount = Round(entity.AdvanceRecoveryAmount + entity.MaterialDeductionAmount + entity.OtherDeductionAmount),
            FinalAccountValue = entity.FinalAccountValue, CertifiedToDate = entity.CertifiedToDate,
            RetentionHeldAmount = current.RetentionHeldAmount, RetentionReleasedAmount = current.RetentionReleasedAmount,
            RetentionOutstandingAmount = current.Amounts.RetentionOutstandingAmount,
            PaidToDateAmount = current.PaidToDateAmount, FinalPaymentAmount = current.Amounts.FinalPaymentAmount,
            Currency = entity.Currency, Notes = entity.Notes, WorkflowInstanceId = entity.WorkflowInstanceId,
            PreparedById = entity.PreparedById, PreparedAt = entity.PreparedAt, SubmittedById = entity.SubmittedById,
            SubmittedAt = entity.SubmittedAt, ApprovedById = entity.ApprovedById, ApprovedAt = entity.ApprovedAt,
            ClosedById = entity.ClosedById, ClosedAt = entity.ClosedAt, RejectionReason = entity.RejectionReason,
            ClosureReason = entity.ClosureReason, HasPendingCommercialRecords = current.HasPendingCommercialRecords,
            CanClose = entity.Status == ProjectFinalAccountStatuses.Approved && blockers.Count == 0,
            ClosureBlockers = blockers, Lines = current.Lines, RowVersion = Convert.ToBase64String(entity.RowVersion)
        };
    }

    private async Task<Policy> ResolvePolicyAsync(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var profile = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId &&
                !value.IsDeleted && value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published &&
                value.PublishedAt != null && value.EffectiveFrom <= now && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).FirstOrDefaultAsync(token)
            ?? throw Conflict("No Published QS configuration is effective for this tenant and date.");
        var decision = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ProfileId == profile.Id && !value.IsDeleted && value.DecisionKey == "QS-DEC-012" &&
            value.Status == QuantitySurveyConfigurationDecisionStatus.Approved &&
            value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved &&
            value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified &&
            (!value.EffectiveFrom.HasValue || value.EffectiveFrom <= now) && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now), token)
            ?? throw Conflict("The effective QS-DEC-012 contract-controls decision is not approved and evidence-verified.");
        QsContractControlsValue controls;
        try { controls = JsonSerializer.Deserialize<QsContractControlsValue>(decision.ValueJson, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException exception) { throw new QuantitySurveyFinalAccountValidationException($"The effective QS-DEC-012 value is invalid: {exception.Message}"); }
        if (controls.FinalAccountWorkflowDefinitionId == Guid.Empty)
            throw Conflict("QS-DEC-012 does not select a final-account workflow.");
        var workflowDefinition = await db.WorkflowDefinitions.AsNoTracking().Include(value => value.EntityType).FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == controls.FinalAccountWorkflowDefinitionId && !value.IsDeleted && value.IsActive &&
            value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !value.EntityType.IsDeleted && value.EntityType.IsActive &&
            value.EntityType.Code == QuantitySurveyWorkflowBindingRegistry.FinalAccount, token)
            ?? throw Conflict("The QS-DEC-012 final-account workflow is not an active Published QS_FINAL_ACCOUNT definition.");
        return new(profile, decision, controls, workflowDefinition, Hash(new
        {
            ProfileId = profile.Id,
            DecisionId = decision.Id,
            decision.ValueJson,
            WorkflowDefinitionId = workflowDefinition.Id
        }));
    }

    private async Task<Contract> RequireContractAsync(Guid projectId, Guid contractId, CancellationToken token)
    {
        if (contractId == Guid.Empty) throw Validation("Select a controlled Works contract.");
        var ids = await LinkedContractIdsAsync(projectId, token);
        return await db.Set<Contract>().Include(value => value.BusinessPartner).FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == contractId && ids.Contains(value.Id) && !value.IsDeleted &&
            value.ContractType == "Works" && (value.Status == "Active" || value.Status == "Completed" || value.Status == "Terminated"), token)
            ?? throw Conflict("The selected contract is not an eligible Works contract linked to this project.");
    }

    private async Task<HashSet<Guid>> LinkedContractIdsAsync(Guid projectId, CancellationToken token)
    {
        var projectContract = await db.Projects.AsNoTracking().Where(value => value.TenantId == TenantId && value.Id == projectId && !value.IsDeleted)
            .Select(value => value.ContractId).FirstOrDefaultAsync(token);
        var ids = (await db.ProjectPackages.AsNoTracking().Where(value => value.TenantId == TenantId && value.ProjectId == projectId &&
                value.ContractId.HasValue && !value.IsDeleted).Select(value => value.ContractId!.Value).ToListAsync(token)).ToHashSet();
        if (projectContract.HasValue) ids.Add(projectContract.Value);
        return ids;
    }

    private async Task RequireProjectAsync(Guid projectId)
    {
        if (!await projectService.HasProjectAccessAsync(projectId))
            throw new UnauthorizedAccessException("You are not permitted to access this project.");
    }

    private IQueryable<ProjectFinalAccount> Query(bool tracked = false)
    {
        var query = db.ProjectFinalAccounts.Where(value => value.TenantId == TenantId && !value.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<ProjectFinalAccount> RequiredAsync(Guid id, bool tracked, CancellationToken token) =>
        await Query(tracked).FirstOrDefaultAsync(value => value.Id == id, token)
        ?? throw new QuantitySurveyFinalAccountNotFoundException("The governed final account was not found.");

    private void AddHistory(ProjectFinalAccount entity, Guid clientRequestId, string requestHash,
        string action, string reason, object? before, object after, string correlationId) =>
        db.ProjectFinalAccountRevisions.Add(new()
        {
            TenantId = TenantId, ProjectFinalAccountId = entity.Id, ClientRequestId = clientRequestId,
            RequestHash = requestHash, Action = action, ActorUserId = UserId,
            ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName, CreatedById = UserId
        });

    private void AddAudit(ProjectFinalAccount entity, string action, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = "ProjectFinalAccount", ResourceId = entity.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
            IpAddress = "api", UserAgent = "QuantitySurvey", Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The final account changed. Refresh and retry."); }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: >= 51861 and <= 51869 } ||
            exception.InnerException?.Message.Contains("final-account", StringComparison.OrdinalIgnoreCase) == true ||
            exception.InnerException?.Message.Contains("FinalAccount", StringComparison.OrdinalIgnoreCase) == true)
        { throw Conflict("The final-account request conflicts with an existing governed record or database control."); }
    }

    private static object Snapshot(ProjectFinalAccount value) => new
    {
        value.Id, value.ProjectId, value.ContractId, value.Status, value.ApprovalStatus, value.SettlementDate,
        value.ApprovedBoqVersionId, value.ApprovedBoqSnapshotHash, value.ApprovedBoqValue,
        value.OriginalContractValue, value.ApprovedVariationAmount, value.ApprovedClaimAmount,
        value.ApprovedEscalationAmount, value.AdvanceRecoveryAmount, value.MaterialDeductionAmount,
        value.OtherDeductionAmount, value.CertifiedToDate, value.RetentionHeldAmount, value.RetentionReleasedAmount,
        value.PaidToDateAmount, value.FinalAccountValue, value.ConfigurationProfileId, value.ContractControlsDecisionId,
        value.ApprovalWorkflowDefinitionId, value.WorkflowInstanceId, value.ReconciliationHash,
        value.PreparedById, value.SubmittedById, value.ApprovedById, value.ClosedById,
        value.PreparedAt, value.SubmittedAt, value.ApprovedAt, value.ClosedAt,
        value.RejectionReason, value.ClosureReason
    };

    private static void ApplyRowVersion(ProjectFinalAccount entity, string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) throw Validation("Refresh the final account before changing it.");
        byte[] expected;
        try { expected = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw Validation("The final-account row version is invalid. Refresh and retry."); }
        if (expected.Length != entity.RowVersion.Length || !CryptographicOperations.FixedTimeEquals(expected, entity.RowVersion))
            throw Conflict("The final account changed. Refresh and retry.");
    }

    private static string RequiredReason(string? value) => string.IsNullOrWhiteSpace(value) || value.Trim().Length is < 5 or > 2000
        ? throw Validation("Reason must contain 5 to 2000 characters.") : value.Trim();
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static QuantitySurveyFinalAccountValidationException Validation(string value) => new(value);
    private static QuantitySurveyFinalAccountConflictException Conflict(string value) => new(value);

    private sealed record Policy(QuantitySurveyConfigurationProfile Profile, QuantitySurveyConfigurationDecision Decision,
        QsContractControlsValue Controls, WorkflowDefinition Workflow, string Hash);
    private sealed record Reconciliation(ProjectBoqVersion? ApprovedBoqVersion, decimal ApprovedBoqValue,
        decimal ApprovedVariationAmount, decimal ApprovedClaimAmount, decimal ApprovedEscalationAmount,
        decimal CertifiedToDate, decimal RetentionHeldAmount, decimal RetentionReleasedAmount,
        decimal AdvanceRecoveryAmount, decimal MaterialDeductionAmount, decimal OtherDeductionAmount,
        decimal PaidToDateAmount, QuantitySurveyFinalAccountAmounts Amounts, bool HasPendingCommercialRecords,
        IReadOnlyList<QuantitySurveyFinalAccountReconciliationLineDto> Lines, string CommercialHash);
}
