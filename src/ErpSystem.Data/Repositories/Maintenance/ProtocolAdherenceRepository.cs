using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Data.Repositories.Maintenance;

/// <summary>
/// Repository for managing protocol adherence records
/// </summary>
public class ProtocolAdherenceRepository : GenericRepository<ProtocolAdherence>, IProtocolAdherenceRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public ProtocolAdherenceRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) 
        : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    public async Task<IEnumerable<ProtocolAdherence>> GetByProtocolIdAsync(Guid protocolId)
    {
        return await _context.Set<ProtocolAdherence>()
            .Where(pa => pa.ProtocolId == protocolId && pa.TenantId == _currentUserProvider.TenantId)
            .Include(pa => pa.Protocol)
            .OrderByDescending(pa => pa.AdherenceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProtocolAdherence>> GetByWorkOrderIdAsync(Guid workOrderId)
    {
        return await _context.Set<ProtocolAdherence>()
            .Where(pa => pa.WorkOrderId == workOrderId && pa.TenantId == _currentUserProvider.TenantId)
            .Include(pa => pa.Protocol)
            .OrderByDescending(pa => pa.AdherenceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProtocolAdherence>> GetByTechnicianIdAsync(Guid technicianId)
    {
        return await _context.Set<ProtocolAdherence>()
            .Where(pa => pa.TechnicianId == technicianId && pa.TenantId == _currentUserProvider.TenantId)
            .Include(pa => pa.Protocol)
            .OrderByDescending(pa => pa.AdherenceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProtocolAdherence>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Set<ProtocolAdherence>()
            .Where(pa => pa.AdherenceDate >= startDate && 
                        pa.AdherenceDate <= endDate && 
                        pa.TenantId == _currentUserProvider.TenantId)
            .Include(pa => pa.Protocol)
            .OrderByDescending(pa => pa.AdherenceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProtocolAdherence>> GetByComplianceScoreRangeAsync(int minScore, int maxScore)
    {
        return await _context.Set<ProtocolAdherence>()
            .Where(pa => pa.ComplianceScore >= minScore && 
                        pa.ComplianceScore <= maxScore && 
                        pa.TenantId == _currentUserProvider.TenantId)
            .Include(pa => pa.Protocol)
            .OrderByDescending(pa => pa.ComplianceScore)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProtocolAdherence>> GetVerifiedAdherenceAsync()
    {
        return await _context.Set<ProtocolAdherence>()
            .Where(pa => pa.VerificationDate.HasValue && pa.TenantId == _currentUserProvider.TenantId)
            .Include(pa => pa.Protocol)
            .OrderByDescending(pa => pa.AdherenceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProtocolAdherence>> GetUnverifiedAdherenceAsync()
    {
        return await _context.Set<ProtocolAdherence>()
            .Where(pa => !pa.VerificationDate.HasValue && pa.TenantId == _currentUserProvider.TenantId)
            .Include(pa => pa.Protocol)
            .OrderByDescending(pa => pa.AdherenceDate)
            .ToListAsync();
    }

    public async Task<decimal> GetComplianceRateAsync(Guid protocolId, DateTime startDate, DateTime endDate)
    {
        var totalRecords = await _context.Set<ProtocolAdherence>()
            .CountAsync(pa => pa.ProtocolId == protocolId && 
                             pa.AdherenceDate >= startDate && 
                             pa.AdherenceDate <= endDate &&
                             pa.TenantId == _currentUserProvider.TenantId);

        if (totalRecords == 0)
            return 0m;

        var compliantRecords = await _context.Set<ProtocolAdherence>()
            .CountAsync(pa => pa.ProtocolId == protocolId && 
                             pa.AdherenceDate >= startDate && 
                             pa.AdherenceDate <= endDate &&
                             pa.ComplianceScore >= 80 && // Assuming 80+ is compliant
                             pa.TenantId == _currentUserProvider.TenantId);

        return (decimal)compliantRecords / totalRecords * 100m;
    }

    public async Task<decimal> GetTechnicianComplianceRateAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        var totalRecords = await _context.Set<ProtocolAdherence>()
            .CountAsync(pa => pa.TechnicianId == technicianId && 
                             pa.AdherenceDate >= startDate && 
                             pa.AdherenceDate <= endDate &&
                             pa.TenantId == _currentUserProvider.TenantId);

        if (totalRecords == 0)
            return 0m;

        var compliantRecords = await _context.Set<ProtocolAdherence>()
            .CountAsync(pa => pa.TechnicianId == technicianId && 
                             pa.AdherenceDate >= startDate && 
                             pa.AdherenceDate <= endDate &&
                             pa.ComplianceScore >= 80 &&
                             pa.TenantId == _currentUserProvider.TenantId);

        return (decimal)compliantRecords / totalRecords * 100m;
    }

    public async Task<int> GetAdherenceCountAsync(Guid protocolId, DateTime startDate, DateTime endDate)
    {
        return await _context.Set<ProtocolAdherence>()
            .CountAsync(pa => pa.ProtocolId == protocolId && 
                             pa.AdherenceDate >= startDate && 
                             pa.AdherenceDate <= endDate &&
                             pa.TenantId == _currentUserProvider.TenantId);
    }
}
