using System.Text.Json;
using System.Text;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Recruitment;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// INTERVIEW QUESTION BANK SERVICE
// ============================================================================

public class JobInterviewQuestionBankService : IJobInterviewQuestionBankService
{
    private readonly IJobInterviewQuestionTypeRepository _questionTypeRepository;
    private readonly IJobInterviewQuestionDetailRepository _questionDetailRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobInterviewQuestionBankService> _logger;

    public JobInterviewQuestionBankService(
        IJobInterviewQuestionTypeRepository questionTypeRepository,
        IJobInterviewQuestionDetailRepository questionDetailRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<JobInterviewQuestionBankService> logger)
    {
        _questionTypeRepository = questionTypeRepository;
        _questionDetailRepository = questionDetailRepository;
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

    // ⚠ The question bank is recruitment setup data — the questions candidates will be asked and the
    // weights their answers are scored against — and is HR-only. That gate lives on
    // InterviewQuestionBankController rather than here: this service has exactly one controller and no
    // per-record rule to express, unlike JobInterviewService where a panelist's access depends on which
    // interview they sit on.

    // A question type owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<JobInterviewQuestionType> GetOwnedQuestionTypeAsync(Guid id)
    {
        var entity = await _questionTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interview question type with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewQuestionDetail> GetOwnedQuestionDetailAsync(Guid id)
    {
        var entity = await _questionDetailRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interview question detail with ID '{id}' not found.");
        return entity;
    }

    public async Task<JobInterviewQuestionTypeDto> GetQuestionTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionTypeAsync(id);
        return entity.ToDto();
    }

    /// <summary>
    /// Every question type in the bank, active or not.
    ///
    /// <para>This backs the question-bank admin list and used to call <c>GetActiveTypesAsync</c>, so a
    /// type someone had deactivated vanished from the only screen that could reactivate it — along with
    /// its questions, which remained attached to it. The interview-scheduling picker wants active types
    /// only and filters on <c>IsActive</c> for itself.</para>
    /// </summary>
    public async Task<IEnumerable<JobInterviewQuestionTypeSummaryDto>> GetAllQuestionTypesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _questionTypeRepository.FindAsync(t => !t.IsDeleted);
        return entities
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.TypeName)
            .Select(e => e.ToSummaryDto());
    }

    public async Task<JobInterviewQuestionTypeDto> CreateQuestionTypeAsync(CreateJobInterviewQuestionTypeDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = createDto.ToEntity(current, createdByUserId);
        await _questionTypeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<JobInterviewQuestionTypeDto> UpdateQuestionTypeAsync(UpdateJobInterviewQuestionTypeDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionTypeAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _questionTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteQuestionTypeAsync(Guid questionTypeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionTypeAsync(questionTypeId);

        await _questionTypeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobInterviewQuestionDetailDto> GetQuestionDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionDetailAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobInterviewQuestionDetailDto>> GetQuestionDetailsByTypeAsync(Guid questionTypeId, CancellationToken cancellationToken = default)
    {
        await GetOwnedQuestionTypeAsync(questionTypeId);
        var tenantId = GetTenantId();
        var entities = await _questionDetailRepository.GetByQuestionTypeIdAsync(questionTypeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewQuestionDetailDto>> GetAllQuestionDetailsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _questionDetailRepository.GetAllWithTypeAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<JobInterviewQuestionDetailDto> CreateQuestionDetailAsync(CreateJobInterviewQuestionDetailDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedQuestionTypeAsync(createDto.QuestionTypeId);

        var entity = createDto.ToEntity(current, createdByUserId);
        await _questionDetailRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<JobInterviewQuestionDetailDto> UpdateQuestionDetailAsync(UpdateJobInterviewQuestionDetailDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionDetailAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _questionDetailRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteQuestionDetailAsync(Guid questionDetailId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionDetailAsync(questionDetailId);

        await _questionDetailRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobInterviewQuestionTypeDto?> GetQuestionTypeByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _questionTypeRepository.GetByCodeAsync(code);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<JobInterviewQuestionTypeDto?> GetQuestionTypeWithQuestionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _questionTypeRepository.GetWithQuestionsAsync(id);
        return entity != null && entity.TenantId == GetTenantId() ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<JobInterviewQuestionDetailDto>> GetActiveQuestionsAsync(Guid? questionTypeId = null, CancellationToken cancellationToken = default)
    {
        if (questionTypeId.HasValue)
            await GetOwnedQuestionTypeAsync(questionTypeId.Value);

        var tenantId = GetTenantId();
        var entities = await _questionDetailRepository.GetActiveQuestionsAsync(questionTypeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }
}

// ============================================================================
// INTERVIEW SERVICE
// ============================================================================

public class JobInterviewService : IJobInterviewService
{
    private readonly IJobInterviewRepository _interviewRepository;
    private readonly IJobInterviewPanelistRepository _panelistRepository;
    private readonly IJobInterviewExternalPanelistRepository _externalPanelistRepository;
    private readonly IJobIntervieweeRepository _intervieweeRepository;
    private readonly IJobInterviewQuestionRepository _questionPlanRepository;
    private readonly IJobInterviewSelectedQuestionRepository _selectedQuestionRepository;
    private readonly IJobInterviewQuestionDetailRepository _questionDetailRepository;
    private readonly IJobInterviewQuestionTypeRepository _questionTypeRepository;
    private readonly IJobInterviewScoreSummaryRepository _scoreSummaryRepository;
    private readonly IJobInterviewScoreEntryRepository _scoreEntryRepository;
    private readonly IJobInterviewScoreDraftRepository _draftRepository;
    private readonly IInterviewQuestionPresetRepository _presetRepository;
    private readonly IApplicationPipelineService _pipelineService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobInterviewService> _logger;
    private readonly IEmailService _email;
    private readonly ITemplatedEmailService _templatedEmail;
    // Cross-domain repositories used only for the advisory panelist-availability conflict check
    // and application/vacancy validation. Open-generic IGenericRepository<T> is DI-registered.
    private readonly IGenericRepository<JobApplication> _applicationRepository;
    private readonly IGenericRepository<Entities.HR.Employee> _employeeRepository;
    private readonly IGenericRepository<Entities.HR.ExternalAssociate> _associateRepository;
    private readonly IGenericRepository<Entities.HR.StaffLeave.LeaveRequest> _leaveRepository;
    private readonly IGenericRepository<Entities.HR.StaffTravel.StaffTravelRequest> _travelRepository;

    /// <summary>
    /// Every place a panelist may already be committed (round 4, lane D1).
    /// </summary>
    /// <remarks>
    /// ⚠ Injected as the whole set. Adding a source is a class plus a registration; nothing here
    /// needs to change, and nothing here can quietly stop asking one. What CAN go wrong is a source
    /// that is written and never registered — it contributes nothing and the check answers "free" —
    /// which is why the result carries <c>SourcesConsulted</c>.
    /// </remarks>
    private readonly IReadOnlyList<IPanelistCommitmentSource> _commitmentSources;

    private readonly string _portalUrl;

    public JobInterviewService(
        IJobInterviewRepository interviewRepository,
        IJobInterviewPanelistRepository panelistRepository,
        IJobInterviewExternalPanelistRepository externalPanelistRepository,
        IJobIntervieweeRepository intervieweeRepository,
        IJobInterviewQuestionRepository questionPlanRepository,
        IJobInterviewSelectedQuestionRepository selectedQuestionRepository,
        IJobInterviewQuestionDetailRepository questionDetailRepository,
        IJobInterviewQuestionTypeRepository questionTypeRepository,
        IJobInterviewScoreSummaryRepository scoreSummaryRepository,
        IJobInterviewScoreEntryRepository scoreEntryRepository,
        IJobInterviewScoreDraftRepository draftRepository,
        IInterviewQuestionPresetRepository presetRepository,
        IApplicationPipelineService pipelineService,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<JobInterviewService> logger,
        IEmailService email,
        ITemplatedEmailService templatedEmail,
        IGenericRepository<JobApplication> applicationRepository,
        IGenericRepository<Entities.HR.Employee> employeeRepository,
        IGenericRepository<Entities.HR.ExternalAssociate> associateRepository,
        IGenericRepository<Entities.HR.StaffLeave.LeaveRequest> leaveRepository,
        IGenericRepository<Entities.HR.StaffTravel.StaffTravelRequest> travelRepository,
        IEnumerable<IPanelistCommitmentSource> commitmentSources,
        IConfiguration configuration)
    {
        _interviewRepository = interviewRepository;
        _panelistRepository = panelistRepository;
        _externalPanelistRepository = externalPanelistRepository;
        _intervieweeRepository = intervieweeRepository;
        _questionPlanRepository = questionPlanRepository;
        _selectedQuestionRepository = selectedQuestionRepository;
        _questionDetailRepository = questionDetailRepository;
        _questionTypeRepository = questionTypeRepository;
        _scoreSummaryRepository = scoreSummaryRepository;
        _scoreEntryRepository = scoreEntryRepository;
        _draftRepository = draftRepository;
        _presetRepository = presetRepository;
        _pipelineService = pipelineService;
        _currentUserProvider = currentUserProvider;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _commitmentSources = (commitmentSources ?? Array.Empty<IPanelistCommitmentSource>()).ToList();
        _logger = logger;
        _email = email;
        _templatedEmail = templatedEmail;
        _applicationRepository = applicationRepository;
        _employeeRepository = employeeRepository;
        _associateRepository = associateRepository;
        _leaveRepository = leaveRepository;
        _travelRepository = travelRepository;
        _portalUrl = (configuration["CandidatePortal:PortalUrl"] ?? "").TrimEnd('/');
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

    // An interview owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<JobInterview> GetOwnedInterviewAsync(Guid id)
    {
        var entity = await _interviewRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interview with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterview> GetOwnedInterviewWithDetailsAsync(Guid id)
    {
        var entity = await _interviewRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interview with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewPanelist> GetOwnedPanelistAsync(Guid id)
    {
        var entity = await _panelistRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Panelist with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewExternalPanelist> GetOwnedExternalPanelistAsync(Guid id)
    {
        var entity = await _externalPanelistRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"External panelist with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewee> GetOwnedIntervieweeAsync(Guid id)
    {
        var entity = await _intervieweeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Interviewee with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewQuestion> GetOwnedQuestionPlanAsync(Guid id)
    {
        var entity = await _questionPlanRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Question plan with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobInterviewScoreSummary> GetOwnedScoreSummaryAsync(Guid id)
    {
        var entity = await _scoreSummaryRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Score summary with ID '{id}' not found.");
        return entity;
    }

    // ── Entitlement ───────────────────────────────────────────────────────────
    //
    // An interview is not HR-only the way an application is (see JobApplicationController): the people
    // who have to open it, read the question plan and file a scorecard are ordinary employees sitting on
    // the panel. But it carries the candidate's name and email, the panel's private comments and the
    // hire/no-hire recommendation, so "any authenticated employee" — which is what the controller
    // previously allowed — is far too wide.
    //
    // The rule, applied in the service so it holds whichever route reaches it:
    //   • A recruitment desk user   — everything, including recording on an external panelist's behalf.
    //   • A panelist on THAT interview — read it, and score as themselves.
    //   • Everyone else             — 403.
    //
    // External panelists have no login at all; they confirm by emailed token and HR enters their scores,
    // which is why an external scorecard is an HR-only write.
    //
    // ⚠ 2026-09-15 (G-9.1): this used to read HasRole("HR") || HasRole("SuperAdmin"), and it was the
    // only place in recruitment that authorised on a role. Every other controller in the module gates
    // on HR.Recruitment.Read/Write/Admin, which SuperAdmin, TenantAdmin, Admin, HR *and* the legacy
    // "HR User" role all hold. So a TenantAdmin could raise a requisition, open a vacancy, shortlist
    // and reject — and then be refused when they tried to schedule the interview, or even to read one,
    // because EnsureCanReadInterviewAsync fell through to the panelist check. It now asks the same
    // question the rest of the module asks. The gate is unchanged in strength: no role gains
    // recruitment permissions here that it did not already hold everywhere else.

    /// <summary>
    /// The recruitment desk: anyone whose roles carry <c>HR.Recruitment.Write</c> or
    /// <c>HR.Recruitment.Admin</c>. Read alone is not enough — every caller of this holds an
    /// interview record open for writing, or is reading panel-private material.
    /// </summary>
    private bool IsHr =>
        _currentUserProvider.HasRole(Constants.Roles.SuperAdmin) ||
        HrPermissions.RolesGrantAny(
            _currentUserProvider.Roles,
            HrPermissions.MaintainRecruitment,
            HrPermissions.AdministerRecruitment);

    private Guid? CallerEmployeeId => _currentUser.EmployeeId;

    /// <summary>
    /// The caller's own panelist row on this interview, or null when they do not sit on it.
    /// </summary>
    private async Task<JobInterviewPanelist?> GetCallerPanelistAsync(Guid interviewId)
    {
        var employeeId = CallerEmployeeId;
        if (employeeId is null || employeeId == Guid.Empty) return null;

        var panelists = await _panelistRepository.GetByInterviewIdAsync(interviewId);
        return panelists.FirstOrDefault(p => p.EmployeeId == employeeId && p.TenantId == GetTenantId());
    }

    /// <summary>Read access: HR, or a panelist on this interview.</summary>
    private async Task EnsureCanReadInterviewAsync(Guid interviewId)
    {
        if (IsHr) return;
        if (await GetCallerPanelistAsync(interviewId) is not null) return;

        throw new UnauthorizedAccessException(
            "Only HR and members of this interview's panel can view it.");
    }

    /// <summary>Write access to the interview record itself (schedule, panel, questions): HR only.</summary>
    private void EnsureHr(string action)
    {
        if (IsHr) return;
        throw new UnauthorizedAccessException($"Only HR can {action}.");
    }

    /// <summary>
    /// Resolves which panelist the caller is allowed to file this scorecard as, and refuses anything else.
    /// The client used to name the panelist in the payload (or the query string, for drafts), so any
    /// authenticated user could submit — or read — a scorecard in someone else's name.
    /// </summary>
    /// <returns>
    /// How the resulting scorecard reached the system (round 4, lane F4) —
    /// <see cref="InterviewScoreSource.Online"/> when the caller <i>is</i> the panelist, and
    /// <see cref="InterviewScoreSource.PaperSheet"/> when somebody is filing on their behalf.
    /// </returns>
    /// <remarks>
    /// ⚠ <b>Provenance is decided here and nowhere else, and the client cannot influence it.</b>
    /// This method already knows the only fact that settles it — whether the caller is the panelist
    /// — so the answer comes back with the authorization rather than being asserted in the payload
    /// and trusted. A card HR types in is a real card; it is just not one the panelist typed, and an
    /// audit trail that cannot tell the difference is not an audit trail.
    /// </remarks>
    private async Task<InterviewScoreSource> EnsureCanScoreAsAsync(
        Guid intervieweeId, Guid? internalPanelistId, Guid? externalPanelistId)
    {
        if (internalPanelistId is null && externalPanelistId is null)
            throw new InvalidOperationException("A scorecard must name the panelist it belongs to.");
        if (internalPanelistId is not null && externalPanelistId is not null)
            throw new InvalidOperationException("A scorecard belongs to one panelist, not both.");

        var interviewee = await GetOwnedIntervieweeAsync(intervieweeId);

        // The panelist must sit on the same interview as the candidate being scored — otherwise a
        // scorecard could be attributed to a panel the candidate never appeared before.
        if (internalPanelistId is not null)
        {
            var panelist = await GetOwnedPanelistAsync(internalPanelistId.Value);
            if (panelist.JobInterviewId != interviewee.JobInterviewId)
                throw new InvalidOperationException("That panelist does not sit on this candidate's interview.");

            // Checked BEFORE the HR branch: an HR user who also sits on this panel is filing their
            // own card, and stamping it "on behalf of" would be wrong about the one person it names.
            if (panelist.EmployeeId == CallerEmployeeId) return InterviewScoreSource.Online;
            if (IsHr) return InterviewScoreSource.PaperSheet;

            throw new UnauthorizedAccessException("You can only score as yourself.");
        }

        var external = await GetOwnedExternalPanelistAsync(externalPanelistId!.Value);
        if (external.JobInterviewId != interviewee.JobInterviewId)
            throw new InvalidOperationException("That panelist does not sit on this candidate's interview.");

        // External associates have no login, so only HR can record on their behalf — which makes
        // every external scorecard a filing on somebody's behalf, by construction.
        EnsureHr("record scores for an external panelist");
        return InterviewScoreSource.PaperSheet;
    }

    // ── Blind scoring (round 4, lane F5) ─────────────────────────────────────

    /// <summary>
    /// Narrows a candidate's scorecards to what the caller may see: their own always, everybody
    /// else's only once they have filed their own.
    /// </summary>
    /// <remarks>
    /// <para><b>Why.</b> Every score read on this controller was gated on <i>read</i> access — "HR,
    /// or a panelist on this interview" — so any panelist could open the Scores tab and read a
    /// colleague's totals, recommendation and private comments <i>before</i> filing their own. A
    /// panel member who looks first is anchored by whoever filed before them, which is the one
    /// thing a panel of independent assessors exists to avoid.</para>
    ///
    /// <para><b>⚠ The unit is the CANDIDATE, not the interview.</b> A panelist who has filed for
    /// Ada but not for Kwame sees the panel's cards for Ada and none for Kwame. Blinding per
    /// interview would either unblind Kwame the moment Ada was scored, or keep Ada blind until the
    /// whole day was done — and the anchoring risk is per person being judged.</para>
    ///
    /// <para><b>HR is never blinded</b>, because HR files on a panelist's behalf from a paper sheet
    /// and has to see what is already recorded to know whether they are correcting or duplicating.
    /// Nor is the panel blinded after the fact: once their own card is in, the full set opens, which
    /// is the calibration conversation this is meant to protect rather than prevent.</para>
    /// </remarks>
    private async Task<List<JobInterviewScoreSummary>> ApplyBlindScoringAsync(
        Guid interviewId, List<JobInterviewScoreSummary> cards)
    {
        if (IsHr) return cards;

        var seat = await GetCallerPanelistAsync(interviewId);
        // No seat and not HR means the read gate let an external or service caller through; there is
        // no "own card" to measure against, so nothing is narrowed here rather than silently emptied.
        if (seat is null) return cards;

        return cards.Any(c => c.InternalPanelistId == seat.Id)
            ? cards
            : cards.Where(c => c.InternalPanelistId == seat.Id).ToList();
    }

    /// <summary>
    /// The same rule for a card reached by its own id, where there is nothing to narrow — so it
    /// refuses instead.
    /// </summary>
    /// <remarks>
    /// ⚠ Without this, blinding the list read would be theatre: the ids are in the DOM of any screen
    /// that ever showed the list, and <c>score-summaries/{id}</c> and its <c>/entries</c> sibling
    /// would hand the card straight back.
    /// </remarks>
    private async Task EnsureCanSeeScoreCardAsync(Guid interviewId, JobInterviewScoreSummary card)
    {
        if (IsHr) return;

        var seat = await GetCallerPanelistAsync(interviewId);
        if (seat is null) return;
        if (card.InternalPanelistId == seat.Id) return;

        var tenantId = GetTenantId();
        var hasFiledOwn = (await _scoreSummaryRepository.GetByIntervieweeIdAsync(card.JobIntervieweeId))
            .Any(c => c.TenantId == tenantId && !c.IsDeleted && c.InternalPanelistId == seat.Id);

        if (!hasFiledOwn)
            throw new UnauthorizedAccessException(
                "Scoring is blind until you have filed your own scorecard for this candidate.");
    }

    private async Task<string> GenerateInterviewNumberAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // ⚠ Counts SOFT-DELETED rows too, via the including-deleted overload — plain GetQueryable()
        // filters them out. Excluding them makes the sequence reuse a number the moment anything is
        // deleted, and JobInterviews carries a UNIQUE index on (TenantId, InterviewNumber) that a soft delete does
        // not release: deleting one draft made the very next create die on a duplicate-key violation,
        // surfaced as a 500 with raw SQL in it. A reference number is an identifier, not a slot —
        // once issued it is spent.
        var last = await _interviewRepository.GetQueryableIncludingDeleted(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.InterviewNumber)
            .Select(i => i.InterviewNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (last != null && int.TryParse(last.Replace("INT-", ""), out var parsed))
            next = parsed + 1;

        return $"INT-{next:D6}";
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<JobInterviewDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(id);
        await EnsureCanReadInterviewAsync(id);
        return entity.ToDto();
    }

    public async Task<JobInterviewDto?> GetByInterviewNumberAsync(string interviewNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _interviewRepository.GetByInterviewNumberAsync(interviewNumber);
        if (entity == null || entity.TenantId != tenantId) return null;

        await EnsureCanReadInterviewAsync(entity.Id);
        return entity.ToDto();
    }

    public async Task<JobInterviewDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewWithDetailsAsync(id);
        await EnsureCanReadInterviewAsync(id);
        return entity.ToDetailDto();
    }

    // The four list reads below are HR's: they span interviews the caller may have nothing to do with,
    // and each row carries the candidate names. A panelist reaches their own sessions through
    // GetMyPanelistSlotsAsync instead.

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByVacancyIdAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        EnsureHr("list a vacancy's interviews");
        var tenantId = GetTenantId();
        var entities = await _interviewRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByStatusAsync(JobInterviewStatus status, CancellationToken cancellationToken = default)
    {
        EnsureHr("list interviews by status");
        var tenantId = GetTenantId();
        var entities = await _interviewRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        EnsureHr("list the interview schedule");
        var tenantId = GetTenantId();
        var entities = await _interviewRepository.GetByDateRangeAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to));
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobInterviewSummaryDto>> GetByRoundAsync(Guid vacancyId, int round, CancellationToken cancellationToken = default)
    {
        EnsureHr("list a round's interviews");
        var tenantId = GetTenantId();
        var entities = await _interviewRepository.GetByRoundAsync(vacancyId, round);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Availability / conflict detection ──────────────────────────────────────
    //
    // ⚠ Round 4, lane D1–D2. This used to know about exactly three things — other interviews, leave
    // and travel — hard-coded into one method, and it read the SESSION window rather than the
    // candidate's slot. It now fans out over every registered IPanelistCommitmentSource, and the
    // caller may narrow the window to one candidate's slot.
    //
    // ⚠ The old version also called GetByEmployeeIdAsync INSIDE the per-employee loop (§ 3 defect
    // 10) while leave and travel, three lines below it, were batch-loaded. Each source now issues
    // its own query for the whole panel.

    public async Task<PanelistAvailabilityCheckDto> CheckPanelistAvailabilityAsync(
        IReadOnlyList<Guid> panelistEmployeeIds, IReadOnlyList<Guid> externalAssociateIds,
        DateOnly date, TimeSpan start, TimeSpan end,
        Guid? excludeInterviewId, CancellationToken cancellationToken = default)
    {
        // This reads other employees' leave, travel, meetings and training to explain a clash.
        // That is HR's to see.
        EnsureHr("check panel availability");
        return await GatherAvailabilityAsync(
            panelistEmployeeIds, externalAssociateIds, date, start, end, excludeInterviewId, cancellationToken);
    }

    /// <summary>
    /// The check itself, without the HR gate — so the write paths can enforce it for a caller who
    /// is already past their own authorisation.
    /// </summary>
    private async Task<PanelistAvailabilityCheckDto> GatherAvailabilityAsync(
        IReadOnlyList<Guid> panelistEmployeeIds, IReadOnlyList<Guid> externalAssociateIds,
        DateOnly date, TimeSpan start, TimeSpan end,
        Guid? excludeInterviewId, CancellationToken cancellationToken)
    {
        var result = new PanelistAvailabilityCheckDto();

        var ids      = (panelistEmployeeIds ?? Array.Empty<Guid>()).Distinct().ToList();
        var assocIds = (externalAssociateIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (ids.Count == 0 && assocIds.Count == 0)
            return result;

        var tenantId = GetTenantId();
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var query = new PanelistCommitmentQuery(
            ids, assocIds, dayStart + start, dayStart + end, excludeInterviewId, tenantId);

        // ── Fan out ───────────────────────────────────────────────────────────
        //
        // ⚠ A source that throws must not take the whole check down with it, but it must not pass
        // silently either: a swallowed exception here means "everybody is free", which is the
        // answer that gets somebody double-booked. The source is dropped from SourcesConsulted, so
        // the caller can see which question went unanswered.
        var commitments = new List<PanelistCommitment>();
        foreach (var source in _commitmentSources)
        {
            try
            {
                commitments.AddRange(await source.GetCommitmentsAsync(query, cancellationToken));
                result.SourcesConsulted.Add(source.SourceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Panelist commitment source '{Source}' failed for {Date} {Start}–{End}. The availability "
                  + "check is INCOMPLETE and does not include it.",
                    source.SourceName, date, start, end);
            }
        }

        // ── Names ─────────────────────────────────────────────────────────────
        var nameById = new Dictionary<Guid, string>();
        if (ids.Count > 0)
        {
            foreach (var e in (await _employeeRepository.FindAsync(e => ids.Contains(e.Id)))
                         .Where(e => e.TenantId == tenantId))
                nameById[e.Id] = $"{e.FirstName} {e.LastName}".Trim();
        }
        if (assocIds.Count > 0)
        {
            foreach (var a in (await _associateRepository.FindAsync(a => assocIds.Contains(a.Id)))
                         .Where(a => a.TenantId == tenantId && !a.IsDeleted))
                nameById[a.Id] = $"{a.FirstName} {a.LastName}".Trim();
        }

        // ── One row per panelist, INCLUDING the ones with nothing ─────────────
        //
        // ⚠ The external half used to emit a row only where there was a conflict, so a panel of
        // three externals with one clash rendered as one row and the screen could not show that the
        // other two had been checked at all. "Checked and clear" and "not checked" must look
        // different.
        foreach (var (subjectId, isExternal) in
                 ids.Select(i => (i, false)).Concat(assocIds.Select(a => (a, true))))
        {
            var mine = commitments
                .Where(c => c.SubjectId == subjectId)
                .OrderByDescending(c => c.Hardness)
                .ThenBy(c => c.Start)
                .ToList();

            var row = new PanelistAvailabilityDto
            {
                EmployeeId   = subjectId,
                EmployeeName = nameById.TryGetValue(subjectId, out var n) && !string.IsNullOrWhiteSpace(n)
                    ? n
                    : isExternal ? "External panelist" : "Panelist",
                IsExternal       = isExternal,
                HasConflicts     = mine.Count > 0,
                HasHardConflicts = mine.Any(c => c.Hardness == CommitmentHardness.Hard),
                Commitments      = mine.Select(c => new PanelistCommitmentDto
                {
                    Kind          = c.Kind,
                    Hardness      = c.Hardness,
                    Label         = c.Label,
                    Start         = c.Start,
                    End           = c.End,
                    IsDayGranular = c.IsDayGranular,
                    Reference     = c.Reference,
                }).ToList(),
            };
            result.Panelists.Add(row);
        }

        result.HasConflicts     = result.Panelists.Any(p => p.HasConflicts);
        result.HasHardConflicts = result.Panelists.Any(p => p.HasHardConflicts);
        return result;
    }

    /// <summary>
    /// The clash check made binding (round 4, D3): a HARD clash refuses the write unless the caller
    /// supplies an override reason, which is recorded on the interview.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Before this, <c>CheckPanelistAvailabilityAsync</c> was advisory and was called from
    /// <b>nothing but its own endpoint</b> (§ 3 defect 7). A recruiter who never opened the
    /// availability panel — or who opened it, saw a clash and pressed Save anyway — scheduled the
    /// double-booking with no record that anyone had been warned.</para>
    ///
    /// <para>Soft clashes never refuse. They are day-granular or unconfirmed evidence, and the
    /// system overruling a recruiter on that basis would be worse than saying nothing. This is the
    /// <c>RoomBooking</c> rule — which already refuses a double booking — applied to people, with
    /// the override the room rule lacks.</para>
    /// </remarks>
    /// <returns>The clash detail, so the caller can record what was overridden.</returns>
    private async Task<PanelistAvailabilityCheckDto> EnsurePanelIsFreeAsync(
        IReadOnlyList<Guid> panelistEmployeeIds, IReadOnlyList<Guid> externalAssociateIds,
        DateOnly date, TimeSpan start, TimeSpan end, Guid? excludeInterviewId,
        string? overrideReason, CancellationToken cancellationToken)
    {
        var availability = await GatherAvailabilityAsync(
            panelistEmployeeIds, externalAssociateIds, date, start, end, excludeInterviewId, cancellationToken);

        if (!availability.HasHardConflicts) return availability;
        if (!string.IsNullOrWhiteSpace(overrideReason)) return availability;

        var blocked = availability.Panelists.Where(p => p.HasHardConflicts).ToList();
        var detail = string.Join("; ", blocked.Select(p =>
        {
            var worst = p.Commitments.First(c => c.Hardness == CommitmentHardness.Hard);
            return $"{p.EmployeeName} — {worst.Label}"
                 + (worst.IsDayGranular ? "" : $" ({worst.Start:HH:mm}–{worst.End:HH:mm})");
        }));

        throw new InvalidOperationException(
            $"{blocked.Count} panelist(s) are already committed at that time: {detail}. "
          + "Pick another time, drop them from the panel, or supply a reason to schedule anyway "
          + "(panelClashOverrideReason) — the reason is recorded on the interview.");
    }

    /// <summary>
    /// Records that a hard clash was scheduled over, and who decided to (round 4, D3).
    /// </summary>
    /// <remarks>
    /// ⚠ Written only when there was actually something to override. A reason typed into the box on
    /// an interview with no clash is discarded rather than stored: a record saying somebody
    /// overrode a clash that never existed is worse than no record.
    /// </remarks>
    private static void RecordClashOverride(
        JobInterview entity, PanelistAvailabilityCheckDto availability, string? reason, Guid actingUserId)
    {
        if (!availability.HasHardConflicts || string.IsNullOrWhiteSpace(reason)) return;

        var summary = string.Join("; ", availability.Panelists
            .Where(p => p.HasHardConflicts)
            .Select(p => $"{p.EmployeeName}: {p.Commitments.First(c => c.Hardness == CommitmentHardness.Hard).Label}"));

        entity.PanelClashOverrideReason   = reason.Trim();
        entity.PanelClashOverriddenById   = actingUserId;
        entity.PanelClashOverriddenAt     = DateTime.UtcNow;
        entity.PanelClashOverrideDetail   = summary.Length <= 2000 ? summary : summary[..2000];
    }

    /// <summary>
    /// Round 4, D8 — the room an interview holds must exist, belong to this tenant, be live, and
    /// actually cover the interview's window.
    /// </summary>
    /// <remarks>
    /// <para>⚠ A booking that does not span the interview is refused rather than silently accepted.
    /// The whole point of holding a room instead of typing its name into <c>LocationOrLink</c> is
    /// that the hold is real; a booking for 09:00–10:00 attached to an interview running until noon
    /// is the free-text problem again, wearing a foreign key.</para>
    ///
    /// <para>The booking is NOT created here. It is made through the meeting-room register, whose
    /// own blocking double-booking check is what makes the hold worth having — recreating that rule
    /// here would be the second copy this module keeps learning not to write.</para>
    /// </remarks>
    private async Task ValidateRoomBookingAsync(
        Guid? roomBookingId, DateOnly date, TimeSpan start, TimeSpan end,
        Guid? interviewId, Guid tenantId, CancellationToken cancellationToken)
    {
        if (roomBookingId is not { } bookingId) return;

        var booking = await _unitOfWork.Repository<Entities.HR.CompanySchedule.RoomBooking>()
            .GetQueryable()
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken);

        if (booking is null)
            throw new ArgumentException($"Room booking '{bookingId}' not found.");

        if (booking.IsCancelled || booking.Status == BookingStatus.Cancelled)
            throw new InvalidOperationException(
                $"Booking {booking.BookingNumber} has been cancelled, so it holds no room for this interview.");

        var day = date.ToDateTime(TimeOnly.MinValue);
        if (booking.StartDateTime > day + start || booking.EndDateTime < day + end)
            throw new InvalidOperationException(
                $"Booking {booking.BookingNumber} covers "
              + $"{booking.StartDateTime:dd MMM HH:mm}–{booking.EndDateTime:HH:mm}, which does not span the "
              // ⚠ Verbatim: TimeSpan needs `hh\:mm` to escape the colon, and `\:` is not a legal
              // escape in an ordinary interpolated string.
              + $@"interview ({date:dd MMM} {start:hh\:mm}–{end:hh\:mm}). Extend the booking, or move the interview.");

        // One booking, one interview. Two interviews pointing at the same hold is the double-booking
        // the hold exists to prevent, arriving from inside recruitment.
        var takenBy = await _interviewRepository.GetQueryable()
            .Where(i => i.RoomBookingId == bookingId && i.TenantId == tenantId && !i.IsDeleted
                     && i.Id != interviewId
                     && i.Status != JobInterviewStatus.Cancelled)
            .Select(i => i.InterviewNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (takenBy is not null)
            throw new InvalidOperationException(
                $"Booking {booking.BookingNumber} is already held by interview {takenBy}.");
    }

    /// <summary>
    /// Round 4, D4 — the windows in a date range where the WHOLE panel is free.
    /// </summary>
    /// <remarks>
    /// <para>A clash check that only says no is half a tool. This walks the range at the requested
    /// interval and returns the windows where nobody has a hard commitment, so the answer to
    /// "they're all busy" is a list of times rather than a shrug.</para>
    ///
    /// <para>⚠ Soft commitments do not exclude a window, but they are reported on it. A slot where
    /// two panelists are nominally on leave is still a slot HR may want — and hiding it would be the
    /// system making that call on day-granular evidence.</para>
    /// </remarks>
    public async Task<List<PanelSlotSuggestionDto>> SuggestPanelSlotsAsync(
        IReadOnlyList<Guid> panelistEmployeeIds, IReadOnlyList<Guid> externalAssociateIds,
        DateOnly fromDate, DateOnly toDate, TimeSpan dayStart, TimeSpan dayEnd,
        int durationMinutes, Guid? excludeInterviewId, int maxSuggestions = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureHr("suggest interview slots");

        if (toDate < fromDate)
            throw new InvalidOperationException("The end of the range falls before its start.");
        if (durationMinutes <= 0)
            throw new InvalidOperationException("A suggested slot needs a duration.");
        if (dayEnd <= dayStart)
            throw new InvalidOperationException("The working window ends before it starts.");
        if ((toDate.DayNumber - fromDate.DayNumber) > 60)
            throw new InvalidOperationException(
                "Sixty days is the most that can be searched at once — narrow the range.");

        var duration = TimeSpan.FromMinutes(durationMinutes);
        if (duration > dayEnd - dayStart)
            throw new InvalidOperationException(
                $"A {durationMinutes}-minute interview does not fit inside the working window given.");

        var suggestions = new List<PanelSlotSuggestionDto>();

        for (var day = fromDate; day <= toDate && suggestions.Count < maxSuggestions; day = day.AddDays(1))
        {
            for (var t = dayStart; t + duration <= dayEnd; t += duration)
            {
                if (suggestions.Count >= maxSuggestions) break;

                var availability = await GatherAvailabilityAsync(
                    panelistEmployeeIds, externalAssociateIds, day, t, t + duration,
                    excludeInterviewId, cancellationToken);

                if (availability.HasHardConflicts) continue;

                suggestions.Add(new PanelSlotSuggestionDto
                {
                    Date      = day,
                    StartTime = t,
                    EndTime   = t + duration,
                    HasSoftConflicts = availability.HasConflicts,
                    SoftConflictSummary = availability.HasConflicts
                        ? string.Join("; ", availability.Panelists
                            .Where(p => p.HasConflicts)
                            .Select(p => $"{p.EmployeeName}: {p.Commitments[0].Label}"))
                        : null,
                });
            }
        }

        return suggestions;
    }


    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<JobInterviewDto> CreateAsync(CreateJobInterviewDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        EnsureHr("schedule an interview");

        await ValidateScheduleAsync(createDto.JobVacancyId, createDto.ScheduledDate, createDto.StartTime, createDto.EndTime,
            createDto.ApplicationIds, cancellationToken);
        await ValidatePanelAsync(createDto.PanelistEmployeeIds, createDto.ExternalPanelistAssociateIds, current);

        // ⚠ Round 4, D3. The clash check BINDS here now. It existed before this lane and was called
        // from nothing but its own endpoint (§ 3 defect 7) — a recruiter who never opened the
        // availability panel scheduled the double-booking with no record that anybody was warned.
        var clash = await EnsurePanelIsFreeAsync(
            createDto.PanelistEmployeeIds ?? new List<Guid>(),
            createDto.ExternalPanelistAssociateIds ?? new List<Guid>(),
            createDto.ScheduledDate, createDto.StartTime, createDto.EndTime,
            excludeInterviewId: null, createDto.PanelClashOverrideReason, cancellationToken);

        await ValidateRoomBookingAsync(createDto.RoomBookingId, createDto.ScheduledDate,
            createDto.StartTime, createDto.EndTime, null, current, cancellationToken);

        var entity = createDto.ToEntity(current, createdByUserId);
        entity.InterviewNumber = await GenerateInterviewNumberAsync(cancellationToken);
        entity.Status = JobInterviewStatus.Scheduled;
        entity.RoomBookingId = createDto.RoomBookingId;
        RecordClashOverride(entity, clash, createDto.PanelClashOverrideReason, createdByUserId);

        await _interviewRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Interview created: {InterviewNumber}", entity.InterviewNumber);

        // Add interviewees, internal panelists, and external panelists if provided
        bool hasRelated = false;

        if (createDto.ApplicationIds?.Count > 0)
        {
            // Slots placed so far in this loop, so two candidates on the same payload cannot be
            // given the same time. Nothing is on the database yet to compare against.
            var placedSlots = new List<(TimeSpan Start, TimeSpan End)>();

            foreach (var appId in createDto.ApplicationIds)
            {
                var ie = new AddJobIntervieweeDto
                {
                    JobInterviewId   = entity.Id,
                    JobApplicationId = appId,
                }.ToEntity(current, createdByUserId);

                var slot = createDto.ApplicationSlots?.FirstOrDefault(s => s.ApplicationId == appId);
                if (slot != null)
                {
                    // ⚠ Round 4, lane C. These arrived from the payload and were written straight
                    // onto the row — a slot could sit outside the session it belongs to, or on top
                    // of the candidate booked before it, and nothing said so until two people
                    // turned up together. Checked here against the session and the slots already
                    // placed in this same loop.
                    ValidateSlotAgainstWindow(entity, slot.SlotStartTime, slot.SlotEndTime, placedSlots);

                    ie.SlotStartTime = slot.SlotStartTime;
                    ie.SlotEndTime   = slot.SlotEndTime;

                    if (slot.SlotStartTime.HasValue && slot.SlotEndTime.HasValue)
                        placedSlots.Add((slot.SlotStartTime.Value, slot.SlotEndTime.Value));
                }

                await _intervieweeRepository.AddAsync(ie);
                hasRelated = true;
            }
        }

        if (createDto.PanelistEmployeeIds?.Count > 0)
        {
            foreach (var empId in createDto.PanelistEmployeeIds)
            {
                var p = new AddJobInterviewPanelistDto
                {
                    JobInterviewId = entity.Id,
                    EmployeeId     = empId,
                    Role           = JobInterviewPanelistRole.Member,
                    IsRequired     = false,
                }.ToEntity(current, createdByUserId);
                await _panelistRepository.AddAsync(p);
                hasRelated = true;
            }
        }

        if (createDto.ExternalPanelistAssociateIds?.Count > 0)
        {
            foreach (var assocId in createDto.ExternalPanelistAssociateIds)
            {
                var ep = new AddJobInterviewExternalPanelistDto
                {
                    JobInterviewId = entity.Id,
                    AssociateId    = assocId,
                    Role           = JobInterviewPanelistRole.Member,
                    IsRequired     = false,
                }.ToEntity(current, createdByUserId);
                await _externalPanelistRepository.AddAsync(ep);
                hasRelated = true;
            }
        }

        if (hasRelated)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Apply evaluation template (preset) if provided
        if (createDto.QuestionPresetId.HasValue)
        {
            var preset = await _presetRepository.GetWithItemsAsync(createDto.QuestionPresetId.Value);
            if (preset?.TenantId == current && preset.Items is { Count: > 0 })
            {
                foreach (var item in preset.Items.OrderBy(i => i.DisplayOrder))
                {
                    var qp = new JobInterviewQuestion
                    {
                        Id                    = Guid.NewGuid(),
                        TenantId              = current,
                        JobInterviewId        = entity.Id,
                        QuestionTypeId        = item.QuestionTypeId,
                        RequiredQuestionCount = item.RequiredQuestionCount,
                        AllowedPoolSize       = item.AllowedPoolSize,
                        DisplayOrder          = item.DisplayOrder,
                        CreatedBy             = createdByUserId.ToString(),
                    };
                    await _questionPlanRepository.AddAsync(qp);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // Auto-select (randomize) questions for each plan now that plans are saved
        if (createDto.QuestionPresetId.HasValue)
            await SelectQuestionsAsync(entity.Id, cancellationToken);

        // Advance pipeline for each interviewee: Interview stage (no-op if no pipeline)
        if (createDto.ApplicationIds?.Count > 0)
        {
            foreach (var appId in createDto.ApplicationIds)
            {
                await _pipelineService.AutoAdvanceToStageTypeAsync(
                    appId, RecruitmentPipelineStageType.Interview, createdByUserId, cancellationToken);
            }
        }

        return entity.ToDto();
    }

    public async Task<JobInterviewDto> UpdateAsync(UpdateJobInterviewDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(updateDto.Id);

        EnsureHr("edit an interview");

        if (entity.Status == JobInterviewStatus.Completed || entity.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("Completed or cancelled interviews cannot be updated.");

        // ⚠ The panel is re-checked against the NEW window, excluding this interview — otherwise it
        // finds itself and every edit refuses.
        var panelIds = (await _panelistRepository.GetByInterviewIdAsync(entity.Id))
            .Where(p => p.TenantId == entity.TenantId && !p.IsDeleted)
            .Select(p => p.EmployeeId).ToList();
        var externalIds = (await _externalPanelistRepository.GetByInterviewIdAsync(entity.Id))
            .Where(p => p.TenantId == entity.TenantId && !p.IsDeleted)
            .Select(p => p.AssociateId).ToList();

        var clash = await EnsurePanelIsFreeAsync(
            panelIds, externalIds, updateDto.ScheduledDate, updateDto.StartTime, updateDto.EndTime,
            excludeInterviewId: entity.Id, updateDto.PanelClashOverrideReason, cancellationToken);

        await ValidateRoomBookingAsync(updateDto.RoomBookingId, updateDto.ScheduledDate,
            updateDto.StartTime, updateDto.EndTime, entity.Id, entity.TenantId, cancellationToken);

        // Status is deliberately NOT taken from the payload — see UpdateJobInterviewDto. It is owned by
        // reschedule / cancel / complete, each of which carries the side effects a bare status write skips.
        entity.UpdateEntity(updateDto, updatedByUserId);
        entity.RoomBookingId = updateDto.RoomBookingId;
        RecordClashOverride(entity, clash, updateDto.PanelClashOverrideReason, updatedByUserId);
        await _interviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(id);

        EnsureHr("delete an interview");

        if (entity.Status == JobInterviewStatus.Completed)
            throw new InvalidOperationException("Completed interviews cannot be deleted.");

        await _interviewRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> RescheduleAsync(RescheduleJobInterviewDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(dto.InterviewId);

        EnsureHr("reschedule an interview");

        if (entity.Status == JobInterviewStatus.Completed || entity.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("Completed or cancelled interviews cannot be rescheduled.");

        ValidateSlot(dto.NewDate, dto.NewStartTime, dto.NewEndTime, "rescheduled to a past date");

        // ⚠ A reschedule is the write MOST likely to create a clash — it is moving a confirmed panel
        // onto a window nobody checked — and it was the one write path with no check at all.
        var reschedulePanelIds = (await _panelistRepository.GetByInterviewIdAsync(entity.Id))
            .Where(p => p.TenantId == entity.TenantId && !p.IsDeleted)
            .Select(p => p.EmployeeId).ToList();
        var rescheduleExternalIds = (await _externalPanelistRepository.GetByInterviewIdAsync(entity.Id))
            .Where(p => p.TenantId == entity.TenantId && !p.IsDeleted)
            .Select(p => p.AssociateId).ToList();

        var rescheduleClash = await EnsurePanelIsFreeAsync(
            reschedulePanelIds, rescheduleExternalIds, dto.NewDate, dto.NewStartTime, dto.NewEndTime,
            excludeInterviewId: entity.Id, dto.PanelClashOverrideReason, cancellationToken);

        await ValidateRoomBookingAsync(dto.RoomBookingId ?? entity.RoomBookingId, dto.NewDate,
            dto.NewStartTime, dto.NewEndTime, entity.Id, entity.TenantId, cancellationToken);

        RecordClashOverride(entity, rescheduleClash, dto.PanelClashOverrideReason, updatedByUserId);
        if (dto.RoomBookingId.HasValue) entity.RoomBookingId = dto.RoomBookingId;

        // The original date is what the reschedule audit is for; it was on the entity and never written.
        entity.OriginalDate ??= entity.ScheduledDate;
        entity.ScheduledDate = dto.NewDate;
        entity.StartTime = dto.NewStartTime;
        entity.EndTime = dto.NewEndTime;
        entity.LocationOrLink = dto.LocationOrLink;
        entity.RescheduleReason = dto.RescheduleReason;
        entity.Status = JobInterviewStatus.Rescheduled;

        // ⚠ Round 4, lane C — § 3 defect 23. The window moved and every candidate's slot stayed
        // where it was, and the reschedule notice below then emailed each candidate their ORIGINAL
        // time against the NEW date, with a fresh confirmation token inviting them to confirm it.
        // Move a 09:00–11:00 session to 14:00–16:00 and everyone was told to arrive at 09:20.
        //
        // Where the day was apportioned, lay it out again at the same interval. Where it was not,
        // CLEAR the slots: a hand-typed time that no longer sits inside the session is worse than
        // no time at all, because it reads as deliberate.
        await ReapportionAfterRescheduleAsync(entity, cancellationToken);

        await _interviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Email #9 — Interview rescheduled; regenerate confirmation token so old links are invalidated
        var fullReschedule = await GetOwnedInterviewWithDetailsAsync(dto.InterviewId);
        if (fullReschedule.Interviewees != null)
        {
            var tenantId = fullReschedule.TenantId;
            var allInterviewees = (await _intervieweeRepository.GetByInterviewIdAsync(dto.InterviewId))
                .Where(ie => ie.TenantId == tenantId)
                .ToList();
            foreach (var ie in allInterviewees)
            {
                var email = ie.JobApplication?.JobCandidate?.Email;
                var name  = ie.JobApplication?.JobCandidate?.FullName ?? "Candidate";

                // Fresh token — candidate must re-confirm for the new date/time
                var confirmToken = Guid.NewGuid().ToString("N");
                ie.ConfirmationToken          = confirmToken;
                ie.ConfirmationTokenExpiresAt = TokenExpiryFor(fullReschedule);
                ie.ConfirmedAttendance        = null;
                ie.ConfirmationDate           = null;
                await _intervieweeRepository.UpdateAsync(ie);

                // Best-effort: an unguarded throw here would abandon the token rotations of every
                // remaining interviewee, whose UpdateAsync calls are only saved after this loop.
                try
                {
                    await SendInterviewEmailAsync(email ?? string.Empty, name, fullReschedule, isReschedule: true, confirmToken,
                        slotStart: ie.SlotStartTime, slotEnd: ie.SlotEndTime);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send interview reschedule email to {Email} — the reschedule itself succeeded.",
                        email);
                }
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> CancelAsync(CancelJobInterviewDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(dto.InterviewId);

        EnsureHr("cancel an interview");

        if (entity.Status == JobInterviewStatus.Completed)
            throw new InvalidOperationException("A completed interview cannot be cancelled.");
        if (entity.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("This interview is already cancelled.");

        entity.Status = JobInterviewStatus.Cancelled;
        entity.CancellationReason = dto.CancellationReason;

        await _interviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteAsync(Guid interviewId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInterviewAsync(interviewId);

        EnsureHr("close an interview");

        // Completion had no state guard at all, so a cancelled interview could be marked complete —
        // reviving a session nobody attended, and with it every downstream read that keys off Completed.
        if (entity.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("A cancelled interview cannot be completed.");
        if (entity.Status == JobInterviewStatus.Completed)
            throw new InvalidOperationException("This interview is already complete.");

        entity.Status = JobInterviewStatus.Completed;

        await _interviewRepository.UpdateAsync(entity);
        await AdvanceApplicationsOnCompletionAsync(entity, updatedByUserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Moves each interviewee's application on when the session closes (G-9.4, G-9.2).
    /// </summary>
    /// <remarks>
    /// <para><b>G-9.4: closing an interview used to have no downstream effect at all.</b>
    /// <c>CompleteAsync</c> set <c>Status = Completed</c> and saved. Nothing else happened — no
    /// application status moved, no pipeline stage advanced, no notification went out, no scorecard
    /// was chased. So the session ended and its applications sat wherever booking had left them, at
    /// <c>InterviewScheduled</c>, until somebody moved each one by hand. Contrast with *booking* a
    /// candidate, which does auto-advance the pipeline — the module knows how to hand over, it just
    /// did not do it here.</para>
    ///
    /// <para><b>G-9.2: the panel's verdict had no reader.</b> <c>JobInterviewee.Outcome</c> was set
    /// by <c>RecordOutcomeAsync</c> and rendered on screen, and <b>no service read it</b> — it
    /// changed no status, gated no offer, fed no analytics and reached no hire decision. The
    /// question "did this candidate pass their interview?" was answered on the interview screen and
    /// nowhere else in the system. It is now what decides where the application goes, which is the
    /// natural consumer and makes recording it worth doing.</para>
    ///
    /// <para><b>What each verdict does — and what it deliberately does not.</b> A positive verdict
    /// advances the application to the Offer stage. <b>Nothing here rejects anybody.</b> That is a
    /// deliberate limit, not an omission: rejecting an application records a reason and feeds the
    /// candidate-notification flow, and a status change that happens as a side effect of closing a
    /// session would produce rejections with no reason attached and no one who decided them. A
    /// <c>NotRecommended</c> verdict is the panel's advice; acting on it is HR's act, through the
    /// decision bar, where the reason is captured.</para>
    ///
    /// <para><c>ProceedToNextRound</c> moves nothing — it is a decision about interviewing, not
    /// about offering, so the application stays in the interview stage for the next round to be
    /// booked into. <c>OnHold</c> and a <b>missing</b> verdict also move nothing: a panel that has
    /// not decided must not have a decision inferred for it, and the commonest reason a verdict is
    /// missing is that scorecards are still outstanding.</para>
    ///
    /// <para>A no-show is skipped: there is nothing to judge, and their application is handled by
    /// whoever chases the reschedule.</para>
    ///
    /// <para>Failures are logged per interviewee rather than thrown. The session really is complete
    /// by this point, and refusing to record that because one application could not be moved would
    /// leave the interview open — the worse of the two states.</para>
    /// </remarks>
    private async Task AdvanceApplicationsOnCompletionAsync(
        JobInterview interview, Guid actingEmployeeId, CancellationToken cancellationToken)
    {
        var interviewees = await _intervieweeRepository.GetByInterviewIdAsync(interview.Id);

        foreach (var interviewee in interviewees.Where(i => i.TenantId == interview.TenantId))
        {
            if (interviewee.CandidateAttended == false) continue;
            if (interviewee.Outcome is not { } outcome) continue;

            var stageType = outcome switch
            {
                JobInterviewOutcome.HighlyRecommended
                or JobInterviewOutcome.Recommended
                or JobInterviewOutcome.Acceptable
                    => (RecruitmentPipelineStageType?)RecruitmentPipelineStageType.Offer,

                // Rejected / NotRecommended / ProceedToNextRound / OnHold all move nothing — see
                // the remarks above for why a rejection in particular is not made here.
                _ => null,
            };

            if (stageType is not { } target) continue;

            try
            {
                await _pipelineService.AutoAdvanceToStageTypeAsync(
                    interviewee.JobApplicationId, target, actingEmployeeId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Interview {InterviewNumber} closed, but application {ApplicationId} could not be " +
                    "advanced on the panel's verdict of {Outcome}.",
                    interview.InterviewNumber, interviewee.JobApplicationId, outcome);
            }
        }
    }

    // ── Internal panelists ────────────────────────────────────────────────────

    public async Task<JobInterviewPanelistDto> AddPanelistAsync(AddJobInterviewPanelistDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedInterviewAsync(createDto.JobInterviewId);
        EnsureHr("change an interview panel");

        // The employee was never validated: an unknown id died on a foreign-key 500 and an id belonging
        // to another tenant was accepted outright.
        await EnsureEmployeeInTenantAsync(createDto.EmployeeId, current);

        var existing = (await _panelistRepository.GetByInterviewIdAsync(createDto.JobInterviewId))
            .Where(p => p.TenantId == current)
            .ToList();
        if (existing.Any(p => p.EmployeeId == createDto.EmployeeId))
            throw new InvalidOperationException("That employee is already on this interview's panel.");

        var entity = createDto.ToEntity(current, createdByUserId);
        await _panelistRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read so the response carries the employee's name and the interview number, which a freshly
        // constructed entity has no navigations for.
        var saved = await _panelistRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> RemovePanelistAsync(Guid panelistId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPanelistAsync(panelistId);
        EnsureHr("change an interview panel");

        await _panelistRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobInterviewPanelistDto> UpdatePanelistAsync(UpdateJobInterviewPanelistDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPanelistAsync(updateDto.Id);
        EnsureHr("change an interview panel");

        entity.Role       = updateDto.Role;
        entity.IsRequired = updateDto.IsRequired;

        await _panelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobInterviewPanelistDto>> GetPanelistsAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        await EnsureCanReadInterviewAsync(interviewId);
        var entities = await _panelistRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<bool> ConfirmPanelistAsync(Guid panelistId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPanelistAsync(panelistId);

        // Confirming an assignment is a statement about yourself. HR may confirm on someone's behalf
        // (they take the phone call); nobody else may confirm for a colleague.
        if (!IsHr && entity.EmployeeId != CallerEmployeeId)
            throw new UnauthorizedAccessException("You can only confirm your own panel assignment.");

        entity.IsConfirmed = true;
        entity.ConfirmationDate = DateTime.UtcNow;

        await _panelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ConfirmPanelistAssignmentResultDto> ConfirmPanelistAssignmentByTokenAsync(
        string token, CancellationToken cancellationToken = default)
    {
        // Search internal panelists first, then external
        var internalPanelist = await _panelistRepository.GetByConfirmationTokenAsync(token);
        if (internalPanelist != null)
        {
            var already = internalPanelist.IsConfirmed;
            if (!already && IsTokenExpired(internalPanelist.ConfirmationTokenExpiresAt))
                throw new ArgumentException("Confirmation link is invalid or has expired.");
            if (!already)
            {
                internalPanelist.IsConfirmed      = true;
                internalPanelist.ConfirmationDate = DateTime.UtcNow;
            }
            internalPanelist.ConfirmationToken = null; // single-use: invalidate once consumed
            await _panelistRepository.UpdateAsync(internalPanelist);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ConfirmPanelistAssignmentResultDto
            {
                AlreadyConfirmed = already,
                PanelistName     = $"{internalPanelist.Employee?.FirstName} {internalPanelist.Employee?.LastName}".Trim(),
                JobTitle         = internalPanelist.JobInterview?.JobVacancy?.JobTitle ?? string.Empty,
                ScheduledDate    = internalPanelist.JobInterview?.ScheduledDate ?? DateOnly.MinValue,
                InterviewNumber  = internalPanelist.JobInterview?.InterviewNumber ?? string.Empty,
                IsExternal       = false,
            };
        }

        var externalPanelist = await _externalPanelistRepository.GetByConfirmationTokenAsync(token);
        if (externalPanelist != null)
        {
            var already = externalPanelist.IsConfirmed;
            if (!already && IsTokenExpired(externalPanelist.ConfirmationTokenExpiresAt))
                throw new ArgumentException("Confirmation link is invalid or has expired.");
            if (!already)
            {
                externalPanelist.IsConfirmed      = true;
                externalPanelist.ConfirmationDate = DateTime.UtcNow;
            }
            externalPanelist.ConfirmationToken = null; // single-use: invalidate once consumed
            await _externalPanelistRepository.UpdateAsync(externalPanelist);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ConfirmPanelistAssignmentResultDto
            {
                AlreadyConfirmed = already,
                PanelistName     = $"{externalPanelist.ExternalAssociate?.FirstName} {externalPanelist.ExternalAssociate?.LastName}".Trim(),
                JobTitle         = externalPanelist.JobInterview?.JobVacancy?.JobTitle ?? string.Empty,
                ScheduledDate    = externalPanelist.JobInterview?.ScheduledDate ?? DateOnly.MinValue,
                InterviewNumber  = externalPanelist.JobInterview?.InterviewNumber ?? string.Empty,
                IsExternal       = true,
            };
        }

        throw new ArgumentException("Confirmation link is invalid or has expired.");
    }

    public async Task<bool> RecordPanelistAttendanceAsync(Guid panelistId, bool? attended, string? noShowReason, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPanelistAsync(panelistId);
        await EnsureCanReadInterviewAsync(entity.JobInterviewId);

        entity.Attended     = attended;
        entity.NoShowReason = attended == false ? noShowReason : null;

        await _panelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── External panelists ────────────────────────────────────────────────────

    public async Task<JobInterviewExternalPanelistDto> AddExternalPanelistAsync(AddJobInterviewExternalPanelistDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedInterviewAsync(createDto.JobInterviewId);
        EnsureHr("change an interview panel");
        await EnsureAssociateInTenantAsync(createDto.AssociateId, current);

        var existing = (await _externalPanelistRepository.GetByInterviewIdAsync(createDto.JobInterviewId))
            .Where(p => p.TenantId == current)
            .ToList();
        if (existing.Any(p => p.AssociateId == createDto.AssociateId))
            throw new InvalidOperationException("That associate is already on this interview's panel.");

        var entity = createDto.ToEntity(current, createdByUserId);
        await _externalPanelistRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _externalPanelistRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> RemoveExternalPanelistAsync(Guid externalPanelistId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExternalPanelistAsync(externalPanelistId);
        EnsureHr("change an interview panel");

        await _externalPanelistRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobInterviewExternalPanelistDto> UpdateExternalPanelistAsync(UpdateJobInterviewExternalPanelistDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExternalPanelistAsync(updateDto.Id);
        EnsureHr("change an interview panel");

        entity.Role       = updateDto.Role;
        entity.IsRequired = updateDto.IsRequired;
        entity.UpdatedAt  = DateTime.UtcNow;
        entity.UpdatedBy  = updatedByUserId.ToString();

        await _externalPanelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobInterviewExternalPanelistDto>> GetExternalPanelistsAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        await EnsureCanReadInterviewAsync(interviewId);
        var entities = await _externalPanelistRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<bool> RecordExternalPanelistAttendanceAsync(Guid extPanelistId, bool? attended, string? noShowReason, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExternalPanelistAsync(extPanelistId);
        await EnsureCanReadInterviewAsync(entity.JobInterviewId);

        entity.Attended     = attended;
        entity.NoShowReason = attended == false ? noShowReason : null;
        entity.UpdatedAt    = DateTime.UtcNow;
        entity.UpdatedBy    = updatedByUserId.ToString();

        await _externalPanelistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Interviewees ──────────────────────────────────────────────────────────

    public async Task<JobIntervieweeDto> AddIntervieweeAsync(AddJobIntervieweeDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var interview = await GetOwnedInterviewAsync(createDto.JobInterviewId);
        EnsureHr("add a candidate to an interview");

        // CreateAsync validated that every application exists, is in-tenant and belongs to the
        // interview's vacancy. This — the other door onto the same table — validated nothing, so a
        // candidate who had never applied for the role could be booked into its interview.
        await ValidateApplicationsForVacancyAsync(
            interview.JobVacancyId, new List<Guid> { createDto.JobApplicationId }, current);

        var already = (await _intervieweeRepository.GetByInterviewIdAsync(createDto.JobInterviewId))
            .Where(ie => ie.TenantId == current)
            .ToList();
        if (already.Any(ie => ie.JobApplicationId == createDto.JobApplicationId))
            throw new InvalidOperationException("That candidate is already booked into this interview.");

        var entity = createDto.ToEntity(current, createdByUserId);
        await _intervieweeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Note: interview invite emails are NOT sent automatically.
        // Use SendInvitesAsync to send invites on demand.

        // Keep the pipeline in step with the create path, which advances every application it books in.
        await _pipelineService.AutoAdvanceToStageTypeAsync(
            createDto.JobApplicationId, RecruitmentPipelineStageType.Interview, createdByUserId, cancellationToken);

        var saved = await _intervieweeRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> RemoveIntervieweeAsync(Guid intervieweeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIntervieweeAsync(intervieweeId);
        EnsureHr("remove a candidate from an interview");

        await _intervieweeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<JobIntervieweeDto>> GetIntervieweesAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        await EnsureCanReadInterviewAsync(interviewId);
        var entities = await _intervieweeRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<SendInterviewInvitesResultDto> SendInvitesAsync(
        Guid interviewId, List<Guid> applicationIds, CancellationToken cancellationToken = default)
    {
        // Load interview with full interviewee details for email context
        var fullInterview = await GetOwnedInterviewWithDetailsAsync(interviewId);
        EnsureHr("send interview invitations");

        if (fullInterview.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("Invitations cannot be sent for a cancelled interview.");

        applicationIds ??= new List<Guid>();

        var allInterviewees = (await _intervieweeRepository.GetByInterviewIdAsync(interviewId))
            .Where(ie => ie.TenantId == fullInterview.TenantId)
            .ToList();

        // If no applicationIds specified, send to everyone; otherwise filter
        var targets = applicationIds.Count > 0
            ? allInterviewees.Where(ie => applicationIds.Contains(ie.JobApplicationId)).ToList()
            : allInterviewees.ToList();

        var result = new SendInterviewInvitesResultDto
        {
            TotalRequested = targets.Count,
        };

        foreach (var ie in targets)
        {
            var email = ie.JobApplication?.JobCandidate?.Email ?? string.Empty;
            var name  = ie.JobApplication?.JobCandidate?.FullName ?? "Candidate";
            var item  = new InterviewInviteResultItemDto
            {
                ApplicationId = ie.JobApplicationId,
                CandidateName = name,
            };

            try
            {
                // Generate a fresh confirmation token for this candidate
                var confirmToken = Guid.NewGuid().ToString("N");
                ie.ConfirmationToken          = confirmToken;
                ie.ConfirmationTokenExpiresAt = TokenExpiryFor(fullInterview);
                ie.ConfirmedAttendance        = null;  // reset any prior confirmation
                ie.ConfirmationDate           = null;
                ie.InvitationSentDate         = DateTime.UtcNow;

                await SendInterviewEmailAsync(email, name, fullInterview, isReschedule: false, confirmToken,
                    slotStart: ie.SlotStartTime, slotEnd: ie.SlotEndTime);
                await _intervieweeRepository.UpdateAsync(ie);
                item.Sent = true;
                result.Sent++;
            }
            catch (Exception ex)
            {
                item.Sent  = false;
                item.Error = ex.Message;
                result.Skipped++;
            }

            result.Results.Add(item);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<SendPanelistNotificationsResultDto> SendPanelistNotificationsAsync(
        Guid interviewId, List<Guid>? employeeIds, List<Guid>? externalAssociateIds,
        CancellationToken cancellationToken = default)
    {
        var fullInterview = await GetOwnedInterviewWithDetailsAsync(interviewId);
        EnsureHr("notify an interview panel");

        if (fullInterview.Status == JobInterviewStatus.Cancelled)
            throw new InvalidOperationException("Panel notifications cannot be sent for a cancelled interview.");

        var allPanelists    = (await _panelistRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == fullInterview.TenantId).ToList();
        var allExtPanelists = (await _externalPanelistRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == fullInterview.TenantId).ToList();

        // null  = notify everyone in this group (broadcast)
        // empty = skip this group entirely
        // [ids] = notify only the specified members
        var targets = employeeIds == null
            ? allPanelists
            : employeeIds.Count > 0
                ? allPanelists.Where(p => employeeIds.Contains(p.EmployeeId)).ToList()
                : new List<JobInterviewPanelist>();

        var extTargets = externalAssociateIds == null
            ? allExtPanelists
            : externalAssociateIds.Count > 0
                ? allExtPanelists.Where(p => externalAssociateIds.Contains(p.AssociateId)).ToList()
                : new List<JobInterviewExternalPanelist>();

        var result = new SendPanelistNotificationsResultDto
        {
            TotalRequested = targets.Count + extTargets.Count,
        };

        foreach (var p in targets)
        {
            var email = p.Employee?.EmailAddress ?? string.Empty;
            var name  = $"{p.Employee?.FirstName} {p.Employee?.LastName}".Trim();
            var item  = new PanelistNotificationResultItemDto
            {
                PersonId   = p.EmployeeId,
                Name       = name,
                IsExternal = false,
            };
            try
            {
                var pToken = Guid.NewGuid().ToString("N");
                p.ConfirmationToken          = pToken;
                p.ConfirmationTokenExpiresAt = TokenExpiryFor(fullInterview);
                p.IsConfirmed                = false;
                p.ConfirmationDate           = null;
                p.InvitationSentDate         = DateTime.UtcNow;
                await SendPanelistEmailAsync(email, name, fullInterview, pToken);
                await _panelistRepository.UpdateAsync(p);
                item.Sent = true;
                result.Sent++;
            }
            catch (Exception ex)
            {
                item.Error = ex.Message;
                result.Skipped++;
            }
            result.Results.Add(item);
        }

        foreach (var ep in extTargets)
        {
            var email = ep.ExternalAssociate?.Email ?? string.Empty;
            var name  = $"{ep.ExternalAssociate?.FirstName} {ep.ExternalAssociate?.LastName}".Trim();
            var item  = new PanelistNotificationResultItemDto
            {
                PersonId   = ep.AssociateId,
                Name       = name,
                IsExternal = true,
            };
            try
            {
                var epToken = Guid.NewGuid().ToString("N");
                ep.ConfirmationToken          = epToken;
                ep.ConfirmationTokenExpiresAt = TokenExpiryFor(fullInterview);
                ep.IsConfirmed                = false;
                ep.ConfirmationDate           = null;
                ep.InvitationSentDate         = DateTime.UtcNow;
                await SendPanelistEmailAsync(email, name, fullInterview, epToken);
                await _externalPanelistRepository.UpdateAsync(ep);
                item.Sent = true;
                result.Sent++;
            }
            catch (Exception ex)
            {
                item.Error = ex.Message;
                result.Skipped++;
            }
            result.Results.Add(item);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<ConfirmInterviewAttendanceResultDto> ConfirmAttendanceByTokenAsync(
        string token, CancellationToken cancellationToken = default)
    {
        var ie = await _intervieweeRepository.GetByConfirmationTokenAsync(token);
        if (ie == null)
            throw new ArgumentException("Confirmation link is invalid or has expired.");

        var alreadyConfirmed = ie.ConfirmedAttendance == true;
        if (!alreadyConfirmed && IsTokenExpired(ie.ConfirmationTokenExpiresAt))
            throw new ArgumentException("Confirmation link is invalid or has expired.");
        if (!alreadyConfirmed)
        {
            ie.ConfirmedAttendance = true;
            ie.ConfirmationDate    = DateTime.UtcNow;
        }
        ie.ConfirmationToken = null; // single-use: invalidate once consumed
        await _intervieweeRepository.UpdateAsync(ie);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConfirmInterviewAttendanceResultDto
        {
            AlreadyConfirmed = alreadyConfirmed,
            CandidateName    = ie.JobApplication?.JobCandidate?.FullName ?? string.Empty,
            JobTitle         = ie.JobInterview?.JobVacancy?.JobTitle ?? string.Empty,
            ScheduledDate    = ie.JobInterview?.ScheduledDate ?? DateOnly.MinValue,
            InterviewNumber  = ie.JobInterview?.InterviewNumber ?? string.Empty,
        };
    }

    public async Task<bool> UpdateIntervieweeSlotAsync(UpdateIntervieweeSlotDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIntervieweeAsync(dto.IntervieweeId);
        EnsureHr("change a candidate's interview slot");

        if (dto.SlotStartTime.HasValue && dto.SlotEndTime.HasValue && dto.SlotEndTime <= dto.SlotStartTime)
            throw new InvalidOperationException("A candidate's slot must end after it starts.");

        // ⚠ Round 4, lane C. Until this, a slot was checked for nothing beyond ending after it
        // started: it could sit wholly outside the session, land on top of another candidate, or
        // fall inside the lunch break, and the only sign was two people in the corridor.
        var interview = await GetOwnedInterviewAsync(entity.JobInterviewId);
        await EnsureSlotIsUsableAsync(interview, entity.Id, dto.SlotStartTime, dto.SlotEndTime, cancellationToken);

        entity.SlotStartTime = dto.SlotStartTime;
        entity.SlotEndTime   = dto.SlotEndTime;

        await _intervieweeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Refuses a slot that does not sit inside the session, collides with another candidate, or
    /// falls in a break — round 4, lane C.
    /// </summary>
    /// <remarks>
    /// <para><b>A half-supplied slot is refused too.</b> A start with no end is not a shorter
    /// interview, it is a record nobody can read: the timetable cannot place it, the invitation
    /// cannot state it, and the clash check cannot compare it.</para>
    ///
    /// <para><b>Overlap is tested against the OTHER candidates, excluding this one</b>, so saving a
    /// slot unchanged is never refused for colliding with itself.</para>
    /// </remarks>
    private async Task EnsureSlotIsUsableAsync(
        JobInterview interview,
        Guid intervieweeId,
        TimeSpan? start,
        TimeSpan? end,
        CancellationToken cancellationToken)
    {
        // Clearing a slot is always allowed — it returns the candidate to "see them during the
        // session", which is what an un-apportioned interview means.
        if (start is null && end is null) return;

        if (start is null || end is null)
            throw new InvalidOperationException(
                "A slot needs both a start and an end time. Give both, or clear both to put the "
                + "candidate back on the session's own time.");

        if (start < interview.StartTime || end > interview.EndTime)
            throw new InvalidOperationException(
                $"That slot falls outside the interview, which runs {interview.StartTime:hh\\:mm}"
                + $"–{interview.EndTime:hh\\:mm}. Move the slot, or widen the session first.");

        foreach (var b in ReadBreaks(interview))
        {
            if (start < b.End && b.Start < end)
                throw new InvalidOperationException(
                    $"That slot runs into the {b.Label ?? "break"} at {b.Start:hh\\:mm}–{b.End:hh\\:mm}.");
        }

        var others = (await _intervieweeRepository.GetByInterviewIdAsync(interview.Id))
            .Where(i => i.Id != intervieweeId && i.TenantId == interview.TenantId && !i.IsDeleted)
            .Where(i => i.SlotStartTime.HasValue && i.SlotEndTime.HasValue);

        foreach (var other in others)
        {
            if (start < other.SlotEndTime && other.SlotStartTime < end)
            {
                var who = other.JobApplication?.JobCandidate?.FullName ?? "another candidate";
                throw new InvalidOperationException(
                    $"That slot overlaps {who} at {other.SlotStartTime:hh\\:mm}–{other.SlotEndTime:hh\\:mm}. "
                    + "Two candidates cannot be seen at once.");
            }
        }
    }

    /// <summary>
    /// Re-lays the day after a reschedule, or clears the slots when there is no layout to reproduce.
    /// </summary>
    /// <remarks>
    /// ⚠ Called BEFORE the reschedule emails go out, because those emails read
    /// <c>ie.SlotStartTime</c>. Moving this after them puts the defect straight back.
    /// </remarks>
    private async Task ReapportionAfterRescheduleAsync(JobInterview interview, CancellationToken cancellationToken)
    {
        var attendees = (await _intervieweeRepository.GetByInterviewIdAsync(interview.Id))
            .Where(i => i.TenantId == interview.TenantId && !i.IsDeleted)
            .ToList();
        if (attendees.Count == 0) return;

        if (interview.SlotMinutes is { } slotMinutes)
        {
            var plan = InterviewSlotApportioner.Apportion(
                interview.StartTime,
                interview.EndTime,
                slotMinutes,
                interview.SlotBufferMinutes ?? 0,
                ReadBreaks(interview),
                attendees.Select(a => a.JobApplicationId).ToList());

            var bySlot = plan.Slots.ToDictionary(s => s.ApplicationId);
            foreach (var attendee in attendees)
            {
                var hit = bySlot.TryGetValue(attendee.JobApplicationId, out var slot);
                attendee.SlotStartTime = hit ? slot!.Start : null;
                attendee.SlotEndTime = hit ? slot!.End : null;
                await _intervieweeRepository.UpdateAsync(attendee);
            }

            if (!plan.AllFit)
                _logger.LogWarning(
                    "Interview {InterviewNumber} was rescheduled into a window that holds only "
                    + "{Placed} of {Total} candidates; {Unplaced} lost their slot.",
                    interview.InterviewNumber, plan.Slots.Count, attendees.Count, plan.Unplaced.Count);
            return;
        }

        // Never apportioned: any slot present was typed by hand against the OLD window.
        foreach (var attendee in attendees.Where(a => a.SlotStartTime.HasValue || a.SlotEndTime.HasValue))
        {
            attendee.SlotStartTime = null;
            attendee.SlotEndTime = null;
            await _intervieweeRepository.UpdateAsync(attendee);
        }
    }

    /// <summary>
    /// The synchronous half of the slot rules, for the create path — where nothing is on the
    /// database yet and the only slots to collide with are the ones on the same payload.
    /// </summary>
    private static void ValidateSlotAgainstWindow(
        JobInterview interview,
        TimeSpan? start,
        TimeSpan? end,
        IReadOnlyList<(TimeSpan Start, TimeSpan End)> alreadyPlaced)
    {
        if (start is null && end is null) return;

        if (start is null || end is null)
            throw new InvalidOperationException(
                "A slot needs both a start and an end time. Give both, or omit both.");

        if (end <= start)
            throw new InvalidOperationException("A candidate's slot must end after it starts.");

        if (start < interview.StartTime || end > interview.EndTime)
            throw new InvalidOperationException(
                $"A slot of {start:hh\\:mm}–{end:hh\\:mm} falls outside the interview, which runs "
                + $"{interview.StartTime:hh\\:mm}–{interview.EndTime:hh\\:mm}.");

        foreach (var (otherStart, otherEnd) in alreadyPlaced)
        {
            if (start < otherEnd && otherStart < end)
                throw new InvalidOperationException(
                    $"Two candidates are booked into overlapping slots ({start:hh\\:mm}–{end:hh\\:mm} "
                    + $"and {otherStart:hh\\:mm}–{otherEnd:hh\\:mm}). They cannot be seen at once.");
        }
    }

    /// <summary>The interview's stored breaks, or an empty set when the day was never apportioned.</summary>
    private static IReadOnlyList<InterviewSlotApportioner.Break> ReadBreaks(JobInterview interview)
    {
        if (string.IsNullOrWhiteSpace(interview.BreaksJson))
            return Array.Empty<InterviewSlotApportioner.Break>();
        try
        {
            return JsonSerializer.Deserialize<List<InterviewSlotApportioner.Break>>(interview.BreaksJson)
                   ?? (IReadOnlyList<InterviewSlotApportioner.Break>)Array.Empty<InterviewSlotApportioner.Break>();
        }
        catch (JsonException)
        {
            // A break list that will not parse must not make the interview unsaveable. The day
            // simply has no breaks as far as validation is concerned, and the log says so.
            return Array.Empty<InterviewSlotApportioner.Break>();
        }
    }

    /// <inheritdoc />
    public async Task<InterviewSlotPlanDto> PreviewSlotApportionmentAsync(
        ApportionInterviewSlotsDto dto, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewWithDetailsAsync(dto.InterviewId);
        await EnsureCanReadInterviewAsync(interview.Id);
        return BuildPlan(interview, dto);
    }

    /// <inheritdoc />
    public async Task<InterviewSlotPlanDto> ApplySlotApportionmentAsync(
        ApportionInterviewSlotsDto dto, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewWithDetailsAsync(dto.InterviewId);
        EnsureHr("apportion interview slots");

        if (interview.Status is JobInterviewStatus.Completed or JobInterviewStatus.Cancelled)
            throw new InvalidOperationException(
                $"A {interview.Status.ToString().ToLowerInvariant()} interview cannot be re-timetabled.");

        var plan = BuildPlan(interview, dto);

        // ⚠ The parameters are stored even when somebody did not fit. The day WAS apportioned at
        // this interval, and a reschedule must be able to reproduce it — refusing to remember the
        // shape of a partially-placed day would mean the reschedule silently fell back to clearing
        // every slot, which is the defect this column exists to close.
        interview.SlotMinutes = dto.SlotMinutes;
        interview.SlotBufferMinutes = dto.BufferMinutes;
        interview.BreaksJson = plan.Breaks.Count == 0
            ? null
            : JsonSerializer.Serialize(plan.Breaks.Select(b =>
                new InterviewSlotApportioner.Break(b.Start, b.End, b.Label)));

        var bySlot = plan.Slots.ToDictionary(s => s.IntervieweeId);
        var attendees = (await _intervieweeRepository.GetByInterviewIdAsync(interview.Id))
            .Where(i => i.TenantId == interview.TenantId && !i.IsDeleted)
            .ToList();

        foreach (var attendee in attendees)
        {
            if (bySlot.TryGetValue(attendee.Id, out var slot))
            {
                attendee.SlotStartTime = slot.SlotStartTime;
                attendee.SlotEndTime = slot.SlotEndTime;
            }
            else
            {
                // Everyone who did not fit loses any slot they had. Leaving a stale time on a
                // candidate the new layout could not place is exactly how somebody arrives for an
                // appointment nobody is expecting to keep.
                attendee.SlotStartTime = null;
                attendee.SlotEndTime = null;
            }
            await _intervieweeRepository.UpdateAsync(attendee);
        }

        await _interviewRepository.UpdateAsync(interview);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Interview {InterviewNumber} apportioned: {Placed} placed, {Unplaced} unplaced at {Slot} min.",
            interview.InterviewNumber, plan.Slots.Count, plan.Unplaced.Count, dto.SlotMinutes);

        return plan;
    }

    /// <summary>Runs the apportioner over one interview's attendees and dresses the result for a screen.</summary>
    private static InterviewSlotPlanDto BuildPlan(JobInterview interview, ApportionInterviewSlotsDto dto)
    {
        var attendees = (interview.Interviewees ?? new List<JobInterviewee>())
            .Where(i => !i.IsDeleted)
            .ToList();

        // The caller may name an order; otherwise take them as they were added. Ids the caller
        // supplies that are not on this interview are ignored rather than refused — a stale board
        // should not make the preview unusable.
        var ordered = dto.ApplicationIds is { Count: > 0 }
            ? dto.ApplicationIds
                .Select(id => attendees.FirstOrDefault(a => a.JobApplicationId == id))
                .Where(a => a is not null)
                .Select(a => a!)
                .ToList()
            : attendees;

        var breaks = dto.Breaks?
            .Select(b => new InterviewSlotApportioner.Break(b.Start, b.End, b.Label))
            ?? Array.Empty<InterviewSlotApportioner.Break>();

        var plan = InterviewSlotApportioner.Apportion(
            interview.StartTime,
            interview.EndTime,
            dto.SlotMinutes,
            dto.BufferMinutes,
            breaks,
            ordered.Select(a => a.JobApplicationId).ToList());

        InterviewSlotAssignmentDto Describe(JobInterviewee a, InterviewSlotApportioner.Slot? s) => new()
        {
            IntervieweeId = a.Id,
            JobApplicationId = a.JobApplicationId,
            CandidateName = a.JobApplication?.JobCandidate?.FullName ?? "Candidate",
            ApplicationNumber = a.JobApplication?.ApplicationNumber ?? string.Empty,
            Ordinal = s?.Ordinal ?? 0,
            SlotStartTime = s?.Start ?? default,
            SlotEndTime = s?.End ?? default,
        };

        var byApplication = ordered.ToDictionary(a => a.JobApplicationId);

        return new InterviewSlotPlanDto
        {
            InterviewId = interview.Id,
            ScheduledDate = interview.ScheduledDate,
            WindowStart = interview.StartTime,
            WindowEnd = interview.EndTime,
            SlotMinutes = dto.SlotMinutes,
            BufferMinutes = dto.BufferMinutes,
            Breaks = plan.Breaks
                .Select(b => new InterviewBreakDto { Start = b.Start, End = b.End, Label = b.Label })
                .ToList(),
            Slots = plan.Slots
                .Where(s => byApplication.ContainsKey(s.ApplicationId))
                .Select(s => Describe(byApplication[s.ApplicationId], s))
                .ToList(),
            Unplaced = plan.Unplaced
                .Where(byApplication.ContainsKey)
                .Select(id => Describe(byApplication[id], null))
                .ToList(),
            AllFit = plan.AllFit,
            FirstFreeAfterWindow = plan.FirstFreeAfterWindow,
            Summary = plan.Summary,
        };
    }

    public async Task<bool> RecordAttendanceAsync(Guid intervieweeId, bool? attended, string? noShowReason, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIntervieweeAsync(intervieweeId);
        // Attendance is recorded in the room, so the panel may record it as well as HR.
        await EnsureCanReadInterviewAsync(entity.JobInterviewId);

        entity.CandidateAttended = attended;
        entity.NoShowReason      = attended == false ? noShowReason : null;

        await _intervieweeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordOutcomeAsync(RecordIntervieweeOutcomeDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIntervieweeAsync(dto.IntervieweeId);

        // ⚠ G-9.3 (2026-09-15): this guarded with EnsureCanReadInterviewAsync — **read** access —
        // not EnsureHr. So one member of a five-person panel could set the panel's verdict on a
        // candidate, overwrite another member's, and do so without having signed off their own
        // scorecard. Every other write on the interview record itself is HR-only; this one, the
        // hire/no-hire recommendation, was the exception.
        //
        // It is now the recruitment desk's to record — the panel scores, the desk records what the
        // panel concluded — which is also what makes the outcome safe to act on downstream (G-9.2).
        // The genuine guard that was already here stays: a no-show has no verdict to give.
        await GetOwnedInterviewAsync(entity.JobInterviewId);
        EnsureHr("record the panel's verdict on a candidate");

        if (entity.CandidateAttended == false)
            throw new InvalidOperationException(
                "This candidate was recorded as a no-show, so there is no interview outcome to record.");

        entity.Outcome = dto.Outcome;

        await _intervieweeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Question plans ────────────────────────────────────────────────────────

    public async Task<JobInterviewQuestionDto> AddQuestionPlanAsync(CreateJobInterviewQuestionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedInterviewAsync(createDto.JobInterviewId);
        EnsureHr("change an interview's question plan");

        await EnsureQuestionTypeInTenantAsync(createDto.QuestionTypeId, current);

        var existingPlans = (await _questionPlanRepository.GetByInterviewIdAsync(createDto.JobInterviewId))
            .Where(p => p.TenantId == current)
            .ToList();
        if (existingPlans.Any(p => p.QuestionTypeId == createDto.QuestionTypeId))
            throw new InvalidOperationException("This interview already has a plan for that question type.");

        var entity = createDto.ToEntity(current, createdByUserId);
        await _questionPlanRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _questionPlanRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<IEnumerable<JobInterviewQuestionDto>> GetQuestionPlansAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        // Panelists must be able to read the questions they are about to ask.
        await EnsureCanReadInterviewAsync(interviewId);
        var entities = await _questionPlanRepository.GetByInterviewIdAsync(interviewId);
        return entities.Where(e => e.TenantId == interview.TenantId).Select(e => e.ToDto());
    }

    public async Task<JobInterviewQuestionDto> UpdateQuestionPlanAsync(UpdateJobInterviewQuestionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionPlanAsync(updateDto.Id);
        EnsureHr("change an interview's question plan");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _questionPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteQuestionPlanAsync(Guid questionPlanId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionPlanAsync(questionPlanId);
        EnsureHr("change an interview's question plan");

        await _questionPlanRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AddSelectedQuestionAsync(Guid questionPlanId, Guid questionDetailId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var plan = await GetOwnedQuestionPlanAsync(questionPlanId);
        EnsureHr("change an interview's question plan");

        var questionDetail = await _questionDetailRepository.GetByIdAsync(questionDetailId);
        if (questionDetail == null || questionDetail.TenantId != plan.TenantId)
            throw new ArgumentException($"Question detail with ID '{questionDetailId}' not found.");

        // A plan is "N questions of this type" — a question from another type is not a member of it.
        if (questionDetail.QuestionTypeId != plan.QuestionTypeId)
            throw new InvalidOperationException("That question belongs to a different question type.");

        var alreadySelected = (await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(questionPlanId))
            .Any(sq => sq.TenantId == plan.TenantId && sq.QuestionDetailId == questionDetailId);
        if (alreadySelected)
            throw new InvalidOperationException("That question is already in this plan.");

        var selectedQuestion = new JobInterviewSelectedQuestion
        {
            Id = Guid.NewGuid(),
            TenantId = plan.TenantId,
            JobInterviewQuestionId = questionPlanId,
            QuestionDetailId = questionDetailId
        };

        await _selectedQuestionRepository.AddAsync(selectedQuestion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveSelectedQuestionAsync(Guid questionPlanId, Guid questionDetailId, CancellationToken cancellationToken = default)
    {
        await GetOwnedQuestionPlanAsync(questionPlanId);
        EnsureHr("change an interview's question plan");
        var selectedQuestions = await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(questionPlanId);
        var toRemove = selectedQuestions.FirstOrDefault(sq => sq.QuestionDetailId == questionDetailId && sq.TenantId == GetTenantId());
        if (toRemove == null)
            return false;

        await _selectedQuestionRepository.DeleteAsync(toRemove);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SelectQuestionsAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        EnsureHr("draw an interview's questions");
        var plans = (await _questionPlanRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == interview.TenantId)
            .ToList();
        foreach (var plan in plans)
        {
            // Clear existing selections
            var existing = (await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(plan.Id))
                .Where(sq => sq.TenantId == plan.TenantId)
                .ToList();
            foreach (var sq in existing)
                await _selectedQuestionRepository.DeleteAsync(sq);

            // Get active questions for this type and randomly draw up to AllowedPoolSize
            var questions = (await _questionDetailRepository.GetByQuestionTypeIdAsync(plan.QuestionTypeId))
                .Where(q => q.TenantId == plan.TenantId && q.IsActive)
                .OrderBy(_ => Guid.NewGuid())
                .Take(plan.AllowedPoolSize)
                .ToList();

            for (int i = 0; i < questions.Count; i++)
            {
                await _selectedQuestionRepository.AddAsync(new JobInterviewSelectedQuestion
                {
                    Id                    = Guid.NewGuid(),
                    TenantId              = plan.TenantId,
                    JobInterviewQuestionId = plan.Id,
                    QuestionDetailId      = questions[i].Id,
                    DisplayOrder          = i + 1,
                    CreatedBy             = "system",
                });
            }
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<QuestionPlanPreviewDto>> PreviewSelectQuestionsAsync(
        Guid interviewId, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        EnsureHr("draw an interview's questions");
        var plans  = (await _questionPlanRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == interview.TenantId)
            .ToList();
        var result = new List<QuestionPlanPreviewDto>(plans.Count);

        foreach (var plan in plans)
        {
            var available = (await _questionDetailRepository.GetByQuestionTypeIdAsync(plan.QuestionTypeId))
                .Where(q => q.TenantId == plan.TenantId && q.IsActive)
                .ToList();

            var questions = available
                .OrderBy(_ => Guid.NewGuid())
                .Take(plan.AllowedPoolSize)
                .ToList();

            result.Add(new QuestionPlanPreviewDto
            {
                PlanId               = plan.Id,
                QuestionTypeId       = plan.QuestionTypeId,
                QuestionTypeName     = plan.QuestionType?.TypeName ?? string.Empty,
                RequiredQuestionCount = plan.RequiredQuestionCount,
                AllowedPoolSize      = plan.AllowedPoolSize,
                DisplayOrder         = plan.DisplayOrder,
                // The bank can hold fewer active questions of this type than the plan requires. Nothing
                // used to say so, and the shortfall only became visible when the panel ran out of
                // questions mid-interview.
                AvailableQuestionCount = available.Count,
                MeetsRequiredCount     = questions.Count >= plan.RequiredQuestionCount,
                Questions = questions.Select((q, i) => new QuestionPreviewItemDto
                {
                    QuestionDetailId = q.Id,
                    QuestionText     = q.QuestionText,
                    Weight           = q.Weight,
                    MinScore         = q.MinScore,
                    MaxScore         = q.MaxScore,
                    DisplayOrder     = i + 1,
                }).ToList(),
            });
        }

        return result;
    }

    public async Task CommitSelectedQuestionsAsync(
        Guid interviewId, CommitInterviewQuestionsDto dto, CancellationToken cancellationToken = default)
    {
        var interview = await GetOwnedInterviewAsync(interviewId);
        EnsureHr("save an interview's questions");

        var plans = (await _questionPlanRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == interview.TenantId)
            .ToList();

        // Validate every id BEFORE mutating anything: the commit clears each plan's selections first, so
        // a bad id half-way through used to leave the plan emptied and only partly rewritten. Same rule
        // as SetGoalRequiredSkillsAsync — validate the whole set, then write it.
        foreach (var planSel in dto.Plans)
        {
            var plan = plans.FirstOrDefault(p => p.Id == planSel.PlanId);
            if (plan is null) continue;

            var ids = planSel.QuestionDetailIds.Distinct().ToList();
            if (ids.Count != planSel.QuestionDetailIds.Count)
                throw new InvalidOperationException("A question cannot be selected twice in the same plan.");
            if (ids.Count == 0) continue;

            var found = (await _questionDetailRepository.FindAsync(q => ids.Contains(q.Id)))
                .Where(q => q.TenantId == interview.TenantId)
                .ToList();

            var missing = ids.Where(id => found.All(q => q.Id != id)).ToList();
            if (missing.Count > 0)
                throw new ArgumentException("One or more selected questions could not be found.");

            if (found.Any(q => q.QuestionTypeId != plan.QuestionTypeId))
                throw new InvalidOperationException(
                    "One or more selected questions belong to a different question type than the plan they were assigned to.");
        }

        foreach (var planSel in dto.Plans)
        {
            var plan = plans.FirstOrDefault(p => p.Id == planSel.PlanId);
            if (plan is null) continue;

            // Clear current selections
            var existing = (await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(plan.Id))
                .Where(sq => sq.TenantId == plan.TenantId)
                .ToList();
            foreach (var sq in existing)
                await _selectedQuestionRepository.DeleteAsync(sq);

            // Persist the user-confirmed questions in order
            for (int i = 0; i < planSel.QuestionDetailIds.Count; i++)
            {
                await _selectedQuestionRepository.AddAsync(new JobInterviewSelectedQuestion
                {
                    Id                     = Guid.NewGuid(),
                    TenantId               = plan.TenantId,
                    JobInterviewQuestionId = plan.Id,
                    QuestionDetailId       = planSel.QuestionDetailIds[i],
                    DisplayOrder           = i + 1,
                    CreatedBy              = "system",
                });
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ── Score summaries ───────────────────────────────────────────────────────

    public async Task<JobInterviewScoreSummaryDto> CreateScoreSummaryAsync(CreateJobInterviewScoreSummaryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var interviewee = await GetOwnedIntervieweeAsync(createDto.JobIntervieweeId);
        var scoreSource = await EnsureCanScoreAsAsync(
            createDto.JobIntervieweeId, createDto.InternalPanelistId, createDto.ExternalPanelistId);

        // 1. One scorecard per panelist per candidate. There is no update endpoint, so without this a
        //    corrected submission simply piled a second scorecard on top of the first and every
        //    aggregate double-counted that panelist. An unfinalised scorecard is replaced in place;
        //    a finalised one is the panelist's signed verdict and is refused.
        var existing = (await _scoreSummaryRepository.GetByIntervieweeIdAsync(createDto.JobIntervieweeId))
            .FirstOrDefault(s => s.TenantId == current
                && s.InternalPanelistId == createDto.InternalPanelistId
                && s.ExternalPanelistId == createDto.ExternalPanelistId);

        if (existing is { IsFinalized: true })
            throw new InvalidOperationException(
                "This panelist has already finalised a scorecard for this candidate. It cannot be replaced.");

        var entries = await BuildScoreEntriesAsync(
            interviewee, createDto.ScoreEntries, current, createdByUserId, cancellationToken);

        // Who filed it, recorded on every write — including a replacement, because the person who
        // corrects a scorecard is the person who is claiming it now. An upsert that kept the first
        // filer's provenance would say the panelist typed a card HR later rewrote.
        //
        // ⚠ `CallerEmployeeId`, not the `createdByUserId` parameter. They hold the same value today
        // — the controller passes `_currentUser.EmployeeId` into a parameter the whole family calls
        // `createdByUserId` — but the column is an EMPLOYEE reference and should be read from
        // something that says employee. Taking it from the misnamed parameter is how the next
        // refactor of that signature silently writes a user id into an employee FK.
        var filedOnBehalfOf = scoreSource == InterviewScoreSource.PaperSheet
            ? CallerEmployeeId
            : null;

        JobInterviewScoreSummary entity;
        if (existing is null)
        {
            entity = createDto.ToEntity(current, createdByUserId);
            entity.ScoreSource = scoreSource;
            entity.FiledByHrOnBehalfOfEmployeeId = filedOnBehalfOf;
            await _scoreSummaryRepository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            entity = existing;
            entity.Recommendation = createDto.Recommendation;
            entity.Comments       = createDto.Comments;
            entity.EvaluationDate = createDto.EvaluationDate;
            entity.ScoreSource    = scoreSource;
            entity.FiledByHrOnBehalfOfEmployeeId = filedOnBehalfOf;
            entity.UpdatedAt      = DateTime.UtcNow;
            entity.UpdatedBy      = createdByUserId.ToString();

            foreach (var stale in (await _scoreEntryRepository.GetBySummaryIdAsync(entity.Id))
                     .Where(e => e.TenantId == current))
                await _scoreEntryRepository.DeleteAsync(stale);
        }

        // 2. Persist score entries with server-computed WeightedScore.
        foreach (var e in entries)
            e.ScoreSummaryId = entity.Id;

        if (entries.Count > 0)
            await _scoreEntryRepository.AddRangeAsync(entries);

        // 3. Refresh summary totals from the entries just written.
        entity.TotalRawScore      = entries.Sum(e => e.RawScore);
        entity.TotalWeightedScore = entries.Sum(e => e.WeightedScore);
        await _scoreSummaryRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _scoreSummaryRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    /// <summary>
    /// Validates a scorecard's entries against the interview's own question set and computes the
    /// weighted score for each. Formula: (RawScore / MaxScore) × Weight — normalises for score-range
    /// differences between questions.
    /// </summary>
    private async Task<List<JobInterviewScoreEntry>> BuildScoreEntriesAsync(
        JobInterviewee interviewee,
        List<CreateJobInterviewScoreEntryDto> scoreEntries,
        Guid tenantId,
        Guid createdByUserId,
        CancellationToken cancellationToken)
    {
        if (scoreEntries.Count == 0) return new List<JobInterviewScoreEntry>();

        var questionIds = scoreEntries.Select(e => e.QuestionDetailId).ToList();
        if (questionIds.Distinct().Count() != questionIds.Count)
            throw new InvalidOperationException("A question cannot be scored twice on the same scorecard.");

        // The questions must be the ones this interview actually locked in. Previously any question id
        // was accepted — including another tenant's, which silently scored 0 because the weight lookup
        // missed, and questions this panel never asked.
        var selectedIds = await GetSelectedQuestionIdsAsync(interviewee.JobInterviewId, tenantId);
        var stray = questionIds.Where(id => !selectedIds.Contains(id)).ToList();
        if (stray.Count > 0)
            throw new InvalidOperationException(
                "One or more scored questions are not part of this interview's question set.");

        var questions = (await _questionDetailRepository.FindAsync(q => questionIds.Contains(q.Id)))
            .Where(q => q.TenantId == tenantId)
            .ToDictionary(q => q.Id);

        return scoreEntries
            .Select(e =>
            {
                var q = questions[e.QuestionDetailId];

                // The raw score has to sit inside the question's own band. The DTO allows 0–100 while a
                // question is typically scored 1–10, and (raw / max) × weight then produced a weighted
                // score many times the question's ceiling — one out-of-range entry could outweigh the
                // whole rest of the scorecard.
                if (e.RawScore < q.MinScore || e.RawScore > q.MaxScore)
                    throw new InvalidOperationException(
                        $"Score {e.RawScore} for \"{q.QuestionText}\" is outside its {q.MinScore}–{q.MaxScore} range.");

                var weighted = q.MaxScore > 0
                    ? Math.Round((e.RawScore / q.MaxScore) * q.Weight, 4)
                    : 0m;

                return new JobInterviewScoreEntry
                {
                    TenantId         = tenantId,
                    QuestionDetailId = e.QuestionDetailId,
                    RawScore         = e.RawScore,
                    WeightedScore    = weighted,
                    Remarks          = e.Remarks,
                    CreatedBy        = createdByUserId.ToString(),
                };
            })
            .ToList();
    }

    /// <summary>Every question locked into an interview's plans, across all of them.</summary>
    private async Task<HashSet<Guid>> GetSelectedQuestionIdsAsync(Guid interviewId, Guid tenantId)
    {
        var plans = (await _questionPlanRepository.GetByInterviewIdAsync(interviewId))
            .Where(p => p.TenantId == tenantId)
            .ToList();

        var ids = new HashSet<Guid>();
        foreach (var plan in plans)
        {
            var selected = await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(plan.Id);
            foreach (var sq in selected.Where(s => s.TenantId == tenantId))
                ids.Add(sq.QuestionDetailId);
        }
        return ids;
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoreSummariesAsync(Guid intervieweeId, CancellationToken cancellationToken = default)
    {
        var interviewee = await GetOwnedIntervieweeAsync(intervieweeId);
        await EnsureCanReadInterviewAsync(interviewee.JobInterviewId);
        var tenantId = GetTenantId();
        var entities = (await _scoreSummaryRepository.GetByIntervieweeIdAsync(intervieweeId))
            .Where(e => e.TenantId == tenantId)
            .ToList();

        var visible = await ApplyBlindScoringAsync(interviewee.JobInterviewId, entities);
        return visible.Select(e => e.ToDto());
    }

    public async Task<JobInterviewScoreSummaryDetailDto> GetScoreSummaryDetailAsync(Guid scoreSummaryId, CancellationToken cancellationToken = default)
    {
        var entity = await _scoreSummaryRepository.GetWithScoreEntriesAsync(scoreSummaryId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Score summary with ID '{scoreSummaryId}' not found.");

        var interviewee = await GetOwnedIntervieweeAsync(entity.JobIntervieweeId);
        await EnsureCanReadInterviewAsync(interviewee.JobInterviewId);
        await EnsureCanSeeScoreCardAsync(interviewee.JobInterviewId, entity);
        return entity.ToDetailDto();
    }

    public async Task<bool> FinalizeScoreAsync(Guid scoreSummaryId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedScoreSummaryAsync(scoreSummaryId);
        await EnsureCanScoreAsAsync(entity.JobIntervieweeId, entity.InternalPanelistId, entity.ExternalPanelistId);

        if (entity.IsFinalized)
            throw new InvalidOperationException("This scorecard is already finalised.");

        await EnsureScorecardCoversRequiredQuestionsAsync(entity, cancellationToken);

        entity.IsFinalized = true;
        entity.FinalizedDate = DateTime.UtcNow;

        await _scoreSummaryRepository.UpdateAsync(entity);

        // The draft is a scratchpad for a scorecard that has now been signed off. Leaving it behind
        // means the scoring screen reloads the half-finished version over the submitted one.
        var draft = await _draftRepository.GetByPanelistAsync(
            entity.JobIntervieweeId, entity.InternalPanelistId, entity.ExternalPanelistId, cancellationToken);
        if (draft is not null && draft.TenantId == entity.TenantId)
            await _draftRepository.DeleteAsync(draft);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// <c>RequiredQuestionCount</c> is documented on the entity as "minimum number of questions from
    /// this type that must be asked" — and was read by nothing, so a plan could demand five competency
    /// questions and be signed off with one. Finalisation is where it bites: a draft may be as
    /// incomplete as the panelist likes, a signed scorecard may not.
    /// </summary>
    private async Task EnsureScorecardCoversRequiredQuestionsAsync(
        JobInterviewScoreSummary summary, CancellationToken cancellationToken)
    {
        var interviewee = await GetOwnedIntervieweeAsync(summary.JobIntervieweeId);
        var plans = (await _questionPlanRepository.GetByInterviewIdAsync(interviewee.JobInterviewId))
            .Where(p => p.TenantId == summary.TenantId)
            .ToList();
        if (plans.Count == 0) return;   // an unstructured interview has nothing to require

        var scored = (await _scoreEntryRepository.GetBySummaryIdAsync(summary.Id))
            .Where(e => e.TenantId == summary.TenantId)
            .Select(e => e.QuestionDetailId)
            .ToHashSet();

        foreach (var plan in plans)
        {
            var selected = (await _selectedQuestionRepository.GetByInterviewQuestionIdAsync(plan.Id))
                .Where(sq => sq.TenantId == summary.TenantId)
                .Select(sq => sq.QuestionDetailId)
                .ToList();

            // Never demand more than the bank could actually supply — that would make the scorecard
            // unfinalisable through no fault of the panelist.
            var required = Math.Min(plan.RequiredQuestionCount, selected.Count);
            var answered = selected.Count(scored.Contains);

            if (answered < required)
                throw new InvalidOperationException(
                    $"\"{plan.QuestionType?.TypeName ?? "This section"}\" requires at least {required} " +
                    $"scored question(s); {answered} were scored.");
        }
    }

    /// <summary>The caller's own panel assignments — the diary a panelist opens to find their sessions.</summary>
    public async Task<IEnumerable<JobInterviewPanelistDto>> GetMyPanelistSlotsAsync(CancellationToken cancellationToken = default)
    {
        var employeeId = CallerEmployeeId;
        if (employeeId is null || employeeId == Guid.Empty)
            throw new UnauthorizedAccessException("Your user account is not linked to an employee record.");

        var tenantId = GetTenantId();
        var entities = await _panelistRepository.GetByEmployeeIdAsync(employeeId.Value);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    /// <summary>
    /// The caller's own scorecard worklist — each session they sit on, the candidates on it, and how
    /// far their own card for each has got (round 4, lane F5).
    /// </summary>
    /// <remarks>
    /// <para><b>Why this read exists.</b> A panelist's only route to a scorecard ran through HR's
    /// interview desk screen and its Candidates tab, and the portal diary listed interview
    /// <i>numbers</i> while describing itself as "the scorecards you owe". This is what lets the
    /// portal keep that promise.</para>
    ///
    /// <para>⚠ <b>Only the caller's own cards.</b> A colleague's mark never appears here, even for
    /// a candidate the caller has already filed for and could therefore see elsewhere. A worklist
    /// showing somebody else's answer is the anchoring problem in a different shape.</para>
    ///
    /// <para>⚠ <b>Three queries, not three per session.</b> The seats, then every card across those
    /// seats, then which of them hold a draft — round 4 § 3 defect 10 is a per-row repository call
    /// made inside a loop in this very service.</para>
    /// </remarks>
    public async Task<IEnumerable<PanelistScorecardWorklistDto>> GetMyScorecardWorklistAsync(
        CancellationToken cancellationToken = default)
    {
        var employeeId = CallerEmployeeId;
        if (employeeId is null || employeeId == Guid.Empty)
            throw new UnauthorizedAccessException("Your user account is not linked to an employee record.");

        var tenantId = GetTenantId();

        var seats = (await _panelistRepository.GetWorklistByEmployeeIdAsync(employeeId.Value))
            .Where(p => p.TenantId == tenantId && p.JobInterview is not null && !p.JobInterview.IsDeleted)
            .ToList();
        if (seats.Count == 0) return Array.Empty<PanelistScorecardWorklistDto>();

        var seatIds = seats.Select(p => p.Id).ToList();

        var cards = (await _scoreSummaryRepository.GetByInternalPanelistIdsAsync(seatIds))
            .Where(c => c.TenantId == tenantId)
            .ToList();
        // One card per (seat, candidate) — the create path upserts on exactly that pair.
        var cardBySeatAndCandidate = cards
            .GroupBy(c => (Seat: c.InternalPanelistId!.Value, Candidate: c.JobIntervieweeId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt).First());

        var draftedFor = (await _draftRepository
                .GetIntervieweeIdsWithDraftAsync(seatIds, cancellationToken))
            .ToHashSet();

        var worklist = new List<PanelistScorecardWorklistDto>();
        foreach (var seat in seats)
        {
            var interview = seat.JobInterview!;

            var candidates = (interview.Interviewees ?? new List<JobInterviewee>())
                .Where(ie => !ie.IsDeleted && ie.TenantId == tenantId)
                // Slotted candidates first and in time order, then the unslotted — the order the
                // panel will actually see people, which is the only order that helps in the room.
                .OrderBy(ie => ie.SlotStartTime.HasValue ? 0 : 1)
                .ThenBy(ie => ie.SlotStartTime ?? TimeSpan.Zero)
                .ThenBy(ie => ie.JobApplication?.JobCandidate?.FullName)
                .Select(ie =>
                {
                    cardBySeatAndCandidate.TryGetValue((seat.Id, ie.Id), out var card);
                    return new PanelistScorecardCandidateDto
                    {
                        IntervieweeId = ie.Id,
                        JobApplicationId = ie.JobApplicationId,
                        CandidateName = ie.JobApplication?.JobCandidate?.FullName ?? "Candidate",
                        ApplicationNumber = ie.JobApplication?.ApplicationNumber ?? string.Empty,
                        SlotStartTime = ie.SlotStartTime,
                        SlotEndTime = ie.SlotEndTime,
                        CandidateAttended = ie.CandidateAttended,
                        ScoreSummaryId = card?.Id,
                        TotalWeightedScore = card?.TotalWeightedScore,
                        Recommendation = card?.Recommendation,
                        // ⚠ A draft is NOT a filed card: it is private, it does not unblind the
                        // panel, and it leaves the scorecard still owed. Collapsing the two would
                        // let a panelist's own to-do list tell them they were finished.
                        State = card is null
                            ? (draftedFor.Contains(ie.Id)
                                ? PanelistScorecardState.Draft
                                : PanelistScorecardState.NotStarted)
                            : card.IsFinalized
                                ? PanelistScorecardState.SignedOff
                                : PanelistScorecardState.Saved,
                    };
                })
                .ToList();

            worklist.Add(new PanelistScorecardWorklistDto
            {
                InterviewId = interview.Id,
                InterviewNumber = interview.InterviewNumber,
                JobTitle = interview.JobVacancy?.JobTitle ?? "Interview",
                VacancyNumber = interview.JobVacancy?.VacancyNumber ?? string.Empty,
                Round = interview.Round,
                Type = interview.Type,
                Mode = interview.Mode,
                Status = interview.Status,
                ScheduledDate = interview.ScheduledDate,
                StartTime = interview.StartTime,
                EndTime = interview.EndTime,
                LocationOrLink = interview.LocationOrLink,
                PanelistId = seat.Id,
                Role = seat.Role,
                IsRequired = seat.IsRequired,
                IsConfirmed = seat.IsConfirmed,
                HasQuestionPlan = (interview.Questions ?? new List<JobInterviewQuestion>())
                    .Any(q => !q.IsDeleted),
                Candidates = candidates,
            });
        }

        return worklist;
    }

    public async Task<IEnumerable<JobInterviewPanelistDto>> GetInterviewsByPanelistAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        // Reading someone else's interview diary is HR's; reading your own goes through /me above, which
        // takes the employee from the token rather than the URL.
        if (employeeId != CallerEmployeeId)
            EnsureHr("view another employee's interview assignments");

        var tenantId = GetTenantId();
        var entities = await _panelistRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobIntervieweeDto>> GetInterviewsByApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        EnsureHr("view an application's interview history");
        var tenantId = GetTenantId();
        var entities = await _intervieweeRepository.GetByApplicationIdAsync(applicationId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoresByInternalPanelistAsync(Guid panelistId, CancellationToken cancellationToken = default)
    {
        var panelist = await GetOwnedPanelistAsync(panelistId);
        if (!IsHr && panelist.EmployeeId != CallerEmployeeId)
            throw new UnauthorizedAccessException("You can only view your own scorecards.");

        var tenantId = GetTenantId();
        var entities = await _scoreSummaryRepository.GetByInternalPanelistIdAsync(panelistId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoresByExternalPanelistAsync(Guid externalPanelistId, CancellationToken cancellationToken = default)
    {
        await GetOwnedExternalPanelistAsync(externalPanelistId);
        EnsureHr("view an external panelist's scorecards");
        var tenantId = GetTenantId();
        var entities = await _scoreSummaryRepository.GetByExternalPanelistIdAsync(externalPanelistId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreSummaryDto>> GetFinalizedScoresForIntervieweeAsync(Guid intervieweeId, CancellationToken cancellationToken = default)
    {
        var interviewee = await GetOwnedIntervieweeAsync(intervieweeId);
        await EnsureCanReadInterviewAsync(interviewee.JobInterviewId);
        var tenantId = GetTenantId();
        // ⚠ Blinded too. "Finalized" is not a lesser read — a signed-off card is the strongest
        // anchor there is, and this endpoint would otherwise be the way round the rule.
        var entities = (await _scoreSummaryRepository.GetFinalizedForIntervieweeAsync(intervieweeId))
            .Where(e => e.TenantId == tenantId)
            .ToList();

        var visible = await ApplyBlindScoringAsync(interviewee.JobInterviewId, entities);
        return visible.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<JobInterviewScoreEntryDto>> GetScoreEntriesAsync(Guid scoreSummaryId, CancellationToken cancellationToken = default)
    {
        var summary = await GetOwnedScoreSummaryAsync(scoreSummaryId);
        var interviewee = await GetOwnedIntervieweeAsync(summary.JobIntervieweeId);
        await EnsureCanReadInterviewAsync(interviewee.JobInterviewId);
        await EnsureCanSeeScoreCardAsync(interviewee.JobInterviewId, summary);
        var entities = await _scoreEntryRepository.GetBySummaryIdAsync(scoreSummaryId);
        return entities.Where(e => e.TenantId == summary.TenantId).Select(e => e.ToDto());
    }

    // ── Score drafts ──────────────────────────────────────────────────────────

    public async Task<InterviewScoreDraftDto?> GetScoreDraftAsync(
        Guid intervieweeId,
        Guid? internalPanelistId,
        Guid? externalPanelistId,
        CancellationToken cancellationToken = default)
    {
        // A draft is one panelist's unfinished thinking about a candidate. The panelist used to be named
        // in the query string, so passing a colleague's id read their draft — comments and all.
        await EnsureCanScoreAsAsync(intervieweeId, internalPanelistId, externalPanelistId);

        var tenantId = GetTenantId();
        var entity = await _draftRepository.GetByPanelistAsync(
            intervieweeId, internalPanelistId, externalPanelistId, cancellationToken);
        return entity != null && entity.TenantId == tenantId ? entity.ToDraftDto() : null;
    }

    public async Task<InterviewScoreDraftDto> SaveScoreDraftAsync(
        Guid interviewId,
        SaveInterviewScoreDraftDto dto,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedInterviewAsync(interviewId);
        var interviewee = await GetOwnedIntervieweeAsync(dto.JobIntervieweeId);
        await EnsureCanScoreAsAsync(dto.JobIntervieweeId, dto.InternalPanelistId, dto.ExternalPanelistId);

        // The route's interview and the candidate's interview have to be the same one, or the draft is
        // filed against a session the candidate never sat in.
        if (interviewee.JobInterviewId != interviewId)
            throw new InvalidOperationException("That candidate is not booked into this interview.");

        var existing = await _draftRepository.GetByPanelistAsync(
            dto.JobIntervieweeId, dto.InternalPanelistId, dto.ExternalPanelistId, cancellationToken);
        if (existing != null && existing.TenantId != current)
            existing = null;

        var entriesJson = System.Text.Json.JsonSerializer.Serialize(dto.ScoreEntries);

        if (existing is null)
        {
            existing = new JobInterviewScoreDraft
            {
                TenantId            = current,
                JobInterviewId      = interviewId,
                JobIntervieweeId    = dto.JobIntervieweeId,
                InternalPanelistId  = dto.InternalPanelistId,
                ExternalPanelistId  = dto.ExternalPanelistId,
                DraftJson           = entriesJson,
                Comments            = dto.Comments,
                Recommendation      = dto.Recommendation,
                LastModified        = DateTime.UtcNow
            };
            await _draftRepository.AddAsync(existing);
        }
        else
        {
            existing.DraftJson       = entriesJson;
            existing.Comments        = dto.Comments;
            existing.Recommendation  = dto.Recommendation;
            existing.LastModified    = DateTime.UtcNow;
            await _draftRepository.UpdateAsync(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return existing.ToDraftDto();
    }

    // ── Validation + calendar-invite helpers ───────────────────────────────────

    /// <summary>Validates slot sanity plus that all applications belong to the interview's vacancy.</summary>
    private async Task ValidateScheduleAsync(
        Guid vacancyId, DateOnly date, TimeSpan start, TimeSpan end,
        List<Guid>? applicationIds, CancellationToken cancellationToken)
    {
        ValidateSlot(date, start, end, "scheduled in the past");
        await ValidateApplicationsForVacancyAsync(vacancyId, applicationIds, GetTenantId());
    }

    /// <summary>
    /// Every application booked into an interview must exist, be in-tenant, and be an application for
    /// the interview's own vacancy. Factored out of <c>ValidateScheduleAsync</c> so the add-interviewee
    /// path enforces the same rule — it is the same table, reached by a different route.
    /// </summary>
    private async Task ValidateApplicationsForVacancyAsync(
        Guid vacancyId, List<Guid>? applicationIds, Guid tenantId)
    {
        if (applicationIds is not { Count: > 0 }) return;

        var appIds = applicationIds.Distinct().ToList();
        var apps = (await _applicationRepository.FindAsync(a => appIds.Contains(a.Id)))
            .Where(a => a.TenantId == tenantId)
            .ToList();

        var missing = appIds.Where(id => apps.All(a => a.Id != id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException("One or more selected applications could not be found.");

        if (apps.Any(a => a.JobVacancyId != vacancyId))
            throw new InvalidOperationException("One or more selected applications do not belong to the selected vacancy.");
    }

    /// <summary>
    /// The panel named on a create payload gets the same checks the add-panelist endpoints now make.
    /// Without them an unknown employee id died on a foreign-key violation <i>after</i> the interview
    /// row had already been committed, leaving a half-built interview behind.
    /// </summary>
    private async Task ValidatePanelAsync(
        List<Guid>? panelistEmployeeIds, List<Guid>? externalAssociateIds, Guid tenantId)
    {
        if (panelistEmployeeIds is { Count: > 0 })
        {
            var ids = panelistEmployeeIds.Distinct().ToList();
            if (ids.Count != panelistEmployeeIds.Count)
                throw new InvalidOperationException("The same employee cannot be added to the panel twice.");

            var found = (await _employeeRepository.FindAsync(e => ids.Contains(e.Id)))
                .Where(e => e.TenantId == tenantId)
                .ToList();
            if (found.Count != ids.Count)
                throw new InvalidOperationException("One or more selected panelists could not be found.");
        }

        if (externalAssociateIds is { Count: > 0 })
        {
            var ids = externalAssociateIds.Distinct().ToList();
            if (ids.Count != externalAssociateIds.Count)
                throw new InvalidOperationException("The same associate cannot be added to the panel twice.");

            foreach (var id in ids)
                await EnsureAssociateInTenantAsync(id, tenantId);
        }
    }

    private async Task EnsureEmployeeInTenantAsync(Guid employeeId, Guid tenantId)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != tenantId || employee.IsDeleted)
            throw new ArgumentException($"Employee with ID '{employeeId}' not found.");
    }

    private async Task EnsureAssociateInTenantAsync(Guid associateId, Guid tenantId)
    {
        var associate = await _associateRepository.GetByIdAsync(associateId);
        if (associate == null || associate.TenantId != tenantId || associate.IsDeleted)
            throw new ArgumentException($"External associate with ID '{associateId}' not found.");
    }

    private async Task EnsureQuestionTypeInTenantAsync(Guid questionTypeId, Guid tenantId)
    {
        var type = await _questionTypeRepository.GetByIdAsync(questionTypeId);
        if (type == null || type.TenantId != tenantId || type.IsDeleted)
            throw new ArgumentException($"Interview question type with ID '{questionTypeId}' not found.");
    }

    /// <summary>Shared slot sanity checks: end after start, and not in the past.</summary>
    private static void ValidateSlot(DateOnly date, TimeSpan start, TimeSpan end, string pastPhrase)
    {
        if (end <= start)
            throw new InvalidOperationException("Interview end time must be after the start time.");
        if (date < DateOnly.FromDateTime(DateTime.Today))
            throw new InvalidOperationException($"Interview cannot be {pastPhrase}.");
    }

    private static bool IsTokenExpired(DateTime? expiresAt) => expiresAt.HasValue && expiresAt.Value < DateTime.UtcNow;

    /// <summary>Confirmation links stay valid until the end of the interview day.</summary>
    private static DateTime TokenExpiryFor(JobInterview interview) =>
        interview.ScheduledDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// The interview's calendar file, through the builder company events share (company-schedule final closure,
    /// lane 2e-3, D-14).
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>One UID per interview, for ever.</b> It was random on every email, so a rescheduled interview
    /// arrived as a second calendar entry beside the first. Now a move replaces the entry. The interview stores no
    /// change counter, so SEQUENCE stays 0 and the newer DTSTAMP wins, as RFC 5546 allows (the user's ruling: no schema
    /// change for recruitment).</para>
    ///
    /// <para>Times are UTC: TDC works on GMT, and the interview's day-clock is that. They were floating, which a client
    /// in another zone read as its own local time. No ORGANIZER, as before: an interview names no organiser to answer.</para>
    /// </remarks>
    private static EmailAttachmentDto BuildInterviewIcs(
        JobInterview interview, string summary, string? attendeeEmail, TimeSpan? slotStart, TimeSpan? slotEnd)
    {
        var startTod = slotStart ?? interview.StartTime;
        var endTod   = slotEnd   ?? interview.EndTime;
        var location = string.IsNullOrWhiteSpace(interview.LocationOrLink) ? "To be advised" : interview.LocationOrLink;

        return HrCalendarFile.Build(new HrCalendarEntry
        {
            Uid = $"interview-{interview.Id:N}@rhema-erp",
            Summary = summary,
            Description = $"Interview reference {interview.InterviewNumber}",
            Location = location,
            StartUtc = interview.ScheduledDate.ToDateTime(TimeOnly.FromTimeSpan(startTod)),
            EndUtc = interview.ScheduledDate.ToDateTime(TimeOnly.FromTimeSpan(endTod)),
            Attendee = string.IsNullOrWhiteSpace(attendeeEmail) ? null : new HrCalendarPerson(attendeeEmail),
            RsvpRequested = true,
        }, $"interview-{interview.InterviewNumber}.ics");
    }

    private async Task SendInterviewEmailAsync(
        string toEmail, string candidateName, JobInterview interview, bool isReschedule, string confirmToken,
        TimeSpan? slotStart = null, TimeSpan? slotEnd = null)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;
        var jobTitle   = interview.JobVacancy?.JobTitle ?? "the position";
        var dateStr    = interview.ScheduledDate.ToString("dddd, d MMMM yyyy");
        var startStr   = DateTime.Today.Add(interview.StartTime).ToString("hh:mm tt");
        var endStr     = DateTime.Today.Add(interview.EndTime).ToString("hh:mm tt");
        var location   = string.IsNullOrWhiteSpace(interview.LocationOrLink) ? "To be advised" : interview.LocationOrLink;
        var typeLabel  = interview.Type.ToString().Replace("Interview", " Interview").Trim();
        var roundLabel = $"Round {interview.Round}";

        // Per-candidate slot time: show the candidate's window if set, otherwise fall back to the session time
        var hasSlot      = slotStart.HasValue || slotEnd.HasValue;
        var timeDisplay  = hasSlot
            ? $"{(slotStart.HasValue ? DateTime.Today.Add(slotStart.Value).ToString("hh:mm tt") : "—")} – {(slotEnd.HasValue ? DateTime.Today.Add(slotEnd.Value).ToString("hh:mm tt") : "—")}"
            : $"{startStr} – {endStr}";
        // The session window is shown as a secondary row only when a personal slot is set
        var sessionWindow = hasSlot ? $"{startStr} – {endStr}" : string.Empty;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"]   = candidateName,
            ["JobTitle"]        = jobTitle,
            ["InterviewNumber"] = interview.InterviewNumber,
            ["Date"]            = dateStr,
            ["TimeSlot"]        = timeDisplay,
            ["SessionWindow"]   = sessionWindow,
            ["Format"]          = $"{typeLabel} ({roundLabel})",
            ["Location"]        = location,
            ["ConfirmToken"]    = confirmToken,
        };
        if (isReschedule) tokens["RescheduleReason"] = interview.RescheduleReason;

        var eventKey = isReschedule
            ? RecruitmentEmailCatalog.Events.InterviewRescheduled
            : RecruitmentEmailCatalog.Events.InterviewInvitation;

        var ics = BuildInterviewIcs(interview, $"Interview — {jobTitle}", toEmail, slotStart, slotEnd);
        await _templatedEmail.SendAsync(RecruitmentEmailCatalog.Module, eventKey, toEmail, tokens, new[] { ics });
    }

    private async Task SendPanelistEmailAsync(
        string toEmail, string panelistName, JobInterview interview, string? confirmToken = null)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var jobTitle   = interview.JobVacancy?.JobTitle ?? "the position";
        var dateStr    = interview.ScheduledDate.ToString("dddd, d MMMM yyyy");
        var startStr   = DateTime.Today.Add(interview.StartTime).ToString("hh:mm tt");
        var endStr     = DateTime.Today.Add(interview.EndTime).ToString("hh:mm tt");
        var location   = string.IsNullOrWhiteSpace(interview.LocationOrLink) ? "To be advised" : interview.LocationOrLink;
        var typeLabel  = interview.Type.ToString().Replace("Interview", " Interview").Trim();
        var roundLabel = $"Round {interview.Round}";

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PanelistName"]    = panelistName,
            ["JobTitle"]        = jobTitle,
            ["InterviewNumber"] = interview.InterviewNumber,
            ["Date"]            = dateStr,
            ["Time"]            = $"{startStr} – {endStr}",
            ["Format"]          = $"{typeLabel} ({roundLabel})",
            ["Location"]        = location,
            ["Instructions"]    = interview.Instructions,
            ["ConfirmToken"]    = confirmToken,
        };

        // The panelist path historically propagated send failures to its caller — preserve that by
        // throwing when the templated send reports failure.
        var ics = BuildInterviewIcs(interview, $"Interview panel — {jobTitle}", toEmail, slotStart: null, slotEnd: null);
        var sent = await _templatedEmail.SendAsync(
            RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.InterviewPanelistAssignment, toEmail, tokens, new[] { ics });
        if (!sent)
            throw new InvalidOperationException($"Failed to send panelist notification email to {toEmail}.");
    }
}
