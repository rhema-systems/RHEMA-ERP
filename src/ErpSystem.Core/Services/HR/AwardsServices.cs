using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

#region Award Type Services

public class AwardTypeService : IAwardTypeService
{
    private readonly IAwardTypeRepository _awardTypeRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AwardTypeService(IAwardTypeRepository awardTypeRepo, IUnitOfWork unitOfWork)
    {
        _awardTypeRepo = awardTypeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<AwardTypeDto?> GetByIdAsync(Guid id)
    {
        var entity = await _awardTypeRepo.GetByIdAsync(id);
        return entity?.ToDto();
    }

    public async Task<AwardTypeDto?> GetByCodeAsync(Guid tenantId, string code)
    {
        var entity = await _awardTypeRepo.GetByCodeAsync(tenantId, code);
        return entity?.ToDto();
    }

    public async Task<AwardTypeDto?> GetWithDetailsAsync(Guid id)
    {
        var entity = await _awardTypeRepo.GetWithDetailsAsync(id);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AwardTypeSummaryDto>> GetAllAsync(Guid tenantId)
    {
        var types = await _awardTypeRepo.GetByTenantAsync(tenantId);
        var result = new List<AwardTypeSummaryDto>();
        foreach (var type in types)
        {
            var count = await _awardTypeRepo.GetAwardCountByTypeAsync(type.Id);
            result.Add(type.ToSummaryDto(count));
        }
        return result;
    }

    public async Task<PagedResult<AwardTypeSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, AwardCategory? category = null)
    {
        var all = await _awardTypeRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(at => at.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || at.Code.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

        if (category.HasValue)
            q = q.Where(at => at.Category == category.Value);

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var dtos = new List<AwardTypeSummaryDto>();
        foreach (var type in items)
        {
            var count = await _awardTypeRepo.GetAwardCountByTypeAsync(type.Id);
            dtos.Add(type.ToSummaryDto(count));
        }

        return new PagedResult<AwardTypeSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<AwardTypeSummaryDto>> GetActiveByCategoryAsync(Guid tenantId, AwardCategory category)
    {
        var types = await _awardTypeRepo.GetActiveByCategoryAsync(tenantId, category);
        return types.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardTypeSummaryDto>> GetActiveByFrequencyAsync(Guid tenantId, AwardFrequency frequency)
    {
        var types = await _awardTypeRepo.GetActiveByFrequencyAsync(tenantId, frequency);
        return types.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardTypeSummaryDto>> GetWithLevelsAsync(Guid tenantId)
    {
        var types = await _awardTypeRepo.GetWithLevelsAsync(tenantId);
        return types.ToSummaryDtoList();
    }

    public async Task<bool> CanDeleteAsync(Guid id)
    {
        return !await _awardTypeRepo.HasActiveNominationsAsync(id);
    }

    public async Task<bool> IsInUseAsync(Guid id)
    {
        return await _awardTypeRepo.IsInUseAsync(id);
    }

    public async Task<AwardTypeDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardTypeDto dto)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _awardTypeRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AwardTypeDto> UpdateAsync(Guid id, Guid userId, UpdateAwardTypeDto dto)
    {
        var entity = await _awardTypeRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardType {id} not found.");
        entity.UpdateEntity(dto, userId);
        await _awardTypeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _awardTypeRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Award Level Services

public class AwardLevelService : IAwardLevelService
{
    private readonly IAwardLevelRepository _levelRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AwardLevelService(IAwardLevelRepository levelRepo, IUnitOfWork unitOfWork)
    {
        _levelRepo = levelRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<AwardLevelDto?> GetByIdAsync(Guid id)
    {
        var entity = await _levelRepo.GetByIdAsync(id);
        return entity?.ToDto();
    }

    public async Task<AwardLevelDto?> GetByCodeAsync(Guid tenantId, string code)
    {
        var entity = await _levelRepo.GetByCodeAsync(tenantId, code);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AwardLevelDto>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        var levels = await _levelRepo.GetByAwardTypeIdAsync(awardTypeId);
        return levels.ToDtoList();
    }

    public async Task<IEnumerable<AwardLevelDto>> GetActiveByAwardTypeIdAsync(Guid awardTypeId)
    {
        var levels = await _levelRepo.GetActiveByAwardTypeIdAsync(awardTypeId);
        return levels.ToDtoList();
    }

    public async Task<AwardLevelDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardLevelDto dto)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _levelRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AwardLevelDto> UpdateAsync(Guid id, Guid userId, UpdateAwardLevelDto dto)
    {
        var entity = await _levelRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardLevel {id} not found.");
        entity.UpdateEntity(dto, userId);
        await _levelRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _levelRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardTypeTargetService : IAwardTypeTargetService
{
    private readonly IAwardTypeTargetRepository _targetRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AwardTypeTargetService(IAwardTypeTargetRepository targetRepo, IUnitOfWork unitOfWork)
    {
        _targetRepo = targetRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<AwardTypeTargetDto?> GetByIdAsync(Guid id)
    {
        var entity = await _targetRepo.GetByIdAsync(id);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AwardTypeTargetDto>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        var targets = await _targetRepo.GetByAwardTypeIdAsync(awardTypeId);
        return targets.ToDtoList();
    }

    public async Task<IEnumerable<AwardTypeTargetDto>> GetByScopeAsync(Guid awardTypeId, AwardScope scope)
    {
        var targets = await _targetRepo.GetByScopeAsync(awardTypeId, scope);
        return targets.ToDtoList();
    }

    public async Task<bool> IsEmployeeEligibleAsync(Guid awardTypeId, Guid employeeId)
    {
        return await _targetRepo.IsEmployeeEligibleAsync(awardTypeId, employeeId);
    }

    public async Task<AwardTypeTargetDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardTypeTargetDto dto)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _targetRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AwardTypeTargetDto> UpdateAsync(Guid id, Guid userId, UpdateAwardTypeTargetDto dto)
    {
        var entity = await _targetRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardTypeTarget {id} not found.");
        entity.UpdateEntity(dto, userId);
        await _targetRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _targetRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardBudgetService : IAwardBudgetService
{
    private readonly IAwardBudgetRepository _budgetRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AwardBudgetService(IAwardBudgetRepository budgetRepo, IUnitOfWork unitOfWork)
    {
        _budgetRepo = budgetRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<AwardBudgetDto?> GetByIdAsync(Guid id)
    {
        var entity = await _budgetRepo.GetByIdAsync(id);
        return entity?.ToDto();
    }

    public async Task<AwardBudgetDto?> GetByYearAsync(Guid awardTypeId, int year)
    {
        var entity = await _budgetRepo.GetByYearAsync(awardTypeId, year);
        return entity?.ToDto();
    }

    public async Task<AwardBudgetDto?> GetByBudgetCodeAsync(Guid tenantId, string budgetCode)
    {
        var entity = await _budgetRepo.GetByBudgetCodeAsync(tenantId, budgetCode);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AwardBudgetDto>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        var budgets = await _budgetRepo.GetByAwardTypeIdAsync(awardTypeId);
        return budgets.ToDtoList();
    }

    public async Task<IEnumerable<AwardBudgetDto>> GetByYearRangeAsync(Guid tenantId, int startYear, int endYear)
    {
        var budgets = await _budgetRepo.GetByYearRangeAsync(tenantId, startYear, endYear);
        return budgets.ToDtoList();
    }

    public async Task<decimal> GetAvailableBudgetAsync(Guid awardTypeId, int year)
    {
        return await _budgetRepo.GetAvailableBudgetAsync(awardTypeId, year);
    }

    public async Task<AwardBudgetDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardBudgetDto dto)
    {
        // Check if budget already exists for this award type and year
        var existing = await _budgetRepo.GetByYearAsync(dto.AwardTypeId, dto.Year);
        if (existing != null)
            throw new InvalidOperationException($"Budget for award type {dto.AwardTypeId} and year {dto.Year} already exists.");

        var entity = dto.ToEntity(tenantId, userId);
        await _budgetRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AwardBudgetDto> UpdateAsync(Guid id, Guid userId, UpdateAwardBudgetDto dto)
    {
        var entity = await _budgetRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardBudget {id} not found.");
        entity.UpdateEntity(dto, userId);
        await _budgetRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _budgetRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Employee Award Services

public class EmployeeAwardService : IEmployeeAwardService
{
    private readonly IEmployeeAwardRepository _awardRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public EmployeeAwardService(IEmployeeAwardRepository awardRepo, IAwardNominationRepository nominationRepo, IUnitOfWork unitOfWork)
    {
        _awardRepo = awardRepo;
        _nominationRepo = nominationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<EmployeeAwardDto?> GetByIdAsync(Guid id)
    {
        var entity = await _awardRepo.GetWithDetailsAsync(id);
        return entity?.ToDto();
    }

    public async Task<EmployeeAwardDto?> GetByAwardNumberAsync(Guid tenantId, string awardNumber)
    {
        var entity = await _awardRepo.GetByAwardNumberAsync(tenantId, awardNumber);
        return entity?.ToDto();
    }

    public async Task<EmployeeAwardDetailDto?> GetWithDetailsAsync(Guid id)
    {
        var entity = await _awardRepo.GetWithDetailsAsync(id);
        return entity?.ToDetailDto();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetAllAsync(Guid tenantId)
    {
        var awards = await _awardRepo.GetByTenantAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<PagedResult<EmployeeAwardSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, int? year = null, AwardStatus? status = null)
    {
        var all = await _awardRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(a => a.AwardNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (a.Employee != null && (a.Employee.FirstName + " " + a.Employee.LastName).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

        // Filter by year via nomination if needed
        if (year.HasValue)
            q = q.Where(a => a.AwardNomination != null && a.AwardNomination.Year == year.Value);

        // Status filter removed - EmployeeAward doesn't have Status property
        // Award status is now tracked through the nomination workflow

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<EmployeeAwardSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetByEmployeeIdAsync(Guid employeeId)
    {
        var awards = await _awardRepo.GetByEmployeeIdAsync(employeeId);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        var awards = await _awardRepo.GetByAwardTypeIdAsync(awardTypeId);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetByAwardLevelIdAsync(Guid awardLevelId)
    {
        var awards = await _awardRepo.GetByAwardLevelIdAsync(awardLevelId);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetByNominationIdAsync(Guid nominationId)
    {
        var awards = await _awardRepo.GetByNominationIdAsync(nominationId);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate)
    {
        var awards = await _awardRepo.GetByDateRangeAsync(tenantId, startDate, endDate);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetPendingPresentationsAsync(Guid tenantId)
    {
        var awards = await _awardRepo.GetPendingPresentationsAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetPendingPaymentsAsync(Guid tenantId)
    {
        var awards = await _awardRepo.GetPendingPaymentsAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeAwardSummaryDto>> GetPendingLeaveProcessingAsync(Guid tenantId)
    {
        var awards = await _awardRepo.GetPendingLeaveProcessingAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<EmployeeAwardDto> CreateAsync(Guid tenantId, Guid userId, CreateEmployeeAwardDto dto)
    {
        var awardNumber = $"AWD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var entity = dto.ToEntity(tenantId, userId, awardNumber);
        await _awardRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _awardRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<EmployeeAwardDto> CreateFromNominationAsync(Guid nominationId, Guid userId, CreateEmployeeAwardFromNominationDto dto)
    {
        var nomination = await _nominationRepo.GetWithDetailsAsync(nominationId)
            ?? throw new InvalidOperationException($"AwardNomination {nominationId} not found.");

        var awardNumber = $"AWD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var entity = dto.ToEntity(nomination, nomination.TenantId, userId, awardNumber);
        await _awardRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _awardRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<EmployeeAwardDto> UpdateAsync(Guid id, Guid userId, UpdateEmployeeAwardDto dto)
    {
        var entity = await _awardRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"EmployeeAward {id} not found.");
        entity.UpdateEntity(dto, userId);
        await _awardRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _awardRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _awardRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task SchedulePresentationAsync(Guid userId, ScheduleAwardPresentationDto dto)
    {
        var entity = await _awardRepo.GetByIdAsync(dto.AwardId)
            ?? throw new InvalidOperationException($"EmployeeAward {dto.AwardId} not found.");

        entity.PresentationDate = dto.PresentationDate;
        entity.PresentationVenue = dto.PresentationVenue;
        entity.PresentedById = dto.PresentedById;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _awardRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RecordPresentationAsync(Guid id, Guid userId, RecordAwardPresentationDto dto)
    {
        var entity = await _awardRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"EmployeeAward {id} not found.");

        entity.PresentationDate = dto.PresentationDate;
        entity.PresentationVenue = dto.PresentationVenue;
        entity.CertificateIssued = dto.CertificateIssued;
        entity.TrophyIssued = dto.TrophyIssued;
        entity.CertificateNumber = dto.CertificateNumber;
        entity.PublicationNotes = dto.PresentationNotes; // Store presentation notes in PublicationNotes
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _awardRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ProcessPaymentAsync(Guid userId, ProcessAwardPaymentDto dto)
    {
        var entity = await _awardRepo.GetByIdAsync(dto.AwardId)
            ?? throw new InvalidOperationException($"EmployeeAward {dto.AwardId} not found.");

        entity.PaymentProcessed = true;
        entity.PaymentDate = DateTime.UtcNow;
        entity.PaymentReference = dto.PaymentReference;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _awardRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ProcessLeaveAsync(Guid id, Guid userId)
    {
        var entity = await _awardRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"EmployeeAward {id} not found.");

        entity.LeaveProcessed = true;
        entity.LeaveProcessedDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _awardRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardAttachmentService : IAwardAttachmentService
{
    private readonly IAwardAttachmentRepository _attachmentRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AwardAttachmentService(IAwardAttachmentRepository attachmentRepo, IUnitOfWork unitOfWork)
    {
        _attachmentRepo = attachmentRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<AwardAttachmentDto?> GetByIdAsync(Guid id)
    {
        var entity = await _attachmentRepo.GetByIdAsync(id);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AwardAttachmentDto>> GetByAwardIdAsync(Guid awardId)
    {
        var attachments = await _attachmentRepo.GetByAwardIdAsync(awardId);
        return attachments.ToDtoList();
    }

    public async Task<IEnumerable<AwardAttachmentDto>> GetByTypeAsync(Guid awardId, AwardAttachmentType type)
    {
        var attachments = await _attachmentRepo.GetByTypeAsync(awardId, type);
        return attachments.ToDtoList();
    }

    public async Task<AwardAttachmentDto> CreateAsync(Guid tenantId, Guid awardId, Guid userId, CreateAwardAttachmentDto dto)
    {
        var entity = dto.ToEntity(tenantId, awardId, userId);
        await _attachmentRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _attachmentRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Award Nomination Services

public class AwardNominationService : IAwardNominationService
{
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly IEmployeeAwardRepository _awardRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AwardNominationService(
        IAwardNominationRepository nominationRepo,
        IEmployeeAwardRepository awardRepo,
        IUnitOfWork unitOfWork)
    {
        _nominationRepo = nominationRepo;
        _awardRepo = awardRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<AwardNominationDto?> GetByIdAsync(Guid id)
    {
        var entity = await _nominationRepo.GetWithDetailsAsync(id);
        return entity?.ToDto();
    }

    public async Task<AwardNominationDto?> GetByNominationNumberAsync(Guid tenantId, string nominationNumber)
    {
        var entity = await _nominationRepo.GetByNominationNumberAsync(tenantId, nominationNumber);
        return entity?.ToDto();
    }

    public async Task<AwardNominationDetailDto?> GetWithDetailsAsync(Guid id)
    {
        var entity = await _nominationRepo.GetWithDetailsAsync(id);
        return entity?.ToDetailDto();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetAllAsync(Guid tenantId)
    {
        var nominations = await _nominationRepo.GetByTenantAsync(tenantId);
        return nominations.ToSummaryDtoList();
    }

    public async Task<PagedResult<AwardNominationSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, int? year = null, AwardNominationStatus? status = null)
    {
        var all = await _nominationRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(n => n.NominationNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (n.Nominee != null && (n.Nominee.FirstName + " " + n.Nominee.LastName).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

        if (year.HasValue)
            q = q.Where(n => n.Year == year.Value);

        if (status.HasValue)
            q = q.Where(n => n.Status == status.Value);

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<AwardNominationSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByNomineeIdAsync(Guid nomineeId)
    {
        var nominations = await _nominationRepo.GetByNomineeIdAsync(nomineeId);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByNominatedByIdAsync(Guid nominatedById)
    {
        var nominations = await _nominationRepo.GetByNominatedByIdAsync(nominatedById);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        var nominations = await _nominationRepo.GetByAwardTypeIdAsync(awardTypeId);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByYearAsync(Guid tenantId, int year)
    {
        var nominations = await _nominationRepo.GetByYearAsync(tenantId, year);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByYearAndPeriodAsync(Guid tenantId, int year, int? quarter = null, int? month = null)
    {
        var nominations = await _nominationRepo.GetByYearAndPeriodAsync(tenantId, year, quarter, month);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByStatusAsync(Guid tenantId, AwardNominationStatus status)
    {
        var nominations = await _nominationRepo.GetByStatusAsync(tenantId, status);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetByCommitteeIdAsync(Guid committeeId)
    {
        var nominations = await _nominationRepo.GetByCommitteeIdAsync(committeeId);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetRequiringCommitteeReviewAsync(Guid tenantId)
    {
        var nominations = await _nominationRepo.GetRequiringCommitteeReviewAsync(tenantId);
        return nominations.ToSummaryDtoList();
    }

    public async Task<IEnumerable<AwardNominationSummaryDto>> GetApprovedWithoutAwardAsync(Guid tenantId)
    {
        var nominations = await _nominationRepo.GetApprovedWithoutAwardAsync(tenantId);
        return nominations.ToSummaryDtoList();
    }

    public async Task<AwardNominationDto> CreateAsync(Guid tenantId, Guid nominatedById, Guid userId, CreateAwardNominationDto dto)
    {
        var nominationNumber = $"NOM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var entity = dto.ToEntity(tenantId, nominatedById, userId, nominationNumber);
        await _nominationRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _nominationRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<AwardNominationDto> UpdateAsync(Guid id, Guid userId, UpdateAwardNominationDto dto)
    {
        var entity = await _nominationRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardNomination {id} not found.");

        if (entity.Status != AwardNominationStatus.Draft)
            throw new InvalidOperationException("Only draft nominations can be updated.");

        entity.UpdateEntity(dto, userId);
        await _nominationRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _nominationRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _nominationRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<AwardNominationDto> SubmitAsync(Guid id, Guid userId)
    {
        var entity = await _nominationRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardNomination {id} not found.");

        if (entity.Status != AwardNominationStatus.Draft)
            throw new InvalidOperationException("Only draft nominations can be submitted.");

        entity.Status = AwardNominationStatus.Submitted;
        entity.NominationDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _nominationRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _nominationRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task<AwardNominationDto> AssignToCommitteeAsync(Guid id, Guid committeeId, Guid userId)
    {
        var entity = await _nominationRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardNomination {id} not found.");

        entity.CommitteeId = committeeId;
        entity.Status = AwardNominationStatus.UnderReview;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _nominationRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _nominationRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task<AwardNominationDto> SetOutcomeAsync(Guid id, Guid userId, SetNominationOutcomeDto dto)
    {
        var entity = await _nominationRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardNomination {id} not found.");

        entity.Status = dto.Status;
        entity.OutcomeDate = DateTime.UtcNow;
        entity.OutcomeReason = dto.OutcomeReason;
        entity.AwardLevelId = dto.AwardLevelId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _nominationRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _nominationRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }
}

public class TeamAwardNomineeService : ITeamAwardNomineeService
{
    private readonly ITeamAwardNomineeRepository _nomineeRepo;
    private readonly IUnitOfWork _unitOfWork;

    public TeamAwardNomineeService(ITeamAwardNomineeRepository nomineeRepo, IUnitOfWork unitOfWork)
    {
        _nomineeRepo = nomineeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<TeamAwardNomineeDto>> GetByNominationIdAsync(Guid nominationId)
    {
        var nominees = await _nomineeRepo.GetByNominationIdAsync(nominationId);
        return nominees.ToDtoList();
    }

    public async Task<IEnumerable<TeamAwardNomineeDto>> GetByEmployeeIdAsync(Guid employeeId)
    {
        var nominees = await _nomineeRepo.GetByEmployeeIdAsync(employeeId);
        return nominees.ToDtoList();
    }

    public async Task<TeamAwardNomineeDto> AddAsync(Guid nominationId, Guid employeeId, Guid userId, CreateTeamAwardNomineeDto dto)
    {
        var entity = dto.ToEntity(nominationId, employeeId, userId);
        await _nomineeRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task UpdateAsync(Guid id, Guid userId, UpdateTeamAwardNomineeDto dto)
    {
        var entity = await _nomineeRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"TeamAwardNominee {id} not found.");
        entity.UpdateEntity(dto, userId);
        await _nomineeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid id)
    {
        await _nomineeRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardNomineeContributionService : IAwardNomineeContributionService
{
    private readonly IAwardNomineeContributionRepository _contributionRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AwardNomineeContributionService(
        IAwardNomineeContributionRepository contributionRepo,
        IAwardNominationRepository nominationRepo,
        IUnitOfWork unitOfWork)
    {
        _contributionRepo = contributionRepo;
        _nominationRepo = nominationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<AwardNomineeContributionDto>> GetByNominationIdAsync(Guid nominationId)
    {
        var contributions = await _contributionRepo.GetByNominationIdAsync(nominationId);
        return contributions.ToDtoList();
    }

    public async Task<AwardNomineeContributionDto> AddAsync(Guid nominationId, Guid userId, CreateAwardNomineeContributionDto dto)
    {
        var nomination = await _nominationRepo.GetByIdAsync(nominationId)
            ?? throw new InvalidOperationException($"AwardNomination {nominationId} not found.");

        var entity = dto.ToEntity(nomination.TenantId, nominationId, userId);
        await _contributionRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task UpdateAsync(Guid id, Guid userId, UpdateAwardNomineeContributionDto dto)
    {
        var entity = await _contributionRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardNomineeContribution {id} not found.");

        entity.UpdateEntity(dto, userId);
        await _contributionRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid id)
    {
        await _contributionRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardNominationAttachmentService : IAwardNominationAttachmentService
{
    private readonly IAwardNominationAttachmentRepository _attachmentRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AwardNominationAttachmentService(
        IAwardNominationAttachmentRepository attachmentRepo,
        IAwardNominationRepository nominationRepo,
        IUnitOfWork unitOfWork)
    {
        _attachmentRepo = attachmentRepo;
        _nominationRepo = nominationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<AwardNominationAttachmentDto>> GetByNominationIdAsync(Guid nominationId)
    {
        var attachments = await _attachmentRepo.GetByNominationIdAsync(nominationId);
        return attachments.ToDtoList();
    }

    public async Task<AwardNominationAttachmentDto> AddAsync(Guid nominationId, Guid uploadedById, Guid userId, CreateAwardNominationAttachmentDto dto)
    {
        var nomination = await _nominationRepo.GetByIdAsync(nominationId)
            ?? throw new InvalidOperationException($"AwardNomination {nominationId} not found.");

        var entity = dto.ToEntity(nomination.TenantId, nominationId, uploadedById, userId);
        await _attachmentRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task UpdateAsync(Guid id, Guid userId, UpdateAwardNominationAttachmentDto dto)
    {
        var entity = await _attachmentRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardNominationAttachment {id} not found.");

        entity.UpdateEntity(dto, userId);
        await _attachmentRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid id)
    {
        await _attachmentRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardCommitteeService : IAwardCommitteeService
{
    private readonly IAwardCommitteeRepository _committeeRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AwardCommitteeService(IAwardCommitteeRepository committeeRepo, IUnitOfWork unitOfWork)
    {
        _committeeRepo = committeeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<AwardCommitteeDto?> GetByIdAsync(Guid id)
    {
        var entity = await _committeeRepo.GetByIdAsync(id);
        return entity?.ToDto();
    }

    public async Task<AwardCommitteeDto?> GetWithMembersAsync(Guid id)
    {
        var entity = await _committeeRepo.GetWithMembersAsync(id);
        return entity?.ToDto();
    }

    public async Task<AwardCommitteeDto?> GetActiveForDateAsync(Guid tenantId, DateTime date)
    {
        var entity = await _committeeRepo.GetActiveForDateAsync(tenantId, date);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AwardCommitteeDto>> GetAllAsync(Guid tenantId)
    {
        var committees = await _committeeRepo.GetByTenantAsync(tenantId);
        return committees.ToDtoList();
    }

    public async Task<IEnumerable<AwardCommitteeDto>> GetActiveCommitteesAsync(Guid tenantId)
    {
        var committees = await _committeeRepo.GetActiveCommitteesAsync(tenantId);
        return committees.ToDtoList();
    }

    public async Task<bool> HasQuorumAsync(Guid committeeId)
    {
        return await _committeeRepo.HasQuorumAsync(committeeId);
    }

    public async Task<AwardCommitteeDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardCommitteeDto dto)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _committeeRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AwardCommitteeDto> UpdateAsync(Guid id, Guid userId, UpdateAwardCommitteeDto dto)
    {
        var entity = await _committeeRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardCommittee {id} not found.");
        entity.UpdateEntity(dto, userId);
        await _committeeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _committeeRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardCommitteeMemberService : IAwardCommitteeMemberService
{
    private readonly IAwardCommitteeMemberRepository _memberRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AwardCommitteeMemberService(IAwardCommitteeMemberRepository memberRepo, IUnitOfWork unitOfWork)
    {
        _memberRepo = memberRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<AwardCommitteeMemberDto?> GetByIdAsync(Guid id)
    {
        var entity = await _memberRepo.GetByIdAsync(id);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AwardCommitteeMemberDto>> GetByCommitteeIdAsync(Guid committeeId)
    {
        var members = await _memberRepo.GetByCommitteeIdAsync(committeeId);
        return members.ToDtoList();
    }

    public async Task<IEnumerable<AwardCommitteeMemberDto>> GetActiveByCommitteeIdAsync(Guid committeeId)
    {
        var members = await _memberRepo.GetActiveByCommitteeIdAsync(committeeId);
        return members.ToDtoList();
    }

    public async Task<IEnumerable<AwardCommitteeMemberDto>> GetByEmployeeIdAsync(Guid employeeId)
    {
        var members = await _memberRepo.GetByEmployeeIdAsync(employeeId);
        return members.ToDtoList();
    }

    public async Task<bool> IsActiveMemberAsync(Guid committeeId, Guid employeeId)
    {
        return await _memberRepo.IsActiveMemberAsync(committeeId, employeeId);
    }

    public async Task<AwardCommitteeMemberDto> AddAsync(Guid committeeId, Guid userId, CreateAwardCommitteeMemberDto dto)
    {
        var entity = dto.ToEntity(committeeId, userId);
        await _memberRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AwardCommitteeMemberDto> UpdateAsync(Guid id, Guid userId, UpdateAwardCommitteeMemberDto dto)
    {
        var entity = await _memberRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardCommitteeMember {id} not found.");
        entity.UpdateEntity(dto, userId);
        await _memberRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task RemoveAsync(Guid id)
    {
        await _memberRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeactivateAsync(Guid id, Guid userId, DateTime? endDate = null)
    {
        var entity = await _memberRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardCommitteeMember {id} not found.");
        
        entity.IsActive = false;
        entity.EndDate = endDate ?? DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _memberRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AwardCommitteeReviewService : IAwardCommitteeReviewService
{
    private readonly IAwardNominationReviewRepository _reviewRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AwardCommitteeReviewService(IAwardNominationReviewRepository reviewRepo, IAwardNominationRepository nominationRepo, IUnitOfWork unitOfWork)
    {
        _reviewRepo = reviewRepo;
        _nominationRepo = nominationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<AwardCommitteeReviewDto?> GetByIdAsync(Guid id)
    {
        var entity = await _reviewRepo.GetByIdAsync(id);
        return entity?.ToDto();
    }

    public async Task<AwardCommitteeReviewDto?> GetReviewAsync(Guid nominationId, Guid reviewerId)
    {
        var entity = await _reviewRepo.GetReviewAsync(nominationId, reviewerId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AwardCommitteeReviewDto>> GetByNominationIdAsync(Guid nominationId)
    {
        var reviews = await _reviewRepo.GetByNominationIdAsync(nominationId);
        return reviews.ToDtoList();
    }

    public async Task<IEnumerable<AwardCommitteeReviewDto>> GetByReviewerIdAsync(Guid reviewerId)
    {
        var reviews = await _reviewRepo.GetByReviewerIdAsync(reviewerId);
        return reviews.ToDtoList();
    }

    public async Task<IEnumerable<AwardCommitteeReviewDto>> GetPendingReviewsAsync(Guid reviewerId)
    {
        var reviews = await _reviewRepo.GetPendingReviewsAsync(reviewerId);
        return reviews.ToDtoList();
    }

    public async Task<int> GetApprovalCountAsync(Guid nominationId)
    {
        return await _reviewRepo.GetApprovalCountAsync(nominationId);
    }

    public async Task<int> GetRejectionCountAsync(Guid nominationId)
    {
        return await _reviewRepo.GetRejectionCountAsync(nominationId);
    }

    public async Task<AwardCommitteeReviewDto> SubmitReviewAsync(Guid nominationId, Guid reviewerId, Guid userId, SubmitCommitteeReviewDto dto)
    {
        // Check if review already exists
        var existing = await _reviewRepo.GetReviewAsync(nominationId, reviewerId);
        if (existing != null)
            throw new InvalidOperationException($"Review already exists for nomination {nominationId} by reviewer {reviewerId}.");

        // Get nomination to get tenantId
        var nomination = await _nominationRepo.GetByIdAsync(nominationId)
            ?? throw new InvalidOperationException($"AwardNomination {nominationId} not found.");

        var entity = dto.ToEntity(nominationId, reviewerId, nomination.TenantId, userId);
        await _reviewRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AwardCommitteeReviewDto> UpdateReviewAsync(Guid id, Guid userId, UpdateCommitteeReviewDto dto)
    {
        var entity = await _reviewRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"AwardNominationReview {id} not found.");
        entity.UpdateEntity(dto, userId);
        await _reviewRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }
}

#endregion

#region Long Service Award Services

public class LongServiceAwardService : ILongServiceAwardService
{
    private readonly ILongServiceAwardRepository _lsaRepo;
    private readonly IUnitOfWork _unitOfWork;

    public LongServiceAwardService(ILongServiceAwardRepository lsaRepo, IUnitOfWork unitOfWork)
    {
        _lsaRepo = lsaRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<LongServiceAwardDto?> GetByIdAsync(Guid id)
    {
        var entity = await _lsaRepo.GetWithDetailsAsync(id);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<LongServiceAwardSummaryDto>> GetAllAsync(Guid tenantId)
    {
        var awards = await _lsaRepo.GetByTenantAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<PagedResult<LongServiceAwardSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, int? yearsOfService = null)
    {
        var all = await _lsaRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(lsa => lsa.Employee != null && (lsa.Employee.FirstName + " " + lsa.Employee.LastName).Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

        if (yearsOfService.HasValue)
            q = q.Where(lsa => lsa.YearsOfService == yearsOfService.Value);

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<LongServiceAwardSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<LongServiceAwardSummaryDto>> GetByEmployeeIdAsync(Guid employeeId)
    {
        var awards = await _lsaRepo.GetByEmployeeIdAsync(employeeId);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<LongServiceAwardSummaryDto>> GetUpcomingMilestonesAsync(Guid tenantId, int daysAhead = 90)
    {
        var awards = await _lsaRepo.GetUpcomingMilestonesAsync(tenantId, daysAhead);
        return awards.ToSummaryDtoList();
    }

    public async Task<IEnumerable<LongServiceAwardSummaryDto>> GetPendingProcessingAsync(Guid tenantId)
    {
        var awards = await _lsaRepo.GetPendingProcessingAsync(tenantId);
        return awards.ToSummaryDtoList();
    }

    public async Task<LongServiceAwardDto> CreateAsync(Guid tenantId, Guid userId, CreateLongServiceAwardDto dto)
    {
        // Check if award already exists for this employee and years
        var existing = await _lsaRepo.GetByEmployeeAndYearsAsync(dto.EmployeeId, dto.YearsOfService);
        if (existing != null)
            throw new InvalidOperationException($"Long service award for {dto.YearsOfService} years already exists for this employee.");

        var entity = dto.ToEntity(tenantId, userId);
        await _lsaRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _lsaRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<LongServiceAwardDto> UpdateAsync(Guid id, Guid userId, UpdateLongServiceAwardDto dto)
    {
        var entity = await _lsaRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"LongServiceAward {id} not found.");
        entity.UpdateEntity(dto, userId);
        await _lsaRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _lsaRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await _lsaRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ProcessAsync(Guid userId, ProcessLongServiceAwardDto dto)
    {
        var entity = await _lsaRepo.GetByIdAsync(dto.AwardId)
            ?? throw new InvalidOperationException($"LongServiceAward {dto.AwardId} not found.");

        entity.IsProcessed = true;
        entity.ProcessedDate = DateTime.UtcNow;
        entity.PresentationDate = dto.PresentationDate;
        entity.PresentationNotes = dto.PresentationNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _lsaRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion





