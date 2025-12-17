using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

#region Performance Tracking Repository Implementations

public class SupplierPerformanceMetricRepository : GenericRepository<SupplierPerformanceMetric>, ISupplierPerformanceMetricRepository
{
    public SupplierPerformanceMetricRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SupplierPerformanceMetric?> GetByBusinessPartnerAndPeriodAsync(Guid businessPartnerId, string metricPeriod, int year, int? month, int? quarter)
    {
        return await _dbSet
            .Where(m => m.BusinessPartnerId == businessPartnerId 
                && m.MetricPeriod == metricPeriod 
                && m.Year == year
                && m.Month == month
                && m.Quarter == quarter
                && !m.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SupplierPerformanceMetric>> GetByBusinessPartnerAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(m => m.BusinessPartnerId == businessPartnerId && !m.IsDeleted)
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Quarter ?? 0)
            .ThenByDescending(m => m.Month ?? 0)
            .ToListAsync();
    }

    public async Task<IEnumerable<SupplierPerformanceMetric>> GetByPeriodAsync(string metricPeriod, int year, int? month = null, int? quarter = null)
    {
        return await _dbSet
            .Where(m => m.MetricPeriod == metricPeriod 
                && m.Year == year
                && m.Month == month
                && m.Quarter == quarter
                && !m.IsDeleted)
            .Include(m => m.BusinessPartner)
            .OrderBy(m => m.BusinessPartner.PartnerName)
            .ToListAsync();
    }

    public async Task<IEnumerable<SupplierPerformanceMetric>> GetTrendsAsync(Guid businessPartnerId, int numberOfPeriods)
    {
        return await _dbSet
            .Where(m => m.BusinessPartnerId == businessPartnerId && !m.IsDeleted)
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Quarter ?? 0)
            .ThenByDescending(m => m.Month ?? 0)
            .Take(numberOfPeriods)
            .ToListAsync();
    }

    public async Task<string> GenerateMetricNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var yearPrefix = year.ToString().Substring(2, 2);

        var lastMetric = await _dbSet
            .Where(m => m.CreatedAt.Year == year)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        int nextSequence = 1;
        if (lastMetric != null)
        {
            // Extract sequence number from last metric if it follows the pattern
            var lastNumber = lastMetric.Id.ToString();
            if (lastNumber.Length >= 4)
            {
                var lastSeq = lastNumber.Substring(lastNumber.Length - 4);
                if (int.TryParse(lastSeq, out int seq))
                {
                    nextSequence = seq + 1;
                }
            }
        }

        return $"PM{yearPrefix}{nextSequence:D4}"; // Format as PM24NNNN
    }
}

public class QualityIncidentRepository : GenericRepository<QualityIncident>, IQualityIncidentRepository
{
    public QualityIncidentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<QualityIncident>> GetByBusinessPartnerAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(i => i.BusinessPartnerId == businessPartnerId && !i.IsDeleted)
            .OrderByDescending(i => i.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<QualityIncident>> GetByStatusAsync(string status)
    {
        return await _dbSet
            .Where(i => i.Status == status && !i.IsDeleted)
            .Include(i => i.BusinessPartner)
            .OrderByDescending(i => i.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<QualityIncident>> GetBySeverityAsync(string severity)
    {
        return await _dbSet
            .Where(i => i.Severity == severity && !i.IsDeleted)
            .Include(i => i.BusinessPartner)
            .OrderByDescending(i => i.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<QualityIncident>> GetOpenIncidentsAsync()
    {
        return await _dbSet
            .Where(i => i.Status != "Closed" && !i.IsDeleted)
            .Include(i => i.BusinessPartner)
            .OrderByDescending(i => i.IncidentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<QualityIncident>> GetRecentIncidentsAsync(Guid businessPartnerId, int days = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        return await _dbSet
            .Where(i => i.BusinessPartnerId == businessPartnerId 
                && i.IncidentDate >= cutoffDate 
                && !i.IsDeleted)
            .OrderByDescending(i => i.IncidentDate)
            .ToListAsync();
    }

    public async Task<QualityIncident?> GetByIncidentNumberAsync(string incidentNumber)
    {
        return await _dbSet
            .Where(i => i.IncidentNumber == incidentNumber && !i.IsDeleted)
            .Include(i => i.BusinessPartner)
            .FirstOrDefaultAsync();
    }

    public async Task<string> GenerateIncidentNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var yearPrefix = year.ToString().Substring(2, 2);

        var lastIncident = await _dbSet
            .Where(i => i.CreatedAt.Year == year)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync();

        int nextSequence = 1;
        if (lastIncident != null && !string.IsNullOrEmpty(lastIncident.IncidentNumber))
        {
            // Extract sequence from QI24NNNN format
            var parts = lastIncident.IncidentNumber.Replace("QI", "").Replace(yearPrefix, "");
            if (int.TryParse(parts, out int seq))
            {
                nextSequence = seq + 1;
            }
        }

        return $"QI{yearPrefix}{nextSequence:D4}"; // Format as QI24NNNN
    }

    public async Task<int> GetOpenIncidentCountAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(i => i.BusinessPartnerId == businessPartnerId
                && i.Status != "Closed"
                && !i.IsDeleted)
            .CountAsync();
    }
}

public class PerformanceReviewRepository : GenericRepository<PerformanceReview>, IPerformanceReviewRepository
{
    public PerformanceReviewRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PerformanceReview>> GetByBusinessPartnerAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(r => r.BusinessPartnerId == businessPartnerId && !r.IsDeleted)
            .Include(r => r.ReviewedBy)
            .OrderByDescending(r => r.ReviewDate)
            .ToListAsync();
    }

    public async Task<PerformanceReview?> GetLatestReviewAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(r => r.BusinessPartnerId == businessPartnerId && !r.IsDeleted)
            .Include(r => r.ReviewedBy)
            .Include(r => r.BusinessPartner)
            .OrderByDescending(r => r.ReviewDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PerformanceReview>> GetByPeriodAsync(string reviewPeriod)
    {
        return await _dbSet
            .Where(r => r.ReviewPeriod == reviewPeriod && !r.IsDeleted)
            .Include(r => r.BusinessPartner)
            .Include(r => r.ReviewedBy)
            .OrderBy(r => r.BusinessPartner.PartnerName)
            .ToListAsync();
    }

    public async Task<IEnumerable<PerformanceReview>> GetByStatusAsync(string status)
    {
        return await _dbSet
            .Where(r => r.Status == status && !r.IsDeleted)
            .Include(r => r.BusinessPartner)
            .Include(r => r.ReviewedBy)
            .OrderByDescending(r => r.ReviewDate)
            .ToListAsync();
    }

    public async Task<PerformanceReview?> GetByReviewNumberAsync(string reviewNumber)
    {
        return await _dbSet
            .Where(r => r.ReviewNumber == reviewNumber && !r.IsDeleted)
            .Include(r => r.BusinessPartner)
            .Include(r => r.ReviewedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<string> GenerateReviewNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var yearPrefix = year.ToString().Substring(2, 2);

        var lastReview = await _dbSet
            .Where(r => r.CreatedAt.Year == year)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        int nextSequence = 1;
        if (lastReview != null && !string.IsNullOrEmpty(lastReview.ReviewNumber))
        {
            // Extract sequence from PR24NNNN format
            var parts = lastReview.ReviewNumber.Replace("PR", "").Replace(yearPrefix, "");
            if (int.TryParse(parts, out int seq))
            {
                nextSequence = seq + 1;
            }
        }

        return $"PR{yearPrefix}{nextSequence:D4}"; // Format as PR24NNNN
    }
}

#endregion

