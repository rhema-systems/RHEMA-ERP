using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SLICE 15 — SHE AUDIT MANAGEMENT (T) & STOP-WORK AUTHORITY (U).
//
// Audits: Planned → InProgress → ReportIssued → Closed (Cancelled aside).
// Findings are recorded during execution; their template-linked corrective
// actions are the unified tracker's fifth source; a finding closes only after
// verification, and the audit closes only after every finding does.
//
// Stop-work: Raised → UnderReview → Resolved → Cleared (Cancelled aside).
// Raising is deliberately OPEN (FR-SHE-200 stop-work authority): the
// controller forces non-HR raisers onto the token's employee. The order is
// testimony — there is no general edit path, only the lifecycle actions.
//
// Both use IUnitOfWork's generic repositories (the slice-13/14 idiom); numbers
// are server-assigned via numeric max including soft-deleted rows.
// ============================================================================

public class SheAuditService : ISheAuditService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<SheAuditService> _logger;

    public SheAuditService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider, ILogger<SheAuditService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Reads scope to the authenticated tenant explicitly.
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

    private async Task<Employee> GetOwnedEmployeeAsync(Guid id)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(id);
        if (employee == null || employee.TenantId != GetTenantId() || employee.IsDeleted)
            throw new ArgumentException($"Employee with ID '{id}' not found.");
        return employee;
    }

    private async Task<SheAudit> GetOwnedAuditAsync(Guid id)
    {
        var audit = await _unitOfWork.Repository<SheAudit>().GetByIdAsync(id);
        if (audit == null || audit.TenantId != GetTenantId() || audit.IsDeleted)
            throw new ArgumentException($"Audit with ID '{id}' not found.");
        return audit;
    }

    private IQueryable<SheAudit> FullDetail(Guid tenantId) =>
        _unitOfWork.Repository<SheAudit>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted)
            .AsSplitQuery()
            .Include(a => a.Location)
            .Include(a => a.OrganizationUnit)
            .Include(a => a.LeadAuditor)
            .Include(a => a.ClosedBy)
            .Include(a => a.TeamMembers.Where(m => !m.IsDeleted)).ThenInclude(m => m.Employee)
            .Include(a => a.Findings.Where(f => !f.IsDeleted)).ThenInclude(f => f.ResponsiblePerson)
            .Include(a => a.Findings.Where(f => !f.IsDeleted)).ThenInclude(f => f.VerifiedBy)
            .Include(a => a.Findings.Where(f => !f.IsDeleted)).ThenInclude(f => f.Actions.Where(x => !x.IsDeleted)).ThenInclude(x => x.CorrectiveActionTemplate)
            .Include(a => a.Findings.Where(f => !f.IsDeleted)).ThenInclude(f => f.Actions.Where(x => !x.IsDeleted)).ThenInclude(x => x.AssignedTo);

    private async Task<SheAuditDto> ReadDtoAsync(Guid id, CancellationToken ct)
    {
        var entity = await FullDetail(GetTenantId()).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity == null)
            throw new ArgumentException($"Audit with ID '{id}' not found.");
        return ToDto(entity);
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<SheAuditSummaryDto>> GetAllAsync(SheAuditStatus? status = null, int? year = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _unitOfWork.Repository<SheAudit>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted);
        if (status != null) query = query.Where(a => a.Status == status);
        if (year != null) query = query.Where(a => a.PlannedStartDate.Year == year);

        var rows = await query
            .Include(a => a.Location)
            .Include(a => a.LeadAuditor)
            .Include(a => a.Findings)
            .OrderByDescending(a => a.PlannedStartDate)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSummaryDto).ToList();
    }

    public async Task<SheAuditDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await ReadDtoAsync(id, cancellationToken);

    public async Task<SheAuditDto?> GetByNumberAsync(string auditNumber, CancellationToken cancellationToken = default)
    {
        var entity = await FullDetail(GetTenantId())
            .FirstOrDefaultAsync(a => a.AuditNumber == auditNumber, cancellationToken);
        return entity == null ? null : ToDto(entity);
    }

    public async Task<IEnumerable<SheAuditSummaryDto>> GetUpcomingAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var today = DateTime.UtcNow.Date;
        var cutoff = today.AddDays(Math.Clamp(daysAhead, 1, 365));
        var rows = await _unitOfWork.Repository<SheAudit>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted &&
                               a.Status == SheAuditStatus.Planned &&
                               a.PlannedStartDate >= today && a.PlannedStartDate <= cutoff)
            .Include(a => a.Location)
            .Include(a => a.LeadAuditor)
            .Include(a => a.Findings)
            .OrderBy(a => a.PlannedStartDate)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSummaryDto).ToList();
    }

    // ── lifecycle ────────────────────────────────────────────────────────────

    public async Task<SheAuditDto> CreateAsync(CreateSheAuditDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedEmployeeAsync(dto.LeadAuditorId);
        await GuardOptionalLocationAsync(dto.LocationId);
        await GuardOptionalOrgUnitAsync(dto.OrganizationUnitId);

        string number;
        if (string.IsNullOrWhiteSpace(dto.AuditNumber))
        {
            number = await NextNumberAsync(tenantId, cancellationToken);
        }
        else
        {
            // Trim BEFORE both the probe and the store — the slice-8 trim-store lesson.
            number = dto.AuditNumber.Trim();
            var taken = await _unitOfWork.Repository<SheAudit>()
                .GetQueryable(a => a.TenantId == tenantId && a.AuditNumber == number && !a.IsDeleted)
                .AnyAsync(cancellationToken);
            if (taken)
                throw new InvalidOperationException($"An audit with number '{number}' already exists.");
        }

        var entity = new SheAudit
        {
            TenantId = tenantId,
            AuditNumber = number,
            Title = dto.Title.Trim(),
            Type = dto.Type,
            Standard = dto.Standard,
            Scope = dto.Scope,
            Objectives = dto.Objectives,
            LocationId = dto.LocationId,
            OrganizationUnitId = dto.OrganizationUnitId,
            LeadAuditorId = dto.LeadAuditorId,
            ExternalAuditorName = dto.ExternalAuditorName,
            ExternalAuditorOrganization = dto.ExternalAuditorOrganization,
            PlannedStartDate = dto.PlannedStartDate,
            PlannedEndDate = dto.PlannedEndDate,
            Status = SheAuditStatus.Planned,
            CreatedBy = userId.ToString(),
        };
        await _unitOfWork.Repository<SheAudit>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("SHE audit created: {Number}", entity.AuditNumber);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheAuditDto> UpdateAsync(UpdateSheAuditDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAuditAsync(dto.Id);
        if (entity.Status is not (SheAuditStatus.Planned or SheAuditStatus.InProgress))
            throw new InvalidOperationException(
                $"Audit '{entity.AuditNumber}' is {entity.Status} — planning details can only change while it is Planned or InProgress.");

        await GetOwnedEmployeeAsync(dto.LeadAuditorId);
        await GuardOptionalLocationAsync(dto.LocationId);
        await GuardOptionalOrgUnitAsync(dto.OrganizationUnitId);

        entity.Title = dto.Title.Trim();
        entity.Type = dto.Type;
        entity.Standard = dto.Standard;
        entity.Scope = dto.Scope;
        entity.Objectives = dto.Objectives;
        entity.LocationId = dto.LocationId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.LeadAuditorId = dto.LeadAuditorId;
        entity.ExternalAuditorName = dto.ExternalAuditorName;
        entity.ExternalAuditorOrganization = dto.ExternalAuditorOrganization;
        entity.PlannedStartDate = dto.PlannedStartDate;
        entity.PlannedEndDate = dto.PlannedEndDate;
        Touch(entity, userId);

        await _unitOfWork.Repository<SheAudit>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheAuditDto> StartAsync(StartSheAuditDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAuditAsync(dto.AuditId);
        if (entity.Status != SheAuditStatus.Planned)
            throw new InvalidOperationException($"Audit '{entity.AuditNumber}' is {entity.Status} — only a Planned audit can be started.");

        entity.Status = SheAuditStatus.InProgress;
        entity.ActualStartDate = dto.ActualStartDate;
        Touch(entity, userId);
        await _unitOfWork.Repository<SheAudit>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheAuditDto> IssueReportAsync(IssueSheAuditReportDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAuditAsync(dto.AuditId);
        if (entity.Status != SheAuditStatus.InProgress)
            throw new InvalidOperationException($"Audit '{entity.AuditNumber}' is {entity.Status} — the report is issued from an InProgress audit.");

        entity.Status = SheAuditStatus.ReportIssued;
        entity.Summary = dto.Summary;
        entity.ReportDocumentPath = dto.ReportDocumentPath;
        entity.ReportIssuedDate = dto.ReportIssuedDate;
        entity.ActualEndDate = dto.ActualEndDate ?? dto.ReportIssuedDate;
        Touch(entity, userId);
        await _unitOfWork.Repository<SheAudit>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheAuditDto> CloseAsync(CloseSheAuditDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAuditAsync(dto.AuditId);
        if (entity.Status != SheAuditStatus.ReportIssued)
            throw new InvalidOperationException($"Audit '{entity.AuditNumber}' is {entity.Status} — only a ReportIssued audit closes.");

        var openFindings = await _unitOfWork.Repository<SheAuditFinding>()
            .GetQueryable(f => f.AuditId == entity.Id && !f.IsDeleted && f.Status != SheAuditFindingStatus.Closed)
            .CountAsync(cancellationToken);
        if (openFindings > 0)
            throw new InvalidOperationException(
                $"Audit '{entity.AuditNumber}' still has {openFindings} unclosed finding(s) — every finding must be verified and closed first.");

        await GetOwnedEmployeeAsync(dto.ClosedById);
        entity.Status = SheAuditStatus.Closed;
        entity.ClosedById = dto.ClosedById;
        entity.ClosedDate = dto.ClosedDate;
        entity.ClosureNotes = dto.ClosureNotes;
        Touch(entity, userId);
        await _unitOfWork.Repository<SheAudit>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("SHE audit closed: {Number}", entity.AuditNumber);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheAuditDto> CancelAsync(CancelSheAuditDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAuditAsync(dto.AuditId);
        if (entity.Status is not (SheAuditStatus.Planned or SheAuditStatus.InProgress))
            throw new InvalidOperationException($"Audit '{entity.AuditNumber}' is {entity.Status} — only a Planned or InProgress audit can be cancelled.");

        entity.Status = SheAuditStatus.Cancelled;
        entity.ClosureNotes = dto.Reason;
        Touch(entity, userId);
        await _unitOfWork.Repository<SheAudit>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAuditAsync(id);
        // Executed audits are evidence — findings and reports must survive.
        if (entity.Status is not (SheAuditStatus.Planned or SheAuditStatus.Cancelled))
            throw new InvalidOperationException($"Audit '{entity.AuditNumber}' is {entity.Status} — only a Planned or Cancelled audit can be deleted.");
        await _unitOfWork.Repository<SheAudit>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── team ─────────────────────────────────────────────────────────────────

    public async Task<SheAuditTeamMemberDto> AddTeamMemberAsync(CreateSheAuditTeamMemberDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var audit = await GetOwnedAuditAsync(dto.AuditId);
        var employee = await GetOwnedEmployeeAsync(dto.EmployeeId);

        var duplicate = await _unitOfWork.Repository<SheAuditTeamMember>()
            .GetQueryable(m => m.AuditId == audit.Id && m.EmployeeId == employee.Id && !m.IsDeleted)
            .AnyAsync(cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"{employee.FullName} is already on the audit team.");

        var entity = new SheAuditTeamMember
        {
            TenantId = tenantId,
            AuditId = audit.Id,
            EmployeeId = employee.Id,
            Role = dto.Role.Trim(),
            CreatedBy = userId.ToString(),
        };
        await _unitOfWork.Repository<SheAuditTeamMember>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        entity.Employee = employee;
        return ToDto(entity);
    }

    public async Task<bool> RemoveTeamMemberAsync(Guid teamMemberId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.Repository<SheAuditTeamMember>().GetByIdAsync(teamMemberId);
        if (entity == null || entity.TenantId != GetTenantId() || entity.IsDeleted)
            throw new ArgumentException($"Team member with ID '{teamMemberId}' not found.");
        await _unitOfWork.Repository<SheAuditTeamMember>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── findings ─────────────────────────────────────────────────────────────

    private async Task<SheAuditFinding> GetOwnedFindingAsync(Guid id)
    {
        var finding = await _unitOfWork.Repository<SheAuditFinding>().GetByIdAsync(id);
        if (finding == null || finding.TenantId != GetTenantId() || finding.IsDeleted)
            throw new ArgumentException($"Audit finding with ID '{id}' not found.");
        return finding;
    }

    private async Task<SheAuditFindingDto> ReadFindingDtoAsync(Guid findingId, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<SheAuditFinding>()
            .GetQueryable(f => f.Id == findingId && f.TenantId == tenantId && !f.IsDeleted)
            .Include(f => f.Audit)
            .Include(f => f.ResponsiblePerson)
            .Include(f => f.VerifiedBy)
            .Include(f => f.Actions.Where(a => !a.IsDeleted)).ThenInclude(a => a.CorrectiveActionTemplate)
            .Include(f => f.Actions.Where(a => !a.IsDeleted)).ThenInclude(a => a.AssignedTo)
            .FirstOrDefaultAsync(ct);
        if (entity == null)
            throw new ArgumentException($"Audit finding with ID '{findingId}' not found.");
        return ToDto(entity);
    }

    public async Task<SheAuditFindingDto> AddFindingAsync(CreateSheAuditFindingDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var audit = await GetOwnedAuditAsync(dto.AuditId);
        if (audit.Status != SheAuditStatus.InProgress)
            throw new InvalidOperationException(
                $"Audit '{audit.AuditNumber}' is {audit.Status} — findings are recorded while the audit is InProgress.");

        if (dto.ResponsiblePersonId != null)
            await GetOwnedEmployeeAsync(dto.ResponsiblePersonId.Value);

        // Server-assigned sequence; soft-deleted findings keep their number.
        var maxNumber = await _unitOfWork.Repository<SheAuditFinding>()
            .GetQueryable(f => f.AuditId == audit.Id)
            .Select(f => (int?)f.FindingNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var entity = new SheAuditFinding
        {
            TenantId = tenantId,
            AuditId = audit.Id,
            FindingNumber = maxNumber + 1,
            Classification = dto.Classification,
            ClauseReference = dto.ClauseReference,
            Description = dto.Description,
            Evidence = dto.Evidence,
            Status = SheAuditFindingStatus.Open,
            ResponsiblePersonId = dto.ResponsiblePersonId,
            DueDate = dto.DueDate,
            CreatedBy = userId.ToString(),
        };
        await _unitOfWork.Repository<SheAuditFinding>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadFindingDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheAuditFindingDto> UpdateFindingAsync(UpdateSheAuditFindingDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFindingAsync(dto.Id);
        if (entity.Status == SheAuditFindingStatus.Closed)
            throw new InvalidOperationException($"Finding #{entity.FindingNumber} is closed and can no longer be edited.");

        if (dto.ResponsiblePersonId != null)
            await GetOwnedEmployeeAsync(dto.ResponsiblePersonId.Value);

        entity.Classification = dto.Classification;
        entity.ClauseReference = dto.ClauseReference;
        entity.Description = dto.Description;
        entity.Evidence = dto.Evidence;
        entity.ResponsiblePersonId = dto.ResponsiblePersonId;
        entity.DueDate = dto.DueDate;
        entity.ResolutionNotes = dto.ResolutionNotes;
        entity.ResolvedDate = dto.ResolvedDate;
        // Recording a resolution moves an open finding forward; verification stays a separate act.
        if (entity.Status == SheAuditFindingStatus.Open && entity.ResolvedDate != null)
            entity.Status = SheAuditFindingStatus.Resolved;
        Touch(entity, userId);

        await _unitOfWork.Repository<SheAuditFinding>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadFindingDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheAuditFindingDto> VerifyFindingAsync(VerifySheAuditFindingDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFindingAsync(dto.FindingId);
        if (entity.Status != SheAuditFindingStatus.Resolved)
            throw new InvalidOperationException(
                $"Finding #{entity.FindingNumber} is {entity.Status} — only a Resolved finding can be verified for effectiveness.");

        await GetOwnedEmployeeAsync(dto.VerifiedById);
        entity.Status = SheAuditFindingStatus.Verified;
        entity.VerifiedById = dto.VerifiedById;
        entity.VerifiedDate = dto.VerifiedDate;
        entity.VerificationNotes = dto.VerificationNotes;
        Touch(entity, userId);
        await _unitOfWork.Repository<SheAuditFinding>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadFindingDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheAuditFindingDto> CloseFindingAsync(Guid findingId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFindingAsync(findingId);
        if (entity.Status != SheAuditFindingStatus.Verified)
            throw new InvalidOperationException($"Finding #{entity.FindingNumber} is {entity.Status} — a finding closes after verification.");

        var openActions = await _unitOfWork.Repository<SheAuditFindingAction>()
            .GetQueryable(a => a.FindingId == entity.Id && !a.IsDeleted &&
                               a.Status != SheCorrectiveActionStatus.Completed &&
                               a.Status != SheCorrectiveActionStatus.Verified &&
                               a.Status != SheCorrectiveActionStatus.Cancelled)
            .CountAsync(cancellationToken);
        if (openActions > 0)
            throw new InvalidOperationException(
                $"Finding #{entity.FindingNumber} still has {openActions} open corrective action(s).");

        entity.Status = SheAuditFindingStatus.Closed;
        Touch(entity, userId);
        await _unitOfWork.Repository<SheAuditFinding>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadFindingDtoAsync(entity.Id, cancellationToken);
    }

    // ── finding actions ──────────────────────────────────────────────────────

    public async Task<SheAuditFindingActionDto> AddFindingActionAsync(CreateSheAuditFindingActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var finding = await GetOwnedFindingAsync(dto.FindingId);
        if (finding.Status == SheAuditFindingStatus.Closed)
            throw new InvalidOperationException($"Finding #{finding.FindingNumber} is closed — no further actions can be attached.");

        var template = await _unitOfWork.Repository<SheCorrectiveActionTemplate>().GetByIdAsync(dto.CorrectiveActionTemplateId);
        if (template == null || template.TenantId != tenantId || template.IsDeleted)
            throw new ArgumentException($"Corrective-action template with ID '{dto.CorrectiveActionTemplateId}' not found.");

        Employee? assignee = null;
        if (dto.AssignedToId != null)
            assignee = await GetOwnedEmployeeAsync(dto.AssignedToId.Value);

        var entity = new SheAuditFindingAction
        {
            TenantId = tenantId,
            FindingId = finding.Id,
            CorrectiveActionTemplateId = template.Id,
            Status = dto.Status,
            DueDate = dto.DueDate ?? (template.DefaultDeadlineDays is int d ? DateTime.UtcNow.Date.AddDays(d) : null),
            AssignedToId = dto.AssignedToId,
            CreatedBy = userId.ToString(),
        };
        await _unitOfWork.Repository<SheAuditFindingAction>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        entity.CorrectiveActionTemplate = template;
        entity.AssignedTo = assignee;
        return ToDto(entity);
    }

    public async Task<SheAuditFindingActionDto> UpdateFindingActionAsync(UpdateSheAuditFindingActionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<SheAuditFindingAction>()
            .GetQueryable(a => a.Id == dto.Id && a.TenantId == tenantId && !a.IsDeleted)
            .Include(a => a.Finding)
            .Include(a => a.CorrectiveActionTemplate)
            .Include(a => a.AssignedTo)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Finding action with ID '{dto.Id}' not found.");
        if (entity.Finding.Status == SheAuditFindingStatus.Closed)
            throw new InvalidOperationException($"Finding #{entity.Finding.FindingNumber} is closed — its actions can no longer change.");

        Employee? assignee = null;
        if (dto.AssignedToId != null && dto.AssignedToId != entity.AssignedToId)
            assignee = await GetOwnedEmployeeAsync(dto.AssignedToId.Value);

        entity.Status = dto.Status;
        entity.DueDate = dto.DueDate;
        entity.CompletionDate = dto.CompletionDate;
        entity.CompletionNotes = dto.CompletionNotes;
        entity.AssignedToId = dto.AssignedToId;
        if (assignee != null) entity.AssignedTo = assignee;
        Touch(entity, userId);

        await _unitOfWork.Repository<SheAuditFindingAction>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task GuardOptionalLocationAsync(Guid? locationId)
    {
        if (locationId == null) return;
        var location = await _unitOfWork.Repository<Location>().GetByIdAsync(locationId.Value);
        if (location == null || location.TenantId != GetTenantId() || location.IsDeleted)
            throw new ArgumentException($"Location with ID '{locationId}' not found.");
    }

    private async Task GuardOptionalOrgUnitAsync(Guid? orgUnitId)
    {
        if (orgUnitId == null) return;
        var unit = await _unitOfWork.Repository<OrganizationUnit>().GetByIdAsync(orgUnitId.Value);
        if (unit == null || unit.TenantId != GetTenantId() || unit.IsDeleted)
            throw new ArgumentException($"Organization unit with ID '{orgUnitId}' not found.");
    }

    private async Task<string> NextNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var prefix = $"AUD-{DateTime.UtcNow.Year}-";
        // Numeric max including soft-deleted rows — the string-ordering re-issue trap.
        var numbers = await _unitOfWork.Repository<SheAudit>()
            .GetQueryableIncludingDeleted(a => a.TenantId == tenantId && a.AuditNumber.StartsWith(prefix))
            .Select(a => a.AuditNumber)
            .ToListAsync(ct);
        var max = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{max + 1:D4}";
    }

    private static void Touch(TenantEntity entity, Guid userId)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    private static SheAuditDto ToDto(SheAudit e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        AuditNumber = e.AuditNumber,
        Title = e.Title,
        Type = e.Type,
        Standard = e.Standard,
        Scope = e.Scope,
        Objectives = e.Objectives,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        OrganizationUnitId = e.OrganizationUnitId,
        OrganizationUnitName = e.OrganizationUnit?.Name,
        LeadAuditorId = e.LeadAuditorId,
        LeadAuditorName = e.LeadAuditor?.FullName ?? string.Empty,
        ExternalAuditorName = e.ExternalAuditorName,
        ExternalAuditorOrganization = e.ExternalAuditorOrganization,
        PlannedStartDate = e.PlannedStartDate,
        PlannedEndDate = e.PlannedEndDate,
        ActualStartDate = e.ActualStartDate,
        ActualEndDate = e.ActualEndDate,
        Status = e.Status,
        Summary = e.Summary,
        ReportDocumentPath = e.ReportDocumentPath,
        ReportIssuedDate = e.ReportIssuedDate,
        ClosedDate = e.ClosedDate,
        ClosedById = e.ClosedById,
        ClosedByName = e.ClosedBy?.FullName,
        ClosureNotes = e.ClosureNotes,
        TeamMembers = e.TeamMembers.Where(m => !m.IsDeleted).Select(ToDto).ToList(),
        Findings = e.Findings.Where(f => !f.IsDeleted).OrderBy(f => f.FindingNumber).Select(ToDto).ToList(),
    };

    private static SheAuditSummaryDto ToSummaryDto(SheAudit e) => new()
    {
        Id = e.Id,
        AuditNumber = e.AuditNumber,
        Title = e.Title,
        Type = e.Type,
        Standard = e.Standard,
        LocationName = e.Location?.Name,
        LeadAuditorName = e.LeadAuditor?.FullName ?? string.Empty,
        PlannedStartDate = e.PlannedStartDate,
        Status = e.Status,
        FindingCount = e.Findings.Count(f => !f.IsDeleted),
        OpenFindingCount = e.Findings.Count(f => !f.IsDeleted && f.Status != SheAuditFindingStatus.Closed),
    };

    private static SheAuditTeamMemberDto ToDto(SheAuditTeamMember e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        AuditId = e.AuditId,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        Role = e.Role,
    };

    private static SheAuditFindingDto ToDto(SheAuditFinding e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        AuditId = e.AuditId,
        AuditNumber = e.Audit?.AuditNumber,
        FindingNumber = e.FindingNumber,
        Classification = e.Classification,
        ClauseReference = e.ClauseReference,
        Description = e.Description,
        Evidence = e.Evidence,
        Status = e.Status,
        ResponsiblePersonId = e.ResponsiblePersonId,
        ResponsiblePersonName = e.ResponsiblePerson?.FullName,
        DueDate = e.DueDate,
        ResolutionNotes = e.ResolutionNotes,
        ResolvedDate = e.ResolvedDate,
        VerifiedById = e.VerifiedById,
        VerifiedByName = e.VerifiedBy?.FullName,
        VerifiedDate = e.VerifiedDate,
        VerificationNotes = e.VerificationNotes,
        Actions = e.Actions.Where(a => !a.IsDeleted).Select(ToDto).ToList(),
    };

    private static SheAuditFindingActionDto ToDto(SheAuditFindingAction e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        FindingId = e.FindingId,
        CorrectiveActionTemplateId = e.CorrectiveActionTemplateId,
        CorrectiveActionTemplateTitle = e.CorrectiveActionTemplate?.Title ?? string.Empty,
        Status = e.Status,
        DueDate = e.DueDate,
        CompletionDate = e.CompletionDate,
        CompletionNotes = e.CompletionNotes,
        AssignedToId = e.AssignedToId,
        AssignedToName = e.AssignedTo?.FullName,
    };
}

public class SheStopWorkService : ISheStopWorkService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<SheStopWorkService> _logger;

    public SheStopWorkService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider, ILogger<SheStopWorkService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Reads scope to the authenticated tenant explicitly.
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

    private async Task<Employee> GetOwnedEmployeeAsync(Guid id)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(id);
        if (employee == null || employee.TenantId != GetTenantId() || employee.IsDeleted)
            throw new ArgumentException($"Employee with ID '{id}' not found.");
        return employee;
    }

    private IQueryable<SheStopWorkOrder> WithNavigations(Guid tenantId) =>
        _unitOfWork.Repository<SheStopWorkOrder>()
            .GetQueryable(o => o.TenantId == tenantId && !o.IsDeleted)
            .Include(o => o.RaisedBy)
            .Include(o => o.Location)
            .Include(o => o.PermitToWork)
            .Include(o => o.Hazard)
            .Include(o => o.Incident)
            .Include(o => o.RoutedTo)
            .Include(o => o.ResolvedBy)
            .Include(o => o.ClearedBy)
            .Include(o => o.CancelledBy);

    private async Task<SheStopWorkOrderDto> ReadDtoAsync(Guid id, CancellationToken ct)
    {
        var entity = await WithNavigations(GetTenantId()).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (entity == null)
            throw new ArgumentException($"Stop-work order with ID '{id}' not found.");
        return ToDto(entity);
    }

    private async Task<SheStopWorkOrder> GetOwnedOrderAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SheStopWorkOrder>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId() || entity.IsDeleted)
            throw new ArgumentException($"Stop-work order with ID '{id}' not found.");
        return entity;
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<SheStopWorkOrderDto>> GetAllAsync(SheStopWorkStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = WithNavigations(GetTenantId());
        if (status != null) query = query.Where(o => o.Status == status);
        var rows = await query.OrderByDescending(o => o.RaisedDate).ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<SheStopWorkOrderDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await ReadDtoAsync(id, cancellationToken);

    public async Task<SheStopWorkOrderDto?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        var entity = await WithNavigations(GetTenantId())
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, cancellationToken);
        return entity == null ? null : ToDto(entity);
    }

    public async Task<IEnumerable<SheStopWorkOrderDto>> GetMineAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var rows = await WithNavigations(GetTenantId())
            .Where(o => o.RaisedById == employeeId)
            .OrderByDescending(o => o.RaisedDate)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    // ── lifecycle ────────────────────────────────────────────────────────────

    public async Task<SheStopWorkOrderDto> RaiseAsync(CreateSheStopWorkOrderDto dto, Guid raisedById, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedEmployeeAsync(raisedById);

        if (dto.LocationId != null)
        {
            var location = await _unitOfWork.Repository<Location>().GetByIdAsync(dto.LocationId.Value);
            if (location == null || location.TenantId != tenantId || location.IsDeleted)
                throw new ArgumentException($"Location with ID '{dto.LocationId}' not found.");
        }
        if (dto.PermitToWorkId != null)
        {
            var permit = await _unitOfWork.Repository<ShePermitToWork>().GetByIdAsync(dto.PermitToWorkId.Value);
            if (permit == null || permit.TenantId != tenantId || permit.IsDeleted)
                throw new ArgumentException($"Permit to work with ID '{dto.PermitToWorkId}' not found.");
        }
        if (dto.HazardId != null)
        {
            var hazard = await _unitOfWork.Repository<SheHazard>().GetByIdAsync(dto.HazardId.Value);
            if (hazard == null || hazard.TenantId != tenantId || hazard.IsDeleted)
                throw new ArgumentException($"Hazard with ID '{dto.HazardId}' not found.");
        }
        if (dto.IncidentId != null)
        {
            var incident = await _unitOfWork.Repository<SafetyIncident>().GetByIdAsync(dto.IncidentId.Value);
            if (incident == null || incident.TenantId != tenantId || incident.IsDeleted)
                throw new ArgumentException($"Incident with ID '{dto.IncidentId}' not found.");
        }

        var entity = new SheStopWorkOrder
        {
            TenantId = tenantId,
            OrderNumber = await NextNumberAsync(tenantId, cancellationToken),
            RaisedById = raisedById,
            RaisedDate = dto.RaisedDate,
            LocationId = dto.LocationId,
            SpecificArea = dto.SpecificArea,
            WorkDescription = dto.WorkDescription,
            ReasonDescription = dto.ReasonDescription,
            ImmediateActionsTaken = dto.ImmediateActionsTaken,
            PermitToWorkId = dto.PermitToWorkId,
            HazardId = dto.HazardId,
            IncidentId = dto.IncidentId,
            Status = SheStopWorkStatus.Raised,
            CreatedBy = userId.ToString(),
        };
        await _unitOfWork.Repository<SheStopWorkOrder>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Stop-work order raised: {Number}", entity.OrderNumber);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheStopWorkOrderDto> RouteAsync(RouteSheStopWorkOrderDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOrderAsync(dto.OrderId);
        if (entity.Status is not (SheStopWorkStatus.Raised or SheStopWorkStatus.UnderReview))
            throw new InvalidOperationException($"Stop-work order '{entity.OrderNumber}' is {entity.Status} — only a Raised or UnderReview order can be routed.");

        await GetOwnedEmployeeAsync(dto.RoutedToId);
        entity.Status = SheStopWorkStatus.UnderReview;
        entity.RoutedToId = dto.RoutedToId;
        entity.RoutedDate = dto.RoutedDate;
        Touch(entity, userId);
        await _unitOfWork.Repository<SheStopWorkOrder>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheStopWorkOrderDto> ResolveAsync(ResolveSheStopWorkOrderDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOrderAsync(dto.OrderId);
        if (entity.Status is not (SheStopWorkStatus.Raised or SheStopWorkStatus.UnderReview))
            throw new InvalidOperationException($"Stop-work order '{entity.OrderNumber}' is {entity.Status} — only a Raised or UnderReview order can be resolved.");

        await GetOwnedEmployeeAsync(dto.ResolvedById);
        entity.Status = SheStopWorkStatus.Resolved;
        entity.ResolutionDescription = dto.ResolutionDescription;
        entity.ResolvedById = dto.ResolvedById;
        entity.ResolvedDate = dto.ResolvedDate;
        Touch(entity, userId);
        await _unitOfWork.Repository<SheStopWorkOrder>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheStopWorkOrderDto> ClearAsync(ClearSheStopWorkOrderDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOrderAsync(dto.OrderId);
        // Work resumes only once the danger is resolved — clearing an unresolved order
        // would be authorising the exact condition somebody stopped the job over.
        if (entity.Status != SheStopWorkStatus.Resolved)
            throw new InvalidOperationException($"Stop-work order '{entity.OrderNumber}' is {entity.Status} — work is cleared to resume only from a Resolved order.");

        await GetOwnedEmployeeAsync(dto.ClearedById);
        entity.Status = SheStopWorkStatus.Cleared;
        entity.ClearedById = dto.ClearedById;
        entity.ClearedDate = dto.ClearedDate;
        entity.ClearanceNotes = dto.ClearanceNotes;
        Touch(entity, userId);
        await _unitOfWork.Repository<SheStopWorkOrder>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Stop-work order cleared: {Number}", entity.OrderNumber);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheStopWorkOrderDto> CancelAsync(CancelSheStopWorkOrderDto dto, Guid cancelledById, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOrderAsync(dto.OrderId);
        if (entity.Status is not (SheStopWorkStatus.Raised or SheStopWorkStatus.UnderReview))
            throw new InvalidOperationException($"Stop-work order '{entity.OrderNumber}' is {entity.Status} — only a Raised or UnderReview order can be cancelled.");

        entity.Status = SheStopWorkStatus.Cancelled;
        entity.CancelledById = cancelledById;
        entity.CancelledDate = DateTime.UtcNow;
        entity.CancellationReason = dto.Reason;
        Touch(entity, userId);
        await _unitOfWork.Repository<SheStopWorkOrder>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    private async Task<string> NextNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var prefix = $"SWO-{DateTime.UtcNow.Year}-";
        var numbers = await _unitOfWork.Repository<SheStopWorkOrder>()
            .GetQueryableIncludingDeleted(o => o.TenantId == tenantId && o.OrderNumber.StartsWith(prefix))
            .Select(o => o.OrderNumber)
            .ToListAsync(ct);
        var max = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{max + 1:D4}";
    }

    private static void Touch(TenantEntity entity, Guid userId)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    private static SheStopWorkOrderDto ToDto(SheStopWorkOrder e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        OrderNumber = e.OrderNumber,
        RaisedById = e.RaisedById,
        RaisedByName = e.RaisedBy?.FullName ?? string.Empty,
        RaisedDate = e.RaisedDate,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        SpecificArea = e.SpecificArea,
        WorkDescription = e.WorkDescription,
        ReasonDescription = e.ReasonDescription,
        ImmediateActionsTaken = e.ImmediateActionsTaken,
        PermitToWorkId = e.PermitToWorkId,
        PermitNumber = e.PermitToWork?.PermitNumber,
        HazardId = e.HazardId,
        HazardName = e.Hazard?.Name,
        IncidentId = e.IncidentId,
        IncidentNumber = e.Incident?.IncidentNumber,
        Status = e.Status,
        RoutedToId = e.RoutedToId,
        RoutedToName = e.RoutedTo?.FullName,
        RoutedDate = e.RoutedDate,
        ResolutionDescription = e.ResolutionDescription,
        ResolvedById = e.ResolvedById,
        ResolvedByName = e.ResolvedBy?.FullName,
        ResolvedDate = e.ResolvedDate,
        ClearedById = e.ClearedById,
        ClearedByName = e.ClearedBy?.FullName,
        ClearedDate = e.ClearedDate,
        ClearanceNotes = e.ClearanceNotes,
        CancelledById = e.CancelledById,
        CancelledByName = e.CancelledBy?.FullName,
        CancelledDate = e.CancelledDate,
        CancellationReason = e.CancellationReason,
    };
}
