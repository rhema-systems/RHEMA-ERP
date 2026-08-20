using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Finance;
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
    private readonly ICurrencyService _currencies;
    private readonly ILogger<SeparationService> _logger;

    public SeparationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ICompanyHrPolicyProvider policyProvider,
        ICurrencyService currencies,
        ILogger<SeparationService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _policyProvider = policyProvider;
        _currencies = currencies;
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

    // ── Final settlement (FR-HR-184) ──────────────────────────────────────────

    /// <summary>Days per year used to turn a monthly salary into a daily rate.</summary>
    /// <remarks>
    /// ⚠ <b>A policy assumption, stated rather than buried.</b> Calendar days: monthly × 12 ÷ 365.
    /// A 30-day-month or working-day basis gives different money on the same facts, and TDC has not
    /// said which it uses — so the basis is written onto every computed line in words, and the
    /// question is recorded in <c>docs/HR-OPEN-QUESTIONS-FOR-TDC.md</c>. Do not change this quietly.
    /// </remarks>
    private const decimal DaysPerYear = 365m;

    /// <summary>
    /// The currency a settlement is stated in: HR's configured default, validated against Finance,
    /// falling back to Finance's base currency.
    /// </summary>
    /// <remarks>
    /// Finance owns what a currency <i>is</i>; HR owns which one it uses. A code HR has configured
    /// that Finance does not hold is refused rather than silently swapped — a misconfiguration that
    /// heals itself invisibly stays broken. Same division <c>StaffTravelCurrencyBridge</c> settled
    /// for travel; when a third area needs this the two should become one HR-wide bridge.
    /// </remarks>
    private async Task<string> ResolveCurrencyAsync(
        CompanyHrPolicySettings settings, CancellationToken cancellationToken)
    {
        var configured = settings.DefaultCurrencyCode?.Trim().ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(configured))
        {
            var known = await _currencies.GetByCodeAsync(configured, cancellationToken);
            if (known is null)
                throw new InvalidOperationException(
                    $"HR's default currency '{configured}' is not one Finance holds. Either correct "
                    + "it in HR settings or add the currency in Finance before preparing a settlement.");

            return configured;
        }

        var baseCurrency = await _currencies.GetBaseCurrencyAsync(cancellationToken);
        if (baseCurrency is null)
            throw new InvalidOperationException(
                "No currency is configured. Set HR's default currency, or a base currency in Finance.");

        return baseCurrency.CurrencyCode;
    }

    /// <summary>
    /// The employee's daily rate, and how it was arrived at — or null with the reason, where no
    /// salary is on record.
    /// </summary>
    private async Task<(decimal? Rate, string Basis)> DailyRateAsync(
        Guid tenantId, Guid employeeId, string currency, CancellationToken cancellationToken)
    {
        var contract = await _unitOfWork.Repository<EmployeeContractDetail>().GetQueryable()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.EmployeeId == employeeId && c.Salary > 0)
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (contract is null)
            return (null, "No salary is on record for this employee, so amounts based on pay cannot be computed.");

        var rate = Math.Round(contract.Salary * 12m / DaysPerYear, 4, MidpointRounding.AwayFromZero);
        return (rate,
            $"{currency} {contract.Salary:N2} per month × 12 ÷ {DaysPerYear:N0} days = "
            + $"{currency} {rate:N4} per day (contract {contract.ContractNumber}).");
    }

    /// <inheritdoc />
    public async Task<SeparationSettlementDto> PrepareSettlementAsync(
        Guid separationId, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);

        var existing = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .AnyAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SeparationId == separationId, cancellationToken);
        if (existing)
            throw new InvalidOperationException("A settlement has already been prepared for this separation.");

        // FR-HR-091: entitlements are computed only AFTER the clearance form is complete. This is
        // the sentence that gate exists to enforce, so it is checked here as well as there.
        if (separation.Status != SeparationStatus.ClearanceCompleted)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}. A settlement is prepared once clearance is "
                + "complete — FR-HR-091 requires the clearance form before entitlements are computed.");

        var settings = await _policyProvider.GetAsync(cancellationToken);
        var currency = await ResolveCurrencyAsync(settings, cancellationToken);
        var (rate, rateBasis) = await DailyRateAsync(tenantId, separation.EmployeeId, currency, cancellationToken);

        var settlement = new SeparationSettlement
        {
            TenantId = tenantId,
            SeparationId = separationId,
            CurrencyCode = currency,
            DailyRate = rate,
            DailyRateBasis = rateBasis,
            PreparedById = actorEmployeeId,
            PreparedOn = DateTime.UtcNow,
        };

        await _unitOfWork.Repository<SeparationSettlement>().AddAsync(settlement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var lines = new List<SeparationSettlementLine>();
        var order = 0;

        void Add(SettlementLineCategory category, bool deduction, string description,
                 decimal? amount, SettlementLineComputation computation, string basis,
                 Guid? clearanceItemId = null, Guid? travelAdvanceId = null)
        {
            order += 10;
            lines.Add(new SeparationSettlementLine
            {
                TenantId = tenantId,
                SettlementId = settlement.Id,
                Category = category,
                IsDeduction = deduction,
                Description = description,
                Amount = amount,
                Computation = computation,
                Basis = basis,
                SourceClearanceItemId = clearanceItemId,
                SourceTravelAdvanceId = travelAdvanceId,
                IsSystemGenerated = true,
                SortOrder = order,
            });
        }

        // ── Earnings ──────────────────────────────────────────────────────────

        // Unpaid salary has no source in this system: PayrollPayslipSnapshots is empty and there is
        // no accrual to read. Recorded as owed and uncomputed rather than omitted, so nobody signs
        // a statement that quietly forgot the last month's pay.
        Add(SettlementLineCategory.UnpaidSalary, false,
            "Unpaid salary to the last working day", null, SettlementLineComputation.CannotCompute,
            "No payroll figure is available in this system. Enter the amount from payroll and name the source.");

        // Notice pay only where the notice was to be PAID rather than served or waived.
        var (_, _, shortfall) = Notice(separation);
        if (separation.IsNoticePaidInLieu && shortfall is > 0)
        {
            if (rate is { } r)
                Add(SettlementLineCategory.NoticePay, false,
                    $"Notice pay in lieu — {shortfall} day(s) not served",
                    Math.Round(r * shortfall.Value, 2, MidpointRounding.AwayFromZero),
                    SettlementLineComputation.Computed,
                    $"{shortfall} day(s) × {rateBasis}");
            else
                Add(SettlementLineCategory.NoticePay, false,
                    $"Notice pay in lieu — {shortfall} day(s) not served",
                    null, SettlementLineComputation.CannotCompute, rateBasis);
        }

        // Leave encashment: FR-HR-046 (on exit only) and FR-HR-152 (capped at 56 days).
        await AddLeaveEncashmentLineAsync(tenantId, separation, rate, rateBasis, currency, Add, cancellationToken);

        // ── Deductions ────────────────────────────────────────────────────────

        var clearanceOutstanding = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.SeparationId == separationId
                        && i.OutstandingAmount != null && i.OutstandingAmount > 0)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);

        foreach (var item in clearanceOutstanding)
        {
            Add(CategoryForClearance(item.Kind), true,
                $"{item.Name} — outstanding at clearance",
                item.OutstandingAmount,
                SettlementLineComputation.Computed,
                $"Recorded on the clearance form as {item.Status}"
                + (string.IsNullOrWhiteSpace(item.SignedOffBy) ? "." : $", signed off by {item.SignedOffBy}."),
                clearanceItemId: item.Id);
        }

        // Travel advances the employee still holds. HR's own data, and until now nothing connected
        // it to somebody leaving — an employee could walk out owing one with nothing to notice.
        var advances = await _unitOfWork.Repository<StaffTravelAdvance>().GetQueryable()
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.EmployeeId == separation.EmployeeId
                        && (a.Status == TravelAdvanceStatus.Disbursed
                            || a.Status == TravelAdvanceStatus.PartiallySettled))
            .ToListAsync(cancellationToken);

        foreach (var advance in advances)
        {
            var outstanding = (advance.ApprovedAmount ?? advance.RequestedAmount) - advance.SettledAmount;
            if (outstanding <= 0) continue;

            // ⚠ A currency the settlement is not stated in cannot simply be added to it. Recorded
            // as uncomputed with the figure in the text, rather than converted here: Finance owns
            // conversion, and its rates are known to be inverted (see StaffTravelCurrencyBridge).
            var sameCurrency = string.Equals(advance.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase);

            Add(SettlementLineCategory.TravelAdvanceRecovery, true,
                $"Travel advance {advance.AdvanceNumber} outstanding",
                sameCurrency ? outstanding : null,
                sameCurrency ? SettlementLineComputation.Computed : SettlementLineComputation.CannotCompute,
                sameCurrency
                    ? $"Advance {advance.AdvanceNumber}: {currency} {(advance.ApprovedAmount ?? advance.RequestedAmount):N2} less {currency} {advance.SettledAmount:N2} settled."
                    : $"Advance {advance.AdvanceNumber} is in {advance.CurrencyCode}, not {currency} — {advance.CurrencyCode} {outstanding:N2} outstanding. Convert through Finance and enter the amount.",
                travelAdvanceId: advance.Id);
        }

        foreach (var line in lines)
            await _unitOfWork.Repository<SeparationSettlementLine>().AddAsync(line);

        separation.Status = SeparationStatus.SettlementPending;
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Settlement prepared for separation {Number}: {Lines} lines, {Uncomputed} uncomputed",
            separation.SeparationNumber, lines.Count,
            lines.Count(l => l.Computation == SettlementLineComputation.CannotCompute));

        return await GetSettlementAsync(separationId, cancellationToken);
    }

    /// <summary>
    /// The leave encashment line — FR-HR-046 (encashed on exit, and only on exit) and FR-HR-152
    /// (capped at fifty-six days).
    /// </summary>
    /// <remarks>
    /// ⚠ Measured 2026-08-20: <c>LeaveBalances</c> holds <b>zero</b> rows, so on live data this
    /// always lands on <c>CannotCompute</c>. The cap and the rate are applied anyway, so that the
    /// rules bite the moment balances exist rather than being remembered later.
    /// </remarks>
    private async Task AddLeaveEncashmentLineAsync(
        Guid tenantId, EmployeeSeparation separation, decimal? rate, string rateBasis, string currency,
        Action<SettlementLineCategory, bool, string, decimal?, SettlementLineComputation, string, Guid?, Guid?> add,
        CancellationToken cancellationToken)
    {
        const decimal encashmentCapDays = 56m;   // FR-HR-152

        var balances = await _unitOfWork.Repository<LeaveBalance>().GetQueryable()
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && !b.IsDeleted && b.EmployeeId == separation.EmployeeId)
            .ToListAsync(cancellationToken);

        if (balances.Count == 0)
        {
            add(SettlementLineCategory.LeaveEncashment, false,
                "Accrued leave encashed on exit", null, SettlementLineComputation.CannotCompute,
                "No leave balance is on record for this employee. Enter the days and amount, and name the source.",
                null, null);
            return;
        }

        var available = balances.Sum(b =>
            b.EntitledDays + b.CarriedOverDays + b.AdjustmentDays - b.UsedDays - b.PendingDays - b.EncashedDays);

        if (available <= 0)
            return;   // nothing accrued: no line rather than a zero one

        var capped = Math.Min(available, encashmentCapDays);
        var cappedNote = capped < available
            ? $" Capped at {encashmentCapDays:N0} days under FR-HR-152 (from {available:N2} accrued)."
            : string.Empty;

        if (rate is { } r)
            add(SettlementLineCategory.LeaveEncashment, false,
                $"Accrued leave encashed on exit — {capped:N2} day(s)",
                Math.Round(r * capped, 2, MidpointRounding.AwayFromZero),
                SettlementLineComputation.Computed,
                $"{capped:N2} day(s) × {rateBasis}{cappedNote}",
                null, null);
        else
            add(SettlementLineCategory.LeaveEncashment, false,
                $"Accrued leave encashed on exit — {capped:N2} day(s)",
                null, SettlementLineComputation.CannotCompute,
                $"{rateBasis}{cappedNote}",
                null, null);
    }

    /// <summary>Which settlement category a clearance line's outstanding amount belongs under.</summary>
    private static SettlementLineCategory CategoryForClearance(ClearanceItemKind kind) => kind switch
    {
        ClearanceItemKind.OutstandingLoan => SettlementLineCategory.LoanRepayment,
        ClearanceItemKind.SalaryAdvance => SettlementLineCategory.SalaryAdvanceRecovery,
        ClearanceItemKind.PayrollRecovery => SettlementLineCategory.OtherDeduction,
        _ => SettlementLineCategory.PropertyRecovery,
    };

    /// <inheritdoc />
    public async Task<SeparationSettlementDto> GetSettlementAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var separation = await Scoped(tenantId).AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == separationId, cancellationToken)
            ?? throw new ArgumentException($"Separation with ID '{separationId}' not found.");

        var settlement = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .Include(s => s.PreparedBy)
            .Include(s => s.FinalisedBy)
            .Include(s => s.ReviewedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SeparationId == separationId,
                cancellationToken)
            ?? throw new ArgumentException("No settlement has been prepared for this separation.");

        var lines = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.SettlementId == settlement.Id)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Description)
            .ToListAsync(cancellationToken);

        var uncomputed = lines.Count(l => l.Computation == SettlementLineComputation.CannotCompute);
        var earnings = lines.Where(l => !l.IsDeduction).Sum(l => l.Amount ?? 0m);
        var deductions = lines.Where(l => l.IsDeduction).Sum(l => l.Amount ?? 0m);

        var canFinalise = !settlement.FinalisedOn.HasValue
                          && uncomputed == 0
                          && lines.Count > 0
                          && separation.Status == SeparationStatus.SettlementPending;

        return new SeparationSettlementDto
        {
            Id = settlement.Id,
            SeparationId = separationId,
            SeparationNumber = separation.SeparationNumber,
            EmployeeName = FullName(separation.Employee),
            SeparationStatus = separation.Status,
            SeparationStatusName = separation.Status.ToString(),
            CurrencyCode = settlement.CurrencyCode,
            DailyRate = settlement.DailyRate,
            DailyRateBasis = settlement.DailyRateBasis,
            Lines = lines.Select(ToSettlementLineDto).ToList(),
            GrossEarnings = earnings,
            TotalDeductions = deductions,
            NetPayable = earnings - deductions,
            UncomputedLines = uncomputed,
            IsFinalised = settlement.FinalisedOn.HasValue,
            FinalisedOn = settlement.FinalisedOn,
            FinalisedByName = settlement.FinalisedBy == null ? null : FullName(settlement.FinalisedBy),
            PreparedById = settlement.PreparedById,
            PreparedByName = settlement.PreparedBy == null ? null : FullName(settlement.PreparedBy),
            PreparedOn = settlement.PreparedOn,
            Notes = settlement.Notes,
            CanFinalise = canFinalise,
            BlockedReason = SettlementBlockedReason(settlement, separation, lines, uncomputed),
            ReviewOutcome = settlement.ReviewOutcome,
            ReviewOutcomeName = settlement.ReviewOutcome.ToString(),
            ReviewedById = settlement.ReviewedById,
            ReviewedByName = settlement.ReviewedBy == null ? null : FullName(settlement.ReviewedBy),
            ReviewedOn = settlement.ReviewedOn,
            ReviewNotes = settlement.ReviewNotes,
            ReturnCount = settlement.ReturnCount,
            IsClearedForPayment = settlement.ReviewOutcome == SettlementReviewOutcome.Approved
                                  && separation.Status == SeparationStatus.SettlementApproved,
        };
    }

    private static string? SettlementBlockedReason(
        SeparationSettlement settlement, EmployeeSeparation separation,
        List<SeparationSettlementLine> lines, int uncomputed)
    {
        if (settlement.FinalisedOn.HasValue)
            return "This settlement has been finalised and is with Internal Audit.";

        if (separation.Status != SeparationStatus.SettlementPending)
            return $"This separation is {separation.Status}; a settlement is finalised while it is awaiting one.";

        if (lines.Count == 0)
            return "The settlement has no lines.";

        if (uncomputed > 0)
            return $"{uncomputed} line(s) could not be valued. Enter each amount and name its source, "
                   + "or remove the line if nothing is owed.";

        return null;
    }

    /// <inheritdoc />
    public async Task<SeparationSettlementLineDto> AddSettlementLineAsync(
        Guid separationId, AddSettlementLineDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var settlement = await RequireEditableSettlementAsync(tenantId, separationId, cancellationToken);

        if (string.IsNullOrWhiteSpace(dto.Description))
            throw new InvalidOperationException("Describe what the line is for.");

        // An amount with no stated source is a number nobody can check. FR-HR-185 puts Internal
        // Audit in front of this statement; they need to know where each figure came from.
        if (dto.Amount is not null && string.IsNullOrWhiteSpace(dto.SourceReference))
            throw new InvalidOperationException(
                "Name the source of the amount — the payroll report, loan statement or letter it came from.");

        if (dto.Amount is < 0)
            throw new InvalidOperationException("A settlement amount cannot be negative. Use a deduction line instead.");

        var maxOrder = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.SettlementId == settlement.Id)
            .Select(l => (int?)l.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var line = new SeparationSettlementLine
        {
            TenantId = tenantId,
            SettlementId = settlement.Id,
            Category = dto.Category,
            IsDeduction = dto.IsDeduction,
            Description = dto.Description.Trim(),
            Amount = dto.Amount,
            Computation = dto.Amount is null
                ? SettlementLineComputation.CannotCompute
                : SettlementLineComputation.ManuallyEntered,
            Basis = dto.Amount is null ? "Recorded as owed; the amount is not yet known." : null,
            SourceReference = string.IsNullOrWhiteSpace(dto.SourceReference) ? null : dto.SourceReference.Trim(),
            IsSystemGenerated = false,
            SortOrder = maxOrder + 10,
        };

        await _unitOfWork.Repository<SeparationSettlementLine>().AddAsync(line);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToSettlementLineDto(line);
    }

    /// <inheritdoc />
    public async Task<SeparationSettlementLineDto> UpdateSettlementLineAsync(
        Guid lineId, UpdateSettlementLineDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        var line = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .FirstOrDefaultAsync(l => l.Id == lineId && l.TenantId == tenantId && !l.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Settlement line with ID '{lineId}' not found.");

        var settlement = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == line.SettlementId && s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
            ?? throw new ArgumentException("The settlement this line belongs to was not found.");

        RequireEditable(await RequireAsync(tenantId, settlement.SeparationId, cancellationToken));

        if (dto.Description is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Description))
                throw new InvalidOperationException("Describe what the line is for.");
            line.Description = dto.Description.Trim();
        }

        if (dto.IsDeduction is { } deduction) line.IsDeduction = deduction;

        if (dto.SourceReference is not null)
            line.SourceReference = string.IsNullOrWhiteSpace(dto.SourceReference) ? null : dto.SourceReference.Trim();

        if (dto.Amount is { } amount)
        {
            if (amount < 0)
                throw new InvalidOperationException("A settlement amount cannot be negative. Use a deduction line instead.");

            if (string.IsNullOrWhiteSpace(line.SourceReference))
                throw new InvalidOperationException(
                    "Name the source of the amount — the payroll report, loan statement or letter it came from.");

            line.Amount = amount;

            // Supplying the figure the system could not work out is what clears the block. The line
            // becomes ManuallyEntered rather than Computed: a person vouched for it, not the system.
            line.Computation = SettlementLineComputation.ManuallyEntered;
        }

        await _unitOfWork.Repository<SeparationSettlementLine>().UpdateAsync(line);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToSettlementLineDto(line);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSettlementLineAsync(Guid lineId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var line = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .FirstOrDefaultAsync(l => l.Id == lineId && l.TenantId == tenantId && !l.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Settlement line with ID '{lineId}' not found.");

        var settlement = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == line.SettlementId && s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
            ?? throw new ArgumentException("The settlement this line belongs to was not found.");

        RequireEditable(await RequireAsync(tenantId, settlement.SeparationId, cancellationToken));

        await _unitOfWork.Repository<SeparationSettlementLine>().DeleteAsync(lineId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<SeparationSettlementDto> FinaliseSettlementAsync(
        Guid separationId, FinaliseSettlementDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);
        var settlement = await RequireEditableSettlementAsync(tenantId, separationId, cancellationToken);

        if (separation.Status != SeparationStatus.SettlementPending)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}; a settlement is finalised while it is awaiting one.");

        var lines = await _unitOfWork.Repository<SeparationSettlementLine>().GetQueryable()
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.SettlementId == settlement.Id)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
            throw new InvalidOperationException("The settlement has no lines, so there is nothing to finalise.");

        // The rule this whole slice turns on. A statement finalised with an unvalued line would go
        // to Internal Audit, and then to payment, carrying a silent zero where a real amount was
        // owed. Zero is a claim; the block is what keeps it from being made by accident.
        var uncomputed = lines.Where(l => l.Computation == SettlementLineComputation.CannotCompute).ToList();
        if (uncomputed.Count > 0)
        {
            var names = string.Join(", ", uncomputed.Take(4).Select(l => l.Description));
            var more = uncomputed.Count > 4 ? $" and {uncomputed.Count - 4} more" : string.Empty;
            throw new InvalidOperationException(
                $"{uncomputed.Count} line(s) could not be valued — {names}{more}. Enter each amount "
                + "and name its source, or remove the line if nothing is owed. A settlement is not "
                + "finalised with an unknown amount showing as zero.");
        }

        settlement.FinalisedOn = DateTime.UtcNow;
        settlement.FinalisedById = actorEmployeeId;
        if (!string.IsNullOrWhiteSpace(dto?.Notes)) settlement.Notes = dto.Notes.Trim();

        separation.Status = SeparationStatus.SettlementUnderReview;

        await _unitOfWork.Repository<SeparationSettlement>().UpdateAsync(settlement);
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Settlement finalised for separation {Number}; net {Currency} {Net}",
            separation.SeparationNumber, settlement.CurrencyCode,
            lines.Where(l => !l.IsDeduction).Sum(l => l.Amount ?? 0m)
            - lines.Where(l => l.IsDeduction).Sum(l => l.Amount ?? 0m));

        return await GetSettlementAsync(separationId, cancellationToken);
    }

    /// <summary>
    /// A settlement may be edited only while its separation is awaiting one.
    /// </summary>
    /// <remarks>
    /// ⚠ Keyed on the <b>separation's status</b>, not on <c>FinalisedOn</c>. When Internal Audit
    /// returns a statement it becomes editable again, but <c>FinalisedOn</c> is deliberately kept —
    /// it records that the statement was finalised once, and clearing it to unlock editing would
    /// erase that. The status is the live question; the timestamp is history.
    /// </remarks>
    private static void RequireEditable(EmployeeSeparation separation)
    {
        if (separation.Status == SeparationStatus.SettlementUnderReview)
            throw new InvalidOperationException(
                "This settlement has been finalised and is with Internal Audit. Its lines can no "
                + "longer be changed unless Internal Audit returns it.");

        if (separation.Status != SeparationStatus.SettlementPending)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}; its settlement can only be changed while "
                + "it is awaiting one.");
    }

    private async Task<SeparationSettlement> RequireEditableSettlementAsync(
        Guid tenantId, Guid separationId, CancellationToken cancellationToken)
    {
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);
        RequireEditable(separation);

        return await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
                   .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SeparationId == separationId,
                       cancellationToken)
               ?? throw new ArgumentException("No settlement has been prepared for this separation.");
    }

    // ── FR-HR-185: Internal Audit's review, before payment is released ────────

    /// <inheritdoc />
    public async Task<SeparationSettlementDto> ApproveSettlementReviewAsync(
        Guid separationId, ReviewSettlementDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var (separation, settlement) = await RequireSettlementUnderReviewAsync(tenantId, separationId, cancellationToken);

        settlement.ReviewOutcome = SettlementReviewOutcome.Approved;
        settlement.ReviewedById = actorEmployeeId;
        settlement.ReviewedOn = DateTime.UtcNow;
        settlement.ReviewNotes = string.IsNullOrWhiteSpace(dto?.Notes) ? null : dto!.Notes.Trim();

        separation.Status = SeparationStatus.SettlementApproved;

        await _unitOfWork.Repository<SeparationSettlement>().UpdateAsync(settlement);
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Settlement for separation {Number} reviewed and approved by Internal Audit; payment may be released",
            separation.SeparationNumber);

        return await GetSettlementAsync(separationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SeparationSettlementDto> ReturnSettlementAsync(
        Guid separationId, ReviewSettlementDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // A control that can refuse without saying why leaves HR guessing at what to correct, and
        // the statement comes back unchanged.
        if (string.IsNullOrWhiteSpace(dto.Notes))
            throw new InvalidOperationException(
                "Set out the findings when returning a settlement — what is wrong with it, so it can be corrected.");

        var tenantId = GetTenantId();
        var (separation, settlement) = await RequireSettlementUnderReviewAsync(tenantId, separationId, cancellationToken);

        settlement.ReviewOutcome = SettlementReviewOutcome.Returned;
        settlement.ReviewedById = actorEmployeeId;
        settlement.ReviewedOn = DateTime.UtcNow;
        settlement.ReviewNotes = dto.Notes.Trim();
        settlement.ReturnCount += 1;

        // Back to HR, editable again. FinalisedOn is kept: it says the statement was finalised once,
        // and ReturnCount says how often Internal Audit sent it back — the question an auditor asks
        // later is "how many times was this queried before it was paid".
        separation.Status = SeparationStatus.SettlementPending;

        await _unitOfWork.Repository<SeparationSettlement>().UpdateAsync(settlement);
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Settlement for separation {Number} returned by Internal Audit (return #{Count})",
            separation.SeparationNumber, settlement.ReturnCount);

        return await GetSettlementAsync(separationId, cancellationToken);
    }

    private async Task<(EmployeeSeparation Separation, SeparationSettlement Settlement)>
        RequireSettlementUnderReviewAsync(Guid tenantId, Guid separationId, CancellationToken cancellationToken)
    {
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);

        if (separation.Status != SeparationStatus.SettlementUnderReview)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}. Internal Audit reviews a settlement once HR "
                + "has finalised it.");

        var settlement = await _unitOfWork.Repository<SeparationSettlement>().GetQueryable()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SeparationId == separationId,
                cancellationToken)
            ?? throw new ArgumentException("No settlement has been prepared for this separation.");

        return (separation, settlement);
    }

    private static SeparationSettlementLineDto ToSettlementLineDto(SeparationSettlementLine l) => new()
    {
        Id = l.Id,
        SettlementId = l.SettlementId,
        Category = l.Category,
        CategoryName = l.Category.ToString(),
        IsDeduction = l.IsDeduction,
        Description = l.Description,
        Amount = l.Amount,
        Computation = l.Computation,
        ComputationName = l.Computation.ToString(),
        Basis = l.Basis,
        SourceReference = l.SourceReference,
        SourceClearanceItemId = l.SourceClearanceItemId,
        SourceTravelAdvanceId = l.SourceTravelAdvanceId,
        IsSystemGenerated = l.IsSystemGenerated,
        SortOrder = l.SortOrder,
    };

    // ── Clearance: the catalogue ──────────────────────────────────────────────

    /// <summary>The kinds that can carry money, and therefore feed the FR-HR-184 settlement.</summary>
    /// <remarks>
    /// Nobody owes a quantity of duty-post keys. Recording an amount against a kind that cannot
    /// carry one is refused rather than stored and ignored — a number the settlement will never
    /// read is worse than no number, because somebody will believe it.
    /// </remarks>
    private static bool CarriesAmount(ClearanceItemKind kind)
        => kind is ClearanceItemKind.OutstandingLoan
                or ClearanceItemKind.SalaryAdvance
                or ClearanceItemKind.PayrollRecovery;

    /// <summary>FR-HR-183's list, as a starting catalogue for a tenant that has none.</summary>
    private static readonly (string Name, ClearanceItemKind Kind, string Description)[] DefaultTemplates =
    {
        ("Outstanding loans", ClearanceItemKind.OutstandingLoan,
            "Any staff loan not yet repaid in full. The balance is recovered from the final settlement."),
        ("Salary advances", ClearanceItemKind.SalaryAdvance,
            "Advances drawn against salary and not yet recovered."),
        ("Company property", ClearanceItemKind.CompanyProperty,
            "Vehicles, phones, tools, protective equipment and anything else issued to the employee."),
        ("Office equipment", ClearanceItemKind.OfficeEquipment,
            "Computers, peripherals and office equipment assigned to the employee or their desk."),
        ("Duty-post keys", ClearanceItemKind.DutyPostKeys,
            "Keys, access cards and passes for offices, stores, gates and vehicles."),
        ("Documents and records", ClearanceItemKind.DocumentsAndRecords,
            "Files, drawings, contracts and records held by the employee, and the handover of work in progress."),
        ("Payroll recoveries", ClearanceItemKind.PayrollRecovery,
            "Any other amount due back to the organisation through payroll."),
    };

    /// <inheritdoc />
    public async Task<IEnumerable<SeparationClearanceTemplateDto>> GetClearanceTemplatesAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .Include(t => t.OwningOrganizationUnit)
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

        if (!includeInactive)
            query = query.Where(t => t.IsActive);

        var templates = await query
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(ToTemplateDto).ToList();
    }

    /// <inheritdoc />
    public async Task<SeparationClearanceTemplateDto> CreateClearanceTemplateAsync(
        CreateSeparationClearanceTemplateDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Give the clearance line a name.");

        var name = dto.Name.Trim();

        var clash = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Name == name, cancellationToken);
        if (clash)
            throw new InvalidOperationException($"A clearance line named '{name}' already exists.");

        await RequireOrganizationUnitAsync(tenantId, dto.OwningOrganizationUnitId, cancellationToken);

        var template = new SeparationClearanceTemplate
        {
            TenantId = tenantId,
            Name = name,
            Kind = dto.Kind,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            OwningOrganizationUnitId = dto.OwningOrganizationUnitId,
            IsMandatory = dto.IsMandatory,
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder,
        };

        await _unitOfWork.Repository<SeparationClearanceTemplate>().AddAsync(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToTemplateDto(await ReloadTemplateAsync(tenantId, template.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<SeparationClearanceTemplateDto> UpdateClearanceTemplateAsync(
        Guid id, UpdateSeparationClearanceTemplateDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        var template = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Clearance line with ID '{id}' not found.");

        if (dto.Name is not null)
        {
            var name = dto.Name.Trim();
            if (name.Length == 0)
                throw new InvalidOperationException("Give the clearance line a name.");

            var clash = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
                .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Name == name && t.Id != id, cancellationToken);
            if (clash)
                throw new InvalidOperationException($"A clearance line named '{name}' already exists.");

            template.Name = name;
        }

        if (dto.Kind is { } kind) template.Kind = kind;
        if (dto.Description is not null)
            template.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        if (dto.OwningOrganizationUnitId is { } unitId)
        {
            await RequireOrganizationUnitAsync(tenantId, unitId, cancellationToken);
            template.OwningOrganizationUnitId = unitId;
        }
        if (dto.IsMandatory is { } mandatory) template.IsMandatory = mandatory;
        if (dto.IsActive is { } active) template.IsActive = active;
        if (dto.SortOrder is { } order) template.SortOrder = order;

        await _unitOfWork.Repository<SeparationClearanceTemplate>().UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToTemplateDto(await ReloadTemplateAsync(tenantId, template.Id, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<bool> DeleteClearanceTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var template = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Clearance line with ID '{id}' not found.");

        // Items snapshot their template and hold no foreign key to it, so deleting a catalogue line
        // cannot orphan a signed form — which is exactly why deleting is allowed at all. Retiring
        // it (IsActive = false) is usually the better move and keeps it out of future forms only.
        await _unitOfWork.Repository<SeparationClearanceTemplate>().DeleteAsync(template.Id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SeparationClearanceTemplateDto>> SeedDefaultClearanceTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var existing = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);
        var have = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var order = 0;
        foreach (var (name, kind, description) in DefaultTemplates)
        {
            order += 10;
            if (have.Contains(name)) continue;

            await _unitOfWork.Repository<SeparationClearanceTemplate>().AddAsync(new SeparationClearanceTemplate
            {
                TenantId = tenantId,
                Name = name,
                Kind = kind,
                Description = description,
                IsMandatory = true,
                IsActive = true,
                SortOrder = order,
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetClearanceTemplatesAsync(includeInactive: true, cancellationToken);
    }

    // ── Clearance: the run ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SeparationClearanceDto> StartClearanceAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);

        // ⚠ Asked BEFORE the status check, not after. A separation whose clearance has begun is no
        // longer Approved — it is ClearanceInProgress — so a status-first check answers a restart
        // with "clearance begins once the separation has been approved", which is both confusing
        // and wrong: it *is* approved. Order the questions so the more specific one answers first.
        var alreadyStarted = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .AnyAsync(i => i.TenantId == tenantId && !i.IsDeleted && i.SeparationId == separationId, cancellationToken);
        if (alreadyStarted)
            throw new InvalidOperationException("Clearance has already been started for this separation.");

        if (separation.Status != SeparationStatus.Approved)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}. Clearance begins once the separation has "
                + "been approved.");

        var templates = await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        // ⚠ Refused rather than started empty. A clearance form with no lines would complete the
        // instant it began — every mandatory item satisfied because there are none — and FR-HR-091's
        // gate would report "cleared" having checked nothing. An empty catalogue is a configuration
        // gap, and it must look like one.
        if (templates.Count == 0)
            throw new InvalidOperationException(
                "No clearance lines are configured, so there is nothing to clear. Set up the "
                + "clearance form first — the FR-HR-183 defaults can be seeded in one step.");

        foreach (var template in templates)
        {
            await _unitOfWork.Repository<SeparationClearanceItem>().AddAsync(new SeparationClearanceItem
            {
                TenantId = tenantId,
                SeparationId = separationId,
                TemplateId = template.Id,
                // Snapshotted, not read through the template: editing the catalogue afterwards must
                // not rewrite a form somebody has already signed.
                Name = template.Name,
                Kind = template.Kind,
                OwningOrganizationUnitId = template.OwningOrganizationUnitId,
                IsMandatory = template.IsMandatory,
                SortOrder = template.SortOrder,
                Status = ClearanceItemStatus.Pending,
            });
        }

        separation.Status = SeparationStatus.ClearanceInProgress;
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Clearance started for separation {Number} with {Count} lines",
            separation.SeparationNumber, templates.Count);

        return await GetClearanceAsync(separationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SeparationClearanceDto> GetClearanceAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await Scoped(tenantId).AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == separationId, cancellationToken)
            ?? throw new ArgumentException($"Separation with ID '{separationId}' not found.");

        var items = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .Include(i => i.OwningOrganizationUnit)
            .Include(i => i.RecordedBy)
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.SeparationId == separationId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);

        var mandatoryOutstanding = items.Count(i => i.IsMandatory && !IsSettled(i.Status));

        return new SeparationClearanceDto
        {
            SeparationId = separation.Id,
            SeparationNumber = separation.SeparationNumber,
            EmployeeName = FullName(separation.Employee),
            SeparationStatus = separation.Status,
            SeparationStatusName = separation.Status.ToString(),
            Items = items.Select(ToClearanceItemDto).ToList(),
            TotalItems = items.Count,
            PendingItems = items.Count(i => i.Status == ClearanceItemStatus.Pending),
            ClearedItems = items.Count(i => i.Status == ClearanceItemStatus.Cleared),
            BlockedItems = items.Count(i => i.Status == ClearanceItemStatus.Blocked),
            WaivedItems = items.Count(i => i.Status == ClearanceItemStatus.Waived),
            NotApplicableItems = items.Count(i => i.Status == ClearanceItemStatus.NotApplicable),
            MandatoryOutstanding = mandatoryOutstanding,
            TotalOutstandingAmount = items.Sum(i => i.OutstandingAmount ?? 0m),
            CanComplete = items.Count > 0
                          && mandatoryOutstanding == 0
                          && separation.Status == SeparationStatus.ClearanceInProgress,
            BlockedReason = ClearanceBlockedReason(separation, items, mandatoryOutstanding),
        };
    }

    /// <inheritdoc />
    public async Task<SeparationClearanceItemDto> RecordClearanceItemAsync(
        Guid itemId, RecordClearanceItemDto dto, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        var item = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == tenantId && !i.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Clearance item with ID '{itemId}' not found.");

        var separation = await RequireAsync(tenantId, item.SeparationId, cancellationToken);

        if (separation.Status != SeparationStatus.ClearanceInProgress)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}; clearance lines can only be answered while "
                + "clearance is in progress.");

        // Both of these are answers that need explaining: "still outstanding" and "set aside" are
        // the two that stop a clearance form being a record of nothing.
        if (item.IsMandatory && dto.Status == ClearanceItemStatus.Waived && string.IsNullOrWhiteSpace(dto.Notes))
            throw new InvalidOperationException("Give a reason for waiving a mandatory clearance line.");

        if (dto.Status == ClearanceItemStatus.Blocked && string.IsNullOrWhiteSpace(dto.Notes))
            throw new InvalidOperationException("Say what is outstanding when marking a clearance line blocked.");

        if (dto.OutstandingAmount is { } amount)
        {
            if (amount < 0)
                throw new InvalidOperationException("An outstanding amount cannot be negative.");

            if (!CarriesAmount(item.Kind))
                throw new InvalidOperationException(
                    $"A '{item.Kind}' clearance line does not carry an amount. Record what is "
                    + "outstanding in the notes instead.");
        }

        item.Status = dto.Status;
        item.SignedOffBy = string.IsNullOrWhiteSpace(dto.SignedOffBy) ? null : dto.SignedOffBy.Trim();
        item.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        item.OutstandingAmount = dto.OutstandingAmount;
        item.RecordedById = actorEmployeeId;
        item.RecordedOn = DateTime.UtcNow;

        await _unitOfWork.Repository<SeparationClearanceItem>().UpdateAsync(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .Include(i => i.OwningOrganizationUnit)
            .Include(i => i.RecordedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken)
            ?? throw new InvalidOperationException("Clearance line saved but could not be reloaded.");

        return ToClearanceItemDto(saved);
    }

    /// <inheritdoc />
    public async Task<EmployeeSeparationDetailDto> CompleteClearanceAsync(
        Guid separationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var separation = await RequireAsync(tenantId, separationId, cancellationToken);

        if (separation.Status != SeparationStatus.ClearanceInProgress)
            throw new InvalidOperationException(
                $"This separation is {separation.Status}; only a clearance in progress can be completed.");

        var items = await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.SeparationId == separationId)
            .ToListAsync(cancellationToken);

        var outstanding = items.Where(i => i.IsMandatory && !IsSettled(i.Status)).ToList();

        // FR-HR-091, enforced. This is the gate the whole area turns on: entitlements are computed
        // only after the clearance form is complete, so anything still owed is still recoverable.
        if (outstanding.Count > 0)
        {
            var names = string.Join(", ", outstanding.Take(5).Select(i => i.Name));
            var more = outstanding.Count > 5 ? $" and {outstanding.Count - 5} more" : string.Empty;
            throw new InvalidOperationException(
                $"Clearance is not complete: {outstanding.Count} mandatory line(s) are still "
                + $"outstanding or blocked — {names}{more}.");
        }

        separation.Status = SeparationStatus.ClearanceCompleted;
        await _unitOfWork.Repository<EmployeeSeparation>().UpdateAsync(separation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Clearance completed for separation {Number}; {Amount} outstanding carried to settlement",
            separation.SeparationNumber, items.Sum(i => i.OutstandingAmount ?? 0m));

        return ToDetailDto(await ReloadAsync(tenantId, separationId, cancellationToken));
    }

    /// <summary>A clearance line with a terminal answer that does not block the gate.</summary>
    private static bool IsSettled(ClearanceItemStatus status)
        => status is ClearanceItemStatus.Cleared
                  or ClearanceItemStatus.Waived
                  or ClearanceItemStatus.NotApplicable;

    private static string? ClearanceBlockedReason(
        EmployeeSeparation separation, List<SeparationClearanceItem> items, int mandatoryOutstanding)
    {
        if (items.Count == 0)
            return "Clearance has not been started for this separation.";

        if (separation.Status != SeparationStatus.ClearanceInProgress)
            return $"This separation is {separation.Status}; clearance can only be completed while it is in progress.";

        if (mandatoryOutstanding > 0)
            return $"{mandatoryOutstanding} mandatory clearance line(s) are still outstanding or blocked.";

        return null;
    }

    private async Task RequireOrganizationUnitAsync(Guid tenantId, Guid? unitId, CancellationToken cancellationToken)
    {
        if (unitId is not { } id || id == Guid.Empty) return;

        var exists = await _unitOfWork.Repository<OrganizationUnit>().GetQueryable()
            .AnyAsync(u => u.Id == id && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);

        if (!exists)
            throw new ArgumentException($"Organisation unit with ID '{id}' not found.");
    }

    private async Task<SeparationClearanceTemplate> ReloadTemplateAsync(
        Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<SeparationClearanceTemplate>().GetQueryable()
               .Include(t => t.OwningOrganizationUnit)
               .AsNoTracking()
               .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, cancellationToken)
           ?? throw new InvalidOperationException("Clearance line saved but could not be reloaded.");

    private static SeparationClearanceTemplateDto ToTemplateDto(SeparationClearanceTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Kind = t.Kind,
        KindName = t.Kind.ToString(),
        Description = t.Description,
        OwningOrganizationUnitId = t.OwningOrganizationUnitId,
        OwningOrganizationUnitName = t.OwningOrganizationUnit?.Name,
        IsMandatory = t.IsMandatory,
        IsActive = t.IsActive,
        SortOrder = t.SortOrder,
        CarriesAmount = CarriesAmount(t.Kind),
    };

    private static SeparationClearanceItemDto ToClearanceItemDto(SeparationClearanceItem i) => new()
    {
        Id = i.Id,
        SeparationId = i.SeparationId,
        TemplateId = i.TemplateId,
        Name = i.Name,
        Kind = i.Kind,
        KindName = i.Kind.ToString(),
        OwningOrganizationUnitId = i.OwningOrganizationUnitId,
        OwningOrganizationUnitName = i.OwningOrganizationUnit?.Name,
        IsMandatory = i.IsMandatory,
        SortOrder = i.SortOrder,
        Status = i.Status,
        StatusName = i.Status.ToString(),
        OutstandingAmount = i.OutstandingAmount,
        CarriesAmount = CarriesAmount(i.Kind),
        Notes = i.Notes,
        SignedOffBy = i.SignedOffBy,
        RecordedById = i.RecordedById,
        RecordedByName = i.RecordedBy == null ? null : FullName(i.RecordedBy),
        RecordedOn = i.RecordedOn,
    };

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
