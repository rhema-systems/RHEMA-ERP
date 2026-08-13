using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingRequestService : ITrainingRequestService
{
    private readonly ITrainingRequestRepository _requestRepository;
    private readonly IGenericRepository<TrainingProgram> _programRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingRequestService> _logger;

    private readonly INumberSequenceService _numberSequence;

    public TrainingRequestService(
        ITrainingRequestRepository requestRepository,
        IGenericRepository<TrainingProgram> programRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        INumberSequenceService numberSequence,
        ILogger<TrainingRequestService> logger)
    {
        _requestRepository = requestRepository;
        _programRepository = programRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _numberSequence = numberSequence;
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

    // A request owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<TrainingRequest> GetOwnedAsync(Guid id)
    {
        var entity = await _requestRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training request with ID '{id}' not found.");
        return entity;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<TrainingRequestDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<TrainingRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _requestRepository.GetByRequestNumberAsync(requestNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<PagedResult<TrainingRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Scoping the query before the count matters: an unscoped CountAsync reports other tenants' rows
        // in TotalCount and breaks the pager.
        var query = _requestRepository.GetQueryable().Where(r => r.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);
        // Eager-load navigations the summary DTO reads (employee + linked program names).
        var items = await query
            .Include(r => r.Employee)
            .Include(r => r.LinkedProgram)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<TrainingRequestSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<TrainingRequestSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requestRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requestRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingRequestSummaryDto>> GetByStatusAsync(TrainingRequestStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requestRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requestRepository.GetByStatusAsync(TrainingRequestStatus.Submitted);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<TrainingRequestDto> CreateAsync(CreateTrainingRequestDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, createdByUserId);
        entity.RequestNumber = await GenerateRequestNumberAsync(cancellationToken);
        entity.Status = TrainingRequestStatus.Draft;

        await _requestRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training request created: {RequestNumber}", entity.RequestNumber);

        // Freshly written: no Employee/LinkedProgram loaded, so map from a re-read instead.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainingRequestDto> UpdateAsync(UpdateTrainingRequestDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        if (entity.Status != TrainingRequestStatus.Draft)
            throw new InvalidOperationException("Only draft training requests can be updated.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training request {RequestId} updated", updateDto.Id);

        // A changed LinkedProgramId does not refresh the loaded navigation — re-read.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != TrainingRequestStatus.Draft)
            throw new InvalidOperationException("Only draft training requests can be deleted.");

        await _requestRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training request {RequestId} deleted", id);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> SubmitAsync(Guid requestId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(requestId);

        if (entity.Status != TrainingRequestStatus.Draft)
            throw new InvalidOperationException($"Training request cannot be submitted from status '{entity.Status}'.");

        entity.Status = TrainingRequestStatus.Submitted;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training request {RequestNumber} submitted", entity.RequestNumber);

        return true;
    }

    public async Task<TrainingRequestDto> ApproveAsync(ApproveTrainingRequestDto dto, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.RequestId);

        if (entity.Status != TrainingRequestStatus.Submitted)
            throw new InvalidOperationException($"Training request cannot be approved from status '{entity.Status}'.");

        if (dto.LinkedProgramId.HasValue && dto.LinkedProgramId.Value != Guid.Empty)
        {
            var program = await _programRepository.GetByIdAsync(dto.LinkedProgramId.Value);
            if (program == null || program.TenantId != entity.TenantId)
                throw new ArgumentException($"Training program with ID '{dto.LinkedProgramId}' not found.");
        }

        entity.Status = TrainingRequestStatus.Approved;
        entity.ApprovedById = approvedById;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.LinkedProgramId = dto.LinkedProgramId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = approvedById.ToString();

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training request {RequestNumber} approved by {ApprovedById}", entity.RequestNumber, approvedById);

        return entity.ToDto();
    }

    public async Task<bool> RejectAsync(RejectTrainingRequestDto dto, Guid rejectedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.RequestId);

        if (entity.Status != TrainingRequestStatus.Submitted)
            throw new InvalidOperationException($"Training request cannot be rejected from status '{entity.Status}'.");

        entity.Status = TrainingRequestStatus.Rejected;
        entity.RejectionReason = dto.RejectionReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = rejectedById.ToString();

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training request {RequestNumber} rejected", entity.RequestNumber);

        return true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task<string> GenerateRequestNumberAsync(CancellationToken ct)
        => _numberSequence.GenerateAsync("TREQ", ct);
}
