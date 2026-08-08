using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Peer Nomination

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

    // A nomination owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<PeerNomination> GetOwnedNominationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _nominationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Peer nomination with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<PeerNomination> TenantNominationQuery()
    {
        var tenantId = GetTenantId();
        return _nominationRepository.GetQueryable().Where(n => n.TenantId == tenantId);
    }

    private async Task<PerformanceAppraisal> GetOwnedAppraisalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Performance appraisal not found.");
        return entity;
    }

    private static void EnsureNominationsEditable(PerformanceAppraisal appraisal, AppraisalSettings settings)
    {
        if (settings.PeerNominationMode == PeerNominationMode.Employee)
        {
            // Employee-driven: nominations only editable during Open/SelfEvaluation
            if (appraisal.Status != AppraisalStatus.Active && appraisal.Status != AppraisalStatus.Draft)
            {
                throw new InvalidOperationException("Peer nominations are locked and cannot be edited after self-evaluation submission.");
            }
        }
        else // Manager
        {
            // Manager-driven: nominations editable until appraisal is completed or closed
            if (appraisal.Status == AppraisalStatus.Completed || appraisal.Status == AppraisalStatus.Closed)
            {
                throw new InvalidOperationException("Peer nominations are locked for completed or closed appraisals.");
            }
        }
    }

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

    public async Task<IEnumerable<PeerNominationDto>> GetByPeerEmployeeIdAsync(Guid peerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entities = await TenantNominationQuery()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => n.PeerEmployeeId == peerEmployeeId)
            .OrderByDescending(n => n.NominationDate)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<PeerNominationDto>> GetPendingNominationsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        // Get nominations where the employee is the peer and invitation not yet sent, or sent but not completed
        var entities = await TenantNominationQuery()
            .Include(n => n.Appraisal)
                .ThenInclude(a => a.EvaluatorEvaluations)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => n.PeerEmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        // Filter to those without a corresponding peer evaluation submitted
        var pending = entities.Where(n => 
            !n.Appraisal.EvaluatorEvaluations.Any(e => 
                e.EvaluatorId == employeeId && 
                e.EvaluatorRole == EvaluatorRole.Peer &&
                e.SubmittedDate.HasValue))
            .OrderBy(n => n.DueDate ?? DateTime.MaxValue)
            .ToList();

        return pending.ToDtoList();
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

        // Validate peer employee exists and is not the same as appraisee
        var peerEmployee = await _employeeRepository.GetByIdAsync(createDto.PeerEmployeeId);
        if (peerEmployee == null || peerEmployee.TenantId != tenantId)
            throw new ArgumentException("Peer employee not found.");

        if (createDto.PeerEmployeeId == appraisal.EmployeeId)
            throw new InvalidOperationException("Cannot nominate the appraisee as their own peer evaluator.");

        // Check if peer is already nominated
        var alreadyNominated = appraisal.PeerNominations.Any(n => n.PeerEmployeeId == createDto.PeerEmployeeId);
        if (alreadyNominated)
            throw new InvalidOperationException("This peer has already been nominated for this appraisal.");

        // Validate against max peer evaluators from settings
        if (settings.RequirePeerReviews && appraisal.PeerNominations.Count >= settings.MaxPeerEvaluators)
        {
            throw new InvalidOperationException($"Maximum of {settings.MaxPeerEvaluators} peer evaluators allowed.");
        }

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        await _nominationRepository.AddAsync(entity);
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

        updateDto.UpdateEntity(entity);

        await _nominationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Peer nomination updated successfully: {nominationId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await TenantNominationQuery()
            .Include(n => n.Appraisal)
                .ThenInclude(a => a.AppraisalCycle)
                    .ThenInclude(c => c.AppraisalSettings)
            .Include(n => n.Appraisal)
                .ThenInclude(a => a.PeerNominations)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        
        if (entity == null)
            throw new ArgumentException($"Peer nomination with ID '{id}' not found.");

        var settings = entity.Appraisal.AppraisalCycle.AppraisalSettings;
        EnsureNominationsEditable(entity.Appraisal, settings);

        // Don't allow deletion if invitation has been sent
        if (entity.InvitationSentDate.HasValue)
        {
            throw new InvalidOperationException("Cannot delete a peer nomination after invitation has been sent.");
        }

        await _nominationRepository.DeleteAsync(entity);

        // Update peer evaluators count only if the nomination was approved
        if (entity.NominationStatus == PeerNominationStatus.Approved)
        {
            var appraisal = entity.Appraisal;
            appraisal.PeerEvaluatorsCount = Math.Max(0, appraisal.PeerEvaluatorsCount - 1); // prevent count from going negative
            await _appraisalRepository.UpdateAsync(appraisal);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Peer nomination deleted: {nominationId}", id);

        return true;
    }

    public async Task<bool> SendInvitationAsync(SendPeerEvaluationInvitationDto invitationDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNominationAsync(invitationDto.PeerNominationId, cancellationToken);

        if (entity.InvitationSentDate.HasValue)
        {
            throw new InvalidOperationException("Invitation has already been sent for this peer nomination.");
        }

        entity.InvitationSentDate = DateTime.UtcNow;

        await _nominationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Peer evaluation invitation sent: {nominationId}", invitationDto.PeerNominationId);

        // TODO: Send email notification to peer employee

        return true;
    }

    public async Task<PeerNominationSummaryDto> GetNominationSummaryAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.PeerNominations)
                .ThenInclude(n => n.PeerEmployee)
            .Include(a => a.PeerNominations)
                .ThenInclude(n => n.NominatedBy)
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

        var canSubmit = nominations.Count >= settings.MinPeerEvaluators && 
                       nominations.Count <= settings.MaxPeerEvaluators;
        
        var canEdit = appraisal.Status == AppraisalStatus.Active || 
                     appraisal.Status == AppraisalStatus.Draft;

        return new PeerNominationSummaryDto
        {
            AppraisalId = appraisalId,
            TotalNominations = nominations.Count,
            PendingCount = pendingCount,
            ApprovedCount = approvedCount,
            RejectedCount = rejectedCount,
            MinRequired = settings.MinPeerEvaluators,
            MaxAllowed = settings.MaxPeerEvaluators,
            CanSubmit = canSubmit,
            CanEdit = canEdit,
            NominationMode = settings.PeerNominationMode,
            Nominations = nominations.ToDtoList()
        };
    }

    public async Task<IEnumerable<PeerNominationDto>> BatchCreateAsync(BatchCreatePeerNominationsDto batchDto, CancellationToken cancellationToken = default)
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

        // Validate count
        var totalAfterAdd = appraisal.PeerNominations.Count + batchDto.PeerEmployeeIds.Count;
        if (totalAfterAdd > settings.MaxPeerEvaluators)
        {
            throw new InvalidOperationException($"Adding {batchDto.PeerEmployeeIds.Count} peers would exceed the maximum of {settings.MaxPeerEvaluators}.");
        }

        // Check for duplicates in batch
        if (batchDto.PeerEmployeeIds.Distinct().Count() != batchDto.PeerEmployeeIds.Count)
        {
            throw new InvalidOperationException("Duplicate peer IDs found in nomination list.");
        }

        // Check for self-nomination
        if (batchDto.PeerEmployeeIds.Contains(appraisal.EmployeeId))
        {
            throw new InvalidOperationException("Cannot nominate the appraisee as their own peer evaluator.");
        }

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

        // Create nominations
        var createdNominations = new List<PeerNomination>();
        var nominatorId = appraisal.EmployeeId; // Default to employee

        // If manager-driven mode, we'd pass the manager ID differently (would need to be in DTO)
        foreach (var peerId in batchDto.PeerEmployeeIds)
        {
            var nomination = new PeerNomination
            {
                TenantId = tenantId,
                AppraisalId = batchDto.AppraisalId,
                PeerEmployeeId = peerId,
                NominatedById = nominatorId,
                NominationDate = DateTime.UtcNow,
                DueDate = batchDto.DueDate,
                InstructionsToPeer = batchDto.InstructionsToPeer,
                NominationStatus = PeerNominationStatus.Pending
            };

            await _nominationRepository.AddAsync(nomination);
            createdNominations.Add(nomination);
        }

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

        // Nominations sit at Pending until someone approves them, so the approver is told there
        // is something waiting. Who that is follows the cycle's nomination mode: when the
        // employee nominates, their manager signs the list off.
        var appraisee = await _employeeRepository.GetQueryable()
            .Where(e => e.Id == appraisal.EmployeeId && e.TenantId == tenantId)
            .Select(e => new { e.FullName, e.ManagerId })
            .FirstOrDefaultAsync(cancellationToken);

        var appraiseeName = appraisee?.FullName;
        var approverId = settings.PeerNominationMode == PeerNominationMode.Employee ? appraisee?.ManagerId : null;

        if (approverId is Guid managerId && managerId != Guid.Empty)
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
                    appraiseeName),
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
            .Include(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(a => a.Id == approvalDto.AppraisalId && a.TenantId == tenantId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Performance appraisal not found.");

        var nominations = appraisal.PeerNominations
            .Where(n => approvalDto.NominationIds.Contains(n.Id))
            .ToList();

        if (nominations.Count != approvalDto.NominationIds.Count)
            throw new ArgumentException("Some nominations were not found.");

        var settings = appraisal.AppraisalCycle.AppraisalSettings;

        foreach (var nomination in nominations)
        {
            if (nomination.NominationStatus != PeerNominationStatus.Pending)
                throw new InvalidOperationException($"Nomination {nomination.Id} is not in pending status.");

            // Update nomination status
            nomination.NominationStatus = PeerNominationStatus.Approved;
            nomination.ApprovedDate = DateTime.UtcNow;
            nomination.InvitationSentDate = DateTime.UtcNow;
            if (approvalDto.DueDate.HasValue)
                nomination.DueDate = approvalDto.DueDate;

            await _nominationRepository.UpdateAsync(nomination);

            // Create EvaluatorEvaluation record ONLY on approval
            var evaluatorEvaluation = new EvaluatorEvaluation
            {
                TenantId = tenantId,
                AppraisalId = appraisal.Id,
                EvaluatorId = nomination.PeerEmployeeId,
                EvaluatorRole = EvaluatorRole.Peer,
                EvaluatorWeight = settings.PeerEvaluationWeight,
                IsAuthoritative = false
            };

            await _evaluatorEvaluationRepository.AddAsync(evaluatorEvaluation);
        }

        // Update peer evaluators count to reflect approved nominations
        appraisal.PeerEvaluatorsCount += nominations.Count;
        await _appraisalRepository.UpdateAsync(appraisal);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        var reloadedNominations = await TenantNominationQuery()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => approvalDto.NominationIds.Contains(n.Id))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Approved {count} peer nominations for appraisal {appraisalId}",
            nominations.Count, approvalDto.AppraisalId);

        // Approval is what creates the peer's EvaluatorEvaluation, so it is the first moment the
        // peer has anything to do. In AfterSelfEval mode the form is not open yet — the employee
        // submitting their self-evaluation raises the "now open" notification instead.
        var appraiseeName = await _employeeRepository.GetQueryable()
            .Where(e => e.Id == appraisal.EmployeeId && e.TenantId == tenantId)
            .Select(e => e.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var opensNow = settings.PeerEvaluationOpenMode == PeerEvaluationOpenMode.WithSelfEval;
        var dueText = approvalDto.DueDate.HasValue ? $" Due {approvalDto.DueDate.Value:d MMM yyyy}." : string.Empty;

        await NotifyQuietlyAsync(nominations.Select(n => new AppraisalNotificationRequest(
            n.PeerEmployeeId,
            AppraisalNotificationType.PeerEvaluationAssigned,
            $"You have been asked to review {appraiseeName ?? "a colleague"}",
            opensNow
                ? $"Your peer feedback form is open.{dueText}"
                : $"Your peer feedback form opens once they submit their self-evaluation.{dueText}",
            appraisal.AppraisalCycle?.CycleName,
            "/hr/performance/peer-reviews",
            appraisal.Id,
            appraiseeName)), cancellationToken);

        return reloadedNominations.ToDtoList();
    }

    public async Task<IEnumerable<PeerNominationDto>> RejectNominationsAsync(RejectPeerNominationsDto rejectionDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedAppraisalAsync(rejectionDto.AppraisalId, cancellationToken);

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
                    $"/hr/performance/appraisals/{appraisal.Id}",
                    appraisal.Id,
                    appraisal.Employee?.FullName,
                    NotificationUrgency.Warning),
            }, cancellationToken);
        }

        return reloadedNominations.ToDtoList();
    }
}

#endregion Peer Nomination

