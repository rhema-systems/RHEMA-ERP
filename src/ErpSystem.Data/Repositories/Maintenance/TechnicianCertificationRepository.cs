using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

/// <summary>
/// Repository implementation for technician certification operations
/// </summary>
public class TechnicianCertificationRepository : GenericRepository<TechnicianCertification>, ITechnicianCertificationRepository
{
    public TechnicianCertificationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TechnicianCertification>> GetByTechnicianIdAsync(Guid technicianId)
    {
        return await _context.TechnicianCertifications
            .Where(tc => tc.TechnicianId == technicianId && !tc.IsDeleted)
            .Include(tc => tc.Technician)
            .OrderBy(tc => tc.CertificationName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianCertification>> GetExpiredCertificationsAsync()
    {
        var today = DateTime.UtcNow.Date;

        return await _context.TechnicianCertifications
            .Where(tc => tc.ExpirationDate.HasValue &&
                        tc.ExpirationDate.Value < today &&
                        !tc.IsDeleted)
            .Include(tc => tc.Technician)
            .OrderBy(tc => tc.ExpirationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianCertification>> GetExpiringSoonAsync(int daysAhead = 30)
    {
        var today = DateTime.UtcNow.Date;
        var futureDate = today.AddDays(daysAhead);

        return await _context.TechnicianCertifications
            .Where(tc => tc.ExpirationDate.HasValue &&
                        tc.ExpirationDate.Value >= today &&
                        tc.ExpirationDate.Value <= futureDate &&
                        !tc.IsDeleted)
            .Include(tc => tc.Technician)
            .OrderBy(tc => tc.ExpirationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianCertification>> GetByCategoryAsync(string category)
    {
        return await _context.TechnicianCertifications
            .Where(tc => tc.Category == category && !tc.IsDeleted)
            .Include(tc => tc.Technician)
            .OrderBy(tc => tc.CertificationName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianCertification>> GetByStatusAsync(string status)
    {
        return await _context.TechnicianCertifications
            .Where(tc => tc.Status == status && !tc.IsDeleted)
            .Include(tc => tc.Technician)
            .OrderBy(tc => tc.CertificationName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianCertification>> GetMandatoryCertificationsAsync()
    {
        return await _context.TechnicianCertifications
            .Where(tc => tc.IsMandatory && !tc.IsDeleted)
            .Include(tc => tc.Technician)
            .OrderBy(tc => tc.CertificationName)
            .ToListAsync();
    }

    public async Task<TechnicianCertification?> GetByCertificationNumberAsync(string certificationNumber)
    {
        return await _context.TechnicianCertifications
            .Where(tc => tc.CertificationNumber == certificationNumber && !tc.IsDeleted)
            .Include(tc => tc.Technician)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsCertificationNumberUniqueAsync(string certificationNumber, Guid? excludeId = null)
    {
        var query = _context.TechnicianCertifications
            .Where(tc => tc.CertificationNumber == certificationNumber && !tc.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(tc => tc.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<TechnicianCertification>> GetByIssuingOrganizationAsync(string organization)
    {
        return await _context.TechnicianCertifications
            .Where(tc => tc.IssuingOrganization == organization && !tc.IsDeleted)
            .Include(tc => tc.Technician)
            .OrderBy(tc => tc.CertificationName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianCertification>> GetUnverifiedCertificationsAsync()
    {
        return await _context.TechnicianCertifications
            .Where(tc => !tc.IsVerified && !tc.IsDeleted)
            .Include(tc => tc.Technician)
            .OrderBy(tc => tc.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianCertification>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.TechnicianCertifications
            .Where(tc => tc.IssueDate >= startDate.Date &&
                        tc.IssueDate <= endDate.Date &&
                        !tc.IsDeleted)
            .Include(tc => tc.Technician)
            .OrderBy(tc => tc.IssueDate)
            .ToListAsync();
    }

    public async Task<int> GetCertificationCountByTechnicianAsync(Guid technicianId)
    {
        return await _context.TechnicianCertifications
            .Where(tc => tc.TechnicianId == technicianId && !tc.IsDeleted)
            .CountAsync();
    }

    public async Task<int> GetExpiredCountByTechnicianAsync(Guid technicianId)
    {
        var today = DateTime.UtcNow.Date;

        return await _context.TechnicianCertifications
            .Where(tc => tc.TechnicianId == technicianId &&
                        tc.ExpirationDate.HasValue &&
                        tc.ExpirationDate.Value < today &&
                        !tc.IsDeleted)
            .CountAsync();
    }
}
