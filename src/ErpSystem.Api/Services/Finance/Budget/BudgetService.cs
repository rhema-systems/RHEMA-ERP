using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Api.Services.Finance.Budget;

public partial class BudgetService : IBudgetService
{
    private const string DraftStatus = "Draft";
    private const string SubmittedStatus = "Submitted";
    private const string ApprovedStatus = "Approved";
    private const string RejectedStatus = "Rejected";
    private const string CollectingStatus = "Collecting";
    private const string InReviewStatus = "InReview";
    private const string SupersededStatus = "Superseded";
    private const string ArchivedStatus = "Archived";

    private static readonly string[] OversightPermissions =
    {
        FinancePermissions.MaintainBudgets,
        FinancePermissions.AssignBudgetReturns,
        FinancePermissions.ApproveBudgetReturns,
        FinancePermissions.LockBudgets
    };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkflowService _workflowService;
    private readonly IDocumentNumberingService? _documentNumberingService;
    private readonly IFinanceAuditService? _financeAuditService;

    public BudgetService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IWorkflowService workflowService,
        IDocumentNumberingService? documentNumberingService = null,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _workflowService = workflowService;
        _documentNumberingService = documentNumberingService;
        _financeAuditService = financeAuditService;
    }

    private Guid CurrentUserId =>
        Guid.TryParse(_currentUserService.UserId, out var id) && id != Guid.Empty
            ? id
            : throw new UnauthorizedAccessException("An authenticated user is required.");

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    // ========================================================================
    // SCENARIOS
    // ========================================================================

    public async Task<BudgetScenarioDto> CreateScenarioAsync(CreateBudgetScenarioDto dto)
    {
        var tenantId = TenantId;
        var controlDimensions = await ResolveControlDimensionsAsync(
            tenantId, dto.ControlDimensionDefinitionIds);
        var fiscalYearExists = await _context.FiscalYears
            .AnyAsync(fy => fy.TenantId == tenantId && fy.Id == dto.FiscalYearId && !fy.IsDeleted);
        if (!fiscalYearExists)
            throw new InvalidOperationException("Fiscal year not found for the current tenant.");

        var currencyCode = NormalizeCurrency(dto.BaseCurrencyCode);
        var currencyExists = await _context.Currencies
            .AnyAsync(currency => currency.TenantId == tenantId
                && currency.CurrencyCode == currencyCode
                && currency.IsActive
                && !currency.IsDeleted);
        if (!currencyExists)
            throw new InvalidOperationException("Base currency is not active for the current tenant.");

        var normalizedName = dto.Name.Trim();
        var duplicateName = await _context.BudgetScenarios.AnyAsync(scenario =>
            scenario.TenantId == tenantId
            && scenario.FiscalYearId == dto.FiscalYearId
            && scenario.Name == normalizedName
            && !scenario.IsDeleted);
        if (duplicateName)
            throw new InvalidOperationException("A budget scenario with this name already exists for the fiscal year.");

        var scenario = new BudgetScenario
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedName,
            Description = NormalizeOptionalText(dto.Description),
            FiscalYearId = dto.FiscalYearId,
            BaseCurrencyCode = currencyCode,
            Status = DraftStatus,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedById = CurrentUserId
        };

        _context.BudgetScenarios.Add(scenario);
        foreach (var (definition, index) in controlDimensions.Select((definition, index) => (definition, index)))
        {
            scenario.ControlDimensions.Add(new BudgetScenarioControlDimension
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FinanceDimensionDefinitionId = definition.Id,
                DisplayOrder = index,
                CreatedAt = DateTime.UtcNow,
                CreatedById = CurrentUserId
            });
        }
        await _context.SaveChangesAsync();
        await RecordAuditAsync(
            FinanceAuditEvents.BudgetScenarioCreated,
            "BudgetScenario",
            scenario.Id,
            null,
            new { scenario.Name, scenario.Status, scenario.FiscalYearId });
        return await MapToDtoAsync(scenario);
    }

    public async Task<BudgetScenarioDto> UpdateScenarioAsync(UpdateBudgetScenarioDto dto)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .Include(s => s.FiscalYear)
            .Include(s => s.BudgetReturns)
            .Include(s => s.ControlDimensions)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == dto.Id);

        if (scenario == null)
            throw new KeyNotFoundException("Budget scenario not found.");
        EnsureScenarioEditable(scenario);
        ApplyRowVersion(scenario, dto.RowVersion);
        var before = new { scenario.Name, scenario.Description, scenario.IsActive, scenario.Status };

        var normalizedName = dto.Name.Trim();
        var duplicateName = await _context.BudgetScenarios.AnyAsync(candidate =>
            candidate.TenantId == tenantId
            && candidate.FiscalYearId == scenario.FiscalYearId
            && candidate.Id != scenario.Id
            && candidate.Name == normalizedName
            && !candidate.IsDeleted);
        if (duplicateName)
            throw new InvalidOperationException("A budget scenario with this name already exists for the fiscal year.");
        if (dto.IsActive)
            throw new InvalidOperationException(
                "A budget scenario becomes active only after workflow approval.");

        if (dto.ControlDimensionDefinitionIds is not null)
        {
            var controlDimensions = await ResolveControlDimensionsAsync(
                tenantId, dto.ControlDimensionDefinitionIds);
            var requestedControlIds = controlDimensions.Select(item => item.Id).ToHashSet();
            var currentControlIds = scenario.ControlDimensions
                .Where(item => !item.IsDeleted)
                .Select(item => item.FinanceDimensionDefinitionId)
                .ToHashSet();
            if (!requestedControlIds.SetEquals(currentControlIds))
            {
                var hasEntries = await _context.BudgetEntries.AnyAsync(entry =>
                    entry.TenantId == tenantId && !entry.IsDeleted
                    && entry.BudgetReturn!.BudgetScenarioId == scenario.Id);
                if (hasEntries)
                    throw new InvalidOperationException(
                        "Budget-control dimensions cannot change after worksheet entries exist. Create a new scenario version instead.");

                _context.BudgetScenarioControlDimensions.RemoveRange(scenario.ControlDimensions);
                scenario.ControlDimensions.Clear();
                foreach (var (definition, index) in controlDimensions.Select((definition, index) => (definition, index)))
                {
                    scenario.ControlDimensions.Add(new BudgetScenarioControlDimension
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId,
                        FinanceDimensionDefinitionId = definition.Id,
                        DisplayOrder = index, CreatedAt = DateTime.UtcNow,
                        CreatedById = CurrentUserId
                    });
                }
            }
        }

        scenario.Name = normalizedName;
        scenario.Description = NormalizeOptionalText(dto.Description);
        scenario.UpdatedAt = DateTime.UtcNow;
        scenario.LastModifiedById = CurrentUserId;

        await _context.SaveChangesAsync();
        await RecordAuditAsync(
            FinanceAuditEvents.BudgetScenarioUpdated,
            "BudgetScenario",
            scenario.Id,
            before,
            new { scenario.Name, scenario.Description, scenario.IsActive, scenario.Status });
        return await MapToDtoAsync(scenario);
    }

    public async Task<BudgetScenarioDto> GetScenarioAsync(Guid id)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .Include(s => s.FiscalYear)
            .Include(s => s.BudgetReturns)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id);

        if (scenario == null)
            throw new KeyNotFoundException("Budget scenario not found.");

        return await MapToDtoAsync(scenario);
    }

    public async Task<IEnumerable<BudgetScenarioDto>> GetScenariosForYearAsync(Guid fiscalYearId)
    {
        var tenantId = TenantId;
        var scenarios = await _context.BudgetScenarios
            .Include(s => s.FiscalYear)
            .Include(s => s.BudgetReturns)
            .Where(s => s.TenantId == tenantId && s.FiscalYearId == fiscalYearId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var result = new List<BudgetScenarioDto>(scenarios.Count);
        foreach (var scenario in scenarios)
            result.Add(await MapToDtoAsync(scenario));

        return result;
    }

    public async Task<bool> DeleteScenarioAsync(Guid id)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .Include(s => s.BudgetReturns)
            .ThenInclude(r => r.BudgetEntries)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id);

        if (scenario == null)
            return false;
        EnsureScenarioEditable(scenario);

        var entries = scenario.BudgetReturns.SelectMany(budgetReturn => budgetReturn.BudgetEntries).ToList();
        if (entries.Count > 0)
            _context.BudgetEntries.RemoveRange(entries);
        if (scenario.BudgetReturns.Count > 0)
            _context.BudgetReturns.RemoveRange(scenario.BudgetReturns);

        _context.BudgetScenarios.Remove(scenario);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<BudgetScenarioDto> OpenScenarioAsync(Guid id, string rowVersion)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .Include(s => s.FiscalYear)
            .Include(s => s.BudgetReturns)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id);

        if (scenario == null)
            throw new KeyNotFoundException("Budget scenario not found.");
        if (scenario.Status != DraftStatus)
            throw new InvalidOperationException("Only Draft scenarios can be opened for collection.");
        ApplyRowVersion(scenario, rowVersion);

        scenario.Status = CollectingStatus;
        scenario.UpdatedAt = DateTime.UtcNow;
        scenario.LastModifiedById = CurrentUserId;

        await _context.SaveChangesAsync();
        await RecordAuditAsync(
            FinanceAuditEvents.BudgetScenarioOpened,
            "BudgetScenario",
            scenario.Id,
            new { Status = DraftStatus },
            new { scenario.Status });
        return await MapToDtoAsync(scenario);
    }

    public async Task<BudgetScenarioDto> SubmitScenarioAsync(Guid id, string rowVersion)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .Include(s => s.FiscalYear)
            .Include(s => s.BudgetReturns)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id);

        if (scenario == null)
            throw new KeyNotFoundException("Budget scenario not found.");
        if (scenario.Status != CollectingStatus)
            throw new InvalidOperationException("Only Collecting scenarios can be submitted for approval.");
        ApplyRowVersion(scenario, rowVersion);
        if (scenario.BudgetReturns.Count == 0)
            throw new InvalidOperationException("A scenario without budget returns cannot be submitted.");
        if (scenario.BudgetReturns.Any(budgetReturn => budgetReturn.Status != ApprovedStatus))
            throw new InvalidOperationException("All budget returns must be approved before the scenario can be submitted.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            scenario.Status = InReviewStatus;
            scenario.UpdatedAt = DateTime.UtcNow;
            scenario.LastModifiedById = CurrentUserId;
            await _context.SaveChangesAsync();

            var workflowResult = await _workflowService.StartApprovalWorkflowAsync("BudgetScenario", id);
            if (!workflowResult.Success)
                throw new InvalidOperationException(
                    workflowResult.Message ?? "Unable to start budget scenario approval workflow.");

            await RecordAuditAsync(
                FinanceAuditEvents.BudgetScenarioSubmitted,
                "BudgetScenario",
                scenario.Id,
                new { Status = CollectingStatus },
                new { scenario.Status },
                workflowInstanceId: workflowResult.WorkflowInstanceId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return await MapToDtoAsync(scenario);
    }

    public async Task<BudgetScenarioDto> ArchiveScenarioAsync(Guid id, string rowVersion)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .Include(s => s.FiscalYear)
            .Include(s => s.BudgetReturns)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id);

        if (scenario == null)
            throw new KeyNotFoundException("Budget scenario not found.");
        if (scenario.Status is not (ApprovedStatus or SupersededStatus))
            throw new InvalidOperationException("Only Approved or Superseded scenarios can be archived.");
        ApplyRowVersion(scenario, rowVersion);

        scenario.Status = ArchivedStatus;
        scenario.IsActive = false;
        scenario.UpdatedAt = DateTime.UtcNow;
        scenario.LastModifiedById = CurrentUserId;

        await _context.SaveChangesAsync();
        await RecordAuditAsync(
            FinanceAuditEvents.BudgetScenarioArchived,
            "BudgetScenario",
            scenario.Id,
            new { Status = ApprovedStatus },
            new { scenario.Status, scenario.IsActive });
        return await MapToDtoAsync(scenario);
    }

    // ========================================================================
    // RETURNS
    // ========================================================================

    public async Task<BudgetReturnDto> CreateReturnAsync(CreateBudgetReturnDto dto)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == dto.BudgetScenarioId);
        if (scenario == null)
            throw new InvalidOperationException("Budget scenario not found for the current tenant.");
        EnsureScenarioCollecting(scenario);

        if (dto.SegmentValueId.HasValue)
        {
            var segmentExists = await _context.SegmentLookupValues
                .AnyAsync(s => s.TenantId == tenantId
                    && s.Id == dto.SegmentValueId.Value
                    && s.IsActive
                    && !s.IsDeleted);
            if (!segmentExists)
                throw new InvalidOperationException("Segment value not found for the current tenant.");
        }

        var duplicateReturn = await _context.BudgetReturns.AnyAsync(budgetReturn =>
            budgetReturn.TenantId == tenantId
            && budgetReturn.BudgetScenarioId == dto.BudgetScenarioId
            && budgetReturn.SegmentValueId == dto.SegmentValueId
            && !budgetReturn.IsDeleted);
        if (duplicateReturn)
            throw new InvalidOperationException("A budget return already exists for this scenario and segment.");

        if ((dto.AssignedToUserId.HasValue || dto.ApproverUserId.HasValue)
            && !await HasAnyBudgetPermissionAsync(new[] { FinancePermissions.AssignBudgetReturns }))
        {
            throw new UnauthorizedAccessException(
                "The Assign Budget Returns permission is required to set an assignee or approver.");
        }

        await ValidateTenantUserAsync(dto.AssignedToUserId, "Assigned user");
        await ValidateTenantUserAsync(dto.ApproverUserId, "Approver");

        var budgetReturn = new BudgetReturn
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BudgetScenarioId = dto.BudgetScenarioId,
            SegmentValueId = dto.SegmentValueId,
            AssignedToUserId = dto.AssignedToUserId,
            ApproverUserId = dto.ApproverUserId,
            Status = DraftStatus,
            Notes = NormalizeOptionalText(dto.Notes),
            CreatedAt = DateTime.UtcNow,
            CreatedById = CurrentUserId
        };

        _context.BudgetReturns.Add(budgetReturn);
        await _context.SaveChangesAsync();
        await RecordAuditAsync(
            FinanceAuditEvents.BudgetReturnCreated,
            "BudgetReturn",
            budgetReturn.Id,
            null,
            new
            {
                budgetReturn.BudgetScenarioId,
                budgetReturn.SegmentValueId,
                budgetReturn.AssignedToUserId,
                budgetReturn.ApproverUserId,
                budgetReturn.Status
            });
        return await MapToReturnDtoAsync(await GetReturnEntityAsync(budgetReturn.Id));
    }

    public async Task<BudgetReturnDto> UpdateReturnAsync(Guid id, UpdateBudgetReturnDto dto)
    {
        var budgetReturn = await GetReturnEntityAsync(id);
        EnsureScenarioCollecting(budgetReturn.BudgetScenario!);
        if (budgetReturn.Status != DraftStatus && budgetReturn.Status != RejectedStatus)
            throw new InvalidOperationException("Only Draft or Rejected returns can be updated.");
        ApplyRowVersion(budgetReturn, dto.RowVersion);
        var before = new
        {
            budgetReturn.AssignedToUserId,
            budgetReturn.ApproverUserId,
            budgetReturn.Notes,
            budgetReturn.Status
        };
        if (dto.ClearAssignedToUser && dto.AssignedToUserId.HasValue)
            throw new InvalidOperationException("An assignee cannot be set and cleared in the same request.");
        if (dto.ClearApproverUser && dto.ApproverUserId.HasValue)
            throw new InvalidOperationException("An approver cannot be set and cleared in the same request.");

        await ValidateTenantUserAsync(dto.AssignedToUserId, "Assigned user");
        await ValidateTenantUserAsync(dto.ApproverUserId, "Approver");

        if (dto.ClearAssignedToUser)
            budgetReturn.AssignedToUserId = null;
        else if (dto.AssignedToUserId.HasValue)
            budgetReturn.AssignedToUserId = dto.AssignedToUserId;

        if (dto.ClearApproverUser)
            budgetReturn.ApproverUserId = null;
        else if (dto.ApproverUserId.HasValue)
            budgetReturn.ApproverUserId = dto.ApproverUserId;

        if (dto.Notes != null)
            budgetReturn.Notes = NormalizeOptionalText(dto.Notes);

        budgetReturn.UpdatedAt = DateTime.UtcNow;
        budgetReturn.LastModifiedById = CurrentUserId;

        await _context.SaveChangesAsync();
        await RecordAuditAsync(
            FinanceAuditEvents.BudgetReturnUpdated,
            "BudgetReturn",
            budgetReturn.Id,
            before,
            new
            {
                budgetReturn.AssignedToUserId,
                budgetReturn.ApproverUserId,
                budgetReturn.Notes,
                budgetReturn.Status
            });
        return await MapToReturnDtoAsync(budgetReturn);
    }

    public async Task<BudgetReturnDto> GetReturnAsync(Guid id)
    {
        var budgetReturn = await GetReturnEntityAsync(id);
        await EnsureCanViewReturnAsync(budgetReturn);
        return await MapToReturnDtoAsync(budgetReturn);
    }

    public async Task<IEnumerable<BudgetReturnDto>> GetMyReturnsAsync()
    {
        var tenantId = TenantId;
        var userId = CurrentUserId;
        var returns = await _context.BudgetReturns
            .AsNoTracking()
            .Include(r => r.BudgetScenario)
            .Include(r => r.SegmentValue)
            .Where(r => r.TenantId == tenantId && r.AssignedToUserId == userId)
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToListAsync();

        return await MapToReturnDtosAsync(returns);
    }

    public async Task<IEnumerable<BudgetReturnDto>> GetReturnsForScenarioAsync(Guid scenarioId)
    {
        var tenantId = TenantId;
        var scenarioExists = await _context.BudgetScenarios
            .AnyAsync(scenario => scenario.TenantId == tenantId && scenario.Id == scenarioId);
        if (!scenarioExists)
            throw new KeyNotFoundException("Budget scenario not found.");

        var returns = await _context.BudgetReturns
            .AsNoTracking()
            .Include(r => r.BudgetScenario)
            .Include(r => r.SegmentValue)
            .Where(r => r.TenantId == tenantId && r.BudgetScenarioId == scenarioId)
            .OrderBy(r => r.SegmentValue!.SegmentValue)
            .ToListAsync();

        return await MapToReturnDtosAsync(returns);
    }

    public async Task<BudgetReturnDto> SubmitReturnAsync(Guid id, string rowVersion)
    {
        var budgetReturn = await GetReturnEntityAsync(id);
        EnsureScenarioCollecting(budgetReturn.BudgetScenario!);
        await EnsureCanPrepareReturnAsync(budgetReturn);

        if (budgetReturn.Status != DraftStatus && budgetReturn.Status != RejectedStatus)
            throw new InvalidOperationException("Only Draft or Rejected returns can be submitted.");
        if (!budgetReturn.AssignedToUserId.HasValue)
            throw new InvalidOperationException("The budget return must be assigned before it can be submitted.");

        var hasMaterialEntry = await _context.BudgetEntries.AnyAsync(entry =>
            entry.TenantId == TenantId
            && entry.BudgetReturnId == id
            && entry.Amount != 0);
        if (!hasMaterialEntry)
            throw new InvalidOperationException("Enter at least one non-zero budget amount before submitting.");
        ApplyRowVersion(budgetReturn, rowVersion);
        var previousStatus = budgetReturn.Status;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            budgetReturn.Status = SubmittedStatus;
            budgetReturn.SubmittedDate = DateTime.UtcNow;
            budgetReturn.ApprovedDate = null;
            budgetReturn.RejectionReason = null;
            budgetReturn.UpdatedAt = DateTime.UtcNow;
            budgetReturn.LastModifiedById = CurrentUserId;
            await _context.SaveChangesAsync();

            var workflowResult = await _workflowService.StartApprovalWorkflowAsync("BudgetReturn", id);
            if (!workflowResult.Success)
                throw new InvalidOperationException(
                    workflowResult.Message ?? "Unable to start budget return approval workflow.");

            await RecordAuditAsync(
                FinanceAuditEvents.BudgetReturnSubmitted,
                "BudgetReturn",
                budgetReturn.Id,
                new { Status = previousStatus },
                new { budgetReturn.Status, budgetReturn.SubmittedDate },
                workflowInstanceId: workflowResult.WorkflowInstanceId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return await MapToReturnDtoAsync(budgetReturn);
    }

    public async Task<BudgetReturnDto> RecallReturnAsync(Guid id, string rowVersion, string? reason)
    {
        var budgetReturn = await GetReturnEntityAsync(id);
        EnsureScenarioCollecting(budgetReturn.BudgetScenario!);
        await EnsureCanPrepareReturnAsync(budgetReturn);
        if (budgetReturn.Status != SubmittedStatus)
            throw new InvalidOperationException("Only Submitted returns can be recalled.");
        ApplyRowVersion(budgetReturn, rowVersion);
        var normalizedReason = NormalizeOptionalText(reason) ?? "Recalled by preparer.";

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var workflowResult = await _workflowService.RecallWorkflowAsync(
                "BudgetReturn",
                id,
                CurrentUserId,
                normalizedReason);
            if (!workflowResult.Success)
                throw new InvalidOperationException(
                    workflowResult.Message ?? "Unable to recall the budget return workflow.");

            budgetReturn.Status = DraftStatus;
            budgetReturn.SubmittedDate = null;
            budgetReturn.ApprovedDate = null;
            budgetReturn.RejectionReason = normalizedReason;
            budgetReturn.UpdatedAt = DateTime.UtcNow;
            budgetReturn.LastModifiedById = CurrentUserId;
            await _context.SaveChangesAsync();

            await RecordAuditAsync(
                FinanceAuditEvents.BudgetReturnRecalled,
                "BudgetReturn",
                budgetReturn.Id,
                new { Status = SubmittedStatus },
                new { budgetReturn.Status },
                normalizedReason,
                workflowResult.WorkflowInstanceId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return await MapToReturnDtoAsync(budgetReturn);
    }

    // ========================================================================
    // ENTRIES
    // ========================================================================

    public async Task<IEnumerable<BudgetEntryDto>> GetEntriesAsync(Guid returnId)
    {
        var budgetReturn = await GetReturnEntityAsync(returnId);
        await EnsureCanViewReturnAsync(budgetReturn);

        var tenantId = TenantId;
        var entries = await _context.BudgetEntries
            .AsNoTracking()
            .Include(e => e.Account)
            .Include(e => e.FiscalPeriod)
            .Include(e => e.FinanceDimensionSet).ThenInclude(set => set!.Items)
                .ThenInclude(item => item.FinanceDimensionDefinition)
            .Include(e => e.FinanceDimensionSet).ThenInclude(set => set!.Items)
                .ThenInclude(item => item.FinanceDimensionValue)
            .Where(e => e.TenantId == tenantId && e.BudgetReturnId == returnId)
            .OrderBy(e => e.Account!.AccountCode)
            .ThenBy(e => e.FiscalPeriod!.PeriodNumber)
            .ToListAsync();

        return entries.Select(MapToEntryDto).ToList();
    }

    public async Task<BudgetReturnDto> BulkSaveEntriesAsync(BulkSaveBudgetEntriesDto dto)
    {
        var tenantId = TenantId;
        var budgetReturn = await GetReturnEntityAsync(dto.BudgetReturnId);
        EnsureScenarioCollecting(budgetReturn.BudgetScenario!);
        await EnsureCanPrepareReturnAsync(budgetReturn);

        if (budgetReturn.Status != DraftStatus && budgetReturn.Status != RejectedStatus)
            throw new InvalidOperationException("Only Draft or Rejected returns can be edited.");
        ApplyRowVersion(budgetReturn, dto.ReturnRowVersion);

        if (dto.Entries.Any(entry => entry.BudgetReturnId != dto.BudgetReturnId))
            throw new InvalidOperationException("Every entry must reference the parent budget return.");
        if (dto.Entries.Any(entry => entry.Amount < 0))
            throw new InvalidOperationException("Budget amounts cannot be negative.");

        var accountIds = dto.Entries.Select(e => e.AccountId).Distinct().ToList();
        var periodIds = dto.Entries.Select(e => e.FiscalPeriodId).Distinct().ToList();

        var validAccountCount = await _context.Accounts.CountAsync(account =>
            account.TenantId == tenantId
            && accountIds.Contains(account.Id)
            && !account.IsDeleted
            && account.Status == AccountStatus.Active
            && account.AllowDirectPosting);
        if (validAccountCount != accountIds.Count)
            throw new InvalidOperationException(
                "One or more budget accounts are inactive, non-posting, or outside the current tenant.");

        var periods = await _context.FiscalPeriods.Where(period =>
            period.TenantId == tenantId
            && periodIds.Contains(period.Id)
            && period.FiscalYearId == budgetReturn.BudgetScenario!.FiscalYearId
            && !period.IsDeleted).ToListAsync();
        if (periods.Count != periodIds.Count)
            throw new InvalidOperationException(
                "One or more fiscal periods do not belong to the scenario fiscal year.");

        var controlDimensionIds = await _context.BudgetScenarioControlDimensions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && item.BudgetScenarioId == budgetReturn.BudgetScenarioId)
            .OrderBy(item => item.DisplayOrder)
            .Select(item => item.FinanceDimensionDefinitionId)
            .ToListAsync();
        var periodById = periods.ToDictionary(period => period.Id);
        var resolvedEntries = new List<(BudgetEntrySaveDto Incoming, FinanceDimensionSet? Set)>();
        foreach (var incoming in dto.Entries)
        {
            var set = await ResolveBudgetDimensionSetAsync(
                tenantId, controlDimensionIds, incoming.DimensionAssignments,
                periodById[incoming.FiscalPeriodId]);
            resolvedEntries.Add((incoming, set));
        }
        var duplicateCell = resolvedEntries
            .GroupBy(item => new
            {
                item.Incoming.AccountId,
                item.Incoming.FiscalPeriodId,
                FinanceDimensionSetId = item.Set?.Id
            })
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCell != null)
            throw new InvalidOperationException(
                "The request contains duplicate account, period, and budget-dimension entries.");

        var baseCurrency = NormalizeCurrency(budgetReturn.BudgetScenario!.BaseCurrencyCode);
        foreach (var incoming in dto.Entries)
        {
            if (!string.Equals(
                    NormalizeCurrency(incoming.CurrencyCode),
                    baseCurrency,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Budget worksheet entries must use the scenario base currency.");
            }
        }

        var existingEntries = await _context.BudgetEntries
            .Where(e => e.TenantId == tenantId && e.BudgetReturnId == dto.BudgetReturnId)
            .ToListAsync();
        var entryMap = existingEntries.ToDictionary(
            e => (e.AccountId, e.FiscalPeriodId, e.FinanceDimensionSetId));
        var entryById = existingEntries.ToDictionary(entry => entry.Id);

        foreach (var (incoming, dimensionSet) in resolvedEntries)
        {
            BudgetEntry? existing = null;
            if (incoming.Id.HasValue)
            {
                if (!entryById.TryGetValue(incoming.Id.Value, out existing))
                    throw new InvalidOperationException("A budget worksheet entry was not found in this return.");
            }
            else
            {
                entryMap.TryGetValue(
                    (incoming.AccountId, incoming.FiscalPeriodId, dimensionSet?.Id), out existing);
            }
            if (existing is not null)
            {
                if (!string.IsNullOrWhiteSpace(incoming.RowVersion))
                    ApplyRowVersion(existing, incoming.RowVersion);
                existing.AccountId = incoming.AccountId;
                existing.FiscalPeriodId = incoming.FiscalPeriodId;
                existing.FinanceDimensionSetId = dimensionSet?.Id;
                existing.Amount = incoming.Amount;
                existing.CurrencyCode = baseCurrency;
                existing.ExchangeRate = 1;
                existing.AmountBase = incoming.Amount;
                existing.Notes = NormalizeOptionalText(incoming.Notes);
                existing.UpdatedAt = DateTime.UtcNow;
                existing.LastModifiedById = CurrentUserId;
                continue;
            }

            var newEntry = new BudgetEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BudgetReturnId = dto.BudgetReturnId,
                AccountId = incoming.AccountId,
                FiscalPeriodId = incoming.FiscalPeriodId,
                FinanceDimensionSetId = dimensionSet?.Id,
                CurrencyCode = baseCurrency,
                ExchangeRate = 1,
                Amount = incoming.Amount,
                AmountBase = incoming.Amount,
                Notes = NormalizeOptionalText(incoming.Notes),
                CreatedAt = DateTime.UtcNow,
                CreatedById = CurrentUserId
            };
            _context.BudgetEntries.Add(newEntry);
            entryMap.Add((incoming.AccountId, incoming.FiscalPeriodId, dimensionSet?.Id), newEntry);
        }

        budgetReturn.UpdatedAt = DateTime.UtcNow;
        budgetReturn.LastModifiedById = CurrentUserId;
        await _context.SaveChangesAsync();
        await RecordAuditAsync(
            FinanceAuditEvents.BudgetWorksheetSaved,
            "BudgetReturn",
            budgetReturn.Id,
            null,
            new
            {
                EntryCount = dto.Entries.Count,
                TotalAmount = dto.Entries.Sum(entry => entry.Amount)
            });
        return await MapToReturnDtoAsync(budgetReturn);
    }

    public async Task<IEnumerable<BudgetEntryDto>> GetConsolidatedBudgetAsync(
        Guid scenarioId,
        Guid? accountId = null)
    {
        var tenantId = TenantId;
        var query = _context.BudgetEntries
            .AsNoTracking()
            .Include(e => e.Account)
            .Include(e => e.FiscalPeriod)
            .Include(e => e.BudgetReturn)
            .Include(e => e.FinanceDimensionSet).ThenInclude(set => set!.Items)
                .ThenInclude(item => item.FinanceDimensionDefinition)
            .Include(e => e.FinanceDimensionSet).ThenInclude(set => set!.Items)
                .ThenInclude(item => item.FinanceDimensionValue)
            .Where(e => e.TenantId == tenantId
                && e.BudgetReturn!.TenantId == tenantId
                && e.BudgetReturn.BudgetScenarioId == scenarioId
                && e.BudgetReturn.Status == ApprovedStatus);

        if (accountId.HasValue)
            query = query.Where(e => e.AccountId == accountId.Value);

        var entries = await query.ToListAsync();
        return entries.Select(MapToEntryDto).ToList();
    }

    public async Task<BudgetSummaryDto> GetScenarioSummaryAsync(Guid scenarioId)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == scenarioId && !s.IsDeleted);
        if (scenario == null)
            throw new KeyNotFoundException("Budget scenario not found.");

        var rows = await _context.BudgetEntries
            .Where(e => e.TenantId == tenantId
                && !e.IsDeleted
                && e.BudgetReturn!.TenantId == tenantId
                && e.BudgetReturn.BudgetScenarioId == scenarioId
                && e.BudgetReturn.Status == ApprovedStatus)
            .Select(e => new { e.Account!.AccountType, e.AmountBase })
            .ToListAsync();

        var totalRevenue = rows
            .Where(row => row.AccountType == AccountType.Revenue)
            .Sum(row => row.AmountBase);
        var totalExpense = rows
            .Where(row => row.AccountType == AccountType.Expense)
            .Sum(row => row.AmountBase);

        return new BudgetSummaryDto
        {
            ScenarioId = scenarioId,
            TotalRevenue = totalRevenue,
            TotalExpense = totalExpense,
            NetIncome = totalRevenue - totalExpense,
            CurrencyCode = scenario.BaseCurrencyCode
        };
    }

    public async Task<IReadOnlyList<BudgetAuditEventDto>> GetAuditHistoryAsync(
        string entityType,
        Guid entityId)
    {
        var normalizedEntityType = entityType.Trim();
        if (normalizedEntityType is not ("BudgetScenario" or "BudgetReturn"))
            throw new InvalidOperationException("Budget audit history supports BudgetScenario or BudgetReturn.");

        var tenantId = TenantId;
        var exists = normalizedEntityType == "BudgetScenario"
            ? await _context.BudgetScenarios.AnyAsync(item => item.TenantId == tenantId && item.Id == entityId)
            : await _context.BudgetReturns.AnyAsync(item => item.TenantId == tenantId && item.Id == entityId);
        if (!exists)
            throw new KeyNotFoundException($"{normalizedEntityType} not found.");

        if (_financeAuditService == null)
            return Array.Empty<BudgetAuditEventDto>();

        var auditRows = await _financeAuditService.GetAuditTrailAsync(
            tenantId,
            $"Finance.{normalizedEntityType}",
            entityId.ToString());
        return auditRows.Select(row => new BudgetAuditEventDto
        {
            Id = row.Id,
            Action = row.Action,
            Username = row.Username,
            Timestamp = row.Timestamp,
            OldValues = row.OldValues,
            NewValues = row.NewValues
        }).ToList();
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    private async Task<BudgetReturn> GetReturnEntityAsync(Guid id)
    {
        var tenantId = TenantId;
        var budgetReturn = await _context.BudgetReturns
            .Include(r => r.BudgetScenario)
            .Include(r => r.SegmentValue)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id);

        if (budgetReturn == null)
            throw new KeyNotFoundException("Budget return not found.");
        return budgetReturn;
    }

    private static void EnsureScenarioEditable(BudgetScenario scenario)
    {
        if (scenario.Status is not (DraftStatus or CollectingStatus))
            throw new InvalidOperationException(
                "Only Draft or Collecting budget scenarios can be modified.");
    }

    private static void EnsureScenarioCollecting(BudgetScenario scenario)
    {
        if (scenario.Status != CollectingStatus)
            throw new InvalidOperationException(
                "Budget returns can only be created or edited while the scenario is Collecting.");
    }

    private async Task EnsureCanViewReturnAsync(BudgetReturn budgetReturn)
    {
        if (budgetReturn.AssignedToUserId == CurrentUserId)
            return;
        if (await HasAnyBudgetPermissionAsync(OversightPermissions))
            return;

        throw new UnauthorizedAccessException("You do not have access to this budget return.");
    }

    private async Task EnsureCanPrepareReturnAsync(BudgetReturn budgetReturn)
    {
        if (budgetReturn.AssignedToUserId == CurrentUserId)
            return;
        if (await IsTenantAdministratorAsync())
            return;

        throw new UnauthorizedAccessException(
            "Only the assigned preparer can edit or submit this budget return.");
    }

    private async Task<bool> HasAnyBudgetPermissionAsync(IEnumerable<string> permissions)
    {
        var userId = CurrentUserId;
        var normalizedPermissions = permissions
            .Select(permission => permission.ToUpperInvariant())
            .ToArray();

        return await _context.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .AnyAsync(userRole =>
                userRole.Role.NormalizedName == Constants.Roles.SuperAdmin.ToUpper()
                || userRole.Role.NormalizedName == Constants.Roles.TenantAdmin.ToUpper()
                || userRole.Role.RolePermissions.Any(rolePermission =>
                    normalizedPermissions.Contains(rolePermission.Permission.Name.ToUpper())));
    }

    private async Task<bool> IsTenantAdministratorAsync()
    {
        var userId = CurrentUserId;
        var roleNames = new[]
        {
            Constants.Roles.SuperAdmin.ToUpperInvariant(),
            Constants.Roles.TenantAdmin.ToUpperInvariant()
        };

        return await _context.UserRoles
            .AsNoTracking()
            .AnyAsync(userRole =>
                userRole.UserId == userId
                && roleNames.Contains(userRole.Role.NormalizedName!));
    }

    private async Task ValidateTenantUserAsync(Guid? userId, string label)
    {
        if (!userId.HasValue)
            return;

        var tenantId = TenantId;
        var now = DateTime.UtcNow;
        var isValid = await _context.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId.Value
                && user.IsActive
                && (user.TenantId == tenantId
                    || user.UserTenants.Any(userTenant =>
                        userTenant.TenantId == tenantId
                        && !userTenant.IsDeleted
                        && userTenant.Status == UserTenantStatus.Active
                        && (userTenant.ExpiresAt == null || userTenant.ExpiresAt > now))));

        if (!isValid)
            throw new InvalidOperationException($"{label} is not an active user in the current tenant.");
    }

    private async Task<BudgetScenarioDto> MapToDtoAsync(BudgetScenario scenario)
    {
        var controlDimensions = await _context.BudgetScenarioControlDimensions
            .AsNoTracking()
            .Include(item => item.FinanceDimensionDefinition)
            .Where(item => item.TenantId == scenario.TenantId
                && item.BudgetScenarioId == scenario.Id && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.FinanceDimensionDefinition.Code)
            .ToListAsync();
        var userIds = new[]
            {
                scenario.LockedByUserId,
                scenario.AdoptedByUserId,
                scenario.SupersededByUserId
            }
            .Where(userId => userId.HasValue)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToArray();
        var userNames = await _context.Users
            .AsNoTracking()
            .Where(candidate => userIds.Contains(candidate.Id))
            .Select(candidate => new
            {
                candidate.Id,
                candidate.FirstName,
                candidate.LastName,
                candidate.UserName
            })
            .ToDictionaryAsync(
                candidate => candidate.Id,
                candidate =>
                {
                    var name = $"{candidate.FirstName} {candidate.LastName}".Trim();
                    return string.IsNullOrWhiteSpace(name) ? candidate.UserName ?? string.Empty : name;
                });

        return new BudgetScenarioDto
        {
            Id = scenario.Id,
            TenantId = scenario.TenantId,
            Name = scenario.Name,
            Description = scenario.Description,
            VersionType = scenario.VersionType,
            VersionNumber = scenario.VersionNumber,
            ParentScenarioId = scenario.ParentScenarioId,
            FiscalYearId = scenario.FiscalYearId,
            FiscalYearName = scenario.FiscalYear?.FiscalYearName ?? string.Empty,
            BaseCurrencyCode = scenario.BaseCurrencyCode,
            IsActive = scenario.IsActive,
            Status = scenario.Status,
            LockedDate = scenario.LockedDate,
            LockedByUserId = scenario.LockedByUserId,
            LockedByUserName = scenario.LockedByUserId.HasValue
                ? userNames.GetValueOrDefault(scenario.LockedByUserId.Value)
                : null,
            AdoptedAt = scenario.AdoptedAt,
            AdoptionEffectiveDate = scenario.AdoptionEffectiveDate,
            AdoptedByUserId = scenario.AdoptedByUserId,
            AdoptedByUserName = scenario.AdoptedByUserId.HasValue
                ? userNames.GetValueOrDefault(scenario.AdoptedByUserId.Value)
                : null,
            AdoptionReason = scenario.AdoptionReason,
            SupersededAt = scenario.SupersededAt,
            SupersededByUserId = scenario.SupersededByUserId,
            SupersededByUserName = scenario.SupersededByUserId.HasValue
                ? userNames.GetValueOrDefault(scenario.SupersededByUserId.Value)
                : null,
            SupersessionReason = scenario.SupersessionReason,
            CreatedAt = scenario.CreatedAt,
            UpdatedAt = scenario.UpdatedAt,
            ReturnCount = scenario.BudgetReturns.Count,
            ControlDimensions = controlDimensions.Select(item => new BudgetControlDimensionDto
            {
                FinanceDimensionDefinitionId = item.FinanceDimensionDefinitionId,
                DimensionCode = item.FinanceDimensionDefinition.Code,
                DimensionName = item.FinanceDimensionDefinition.Name,
                DisplayOrder = item.DisplayOrder
            }).ToList(),
            RowVersion = Convert.ToBase64String(scenario.RowVersion)
        };
    }

    private async Task<IReadOnlyList<FinanceDimensionDefinition>> ResolveControlDimensionsAsync(
        Guid tenantId,
        IEnumerable<Guid>? requestedIds)
    {
        var ids = (requestedIds ?? Array.Empty<Guid>()).ToList();
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new InvalidOperationException("Budget-control dimensions must contain unique Finance dimension IDs.");
        if (ids.Count == 0)
            return Array.Empty<FinanceDimensionDefinition>();

        var definitions = await _context.FinanceDimensionDefinitions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && ids.Contains(item.Id)
                && !item.IsDeleted && item.IsActive && item.Classification != "Derived")
            .ToListAsync();
        if (definitions.Count != ids.Count)
            throw new InvalidOperationException(
                "One or more budget-control dimensions are missing, inactive, derived, or outside the current tenant.");
        var byId = definitions.ToDictionary(item => item.Id);
        return ids.Select(id => byId[id]).ToList();
    }

    private async Task<FinanceDimensionSet?> ResolveBudgetDimensionSetAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> controlDimensionIds,
        IReadOnlyCollection<BudgetDimensionAssignmentInputDto>? assignments,
        FiscalPeriod period)
    {
        var supplied = assignments?.ToList() ?? new List<BudgetDimensionAssignmentInputDto>();
        if (controlDimensionIds.Count == 0)
        {
            if (supplied.Count != 0)
                throw new InvalidOperationException(
                    "This legacy budget scenario declares no controlling transaction dimensions.");
            return null;
        }
        if (supplied.Count != controlDimensionIds.Count
            || supplied.Any(item => item.FinanceDimensionDefinitionId == Guid.Empty
                || item.FinanceDimensionValueId == Guid.Empty)
            || supplied.Select(item => item.FinanceDimensionDefinitionId).Distinct().Count() != supplied.Count
            || !supplied.Select(item => item.FinanceDimensionDefinitionId).ToHashSet()
                .SetEquals(controlDimensionIds))
        {
            throw new InvalidOperationException(
                "Every budget entry must provide exactly one value for each scenario budget-control dimension.");
        }

        var valueIds = supplied.Select(item => item.FinanceDimensionValueId).ToArray();
        var values = await _context.FinanceDimensionValues.AsNoTracking()
            .Include(item => item.FinanceDimensionDefinition)
            .Where(item => item.TenantId == tenantId && valueIds.Contains(item.Id)
                && !item.IsDeleted && item.IsActive
                && item.FinanceDimensionDefinition.TenantId == tenantId
                && !item.FinanceDimensionDefinition.IsDeleted
                && item.FinanceDimensionDefinition.IsActive
                && item.EffectiveDate.Date <= period.StartDate.Date
                && (!item.ExpiryDate.HasValue || item.ExpiryDate.Value.Date >= period.EndDate.Date))
            .ToListAsync();
        if (values.Count != supplied.Count)
            throw new InvalidOperationException(
                "A budget dimension value is inactive, outside the tenant, or not effective for the full fiscal period.");
        var valueById = values.ToDictionary(item => item.Id);
        if (supplied.Any(item => valueById[item.FinanceDimensionValueId].FinanceDimensionDefinitionId
            != item.FinanceDimensionDefinitionId))
            throw new InvalidOperationException("A budget dimension value does not belong to its declared dimension.");

        var resolved = supplied.Select(item => valueById[item.FinanceDimensionValueId])
            .OrderBy(item => item.FinanceDimensionDefinition.DisplayOrder)
            .ThenBy(item => item.FinanceDimensionDefinition.Code)
            .ToList();
        var canonical = string.Join("|", resolved.Select(item =>
            $"{item.FinanceDimensionDefinitionId:N}:{item.Id:N}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var idBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"FIN-DIMSET|{tenantId:N}|{hash}"));
        var setId = new Guid(idBytes.AsSpan(0, 16));
        var existing = _context.FinanceDimensionSets.Local.FirstOrDefault(item => item.Id == setId)
            ?? await _context.FinanceDimensionSets.Include(item => item.Items)
                .SingleOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted
                    && (item.Id == setId || item.CombinationHash == hash));
        if (existing is not null)
        {
            if (existing.Id != setId || existing.CombinationHash != hash
                || existing.Items.Count != resolved.Count
                || resolved.Any(value => !existing.Items.Any(item =>
                    item.FinanceDimensionDefinitionId == value.FinanceDimensionDefinitionId
                    && item.FinanceDimensionValueId == value.Id)))
                throw new InvalidOperationException("Finance dimension-set identity or assignment evidence is inconsistent.");
            return existing;
        }

        var now = DateTime.UtcNow;
        var set = new FinanceDimensionSet
        {
            Id = setId, TenantId = tenantId, CombinationHash = hash,
            DisplayValue = string.Join(" · ", resolved.Select(item =>
                $"{item.FinanceDimensionDefinition.Code}={item.Code}")),
            CreatedAt = now, CreatedById = CurrentUserId
        };
        foreach (var value in resolved)
        {
            set.Items.Add(new FinanceDimensionSetItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FinanceDimensionSetId = set.Id,
                FinanceDimensionDefinitionId = value.FinanceDimensionDefinitionId,
                FinanceDimensionValueId = value.Id,
                DimensionCodeSnapshot = value.FinanceDimensionDefinition.Code,
                DimensionValueCodeSnapshot = value.Code,
                DimensionValueNameSnapshot = value.Name,
                CreatedAt = now, CreatedById = CurrentUserId
            });
        }
        _context.FinanceDimensionSets.Add(set);
        return set;
    }

    private async Task<BudgetReturnDto> MapToReturnDtoAsync(BudgetReturn budgetReturn)
    {
        var dtos = await MapToReturnDtosAsync(new[] { budgetReturn });
        return dtos[0];
    }

    private async Task<IReadOnlyList<BudgetReturnDto>> MapToReturnDtosAsync(
        IReadOnlyCollection<BudgetReturn> returns)
    {
        if (returns.Count == 0)
            return Array.Empty<BudgetReturnDto>();

        var tenantId = TenantId;
        var returnIds = returns.Select(budgetReturn => budgetReturn.Id).ToArray();
        var totals = await _context.BudgetEntries
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId && returnIds.Contains(entry.BudgetReturnId))
            .GroupBy(entry => entry.BudgetReturnId)
            .Select(group => new { ReturnId = group.Key, Total = group.Sum(entry => entry.AmountBase) })
            .ToDictionaryAsync(row => row.ReturnId, row => row.Total);

        var userIds = returns
            .SelectMany(budgetReturn => new[]
            {
                budgetReturn.AssignedToUserId,
                budgetReturn.ApproverUserId
            })
            .Where(userId => userId.HasValue)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToArray();

        var userRows = await _context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.UserName })
            .ToListAsync();
        var userNames = userRows.ToDictionary(
            user => user.Id,
            user =>
            {
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                return string.IsNullOrWhiteSpace(fullName) ? user.UserName ?? string.Empty : fullName;
            });

        return returns.Select(budgetReturn => new BudgetReturnDto
        {
            Id = budgetReturn.Id,
            TenantId = budgetReturn.TenantId,
            BudgetScenarioId = budgetReturn.BudgetScenarioId,
            BudgetScenarioName = budgetReturn.BudgetScenario?.Name ?? string.Empty,
            SegmentValueId = budgetReturn.SegmentValueId,
            SegmentValueName = budgetReturn.SegmentValue?.Description,
            SegmentValueCode = budgetReturn.SegmentValue?.SegmentValue,
            AssignedToUserId = budgetReturn.AssignedToUserId,
            AssignedToUserName = budgetReturn.AssignedToUserId.HasValue
                && userNames.TryGetValue(budgetReturn.AssignedToUserId.Value, out var assigneeName)
                    ? assigneeName
                    : null,
            ApproverUserId = budgetReturn.ApproverUserId,
            ApproverUserName = budgetReturn.ApproverUserId.HasValue
                && userNames.TryGetValue(budgetReturn.ApproverUserId.Value, out var approverName)
                    ? approverName
                    : null,
            Status = budgetReturn.Status,
            Notes = budgetReturn.Notes,
            RejectionReason = budgetReturn.RejectionReason,
            SubmittedDate = budgetReturn.SubmittedDate,
            ApprovedDate = budgetReturn.ApprovedDate,
            TotalAmountBase = totals.GetValueOrDefault(budgetReturn.Id),
            CreatedAt = budgetReturn.CreatedAt,
            UpdatedAt = budgetReturn.UpdatedAt,
            RowVersion = Convert.ToBase64String(budgetReturn.RowVersion)
        }).ToList();
    }

    private static BudgetEntryDto MapToEntryDto(BudgetEntry entry)
    {
        return new BudgetEntryDto
        {
            Id = entry.Id,
            TenantId = entry.TenantId,
            BudgetReturnId = entry.BudgetReturnId,
            AccountId = entry.AccountId,
            AccountCode = entry.Account?.AccountCode ?? string.Empty,
            AccountName = entry.Account?.AccountName ?? string.Empty,
            FiscalPeriodId = entry.FiscalPeriodId,
            FinanceDimensionSetId = entry.FinanceDimensionSetId,
            DimensionCombinationHash = entry.FinanceDimensionSet?.CombinationHash,
            DimensionAssignments = entry.FinanceDimensionSet?.Items
                .OrderBy(item => item.FinanceDimensionDefinition.DisplayOrder)
                .ThenBy(item => item.DimensionCodeSnapshot)
                .Select(item => new BudgetDimensionAssignmentDto
                {
                    FinanceDimensionDefinitionId = item.FinanceDimensionDefinitionId,
                    FinanceDimensionValueId = item.FinanceDimensionValueId,
                    DimensionCode = item.DimensionCodeSnapshot,
                    DimensionName = item.FinanceDimensionDefinition.Name,
                    ValueCode = item.DimensionValueCodeSnapshot,
                    ValueName = item.DimensionValueNameSnapshot
                }).ToList() ?? new List<BudgetDimensionAssignmentDto>(),
            PeriodName = entry.FiscalPeriod?.PeriodName ?? string.Empty,
            CurrencyCode = entry.CurrencyCode,
            ExchangeRate = entry.ExchangeRate,
            Amount = entry.Amount,
            AmountBase = entry.AmountBase,
            Notes = entry.Notes,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt,
            RowVersion = Convert.ToBase64String(entry.RowVersion)
        };
    }

    private void ApplyRowVersion<TEntity>(TEntity entity, string rowVersion)
        where TEntity : class
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
            throw new InvalidOperationException("A row-version token is required.");

        byte[] token;
        try
        {
            token = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("The row-version token is invalid.");
        }

        if (token.Length != 8)
            throw new InvalidOperationException("The row-version token is invalid.");

        _context.Entry(entity).Property<byte[]>("RowVersion").OriginalValue = token;
    }

    private async Task RecordAuditAsync(
        string eventType,
        string entityType,
        Guid entityId,
        object? beforeValues,
        object? afterValues,
        string? comment = null,
        Guid? workflowInstanceId = null)
    {
        if (_financeAuditService == null)
            return;

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = TenantId,
            SourceModule = "BUDGETING",
            SourceDocumentType = entityType,
            SourceDocumentId = entityId,
            WorkflowInstanceId = workflowInstanceId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Comment = comment,
            Reason = eventType is FinanceAuditEvents.BudgetReturnRecalled
                or FinanceAuditEvents.BudgetReturnRejected
                or FinanceAuditEvents.BudgetScenarioRejected
                or FinanceAuditEvents.BudgetRevisionRejected
                    ? comment
                    : null,
            Resource = $"Finance.{entityType}",
            ResourceId = entityId.ToString()
        });
    }

    private static string NormalizeCurrency(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 3)
            throw new InvalidOperationException("A valid three-letter currency code is required.");
        return normalized;
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
