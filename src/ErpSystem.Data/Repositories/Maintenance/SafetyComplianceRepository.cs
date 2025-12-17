using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

/// <summary>
/// Repository implementation for safety compliance record operations
/// </summary>
public class SafetyComplianceRepository : GenericRepository<SafetyComplianceRecord>, ISafetyComplianceRepository
{
    public SafetyComplianceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<SafetyComplianceRecord>> GetByProtocolIdAsync(Guid protocolId)
    {
        return await _context.SafetyComplianceRecords
            .Where(scr => scr.ProtocolId == protocolId && !scr.IsDeleted)
            .OrderByDescending(scr => scr.CheckDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyComplianceRecord>> GetByTechnicianIdAsync(Guid technicianId)
    {
        return await _context.SafetyComplianceRecords
            .Where(scr => scr.TechnicianId == technicianId && !scr.IsDeleted)
            .OrderByDescending(scr => scr.CheckDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyComplianceRecord>> GetByWorkOrderIdAsync(Guid workOrderId)
    {
        return await _context.SafetyComplianceRecords
            .Where(scr => scr.WorkOrderId == workOrderId && !scr.IsDeleted)
            .OrderByDescending(scr => scr.CheckDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyComplianceRecord>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.SafetyComplianceRecords
            .Where(scr => scr.CheckDate >= startDate &&
                         scr.CheckDate <= endDate &&
                         !scr.IsDeleted)
            .OrderByDescending(scr => scr.CheckDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyComplianceRecord>> GetByComplianceStatusAsync(string status)
    {
        return await _context.SafetyComplianceRecords
            .Where(scr => scr.ComplianceStatus == status && !scr.IsDeleted)
            .OrderByDescending(scr => scr.CheckDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyComplianceRecord>> GetViolationsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.SafetyComplianceRecords
            .Where(scr => scr.ComplianceStatus == "Non-Compliant" &&
                         !string.IsNullOrEmpty(scr.Violations) &&
                         !scr.IsDeleted);

        if (startDate.HasValue)
        {
            query = query.Where(scr => scr.CheckDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(scr => scr.CheckDate <= endDate.Value);
        }

        return await query
            .OrderByDescending(scr => scr.CheckDate)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetComplianceStatusCountsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.SafetyComplianceRecords
            .Where(scr => !scr.IsDeleted);

        if (fromDate.HasValue)
        {
            query = query.Where(scr => scr.CheckDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(scr => scr.CheckDate <= toDate.Value);
        }

        return await query
            .GroupBy(scr => scr.ComplianceStatus)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<Guid, int>> GetViolationsByProtocolAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.SafetyComplianceRecords
            .Where(scr => !string.IsNullOrEmpty(scr.Violations) && !scr.IsDeleted);

        if (fromDate.HasValue)
        {
            query = query.Where(scr => scr.CheckDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(scr => scr.CheckDate <= toDate.Value);
        }

        return await query
            .GroupBy(scr => scr.ProtocolId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<IEnumerable<SafetyComplianceRecord>> GetRecentComplianceChecksAsync(int daysBack = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);

        return await _context.SafetyComplianceRecords
            .Where(scr => scr.CheckDate >= cutoffDate && !scr.IsDeleted)
            .OrderByDescending(scr => scr.CheckDate)
            .ToListAsync();
    }

    public async Task<decimal> GetComplianceRateAsync(Guid? protocolId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.SafetyComplianceRecords
            .Where(scr => !scr.IsDeleted);

        if (protocolId.HasValue)
        {
            query = query.Where(scr => scr.ProtocolId == protocolId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(scr => scr.CheckDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(scr => scr.CheckDate <= toDate.Value);
        }

        var totalChecks = await query.CountAsync();
        if (totalChecks == 0)
        {
            return 0;
        }

        var compliantChecks = await query.CountAsync(scr => scr.ComplianceStatus == "Compliant");
        return (decimal)compliantChecks / totalChecks * 100;
    }

    public async Task<SafetyComplianceRecord?> GetLatestComplianceAsync(Guid protocolId, Guid technicianId)
    {
        return await _context.SafetyComplianceRecords
            .Where(scr => scr.ProtocolId == protocolId &&
                         scr.TechnicianId == technicianId &&
                         !scr.IsDeleted)
            .OrderByDescending(scr => scr.CheckDate)
            .FirstOrDefaultAsync();
    }

    public async Task<decimal> GetComplianceRateAsync(Guid protocolId, DateTime startDate, DateTime endDate)
    {
        var query = _context.SafetyComplianceRecords
            .Where(scr => scr.ProtocolId == protocolId &&
                         scr.CheckDate >= startDate &&
                         scr.CheckDate <= endDate &&
                         !scr.IsDeleted);

        var totalChecks = await query.CountAsync();
        if (totalChecks == 0)
        {
            return 0;
        }

        var compliantChecks = await query.CountAsync(scr => scr.ComplianceStatus == "Compliant");
        return (decimal)compliantChecks / totalChecks * 100;
    }
}
