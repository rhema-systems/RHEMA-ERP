using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>Theme 11 — read + approve the salary review proposals raised from appraisal recommendations.</summary>
public class SalaryReviewProposalService : ISalaryReviewProposalService
{
    private readonly IGenericRepository<SalaryReviewProposal> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SalaryReviewProposalService> _logger;

    public SalaryReviewProposalService(
        IGenericRepository<SalaryReviewProposal> repository,
        IUnitOfWork unitOfWork,
        ILogger<SalaryReviewProposalService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<SalaryReviewProposalDto>> GetAllAsync(SalaryReviewProposalStatus? status = null, CancellationToken cancellationToken = default)
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

    public async Task<SalaryReviewProposalDto> SetStatusAsync(Guid id, SalaryReviewProposalStatus status, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Salary review proposal '{id}' not found.");

        entity.Status = status;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary review proposal {Id} set to {Status}", id, status);

        var reloaded = await _repository.GetQueryable()
            .Include(p => p.Employee)
            .Include(p => p.SourceAppraisal)
            .FirstAsync(p => p.Id == id, cancellationToken);
        return ToDto(reloaded);
    }

    private static SalaryReviewProposalDto ToDto(SalaryReviewProposal p) => new()
    {
        Id = p.Id,
        EmployeeId = p.EmployeeId,
        EmployeeName = p.Employee?.FullName,
        SourceAppraisalId = p.SourceAppraisalId,
        AppraisalNumber = p.SourceAppraisal?.AppraisalNumber,
        ProposalType = p.ProposalType,
        ProposedPercent = p.ProposedPercent,
        ProposedAmount = p.ProposedAmount,
        Status = p.Status,
        Notes = p.Notes
    };
}
