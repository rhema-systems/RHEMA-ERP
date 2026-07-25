using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class LeadService : ILeadService
{
    private readonly IGenericRepository<Lead> _leadRepo;
    private readonly IGenericRepository<BusinessPartner> _bpRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<LeadService> _logger;

    public LeadService(
        IGenericRepository<Lead> leadRepo,
        IGenericRepository<BusinessPartner> bpRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<LeadService> logger)
    {
        _leadRepo = leadRepo;
        _bpRepo = bpRepo;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region CRUD

    public async Task<LeadDetailDto> CreateAsync(CreateLeadDto dto)
    {
        var lead = new Lead
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            CompanyName = dto.CompanyName,
            JobTitle = dto.JobTitle,
            Email = dto.Email,
            Phone = dto.Phone,
            Mobile = dto.Mobile,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City = dto.City,
            State = dto.State,
            PostalCode = dto.PostalCode,
            Country = dto.Country,
            LeadSource = dto.LeadSource,
            LeadStatus = "New",
            QualificationScore = 0,
            EstimatedValue = dto.EstimatedValue ?? 0,
            AssignedToId = dto.AssignedToId,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };

        await _leadRepo.AddAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Lead {LeadName} from source {Source}", lead.FullName, lead.LeadSource);
        return await GetByIdAsync(lead.Id) ?? throw new InvalidOperationException("Failed to retrieve created Lead");
    }

    public async Task<LeadDetailDto> UpdateAsync(Guid id, UpdateLeadDto dto)
    {
        var lead = await _leadRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Lead {id} not found");

        if (dto.FirstName != null) lead.FirstName = dto.FirstName;
        if (dto.LastName != null) lead.LastName = dto.LastName;
        if (dto.CompanyName != null) lead.CompanyName = dto.CompanyName;
        if (dto.JobTitle != null) lead.JobTitle = dto.JobTitle;
        if (dto.Email != null) lead.Email = dto.Email;
        if (dto.Phone != null) lead.Phone = dto.Phone;
        if (dto.Mobile != null) lead.Mobile = dto.Mobile;
        if (dto.AddressLine1 != null) lead.AddressLine1 = dto.AddressLine1;
        if (dto.AddressLine2 != null) lead.AddressLine2 = dto.AddressLine2;
        if (dto.City != null) lead.City = dto.City;
        if (dto.State != null) lead.State = dto.State;
        if (dto.PostalCode != null) lead.PostalCode = dto.PostalCode;
        if (dto.Country != null) lead.Country = dto.Country;
        if (dto.LeadSource != null) lead.LeadSource = dto.LeadSource;
        if (dto.LeadStatus != null) lead.LeadStatus = dto.LeadStatus;
        if (dto.QualificationScore.HasValue) lead.QualificationScore = dto.QualificationScore.Value;
        if (dto.EstimatedValue.HasValue) lead.EstimatedValue = dto.EstimatedValue.Value;
        if (dto.AssignedToId.HasValue) lead.AssignedToId = dto.AssignedToId;
        if (dto.NextFollowUpDate.HasValue) lead.NextFollowUpDate = dto.NextFollowUpDate;
        if (dto.Notes != null) lead.Notes = dto.Notes;

        lead.LastContactDate = DateTime.UtcNow;

        await _leadRepo.UpdateAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated Lead");
    }

    public async Task<LeadDetailDto?> GetByIdAsync(Guid id)
    {
        var lead = await _leadRepo.GetByIdAsync(id,
            l => l.AssignedTo!,
            l => l.Activities,
            l => l.Opportunities);

        return lead == null ? null : MapToDetailDto(lead);
    }

    public async Task<PagedResult<LeadSummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null, string? status = null, string? source = null,
        Guid? assignedToId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _leadRepo.GetQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(l => l.FirstName.Contains(search) || l.LastName.Contains(search) ||
                                     (l.CompanyName != null && l.CompanyName.Contains(search)) ||
                                     (l.Email != null && l.Email.Contains(search)));
        if (!string.IsNullOrEmpty(status))
            query = query.Where(l => l.LeadStatus == status);
        if (!string.IsNullOrEmpty(source))
            query = query.Where(l => l.LeadSource == source);
        if (assignedToId.HasValue)
            query = query.Where(l => l.AssignedToId == assignedToId.Value);
        if (startDate.HasValue)
            query = query.Where(l => l.CreatedAt >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(l => l.CreatedAt <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(l => l.AssignedTo)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<LeadSummaryDto>
        {
            Items = items.Select(MapToSummaryDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    #endregion

    #region Lifecycle

    public async Task<LeadDetailDto> QualifyAsync(Guid id, int? score = null)
    {
        var lead = await _leadRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Lead {id} not found");

        lead.LeadStatus = "Qualified";
        if (score.HasValue) lead.QualificationScore = score.Value;
        else if (lead.QualificationScore < 70) lead.QualificationScore = 70;

        await _leadRepo.UpdateAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Lead {LeadName} qualified with score {Score}", lead.FullName, lead.QualificationScore);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<LeadDetailDto> DisqualifyAsync(Guid id, string? reason = null)
    {
        var lead = await _leadRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Lead {id} not found");

        lead.LeadStatus = "Unqualified";
        if (reason != null) lead.Notes = $"{lead.Notes}\n[Disqualified] {reason}".Trim();

        await _leadRepo.UpdateAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Lead {LeadName} disqualified: {Reason}", lead.FullName, reason);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<LeadDetailDto> ConvertToCustomerAsync(Guid id, ConvertLeadDto dto)
    {
        var lead = await _leadRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Lead {id} not found");

        if (lead.LeadStatus == "Converted")
            throw new InvalidOperationException("Lead has already been converted");

        // Create a BusinessPartner from the lead
        var bp = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            PartnerName = lead.CompanyName ?? lead.FullName,
            PartnerCode = $"C-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CustomerAccountNumber = $"C-{DateTime.UtcNow:yyyyMMddHHmmss}",
            PartnerType = "Customer",
            CustomerType = lead.CompanyName != null ? "Corporate" : "Individual",
            PrimaryEmail = lead.Email,
            PrimaryPhone = lead.Phone,
            PhysicalAddress = lead.AddressLine1,
            PhysicalCity = lead.City,
            PhysicalState = lead.State,
            PhysicalPostalCode = lead.PostalCode,
            PhysicalCountry = lead.Country,
            PrimaryContactName = lead.FullName,
            RegistrationStatus = "Approved",
            ApprovalStatus = "Approved",
            IsActive = true,
            TenantId = lead.TenantId
        };

        await _bpRepo.AddAsync(bp);

        // Mark lead as converted
        lead.LeadStatus = "Converted";
        lead.ConvertedCustomerId = bp.Id;
        lead.ConvertedDate = DateTime.UtcNow;

        await _leadRepo.UpdateAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Lead {LeadName} converted to customer BusinessPartner {BusinessPartnerId}", lead.FullName, bp.Id);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    #endregion

    #region Queries

    public async Task<List<LeadSummaryDto>> GetByStatusAsync(string status)
    {
        var leads = await _leadRepo.GetQueryable()
            .Where(l => l.LeadStatus == status)
            .Include(l => l.AssignedTo)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
        return leads.Select(MapToSummaryDto).ToList();
    }

    public async Task<List<LeadSummaryDto>> GetUpcomingFollowUpsAsync(int daysAhead = 7)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        var leads = await _leadRepo.GetQueryable()
            .Where(l => l.NextFollowUpDate.HasValue && l.NextFollowUpDate <= cutoff && l.LeadStatus != "Converted" && l.LeadStatus != "Unqualified")
            .Include(l => l.AssignedTo)
            .OrderBy(l => l.NextFollowUpDate)
            .ToListAsync();
        return leads.Select(MapToSummaryDto).ToList();
    }

    #endregion

    #region Mapping

    private static LeadSummaryDto MapToSummaryDto(Lead l) => new()
    {
        Id = l.Id,
        FirstName = l.FirstName,
        LastName = l.LastName,
        FullName = l.FullName,
        CompanyName = l.CompanyName,
        Email = l.Email,
        Phone = l.Phone,
        LeadSource = l.LeadSource,
        LeadStatus = l.LeadStatus,
        QualificationScore = l.QualificationScore,
        EstimatedValue = l.EstimatedValue,
        LastContactDate = l.LastContactDate,
        NextFollowUpDate = l.NextFollowUpDate,
        AssignedToName = l.AssignedTo?.UserName,
        IsConverted = l.ConvertedCustomerId.HasValue,
        CreatedAt = l.CreatedAt
    };

    private static LeadDetailDto MapToDetailDto(Lead l) => new()
    {
        Id = l.Id,
        FirstName = l.FirstName,
        LastName = l.LastName,
        FullName = l.FullName,
        CompanyName = l.CompanyName,
        Email = l.Email,
        Phone = l.Phone,
        JobTitle = l.JobTitle,
        Mobile = l.Mobile,
        AddressLine1 = l.AddressLine1,
        AddressLine2 = l.AddressLine2,
        City = l.City,
        State = l.State,
        PostalCode = l.PostalCode,
        Country = l.Country,
        LeadSource = l.LeadSource,
        LeadStatus = l.LeadStatus,
        QualificationScore = l.QualificationScore,
        EstimatedValue = l.EstimatedValue,
        LastContactDate = l.LastContactDate,
        NextFollowUpDate = l.NextFollowUpDate,
        AssignedToId = l.AssignedToId,
        AssignedToName = l.AssignedTo?.UserName,
        ConvertedCustomerId = l.ConvertedCustomerId,
        ConvertedCustomerName = l.ConvertedCustomerId.HasValue ? (l.CompanyName ?? l.FullName) : null,
        ConvertedDate = l.ConvertedDate,
        Notes = l.Notes,
        IsConverted = l.ConvertedCustomerId.HasValue,
        CreatedAt = l.CreatedAt,
        Activities = l.Activities?.Select(a => new ActivitySummaryDto
        {
            Id = a.Id,
            Subject = a.Subject,
            ActivityType = a.ActivityType,
            ActivityDate = a.ActivityDate,
            DueDate = a.DueDate,
            ActivityStatus = a.ActivityStatus,
            Priority = a.Priority,
            Outcome = a.Outcome,
            RequiresFollowUp = a.RequiresFollowUp,
            CreatedAt = a.CreatedAt
        }).ToList() ?? new(),
        Opportunities = l.Opportunities?.Select(o => new OpportunitySummaryDto
        {
            Id = o.Id,
            Name = o.Name,
            Stage = o.Stage,
            Probability = o.Probability,
            Amount = o.Amount,
            Currency = o.Currency,
            ExpectedCloseDate = o.ExpectedCloseDate,
            OpportunityType = o.OpportunityType,
            CreatedAt = o.CreatedAt
        }).ToList() ?? new()
    };

    #endregion
}
