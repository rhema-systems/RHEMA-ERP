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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmploymentActionProposalService> _logger;

    public EmploymentActionProposalService(
        IGenericRepository<EmploymentActionProposal> repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmploymentActionProposalService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A proposal owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<EmploymentActionProposal> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Employment action proposal '{id}' not found.");
        return entity;
    }

    public async Task<IEnumerable<EmploymentActionProposalDto>> GetAllAsync(EmploymentActionProposalStatus? status = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable()
            .Where(p => p.TenantId == tenantId)
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
        var entity = await GetOwnedAsync(id);

        entity.Status = status;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employment action proposal {Id} set to {Status}", id, status);

        var reloaded = await _repository.GetQueryable()
            .Where(p => p.TenantId == entity.TenantId)
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
