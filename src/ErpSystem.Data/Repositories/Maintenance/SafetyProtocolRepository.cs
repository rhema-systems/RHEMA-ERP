using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

/// <summary>
/// Repository implementation for safety protocol operations
/// </summary>
public class SafetyProtocolRepository : GenericRepository<SafetyProtocol>, ISafetyProtocolRepository
{
    public SafetyProtocolRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<SafetyProtocol>> GetActiveAsync()
    {
        return await _context.SafetyProtocols
            .Where(sp => sp.IsActive && !sp.IsDeleted)
            .OrderBy(sp => sp.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetByCategoryAsync(string category)
    {
        return await _context.SafetyProtocols
            .Where(sp => sp.Category == category && !sp.IsDeleted)
            .OrderBy(sp => sp.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetByRiskLevelAsync(string riskLevel)
    {
        return await _context.SafetyProtocols
            .Where(sp => sp.RiskLevel == riskLevel && !sp.IsDeleted)
            .OrderBy(sp => sp.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetRegulatoryAsync()
    {
        return await _context.SafetyProtocols
            .Where(sp => sp.IsRegulatory && !sp.IsDeleted)
            .OrderBy(sp => sp.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetDueForReviewAsync(int daysAhead = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(daysAhead);

        return await _context.SafetyProtocols
            .Where(sp => sp.NextReviewDate <= cutoffDate &&
                        sp.IsActive &&
                        !sp.IsDeleted)
            .OrderBy(sp => sp.NextReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetExpiredAsync()
    {
        var today = DateTime.UtcNow;

        return await _context.SafetyProtocols
            .Where(sp => sp.ExpirationDate <= today && !sp.IsDeleted)
            .OrderBy(sp => sp.ExpirationDate)
            .ToListAsync();
    }

    public async Task<SafetyProtocol?> GetByCodeAsync(string code)
    {
        return await _context.SafetyProtocols
            .Where(sp => sp.Code == code && !sp.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        var query = _context.SafetyProtocols
            .Where(sp => sp.Code == code && !sp.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(sp => sp.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<Dictionary<string, int>> GetCategoryCountsAsync()
    {
        return await _context.SafetyProtocols
            .Where(sp => !sp.IsDeleted)
            .GroupBy(sp => sp.Category)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetRiskLevelCountsAsync()
    {
        return await _context.SafetyProtocols
            .Where(sp => !sp.IsDeleted)
            .GroupBy(sp => sp.RiskLevel)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, decimal>> GetComplianceScoresByProtocolAsync()
    {
        return await _context.SafetyProtocols
            .Where(sp => !sp.IsDeleted)
            .ToDictionaryAsync(sp => sp.Title, sp => sp.ComplianceScore);
    }

    public async Task<IEnumerable<SafetyProtocol>> GetProtocolsWithLowComplianceAsync(decimal threshold = 75.0m)
    {
        return await _context.SafetyProtocols
            .Where(sp => sp.ComplianceScore < threshold &&
                        sp.IsActive &&
                        !sp.IsDeleted)
            .OrderBy(sp => sp.ComplianceScore)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetProtocolsWithHighViolationsAsync(int violationThreshold = 5)
    {
        return await _context.SafetyProtocols
            .Where(sp => sp.TotalViolations >= violationThreshold &&
                        sp.IsActive &&
                        !sp.IsDeleted)
            .OrderByDescending(sp => sp.TotalViolations)
            .ToListAsync();
    }

    public async Task UpdateComplianceStatsAsync(Guid protocolId, decimal complianceScore, int totalViolations)
    {
        var protocol = await GetByIdAsync(protocolId);
        if (protocol != null)
        {
            protocol.ComplianceScore = complianceScore;
            protocol.TotalViolations = totalViolations;
            await UpdateAsync(protocol);
        }
    }

    public async Task<IEnumerable<SafetyProtocol>> GetBySeverityAsync(string severity)
    {
        return await _context.SafetyProtocols
            .Where(sp => sp.Severity == severity && !sp.IsDeleted)
            .OrderBy(sp => sp.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetByRegulatoryStandardAsync(string standard)
    {
        return await _context.SafetyProtocols
            .Where(sp => sp.RegulatoryStandard == standard && !sp.IsDeleted)
            .OrderBy(sp => sp.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetMandatoryProtocolsAsync()
    {
        return await _context.SafetyProtocols
            .Where(sp => sp.IsMandatory && !sp.IsDeleted)
            .OrderBy(sp => sp.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetProtocolsDueForReviewAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _context.SafetyProtocols
            .Where(sp => sp.NextReviewDate <= today &&
                        sp.IsActive &&
                        !sp.IsDeleted)
            .OrderBy(sp => sp.NextReviewDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetOverdueProtocolsAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _context.SafetyProtocols
            .Where(sp => sp.NextReviewDate < today &&
                        sp.IsActive &&
                        !sp.IsDeleted)
            .OrderBy(sp => sp.NextReviewDate)
            .ToListAsync();
    }

    public async Task<bool> IsProtocolNameUniqueAsync(string name, Guid? excludeId = null)
    {
        var query = _context.SafetyProtocols
            .Where(sp => sp.Title == name && !sp.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(sp => sp.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetMandatoryAsync()
    {
        return await GetMandatoryProtocolsAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetOverdueAsync()
    {
        return await GetOverdueProtocolsAsync();
    }

    public async Task<IEnumerable<SafetyProtocol>> GetDueForReviewAsync()
    {
        return await GetProtocolsDueForReviewAsync();
    }
}
