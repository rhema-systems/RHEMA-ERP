using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Ehc;

public sealed class EhcProblemService : IEhcProblemService
{
    private static readonly Regex ProblemNumberRegex = new("^PRB-(?<yy>\\d{2})-(?<seq>\\d{6})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IEhcTicketRepository _ticketRepository;
    private readonly ICurrentUserService _currentUserService;

    public EhcProblemService(
        IUnitOfWork unitOfWork,
        IEhcTicketRepository ticketRepository,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _ticketRepository = ticketRepository;
        _currentUserService = currentUserService;
    }

    private Guid GetTenantIdOrThrow()
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty) throw new InvalidOperationException("Tenant context is required.");
        return tenantId;
    }

    private Guid? TryGetActorUserId()
    {
        return Guid.TryParse(_currentUserService.UserId, out var uid) && uid != Guid.Empty ? uid : (Guid?)null;
    }

    private async Task<string> GenerateProblemNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var yy = (year % 100).ToString("D2");
        var prefix = $"PRB-{yy}-";

        var last = await _unitOfWork.Repository<EhcProblem>()
            .GetQueryable(p => p.TenantId == tenantId && p.ProblemNumber.StartsWith(prefix))
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => p.ProblemNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSeq = 1;
        if (!string.IsNullOrWhiteSpace(last))
        {
            var m = ProblemNumberRegex.Match(last);
            if (m.Success && int.TryParse(m.Groups["seq"].Value, out var lastSeq))
            {
                nextSeq = lastSeq + 1;
            }
        }

        return $"{prefix}{nextSeq:D6}";
    }

    private async Task AddProblemAuditAsync(
        EhcProblem problem,
        string eventType,
        string? title,
        string? body,
        Guid? actorUserId,
        object? data = null)
    {
        if (problem.TenantId == Guid.Empty || problem.Id == Guid.Empty) return;

        var ev = new EhcProblemAuditEvent
        {
            TenantId = problem.TenantId,
            ProblemId = problem.Id,
            EventType = (eventType ?? string.Empty).Trim(),
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
            ActorUserId = actorUserId,
            DataJson = data == null ? null : JsonSerializer.Serialize(data),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = actorUserId
        };

        await _unitOfWork.Repository<EhcProblemAuditEvent>().AddAsync(ev);
    }

    private async Task AddTicketAuditAsync(Guid tenantId, Guid ticketId, string eventType, string? title, string? body, Guid? actorUserId, object? data = null)
    {
        if (tenantId == Guid.Empty || ticketId == Guid.Empty) return;

        var ev = new EhcTicketAuditEvent
        {
            TenantId = tenantId,
            TicketId = ticketId,
            EventType = (eventType ?? string.Empty).Trim(),
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
            IsInternal = true,
            ActorUserId = actorUserId,
            DataJson = data == null ? null : JsonSerializer.Serialize(data),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = actorUserId
        };

        await _unitOfWork.Repository<EhcTicketAuditEvent>().AddAsync(ev);
    }

    public async Task<IReadOnlyList<EhcProblemListItemDto>> GetProblemsAsync(
        int page = 1,
        int pageSize = 25,
        string? q = null,
        EhcProblemStatus? status = null,
        EhcTicketPriority? priority = null,
        Guid? departmentId = null,
        Guid? ownerUserId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 200) pageSize = 200;

        IQueryable<EhcProblem> query = _unitOfWork.Repository<EhcProblem>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted)
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.OwnerUser);

        q = (q ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(p => p.ProblemNumber.Contains(q) || p.Title.Contains(q));
        }

        if (status.HasValue) query = query.Where(p => p.Status == status.Value);
        if (priority.HasValue) query = query.Where(p => p.Priority == priority.Value);
        if (departmentId.HasValue && departmentId.Value != Guid.Empty) query = query.Where(p => p.DepartmentId == departmentId.Value);
        if (ownerUserId.HasValue && ownerUserId.Value != Guid.Empty) query = query.Where(p => p.OwnerUserId == ownerUserId.Value);
        if (createdFrom.HasValue)
        {
            var from = createdFrom.Value.Date;
            query = query.Where(p => p.CreatedAt >= from);
        }
        if (createdTo.HasValue)
        {
            var toExclusive = createdTo.Value.Date.AddDays(1);
            query = query.Where(p => p.CreatedAt < toExclusive);
        }

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new EhcProblemListItemDto
            {
                Id = p.Id,
                ProblemNumber = p.ProblemNumber,
                Title = p.Title,
                Status = p.Status,
                Priority = p.Priority,
                DepartmentName = p.Department != null ? p.Department.Name : null,
                OwnerName = p.OwnerUser != null ? (p.OwnerUser.FirstName + " " + p.OwnerUser.LastName).Trim() : null,
                LinkedTicketsCount = 0,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return Array.Empty<EhcProblemListItemDto>();

        var ids = rows.Select(r => r.Id).ToList();
        var counts = await _unitOfWork.Repository<EhcProblemTicketLink>()
            .GetQueryable(l => l.TenantId == tenantId && ids.Contains(l.ProblemId))
            .AsNoTracking()
            .GroupBy(l => l.ProblemId)
            .Select(g => new { ProblemId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byId = counts.ToDictionary(x => x.ProblemId, x => x.Count);
        foreach (var r in rows)
        {
            if (byId.TryGetValue(r.Id, out var c)) r.LinkedTicketsCount = c;
        }

        return rows;
    }

    public async Task<EhcProblemDetailDto?> GetProblemByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        if (id == Guid.Empty) return null;

        var p = await _unitOfWork.Repository<EhcProblem>()
            .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == id)
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Subcategory)
            .Include(x => x.Department)
            .Include(x => x.OwnerUser)
            .Include(x => x.RootCause)
            .Include(x => x.CreatedFromTicket)
            .FirstOrDefaultAsync(cancellationToken);

        if (p == null) return null;

        var links = await _unitOfWork.Repository<EhcProblemTicketLink>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && l.ProblemId == p.Id)
            .AsNoTracking()
            .Include(l => l.Ticket)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new EhcProblemLinkedTicketDto
            {
                TicketId = l.TicketId,
                TicketNumber = l.Ticket.TicketNumber,
                Subject = l.Ticket.Subject,
                Status = l.Ticket.Status,
                Priority = l.Ticket.Priority,
                CreatedAt = l.Ticket.CreatedAt,
                Notes = l.Notes,
                LinkedAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var tasks = await _unitOfWork.Repository<EhcCapaTask>()
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && t.ProblemId == p.Id)
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedDepartment)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new EhcCapaTaskDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                AssignedToUserId = t.AssignedToUserId,
                AssignedToName = t.AssignedToUser != null ? (t.AssignedToUser.FirstName + " " + t.AssignedToUser.LastName).Trim() : null,
                AssignedDepartmentId = t.AssignedDepartmentId,
                AssignedDepartmentName = t.AssignedDepartment != null ? t.AssignedDepartment.Name : null,
                DueAt = t.DueAt,
                CompletedAt = t.CompletedAt,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var audit = await _unitOfWork.Repository<EhcProblemAuditEvent>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted && a.ProblemId == p.Id)
            .AsNoTracking()
            .Include(a => a.ActorUser)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new EhcProblemAuditEventDto
            {
                Id = a.Id,
                EventType = a.EventType,
                Title = a.Title,
                Body = a.Body,
                ActorUserId = a.ActorUserId,
                ActorName = a.ActorUser != null ? (a.ActorUser.FirstName + " " + a.ActorUser.LastName).Trim() : null,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new EhcProblemDetailDto
        {
            Id = p.Id,
            ProblemNumber = p.ProblemNumber,
            Title = p.Title,
            Description = p.Description,
            Status = p.Status,
            Priority = p.Priority,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name,
            SubcategoryId = p.SubcategoryId,
            SubcategoryName = p.Subcategory?.Name,
            DepartmentId = p.DepartmentId,
            DepartmentName = p.Department?.Name,
            OwnerUserId = p.OwnerUserId,
            OwnerName = p.OwnerUser != null ? (p.OwnerUser.FirstName + " " + p.OwnerUser.LastName).Trim() : null,
            RootCauseId = p.RootCauseId,
            RootCauseCode = p.RootCause?.Code,
            RootCauseName = p.RootCause?.Name,
            RootCauseDetails = p.RootCauseDetails,
            ResolutionSummary = p.ResolutionSummary,
            CreatedFromTicketId = p.CreatedFromTicketId,
            CreatedFromTicketNumber = p.CreatedFromTicket?.TicketNumber,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            LinkedTickets = links,
            CapaTasks = tasks,
            AuditTrail = audit
        };
    }

    public async Task<EhcProblemDetailDto> CreateProblemAsync(CreateEhcProblemRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        request ??= new CreateEhcProblemRequestDto();

        var title = (request.Title ?? string.Empty).Trim();
        var description = (request.Description ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.");

        var actorUserId = TryGetActorUserId();

        var entity = new EhcProblem
        {
            TenantId = tenantId,
            ProblemNumber = await GenerateProblemNumberAsync(tenantId, cancellationToken),
            Title = title,
            Description = description,
            Priority = request.Priority,
            Status = EhcProblemStatus.Open,
            CategoryId = request.CategoryId,
            SubcategoryId = request.SubcategoryId,
            DepartmentId = request.DepartmentId,
            OwnerUserId = request.OwnerUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = actorUserId
        };

        await _unitOfWork.Repository<EhcProblem>().AddAsync(entity);
        await AddProblemAuditAsync(entity, "ProblemCreated", "Problem created", null, actorUserId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await GetProblemByIdAsync(entity.Id, cancellationToken);
        if (refreshed == null) throw new InvalidOperationException("Failed to load created problem.");
        return refreshed;
    }

    public async Task<EhcProblemDetailDto> UpdateProblemAsync(Guid id, UpdateEhcProblemRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        request ??= new UpdateEhcProblemRequestDto();
        if (id == Guid.Empty) throw new ArgumentException("Problem id is required.");

        var repo = _unitOfWork.Repository<EhcProblem>();
        var entity = await repo.FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.Id == id);
        if (entity == null) throw new KeyNotFoundException("Problem not found.");

        var title = (request.Title ?? string.Empty).Trim();
        var description = (request.Description ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.");

        var actorUserId = TryGetActorUserId();

        entity.Title = title;
        entity.Description = description;
        entity.Priority = request.Priority;
        entity.Status = request.Status;
        entity.CategoryId = request.CategoryId;
        entity.SubcategoryId = request.SubcategoryId;
        entity.DepartmentId = request.DepartmentId;
        entity.OwnerUserId = request.OwnerUserId;
        entity.RootCauseId = request.RootCauseId;
        entity.RootCauseDetails = string.IsNullOrWhiteSpace(request.RootCauseDetails) ? null : request.RootCauseDetails.Trim();
        entity.ResolutionSummary = string.IsNullOrWhiteSpace(request.ResolutionSummary) ? null : request.ResolutionSummary.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserName ?? "System";
        entity.LastModifiedById = actorUserId;

        await repo.UpdateAsync(entity);
        await AddProblemAuditAsync(entity, "ProblemUpdated", "Problem updated", null, actorUserId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await GetProblemByIdAsync(entity.Id, cancellationToken);
        if (refreshed == null) throw new InvalidOperationException("Failed to load updated problem.");
        return refreshed;
    }

    public async Task DeleteProblemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        if (id == Guid.Empty) throw new ArgumentException("Problem id is required.");

        var repo = _unitOfWork.Repository<EhcProblem>();
        var entity = await repo.FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.Id == id);
        if (entity == null) throw new KeyNotFoundException("Problem not found.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = _currentUserService.UserName ?? "System";
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserName ?? "System";
        entity.LastModifiedById = TryGetActorUserId();

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<EhcProblemDetailDto> ConvertTicketToProblemAsync(Guid ticketId, ConvertEhcTicketToProblemRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        request ??= new ConvertEhcTicketToProblemRequestDto();
        if (ticketId == Guid.Empty) throw new ArgumentException("Ticket id is required.");

        var actorUserId = TryGetActorUserId();

        var ticket = await _ticketRepository.GetByIdWithDetailsAsync(ticketId, tenantId, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        var title = string.IsNullOrWhiteSpace(request.Title) ? (ticket.Subject ?? ticket.TicketNumber) : request.Title.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? ticket.Description : request.Description.Trim();

        var problem = new EhcProblem
        {
            TenantId = tenantId,
            ProblemNumber = await GenerateProblemNumberAsync(tenantId, cancellationToken),
            Title = title,
            Description = description,
            Priority = request.Priority ?? ticket.Priority,
            Status = EhcProblemStatus.Open,
            CategoryId = ticket.CategoryId,
            SubcategoryId = ticket.SubcategoryId,
            DepartmentId = request.DepartmentId ?? ticket.AssignedDepartmentId,
            OwnerUserId = request.OwnerUserId ?? ticket.AssignedToUserId,
            CreatedFromTicketId = ticket.Id,
            RootCauseId = ticket.RootCauseId,
            RootCauseDetails = ticket.RootCauseDetails,
            ResolutionSummary = ticket.ResolutionSummary,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = actorUserId
        };

        await _unitOfWork.Repository<EhcProblem>().AddAsync(problem);
        await AddProblemAuditAsync(problem, "ProblemCreatedFromTicket", "Created from ticket", $"From {ticket.TicketNumber}", actorUserId, new { ticketId = ticket.Id, ticketNumber = ticket.TicketNumber });

        await _unitOfWork.Repository<EhcProblemTicketLink>().AddAsync(new EhcProblemTicketLink
        {
            TenantId = tenantId,
            ProblemId = problem.Id,
            TicketId = ticket.Id,
            Notes = "Converted from ticket",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = actorUserId
        });

        await AddTicketAuditAsync(tenantId, ticket.Id, "ConvertedToProblem", "Converted to problem", $"Problem {problem.ProblemNumber} created.", actorUserId, new { problemId = problem.Id, problemNumber = problem.ProblemNumber });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await GetProblemByIdAsync(problem.Id, cancellationToken);
        if (refreshed == null) throw new InvalidOperationException("Failed to load created problem.");
        return refreshed;
    }

    public async Task LinkTicketAsync(Guid problemId, LinkEhcTicketToProblemRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        request ??= new LinkEhcTicketToProblemRequestDto();
        if (problemId == Guid.Empty) throw new ArgumentException("Problem id is required.");
        if (request.TicketId == Guid.Empty) throw new ArgumentException("TicketId is required.");

        var actorUserId = TryGetActorUserId();

        var problem = await _unitOfWork.Repository<EhcProblem>().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == problemId);
        if (problem == null) throw new KeyNotFoundException("Problem not found.");

        var ticket = await _ticketRepository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Id == request.TicketId, cancellationToken);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found.");

        var linkRepo = _unitOfWork.Repository<EhcProblemTicketLink>();
        var exists = await linkRepo.ExistsAsync(l => l.TenantId == tenantId && !l.IsDeleted && l.ProblemId == problemId && l.TicketId == request.TicketId);
        if (exists) return;

        await linkRepo.AddAsync(new EhcProblemTicketLink
        {
            TenantId = tenantId,
            ProblemId = problemId,
            TicketId = request.TicketId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = actorUserId
        });

        await AddProblemAuditAsync(problem, "TicketLinked", "Ticket linked", ticket.TicketNumber, actorUserId, new { ticketId = ticket.Id, ticketNumber = ticket.TicketNumber });
        await AddTicketAuditAsync(tenantId, ticket.Id, "LinkedToProblem", "Linked to problem", $"Problem {problem.ProblemNumber}", actorUserId, new { problemId = problem.Id, problemNumber = problem.ProblemNumber });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlinkTicketAsync(Guid problemId, Guid ticketId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        if (problemId == Guid.Empty || ticketId == Guid.Empty) throw new ArgumentException("ProblemId and TicketId are required.");

        var linkRepo = _unitOfWork.Repository<EhcProblemTicketLink>();
        var link = await linkRepo.FirstOrDefaultAsync(l => l.TenantId == tenantId && !l.IsDeleted && l.ProblemId == problemId && l.TicketId == ticketId);
        if (link == null) return;

        link.IsDeleted = true;
        link.DeletedAt = DateTime.UtcNow;
        link.DeletedBy = _currentUserService.UserName ?? "System";
        link.UpdatedAt = DateTime.UtcNow;
        link.UpdatedBy = _currentUserService.UserName ?? "System";
        link.LastModifiedById = TryGetActorUserId();

        await linkRepo.UpdateAsync(link);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<EhcCapaTaskDto> MapCapaTaskDtoAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken)
    {
        var t = await _unitOfWork.Repository<EhcCapaTask>()
            .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == taskId)
            .AsNoTracking()
            .Include(x => x.AssignedToUser)
            .Include(x => x.AssignedDepartment)
            .FirstOrDefaultAsync(cancellationToken);

        if (t == null) throw new KeyNotFoundException("Task not found.");

        return new EhcCapaTaskDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Status = t.Status,
            AssignedToUserId = t.AssignedToUserId,
            AssignedToName = t.AssignedToUser != null ? (t.AssignedToUser.FirstName + " " + t.AssignedToUser.LastName).Trim() : null,
            AssignedDepartmentId = t.AssignedDepartmentId,
            AssignedDepartmentName = t.AssignedDepartment != null ? t.AssignedDepartment.Name : null,
            DueAt = t.DueAt,
            CompletedAt = t.CompletedAt,
            CreatedAt = t.CreatedAt
        };
    }

    public async Task<EhcCapaTaskDto> CreateCapaTaskAsync(Guid problemId, CreateEhcCapaTaskRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        request ??= new CreateEhcCapaTaskRequestDto();
        if (problemId == Guid.Empty) throw new ArgumentException("Problem id is required.");

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.");

        var problem = await _unitOfWork.Repository<EhcProblem>().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == problemId);
        if (problem == null) throw new KeyNotFoundException("Problem not found.");

        var actorUserId = TryGetActorUserId();
        var task = new EhcCapaTask
        {
            TenantId = tenantId,
            ProblemId = problemId,
            Title = title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = EhcCapaTaskStatus.Open,
            AssignedToUserId = request.AssignedToUserId,
            AssignedDepartmentId = request.AssignedDepartmentId,
            DueAt = request.DueAt,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = actorUserId
        };

        await _unitOfWork.Repository<EhcCapaTask>().AddAsync(task);
        await AddProblemAuditAsync(problem, "CapaTaskCreated", "CAPA task created", task.Title, actorUserId, new { taskId = task.Id, title = task.Title });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapCapaTaskDtoAsync(tenantId, task.Id, cancellationToken);
    }

    public async Task<EhcCapaTaskDto> UpdateCapaTaskAsync(Guid problemId, Guid taskId, UpdateEhcCapaTaskRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        request ??= new UpdateEhcCapaTaskRequestDto();
        if (problemId == Guid.Empty || taskId == Guid.Empty) throw new ArgumentException("ProblemId and TaskId are required.");

        var problem = await _unitOfWork.Repository<EhcProblem>().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == problemId);
        if (problem == null) throw new KeyNotFoundException("Problem not found.");

        var repo = _unitOfWork.Repository<EhcCapaTask>();
        var task = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.ProblemId == problemId && t.Id == taskId);
        if (task == null) throw new KeyNotFoundException("Task not found.");

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.");

        var actorUserId = TryGetActorUserId();
        var prevStatus = task.Status;

        task.Title = title;
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.Status = request.Status;
        task.AssignedToUserId = request.AssignedToUserId;
        task.AssignedDepartmentId = request.AssignedDepartmentId;
        task.DueAt = request.DueAt;
        task.CompletedAt = request.Status == EhcCapaTaskStatus.Done ? (task.CompletedAt ?? DateTime.UtcNow) : null;
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedBy = _currentUserService.UserName ?? "System";
        task.LastModifiedById = actorUserId;

        await repo.UpdateAsync(task);
        await AddProblemAuditAsync(problem, "CapaTaskUpdated", "CAPA task updated", $"{task.Title} ({prevStatus} → {task.Status})", actorUserId, new { taskId = task.Id, title = task.Title, status = task.Status.ToString() });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapCapaTaskDtoAsync(tenantId, task.Id, cancellationToken);
    }

    public async Task DeleteCapaTaskAsync(Guid problemId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        if (problemId == Guid.Empty || taskId == Guid.Empty) throw new ArgumentException("ProblemId and TaskId are required.");

        var problem = await _unitOfWork.Repository<EhcProblem>().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == problemId);
        if (problem == null) throw new KeyNotFoundException("Problem not found.");

        var repo = _unitOfWork.Repository<EhcCapaTask>();
        var task = await repo.FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.ProblemId == problemId && t.Id == taskId);
        if (task == null) return;

        task.IsDeleted = true;
        task.DeletedAt = DateTime.UtcNow;
        task.DeletedBy = _currentUserService.UserName ?? "System";
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedBy = _currentUserService.UserName ?? "System";
        task.LastModifiedById = TryGetActorUserId();

        await repo.UpdateAsync(task);
        await AddProblemAuditAsync(problem, "CapaTaskDeleted", "CAPA task deleted", task.Title, TryGetActorUserId(), new { taskId = task.Id, title = task.Title });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EhcProblemLookupItemDto>> SearchProblemsAsync(string q, int limit = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        q = (q ?? string.Empty).Trim();
        if (q.Length < 2) return Array.Empty<EhcProblemLookupItemDto>();

        if (limit < 1) limit = 1;
        if (limit > 50) limit = 50;

        return await _unitOfWork.Repository<EhcProblem>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted && (p.ProblemNumber.Contains(q) || p.Title.Contains(q)))
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .Select(p => new EhcProblemLookupItemDto
            {
                Id = p.Id,
                ProblemNumber = p.ProblemNumber,
                Title = p.Title,
                Status = p.Status,
                Priority = p.Priority,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EhcProblemLookupItemDto>> GetTicketProblemsAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrThrow();
        if (ticketId == Guid.Empty) return Array.Empty<EhcProblemLookupItemDto>();

        var ids = await _unitOfWork.Repository<EhcProblemTicketLink>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && l.TicketId == ticketId)
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => l.ProblemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (ids.Count == 0) return Array.Empty<EhcProblemLookupItemDto>();

        return await _unitOfWork.Repository<EhcProblem>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted && ids.Contains(p.Id))
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new EhcProblemLookupItemDto
            {
                Id = p.Id,
                ProblemNumber = p.ProblemNumber,
                Title = p.Title,
                Status = p.Status,
                Priority = p.Priority,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
