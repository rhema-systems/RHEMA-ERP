using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

#region Asset Type Repositories

public class AssetTypeRepository : GenericRepository<AssetType>, IAssetTypeRepository
{
    public AssetTypeRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<AssetType> GetByIdAsync(Guid id)
    {
        return await _context.Set<AssetType>()
            .Include(at => at.AssetTypeAttributes)
            .FirstOrDefaultAsync(at => at.Id == id && !at.IsDeleted);
    }

    public async Task<IEnumerable<AssetType>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<AssetType>()
            .Include(at => at.AssetTypeAttributes)
            .Where(at => at.TenantId == tenantId && !at.IsDeleted)
            .OrderBy(at => at.Name)
            .ToListAsync();
    }

    public async Task<AssetType?> GetWithAttributesAsync(Guid id)
    {
        return await _context.Set<AssetType>()
            .Include(at => at.AssetTypeAttributes)
            .FirstOrDefaultAsync(at => at.Id == id && !at.IsDeleted);
    }

    public async Task<AssetType?> GetByNameAsync(Guid tenantId, string name)
    {
        return await _context.Set<AssetType>()
            .FirstOrDefaultAsync(at => at.TenantId == tenantId && at.Name == name && !at.IsDeleted);
    }

    public async Task<int> GetAssetCountByTypeAsync(Guid assetTypeId)
    {
        return await _context.Set<CompanyAsset>()
            .CountAsync(ca => ca.AssetTypeId == assetTypeId && !ca.IsDeleted);
    }
}

public class AssetTypeAttributeRepository : GenericRepository<AssetTypeAttribute>, IAssetTypeAttributeRepository
{
    public AssetTypeAttributeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<AssetTypeAttribute?> GetWithTypeAsync(Guid id)
    {
        return await _context.Set<AssetTypeAttribute>()
            .Include(ata => ata.AssetType)
            .FirstOrDefaultAsync(ata => ata.Id == id && !ata.IsDeleted);
    }

    public async Task<IEnumerable<AssetTypeAttribute>> GetByAssetTypeIdAsync(Guid assetTypeId)
    {
        return await _context.Set<AssetTypeAttribute>()
            .Include(ata => ata.AssetType)
            .Where(ata => ata.AssetTypeId == assetTypeId && !ata.IsDeleted)
            .OrderBy(ata => ata.AttributeName)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTypeAttribute>> GetRequiredAttributesAsync(Guid assetTypeId)
    {
        return await _context.Set<AssetTypeAttribute>()
            .Where(ata => ata.AssetTypeId == assetTypeId && ata.IsRequired && !ata.IsDeleted)
            .ToListAsync();
    }
}

#endregion

#region Company Asset Repositories

public class CompanyAssetRepository : GenericRepository<CompanyAsset>, ICompanyAssetRepository
{
    public CompanyAssetRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<CompanyAsset>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<CompanyAsset>()
            .Include(ca => ca.AssetType)
            .Include(ca => ca.Location)
            .Include(ca => ca.Unit)
            .Include(ca => ca.CurrentAssignedTo)
            .Where(ca => ca.TenantId == tenantId && !ca.IsDeleted)
            .OrderBy(ca => ca.AssetName)
            .ToListAsync();
    }

    public async Task<CompanyAsset?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<CompanyAsset>()
            .Include(ca => ca.AssetType)
            .Include(ca => ca.Location)
            .Include(ca => ca.Unit)
            .Include(ca => ca.CurrentAssignedTo)
            .Include(ca => ca.AssetAttributeValues).ThenInclude(av => av.AssetTypeAttribute)
            .Include(ca => ca.AssignmentHistory.OrderByDescending(a => a.AssignmentDate).Take(10))
            .Include(ca => ca.MaintenanceRecords.OrderByDescending(m => m.MaintenanceDate).Take(10))
            .Include(ca => ca.Attachments)
            .FirstOrDefaultAsync(ca => ca.Id == id && !ca.IsDeleted);
    }

    public async Task<CompanyAsset?> GetByAssetNumberAsync(Guid tenantId, string assetNumber)
    {
        return await _context.Set<CompanyAsset>()
            .FirstOrDefaultAsync(ca => ca.TenantId == tenantId && ca.AssetNumber == assetNumber && !ca.IsDeleted);
    }

    public async Task<CompanyAsset?> GetByAssetTagAsync(Guid tenantId, string assetTag)
    {
        return await _context.Set<CompanyAsset>()
            .FirstOrDefaultAsync(ca => ca.TenantId == tenantId && ca.AssetTag == assetTag && !ca.IsDeleted);
    }

    public async Task<CompanyAsset?> GetBySerialNumberAsync(Guid tenantId, string serialNumber)
    {
        return await _context.Set<CompanyAsset>()
            .FirstOrDefaultAsync(ca => ca.TenantId == tenantId && ca.SerialNumber == serialNumber && !ca.IsDeleted);
    }

    public async Task<IEnumerable<CompanyAsset>> GetByAssetTypeAsync(Guid assetTypeId)
    {
        return await _context.Set<CompanyAsset>()
            .Include(ca => ca.AssetType)
            .Include(ca => ca.Location)
            .Include(ca => ca.Unit)
            .Include(ca => ca.CurrentAssignedTo)
            .Where(ca => ca.AssetTypeId == assetTypeId && !ca.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyAsset>> GetByStatusAsync(Guid tenantId, CompanyAssetStatus status)
    {
        return await _context.Set<CompanyAsset>()
            .Include(ca => ca.AssetType)
            .Include(ca => ca.Location)
            .Include(ca => ca.Unit)
            .Include(ca => ca.CurrentAssignedTo)
            .Where(ca => ca.TenantId == tenantId && ca.Status == status && !ca.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyAsset>> GetByLocationAsync(Guid locationId)
    {
        return await _context.Set<CompanyAsset>()
            .Include(ca => ca.AssetType)
            .Include(ca => ca.Location)
            .Include(ca => ca.Unit)
            .Include(ca => ca.CurrentAssignedTo)
            .Where(ca => ca.LocationId == locationId && !ca.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyAsset>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _context.Set<CompanyAsset>()
            .Include(ca => ca.AssetType)
            .Include(ca => ca.Location)
            .Include(ca => ca.Unit)
            .Include(ca => ca.CurrentAssignedTo)
            .Where(ca => ca.CurrentAssignedToId == employeeId && !ca.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyAsset>> GetAvailableForAssignmentAsync(Guid tenantId)
    {
        return await _context.Set<CompanyAsset>()
            .Include(ca => ca.AssetType)
            .Include(ca => ca.Location)
            .Include(ca => ca.Unit)
            .Include(ca => ca.CurrentAssignedTo)
            .Where(ca => ca.TenantId == tenantId 
                && ca.IsAssignable 
                && !ca.IsCurrentlyAssigned 
                && ca.Status == CompanyAssetStatus.Available
                && !ca.IsDeleted)
            .ToListAsync();
    }

    /// <summary>
    /// Assets whose next maintenance falls on or before <paramref name="onOrBefore"/>. Area 16
    /// slice 9 gave this an explicit horizon date instead of computing one from the clock, so the
    /// service can offer an <c>asOf</c> seam and the reminder sweep and the read agree by
    /// construction rather than by coincidence.
    /// </summary>
    public async Task<IEnumerable<CompanyAsset>> GetDueForMaintenanceAsync(Guid tenantId, DateOnly onOrBefore)
    {
        return await _context.Set<CompanyAsset>()
            .Include(ca => ca.AssetType)
            .Include(ca => ca.Location)
            .Include(ca => ca.Unit)
            .Include(ca => ca.CurrentAssignedTo)
            .Where(ca => ca.TenantId == tenantId 
                && ca.RequiresRegularMaintenance 
                && ca.NextMaintenanceDate != null 
                && ca.NextMaintenanceDate <= onOrBefore
                && !ca.IsDeleted)
            .OrderBy(ca => ca.NextMaintenanceDate)
            .ToListAsync();
    }

    /// <summary>
    /// Assets that require regular maintenance and have <b>no next date at all</b> — area 16 slice 9.
    /// </summary>
    /// <remarks>
    /// These are invisible to every other maintenance read, because all of them filter on
    /// <c>NextMaintenanceDate != null</c>. An asset flagged as needing regular servicing that has
    /// never been scheduled is precisely the one a monitoring feature must not lose, and before this
    /// slice it was the one row shape guaranteed never to appear anywhere.
    /// </remarks>
    public async Task<IEnumerable<CompanyAsset>> GetUnscheduledMaintenanceAsync(Guid tenantId)
    {
        return await _context.Set<CompanyAsset>()
            .Include(ca => ca.AssetType)
            .Include(ca => ca.Location)
            .Include(ca => ca.Unit)
            .Include(ca => ca.CurrentAssignedTo)
            .Where(ca => ca.TenantId == tenantId
                && ca.RequiresRegularMaintenance
                && ca.NextMaintenanceDate == null
                && ca.Status != CompanyAssetStatus.Disposed
                && !ca.IsDeleted)
            .OrderBy(ca => ca.AssetNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyAsset>> GetWarrantyExpiringAsync(Guid tenantId, int daysAhead = 30)
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
        return await _context.Set<CompanyAsset>()
            .Include(ca => ca.AssetType)
            .Include(ca => ca.Location)
            .Include(ca => ca.Unit)
            .Include(ca => ca.CurrentAssignedTo)
            .Where(ca => ca.TenantId == tenantId 
                && ca.HasWarranty 
                && ca.WarrantyEndDate != null 
                && ca.WarrantyEndDate <= futureDate
                && ca.WarrantyEndDate >= DateOnly.FromDateTime(DateTime.UtcNow)
                && !ca.IsDeleted)
            .ToListAsync();
    }
}

public class AssetAttributeValueRepository : GenericRepository<AssetAttributeValue>, IAssetAttributeValueRepository
{
    public AssetAttributeValueRepository(ApplicationDbContext context) : base(context) { }

    public async Task<AssetAttributeValue?> GetWithAttributeAsync(Guid id)
    {
        return await _context.Set<AssetAttributeValue>()
            .Include(av => av.AssetTypeAttribute)
            .FirstOrDefaultAsync(av => av.Id == id && !av.IsDeleted);
    }

    public async Task<IEnumerable<AssetAttributeValue>> GetByAssetIdAsync(Guid assetId)
    {
        return await _context.Set<AssetAttributeValue>()
            .Include(av => av.AssetTypeAttribute)
            .Where(av => av.AssetId == assetId && !av.IsDeleted)
            .ToListAsync();
    }

    public async Task<AssetAttributeValue?> GetByAssetAndAttributeAsync(Guid assetId, Guid attributeId)
    {
        return await _context.Set<AssetAttributeValue>()
            .FirstOrDefaultAsync(av => av.AssetId == assetId && av.AssetTypeAttributeId == attributeId && !av.IsDeleted);
    }

    public async Task DeleteByAssetIdAsync(Guid assetId)
    {
        var values = await _context.Set<AssetAttributeValue>()
            .Where(av => av.AssetId == assetId)
            .ToListAsync();
        
        foreach (var value in values)
        {
            value.IsDeleted = true;
            value.DeletedAt = DateTime.UtcNow;
        }
    }
}

#endregion

#region Asset Assignment Repositories

public class AssetAssignmentRepository : GenericRepository<AssetAssignment>, IAssetAssignmentRepository
{
    public AssetAssignmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AssetAssignment>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<AssetAssignment>()
            .Include(aa => aa.Asset).ThenInclude(a => a.AssetType)
            .Include(aa => aa.Employee)
            .Where(aa => aa.TenantId == tenantId && !aa.IsDeleted)
            .OrderByDescending(aa => aa.AssignmentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetAssignment>> GetByRequisitionIdAsync(Guid requisitionId)
    {
        return await _context.Set<AssetAssignment>()
            .Include(aa => aa.Asset).ThenInclude(a => a.AssetType)
            .Include(aa => aa.Employee)
            .Where(aa => aa.RequisitionId == requisitionId && !aa.IsDeleted)
            .OrderBy(aa => aa.AssignmentDate)
            .ToListAsync();
    }

    public async Task<AssetAssignment?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<AssetAssignment>()
            .Include(aa => aa.Asset).ThenInclude(a => a.AssetType)
            .Include(aa => aa.Employee)
            .Include(aa => aa.Requisition)
            // ⚠ In with the mapping that reads it, not after somebody notices the field is null.
            // `transferNumber` without this .Include is the sixth instance of that shape here.
            .Include(aa => aa.Transfer)
            .Include(aa => aa.ApprovedBy)
            .Include(aa => aa.ReturnedTo)
            .Include(aa => aa.TermsDocumentSentBy)
            .FirstOrDefaultAsync(aa => aa.Id == id && !aa.IsDeleted);
    }

    public async Task<AssetAssignment?> GetByAssignmentNumberAsync(Guid tenantId, string assignmentNumber)
    {
        return await _context.Set<AssetAssignment>()
            .FirstOrDefaultAsync(aa => aa.TenantId == tenantId && aa.AssignmentNumber == assignmentNumber && !aa.IsDeleted);
    }

    public async Task<IEnumerable<AssetAssignment>> GetByAssetIdAsync(Guid assetId)
    {
        return await _context.Set<AssetAssignment>()
            .Include(aa => aa.Asset).ThenInclude(a => a.AssetType)
            .Include(aa => aa.Employee)
            .Where(aa => aa.AssetId == assetId && !aa.IsDeleted)
            .OrderByDescending(aa => aa.AssignmentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetAssignment>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _context.Set<AssetAssignment>()
            .Include(aa => aa.Asset).ThenInclude(a => a.AssetType)
            .Include(aa => aa.Employee)
            .Where(aa => aa.EmployeeId == employeeId && !aa.IsDeleted)
            .OrderByDescending(aa => aa.AssignmentDate)
            .ToListAsync();
    }

    public async Task<AssetAssignment?> GetActiveAssignmentForAssetAsync(Guid assetId)
    {
        // ⚠ This is the ONE list-style query whose result is mapped by `ToDto`, not `ToSummaryDto`,
        // so it must load everything the full DTO reads. With `Employee` alone, `assets/assignments/
        // asset/{id}/current` answered 200 with a blank asset name and number, no requisition or
        // transfer number, and no approver, returned-to or terms-document actor — the same record
        // the by-id read renders in full. It carries the same graph as `GetWithDetailsAsync` for
        // exactly that reason.
        return await _context.Set<AssetAssignment>()
            .Include(aa => aa.Asset).ThenInclude(a => a.AssetType)
            .Include(aa => aa.Employee)
            .Include(aa => aa.Requisition)
            .Include(aa => aa.Transfer)
            .Include(aa => aa.ApprovedBy)
            .Include(aa => aa.ReturnedTo)
            .Include(aa => aa.TermsDocumentSentBy)
            .FirstOrDefaultAsync(aa => aa.AssetId == assetId && aa.Status == AssignmentStatus.Active && !aa.IsDeleted);
    }

    public async Task<IEnumerable<AssetAssignment>> GetActiveAssignmentsForEmployeeAsync(Guid employeeId)
    {
        return await _context.Set<AssetAssignment>()
            .Include(aa => aa.Asset).ThenInclude(a => a.AssetType)
            .Include(aa => aa.Employee)
            .Where(aa => aa.EmployeeId == employeeId && aa.Status == AssignmentStatus.Active && !aa.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetAssignment>> GetOverdueAssignmentsAsync(Guid tenantId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _context.Set<AssetAssignment>()
            .Include(aa => aa.Asset).ThenInclude(a => a.AssetType)
            .Include(aa => aa.Employee)
            .Where(aa => aa.TenantId == tenantId 
                && aa.Status == AssignmentStatus.Active 
                && aa.ExpectedReturnDate != null 
                && aa.ExpectedReturnDate < today
                && !aa.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetAssignment>> GetRentalArrangementsAsync(
        Guid tenantId, DateOnly periodStart, DateOnly periodEnd)
    {
        return await _context.Set<AssetAssignment>()
            .Include(aa => aa.Asset).ThenInclude(a => a.AssetType)
            .Include(aa => aa.Employee)
            .Where(aa => aa.TenantId == tenantId
                && !aa.IsDeleted
                && aa.RentalFrequency != null
                // The window overlaps the period. A null start means "from the beginning" and a
                // null end means "still running", so each side is open unless it says otherwise.
                && (aa.RentalEffectiveFrom == null || aa.RentalEffectiveFrom <= periodEnd)
                && (aa.RentalEffectiveTo == null || aa.RentalEffectiveTo >= periodStart))
            .OrderBy(aa => aa.EmployeeId)
            .ThenBy(aa => aa.RentalEffectiveFrom)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetAssignment>> GetByStatusAsync(Guid tenantId, AssignmentStatus status)
    {
        return await _context.Set<AssetAssignment>()
            .Include(aa => aa.Asset).ThenInclude(a => a.AssetType)
            .Include(aa => aa.Employee)
            .Where(aa => aa.TenantId == tenantId && aa.Status == status && !aa.IsDeleted)
            .ToListAsync();
    }
}

#endregion

#region Asset Maintenance Repositories

public class AssetMaintenanceRepository : GenericRepository<AssetMaintenance>, IAssetMaintenanceRepository
{
    public AssetMaintenanceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AssetMaintenance>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<AssetMaintenance>()
            .Include(am => am.Asset)
            .Include(am => am.PerformedBy)
            .Where(am => am.TenantId == tenantId && !am.IsDeleted)
            .OrderByDescending(am => am.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<AssetMaintenance?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<AssetMaintenance>()
            .Include(am => am.Asset).ThenInclude(a => a.AssetType)
            .Include(am => am.PerformedBy)
            .FirstOrDefaultAsync(am => am.Id == id && !am.IsDeleted);
    }

    public async Task<AssetMaintenance?> GetByMaintenanceNumberAsync(Guid tenantId, string maintenanceNumber)
    {
        return await _context.Set<AssetMaintenance>()
            .FirstOrDefaultAsync(am => am.TenantId == tenantId && am.MaintenanceNumber == maintenanceNumber && !am.IsDeleted);
    }

    public async Task<IEnumerable<AssetMaintenance>> GetByAssetIdAsync(Guid assetId)
    {
        return await _context.Set<AssetMaintenance>()
            // ⚠ D-dd, area 16 slice 9. Without this Include every row of this list answered with a
            // blank asset name while the by-id read filled it — the seventh time in this area that a
            // mapping and the Include that feeds it were changed apart. The assertion that catches
            // it either way round is "the list read and the by-id read agree".
            .Include(am => am.Asset)
            .Include(am => am.PerformedBy)
            .Where(am => am.AssetId == assetId && !am.IsDeleted)
            .OrderByDescending(am => am.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetMaintenance>> GetByStatusAsync(Guid tenantId, MaintenanceStatus status)
    {
        return await _context.Set<AssetMaintenance>()
            .Include(am => am.Asset)
            .Where(am => am.TenantId == tenantId && am.Status == status && !am.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetMaintenance>> GetScheduledMaintenanceAsync(Guid tenantId, DateTime from, DateTime to)
    {
        return await _context.Set<AssetMaintenance>()
            .Include(am => am.Asset)
            .Where(am => am.TenantId == tenantId 
                && am.MaintenanceDate >= from 
                && am.MaintenanceDate <= to
                && !am.IsDeleted)
            .OrderBy(am => am.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<AssetMaintenance?> GetLatestMaintenanceForAssetAsync(Guid assetId)
    {
        return await _context.Set<AssetMaintenance>()
            .Where(am => am.AssetId == assetId && am.Status == MaintenanceStatus.Completed && !am.IsDeleted)
            .OrderByDescending(am => am.MaintenanceDate)
            .FirstOrDefaultAsync();
    }
}

#endregion

#region Asset Image Repositories

public class AssetImageRepository : GenericRepository<AssetImage>, IAssetImageRepository
{
    public AssetImageRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AssetImage>> GetByAssetIdAsync(Guid assetId)
    {
        return await _context.Set<AssetImage>()
            .Where(ai => ai.AssetId == assetId && !ai.IsDeleted)
            .OrderByDescending(ai => ai.UploadDate)
            .ToListAsync();
    }
}

#endregion

#region Asset Attachment Repositories

public class AssetAttachmentRepository : GenericRepository<AssetAttachment>, IAssetAttachmentRepository
{
    public AssetAttachmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AssetAttachment>> GetByAssetIdAsync(Guid assetId)
    {
        return await _context.Set<AssetAttachment>()
            .Where(aa => aa.AssetId == assetId && !aa.IsDeleted)
            .OrderByDescending(aa => aa.UploadDate)
            .ToListAsync();
    }

    public async Task DeleteByAssetIdAsync(Guid assetId)
    {
        var attachments = await _context.Set<AssetAttachment>()
            .Where(aa => aa.AssetId == assetId)
            .ToListAsync();
        
        foreach (var attachment in attachments)
        {
            attachment.IsDeleted = true;
            attachment.DeletedAt = DateTime.UtcNow;
        }
    }
}

#endregion

#region Asset Requisition Repositories

public class AssetRequisitionRepository : GenericRepository<AssetRequisition>, IAssetRequisitionRepository
{
    public AssetRequisitionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AssetRequisition>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<AssetRequisition>()
            .Include(ar => ar.RequestedBy)
            .Include(ar => ar.AssetType)
            .Include(ar => ar.BeneficiaryEmployee)
            .Where(ar => ar.TenantId == tenantId && !ar.IsDeleted)
            .OrderByDescending(ar => ar.RequestDate)
            .ToListAsync();
    }

    public async Task<AssetRequisition?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<AssetRequisition>()
            .Include(ar => ar.RequestedBy)
            .Include(ar => ar.AssetType)
            .Include(ar => ar.BeneficiaryEmployee)
            .Include(ar => ar.ApprovedBy)
            .Include(ar => ar.FulfilledBy)
            .FirstOrDefaultAsync(ar => ar.Id == id && !ar.IsDeleted);
    }

    public async Task<AssetRequisition?> GetByRequisitionNumberAsync(Guid tenantId, string requisitionNumber)
    {
        return await _context.Set<AssetRequisition>()
            .FirstOrDefaultAsync(ar => ar.TenantId == tenantId && ar.RequisitionNumber == requisitionNumber && !ar.IsDeleted);
    }

    public async Task<IEnumerable<AssetRequisition>> GetByRequestedByIdAsync(Guid employeeId)
    {
        return await _context.Set<AssetRequisition>()
            .Include(ar => ar.AssetType)
            .Include(ar => ar.RequestedBy)
            .Include(ar => ar.BeneficiaryEmployee)
            .Where(ar => ar.RequestedById == employeeId && !ar.IsDeleted)
            .OrderByDescending(ar => ar.RequestDate)
            .ToListAsync();
    }

    /// <summary>
    /// Every requisition an employee is a party to — raised BY them or FOR them (AST-6b).
    /// </summary>
    /// <remarks>
    /// <c>GetByRequestedByIdAsync</c> matches <c>RequestedById</c> alone, which is correct for the
    /// question it asks and wrong for the portal's. Once a manager or HR may raise a request on
    /// somebody's behalf, "my requests" filtered on the requester hides from an employee the very
    /// requisitions that exist to give them something. Both actor columns, one list.
    /// </remarks>
    public async Task<IEnumerable<AssetRequisition>> GetForEmployeeAsync(Guid employeeId)
    {
        return await _context.Set<AssetRequisition>()
            .Include(ar => ar.AssetType)
            .Include(ar => ar.RequestedBy)
            .Include(ar => ar.BeneficiaryEmployee)
            .Where(ar => (ar.RequestedById == employeeId || ar.BeneficiaryEmployeeId == employeeId)
                && !ar.IsDeleted)
            .OrderByDescending(ar => ar.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetRequisition>> GetByStatusAsync(Guid tenantId, AssetRequisitionStatus status)
    {
        return await _context.Set<AssetRequisition>()
            .Include(ar => ar.RequestedBy)
            .Include(ar => ar.AssetType)
            .Include(ar => ar.BeneficiaryEmployee)
            .Where(ar => ar.TenantId == tenantId && ar.Status == status && !ar.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetRequisition>> GetPendingApprovalsAsync(Guid tenantId)
    {
        return await _context.Set<AssetRequisition>()
            .Include(ar => ar.RequestedBy)
            .Include(ar => ar.AssetType)
            .Include(ar => ar.BeneficiaryEmployee)
            .Where(ar => ar.TenantId == tenantId 
                && (ar.Status == AssetRequisitionStatus.Submitted || ar.Status == AssetRequisitionStatus.UnderReview)
                && !ar.IsDeleted)
            .OrderBy(ar => ar.RequestDate)
            .ToListAsync();
    }
}

#endregion

#region Asset Transfer Repositories

public class AssetTransferRepository : GenericRepository<AssetTransfer>, IAssetTransferRepository
{
    public AssetTransferRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AssetTransfer>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<AssetTransfer>()
            .Include(at => at.Asset)
            .Include(at => at.InitiatedBy)
            .Include(at => at.FromEmployee)
            .Include(at => at.ToEmployee)
            .Include(at => at.FromLocation)
            .Include(at => at.ToLocation)
            .Include(at => at.FromUnit)
            .Include(at => at.ToUnit)
            .Where(at => at.TenantId == tenantId && !at.IsDeleted)
            .OrderByDescending(at => at.TransferDate)
            .ToListAsync();
    }

    public async Task<AssetTransfer?> GetWithDetailsAsync(Guid id)
    {
        return await _context.Set<AssetTransfer>()
            .Include(at => at.Asset).ThenInclude(a => a.AssetType)
            .Include(at => at.InitiatedBy)
            .Include(at => at.ApprovedBy)
            .Include(at => at.FromEmployee)
            .Include(at => at.ToEmployee)
            .Include(at => at.FromLocation)
            .Include(at => at.ToLocation)
            .Include(at => at.FromUnit)
            .Include(at => at.ToUnit)
            .FirstOrDefaultAsync(at => at.Id == id && !at.IsDeleted);
    }

    public async Task<AssetTransfer?> GetByTransferNumberAsync(Guid tenantId, string transferNumber)
    {
        return await _context.Set<AssetTransfer>()
            .FirstOrDefaultAsync(at => at.TenantId == tenantId && at.TransferNumber == transferNumber && !at.IsDeleted);
    }

    // ⚠ The three reads below load the SAME graph as GetByTenantAsync, and they have to.
    // AssetTransferSummaryDto reads the asset's name, and then reads From/To off the employee, the
    // location or the unit depending on the transfer's TYPE — so a read that includes only the
    // employees returns a blank `fromName`/`toName` for every location or unit transfer, and a read
    // that omits the asset returns a blank `assetName` everywhere. That is the uneven-.Include
    // family this area has now met five times (D-n, D-o, D-o(b), requisitionNumber in slice 3):
    // a mapping and the read that feeds it are ONE change.
    public async Task<IEnumerable<AssetTransfer>> GetByAssetIdAsync(Guid assetId)
    {
        return await _context.Set<AssetTransfer>()
            .Include(at => at.Asset)
            .Include(at => at.InitiatedBy)
            .Include(at => at.FromEmployee)
            .Include(at => at.ToEmployee)
            .Include(at => at.FromLocation)
            .Include(at => at.ToLocation)
            .Include(at => at.FromUnit)
            .Include(at => at.ToUnit)
            .Where(at => at.AssetId == assetId && !at.IsDeleted)
            .OrderByDescending(at => at.TransferDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTransfer>> GetByStatusAsync(Guid tenantId, HRAssetTransferStatus status)
    {
        return await _context.Set<AssetTransfer>()
            .Include(at => at.Asset)
            .Include(at => at.InitiatedBy)
            .Include(at => at.FromEmployee)
            .Include(at => at.ToEmployee)
            .Include(at => at.FromLocation)
            .Include(at => at.ToLocation)
            .Include(at => at.FromUnit)
            .Include(at => at.ToUnit)
            .Where(at => at.TenantId == tenantId && at.Status == status && !at.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTransfer>> GetPendingTransfersAsync(Guid tenantId)
    {
        return await _context.Set<AssetTransfer>()
            .Include(at => at.Asset)
            .Include(at => at.InitiatedBy)
            .Include(at => at.FromEmployee)
            .Include(at => at.ToEmployee)
            .Include(at => at.FromLocation)
            .Include(at => at.ToLocation)
            .Include(at => at.FromUnit)
            .Include(at => at.ToUnit)
            .Where(at => at.TenantId == tenantId && at.Status == HRAssetTransferStatus.Pending && !at.IsDeleted)
            .OrderBy(at => at.TransferDate)
            .ToListAsync();
    }
}

#endregion

#region Asset Surcharge Repositories — area 16 slice 7

public class AssetSurchargeRepository : GenericRepository<AssetSurcharge>, IAssetSurchargeRepository
{
    public AssetSurchargeRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// The graph every surcharge read needs.
    /// </summary>
    /// <remarks>
    /// ⚠ Stated once and reused by all four reads below, rather than copied. Four queries feeding
    /// one mapping is how this area produced a blank field six times over — a list read that loads
    /// less than the by-id read renders the same record differently, and only an assertion that the
    /// two agree ever catches it. There is no reason for them to differ here, so they do not.
    /// </remarks>
    private IQueryable<AssetSurcharge> WithGraph() =>
        _context.Set<AssetSurcharge>()
            .Include(x => x.Assignment).ThenInclude(a => a.Asset)
            .Include(x => x.Employee)
            .Include(x => x.RaisedBy)
            .Include(x => x.ApprovedBy)
            .Include(x => x.WaivedBy)
            .Include(x => x.Recoveries.Where(r => !r.IsDeleted)).ThenInclude(r => r.RecordedBy);

    public async Task<IEnumerable<AssetSurcharge>> GetByTenantAsync(Guid tenantId)
        => await WithGraph()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.RaisedAt)
            .ToListAsync();

    public async Task<AssetSurcharge?> GetWithDetailsAsync(Guid id)
        => await WithGraph().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

    public async Task<IEnumerable<AssetSurcharge>> GetByEmployeeIdAsync(Guid employeeId)
        => await WithGraph()
            .Where(x => x.EmployeeId == employeeId && !x.IsDeleted)
            .OrderByDescending(x => x.RaisedAt)
            .ToListAsync();

    public async Task<IEnumerable<AssetSurcharge>> GetByAssignmentIdAsync(Guid assignmentId)
        => await WithGraph()
            .Where(x => x.AssignmentId == assignmentId && !x.IsDeleted)
            .OrderByDescending(x => x.RaisedAt)
            .ToListAsync();

    /// <summary>
    /// Charges that have been decided and still carry a balance.
    /// </summary>
    /// <remarks>
    /// Draft, with-employee and submitted charges are deliberately absent: a figure nobody has
    /// ruled on is not a debt, and putting it in an outstanding list would show a proposal as
    /// though it were money owed.
    /// </remarks>
    public async Task<IEnumerable<AssetSurcharge>> GetOutstandingAsync(Guid tenantId)
        => await WithGraph()
            .Where(x => x.TenantId == tenantId
                && !x.IsDeleted
                && (x.Status == AssetSurchargeStatus.Approved || x.Status == AssetSurchargeStatus.Recovering)
                && x.AmountRecovered < x.AssessedAmount)
            .OrderBy(x => x.RecoveryStartDate)
            .ToListAsync();
}

public class AssetSurchargeRecoveryRepository
    : GenericRepository<AssetSurchargeRecovery>, IAssetSurchargeRecoveryRepository
{
    public AssetSurchargeRecoveryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AssetSurchargeRecovery>> GetBySurchargeIdAsync(Guid surchargeId)
        => await _context.Set<AssetSurchargeRecovery>()
            .Include(x => x.RecordedBy)
            .Where(x => x.SurchargeId == surchargeId && !x.IsDeleted)
            .OrderBy(x => x.RecoveredOn)
            .ToListAsync();
}

#endregion

