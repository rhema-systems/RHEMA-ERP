using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Peer Nomination

/// <summary>
/// Who scores whom as a peer (performance closure lane D). A nomination is made Pending and approved
/// by the one approval path (<see cref="StageApprovalAsync"/>), which creates the peer's evaluation —
/// through the manager's approve, a Manager-mode nomination (the manager chooses, so there is no
/// approval step, D4) and HR's advance past the step (D-39). Only a pending nomination is changed or
/// withdrawn, and a rejected one leaves room for a replacement (D2).
/// </summary>
public class PeerNominationService : IPeerNominationService
{
    private readonly IGenericRepository<PeerNomination> _nominationRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<EvaluatorEvaluation> _evaluatorEvaluationRepository;
    private readonly IAppraisalNotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PeerNominationService> _logger;

    public PeerNominationService(
        IGenericRepository<PeerNomination> nominationRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<EvaluatorEvaluation> evaluatorEvaluationRepository,
        IAppraisalNotificationService notifications,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<PeerNominationService> logger)
    {
        _nominationRepository = nominationRepository;
        _appraisalRepository = appraisalRepository;
        _employeeRepository = employeeRepository;
        _evaluatorEvaluationRepository = evaluatorEvaluationRepository;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>
    /// Raises notifications without letting a notification failure undo the nomination change
    /// that produced it — the same best-effort contract used elsewhere in the appraisal run.
    /// </summary>
    private async Task NotifyQuietlyAsync(IEnumerable<AppraisalNotificationRequest> requests, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.RaiseAsync(requests, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to raise peer nomination notification(s); the nomination change stands.");
        }
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

    private IQueryable<PeerNomination> TenantNominationQuery()
    {
        var tenantId = GetTenantId();
        return _nominationRepository.GetQueryable().Where(n => n.TenantId == tenantId);
    }

    /// <summary>
    /// Whether nominations may be made, changed or decided on this appraisal: in Employee mode while
    /// it is Draft or Active, in Manager mode until it is completed or closed — and never once it is
    /// withdrawn (performance closure E-d1), which Manager mode let through: an approval there created
    /// a peer evaluation on an appraisal no one would finish, and asked the peer to write it. Only on an
    /// Open cycle (E-d2b). The caller loads the appraisal's cycle.
    /// </summary>
    private static bool NominationsEditable(PerformanceAppraisal appraisal, AppraisalSettings settings)
        => appraisal.AppraisalCycle != null && AppraisalLiveCycle.IsLive(appraisal.AppraisalCycle.Status)
            && (settings.PeerNominationMode == PeerNominationMode.Employee
                ? appraisal.Status is AppraisalStatus.Active or AppraisalStatus.Draft
                : appraisal.Status is not (AppraisalStatus.Completed or AppraisalStatus.Closed or AppraisalStatus.Withdrawn));

    private static void EnsureNominationsEditable(PerformanceAppraisal appraisal, AppraisalSettings settings)
    {
        // The appraisal's work is done while its cycle is Open (performance closure E-d2b).
        AppraisalLiveCycle.EnsureOpen(appraisal.AppraisalCycle.Status, appraisal.AppraisalCycle.CycleName,
            "Peer nominations cannot be changed");

        if (NominationsEditable(appraisal, settings)) return;

        if (appraisal.Status == AppraisalStatus.Withdrawn)
            throw new InvalidOperationException(
                "This appraisal was withdrawn from its cycle, so its peer nominations are closed.");

        // The Employee-mode message said "after self-evaluation submission"; the rule is the status.
        throw new InvalidOperationException(settings.PeerNominationMode == PeerNominationMode.Employee
            ? "Peer nominations are closed on this appraisal: it has moved past the evaluations."
            : "Peer nominations are locked for completed or closed appraisals.");
    }

    /// <summary>
    /// Who may nominate (performance closure P14 — the access half of D4): in Manager mode the
    /// manager chooses the peers, so the appraisee is refused; in Employee mode the appraisee
    /// nominates, and their manager or HR may add to the list for approval. Neither create path
    /// asked — the mode only timed the editing window — and the batch recorded every nomination as
    /// the appraisee's, whoever made it.
    /// </summary>
    private static void EnsureMayNominate(PerformanceAppraisal appraisal, AppraisalSettings settings, Guid nominatorId)
    {
        if (nominatorId == Guid.Empty)
            throw new ArgumentException("A nomination has to be made by an employee.");
        if (settings.PeerNominationMode == PeerNominationMode.Manager && nominatorId == appraisal.EmployeeId)
            throw new InvalidOperationException("In this cycle your manager chooses your peer evaluators.");
    }

    /// <summary>
    /// Who may be a peer (D1): an employee of this tenant, neither the appraisee nor the appraisee's line
    /// manager — the manager evaluates as the manager. Neither route refused the manager, and the batch
    /// took any id, found or not.
    ///
    /// <para>And one still at work (performance closure E-d1): a leaver was nominated as readily as
    /// anyone, and approving them asked someone who had gone to write an evaluation.</para>
    /// </summary>
    private async Task EnsurePeersMayBeNominatedAsync(
        PerformanceAppraisal appraisal, IReadOnlyCollection<Guid> peerIds, CancellationToken cancellationToken)
    {
        var tenantId = appraisal.TenantId;

        if (peerIds.Contains(appraisal.EmployeeId))
            throw new InvalidOperationException("Cannot nominate the appraisee as their own peer evaluator.");

        var managerId = await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && e.Id == appraisal.EmployeeId)
            .Select(e => e.ManagerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (managerId is Guid manager && peerIds.Contains(manager))
            throw new InvalidOperationException("The appraisee's manager evaluates as the manager, not as a peer.");

        var found = await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && peerIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        if (found.Count != peerIds.Distinct().Count())
            throw new ArgumentException("A nominated peer was not found.");

        await EnsurePeersAtWorkAsync(tenantId, peerIds, cancellationToken);
    }

    /// <summary>
    /// Refuses a peer who has left (E-d1) — the scope's own reading of who is at work: active, and
    /// not deleted. Checked when a peer is nominated and again when the nomination is approved,
    /// since a peer can leave in between.
    /// </summary>
    private async Task EnsurePeersAtWorkAsync(
        Guid tenantId, IReadOnlyCollection<Guid> peerIds, CancellationToken cancellationToken)
    {
        var gone = await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && peerIds.Contains(e.Id) && !e.IsActive)
            .Select(e => e.FirstName + " " + e.LastName)
            .ToListAsync(cancellationToken);

        if (gone.Count > 0)
            throw new InvalidOperationException(
                $"{string.Join(", ", gone)} {(gone.Count == 1 ? "is" : "are")} no longer at work, so cannot " +
                "evaluate as a peer. Nominate someone else.");
    }

    /// <summary>The nominations that stand or may: a rejected one no longer counts, so a replacement can be nominated (D2).</summary>
    private static int LiveCount(IEnumerable<PeerNomination> nominations)
        => nominations.Count(n => n.NominationStatus != PeerNominationStatus.Rejected);

    public async Task<PeerNominationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await TenantNominationQuery()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Peer nomination with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<PeerNominationDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var entities = await TenantNominationQuery()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => n.AppraisalId == appraisalId)
            .OrderByDescending(n => n.NominationDate)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    /// <summary>
    /// The nominations a peer has been asked to act on (D-41): approved ones only, without a rejection
    /// reason. This listed every nomination naming the peer — pending ones not yet decided, and rejected
    /// ones with the reason written for the appraisee. Not on a withdrawn appraisal (E-d1): nothing is
    /// asked of the peer there any more.
    /// </summary>
    public async Task<IEnumerable<PeerNominationDto>> GetByPeerEmployeeIdAsync(Guid peerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entities = await TenantNominationQuery()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => n.PeerEmployeeId == peerEmployeeId && n.NominationStatus == PeerNominationStatus.Approved
                        && n.Appraisal.Status != AppraisalStatus.Withdrawn)
            .OrderByDescending(n => n.NominationDate)
            .ToListAsync(cancellationToken);

        return AsPeerSees(entities);
    }

    /// <summary>
    /// The approved nominations whose peer evaluation this employee has not submitted yet (D-41) —
    /// on appraisals still in their cycle (E-d1).
    /// </summary>
    public async Task<IEnumerable<PeerNominationDto>> GetPendingNominationsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await TenantNominationQuery()
            .Include(n => n.Appraisal)
                .ThenInclude(a => a.EvaluatorEvaluations)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => n.PeerEmployeeId == employeeId && n.NominationStatus == PeerNominationStatus.Approved
                        && n.Appraisal.Status != AppraisalStatus.Withdrawn)
            .ToListAsync(cancellationToken);

        // Filter to those without a corresponding peer evaluation submitted
        var pending = entities.Where(n =>
            !n.Appraisal.EvaluatorEvaluations.Any(e =>
                e.EvaluatorId == employeeId &&
                e.EvaluatorRole == EvaluatorRole.Peer &&
                e.SubmittedDate.HasValue))
            .OrderBy(n => n.DueDate ?? DateTime.MaxValue)
            .ToList();

        return AsPeerSees(pending);
    }

    private static List<PeerNominationDto> AsPeerSees(IEnumerable<PeerNomination> nominations)
    {
        var dtos = nominations.ToDtoList();
        foreach (var dto in dtos) dto.RejectionReason = null;
        return dtos;
    }

    public async Task<PeerNominationDto> CreateAsync(CreatePeerNominationDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate appraisal exists
        var tenantId = GetTenantId();
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.PeerNominations)
            .FirstOrDefaultAsync(a => a.Id == createDto.AppraisalId && a.TenantId == tenantId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Performance appraisal not found.");

        var settings = appraisal.AppraisalCycle.AppraisalSettings;
        EnsureNominationsEditable(appraisal, settings);
        // The controller sets NominatedById from the token (P14).
        EnsureMayNominate(appraisal, settings, createDto.NominatedById);

        // D1: a nomination starts Pending — the body's status was stored as sent, so a raw POST made an
        // Approved nomination with no peer evaluation behind it.
        if (createDto.NominationStatus != PeerNominationStatus.Pending)
            throw new InvalidOperationException(
                "A nomination starts Pending: approving it is the manager's step, which asks the peer for their feedback.");

        await EnsurePeersMayBeNominatedAsync(appraisal, new[] { createDto.PeerEmployeeId }, cancellationToken);

        // Check if peer is already nominated (the unique index holds a rejected one too)
        var alreadyNominated = appraisal.PeerNominations.Any(n => n.PeerEmployeeId == createDto.PeerEmployeeId);
        if (alreadyNominated)
            throw new InvalidOperationException("This peer has already been nominated for this appraisal.");

        // Validate against max peer evaluators from settings — rejected ones leave room (D2).
        if (settings.RequirePeerReviews && LiveCount(appraisal.PeerNominations) >= settings.MaxPeerEvaluators)
        {
            throw new InvalidOperationException($"Maximum of {settings.MaxPeerEvaluators} peer evaluators allowed.");
        }

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        await _nominationRepository.AddAsync(entity);

        // D4: in Manager mode the manager chooses the peers, so there is no approval step — the
        // nomination is approved as it is made, and the peer asked.
        var approved = settings.PeerNominationMode == PeerNominationMode.Manager
            ? await StageApprovalAsync(appraisal, new[] { entity }, null, cancellationToken)
            : Array.Empty<PeerNomination>();

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique (AppraisalId, PeerEmployeeId) index may have rejected a concurrent duplicate.
            if (await PeerAlreadyNominatedAsync(createDto.AppraisalId, createDto.PeerEmployeeId, cancellationToken))
                throw new InvalidOperationException("This peer has already been nominated for this appraisal.");
            throw;
        }

        if (approved.Count > 0)
            await NotifyApprovedAsync(appraisal.Id, approved, null, cancellationToken);

        // Reload with includes
        entity = await TenantNominationQuery()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .FirstOrDefaultAsync(n => n.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Peer nomination created successfully: {nominationId}", entity!.Id);

        return entity.ToDto();
    }

    /// <summary>True when an active nomination for this (appraisal, peer) already exists in the database.</summary>
    private async Task<bool> PeerAlreadyNominatedAsync(Guid appraisalId, Guid peerEmployeeId, CancellationToken cancellationToken)
        => await TenantNominationQuery()
            .AnyAsync(n => n.AppraisalId == appraisalId && n.PeerEmployeeId == peerEmployeeId, cancellationToken);

    /// <summary>
    /// Changes a pending nomination's due date and instructions — nothing else (D1). The update took
    /// the appraisal, the peer, the nominator, the invitation date and the status from the body: a
    /// party could move a nomination onto another appraisal, mark it approved with no evaluation
    /// behind it, or clear the date that guarded its deletion.
    /// </summary>
    public async Task<PeerNominationDto> UpdateAsync(UpdatePeerNominationDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await TenantNominationQuery()
            .Include(n => n.Appraisal)
                .ThenInclude(a => a.AppraisalCycle)
                    .ThenInclude(c => c.AppraisalSettings)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .FirstOrDefaultAsync(n => n.Id == updateDto.Id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Peer nomination with ID '{updateDto.Id}' not found.");

        var settings = entity.Appraisal.AppraisalCycle.AppraisalSettings;
        EnsureNominationsEditable(entity.Appraisal, settings);

        if (entity.NominationStatus != PeerNominationStatus.Pending)
            throw new InvalidOperationException(
                "Only a pending nomination can be changed: an approved peer has their form, and a rejected nomination is closed.");

        updateDto.UpdateEntity(entity);

        await _nominationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Peer nomination updated successfully: {nominationId}", entity.Id);

        return entity.ToDto();
    }

    /// <summary>
    /// Withdraws a pending nomination (D5). An approved peer has their evaluation, and withdrawing them
    /// is not a nomination change — the decrement kept here for an approved one never ran, because
    /// approval stamps the invitation date that the old guard refused on.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await TenantNominationQuery()
            .Include(n => n.Appraisal)
                .ThenInclude(a => a.AppraisalCycle)
                    .ThenInclude(c => c.AppraisalSettings)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Peer nomination with ID '{id}' not found.");

        var settings = entity.Appraisal.AppraisalCycle.AppraisalSettings;
        EnsureNominationsEditable(entity.Appraisal, settings);

        if (entity.NominationStatus != PeerNominationStatus.Pending)
            throw new InvalidOperationException(
                "Only a pending nomination can be withdrawn: an approved peer has been asked for their feedback.");

        await _nominationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Peer nomination deleted: {nominationId}", id);

        return true;
    }

    /// <summary>
    /// The appraisal's nominations and where they stand. The counts leave rejected nominations out
    /// (D2), and the window follows the cycle's mode. In Manager mode with anonymous reviews the
    /// appraisee is told the counts only (D-40): the manager chose the peers, and with one peer the
    /// peer average the appraisee reads is that person's score.
    /// </summary>
    public async Task<PeerNominationSummaryDto> GetNominationSummaryAsync(
        Guid appraisalId, Guid? viewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.PeerNominations)
                .ThenInclude(n => n.PeerEmployee)
            .Include(a => a.PeerNominations)
                .ThenInclude(n => n.NominatedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == appraisalId && a.TenantId == tenantId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Performance appraisal not found.");

        if (appraisal.AppraisalCycle?.AppraisalSettings == null)
            throw new InvalidOperationException("Appraisal cycle or settings not configured.");

        var settings = appraisal.AppraisalCycle.AppraisalSettings;
        var nominations = appraisal.PeerNominations.ToList();

        var pendingCount = nominations.Count(n => n.NominationStatus == PeerNominationStatus.Pending);
        var approvedCount = nominations.Count(n => n.NominationStatus == PeerNominationStatus.Approved);
        var rejectedCount = nominations.Count(n => n.NominationStatus == PeerNominationStatus.Rejected);
        var live = pendingCount + approvedCount;

        var peersWithheld = viewerEmployeeId is Guid viewer && viewer == appraisal.EmployeeId
            && settings.PeerNominationMode == PeerNominationMode.Manager
            && settings.PeerReviewsAnonymous;

        return new PeerNominationSummaryDto
        {
            AppraisalId = appraisalId,
            TotalNominations = nominations.Count,
            ActiveNominations = live,
            PendingCount = pendingCount,
            ApprovedCount = approvedCount,
            RejectedCount = rejectedCount,
            MinRequired = settings.MinPeerEvaluators,
            MaxAllowed = settings.MaxPeerEvaluators,
            CanSubmit = live >= settings.MinPeerEvaluators && live <= settings.MaxPeerEvaluators,
            CanEdit = NominationsEditable(appraisal, settings),
            NominationMode = settings.PeerNominationMode,
            PeersWithheld = peersWithheld,
            Nominations = peersWithheld ? new List<PeerNominationDto>() : nominations.ToDtoList()
        };
    }

    public async Task<IEnumerable<PeerNominationDto>> BatchCreateAsync(BatchCreatePeerNominationsDto batchDto, Guid nominatedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.PeerNominations)
            .FirstOrDefaultAsync(a => a.Id == batchDto.AppraisalId && a.TenantId == tenantId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Performance appraisal not found.");

        var settings = appraisal.AppraisalCycle.AppraisalSettings;
        EnsureNominationsEditable(appraisal, settings);
        EnsureMayNominate(appraisal, settings, nominatedById);

        // Validate count — a rejected nomination leaves room for its replacement (D2).
        var totalAfterAdd = LiveCount(appraisal.PeerNominations) + batchDto.PeerEmployeeIds.Count;
        if (totalAfterAdd > settings.MaxPeerEvaluators)
        {
            throw new InvalidOperationException($"Adding {batchDto.PeerEmployeeIds.Count} peers would exceed the maximum of {settings.MaxPeerEvaluators}.");
        }

        // Check for duplicates in batch
        if (batchDto.PeerEmployeeIds.Distinct().Count() != batchDto.PeerEmployeeIds.Count)
        {
            throw new InvalidOperationException("Duplicate peer IDs found in nomination list.");
        }

        // Not the appraisee, not their manager, and each an employee of this tenant (D1).
        await EnsurePeersMayBeNominatedAsync(appraisal, batchDto.PeerEmployeeIds, cancellationToken);

        // Check for already nominated peers
        var existingPeerIds = appraisal.PeerNominations.Select(n => n.PeerEmployeeId).ToList();
        var duplicates = batchDto.PeerEmployeeIds.Intersect(existingPeerIds).ToList();
        if (duplicates.Any())
        {
            var employees = await _employeeRepository.GetQueryable()
                .Where(e => e.TenantId == tenantId && duplicates.Contains(e.Id))
                .Select(e => e.FullName)
                .ToListAsync(cancellationToken);
            throw new InvalidOperationException($"The following peers are already nominated: {string.Join(", ", employees)}");
        }

        // Create nominations — recorded as whoever made them (P14), from the caller's token.
        var createdNominations = new List<PeerNomination>();
        foreach (var peerId in batchDto.PeerEmployeeIds)
        {
            var nomination = new PeerNomination
            {
                TenantId = tenantId,
                AppraisalId = batchDto.AppraisalId,
                PeerEmployeeId = peerId,
                NominatedById = nominatedById,
                NominationDate = DateTime.UtcNow,
                DueDate = batchDto.DueDate,
                InstructionsToPeer = batchDto.InstructionsToPeer,
                NominationStatus = PeerNominationStatus.Pending
            };

            await _nominationRepository.AddAsync(nomination);
            createdNominations.Add(nomination);
        }

        // D4: in Manager mode the manager chooses the peers, so there is no approval step — the
        // nominations are approved as they are made and the peers asked. Nobody was told anything in
        // that mode: the list sat Pending for the manager's own approval.
        var managerMode = settings.PeerNominationMode == PeerNominationMode.Manager;
        var approved = managerMode
            ? await StageApprovalAsync(appraisal, createdNominations, batchDto.DueDate, cancellationToken)
            : Array.Empty<PeerNomination>();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        var reloadedNominations = await TenantNominationQuery()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => createdNominations.Select(c => c.Id).Contains(n.Id))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Batch created {count} peer nominations for appraisal {appraisalId}",
            createdNominations.Count, batchDto.AppraisalId);

        if (managerMode)
        {
            await NotifyApprovedAsync(appraisal.Id, approved, batchDto.DueDate, cancellationToken);
            return reloadedNominations.ToDtoList();
        }

        // Nominations sit at Pending until the employee's manager signs the list off, so the manager
        // is told there is something waiting.
        var appraisee = await _employeeRepository.GetQueryable()
            .Where(e => e.Id == appraisal.EmployeeId && e.TenantId == tenantId)
            .Select(e => new { e.FullName, e.ManagerId })
            .FirstOrDefaultAsync(cancellationToken);

        if (appraisee?.ManagerId is Guid managerId && managerId != Guid.Empty)
        {
            await NotifyQuietlyAsync(new[]
            {
                new AppraisalNotificationRequest(
                    managerId,
                    AppraisalNotificationType.PeerNominationSubmitted,
                    $"{createdNominations.Count} peer nomination(s) awaiting your approval",
                    "Approve or reject the nominated peers to open their feedback forms.",
                    appraisal.AppraisalCycle?.CycleName,
                    $"/hr/performance/team-appraisals/{appraisal.Id}",
                    appraisal.Id,
                    appraisee.FullName),
            }, cancellationToken);
        }

        return reloadedNominations.ToDtoList();
    }

    public async Task<IEnumerable<PeerNominationDto>> ApproveNominationsAsync(ApprovePeerNominationsDto approvalDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.PeerNominations)
            .FirstOrDefaultAsync(a => a.Id == approvalDto.AppraisalId && a.TenantId == tenantId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Performance appraisal not found.");

        // A decision on the list is made while it is open — it was taken on a completed appraisal too.
        EnsureNominationsEditable(appraisal, appraisal.AppraisalCycle.AppraisalSettings);

        var nominations = appraisal.PeerNominations
            .Where(n => approvalDto.NominationIds.Contains(n.Id))
            .ToList();

        if (nominations.Count != approvalDto.NominationIds.Count)
            throw new ArgumentException("Some nominations were not found.");

        foreach (var nomination in nominations)
        {
            if (nomination.NominationStatus != PeerNominationStatus.Pending)
                throw new InvalidOperationException($"Nomination {nomination.Id} is not in pending status.");
        }

        // A peer nominated while at work may have left since (E-d1).
        await EnsurePeersAtWorkAsync(tenantId, nominations.Select(n => n.PeerEmployeeId).Distinct().ToList(), cancellationToken);

        var approved = await StageApprovalAsync(appraisal, nominations, approvalDto.DueDate, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        var reloadedNominations = await TenantNominationQuery()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => approvalDto.NominationIds.Contains(n.Id))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Approved {count} peer nominations for appraisal {appraisalId}",
            approved.Count, approvalDto.AppraisalId);

        await NotifyApprovedAsync(appraisal.Id, approved, approvalDto.DueDate, cancellationToken);

        return reloadedNominations.ToDtoList();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The one approval path (performance closure D1, D4, D-39). HR's advance past the nomination step
    /// set the status alone, so its "approved" peers had no evaluation to fill in and were told nothing.
    /// ⚠ Nothing here calls the repositories' <c>UpdateAsync</c>: a Manager-mode nomination is still
    /// being added in the caller's unit of work, and an update would turn its INSERT into an UPDATE of
    /// a row that does not exist. The tracked entities carry the changes.
    /// </remarks>
    public async Task<IReadOnlyList<PeerNomination>> StageApprovalAsync(
        PerformanceAppraisal appraisal,
        IEnumerable<PeerNomination> nominations,
        DateTime? dueDate,
        CancellationToken cancellationToken = default)
    {
        var weight = appraisal.AppraisalCycle?.AppraisalSettings?.PeerEvaluationWeight
            ?? await _appraisalRepository.GetQueryable()
                .Where(a => a.Id == appraisal.Id)
                .Select(a => a.AppraisalCycle.AppraisalSettings.PeerEvaluationWeight)
                .FirstAsync(cancellationToken);

        // A peer already evaluating this appraisal is not given a second evaluation.
        var evaluating = (await _evaluatorEvaluationRepository.GetQueryable()
                .Where(e => e.TenantId == appraisal.TenantId && e.AppraisalId == appraisal.Id && e.EvaluatorRole == EvaluatorRole.Peer)
                .Select(e => e.EvaluatorId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var approved = new List<PeerNomination>();
        foreach (var nomination in nominations.Where(n => n.NominationStatus == PeerNominationStatus.Pending))
        {
            nomination.NominationStatus = PeerNominationStatus.Approved;
            nomination.ApprovedDate = now;
            nomination.InvitationSentDate = now;
            if (dueDate.HasValue)
                nomination.DueDate = dueDate;

            // The peer's evaluation — what approval exists to create.
            if (evaluating.Add(nomination.PeerEmployeeId))
            {
                await _evaluatorEvaluationRepository.AddAsync(new EvaluatorEvaluation
                {
                    TenantId = appraisal.TenantId,
                    AppraisalId = appraisal.Id,
                    EvaluatorId = nomination.PeerEmployeeId,
                    EvaluatorRole = EvaluatorRole.Peer,
                    EvaluatorWeight = weight
                });
            }

            approved.Add(nomination);
        }

        appraisal.PeerEvaluatorsCount += approved.Count;
        return approved;
    }

    /// <inheritdoc/>
    public async Task NotifyApprovedAsync(
        Guid appraisalId,
        IReadOnlyCollection<PeerNomination> approved,
        DateTime? dueDate,
        CancellationToken cancellationToken = default)
    {
        if (approved.Count == 0) return;

        var tenantId = GetTenantId();
        var context = await _appraisalRepository.GetQueryable()
            .Where(a => a.Id == appraisalId && a.TenantId == tenantId)
            .Select(a => new
            {
                AppraiseeName = a.Employee.FullName,
                a.AppraisalCycle.CycleName,
                a.AppraisalCycle.AppraisalSettings.PeerEvaluationOpenMode,
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Approval is what creates the peer's EvaluatorEvaluation, so it is the first moment the
        // peer has anything to do. In AfterSelfEval mode the form is not open yet — the employee
        // submitting their self-evaluation raises the "now open" notification instead.
        var opensNow = context?.PeerEvaluationOpenMode == PeerEvaluationOpenMode.WithSelfEval;
        var appraiseeName = context?.AppraiseeName;

        await NotifyQuietlyAsync(approved.Select(n =>
        {
            var due = dueDate ?? n.DueDate;
            var dueText = due.HasValue ? $" Due {due.Value:d MMM yyyy}." : string.Empty;
            return new AppraisalNotificationRequest(
                n.PeerEmployeeId,
                AppraisalNotificationType.PeerEvaluationAssigned,
                $"You have been asked to review {appraiseeName ?? "a colleague"}",
                opensNow
                    ? $"Your peer feedback form is open.{dueText}"
                    : $"Your peer feedback form opens once they submit their self-evaluation.{dueText}",
                context?.CycleName,
                "/me/performance/peer-reviews",
                appraisalId,
                appraiseeName);
        }), cancellationToken);
    }

    public async Task<IEnumerable<PeerNominationDto>> RejectNominationsAsync(RejectPeerNominationsDto rejectionDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var owner = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == rejectionDto.AppraisalId && a.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("Performance appraisal not found.");

        // A decision on the list is made while it is open, as an approval is.
        EnsureNominationsEditable(owner, owner.AppraisalCycle.AppraisalSettings);

        var nominations = await TenantNominationQuery()
            .Where(n => rejectionDto.NominationIds.Contains(n.Id) && n.AppraisalId == rejectionDto.AppraisalId)
            .ToListAsync(cancellationToken);

        if (nominations.Count != rejectionDto.NominationIds.Count)
            throw new ArgumentException("Some nominations were not found.");

        foreach (var nomination in nominations)
        {
            if (nomination.NominationStatus != PeerNominationStatus.Pending)
                throw new InvalidOperationException($"Nomination {nomination.Id} is not in pending status.");

            nomination.NominationStatus = PeerNominationStatus.Rejected;
            nomination.RejectionReason = rejectionDto.RejectionReason;
            await _nominationRepository.UpdateAsync(nomination);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        var reloadedNominations = await TenantNominationQuery()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => rejectionDto.NominationIds.Contains(n.Id))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Rejected {count} peer nominations for appraisal {appraisalId} with reason: {reason}",
            nominations.Count, rejectionDto.AppraisalId, rejectionDto.RejectionReason);

        // The appraisee has to know: a rejected nomination may leave them below the cycle's
        // minimum, and their self-evaluation cannot be submitted until the list is back in range.
        // They are the one who has to nominate someone else, so silence here would strand them.
        var appraisal = await _appraisalRepository.GetQueryable()
            .Where(a => a.Id == rejectionDto.AppraisalId && a.TenantId == tenantId)
            .Include(a => a.AppraisalCycle)
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(cancellationToken);

        if (appraisal is not null)
        {
            await NotifyQuietlyAsync(new[]
            {
                new AppraisalNotificationRequest(
                    appraisal.EmployeeId,
                    AppraisalNotificationType.ActionRequired,
                    $"{nominations.Count} of your peer nomination(s) were not approved",
                    $"Reason: {rejectionDto.RejectionReason}. Nominate a replacement if you are now below the minimum.",
                    appraisal.AppraisalCycle?.CycleName,
                    $"/me/performance/appraisals/{appraisal.Id}",
                    appraisal.Id,
                    appraisal.Employee?.FullName,
                    NotificationUrgency.Warning),
            }, cancellationToken);
        }

        return reloadedNominations.ToDtoList();
    }
}

#endregion Peer Nomination
