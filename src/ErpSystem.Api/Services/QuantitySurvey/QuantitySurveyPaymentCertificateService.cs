using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Documents;
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
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyPaymentCertificateService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    ICivilEngineeringIpcEndorsementService ipcEndorsements,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters,
    IVendorInvoiceService vendorInvoices,
    ITaxCalculationEngine taxEngine,
    IDocumentOutputService documentOutput,
    IControlledFileUploadService controlledFiles,
    ICentralDocumentRepositoryFileService centralDocuments,
    IProcurementBudgetCommitmentLifecycleService budgetCommitments,
    IApSupplierIdentityService supplierIdentity) : IQuantitySurveyPaymentCertificateService
{
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

    public async Task<QuantitySurveyPaymentCertificateLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        var worksheets = await db.QuantitySurveyValuationWorksheets.AsNoTracking()
            .Include(value => value.ProjectInterimValuation)
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                            value.Status == QuantitySurveyValuationWorkflowStatuses.Approved && value.CertificateReady)
            .OrderBy(value => value.ProjectInterimValuation.ValuationDate).ToListAsync(token);
        var used = await db.Set<ProjectPaymentCertificate>().AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                            value.QuantitySurveyValuationWorksheetId != null)
            .Select(value => value.QuantitySurveyValuationWorksheetId!.Value).ToListAsync(token);
        var usedSet = used.ToHashSet();
        var options = new List<QuantitySurveyPaymentCertificateLookupDto>();
        foreach (var worksheet in worksheets.Where(value => !usedSet.Contains(value.Id)))
        {
            var previous = await PreviousCertificateAsync(worksheet.ProjectId,
                worksheet.ProjectInterimValuation.ContractId, worksheet.ProjectInterimValuation.ValuationDate, token);
            var previousAmount = previous?.CertifiedToDateAmount ?? 0m;
            options.Add(new QuantitySurveyPaymentCertificateLookupDto
            {
                WorksheetId = worksheet.Id,
                InterimValuationId = worksheet.ProjectInterimValuationId,
                ContractId = worksheet.ProjectInterimValuation.ContractId,
                Label = $"{worksheet.ProjectInterimValuation.ValuationNumber ?? worksheet.ProjectInterimValuation.Title} · {worksheet.ProjectInterimValuation.ValuationDate:dd MMM yyyy}",
                Currency = worksheet.ProjectInterimValuation.Currency,
                CertifiedToDateAmount = worksheet.CurrentCertifiedValue,
                PreviousCertificateAmount = previousAmount,
                CurrentGrossAmount = Math.Max(0m, worksheet.CurrentCertifiedValue - previousAmount),
                CurrentRetentionAmount = worksheet.CurrentRetentionValue
            });
        }
        var agreements = await db.QuantitySurveyAdvanceRecoveryAgreements.AsNoTracking().Where(value =>
                value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                value.Status == QuantitySurveyAdvanceRecoveryStatuses.Approved)
            .OrderBy(value => value.ContractNumberSnapshot).ThenBy(value => value.RecoveryNumber).ToListAsync(token);
        var advanceLookups = new List<QuantitySurveyPaymentCertificateAdvanceLookupDto>();
        foreach (var agreement in agreements)
        {
            var committed = await db.Set<ProjectPaymentCertificate>().AsNoTracking().Where(value => value.TenantId == TenantId &&
                    value.QuantitySurveyAdvanceRecoveryAgreementId == agreement.Id && !value.IsDeleted &&
                    value.Status != ProjectPaymentCertificateStatuses.Cancelled)
                .SumAsync(value => (decimal?)value.AdvanceRecoveryAmount, token) ?? 0m;
            var remaining = QuantitySurveyAdvanceRecoveryRules.Remaining(agreement.OriginalAdvanceAmount, committed);
            advanceLookups.Add(new(agreement.Id, agreement.ContractId,
                $"{agreement.RecoveryNumber} · {agreement.PaymentNumberSnapshot} · {agreement.RecoveryPercentage:0.####}%",
                agreement.CurrencyCodeSnapshot, agreement.RecoveryPercentage, remaining));
        }
        var materialLookups = await db.QuantitySurveyMaterialReconciliations.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                            value.Status == QuantitySurveyMaterialReconciliationStatuses.Approved &&
                            value.ApprovalStatus == "Approved")
            .OrderBy(value => value.ReconciliationNumber)
            .Select(value => new QuantitySurveyPaymentCertificateMaterialLookupDto(
                value.Id, value.ValuationWorksheetId, value.ContractId,
                value.ReconciliationNumber + " - " + value.ContractorNameSnapshot,
                value.CurrencyCodeSnapshot, value.MaterialOnSiteAmount, value.MaterialOffSiteAmount,
                value.TdcSuppliedDeductionAmount)).ToListAsync(token);
        return new QuantitySurveyPaymentCertificateLookupsDto
        {
            EligibleValuations = options, EligibleAdvanceRecoveries = advanceLookups,
            ApprovedMaterialReconciliations = materialLookups
        };
    }

    public async Task<IReadOnlyList<QuantitySurveyPaymentCertificateDto>> ListAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        return (await Query().Where(value => value.ProjectId == projectId)
            .OrderByDescending(value => value.IssueDate).ThenByDescending(value => value.CreatedAt).ToListAsync(token))
            .Select(Map).ToList();
    }

    public async Task<QuantitySurveyPaymentCertificateDto> GetAsync(Guid id, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        await RequireProjectAsync(entity.ProjectId);
        return Map(entity);
    }

    public async Task<QuantitySurveyPaymentCertificateDto> GenerateAsync(Guid projectId,
        GenerateQuantitySurveyPaymentCertificateRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || request.ValuationWorksheetId == Guid.Empty)
            throw Validation("Select an approved certificate-ready valuation and provide a client request identifier.");
        await RequireProjectAsync(projectId);
        var requestHash = Hash(new { projectId, request.ValuationWorksheetId, request.AdvanceRecoveryAgreementId,
            request.MaterialReconciliationId, request.PaymentDueDate,
            request.AdvanceRecoveryAmount, request.OtherDeductionsAmount,
            Notes = Normalize(request.Notes) });
        var strategy = db.Database.CreateExecutionStrategy();
        Guid certificateId = Guid.Empty;
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var retry = await db.Set<ProjectPaymentCertificate>().FirstOrDefaultAsync(value =>
                value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
            if (retry is not null)
            {
                if (retry.ProjectId != projectId || !FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
                certificateId = retry.Id;
                await transaction.CommitAsync(token);
                return;
            }
            var worksheet = await db.QuantitySurveyValuationWorksheets
                .Include(value => value.ProjectInterimValuation).Include(value => value.ProjectBoqVersion)
                .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.ValuationWorksheetId &&
                    value.ProjectId == projectId && !value.IsDeleted, token)
                ?? throw NotFound("The selected valuation worksheet was not found in this project.");
            if (worksheet.Status != QuantitySurveyValuationWorkflowStatuses.Approved || !worksheet.CertificateReady ||
                worksheet.ApprovalStatus != "Approved" || !worksheet.ApprovedById.HasValue)
                throw Conflict("Only an independently approved certificate-ready valuation can generate a payment certificate.");
            if (!worksheet.ProjectInterimValuation.ContractId.HasValue)
                throw Conflict("The approved valuation must be linked to its Works contract before certificate generation.");
            if (await db.Set<ProjectPaymentCertificate>().AnyAsync(value => value.TenantId == TenantId &&
                value.QuantitySurveyValuationWorksheetId == worksheet.Id && !value.IsDeleted, token))
                throw Conflict("This valuation already has a payment certificate.");
            var policy = await ResolvePolicyAsync(worksheet, token);
            var contract = await db.Set<Contract>().AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId &&
                value.Id == worksheet.ProjectInterimValuation.ContractId && !value.IsDeleted, token)
                ?? throw Conflict("The valuation's Works contract is unavailable.");
            var previous = await PreviousCertificateAsync(projectId, contract.Id,
                worksheet.ProjectInterimValuation.ValuationDate, token);
            if (policy.Value.RequirePreviousCertificate)
            {
                var olderApprovedValuationExists = await db.QuantitySurveyValuationWorksheets.AsNoTracking()
                    .AnyAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                        value.Id != worksheet.Id && value.Status == QuantitySurveyValuationWorkflowStatuses.Approved &&
                        value.ProjectInterimValuation.ContractId == contract.Id &&
                        value.ProjectInterimValuation.ValuationDate < worksheet.ProjectInterimValuation.ValuationDate, token);
                if (olderApprovedValuationExists && previous is null)
                    throw Conflict("The previous approved valuation must have an approved payment certificate before this certificate can be generated.");
            }
            if (!policy.Value.ApplyAdvanceRecovery && (request.AdvanceRecoveryAmount != 0m || request.AdvanceRecoveryAgreementId.HasValue))
                throw Validation("Advance recovery is disabled by the effective valuation and certificate policy.");
            var governedRecovery = await ResolveAdvanceRecoveryAsync(projectId, contract, request.AdvanceRecoveryAgreementId,
                Math.Max(0m, worksheet.CurrentCertifiedValue - (previous?.CertifiedToDateAmount ?? 0m)), null, token);
            if (decimal.Round(request.AdvanceRecoveryAmount, 2) != governedRecovery.Amount)
                throw Validation($"Advance recovery must equal the governed amount of {governedRecovery.Amount:0.00}. Refresh the controlled recovery selection.");
            var governedMaterials = await ResolveMaterialReconciliationAsync(projectId, contract.Id, worksheet.Id,
                request.MaterialReconciliationId, null, token);
            var amounts = await CalculateAsync(worksheet.CurrentCertifiedValue,
                previous?.CertifiedToDateAmount ?? 0m,
                policy.Value.ApplyRetention ? worksheet.CurrentRetentionValue : 0m, 0m,
                request.AdvanceRecoveryAmount, governedMaterials.OnSite, governedMaterials.OffSite,
                governedMaterials.Deduction, request.OtherDeductionsAmount,
                policy, worksheet.ProjectInterimValuation.ValuationDate, token);
            var now = DateTime.UtcNow;
            var yearCount = await db.Set<ProjectPaymentCertificate>().IgnoreQueryFilters().CountAsync(value =>
                value.TenantId == TenantId && value.IssueDate.Year == now.Year, token);
            var entity = new ProjectPaymentCertificate
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId,
                ProjectPhaseId = worksheet.ProjectInterimValuation.ProjectPhaseId,
                ProjectPackageId = worksheet.ProjectInterimValuation.ProjectPackageId,
                ContractId = contract.Id, ProjectInterimValuationId = worksheet.ProjectInterimValuationId,
                QuantitySurveyValuationWorksheetId = worksheet.Id, PreviousPaymentCertificateId = previous?.Id,
                QuantitySurveyAdvanceRecoveryAgreementId = governedRecovery.Agreement?.Id,
                QuantitySurveyMaterialReconciliationId = governedMaterials.Reconciliation?.Id,
                ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
                CertificateNumber = $"IPC-{now:yyyy}-{yearCount + 1:00000}",
                Title = $"Payment Certificate · {worksheet.ProjectInterimValuation.ValuationNumber ?? worksheet.ProjectInterimValuation.Title}",
                Status = ProjectPaymentCertificateStatuses.Draft, ApprovalStatus = "Draft",
                IssueDate = now, PaymentDueDate = request.PaymentDueDate,
                CertifiedToDateAmount = amounts.CertifiedToDate, PreviouslyCertifiedAmount = amounts.PreviouslyCertified,
                GrossCertifiedAmount = amounts.GrossCurrent, RetentionHeldAmount = amounts.RetentionHeld,
                RetentionReleasedAmount = amounts.RetentionReleased, AdvanceRecoveryAmount = amounts.AdvanceRecovery,
                MaterialOnSiteAmount = amounts.MaterialOnSite, MaterialOffSiteAmount = amounts.MaterialOffSite,
                MaterialDeductionAmount = amounts.MaterialDeduction, OtherDeductionsAmount = amounts.OtherDeductions,
                TaxAmount = amounts.Tax, NetCertifiedAmount = amounts.NetCurrent,
                Currency = worksheet.ProjectInterimValuation.Currency, Notes = Normalize(request.Notes),
                ConfigurationProfileId = policy.Profile.Id, ValuationDecisionId = policy.Decision.Id,
                ApprovalWorkflowDefinitionId = policy.Workflow.Id, CertificateTemplateId = policy.Template.Id,
                CertificateTemplateVersionSnapshot = policy.Template.Version,
                CertificateMetadataTemplateId = policy.MetadataTemplate.Id,
                CertificateMetadataTemplateCodeSnapshot = policy.MetadataTemplate.TemplateCode,
                PolicyHash = policy.Hash, ExpenseAccountId = policy.Value.CertificateExpenseAccountId,
                AccountsPayableAccountId = policy.Value.CertificateAccountsPayableAccountId,
                PaymentTermId = policy.Value.CertificatePaymentTermId, TaxGroupId = policy.Value.CertificateTaxGroupId,
                WithholdingTaxId = policy.Value.CertificateWithholdingTaxId,
                TaxHandling = policy.Value.TaxHandling.ToString(),
                PreviousCertificateRequired = policy.Value.RequirePreviousCertificate,
                RetentionApplied = policy.Value.ApplyRetention,
                AdvanceRecoveryApplied = policy.Value.ApplyAdvanceRecovery,
                PreparedById = UserId, PreparedAt = now,
                ApHandoffStatus = ProjectPaymentCertificateApHandoffStatuses.NotReady,
                PaymentStatusSnapshot = "NotInvoiced", CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            };
            db.Set<ProjectPaymentCertificate>().Add(entity);
            AddHistory(entity, QuantitySurveyAuditEventMap.GeneratePaymentCertificate, null, Snapshot(entity), correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.GeneratePaymentCertificate, null, Snapshot(entity), correlationId);
            await SaveAsync(token);
            certificateId = entity.Id;
            await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear();
        return Map(await RequiredAsync(certificateId, false, token));
    }

    public async Task<QuantitySurveyPaymentCertificateDto> UpdateAsync(Guid id,
        UpdateQuantitySurveyPaymentCertificateRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var hash = Hash(new { Action = "Update", request.PaymentDueDate, request.AdvanceRecoveryAmount,
            request.MaterialReconciliationId, request.OtherDeductionsAmount, Notes = Normalize(request.Notes) });
        await MutateAsync(id, request.ClientRequestId, request.RowVersion, hash, correlationId,
            QuantitySurveyAuditEventMap.UpdatePaymentCertificate, async entity =>
                {
                    if (entity.Status != ProjectPaymentCertificateStatuses.Draft || entity.ApprovalStatus != "Draft")
                        throw Conflict("Only a Draft payment certificate can be amended.");
                    await ipcEndorsements.EnsureCertificateCanBeAmendedAsync(entity.Id, token);
                    if (!entity.AdvanceRecoveryApplied && request.AdvanceRecoveryAmount != 0m)
                    throw Validation("Advance recovery is disabled by the frozen certificate policy.");
                if (decimal.Round(request.AdvanceRecoveryAmount, 2) != entity.AdvanceRecoveryAmount)
                    throw Validation("Advance recovery is governed by the approved recovery agreement and cannot be typed or amended on the certificate.");
                var policy = await ResolveFrozenPolicyAsync(entity, token);
                var governedMaterials = await ResolveMaterialReconciliationAsync(entity.ProjectId, entity.ContractId!.Value,
                    entity.QuantitySurveyValuationWorksheetId!.Value, request.MaterialReconciliationId, entity.Id, token);
                entity.QuantitySurveyMaterialReconciliationId = governedMaterials.Reconciliation?.Id;
                var amounts = await CalculateAsync(entity.CertifiedToDateAmount, entity.PreviouslyCertifiedAmount,
                    entity.RetentionHeldAmount, entity.RetentionReleasedAmount, request.AdvanceRecoveryAmount,
                    governedMaterials.OnSite, governedMaterials.OffSite, governedMaterials.Deduction,
                    request.OtherDeductionsAmount, policy, entity.IssueDate, token);
                ApplyAmounts(entity, amounts);
                entity.PaymentDueDate = request.PaymentDueDate;
                entity.Notes = Normalize(request.Notes);
            }, token);
        return Map(await RequiredAsync(id, false, token));
    }

    public async Task<QuantitySurveyPaymentCertificateDto> SubmitAsync(Guid id,
        QuantitySurveyPaymentCertificateActionRequest request, string correlationId, CancellationToken token = default)
    {
        var reason = RequiredText(request.Reason, "Submission reason");
        var hash = Hash(new { Action = "Submit", reason });
        await MutateAsync(id, request.ClientRequestId, request.RowVersion, hash, correlationId,
            QuantitySurveyAuditEventMap.SubmitPaymentCertificate, async entity =>
                {
                    if (entity.Status != ProjectPaymentCertificateStatuses.Draft || entity.ApprovalStatus != "Draft")
                        throw Conflict("Only a Draft payment certificate can be submitted.");
                    await ipcEndorsements.EnsureCertificateCanProceedAsync(entity.Id, token);
                    await ValidateReadinessAsync(entity, token);
                var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.PaymentCertificate,
                    entity.Id, entity.ApprovalWorkflowDefinitionId!.Value);
                if (!result.ExecutionResult.Success)
                    throw Conflict(result.ExecutionResult.Message ?? "The configured payment-certificate workflow could not be started.");
                workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.PaymentCertificate)
                    .ApplySubmitOutcome(new CertificateWorkflowAdapter(entity), result, UserId);
                if (result.Outcome != WorkflowOutcome.Pending)
                    throw Conflict("The payment-certificate workflow must stop at an independent approval step.");
                entity.Status = ProjectPaymentCertificateStatuses.Issued;
                entity.ApprovalStatus = "Pending";
                entity.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
                entity.SubmittedById = UserId;
                entity.SubmittedAt = DateTime.UtcNow;
            }, token);
        return Map(await RequiredAsync(id, false, token));
    }

    public Task<QuantitySurveyPaymentCertificateDto> ApproveAsync(Guid id,
        QuantitySurveyPaymentCertificateActionRequest request, string correlationId, CancellationToken token = default) =>
        CompleteApprovalAsync(id, request, true, correlationId, token);

    public Task<QuantitySurveyPaymentCertificateDto> RejectAsync(Guid id,
        QuantitySurveyPaymentCertificateActionRequest request, string correlationId, CancellationToken token = default) =>
        CompleteApprovalAsync(id, request, false, correlationId, token);

    private async Task<QuantitySurveyPaymentCertificateDto> CompleteApprovalAsync(Guid id,
        QuantitySurveyPaymentCertificateActionRequest request, bool approve, string correlationId, CancellationToken token)
    {
        var reason = RequiredText(request.Reason, approve ? "Approval reason" : "Rejection reason");
        var action = approve ? "Approve" : "Reject";
        var hash = Hash(new { Action = action, reason });
        var current = await RequiredAsync(id, false, token);
        await RequireProjectAsync(current.ProjectId);
        var resumesCommittedApproval = approve &&
            current.Status == ProjectPaymentCertificateStatuses.Approved &&
            current.ApprovalStatus == "Approved";
        if (!resumesCommittedApproval)
        {
            await MutateAsync(id, request.ClientRequestId, request.RowVersion, hash, correlationId,
                approve ? QuantitySurveyAuditEventMap.ApprovePaymentCertificate : QuantitySurveyAuditEventMap.RejectPaymentCertificate,
                async entity =>
                {
                if (entity.Status != ProjectPaymentCertificateStatuses.Issued || entity.ApprovalStatus != "Pending")
                    throw Conflict("The payment certificate must be Pending approval before this decision.");
                if (approve)
                {
                    await ipcEndorsements.EnsureCertificateCanProceedAsync(entity.Id, token);
                    await ValidateReadinessAsync(entity, token);
                }
                try { QuantitySurveyPaymentCertificateRules.RequireIndependentApprover(entity.PreparedById ?? Guid.Empty, entity.SubmittedById ?? Guid.Empty, UserId); }
                catch (InvalidOperationException exception) { throw Conflict(exception.Message); }
                var workflowStatus = await db.WorkflowInstances.AsNoTracking().Where(value => value.TenantId == TenantId &&
                    value.Id == entity.WorkflowInstanceId && value.EntityId == entity.Id).Select(value => (WorkflowInstanceStatus?)value.Status)
                    .FirstOrDefaultAsync(token);
                if (!approve && workflowStatus == WorkflowInstanceStatus.Completed)
                    throw Conflict("A completed payment-certificate workflow is approved and cannot be rejected.");
                WorkflowOutcome outcome;
                if (approve && workflowStatus == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
                else if (!approve && workflowStatus is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                    outcome = WorkflowOutcome.Rejected;
                else
                {
                    if (workflowStatus is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                        throw Conflict("The payment-certificate workflow ended without approval.");
                    if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.PaymentCertificate, entity.Id, UserId))
                        throw new UnauthorizedAccessException("You are not assigned to the current payment-certificate approval step.");
                    var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.PaymentCertificate,
                        entity.Id, UserId, action, reason);
                    if (!result.ExecutionResult.Success)
                        throw Conflict(result.ExecutionResult.Message ?? $"The payment-certificate {action.ToLowerInvariant()} could not be processed.");
                    outcome = result.Outcome;
                }
                var expected = approve ? WorkflowOutcome.Approved : WorkflowOutcome.Rejected;
                if (outcome != expected)
                {
                    if (!approve) throw Conflict("The shared workflow did not return a rejected outcome.");
                    return;
                }
                workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.PaymentCertificate)
                    .ApplyApprovalOutcome(new CertificateWorkflowAdapter(entity), outcome, UserId, approve ? null : reason);
                entity.Status = approve ? ProjectPaymentCertificateStatuses.Approved : ProjectPaymentCertificateStatuses.Cancelled;
                entity.ApprovalStatus = approve ? "Approved" : "Rejected";
                entity.ApprovedById = approve ? UserId : null;
                entity.ApprovedAt = approve ? DateTime.UtcNow : null;
                entity.RejectionReason = approve ? null : reason;
                entity.ApHandoffStatus = approve ? ProjectPaymentCertificateApHandoffStatuses.Ready : ProjectPaymentCertificateApHandoffStatuses.NotReady;
                if (approve)
                {
                    if (!entity.ContractId.HasValue)
                        throw Conflict("The approved payment certificate has no procurement contract lineage.");
                    await budgetCommitments.UtilizeContractCertificateAsync(
                        entity.ContractId.Value,
                        entity.Id,
                        entity.CertificateNumber ?? entity.Id.ToString("N"),
                        entity.GrossCertifiedAmount,
                        correlationId,
                        token);
                }
                }, token);
        }
        var completed = await RequiredAsync(id, false, token);
        if (!approve || completed.Status != ProjectPaymentCertificateStatuses.Approved ||
            completed.ApprovalStatus != "Approved")
        {
            return Map(completed);
        }

        // Finance accepts only an approved Works certificate with governed DMS evidence.
        // Keep each durable boundary idempotent: approval and budget utilization commit first,
        // then the controlled document is issued, and only then is the AP invoice created.
        // A retry of the final approval resumes at the first incomplete boundary without
        // repeating workflow approval or budget utilization.
        if (!completed.CentralDocumentRecordId.HasValue || !completed.CentralDocumentVersionId.HasValue)
        {
            await RenderAsync(id, correlationId, token);
            completed = await RequiredAsync(id, false, token);
        }

        if (!completed.VendorInvoiceId.HasValue)
        {
            return await HandoffToApAsync(id, new QuantitySurveyPaymentCertificateActionRequest
            {
                ClientRequestId = DerivedRequestId(request.ClientRequestId, "approved-certificate-ap-handoff"),
                RowVersion = Convert.ToBase64String(completed.RowVersion),
                Reason = "Automatic governed handoff of the approved and documented Works certificate to Finance AP."
            }, correlationId, token);
        }

        return Map(completed);
    }

    public async Task<QuantitySurveyPaymentCertificateDto> HandoffToApAsync(Guid id,
        QuantitySurveyPaymentCertificateActionRequest request, string correlationId, CancellationToken token = default)
    {
        var reason = RequiredText(request.Reason, "AP handoff reason");
        var hash = Hash(new { Action = "HandoffToAp", reason });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var entity = await RequiredAsync(id, true, token);
            await RequireProjectAsync(entity.ProjectId);
            if (IsRetry(entity, request.ClientRequestId, hash)) { await transaction.CommitAsync(token); return; }
            ApplyRowVersion(entity, request.RowVersion);
            if (entity.Status is not (ProjectPaymentCertificateStatuses.Approved or ProjectPaymentCertificateStatuses.Paid) || entity.ApprovalStatus != "Approved")
                throw Conflict("Only an approved payment certificate can be handed to Finance AP.");
            if (entity.VendorInvoiceId.HasValue)
            {
                Touch(entity, request.ClientRequestId, hash);
                await SaveAsync(token);
                await transaction.CommitAsync(token);
                return;
            }
            await ValidateReadinessAsync(entity, token);
            try
            {
                await CreateApInvoiceAsync(entity, correlationId, token);
                Touch(entity, request.ClientRequestId, hash);
                await SaveAsync(token);
                await transaction.CommitAsync(token);
            }
            catch (QuantitySurveyPaymentCertificateException) { throw; }
            catch (InvalidOperationException exception) { throw Conflict(exception.Message); }
        });
        db.ChangeTracker.Clear();
        return Map(await RequiredAsync(id, false, token));
    }

    private async Task CreateApInvoiceAsync(ProjectPaymentCertificate entity, string correlationId,
        CancellationToken token)
    {
        if (entity.VendorInvoiceId.HasValue) return;

        var contract = await db.Set<Contract>().AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == entity.ContractId && !value.IsDeleted, token)
            ?? throw Conflict("The certificate's Works contract is unavailable.");
        var before = Snapshot(entity);
        var taxableBase = decimal.Round(
            entity.NetCertifiedAmount - entity.TaxAmount,
            2,
            MidpointRounding.AwayFromZero);
        if (taxableBase < 0m)
            throw Conflict("The certificate tax snapshot exceeds its net payable amount.");

        // This is the purpose-authorized handoff of an approved Works certificate.
        // Finance owns the tenant-safe, idempotent partner-to-supplier projection; normal
        // invoice entry still cannot implicitly onboard an unlinked contractor.
        var supplier = await supplierIdentity.ResolveByBusinessPartnerAsync(contract.BusinessPartnerId, token);
        if (supplier.BusinessPartnerId != contract.BusinessPartnerId || supplier.SupplierId == Guid.Empty)
            throw Conflict("The Finance supplier identity does not match the certificate's contractor.");
        var invoice = await vendorInvoices.CreateAsync(new VendorInvoiceCreateDto
        {
            SupplierId = supplier.SupplierId,
            SupplierInvoiceNumber = entity.CertificateNumber,
            InvoiceDate = entity.IssueDate,
            ReceivedDate = DateTime.UtcNow,
            DueDate = entity.PaymentDueDate,
            CurrencyCode = entity.Currency,
            ExchangeRate = 1m,
            PaymentTermId = entity.PaymentTermId,
            WithholdingTaxId = entity.WithholdingTaxId,
            MatchingType = InvoiceMatchingType.None,
            AcceptedSupplyKind = ProcurementAcceptedSupplyKind.WorksPaymentCertificate,
            AcceptedSupplySourceId = entity.Id,
            IsTrustedAcceptedSupplyHandoff = true,
            ExpenseAccountId = entity.ExpenseAccountId,
            ApAccountId = entity.AccountsPayableAccountId,
            Notes = $"Generated from approved QS payment certificate {entity.CertificateNumber}. {entity.Notes}".Trim(),
            Reference = $"QS-CERT:{entity.Id:N}",
            LineItems =
            [
                new VendorInvoiceLineItemCreateDto
                {
                    LineItemType = "Service",
                    GLAccountId = entity.ExpenseAccountId,
                    Description = entity.Title,
                    Quantity = 1m,
                    UnitPrice = taxableBase,
                    TaxGroupId = entity.TaxGroupId,
                    TaxTreatment = TaxTreatment.Standard,
                    Unit = "Certificate"
                }
            ]
        }, token);
        if (decimal.Round(invoice.TotalAmount, 2) != decimal.Round(entity.NetCertifiedAmount, 2))
            throw Conflict(
                "Finance AP recalculated a different invoice total. Review the frozen tax group before retrying the handoff.");

        entity.VendorInvoiceId = invoice.Id;
        entity.ApHandoffStatus = ProjectPaymentCertificateApHandoffStatuses.Created;
        entity.ApHandoffAt = DateTime.UtcNow;
        entity.ApHandoffFailure = null;
        entity.PaymentStatusSnapshot = invoice.Status.ToString();
        entity.PaymentStatusUpdatedAt = DateTime.UtcNow;
        AddHistory(entity, QuantitySurveyAuditEventMap.HandoffPaymentCertificateToAp,
            before, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.HandoffPaymentCertificateToAp,
            before, Snapshot(entity), correlationId);
    }

    public async Task<QuantitySurveyPaymentCertificateDto> RefreshPaymentStatusAsync(Guid id,
        string correlationId, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token);
        await RequireProjectAsync(entity.ProjectId);
        if (!entity.VendorInvoiceId.HasValue) throw Conflict("This certificate has not been handed to Finance AP.");
        var invoice = await vendorInvoices.GetByIdAsync(entity.VendorInvoiceId.Value, token)
            ?? throw Conflict("The linked Finance AP invoice is unavailable.");
        var before = Snapshot(entity);
        entity.PaymentStatusSnapshot = invoice.Status.ToString();
        entity.PaymentStatusUpdatedAt = DateTime.UtcNow;
        entity.Status = invoice.Status == VendorInvoiceStatus.Paid
            ? ProjectPaymentCertificateStatuses.Paid : ProjectPaymentCertificateStatuses.Approved;
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
        AddHistory(entity, QuantitySurveyAuditEventMap.RefreshPaymentCertificateStatus, before, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.RefreshPaymentCertificateStatus, before, Snapshot(entity), correlationId);
        await SaveAsync(token);
        return Map(entity);
    }

    public async Task<RenderedDocumentDto> RenderAsync(Guid id, string correlationId, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, true, token);
        await RequireProjectAsync(entity.ProjectId);
        if (entity.Status is not (ProjectPaymentCertificateStatuses.Approved or ProjectPaymentCertificateStatuses.Paid))
            throw Conflict("Approve the payment certificate before generating its controlled PDF.");
        var rendered = await documentOutput.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.QuantitySurveyPaymentCertificate,
            EntityId = id, Format = "pdf", Options = new Dictionary<string, string> { ["correlationId"] = correlationId }
        }, token);
        if (entity.CentralDocumentRecordId.HasValue) return rendered;
        var checksum = Convert.ToHexString(SHA256.HashData(rendered.Content)).ToLowerInvariant();
        var upload = await controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
            Category = ControlledFileUploadCategories.QuantitySurveyPaymentCertificate,
            FileName = rendered.FileName, ContentType = rendered.ContentType, FileSize = rendered.Content.LongLength,
            OpenReadStream = () => new MemoryStream(rendered.Content, false)
        }, token);
        if (upload.Record.VirusScanStatus != FileVirusScanStatus.Clean || !FixedEquals(upload.ChecksumSha256, checksum))
        {
            await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token);
            throw Conflict("The generated certificate failed its central integrity scan.");
        }
        var metadataTemplate = await db.CentralDocumentMetadataTemplates.AsNoTracking()
            .Where(value => value.TenantId == TenantId &&
                value.Id == entity.CertificateMetadataTemplateId &&
                value.TemplateCode == entity.CertificateMetadataTemplateCodeSnapshot &&
                value.IsActive && value.PublishedAt.HasValue && !value.IsDeleted)
            .Select(value => new { value.AccessProfile, value.DocumentType })
            .SingleOrDefaultAsync(token);
        if (metadataTemplate is null || string.IsNullOrWhiteSpace(metadataTemplate.AccessProfile))
        {
            await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token);
            throw Conflict("The frozen payment-certificate metadata template is no longer published and available.");
        }
        CentralDocumentRepositoryLink document;
        try
        {
            document = await centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
                FileUploadRecordId = upload.Record.Id, SourceModule = "QuantitySurvey",
                SourceLabel = "Quantity Survey payment certificate", SourceEntityType = nameof(ProjectPaymentCertificate),
                SourceRecordId = entity.Id, SourceRecordReference = entity.CertificateNumber,
                Title = entity.Title, DocumentType = metadataTemplate.DocumentType,
                MetadataTemplateCode = entity.CertificateMetadataTemplateCodeSnapshot,
                AccessProfile = metadataTemplate.AccessProfile, VersionStatus = "Approved", RequirePublishedGovernance = true,
                ChangeSummary = "Approved QS payment certificate generated from frozen valuation and policy lineage.",
                MetadataValues =
                [
                    new("projectId", "Project ID", entity.ProjectId.ToString(), "guid"),
                    new("valuationWorksheetId", "Valuation worksheet ID", entity.QuantitySurveyValuationWorksheetId?.ToString(), "guid"),
                    new("certificateNumber", "Certificate number", entity.CertificateNumber),
                    new("contractId", "Contract ID", entity.ContractId?.ToString(), "guid"),
                    new("recordReference", "Certificate reference", entity.CertificateNumber ?? entity.Id.ToString()),
                    new("evidenceDate", "Certificate issue date", entity.IssueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), "date"),
                    new("checksumSha256", "Checksum SHA-256", checksum)
                ]
            }, token);
        }
        catch { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw; }
        try
        {
            // Central upload/registration shares this context and can advance aggregate rowversions.
            db.ChangeTracker.Clear();
            entity = await RequiredAsync(id, true, token);
            var before = Snapshot(entity);
            entity.CentralDocumentRecordId = document.DocumentRecordId;
            entity.CentralDocumentVersionId = document.DocumentVersionId;
            entity.GeneratedDocumentHash = checksum;
            entity.GeneratedAt = entity.ApprovedAt ?? DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
            AddHistory(entity, QuantitySurveyAuditEventMap.IssuePaymentCertificateDocument, before, Snapshot(entity), correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.IssuePaymentCertificateDocument, before, Snapshot(entity), correlationId);
            await SaveAsync(token);
        }
        catch { await centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, token); throw; }
        return rendered;
    }

    public async Task<IReadOnlyList<QuantitySurveyPaymentCertificateRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token); await RequireProjectAsync(entity.ProjectId);
        return await db.QuantitySurveyPaymentCertificateRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.PaymentCertificateId == id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt).Select(value => new QuantitySurveyPaymentCertificateRevisionDto
            {
                Id = value.Id, Action = value.Action, ActorName = value.ActorName, ActorRoles = value.ActorRoles,
                CorrelationId = value.CorrelationId, CreatedAt = value.CreatedAt,
                BeforeJson = value.BeforeJson, AfterJson = value.AfterJson
            }).ToListAsync(token);
    }

    private async Task MutateAsync(Guid id, Guid clientRequestId, string rowVersion, string hash,
        string correlationId, string action, Func<ProjectPaymentCertificate, Task> mutation, CancellationToken token)
    {
        if (clientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var entity = await RequiredAsync(id, true, token); await RequireProjectAsync(entity.ProjectId);
            if (IsRetry(entity, clientRequestId, hash)) { await transaction.CommitAsync(token); return; }
            ApplyRowVersion(entity, rowVersion);
            var before = Snapshot(entity);
            await mutation(entity);
            Touch(entity, clientRequestId, hash);
            AddHistory(entity, action, before, Snapshot(entity), correlationId);
            AddAudit(entity, action, before, Snapshot(entity), correlationId);
            await SaveAsync(token);
            await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear();
    }

    private async Task<PaymentCertificatePolicy> ResolvePolicyAsync(QuantitySurveyValuationWorksheet worksheet, CancellationToken token)
    {
        if (!worksheet.ConfigurationProfileId.HasValue || !worksheet.ValuationDecisionId.HasValue)
            throw Conflict("The approved valuation does not retain its effective QS policy lineage.");
        var profile = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == worksheet.ConfigurationProfileId && !value.IsDeleted &&
            value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published, token)
            ?? throw Conflict("The valuation's Published QS profile is unavailable.");
        var decision = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == worksheet.ValuationDecisionId && value.ProfileId == profile.Id &&
            value.DecisionKey == "QS-DEC-008" && !value.IsDeleted &&
            value.Status == QuantitySurveyConfigurationDecisionStatus.Approved &&
            value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved &&
            value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified, token)
            ?? throw Conflict("The valuation's approved QS-DEC-008 decision is unavailable.");
        QsValuationCertificateValue value;
        try
        {
            value = JsonSerializer.Deserialize<QsValuationCertificateValue>(decision.ValueJson, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw Conflict("QS-DEC-008 contains invalid valuation and certificate policy data.");
        }
        var workflowDefinition = await db.WorkflowDefinitions.AsNoTracking().Include(item => item.EntityType).Include(item => item.Steps)
            .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == value.CertificateWorkflowDefinitionId &&
                !item.IsDeleted && item.IsActive && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published, token)
            ?? throw Conflict("The configured Published payment-certificate workflow is unavailable.");
        if (!string.Equals(workflowDefinition.EntityType.Code, QuantitySurveyWorkflowBindingRegistry.PaymentCertificate, StringComparison.OrdinalIgnoreCase) ||
            !workflowDefinition.Steps.Any(step => !step.IsDeleted && step.IsRequired && step.StepType == WorkflowStepType.Approval))
            throw Conflict("The configured payment-certificate workflow must target QS_PAYMENT_CERTIFICATE and contain a required approval step.");
        var template = await db.ReportTemplates.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == TenantId &&
            item.Id == value.CertificateTemplateId && !item.IsDeleted && (item.Status == "Published" || item.Status == "published"), token)
            ?? throw Conflict("The configured Published payment-certificate report template is unavailable.");
        var metadata = await db.CentralDocumentMetadataTemplates.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == TenantId &&
            item.Id == value.CertificateMetadataTemplateId && !item.IsDeleted && item.IsActive && item.PublishedAt.HasValue, token)
            ?? throw Conflict("The configured Published payment-certificate DMS template is unavailable.");
        var expense = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == TenantId &&
            item.Id == value.CertificateExpenseAccountId && !item.IsDeleted && item.Status == AccountStatus.Active &&
            item.AllowDirectPosting && item.AccountType == AccountType.Expense, token)
            ?? throw Conflict("The configured certificate project-cost account is not an active posting Expense account.");
        var ap = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == TenantId &&
            item.Id == value.CertificateAccountsPayableAccountId && !item.IsDeleted && item.Status == AccountStatus.Active &&
            item.IsControlAccount && item.AccountType == AccountType.Liability, token)
            ?? throw Conflict("The configured certificate AP account is not an active Liability control account.");
        var term = await db.PaymentTerms.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == TenantId &&
            item.Id == value.CertificatePaymentTermId && !item.IsDeleted && item.IsActive, token)
            ?? throw Conflict("The configured certificate payment term is unavailable.");
        var taxGroup = await db.TaxGroups.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == TenantId &&
            item.Id == value.CertificateTaxGroupId && !item.IsDeleted && item.IsActive &&
            (item.Applicability == TaxApplicability.Purchases || item.Applicability == TaxApplicability.Both), token)
            ?? throw Conflict("The configured certificate purchase tax group is unavailable.");
        if (value.CertificateWithholdingTaxId.HasValue && !await db.Taxes.AsNoTracking().AnyAsync(item =>
            item.TenantId == TenantId && item.Id == value.CertificateWithholdingTaxId && !item.IsDeleted && item.IsActive &&
            item.Category == TaxCategory.Withholding && (item.Applicability == TaxApplicability.Purchases || item.Applicability == TaxApplicability.Both), token))
            throw Conflict("The configured certificate withholding tax is unavailable.");
        var hash = Hash(new { Profile = profile.Id, Decision = decision.Id, Workflow = workflowDefinition.Id, Template = template.Id,
            TemplateVersion = template.Version, Metadata = metadata.Id, Expense = expense.Id, Ap = ap.Id, Term = term.Id,
            TaxGroup = taxGroup.Id, value.CertificateWithholdingTaxId, value.TaxHandling,
            value.RequirePreviousCertificate, value.ApplyAdvanceRecovery, value.ApplyRetention });
        return new(profile, decision, value, workflowDefinition, template, metadata, hash);
    }

    private async Task<PaymentCertificatePolicy> ResolveFrozenPolicyAsync(ProjectPaymentCertificate entity, CancellationToken token)
    {
        var worksheet = await db.QuantitySurveyValuationWorksheets.Include(value => value.ProjectInterimValuation)
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == entity.QuantitySurveyValuationWorksheetId && !value.IsDeleted, token)
            ?? throw Conflict("The certificate's approved valuation lineage is unavailable.");
        var policy = await ResolvePolicyAsync(worksheet, token);
        if (entity.ConfigurationProfileId != policy.Profile.Id || entity.ValuationDecisionId != policy.Decision.Id ||
            entity.ApprovalWorkflowDefinitionId != policy.Workflow.Id || entity.CertificateTemplateId != policy.Template.Id ||
            entity.CertificateMetadataTemplateId != policy.MetadataTemplate.Id || !FixedEquals(entity.PolicyHash, policy.Hash))
            throw Conflict("The certificate's frozen policy lineage no longer matches the approved valuation. Generate a new certificate instead of altering this one.");
        return policy;
    }

    private async Task ValidateReadinessAsync(ProjectPaymentCertificate entity, CancellationToken token)
    {
        if (!entity.QuantitySurveyValuationWorksheetId.HasValue || !entity.ContractId.HasValue ||
            !entity.ApprovalWorkflowDefinitionId.HasValue || !entity.CertificateTemplateId.HasValue ||
            !entity.CertificateMetadataTemplateId.HasValue || !entity.ExpenseAccountId.HasValue ||
            !entity.AccountsPayableAccountId.HasValue || !entity.PaymentTermId.HasValue || !entity.TaxGroupId.HasValue)
            throw Conflict("The payment certificate is missing governed valuation, workflow, template, DMS, contract, or Finance lineage.");
        var worksheet = await db.QuantitySurveyValuationWorksheets.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == entity.QuantitySurveyValuationWorksheetId && !value.IsDeleted, token);
        if (worksheet is null || worksheet.Status != QuantitySurveyValuationWorkflowStatuses.Approved || !worksheet.CertificateReady)
            throw Conflict("The source valuation is no longer approved and certificate-ready.");
        await ResolveFrozenPolicyAsync(entity, token);
        await ValidateAdvanceRecoveryAsync(entity, token);
        await ValidateMaterialReconciliationAsync(entity, token);
        var recalculated = QuantitySurveyPaymentCertificateRules.Calculate(entity.CertifiedToDateAmount,
            entity.PreviouslyCertifiedAmount, entity.RetentionHeldAmount, entity.RetentionReleasedAmount,
            entity.AdvanceRecoveryAmount, entity.MaterialOnSiteAmount, entity.MaterialOffSiteAmount,
            entity.MaterialDeductionAmount, entity.OtherDeductionsAmount, entity.TaxAmount,
            !string.Equals(entity.TaxHandling, QuantitySurveyTaxHandling.Inclusive.ToString(), StringComparison.Ordinal));
        if (recalculated != Amounts(entity)) throw Conflict("The certificate totals no longer reconcile to their frozen inputs.");
    }

    private async Task ValidateAdvanceRecoveryAsync(ProjectPaymentCertificate entity, CancellationToken token)
    {
        if (entity.AdvanceRecoveryAmount == 0m && !entity.QuantitySurveyAdvanceRecoveryAgreementId.HasValue) return;
        if (!entity.QuantitySurveyAdvanceRecoveryAgreementId.HasValue || !entity.ContractId.HasValue)
            throw Conflict("The certificate advance recovery is missing its approved recovery agreement.");
        var agreement = await db.QuantitySurveyAdvanceRecoveryAgreements.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == entity.QuantitySurveyAdvanceRecoveryAgreementId && !value.IsDeleted &&
            value.ProjectId == entity.ProjectId && value.ContractId == entity.ContractId &&
            (value.Status == QuantitySurveyAdvanceRecoveryStatuses.Approved ||
             value.Status == QuantitySurveyAdvanceRecoveryStatuses.Closed), token)
            ?? throw Conflict("The certificate's approved advance recovery agreement is unavailable.");
        var payment = await db.Set<VendorPayment>().AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId &&
            value.Id == agreement.VendorPaymentId && !value.IsDeleted && value.IsSupplierAdvance && value.JournalEntryId != null &&
            value.Status != VendorPaymentStatus.Voided && value.Status != VendorPaymentStatus.Failed && value.Status != VendorPaymentStatus.Reversed, token)
            ?? throw Conflict("The linked Finance supplier advance is no longer posted and recoverable.");
        var committedByOthers = await db.Set<ProjectPaymentCertificate>().AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.QuantitySurveyAdvanceRecoveryAgreementId == agreement.Id && value.Id != entity.Id && !value.IsDeleted &&
                value.Status != ProjectPaymentCertificateStatuses.Cancelled)
            .SumAsync(value => (decimal?)value.AdvanceRecoveryAmount, token) ?? 0m;
        if (committedByOthers + entity.AdvanceRecoveryAmount > agreement.OriginalAdvanceAmount)
            throw Conflict("Certificate recoveries exceed the original posted supplier advance.");
        var expected = QuantitySurveyAdvanceRecoveryRules.CalculateCertificateRecovery(agreement.OriginalAdvanceAmount,
            agreement.RecoveryPercentage, entity.GrossCertifiedAmount, committedByOthers);
        if (expected != entity.AdvanceRecoveryAmount)
            throw Conflict("The certificate advance recovery no longer matches the approved percentage and remaining balance.");
    }

    private async Task ValidateMaterialReconciliationAsync(ProjectPaymentCertificate entity, CancellationToken token)
    {
        if (!entity.QuantitySurveyMaterialReconciliationId.HasValue)
        {
            if (entity.MaterialOnSiteAmount != 0m || entity.MaterialOffSiteAmount != 0m || entity.MaterialDeductionAmount != 0m)
                throw Conflict("Certificate material values require an approved governed reconciliation.");
            return;
        }
        if (!entity.ContractId.HasValue || !entity.QuantitySurveyValuationWorksheetId.HasValue)
            throw Conflict("The certificate material reconciliation is missing its contract or valuation lineage.");
        var reconciliation = await db.QuantitySurveyMaterialReconciliations.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == entity.QuantitySurveyMaterialReconciliationId && !value.IsDeleted &&
            value.ProjectId == entity.ProjectId && value.ContractId == entity.ContractId &&
            value.ValuationWorksheetId == entity.QuantitySurveyValuationWorksheetId &&
            value.Status == QuantitySurveyMaterialReconciliationStatuses.Approved && value.ApprovalStatus == "Approved", token)
            ?? throw Conflict("The certificate's approved material reconciliation is unavailable.");
        if (reconciliation.MaterialOnSiteAmount != entity.MaterialOnSiteAmount ||
            reconciliation.MaterialOffSiteAmount != entity.MaterialOffSiteAmount ||
            reconciliation.TdcSuppliedDeductionAmount != entity.MaterialDeductionAmount)
            throw Conflict("Certificate material values no longer match the approved reconciliation.");
    }

    private async Task<(QuantitySurveyMaterialReconciliation? Reconciliation, decimal OnSite, decimal OffSite, decimal Deduction)>
        ResolveMaterialReconciliationAsync(Guid projectId, Guid contractId, Guid worksheetId,
            Guid? reconciliationId, Guid? certificateId, CancellationToken token)
    {
        if (!reconciliationId.HasValue) return (null, 0m, 0m, 0m);
        var reconciliation = await db.QuantitySurveyMaterialReconciliations.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == reconciliationId.Value && !value.IsDeleted &&
            value.ProjectId == projectId && value.ContractId == contractId && value.ValuationWorksheetId == worksheetId &&
            value.Status == QuantitySurveyMaterialReconciliationStatuses.Approved && value.ApprovalStatus == "Approved", token)
            ?? throw Validation("Select an approved material reconciliation for this valuation and Works contract.");
        var usedElsewhere = await db.Set<ProjectPaymentCertificate>().AsNoTracking().AnyAsync(value =>
            value.TenantId == TenantId && value.QuantitySurveyMaterialReconciliationId == reconciliation.Id &&
            value.Id != certificateId && !value.IsDeleted && value.Status != ProjectPaymentCertificateStatuses.Cancelled, token);
        if (usedElsewhere) throw Conflict("The approved material reconciliation is already linked to another active certificate.");
        return (reconciliation, reconciliation.MaterialOnSiteAmount, reconciliation.MaterialOffSiteAmount,
            reconciliation.TdcSuppliedDeductionAmount);
    }

    private async Task<(QuantitySurveyAdvanceRecoveryAgreement? Agreement, decimal Amount)> ResolveAdvanceRecoveryAsync(
        Guid projectId, Contract contract, Guid? agreementId, decimal grossAmount, Guid? certificateId, CancellationToken token)
    {
        var approved = await db.QuantitySurveyAdvanceRecoveryAgreements.AsNoTracking().Where(value =>
                value.TenantId == TenantId && value.ProjectId == projectId && value.ContractId == contract.Id && !value.IsDeleted &&
                value.Status == QuantitySurveyAdvanceRecoveryStatuses.Approved)
            .OrderBy(value => value.PreparedAt).ToListAsync(token);
        if (!agreementId.HasValue)
        {
            if (approved.Count == 0) return (null, 0m);
            throw Validation("Select the approved advance recovery agreement for this Works contract.");
        }
        var agreement = approved.SingleOrDefault(value => value.Id == agreementId.Value)
            ?? throw Conflict("The selected advance recovery agreement is not approved for this project and Works contract.");
        if (!string.Equals(agreement.CurrencyCodeSnapshot, contract.Currency, StringComparison.OrdinalIgnoreCase))
            throw Conflict("The advance recovery agreement currency no longer matches the Works contract.");
        var committed = await db.Set<ProjectPaymentCertificate>().AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.QuantitySurveyAdvanceRecoveryAgreementId == agreement.Id && value.Id != certificateId && !value.IsDeleted &&
                value.Status != ProjectPaymentCertificateStatuses.Cancelled)
            .SumAsync(value => (decimal?)value.AdvanceRecoveryAmount, token) ?? 0m;
        return (agreement, QuantitySurveyAdvanceRecoveryRules.CalculateCertificateRecovery(
            agreement.OriginalAdvanceAmount, agreement.RecoveryPercentage, grossAmount, committed));
    }

    private async Task<QuantitySurveyPaymentCertificateAmounts> CalculateAsync(decimal certifiedToDate,
        decimal previouslyCertified, decimal retentionHeld, decimal retentionReleased, decimal advanceRecovery,
        decimal materialOnSite, decimal materialOffSite, decimal materialDeduction, decimal otherDeductions,
        PaymentCertificatePolicy policy, DateTime date,
        CancellationToken token)
    {
        var preTax = decimal.Round(certifiedToDate - previouslyCertified + materialOnSite + materialOffSite + retentionReleased - retentionHeld -
            advanceRecovery - materialDeduction - otherDeductions, 2, MidpointRounding.AwayFromZero);
        if (preTax < 0m) throw Validation("Certificate deductions cannot exceed its current gross amount plus releases.");
        var tax = await taxEngine.CalculateTaxesAsync(new TaxCalculationRequestDto
        {
            BaseAmount = preTax, TaxGroupId = policy.Value.CertificateTaxGroupId,
            TransactionDate = date, TransactionType = TaxTransactionType.PurchaseOfServices
        }, token);
        var inclusive = policy.Value.TaxHandling == QuantitySurveyTaxHandling.Inclusive;
        var taxAmount = tax.TotalTaxAmount;
        if (inclusive && taxAmount > 0m)
        {
            var effectiveRate = taxAmount / preTax;
            taxAmount = decimal.Round(preTax * effectiveRate / (1m + effectiveRate), 2, MidpointRounding.AwayFromZero);
        }
        return QuantitySurveyPaymentCertificateRules.Calculate(certifiedToDate, previouslyCertified,
            retentionHeld, retentionReleased, advanceRecovery, materialOnSite, materialOffSite,
            materialDeduction, otherDeductions, taxAmount, !inclusive);
    }

    private async Task<ProjectPaymentCertificate?> PreviousCertificateAsync(Guid projectId, Guid? contractId,
        DateTime before, CancellationToken token) => !contractId.HasValue ? null : await db.Set<ProjectPaymentCertificate>()
        .AsNoTracking().Include(value => value.ProjectInterimValuation).Where(value => value.TenantId == TenantId && value.ProjectId == projectId &&
            value.ContractId == contractId && !value.IsDeleted &&
            value.ProjectInterimValuation != null && value.ProjectInterimValuation.ValuationDate < before &&
            (value.Status == ProjectPaymentCertificateStatuses.Approved || value.Status == ProjectPaymentCertificateStatuses.Paid) &&
            value.ApprovalStatus == "Approved")
        .OrderByDescending(value => value.ProjectInterimValuation!.ValuationDate).ThenByDescending(value => value.CreatedAt).FirstOrDefaultAsync(token);

    private IQueryable<ProjectPaymentCertificate> Query(bool tracked = false)
    {
        var query = db.Set<ProjectPaymentCertificate>().Where(value => value.TenantId == TenantId && !value.IsDeleted &&
            value.QuantitySurveyValuationWorksheetId != null)
            .Include(value => value.ProjectInterimValuation).Include(value => value.VendorInvoice);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<ProjectPaymentCertificate> RequiredAsync(Guid id, bool tracked, CancellationToken token) =>
        await Query(tracked).FirstOrDefaultAsync(value => value.Id == id, token)
        ?? throw NotFound("The governed payment certificate was not found.");

    private async Task RequireProjectAsync(Guid projectId)
    {
        if (!await projectService.HasProjectAccessAsync(projectId))
            throw new UnauthorizedAccessException("You are not permitted to access this project.");
    }

    private void AddHistory(ProjectPaymentCertificate entity, string action, object? before, object after, string correlationId) =>
        db.QuantitySurveyPaymentCertificateRevisions.Add(new QuantitySurveyPaymentCertificateRevision
        {
            TenantId = TenantId, PaymentCertificateId = entity.Id, Action = action,
            ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles,
            CorrelationId = Correlation(correlationId), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });

    private void AddAudit(ProjectPaymentCertificate entity, string action, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = ProjectPaymentCertificateAuditEvents.Resource, ResourceId = entity.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
            IpAddress = "api", UserAgent = "QuantitySurvey", Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });

    private static object Snapshot(ProjectPaymentCertificate value) => new
    {
        value.Id, value.ProjectId, value.ProjectInterimValuationId, value.QuantitySurveyValuationWorksheetId,
        value.PreviousPaymentCertificateId, value.QuantitySurveyAdvanceRecoveryAgreementId,
        value.QuantitySurveyMaterialReconciliationId, value.CertificateNumber, value.Status, value.ApprovalStatus,
        value.CertifiedToDateAmount, value.PreviouslyCertifiedAmount, value.GrossCertifiedAmount,
        value.RetentionHeldAmount, value.RetentionReleasedAmount, value.AdvanceRecoveryAmount,
        value.MaterialOnSiteAmount, value.MaterialOffSiteAmount, value.MaterialDeductionAmount,
        value.OtherDeductionsAmount, value.TaxAmount, value.NetCertifiedAmount,
        value.Currency, value.ConfigurationProfileId, value.ValuationDecisionId, value.ApprovalWorkflowDefinitionId,
        value.WorkflowInstanceId, value.CertificateTemplateId, value.CertificateTemplateVersionSnapshot,
        value.ExpenseAccountId, value.AccountsPayableAccountId, value.PaymentTermId, value.TaxGroupId,
        value.WithholdingTaxId, value.VendorInvoiceId, value.ApHandoffStatus, value.PaymentStatusSnapshot,
        value.CentralDocumentRecordId, value.CentralDocumentVersionId, value.GeneratedDocumentHash
    };

    private static QuantitySurveyPaymentCertificateAmounts Amounts(ProjectPaymentCertificate value) => new(
        value.CertifiedToDateAmount, value.PreviouslyCertifiedAmount, value.GrossCertifiedAmount,
        value.RetentionHeldAmount, value.RetentionReleasedAmount, value.AdvanceRecoveryAmount,
        value.MaterialOnSiteAmount, value.MaterialOffSiteAmount, value.MaterialDeductionAmount,
        value.OtherDeductionsAmount, value.TaxAmount, value.NetCertifiedAmount);

    private static void ApplyAmounts(ProjectPaymentCertificate entity, QuantitySurveyPaymentCertificateAmounts value)
    {
        entity.CertifiedToDateAmount = value.CertifiedToDate; entity.PreviouslyCertifiedAmount = value.PreviouslyCertified;
        entity.GrossCertifiedAmount = value.GrossCurrent; entity.RetentionHeldAmount = value.RetentionHeld;
        entity.RetentionReleasedAmount = value.RetentionReleased; entity.AdvanceRecoveryAmount = value.AdvanceRecovery;
        entity.MaterialOnSiteAmount = value.MaterialOnSite; entity.MaterialOffSiteAmount = value.MaterialOffSite;
        entity.MaterialDeductionAmount = value.MaterialDeduction; entity.OtherDeductionsAmount = value.OtherDeductions;
        entity.TaxAmount = value.Tax; entity.NetCertifiedAmount = value.NetCurrent;
    }

    private static QuantitySurveyPaymentCertificateDto Map(ProjectPaymentCertificate value)
    {
        var invoice = value.VendorInvoice;
        var reconciliation = QuantitySurveyPaymentCertificateReconciliationRules.Evaluate(
            value.Status,
            value.PaymentStatusSnapshot,
            value.NetCertifiedAmount,
            invoice?.Status,
            invoice?.TotalAmount,
            invoice?.PaidAmount,
            invoice?.JournalEntryId.HasValue == true);

        return new QuantitySurveyPaymentCertificateDto
        {
        Id = value.Id, ProjectId = value.ProjectId, ProjectInterimValuationId = value.ProjectInterimValuationId!.Value,
        QuantitySurveyValuationWorksheetId = value.QuantitySurveyValuationWorksheetId!.Value,
        PreviousPaymentCertificateId = value.PreviousPaymentCertificateId,
        AdvanceRecoveryAgreementId = value.QuantitySurveyAdvanceRecoveryAgreementId,
        MaterialReconciliationId = value.QuantitySurveyMaterialReconciliationId, VendorInvoiceId = value.VendorInvoiceId,
        VendorInvoiceNumber = value.VendorInvoice?.InvoiceNumber, CertificateNumber = value.CertificateNumber,
        Title = value.Title, Status = reconciliation.CertificateStatus, ApprovalStatus = value.ApprovalStatus,
        IssueDate = value.IssueDate, PaymentDueDate = value.PaymentDueDate, Currency = value.Currency,
        CertifiedToDateAmount = value.CertifiedToDateAmount, PreviouslyCertifiedAmount = value.PreviouslyCertifiedAmount,
        GrossCertifiedAmount = value.GrossCertifiedAmount, RetentionHeldAmount = value.RetentionHeldAmount,
        RetentionReleasedAmount = value.RetentionReleasedAmount, AdvanceRecoveryAmount = value.AdvanceRecoveryAmount,
        MaterialOnSiteAmount = value.MaterialOnSiteAmount, MaterialOffSiteAmount = value.MaterialOffSiteAmount,
        MaterialDeductionAmount = value.MaterialDeductionAmount, OtherDeductionsAmount = value.OtherDeductionsAmount,
        TaxAmount = value.TaxAmount, NetCertifiedAmount = value.NetCertifiedAmount, TaxHandling = value.TaxHandling,
        Notes = value.Notes, WorkflowInstanceId = value.WorkflowInstanceId,
        ApHandoffStatus = value.ApHandoffStatus, PaymentStatus = reconciliation.PaymentStatus,
        FinanceInvoiceAmount = invoice?.TotalAmount, FinancePaidAmount = invoice?.PaidAmount,
        FinanceBalanceAmount = invoice?.BalanceAmount, FinanceInvoiceJournalEntryId = invoice?.JournalEntryId,
        FinancePostingStatus = reconciliation.FinancePostingStatus,
        ReconciliationStatus = reconciliation.ReconciliationStatus,
        ApHandoffFailure = value.ApHandoffFailure,
        PaymentStatusUpdatedAt = invoice?.UpdatedAt ?? invoice?.CreatedAt ?? value.PaymentStatusUpdatedAt,
        DocumentGenerated = value.CentralDocumentRecordId.HasValue && value.CentralDocumentVersionId.HasValue,
        GeneratedAt = value.GeneratedAt, RowVersion = Convert.ToBase64String(value.RowVersion)
        };
    }

    private static bool IsRetry(ProjectPaymentCertificate value, Guid id, string hash) =>
        value.LastMutationClientRequestId == id && FixedEquals(value.LastMutationRequestHash, hash);
    private static void Touch(ProjectPaymentCertificate value, Guid id, string hash)
    {
        value.LastMutationClientRequestId = id; value.LastMutationRequestHash = hash;
        value.UpdatedAt = DateTime.UtcNow; // actor is applied immediately before save
    }
    private void ApplyActor(ProjectPaymentCertificate value)
    { value.UpdatedBy = UserName; value.LastModifiedById = UserId; value.UpdatedAt = DateTime.UtcNow; }
    private static void ApplyRowVersion(ProjectPaymentCertificate value, string encoded)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw Validation("The payment-certificate row version is invalid. Refresh and retry."); }

        if (expected.Length != value.RowVersion.Length ||
            !CryptographicOperations.FixedTimeEquals(expected, value.RowVersion))
        {
            throw Conflict("The payment certificate changed. Refresh and retry.");
        }
    }
    private async Task SaveAsync(CancellationToken token)
    {
        foreach (var item in db.ChangeTracker.Entries<ProjectPaymentCertificate>().Where(value => value.State == EntityState.Modified)) ApplyActor(item.Entity);
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The payment certificate changed. Refresh and retry."); }
    }
    private static string RequiredText(string? value, string label)
    { var text = Normalize(value); return text is { Length: >= 5 and <= 2000 } ? text : throw Validation($"{label} must contain 5 to 2000 characters."); }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static Guid DerivedRequestId(Guid sourceRequestId, string purpose)
    {
        var source = Encoding.UTF8.GetBytes($"{sourceRequestId:N}|{purpose}");
        var hash = SHA256.HashData(source);
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, bytes.Length).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
    private static bool FixedEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
    private static QuantitySurveyPaymentCertificateNotFoundException NotFound(string value) => new(value);
    private static QuantitySurveyPaymentCertificateValidationException Validation(string value) => new(value);
    private static QuantitySurveyPaymentCertificateConflictException Conflict(string value) => new(value);
    private static QuantitySurveyPaymentCertificateConflictException RetryConflict() => Conflict("This client request identifier is already bound to different certificate inputs.");

    private sealed record PaymentCertificatePolicy(QuantitySurveyConfigurationProfile Profile,
        QuantitySurveyConfigurationDecision Decision, QsValuationCertificateValue Value,
        WorkflowDefinition Workflow, ReportTemplate Template,
        ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate MetadataTemplate,
        string Hash);

    private sealed class CertificateWorkflowAdapter(ProjectPaymentCertificate entity) : IQuantitySurveyWorkflowRecord
    {
        public string Status { get => entity.Status; set => entity.Status = value; }
        public string ApprovalStatus { get => entity.ApprovalStatus; set => entity.ApprovalStatus = value; }
        public Guid? ApprovedById { get => entity.ApprovedById; set => entity.ApprovedById = value; }
        public DateTime? ApprovedAt { get => entity.ApprovedAt; set => entity.ApprovedAt = value; }
        public string? RejectionReason { get => entity.RejectionReason; set => entity.RejectionReason = value; }
    }
}
