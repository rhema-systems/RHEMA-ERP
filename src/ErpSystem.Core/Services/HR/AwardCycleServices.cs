using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Award cycles: the run of an award and the windows in which people may take part.
/// </summary>
/// <remarks>
/// See <see cref="AwardCycle"/> for the design. The rules here all come from one place — an award
/// type's selection model, set in slice 2 — so a cycle can never describe a stage the award does
/// not have.
/// </remarks>
public class AwardCycleService : IAwardCycleService
{
    private readonly IAwardCycleRepository _cycleRepo;
    private readonly IAwardTypeRepository _awardTypeRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardCycleService(
        IAwardCycleRepository cycleRepo,
        IAwardTypeRepository awardTypeRepo,
        IAwardNominationRepository nominationRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _cycleRepo = cycleRepo;
        _awardTypeRepo = awardTypeRepo;
        _nominationRepo = nominationRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private async Task<AwardCycle> GetOwnedAsync(Guid id)
    {
        var entity = await _cycleRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardCycle {id} not found.");
        return entity;
    }

    private async Task<AwardType> GetAwardTypeAsync(Guid awardTypeId)
    {
        var awardType = await _awardTypeRepo.GetByIdAsync(awardTypeId);
        if (awardType == null || awardType.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"AwardType {awardTypeId} not found.");
        return awardType;
    }

    // ── the window rules ──────────────────────────────────────────────────────

    /// <summary>
    /// Checks the windows against the award's selection model and against each other.
    /// </summary>
    /// <remarks>
    /// Every message names the field to change, because this is the form HR fills in to open an
    /// award and a refusal that only says "invalid" leaves them guessing which of four dates is
    /// wrong.
    /// </remarks>
    private static void ValidateWindows(
        AwardType awardType,
        DateTime? nomOpens, DateTime? nomCloses,
        DateTime? voteOpens, DateTime? voteCloses)
    {
        var hasNominationStage = awardType.NominationSource != AwardNominationSource.ManagementDirect;
        var isVoted = awardType.WinnerDecision == AwardWinnerDecision.StaffVote;

        // 1. A nomination stage needs both ends of its window, or neither means anything.
        if (hasNominationStage)
        {
            if (nomOpens == null || nomCloses == null)
                throw AwardsWorkflowException.Invalid(
                    "This award is nominated for, so the cycle needs both a nomination opening and a " +
                    "nomination closing date. Set NominationOpensOn and NominationClosesOn.");

            if (nomCloses <= nomOpens)
                throw AwardsWorkflowException.Invalid(
                    $"Nominations cannot close ({nomCloses:yyyy-MM-dd HH:mm}) before or when they open " +
                    $"({nomOpens:yyyy-MM-dd HH:mm}). Set NominationClosesOn later than NominationOpensOn.");
        }
        else if (nomOpens != null || nomCloses != null)
        {
            throw AwardsWorkflowException.Invalid(
                "This award is a direct management selection, so it has no nomination stage and the " +
                "cycle must not carry a nomination window. Clear NominationOpensOn and " +
                "NominationClosesOn, or change the award's nomination source.");
        }

        // 2. A voting window is required exactly when there is a vote — and forbidden otherwise,
        //    because a voting window on an award nobody votes on describes a ballot that will
        //    never be held.
        if (isVoted)
        {
            if (voteOpens == null || voteCloses == null)
                throw AwardsWorkflowException.Invalid(
                    "This award is decided by a staff vote, so the cycle needs both a voting opening " +
                    "and a voting closing date. Set VotingOpensOn and VotingClosesOn.");

            if (voteCloses <= voteOpens)
                throw AwardsWorkflowException.Invalid(
                    $"Voting cannot close ({voteCloses:yyyy-MM-dd HH:mm}) before or when it opens " +
                    $"({voteOpens:yyyy-MM-dd HH:mm}). Set VotingClosesOn later than VotingOpensOn.");

            // 3. TDC's note: the awards are "listed for people to nominate before the voting takes
            //    place". Voting on a list that is still being added to would mean early ballots
            //    were cast against a different set of candidates from later ones.
            if (nomCloses != null && voteOpens < nomCloses)
                throw AwardsWorkflowException.Invalid(
                    $"Voting cannot open ({voteOpens:yyyy-MM-dd HH:mm}) before nominations close " +
                    $"({nomCloses:yyyy-MM-dd HH:mm}) — the list of nominees would still be changing " +
                    "while people were voting on it. Set VotingOpensOn on or after NominationClosesOn.");
        }
        else if (voteOpens != null || voteCloses != null)
        {
            throw AwardsWorkflowException.Invalid(
                $"This award is decided by {awardType.WinnerDecision}, not a staff vote, so the cycle " +
                "must not carry a voting window. Clear VotingOpensOn and VotingClosesOn, or change " +
                "the award's winner decision to StaffVote.");
        }
    }

    // ── mapping ───────────────────────────────────────────────────────────────

    private static bool WindowOpen(AwardCycle c, DateTime? opens, DateTime? closes, DateTime now)
        => c.Status == AwardCycleStatus.Published
           && opens != null && closes != null
           && now >= opens && now <= closes;

    private static string? DescribeWindows(AwardCycle c, DateTime now)
    {
        if (c.Status == AwardCycleStatus.Draft) return "This cycle is still a draft and is not open to anyone.";
        if (c.Status == AwardCycleStatus.Cancelled) return "This cycle was cancelled.";
        if (c.Status == AwardCycleStatus.Completed) return "This cycle is finished.";

        if (WindowOpen(c, c.NominationOpensOn, c.NominationClosesOn, now)) return null;
        if (WindowOpen(c, c.VotingOpensOn, c.VotingClosesOn, now)) return null;

        if (c.NominationOpensOn != null && now < c.NominationOpensOn)
            return $"Nominations open on {c.NominationOpensOn:yyyy-MM-dd HH:mm}.";
        if (c.VotingOpensOn != null && now < c.VotingOpensOn)
            return $"Voting opens on {c.VotingOpensOn:yyyy-MM-dd HH:mm}.";
        if (c.VotingClosesOn != null && now > c.VotingClosesOn)
            return $"Voting closed on {c.VotingClosesOn:yyyy-MM-dd HH:mm}.";
        if (c.NominationClosesOn != null && now > c.NominationClosesOn)
            return $"Nominations closed on {c.NominationClosesOn:yyyy-MM-dd HH:mm}.";

        return "This cycle has no open window at the moment.";
    }

    private AwardCycleDto ToDto(AwardCycle c, AwardType awardType, int nominationCount)
    {
        var now = DateTime.UtcNow;
        return new AwardCycleDto
        {
            Id = c.Id,
            TenantId = c.TenantId,
            CycleCode = c.CycleCode,
            Name = c.Name,
            AwardTypeId = c.AwardTypeId,
            AwardTypeName = awardType.Name,
            WinnerDecision = awardType.WinnerDecision,
            NominationSource = awardType.NominationSource,
            Year = c.Year,
            Quarter = c.Quarter,
            Month = c.Month,
            NominationOpensOn = c.NominationOpensOn,
            NominationClosesOn = c.NominationClosesOn,
            VotingOpensOn = c.VotingOpensOn,
            VotingClosesOn = c.VotingClosesOn,
            Status = c.Status,
            IsNominationOpen = WindowOpen(c, c.NominationOpensOn, c.NominationClosesOn, now),
            IsVotingOpen = WindowOpen(c, c.VotingOpensOn, c.VotingClosesOn, now),
            WindowState = DescribeWindows(c, now),
            NominationCount = nominationCount,
            Notes = c.Notes,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }

    private async Task<AwardCycleDto> ToDtoAsync(AwardCycle c)
    {
        var awardType = await GetAwardTypeAsync(c.AwardTypeId);
        var counts = await _nominationRepo.GetCountsByCycleAsync(c.TenantId);
        return ToDto(c, awardType, counts.TryGetValue(c.Id, out var n) ? n : 0);
    }

    // ── reads ─────────────────────────────────────────────────────────────────

    public async Task<AwardCycleDto?> GetByIdAsync(Guid id)
    {
        var entity = await _cycleRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId()) return null;
        return await ToDtoAsync(entity);
    }

    public async Task<IEnumerable<AwardCycleSummaryDto>> GetByAwardTypeIdAsync(Guid awardTypeId)
    {
        var cycles = (await _cycleRepo.GetByAwardTypeIdAsync(GetTenantId(), awardTypeId)).ToList();
        return await ToSummariesAsync(cycles);
    }

    public async Task<IEnumerable<AwardCycleSummaryDto>> GetOpenForNominationAsync()
    {
        var now = DateTime.UtcNow;
        var published = await _cycleRepo.GetPublishedAsync(GetTenantId());
        var cycles = published.Where(c => WindowOpen(c, c.NominationOpensOn, c.NominationClosesOn, now)).ToList();
        return await ToSummariesAsync(cycles);
    }

    public async Task<IEnumerable<AwardCycleSummaryDto>> GetOpenForVotingAsync()
    {
        var now = DateTime.UtcNow;
        var published = await _cycleRepo.GetPublishedAsync(GetTenantId());
        var cycles = published.Where(c => WindowOpen(c, c.VotingOpensOn, c.VotingClosesOn, now)).ToList();
        return await ToSummariesAsync(cycles);
    }

    private async Task<List<AwardCycleSummaryDto>> ToSummariesAsync(List<AwardCycle> cycles)
    {
        var now = DateTime.UtcNow;
        var tenantId = GetTenantId();

        // One query for the counts and one for the award types, not one of each per row — a cycle
        // register lists every run of every award, so the per-row shape scales with the register.
        var counts = await _nominationRepo.GetCountsByCycleAsync(tenantId);
        var awardTypes = (await _awardTypeRepo.GetByTenantAsync(tenantId)).ToDictionary(t => t.Id);

        var summaries = new List<AwardCycleSummaryDto>();

        foreach (var c in cycles)
        {
            awardTypes.TryGetValue(c.AwardTypeId, out var awardType);
            summaries.Add(new AwardCycleSummaryDto
            {
                Id = c.Id,
                CycleCode = c.CycleCode,
                Name = c.Name,
                AwardTypeId = c.AwardTypeId,
                AwardTypeName = awardType?.Name ?? string.Empty,
                Year = c.Year,
                Quarter = c.Quarter,
                Month = c.Month,
                Status = c.Status,
                IsNominationOpen = WindowOpen(c, c.NominationOpensOn, c.NominationClosesOn, now),
                IsVotingOpen = WindowOpen(c, c.VotingOpensOn, c.VotingClosesOn, now),
                NominationCount = counts.TryGetValue(c.Id, out var n) ? n : 0
            });
        }

        return summaries.OrderByDescending(s => s.Year).ThenBy(s => s.Name).ToList();
    }

    // ── writes ────────────────────────────────────────────────────────────────

    public async Task<AwardCycleDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardCycleDto dto)
    {
        tenantId = GetTenantId();
        var awardType = await GetAwardTypeAsync(dto.AwardTypeId);
        ValidateWindows(awardType, dto.NominationOpensOn, dto.NominationClosesOn, dto.VotingOpensOn, dto.VotingClosesOn);

        if (!string.IsNullOrWhiteSpace(dto.CycleCode)
            && await _cycleRepo.CodeExistsAsync(tenantId, dto.CycleCode))
        {
            throw AwardsWorkflowException.Conflict($"A cycle with the code '{dto.CycleCode}' already exists.");
        }

        var entity = new AwardCycle
        {
            TenantId = tenantId,
            CycleCode = string.IsNullOrWhiteSpace(dto.CycleCode)
                ? $"CYC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}"
                : dto.CycleCode,
            Name = dto.Name,
            AwardTypeId = dto.AwardTypeId,
            Year = dto.Year,
            Quarter = dto.Quarter,
            Month = dto.Month,
            NominationOpensOn = dto.NominationOpensOn,
            NominationClosesOn = dto.NominationClosesOn,
            VotingOpensOn = dto.VotingOpensOn,
            VotingClosesOn = dto.VotingClosesOn,
            Status = AwardCycleStatus.Draft,
            Notes = dto.Notes,
            CreatedBy = userId.ToString()
        };

        await _cycleRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(entity, awardType, 0);
    }

    public async Task<AwardCycleDto> UpdateAsync(Guid id, Guid userId, UpdateAwardCycleDto dto)
    {
        var entity = await GetOwnedAsync(id);
        var awardType = await GetAwardTypeAsync(entity.AwardTypeId);

        if (entity.Status is AwardCycleStatus.Completed or AwardCycleStatus.Cancelled)
            throw AwardsWorkflowException.InvalidState(
                $"This cycle is {entity.Status} and can no longer be edited.");

        ValidateWindows(awardType, dto.NominationOpensOn, dto.NominationClosesOn, dto.VotingOpensOn, dto.VotingClosesOn);

        entity.Name = dto.Name;
        entity.Year = dto.Year;
        entity.Quarter = dto.Quarter;
        entity.Month = dto.Month;
        entity.NominationOpensOn = dto.NominationOpensOn;
        entity.NominationClosesOn = dto.NominationClosesOn;
        entity.VotingOpensOn = dto.VotingOpensOn;
        entity.VotingClosesOn = dto.VotingClosesOn;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _cycleRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await ToDtoAsync(entity);
    }

    public async Task<AwardCycleDto> PublishAsync(Guid id, Guid userId)
    {
        var entity = await GetOwnedAsync(id);
        if (entity.Status != AwardCycleStatus.Draft)
            throw AwardsWorkflowException.InvalidState(
                $"Only a draft cycle can be published; this one is {entity.Status}.");

        // Re-check on the way out of draft. The award type's selection model can have been edited
        // since the cycle was drafted, which would leave a published cycle describing a stage the
        // award no longer has.
        var awardType = await GetAwardTypeAsync(entity.AwardTypeId);
        ValidateWindows(awardType, entity.NominationOpensOn, entity.NominationClosesOn,
            entity.VotingOpensOn, entity.VotingClosesOn);

        entity.Status = AwardCycleStatus.Published;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _cycleRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await ToDtoAsync(entity);
    }

    public async Task<AwardCycleDto> CancelAsync(Guid id, Guid userId, string reason)
    {
        var entity = await GetOwnedAsync(id);
        if (entity.Status == AwardCycleStatus.Completed)
            throw AwardsWorkflowException.InvalidState("A completed cycle cannot be cancelled.");
        if (entity.Status == AwardCycleStatus.Cancelled)
            throw AwardsWorkflowException.InvalidState("This cycle is already cancelled.");

        if (string.IsNullOrWhiteSpace(reason))
            throw AwardsWorkflowException.Invalid("Cancelling a cycle requires a reason.");

        entity.Status = AwardCycleStatus.Cancelled;
        entity.Notes = string.IsNullOrWhiteSpace(entity.Notes)
            ? $"Cancelled: {reason}"
            : $"{entity.Notes}\nCancelled: {reason}";
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _cycleRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await ToDtoAsync(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedAsync(id);

        var counts = await _nominationRepo.GetCountsByCycleAsync(entity.TenantId);
        var nominations = counts.TryGetValue(id, out var n) ? n : 0;
        if (nominations > 0)
            throw AwardsWorkflowException.InvalidState(
                $"This cycle has {nominations} nomination(s) against it and cannot be deleted. Cancel it instead.");

        await _cycleRepo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }
}

/// <summary>Who qualifies for an award, and why anyone else does not.</summary>
public class AwardEligibilityService : IAwardEligibilityService
{
    private readonly IAwardEligibilityEvaluator _evaluator;
    private readonly IAwardTypeRepository _awardTypeRepo;
    private readonly ICurrentUserProvider _currentUserProvider;

    public AwardEligibilityService(
        IAwardEligibilityEvaluator evaluator,
        IAwardTypeRepository awardTypeRepo,
        ICurrentUserProvider currentUserProvider)
    {
        _evaluator = evaluator;
        _awardTypeRepo = awardTypeRepo;
        _currentUserProvider = currentUserProvider;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>
    /// Who qualifies for an award, one page at a time (D-9).
    /// </summary>
    /// <remarks>
    /// <para><b>The counts are always over everybody</b>, never over the page or the filter. A screen
    /// showing "12 eligible" beside a filtered page of 12 rows would be telling the reader nothing;
    /// the useful sentence is "12 of 5,579", and it has to survive whatever filter is applied.</para>
    ///
    /// <para><b>Eligible first, then ineligible</b>, each by name. The reader is looking for who
    /// qualifies; making them page past four thousand refusals to find twelve names would be a
    /// strange way to answer the question they asked.</para>
    /// </remarks>
    public async Task<AwardEligibilityResultDto> EvaluateAsync(
        Guid awardTypeId, DateTime? asOf, string? filter = null, int page = 1, int pageSize = 50,
        string? search = null)
    {
        var tenantId = GetTenantId();
        var awardType = await _awardTypeRepo.GetByIdAsync(awardTypeId);
        if (awardType == null || awardType.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardType {awardTypeId} not found.");

        var result = await _evaluator.EvaluateAsync(awardTypeId, tenantId, asOf ?? DateTime.UtcNow);

        // An unrecognised filter widens rather than fails: the verdict is on every row, so a reader
        // can see at a glance that nothing was filtered.
        var normalised = (filter ?? "all").Trim().ToLowerInvariant();
        if (normalised is not ("eligible" or "ineligible")) normalised = "all";

        var selected = normalised switch
        {
            "eligible" => result.Eligible,
            "ineligible" => result.Ineligible,
            _ => result.Eligible.Concat(result.Ineligible).ToList(),
        };

        selected = Search(selected, search);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var totalItems = selected.Count;
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        return new AwardEligibilityResultDto
        {
            AwardTypeId = awardTypeId,
            AwardTypeName = awardType.Name,
            AsOf = result.AsOf,
            ConsideredCount = result.ConsideredCount,
            EligibleCount = result.Eligible.Count,
            IneligibleCount = result.Ineligible.Count,
            Filter = normalised,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalItems,
            TotalPages = totalPages,
            HasNext = page < totalPages,
            HasPrevious = page > 1 && totalPages > 0,
            Items = selected.Skip((page - 1) * pageSize).Take(pageSize).Select(Map).ToList(),
        };
    }

    /// <summary>
    /// Who an employee may put forward, paged and searchable.
    /// </summary>
    /// <remarks>
    /// <para><b>Only the qualified, and never the reasons.</b> The awards desk sees the ineligible
    /// names and why each failed — that is how HR checks its own criteria — but an employee choosing
    /// somebody to nominate has no business reading why a colleague failed a rule. This returns a
    /// plain paged list rather than the desk's result, so there is no ineligible count on it to leak
    /// by accident.</para>
    ///
    /// <para><b>Paged and searchable because of who calls it.</b> Every employee hits this the moment
    /// they open the nominate form, and on the live tenant the qualified set can be most of 5,579
    /// people. Nobody scrolls five thousand names to find a colleague — they type one. Returning the
    /// whole list would be the same unbounded payload D-9 was raised about, on the one endpoint the
    /// entire workforce touches.</para>
    /// </remarks>
    public async Task<PagedResult<AwardEligibilityVerdictDto>> GetCandidatesAsync(
        Guid awardTypeId, string? search = null, int page = 1, int pageSize = 25)
    {
        var tenantId = GetTenantId();
        var awardType = await _awardTypeRepo.GetByIdAsync(awardTypeId);
        if (awardType == null || awardType.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardType {awardTypeId} not found.");

        var result = await _evaluator.EvaluateAsync(awardTypeId, tenantId, DateTime.UtcNow);
        var matches = Search(result.Eligible, search);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        return new PagedResult<AwardEligibilityVerdictDto>
        {
            TotalCount = matches.Count,
            Page = page,
            PageSize = pageSize,
            Items = matches.Skip((page - 1) * pageSize).Take(pageSize).Select(Map).ToList(),
        };
    }

    /// <summary>Name or employee-number match. Blank means everybody.</summary>
    private static List<AwardEligibilityVerdict> Search(List<AwardEligibilityVerdict> source, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return source;
        var term = search.Trim();
        return source
            .Where(v => v.EmployeeName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (v.EmployeeNumber ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<AwardEligibilityVerdictDto> EvaluateEmployeeAsync(Guid awardTypeId, Guid employeeId, DateTime? asOf)
    {
        var tenantId = GetTenantId();
        var awardType = await _awardTypeRepo.GetByIdAsync(awardTypeId);
        if (awardType == null || awardType.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardType {awardTypeId} not found.");

        return Map(await _evaluator.EvaluateEmployeeAsync(awardTypeId, employeeId, tenantId, asOf ?? DateTime.UtcNow));
    }

    private static AwardEligibilityVerdictDto Map(AwardEligibilityVerdict v) => new()
    {
        EmployeeId = v.EmployeeId,
        EmployeeName = v.EmployeeName,
        EmployeeNumber = v.EmployeeNumber,
        IsEligible = v.IsEligible,
        Reasons = v.Reasons
    };
}
