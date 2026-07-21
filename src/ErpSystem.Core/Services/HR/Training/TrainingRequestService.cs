using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingRequestService : ITrainingRequestService
{
    private readonly ITrainingRequestRepository _requestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingRequestService> _logger;

    private readonly INumberSequenceService _numberSequence;

    public TrainingRequestService(
        ITrainingRequestRepository requestRepository,
        IUnitOfWork unitOfWork,
        INumberSequenceService numberSequence,
        ILogger<TrainingRequestService> logger)
    {
        _requestRepository = requestRepository;
        _unitOfWork = unitOfWork;
        _numberSequence = numberSequence;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<TrainingRequestDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training request with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByRequestNumberAsync(requestNumber);
        return entity?.ToDto();
    }

    public async Task<PagedResult<TrainingRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _requestRepository.GetQueryable();
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
        var entities = await _requestRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _requestRepository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingRequestSummaryDto>> GetByStatusAsync(TrainingRequestStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _requestRepository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _requestRepository.GetByStatusAsync(TrainingRequestStatus.Submitted);
        return entities.ToSummaryDtoList();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<TrainingRequestDto> CreateAsync(CreateTrainingRequestDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, createdByUserId);
        entity.RequestNumber = await GenerateRequestNumberAsync(cancellationToken);
        entity.Status = TrainingRequestStatus.Draft;

        await _requestRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training request created: {RequestNumber}", entity.RequestNumber);

        return entity.ToDto();
    }

    public async Task<TrainingRequestDto> UpdateAsync(UpdateTrainingRequestDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Training request with ID '{updateDto.Id}' not found.");

        if (entity.Status != TrainingRequestStatus.Draft)
            throw new InvalidOperationException("Only draft training requests can be updated.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training request {RequestId} updated", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training request with ID '{id}' not found.");

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
        var entity = await _requestRepository.GetByIdAsync(requestId);

        if (entity == null)
            throw new ArgumentException($"Training request with ID '{requestId}' not found.");

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

    public async Task<TrainingRequestDto> ApproveAsync(ApproveTrainingRequestDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(dto.RequestId);

        if (entity == null)
            throw new ArgumentException($"Training request with ID '{dto.RequestId}' not found.");

        if (entity.Status != TrainingRequestStatus.Submitted)
            throw new InvalidOperationException($"Training request cannot be approved from status '{entity.Status}'.");

        entity.Status = TrainingRequestStatus.Approved;
        entity.ApprovedById = dto.ApprovedById;
        entity.ApprovalDate = dto.ApprovalDate;
        entity.LinkedProgramId = dto.LinkedProgramId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = dto.ApprovedById.ToString();

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training request {RequestNumber} approved by {ApprovedById}", entity.RequestNumber, dto.ApprovedById);

        return entity.ToDto();
    }

    public async Task<bool> RejectAsync(RejectTrainingRequestDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(dto.RequestId);

        if (entity == null)
            throw new ArgumentException($"Training request with ID '{dto.RequestId}' not found.");

        if (entity.Status != TrainingRequestStatus.Submitted)
            throw new InvalidOperationException($"Training request cannot be rejected from status '{entity.Status}'.");

        entity.Status = TrainingRequestStatus.Rejected;
        entity.RejectionReason = dto.RejectionReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = dto.RejectedById.ToString();

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training request {RequestNumber} rejected", entity.RequestNumber);

        return true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task<string> GenerateRequestNumberAsync(CancellationToken ct)
        => _numberSequence.GenerateAsync("TREQ", ct);
}
