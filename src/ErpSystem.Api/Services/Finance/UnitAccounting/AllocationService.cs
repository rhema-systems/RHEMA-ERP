using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;

namespace ErpSystem.Api.Services.Finance.UnitAccounting
{
    /// <summary>
    /// Service implementation for Allocation Rule operations.
    /// </summary>
    public class AllocationService : IAllocationService
    {
        private const string AllocationRunBatchWorkflowEntityType = "AllocationRunBatch";
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly IWorkflowService _workflowService;
        private readonly IFinancePostingEngine _financePostingEngine;
        private readonly ILogger<AllocationService> _logger;

        public AllocationService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            IWorkflowService workflowService,
            IFinancePostingEngine financePostingEngine,
            ILogger<AllocationService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _workflowService = workflowService;
            _financePostingEngine = financePostingEngine;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private Guid UserId => Guid.TryParse(_currentUserService.UserId, out var id) ? id : Guid.Empty;
        private string UserName => _currentUserService.UserName ?? "system";

        public async Task<IReadOnlyList<AllocationRuleDto>> GetAllRulesAsync(CancellationToken cancellationToken = default)
        {
            var rules = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.TenantId == TenantId && !r.IsDeleted)
                .Include(r => r.SourceAccount)
                .Include(r => r.DriverUnitAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetAccount)
                .OrderBy(r => r.Code)
                .ToListAsync(cancellationToken);

            return rules.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<AllocationRuleDto>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
        {
            var rules = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.TenantId == TenantId && r.IsActive && !r.IsDeleted)
                .Include(r => r.SourceAccount)
                .Include(r => r.DriverUnitAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetAccount)
                .OrderBy(r => r.Code)
                .ToListAsync(cancellationToken);

            return rules.Select(MapToDto).ToList();
        }

        public async Task<AllocationRuleDto?> GetRuleByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted)
                .Include(r => r.SourceAccount)
                .Include(r => r.DriverUnitAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetDriverUnitAccount)
                .FirstOrDefaultAsync(cancellationToken);

            return rule == null ? null : MapToDto(rule);
        }

        public async Task<AllocationRuleDto?> GetRuleByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.Code == code && r.TenantId == TenantId && !r.IsDeleted)
                .Include(r => r.SourceAccount)
                .Include(r => r.DriverUnitAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetAccount)
                .FirstOrDefaultAsync(cancellationToken);

            return rule == null ? null : MapToDto(rule);
        }

        public async Task<AllocationRuleDto> CreateRuleAsync(CreateAllocationRuleDto dto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            // Check for duplicate code
            var existing = await _unitOfWork.Repository<AllocationRule>()
                .FirstOrDefaultAsync(r => r.Code == dto.Code && r.TenantId == TenantId && !r.IsDeleted);
            if (existing != null)
                throw new InvalidOperationException($"An allocation rule with code '{dto.Code}' already exists.");

            var allocationType = ParseAllocationType(dto.AllocationType);
            ValidateAllocationRuleShape(dto.SourceAccountId, allocationType, dto.DriverUnitAccountId, dto.Targets);
            await ValidateAllocationReferencesAsync(
                dto.SourceAccountId,
                dto.DriverUnitAccountId,
                dto.Targets,
                cancellationToken);

            var rule = new AllocationRule
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                SourceAccountId = dto.SourceAccountId,
                AllocationType = allocationType,
                DriverUnitAccountId = dto.DriverUnitAccountId,
                IsActive = true,
                AutoReverse = dto.AutoReverse,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<AllocationRule>().AddAsync(rule);

            // Add targets
            foreach (var targetDto in dto.Targets)
            {
                var target = new AllocationTarget
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    AllocationRuleId = rule.Id,
                    TargetAccountId = targetDto.TargetAccountId,
                    FixedPercentage = targetDto.FixedPercentage,
                    TargetDriverUnitAccountId = targetDto.TargetDriverUnitAccountId,
                    CostCenterCode = targetDto.CostCenterCode,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                };
                await _unitOfWork.Repository<AllocationTarget>().AddAsync(target);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Allocation rule {Code} created", dto.Code);

            return (await GetRuleByIdAsync(rule.Id, cancellationToken))!;
        }

        public async Task<AllocationRuleDto> UpdateRuleAsync(Guid id, UpdateAllocationRuleDto dto, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted)
                .Include(r => r.Targets)
                .FirstOrDefaultAsync(cancellationToken);

            if (rule == null)
                throw new ArgumentException($"Allocation rule with ID '{id}' not found.");

            var allocationType = ParseAllocationType(dto.AllocationType);
            ValidateAllocationRuleShape(dto.SourceAccountId, allocationType, dto.DriverUnitAccountId, dto.Targets);
            await ValidateAllocationReferencesAsync(
                dto.SourceAccountId,
                dto.DriverUnitAccountId,
                dto.Targets,
                cancellationToken);

            rule.Name = dto.Name;
            rule.Description = dto.Description;
            rule.SourceAccountId = dto.SourceAccountId;
            rule.AllocationType = allocationType;
            rule.DriverUnitAccountId = dto.DriverUnitAccountId;
            rule.IsActive = dto.IsActive;
            rule.AutoReverse = dto.AutoReverse;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = UserName;

            await _unitOfWork.Repository<AllocationRule>().UpdateAsync(rule);

            // Remove existing targets
            foreach (var target in rule.Targets.ToList())
            {
                target.IsDeleted = true;
                target.DeletedAt = DateTime.UtcNow;
                target.DeletedBy = UserName;
                await _unitOfWork.Repository<AllocationTarget>().UpdateAsync(target);
            }

            // Add new targets
            foreach (var targetDto in dto.Targets)
            {
                var target = new AllocationTarget
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    AllocationRuleId = rule.Id,
                    TargetAccountId = targetDto.TargetAccountId,
                    FixedPercentage = targetDto.FixedPercentage,
                    TargetDriverUnitAccountId = targetDto.TargetDriverUnitAccountId,
                    CostCenterCode = targetDto.CostCenterCode,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                };
                await _unitOfWork.Repository<AllocationTarget>().AddAsync(target);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Allocation rule {Code} updated", rule.Code);

            return (await GetRuleByIdAsync(id, cancellationToken))!;
        }

        public async Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted);

            if (rule == null)
                return;

            rule.IsDeleted = true;
            rule.DeletedAt = DateTime.UtcNow;
            rule.DeletedBy = UserName;

            await _unitOfWork.Repository<AllocationRule>().UpdateAsync(rule);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Allocation rule {Code} deleted", rule.Code);
        }

        public async Task<AllocationRuleDto> ActivateRuleAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted);

            if (rule == null)
                throw new ArgumentException($"Allocation rule with ID '{id}' not found.");

            rule.IsActive = true;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = UserName;

            await _unitOfWork.Repository<AllocationRule>().UpdateAsync(rule);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return (await GetRuleByIdAsync(id, cancellationToken))!;
        }

        public async Task<AllocationRuleDto> DeactivateRuleAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted);

            if (rule == null)
                throw new ArgumentException($"Allocation rule with ID '{id}' not found.");

            rule.IsActive = false;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = UserName;

            await _unitOfWork.Repository<AllocationRule>().UpdateAsync(rule);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return (await GetRuleByIdAsync(id, cancellationToken))!;
        }

        public async Task<AllocationResultDto> RunAllocationAsync(RunAllocationDto dto, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var rule = await _unitOfWork.Repository<AllocationRule>()
                        .GetQueryable(r => r.Id == dto.AllocationRuleId && r.TenantId == TenantId && !r.IsDeleted)
                        .Include(r => r.SourceAccount)
                        .Include(r => r.DriverUnitAccount)
                        .Include(r => r.Targets)
                            .ThenInclude(t => t.TargetAccount)
                        .Include(r => r.Targets)
                            .ThenInclude(t => t.TargetDriverUnitAccount)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (rule == null)
                        throw new ArgumentException($"Allocation rule with ID '{dto.AllocationRuleId}' not found.");

                    if (!rule.IsActive)
                        throw new InvalidOperationException("Cannot run an inactive allocation rule.");

                    var fiscalPeriod = await _unitOfWork.Repository<FiscalPeriod>()
                        .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Id == dto.FiscalPeriodId && !p.IsDeleted);
                    if (fiscalPeriod == null)
                        throw new ArgumentException($"Fiscal period with ID '{dto.FiscalPeriodId}' not found.");

                    if (!fiscalPeriod.IsOpen || fiscalPeriod.IsClosed || fiscalPeriod.IsLocked)
                        throw new InvalidOperationException("Allocation fiscal period is not open.");

                    var allocationDate = dto.AllocationDate == default ? fiscalPeriod.EndDate.Date : dto.AllocationDate.Date;
                    if (allocationDate < fiscalPeriod.StartDate.Date || allocationDate > fiscalPeriod.EndDate.Date)
                        throw new InvalidOperationException("Allocation date does not fall inside the fiscal period.");

                    var activeTargets = rule.Targets.Where(t => t.TenantId == TenantId && !t.IsDeleted).ToList();
                    ValidateAllocationRuleShape(rule.SourceAccountId, rule.AllocationType, rule.DriverUnitAccountId, activeTargets.Select(ToCreateTargetDto).ToList());
                    ValidateTargetAccountTypes(rule);

                    var sourceBalance = await GetSourceAccountPeriodBalanceAsync(rule.SourceAccountId, dto.FiscalPeriodId, cancellationToken);
                    var sourceAmount = Math.Abs(sourceBalance);
                    if (sourceAmount == 0m)
                        throw new InvalidOperationException("Allocation source account has no period balance to allocate.");

                    var lines = await CalculateAllocationLinesAsync(rule, activeTargets, sourceAmount, dto.FiscalPeriodId, cancellationToken);
                    var totalAllocated = lines.Sum(line => line.AllocatedAmount);
                    var postingLines = BuildPostingLines(rule, lines, totalAllocated);

                    var postingResult = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                    {
                        SourceModule = "GL",
                        OriginModuleCode = "FIN",
                        SourceDocumentType = "AllocationRule",
                        SourceDocumentId = rule.Id,
                        SourceDocumentReference = rule.Code,
                        PostingAction = $"Allocate:{dto.FiscalPeriodId:N}",
                        Description = string.IsNullOrWhiteSpace(dto.Description)
                            ? $"Allocation {rule.Code} for {fiscalPeriod.PeriodName}"
                            : dto.Description.Trim(),
                        PostingDate = allocationDate,
                        FiscalPeriodId = dto.FiscalPeriodId,
                        JournalType = "Allocation",
                        BookClassification = "IFRS",
                        FunctionalCurrencyCode = "GHS",
                        IdempotencyKey = $"allocation:{TenantId:N}:{rule.Id:N}:{dto.FiscalPeriodId:N}",
                        ReturnExistingOnDuplicate = true,
                        Lines = postingLines
                    }, cancellationToken);

                    var now = DateTime.UtcNow;
                    rule.LastRunDate = now;
                    rule.UpdatedAt = now;
                    rule.UpdatedBy = UserName;
                    await _unitOfWork.Repository<AllocationRule>().UpdateAsync(rule);
                    await _unitOfWork.CommitAsync(cancellationToken);

                    _logger.LogInformation(
                        "Allocation rule {Code} posted journal {JournalEntryNumber} with total {Amount}",
                        rule.Code,
                        postingResult.JournalEntryNumber,
                        totalAllocated);

                    return new AllocationResultDto(
                        rule.Id,
                        rule.Code,
                        rule.Name,
                        now,
                        postingResult.JournalEntryId,
                        postingResult.JournalEntryNumber,
                        totalAllocated,
                        lines);
                }
                catch
                {
                    await TryRollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<AllocationRunBatchDto>> GetRunBatchesAsync(
            string? status = null,
            CancellationToken cancellationToken = default)
        {
            var query = _unitOfWork.Repository<AllocationRunBatch>()
                .GetQueryable(b => b.TenantId == TenantId && !b.IsDeleted)
                .Include(b => b.AllocationRule)
                .Include(b => b.FiscalPeriod)
                .Include(b => b.SourceAccount)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.TargetAccount)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.TargetDriverUnitAccount)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(b => b.Status == ParseRunBatchStatus(status));
            }

            var batches = await query
                .OrderByDescending(b => b.CreatedAt)
                .ThenByDescending(b => b.BatchNumber)
                .ToListAsync(cancellationToken);

            return batches.Select(MapToRunBatchDto).ToList();
        }

        public async Task<AllocationRunBatchDto?> GetRunBatchByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var batch = await LoadRunBatchAsync(id, cancellationToken);
            return batch == null ? null : MapToRunBatchDto(batch);
        }

        public async Task<AllocationRunBatchDto> CreateRunBatchAsync(
            CreateAllocationRunBatchDto dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var existingBatch = await _unitOfWork.Repository<AllocationRunBatch>()
                        .GetQueryable(b => b.TenantId == TenantId
                            && b.AllocationRuleId == dto.AllocationRuleId
                            && b.FiscalPeriodId == dto.FiscalPeriodId
                            && !b.IsDeleted
                            && b.Status != AllocationRunBatchStatus.Rejected
                            && b.Status != AllocationRunBatchStatus.Cancelled)
                        .OrderByDescending(b => b.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (existingBatch != null)
                    {
                        throw new InvalidOperationException(
                            $"Allocation run batch '{existingBatch.BatchNumber}' already exists for this rule and period.");
                    }

                    var rule = await LoadRunnableRuleAsync(dto.AllocationRuleId, cancellationToken);
                    var fiscalPeriod = await LoadOpenFiscalPeriodAsync(dto.FiscalPeriodId, cancellationToken);
                    var allocationDate = ResolveAllocationDate(dto.AllocationDate, fiscalPeriod);

                    var activeTargets = rule.Targets.Where(t => t.TenantId == TenantId && !t.IsDeleted).ToList();
                    ValidateAllocationRuleShape(
                        rule.SourceAccountId,
                        rule.AllocationType,
                        rule.DriverUnitAccountId,
                        activeTargets.Select(ToCreateTargetDto).ToList());
                    ValidateTargetAccountTypes(rule);

                    var sourceBalance = await GetSourceAccountPeriodBalanceAsync(
                        rule.SourceAccountId,
                        fiscalPeriod.Id,
                        cancellationToken);
                    var sourceAmount = Math.Abs(sourceBalance);
                    if (sourceAmount == 0m)
                        throw new InvalidOperationException("Allocation source account has no period balance to allocate.");

                    var lines = await CalculateAllocationLinesAsync(
                        rule,
                        activeTargets,
                        sourceAmount,
                        fiscalPeriod.Id,
                        cancellationToken);
                    var totalAllocated = lines.Sum(line => line.AllocatedAmount);
                    var now = DateTime.UtcNow;
                    var batchId = Guid.NewGuid();
                    var idempotencyKey = $"allocation-run-batch:{TenantId:N}:{batchId:N}";

                    var batch = new AllocationRunBatch
                    {
                        Id = batchId,
                        TenantId = TenantId,
                        BatchNumber = GenerateRunBatchNumber(now),
                        AllocationRuleId = rule.Id,
                        FiscalPeriodId = fiscalPeriod.Id,
                        AllocationDate = allocationDate,
                        Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                        Status = AllocationRunBatchStatus.Draft,
                        SourceAccountId = rule.SourceAccountId,
                        SourcePeriodBalance = sourceBalance,
                        TotalAllocated = totalAllocated,
                        AllocationType = rule.AllocationType.ToString(),
                        BookClassification = "IFRS",
                        FunctionalCurrencyCode = "GHS",
                        IdempotencyKey = idempotencyKey,
                        CreatedAt = now,
                        CreatedBy = UserName
                    };

                    await _unitOfWork.Repository<AllocationRunBatch>().AddAsync(batch);

                    for (var index = 0; index < lines.Count; index++)
                    {
                        var calculatedLine = lines[index];
                        var target = activeTargets.First(t => t.TargetAccountId == calculatedLine.TargetAccountId);
                        await _unitOfWork.Repository<AllocationRunBatchLine>().AddAsync(new AllocationRunBatchLine
                        {
                            Id = Guid.NewGuid(),
                            TenantId = TenantId,
                            AllocationRunBatchId = batch.Id,
                            LineNumber = index + 1,
                            TargetAccountId = calculatedLine.TargetAccountId,
                            TargetDriverUnitAccountId = target.TargetDriverUnitAccountId,
                            AllocationBasis = calculatedLine.AllocationBasis,
                            AllocationPercent = calculatedLine.AllocationPercent,
                            AllocatedAmount = calculatedLine.AllocatedAmount,
                            CostCenterCode = target.CostCenterCode,
                            CreatedAt = now,
                            CreatedBy = UserName
                        });
                    }

                    await _unitOfWork.CommitAsync(cancellationToken);

                    _logger.LogInformation(
                        "Allocation run batch {BatchNumber} created for rule {Code} and period {PeriodCode}",
                        batch.BatchNumber,
                        rule.Code,
                        fiscalPeriod.PeriodCode);

                    return (await GetRunBatchByIdAsync(batch.Id, cancellationToken))!;
                }
                catch
                {
                    await TryRollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<AllocationRunBatchDto> SubmitRunBatchAsync(
            Guid id,
            string? comment = null,
            CancellationToken cancellationToken = default)
        {
            var batch = await LoadRunBatchAsync(id, cancellationToken)
                ?? throw new ArgumentException($"Allocation run batch with ID '{id}' not found.");

            if (batch.Status != AllocationRunBatchStatus.Draft)
                throw new InvalidOperationException("Only draft allocation run batches can be submitted.");

            EnsureRunBatchPeriodIsOpen(batch);

            var now = DateTime.UtcNow;
            batch.Status = AllocationRunBatchStatus.PendingApproval;
            batch.SubmittedAt = now;
            batch.SubmittedBy = UserId == Guid.Empty ? null : UserId;
            batch.SubmittedByName = UserName;
            batch.UpdatedAt = now;
            batch.UpdatedBy = UserName;

            await _unitOfWork.Repository<AllocationRunBatch>().UpdateAsync(batch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                var workflowResult = await _workflowService.StartApprovalWorkflowAsync(AllocationRunBatchWorkflowEntityType, id);
                if (!workflowResult.Success)
                    throw new InvalidOperationException(workflowResult.Message ?? "Failed to start allocation run approval workflow.");

                batch.WorkflowInstanceId = workflowResult.WorkflowInstanceId ?? batch.WorkflowInstanceId;
                batch.UpdatedAt = DateTime.UtcNow;
                batch.UpdatedBy = UserName;
                await _unitOfWork.Repository<AllocationRunBatch>().UpdateAsync(batch);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                batch.Status = AllocationRunBatchStatus.Draft;
                batch.SubmittedAt = null;
                batch.SubmittedBy = null;
                batch.SubmittedByName = null;
                batch.UpdatedAt = DateTime.UtcNow;
                batch.UpdatedBy = UserName;
                await _unitOfWork.Repository<AllocationRunBatch>().UpdateAsync(batch);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw;
            }

            return (await GetRunBatchByIdAsync(id, cancellationToken))!;
        }

        public async Task<AllocationRunBatchDto> ApproveRunBatchAsync(
            Guid id,
            string? comment = null,
            CancellationToken cancellationToken = default)
        {
            var batch = await LoadRunBatchAsync(id, cancellationToken)
                ?? throw new ArgumentException($"Allocation run batch with ID '{id}' not found.");

            if (batch.Status != AllocationRunBatchStatus.PendingApproval)
                throw new InvalidOperationException("Only allocation run batches pending approval can be approved.");

            if (UserId == Guid.Empty)
                throw new UnauthorizedAccessException("User not authenticated.");

            if (!await _workflowService.CanUserApproveAsync(AllocationRunBatchWorkflowEntityType, id, UserId))
                throw new UnauthorizedAccessException("Current user cannot approve this allocation run batch.");

            var workflowResult = await _workflowService.ProcessApprovalStepAsync(
                AllocationRunBatchWorkflowEntityType,
                id,
                UserId,
                "Approve",
                comment);
            if (!workflowResult.Success)
                throw new InvalidOperationException(workflowResult.Message ?? "Workflow approval failed.");

            // A sequential workflow may still be waiting for later approval stages; only the final
            // completed workflow can release the run batch for GL posting.
            if (workflowResult.Status == WorkflowInstanceStatus.Completed)
            {
                var now = DateTime.UtcNow;
                batch.Status = AllocationRunBatchStatus.Approved;
                batch.ApprovedAt = now;
                batch.ApprovedBy = UserId;
                batch.ApprovedByName = UserName;
                batch.UpdatedAt = now;
                batch.UpdatedBy = UserName;
                await _unitOfWork.Repository<AllocationRunBatch>().UpdateAsync(batch);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return (await GetRunBatchByIdAsync(id, cancellationToken))!;
        }

        public async Task<AllocationRunBatchDto> RejectRunBatchAsync(
            Guid id,
            string reason,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A rejection reason is required.");

            var batch = await LoadRunBatchAsync(id, cancellationToken)
                ?? throw new ArgumentException($"Allocation run batch with ID '{id}' not found.");

            if (batch.Status != AllocationRunBatchStatus.PendingApproval)
                throw new InvalidOperationException("Only allocation run batches pending approval can be rejected.");

            if (UserId == Guid.Empty)
                throw new UnauthorizedAccessException("User not authenticated.");

            if (!await _workflowService.CanUserApproveAsync(AllocationRunBatchWorkflowEntityType, id, UserId))
                throw new UnauthorizedAccessException("Current user cannot reject this allocation run batch.");

            var workflowResult = await _workflowService.ProcessApprovalStepAsync(
                AllocationRunBatchWorkflowEntityType,
                id,
                UserId,
                "Reject",
                reason);
            if (!workflowResult.Success)
                throw new InvalidOperationException(workflowResult.Message ?? "Workflow rejection failed.");

            var now = DateTime.UtcNow;
            batch.Status = AllocationRunBatchStatus.Rejected;
            batch.RejectionReason = reason.Trim();
            batch.UpdatedAt = now;
            batch.UpdatedBy = UserName;
            await _unitOfWork.Repository<AllocationRunBatch>().UpdateAsync(batch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return (await GetRunBatchByIdAsync(id, cancellationToken))!;
        }

        public async Task<AllocationRunBatchDto> PostRunBatchAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var batch = await LoadRunBatchAsync(id, cancellationToken)
                        ?? throw new ArgumentException($"Allocation run batch with ID '{id}' not found.");

                    if (batch.Status == AllocationRunBatchStatus.Posted)
                    {
                        await _unitOfWork.CommitAsync(cancellationToken);
                        return MapToRunBatchDto(batch);
                    }

                    if (batch.Status != AllocationRunBatchStatus.Approved)
                        throw new InvalidOperationException("Only approved allocation run batches can be posted.");

                    EnsureRunBatchPeriodIsOpen(batch);

                    var snapshotLines = batch.Lines
                        .Where(line => !line.IsDeleted)
                        .OrderBy(line => line.LineNumber)
                        .Select(line => new AllocationLineResultDto(
                            line.TargetAccountId,
                            line.TargetAccount?.AccountNumber ?? string.Empty,
                            line.TargetAccount?.AccountName ?? string.Empty,
                            line.AllocationBasis,
                            line.AllocationPercent,
                            line.AllocatedAmount))
                        .ToList();
                    if (snapshotLines.Count == 0)
                        throw new InvalidOperationException("Allocation run batch has no lines to post.");

                    var sourceAccount = batch.SourceAccount
                        ?? throw new InvalidOperationException("Allocation run batch source account could not be loaded.");
                    var ruleReference = batch.AllocationRule?.Code ?? batch.BatchNumber;
                    var postingLines = BuildPostingLines(
                        sourceAccount,
                        batch.SourceAccountId,
                        ruleReference,
                        snapshotLines,
                        batch.TotalAllocated);

                    var postingResult = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                    {
                        SourceModule = "GL",
                        OriginModuleCode = "FIN",
                        SourceDocumentType = "AllocationRunBatch",
                        SourceDocumentId = batch.Id,
                        SourceDocumentReference = batch.BatchNumber,
                        PostingAction = "Post",
                        Description = string.IsNullOrWhiteSpace(batch.Description)
                            ? $"Allocation {ruleReference} for {batch.FiscalPeriod?.PeriodName ?? batch.FiscalPeriodId.ToString()}"
                            : batch.Description.Trim(),
                        PostingDate = batch.AllocationDate,
                        FiscalPeriodId = batch.FiscalPeriodId,
                        JournalType = "Allocation",
                        BookClassification = batch.BookClassification,
                        FunctionalCurrencyCode = batch.FunctionalCurrencyCode,
                        IdempotencyKey = batch.IdempotencyKey ?? $"allocation-run-batch:{TenantId:N}:{batch.Id:N}",
                        ReturnExistingOnDuplicate = true,
                        Lines = postingLines
                    }, cancellationToken);

                    var now = DateTime.UtcNow;
                    batch.Status = AllocationRunBatchStatus.Posted;
                    batch.JournalEntryId = postingResult.JournalEntryId;
                    batch.JournalEntryNumber = postingResult.JournalEntryNumber;
                    batch.PostedAt = now;
                    batch.PostedBy = UserId == Guid.Empty ? null : UserId;
                    batch.PostedByName = UserName;
                    batch.UpdatedAt = now;
                    batch.UpdatedBy = UserName;
                    await _unitOfWork.Repository<AllocationRunBatch>().UpdateAsync(batch);

                    if (batch.AllocationRule != null)
                    {
                        batch.AllocationRule.LastRunDate = now;
                        batch.AllocationRule.UpdatedAt = now;
                        batch.AllocationRule.UpdatedBy = UserName;
                        await _unitOfWork.Repository<AllocationRule>().UpdateAsync(batch.AllocationRule);
                    }

                    await _unitOfWork.CommitAsync(cancellationToken);

                    _logger.LogInformation(
                        "Allocation run batch {BatchNumber} posted journal {JournalEntryNumber} with total {Amount}",
                        batch.BatchNumber,
                        postingResult.JournalEntryNumber,
                        batch.TotalAllocated);

                    return MapToRunBatchDto(batch);
                }
                catch
                {
                    await TryRollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);
        }

        private static AllocationRuleDto MapToDto(AllocationRule rule)
        {
            return new AllocationRuleDto(
                rule.Id,
                rule.Code,
                rule.Name,
                rule.Description,
                rule.SourceAccountId,
                rule.SourceAccount?.AccountNumber ?? "",
                rule.SourceAccount?.AccountName ?? "",
                rule.AllocationType.ToString(),
                rule.DriverUnitAccountId,
                rule.DriverUnitAccount?.AccountNumber,
                rule.DriverUnitAccount?.Name,
                rule.IsActive,
                rule.AutoReverse,
                rule.LastRunDate,
                rule.CreatedAt,
                rule.Targets.Where(t => !t.IsDeleted).Select(t => new AllocationTargetDto(
                    t.Id,
                    t.TargetAccountId,
                    t.TargetAccount?.AccountNumber ?? "",
                    t.TargetAccount?.AccountName ?? "",
                    t.FixedPercentage,
                    t.TargetDriverUnitAccountId,
                    t.TargetDriverUnitAccount?.AccountNumber,
                    t.CostCenterCode
                )).ToList()
            );
        }

        private static AllocationRunBatchDto MapToRunBatchDto(AllocationRunBatch batch)
        {
            return new AllocationRunBatchDto(
                batch.Id,
                batch.BatchNumber,
                batch.AllocationRuleId,
                batch.AllocationRule?.Code ?? string.Empty,
                batch.AllocationRule?.Name ?? string.Empty,
                batch.FiscalPeriodId,
                batch.FiscalPeriod?.PeriodCode ?? string.Empty,
                batch.FiscalPeriod?.PeriodName ?? string.Empty,
                batch.AllocationDate,
                batch.Description,
                batch.Status.ToString(),
                batch.SourceAccountId,
                batch.SourceAccount?.AccountNumber ?? string.Empty,
                batch.SourceAccount?.AccountName ?? string.Empty,
                batch.SourcePeriodBalance,
                batch.TotalAllocated,
                batch.AllocationType,
                batch.BookClassification,
                batch.FunctionalCurrencyCode,
                batch.WorkflowInstanceId,
                batch.JournalEntryId,
                batch.JournalEntryNumber,
                batch.SubmittedAt,
                batch.SubmittedByName,
                batch.ApprovedAt,
                batch.ApprovedByName,
                batch.PostedAt,
                batch.PostedByName,
                batch.RejectionReason,
                batch.CreatedAt,
                batch.Lines
                    .Where(line => !line.IsDeleted)
                    .OrderBy(line => line.LineNumber)
                    .Select(line => new AllocationRunBatchLineDto(
                        line.Id,
                        line.LineNumber,
                        line.TargetAccountId,
                        line.TargetAccount?.AccountNumber ?? string.Empty,
                        line.TargetAccount?.AccountName ?? string.Empty,
                        line.TargetDriverUnitAccountId,
                        line.TargetDriverUnitAccount?.AccountNumber,
                        line.TargetDriverUnitAccount?.Name,
                        line.AllocationBasis,
                        line.AllocationPercent,
                        line.AllocatedAmount,
                        line.CostCenterCode))
                    .ToList());
        }

        private async Task<AllocationRunBatch?> LoadRunBatchAsync(Guid id, CancellationToken cancellationToken)
        {
            return await _unitOfWork.Repository<AllocationRunBatch>()
                .GetQueryable(b => b.Id == id && b.TenantId == TenantId && !b.IsDeleted)
                .Include(b => b.AllocationRule)
                .Include(b => b.FiscalPeriod)
                .Include(b => b.SourceAccount)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.TargetAccount)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.TargetDriverUnitAccount)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<AllocationRule> LoadRunnableRuleAsync(Guid ruleId, CancellationToken cancellationToken)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.Id == ruleId && r.TenantId == TenantId && !r.IsDeleted)
                .Include(r => r.SourceAccount)
                .Include(r => r.DriverUnitAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetDriverUnitAccount)
                .FirstOrDefaultAsync(cancellationToken);

            if (rule == null)
                throw new ArgumentException($"Allocation rule with ID '{ruleId}' not found.");

            if (!rule.IsActive)
                throw new InvalidOperationException("Cannot run an inactive allocation rule.");

            return rule;
        }

        private async Task<FiscalPeriod> LoadOpenFiscalPeriodAsync(Guid fiscalPeriodId, CancellationToken cancellationToken)
        {
            var fiscalPeriod = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Id == fiscalPeriodId && !p.IsDeleted);
            if (fiscalPeriod == null)
                throw new ArgumentException($"Fiscal period with ID '{fiscalPeriodId}' not found.");

            if (!fiscalPeriod.IsOpen || fiscalPeriod.IsClosed || fiscalPeriod.IsLocked)
                throw new InvalidOperationException("Allocation fiscal period is not open.");

            return fiscalPeriod;
        }

        private static void EnsureRunBatchPeriodIsOpen(AllocationRunBatch batch)
        {
            var fiscalPeriod = batch.FiscalPeriod
                ?? throw new InvalidOperationException("Allocation run batch fiscal period could not be loaded.");

            if (!fiscalPeriod.IsOpen || fiscalPeriod.IsClosed || fiscalPeriod.IsLocked)
                throw new InvalidOperationException("Allocation fiscal period is not open.");

            if (batch.AllocationDate.Date < fiscalPeriod.StartDate.Date || batch.AllocationDate.Date > fiscalPeriod.EndDate.Date)
                throw new InvalidOperationException("Allocation date does not fall inside the fiscal period.");
        }

        private static DateTime ResolveAllocationDate(DateTime requestedDate, FiscalPeriod fiscalPeriod)
        {
            var allocationDate = requestedDate == default
                ? fiscalPeriod.EndDate.Date
                : requestedDate.Date;

            if (allocationDate < fiscalPeriod.StartDate.Date || allocationDate > fiscalPeriod.EndDate.Date)
                throw new InvalidOperationException("Allocation date does not fall inside the fiscal period.");

            return allocationDate;
        }

        private static AllocationRunBatchStatus ParseRunBatchStatus(string status)
        {
            if (!Enum.TryParse<AllocationRunBatchStatus>(status, ignoreCase: true, out var parsed))
                throw new ArgumentException($"Unsupported allocation run batch status '{status}'.");

            return parsed;
        }

        private static string GenerateRunBatchNumber(DateTime now)
            => $"ALR-{now:yyyyMMddHHmmssfff}";

        private static AllocationType ParseAllocationType(string allocationType)
        {
            if (!Enum.TryParse<AllocationType>(allocationType, ignoreCase: true, out var parsed))
                throw new ArgumentException($"Unsupported allocation type '{allocationType}'.");

            return parsed;
        }

        private static void ValidateAllocationRuleShape(
            Guid sourceAccountId,
            AllocationType allocationType,
            Guid? driverUnitAccountId,
            IReadOnlyCollection<CreateAllocationTargetDto> targets)
        {
            if (sourceAccountId == Guid.Empty)
                throw new InvalidOperationException("Allocation source account is required.");

            if (targets == null || targets.Count == 0)
                throw new InvalidOperationException("At least one allocation target is required.");

            if (targets.Any(t => t.TargetAccountId == Guid.Empty))
                throw new InvalidOperationException("Each allocation target must have a target GL account.");

            if (targets.Any(t => t.TargetAccountId == sourceAccountId))
                throw new InvalidOperationException("Allocation target accounts cannot include the source account.");

            if (targets.GroupBy(t => t.TargetAccountId).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Allocation target accounts must be unique within a rule.");

            switch (allocationType)
            {
                case AllocationType.FixedPercentage:
                    var totalPercent = targets.Sum(t => t.FixedPercentage ?? 0m);
                    if (targets.Any(t => !t.FixedPercentage.HasValue || t.FixedPercentage <= 0m))
                        throw new InvalidOperationException("Fixed-percentage allocation targets must have positive percentages.");
                    if (Math.Abs(totalPercent - 100m) > 0.01m)
                        throw new InvalidOperationException("Fixed-percentage allocation targets must total 100%.");
                    break;

                case AllocationType.UnitAccountBased:
                    if (!driverUnitAccountId.HasValue || driverUnitAccountId.Value == Guid.Empty)
                        throw new InvalidOperationException("A driver unit account is required for unit-account-based allocations.");
                    if (targets.Any(t => !t.TargetDriverUnitAccountId.HasValue || t.TargetDriverUnitAccountId.Value == Guid.Empty))
                        throw new InvalidOperationException("Each unit-account-based allocation target must have a target driver unit account.");
                    break;
            }
        }

        private static CreateAllocationTargetDto ToCreateTargetDto(AllocationTarget target)
            => new(
                target.TargetAccountId,
                target.FixedPercentage,
                target.TargetDriverUnitAccountId,
                target.CostCenterCode);

        private static void ValidateTargetAccountTypes(AllocationRule rule)
        {
            if (rule.SourceAccount == null)
                throw new InvalidOperationException("Allocation source account could not be loaded.");

            var invalidTarget = rule.Targets
                .Where(t => !t.IsDeleted)
                .FirstOrDefault(t => t.TargetAccount == null || t.TargetAccount.AccountType != rule.SourceAccount.AccountType);

            if (invalidTarget != null)
            {
                throw new InvalidOperationException(
                    "Allocation source and target accounts must have the same account type so the generated journal moves balances consistently.");
            }
        }

        private async Task<decimal> GetSourceAccountPeriodBalanceAsync(
            Guid sourceAccountId,
            Guid fiscalPeriodId,
            CancellationToken cancellationToken)
        {
            // The current run contract has no book/currency fields yet; use the default IFRS/GHS balance for now.
            return await _unitOfWork.Repository<AccountBalance>()
                .GetQueryable(b => b.TenantId == TenantId
                    && b.AccountId == sourceAccountId
                    && b.FiscalPeriodId == fiscalPeriodId
                    && b.BookClassification == "IFRS"
                    && !b.IsDeleted)
                .Select(b => (decimal?)b.ClosingBalance)
                .FirstOrDefaultAsync(cancellationToken) ?? 0m;
        }

        private async Task<List<AllocationLineResultDto>> CalculateAllocationLinesAsync(
            AllocationRule rule,
            IReadOnlyList<AllocationTarget> activeTargets,
            decimal sourceAmount,
            Guid fiscalPeriodId,
            CancellationToken cancellationToken)
        {
            var rawLines = new List<(AllocationTarget Target, decimal Basis, decimal Percent)>();

            switch (rule.AllocationType)
            {
                case AllocationType.FixedPercentage:
                    rawLines.AddRange(activeTargets.Select(target => (
                        target,
                        Basis: target.FixedPercentage ?? 0m,
                        Percent: target.FixedPercentage ?? 0m)));
                    break;

                case AllocationType.EqualDistribution:
                    var equalPercent = activeTargets.Count > 0 ? 100m / activeTargets.Count : 0m;
                    rawLines.AddRange(activeTargets.Select(target => (
                        target,
                        Basis: 1m,
                        Percent: equalPercent)));
                    break;

                case AllocationType.UnitAccountBased:
                    var targetDriverValues = new Dictionary<Guid, decimal>();
                    foreach (var target in activeTargets)
                    {
                        var driverValue = await GetUnitAccountPeriodBalanceAsync(
                            target.TargetDriverUnitAccountId!.Value,
                            fiscalPeriodId,
                            cancellationToken);
                        targetDriverValues[target.Id] = driverValue;
                    }

                    var totalDriverValue = targetDriverValues.Values.Sum();
                    if (totalDriverValue <= 0m)
                        throw new InvalidOperationException("Unit-account allocation driver values must total more than zero.");

                    rawLines.AddRange(activeTargets.Select(target =>
                    {
                        var basis = targetDriverValues[target.Id];
                        return (
                            target,
                            Basis: basis,
                            Percent: (basis / totalDriverValue) * 100m);
                    }));
                    break;
            }

            return BuildRoundedAllocationResults(rawLines, sourceAmount);
        }

        private async Task<decimal> GetUnitAccountPeriodBalanceAsync(
            Guid unitAccountId,
            Guid fiscalPeriodId,
            CancellationToken cancellationToken)
        {
            return await _unitOfWork.Repository<UnitAccountBalance>()
                .GetQueryable(b => b.TenantId == TenantId
                    && b.UnitAccountId == unitAccountId
                    && b.FiscalPeriodId == fiscalPeriodId
                    && !b.IsDeleted)
                .Select(b => (decimal?)b.ClosingBalance)
                .FirstOrDefaultAsync(cancellationToken) ?? 0m;
        }

        private static List<AllocationLineResultDto> BuildRoundedAllocationResults(
            IReadOnlyList<(AllocationTarget Target, decimal Basis, decimal Percent)> rawLines,
            decimal sourceAmount)
        {
            var results = new List<AllocationLineResultDto>();
            decimal allocatedSoFar = 0m;

            for (var index = 0; index < rawLines.Count; index++)
            {
                var raw = rawLines[index];
                var amount = index == rawLines.Count - 1
                    ? sourceAmount - allocatedSoFar
                    : Math.Round(sourceAmount * (raw.Percent / 100m), 2, MidpointRounding.AwayFromZero);

                allocatedSoFar += amount;
                results.Add(new AllocationLineResultDto(
                    raw.Target.TargetAccountId,
                    raw.Target.TargetAccount?.AccountNumber ?? string.Empty,
                    raw.Target.TargetAccount?.AccountName ?? string.Empty,
                    raw.Basis,
                    Math.Round(raw.Percent, 2, MidpointRounding.AwayFromZero),
                    amount));
            }

            return results;
        }

        private static List<FinancePostingLineDto> BuildPostingLines(
            AllocationRule rule,
            IReadOnlyCollection<AllocationLineResultDto> allocationLines,
            decimal totalAllocated)
        {
            if (rule.SourceAccount == null)
                throw new InvalidOperationException("Allocation source account could not be loaded.");

            return BuildPostingLines(
                rule.SourceAccount,
                rule.SourceAccountId,
                rule.Code,
                allocationLines,
                totalAllocated);
        }

        private static List<FinancePostingLineDto> BuildPostingLines(
            Account sourceAccount,
            Guid sourceAccountId,
            string allocationReference,
            IReadOnlyCollection<AllocationLineResultDto> allocationLines,
            decimal totalAllocated)
        {
            var increaseTargetsWithDebit = sourceAccount.AccountType is AccountType.Asset or AccountType.Expense;
            var postingLines = new List<FinancePostingLineDto>();

            foreach (var line in allocationLines.Where(l => l.AllocatedAmount != 0m))
            {
                postingLines.Add(new FinancePostingLineDto
                {
                    AccountId = line.TargetAccountId,
                    Description = $"Allocation {allocationReference}: {line.AllocationPercent:N2}%",
                    DebitAmount = increaseTargetsWithDebit ? line.AllocatedAmount : 0m,
                    CreditAmount = increaseTargetsWithDebit ? 0m : line.AllocatedAmount
                });
            }

            postingLines.Add(new FinancePostingLineDto
            {
                AccountId = sourceAccountId,
                Description = $"Allocation source {allocationReference}",
                DebitAmount = increaseTargetsWithDebit ? 0m : totalAllocated,
                CreditAmount = increaseTargetsWithDebit ? totalAllocated : 0m
            });

            return postingLines;
        }

        private async Task TryRollbackAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // CommitAsync already rolls back and clears the active transaction when SaveChanges fails.
            }
        }

        private async Task ValidateAllocationReferencesAsync(
            Guid sourceAccountId,
            Guid? driverUnitAccountId,
            IReadOnlyCollection<CreateAllocationTargetDto> targets,
            CancellationToken cancellationToken)
        {
            var sourceAccountExists = await _unitOfWork.Repository<Account>()
                .GetQueryable(a => a.TenantId == TenantId
                    && a.Id == sourceAccountId
                    && a.Status == AccountStatus.Active
                    && a.AllowDirectPosting
                    && !a.IsControlAccount
                    && !a.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!sourceAccountExists)
                throw new ArgumentException($"Source account with ID '{sourceAccountId}' was not found or is not eligible for allocation posting.");

            if (driverUnitAccountId.HasValue)
            {
                var driverExists = await _unitOfWork.Repository<UnitAccount>()
                    .GetQueryable(a => a.TenantId == TenantId
                        && a.Id == driverUnitAccountId.Value
                        && a.IsActive
                        && !a.IsDeleted)
                    .AnyAsync(cancellationToken);
                if (!driverExists)
                    throw new ArgumentException($"Driver unit account with ID '{driverUnitAccountId.Value}' not found.");
            }

            var targetAccountIds = targets.Select(t => t.TargetAccountId).Distinct().ToList();
            var validTargetAccountCount = await _unitOfWork.Repository<Account>()
                .GetQueryable(a => a.TenantId == TenantId
                    && targetAccountIds.Contains(a.Id)
                    && a.Status == AccountStatus.Active
                    && a.AllowDirectPosting
                    && !a.IsControlAccount
                    && !a.IsDeleted)
                .CountAsync(cancellationToken);
            if (validTargetAccountCount != targetAccountIds.Count)
                throw new ArgumentException("One or more target accounts were not found or are not eligible for allocation posting.");

            var targetDriverIds = targets
                .Where(t => t.TargetDriverUnitAccountId.HasValue)
                .Select(t => t.TargetDriverUnitAccountId!.Value)
                .Distinct()
                .ToList();
            if (targetDriverIds.Count == 0)
                return;

            var validTargetDriverCount = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(a => a.TenantId == TenantId
                    && targetDriverIds.Contains(a.Id)
                    && a.IsActive
                    && !a.IsDeleted)
                .CountAsync(cancellationToken);
            if (validTargetDriverCount != targetDriverIds.Count)
                throw new ArgumentException("One or more target driver unit accounts were not found for the current tenant.");
        }
    }
}
