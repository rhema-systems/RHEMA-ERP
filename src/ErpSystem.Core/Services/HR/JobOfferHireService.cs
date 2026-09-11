using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// JOB OFFER SERVICE
// ============================================================================

public class JobOfferService : IJobOfferService
{
    private readonly IJobOfferRepository _offerRepository;
    private readonly IJobOfferBenefitRepository _benefitRepository;
    private readonly IJobOfferNoteRepository _noteRepository;
    private readonly IApplicationPipelineService _pipelineService;
    private readonly IJobApplicationRepository _applicationRepository;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPositionNamedSetService _namedSets;
    private readonly ILogger<JobOfferService> _logger;
    private readonly IEmailService _email;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly string _portalBaseUrl;

    public JobOfferService(
        IJobOfferRepository offerRepository,
        IJobOfferBenefitRepository benefitRepository,
        IJobOfferNoteRepository noteRepository,
        IApplicationPipelineService pipelineService,
        IJobApplicationRepository applicationRepository,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        IPositionNamedSetService namedSets,
        ILogger<JobOfferService> logger,
        IEmailService email,
        ITemplatedEmailService templatedEmail,
        IConfiguration configuration)
    {
        _offerRepository         = offerRepository;
        _benefitRepository       = benefitRepository;
        _noteRepository          = noteRepository;
        _pipelineService         = pipelineService;
        _applicationRepository   = applicationRepository;
        _workflowIntegrationService    = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currentUserProvider     = currentUserProvider;
        _unitOfWork              = unitOfWork;
        _namedSets               = namedSets;
        _logger                  = logger;
        _email                   = email;
        _templatedEmail          = templatedEmail;
        // No localhost fallback: this URL goes into offer emails sent to real candidates. A missing
        // config value must fail at startup, not silently mail every candidate a link to localhost.
        _portalBaseUrl           = configuration["CandidatePortal:PortalUrl"]
            ?? throw new InvalidOperationException(
                "CandidatePortal:PortalUrl is not configured. It is required to build the offer-response " +
                "links emailed to candidates.");
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

    // An offer owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<JobOffer> GetOwnedOfferAsync(Guid id)
    {
        var entity = await _offerRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Job offer with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobOfferBenefit> GetOwnedBenefitAsync(Guid id)
    {
        var entity = await _benefitRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Job offer benefit '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// The proposed base salary must sit inside the position's grade band.
    ///
    /// <para>Factored out because the rule was written three times over and enforced in only one of
    /// them: create checked it, update checked the <i>pre-update</i> value, and revise — the path
    /// whose entire purpose is to change the salary after a counter-offer — never checked at all.
    /// A band is only a band if every write path respects it.</para>
    ///
    /// <para>Silent when the position carries no grade: an unbanded position constrains nothing.</para>
    ///
    /// <para>⚠ <b>Also silent when the grade exists but its band is unset (max ≤ 0), or is inverted.</b>
    /// That is not a technicality. Salary grades are owned by payroll and mirrored into HR, and in
    /// this deployment every one of them currently carries <c>0 – 0</c>. Treating an unconfigured
    /// band as a constraint rather than as an absence made the rule read "no salary is permissible",
    /// which refused <i>every offer in the tenant</i> — the whole feature was unusable, and the
    /// message blamed the salary rather than the missing configuration. An absent band means the
    /// grade has nothing to say about the number, not that the number is wrong.</para>
    /// </summary>
    private static void EnsureSalaryWithinBand(JobOffer offer)
    {
        if (!offer.BaseSalary.HasValue) return;

        var min = offer.SalaryGradeMin ?? 0m;
        var max = offer.SalaryGradeMax ?? 0m;

        // No usable band: unset, or configured back-to-front. Either way it constrains nothing.
        if (max <= 0m || max < min) return;

        if (offer.BaseSalary < min || offer.BaseSalary > max)
            throw new InvalidOperationException(
                $"Proposed base salary {offer.BaseSalary:N2} is outside the salary band " +
                $"({min:N2} – {max:N2}) for this position.");
    }

    private async Task<string> GenerateOfferNumberAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // ⚠ Counts SOFT-DELETED rows too, via the including-deleted overload — plain GetQueryable()
        // filters them out. Excluding them makes the sequence reuse a number the moment anything is
        // deleted, and JobOffers carries a UNIQUE index on (TenantId, OfferNumber) that a soft delete does
        // not release: deleting one draft made the very next create die on a duplicate-key violation,
        // surfaced as a 500 with raw SQL in it. A reference number is an identifier, not a slot —
        // once issued it is spent.
        var last = await _offerRepository.GetQueryableIncludingDeleted(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.OfferNumber)
            .Select(o => o.OfferNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (last != null && int.TryParse(last.Replace("OFR-", ""), out var parsed))
            next = parsed + 1;

        return $"OFR-{next:D6}";
    }

    public async Task<JobOfferDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(id);
        return entity.ToDto();
    }

    public async Task<JobOfferDto?> GetByOfferNumberAsync(string offerNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _offerRepository.GetByOfferNumberAsync(offerNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<JobOfferDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Job offer with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<JobOfferDto?> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _offerRepository.GetByApplicationIdAsync(applicationId);
        return entities.Where(e => e.TenantId == tenantId).OrderByDescending(e => e.CreatedAt).FirstOrDefault()?.ToDto();
    }

    public async Task<IEnumerable<JobOfferSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _offerRepository.GetAllForSummaryAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobOfferSummaryDto>> GetByStatusAsync(JobOfferStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _offerRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobOfferSummaryDto>> GetExpiringOffersAsync(int daysAhead = 7, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _offerRepository.GetExpiringOffersAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<JobOfferDto> CreateAsync(CreateJobOfferDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // Load application with position data needed for seeding the offer
        var application = await _applicationRepository.GetForOfferSeedingAsync(createDto.JobApplicationId);
        if (application == null || application.TenantId != current)
            throw new ArgumentException($"Application '{createDto.JobApplicationId}' not found.");

        var vacancy  = application.JobVacancy;
        var position = vacancy?.Position;

        // ⚠ These were `if (x != null)` guards that silently skipped the snapshot. That was survivable
        // only because the client also sent PositionId/PositionTitle/ReportsToTitle/GradeTitle/
        // EmploymentType and the entity kept those. Those fields are off the payload now — they were
        // required of the caller and then always discarded — so an unresolved vacancy or position
        // would write PositionId = Guid.Empty and die on a foreign-key violation with nothing to
        // explain it. A vacancy's PositionId is non-nullable and the seeding query includes the
        // chain, so this cannot happen in practice; if it ever does, it means the query changed and
        // that deserves to be said out loud rather than half-written.
        if (vacancy is null)
            throw new InvalidOperationException(
                "This application is not linked to a vacancy, so an offer cannot be raised from it.");
        if (position is null)
            throw new InvalidOperationException(
                "The vacancy behind this application has no position, so the offer has no role to describe.");

        // Build entity from the client-supplied negotiated fields
        var entity = createDto.ToEntity(current, createdByUserId);

        // --- Position snapshot: always server-authoritative, never client-supplied ---
        entity.PositionId            = position.Id;
        entity.PositionTitle         = position.Title;
        entity.ReportsToTitle        = position.ReportsToPosition?.Title ?? string.Empty;
        entity.GradeTitle            = position.SalaryGrade?.Name ?? string.Empty;
        entity.DepartmentName        = position.OrganizationUnit?.Name ?? string.Empty;
        entity.SalaryGradeMin        = position.SalaryGrade?.MinSalary;
        entity.SalaryGradeMax        = position.SalaryGrade?.MaxSalary;
        // HR override from the payload wins; fall back to the position's default
        entity.ProbationPeriodMonths = createDto.ProbationPeriodMonths ?? position.ProbationPeriodMonths;
        entity.NoticePeriodMonths    = createDto.NoticePeriodMonths    ?? position.NoticePeriodMonths;

        entity.WorkMode       = vacancy.WorkMode;
        entity.EmploymentType = vacancy.EmploymentType;

        // Default weekly hours: 20 for part-time, 40 for all other employment types
        entity.WeeklyHours ??= entity.EmploymentType == EmploymentType.PartTime ? 20m : 40m;

        // --- Salary within-band validation ---
        EnsureSalaryWithinBand(entity);

        entity.OfferNumber = await GenerateOfferNumberAsync(cancellationToken);
        entity.OfferStatus = JobOfferStatus.Draft;

        await _offerRepository.AddAsync(entity);

        // --- Seed JobOfferBenefit records from the position's EFFECTIVE benefits ---
        // ⚠ Round 2, lane C3. This read the position's individual rows directly, so an offer for a
        // post whose benefits came through a BENEFIT GROUP would have listed none of them — the
        // candidate would have been sent an offer letter missing most of the package.
        if (position != null)
        {
            var order = 1;
            var effective = await _namedSets.GetEffectiveBenefitsAsync(position.Id, current, cancellationToken);
            var descriptions = await _unitOfWork.Repository<BenefitPolicy>().GetQueryable()
                .Where(p => p.TenantId == current && !p.IsDeleted)
                .Select(p => new { p.Id, p.Description })
                .ToDictionaryAsync(p => p.Id, p => p.Description, cancellationToken);

            var seededBenefits = effective
                .Where(pb => pb.PolicyIsActive)
                .Select(pb => new JobOfferBenefit
                {
                    TenantId      = current,
                    JobOfferId    = entity.Id,
                    BenefitName   = pb.PolicyName,
                    Description   = descriptions.TryGetValue(pb.PolicyId, out var d) ? d : null,
                    MonetaryValue = pb.PositionAmount,
                    CurrencyCode  = entity.CurrencyCode,
                    IsMonetary    = pb.PositionAmount.HasValue,
                    DisplayOrder  = order++,
                    CreatedBy     = createdByUserId.ToString(),
                })
                .ToList();

            if (seededBenefits.Count > 0)
                await _benefitRepository.AddRangeAsync(seededBenefits);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job offer created: {OfferNumber}", entity.OfferNumber);

        // Re-read so the response carries the candidate's name. A freshly constructed entity has no
        // navigations loaded, and EF will not populate them just because the FK is set — so the
        // create response used to come back with an empty candidateName even though the very next
        // GET showed it.
        var saved = await _offerRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<JobOfferDto> UpdateAsync(UpdateJobOfferDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(updateDto.Id);

        if (entity.OfferStatus != JobOfferStatus.Draft && entity.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException("Only draft or pending-approval offers can be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        // \u26a0 Validated AFTER the update is applied. This check used to run first, so it tested the
        // salary already on the record and never the one being saved \u2014 the band guard that create
        // enforces was a no-op on every edit.
        EnsureSalaryWithinBand(entity);

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job offer updated: {OfferNumber}", entity.OfferNumber);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(id);

        if (entity.OfferStatus != JobOfferStatus.Draft)
            throw new InvalidOperationException("Only draft offers can be deleted.");

        await _offerRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────
    //
    // Offer approval runs on the generic workflow engine (see JobOfferWorkflowStatusAdapter). The
    // service never sets an approval status itself: it asks the engine, then lets the adapter apply
    // whatever the engine decided. That is what makes routing — who approves an offer above the
    // band midpoint, say — a published policy rather than a hard-coded branch.
    //
    // ⚠ Until a JobOffer workflow definition is published and `POST api/Workflow/entity-types/seed`
    // has been re-run, submit/approve/reject are inoperable BY DESIGN — authority comes from the
    // definition, not from a role attribute.

    private const string EntityType = "JobOffer";

    public async Task<bool> SubmitForApprovalAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(offerId);

        var allowedStatuses = new[] { JobOfferStatus.Draft, JobOfferStatus.Rejected };
        if (!allowedStatuses.Contains(entity.OfferStatus))
            throw new InvalidOperationException(
                $"Only Draft or Rejected offers can be submitted for approval (current: {entity.OfferStatus}).");

        // The band is a fact about the offer, not a routing choice, so it is enforced here before
        // an approver is ever troubled with it — the same reasoning that keeps requisition budget
        // enforcement in its service rather than in a definition.
        EnsureSalaryWithinBand(entity);

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, entity.Id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(
                workflowResult.ExecutionResult.Message ?? "Failed to start the offer approval workflow.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, workflowResult.Outcome, _currentUserProvider.UserId);
        entity.UpdatedAt = DateTime.UtcNow;

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Offer {OfferNumber} submitted for approval (now {Status}).",
            entity.OfferNumber, entity.OfferStatus);
        return true;
    }

    public async Task<bool> ApproveAsync(ApproveJobOfferDto dto, Guid approvedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(dto.OfferId);

        if (entity.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Only offers in PendingApproval can be approved (current: {entity.OfferStatus}).");

        // The engine resolves approvers by ApplicationUser, so it gets UserId; everything the entity
        // stores (PreparedById, ApprovedById) is an Employee FK and gets the id the controller
        // passed. See hr-attendance-actor-conventions.
        var actingUserId = _currentUserProvider.UserId;
        if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, entity.Id, actingUserId))
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            EntityType, entity.Id, actingUserId, "Approve", dto.Comments);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(
                workflowResult.ExecutionResult.Message ?? "Failed to process the approval.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, workflowResult.Outcome, actingUserId);

        // ⚠ ApprovedDate is the server's clock, not `dto.ApprovedDate`. It used to be taken from the
        // request body, so an approver could date their own approval to whenever suited them.
        entity.ApprovedById = approvedByUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = approvedByUserId.ToString();

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Offer approval step processed: {OfferNumber} (now {Status})",
            entity.OfferNumber, entity.OfferStatus);
        return true;
    }

    public async Task<bool> RejectApprovalAsync(RejectJobOfferDto dto, Guid rejectedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(dto.OfferId);

        if (entity.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Only offers in PendingApproval can be rejected (current: {entity.OfferStatus}).");

        var actingUserId = _currentUserProvider.UserId;
        if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, entity.Id, actingUserId))
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            EntityType, entity.Id, actingUserId, "Reject", dto.RejectionReason);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(
                workflowResult.ExecutionResult.Message ?? "Failed to process the rejection.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, workflowResult.Outcome, actingUserId, dto.RejectionReason);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = rejectedByUserId.ToString();

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Offer {OfferNumber} approval rejected by {RejectedBy}. Reason: {Reason}",
            entity.OfferNumber, rejectedByUserId, dto.RejectionReason);

        return true;
    }

    /// <summary>
    /// The preparer withdrawing an offer before anyone has ruled on it. Returns it to Draft so it
    /// can be reworked and resubmitted.
    /// </summary>
    public async Task<bool> RecallApprovalAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(offerId);

        if (entity.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Only an offer awaiting approval can be recalled (current: {entity.OfferStatus}).");

        var workflowResult = await _workflowIntegrationService.RecallAsync(
            EntityType, entity.Id, _currentUserProvider.UserId);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(
                workflowResult.ExecutionResult.Message ?? "Failed to recall the offer.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyRecallOutcome(entity, _currentUserProvider.UserId);
        entity.UpdatedAt = DateTime.UtcNow;

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> IssueAsync(IssueJobOfferDto dto, Guid issuedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(dto.OfferId);

        if (entity.OfferStatus != JobOfferStatus.Approved)
            throw new InvalidOperationException("Only approved offers can be issued.");

        entity.OfferStatus = JobOfferStatus.Sent;
        entity.OfferDate = dto.OfferDate;
        if (dto.ExpiryDate.HasValue)
            entity.ExpiryDate = dto.ExpiryDate;
        // ⚠ The letter is no longer settable from the payload — see IssueJobOfferDto. It arrives
        // through the controlled-upload gate, which is what records it in the DMS.

        // Generate a secure candidate response token
        var expiresAt = entity.ExpiryDate ?? dto.ExpiryDate ?? DateTime.UtcNow.AddDays(14);
        var candidateToken = new OfferCandidateToken
        {
            TenantId   = entity.TenantId,
            JobOfferId = entity.Id,
            Token      = Guid.NewGuid(),
            ExpiresAt  = expiresAt,
            CreatedBy  = issuedByUserId.ToString(),
        };
        var tokenRepo = _unitOfWork.Repository<OfferCandidateToken>();
        await tokenRepo.AddAsync(candidateToken);

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Advance pipeline: offer issued → Offer stage (no-op if no pipeline)
        await _pipelineService.AutoAdvanceToStageTypeAsync(
            entity.JobApplicationId, RecruitmentPipelineStageType.Offer, issuedByUserId, cancellationToken);

        // Email #11 — Offer issued (with token link)
        var fullOffer = await _offerRepository.GetWithFullDetailsAsync(entity.Id);
        if (fullOffer != null)
        {
            var candidateEmail = fullOffer.Application?.JobCandidate?.Email;
            var candidateName  = fullOffer.Application?.JobCandidate?.FullName ?? "Candidate";
            await SendOfferIssuedEmailAsync(candidateEmail ?? string.Empty, candidateName, fullOffer, candidateToken.Token);
        }

        return true;
    }

    public async Task<bool> RecordResponseAsync(RecordOfferResponseDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(dto.OfferId);

        if (entity.OfferStatus != JobOfferStatus.Sent)
            throw new InvalidOperationException("Can only record a response for a sent offer.");

        // ⚠ `dto.Response` is a full JobOfferStatus, and this assigned it unchecked — so "record the
        // candidate's response" could set an offer to Approved, Draft or anything else in the enum,
        // straight past every gate that owns those states. The token-based candidate flow already
        // constrains itself to these three; the internal one did not.
        var allowedResponses = new[]
        {
            JobOfferStatus.Accepted, JobOfferStatus.Negotiating, JobOfferStatus.Declined,
        };
        if (!allowedResponses.Contains(dto.Response))
            throw new InvalidOperationException(
                "A candidate response must be Accepted, Negotiating or Declined. " +
                "Use conditionally-accept, revoke or revise for the other outcomes.");

        entity.OfferStatus = dto.Response;
        if (dto.Response == JobOfferStatus.Accepted) entity.AcceptedDate = DateTime.UtcNow;
        else if (dto.Response == JobOfferStatus.Declined) entity.DeclinedDate = DateTime.UtcNow;
        entity.CandidateResponseNotes = dto.CandidateResponseNotes;

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Email #13 — Hired / offer accepted
        if (dto.Response == JobOfferStatus.Accepted)
        {
            var fullOffer = await _offerRepository.GetWithFullDetailsAsync(entity.Id);
            if (fullOffer != null)
            {
                var candidateEmail = fullOffer.Application?.JobCandidate?.Email;
                var candidateName  = fullOffer.Application?.JobCandidate?.FullName ?? "Candidate";
                await SendOfferAcceptedEmailAsync(candidateEmail ?? string.Empty, candidateName, fullOffer);
            }
        }

        return true;
    }

    public async Task<bool> RevokeAsync(RevokeJobOfferDto dto, Guid revokedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(dto.OfferId);

        var terminalStatuses = new[]
        {
            JobOfferStatus.Withdrawn,
            JobOfferStatus.Declined,
            JobOfferStatus.Expired,
        };
        if (terminalStatuses.Contains(entity.OfferStatus))
            throw new InvalidOperationException($"An offer in '{entity.OfferStatus}' status cannot be revoked.");

        entity.OfferStatus = JobOfferStatus.Withdrawn;
        entity.RevokedDate = DateTime.UtcNow;
        entity.RevocationReason = dto.RevocationReason;

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<JobOfferSummaryDto>> GetByPreparedByAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _offerRepository.GetByPreparedByAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToSummaryDto());
    }

    public async Task<JobOfferDto> AcceptConditionallyAsync(Guid offerId, Guid updatedByUserId, string? candidateResponseNotes = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(offerId);

        var allowedStatuses = new[] { JobOfferStatus.Sent, JobOfferStatus.Negotiating };
        if (!allowedStatuses.Contains(entity.OfferStatus))
            throw new InvalidOperationException(
                $"Only sent or negotiating offers can be conditionally accepted (current: {entity.OfferStatus}).");

        entity.OfferStatus = JobOfferStatus.ConditionallyAccepted;
        entity.IsConditional = true;
        entity.AcceptedDate = DateTime.UtcNow;
        entity.CandidateResponseNotes = candidateResponseNotes;

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Offer {OfferNumber} conditionally accepted — pre-employment checks pending.",
            entity.OfferNumber);

        return entity.ToDto();
    }

    // ── Benefits ──────────────────────────────────────────────────────────────

    public async Task<JobOfferBenefitDto> AddBenefitAsync(CreateJobOfferBenefitDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var offer = await GetOwnedOfferAsync(createDto.JobOfferId);

        if (offer.OfferStatus != JobOfferStatus.Draft && offer.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException("Benefits can only be added to a Draft or Pending-Approval offer.");

        var benefit = new JobOfferBenefit
        {
            TenantId = current,
            JobOfferId = createDto.JobOfferId,
            BenefitName = createDto.BenefitName.Trim(),
            Description = createDto.Description,
            MonetaryValue = createDto.MonetaryValue,
            CurrencyCode = createDto.CurrencyCode?.ToUpperInvariant(),
            IsMonetary = createDto.IsMonetary,
            DisplayOrder = createDto.DisplayOrder,
            CreatedBy = createdByUserId.ToString(),
        };

        await _benefitRepository.AddAsync(benefit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return benefit.ToDto();
    }

    public async Task<IEnumerable<JobOfferBenefitDto>> GetBenefitsAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        await GetOwnedOfferAsync(offerId);
        var tenantId = GetTenantId();
        var benefits = await _benefitRepository.GetByOfferIdAsync(offerId);
        return benefits.Where(b => b.TenantId == tenantId).Select(b => b.ToDto());
    }

    public async Task<JobOfferBenefitDto> UpdateBenefitAsync(UpdateJobOfferBenefitDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var benefit = await GetOwnedBenefitAsync(updateDto.Id);

        // ⚠ This guard was missing. Adding a benefit and removing one both refuse outside
        // Draft/PendingApproval, but editing one did not — so a benefit could not be added to a Sent
        // offer, yet its monetary value could be rewritten to anything after the candidate had the
        // letter in hand. Three doors onto the same collection, one of them unlocked.
        var owningOffer = await GetOwnedOfferAsync(benefit.JobOfferId);
        if (owningOffer.OfferStatus != JobOfferStatus.Draft && owningOffer.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException("Benefits can only be edited on a Draft or Pending-Approval offer.");

        if (updateDto.BenefitName != null)  benefit.BenefitName  = updateDto.BenefitName.Trim();
        if (updateDto.Description != null)  benefit.Description  = updateDto.Description;
        if (updateDto.MonetaryValue.HasValue) benefit.MonetaryValue = updateDto.MonetaryValue;
        if (updateDto.CurrencyCode != null)  benefit.CurrencyCode  = updateDto.CurrencyCode.ToUpperInvariant();
        if (updateDto.IsMonetary.HasValue)   benefit.IsMonetary    = updateDto.IsMonetary.Value;
        if (updateDto.DisplayOrder.HasValue) benefit.DisplayOrder  = updateDto.DisplayOrder.Value;
        benefit.UpdatedBy = updatedByUserId.ToString();
        benefit.UpdatedAt = DateTime.UtcNow;

        await _benefitRepository.UpdateAsync(benefit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return benefit.ToDto();
    }

    public async Task<bool> DeleteBenefitAsync(Guid benefitId, CancellationToken cancellationToken = default)
    {
        var benefit = await GetOwnedBenefitAsync(benefitId);

        var offer = await GetOwnedOfferAsync(benefit.JobOfferId);
        if (offer.OfferStatus != JobOfferStatus.Draft && offer.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException("Benefits can only be removed from a Draft or Pending-Approval offer.");

        await _benefitRepository.DeleteAsync(benefit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Position-benefit integration ──────────────────────────────────────────

    public async Task<IEnumerable<JobOfferBenefitDto>> SuggestBenefitsFromPositionAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetWithFullDetailsAsync(offerId);
        if (offer == null || offer.TenantId != GetTenantId())
            throw new ArgumentException($"Job offer '{offerId}' not found.");

        var positionBenefitList = offer.Position?.PositionBenefits ?? new List<EmployeePositionBenefit>();

        // Monetary if the policy has a numeric employer contribution or position override amount
        int order = 1;
        return positionBenefitList
            .Where(pb => pb.BenefitPolicy != null)
            .Select(pb =>
            {
                var value = pb.PositionAmount ?? pb.BenefitPolicy.EmployerContribution;
                return new JobOfferBenefitDto
                {
                    Id = Guid.Empty,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = string.Empty,
                    JobOfferId = offerId,
                    BenefitName = pb.BenefitPolicy.PolicyName,
                    Description = pb.BenefitPolicy.Description,
                    MonetaryValue = value,
                    CurrencyCode = null,
                    IsMonetary = value.HasValue && value > 0,
                    DisplayOrder = order++,
                };
            });
    }

    public async Task<IEnumerable<JobOfferBenefitDto>> ImportBenefitsFromPositionAsync(Guid offerId, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var offer = await _offerRepository.GetWithFullDetailsAsync(offerId);
        if (offer == null || offer.TenantId != current)
            throw new ArgumentException($"Job offer '{offerId}' not found.");

        if (offer.OfferStatus != JobOfferStatus.Draft && offer.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException("Benefits can only be imported into a Draft or Pending-Approval offer.");

        var positionBenefits = offer.Position?.PositionBenefits ?? new List<EmployeePositionBenefit>();
        if (!positionBenefits.Any())
            return Enumerable.Empty<JobOfferBenefitDto>();

        // Fetch existing names to avoid duplicates
        var existing = await _benefitRepository.GetByOfferIdAsync(offerId);
        var existingNames = existing.Where(b => b.TenantId == current).Select(b => b.BenefitName.ToLowerInvariant()).ToHashSet();

        var tenantExisting = existing.Where(b => b.TenantId == current).ToList();
        int nextOrder = tenantExisting.Any() ? tenantExisting.Max(b => b.DisplayOrder) + 1 : 1;
        var newBenefits = new List<JobOfferBenefit>();

        foreach (var pb in positionBenefits.Where(pb => pb.BenefitPolicy != null))
        {
            var name = pb.BenefitPolicy.PolicyName.Trim();
            if (existingNames.Contains(name.ToLowerInvariant()))
                continue;

            var value = pb.PositionAmount ?? pb.BenefitPolicy.EmployerContribution;
            newBenefits.Add(new JobOfferBenefit
            {
                TenantId = current,
                JobOfferId = offerId,
                BenefitName = name,
                Description = pb.BenefitPolicy.Description,
                MonetaryValue = value,
                IsMonetary = value.HasValue && value > 0,
                DisplayOrder = nextOrder++,
                CreatedBy = createdByUserId.ToString(),
            });
        }

        if (newBenefits.Any())
        {
            await _benefitRepository.AddRangeAsync(newBenefits);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Imported {Count} position benefit(s) into offer {OfferId}",
            newBenefits.Count, offerId);

        return newBenefits.Select(b => b.ToDto());
    }

    // ── Notes ─────────────────────────────────────────────────────────────────

    public async Task<JobOfferNoteDto> AddNoteAsync(CreateJobOfferNoteDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedOfferAsync(createDto.JobOfferId);

        var note = new JobOfferNote
        {
            TenantId   = current,
            JobOfferId = createDto.JobOfferId,
            Body       = createDto.Body.Trim(),
            AuthorName = await ResolveAuthorNameAsync(createdByUserId),
            CreatedBy  = createdByUserId.ToString(),
        };

        await _noteRepository.AddAsync(note);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return note.ToDto();
    }

    public async Task<IEnumerable<JobOfferNoteDto>> GetNotesAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        await GetOwnedOfferAsync(offerId);
        var tenantId = GetTenantId();
        var notes = await _noteRepository.GetByOfferIdAsync(offerId);
        return notes.Where(n => n.TenantId == tenantId).Select(n => n.ToDto());
    }

    private async Task<string> ResolveAuthorNameAsync(Guid employeeId)
    {
        var tenantId = GetTenantId();
        var emp = await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId);
        if (emp == null || emp.TenantId != tenantId) return employeeId.ToString();
        return string.IsNullOrWhiteSpace(emp.MiddleName)
            ? $"{emp.FirstName} {emp.LastName}".Trim()
            : $"{emp.FirstName} {emp.MiddleName} {emp.LastName}".Trim();
    }

    // ── Offer versioning ──────────────────────────────────────────────────────

    public async Task<JobOfferDto> ReviseOfferAsync(ReviseJobOfferDto dto, Guid revisedByUserId, CancellationToken cancellationToken = default)
    {
        var original = await _offerRepository.GetWithFullDetailsAsync(dto.OriginalOfferId);
        if (original == null || original.TenantId != GetTenantId())
            throw new ArgumentException($"Job offer '{dto.OriginalOfferId}' not found.");

        // A revision is typically triggered after the candidate counters
        if (original.OfferStatus != JobOfferStatus.Negotiating && original.OfferStatus != JobOfferStatus.Sent)
            throw new InvalidOperationException(
                "A revised offer can only be issued against a Sent or Negotiating offer.");

        // Mark the old offer as superseded
        original.IsLatestVersion = false;
        await _offerRepository.UpdateAsync(original);

        // Clone terms, apply overrides from the dto
        var revised = new JobOffer
        {
            TenantId = original.TenantId,
            JobApplicationId = original.JobApplicationId,
            OfferStatus = JobOfferStatus.Draft,
            OfferNumber = await GenerateOfferNumberAsync(cancellationToken),
            PositionId = original.PositionId,
            PositionTitle = original.PositionTitle,
            ReportsToTitle = original.ReportsToTitle,
            GradeTitle = original.GradeTitle,
            DepartmentName = original.DepartmentName,
            WorkMode = original.WorkMode,
            SalaryGradeMin = original.SalaryGradeMin,
            SalaryGradeMax = original.SalaryGradeMax,
            ProbationPeriodMonths = original.ProbationPeriodMonths,
            NoticePeriodMonths = original.NoticePeriodMonths,
            LocationLevelId = original.LocationLevelId,
            LocationId = original.LocationId,
            EmploymentType = original.EmploymentType,
            ContractDurationMonths = original.ContractDurationMonths,
            BaseSalary = dto.NewBaseSalary ?? original.BaseSalary,
            CurrencyCode = original.CurrencyCode,
            Bonus = original.Bonus,
            BonusTerms = original.BonusTerms,
            Commission = original.Commission,
            CommissionStructure = original.CommissionStructure,
            AdditionalTerms = dto.NewAdditionalTerms ?? original.AdditionalTerms,
            ProposedStartDate = dto.NewProposedStartDate ?? original.ProposedStartDate,
            PreparedById = revisedByUserId,
            Version = original.Version + 1,
            IsLatestVersion = true,
            PreviousOfferId = original.Id,
            CreatedBy = revisedByUserId.ToString(),
            // Carried forward deliberately: a revision inherits the original's conditionality and
            // working pattern. Only the negotiated terms below are open to change.
            IsConditional = original.IsConditional,
            WeeklyHours = original.WeeklyHours,
        };

        // A revision exists to change the money after a counter-offer, so this is precisely the path
        // that must respect the band — and it was the one path that never checked.
        EnsureSalaryWithinBand(revised);

        await _offerRepository.AddAsync(revised);

        // Carry the existing benefits forward to the new version
        var originalBenefits = (await _benefitRepository.GetByOfferIdAsync(original.Id))
            .Where(b => b.TenantId == original.TenantId);
        if (originalBenefits.Any())
        {
            var copiedBenefits = originalBenefits.Select(b => new JobOfferBenefit
            {
                TenantId = b.TenantId,
                JobOfferId = revised.Id,
                BenefitName = b.BenefitName,
                Description = b.Description,
                MonetaryValue = b.MonetaryValue,
                CurrencyCode = b.CurrencyCode,
                IsMonetary = b.IsMonetary,
                DisplayOrder = b.DisplayOrder,
                CreatedBy = revisedByUserId.ToString(),
            }).ToList();
            await _benefitRepository.AddRangeAsync(copiedBenefits);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Revised offer created: {NewNumber} (v{Version}) from {OldNumber} (v{OldVersion})",
            revised.OfferNumber, revised.Version, original.OfferNumber, original.Version);

        return revised.ToDto();
    }

    // ── File uploads ──────────────────────────────────────────────────────────

    public async Task RecordOfferLetterAsync(
        Guid offerId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(offerId);

        entity.OfferLetterFileUploadRecordId = fileUploadRecordId;
        entity.OfferLetterDocumentRecordId = documentRecordId;
        entity.OfferLetterDocumentVersionId = documentVersionId;
        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Offer letter recorded for offer {OfferNumber} (upload {UploadId}).",
            entity.OfferNumber, fileUploadRecordId);
    }

    public async Task RecordSignedLetterAsync(
        Guid offerId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedOfferAsync(offerId);

        entity.SignedOfferLetterFileUploadRecordId = fileUploadRecordId;
        entity.SignedOfferLetterDocumentRecordId = documentRecordId;
        entity.SignedOfferLetterDocumentVersionId = documentVersionId;
        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Signed offer letter recorded for offer {OfferNumber} (upload {UploadId}).",
            entity.OfferNumber, fileUploadRecordId);
    }

    // ── Public candidate response (token-based) ───────────────────────────────

    public async Task<CandidateOfferSummaryDto> ValidateCandidateTokenAsync(Guid token, CancellationToken cancellationToken = default)
    {
        var tokenRepo = _unitOfWork.Repository<OfferCandidateToken>();
        var tokenEntity = await tokenRepo.FirstOrDefaultAsync(t => t.Token == token);

        if (tokenEntity == null)
            return new CandidateOfferSummaryDto { TokenExpired = true };

        if (tokenEntity.IsUsed)
            return new CandidateOfferSummaryDto { TokenAlreadyUsed = true };

        if (tokenEntity.ExpiresAt < DateTime.UtcNow)
            return new CandidateOfferSummaryDto { TokenExpired = true };

        var offer = await _offerRepository.GetWithFullDetailsAsync(tokenEntity.JobOfferId);
        if (offer == null)
            return new CandidateOfferSummaryDto { TokenExpired = true };

        return new CandidateOfferSummaryDto
        {
            OfferId         = offer.Id,
            OfferNumber     = offer.OfferNumber,
            PositionTitle   = offer.PositionTitle,
            BaseSalary      = offer.BaseSalary.HasValue ? $"{offer.CurrencyCode} {offer.BaseSalary.Value:N2} per annum" : null,
            StartDate       = offer.ProposedStartDate?.ToString("dddd, d MMMM yyyy"),
            ExpiryDate      = offer.ExpiryDate?.ToString("dddd, d MMMM yyyy"),
            AdditionalTerms = offer.AdditionalTerms,
            IsConditional   = offer.IsConditional,
            // ⚠ Deliberately NOT `offer.OfferLetterPath`. That is a server filesystem path, and this
            // payload goes to an unauthenticated caller holding only an emailed token — it told them
            // where the file lives on disk and was useless to them as a link anyway. The letter
            // reaches the candidate as an email attachment; a download route for them would need its
            // own token check, which is candidate-portal work rather than something to bolt on here.
            OfferLetterUrl  = null,
        };
    }

    public async Task<bool> RecordCandidateResponseAsync(CandidateResponseDto dto, CancellationToken cancellationToken = default)
    {
        var allowedResponses = new[] { JobOfferStatus.Accepted, JobOfferStatus.Negotiating, JobOfferStatus.Declined };
        if (!allowedResponses.Contains(dto.Response))
            throw new ArgumentException("Response must be Accepted, Negotiating, or Declined.");

        var tokenRepo = _unitOfWork.Repository<OfferCandidateToken>();
        var tokenEntity = await tokenRepo.FirstOrDefaultAsync(t => t.Token == dto.Token);

        if (tokenEntity == null || tokenEntity.IsUsed || tokenEntity.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("The offer response link is invalid or has expired.");

        var entity = await _offerRepository.GetByIdAsync(tokenEntity.JobOfferId);
        if (entity == null)
            throw new ArgumentException("Associated offer not found.");

        if (entity.OfferStatus != JobOfferStatus.Sent && entity.OfferStatus != JobOfferStatus.ConditionallyAccepted)
            throw new InvalidOperationException("This offer is no longer awaiting a response.");

        entity.OfferStatus = dto.Response;
        entity.CandidateResponseNotes = dto.Notes;
        if (dto.Response == JobOfferStatus.Accepted || dto.Response == JobOfferStatus.ConditionallyAccepted)
            entity.AcceptedDate = DateTime.UtcNow;
        else if (dto.Response == JobOfferStatus.Declined)
        {
            entity.DeclinedDate = DateTime.UtcNow;
            entity.DeclineReason = dto.DeclineReason;
        }

        tokenEntity.IsUsed = true;
        tokenEntity.UsedAt = DateTime.UtcNow;

        await _offerRepository.UpdateAsync(entity);
        await tokenRepo.UpdateAsync(tokenEntity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Candidate responded to offer {OfferNumber} via token: {Response}",
            entity.OfferNumber, dto.Response);

        return true;
    }

    public async Task<bool> RecordPortalCandidateResponseAsync(Guid applicationId, CandidatePortalOfferResponseDto dto, CancellationToken cancellationToken = default)
    {
        // Anonymous candidate-portal flow: no authenticated tenant — scope by the application's tenant.
        var application = await _applicationRepository.GetByIdAsync(applicationId);
        if (application == null)
            throw new InvalidOperationException("No active offer found for this application.");

        var allowedResponses = new[] { JobOfferStatus.Accepted, JobOfferStatus.Negotiating, JobOfferStatus.Declined };
        if (!allowedResponses.Contains(dto.Response))
            throw new ArgumentException("Response must be Accepted, Negotiating, or Declined.");

        var entities = await _offerRepository.GetByApplicationIdAsync(applicationId);
        var entity = entities
            .Where(e => e.TenantId == application.TenantId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefault(e => e.OfferStatus == JobOfferStatus.Sent || e.OfferStatus == JobOfferStatus.Negotiating);

        if (entity == null)
            throw new InvalidOperationException("No active offer found for this application.");

        entity.OfferStatus = dto.Response;
        entity.CandidateResponseNotes = dto.Notes;

        if (dto.Response is JobOfferStatus.Accepted or JobOfferStatus.ConditionallyAccepted)
            entity.AcceptedDate = DateTime.UtcNow;
        else if (dto.Response == JobOfferStatus.Declined)
        {
            entity.DeclinedDate = DateTime.UtcNow;
            entity.DeclineReason = dto.DeclineReason;
        }

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Candidate responded to offer {OfferNumber} via portal: {Response}",
            entity.OfferNumber, dto.Response);

        return true;
    }

    private async Task SendOfferIssuedEmailAsync(string toEmail, string candidateName, JobOffer offer, Guid? candidateToken = null)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"] = candidateName,
            ["PositionTitle"] = offer.PositionTitle,
            ["OfferNumber"]   = offer.OfferNumber,
            ["SalaryLine"]    = offer.BaseSalary.HasValue ? $"{offer.CurrencyCode} {offer.BaseSalary:N2} per annum" : null,
            ["StartDate"]     = offer.ProposedStartDate?.ToString("dddd, d MMMM yyyy"),
            ["ExpiryDate"]    = offer.ExpiryDate?.ToString("dddd, d MMMM yyyy"),
            // Point at the header-free, token-based offer-response flow (api/offer-response, backed by
            // the globally-unique OfferCandidateToken) so a recipient can respond straight from the
            // email without logging in / supplying a tenant. Falls back to the login deep-link only if
            // no token was issued.
            ["RespondUrl"]    = candidateToken.HasValue
                ? $"{_portalBaseUrl}/careers/portal/offer-response?token={candidateToken.Value}"
                : $"{_portalBaseUrl}/careers/portal/login?returnUrl=/careers/portal/offer/{offer.JobApplicationId}",
        };

        // Best-effort: the offer is already committed when this runs, so a mail failure must not turn a
        // successful issue into an error response.
        try
        {
            await _templatedEmail.SendAsync(
                RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.OfferIssued, toEmail, tokens);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send offer issued email to {Email} — offer {OfferNumber} was issued successfully.",
                toEmail, offer.OfferNumber);
        }
    }

    private async Task SendOfferAcceptedEmailAsync(string toEmail, string candidateName, JobOffer offer)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"] = candidateName,
            ["PositionTitle"] = offer.PositionTitle,
            ["OfferNumber"]   = offer.OfferNumber,
            ["StartDate"]     = offer.ProposedStartDate?.ToString("dddd, d MMMM yyyy"),
        };

        // Best-effort with a timeout race: this is reachable from the header-free, anonymous
        // offer-response token flow, where the candidate's acceptance is already committed and an
        // unresponsive SMTP server would otherwise hold their response open.
        try
        {
            var emailTask = _templatedEmail.SendAsync(
                RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.OfferAccepted, toEmail, tokens);

            if (await Task.WhenAny(emailTask, Task.Delay(TimeSpan.FromSeconds(10))) == emailTask)
                await emailTask;
            else
                _logger.LogWarning(
                    "Offer accepted email timed out after 10 s for {Email} — offer {OfferNumber} was accepted successfully.",
                    toEmail, offer.OfferNumber);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send offer accepted email to {Email} — offer {OfferNumber} was accepted successfully.",
                toEmail, offer.OfferNumber);
        }
    }
}

// ============================================================================
// JOB HIRE SERVICE
// ============================================================================

public class JobHireService : IJobHireService
{
    private readonly IJobHireRecordRepository _hireRepository;
    private readonly IJobOfferRepository _offerRepository;
    private readonly IApplicationPipelineService _pipelineService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmployeeContractDetailRepository _contractDetailRepository;
    private readonly IProbationPeriodRepository _probationRepository;
    private readonly IEmployeePositionHistoryRepository _positionHistoryRepository;
    private readonly IEmployeeSalaryAssignmentRepository _salaryAssignmentRepository;
    private readonly IEmployeeQualificationRepository _qualificationRepository;
    private readonly IEmployeeWorkHistoryRepository _workHistoryRepository;
    private readonly IEmployeeRefereeRepository _refereeRepository;
    private readonly IEmployeeSkillRepository _skillRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    // The register's numbering rule. A hire is how contract staff actually arrive, so this is the
    // path that most needs to honour a register with its own series.
    private readonly IStaffNumberService _staffNumbers;

    private readonly ILogger<JobHireService> _logger;

    // Enrols the new employee in payroll once the hire has committed — the same create-only bridge
    // the employee master uses, so a hire and a direct create arrive in payroll the same way.
    private readonly IPayrollMembershipService _payrollMembership;
    private readonly IEmployeeService _employees;

    public JobHireService(
        IJobHireRecordRepository hireRepository,
        IJobOfferRepository offerRepository,
        IApplicationPipelineService pipelineService,
        IEmployeeRepository employeeRepository,
        IEmployeeContractDetailRepository contractDetailRepository,
        IProbationPeriodRepository probationRepository,
        IEmployeePositionHistoryRepository positionHistoryRepository,
        IEmployeeSalaryAssignmentRepository salaryAssignmentRepository,
        IEmployeeQualificationRepository qualificationRepository,
        IEmployeeWorkHistoryRepository workHistoryRepository,
        IEmployeeRefereeRepository refereeRepository,
        IEmployeeSkillRepository skillRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        IStaffNumberService staffNumbers,
        IPayrollMembershipService payrollMembership,
        IEmployeeService employees,
        ILogger<JobHireService> logger)
    {
        _staffNumbers             = staffNumbers;
        _payrollMembership        = payrollMembership;
        _employees                = employees;
        _hireRepository           = hireRepository;
        _offerRepository          = offerRepository;
        _pipelineService          = pipelineService;
        _employeeRepository       = employeeRepository;
        _contractDetailRepository = contractDetailRepository;
        _probationRepository      = probationRepository;
        _positionHistoryRepository  = positionHistoryRepository;
        _salaryAssignmentRepository = salaryAssignmentRepository;
        _qualificationRepository    = qualificationRepository;
        _workHistoryRepository      = workHistoryRepository;
        _refereeRepository          = refereeRepository;
        _skillRepository            = skillRepository;
        _currentUserProvider        = currentUserProvider;
        _unitOfWork               = unitOfWork;
        _logger                   = logger;
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

    // A hire record owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<JobHireRecord> GetOwnedHireAsync(Guid id)
    {
        var entity = await _hireRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Hire record with ID '{id}' not found.");
        return entity;
    }

    private async Task<string> GenerateHireNumberAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // ⚠ Counts SOFT-DELETED rows too, via the including-deleted overload — plain GetQueryable()
        // filters them out. Excluding them makes the sequence reuse a number the moment anything is
        // deleted, and JobHireRecords carries a UNIQUE index on (TenantId, HireNumber) that a soft delete does
        // not release: deleting one draft made the very next create die on a duplicate-key violation,
        // surfaced as a 500 with raw SQL in it. A reference number is an identifier, not a slot —
        // once issued it is spent.
        var last = await _hireRepository.GetQueryableIncludingDeleted(h => h.TenantId == tenantId)
            .OrderByDescending(h => h.HireNumber)
            .Select(h => h.HireNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (last != null && int.TryParse(last.Replace("HIR-", ""), out var parsed))
            next = parsed + 1;

        return $"HIR-{next:D6}";
    }

    public async Task<JobHireRecordDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHireAsync(id);
        return entity.ToDto();
    }

    public async Task<JobHireRecordDto?> GetByHireNumberAsync(string hireNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _hireRepository.GetByHireNumberAsync(hireNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<JobHireRecordDto?> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _hireRepository.GetByApplicationIdAsync(applicationId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<JobHireRecordDto?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _hireRepository.GetByEmployeeIdAsync(employeeId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<JobHireRecordSummaryDto>> GetByStatusAsync(JobHireStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _hireRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobHireRecordSummaryDto>> GetWithStartDateApproachingAsync(int daysAhead = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _hireRepository.GetWithStartDateApproachingAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<JobHireRecordDto> CreateAsync(CreateJobHireRecordDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // Guard: for conditional offers, checks must have cleared before a hire record can be created
        var offer = await _offerRepository.GetByIdAsync(createDto.OfferId);
        if (offer == null || offer.TenantId != current)
            throw new ArgumentException($"Job offer '{createDto.OfferId}' not found.");

        // ⚠ This gate previously accepted `Accepted` as well as `ChecksCleared`, so a conditional
        // offer could be hired the moment the candidate said yes — while its message told the reader
        // the opposite. Conditional now means what it says: the checks have to clear.
        if (offer.IsConditional && offer.OfferStatus != JobOfferStatus.ChecksCleared)
        {
            throw new InvalidOperationException(
                "This offer is conditional. Pre-employment checks must be completed and cleared " +
                $"before a hire record can be created (offer is currently {offer.OfferStatus}).");
        }

        // A non-conditional offer still has to have been accepted — hiring against a draft, a
        // withdrawn or a declined offer was never intended and nothing prevented it.
        if (!offer.IsConditional
            && offer.OfferStatus is not (JobOfferStatus.Accepted or JobOfferStatus.ChecksCleared))
        {
            throw new InvalidOperationException(
                $"Only an accepted offer can be hired against (offer is currently {offer.OfferStatus}).");
        }

        // One hire per offer. Nothing enforced this, so a repeated create left two hire records
        // racing to become the same person's employment.
        var existingForApplication = await _hireRepository.GetByApplicationIdAsync(createDto.ApplicationId);
        if (existingForApplication is not null && existingForApplication.TenantId == current)
            throw new InvalidOperationException(
                $"This application already has hire record {existingForApplication.HireNumber}.");

        var entity = createDto.ToEntity(current, createdByUserId);
        entity.HireNumber = await GenerateHireNumberAsync(cancellationToken);
        entity.Status = JobHireStatus.PendingOnboarding;

        await _hireRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Hire record created: {HireNumber}", entity.HireNumber);

        // Advance pipeline: hire record created → Hired stage (no-op if no pipeline)
        await _pipelineService.AutoAdvanceToStageTypeAsync(
            entity.ApplicationId, RecruitmentPipelineStageType.Hired, createdByUserId, cancellationToken);

        var saved = await _hireRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    /// <summary>
    /// Moves a hire through onboarding.
    ///
    /// <para>⚠ <b>This had no state machine at all</b> — any status to any other, and that included
    /// <c>Active</c>, which is what <see cref="ConfirmStartAsync"/> sets <i>after</i> it has created
    /// the employee record, the contract, the probation period and the rest. Worse, setting it here
    /// was a one-way door: <c>ConfirmStartAsync</c>'s idempotency guard refuses when the status is
    /// already <c>Active</c>, so a stray status update permanently prevented the employee from ever
    /// being created and there was no way back. <c>Active</c> is therefore reachable only through
    /// confirm-start, and terminal states cannot be edited.</para>
    /// </summary>
    private static readonly IReadOnlyDictionary<JobHireStatus, JobHireStatus[]> HireTransitions =
        new Dictionary<JobHireStatus, JobHireStatus[]>
        {
            [JobHireStatus.PendingOnboarding]    = new[] { JobHireStatus.OnboardingInProgress, JobHireStatus.Cancelled },
            [JobHireStatus.OnboardingInProgress] = new[] { JobHireStatus.OnboardingCompleted, JobHireStatus.Cancelled },
            [JobHireStatus.OnboardingCompleted]  = new[] { JobHireStatus.Cancelled },
            [JobHireStatus.Active]               = Array.Empty<JobHireStatus>(),
            [JobHireStatus.Cancelled]            = Array.Empty<JobHireStatus>(),
        };

    public async Task<JobHireRecordDto> UpdateStatusAsync(UpdateJobHireRecordStatusDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHireAsync(updateDto.HireRecordId);

        if (updateDto.NewStatus == JobHireStatus.Active)
            throw new InvalidOperationException(
                "A hire becomes Active by confirming its start date, which creates the employee record. " +
                "Use confirm-start rather than setting the status directly.");

        if (entity.Status == updateDto.NewStatus)
            throw new InvalidOperationException($"This hire is already {entity.Status}.");

        var allowed = HireTransitions.TryGetValue(entity.Status, out var next) ? next : Array.Empty<JobHireStatus>();
        if (!allowed.Contains(updateDto.NewStatus))
            throw new InvalidOperationException(
                $"A hire in '{entity.Status}' cannot move to '{updateDto.NewStatus}'.");

        entity.Status = updateDto.NewStatus;
        // Only replace the notes when the caller actually sent some — this used to blank them on
        // every status change that omitted the field.
        if (updateDto.Notes != null)
            entity.Notes = updateDto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _hireRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _hireRepository.GetByIdAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<bool> ConfirmStartAsync(Guid hireRecordId, DateTime actualStartDate, Guid? linkedEmployeeId, Guid confirmedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _hireRepository.GetForConfirmStartAsync(hireRecordId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Hire record with ID '{hireRecordId}' not found.");

        // Idempotency guard. This method creates an Employee plus a contract, probation period, salary
        // assignment, position history and the candidate's qualifications/work history/referees/skills.
        // Without this check a double-click (or any retried request) would create a SECOND employee and
        // a second set of all of it, and burn another employee number.
        if (entity.EmployeeId.HasValue || entity.Status == JobHireStatus.Active)
            throw new InvalidOperationException(
                $"Hire '{entity.HireNumber}' has already been confirmed and is linked to an employee record.");

        entity.ActualStartDate = DateOnly.FromDateTime(actualStartDate);
        entity.ConfirmedById   = confirmedByUserId;
        entity.ConfirmedDate   = DateTime.UtcNow;
        entity.Status          = JobHireStatus.Active;

        if (linkedEmployeeId.HasValue)
        {
            var linkedEmployee = await _employeeRepository.GetByIdAsync(linkedEmployeeId.Value);
            if (linkedEmployee == null || linkedEmployee.TenantId != entity.TenantId)
                throw new InvalidOperationException("The linked employee record could not be found.");

            // Internal hire or manual link — use the existing employee record
            entity.EmployeeId = linkedEmployeeId.Value;
        }
        else
        {
            // External hire — auto-create employee record and all associated profile data

            var offer     = entity.Offer;
            var candidate = entity.Offer?.Application?.JobCandidate
                         ?? entity.Application?.JobCandidate;

            if (offer == null || candidate == null)
                throw new InvalidOperationException(
                    "Cannot auto-create employee: hire record is missing offer or candidate data.");

            // 1 — Employee
            // ⚠ Through the register's rule, not the old compiled-in format. This is the path by
            // which contract staff actually arrive, so it is the path that most needs to honour a
            // register with its own numbering — a hire that bypassed it would silently issue a
            // permanent-series number to a contract employee.
            //
            // The offer's EmploymentType selects the register. A register set to manual has no
            // number to offer here, so the resolver refuses and the hire cannot complete without
            // one — which is correct: nobody should be hired into a register whose numbers the
            // organisation issues by hand without somebody supplying the number.
            var empNumber = await _staffNumbers.ResolveForCreateAsync(
                offer.EmploymentType, null, cancellationToken);
            var probDays  = offer.ProbationPeriodMonths.HasValue ? offer.ProbationPeriodMonths.Value * 30 : 90;

            // On payroll when the offer gives the run something to pay from: a base salary, or a
            // grade to resolve one. An offer with neither (a consultant engaged on a fee, an intern
            // on an allowance) arrives off payroll, and HR states why on the employee record.
            var hireGradeId  = offer.SalaryLevel?.SalaryGradeId ?? offer.Position?.SalaryGradeId;
            var onPayroll    = offer.BaseSalary is > 0 || hireGradeId.HasValue;
            var employee  = new Employee
            {
                TenantId            = entity.TenantId,
                CreatedById         = confirmedByUserId,
                HireRecordId        = entity.Id,
                EmployeeNumber      = empNumber,
                FirstName           = candidate.FirstName,
                MiddleName          = candidate.MiddleName,
                LastName            = candidate.LastName,
                EmailAddress        = candidate.Email,
                MobileNumber        = candidate.Phone,
                Gender              = candidate.Gender,
                DateOfBirth         = DateOnly.FromDateTime(candidate.DateOfBirth),
                PositionId          = offer.PositionId,
                OrganizationLevelId = offer.Position?.OrganizationLevelId,
                OrganizationUnitId  = offer.Position?.OrganizationUnitId,
                EmploymentType      = offer.EmploymentType,
                IsFullTime          = offer.EmploymentType is EmploymentType.Permanent
                                                           or EmploymentType.Contract
                                                           or EmploymentType.FixedTerm,
                DateEmployed        = DateOnly.FromDateTime(actualStartDate),
                LocationId          = offer.LocationId,
                LocationLevelId     = offer.LocationLevelId,
                Salary              = onPayroll ? offer.BaseSalary : null,
                IsOnPayroll         = onPayroll,
                OffPayrollReason    = onPayroll ? null
                                    : offer.EmploymentType is EmploymentType.Consultant or EmploymentType.Freelance
                                        ? OffPayrollReason.PaidByInvoice
                                    : offer.EmploymentType is EmploymentType.Internship
                                        ? OffPayrollReason.Allowance
                                        : OffPayrollReason.Other,
                OffPayrollNote      = onPayroll ? null : "Hired without a base salary or salary grade on the offer; confirm how this person is paid.",
                StaffStatus         = offer.ProbationPeriodMonths is > 0
                                          ? StaffStatus.Probation
                                          : StaffStatus.Active,
                IsActive            = true,
                ProbationPeriodDays = probDays,
            };
            await _employeeRepository.AddAsync(employee);
            entity.EmployeeId = employee.Id;

            // 2 — EmployeeContractDetail
            var startDate       = DateOnly.FromDateTime(actualStartDate);
            var contractEndDate = offer.ContractDurationMonths.HasValue
                ? startDate.AddMonths(offer.ContractDurationMonths.Value)
                : (DateOnly?)null;
            var contractDetail = new EmployeeContractDetail
            {
                TenantId            = entity.TenantId,
                CreatedById         = confirmedByUserId,
                EmployeeId          = employee.Id,
                ContractNumber      = $"CTR-{empNumber}",
                EmploymentType      = offer.EmploymentType,
                StartDate           = startDate,
                EffectiveDate       = startDate,
                EndDate             = contractEndDate,
                ContractEndDate     = contractEndDate,
                Salary              = offer.BaseSalary ?? 0m,
                CurrencyCode        = offer.CurrencyCode ?? "GHS",
                ProbationPeriodDays = probDays,
                WorkingHoursPerWeek = offer.WeeklyHours.HasValue ? (int)offer.WeeklyHours.Value : 40,
                WorkSchedule        = offer.EmploymentType == EmploymentType.PartTime
                                          ? WorkArrangementType.PartTime
                                          : WorkArrangementType.FullTime,
                IsCurrent           = true,
                IsActive            = true,
                ContractStatus      = ContractStatus.Active,
            };
            await _contractDetailRepository.AddAsync(contractDetail);

            // 3 — ProbationPeriod (only when offer specifies a duration)
            if (offer.ProbationPeriodMonths is > 0)
            {
                var probEnd = startDate.AddMonths(offer.ProbationPeriodMonths.Value);
                var probation = new ProbationPeriod
                {
                    TenantId         = entity.TenantId,
                    CreatedById      = confirmedByUserId,
                    EmployeeId       = employee.Id,
                    ContractDetailId = contractDetail.Id,
                    StartDate        = startDate,
                    OriginalEndDate  = probEnd,
                    CurrentEndDate   = probEnd,
                    DurationMonths   = offer.ProbationPeriodMonths.Value,
                    Status           = ProbationStatus.Active,
                };
                await _probationRepository.AddAsync(probation);
            }

            // 4 — EmployeePositionHistory (initial assignment)
            // OrganizationLevelId is guaranteed non-null because EmployeePosition always requires it.
            // LocationLevelId is nullable; skip only if Position itself is missing.
            if (offer.Position != null)
            {
                var posHistory = new EmployeePositionHistory
                {
                    TenantId            = entity.TenantId,
                    CreatedById         = confirmedByUserId,
                    EmployeeId          = employee.Id,
                    PositionId          = offer.PositionId,
                    OrganizationLevelId = offer.Position.OrganizationLevelId,
                    OrganizationUnitId  = offer.Position.OrganizationUnitId,
                    LocationLevelId     = offer.LocationLevelId,
                    LocationId          = offer.LocationId,
                    StartDate           = actualStartDate,
                    ChangeReason        = PositionChangeReason.InitialAssignment,
                };
                await _positionHistoryRepository.AddAsync(posHistory);
            }

            // 5 — EmployeeSalaryAssignment
            // Prefer the grade linked to the offer's salary level; fall back to the position's default grade.
            // Gated on payroll membership like every other grade placement (EmployeeService.AssignSalaryAsync).
            var gradeId = offer.SalaryLevel?.SalaryGradeId ?? offer.Position?.SalaryGradeId;
            if (gradeId.HasValue && employee.IsOnPayroll)
            {
                // Round 3, lane H: the level resolves the way every other placement's does (a
                // two-tier structure's implicit level is filled; a notch of another grade is
                // refused). The row is still written here rather than through AssignSalaryAsync
                // because the employee is not yet saved at this point of the hire transaction.
                var resolvedLevelId = await _employees.ResolvePlacementLevelAsync(
                    gradeId.Value, offer.SalaryLevelId, offer.SalaryNotchId, cancellationToken);
                var salaryAssignment = new EmployeeSalaryAssignment
                {
                    TenantId         = entity.TenantId,
                    CreatedById      = confirmedByUserId,
                    EmployeeId       = employee.Id,
                    GradeId          = gradeId.Value,
                    LevelId          = resolvedLevelId,
                    NotchId          = offer.SalaryNotchId,
                    EffectiveDate    = actualStartDate,
                    AssignmentReason = "Initial hire assignment",
                };
                await _salaryAssignmentRepository.AddAsync(salaryAssignment);
            }

            // 6 — EmployeeQualifications (from live candidate profile)
            foreach (var cq in candidate.Qualifications)
            {
                var eq = new EmployeeQualification
                {
                    TenantId                = entity.TenantId,
                    CreatedById             = confirmedByUserId,
                    EmployeeId              = employee.Id,
                    QualificationId         = cq.QualificationId,
                    CustomQualificationName = cq.QualificationId == null ? cq.QualificationFreeText : null,
                    Institution             = cq.Institution,
                    CompletionDate          = cq.DateAwarded,
                    Grade                   = cq.Grade,
                    IsVerified              = false,
                };
                await _qualificationRepository.AddAsync(eq);
            }

            // 7 — EmployeeWorkHistory (from live candidate profile)
            foreach (var wh in candidate.WorkHistories)
            {
                var ewh = new EmployeeWorkHistory
                {
                    TenantId         = entity.TenantId,
                    CreatedById      = confirmedByUserId,
                    EmployeeId       = employee.Id,
                    CompanyName      = wh.InstitutionName,
                    JobTitle         = wh.PositionHeld,
                    StartDate        = wh.StartDate,
                    EndDate          = wh.EndDate,
                    JobDescription   = wh.Responsibilities,
                    ReasonForLeaving = wh.ReasonForLeaving,
                    CanContact       = false,
                };
                await _workHistoryRepository.AddAsync(ewh);
            }

            // 8 — EmployeeReferees (from live candidate profile; not in snapshot)
            foreach (var cr in candidate.Referees)
            {
                var er = new EmployeeReferee
                {
                    TenantId        = entity.TenantId,
                    CreatedById     = confirmedByUserId,
                    EmployeeId      = employee.Id,
                    FullName        = cr.FullName,
                    PositionOrTitle = cr.Position,
                    Organization    = cr.Organization,
                    PhoneNumber     = cr.Phone,
                    EmailAddress    = cr.Email,
                    Relationship    = cr.Relationship,
                    RefereeType     = RefereeType.Professional,
                    IsActive        = true,
                    IsContacted     = false,
                    IsPrimary       = false,
                };
                await _refereeRepository.AddAsync(er);
            }

            // 9 — EmployeeSkills (only catalogue-linked skills; free-text skills have no SkillId FK)
            foreach (var cs in candidate.Skills.Where(s => s.SkillId.HasValue))
            {
                var es = new EmployeeSkill
                {
                    TenantId      = entity.TenantId,
                    CreatedById   = confirmedByUserId,
                    EmployeeId    = employee.Id,
                    SkillId       = cs.SkillId!.Value,
                    SkillLevel    = MapCandidateProficiency(cs.Proficiency),
                    IsCertified   = cs.IsCertified,
                    CertifyingBody = cs.CertificationName,
                    IsVerified    = false,
                };
                await _skillRepository.AddAsync(es);
            }
        }

        // Keep the originating StaffRequisition's fill counter in step with reality. Confirming a
        // hire fills one seat of the requisition that opened the vacancy; without this the
        // requisition's PositionsFilled only ever advances via a separate manual /fulfill call,
        // so requisition progress silently drifts from the number of people actually hired.
        await IncrementRequisitionFillAsync(entity, cancellationToken);

        await _hireRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Hire confirmed: {HireNumber}, Employee: {EmployeeId}",
            entity.HireNumber, entity.EmployeeId);

        // After the commit, best-effort, create-only — the hire stands whether or not payroll can
        // take the person today; a gap shows on the payroll reconciliation read.
        if (entity.EmployeeId is { } hiredEmployeeId)
        {
            var hired = await _employeeRepository.GetByIdAsync(hiredEmployeeId);
            if (hired != null)
                await _payrollMembership.EnsurePayrollProfileAsync(hired, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Advances the fill counter on the StaffRequisition that opened the vacancy this hire came
    /// through (hire → offer/application → JobVacancy → Requisition). Increments PositionsFilled
    /// by one (capped at NumberOfPositions) and, only from an in-progress state, promotes the
    /// requisition to PartiallyFulfilled / Fulfilled. Never over-counts, never resurrects a
    /// cancelled/on-hold requisition, and never downgrades one already Fulfilled.
    /// </summary>
    private async Task IncrementRequisitionFillAsync(JobHireRecord hire, CancellationToken cancellationToken)
    {
        var application = hire.Offer?.Application ?? hire.Application;
        if (application is null)
            return;

        var vacancy = await _unitOfWork.Repository<JobVacancy>().GetByIdAsync(application.JobVacancyId);
        if (vacancy is null || vacancy.TenantId != hire.TenantId)
            return;

        var requisition = await _unitOfWork.Repository<StaffRequisition>().GetByIdAsync(vacancy.StaffRequisitionId);
        if (requisition is null || requisition.TenantId != hire.TenantId)
            return;

        // Already counted for everyone requested — don't over-count on re-hires against the same req.
        if (requisition.PositionsFilled >= requisition.NumberOfPositions)
            return;

        requisition.PositionsFilled++;

        var fullyFilled = requisition.PositionsFilled >= requisition.NumberOfPositions;
        if (requisition.Status is StaffRequisitionStatus.Approved or StaffRequisitionStatus.PartiallyFulfilled)
        {
            requisition.Status = fullyFilled ? StaffRequisitionStatus.Fulfilled : StaffRequisitionStatus.PartiallyFulfilled;
            requisition.IsFulfilled = fullyFilled;
            if (fullyFilled)
                requisition.FulfilledDate = DateTime.UtcNow;
        }

        await _unitOfWork.Repository<StaffRequisition>().UpdateAsync(requisition);

        _logger.LogInformation(
            "Requisition {RequisitionNumber} fill advanced to {Filled}/{Total} by hire {HireNumber}.",
            requisition.RequisitionNumber, requisition.PositionsFilled, requisition.NumberOfPositions, hire.HireNumber);
    }

    private static SkillLevel MapCandidateProficiency(ProficiencyLevel? proficiency) => proficiency switch
    {
        ProficiencyLevel.Proficient                             => SkillLevel.Intermediate,
        ProficiencyLevel.Advanced or ProficiencyLevel.Expert   => SkillLevel.Advanced,
        _                                                       => SkillLevel.Beginner,
    };
}
