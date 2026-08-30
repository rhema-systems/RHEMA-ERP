using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyEscalationCalculationService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IWorkflowIntegrationService workflow,
    IWorkflowStatusAdapterRegistry workflowAdapters) : IQuantitySurveyEscalationCalculationService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly IReadOnlySet<string> AllowedStatuses = new HashSet<string>(
        ["Draft", "PendingApproval", "ApprovedPendingApplication", "Rejected"],
        StringComparer.Ordinal);

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value
        : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value
        : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value));

    public async Task<QuantitySurveyEscalationCalculationLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        var accessible = await AccessibleProjectIdsAsync();
        var now = DateTime.UtcNow.Date;
        var formulas = await db.QuantitySurveyEscalationFormulas.AsNoTracking()
            .Include(value => value.Project)
            .Include(value => value.Contract)
            .Where(value => value.TenantId == TenantId && accessible.Contains(value.ProjectId) &&
                            !value.IsDeleted && value.Status == "Approved" && value.ApprovalStatus == "Approved" &&
                            value.EffectiveFrom <= now && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
            .OrderBy(value => value.Project.ProjectCode).ThenBy(value => value.Code).ThenByDescending(value => value.Version)
            .Select(value => new QuantitySurveyEscalationCalculationLookupDto
            {
                Id = value.Id,
                Label = value.Code + "/v" + value.Version + " · " + value.Name,
                Group = value.Project.ProjectCode + " · " + value.Contract.ContractNumber,
                Status = value.Status
            })
            .ToListAsync(cancellationToken);
        return new QuantitySurveyEscalationCalculationLookupsDto { Formulas = formulas };
    }

    public async Task<QuantitySurveyEscalationImpactTargetsDto> GetImpactTargetsAsync(Guid formulaId, CancellationToken cancellationToken = default)
    {
        var formula = await RequiredFormulaAsync(formulaId, cancellationToken);
        await RequireProjectAccessAsync(formula.ProjectId);
        var certificateRows = await db.ProjectPaymentCertificates.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == formula.ProjectId &&
                            value.ContractId == formula.ContractId && !value.IsDeleted &&
                            (value.Status == ProjectPaymentCertificateStatuses.Draft || value.Status == ProjectPaymentCertificateStatuses.Issued) &&
                            value.GrossCertifiedAmount > 0m)
            .OrderByDescending(value => value.IssueDate)
            .Select(value => new { value.Id, value.CertificateNumber, value.Title, value.IssueDate, value.GrossCertifiedAmount, value.Currency, value.Status })
            .ToListAsync(cancellationToken);
        var certificates = certificateRows.Select(value => new QuantitySurveyEscalationCalculationLookupDto
        {
            Id = value.Id,
            Label = (value.CertificateNumber ?? "Unnumbered certificate") + " · " + value.Title,
            Group = value.IssueDate.ToString("yyyy-MM-dd"),
            Amount = value.GrossCertifiedAmount,
            CurrencyCode = value.Currency,
            Status = value.Status
        }).ToList();
        var finalAccountRows = await db.ProjectFinalAccounts.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == formula.ProjectId &&
                            value.ContractId == formula.ContractId && !value.IsDeleted &&
                            (value.Status == ProjectFinalAccountStatuses.Draft || value.Status == ProjectFinalAccountStatuses.UnderReview) &&
                            value.FinalAccountValue > 0m)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new { value.Id, value.Currency, value.FinalAccountValue, value.SettlementDate, value.Status })
            .ToListAsync(cancellationToken);
        var finalAccounts = finalAccountRows.Select(value => new QuantitySurveyEscalationCalculationLookupDto
        {
            Id = value.Id,
            Label = $"Final account · {value.Currency} {value.FinalAccountValue:N2}",
            Group = value.SettlementDate?.ToString("yyyy-MM-dd") ?? "Settlement date pending",
            Amount = value.FinalAccountValue,
            CurrencyCode = value.Currency,
            Status = value.Status
        }).ToList();
        return new QuantitySurveyEscalationImpactTargetsDto
        {
            PaymentCertificates = certificates,
            FinalAccounts = finalAccounts
        };
    }

    public async Task<QuantitySurveyEscalationCalculationPageDto> ListAsync(
        QuantitySurveyEscalationCalculationListRequest request,
        CancellationToken cancellationToken = default)
    {
        var accessible = await AccessibleProjectIdsAsync();
        var query = RunQuery().Where(value => accessible.Contains(value.ProjectId));
        if (request.ProjectId.HasValue) query = query.Where(value => value.ProjectId == request.ProjectId.Value);
        if (request.FormulaId.HasValue) query = query.Where(value => value.FormulaId == request.FormulaId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            if (!AllowedStatuses.Contains(status))
                throw new QuantitySurveyEscalationCalculationValidationException("Select a valid calculation status.");
            query = query.Where(value => value.Status == status);
        }

        var page = Math.Clamp(request.Page, 1, 1_000_000);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var count = await query.CountAsync(cancellationToken);
        var values = await query.OrderByDescending(value => value.PreparedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new QuantitySurveyEscalationCalculationPageDto
        {
            Items = values.Select(Map).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = count
        };
    }

    public async Task<QuantitySurveyEscalationCalculationDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var value = await RequiredAsync(id, false, cancellationToken);
        await RequireProjectAccessAsync(value.ProjectId);
        return Map(value);
    }

    public async Task<QuantitySurveyEscalationCalculationDto> CalculateAsync(
        CreateQuantitySurveyEscalationCalculationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw new QuantitySurveyEscalationCalculationValidationException("A client request ID is required for safe retry.");
        if (request.FormulaId == Guid.Empty || request.ImpactTargetId == Guid.Empty)
            throw new QuantitySurveyEscalationCalculationValidationException("Select an approved formula and a controlled impact target.");
        var reason = RequireText(request.Reason, "Calculation reason", 1000);
        var currentPeriod = NormalizeMonth(request.CurrentIndexPeriod, "Current index period");
        if (currentPeriod > FirstOfMonth(DateTime.UtcNow))
            throw new QuantitySurveyEscalationCalculationValidationException("The current index period cannot be in the future.");
        if (!Enum.IsDefined(request.ImpactTargetType))
            throw new QuantitySurveyEscalationCalculationValidationException("Select a valid certificate or final-account impact target.");

        var requestHash = Hash(JsonSerializer.Serialize(new
        {
            request.ClientRequestId,
            request.FormulaId,
            request.ImpactTargetType,
            request.ImpactTargetId,
            CurrentIndexPeriod = currentPeriod,
            Reason = reason
        }, JsonOptions));
        var existing = await RunQuery().FirstOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, cancellationToken);
        if (existing is not null)
        {
            if (!FixedEquals(existing.RequestHash, requestHash))
                throw new QuantitySurveyEscalationCalculationConflictException("This client request ID was already used with a different calculation payload.");
            return Map(existing);
        }

        var formula = await RequiredFormulaAsync(request.FormulaId, cancellationToken);
        await RequireProjectAccessAsync(formula.ProjectId);
        ValidateFormulaForPeriod(formula, currentPeriod);
        var target = await ResolveTargetAsync(formula, request.ImpactTargetType, request.ImpactTargetId, cancellationToken);
        if (!string.Equals(target.CurrencyCode, formula.Contract.Currency, StringComparison.OrdinalIgnoreCase))
            throw new QuantitySurveyEscalationCalculationValidationException("The impact target currency must match the Works contract currency.");
        var basePeriod = FirstOfMonth(formula.BaseDate);
        if (currentPeriod < basePeriod)
            throw new QuantitySurveyEscalationCalculationValidationException("The current index period cannot be before the formula base-index period.");

        var indexValues = await ResolveIndexValuesAsync(formula, basePeriod, currentPeriod, cancellationToken);
        QuantitySurveyEscalationCalculationResult calculation;
        try
        {
            calculation = QuantitySurveyEscalationCalculationRules.Calculate(
                target.BaseAmount,
                indexValues.Select(value => new QuantitySurveyEscalationCalculationInput(
                    value.Component.Component,
                    value.Component.Coefficient,
                    value.Base.IndexValue,
                    value.Current.IndexValue)));
        }
        catch (ArgumentException exception)
        {
            throw new QuantitySurveyEscalationCalculationValidationException(exception.Message);
        }

        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        var entity = new QuantitySurveyEscalationCalculationRun
        {
            Id = id,
            TenantId = TenantId,
            RunReference = $"ESC-{now:yyyyMMdd}-{id:N}"[..21].ToUpperInvariant(),
            ClientRequestId = request.ClientRequestId,
            RequestHash = requestHash,
            FormulaId = formula.Id,
            FormulaKeySnapshot = formula.FormulaKey,
            FormulaVersionSnapshot = formula.Version,
            FormulaCodeSnapshot = formula.Code,
            ProjectId = formula.ProjectId,
            ContractId = formula.ContractId,
            ContractNumberSnapshot = formula.Contract.ContractNumber,
            BaseIndexPeriod = basePeriod,
            CurrentIndexPeriod = currentPeriod,
            CalculationDate = now.Date,
            ImpactTargetType = request.ImpactTargetType,
            PaymentCertificateId = request.ImpactTargetType == QuantitySurveyEscalationImpactTargetType.PaymentCertificate ? request.ImpactTargetId : null,
            FinalAccountId = request.ImpactTargetType == QuantitySurveyEscalationImpactTargetType.FinalAccount ? request.ImpactTargetId : null,
            ImpactTargetReferenceSnapshot = target.Reference,
            ImpactTargetStatusSnapshot = target.Status,
            ImpactTargetSnapshotHash = target.SnapshotHash,
            CurrencyCode = target.CurrencyCode,
            BaseRate = calculation.BaseRate,
            RevisedRate = calculation.RevisedRate,
            AdjustmentFactor = calculation.AdjustmentFactor,
            CalculatedFluctuationAmount = calculation.CalculatedFluctuationAmount,
            ReviewerAdjustmentAmount = 0m,
            ApprovedImpactAmount = calculation.ApprovedImpactAmount,
            ImpactApplicationStatus = "Projected",
            AuthorityRoleId = formula.AuthorityRoleId,
            AuthorityRoleNameSnapshot = formula.AuthorityRoleNameSnapshot,
            ConfigurationProfileId = formula.ConfigurationProfileId,
            ConfigurationDecisionId = formula.ConfigurationDecisionId,
            ApprovalWorkflowDefinitionId = formula.ApprovalWorkflowDefinitionId,
            Status = "Draft",
            ApprovalStatus = "Draft",
            PreparedById = UserId,
            PreparedAt = now,
            AuditAction = QuantitySurveyAuditEventMap.CalculateEscalationRun,
            CorrelationId = NormalizeCorrelation(correlationId),
            ChangeReason = reason,
            ActorRoles = ActorRoles,
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId,
            Lines = indexValues.Select((value, sequence) =>
            {
                var result = calculation.Lines.Single(item => item.Component == value.Component.Component);
                return new QuantitySurveyEscalationCalculationLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    Sequence = sequence + 1,
                    Component = value.Component.Component,
                    Coefficient = value.Component.Coefficient,
                    IndexFamilyId = value.Component.IndexFamilyId,
                    IndexFamilyCodeSnapshot = value.Component.IndexFamilyCodeSnapshot,
                    BaseIndexValueId = value.Base.Id,
                    CurrentIndexValueId = value.Current.Id,
                    BaseIndexValue = value.Base.IndexValue,
                    CurrentIndexValue = value.Current.IndexValue,
                    IndexRatio = result.IndexRatio,
                    WeightedContribution = result.WeightedContribution,
                    CreatedAt = now,
                    CreatedBy = UserName,
                    CreatedById = UserId
                };
            }).ToList()
        };
        entity.SnapshotHash = ComputeSnapshot(entity);
        db.QuantitySurveyEscalationCalculationRuns.Add(entity);
        AddRevision(entity, QuantitySurveyAuditEventMap.CalculateEscalationRun, reason, null, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.CalculateEscalationRun, null, Snapshot(entity), correlationId);
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUnique(exception))
        {
            db.ChangeTracker.Clear();
            var retry = await RunQuery().FirstOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, cancellationToken);
            if (retry is null || !FixedEquals(retry.RequestHash, requestHash))
                throw new QuantitySurveyEscalationCalculationConflictException("The calculation request was created concurrently with a different payload. Refresh and retry.");
            return Map(retry);
        }
        return await GetAsync(entity.Id, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationCalculationDto> SubmitAsync(
        Guid id,
        QuantitySurveyEscalationCalculationLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await RequiredAsync(id, true, cancellationToken);
        await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.Status is "PendingApproval" or "ApprovedPendingApplication") return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status is not ("Draft" or "Rejected"))
            throw new QuantitySurveyEscalationCalculationConflictException("Only a Draft or Rejected calculation can be submitted.");
        var reason = RequireText(request.Reason, "Submission reason", 1000);
        await ValidateStoredAsync(entity, cancellationToken);
        var before = Snapshot(entity);
        var result = await workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, entity.ApprovalWorkflowDefinitionId);
        if (!result.ExecutionResult.Success)
            throw new QuantitySurveyEscalationCalculationConflictException(result.ExecutionResult.Message ?? "The configured escalation workflow could not be started.");
        workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Escalation).ApplySubmitOutcome(entity, result.Outcome, UserId);
        if (result.Outcome == WorkflowOutcome.Approved)
        {
            entity.Status = "PendingApproval";
            entity.ApprovalStatus = "Pending";
            entity.ApprovedById = null;
            entity.ApprovedAt = null;
        }
        else if (result.Outcome == WorkflowOutcome.Rejected)
        {
            entity.RejectionReason = reason;
        }
        entity.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
        entity.SubmittedById = UserId;
        entity.SubmittedAt = DateTime.UtcNow;
        Touch(entity, QuantitySurveyAuditEventMap.SubmitEscalationRun, reason, correlationId);
        entity.SnapshotHash = ComputeSnapshot(entity);
        AddRevision(entity, QuantitySurveyAuditEventMap.SubmitEscalationRun, reason, before, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.SubmitEscalationRun, before, Snapshot(entity), correlationId);
        await SaveAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationCalculationDto> ReviewAdjustmentAsync(
        Guid id,
        ReviewQuantitySurveyEscalationCalculationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await RequiredAsync(id, true, cancellationToken);
        await RequireProjectAccessAsync(entity.ProjectId);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status != "PendingApproval")
            throw new QuantitySurveyEscalationCalculationConflictException("Reviewer adjustments can be recorded only while the calculation is PendingApproval.");
        if (entity.PreparedById == UserId)
            throw new QuantitySurveyEscalationCalculationConflictException("Maker-checker control prevents the calculation preparer from reviewing it.");
        RequireAuthorityRole(entity);
        if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId))
            throw new UnauthorizedAccessException("You are not assigned to the current escalation approval step.");
        var reason = RequireText(request.Reason, "Reviewer adjustment reason", 1000);
        if (reason.Length < 10)
            throw new QuantitySurveyEscalationCalculationValidationException("Reviewer adjustment reason must contain at least 10 characters.");
        await ValidateStoredAsync(entity, cancellationToken);
        QuantitySurveyEscalationCalculationResult calculation;
        try
        {
            calculation = QuantitySurveyEscalationCalculationRules.Calculate(
                entity.BaseRate,
                entity.Lines.Select(value => new QuantitySurveyEscalationCalculationInput(
                    value.Component, value.Coefficient, value.BaseIndexValue, value.CurrentIndexValue)),
                request.AdjustmentAmount);
        }
        catch (ArgumentException exception)
        {
            throw new QuantitySurveyEscalationCalculationValidationException(exception.Message);
        }
        var before = Snapshot(entity);
        entity.ReviewerAdjustmentAmount = calculation.ReviewerAdjustmentAmount;
        entity.ReviewerAdjustmentReason = reason;
        entity.ReviewedById = UserId;
        entity.ReviewedAt = DateTime.UtcNow;
        entity.ApprovedImpactAmount = calculation.ApprovedImpactAmount;
        Touch(entity, QuantitySurveyAuditEventMap.ReviewEscalationRun, reason, correlationId);
        entity.SnapshotHash = ComputeSnapshot(entity);
        AddRevision(entity, QuantitySurveyAuditEventMap.ReviewEscalationRun, reason, before, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.ReviewEscalationRun, before, Snapshot(entity), correlationId);
        await SaveAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationCalculationDto> ApproveAsync(
        Guid id,
        QuantitySurveyEscalationCalculationLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await RequiredAsync(id, true, cancellationToken);
        await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.Status == "ApprovedPendingApplication") return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status != "PendingApproval")
            throw new QuantitySurveyEscalationCalculationConflictException("The calculation must be PendingApproval before approval.");
        if (entity.PreparedById == UserId)
            throw new QuantitySurveyEscalationCalculationConflictException("Maker-checker control prevents the calculation preparer from approving it.");
        RequireAuthorityRole(entity);
        var reason = RequireText(request.Reason, "Approval reason", 1000);
        await ValidateStoredAsync(entity, cancellationToken);
        var status = await WorkflowStatusAsync(entity, cancellationToken);
        WorkflowOutcome outcome;
        if (status == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
        else
        {
            if (status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                throw new QuantitySurveyEscalationCalculationConflictException("The escalation workflow ended without approval.");
            if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId))
                throw new UnauthorizedAccessException("You are not assigned to the current escalation approval step.");
            var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId, "Approve", reason);
            if (!result.ExecutionResult.Success)
                throw new QuantitySurveyEscalationCalculationConflictException(result.ExecutionResult.Message ?? "The escalation calculation approval could not be processed.");
            outcome = result.Outcome;
        }
        if (outcome == WorkflowOutcome.Approved)
            await FinalizeApprovalAsync(id, reason, correlationId, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationCalculationDto> RejectAsync(
        Guid id,
        QuantitySurveyEscalationCalculationLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await RequiredAsync(id, true, cancellationToken);
        await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.Status == "Rejected") return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status != "PendingApproval")
            throw new QuantitySurveyEscalationCalculationConflictException("The calculation must be PendingApproval before rejection.");
        if (entity.PreparedById == UserId)
            throw new QuantitySurveyEscalationCalculationConflictException("Maker-checker control prevents the calculation preparer from rejecting it.");
        RequireAuthorityRole(entity);
        var reason = RequireText(request.Reason, "Rejection reason", 1000);
        var status = await WorkflowStatusAsync(entity, cancellationToken);
        if (status == WorkflowInstanceStatus.Completed)
            throw new QuantitySurveyEscalationCalculationConflictException("The completed escalation workflow is approved and cannot be rejected.");
        WorkflowOutcome outcome;
        if (status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) outcome = WorkflowOutcome.Rejected;
        else
        {
            if (!await workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId))
                throw new UnauthorizedAccessException("You are not assigned to the current escalation approval step.");
            var result = await workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId, "Reject", reason);
            if (!result.ExecutionResult.Success)
                throw new QuantitySurveyEscalationCalculationConflictException(result.ExecutionResult.Message ?? "The escalation calculation rejection could not be processed.");
            outcome = result.Outcome;
        }
        if (outcome != WorkflowOutcome.Rejected)
            throw new QuantitySurveyEscalationCalculationConflictException("The shared workflow did not return a rejected outcome.");
        var before = Snapshot(entity);
        workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Escalation).ApplyApprovalOutcome(entity, outcome, UserId, reason);
        Touch(entity, QuantitySurveyAuditEventMap.RejectEscalationRun, reason, correlationId);
        entity.SnapshotHash = ComputeSnapshot(entity);
        AddRevision(entity, QuantitySurveyAuditEventMap.RejectEscalationRun, reason, before, Snapshot(entity), correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.RejectEscalationRun, before, Snapshot(entity), correlationId);
        await SaveAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<QuantitySurveyEscalationCalculationRevisionDto>> HistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequiredAsync(id, false, cancellationToken);
        await RequireProjectAccessAsync(entity.ProjectId);
        return await db.QuantitySurveyEscalationCalculationRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.CalculationRunId == id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new QuantitySurveyEscalationCalculationRevisionDto
            {
                Id = value.Id,
                Action = value.Action,
                ActorUserId = value.ActorUserId,
                ActorName = value.ActorName,
                ActorRoles = value.ActorRoles,
                CorrelationId = value.CorrelationId,
                Reason = value.Reason,
                BeforeJson = value.BeforeJson,
                AfterJson = value.AfterJson,
                CreatedAt = value.CreatedAt
            }).ToListAsync(cancellationToken);
    }

    private async Task FinalizeApprovalAsync(Guid id, string reason, string correlationId, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var entity = await RequiredAsync(id, true, cancellationToken);
            if (entity.Status == "ApprovedPendingApplication") { await transaction.CommitAsync(cancellationToken); return; }
            if (entity.Status != "PendingApproval")
                throw new QuantitySurveyEscalationCalculationConflictException("The calculation must remain PendingApproval until final workflow approval.");
            if (entity.PreparedById == UserId)
                throw new QuantitySurveyEscalationCalculationConflictException("Maker-checker control prevents the calculation preparer from approving it.");
            RequireAuthorityRole(entity);
            await ValidateStoredAsync(entity, cancellationToken);
            var before = Snapshot(entity);
            workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Escalation).ApplyApprovalOutcome(entity, WorkflowOutcome.Approved, UserId, null);
            entity.Status = "ApprovedPendingApplication";
            entity.ApprovalStatus = "Approved";
            entity.ImpactApplicationStatus = "PendingApplication";
            Touch(entity, QuantitySurveyAuditEventMap.ApproveEscalationRun, reason, correlationId);
            entity.SnapshotHash = ComputeSnapshot(entity);
            AddRevision(entity, QuantitySurveyAuditEventMap.ApproveEscalationRun, reason, before, Snapshot(entity), correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.ApproveEscalationRun, before, Snapshot(entity), correlationId);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task ApplyApprovedFinalAccountImpactsAsync(
        Guid finalAccountId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (finalAccountId == Guid.Empty)
            throw new QuantitySurveyEscalationCalculationValidationException("A governed final account is required before escalation impacts can be applied.");
        if (db.Database.CurrentTransaction is null)
            throw new QuantitySurveyEscalationCalculationConflictException("Escalation impacts must be applied inside the governed final-account transaction.");

        var finalAccount = await db.ProjectFinalAccounts.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == finalAccountId && !value.IsDeleted,
            cancellationToken) ?? throw new QuantitySurveyEscalationCalculationNotFoundException("The final account selected for escalation application was not found.");
        if (finalAccount.Status != ProjectFinalAccountStatuses.Draft)
            throw new QuantitySurveyEscalationCalculationConflictException("Approved escalation impacts can be applied only while the final account is Draft. Refresh the final account before calculating a replacement escalation run.");

        var runs = await db.QuantitySurveyEscalationCalculationRuns.AsTracking()
            .Include(value => value.Lines)
            .Where(value => value.TenantId == TenantId && value.FinalAccountId == finalAccountId &&
                            value.ProjectId == finalAccount.ProjectId && value.ContractId == finalAccount.ContractId &&
                            !value.IsDeleted && value.Status == "ApprovedPendingApplication" &&
                            value.ApprovalStatus == "Approved" && value.ImpactApplicationStatus == "PendingApplication")
            .OrderBy(value => value.ApprovedAt)
            .ThenBy(value => value.RunReference)
            .ToListAsync(cancellationToken);
        if (runs.Count == 0) return;

        var runIds = runs.Select(value => value.Id).ToList();
        var disputes = await db.QuantitySurveyEscalationDisputes.AsNoTracking()
            .Where(value => value.TenantId == TenantId && runIds.Contains(value.CalculationRunId) && !value.IsDeleted)
            .Select(value => new { value.CalculationRunId, value.Status, value.Outcome })
            .ToListAsync(cancellationToken);
        var blockedRunIds = disputes
            .Where(value => value.Status != "Resolved" || value.Outcome != QuantitySurveyEscalationDisputeOutcome.Accepted)
            .Select(value => value.CalculationRunId)
            .ToHashSet();
        if (blockedRunIds.Count > 0)
            throw new QuantitySurveyEscalationCalculationConflictException("Resolve every escalation dispute with an Accepted outcome before applying it. Rejected or partially accepted outcomes require a replacement calculation.");

        const string reason = "Applied to governed final-account reconciliation.";
        foreach (var run in runs)
        {
            await ValidateStoredAsync(run, cancellationToken);
            var before = Snapshot(run);
            run.ImpactApplicationStatus = "Applied";
            Touch(run, QuantitySurveyAuditEventMap.ApplyEscalationToFinalAccount, reason, correlationId);
            run.SnapshotHash = ComputeSnapshot(run);
            var after = Snapshot(run);
            AddRevision(run, QuantitySurveyAuditEventMap.ApplyEscalationToFinalAccount, reason, before, after, correlationId);
            AddAudit(run, QuantitySurveyAuditEventMap.ApplyEscalationToFinalAccount, before, after, correlationId);
        }

        // Persist inside the caller-owned serializable transaction while the target still
        // matches the approved calculation snapshot. A later final-account failure rolls
        // this save back with the same transaction.
        await SaveAsync(cancellationToken);
    }

    private async Task ValidateStoredAsync(QuantitySurveyEscalationCalculationRun entity, CancellationToken cancellationToken)
    {
        var formula = await RequiredFormulaAsync(entity.FormulaId, cancellationToken);
        ValidateFormulaForPeriod(formula, entity.CurrentIndexPeriod);
        if (formula.FormulaKey != entity.FormulaKeySnapshot || formula.Version != entity.FormulaVersionSnapshot ||
            formula.Code != entity.FormulaCodeSnapshot || formula.ProjectId != entity.ProjectId || formula.ContractId != entity.ContractId ||
            formula.AuthorityRoleId != entity.AuthorityRoleId || formula.AuthorityRoleNameSnapshot != entity.AuthorityRoleNameSnapshot ||
            formula.ConfigurationProfileId != entity.ConfigurationProfileId || formula.ConfigurationDecisionId != entity.ConfigurationDecisionId ||
            formula.ApprovalWorkflowDefinitionId != entity.ApprovalWorkflowDefinitionId)
        {
            throw new QuantitySurveyEscalationCalculationConflictException("The approved formula or its QS policy lineage changed. Calculate a new run.");
        }
        var policyLineageValid = await db.QuantitySurveyConfigurationProfiles.AsNoTracking()
            .AnyAsync(value => value.TenantId == TenantId && value.Id == entity.ConfigurationProfileId &&
                               !value.IsDeleted && value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published,
                cancellationToken) &&
            await db.QuantitySurveyConfigurationDecisions.AsNoTracking()
                .AnyAsync(value => value.TenantId == TenantId && value.Id == entity.ConfigurationDecisionId &&
                                   value.ProfileId == entity.ConfigurationProfileId && value.DecisionKey == "QS-DEC-006" &&
                                   !value.IsDeleted && value.Status == QuantitySurveyConfigurationDecisionStatus.Approved &&
                                   value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved &&
                                   value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified,
                    cancellationToken);
        if (!policyLineageValid)
            throw new QuantitySurveyEscalationCalculationConflictException("The formula's approved QS-DEC-006 policy lineage is no longer valid. Calculate a new run under an approved formula.");
        var authorityValid = await db.Roles.AsNoTracking().AnyAsync(value =>
            value.Id == entity.AuthorityRoleId && value.Name == entity.AuthorityRoleNameSnapshot,
            cancellationToken);
        if (!authorityValid)
            throw new QuantitySurveyEscalationCalculationConflictException("The configured QS-DEC-001 approval authority is no longer available.");
        var workflowValid = await db.WorkflowDefinitions.AsNoTracking()
            .Include(value => value.EntityType)
            .AnyAsync(value => value.TenantId == TenantId && value.Id == entity.ApprovalWorkflowDefinitionId &&
                               !value.IsDeleted && value.IsActive &&
                               value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                               !value.EntityType.IsDeleted && value.EntityType.IsActive &&
                               value.EntityType.Code == QuantitySurveyWorkflowBindingRegistry.Escalation,
                cancellationToken);
        if (!workflowValid)
            throw new QuantitySurveyEscalationCalculationConflictException("The configured Published QS escalation workflow is no longer available.");
        var targetId = entity.ImpactTargetType == QuantitySurveyEscalationImpactTargetType.PaymentCertificate
            ? entity.PaymentCertificateId
            : entity.FinalAccountId;
        if (!targetId.HasValue)
            throw new QuantitySurveyEscalationCalculationConflictException("The stored impact target lineage is incomplete.");
        var target = await ResolveTargetAsync(formula, entity.ImpactTargetType, targetId.Value, cancellationToken);
        if (!FixedEquals(entity.ImpactTargetSnapshotHash, target.SnapshotHash) ||
            entity.BaseRate != target.BaseAmount || !string.Equals(entity.CurrencyCode, target.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new QuantitySurveyEscalationCalculationConflictException("The selected certificate or final account changed after calculation. Calculate a new run from its current values.");
        }
        if (entity.Lines.Count != formula.Components.Count || entity.Lines.Count == 0)
            throw new QuantitySurveyEscalationCalculationConflictException("The stored calculation components no longer match the approved formula.");
        foreach (var line in entity.Lines)
        {
            var component = formula.Components.SingleOrDefault(value => value.Component == line.Component);
            if (component is null || component.IndexFamilyId != line.IndexFamilyId || component.Coefficient != line.Coefficient)
                throw new QuantitySurveyEscalationCalculationConflictException("The stored calculation components no longer match the approved formula.");
            var valuesCurrent = await db.QuantitySurveyPriceIndexValues.AsNoTracking().CountAsync(value =>
                value.TenantId == TenantId && !value.IsDeleted && value.IsCurrent && value.Status == "Approved" &&
                value.IndexFamilyId == line.IndexFamilyId &&
                ((value.Id == line.BaseIndexValueId && value.IndexPeriod == entity.BaseIndexPeriod && value.IndexValue == line.BaseIndexValue) ||
                 (value.Id == line.CurrentIndexValueId && value.IndexPeriod == entity.CurrentIndexPeriod && value.IndexValue == line.CurrentIndexValue)), cancellationToken);
            var requiredCount = line.BaseIndexValueId == line.CurrentIndexValueId ? 1 : 2;
            if (valuesCurrent != requiredCount)
                throw new QuantitySurveyEscalationCalculationConflictException("A selected base or current price index is no longer the approved current revision. Calculate a new run.");
        }
        QuantitySurveyEscalationCalculationResult calculated;
        try
        {
            calculated = QuantitySurveyEscalationCalculationRules.Calculate(entity.BaseRate,
                entity.Lines.Select(value => new QuantitySurveyEscalationCalculationInput(value.Component, value.Coefficient, value.BaseIndexValue, value.CurrentIndexValue)),
                entity.ReviewerAdjustmentAmount);
        }
        catch (ArgumentException exception)
        {
            throw new QuantitySurveyEscalationCalculationConflictException(exception.Message);
        }
        if (entity.RevisedRate != calculated.RevisedRate || entity.AdjustmentFactor != calculated.AdjustmentFactor ||
            entity.CalculatedFluctuationAmount != calculated.CalculatedFluctuationAmount || entity.ApprovedImpactAmount != calculated.ApprovedImpactAmount ||
            !FixedEquals(entity.SnapshotHash, ComputeSnapshot(entity)))
        {
            throw new QuantitySurveyEscalationCalculationConflictException("The stored escalation calculation failed its integrity check.");
        }
    }

    private async Task<QuantitySurveyEscalationFormulaDefinition> RequiredFormulaAsync(Guid id, CancellationToken cancellationToken)
        => await db.QuantitySurveyEscalationFormulas.AsNoTracking()
               .Include(value => value.Project)
               .Include(value => value.Contract)
               .Include(value => value.Components)
               .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, cancellationToken)
           ?? throw new QuantitySurveyEscalationCalculationValidationException("The selected escalation formula is unavailable in this tenant.");

    private static void ValidateFormulaForPeriod(QuantitySurveyEscalationFormulaDefinition formula, DateTime currentPeriod)
    {
        if (formula.Status != "Approved" || formula.ApprovalStatus != "Approved")
            throw new QuantitySurveyEscalationCalculationValidationException("Select an Approved escalation formula.");
        if (formula.EffectiveFrom.Date > currentPeriod || (formula.EffectiveTo.HasValue && formula.EffectiveTo.Value.Date < currentPeriod))
            throw new QuantitySurveyEscalationCalculationValidationException("The selected escalation formula is not effective for the current index period.");
        if (!string.Equals(formula.Contract.ContractType, "Works", StringComparison.OrdinalIgnoreCase))
            throw new QuantitySurveyEscalationCalculationValidationException("Escalation calculations require a project-linked Works contract.");
        if (formula.Components.Count != Enum.GetValues<QuantitySurveyEscalationComponentType>().Length)
            throw new QuantitySurveyEscalationCalculationValidationException("The approved formula does not contain the complete controlled component set.");
    }

    private async Task<IReadOnlyList<ResolvedIndexValues>> ResolveIndexValuesAsync(
        QuantitySurveyEscalationFormulaDefinition formula,
        DateTime basePeriod,
        DateTime currentPeriod,
        CancellationToken cancellationToken)
    {
        var familyIds = formula.Components.Select(value => value.IndexFamilyId).Distinct().ToList();
        var values = await db.QuantitySurveyPriceIndexValues.AsNoTracking()
            .Where(value => value.TenantId == TenantId && familyIds.Contains(value.IndexFamilyId) &&
                            !value.IsDeleted && value.IsCurrent && value.Status == "Approved" &&
                            (value.IndexPeriod == basePeriod || value.IndexPeriod == currentPeriod))
            .ToListAsync(cancellationToken);
        var result = new List<ResolvedIndexValues>();
        foreach (var component in formula.Components.OrderBy(value => value.Sequence))
        {
            var baseMatches = values.Where(value => value.IndexFamilyId == component.IndexFamilyId && value.IndexPeriod == basePeriod).Take(2).ToList();
            var currentMatches = values.Where(value => value.IndexFamilyId == component.IndexFamilyId && value.IndexPeriod == currentPeriod).Take(2).ToList();
            if (baseMatches.Count != 1)
                throw new QuantitySurveyEscalationCalculationValidationException($"Exactly one approved current {component.IndexFamilyCodeSnapshot} index is required for base period {basePeriod:yyyy-MM}.");
            if (currentMatches.Count != 1)
                throw new QuantitySurveyEscalationCalculationValidationException($"Exactly one approved current {component.IndexFamilyCodeSnapshot} index is required for period {currentPeriod:yyyy-MM}.");
            var baseValue = baseMatches[0];
            var currentValue = currentMatches[0];
            if (baseValue.PublicationDate > DateTime.UtcNow.Date || currentValue.PublicationDate > DateTime.UtcNow.Date)
                throw new QuantitySurveyEscalationCalculationValidationException("A selected price index has not yet reached its publication date.");
            result.Add(new ResolvedIndexValues(component, baseValue, currentValue));
        }
        return result;
    }

    private async Task<ResolvedImpactTarget> ResolveTargetAsync(
        QuantitySurveyEscalationFormulaDefinition formula,
        QuantitySurveyEscalationImpactTargetType type,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (type == QuantitySurveyEscalationImpactTargetType.PaymentCertificate)
        {
            var value = await db.ProjectPaymentCertificates.AsNoTracking().FirstOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == id && item.ProjectId == formula.ProjectId && item.ContractId == formula.ContractId && !item.IsDeleted,
                cancellationToken) ?? throw new QuantitySurveyEscalationCalculationValidationException("The selected payment certificate is unavailable for this project and Works contract.");
            if (value.Status is not (ProjectPaymentCertificateStatuses.Draft or ProjectPaymentCertificateStatuses.Issued))
                throw new QuantitySurveyEscalationCalculationValidationException("Select a Draft or Issued payment certificate before its commercial values become final.");
            if (value.GrossCertifiedAmount <= 0m)
                throw new QuantitySurveyEscalationCalculationValidationException("The selected payment certificate must have a positive gross certified amount.");
            var reference = value.CertificateNumber ?? "Unnumbered certificate";
            var snapshot = Hash(JsonSerializer.Serialize(new { value.Id, value.ProjectId, value.ContractId, value.Status, value.GrossCertifiedAmount, value.Currency, value.IssueDate, value.NetCertifiedAmount }, JsonOptions));
            return new ResolvedImpactTarget(reference, value.Status, value.Currency.ToUpperInvariant(), value.GrossCertifiedAmount, snapshot);
        }
        if (type == QuantitySurveyEscalationImpactTargetType.FinalAccount)
        {
            var value = await db.ProjectFinalAccounts.AsNoTracking().FirstOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == id && item.ProjectId == formula.ProjectId && item.ContractId == formula.ContractId && !item.IsDeleted,
                cancellationToken) ?? throw new QuantitySurveyEscalationCalculationValidationException("The selected final account is unavailable for this project and Works contract.");
            if (value.Status is not (ProjectFinalAccountStatuses.Draft or ProjectFinalAccountStatuses.UnderReview))
                throw new QuantitySurveyEscalationCalculationValidationException("Select a Draft or UnderReview final account before its commercial values become final.");
            if (value.FinalAccountValue <= 0m)
                throw new QuantitySurveyEscalationCalculationValidationException("The selected final account must have a positive value.");
            var reference = "Final account " + value.Id.ToString("N")[..10].ToUpperInvariant();
            var snapshot = Hash(JsonSerializer.Serialize(new { value.Id, value.ProjectId, value.ContractId, value.Status, value.FinalAccountValue, value.Currency, value.SettlementDate, value.CertifiedToDate }, JsonOptions));
            return new ResolvedImpactTarget(reference, value.Status, value.Currency.ToUpperInvariant(), value.FinalAccountValue, snapshot);
        }
        throw new QuantitySurveyEscalationCalculationValidationException("Select a valid certificate or final-account impact target.");
    }

    private IQueryable<QuantitySurveyEscalationCalculationRun> RunQuery() => db.QuantitySurveyEscalationCalculationRuns.AsNoTracking()
        .Include(value => value.Formula)
        .Include(value => value.Project)
        .Include(value => value.Contract)
        .Include(value => value.Lines)
        .Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<QuantitySurveyEscalationCalculationRun> RequiredAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.QuantitySurveyEscalationCalculationRuns.AsTracking() : db.QuantitySurveyEscalationCalculationRuns.AsNoTracking();
        return await query.Include(value => value.Formula).Include(value => value.Project).Include(value => value.Contract).Include(value => value.Lines)
                   .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, cancellationToken)
               ?? throw new QuantitySurveyEscalationCalculationNotFoundException("The escalation calculation run was not found.");
    }

    private async Task<List<Guid>> AccessibleProjectIdsAsync()
        => (await projectService.LookupProjectsAsync(take: 5000)).Select(value => value.Id).Distinct().ToList();

    private async Task RequireProjectAccessAsync(Guid projectId)
    {
        if (!await projectService.HasProjectAccessAsync(projectId))
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }

    private void RequireAuthorityRole(QuantitySurveyEscalationCalculationRun entity)
    {
        if (!currentUser.Roles.Any(value => string.Equals(value, entity.AuthorityRoleNameSnapshot, StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Your assigned roles do not include the configured escalation approval authority.");
    }

    private async Task<WorkflowInstanceStatus?> WorkflowStatusAsync(QuantitySurveyEscalationCalculationRun entity, CancellationToken cancellationToken)
        => !entity.WorkflowInstanceId.HasValue
            ? null
            : (await db.WorkflowInstances.AsNoTracking().FirstOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == entity.WorkflowInstanceId && value.EntityId == entity.Id,
                cancellationToken))?.Status;

    private void AddRevision(QuantitySurveyEscalationCalculationRun entity, string action, string reason, object? before, object after, string correlationId)
        => db.QuantitySurveyEscalationCalculationRevisions.Add(new QuantitySurveyEscalationCalculationRevision
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CalculationRunId = entity.Id,
            Action = action,
            ActorUserId = UserId,
            ActorName = UserName,
            ActorRoles = ActorRoles,
            CorrelationId = NormalizeCorrelation(correlationId),
            Reason = reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = UserId
        });

    private void AddAudit(QuantitySurveyEscalationCalculationRun entity, string action, object? before, object after, string correlationId)
        => db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId,
            UserId = UserId,
            Username = UserName,
            Action = action,
            Resource = nameof(QuantitySurveyEscalationCalculationRun),
            ResourceId = entity.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = NormalizeCorrelation(correlationId), value = after }, JsonOptions),
            IpAddress = "api",
            UserAgent = "QS-0303",
            Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = UserId
        });

    private void Touch(QuantitySurveyEscalationCalculationRun entity, string action, string reason, string correlationId)
    {
        entity.AuditAction = action;
        entity.CorrelationId = NormalizeCorrelation(correlationId);
        entity.ChangeReason = reason;
        entity.ActorRoles = ActorRoles;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = UserName;
        entity.LastModifiedById = UserId;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new QuantitySurveyEscalationCalculationConflictException("The calculation changed after it was loaded. Refresh and try again."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql)
        {
            throw new QuantitySurveyEscalationCalculationConflictException(sql.Number switch
            {
                51060 => "The escalation calculation violates its tenant, formula, target, or index lineage controls.",
                51061 => "The escalation calculation lifecycle transition is not permitted.",
                51062 => "The escalation calculation component snapshot is immutable.",
                51063 => "Escalation calculation revision history is append-only.",
                2601 or 2627 => "An equivalent calculation or revision already exists. Refresh and retry.",
                _ => "The escalation calculation could not be saved because a database control rejected it."
            });
        }
    }

    private static QuantitySurveyEscalationCalculationDto Map(QuantitySurveyEscalationCalculationRun value) => new()
    {
        Id = value.Id,
        RunReference = value.RunReference,
        FormulaId = value.FormulaId,
        FormulaCode = value.FormulaCodeSnapshot,
        FormulaVersion = value.FormulaVersionSnapshot,
        ProjectId = value.ProjectId,
        ProjectCode = value.Project.ProjectCode,
        ProjectName = value.Project.Title,
        ContractId = value.ContractId,
        ContractNumber = value.ContractNumberSnapshot,
        BaseIndexPeriod = value.BaseIndexPeriod,
        CurrentIndexPeriod = value.CurrentIndexPeriod,
        CalculationDate = value.CalculationDate,
        ImpactTargetType = value.ImpactTargetType,
        ImpactTargetId = value.ImpactTargetType == QuantitySurveyEscalationImpactTargetType.PaymentCertificate ? value.PaymentCertificateId!.Value : value.FinalAccountId!.Value,
        ImpactTargetReference = value.ImpactTargetReferenceSnapshot,
        ImpactTargetStatus = value.ImpactTargetStatusSnapshot,
        CurrencyCode = value.CurrencyCode,
        BaseRate = value.BaseRate,
        RevisedRate = value.RevisedRate,
        AdjustmentFactor = value.AdjustmentFactor,
        CalculatedFluctuationAmount = value.CalculatedFluctuationAmount,
        ReviewerAdjustmentAmount = value.ReviewerAdjustmentAmount,
        ReviewerAdjustmentReason = value.ReviewerAdjustmentReason,
        ReviewedById = value.ReviewedById,
        ReviewedAt = value.ReviewedAt,
        ApprovedImpactAmount = value.ApprovedImpactAmount,
        ImpactApplicationStatus = value.ImpactApplicationStatus,
        AuthorityRoleId = value.AuthorityRoleId,
        AuthorityRoleName = value.AuthorityRoleNameSnapshot,
        WorkflowInstanceId = value.WorkflowInstanceId,
        Status = value.Status,
        ApprovalStatus = value.ApprovalStatus,
        PreparedById = value.PreparedById,
        PreparedAt = value.PreparedAt,
        ApprovedById = value.ApprovedById,
        ApprovedAt = value.ApprovedAt,
        RejectionReason = value.RejectionReason,
        SnapshotHash = value.SnapshotHash,
        RowVersion = Convert.ToBase64String(value.RowVersion),
        Lines = value.Lines.OrderBy(item => item.Sequence).Select(item => new QuantitySurveyEscalationCalculationLineDto
        {
            Sequence = item.Sequence,
            Component = item.Component,
            Coefficient = item.Coefficient,
            IndexFamilyId = item.IndexFamilyId,
            IndexFamilyCode = item.IndexFamilyCodeSnapshot,
            BaseIndexValueId = item.BaseIndexValueId,
            CurrentIndexValueId = item.CurrentIndexValueId,
            BaseIndexValue = item.BaseIndexValue,
            CurrentIndexValue = item.CurrentIndexValue,
            IndexRatio = item.IndexRatio,
            WeightedContribution = item.WeightedContribution
        }).ToList()
    };

    private static object Snapshot(QuantitySurveyEscalationCalculationRun value) => new
    {
        value.Id,
        value.RunReference,
        value.FormulaId,
        value.FormulaKeySnapshot,
        value.FormulaVersionSnapshot,
        value.ProjectId,
        value.ContractId,
        value.BaseIndexPeriod,
        value.CurrentIndexPeriod,
        value.ImpactTargetType,
        value.PaymentCertificateId,
        value.FinalAccountId,
        value.ImpactTargetSnapshotHash,
        value.CurrencyCode,
        value.BaseRate,
        value.RevisedRate,
        value.AdjustmentFactor,
        value.CalculatedFluctuationAmount,
        value.ReviewerAdjustmentAmount,
        value.ReviewerAdjustmentReason,
        value.ReviewedById,
        value.ApprovedImpactAmount,
        value.ImpactApplicationStatus,
        value.Status,
        value.ApprovalStatus,
        value.PreparedById,
        value.SubmittedById,
        value.ApprovedById,
        value.SnapshotHash,
        Lines = value.Lines.OrderBy(item => item.Sequence).Select(item => new
        {
            item.Sequence,
            item.Component,
            item.Coefficient,
            item.IndexFamilyId,
            item.BaseIndexValueId,
            item.CurrentIndexValueId,
            item.BaseIndexValue,
            item.CurrentIndexValue,
            item.IndexRatio,
            item.WeightedContribution
        })
    };

    private static string ComputeSnapshot(QuantitySurveyEscalationCalculationRun value)
        => Hash(JsonSerializer.Serialize(new
        {
            value.RunReference,
            value.FormulaId,
            value.FormulaKeySnapshot,
            value.FormulaVersionSnapshot,
            value.FormulaCodeSnapshot,
            value.ProjectId,
            value.ContractId,
            value.ContractNumberSnapshot,
            value.BaseIndexPeriod,
            value.CurrentIndexPeriod,
            value.CalculationDate,
            value.ImpactTargetType,
            value.PaymentCertificateId,
            value.FinalAccountId,
            value.ImpactTargetReferenceSnapshot,
            value.ImpactTargetStatusSnapshot,
            value.ImpactTargetSnapshotHash,
            value.CurrencyCode,
            value.BaseRate,
            value.RevisedRate,
            value.AdjustmentFactor,
            value.CalculatedFluctuationAmount,
            value.ReviewerAdjustmentAmount,
            value.ReviewerAdjustmentReason,
            value.ReviewedById,
            value.ReviewedAt,
            value.ApprovedImpactAmount,
            value.ImpactApplicationStatus,
            value.AuthorityRoleId,
            value.AuthorityRoleNameSnapshot,
            value.ConfigurationProfileId,
            value.ConfigurationDecisionId,
            value.ApprovalWorkflowDefinitionId,
            value.WorkflowInstanceId,
            value.Status,
            value.ApprovalStatus,
            value.PreparedById,
            value.SubmittedById,
            value.ApprovedById,
            value.ApprovedAt,
            value.RejectionReason,
            Lines = value.Lines.OrderBy(item => item.Sequence).Select(item => new
            {
                item.Sequence,
                item.Component,
                item.Coefficient,
                item.IndexFamilyId,
                item.IndexFamilyCodeSnapshot,
                item.BaseIndexValueId,
                item.CurrentIndexValueId,
                item.BaseIndexValue,
                item.CurrentIndexValue,
                item.IndexRatio,
                item.WeightedContribution
            })
        }, JsonOptions));

    private static DateTime NormalizeMonth(DateTime value, string label)
    {
        if (value == default) throw new QuantitySurveyEscalationCalculationValidationException(label + " is required.");
        return FirstOfMonth(value);
    }
    private static DateTime FirstOfMonth(DateTime value) => new(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    private static string RequireText(string? value, string label, int maximum)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result)) throw new QuantitySurveyEscalationCalculationValidationException(label + " is required.");
        if (result.Length > maximum) throw new QuantitySurveyEscalationCalculationValidationException(label + $" cannot exceed {maximum} characters.");
        return result;
    }
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(value.Trim().Length, 100)];
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static bool FixedEquals(string left, string right)
        => left.Length == right.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static bool IsUnique(DbUpdateException exception) => exception.InnerException is SqlException { Number: 2601 or 2627 };
    private static void CheckVersion(byte[] current, string? supplied)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied ?? string.Empty); }
        catch { throw new QuantitySurveyEscalationCalculationValidationException("A valid calculation row version is required."); }
        if (!current.SequenceEqual(parsed))
            throw new QuantitySurveyEscalationCalculationConflictException("The calculation changed after it was loaded. Refresh and try again.");
    }
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record ResolvedImpactTarget(string Reference, string Status, string CurrencyCode, decimal BaseAmount, string SnapshotHash);
    private sealed record ResolvedIndexValues(
        QuantitySurveyEscalationFormulaComponent Component,
        QuantitySurveyPriceIndexValue Base,
        QuantitySurveyPriceIndexValue Current);
}
