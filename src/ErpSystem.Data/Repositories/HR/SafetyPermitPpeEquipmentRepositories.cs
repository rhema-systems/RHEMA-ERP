using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// SHE repositories — Permit-to-Work (E), PPE Management (F) and Equipment (G).
// ============================================================================

#region Permit-to-Work

public class ShePermitToWorkRepository : GenericRepository<ShePermitToWork>, IShePermitToWorkRepository
{
    public ShePermitToWorkRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<ShePermitToWork> WithListNavigations() =>
        _dbSet.Include(p => p.Location).Include(p => p.RequestedBy).Include(p => p.Contractor);

    public async Task<ShePermitToWork?> GetByNumberAsync(string permitNumber) =>
        await WithListNavigations().FirstOrDefaultAsync(p => p.PermitNumber == permitNumber && !p.IsDeleted);

    public async Task<ShePermitToWork?> GetWithFullDetailsAsync(Guid id) =>
        await _dbSet
            .Include(p => p.Location)
            .Include(p => p.RequestedBy)
            .Include(p => p.Contractor)
            .Include(p => p.IssuedBy)
            .Include(p => p.ApprovedBy)
            .Include(p => p.SuspendedBy)
            .Include(p => p.ClosedBy)
            .Include(p => p.RiskAssessment)
            .Include(p => p.AuthorisedWorkers).ThenInclude(w => w.Employee)
            .Include(p => p.Extensions).ThenInclude(x => x.ApprovedBy)
            .Include(p => p.Documents).ThenInclude(d => d.UploadedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

    public async Task<IEnumerable<ShePermitToWork>> GetAllSummaryAsync() =>
        await WithListNavigations().Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.PlannedStartDate).ToListAsync();

    public async Task<IEnumerable<ShePermitToWork>> GetByStatusAsync(ShePermitStatus status) =>
        await WithListNavigations().Where(p => p.Status == status && !p.IsDeleted)
            .OrderByDescending(p => p.PlannedStartDate).ToListAsync();

    public async Task<IEnumerable<ShePermitToWork>> GetByTypeAsync(ShePermitType type) =>
        await WithListNavigations().Where(p => p.PermitType == type && !p.IsDeleted)
            .OrderByDescending(p => p.PlannedStartDate).ToListAsync();

    public async Task<IEnumerable<ShePermitToWork>> GetActiveAsync() =>
        await WithListNavigations().Where(p => p.Status == ShePermitStatus.Active && !p.IsDeleted)
            .OrderBy(p => p.PlannedEndDate).ToListAsync();

    public async Task<IEnumerable<ShePermitToWork>> GetByContractorAsync(Guid contractorId) =>
        await WithListNavigations().Where(p => p.ContractorId == contractorId && !p.IsDeleted)
            .OrderByDescending(p => p.PlannedStartDate).ToListAsync();

    public async Task<IEnumerable<ShePermitToWork>> GetByRequestorAsync(Guid requestedById) =>
        await WithListNavigations().Where(p => p.RequestedById == requestedById && !p.IsDeleted)
            .OrderByDescending(p => p.PlannedStartDate).ToListAsync();

    public async Task<IEnumerable<ShePermitToWork>> GetExpiringAsync(int daysAhead = 1)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithListNavigations()
            .Where(p => !p.IsDeleted
                     && (p.Status == ShePermitStatus.Active || p.Status == ShePermitStatus.Approved)
                     && p.PlannedEndDate <= cutoff)
            .OrderBy(p => p.PlannedEndDate).ToListAsync();
    }

    public async Task<IEnumerable<ShePermitToWork>> GetSuspendedAsync() =>
        await WithListNavigations().Where(p => p.IsSuspended && !p.IsDeleted)
            .OrderByDescending(p => p.SuspendedDate).ToListAsync();
}

#endregion

#region PPE

public class PpeTypeRepository : GenericRepository<PpeType>, IPpeTypeRepository
{
    public PpeTypeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<PpeType?> GetByCodeAsync(string code) =>
        await _dbSet.FirstOrDefaultAsync(t => t.Code == code && !t.IsDeleted);

    public async Task<IEnumerable<PpeType>> GetActiveAsync() =>
        await _dbSet.Where(t => t.IsActive && !t.IsDeleted).OrderBy(t => t.Name).ToListAsync();

    public async Task<IEnumerable<PpeType>> GetByCategoryAsync(ShePpeCategory category) =>
        await _dbSet.Where(t => t.Category == category && !t.IsDeleted).OrderBy(t => t.Name).ToListAsync();
}

public class PpeInventoryRepository : GenericRepository<PpeInventory>, IPpeInventoryRepository
{
    public PpeInventoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<PpeInventory?> GetByItemCodeAsync(string itemCode) =>
        await _dbSet.Include(i => i.PpeType).FirstOrDefaultAsync(i => i.ItemCode == itemCode && !i.IsDeleted);

    public async Task<IEnumerable<PpeInventory>> GetByPpeTypeAsync(Guid ppeTypeId) =>
        await _dbSet.Include(i => i.PpeType)
            .Where(i => i.PpeTypeId == ppeTypeId && !i.IsDeleted).OrderBy(i => i.ItemCode).ToListAsync();

    public async Task<IEnumerable<PpeInventory>> GetBelowReorderLevelAsync() =>
        await _dbSet.Include(i => i.PpeType)
            .Where(i => !i.IsDeleted && i.QuantityInStock <= i.ReorderLevel)
            .OrderBy(i => i.QuantityInStock).ToListAsync();
}

public class PpeIssuanceRepository : GenericRepository<PpeIssuance>, IPpeIssuanceRepository
{
    public PpeIssuanceRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<PpeIssuance> WithNavigations() =>
        _dbSet.Include(i => i.Employee).Include(i => i.PpeType).Include(i => i.IssuedBy);

    public async Task<IEnumerable<PpeIssuance>> GetByEmployeeAsync(Guid employeeId) =>
        await WithNavigations().Where(i => i.EmployeeId == employeeId && !i.IsDeleted)
            .OrderByDescending(i => i.IssueDate).ToListAsync();

    public async Task<IEnumerable<PpeIssuance>> GetByPpeTypeAsync(Guid ppeTypeId) =>
        await WithNavigations().Where(i => i.PpeTypeId == ppeTypeId && !i.IsDeleted)
            .OrderByDescending(i => i.IssueDate).ToListAsync();

    public async Task<IEnumerable<PpeIssuance>> GetOutstandingAsync() =>
        await WithNavigations().Where(i => !i.IsReturned && !i.IsDeleted)
            .OrderBy(i => i.ExpectedReturnDate).ToListAsync();

    public async Task<IEnumerable<PpeIssuance>> GetExpiringAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithNavigations()
            .Where(i => !i.IsReturned && !i.IsDeleted && i.ExpiryDate != null && i.ExpiryDate <= cutoff)
            .OrderBy(i => i.ExpiryDate).ToListAsync();
    }

    public async Task<IEnumerable<PpeIssuance>> GetOverdueReturnsAsync()
    {
        var today = DateTime.UtcNow;
        return await WithNavigations()
            .Where(i => !i.IsReturned && !i.IsDeleted && i.ExpectedReturnDate != null && i.ExpectedReturnDate < today)
            .OrderBy(i => i.ExpectedReturnDate).ToListAsync();
    }
}

public class JobRolePpeRequirementRepository : GenericRepository<JobRolePpeRequirement>, IJobRolePpeRequirementRepository
{
    public JobRolePpeRequirementRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobRolePpeRequirement>> GetByJobRoleAsync(string jobRoleCode) =>
        await _dbSet.Include(r => r.PpeType)
            .Where(r => r.JobRoleCode == jobRoleCode && !r.IsDeleted).ToListAsync();

    public async Task<IEnumerable<JobRolePpeRequirement>> GetByPpeTypeAsync(Guid ppeTypeId) =>
        await _dbSet.Where(r => r.PpeTypeId == ppeTypeId && !r.IsDeleted)
            .OrderBy(r => r.JobRoleName).ToListAsync();
}

#endregion

#region Safety Equipment

public class SafetyEquipmentRepository : GenericRepository<SafetyEquipment>, ISafetyEquipmentRepository
{
    public SafetyEquipmentRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SafetyEquipment> WithListNavigations() =>
        _dbSet.Include(e => e.Location).Include(e => e.OrganizationUnit).Include(e => e.ResponsiblePerson);

    public async Task<SafetyEquipment?> GetByNumberAsync(string equipmentNumber) =>
        await WithListNavigations().FirstOrDefaultAsync(e => e.EquipmentNumber == equipmentNumber && !e.IsDeleted);

    public async Task<SafetyEquipment?> GetWithFullDetailsAsync(Guid id) =>
        await _dbSet
            .Include(e => e.Location)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.ResponsiblePerson)
            .Include(e => e.Inspections).ThenInclude(i => i.InspectedBy)
            .Include(e => e.Inspections).ThenInclude(i => i.InspectionActions).ThenInclude(a => a.CorrectiveActionTemplate)
            .Include(e => e.MaintenanceRecords)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

    public async Task<IEnumerable<SafetyEquipment>> GetAllSummaryAsync() =>
        await WithListNavigations().Where(e => !e.IsDeleted)
            .OrderBy(e => e.Name).ToListAsync();

    public async Task<IEnumerable<SafetyEquipment>> GetByStatusAsync(SheSafetyEquipmentStatus status) =>
        await WithListNavigations().Where(e => e.Status == status && !e.IsDeleted)
            .OrderBy(e => e.Name).ToListAsync();

    public async Task<IEnumerable<SafetyEquipment>> GetByTypeAsync(SheSafetyEquipmentType type) =>
        await WithListNavigations().Where(e => e.Type == type && !e.IsDeleted)
            .OrderBy(e => e.Name).ToListAsync();

    public async Task<IEnumerable<SafetyEquipment>> GetByLocationAsync(Guid locationId) =>
        await WithListNavigations().Where(e => e.LocationId == locationId && !e.IsDeleted)
            .OrderBy(e => e.Name).ToListAsync();

    public async Task<IEnumerable<SafetyEquipment>> GetDueForInspectionAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithListNavigations()
            .Where(e => !e.IsDeleted && e.RequiresRegularInspection
                     && e.NextInspectionDueDate != null && e.NextInspectionDueDate <= cutoff)
            .OrderBy(e => e.NextInspectionDueDate).ToListAsync();
    }

    public async Task<IEnumerable<SafetyEquipment>> GetDueForMaintenanceAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithListNavigations()
            .Where(e => !e.IsDeleted && e.NextMaintenanceDueDate != null && e.NextMaintenanceDueDate <= cutoff)
            .OrderBy(e => e.NextMaintenanceDueDate).ToListAsync();
    }

    public async Task<IEnumerable<SafetyEquipment>> GetExpiringCertificationAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithListNavigations()
            .Where(e => !e.IsDeleted && e.RequiresCertification
                     && e.CertificationExpiryDate != null && e.CertificationExpiryDate <= cutoff)
            .OrderBy(e => e.CertificationExpiryDate).ToListAsync();
    }

    public async Task<IEnumerable<SafetyEquipment>> GetOutOfServiceAsync() =>
        await WithListNavigations()
            .Where(e => !e.IsDeleted && e.Status == SheSafetyEquipmentStatus.OutOfService)
            .OrderBy(e => e.Name).ToListAsync();

    public async Task<string> GetNextEquipmentNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"SEQ-{year}-";
        var last = await _dbSet.IgnoreQueryFilters()
            .Where(e => e.EquipmentNumber.StartsWith(prefix))
            .OrderByDescending(e => e.EquipmentNumber)
            .Select(e => e.EquipmentNumber)
            .FirstOrDefaultAsync();

        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n))
            next = n + 1;
        return $"{prefix}{next:D4}";
    }
}

public class SafetyEquipmentInspectionRepository : GenericRepository<SafetyEquipmentInspection>, ISafetyEquipmentInspectionRepository
{
    public SafetyEquipmentInspectionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SafetyEquipmentInspection>> GetByEquipmentIdAsync(Guid equipmentId) =>
        await _dbSet.Include(i => i.InspectedBy)
            .Where(i => i.EquipmentId == equipmentId && !i.IsDeleted)
            .OrderByDescending(i => i.InspectionDate).ToListAsync();

    public async Task<IEnumerable<SafetyEquipmentInspection>> GetByResultAsync(SheInspectionResult result) =>
        await _dbSet.Include(i => i.Equipment).Include(i => i.InspectedBy)
            .Where(i => i.Result == result && !i.IsDeleted)
            .OrderByDescending(i => i.InspectionDate).ToListAsync();
}

#endregion
