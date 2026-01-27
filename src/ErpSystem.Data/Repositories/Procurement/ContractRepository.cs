using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

#region Contract Repository

public class ContractRepository : GenericRepository<Contract>, IContractRepository
{
    public ContractRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Contract?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(c => c.Id == id && !c.IsDeleted)
            .Include(c => c.TenderAward)
            .Include(c => c.Tender)
            .Include(c => c.BusinessPartner)
            .FirstOrDefaultAsync();
    }

    public async Task<Contract?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _dbSet
            .Where(c => c.Id == id && !c.IsDeleted)
            .Include(c => c.TenderAward)
            .Include(c => c.Tender)
            .Include(c => c.BusinessPartner)
            .Include(c => c.TenderBid)
            .Include(c => c.Milestones.Where(m => !m.IsDeleted).OrderBy(m => m.SequenceNumber))
            .Include(c => c.Amendments.Where(a => !a.IsDeleted).OrderBy(a => a.SequenceNumber))
            .Include(c => c.Documents.Where(d => !d.IsDeleted))
            .Include(c => c.SignedBy)
            .Include(c => c.CreatedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<Contract?> GetByContractNumberAsync(string contractNumber)
    {
        return await _dbSet
            .Where(c => c.ContractNumber == contractNumber && !c.IsDeleted)
            .Include(c => c.BusinessPartner)
            .Include(c => c.Tender)
            .FirstOrDefaultAsync();
    }

    public async Task<Contract?> GetByAwardIdAsync(Guid awardId)
    {
        return await _dbSet
            .Where(c => c.TenderAwardId == awardId && !c.IsDeleted)
            .Include(c => c.BusinessPartner)
            .Include(c => c.Tender)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Contract>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(c => c.BusinessPartnerId == businessPartnerId && !c.IsDeleted)
            .Include(c => c.Tender)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Contract>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(c => c.TenderId == tenderId && !c.IsDeleted)
            .Include(c => c.BusinessPartner)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<ErpSystem.Core.DTOs.Common.PagedResult<Contract>> GetContractsAsync(int page, int pageSize, string? search = null, string? status = null, string? contractType = null)
    {
        var query = _dbSet.Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.ContractNumber.Contains(search) || c.ContractTitle.Contains(search) || c.BusinessPartner.PartnerName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(contractType))
        {
            query = query.Where(c => c.ContractType == contractType);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(c => c.BusinessPartner)
            .Include(c => c.Tender)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ErpSystem.Core.DTOs.Common.PagedResult<Contract>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<Contract>> GetActiveContractsAsync()
    {
        return await _dbSet
            .Where(c => c.Status == "Active" && !c.IsDeleted)
            .Include(c => c.BusinessPartner)
            .Include(c => c.Tender)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Contract>> GetExpiringContractsAsync(int daysThreshold)
    {
        var thresholdDate = DateTime.UtcNow.AddDays(daysThreshold);
        return await _dbSet
            .Where(c => c.Status == "Active" && !c.IsDeleted && c.EndDate != null && c.EndDate <= thresholdDate)
            .Include(c => c.BusinessPartner)
            .Include(c => c.Tender)
            .OrderBy(c => c.EndDate)
            .ToListAsync();
    }

    public async Task<Contract> CreateAsync(Contract contract)
    {
        await _dbSet.AddAsync(contract);
        return contract;
    }

    public async Task<Contract> UpdateAsync(Contract contract)
    {
        contract.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(contract);
        return await Task.FromResult(contract);
    }

    public async Task DeleteAsync(Guid id)
    {
        var contract = await _dbSet.FirstOrDefaultAsync(c => c.Id == id);
        if (contract != null)
        {
            contract.IsDeleted = true;
            contract.DeletedAt = DateTime.UtcNow;
        }
    }

    public async Task<string> GenerateContractNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await _dbSet.CountAsync(c => c.ContractNumber.StartsWith($"CTR-{year}"));
        return $"CTR-{year}-{(count + 1):D5}";
    }
}

#endregion

#region Contract Milestone Repository

public class ContractMilestoneRepository : GenericRepository<ContractMilestone>, IContractMilestoneRepository
{
    public ContractMilestoneRepository(ApplicationDbContext context) : base(context) { }

    public async Task<ContractMilestone?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(m => m.Id == id && !m.IsDeleted)
            .Include(m => m.Contract)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ContractMilestone>> GetByContractIdAsync(Guid contractId)
    {
        return await _dbSet
            .Where(m => m.ContractId == contractId && !m.IsDeleted)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<ContractMilestone>> GetPendingMilestonesAsync()
    {
        return await _dbSet
            .Where(m => m.Status == "Pending" && !m.IsDeleted)
            .Include(m => m.Contract)
                .ThenInclude(c => c.BusinessPartner)
            .OrderBy(m => m.PlannedDate)
            .ToListAsync();
    }

    public async Task<ContractMilestone> CreateAsync(ContractMilestone milestone)
    {
        await _dbSet.AddAsync(milestone);
        return milestone;
    }

    public async Task<ContractMilestone> UpdateAsync(ContractMilestone milestone)
    {
        milestone.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(milestone);
        return await Task.FromResult(milestone);
    }

    public async Task DeleteAsync(Guid id)
    {
        var milestone = await _dbSet.FirstOrDefaultAsync(m => m.Id == id);
        if (milestone != null)
        {
            milestone.IsDeleted = true;
            milestone.DeletedAt = DateTime.UtcNow;
        }
    }

    public async Task DeleteByContractIdAsync(Guid contractId)
    {
        var milestones = await _dbSet.Where(m => m.ContractId == contractId && !m.IsDeleted).ToListAsync();
        foreach (var milestone in milestones)
        {
            milestone.IsDeleted = true;
            milestone.DeletedAt = DateTime.UtcNow;
        }
    }
}

#endregion

#region Contract Amendment Repository

public class ContractAmendmentRepository : GenericRepository<ContractAmendment>, IContractAmendmentRepository
{
    public ContractAmendmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<ContractAmendment?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(a => a.Id == id && !a.IsDeleted)
            .Include(a => a.Contract)
            .Include(a => a.RequestedBy)
            .Include(a => a.ApprovedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ContractAmendment>> GetByContractIdAsync(Guid contractId)
    {
        return await _dbSet
            .Where(a => a.ContractId == contractId && !a.IsDeleted)
            .Include(a => a.RequestedBy)
            .Include(a => a.ApprovedBy)
            .OrderBy(a => a.SequenceNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<ContractAmendment>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Where(a => a.Status == "PendingApproval" && !a.IsDeleted)
            .Include(a => a.Contract)
                .ThenInclude(c => c.BusinessPartner)
            .Include(a => a.RequestedBy)
            .OrderBy(a => a.RequestedDate)
            .ToListAsync();
    }

    public async Task<ContractAmendment> CreateAsync(ContractAmendment amendment)
    {
        await _dbSet.AddAsync(amendment);
        return amendment;
    }

    public async Task<ContractAmendment> UpdateAsync(ContractAmendment amendment)
    {
        amendment.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(amendment);
        return await Task.FromResult(amendment);
    }

    public async Task DeleteAsync(Guid id)
    {
        var amendment = await _dbSet.FirstOrDefaultAsync(a => a.Id == id);
        if (amendment != null)
        {
            amendment.IsDeleted = true;
            amendment.DeletedAt = DateTime.UtcNow;
        }
    }

    public async Task<string> GenerateAmendmentNumberAsync(Guid contractId)
    {
        var count = await _dbSet.CountAsync(a => a.ContractId == contractId);
        return $"AMD-{(count + 1):D3}";
    }
}

#endregion

#region Contract Document Repository

public class ContractDocumentRepository : GenericRepository<ContractDocument>, IContractDocumentRepository
{
    public ContractDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<ContractDocument?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(d => d.Id == id && !d.IsDeleted)
            .Include(d => d.UploadedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ContractDocument>> GetByContractIdAsync(Guid contractId)
    {
        return await _dbSet
            .Where(d => d.ContractId == contractId && !d.IsDeleted)
            .Include(d => d.UploadedBy)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<ContractDocument> CreateAsync(ContractDocument document)
    {
        await _dbSet.AddAsync(document);
        return document;
    }

    public async Task<ContractDocument> UpdateAsync(ContractDocument document)
    {
        document.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(document);
        return await Task.FromResult(document);
    }

    public async Task DeleteAsync(Guid id)
    {
        var document = await _dbSet.FirstOrDefaultAsync(d => d.Id == id);
        if (document != null)
        {
            document.IsDeleted = true;
            document.DeletedAt = DateTime.UtcNow;
        }
    }
}

#endregion

