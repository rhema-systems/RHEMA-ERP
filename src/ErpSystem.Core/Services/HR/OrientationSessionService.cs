using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Services.HR.Orientation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class OrientationSessionService : IOrientationSessionService
{
    private readonly IOrientationSessionRepository _sessionRepository;
    private readonly IOrientationProgramRepository _programRepository;
    private readonly IOrientationSessionFacilitatorRepository _facilitatorRepository;
    private readonly IOrientationAttendanceRecordRepository _attendanceRepository;
    private readonly IEmployeeOrientationRepository _enrollmentRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOnboardingOrientationNotices _notices;
    private readonly IEmployeeOrientationService _enrolments;
    private readonly ILogger<OrientationSessionService> _logger;

    public OrientationSessionService(
        IOrientationSessionRepository sessionRepository,
        IOrientationProgramRepository programRepository,
        IOrientationSessionFacilitatorRepository facilitatorRepository,
        IOrientationAttendanceRecordRepository attendanceRepository,
        IEmployeeOrientationRepository enrollmentRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        IOnboardingOrientationNotices notices,
        IEmployeeOrientationService enrolments,
        ILogger<OrientationSessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _programRepository = programRepository;
        _facilitatorRepository = facilitatorRepository;
        _attendanceRepository = attendanceRepository;
        _enrollmentRepository = enrollmentRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _notices = notices;
        _enrolments = enrolments;
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

    private async Task<OrientationSession> GetOwnedSessionAsync(Guid id)
    {
        var entity = await _sessionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation session with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationSession> GetOwnedSessionWithDetailsAsync(Guid id)
    {
        var entity = await _sessionRepository.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation session with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationSessionFacilitator> GetOwnedFacilitatorAsync(Guid id)
    {
        var entity = await _facilitatorRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation session facilitator with ID '{id}' not found.");
        return entity;
    }

    // ====================================================================
    // QUERIES
    // ====================================================================

    public async Task<OrientationSessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionWithDetailsAsync(id);
        var dto = entity.ToDto();
        var map = await _unitOfWork.ResolveEmployeesAsync(GetTenantId(), dto.Facilitators.Select(f => f.EmployeeId));
        dto.Facilitators.FillNames(map);
        await FillRegisterNotesAsync(dto.Facilitators, GetTenantId(), cancellationToken);
        return dto;
    }

    /// <summary>
    /// Fills EnrolledCount on session summaries from one grouped query. List reads do not include the
    /// enrollments collection, and an un-included collection is empty rather than null — so every
    /// session on every list used to report 0 enrolled, which is exactly the number the enrolment
    /// picker subtracts from capacity to show remaining seats.
    /// </summary>
    private async Task<List<OrientationSessionSummaryDto>> HydrateSeatCountsAsync(
        List<OrientationSessionSummaryDto> summaries, Guid tenantId)
    {
        if (summaries.Count == 0) return summaries;

        var counts = await _sessionRepository.GetEnrolledCountsAsync(tenantId, summaries.Select(s => s.Id));
        foreach (var summary in summaries)
            summary.EnrolledCount = counts.TryGetValue(summary.Id, out var c) ? c : 0;

        return summaries;
    }

    public async Task<OrientationSessionDto?> GetBySessionCodeAsync(string sessionCode, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _sessionRepository.GetBySessionCodeAsync(sessionCode);
        if (entity == null || entity.TenantId != tenantId) return null;

        var dto = entity.ToDto();
        var map = await _unitOfWork.ResolveEmployeesAsync(tenantId, dto.Facilitators.Select(f => f.EmployeeId));
        dto.Facilitators.FillNames(map);
        await FillRegisterNotesAsync(dto.Facilitators, tenantId, cancellationToken);
        return dto;
    }

    public async Task<IEnumerable<OrientationSessionSummaryDto>> GetByProgramIdAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateSeatCountsAsync(
            (await _sessionRepository.GetByProgramIdAsync(programId))
                .Where(s => s.TenantId == tenantId).ToSummaryDtoList().ToList(), tenantId);
    }

    public async Task<IEnumerable<OrientationSessionSummaryDto>> GetByStatusAsync(OrientationSessionStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateSeatCountsAsync(
            (await _sessionRepository.GetByStatusAsync(status))
                .Where(s => s.TenantId == tenantId).ToSummaryDtoList().ToList(), tenantId);
    }

    public async Task<IEnumerable<OrientationSessionSummaryDto>> GetUpcomingAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateSeatCountsAsync(
            (await _sessionRepository.GetUpcomingAsync(daysAhead))
                .Where(s => s.TenantId == tenantId).ToSummaryDtoList().ToList(), tenantId);
    }

    public async Task<IEnumerable<OrientationSessionSummaryDto>> GetOpenForEnrollmentAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateSeatCountsAsync(
            (await _sessionRepository.GetOpenForEnrollmentAsync())
                .Where(s => s.TenantId == tenantId).ToSummaryDtoList().ToList(), tenantId);
    }

    // ====================================================================
    // CRUD + LIFECYCLE
    // ====================================================================

    public async Task<OrientationSessionDto> CreateAsync(CreateOrientationSessionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var program = await _programRepository.GetByIdAsync(createDto.ProgramId);
        if (program == null || program.TenantId != tenantId || program.IsDeleted)
            throw new ArgumentException($"Orientation program with ID '{createDto.ProgramId}' not found.");

        // Round 4, lane L: the sessions screen offers only active programmes and its code said "the
        // server would refuse it anyway" — it did not. It does now, so the claim is true for any caller.
        if (program.Status != OrientationProgramStatus.Active)
            throw new InvalidOperationException(
                $"Sessions can only be scheduled for an active programme; \"{program.Title}\" is {program.Status}. Publish it first.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        entity.SessionCode = string.IsNullOrWhiteSpace(createDto.SessionCode)
            ? await GenerateSessionCodeAsync(tenantId, cancellationToken)
            : createDto.SessionCode.Trim();

        var codeExists = await _sessionRepository.SessionCodeExistsAsync(tenantId, entity.SessionCode);
        if (codeExists)
            throw new InvalidOperationException($"Session code '{entity.SessionCode}' is already in use.");

        await _sessionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation session created: {Code}", entity.SessionCode);

        return (await _sessionRepository.GetWithDetailsAsync(entity.Id))!.ToDto();
    }

    /// <summary>
    /// Runs a session again on a new date (round 4, lane J3) — the common case for a briefing that
    /// repeats. A new code, a Draft (as every new session is), the same place, capacity, rules and
    /// facilitators.
    /// </summary>
    /// <remarks>
    /// <para><b>Moved with the date:</b> the end keeps the original's length unless one is given, and
    /// the enrolment deadline keeps the same lead before the start.</para>
    ///
    /// <para><b>Not copied:</b> enrolments and attendance (people sign up for the new run), the actual
    /// start and end, and the recording — it is of the old run. Facilitators come across marked
    /// <b>not confirmed</b>: they agreed to the old date, not this one.</para>
    ///
    /// <para>Scheduling rules apply as they do to a new session: the programme must be active.</para>
    /// </remarks>
    public async Task<OrientationSessionDto> CloneAsync(
        Guid sourceId, CloneOrientationSessionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        if (dto.ScheduledStartAt is not { } start)
            throw new InvalidOperationException("The new run needs a start date and time.");

        var source = await GetOwnedSessionAsync(sourceId);

        var program = await _programRepository.GetByIdAsync(source.ProgramId);
        if (program == null || program.TenantId != tenantId || program.IsDeleted)
            throw new ArgumentException($"Orientation program with ID '{source.ProgramId}' not found.");
        if (program.Status != OrientationProgramStatus.Active)
            throw new InvalidOperationException(
                $"Sessions can only be scheduled for an active programme; \"{program.Title}\" is {program.Status}.");

        DateTime? end = dto.ScheduledEndAt
            ?? (source.ScheduledStartAt is { } oldStart && source.ScheduledEndAt is { } oldEnd ? start + (oldEnd - oldStart) : null);
        if (end is { } e && e <= start)
            throw new InvalidOperationException("The new run must end after it starts.");

        DateTime? deadline = source.EnrollmentDeadlineAt is { } oldDeadline && source.ScheduledStartAt is { } oldFrom
            ? start - (oldFrom - oldDeadline)
            : null;

        var by = createdByUserId.ToString();
        var clone = new OrientationSession
        {
            TenantId = tenantId,
            ProgramId = source.ProgramId,
            SessionCode = await GenerateSessionCodeAsync(tenantId, cancellationToken),
            Title = string.IsNullOrWhiteSpace(dto.Title) ? source.Title : dto.Title.Trim(),
            Description = source.Description,
            DeliveryMode = source.DeliveryMode,
            Status = OrientationSessionStatus.Draft,
            ScheduledStartAt = start,
            ScheduledEndAt = end,
            VenueDescription = source.VenueDescription,
            VirtualMeetingUrl = source.VirtualMeetingUrl,
            MaxParticipants = source.MaxParticipants,
            EnrollmentDeadlineAt = deadline,
            AllowWaitlist = source.AllowWaitlist,
            RequiresApproval = source.RequiresApproval,
            RecordingUrl = null,
            ParticipantInstructions = source.ParticipantInstructions,
            CreatedBy = by,
        };
        await _sessionRepository.AddAsync(clone);

        var facilitators = await _facilitatorRepository.GetQueryable().AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.SessionId == sourceId && !f.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var facilitator in facilitators)
        {
            await _facilitatorRepository.AddAsync(new OrientationSessionFacilitator
            {
                TenantId = tenantId,
                SessionId = clone.Id,
                EmployeeId = facilitator.EmployeeId,
                ExternalFacilitatorName = facilitator.ExternalFacilitatorName,
                ExternalFacilitatorEmail = facilitator.ExternalFacilitatorEmail,
                ExternalFacilitatorOrganization = facilitator.ExternalFacilitatorOrganization,
                // Lane M: the register pick comes with its snapshot. A vendor blacklisted since is not
                // dropped silently — the new run's page says so (RegisterNote), and HR decides.
                ExternalFacilitatorVendorId = facilitator.ExternalFacilitatorVendorId,
                ExternalFacilitatorTrainerProfileId = facilitator.ExternalFacilitatorTrainerProfileId,
                Role = facilitator.Role,
                HasConfirmed = false,
                Notes = facilitator.Notes,
                CreatedBy = by,
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation session run again: {Source} -> {Copy} on {Start:u}", source.SessionCode, clone.SessionCode, start);

        return (await _sessionRepository.GetWithDetailsAsync(clone.Id))!.ToDto();
    }

    public async Task<OrientationSessionDto> UpdateAsync(UpdateOrientationSessionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(updateDto.Id);

        // Lane K-b: an edit that moves a live session tells everyone on it, with where it moved from.
        var before = OrientationSessionSnapshot.Of(entity);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _sessionRepository.UpdateAsync(entity);
        await _notices.SessionRescheduledAsync(entity, before, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _sessionRepository.GetWithDetailsAsync(entity.Id))!.ToDto();
    }

    public async Task<bool> ChangeStatusAsync(ChangeOrientationSessionStatusDto changeDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(changeDto.SessionId);

        var from = entity.Status;
        entity.Status = changeDto.NewStatus;
        if (changeDto.NewStatus == OrientationSessionStatus.InProgress && entity.ActualStartAt == null)
            entity.ActualStartAt = DateTime.UtcNow;
        if (changeDto.NewStatus == OrientationSessionStatus.Completed && entity.ActualEndAt == null)
            entity.ActualEndAt = DateTime.UtcNow;

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _sessionRepository.UpdateAsync(entity);

        // Lane K-b. A live session called off tells everyone on it; a draft going live tells its
        // employee facilitators — nobody else is on a draft (lane L refuses enrolment onto one).
        if (from != changeDto.NewStatus)
        {
            if (OrientationSessionLife.IsLive(from)
                && changeDto.NewStatus is OrientationSessionStatus.Cancelled or OrientationSessionStatus.Postponed)
                await _notices.SessionCalledOffAsync(entity, changeDto.NewStatus, cancellationToken);
            else if (from == OrientationSessionStatus.Draft && OrientationSessionLife.IsLive(changeDto.NewStatus))
                await _notices.FacilitatorsScheduledAsync(entity, await InternalFacilitatorIdsAsync(entity, cancellationToken), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation session {Code} status changed to {Status}", entity.SessionCode, changeDto.NewStatus);

        // Round 4, lane R (R-D2): a session marked Completed completes the people its register shows
        // there, on a programme that is only its session. This runs after the status is saved, so a
        // failure here leaves the session completed, and saving the register again finishes the job.
        if (changeDto.NewStatus == OrientationSessionStatus.Completed)
            await _enrolments.CompleteAttendedOnSessionAsync(entity.Id, updatedByUserId, cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSessionAsync(id);

        var enrolledCount = await _sessionRepository.GetEnrolledCountAsync(id);
        if (enrolledCount > 0)
            throw new InvalidOperationException("Cannot delete a session that has active enrollments. Cancel it instead.");

        await _sessionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // FACILITATORS
    // ====================================================================

    public async Task<OrientationSessionFacilitatorDto> AddFacilitatorAsync(CreateOrientationSessionFacilitatorDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        if (!await _sessionRepository.ExistsAsync(s => s.Id == createDto.SessionId && s.TenantId == tenantId && !s.IsDeleted))
            throw new ArgumentException($"Orientation session with ID '{createDto.SessionId}' not found.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await ResolveFacilitatorAsync(entity, createDto.EmployeeId, createDto.ExternalFacilitatorVendorId,
            createDto.ExternalFacilitatorTrainerProfileId, previous: null, tenantId, cancellationToken);

        await _facilitatorRepository.AddAsync(entity);

        // Lane K-b: an employee added to a session that is already live is told of it (a draft's are
        // told when it goes live).
        if (entity.EmployeeId is { } facilitatorId)
        {
            var session = await GetOwnedSessionAsync(createDto.SessionId);
            if (OrientationSessionLife.IsLive(session.Status))
                await _notices.FacilitatorsScheduledAsync(session, new[] { facilitatorId }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await HydrateFacilitatorAsync(entity.ToDto(), tenantId, cancellationToken);
    }

    /// <summary>The employees facilitating a session — an external facilitator has no inbox here.</summary>
    private async Task<List<Guid>> InternalFacilitatorIdsAsync(OrientationSession session, CancellationToken cancellationToken)
        => await _facilitatorRepository.GetQueryable().AsNoTracking()
            .Where(f => f.TenantId == session.TenantId && f.SessionId == session.Id && !f.IsDeleted && f.EmployeeId != null)
            .Select(f => f.EmployeeId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    /// <summary>What a facilitator was before an edit — who it named, and the snapshot a register pick wrote.</summary>
    private sealed record FacilitatorPick(Guid? EmployeeId, Guid? VendorId, Guid? TrainerId, string? Name, string? Email, string? Organization);

    /// <summary>
    /// Who a facilitator is — one of three (round 4, lane M): one of the organisation's employees; a
    /// vendor, and its trainer when known, from the training vendor register; or somebody from outside,
    /// typed. Called on add and on edit, with the entity already carrying the typed values.
    /// </summary>
    /// <remarks>
    /// <para><b>A register pick writes the snapshot</b> — the vendor's name as the organisation, the
    /// trainer's name, and the trainer's own address when their contact is one, else the vendor's —
    /// and typed values are ignored. It is written when the pick is <b>made or changed</b>, never
    /// otherwise: confirming a facilitator or adding a note keeps what the session said, even if the
    /// vendor has been renamed since. The pick's availability (active, not blacklisted) is checked at
    /// the same moment and only then, so an old session's facilitator can still be edited after the
    /// vendor has been blacklisted — the same rule lane L applied to an enrolment's session. The
    /// register's current state is reported instead, as <c>RegisterNote</c> on every read.</para>
    ///
    /// <para><b>An edit without a register pick had no check at all</b> — the add path refused a
    /// facilitator with neither an employee nor a name, the edit path let one be saved. Both run this
    /// now.</para>
    /// </remarks>
    private async Task ResolveFacilitatorAsync(
        OrientationSessionFacilitator entity, Guid? employeeId, Guid? vendorId, Guid? trainerId,
        FacilitatorPick? previous, Guid tenantId, CancellationToken cancellationToken)
    {
        entity.EmployeeId = employeeId;
        entity.ExternalFacilitatorVendorId = null;
        entity.ExternalFacilitatorTrainerProfileId = null;
        var fromRegister = vendorId is not null || trainerId is not null;

        if (employeeId is { } employee)
        {
            if (fromRegister)
                throw new InvalidOperationException(
                    "A facilitator is either one of your employees or somebody from outside — pick one, not both.");
            // Checked when the employee is named or changed, like a register pick — an edit of an old
            // session's facilitator must not start failing because the employee record has gone since.
            if (previous?.EmployeeId != employee
                && !(await _unitOfWork.ResolveEmployeesAsync(tenantId, new Guid?[] { employee })).ContainsKey(employee))
                throw new InvalidOperationException("That employee was not found in this organisation.");
            return;
        }

        if (!fromRegister)
        {
            entity.ExternalFacilitatorName = Typed(entity.ExternalFacilitatorName);
            entity.ExternalFacilitatorEmail = Typed(entity.ExternalFacilitatorEmail);
            entity.ExternalFacilitatorOrganization = Typed(entity.ExternalFacilitatorOrganization);
            if (entity.ExternalFacilitatorName is null)
                throw new InvalidOperationException(
                    "A facilitator must be one of your employees, a vendor or trainer from the training vendor register, or a named person from outside.");
            return;
        }

        // The same pick as before — a trainer alone names the same pick when it is the same trainer.
        var samePick = previous is { VendorId: not null } p
                       && trainerId == p.TrainerId
                       && (vendorId == p.VendorId || (vendorId is null && trainerId is not null));
        if (samePick)
        {
            entity.ExternalFacilitatorVendorId = previous!.VendorId;
            entity.ExternalFacilitatorTrainerProfileId = previous.TrainerId;
            entity.ExternalFacilitatorName = previous.Name;
            entity.ExternalFacilitatorEmail = previous.Email;
            entity.ExternalFacilitatorOrganization = previous.Organization;
            return;
        }

        TrainerProfile? trainer = null;
        if (trainerId is { } tId)
        {
            trainer = await _unitOfWork.Repository<TrainerProfile>().GetQueryable().AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("That trainer is not in the training vendor register.");
            if (trainer.VendorId is not { } trainersVendor)
                throw new InvalidOperationException(
                    $"{trainer.Name} is one of your own trainers, not a vendor's — add them as an employee.");
            if (vendorId is { } named && named != trainersVendor)
                throw new InvalidOperationException($"{trainer.Name} is not one of that vendor's trainers.");
            vendorId = trainersVendor;
        }

        var vendor = await _unitOfWork.Repository<TrainingVendor>().GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vendorId && v.TenantId == tenantId && !v.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("That vendor is not in the training vendor register.");
        if (vendor.IsBlacklisted)
            throw new InvalidOperationException(
                $"{vendor.Name} is blacklisted in the training vendor register{(string.IsNullOrWhiteSpace(vendor.BlacklistReason) ? "" : $" — {vendor.BlacklistReason.Trim()}")}.");
        if (!vendor.IsActive)
            throw new InvalidOperationException($"{vendor.Name} is not active in the training vendor register.");
        if (trainer is { IsActive: false })
            throw new InvalidOperationException($"{trainer.Name} is not active in the training vendor register.");

        entity.ExternalFacilitatorVendorId = vendor.Id;
        entity.ExternalFacilitatorTrainerProfileId = trainer?.Id;
        entity.ExternalFacilitatorOrganization = vendor.Name;
        entity.ExternalFacilitatorName = trainer?.Name;
        entity.ExternalFacilitatorEmail = EmailIn(trainer?.Contact) ?? Typed(vendor.PrimaryContactEmail);
    }

    private static string? Typed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>A trainer's contact is "cell / personal email" in one box — used only when it is an address.</summary>
    private static string? EmailIn(string? contact) =>
        Typed(contact) is { } c && new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(c) ? c : null;

    /// <summary>
    /// Facilitators reference employees by id with no navigation, so a write response has to go through
    /// the same name lookup the list read uses — otherwise adding a facilitator returns a blank name
    /// and the row only acquires one on the next refetch.
    /// </summary>
    private async Task<OrientationSessionFacilitatorDto> HydrateFacilitatorAsync(OrientationSessionFacilitatorDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var list = new List<OrientationSessionFacilitatorDto> { dto };
        var map = await _unitOfWork.ResolveEmployeesAsync(tenantId, list.Select(f => f.EmployeeId));
        list.FillNames(map);
        await FillRegisterNotesAsync(list, tenantId, cancellationToken);
        return dto;
    }

    /// <summary>
    /// What the register says now about each vendor and trainer picked earlier, when it matters. Reads
    /// deleted rows on purpose: "removed from the register" is one of the answers.
    /// </summary>
    private async Task FillRegisterNotesAsync(IReadOnlyCollection<OrientationSessionFacilitatorDto> facilitators, Guid tenantId, CancellationToken cancellationToken)
    {
        var vendorIds = facilitators.Where(f => f.ExternalFacilitatorVendorId.HasValue).Select(f => f.ExternalFacilitatorVendorId!.Value).Distinct().ToList();
        if (vendorIds.Count == 0) return;
        var trainerIds = facilitators.Where(f => f.ExternalFacilitatorTrainerProfileId.HasValue).Select(f => f.ExternalFacilitatorTrainerProfileId!.Value).Distinct().ToList();

        var vendors = await _unitOfWork.Repository<TrainingVendor>()
            .GetQueryableIncludingDeleted(v => v.TenantId == tenantId && vendorIds.Contains(v.Id)).AsNoTracking()
            .ToDictionaryAsync(v => v.Id, cancellationToken);
        var trainers = trainerIds.Count == 0
            ? new Dictionary<Guid, TrainerProfile>()
            : await _unitOfWork.Repository<TrainerProfile>()
                .GetQueryableIncludingDeleted(t => t.TenantId == tenantId && trainerIds.Contains(t.Id)).AsNoTracking()
                .ToDictionaryAsync(t => t.Id, cancellationToken);

        foreach (var f in facilitators.Where(f => f.ExternalFacilitatorVendorId.HasValue))
        {
            var notes = new List<string>();
            var vendorName = f.ExternalFacilitatorOrganization ?? "The vendor";
            if (!vendors.TryGetValue(f.ExternalFacilitatorVendorId!.Value, out var vendor) || vendor.IsDeleted)
                notes.Add($"{vendorName} has since been removed from the training vendor register");
            else if (vendor.IsBlacklisted)
                notes.Add($"{vendor.Name} has since been blacklisted in the training vendor register");
            else if (!vendor.IsActive)
                notes.Add($"{vendor.Name} is no longer active in the training vendor register");

            if (f.ExternalFacilitatorTrainerProfileId is { } trainerId)
            {
                var trainerName = f.ExternalFacilitatorName ?? "The trainer";
                if (!trainers.TryGetValue(trainerId, out var trainer) || trainer.IsDeleted)
                    notes.Add($"{trainerName} has since been removed from the register");
                else if (!trainer.IsActive)
                    notes.Add($"{trainer.Name} is no longer active in the register");
                else if (trainer.VendorId != f.ExternalFacilitatorVendorId)
                    notes.Add($"{trainer.Name} is no longer listed with {vendorName}");
            }

            f.RegisterNote = notes.Count == 0 ? null : string.Join("; ", notes) + ".";
        }
    }

    public async Task<IEnumerable<OrientationSessionFacilitatorDto>> GetFacilitatorsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var list = (await _facilitatorRepository.GetBySessionIdAsync(sessionId))
            .Where(f => f.TenantId == tenantId)
            .Select(f => f.ToDto())
            .ToList();
        var map = await _unitOfWork.ResolveEmployeesAsync(tenantId, list.Select(f => f.EmployeeId));
        list.FillNames(map);
        await FillRegisterNotesAsync(list, tenantId, cancellationToken);
        return list;
    }

    public async Task<OrientationSessionFacilitatorDto> UpdateFacilitatorAsync(UpdateOrientationSessionFacilitatorDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFacilitatorAsync(updateDto.Id);
        var previous = new FacilitatorPick(entity.EmployeeId, entity.ExternalFacilitatorVendorId, entity.ExternalFacilitatorTrainerProfileId,
            entity.ExternalFacilitatorName, entity.ExternalFacilitatorEmail, entity.ExternalFacilitatorOrganization);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await ResolveFacilitatorAsync(entity, updateDto.EmployeeId, updateDto.ExternalFacilitatorVendorId,
            updateDto.ExternalFacilitatorTrainerProfileId, previous, entity.TenantId, cancellationToken);

        await _facilitatorRepository.UpdateAsync(entity);

        // Lane K-b: the facilitator handed to another employee on a live session — the new one is told.
        if (entity.EmployeeId is { } facilitatorId && facilitatorId != previous.EmployeeId)
        {
            var session = await GetOwnedSessionAsync(entity.SessionId);
            if (OrientationSessionLife.IsLive(session.Status))
                await _notices.FacilitatorsScheduledAsync(session, new[] { facilitatorId }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await HydrateFacilitatorAsync(entity.ToDto(), entity.TenantId, cancellationToken);
    }

    public async Task<bool> RemoveFacilitatorAsync(Guid facilitatorId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFacilitatorAsync(facilitatorId);

        await _facilitatorRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // ATTENDANCE
    // ====================================================================

    public async Task<IEnumerable<OrientationAttendanceRecordDto>> MarkAttendanceAsync(MarkOrientationAttendanceDto markDto, Guid markedByUserId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(markDto.SessionId);
        var tenantId = session.TenantId;

        var results = new List<OrientationAttendanceRecord>();

        foreach (var entry in markDto.Entries)
        {
            var enrollment = await _enrollmentRepository.GetByIdAsync(entry.EnrollmentId);
            if (enrollment == null || enrollment.TenantId != tenantId)
                throw new ArgumentException($"Orientation enrollment with ID '{entry.EnrollmentId}' not found.");

            // The route says which session's register this is; without this check an entry could mark
            // attendance against an enrollment belonging to an entirely different session.
            if (enrollment.SessionId != session.Id)
                throw new InvalidOperationException(
                    $"Enrollment '{entry.EnrollmentId}' is not enrolled in session '{session.SessionCode}'.");

            var record = await _attendanceRepository.GetByEnrollmentAndDayAsync(entry.EnrollmentId, markDto.SessionDay);

            if (record == null)
            {
                record = new OrientationAttendanceRecord
                {
                    TenantId = tenantId,
                    EnrollmentId = entry.EnrollmentId,
                    SessionDay = markDto.SessionDay,
                    CreatedBy = markedByUserId.ToString(),
                };
                ApplyAttendanceEntry(record, entry, markedByUserId);
                await _attendanceRepository.AddAsync(record);
            }
            else
            {
                if (record.TenantId != tenantId)
                    throw new ArgumentException($"Orientation attendance record for enrollment '{entry.EnrollmentId}' not found.");

                ApplyAttendanceEntry(record, entry, markedByUserId);
                record.UpdatedAt = DateTime.UtcNow;
                record.UpdatedBy = markedByUserId.ToString();
                await _attendanceRepository.UpdateAsync(record);
            }

            results.Add(record);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Round 4, lane R: a register saved on a session already marked Completed completes the people
        // it now shows there, so the order HR marks the register and closes the session does not matter.
        if (session.Status == OrientationSessionStatus.Completed)
            await _enrolments.CompleteAttendedOnSessionAsync(session.Id, markedByUserId, cancellationToken);

        // Re-read through the includes chain: the DTO's EmployeeId comes off the enrollment navigation,
        // which these tracked entities never loaded, and that id is also the key the name hydrator uses
        // — so returning them raw blanked the employee on every row the register had just saved.
        return await HydrateAttendanceAsync(
            (await _attendanceRepository.GetBySessionIdAsync(session.Id))
                .Where(a => a.TenantId == tenantId && a.SessionDay == markDto.SessionDay)
                .Select(a => a.ToDto())
                .ToList());
    }

    public async Task<IEnumerable<OrientationAttendanceRecordDto>> GetAttendanceForSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateAttendanceAsync(
            (await _attendanceRepository.GetBySessionIdAsync(sessionId))
                .Where(a => a.TenantId == tenantId)
                .Select(a => a.ToDto())
                .ToList());
    }

    public async Task<IEnumerable<OrientationAttendanceRecordDto>> GetAttendanceForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateAttendanceAsync(
            (await _attendanceRepository.GetByEnrollmentIdAsync(enrollmentId))
                .Where(a => a.TenantId == tenantId)
                .Select(a => a.ToDto())
                .ToList());
    }

    private async Task<List<OrientationAttendanceRecordDto>> HydrateAttendanceAsync(List<OrientationAttendanceRecordDto> list)
    {
        var ids = list.Select(a => a.EmployeeId).Concat(list.Select(a => a.MarkedByEmployeeId));
        var map = await _unitOfWork.ResolveEmployeesAsync(GetTenantId(), ids);
        list.FillNames(map);
        return list;
    }

    // ====================================================================
    // HELPERS
    // ====================================================================

    private static void ApplyAttendanceEntry(OrientationAttendanceRecord record, OrientationAttendanceEntryDto entry, Guid markedByUserId)
    {
        record.AttendanceStatus = entry.AttendanceStatus;
        record.CheckInAt = entry.CheckInAt;
        record.CheckOutAt = entry.CheckOutAt;
        record.MarkedLate = entry.MarkedLate;
        record.AbsenceReason = entry.AbsenceReason;
        record.MarkedByEmployeeId = markedByUserId;

        record.AttendedMinutes = entry.CheckInAt.HasValue && entry.CheckOutAt.HasValue
            ? (int)Math.Max(0, (entry.CheckOutAt.Value - entry.CheckInAt.Value).TotalMinutes)
            : record.AttendedMinutes;
    }

    /// <summary>
    /// Numbers off the highest code ever issued, soft-deleted sessions included.
    /// (TenantId, SessionCode) is UNIQUE and a soft delete does not release the value, so counting live
    /// rows produced a code the database still held — deleting one session made the next create die on
    /// a duplicate key. The old while-loop only skipped *live* collisions, which is precisely the case
    /// that was never the problem.
    /// </summary>
    private async Task<string> GenerateSessionCodeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"OSN-{DateTime.UtcNow.Year}-";
        var issued = await _sessionRepository
            .GetQueryableIncludingDeleted(s => s.TenantId == tenantId && s.SessionCode.StartsWith(prefix))
            .Select(s => s.SessionCode)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var code in issued)
        {
            if (int.TryParse(code[prefix.Length..], out var n) && n > max) max = n;
        }

        return $"{prefix}{(max + 1):D4}";
    }
}
