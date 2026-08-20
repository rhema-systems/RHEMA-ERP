using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The exit register (area 9b, FRD §A1.10 / §3.A.2).
/// </summary>
/// <remarks>
/// <para><b>One record per exit, whatever the route.</b> Before this service the only way out of
/// the organisation was a disciplinary case, so resignation, retirement, contract expiry and death
/// had enum members and no route at all. The disciplinary route now writes here too, through
/// <c>DisciplinaryActionId</c> — two exit stores that can disagree is how 29 disciplinary
/// terminations came to sit against employees who were all still <c>StaffStatus = Active</c>.</para>
///
/// <para><b>Status is never taken from the client.</b> A separation starts as a draft and every
/// move from there — approval, clearance, settlement, completion — is its own endpoint with its own
/// rule and its own gate. There is deliberately no "set status" path, because FR-HR-091 makes
/// clearance a gate on the settlement and a settable status would walk straight past it.</para>
/// </remarks>
public class SeparationService : ISeparationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICompanyHrPolicyProvider _policyProvider;
    private readonly ILogger<SeparationService> _logger;

    public SeparationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ICompanyHrPolicyProvider policyProvider,
        ILogger<SeparationService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _policyProvider = policyProvider;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>
    /// Tenant-scoped separations with every navigation the DTOs read.
    /// </summary>
    /// <remarks>
    /// The includes are here rather than per-query on purpose: a list read that omits one renders
    /// its name column as "—" while still returning 200, which is the single most common defect
    /// shape in the ported HR code.
    /// </remarks>
    private IQueryable<EmployeeSeparation> Scoped(Guid tenantId)
        => _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .Include(s => s.Employee).ThenInclude(e => e.Position)
            .Include(s => s.Employee).ThenInclude(e => e.OrganizationUnit)
            .Include(s => s.InitiatedBy)
            .Include(s => s.ApprovedBy)
            .Include(s => s.CancelledBy)
            .Include(s => s.SubmittedBy)
            .Include(s => s.RejectedBy)
            .Where(s => s.TenantId == tenantId && !s.IsDeleted);

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PagedResult<EmployeeSeparationListDto>> GetPagedAsync(
        EmployeeSeparationQueryDto query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tenantId = GetTenantId();
        var page = query.PageNumber < 1 ? 1 : query.PageNumber;
        var size = query.PageSize is < 1 or > 200 ? 25 : query.PageSize;

        var q = Scoped(tenantId);

        if (query.EmployeeId is { } employeeId && employeeId != Guid.Empty)
            q = q.Where(s => s.EmployeeId == employeeId);

        if (query.SeparationType is { } type)
            q = q.Where(s => s.SeparationType == type);

        if (query.Status is { } status)
            q = q.Where(s => s.Status == status);

        if (query.EffectiveFrom is { } from)
            q = q.Where(s => s.EffectiveDate != null && s.EffectiveDate >= from);

        if (query.EffectiveTo is { } to)
            q = q.Where(s => s.EffectiveDate != null && s.EffectiveDate <= to);

        if (query.OnlyUnappliedToEmployee == true)
            q = q.Where(s => s.Status == SeparationStatus.Completed && s.EmployeeRecordUpdatedOn == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(s =>
                s.SeparationNumber.Contains(term)
                || s.Employee.FirstName.Contains(term)
                || s.Employee.LastName.Contains(term)
                || (s.Employee.EmployeeNumber != null && s.Employee.EmployeeNumber.Contains(term)));
        }

        var totalCount = await q.CountAsync(cancellationToken);

        // Ordered on two keys, the second of them unique. A single non-unique key leaves the page
        // boundary to the server's whim, which is how area 17's audit found a row that appeared on
        // two pages and on neither.
        var items = await q
            .OrderByDescending(s => s.InitiatedOn)
            .ThenByDescending(s => s.SeparationNumber)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return new PagedResult<EmployeeSeparationListDto>
        {
            Items = items.Select(ToListDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = size,
        };
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await Scoped(tenantId).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return entity == null ? null : ToDetailDto(entity);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EmployeeSeparationListDto>> GetForEmployeeAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var items = await Scoped(tenantId)
            .Where(s => s.EmployeeId == employeeId)
            .OrderByDescending(s => s.InitiatedOn)
            .ThenByDescending(s => s.SeparationNumber)
            .ToListAsync(cancellationToken);

        return items.Select(ToListDto).ToList();
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> CreateAsync(
        CreateEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        // Checked explicitly: [Required] is a no-op on a non-nullable Guid, so an omitted employee
        // arrives as Guid.Empty and would otherwise be reported as "employee 00000000-… not found",
        // an answer about the wrong thing.
        if (dto.EmployeeId == Guid.Empty)
            throw new ArgumentException("Name the employee who is leaving.");

        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && e.TenantId == tenantId && !e.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Employee with ID '{dto.EmployeeId}' not found.");

        if (employee.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException(
                $"{employee.FirstName} {employee.LastName} has already left — their staff status is Terminated.");

        // One open separation at a time. Without this an employee could be resigning and retiring
        // simultaneously, each with its own clearance run and its own settlement.
        var open = await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .AnyAsync(s => s.TenantId == tenantId
                        && !s.IsDeleted
                        && s.EmployeeId == dto.EmployeeId
                        && s.Status != SeparationStatus.Completed
                        && s.Status != SeparationStatus.Cancelled
                        && s.Status != SeparationStatus.Rejected, cancellationToken);

        if (open)
            throw new InvalidOperationException(
                $"{employee.FirstName} {employee.LastName} already has a separation in progress. "
                + "Cancel it before raising another.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var initiatedOn = dto.InitiatedOn ?? today;

        if (initiatedOn > today)
            throw new InvalidOperationException("A separation cannot be raised with a future initiation date.");

        if (employee.DateEmployed is { } employed && initiatedOn < employed)
            throw new InvalidOperationException(
                $"The separation is dated {initiatedOn:yyyy-MM-dd}, before this employee joined on {employed:yyyy-MM-dd}.");

        if (dto.LastWorkingDay is { } lastDay && dto.EffectiveDate is { } effective && lastDay > effective)
            throw new InvalidOperationException(
                "The last working day cannot fall after the date employment ends.");

        var settings = await _policyProvider.GetAsync(cancellationToken);
        var noticeDays = dto.NoticeDays ?? DefaultNoticeDays(dto.SeparationType, settings);

        if (noticeDays is < 0)
            throw new InvalidOperationException("Notice days cannot be negative.");

        var entity = new EmployeeSeparation
        {
            // Set explicitly: the DbContext auto-stamp is dead in this codebase, and a missing
            // TenantId surfaces as a foreign-key 547 on save rather than as anything readable.
            TenantId = tenantId,
            SeparationNumber = await NextNumberAsync(tenantId, initiatedOn, cancellationToken),
            EmployeeId = employee.Id,
            SeparationType = dto.SeparationType,
            Status = SeparationStatus.Draft,
            ReasonCategory = dto.ReasonCategory,
            ReasonNotes = string.IsNullOrWhiteSpace(dto.ReasonNotes) ? null : dto.ReasonNotes.Trim(),
            InitiatedOn = initiatedOn,
            NoticeGivenOn = dto.NoticeGivenOn,
            NoticeDays = noticeDays,
            LastWorkingDay = dto.LastWorkingDay,
            EffectiveDate = dto.EffectiveDate,
            InitiatedById = actorEmployeeId,
            IsSystemInitiated = actorEmployeeId is null,
            IsEligibleForRehire = dto.IsEligibleForRehire,
            EligibleForRehireDate = dto.EligibleForRehireDate,
            RehireRestrictions = string.IsNullOrWhiteSpace(dto.RehireRestrictions) ? null : dto.RehireRestrictions.Trim(),
            DisciplinaryActionId = dto.DisciplinaryActionId,
            AbsenceDays = dto.AbsenceDays,
        };

        await _unitOfWork.Repository<EmployeeSeparation>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Separation {Number} raised for employee {EmployeeId} ({Type})",
            entity.SeparationNumber, entity.EmployeeId, entity.SeparationType);

        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> UpdateAsync(
        Guid id, UpdateEmployeeSeparationDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status != SeparationStatus.Draft)
            throw new InvalidOperationException(
                $"This separation is {entity.Status} and can no longer be amended. "
                + "Cancel it and raise a new one if the details are wrong.");

        if (dto.SeparationType is { } type) entity.SeparationType = type;
        if (dto.ReasonCategory is { } reason) entity.ReasonCategory = reason;
        if (dto.ReasonNotes is not null)
            entity.ReasonNotes = string.IsNullOrWhiteSpace(dto.ReasonNotes) ? null : dto.ReasonNotes.Trim();
        if (dto.NoticeGivenOn is { } noticeGiven) entity.NoticeGivenOn = noticeGiven;
        if (dto.NoticeDays is { } noticeDays)
        {
            if (noticeDays < 0) throw new InvalidOperationException("Notice days cannot be negative.");
            entity.NoticeDays = noticeDays;
        }
        if (dto.LastWorkingDay is { } lastDay) entity.LastWorkingDay = lastDay;
        if (dto.EffectiveDate is { } effective) entity.EffectiveDate = effective;
        if (dto.IsEligibleForRehire is { } rehire) entity.IsEligibleForRehire = rehire;
        if (dto.EligibleForRehireDate is { } rehireDate) entity.EligibleForRehireDate = rehireDate;
        if (dto.AbsenceDays is { } absence)
        {
            if (absence < 0) throw new InvalidOperationException("Days of absence cannot be negative.");
            entity.AbsenceDays = absence;
        }
        if (dto.RehireRestrictions is not null)
            entity.RehireRestrictions = string.IsNullOrWhiteSpace(dto.RehireRestrictions) ? null : dto.RehireRestrictions.Trim();

        if (entity.LastWorkingDay is { } lwd && entity.EffectiveDate is { } eff && lwd > eff)
            throw new InvalidOperationException("The last working day cannot fall after the date employment ends.");

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> CancelAsync(
        Guid id, CancelEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("Give a reason for cancelling the separation.");

        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status == SeparationStatus.Completed)
            throw new InvalidOperationException(
                "This separation has completed — the employee has left and been paid. It cannot be cancelled.");

        if (entity.Status == SeparationStatus.Cancelled)
            throw new InvalidOperationException("This separation is already cancelled.");

        entity.Status = SeparationStatus.Cancelled;
        entity.CancelledOn = DateTime.UtcNow;
        entity.CancelledById = actorEmployeeId;
        entity.CancellationReason = dto.Reason.Trim();

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Separation {Number} cancelled", entity.SeparationNumber);
        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status == SeparationStatus.Completed)
            throw new InvalidOperationException(
                "A completed separation is the record of someone's exit and its settlement. It cannot be deleted.");

        await _unitOfWork.Repository<EmployeeSeparation>().DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> SubmitAsync(
        Guid id, SubmitEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status != SeparationStatus.Draft)
            throw new InvalidOperationException(
                $"This separation is {entity.Status}; only a draft can be submitted.");

        // A resignation IS its letter, and its date is where the notice clock starts. Without it
        // there is no way to tell notice served from notice owed, and FR-HR-184's notice pay is
        // computed from exactly that difference. Other routes carry no notice date by nature —
        // nobody serves notice on a bereavement — so this is asked of resignation alone.
        if (entity.SeparationType == EmployeeTerminationType.VoluntaryResignation && entity.NoticeGivenOn is null)
            throw new InvalidOperationException(
                "Record the date notice was given before submitting a resignation — the notice "
                + "period, and any shortfall to be paid, are both counted from it.");

        // Derive what follows rather than demanding it twice. The last working day is the day
        // notice runs out unless someone says otherwise.
        if (entity.LastWorkingDay is null && entity.NoticeGivenOn is { } given && entity.NoticeDays is { } days)
            entity.LastWorkingDay = given.AddDays(days);

        entity.EffectiveDate ??= entity.LastWorkingDay;

        if (entity.EffectiveDate is null)
            throw new InvalidOperationException(
                "This separation has no end date and none can be worked out from the notice given. "
                + "Set the effective date, or the notice date and period, before submitting.");

        if (entity.LastWorkingDay is { } lwd && entity.EffectiveDate is { } eff && lwd > eff)
            throw new InvalidOperationException("The last working day cannot fall after the date employment ends.");

        // FR-HR-092's exception is decided here and frozen, not evaluated at approval time. The
        // threshold is tenant policy and policy can change; who was entitled to sign a separation
        // must not change underneath it after it was queued.
        var settings = await _policyProvider.GetAsync(cancellationToken);
        entity.IsProcedural =
            entity.AbsenceDays is { } absent
            && settings.ProceduralAbsenceDays > 0
            && absent >= settings.ProceduralAbsenceDays;

        entity.Status = SeparationStatus.PendingApproval;
        entity.SubmittedOn = DateTime.UtcNow;
        entity.SubmittedById = actorEmployeeId;

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            entity.ReasonNotes = string.IsNullOrWhiteSpace(entity.ReasonNotes)
                ? dto.Notes.Trim()
                : $"{entity.ReasonNotes}\n\n{dto.Notes.Trim()}";
        }

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Separation {Number} submitted for approval (effective {Effective:yyyy-MM-dd})",
            entity.SeparationNumber, entity.EffectiveDate);

        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    // ── FR-HR-092: the decision ───────────────────────────────────────────────

    /// <summary>
    /// Whether the caller holds the Managing Director role, under either of its two seeded
    /// spellings.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>SuperAdmin is deliberately not here.</b> FR-HR-092 exists to put a named officer's
    /// signature on the ending of someone's employment; a technical superuser signing it is the
    /// thing the control is for, not an exemption from it. SuperAdmin can still read and
    /// administer, and can grant somebody the role.
    /// </remarks>
    private bool IsManagingDirector =>
        _currentUserProvider.Roles.Any(r =>
            string.Equals(r, Constants.Roles.ManagingDirector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r, Constants.Roles.TdcManagingDirector, StringComparison.OrdinalIgnoreCase));

    private bool IsHrActor =>
        _currentUserProvider.Roles.Any(r =>
            string.Equals(r, Constants.Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r, Constants.Roles.Hr, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r, Constants.Roles.LegacyHrUser, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// FR-HR-092 in one place: the MD may decide any separation, HR only a procedural one.
    /// </summary>
    /// <remarks>
    /// This is read off the record rather than expressed as a permission because no permission can
    /// say "may approve this one but not that one" — and stacking a role attribute onto a policy
    /// attribute would AND them, admitting nobody. The endpoint therefore carries a plain
    /// <c>[Authorize]</c> and this decides.
    /// </remarks>
    private void RequireDecisionAuthority(EmployeeSeparation separation)
    {
        if (IsManagingDirector) return;

        if (separation.IsProcedural && IsHrActor) return;

        throw new UnauthorizedAccessException(
            separation.IsProcedural
                ? "Only HR or the Managing Director may decide a separation."
                : "Only the Managing Director may sign this separation. It is not procedural — "
                  + "under FR-HR-092 HR may approve only a termination for absence beyond the "
                  + "tenant's procedural threshold.");
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> ApproveAsync(
        Guid id, ApproveEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status != SeparationStatus.PendingApproval)
            throw new InvalidOperationException(
                $"This separation is {entity.Status}; only one awaiting approval can be signed.");

        RequireDecisionAuthority(entity);

        if (dto.WaiveNotice && dto.PayNoticeInLieu)
            throw new InvalidOperationException(
                "Notice cannot be both waived and paid in lieu — waived notice costs nothing, "
                + "notice paid in lieu is money.");

        if (dto.WaiveNotice && string.IsNullOrWhiteSpace(dto.NoticeWaiverReason))
            throw new InvalidOperationException("Give a reason for waiving the notice period.");

        if (dto.WaiveNotice || dto.PayNoticeInLieu)
        {
            // Nothing to waive or pay where the notice was served in full — and nothing to reason
            // about where no notice period was ever counted, as on a retirement or a death.
            var (_, served, shortfall) = Notice(entity);
            if (served is null)
                throw new InvalidOperationException(
                    "This separation has no notice period to settle, so notice cannot be waived or paid in lieu.");
            if (shortfall is 0)
                throw new InvalidOperationException(
                    "The full notice period was served, so there is no notice to waive or pay in lieu.");
        }

        entity.Status = SeparationStatus.Approved;
        entity.ApprovedById = actorEmployeeId;
        entity.ApprovedOn = DateTime.UtcNow;
        entity.ApprovalNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        entity.IsNoticeWaived = dto.WaiveNotice;
        entity.NoticeWaiverReason = dto.WaiveNotice ? dto.NoticeWaiverReason!.Trim() : null;
        entity.IsNoticePaidInLieu = dto.PayNoticeInLieu;

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Separation {Number} approved (procedural={Procedural})",
            entity.SeparationNumber, entity.IsProcedural);

        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> RejectAsync(
        Guid id, RejectEmployeeSeparationDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("Give a reason for refusing the separation.");

        var tenantId = GetTenantId();
        var entity = await RequireAsync(tenantId, id, cancellationToken);

        if (entity.Status != SeparationStatus.PendingApproval)
            throw new InvalidOperationException(
                $"This separation is {entity.Status}; only one awaiting approval can be refused.");

        RequireDecisionAuthority(entity);

        entity.Status = SeparationStatus.Rejected;
        entity.RejectedById = actorEmployeeId;
        entity.RejectedOn = DateTime.UtcNow;
        entity.RejectionReason = dto.Reason.Trim();

        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Separation {Number} refused", entity.SeparationNumber);
        return ToDetailDto(await ReloadAsync(tenantId, entity.Id, cancellationToken));
    }

    // ── Documents ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IEnumerable<EmployeeSeparationDocumentDto>> GetDocumentsAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await RequireAsync(tenantId, separationId, cancellationToken);

        var documents = await _unitOfWork.Repository<EmployeeSeparationDocument>().GetQueryable()
            .Include(d => d.UploadedBy)
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.SeparationId == separationId)
            .OrderByDescending(d => d.UploadedOn)
            .ThenBy(d => d.FileName)
            .ToListAsync(cancellationToken);

        return documents.Select(ToDocumentDto).ToList();
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDocumentDto> AttachDocumentAsync(
        Guid separationId,
        SeparationDocumentCategory category,
        string fileName,
        string filePath,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId,
        string? description,
        Guid? uploadedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await RequireAsync(tenantId, separationId, cancellationToken);

        var document = new EmployeeSeparationDocument
        {
            TenantId = tenantId,
            SeparationId = separationId,
            Category = category,
            FileName = fileName,
            FilePath = filePath,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            UploadedOn = DateTime.UtcNow,
            UploadedById = uploadedByEmployeeId,
        };

        await _unitOfWork.Repository<EmployeeSeparationDocument>().AddAsync(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _unitOfWork.Repository<EmployeeSeparationDocument>().GetQueryable()
            .Include(d => d.UploadedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == document.Id, cancellationToken)
            ?? throw new InvalidOperationException("Document saved but could not be reloaded.");

        return ToDocumentDto(saved);
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDocument?> GetDocumentEntityAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<EmployeeSeparationDocument>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId && !d.IsDeleted, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var document = await _unitOfWork.Repository<EmployeeSeparationDocument>().GetQueryable()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId && !d.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Separation document with ID '{documentId}' not found.");

        var separation = await RequireAsync(tenantId, document.SeparationId, cancellationToken);

        // A mis-uploaded file can be taken back while the separation is still being prepared. Once
        // it has been submitted the attachments are part of what was approved and what the
        // settlement was computed against, so removing one silently rewrites the record. Deleting
        // the whole separation stays available to an administrator.
        if (separation.Status != SeparationStatus.Draft)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}. Its documents are part of the record and "
                + "can no longer be removed.");

        await _unitOfWork.Repository<EmployeeSeparationDocument>().DeleteAsync(documentId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<EmployeeSeparation> RequireAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
               .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
           ?? throw new ArgumentException($"Separation with ID '{id}' not found.");

    private async Task<EmployeeSeparation> ReloadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await Scoped(tenantId).AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
           ?? throw new InvalidOperationException("Separation saved but could not be reloaded.");

    /// <summary>
    /// The notice that applies to a separation type, from tenant policy.
    /// </summary>
    /// <remarks>
    /// Death, summary dismissal and contract expiry carry none — and that is a real zero, not a
    /// missing value: summary dismissal means dismissal <i>without notice</i>, a contract that
    /// expires was always going to, and nobody serves notice on a bereavement.
    /// </remarks>
    private static int DefaultNoticeDays(EmployeeTerminationType type, CompanyHrPolicySettings settings)
        => type switch
        {
            EmployeeTerminationType.Death => 0,
            EmployeeTerminationType.SummaryDismissal => 0,
            EmployeeTerminationType.ContractExpiry => 0,
            EmployeeTerminationType.VoluntaryResignation => settings.DefaultResignationNoticeDays,
            _ => settings.DefaultTerminationNoticeDays,
        };

    /// <summary>
    /// The next <c>SEP-yyyy-nnnnn</c> for the tenant and year.
    /// </summary>
    /// <remarks>
    /// ⚠ Reads <b>including soft-deleted rows</b>, and that is the point. A global query filter
    /// hides deleted rows from the ordinary queryable, and the unique index is filtered on
    /// <c>IsDeleted</c> too — so a deleted number is free again on both counts. Reissuing it would
    /// give two exits the same reference in whatever letters and payment records already quote it.
    /// Numbers are not reused.
    /// </remarks>
    private async Task<string> NextNumberAsync(Guid tenantId, DateOnly on, CancellationToken cancellationToken)
    {
        var prefix = $"SEP-{on.Year:0000}-";

        var last = await _unitOfWork.Repository<EmployeeSeparation>()
            .GetQueryableIncludingDeleted(s => s.TenantId == tenantId && s.SeparationNumber.StartsWith(prefix))
            .OrderByDescending(s => s.SeparationNumber)
            .Select(s => s.SeparationNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (last is not null && int.TryParse(last[prefix.Length..], out var parsed))
            next = parsed + 1;

        return $"{prefix}{next:00000}";
    }

    private static string FullName(Employee? e)
        => e == null ? string.Empty : $"{e.FirstName} {e.LastName}".Trim();

    /// <summary>
    /// Notice required, served and short — derived, never stored.
    /// </summary>
    /// <remarks>
    /// Served notice is counted from the day notice was given to the last working day. The
    /// shortfall floors at zero: working more notice than was owed is not negative notice pay, it
    /// is just a longer handover.
    /// </remarks>
    private static (int Required, int? Served, int? Shortfall) Notice(EmployeeSeparation s)
    {
        var required = s.NoticeDays ?? 0;

        if (s.NoticeGivenOn is not { } given || s.LastWorkingDay is not { } last)
            return (required, null, null);

        var served = last.DayNumber - given.DayNumber;
        if (served < 0) served = 0;

        return (required, served, Math.Max(0, required - served));
    }

    private static EmployeeSeparationDocumentDto ToDocumentDto(EmployeeSeparationDocument d) => new()
    {
        Id = d.Id,
        SeparationId = d.SeparationId,
        Category = d.Category,
        CategoryName = d.Category.ToString(),
        FileName = d.FileName,
        Description = d.Description,
        UploadedOn = d.UploadedOn,
        UploadedById = d.UploadedById,
        UploadedByName = d.UploadedBy == null ? null : FullName(d.UploadedBy),
        IsRegisteredInDms = d.DocumentRecordId != null,
    };

    private static EmployeeSeparationListDto ToListDto(EmployeeSeparation s) => new()
    {
        Id = s.Id,
        SeparationNumber = s.SeparationNumber,
        EmployeeId = s.EmployeeId,
        EmployeeName = FullName(s.Employee),
        EmployeeNumber = s.Employee?.EmployeeNumber,
        PositionTitle = s.Employee?.Position?.Title,
        OrganizationUnitName = s.Employee?.OrganizationUnit?.Name,
        SeparationType = s.SeparationType,
        SeparationTypeName = s.SeparationType.ToString(),
        Status = s.Status,
        StatusName = s.Status.ToString(),
        InitiatedOn = s.InitiatedOn,
        LastWorkingDay = s.LastWorkingDay,
        EffectiveDate = s.EffectiveDate,
        IsProcedural = s.IsProcedural,
        IsSystemInitiated = s.IsSystemInitiated,
        IsDisciplinary = s.DisciplinaryActionId != null,
        EmployeeRecordUpdated = s.EmployeeRecordUpdatedOn != null,
    };

    private static EmployeeSeparationDetailDto ToDetailDto(EmployeeSeparation s) => new()
    {
        Id = s.Id,
        SeparationNumber = s.SeparationNumber,
        EmployeeId = s.EmployeeId,
        EmployeeName = FullName(s.Employee),
        EmployeeNumber = s.Employee?.EmployeeNumber,
        PositionTitle = s.Employee?.Position?.Title,
        OrganizationUnitName = s.Employee?.OrganizationUnit?.Name,
        SeparationType = s.SeparationType,
        SeparationTypeName = s.SeparationType.ToString(),
        Status = s.Status,
        StatusName = s.Status.ToString(),
        InitiatedOn = s.InitiatedOn,
        LastWorkingDay = s.LastWorkingDay,
        EffectiveDate = s.EffectiveDate,
        IsProcedural = s.IsProcedural,
        IsSystemInitiated = s.IsSystemInitiated,
        IsDisciplinary = s.DisciplinaryActionId != null,
        EmployeeRecordUpdated = s.EmployeeRecordUpdatedOn != null,

        ReasonCategory = s.ReasonCategory,
        ReasonCategoryName = s.ReasonCategory?.ToString(),
        ReasonNotes = s.ReasonNotes,
        NoticeGivenOn = s.NoticeGivenOn,
        NoticeDays = s.NoticeDays,
        NoticeRequiredDays = Notice(s).Required,
        NoticeServedDays = Notice(s).Served,
        NoticeShortfallDays = Notice(s).Shortfall,
        SubmittedOn = s.SubmittedOn,
        SubmittedById = s.SubmittedById,
        SubmittedByName = s.SubmittedBy == null ? null : FullName(s.SubmittedBy),
        InitiatedById = s.InitiatedById,
        InitiatedByName = s.InitiatedBy == null ? null : FullName(s.InitiatedBy),
        ApprovedById = s.ApprovedById,
        ApprovedByName = s.ApprovedBy == null ? null : FullName(s.ApprovedBy),
        ApprovedOn = s.ApprovedOn,
        ApprovalNotes = s.ApprovalNotes,
        WorkflowInstanceId = s.WorkflowInstanceId,
        RejectedById = s.RejectedById,
        RejectedByName = s.RejectedBy == null ? null : FullName(s.RejectedBy),
        RejectedOn = s.RejectedOn,
        RejectionReason = s.RejectionReason,
        AbsenceDays = s.AbsenceDays,
        // The mirror of IsProcedural, said the way a client needs to hear it: who do I send this to.
        RequiresManagingDirectorSignature = !s.IsProcedural,
        IsNoticeWaived = s.IsNoticeWaived,
        NoticeWaiverReason = s.NoticeWaiverReason,
        IsNoticePaidInLieu = s.IsNoticePaidInLieu,
        IsEligibleForRehire = s.IsEligibleForRehire,
        EligibleForRehireDate = s.EligibleForRehireDate,
        RehireRestrictions = s.RehireRestrictions,
        DisciplinaryActionId = s.DisciplinaryActionId,
        EmployeeRecordUpdatedOn = s.EmployeeRecordUpdatedOn,
        CancelledOn = s.CancelledOn,
        CancelledById = s.CancelledById,
        CancelledByName = s.CancelledBy == null ? null : FullName(s.CancelledBy),
        CancellationReason = s.CancellationReason,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };
}
