using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Budget;

public class BudgetService : IBudgetService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkflowService _workflowService;

    public BudgetService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IWorkflowService workflowService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _workflowService = workflowService;
    }

    private Guid CurrentUserId => Guid.TryParse(_currentUserService.UserId, out var id) ? id : Guid.Empty;
    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    // ========================================================================
    // SCENARIOS
    // ========================================================================

    public async Task<BudgetScenarioDto> CreateScenarioAsync(CreateBudgetScenarioDto dto)
    {
        var tenantId = TenantId;
        var fiscalYearExists = await _context.FiscalYears
            .AnyAsync(fy => fy.TenantId == tenantId && fy.Id == dto.FiscalYearId && !fy.IsDeleted);
        if (!fiscalYearExists)
            throw new InvalidOperationException("Fiscal year not found for the current tenant.");

        var scenario = new BudgetScenario
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            FiscalYearId = dto.FiscalYearId,
            BaseCurrencyCode = dto.BaseCurrencyCode,
            Status = "Draft",
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.BudgetScenarios.Add(scenario);
        await _context.SaveChangesAsync();
        return await MapToDto(scenario);
    }

    public async Task<BudgetScenarioDto> UpdateScenarioAsync(UpdateBudgetScenarioDto dto)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .Include(s => s.FiscalYear)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == dto.Id);

        if (scenario == null) throw new KeyNotFoundException("Budget Scenario not found");

        if (scenario.Status == "Locked")
            throw new InvalidOperationException("Cannot edit a locked budget scenario.");

        scenario.Name = dto.Name;
        scenario.Description = dto.Description;
        scenario.IsActive = dto.IsActive;
        scenario.UpdatedAt = DateTime.UtcNow;

        // If making active, deactivate others for same year
        if (dto.IsActive)
        {
            var others = await _context.BudgetScenarios
                .Where(s => s.TenantId == tenantId && s.FiscalYearId == scenario.FiscalYearId && s.Id != scenario.Id)
                .ToListAsync();
            
            foreach (var other in others)
            {
                other.IsActive = false; // Only one active per year
            }
        }

        await _context.SaveChangesAsync();
        return await MapToDto(scenario);
    }

    public async Task<BudgetScenarioDto> GetScenarioAsync(Guid id)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .Include(s => s.FiscalYear)
            .Include(s => s.BudgetReturns)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id);

        if (scenario == null) throw new KeyNotFoundException("Budget Scenario not found");

        return await MapToDto(scenario);
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

        var result = new List<BudgetScenarioDto>();
        foreach (var s in scenarios)
        {
            result.Add(await MapToDto(s));
        }
        return result;
    }

    public async Task<bool> DeleteScenarioAsync(Guid id)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id);
        if (scenario == null) return false;

        if (scenario.Status == "Locked")
            throw new InvalidOperationException("Cannot delete a locked budget scenario.");

        _context.BudgetScenarios.Remove(scenario);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<BudgetScenarioDto> LockScenarioAsync(Guid id)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .Include(s => s.FiscalYear)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id);

        if (scenario == null) throw new KeyNotFoundException("Budget Scenario not found");

        // Validate all returns are approved? Or force lock?
        // Let's assume we can only lock if all returns are Submitted or Approved.
        
        scenario.Status = "Locked";
        scenario.LockedDate = DateTime.UtcNow;
        // scenario.LockedByUserId = ... 

        await _context.SaveChangesAsync();
        return await MapToDto(scenario);
    }

    // ========================================================================
    // RETURNS
    // ========================================================================

    public async Task<BudgetReturnDto> CreateReturnAsync(CreateBudgetReturnDto dto)
    {
        var tenantId = TenantId;
        var scenarioExists = await _context.BudgetScenarios
            .AnyAsync(s => s.TenantId == tenantId && s.Id == dto.BudgetScenarioId && !s.IsDeleted);
        if (!scenarioExists)
            throw new InvalidOperationException("Budget scenario not found for the current tenant.");

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

        var budgetReturn = new BudgetReturn
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BudgetScenarioId = dto.BudgetScenarioId,
            SegmentValueId = dto.SegmentValueId,
            AssignedToUserId = dto.AssignedToUserId,
            ApproverUserId = dto.ApproverUserId,
            Status = "Draft",
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.BudgetReturns.Add(budgetReturn);
        await _context.SaveChangesAsync();
        return await MapToReturnDto(budgetReturn);
    }

    public async Task<BudgetReturnDto> UpdateReturnAsync(Guid id, UpdateBudgetReturnDto dto)
    {
        var budgetReturn = await GetReturnEntityAsync(id);
        if (budgetReturn.Status != "Draft" && budgetReturn.Status != "Rejected")
            throw new InvalidOperationException("Only Draft or Rejected returns can be updated.");

        if (dto.AssignedToUserId.HasValue)
            budgetReturn.AssignedToUserId = dto.AssignedToUserId;

        if (dto.ApproverUserId.HasValue)
            budgetReturn.ApproverUserId = dto.ApproverUserId;

        if (dto.Notes != null)
            budgetReturn.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

        budgetReturn.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await MapToReturnDto(budgetReturn);
    }

    public async Task<BudgetReturnDto> GetReturnAsync(Guid id)
    {
        var budgetReturn = await GetReturnEntityAsync(id);
        return await MapToReturnDto(budgetReturn);
    }

    public async Task<IEnumerable<BudgetReturnDto>> GetReturnsForScenarioAsync(Guid scenarioId)
    {
         var tenantId = TenantId;
         var returns = await _context.BudgetReturns
            .Include(r => r.BudgetScenario)
            .Include(r => r.SegmentValue)
            .Where(r => r.TenantId == tenantId && r.BudgetScenarioId == scenarioId)
            .ToListAsync();

        var dtos = new List<BudgetReturnDto>();
        foreach(var r in returns)
        {
            dtos.Add(await MapToReturnDto(r));
        }
        return dtos;
    }

    public async Task<BudgetReturnDto> SubmitReturnAsync(Guid id)
    {
        var budgetReturn = await GetReturnEntityAsync(id);
        if (budgetReturn.Status != "Draft" && budgetReturn.Status != "Rejected")
            throw new InvalidOperationException("Only Draft or Rejected returns can be submitted.");

        budgetReturn.Status = "Submitted";
        budgetReturn.SubmittedDate = DateTime.UtcNow;
        budgetReturn.RejectionReason = null; // Clear rejection reason

        await _context.SaveChangesAsync();

        var workflowResult = await _workflowService.StartApprovalWorkflowAsync("BudgetReturn", id);
        if (!workflowResult.Success)
        {
            budgetReturn.Status = "Draft";
            budgetReturn.SubmittedDate = null;
            await _context.SaveChangesAsync();

            throw new InvalidOperationException(workflowResult.Message ?? "Unable to start budget return approval workflow.");
        }

        return await MapToReturnDto(budgetReturn);
    }

    public async Task<BudgetReturnDto> ApproveReturnAsync(Guid id, Guid approverId)
    {
        var budgetReturn = await GetReturnEntityAsync(id);
        if (budgetReturn.Status != "Submitted")
            throw new InvalidOperationException("Only Submitted returns can be approved.");

        var workflowUserId = CurrentUserId != Guid.Empty ? CurrentUserId : approverId;
        if (workflowUserId == Guid.Empty)
            throw new InvalidOperationException("Unable to resolve the current approver.");

        if (!await _workflowService.CanUserApproveAsync("BudgetReturn", id, workflowUserId))
            throw new InvalidOperationException("This budget return is assigned to another workflow approver.");

        var workflowResult = await _workflowService.ProcessApprovalStepAsync("BudgetReturn", id, workflowUserId, "Approve");
        if (!workflowResult.Success)
            throw new InvalidOperationException(workflowResult.Message ?? "Unable to process budget return approval.");

        if (workflowResult.Status != WorkflowInstanceStatus.Completed)
            return await MapToReturnDto(budgetReturn);

        budgetReturn.Status = "Approved";
        budgetReturn.ApprovedDate = DateTime.UtcNow;
        // budgetReturn.ApproverUserId = approverId; // Override? Or verify?

        await _context.SaveChangesAsync();
        return await MapToReturnDto(budgetReturn);
    }

    public async Task<BudgetReturnDto> RejectReturnAsync(Guid id, string reason, Guid rejectorId)
    {
        var budgetReturn = await GetReturnEntityAsync(id);
        if (budgetReturn.Status != "Submitted" && budgetReturn.Status != "Approved")
             throw new InvalidOperationException("Can only reject Submitted or Approved returns.");

        var workflowUserId = CurrentUserId != Guid.Empty ? CurrentUserId : rejectorId;
        if (workflowUserId == Guid.Empty)
            throw new InvalidOperationException("Unable to resolve the current approver.");

        if (!await _workflowService.CanUserApproveAsync("BudgetReturn", id, workflowUserId))
            throw new InvalidOperationException("This budget return is assigned to another workflow approver.");

        var workflowResult = await _workflowService.ProcessApprovalStepAsync("BudgetReturn", id, workflowUserId, "Reject", reason);
        if (!workflowResult.Success)
            throw new InvalidOperationException(workflowResult.Message ?? "Unable to process budget return rejection.");

        if (workflowResult.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            return await MapToReturnDto(budgetReturn);

        budgetReturn.Status = "Rejected";
        budgetReturn.RejectionReason = reason;

        await _context.SaveChangesAsync();
        return await MapToReturnDto(budgetReturn);
    }

    // ========================================================================
    // ENTRIES
    // ========================================================================

    public async Task<IEnumerable<BudgetEntryDto>> GetEntriesAsync(Guid returnId)
    {
        var tenantId = TenantId;
        var entries = await _context.BudgetEntries
            .Include(e => e.Account)
            .Include(e => e.FiscalPeriod)
            .Where(e => e.TenantId == tenantId && e.BudgetReturnId == returnId)
            .OrderBy(e => e.Account!.AccountCode)
            .ThenBy(e => e.FiscalPeriod!.PeriodNumber)
            .ToListAsync();

        return entries.Select(MapToEntryDto).ToList();
    }

    public async Task BulkSaveEntriesAsync(BulkSaveBudgetEntriesDto dto)
    {
        var tenantId = TenantId;
        var budgetReturn = await _context.BudgetReturns
            .Include(r => r.BudgetScenario)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == dto.BudgetReturnId);
            
        if (budgetReturn == null) throw new KeyNotFoundException("Budget Return not found");

        if (budgetReturn.Status == "Submitted" || budgetReturn.Status == "Approved")
            throw new InvalidOperationException("Cannot edit entries for Submitted or Approved returns.");

        // Clean slate or Upsert?
        // Upsert is safer. matching on AccountId + PeriodId

        var existingEntries = await _context.BudgetEntries
            .Where(e => e.TenantId == tenantId && e.BudgetReturnId == dto.BudgetReturnId)
            .ToListAsync();

        var accountIds = dto.Entries.Select(e => e.AccountId).Distinct().ToList();
        var periodIds = dto.Entries.Select(e => e.FiscalPeriodId).Distinct().ToList();
        var validAccountCount = await _context.Accounts
            .CountAsync(a => a.TenantId == tenantId && accountIds.Contains(a.Id) && !a.IsDeleted);
        if (validAccountCount != accountIds.Count)
            throw new InvalidOperationException("One or more budget accounts do not belong to the current tenant.");

        var validPeriodCount = await _context.FiscalPeriods
            .CountAsync(p => p.TenantId == tenantId && periodIds.Contains(p.Id) && !p.IsDeleted);
        if (validPeriodCount != periodIds.Count)
            throw new InvalidOperationException("One or more fiscal periods do not belong to the current tenant.");

        var entryMap = existingEntries.ToDictionary(e => (e.AccountId, e.FiscalPeriodId));

        foreach (var incoming in dto.Entries)
        {
            decimal amountBase = incoming.Amount;
            
            // Check exchange rate if not base currency
            if (incoming.CurrencyCode != budgetReturn.BudgetScenario!.BaseCurrencyCode)
            {
                 // Typically we'd fetch rate if not provided, but here we assume rate is provided
                 // Or we could validate:
                 // if (incoming.ExchangeRate <= 0) throw new Exception("Exchange rate must be positive");
                 amountBase = incoming.Amount * incoming.ExchangeRate;
            }
            else
            {
                incoming.ExchangeRate = 1; // Force 1 for base
            }

            if (entryMap.TryGetValue((incoming.AccountId, incoming.FiscalPeriodId), out var existing))
            {
                // Update
                existing.Amount = incoming.Amount;
                existing.CurrencyCode = incoming.CurrencyCode;
                existing.ExchangeRate = incoming.ExchangeRate;
                existing.AmountBase = amountBase;
                existing.Notes = incoming.Notes;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Create
                var newEntry = new BudgetEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BudgetReturnId = dto.BudgetReturnId,
                    AccountId = incoming.AccountId,
                    FiscalPeriodId = incoming.FiscalPeriodId,
                    CurrencyCode = incoming.CurrencyCode,
                    ExchangeRate = incoming.ExchangeRate,
                    Amount = incoming.Amount,
                    AmountBase = amountBase,
                    Notes = incoming.Notes,
                    CreatedAt = DateTime.UtcNow
                };
                _context.BudgetEntries.Add(newEntry);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<BudgetEntryDto>> GetConsolidatedBudgetAsync(Guid scenarioId, Guid? accountId = null)
    {
        var tenantId = TenantId;
        // This aggregates all APPROVED returns for a scenario
        var query = _context.BudgetEntries
            .Include(e => e.Account)
            .Include(e => e.FiscalPeriod)
            .Include(e => e.BudgetReturn)
            .Where(e => e.TenantId == tenantId
                     && e.BudgetReturn!.TenantId == tenantId
                     && e.BudgetReturn.BudgetScenarioId == scenarioId
                     && e.BudgetReturn.Status == "Approved");

        if (accountId.HasValue)
        {
            query = query.Where(e => e.AccountId == accountId.Value);
        }

        var entries = await query.ToListAsync();
        return entries.Select(MapToEntryDto).ToList();
    }

    public async Task<BudgetSummaryDto> GetScenarioSummaryAsync(Guid scenarioId)
    {
        var tenantId = TenantId;
        var scenario = await _context.BudgetScenarios
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == scenarioId && !s.IsDeleted);
        if (scenario == null)
            throw new KeyNotFoundException("Budget scenario not found");

        // Summarize approved returns only, mirroring the consolidated budget definition.
        var rows = await _context.BudgetEntries
            .Where(e => e.TenantId == tenantId
                     && !e.IsDeleted
                     && e.BudgetReturn!.TenantId == tenantId
                     && e.BudgetReturn.BudgetScenarioId == scenarioId
                     && e.BudgetReturn.Status == "Approved")
            .Select(e => new { e.Account!.AccountType, e.AmountBase })
            .ToListAsync();

        var totalRevenue = rows.Where(r => r.AccountType == AccountType.Revenue).Sum(r => r.AmountBase);
        var totalExpense = rows.Where(r => r.AccountType == AccountType.Expense).Sum(r => r.AmountBase);

        return new BudgetSummaryDto
        {
            ScenarioId = scenarioId,
            TotalRevenue = totalRevenue,
            TotalExpense = totalExpense,
            NetIncome = totalRevenue - totalExpense,
            CurrencyCode = scenario.BaseCurrencyCode
        };
    }

    //Helpers
    private async Task<BudgetReturn> GetReturnEntityAsync(Guid id)
    {
        var tenantId = TenantId;
        var r = await _context.BudgetReturns
             .Include(r => r.BudgetScenario)
             .Include(r => r.SegmentValue) // To get segment name
             .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id);
             
         if (r == null) throw new KeyNotFoundException("Budget Return not found");
         return r;
    }

    private Task<BudgetScenarioDto> MapToDto(BudgetScenario s)
    {
        // Re-fetch logic if navigation property missing logic omitted for brevity, assuming Include used
        return Task.FromResult(new BudgetScenarioDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            FiscalYearId = s.FiscalYearId,
            FiscalYearName = s.FiscalYear?.FiscalYearName ?? "",
            BaseCurrencyCode = s.BaseCurrencyCode,
            IsActive = s.IsActive,
            Status = s.Status,
            LockedDate = s.LockedDate,
            CreatedAt = s.CreatedAt,
            ReturnCount = s.BudgetReturns.Count
        });
    }

    private async Task<BudgetReturnDto> MapToReturnDto(BudgetReturn r)
    {
        // Basic mapping
        var tenantId = TenantId;
        var total = await _context.BudgetEntries
            .Where(e => e.TenantId == tenantId && e.BudgetReturnId == r.Id)
            .SumAsync(e => e.AmountBase);
        
        return new BudgetReturnDto
        {
            Id = r.Id,
            BudgetScenarioId = r.BudgetScenarioId,
            BudgetScenarioName = r.BudgetScenario?.Name ?? "",
            SegmentValueId = r.SegmentValueId,
            SegmentValueName = r.SegmentValue?.Description,
            SegmentValueCode = r.SegmentValue?.SegmentValue,
            AssignedToUserId = r.AssignedToUserId,
            ApproverUserId = r.ApproverUserId,
            Status = r.Status,
            Notes = r.Notes,
            RejectionReason = r.RejectionReason,
            SubmittedDate = r.SubmittedDate,
            ApprovedDate = r.ApprovedDate,
            TotalAmountBase = total
        };
    }
    
    private BudgetEntryDto MapToEntryDto(BudgetEntry e)
    {
        return new BudgetEntryDto
        {
            Id = e.Id,
            BudgetReturnId = e.BudgetReturnId,
            AccountId = e.AccountId,
            AccountCode = e.Account?.AccountCode ?? "",
            AccountName = e.Account?.AccountName ?? "",
            FiscalPeriodId = e.FiscalPeriodId,
            PeriodName = e.FiscalPeriod?.PeriodName ?? "",
            CurrencyCode = e.CurrencyCode,
            ExchangeRate = e.ExchangeRate,
            Amount = e.Amount,
            AmountBase = e.AmountBase,
            Notes = e.Notes
        };
    }
}
