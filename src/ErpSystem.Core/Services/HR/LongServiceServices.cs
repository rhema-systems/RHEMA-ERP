using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;

namespace ErpSystem.Core.Services.HR;

#region The ladder

/// <summary>
/// The rungs of a long-service ladder: what years earn an award, and what each one carries (AWD-14).
/// </summary>
/// <remarks>
/// <para>TDC's note asks us to <i>"define the basis for the long service awards"</i>. That is an
/// instruction to build the mechanism, not a statement of the policy, so the policy lives in rows
/// that HR owns. Decision <b>D-8</b>: configurable, seeded at 10 / 15 / 20 / 25 / 30.</para>
/// </remarks>
public class LongServiceMilestoneService : ILongServiceMilestoneService
{
    /// <summary>
    /// The ladder seeded when nothing else says otherwise (D-8).
    /// </summary>
    /// <remarks>
    /// <para>⚠ <c>CompanyHrPolicy.LongServiceMilestoneYears</c> is a different list — 5/10/15/20/25
    /// by default — and it is <b>not</b> read as an override here, for a reason worth stating.</para>
    ///
    /// <para><c>ICompanyHrPolicyProvider</c> returns a coded-defaults instance when a tenant has
    /// never opened the settings page, so its value cannot be told apart from a value HR actually
    /// chose. Treating it as authoritative would mean every fresh tenant silently got 5/10/15/20/25
    /// instead of the ladder that was decided, and nothing would show that a default had beaten a
    /// decision.</para>
    ///
    /// <para>They are also not the same fact. The policy list is the company's tenant-wide notion of
    /// service milestones, shared with succession; this is the ladder of <i>one award</i>. A seed run
    /// therefore <b>reports</b> the policy list alongside its own so a screen can offer it in one
    /// click, and honours it only when the caller passes it in explicitly.</para>
    /// </remarks>
    public static readonly int[] DefaultLadder = { 10, 15, 20, 25, 30 };

    private readonly ILongServiceMilestoneRepository _repo;
    private readonly IAwardTypeRepository _typeRepo;
    private readonly ICompanyHrPolicyProvider _policyProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public LongServiceMilestoneService(
        ILongServiceMilestoneRepository repo,
        IAwardTypeRepository typeRepo,
        ICompanyHrPolicyProvider policyProvider,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _typeRepo = typeRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _policyProvider = policyProvider;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private async Task<AwardType> GetAwardTypeAsync(Guid awardTypeId)
    {
        var type = await _typeRepo.GetByIdAsync(awardTypeId);
        if (type == null || type.TenantId != GetTenantId() || type.IsDeleted)
            throw AwardsWorkflowException.NotFound($"AwardType {awardTypeId} not found.");
        return type;
    }

    private async Task<LongServiceMilestone> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AwardsWorkflowException.NotFound($"LongServiceMilestone {id} not found.");
        return entity;
    }

    public async Task<IEnumerable<LongServiceMilestoneDto>> GetLadderAsync(Guid awardTypeId)
    {
        await GetAwardTypeAsync(awardTypeId);
        var rungs = await _repo.GetByAwardTypeIdAsync(GetTenantId(), awardTypeId);
        return rungs.Select(r => r.ToDto()).ToList();
    }

    public async Task<LongServiceMilestoneDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<LongServiceMilestoneDto> CreateAsync(Guid userId, CreateLongServiceMilestoneDto dto)
    {
        var tenantId = GetTenantId();
        await GetAwardTypeAsync(dto.AwardTypeId);

        // A unique index backs this, but a 409 with a sentence is a better answer than a 500 with a
        // constraint name — the same reasoning as every other duplicate guard in this area.
        var clash = await _repo.GetByYearsAsync(tenantId, dto.AwardTypeId, dto.Years);
        if (clash != null)
            throw AwardsWorkflowException.Conflict(
                $"This award already has a {dto.Years}-year milestone. Edit that rung rather than adding a second one.");

        var entity = new LongServiceMilestone
        {
            TenantId = tenantId,
            AwardTypeId = dto.AwardTypeId,
            Years = dto.Years,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name.Trim(),
            MonetaryAmount = dto.MonetaryAmount,
            LeaveDaysBonus = dto.LeaveDaysBonus,
            Benefits = string.IsNullOrWhiteSpace(dto.Benefits) ? null : dto.Benefits.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _repo.GetByIdAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<LongServiceMilestoneDto> UpdateAsync(Guid id, Guid userId, UpdateLongServiceMilestoneDto dto)
    {
        var entity = await GetOwnedAsync(id);

        // Moving a rung to a year another rung already occupies is the same clash as creating one.
        if (dto.Years != entity.Years)
        {
            var clash = await _repo.GetByYearsAsync(entity.TenantId, entity.AwardTypeId, dto.Years);
            if (clash != null && clash.Id != entity.Id)
                throw AwardsWorkflowException.Conflict(
                    $"This award already has a {dto.Years}-year milestone.");
        }

        entity.Years = dto.Years;
        entity.Name = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name.Trim();
        entity.MonetaryAmount = dto.MonetaryAmount;
        entity.LeaveDaysBonus = dto.LeaveDaysBonus;
        entity.Benefits = string.IsNullOrWhiteSpace(dto.Benefits) ? null : dto.Benefits.Trim();
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _repo.GetByIdAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetOwnedAsync(id);
        await _repo.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Fill in the rungs this award does not have yet.
    /// </summary>
    /// <remarks>
    /// <para><b>Where the years come from.</b> The caller's list if it supplies one, otherwise
    /// <see cref="DefaultLadder"/>. The company-wide list is returned alongside as
    /// <c>CompanyPolicyYears</c> — see <see cref="DefaultLadder"/> for why it is offered rather than
    /// applied.</para>
    ///
    /// <para><b>It never overwrites.</b> A rung HR has already priced survives a second press of the
    /// button — an existing year is reported as already present, not reset to an empty default. The
    /// result names both lists so the caller can see what the run actually did rather than infer it
    /// from a count.</para>
    ///
    /// <para><b>Only the years are seeded.</b> Money, leave days and benefits are left empty on
    /// purpose: TDC has not said what a twenty-year award is worth, and a seeded figure would look
    /// like an approved one. See <c>docs/HR/programme/HR-OPEN-QUESTIONS-FOR-TDC.md</c>.</para>
    /// </remarks>
    public async Task<LongServiceLadderSeedResultDto> SeedDefaultLadderAsync(
        Guid awardTypeId, Guid userId, IReadOnlyList<int>? years = null)
    {
        var tenantId = GetTenantId();
        await GetAwardTypeAsync(awardTypeId);

        var supplied = years == null ? new List<int>() : Clean(years);
        var source = supplied.Count > 0 ? "Supplied by the caller" : "Default (D-8)";
        var ladder = supplied.Count > 0 ? supplied : DefaultLadder.ToList();

        var existing = (await _repo.GetByAwardTypeIdAsync(tenantId, awardTypeId))
            .Select(m => m.Years)
            .ToHashSet();

        var result = new LongServiceLadderSeedResultDto
        {
            AwardTypeId = awardTypeId,
            Source = source,
            CompanyPolicyYears = await ReadPolicyYearsAsync(),
        };

        foreach (var year in ladder)
        {
            if (existing.Contains(year))
            {
                result.AlreadyPresent.Add(year);
                continue;
            }

            await _repo.AddAsync(new LongServiceMilestone
            {
                TenantId = tenantId,
                AwardTypeId = awardTypeId,
                Years = year,
                Name = $"{year} Years of Service",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId.ToString(),
            });
            result.Created.Add(year);
        }

        if (result.Created.Count > 0)
            await _unitOfWork.SaveChangesAsync();

        return result;
    }

    /// <summary>
    /// The company-wide milestone list, reported so a screen can offer it — never applied silently.
    /// </summary>
    /// <remarks>
    /// The field is free text with no validation behind it, so it can hold anything. Junk entries
    /// are dropped rather than thrown on: a seed run that 500s because somebody typed "ten" into a
    /// settings box would be worse than one that reports the years it could read.
    /// </remarks>
    private async Task<List<int>> ReadPolicyYearsAsync()
    {
        var policy = await _policyProvider.GetAsync();
        var csv = policy.LongServiceMilestoneYears;

        if (string.IsNullOrWhiteSpace(csv))
            return new List<int>();

        return Clean(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var n) ? n : 0));
    }

    /// <summary>Positive, plausible, distinct, ordered — the same filter wherever years come from.</summary>
    private static List<int> Clean(IEnumerable<int> years) =>
        years.Where(n => n is > 0 and <= 100).Distinct().OrderBy(n => n).ToList();
}

#endregion

#region The sweep

/// <summary>
/// Grants the long-service awards that have fallen due (AWD-14, AWD-15).
/// </summary>
/// <remarks>
/// <para><b>Preview and run are the same calculation.</b> Both call the one evaluator; the only
/// difference is whether the result is written. A preview computed one way and a run computed
/// another is how a screen comes to promise something the button does not then do — the shape area
/// 9b's reminder sweep and area 8's establishment check both settled this way.</para>
///
/// <para><b>The run is HR pressing a button, not a background job.</b> Granting an award is a
/// decision, and nothing in TDC's note asks for it to happen unattended. It also means the preview
/// somebody looked at is the state the run acts on.</para>
///
/// <para>⚠ <b>What a live run will find.</b> Measured 2026-08-21, exactly one employee on the live
/// tenant has ten completed years and none has fifteen, so the rungs above ten have no live subjects
/// at all. The engine is therefore proved against fixtures and its live behaviour asserted to be the
/// small number it truthfully is — the same position as retirement at 60 in area 9b.</para>
/// </remarks>
public class LongServiceSweepService : ILongServiceSweepService
{
    private readonly ILongServiceSweepEvaluator _evaluator;
    private readonly ILongServiceMilestoneRepository _milestoneRepo;
    private readonly ILongServiceAwardRepository _awardRepo;
    private readonly IAwardTypeRepository _typeRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public LongServiceSweepService(
        ILongServiceSweepEvaluator evaluator,
        ILongServiceMilestoneRepository milestoneRepo,
        ILongServiceAwardRepository awardRepo,
        IAwardTypeRepository typeRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _evaluator = evaluator;
        _milestoneRepo = milestoneRepo;
        _awardRepo = awardRepo;
        _typeRepo = typeRepo;
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

    public Task<LongServiceSweepResultDto> PreviewAsync(Guid awardTypeId, DateTime? asOf = null)
        => SweepAsync(awardTypeId, Guid.Empty, asOf, commit: false);

    public Task<LongServiceSweepResultDto> RunAsync(Guid awardTypeId, Guid userId, DateTime? asOf = null)
        => SweepAsync(awardTypeId, userId, asOf, commit: true);

    private async Task<LongServiceSweepResultDto> SweepAsync(
        Guid awardTypeId, Guid userId, DateTime? asOf, bool commit)
    {
        var tenantId = GetTenantId();

        var awardType = await _typeRepo.GetByIdAsync(awardTypeId);
        if (awardType == null || awardType.TenantId != tenantId || awardType.IsDeleted)
            throw AwardsWorkflowException.NotFound($"AwardType {awardTypeId} not found.");

        // ⚠ An inactive award type is a refusal, not an empty result. A sweep that quietly granted
        // nothing because the type had been retired looks identical to a sweep of a workforce nobody
        // qualified in, and the two call for opposite responses.
        if (commit && !awardType.IsActive)
            throw AwardsWorkflowException.InvalidState(
                "This award type is not active, so no awards can be granted against it.");

        var ladder = (await _milestoneRepo.GetByAwardTypeIdAsync(tenantId, awardTypeId)).ToList();

        // The evaluator stamps the date it worked to, so a caller can replay the same sweep. The
        // `asOf` seam is the one area 9 slice 8 added for exactly this reason: a sweep whose only
        // clock is DateTime.UtcNow cannot be tested at a milestone it has not reached yet.
        var effectiveAsOf = asOf ?? DateTime.UtcNow;

        var verdicts = await _evaluator.EvaluateAsync(awardType, ladder, tenantId, effectiveAsOf);

        // The sweep's own slice of the one calculation. The report reads the same verdicts and takes
        // a different slice; neither recomputes anything.
        var qualified = verdicts.Where(v => v.Standing == LongServiceStanding.Eligible).ToList();

        var dto = new LongServiceSweepResultDto
        {
            AwardTypeId = awardTypeId,
            AsOf = effectiveAsOf,
            Committed = commit,
            MilestonesConfigured = ladder.Count(m => m.IsActive),
            EmployeesConsidered = verdicts.Count,
            WithoutEmploymentDate = verdicts.Count(v => v.Standing == LongServiceStanding.ServiceUnknown),
            DisciplinaryCheckApplied = awardType.DisqualifyOnDisciplinaryRecord,
            Qualified = qualified.Select(ToDto).ToList(),
            Disqualified = verdicts.Where(v => v.Standing == LongServiceStanding.Exempt).Select(ToDto).ToList(),
        };

        if (!commit)
            return dto;

        var rungs = ladder.ToDictionary(m => m.Id);

        foreach (var candidate in qualified)
        {
            var rung = rungs[candidate.MilestoneId!.Value];

            await _awardRepo.AddAsync(new LongServiceAward
            {
                TenantId = tenantId,
                EmployeeId = candidate.EmployeeId,
                AwardTypeId = awardTypeId,
                YearsOfService = candidate.MilestoneYears!.Value,

                // The rung reached, not the years served: an employee swept at 22 years reaches the
                // 20-year milestone, and the award is for that milestone. YearsOfService on the
                // award is what was granted, which is what a certificate has to print.
                ServiceStartDate = candidate.ServiceStartDate!.Value.ToDateTime(TimeOnly.MinValue),
                MilestoneDate = candidate.ServiceStartDate!.Value
                    .AddYears(candidate.MilestoneYears!.Value)
                    .ToDateTime(TimeOnly.MinValue),

                AwardDescription = string.IsNullOrWhiteSpace(rung.Name)
                    ? $"{candidate.MilestoneYears} years of service"
                    : rung.Name,

                // Carried from the rung, so what the ladder promises is what the award records. It
                // is copied rather than referenced because HR may reprice the rung later and an
                // award already granted must not silently change value.
                MonetaryAmount = rung.MonetaryAmount,
                LeaveDaysBonus = rung.LeaveDaysBonus,
                OtherBenefits = rung.Benefits,

                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId.ToString(),
            });

            dto.AwardsCreated++;
        }

        if (dto.AwardsCreated > 0)
            await _unitOfWork.SaveChangesAsync();

        return dto;
    }

    private static LongServiceCandidateDto ToDto(LongServiceVerdict v) => new()
    {
        EmployeeId = v.EmployeeId,
        EmployeeName = v.EmployeeName,
        EmployeeNumber = v.EmployeeNumber,
        YearsOfService = v.YearsOfService ?? 0,
        MilestoneYears = v.MilestoneYears ?? 0,
        MilestoneId = v.MilestoneId ?? Guid.Empty,
        ServiceStartDate = v.ServiceStartDate?.ToDateTime(TimeOnly.MinValue),
        Reason = v.Reason,
    };
}

#endregion
