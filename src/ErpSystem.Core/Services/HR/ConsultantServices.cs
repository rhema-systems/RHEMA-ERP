using System.Net;
using System.Text;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// CONSULTANT CLIENT SERVICE
// ============================================================================

#region Consultant Client Service

public class ConsultantClientService : IConsultantClientService
{
    private readonly IConsultantClientRepository _repository;
    private readonly IClientEngagementRepository _engagementRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConsultantClientService> _logger;

    public ConsultantClientService(
        IConsultantClientRepository repository,
        IClientEngagementRepository engagementRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ConsultantClientService> logger)
    {
        _repository = repository;
        _engagementRepository = engagementRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<ConsultantClient> GetOwnedClientAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Client '{id}' not found.");
        return entity;
    }

    private async Task<ClientEngagement> GetOwnedEngagementAsync(Guid id)
    {
        var entity = await _engagementRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Engagement '{id}' not found.");
        return entity;
    }

    public async Task<ConsultantClientDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Client '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<ConsultantClientDto?> GetByClientCodeAsync(string clientCode, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetQueryable()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ClientCode == clientCode && !c.IsDeleted, ct);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<ConsultantClientSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.FindAsync(c => c.TenantId == tenantId && !c.IsDeleted);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ConsultantClientSummaryDto>> GetActiveClientsAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetActiveClientsAsync())
            .Where(c => c.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<ConsultantClientSummaryDto>> GetByIndustryAsync(string industry, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.FindAsync(c => c.TenantId == tenantId && c.Industry == industry && !c.IsDeleted);
        return entities.ToSummaryDtoList();
    }

    public async Task<ConsultantClientDto> GetWithEngagementsAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Client '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<PagedResult<ConsultantClientSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // Navigation names appear on the summary DTO, so they must be loaded.
        var query = _repository.GetQueryable().Include(c => c.Country).Include(c => c.Engagements).Where(c => c.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.ClientName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ConsultantClientSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ConsultantClientDto> CreateAsync(CreateConsultantClientDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var resolvedTenantId = RequireCurrentTenant(tenantId);

        var codeExists = await _repository.GetQueryable()
            .AnyAsync(c => c.TenantId == resolvedTenantId && c.ClientCode == dto.ClientCode && !c.IsDeleted, ct);
        if (codeExists)
            throw new InvalidOperationException($"A client with code '{dto.ClientCode}' already exists.");

        var entity = dto.ToEntity(resolvedTenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Consultant client created: {Name} ({Code})", entity.ClientName, entity.ClientCode);
        return entity.ToDto();
    }

    public async Task<ConsultantClientDto> UpdateAsync(UpdateConsultantClientDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedClientAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedClientAsync(id);
        var tenantId = GetTenantId();

        var hasActiveEngagements = await _engagementRepository
            .GetQueryable()
            .AnyAsync(e => e.TenantId == tenantId && e.ClientId == id && e.Status == ClientEngagementStatus.Active && !e.IsDeleted, ct);

        if (hasActiveEngagements)
            throw new InvalidOperationException("Cannot delete a client that has active engagements.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ClientEngagementDto> AddEngagementAsync(CreateClientEngagementDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var resolvedTenantId = RequireCurrentTenant(tenantId);
        await GetOwnedClientAsync(dto.ClientId);

        var engagement = dto.ToEntity(resolvedTenantId, userId);
        engagement.EngagementCode = await GenerateEngagementCodeAsync(resolvedTenantId, ct);
        await _engagementRepository.AddAsync(engagement);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Engagement {Code} created for client {ClientId}", engagement.EngagementCode, dto.ClientId);
        return engagement.ToDto();
    }

    public async Task<IEnumerable<ClientEngagementSummaryDto>> GetEngagementsAsync(Guid clientId, CancellationToken ct = default)
    {
        await GetOwnedClientAsync(clientId);
        var tenantId = GetTenantId();
        return (await _engagementRepository.GetByClientIdAsync(clientId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<ClientEngagementDto> GetEngagementByIdAsync(Guid engagementId, CancellationToken ct = default)
    {
        var entity = await GetOwnedEngagementAsync(engagementId);
        return entity.ToDto();
    }

    public async Task<ClientEngagementDto> UpdateEngagementAsync(UpdateClientEngagementDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedEngagementAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _engagementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteEngagementAsync(Guid engagementId, CancellationToken ct = default)
    {
        var entity = await GetOwnedEngagementAsync(engagementId);

        if (entity.Status == ClientEngagementStatus.Active)
            throw new InvalidOperationException("An active engagement cannot be deleted.");

        await _engagementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerateEngagementCodeAsync(Guid tenantId, CancellationToken ct)
    {
        var count = await _engagementRepository.GetQueryable()
            .CountAsync(e => e.TenantId == tenantId, ct);
        return $"ENG-{DateTime.UtcNow:yyyy}-{(count + 1):D5}";
    }
}

#endregion

// ============================================================================
// CLIENT ENGAGEMENT SERVICE
// ============================================================================

#region Client Engagement Service

public class ClientEngagementService : IClientEngagementService
{
    private readonly IClientEngagementRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ClientEngagementService> _logger;

    public ClientEngagementService(
        IClientEngagementRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ClientEngagementService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<ClientEngagement> GetOwnedEngagementAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Engagement '{id}' not found.");
        return entity;
    }

    public async Task<ClientEngagementDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedEngagementAsync(id);
        return entity.ToDto();
    }

    public async Task<ClientEngagementDto?> GetByEngagementCodeAsync(string engagementCode, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetQueryable()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.EngagementCode == engagementCode && !e.IsDeleted, ct);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<ClientEngagementSummaryDto>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetByClientIdAsync(clientId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<ClientEngagementSummaryDto>> GetByConsultantIdAsync(Guid consultantEmployeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetByConsultantIdAsync(consultantEmployeeId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<ClientEngagementSummaryDto>> GetByStatusAsync(ClientEngagementStatus status, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.FindAsync(e => e.TenantId == tenantId && e.Status == status && !e.IsDeleted);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ClientEngagementSummaryDto>> GetActiveEngagementsAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetActiveEngagementsAsync())
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<ClientEngagementSummaryDto>> GetByBillingCycleAsync(BillingCycle cycle, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.FindAsync(e => e.TenantId == tenantId && e.BillingCycle == cycle && !e.IsDeleted);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ClientEngagementSummaryDto>> GetEngagementsEndingWithinAsync(int days, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetExpiringSoonAsync(days))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<PagedResult<ClientEngagementSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // Navigation names appear on the summary DTO, so they must be loaded.
        var query = _repository.GetQueryable().Include(e => e.Client).Include(e => e.Consultant).Where(e => e.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ClientEngagementSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ClientEngagementDto> CreateAsync(CreateClientEngagementDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var resolvedTenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(resolvedTenantId, userId);
        entity.EngagementCode = $"ENG-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        entity.Status = ClientEngagementStatus.Draft;
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Engagement {Code} created for client {ClientId}", entity.EngagementCode, entity.ClientId);
        return entity.ToDto();
    }

    public async Task<ClientEngagementDto> UpdateAsync(UpdateClientEngagementDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedEngagementAsync(dto.Id);

        if (entity.Status is ClientEngagementStatus.Completed or ClientEngagementStatus.Terminated)
            throw new InvalidOperationException("A completed or terminated engagement cannot be edited.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<ClientEngagementDto> ActivateAsync(Guid engagementId, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedEngagementAsync(engagementId);

        if (entity.Status != ClientEngagementStatus.Draft)
            throw new InvalidOperationException("Only draft engagements can be activated.");

        entity.Status = ClientEngagementStatus.Active;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Engagement {Code} activated by {UserId}", entity.EngagementCode, userId);
        return entity.ToDto();
    }

    public async Task<ClientEngagementDto> CompleteAsync(Guid engagementId, DateOnly actualEndDate, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedEngagementAsync(engagementId);

        if (entity.Status != ClientEngagementStatus.Active)
            throw new InvalidOperationException("Only active engagements can be completed.");

        entity.Status = ClientEngagementStatus.Completed;
        entity.ActualEndDate = actualEndDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Engagement {Code} completed on {Date}", entity.EngagementCode, actualEndDate);
        return entity.ToDto();
    }

    public async Task<ClientEngagementDto> SuspendAsync(Guid engagementId, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedEngagementAsync(engagementId);

        if (entity.Status != ClientEngagementStatus.Active)
            throw new InvalidOperationException("Only active engagements can be suspended.");

        entity.Status = ClientEngagementStatus.Suspended;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Engagement {Code} suspended by {UserId}", entity.EngagementCode, userId);
        return entity.ToDto();
    }

    public async Task<ClientEngagementDto> ResumeAsync(Guid engagementId, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedEngagementAsync(engagementId);

        if (entity.Status != ClientEngagementStatus.Suspended)
            throw new InvalidOperationException("Only suspended engagements can be resumed.");

        entity.Status = ClientEngagementStatus.Active;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Engagement {Code} resumed by {UserId}", entity.EngagementCode, userId);
        return entity.ToDto();
    }

    public async Task<ClientEngagementDto> TerminateAsync(
        Guid engagementId,
        string? reason,
        Guid userId,
        CancellationToken ct = default)
    {
        var entity = await GetOwnedEngagementAsync(engagementId);

        if (entity.Status is not (ClientEngagementStatus.Active or ClientEngagementStatus.Suspended))
            throw new InvalidOperationException("Only active or suspended engagements can be terminated.");

        entity.Status = ClientEngagementStatus.Terminated;
        entity.ActualEndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            entity.Notes = string.IsNullOrWhiteSpace(entity.Notes)
                ? $"Terminated: {reason.Trim()}"
                : $"{entity.Notes}\nTerminated: {reason.Trim()}";
        }

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Engagement {Code} terminated by {UserId}", entity.EngagementCode, userId);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedEngagementAsync(id);

        if (entity.Status == ClientEngagementStatus.Active)
            throw new InvalidOperationException("An active engagement cannot be deleted.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

// ============================================================================
// CONSULTANT TIMESHEET SERVICE
// ============================================================================

#region Consultant Timesheet Service

public class ConsultantTimesheetService : IConsultantTimesheetService
{
    private const int MaxEmailEntryRows = 15;

    /// <summary>Workflow entity type; must match the catalog entry and the status adapter.</summary>
    private const string EntityType = "ConsultantTimesheet";

    private readonly IConsultantTimesheetRepository _repository;
    private readonly IConsultantTimesheetEntryRepository _entryRepository;
    private readonly IClientTimesheetConfirmationRepository _confirmationRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConsultantTimesheetService> _logger;
    private readonly IEmailService _email;
    private readonly string _portalBaseUrl;

    public ConsultantTimesheetService(
        IConsultantTimesheetRepository repository,
        IConsultantTimesheetEntryRepository entryRepository,
        IClientTimesheetConfirmationRepository confirmationRepository,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IUnitOfWork unitOfWork,
        ILogger<ConsultantTimesheetService> logger,
        IEmailService email,
        IOptions<CandidatePortalOptions> portalOptions)
    {
        _repository = repository;
        _entryRepository = entryRepository;
        _confirmationRepository = confirmationRepository;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _email = email;
        _portalBaseUrl = (portalOptions.Value.PortalUrl ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(_portalBaseUrl))
            _portalBaseUrl = "http://localhost:5085";
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<ConsultantTimesheet> GetOwnedTimesheetAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Timesheet '{id}' not found.");
        return entity;
    }

    private async Task<ConsultantTimesheetEntry> GetOwnedEntryAsync(Guid id)
    {
        var entity = await _entryRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Timesheet entry '{id}' not found.");
        return entity;
    }

    public async Task<ConsultantTimesheetDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Timesheet '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<ConsultantTimesheetDto?> GetByTimesheetNumberAsync(string timesheetNumber, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetQueryable()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.TimesheetNumber == timesheetNumber && !t.IsDeleted, ct);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetByConsultantIdAsync(Guid consultantEmployeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetByConsultantIdAsync(consultantEmployeeId))
            .Where(t => t.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetByEngagementIdAsync(Guid engagementId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetByEngagementIdAsync(engagementId))
            .Where(t => t.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetByStatusAsync(TimesheetStatus status, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetByStatusAsync(status))
            .Where(t => t.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetByPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetByPeriodAsync(from, to))
            .Where(t => t.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetPendingApprovalAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetByStatusAsync(TimesheetStatus.Submitted))
            .Where(t => t.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetPendingClientConfirmationAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetByStatusAsync(TimesheetStatus.SentToClient))
            .Where(t => t.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetApprovedForInvoicingAsync(Guid? clientId = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        IEnumerable<ConsultantTimesheet> entities;
        if (clientId.HasValue)
            entities = await _repository.GetReadyForInvoicingAsync(clientId.Value);
        else
            entities = await _repository.GetByStatusAsync(TimesheetStatus.ClientConfirmed);
        return entities.Where(t => t.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<PagedResult<ConsultantTimesheetSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // Navigation names appear on the summary DTO, so they must be loaded.
        var query = _repository.GetQueryable().Include(t => t.Consultant).Include(t => t.Client).Where(t => t.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.PeriodStartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ConsultantTimesheetSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ConsultantTimesheetDto> CreateAsync(CreateConsultantTimesheetDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var resolvedTenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(resolvedTenantId, userId);
        entity.TimesheetNumber = await GenerateTimesheetNumberAsync(resolvedTenantId, ct);
        entity.Status = TimesheetStatus.Draft;
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Timesheet {Number} created for consultant {ConsultantId}", entity.TimesheetNumber, entity.ConsultantId);
        return entity.ToDto();
    }

    public async Task<ConsultantTimesheetDto> UpdateAsync(UpdateConsultantTimesheetDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedTimesheetAsync(dto.Id);

        if (entity.Status != TimesheetStatus.Draft && entity.Status != TimesheetStatus.Rejected)
            throw new InvalidOperationException("Only draft or rejected timesheets can be edited.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    /// <summary>
    /// Starts the approval workflow. Unlike the attendance requests in this module, a
    /// timesheet does have a Draft state, so submission is an explicit user action and the
    /// engine's outcome — applied by <c>ConsultantTimesheetWorkflowStatusAdapter</c> —
    /// decides whether it lands on Submitted or straight on Approved.
    /// </summary>
    public async Task<ConsultantTimesheetDto> SubmitAsync(Guid timesheetId, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedTimesheetAsync(timesheetId);

        if (entity.Status != TimesheetStatus.Draft && entity.Status != TimesheetStatus.Rejected)
            throw new InvalidOperationException("Only draft or rejected timesheets can be submitted.");

        // Belt and braces: TotalHours is maintained on every entry change, but submission is
        // the point where the figure starts to matter (it is what the client confirms and
        // what gets invoiced), and it repairs any row created before that was maintained.
        await RecalculateTotalHoursAsync(entity.Id, userId, ct);

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, entity.Id);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start approval workflow.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplySubmitOutcome(entity, workflowResult.Outcome, userId);

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Timesheet {Number} submitted by {UserId}", entity.TimesheetNumber, userId);
        return entity.ToDto();
    }

    public async Task<ConsultantTimesheetDto> ApproveAsync(Guid timesheetId, string? comments, Guid userId, CancellationToken ct = default)
        => await ProcessDecisionAsync(timesheetId, "Approve", comments, userId, ct);

    public async Task<ConsultantTimesheetDto> RejectAsync(Guid timesheetId, string rejectionReason, Guid userId, CancellationToken ct = default)
        => await ProcessDecisionAsync(timesheetId, "Reject", rejectionReason, userId, ct);

    private async Task<ConsultantTimesheetDto> ProcessDecisionAsync(
        Guid timesheetId,
        string action,
        string? comments,
        Guid employeeId,
        CancellationToken ct)
    {
        var entity = await GetOwnedTimesheetAsync(timesheetId);

        if (entity.Status != TimesheetStatus.Submitted)
            throw new InvalidOperationException("Only submitted timesheets can be decided.");

        var isReject = string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase);
        var decisionText = isReject && string.IsNullOrWhiteSpace(comments) ? "Rejected" : comments;

        // The workflow engine keys approvers off ApplicationUser.Id; the controller hands
        // this service the caller's Employee id.
        var currentUserId = _currentUserProvider.UserId;
        if (currentUserId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(EntityType, entity.Id, currentUserId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            EntityType, entity.Id, currentUserId, action, decisionText);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the decision.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(entity, workflowResult.Outcome, employeeId, isReject ? decisionText : null);

        if (!string.IsNullOrWhiteSpace(decisionText))
            entity.Notes = decisionText;

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = employeeId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Timesheet {Number} decision '{Action}' processed by {UserId}; outcome {Outcome}",
            entity.TimesheetNumber, action, currentUserId, workflowResult.Outcome);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedTimesheetAsync(id);

        if (entity.Status == TimesheetStatus.Approved)
            throw new InvalidOperationException("An approved timesheet cannot be deleted.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ConsultantTimesheetEntryDto> AddEntryAsync(CreateConsultantTimesheetEntryDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var resolvedTenantId = RequireCurrentTenant(tenantId);
        await GetOwnedTimesheetAsync(dto.TimesheetId);

        var entity = dto.ToEntity(resolvedTenantId, userId);
        await _entryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        await RecalculateTotalHoursAsync(dto.TimesheetId, userId, ct);
        return entity.ToDto();
    }

    /// <summary>
    /// Re-sums the timesheet's entries into <c>ConsultantTimesheet.TotalHours</c>.
    ///
    /// That column is the figure the client sees on the confirmation page and the one
    /// <c>TimesheetInvoiceService</c> bills against — but nothing recomputed it when entries
    /// changed, so a timesheet with real entries reported 0 hours and invoices generated for
    /// zero. Called after every entry add, edit and delete.
    /// </summary>
    /// <param name="userId">
    /// The acting user, or <see cref="Guid.Empty"/> where the caller has none (the delete
    /// endpoint takes no user). An empty value leaves <c>UpdatedBy</c> alone rather than
    /// stamping a zero GUID over the last real editor.
    /// </param>
    private async Task RecalculateTotalHoursAsync(Guid timesheetId, Guid userId, CancellationToken ct)
    {
        var timesheet = await _repository.GetByIdAsync(timesheetId);
        if (timesheet == null) return;

        var entries = await _entryRepository.GetByTimesheetIdAsync(timesheetId);
        timesheet.TotalHours = entries
            .Where(e => e.TenantId == timesheet.TenantId)
            .Sum(e => e.TotalHours);

        timesheet.UpdatedAt = DateTime.UtcNow;
        if (userId != Guid.Empty)
            timesheet.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(timesheet);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<ConsultantTimesheetEntryDto>> GetEntriesAsync(Guid timesheetId, CancellationToken ct = default)
    {
        await GetOwnedTimesheetAsync(timesheetId);
        var tenantId = GetTenantId();
        var entities = await _entryRepository.GetByTimesheetIdAsync(timesheetId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<ConsultantTimesheetEntryDto> UpdateEntryAsync(UpdateConsultantTimesheetEntryDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedEntryAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _entryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        await RecalculateTotalHoursAsync(entity.TimesheetId, userId, ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteEntryAsync(Guid entryId, CancellationToken ct = default)
    {
        var entity = await GetOwnedEntryAsync(entryId);
        // Captured before the delete; the entity is detached afterwards.
        var timesheetId = entity.TimesheetId;

        await _entryRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        await RecalculateTotalHoursAsync(timesheetId, Guid.Empty, ct);
        return true;
    }

    public async Task<SendTimesheetConfirmationResultDto> SendConfirmationAsync(
        Guid timesheetId,
        SendTimesheetConfirmationRequestDto request,
        Guid tenantId,
        Guid sentById,
        CancellationToken ct = default)
    {
        var resolvedTenantId = RequireCurrentTenant(tenantId);
        var timesheet = await GetOwnedTimesheetAsync(timesheetId);

        if (timesheet.Status != TimesheetStatus.Approved)
            throw new InvalidOperationException("Only approved timesheets can be sent to the client for confirmation.");

        var expiry = request.TokenExpiryDate ?? DateTime.UtcNow.AddDays(7);
        if (expiry <= DateTime.UtcNow)
            throw new InvalidOperationException("Token expiry must be in the future.");

        var sendDto = new SendTimesheetConfirmationDto
        {
            TimesheetId = timesheetId,
            ClientContactEmail = request.ClientContactEmail.Trim(),
            ClientContactName = request.ClientContactName?.Trim(),
            SentById = sentById,
            TokenExpiryDate = expiry
        };

        var confirmation = sendDto.ToEntity(resolvedTenantId, sentById);
        await _confirmationRepository.AddAsync(confirmation);

        timesheet.Status = TimesheetStatus.SentToClient;
        timesheet.UpdatedAt = DateTime.UtcNow;
        timesheet.UpdatedBy = sentById.ToString();
        await _repository.UpdateAsync(timesheet);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Timesheet {Number} sent to client contact {Email}",
            timesheet.TimesheetNumber,
            request.ClientContactEmail);

        var emailSent = await SendTimesheetConfirmationEmailAsync(
            confirmation,
            isResend: false,
            ct);

        return new SendTimesheetConfirmationResultDto
        {
            Confirmation = confirmation.ToDto(),
            ConfirmationToken = confirmation.ConfirmationToken,
            EmailSent = emailSent
        };
    }

    public async Task<SendTimesheetConfirmationResultDto> ResendConfirmationAsync(
        Guid timesheetId,
        Guid sentById,
        CancellationToken ct = default)
    {
        var timesheet = await GetOwnedTimesheetAsync(timesheetId);

        if (timesheet.Status != TimesheetStatus.SentToClient)
            throw new InvalidOperationException("Only timesheets awaiting client confirmation can be resent.");

        var tenantId = GetTenantId();
        var confirmations = await _confirmationRepository.GetByTimesheetIdAsync(timesheetId);
        var confirmation = confirmations.FirstOrDefault(c => c.TenantId == tenantId);
        if (confirmation == null)
            throw new InvalidOperationException("No confirmation request exists for this timesheet.");

        if (confirmation.Status is TimesheetConfirmationStatus.Confirmed or TimesheetConfirmationStatus.Rejected)
            throw new InvalidOperationException("This timesheet confirmation has already been responded to.");

        confirmation.ConfirmationToken = Guid.NewGuid();
        confirmation.TokenExpiryDate = DateTime.UtcNow.AddDays(7);
        confirmation.SentDate = DateTime.UtcNow;
        confirmation.SentById = sentById;
        confirmation.Status = TimesheetConfirmationStatus.Resent;
        confirmation.ResendCount += 1;
        confirmation.ViewedDate = null;
        confirmation.UpdatedAt = DateTime.UtcNow;
        confirmation.UpdatedBy = sentById.ToString();
        await _confirmationRepository.UpdateAsync(confirmation);

        timesheet.UpdatedAt = DateTime.UtcNow;
        timesheet.UpdatedBy = sentById.ToString();
        await _repository.UpdateAsync(timesheet);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Timesheet {Number} confirmation resent (count {ResendCount})",
            timesheet.TimesheetNumber,
            confirmation.ResendCount);

        var emailSent = await SendTimesheetConfirmationEmailAsync(
            confirmation,
            isResend: true,
            ct);

        return new SendTimesheetConfirmationResultDto
        {
            Confirmation = confirmation.ToDto(),
            ConfirmationToken = confirmation.ConfirmationToken,
            EmailSent = emailSent
        };
    }

    public async Task<ClientTimesheetConfirmationPublicDto> ValidateConfirmationTokenAsync(
        Guid token,
        CancellationToken ct = default)
    {
        // Tenant is derived from the token row itself — the token is globally unique, so no
        // X-Tenant-Id header is required for this emailed-link flow.
        var confirmation = await _confirmationRepository.GetByTokenAsync(token);
        if (confirmation == null)
            throw new ArgumentException("Invalid or expired confirmation token.");

        var timesheet = confirmation.Timesheet
            ?? throw new InvalidOperationException("Timesheet not found for this confirmation.");

        var isExpired = confirmation.TokenExpiryDate < DateTime.UtcNow
            || confirmation.Status == TimesheetConfirmationStatus.Expired;

        if (isExpired
            && confirmation.Status is TimesheetConfirmationStatus.Sent
                or TimesheetConfirmationStatus.Viewed
                or TimesheetConfirmationStatus.Resent)
        {
            confirmation.Status = TimesheetConfirmationStatus.Expired;
            confirmation.UpdatedAt = DateTime.UtcNow;
            confirmation.UpdatedBy = "system";
            await _confirmationRepository.UpdateAsync(confirmation);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        if (!isExpired
            && confirmation.ViewedDate is null
            && confirmation.Status is TimesheetConfirmationStatus.Sent or TimesheetConfirmationStatus.Resent)
        {
            confirmation.Status = TimesheetConfirmationStatus.Viewed;
            confirmation.ViewedDate = DateTime.UtcNow;
            confirmation.UpdatedAt = DateTime.UtcNow;
            confirmation.UpdatedBy = "client";
            await _confirmationRepository.UpdateAsync(confirmation);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var canRespond = !isExpired
            && confirmation.Status is TimesheetConfirmationStatus.Sent
                or TimesheetConfirmationStatus.Viewed
                or TimesheetConfirmationStatus.Resent;

        return new ClientTimesheetConfirmationPublicDto
        {
            TimesheetNumber = timesheet.TimesheetNumber,
            ConsultantName = timesheet.Consultant?.FullName ?? string.Empty,
            ClientName = timesheet.Client?.ClientName ?? string.Empty,
            PeriodStartDate = timesheet.PeriodStartDate,
            PeriodEndDate = timesheet.PeriodEndDate,
            TotalHours = timesheet.TotalHours,
            Status = confirmation.Status,
            IsExpired = isExpired,
            CanRespond = canRespond,
            TokenExpiryDate = confirmation.TokenExpiryDate,
            Entries = timesheet.Entries.OrderBy(e => e.WorkDate).Select(e => e.ToDto()).ToList()
        };
    }

    public async Task<ClientTimesheetConfirmationDto> ConfirmByClientAsync(
        ClientConfirmTimesheetDto dto,
        CancellationToken ct = default)
    {
        var confirmation = await _confirmationRepository.GetByTokenAsync(dto.ConfirmationToken);
        if (confirmation == null)
            throw new ArgumentException("Invalid or expired confirmation token.");

        var timesheet = await _repository.GetByIdAsync(confirmation.TimesheetId);
        if (timesheet == null)
            throw new ArgumentException("Timesheet not found for this confirmation.");

        if (confirmation.Status == TimesheetConfirmationStatus.Confirmed)
            throw new InvalidOperationException("This timesheet has already been confirmed.");

        if (confirmation.Status == TimesheetConfirmationStatus.Rejected)
            throw new InvalidOperationException("This timesheet has already been rejected.");

        if (confirmation.TokenExpiryDate < DateTime.UtcNow)
            throw new InvalidOperationException("This confirmation link has expired.");

        confirmation.Status = TimesheetConfirmationStatus.Confirmed;
        confirmation.ConfirmedDate = DateTime.UtcNow;
        confirmation.ClientNotes = dto.ClientNotes;
        confirmation.UpdatedAt = DateTime.UtcNow;
        confirmation.UpdatedBy = "client";
        await _confirmationRepository.UpdateAsync(confirmation);

        timesheet.Status = TimesheetStatus.ClientConfirmed;
        timesheet.UpdatedAt = DateTime.UtcNow;
        timesheet.UpdatedBy = "client";
        await _repository.UpdateAsync(timesheet);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Timesheet {Number} confirmed by client", timesheet.TimesheetNumber);
        return confirmation.ToDto();
    }

    public async Task<ClientTimesheetConfirmationDto> RejectByClientAsync(
        ClientRejectTimesheetDto dto,
        CancellationToken ct = default)
    {
        var confirmation = await _confirmationRepository.GetByTokenAsync(dto.ConfirmationToken);
        if (confirmation == null)
            throw new ArgumentException("Invalid or expired confirmation token.");

        var timesheet = await _repository.GetByIdAsync(confirmation.TimesheetId);
        if (timesheet == null)
            throw new ArgumentException("Timesheet not found for this confirmation.");

        if (confirmation.Status == TimesheetConfirmationStatus.Confirmed)
            throw new InvalidOperationException("This timesheet has already been confirmed.");

        if (confirmation.Status == TimesheetConfirmationStatus.Rejected)
            throw new InvalidOperationException("This timesheet has already been rejected.");

        if (confirmation.TokenExpiryDate < DateTime.UtcNow)
            throw new InvalidOperationException("This confirmation link has expired.");

        confirmation.Status = TimesheetConfirmationStatus.Rejected;
        confirmation.RejectedDate = DateTime.UtcNow;
        confirmation.ClientNotes = dto.ClientNotes.Trim();
        confirmation.UpdatedAt = DateTime.UtcNow;
        confirmation.UpdatedBy = "client";
        await _confirmationRepository.UpdateAsync(confirmation);

        timesheet.Status = TimesheetStatus.ClientRejected;
        timesheet.Notes = dto.ClientNotes.Trim();
        timesheet.UpdatedAt = DateTime.UtcNow;
        timesheet.UpdatedBy = "client";
        await _repository.UpdateAsync(timesheet);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Timesheet {Number} rejected by client", timesheet.TimesheetNumber);
        return confirmation.ToDto();
    }

    public async Task<ClientTimesheetConfirmationDto> ConfirmByPortalClientAsync(
        Guid timesheetId,
        Guid consultantClientId,
        string? clientNotes,
        CancellationToken ct = default)
    {
        var (confirmation, timesheet) = await GetPortalConfirmationContextAsync(
            timesheetId,
            consultantClientId,
            ct);

        confirmation.Status = TimesheetConfirmationStatus.Confirmed;
        confirmation.ConfirmedDate = DateTime.UtcNow;
        confirmation.ClientNotes = clientNotes;
        confirmation.UpdatedAt = DateTime.UtcNow;
        confirmation.UpdatedBy = "portal_client";
        await _confirmationRepository.UpdateAsync(confirmation);

        timesheet.Status = TimesheetStatus.ClientConfirmed;
        timesheet.UpdatedAt = DateTime.UtcNow;
        timesheet.UpdatedBy = "portal_client";
        await _repository.UpdateAsync(timesheet);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Timesheet {Number} confirmed via client portal",
            timesheet.TimesheetNumber);
        return confirmation.ToDto();
    }

    public async Task<ClientTimesheetConfirmationDto> RejectByPortalClientAsync(
        Guid timesheetId,
        Guid consultantClientId,
        string clientNotes,
        CancellationToken ct = default)
    {
        var (confirmation, timesheet) = await GetPortalConfirmationContextAsync(
            timesheetId,
            consultantClientId,
            ct);

        confirmation.Status = TimesheetConfirmationStatus.Rejected;
        confirmation.RejectedDate = DateTime.UtcNow;
        confirmation.ClientNotes = clientNotes;
        confirmation.UpdatedAt = DateTime.UtcNow;
        confirmation.UpdatedBy = "portal_client";
        await _confirmationRepository.UpdateAsync(confirmation);

        timesheet.Status = TimesheetStatus.ClientRejected;
        timesheet.Notes = clientNotes;
        timesheet.UpdatedAt = DateTime.UtcNow;
        timesheet.UpdatedBy = "portal_client";
        await _repository.UpdateAsync(timesheet);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Timesheet {Number} rejected via client portal",
            timesheet.TimesheetNumber);
        return confirmation.ToDto();
    }

    private async Task<(ClientTimesheetConfirmation Confirmation, ConsultantTimesheet Timesheet)>
        GetPortalConfirmationContextAsync(
            Guid timesheetId,
            Guid consultantClientId,
            CancellationToken ct)
    {
        var timesheet = await _repository.GetByIdAsync(timesheetId);
        if (timesheet == null)
            throw new ArgumentException($"Timesheet '{timesheetId}' not found.");

        if (timesheet.ClientId != consultantClientId)
            throw new UnauthorizedAccessException("You do not have access to this timesheet.");

        if (timesheet.Status != TimesheetStatus.SentToClient)
            throw new InvalidOperationException("This timesheet is not awaiting client confirmation.");

        var confirmations = await _confirmationRepository.GetByTimesheetIdAsync(timesheetId);
        var confirmation = confirmations.FirstOrDefault()
            ?? throw new InvalidOperationException("No confirmation request exists for this timesheet.");

        if (confirmation.Status is TimesheetConfirmationStatus.Confirmed or TimesheetConfirmationStatus.Rejected)
            throw new InvalidOperationException("This timesheet has already been responded to.");

        return (confirmation, timesheet);
    }

    public async Task<ClientTimesheetConfirmationDto?> GetConfirmationAsync(Guid timesheetId, CancellationToken ct = default)
    {
        await GetOwnedTimesheetAsync(timesheetId);
        var tenantId = GetTenantId();
        var entities = await _confirmationRepository.GetByTimesheetIdAsync(timesheetId);
        return entities.FirstOrDefault(c => c.TenantId == tenantId)?.ToDto();
    }

    private async Task<string> GenerateTimesheetNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var count = await _repository.GetQueryable()
            .CountAsync(t => t.TenantId == tenantId, ct);
        return $"TS-{DateTime.UtcNow:yyyyMM}-{(count + 1):D5}";
    }

    private async Task<bool> SendTimesheetConfirmationEmailAsync(
        ClientTimesheetConfirmation confirmation,
        bool isResend,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(confirmation.ClientContactEmail))
            return false;

        var timesheet = await _repository.GetWithFullDetailsAsync(confirmation.TimesheetId);
        if (timesheet == null)
        {
            _logger.LogWarning(
                "Timesheet {TimesheetId} not found when sending confirmation email",
                confirmation.TimesheetId);
            return false;
        }

        var toEmail = confirmation.ClientContactEmail.Trim();
        var contactName = string.IsNullOrWhiteSpace(confirmation.ClientContactName)
            ? "there"
            : confirmation.ClientContactName.Trim();
        var consultantName = GetEmployeeDisplayName(timesheet.Consultant);
        var clientName = timesheet.Client?.ClientName ?? "Client";
        var periodLabel = $"{timesheet.PeriodStartDate:dd MMM yyyy} – {timesheet.PeriodEndDate:dd MMM yyyy}";
        var confirmUrl = $"{_portalBaseUrl}/client-timesheet/confirm/{confirmation.ConfirmationToken}";
        var expiryLabel = confirmation.TokenExpiryDate.ToString("dddd, d MMMM yyyy HH:mm") + " UTC";

        var subject = isResend
            ? $"Reminder: timesheet confirmation required — {timesheet.TimesheetNumber}"
            : $"Timesheet confirmation required — {consultantName} ({periodLabel})";

        var headline = isResend
            ? "Reminder: timesheet confirmation required"
            : "Timesheet ready for your confirmation";

        var intro = isResend
            ? $"This is a reminder to review and confirm the consultant timesheet below. A new secure link has been issued because the previous one may have expired or was not used."
            : $"Please review the consultant timesheet below and confirm or reject the recorded hours using the secure link.";

        var body = BuildTimesheetConfirmationEmailBody(
            contactName,
            intro,
            headline,
            timesheet,
            consultantName,
            clientName,
            periodLabel,
            expiryLabel,
            confirmUrl);

        try
        {
            var sent = await _email.SendEmailAsync(new EmailDto
            {
                To = toEmail,
                Subject = subject,
                Body = body,
                IsHtml = true
            });

            if (!sent)
            {
                _logger.LogWarning(
                    "Timesheet confirmation email to {Email} for {Number} was not sent (provider returned false)",
                    toEmail,
                    timesheet.TimesheetNumber);
            }

            return sent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send timesheet confirmation email to {Email} for {Number}",
                toEmail,
                timesheet.TimesheetNumber);
            return false;
        }
    }

    private static string BuildTimesheetConfirmationEmailBody(
        string contactName,
        string intro,
        string headline,
        ConsultantTimesheet timesheet,
        string consultantName,
        string clientName,
        string periodLabel,
        string expiryLabel,
        string confirmUrl)
    {
        var engagementRow = timesheet.Engagement != null
            ? $@"<tr>
  <td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb;font-weight:600'>Engagement</td>
  <td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb'>{Encode(timesheet.Engagement.Title)} ({Encode(timesheet.Engagement.EngagementCode)})</td>
</tr>"
            : string.Empty;

        var notesBlock = string.IsNullOrWhiteSpace(timesheet.Notes)
            ? string.Empty
            : $@"<p style='margin-top:1rem'><strong>Notes:</strong> {Encode(timesheet.Notes)}</p>";

        var entriesSection = BuildTimesheetEntriesEmailSection(timesheet.Entries);

        return $@"
<html><body style='font-family:sans-serif;color:#374151;max-width:640px;margin:0 auto'>
<div style='background:linear-gradient(135deg,#0f766e,#14b8a6);padding:2rem;border-radius:8px 8px 0 0'>
  <h1 style='color:#fff;margin:0;font-size:1.5rem'>{Encode(headline)}</h1>
</div>
<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>
  <p>Hi <strong>{Encode(contactName)}</strong>,</p>
  <p>{Encode(intro)}</p>
  <table style='width:100%;border-collapse:collapse;margin:1rem 0'>
    <tr>
      <td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb;font-weight:600'>Timesheet ref</td>
      <td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb'>{Encode(timesheet.TimesheetNumber)}</td>
    </tr>
    <tr>
      <td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb;font-weight:600'>Consultant</td>
      <td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb'>{Encode(consultantName)}</td>
    </tr>
    <tr>
      <td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb;font-weight:600'>Client</td>
      <td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb'>{Encode(clientName)}</td>
    </tr>
    <tr>
      <td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb;font-weight:600'>Period</td>
      <td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb'>{Encode(periodLabel)}</td>
    </tr>
    <tr>
      <td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb;font-weight:600'>Total hours</td>
      <td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb'>{timesheet.TotalHours:N2}</td>
    </tr>
    {engagementRow}
    <tr>
      <td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb;font-weight:600'>Link expires</td>
      <td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb'>{Encode(expiryLabel)}</td>
    </tr>
  </table>
  {entriesSection}
  {notesBlock}
  <p style='margin-top:1.5rem'>Use the button below to open the secure confirmation page where you can review daily entries and confirm or reject this timesheet.</p>
  <p style='margin-top:1.5rem'>
    <a href='{confirmUrl}'
       style='background:#0f766e;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
      Review and confirm timesheet
    </a>
  </p>
  <p style='color:#6b7280;font-size:0.85rem;margin-top:1.5rem'>If the button does not work, copy and paste this URL into your browser:<br/>
  <a href='{confirmUrl}' style='color:#0f766e;word-break:break-all'>{confirmUrl}</a></p>
  <p style='color:#9ca3af;font-size:0.75rem;margin-top:2rem'>Timesheet reference: {Encode(timesheet.TimesheetNumber)}. No login is required — this link is unique to you.</p>
</div>
</body></html>";
    }

    private static string BuildTimesheetEntriesEmailSection(IEnumerable<ConsultantTimesheetEntry> entries)
    {
        var ordered = entries
            .OrderBy(e => e.WorkDate)
            .ToList();

        if (ordered.Count == 0)
            return string.Empty;

        var rows = new StringBuilder();
        var displayEntries = ordered.Take(MaxEmailEntryRows).ToList();
        var rowIndex = 0;

        foreach (var entry in displayEntries)
        {
            var bg = rowIndex % 2 == 0 ? "#fff" : "#f9fafb";
            var summary = entry.ActivitySummary.Length > 120
                ? entry.ActivitySummary[..117] + "..."
                : entry.ActivitySummary;
            rows.Append($@"
    <tr>
      <td style='padding:0.5rem;background:{bg};border:1px solid #e5e7eb'>{entry.WorkDate:ddd, dd MMM yyyy}</td>
      <td style='padding:0.5rem;background:{bg};border:1px solid #e5e7eb;text-align:right'>{entry.TotalHours:N2}</td>
      <td style='padding:0.5rem;background:{bg};border:1px solid #e5e7eb'>{Encode(summary)}</td>
    </tr>");
            rowIndex++;
        }

        var overflowNote = ordered.Count > MaxEmailEntryRows
            ? $@"<p style='color:#6b7280;font-size:0.85rem;margin:0.5rem 0 0'>
  Showing {MaxEmailEntryRows} of {ordered.Count} daily entries. View the full breakdown on the confirmation page.</p>"
            : string.Empty;

        return $@"
  <p style='margin:1.25rem 0 0.5rem;font-weight:600'>Daily breakdown</p>
  <table style='width:100%;border-collapse:collapse;margin:0'>
    <tr>
      <th style='padding:0.5rem;background:#ecfdf5;border:1px solid #e5e7eb;text-align:left'>Date</th>
      <th style='padding:0.5rem;background:#ecfdf5;border:1px solid #e5e7eb;text-align:right'>Hours</th>
      <th style='padding:0.5rem;background:#ecfdf5;border:1px solid #e5e7eb;text-align:left'>Activity</th>
    </tr>
    {rows}
  </table>
  {overflowNote}";
    }

    private static string GetEmployeeDisplayName(Employee employee)
    {
        var name = $"{employee.FirstName} {employee.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? employee.EmployeeNumber : name;
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}

#endregion

// ============================================================================
// TIMESHEET INVOICE SERVICE
// ============================================================================

#region Timesheet Invoice Service

public class TimesheetInvoiceService : ITimesheetInvoiceService
{
    private readonly ITimesheetInvoiceRepository _repository;
    private readonly ITimesheetInvoiceLinkRepository _linkRepository;
    private readonly IConsultantTimesheetRepository _timesheetRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TimesheetInvoiceService> _logger;

    public TimesheetInvoiceService(
        ITimesheetInvoiceRepository repository,
        ITimesheetInvoiceLinkRepository linkRepository,
        IConsultantTimesheetRepository timesheetRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TimesheetInvoiceService> logger)
    {
        _repository = repository;
        _linkRepository = linkRepository;
        _timesheetRepository = timesheetRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<TimesheetInvoice> GetOwnedInvoiceAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Invoice '{id}' not found.");
        return entity;
    }

    public async Task<TimesheetInvoiceDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetWithLinkedTimesheetsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Invoice '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<TimesheetInvoiceDto?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetQueryable()
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.InvoiceNumber == invoiceNumber && !i.IsDeleted, ct);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<TimesheetInvoiceSummaryDto>> GetByEngagementIdAsync(Guid engagementId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetQueryable()
            .Where(i => i.TenantId == tenantId
                && i.LinkedTimesheets.Any(l => l.Timesheet != null && l.Timesheet.EngagementId == engagementId))
            .ToListAsync(ct);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TimesheetInvoiceSummaryDto>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetByClientIdAsync(clientId))
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<TimesheetInvoiceSummaryDto>> GetByStatusAsync(TimesheetInvoiceStatus status, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetByStatusAsync(status))
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<TimesheetInvoiceSummaryDto>> GetOverdueInvoicesAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return (await _repository.GetOverdueInvoicesAsync())
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<TimesheetInvoiceSummaryDto>> GetByPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetQueryable()
            .Where(i => i.TenantId == tenantId && i.BillingPeriodStart >= from && i.BillingPeriodEnd <= to)
            .ToListAsync(ct);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<TimesheetInvoiceSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        // Navigation names appear on the summary DTO, so they must be loaded.
        var query = _repository.GetQueryable().Include(i => i.Client).Include(i => i.Consultant).Where(i => i.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.IssuedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<TimesheetInvoiceSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<TimesheetInvoiceDto> GenerateAsync(CreateTimesheetInvoiceDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var resolvedTenantId = RequireCurrentTenant(tenantId);

        IEnumerable<ConsultantTimesheet> approvedTimesheets;
        if (dto.TimesheetIds.Any())
        {
            var allTs = await _timesheetRepository.GetByClientIdAsync(dto.ClientId);
            approvedTimesheets = allTs.Where(t =>
                t.TenantId == resolvedTenantId
                && dto.TimesheetIds.Contains(t.Id)
                && t.Status == TimesheetStatus.ClientConfirmed);
        }
        else
        {
            approvedTimesheets = (await _timesheetRepository.GetReadyForInvoicingAsync(dto.ClientId))
                .Where(t => t.TenantId == resolvedTenantId);
        }
        var timesheetList = approvedTimesheets.ToList();

        if (!timesheetList.Any())
            throw new InvalidOperationException("No client-confirmed timesheets available for invoicing.");

        var totalHours = timesheetList.Sum(t => t.TotalHours);
        var entity = dto.ToEntity(resolvedTenantId, userId);
        entity.InvoiceNumber = await GenerateInvoiceNumberAsync(resolvedTenantId, ct);
        entity.Status = TimesheetInvoiceStatus.Draft;
        entity.TotalHours = totalHours;
        entity.SubTotal = totalHours * dto.HourlyRate;
        entity.TaxAmount = entity.SubTotal * (dto.TaxPercentage / 100m);
        entity.TotalAmount = entity.SubTotal + entity.TaxAmount;

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        // Link each timesheet to the invoice
        foreach (var ts in timesheetList)
        {
            var link = new TimesheetInvoiceLink
            {
                Id = Guid.NewGuid(),
                TenantId = resolvedTenantId,
                InvoiceId = entity.Id,
                TimesheetId = ts.Id,
                Hours = ts.TotalHours,
                Amount = ts.TotalHours * dto.HourlyRate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId.ToString()
            };
            await _linkRepository.AddAsync(link);

            ts.Status = TimesheetStatus.Billed;
            ts.UpdatedAt = DateTime.UtcNow;
            ts.UpdatedBy = userId.ToString();
            await _timesheetRepository.UpdateAsync(ts);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Invoice {Number} generated for {Count} timesheets, total {Total}", entity.InvoiceNumber, timesheetList.Count, entity.TotalAmount);
        return entity.ToDto();
    }

    public async Task<TimesheetInvoiceDto> UpdateAsync(UpdateTimesheetInvoiceDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedInvoiceAsync(dto.Id);

        if (entity.Status != TimesheetInvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be edited.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<TimesheetInvoiceDto> SendAsync(Guid invoiceId, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedInvoiceAsync(invoiceId);

        if (entity.Status != TimesheetInvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be sent.");

        entity.Status = TimesheetInvoiceStatus.Sent;
        entity.IssuedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Invoice {Number} sent by {UserId}", entity.InvoiceNumber, userId);
        return entity.ToDto();
    }

    public async Task<TimesheetInvoiceDto> MarkPaidAsync(Guid invoiceId, DateOnly paidDate, decimal paidAmount, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedInvoiceAsync(invoiceId);

        if (entity.Status != TimesheetInvoiceStatus.Sent && entity.Status != TimesheetInvoiceStatus.Overdue)
            throw new InvalidOperationException("Only sent or overdue invoices can be marked as paid.");

        entity.Status = TimesheetInvoiceStatus.Paid;
        entity.PaidDate = paidDate;
        entity.PaidAmount = paidAmount;
        // paidAmount stored for payment reconciliation
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Invoice {Number} marked as paid: {Amount}", entity.InvoiceNumber, paidAmount);
        return entity.ToDto();
    }

    public async Task<TimesheetInvoiceDto> VoidAsync(Guid invoiceId, string reason, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedInvoiceAsync(invoiceId);

        if (entity.Status == TimesheetInvoiceStatus.Paid)
            throw new InvalidOperationException("A paid invoice cannot be voided.");

        entity.Status = TimesheetInvoiceStatus.Voided;
        entity.Notes = reason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Invoice {Number} voided: {Reason}", entity.InvoiceNumber, reason);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedInvoiceAsync(id);

        if (entity.Status == TimesheetInvoiceStatus.Paid)
            throw new InvalidOperationException("A paid invoice cannot be deleted.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerateInvoiceNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var count = await _repository.GetQueryable()
            .CountAsync(i => i.TenantId == tenantId, ct);
        return $"INV-{DateTime.UtcNow:yyyyMM}-{(count + 1):D5}";
    }
}

#endregion
