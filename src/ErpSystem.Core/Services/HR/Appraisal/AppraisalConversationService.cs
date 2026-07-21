using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class AppraisalConversationService : IAppraisalConversationService
{
    private readonly IGenericRepository<AppraisalConversation> _conversationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalConversationService> _logger;

    public AppraisalConversationService(
        IGenericRepository<AppraisalConversation> conversationRepository,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalConversationService> logger)
    {
        _conversationRepository = conversationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private IQueryable<AppraisalConversation> BaseQuery => _conversationRepository.GetQueryable()
        .Include(c => c.Appraisal)
            .ThenInclude(a => a.Employee)
        .Include(c => c.ScheduledBy)
        .Include(c => c.ConductedBy);

    public async Task<AppraisalConversationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Conversation with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalConversationDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(c => c.AppraisalId == appraisalId)
            .OrderByDescending(c => c.ScheduledDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<AppraisalConversationDto>> GetByTypeAsync(Guid appraisalId, ConversationType type, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(c => c.AppraisalId == appraisalId && c.Type == type)
            .OrderByDescending(c => c.ScheduledDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<AppraisalConversationDto>> GetScheduledByManagerAsync(Guid managerId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(c => c.ScheduledById == managerId && !c.IsCompleted && c.ScheduledDate >= DateTime.UtcNow)
            .OrderBy(c => c.ScheduledDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<AppraisalConversationDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.OrderByDescending(c => c.ScheduledDate);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AppraisalConversationDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AppraisalConversationDto> CreateAsync(CreateAppraisalConversationDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();
        entity.IsCompleted = false;

        await _conversationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal conversation created: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<AppraisalConversationDto> UpdateAsync(UpdateAppraisalConversationDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _conversationRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Conversation with ID '{updateDto.Id}' not found.");

        if (entity.IsCompleted)
            throw new InvalidOperationException("Cannot update a completed conversation.");

        updateDto.UpdateEntity(entity);
        await _conversationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal conversation updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _conversationRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Conversation with ID '{id}' not found.");

        await _conversationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Appraisal conversation deleted: {Id}", id);
        return true;
    }

    public async Task<AppraisalConversationDto> CompleteAsync(
        Guid conversationId, string? postMeetingNotes, string? keyTakeaways,
        CancellationToken cancellationToken = default)
    {
        var entity = await _conversationRepository.GetByIdAsync(conversationId);
        if (entity == null)
            throw new ArgumentException($"Conversation with ID '{conversationId}' not found.");

        if (entity.IsCompleted)
            throw new InvalidOperationException("Conversation is already completed.");

        entity.IsCompleted = true;
        entity.HeldDate = DateTime.UtcNow;
        entity.PostMeetingNotes = postMeetingNotes;
        entity.KeyTakeaways = keyTakeaways;

        await _conversationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal conversation {Id} completed", conversationId);
        return await GetByIdAsync(conversationId, cancellationToken);
    }
}
