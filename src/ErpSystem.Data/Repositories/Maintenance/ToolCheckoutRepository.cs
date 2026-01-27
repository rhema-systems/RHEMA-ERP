using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

public class ToolCheckoutRepository : IToolCheckoutRepository
{
    private readonly ApplicationDbContext _context;

    public ToolCheckoutRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ToolCheckout?> GetByIdAsync(Guid id)
    {
        return await _context.ToolCheckouts
            .Include(tc => tc.Tool)
            .Include(tc => tc.CheckedOutBy)
            .Include(tc => tc.CheckedInBy)
            .Include(tc => tc.WorkOrder)
            .Include(tc => tc.JobCard)
            .FirstOrDefaultAsync(tc => tc.Id == id);
    }

    public async Task<List<ToolCheckout>> GetActiveCheckoutsAsync(Guid? employeeId = null)
    {
        var query = _context.ToolCheckouts
            .Include(tc => tc.Tool)
            .Include(tc => tc.CheckedOutBy)
            .Include(tc => tc.WorkOrder)
            .Where(tc => tc.Status == "CheckedOut");

        if (employeeId.HasValue)
        {
            query = query.Where(tc => tc.CheckedOutById == employeeId.Value);
        }

        return await query
            .OrderBy(tc => tc.CheckoutDate)
            .ToListAsync();
    }

    public async Task<List<ToolCheckout>> GetOverdueCheckoutsAsync()
    {
        var now = DateTime.UtcNow;

        return await _context.ToolCheckouts
            .Include(tc => tc.Tool)
            .Include(tc => tc.CheckedOutBy)
            .Include(tc => tc.WorkOrder)
            .Where(tc => tc.Status == "CheckedOut" &&
                        tc.ExpectedReturnDate.HasValue &&
                        tc.ExpectedReturnDate.Value < now)
            .OrderBy(tc => tc.ExpectedReturnDate)
            .ToListAsync();
    }

    public async Task<List<ToolCheckout>> GetToolCheckoutHistoryAsync(Guid toolId, int limit = 50)
    {
        return await _context.ToolCheckouts
            .Include(tc => tc.CheckedOutBy)
            .Include(tc => tc.CheckedInBy)
            .Include(tc => tc.WorkOrder)
            .Where(tc => tc.ToolId == toolId)
            .OrderByDescending(tc => tc.CheckoutDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<ToolCheckout>> GetEmployeeCheckoutHistoryAsync(Guid employeeId, int limit = 50)
    {
        return await _context.ToolCheckouts
            .Include(tc => tc.Tool)
            .Include(tc => tc.WorkOrder)
            .Where(tc => tc.CheckedOutById == employeeId)
            .OrderByDescending(tc => tc.CheckoutDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<ToolCheckout>> GetByWorkOrderAsync(Guid workOrderId)
    {
        return await _context.ToolCheckouts
            .Include(tc => tc.Tool)
            .Include(tc => tc.CheckedOutBy)
            .Where(tc => tc.WorkOrderId == workOrderId)
            .OrderBy(tc => tc.CheckoutDate)
            .ToListAsync();
    }

    public async Task<ToolCheckout?> GetActiveCheckoutByToolIdAsync(Guid toolId)
    {
        return await _context.ToolCheckouts
            .Include(tc => tc.CheckedOutBy)
            .Include(tc => tc.WorkOrder)
            .FirstOrDefaultAsync(tc => tc.ToolId == toolId && tc.Status == "CheckedOut");
    }

    public async Task AddAsync(ToolCheckout checkout)
    {
        await _context.ToolCheckouts.AddAsync(checkout);
    }

    public Task UpdateAsync(ToolCheckout checkout)
    {
        var entry = _context.Entry(checkout);

        if (entry.State == EntityState.Detached)
        {
            _context.ToolCheckouts.Attach(checkout);
            entry.State = EntityState.Modified;
        }
        else
        {
            _context.ToolCheckouts.Update(checkout);
        }

        return Task.CompletedTask;
    }

    public async Task<int> GetTotalUsageDaysAsync(Guid toolId)
    {
        var checkouts = await _context.ToolCheckouts
            .Where(tc => tc.ToolId == toolId && tc.ActualReturnDate.HasValue)
            .ToListAsync();

        return checkouts.Sum(tc =>
        {
            var checkoutDate = tc.CheckoutDate;
            var returnDate = tc.ActualReturnDate ?? DateTime.UtcNow;
            return (int)(returnDate - checkoutDate).TotalDays;
        });
    }

    public async Task<int> GetTotalCheckoutCountAsync(Guid toolId)
    {
        return await _context.ToolCheckouts
            .Where(tc => tc.ToolId == toolId)
            .CountAsync();
    }
}
