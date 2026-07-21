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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PeerNominationService> _logger;

    public PeerNominationService(
        IGenericRepository<PeerNomination> nominationRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<EvaluatorEvaluation> evaluatorEvaluationRepository,
        IUnitOfWork unitOfWork,
        ILogger<PeerNominationService> logger)
    {
        _nominationRepository = nominationRepository;
        _appraisalRepository = appraisalRepository;
        _employeeRepository = employeeRepository;
        _evaluatorEvaluationRepository = evaluatorEvaluationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
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
        var entity = await _nominationRepository.GetQueryable()
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
        var entities = await _nominationRepository.GetQueryable()
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
        var entities = await _nominationRepository.GetQueryable()
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
        var entities = await _nominationRepository.GetQueryable()
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
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.PeerNominations)
            .FirstOrDefaultAsync(a => a.Id == createDto.AppraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Performance appraisal not found.");

        var settings = appraisal.AppraisalCycle.AppraisalSettings;
        EnsureNominationsEditable(appraisal, settings);

        // Validate peer employee exists and is not the same as appraisee
        var peerEmployee = await _employeeRepository.GetByIdAsync(createDto.PeerEmployeeId);
        if (peerEmployee == null)
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
        entity = await _nominationRepository.GetQueryable()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .FirstOrDefaultAsync(n => n.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Peer nomination created successfully: {nominationId}", entity!.Id);

        return entity.ToDto();
    }

    /// <summary>True when an active nomination for this (appraisal, peer) already exists in the database.</summary>
    private async Task<bool> PeerAlreadyNominatedAsync(Guid appraisalId, Guid peerEmployeeId, CancellationToken cancellationToken)
        => await _nominationRepository.GetQueryable()
            .AnyAsync(n => n.AppraisalId == appraisalId && n.PeerEmployeeId == peerEmployeeId, cancellationToken);

    public async Task<PeerNominationDto> UpdateAsync(UpdatePeerNominationDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _nominationRepository.GetQueryable()
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
        var entity = await _nominationRepository.GetQueryable()
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
        var entity = await _nominationRepository.GetByIdAsync(invitationDto.PeerNominationId);
        
        if (entity == null)
            throw new ArgumentException($"Peer nomination with ID '{invitationDto.PeerNominationId}' not found.");

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
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.PeerNominations)
                .ThenInclude(n => n.PeerEmployee)
            .Include(a => a.PeerNominations)
                .ThenInclude(n => n.NominatedBy)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

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
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.PeerNominations)
            .FirstOrDefaultAsync(a => a.Id == batchDto.AppraisalId, cancellationToken);

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
                .Where(e => duplicates.Contains(e.Id))
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
        var reloadedNominations = await _nominationRepository.GetQueryable()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => createdNominations.Select(c => c.Id).Contains(n.Id))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Batch created {count} peer nominations for appraisal {appraisalId}", 
            createdNominations.Count, batchDto.AppraisalId);

        return reloadedNominations.ToDtoList();
    }

    public async Task<IEnumerable<PeerNominationDto>> ApproveNominationsAsync(ApprovePeerNominationsDto approvalDto, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.PeerNominations)
            .Include(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(a => a.Id == approvalDto.AppraisalId, cancellationToken);

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
        var reloadedNominations = await _nominationRepository.GetQueryable()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => approvalDto.NominationIds.Contains(n.Id))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Approved {count} peer nominations for appraisal {appraisalId}", 
            nominations.Count, approvalDto.AppraisalId);

        // TODO: Send email notifications to approved peers

        return reloadedNominations.ToDtoList();
    }

    public async Task<IEnumerable<PeerNominationDto>> RejectNominationsAsync(RejectPeerNominationsDto rejectionDto, CancellationToken cancellationToken = default)
    {
        var nominations = await _nominationRepository.GetQueryable()
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
        var reloadedNominations = await _nominationRepository.GetQueryable()
            .Include(n => n.Appraisal)
            .Include(n => n.PeerEmployee)
            .Include(n => n.NominatedBy)
            .Where(n => rejectionDto.NominationIds.Contains(n.Id))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Rejected {count} peer nominations for appraisal {appraisalId} with reason: {reason}", 
            nominations.Count, rejectionDto.AppraisalId, rejectionDto.RejectionReason);

        // TODO: Send notification to employee about rejection

        return reloadedNominations.ToDtoList();
    }
}

#endregion Peer Nomination

