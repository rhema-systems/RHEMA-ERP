using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Data.Repositories;

public class ReportRepository : GenericRepository<Report>, IReportRepository
{
    public ReportRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Report>> GetReportsByTenantAsync(Guid tenantId, string? type = null, string? status = null)
    {
        var query = _context.Reports
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.Schedules.Where(s => !s.IsDeleted))
            .Include(r => r.Favorites.Where(f => !f.IsDeleted))
            .AsQueryable();

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(r => r.Type == type);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(r => r.Status == status);
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Report>> GetFavoriteReportsByUserAsync(Guid userId, Guid tenantId)
    {
        return await _context.Reports
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Where(r => r.Favorites.Any(f => f.UserId == userId && !f.IsDeleted))
            .Include(r => r.Schedules.Where(s => !s.IsDeleted))
            .Include(r => r.Favorites.Where(f => !f.IsDeleted))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<Report?> GetReportWithDetailsAsync(Guid reportId, Guid tenantId)
    {
        return await _context.Reports
            .Where(r => r.Id == reportId && r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.Schedules.Where(s => !s.IsDeleted))
            .Include(r => r.Executions.Where(e => !e.IsDeleted).OrderByDescending(e => e.ExecutedAt).Take(10))
            .Include(r => r.Favorites.Where(f => !f.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsReportFavoriteAsync(Guid reportId, Guid userId, Guid tenantId)
    {
        return await _context.UserReportFavorites
            .AnyAsync(f => f.ReportId == reportId && f.UserId == userId && f.TenantId == tenantId && !f.IsDeleted);
    }

    public async Task<IEnumerable<Report>> GetScheduledReportsAsync(Guid tenantId)
    {
        return await _context.Reports
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.IsScheduled)
            .Include(r => r.Schedules.Where(s => !s.IsDeleted && s.IsActive))
            .ToListAsync();
    }

    public async Task UpdateLastRunAsync(Guid reportId, DateTime lastRun, DateTime? nextRun = null)
    {
        var report = await _context.Reports.FindAsync(reportId);
        if (report != null)
        {
            report.LastRun = lastRun;
            report.NextRun = nextRun;
            report.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}

public class ReportScheduleRepository : GenericRepository<ReportSchedule>, IReportScheduleRepository
{
    public ReportScheduleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ReportSchedule>> GetSchedulesByReportAsync(Guid reportId, Guid tenantId)
    {
        return await _context.ReportSchedules
            .Where(s => s.ReportId == reportId && s.TenantId == tenantId && !s.IsDeleted)
            .Include(s => s.Report)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReportSchedule>> GetActiveSchedulesAsync(Guid tenantId)
    {
        return await _context.ReportSchedules
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive)
            .Include(s => s.Report)
            .OrderBy(s => s.NextExecutionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReportSchedule>> GetSchedulesDueForExecutionAsync()
    {
        var now = DateTime.UtcNow;
        return await _context.ReportSchedules
            .Where(s => !s.IsDeleted && s.IsActive && 
                       s.NextExecutionDate.HasValue && s.NextExecutionDate <= now)
            .Include(s => s.Report)
            .ToListAsync();
    }
}

public class ReportTemplateRepository : GenericRepository<ReportTemplate>, IReportTemplateRepository
{
    public ReportTemplateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ReportTemplate>> GetTemplatesByTenantAsync(Guid tenantId, string? category = null)
    {
        var query = _context.ReportTemplates
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(t => t.Category == category);
        }

        return await query
            .OrderByDescending(t => t.UsageCount)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReportTemplate>> GetPopularTemplatesAsync(Guid tenantId, int limit = 10)
    {
        return await _context.ReportTemplates
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .OrderByDescending(t => t.UsageCount)
            .ThenByDescending(t => t.LastUsed)
            .Take(limit)
            .ToListAsync();
    }

    public async Task IncrementUsageCountAsync(Guid templateId)
    {
        var template = await _context.ReportTemplates.FindAsync(templateId);
        if (template != null)
        {
            template.UsageCount++;
            template.LastUsed = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}

public class ReportExecutionRepository : GenericRepository<ReportExecution>, IReportExecutionRepository
{
    public ReportExecutionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ReportExecution>> GetExecutionsByReportAsync(Guid reportId, Guid tenantId, int limit = 50)
    {
        return await _context.ReportExecutions
            .Where(e => e.ReportId == reportId && e.TenantId == tenantId && !e.IsDeleted)
            .Include(e => e.User)
            .Include(e => e.Report)
            .OrderByDescending(e => e.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReportExecution>> GetExecutionsByUserAsync(Guid userId, Guid tenantId, int limit = 50)
    {
        return await _context.ReportExecutions
            .Where(e => e.UserId == userId && e.TenantId == tenantId && !e.IsDeleted)
            .Include(e => e.User)
            .Include(e => e.Report)
            .OrderByDescending(e => e.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReportExecution>> GetRecentExecutionsAsync(Guid tenantId, int limit = 20)
    {
        return await _context.ReportExecutions
            .Where(e => e.TenantId == tenantId && !e.IsDeleted)
            .Include(e => e.User)
            .Include(e => e.Report)
            .OrderByDescending(e => e.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<ReportExecution?> GetLatestExecutionAsync(Guid reportId, Guid tenantId)
    {
        return await _context.ReportExecutions
            .Where(e => e.ReportId == reportId && e.TenantId == tenantId && !e.IsDeleted)
            .Include(e => e.User)
            .OrderByDescending(e => e.ExecutedAt)
            .FirstOrDefaultAsync();
    }
}

public class UserReportFavoriteRepository : GenericRepository<UserReportFavorite>, IUserReportFavoriteRepository
{
    public UserReportFavoriteRepository(ApplicationDbContext context) : base(context) { }

    public async Task<UserReportFavorite?> GetFavoriteAsync(Guid reportId, Guid userId, Guid tenantId)
    {
        return await _context.UserReportFavorites
            .Where(f => f.ReportId == reportId && f.UserId == userId && f.TenantId == tenantId && !f.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<UserReportFavorite>> GetUserFavoritesAsync(Guid userId, Guid tenantId)
    {
        return await _context.UserReportFavorites
            .Where(f => f.UserId == userId && f.TenantId == tenantId && !f.IsDeleted)
            .Include(f => f.Report)
            .OrderByDescending(f => f.FavoritedAt)
            .ToListAsync();
    }

    public async Task<bool> ToggleFavoriteAsync(Guid reportId, Guid userId, Guid tenantId)
    {
        var favorite = await GetFavoriteAsync(reportId, userId, tenantId);
        
        if (favorite == null)
        {
            // Add to favorites
            favorite = new UserReportFavorite
            {
                ReportId = reportId,
                UserId = userId,
                TenantId = tenantId,
                FavoritedAt = DateTime.UtcNow,
                CreatedBy = userId.ToString()
            };
            _context.UserReportFavorites.Add(favorite);
            await _context.SaveChangesAsync();
            return true;
        }
        else
        {
            // Remove from favorites (soft delete)
            favorite.IsDeleted = true;
            favorite.DeletedAt = DateTime.UtcNow;
            favorite.DeletedBy = userId.ToString();
            await _context.SaveChangesAsync();
            return false;
        }
    }
}

public class ReportExportRepository : GenericRepository<ReportExport>, IReportExportRepository
{
    public ReportExportRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ReportExport>> GetExportsByUserAsync(Guid userId, Guid tenantId, int limit = 50)
    {
        return await _context.ReportExports
            .Where(e => e.UserId == userId && e.TenantId == tenantId && !e.IsDeleted)
            .Include(e => e.Report)
            .Include(e => e.User)
            .OrderByDescending(e => e.ExportedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReportExport>> GetExportsByReportAsync(Guid reportId, Guid tenantId, int limit = 50)
    {
        return await _context.ReportExports
            .Where(e => e.ReportId == reportId && e.TenantId == tenantId && !e.IsDeleted)
            .Include(e => e.Report)
            .Include(e => e.User)
            .OrderByDescending(e => e.ExportedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<long> GetTotalExportsSizeByUserAsync(Guid userId, Guid tenantId)
    {
        return await _context.ReportExports
            .Where(e => e.UserId == userId && e.TenantId == tenantId && !e.IsDeleted)
            .SumAsync(e => e.FileSize);
    }
}

public class ReportRoleAssignmentRepository : GenericRepository<ReportRoleAssignment>, IReportRoleAssignmentRepository
{
    public ReportRoleAssignmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ReportRoleAssignment>> GetAssignmentsByReportAsync(Guid reportId, Guid tenantId)
    {
        // Use explicit join to ensure Role data is loaded
        var assignments = await _context.ReportRoleAssignments
            .Where(rra => rra.ReportId == reportId && rra.TenantId == tenantId && !rra.IsDeleted)
            .ToListAsync();

        // Load roles separately and map them
        var roleIds = assignments.Select(a => a.RoleId).Distinct().ToList();
        var roles = await _context.Roles
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r);

        // Manually populate Role navigation property
        foreach (var assignment in assignments)
        {
            if (roles.TryGetValue(assignment.RoleId, out var role))
            {
                assignment.Role = role;
            }
        }

        return assignments.OrderBy(rra => rra.Role?.Name ?? "Unknown");
    }

    public async Task<IEnumerable<ReportRoleAssignment>> GetAssignmentsByRoleAsync(Guid roleId, Guid tenantId)
    {
        return await _context.ReportRoleAssignments
            .Where(rra => rra.RoleId == roleId && rra.TenantId == tenantId && !rra.IsDeleted)
            .Include(rra => rra.Report)
            .OrderBy(rra => rra.Report.Name)
            .ToListAsync();
    }

    public async Task<ReportRoleAssignment?> GetAssignmentAsync(Guid reportId, Guid roleId, Guid tenantId)
    {
        return await _context.ReportRoleAssignments
            .Where(rra => rra.ReportId == reportId && rra.RoleId == roleId && rra.TenantId == tenantId && !rra.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Guid>> GetAccessibleReportIdsForUserAsync(Guid userId, Guid tenantId)
    {
        // Get user roles
        var userRoleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        if (!userRoleIds.Any())
            return new List<Guid>();

        // Get reports accessible by user's roles
        return await _context.ReportRoleAssignments
            .Where(rra => userRoleIds.Contains(rra.RoleId) && 
                         rra.TenantId == tenantId && 
                         !rra.IsDeleted &&
                         rra.CanRead)
            .Select(rra => rra.ReportId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<bool> HasReportAccessAsync(Guid reportId, Guid userId, Guid tenantId, string permission)
    {
        // Get user roles
        var userRoleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        if (!userRoleIds.Any())
            return false;

        // Check if user has any role assignments for this report with the required permission
        var query = _context.ReportRoleAssignments
            .Where(rra => rra.ReportId == reportId &&
                         userRoleIds.Contains(rra.RoleId) &&
                         rra.TenantId == tenantId &&
                         !rra.IsDeleted);

        return permission.ToLower() switch
        {
            "read" => await query.AnyAsync(rra => rra.CanRead),
            "execute" => await query.AnyAsync(rra => rra.CanExecute),
            "export" => await query.AnyAsync(rra => rra.CanExport),
            "edit" => await query.AnyAsync(rra => rra.CanEdit),
            "schedule" => await query.AnyAsync(rra => rra.CanSchedule),
            _ => false
        };
    }

    public async Task<bool> RemoveAssignmentsForReportAsync(Guid reportId, Guid tenantId)
    {
        var assignments = await _context.ReportRoleAssignments
            .Where(rra => rra.ReportId == reportId && rra.TenantId == tenantId && !rra.IsDeleted)
            .ToListAsync();

        foreach (var assignment in assignments)
        {
            assignment.IsDeleted = true;
            assignment.DeletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAssignmentsForRoleAsync(Guid roleId, Guid tenantId)
    {
        var assignments = await _context.ReportRoleAssignments
            .Where(rra => rra.RoleId == roleId && rra.TenantId == tenantId && !rra.IsDeleted)
            .ToListAsync();

        foreach (var assignment in assignments)
        {
            assignment.IsDeleted = true;
            assignment.DeletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }
}
