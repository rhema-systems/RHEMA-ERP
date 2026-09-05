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

public class PerformanceJournalService : IPerformanceJournalService
{
    private readonly IGenericRepository<PerformanceJournalEntry> _journalRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PerformanceJournalService> _logger;

    public PerformanceJournalService(
        IGenericRepository<PerformanceJournalEntry> journalRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PerformanceJournalService> logger)
    {
        _journalRepository = journalRepository;
        _cycleRepository = cycleRepository;
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

    // A journal entry owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<PerformanceJournalEntry> GetOwnedAsync(Guid id)
    {
        var entity = await _journalRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Journal entry with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<PerformanceJournalEntry> BaseQuery
    {
        get
        {
            var tenantId = GetTenantId();
            return _journalRepository.GetQueryable()
                .Where(j => j.TenantId == tenantId)
                .Include(j => j.Owner)
                .Include(j => j.SubjectEmployee)
                .Include(j => j.AppraisalCycle)
                .Include(j => j.RelatedGoal);
        }
    }

    public async Task<PerformanceJournalEntryDto> GetByIdAsync(
        Guid id, Guid requestingEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Journal entry with ID '{id}' not found.");

        // Enforce privacy: private entries are only visible to their owner
        if (entity.IsPrivate && entity.OwnerId != requestingEmployeeId)
            throw new UnauthorizedAccessException("This journal entry is private.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<PerformanceJournalEntryDto>> GetByOwnerIdAsync(
        Guid ownerId, Guid? cycleId = null, bool includePrivate = true, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.Where(j => j.OwnerId == ownerId);
        if (!includePrivate)
            query = query.Where(j => !j.IsPrivate);
        if (cycleId.HasValue)
            query = query.Where(j => j.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderByDescending(j => j.EntryDate).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<PerformanceJournalEntryDto>> GetAboutSubjectAsync(
        Guid managerId, Guid subjectEmployeeId, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        // Entries the manager authored about the given subject.
        //
        // No privacy filter here, deliberately: managerId is the caller, taken from their token, so
        // every row returned is one they wrote themselves. Filtering on !IsPrivate made sense when
        // the manager id came from the route and someone else could ask — it now only hid a
        // manager's own private notes from the manager.
        var query = BaseQuery.Where(j =>
            j.OwnerId == managerId &&
            j.SubjectEmployeeId == subjectEmployeeId);

        if (cycleId.HasValue)
            query = query.Where(j => j.AppraisalCycleId == cycleId.Value);

        var entities = await query.OrderByDescending(j => j.EntryDate).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<PerformanceJournalEntryDto>> GetSharedWithManagerAsync(
        Guid ownerId, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        // Entries not marked private are shared with manager
        var query = BaseQuery.Where(j => j.OwnerId == ownerId && !j.IsPrivate);
        if (cycleId.HasValue)
            query = query.Where(j => j.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderByDescending(j => j.EntryDate).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<PerformanceJournalEntryDto>> GetPagedAsync(
        Guid ownerId, int pageNumber, int pageSize, Guid? cycleId = null, bool includePrivate = true, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.Where(j => j.OwnerId == ownerId);
        if (!includePrivate)
            query = query.Where(j => !j.IsPrivate);
        if (cycleId.HasValue)
            query = query.Where(j => j.AppraisalCycleId == cycleId.Value);
        query = query.OrderByDescending(j => j.EntryDate);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<PerformanceJournalEntryDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PerformanceJournalEntryDto> CreateAsync(
        CreatePerformanceJournalEntryDto createDto, Guid ownerId, CancellationToken cancellationToken = default)
    {
        // Gate: private journaling must be enabled for this cycle.
        if (createDto.IsPrivate)
        {
            var tenantId = GetTenantId();
            var settings = await _cycleRepository.GetQueryable(c => c.Id == createDto.AppraisalCycleId && c.TenantId == tenantId)
                .Include(c => c.AppraisalSettings)
                .Select(c => c.AppraisalSettings)
                .FirstOrDefaultAsync(cancellationToken);
            if (settings is { EnablePrivateJournal: false })
                throw new InvalidOperationException("Private journaling is not enabled for this appraisal cycle.");
        }

        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        // The author is the caller, never the payload — a journal entry is evidence about a named
        // person and an attributable note; letting the body name its own owner would let anyone
        // plant one in someone else's journal.
        entity.OwnerId = ownerId;
        entity.EntryDate = DateTime.UtcNow;

        await _journalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance journal entry created: {Id} by owner {OwnerId}", entity.Id, entity.OwnerId);
        return await GetByIdAsync(entity.Id, entity.OwnerId, cancellationToken);
    }

    public async Task<PerformanceJournalEntryDto> UpdateAsync(
        UpdatePerformanceJournalEntryDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        updateDto.UpdateEntity(entity);
        await _journalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance journal entry updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, entity.OwnerId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await _journalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Performance journal entry deleted: {Id}", id);
        return true;
    }

    public async Task<bool> SetPrivacyAsync(
        Guid id, bool isPrivate, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        entity.IsPrivate = isPrivate;
        // No IsSharedWithManager field — IsPrivate controls visibility

        await _journalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Journal entry {Id} privacy set to {IsPrivate}", id, isPrivate);
        return true;
    }

    public async Task<IEnumerable<TeamJournalListDto>> GetTeamJournalAsync(
        Guid managerId,
        IEnumerable<Guid> directReportIds,
        Guid? cycleId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var drList = directReportIds.ToList();
        var tenantId = GetTenantId();

        var query = _journalRepository.GetQueryable()
            .Where(j => j.TenantId == tenantId)
            .Include(j => j.Owner)
            .Include(j => j.SubjectEmployee)
            .Include(j => j.RelatedGoal)
            .Where(j =>
                // Manager-written entries about a direct report (always visible to manager)
                (j.OwnerId == managerId && j.SubjectEmployeeId.HasValue && drList.Contains(j.SubjectEmployeeId.Value))
                ||
                // Employee-written entries that are shared (not private)
                (drList.Contains(j.OwnerId) && !j.IsPrivate));

        if (cycleId.HasValue)
            query = query.Where(j => j.AppraisalCycleId == cycleId.Value);
        if (fromDate.HasValue)
            query = query.Where(j => j.EntryDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(j => j.EntryDate <= toDate.Value.AddDays(1).AddTicks(-1));

        var entities = await query.OrderByDescending(j => j.EntryDate).ToListAsync(cancellationToken);

        return entities.Select(j => new TeamJournalListDto
        {
            Id                  = j.Id,
            Title               = j.Title,
            EntryDate           = j.EntryDate,
            WrittenById         = j.OwnerId,
            WrittenByName       = j.Owner?.FullName ?? string.Empty,
            IsWrittenByManager  = j.OwnerId == managerId,
            SubjectEmployeeId   = j.SubjectEmployee != null ? j.SubjectEmployee.Id : j.OwnerId,
            SubjectEmployeeName = j.SubjectEmployee?.FullName ?? j.Owner?.FullName ?? string.Empty,
            IsPrivate           = j.IsPrivate,
            RelatedGoalTitle    = j.RelatedGoal?.Title,
            PreviewText         = j.Body.Length > 220 ? j.Body[..220] + "\u2026" : j.Body
        }).ToList();
    }
}
