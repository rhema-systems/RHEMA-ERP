using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>Read + status-manage the employment-action proposals raised from appraisal recommendations.</summary>
public class EmploymentActionProposalService : IEmploymentActionProposalService
{
    private readonly IGenericRepository<EmploymentActionProposal> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmploymentActionProposalService> _logger;

    public EmploymentActionProposalService(
        IGenericRepository<EmploymentActionProposal> repository,
        IUnitOfWork unitOfWork,
        ILogger<EmploymentActionProposalService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<EmploymentActionProposalDto>> GetAllAsync(EmploymentActionProposalStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.GetQueryable()
            .Include(p => p.Employee)
            .Include(p => p.SourceAppraisal)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var items = await query.OrderByDescending(p => p.CreatedAt).Take(500).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<EmploymentActionProposalDto> SetStatusAsync(Guid id, EmploymentActionProposalStatus status, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Employment action proposal '{id}' not found.");

        entity.Status = status;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employment action proposal {Id} set to {Status}", id, status);

        var reloaded = await _repository.GetQueryable()
            .Include(p => p.Employee)
            .Include(p => p.SourceAppraisal)
            .FirstAsync(p => p.Id == id, cancellationToken);
        return ToDto(reloaded);
    }

    private static EmploymentActionProposalDto ToDto(EmploymentActionProposal p) => new()
    {
        Id = p.Id,
        EmployeeId = p.EmployeeId,
        EmployeeName = p.Employee?.FullName,
        SourceAppraisalId = p.SourceAppraisalId,
        AppraisalNumber = p.SourceAppraisal?.AppraisalNumber,
        ActionType = p.ActionType,
        Status = p.Status,
        Notes = p.Notes
    };
}
