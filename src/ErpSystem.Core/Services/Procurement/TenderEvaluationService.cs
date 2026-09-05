using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderEvaluationService : ITenderEvaluationService
{
    private readonly ITenderEvaluationRepository _evaluationRepository;
    private readonly ITenderBidRepository _bidRepository;
    private readonly ITenderEvaluatorRepository _evaluatorRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderNotificationService _notificationService;
    private readonly IEvaluationCriterionRepository _criterionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<TenderEvaluationService> _logger;
    private readonly IAppEventBus _appEventBus;
    private readonly IProcurementTenderControlService _tenderControlService;
    private readonly IProcurementExceptionalSourcingControlService _exceptionalSourcingControlService;
    private readonly IProcurementEvaluationCommitteeControlService _evaluationCommittee;

    public TenderEvaluationService(
        ITenderEvaluationRepository evaluationRepository,
        ITenderBidRepository bidRepository,
        ITenderEvaluatorRepository evaluatorRepository,
        ITenderRepository tenderRepository,
        ITenderNotificationService notificationService,
        IEvaluationCriterionRepository criterionRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IAppEventBus appEventBus,
        IProcurementTenderControlService tenderControlService,
        IProcurementExceptionalSourcingControlService exceptionalSourcingControlService,
        IProcurementEvaluationCommitteeControlService evaluationCommittee,
        ILogger<TenderEvaluationService> logger)
    {
        _evaluationRepository = evaluationRepository;
        _bidRepository = bidRepository;
        _evaluatorRepository = evaluatorRepository;
        _tenderRepository = tenderRepository;
        _notificationService = notificationService;
        _criterionRepository = criterionRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _appEventBus = appEventBus;
        _tenderControlService = tenderControlService;
        _exceptionalSourcingControlService = exceptionalSourcingControlService;
        _evaluationCommittee = evaluationCommittee;
        _logger = logger;
    }

    public async Task<TenderEvaluationDto?> GetEvaluationByIdAsync(Guid id)
    {
        try
        {
            var evaluation = await _evaluationRepository.GetByIdAsync(id);
            return evaluation == null ? null : MapToDto(evaluation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluation {EvaluationId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<TenderEvaluationDto>> GetBidEvaluationsAsync(Guid bidId)
    {
        try
        {
            var evaluations = await _evaluationRepository.GetByBidIdAsync(bidId);
            return evaluations.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluations for bid {BidId}", bidId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderEvaluationDto>> GetMyEvaluationsAsync(Guid evaluatorId)
    {
        try
        {
            var evaluations = await _evaluationRepository.GetByEvaluatorIdAsync(evaluatorId);
            return evaluations.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluations for evaluator {EvaluatorId}", evaluatorId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderEvaluationDto>> GetMyEvaluationsByUserIdAsync(Guid userId)
    {
        try
        {
            // Get all TenderEvaluator records for this user
            var evaluators = await _evaluatorRepository.GetByUserIdAsync(userId);

            // Get all evaluations for these evaluators
            var allEvaluations = new List<TenderEvaluation>();
            foreach (var evaluator in evaluators)
            {
                var evaluations = await _evaluationRepository.GetByEvaluatorIdAsync(evaluator.Id);
                allEvaluations.AddRange(evaluations);
            }

            return allEvaluations.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluations for user {UserId}", userId);
            throw;
        }
    }

    public async Task<TenderEvaluationDto> CreateEvaluationAsync(CreateEvaluationDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(dto.TenderBidId)
                ?? throw new InvalidOperationException($"Bid with ID {dto.TenderBidId} not found");
            await EnsureLegacyEvaluationAllowedAsync(bid.TenderId);
            await EnsureEvaluationConfigurationAsync(bid.TenderId);
            EnsureBidEvaluationState(bid);
            await EnsurePaymentAdmissionForEvaluationAsync(bid);

            // A source committee owns membership; legacy assignments are only score-row projections.
            var evaluator = await _evaluationCommittee.EnsureTenderEvaluatorAsync(
                bid.TenderId, Guid.NewGuid().ToString("N"));
            if (evaluator is null)
            {
                var evaluators = await _evaluatorRepository.GetByUserIdAsync(_currentUserProvider.UserId);
                evaluator = evaluators.FirstOrDefault(item => item.TenderId == bid.TenderId &&
                    item.TenantId == _currentUserProvider.TenantId && !item.IsDeleted && item.Status != "Declined");
            }
            if (evaluator == null)
            {
                throw new InvalidOperationException("Current user is not assigned as an evaluator");
            }
            var existingEvaluations = await _evaluationRepository.GetByBidIdAsync(bid.Id);
            if (existingEvaluations.Any(item =>
                    item.TenderEvaluatorId == evaluator.Id &&
                    string.Equals(item.Status, "Draft", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ProcurementEvaluationCommitteeConflictException(
                    "EVALUATION_DRAFT_ALREADY_EXISTS",
                    "A Draft evaluation already exists for this bid and evaluator. Complete or delete it before creating another authorized attempt.");
            }

            // Calculate total score - use EvaluationCriteriaJson if provided, otherwise use legacy scores
            decimal totalScore = 0;
            if (!string.IsNullOrEmpty(dto.EvaluationCriteriaJson))
            {
                totalScore = CalculateTotalScoreFromCriteriaJson(dto.EvaluationCriteriaJson);
            }
            else
            {
                totalScore = ((dto.PriceScore ?? 0) + (dto.QualityScore ?? 0) + (dto.DeliveryScore ?? 0) +
                            (dto.ExperienceScore ?? 0) + (dto.TechnicalScore ?? 0) + (dto.ComplianceScore ?? 0)) / 6;
            }

            var evaluation = new TenderEvaluation
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderBidId = dto.TenderBidId,
                TenderEvaluatorId = evaluator.Id,
                EvaluationDate = DateTime.UtcNow,
                Status = "Draft",
                PriceScore = dto.PriceScore,
                QualityScore = dto.QualityScore,
                DeliveryScore = dto.DeliveryScore,
                ExperienceScore = dto.ExperienceScore,
                TechnicalScore = dto.TechnicalScore,
                ComplianceScore = dto.ComplianceScore,
                TotalScore = totalScore,
                EvaluationCriteriaJson = dto.EvaluationCriteriaJson,
                TechnicalComments = dto.TechnicalComments,
                CommercialComments = dto.CommercialComments,
                OverallComments = dto.OverallComments,
                IsRecommended = dto.IsRecommended,
                Recommendation = dto.Recommendation,
                CreatedById = _currentUserProvider.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _evaluationRepository.CreateAsync(evaluation);
            if (string.Equals(bid.Status, "Opened", StringComparison.OrdinalIgnoreCase))
            {
                bid.Status = "UnderEvaluation";
                bid.UpdatedAt = DateTime.UtcNow;
                await _bidRepository.UpdateAsync(bid);
            }
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created evaluation {EvaluationId} for bid {BidId}", evaluation.Id, dto.TenderBidId);

            // Publish event for admin-configurable notification topics (best-effort).
            try
            {
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = evaluation.TenantId,
                    EntityType = "Evaluation",
                    Activity = "Created",
                    Audience = "Internal",
                    EntityId = evaluation.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>
                    {
                        ["EvaluationId"] = evaluation.Id,
                        ["TenderBidId"] = evaluation.TenderBidId,
                        ["TenderId"] = bid.TenderId,
                        ["EvaluatorId"] = evaluation.TenderEvaluatorId,
                        ["Status"] = evaluation.Status ?? string.Empty,
                        ["TotalScore"] = evaluation.TotalScore ?? 0m
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish Evaluation.Created entity activity event for evaluation {EvaluationId}", evaluation.Id);
            }

            return MapToDto(evaluation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating evaluation for bid {BidId}", dto.TenderBidId);
            throw;
        }
    }

    public async Task<TenderEvaluationDto> UpdateEvaluationAsync(Guid id, UpdateEvaluationDto dto)
    {
        try
        {
            var evaluation = await _evaluationRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Evaluation with ID {id} not found");
            var tenderId = await EnsureLegacyEvaluationAllowedForBidAsync(evaluation.TenderBidId);
            await EnsureCurrentEvaluatorOwnsAsync(evaluation);
            await EnsureEvaluationConfigurationAsync(tenderId);

            if (evaluation.Status == "Submitted")
            {
                throw new InvalidOperationException("Cannot update submitted evaluation");
            }

            if (dto.PriceScore.HasValue) evaluation.PriceScore = dto.PriceScore.Value;
            if (dto.QualityScore.HasValue) evaluation.QualityScore = dto.QualityScore.Value;
            if (dto.DeliveryScore.HasValue) evaluation.DeliveryScore = dto.DeliveryScore.Value;
            if (dto.ExperienceScore.HasValue) evaluation.ExperienceScore = dto.ExperienceScore.Value;
            if (dto.TechnicalScore.HasValue) evaluation.TechnicalScore = dto.TechnicalScore.Value;
            if (dto.ComplianceScore.HasValue) evaluation.ComplianceScore = dto.ComplianceScore.Value;

            // Update EvaluationCriteriaJson if provided
            if (!string.IsNullOrEmpty(dto.EvaluationCriteriaJson))
            {
                evaluation.EvaluationCriteriaJson = dto.EvaluationCriteriaJson;
            }

            // Calculate total score - use EvaluationCriteriaJson if present, otherwise use legacy scores
            if (!string.IsNullOrEmpty(evaluation.EvaluationCriteriaJson))
            {
                evaluation.TotalScore = CalculateTotalScoreFromCriteriaJson(evaluation.EvaluationCriteriaJson);
            }
            else
            {
                evaluation.TotalScore = ((evaluation.PriceScore ?? 0) + (evaluation.QualityScore ?? 0) +
                                       (evaluation.DeliveryScore ?? 0) + (evaluation.ExperienceScore ?? 0) +
                                       (evaluation.TechnicalScore ?? 0) + (evaluation.ComplianceScore ?? 0)) / 6;
            }

            if (!string.IsNullOrEmpty(dto.TechnicalComments)) evaluation.TechnicalComments = dto.TechnicalComments;
            if (!string.IsNullOrEmpty(dto.CommercialComments)) evaluation.CommercialComments = dto.CommercialComments;
            if (!string.IsNullOrEmpty(dto.OverallComments)) evaluation.OverallComments = dto.OverallComments;
            evaluation.IsRecommended = dto.IsRecommended;
            if (!string.IsNullOrEmpty(dto.Recommendation)) evaluation.Recommendation = dto.Recommendation;

            evaluation.UpdatedAt = DateTime.UtcNow;

            await _evaluationRepository.UpdateAsync(evaluation);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated evaluation {EvaluationId}", id);

            return MapToDto(evaluation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating evaluation {EvaluationId}", id);
            throw;
        }
    }

    public async Task<TenderEvaluationDto> SubmitEvaluationAsync(Guid id, SubmitEvaluationDto dto)
    {
        try
        {
            var evaluation = await _evaluationRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Evaluation with ID {id} not found");
            var tenderId = await EnsureLegacyEvaluationAllowedForBidAsync(evaluation.TenderBidId);
            await EnsureCurrentEvaluatorOwnsAsync(evaluation);
            await EnsureEvaluationConfigurationAsync(tenderId);
            await EnsureCommitteeScorerAsync(tenderId, evaluation.TenderBidId);

            if (evaluation.Status == "Submitted")
            {
                throw new InvalidOperationException("Evaluation already submitted");
            }

            if (!dto.ConfirmSubmission)
            {
                throw new InvalidOperationException("Submission must be confirmed");
            }
            if (string.IsNullOrWhiteSpace(dto.SignatureReference) ||
                string.IsNullOrWhiteSpace(dto.EvidenceReference) ||
                string.IsNullOrWhiteSpace(dto.IdempotencyKey))
            {
                throw new ProcurementEvaluationCommitteeValidationException(
                    "EVALUATION_SCORE_SIGNATURE_REQUIRED",
                    "A score-sheet signature, evidence reference, and idempotency key are required.");
            }

            var submittedAtUtc = DateTime.UtcNow;

            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    await LockCommitteeScoreSheetAsync(
                        tenderId,
                        evaluation,
                        submittedAtUtc,
                        dto.SignatureReference.Trim(),
                        dto.EvidenceReference.Trim(),
                        dto.IdempotencyKey.Trim());
                    evaluation.Status = "Submitted";
                    evaluation.SubmittedDate = submittedAtUtc;
                    evaluation.UpdatedAt = submittedAtUtc;
                    await _evaluationRepository.UpdateAsync(evaluation);
                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitAsync();
                }
                catch
                {
                    await _unitOfWork.RollbackAsync();
                    throw;
                }
            });

            _logger.LogInformation("Submitted evaluation {EvaluationId}", id);

            // Publish event for admin-configurable notification topics (best-effort).
            try
            {
                var bid = await _bidRepository.GetByIdAsync(evaluation.TenderBidId);
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = evaluation.TenantId,
                    EntityType = "Evaluation",
                    Activity = "Submitted",
                    Audience = "Internal",
                    EntityId = evaluation.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>
                    {
                        ["EvaluationId"] = evaluation.Id,
                        ["TenderBidId"] = evaluation.TenderBidId,
                        ["TenderId"] = bid?.TenderId ?? Guid.Empty,
                        ["EvaluatorId"] = evaluation.TenderEvaluatorId,
                        ["Status"] = evaluation.Status ?? string.Empty,
                        ["SubmittedDate"] = evaluation.SubmittedDate?.ToString("o") ?? string.Empty,
                        ["TotalScore"] = evaluation.TotalScore ?? 0m
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish Evaluation.Submitted entity activity event for evaluation {EvaluationId}", id);
            }

            // Check if all evaluators have submitted their evaluations for this bid
            await CheckAndUpdateBidEvaluationStatusAsync(evaluation.TenderBidId);

            return MapToDto(evaluation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting evaluation {EvaluationId}", id);
            throw;
        }
    }

    /// <summary>
    /// Checks if all evaluators have submitted their evaluations for a bid and updates the bid status.
    /// Also checks if all bids for a tender have been evaluated and updates the tender status.
    /// </summary>
    private async Task CheckAndUpdateBidEvaluationStatusAsync(Guid bidId)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(bidId);
            if (bid == null) return;

            // Get all evaluators assigned to this tender
            var evaluators = await _evaluatorRepository.GetByTenderIdAsync(bid.TenderId);
            var committeeUsers = await _evaluationCommittee.GetTenderScoringUserIdsAsync(bid.TenderId);
            if (committeeUsers is not null)
            {
                evaluators = evaluators.Where(item => item.TenantId == _currentUserProvider.TenantId &&
                    committeeUsers.Contains(item.UserId)).ToList();
                if (committeeUsers.Count == 0 || committeeUsers.Any(userId => !evaluators.Any(item => item.UserId == userId)))
                    return; // A missing projection cannot reduce the required committee roster.
            }
            var assignedEvaluatorIds = evaluators.Select(e => e.Id).ToList();

            if (!assignedEvaluatorIds.Any()) return;

            // Get all evaluations for this bid
            var evaluations = await _evaluationRepository.GetByBidIdAsync(bidId);
            var submittedEvaluations = evaluations.Where(e => e.Status == "Submitted" &&
                    assignedEvaluatorIds.Contains(e.TenderEvaluatorId))
                .GroupBy(item => item.TenderEvaluatorId)
                .Select(group => group.OrderByDescending(item => item.SubmittedDate ?? item.EvaluationDate).First())
                .ToList();

            // Check if all evaluators have submitted for this bid
            var allEvaluatorsSubmitted = assignedEvaluatorIds.All(evaluatorId =>
                submittedEvaluations.Any(e => e.TenderEvaluatorId == evaluatorId));

            // Calculate and update bid's total score from submitted evaluations
            if (submittedEvaluations.Any())
            {
                var avgTotalScore = submittedEvaluations.Average(e => e.TotalScore ?? 0);
                bid.TotalScore = avgTotalScore;
                bid.EvaluatedDate = submittedEvaluations.Max(e => e.SubmittedDate ?? e.EvaluationDate);
                bid.UpdatedAt = DateTime.UtcNow;
            }

            if (allEvaluatorsSubmitted && bid.Status != "Evaluated")
            {
                // Update bid status to Evaluated
                bid.Status = "Evaluated";
                bid.UpdatedAt = DateTime.UtcNow;
            }

            // Save changes if we updated anything
            if (submittedEvaluations.Any() || (allEvaluatorsSubmitted && bid.Status == "Evaluated"))
            {
                await _bidRepository.UpdateAsync(bid);
                await _unitOfWork.SaveChangesAsync();

                if (allEvaluatorsSubmitted)
                {
                    _logger.LogInformation("All evaluators have submitted for bid {BidId}. Bid status updated to 'Evaluated' with total score {TotalScore}", bidId, bid.TotalScore);

                    // Publish events for admin-configurable notification topics (best-effort).
                    try
                    {
                        var baseData = new Dictionary<string, object>
                        {
                            ["BidId"] = bid.Id,
                            ["TenderId"] = bid.TenderId,
                            ["BusinessPartnerId"] = bid.BusinessPartnerId,
                            ["Status"] = bid.Status ?? string.Empty,
                            ["TotalScore"] = bid.TotalScore ?? 0m,
                            ["EvaluatedDate"] = bid.EvaluatedDate?.ToString("o") ?? string.Empty
                        };

                        await _appEventBus.PublishAsync(new EntityActivityEvent
                        {
                            TenantId = bid.TenantId,
                            EntityType = "Bid",
                            Activity = "Evaluated",
                            Audience = "Internal",
                            EntityId = bid.Id,
                            TriggeredByUserId = _currentUserProvider.UserId,
                            Data = new Dictionary<string, object>(baseData)
                        });

                        await _appEventBus.PublishAsync(new EntityActivityEvent
                        {
                            TenantId = bid.TenantId,
                            EntityType = "Bid",
                            Activity = "Evaluated",
                            Audience = "Supplier",
                            EntityId = bid.Id,
                            TriggeredByUserId = _currentUserProvider.UserId,
                            Data = new Dictionary<string, object>(baseData)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to publish Bid.Evaluated entity activity event for bid {BidId}", bidId);
                    }

                    // Check if all bids for this tender have been evaluated
                    await CheckAndUpdateTenderEvaluationStatusAsync(bid.TenderId);
                }
                else
                {
                    _logger.LogInformation("Updated bid {BidId} total score to {TotalScore} based on {EvaluatorCount} submitted evaluations", bidId, bid.TotalScore, submittedEvaluations.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking/updating bid evaluation status for bid {BidId}", bidId);
            // Don't throw - this is a side effect and shouldn't fail the main submission
        }
    }

    /// <summary>
    /// Checks if all submitted/opened bids for a tender have been evaluated and updates the tender status.
    /// </summary>
    private async Task CheckAndUpdateTenderEvaluationStatusAsync(Guid tenderId)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId);
            if (tender == null || tender.Status == "Evaluated" || tender.Status == "Awarded") return;

            // Get all bids that need to be evaluated (Opened or UnderEvaluation status)
            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);
            var bidsToEvaluate = bids.Where(b => b.Status == "Opened" || b.Status == "UnderEvaluation" || b.Status == "Evaluated").ToList();

            if (!bidsToEvaluate.Any()) return;

            // Check if all bids have been evaluated
            var allBidsEvaluated = bidsToEvaluate.All(b => b.Status == "Evaluated");

            if (allBidsEvaluated)
            {
                // Update tender status to Evaluated
                tender.Status = "Evaluated";
                tender.UpdatedAt = DateTime.UtcNow;
                await _tenderRepository.UpdateAsync(tender);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("All bids for tender {TenderId} have been evaluated. Tender status updated to 'Evaluated'", tenderId);

                // Publish event for admin-configurable notification topics (best-effort).
                try
                {
                    var data = new Dictionary<string, object>
                    {
                        ["TenderId"] = tender.Id,
                        ["TenderNumber"] = tender.TenderNumber ?? string.Empty,
                        ["Title"] = tender.Title ?? string.Empty,
                        ["Status"] = tender.Status ?? string.Empty
                    };

                    await _appEventBus.PublishAsync(new EntityActivityEvent
                    {
                        TenantId = tender.TenantId,
                        EntityType = "Tender",
                        Activity = "Evaluated",
                        Audience = "Internal",
                        EntityId = tender.Id,
                        TriggeredByUserId = _currentUserProvider.UserId,
                        Data = data
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish Tender.Evaluated entity activity event for tender {TenderId}", tenderId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking/updating tender evaluation status for tender {TenderId}", tenderId);
            // Don't throw - this is a side effect and shouldn't fail the main submission
        }
    }

    public async Task DeleteEvaluationAsync(Guid id)
    {
        try
        {
            var evaluation = await _evaluationRepository.GetByIdAsync(id);
            if (evaluation != null)
            {
                await EnsureLegacyEvaluationAllowedForBidAsync(evaluation.TenderBidId);
                await EnsureCurrentEvaluatorOwnsAsync(evaluation);
            }
            if (evaluation?.Status == "Submitted")
            {
                throw new ProcurementEvaluationCommitteeConflictException(
                    "EVALUATION_SCORE_SHEET_LOCKED",
                    "A submitted evaluation is immutable. Use the independently approved controlled-recall path to authorize a new attempt.");
            }
            if (evaluation != null)
            {
                await _evaluationRepository.DeleteAsync(id);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Deleted evaluation {EvaluationId}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting evaluation {EvaluationId}", id);
            throw;
        }
    }

    // Evaluation Reports
    public async Task<EvaluationScorecardDto> GetBidScorecardAsync(Guid bidId)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(bidId)
                ?? throw new InvalidOperationException($"Bid with ID {bidId} not found");

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);
            var evaluations = await _evaluationRepository.GetByBidIdAsync(bidId);

            return new EvaluationScorecardDto
            {
                TenderBidId = bidId,
                BidNumber = bid.BidNumber,
                BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
                TotalBidAmount = bid.TotalBidAmount,
                Currency = tender?.Currency ?? "USD",
                BidStatus = bid.Status,
                AveragePriceScore = evaluations.Any() ? evaluations.Average(e => e.PriceScore) : null,
                AverageQualityScore = evaluations.Any() ? evaluations.Average(e => e.QualityScore) : null,
                AverageDeliveryScore = evaluations.Any() ? evaluations.Average(e => e.DeliveryScore) : null,
                AverageExperienceScore = evaluations.Any() ? evaluations.Average(e => e.ExperienceScore) : null,
                AverageTechnicalScore = evaluations.Any() ? evaluations.Average(e => e.TechnicalScore) : null,
                AverageComplianceScore = evaluations.Any() ? evaluations.Average(e => e.ComplianceScore) : null,
                AverageTotalScore = evaluations.Any() ? evaluations.Average(e => e.TotalScore) : null,
                RecommendationCount = evaluations.Count(e => e.IsRecommended),
                TotalEvaluators = evaluations.Count(),
                Evaluations = evaluations.Select(MapToDto).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scorecard for bid {BidId}", bidId);
            throw;
        }
    }

    public async Task<EvaluationReportDto> GetTenderEvaluationReportAsync(Guid tenderId)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            // Get all evaluators for this tender
            var evaluators = await _evaluatorRepository.GetByTenderIdAsync(tenderId);
            var committeeUsers = await _evaluationCommittee.GetTenderScoringUserIdsAsync(tenderId);
            if (committeeUsers is not null)
                evaluators = evaluators.Where(item => item.TenantId == _currentUserProvider.TenantId &&
                    committeeUsers.Contains(item.UserId)).ToList();
            var totalEvaluators = committeeUsers?.Count ?? evaluators.Count();

            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);
            var bidEvaluations = new List<BidEvaluationSummaryDto>();

            foreach (var bid in bids)
            {
                var evaluations = await _evaluationRepository.GetByBidIdAsync(bid.Id);
                var submittedEvaluations = evaluations.Where(e => e.Status == "Submitted" &&
                        evaluators.Any(evaluator => evaluator.Id == e.TenderEvaluatorId))
                    .GroupBy(item => item.TenderEvaluatorId)
                    .Select(group => group.OrderByDescending(item => item.SubmittedDate ?? item.EvaluationDate).First())
                    .ToList();
                var avgTotalScore = submittedEvaluations.Any() ? (submittedEvaluations.Average(e => e.TotalScore) ?? 0m) : 0m;
                var recommendationCount = submittedEvaluations.Count(e => e.IsRecommended);

                // Determine if bid is fully evaluated (all evaluators have submitted)
                var allEvaluatorsSubmitted = totalEvaluators > 0 &&
                    (committeeUsers is null || committeeUsers.All(userId => evaluators.Any(item => item.UserId == userId))) &&
                    evaluators.All(evaluator => submittedEvaluations.Any(e => e.TenderEvaluatorId == evaluator.Id));

                // Calculate effective status - if all evaluators have submitted, it's "Evaluated"
                var effectiveStatus = allEvaluatorsSubmitted ? "Evaluated" : bid.Status;

                bidEvaluations.Add(new BidEvaluationSummaryDto
                {
                    BidId = bid.Id,
                    BidNumber = bid.BidNumber,
                    BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
                    TotalBidAmount = bid.TotalBidAmount,
                    BidStatus = effectiveStatus,
                    IsCompliant = bid.IsCompliant,
                    NonComplianceReasons = bid.NonComplianceReasons,
                    WeightedPriceScore = submittedEvaluations.Any() ? submittedEvaluations.Average(e => e.PriceScore) : null,
                    WeightedQualityScore = submittedEvaluations.Any() ? submittedEvaluations.Average(e => e.QualityScore) : null,
                    WeightedDeliveryScore = submittedEvaluations.Any() ? submittedEvaluations.Average(e => e.DeliveryScore) : null,
                    WeightedExperienceScore = submittedEvaluations.Any() ? submittedEvaluations.Average(e => e.ExperienceScore) : null,
                    FinalScore = avgTotalScore,
                    // QCBS Scores from bid entity
                    TechnicalScore = bid.TechnicalScore,
                    FinancialScore = bid.FinancialScore,
                    CombinedScore = bid.CombinedScore,
                    IsQualifiedTechnically = bid.IsQualifiedTechnically,
                    DisqualificationReason = bid.DisqualificationReason,
                    Rank = 0, // Will be assigned after sorting
                    RecommendationCount = recommendationCount,
                    IsRecommended = recommendationCount > 0
                });
            }

            // Sort by FinalScore descending and assign ranks
            var rankedBids = bidEvaluations
                .OrderByDescending(b => b.FinalScore)
                .ToList();

            for (int i = 0; i < rankedBids.Count; i++)
            {
                rankedBids[i].Rank = i + 1;
            }

            var compliantBids = bidEvaluations.Count(b => b.IsCompliant);
            var evaluatedBids = bidEvaluations.Count(b => b.BidStatus == "Evaluated");
            var topBid = rankedBids.FirstOrDefault();

            // Calculate QCBS-specific statistics
            var qualifiedBidsCount = tender.UseQCBSEvaluation
                ? bidEvaluations.Count(b => b.IsQualifiedTechnically == true)
                : (int?)null;
            var disqualifiedBidsCount = tender.UseQCBSEvaluation
                ? bidEvaluations.Count(b => b.IsQualifiedTechnically == false)
                : (int?)null;
            var lowestBidAmount = tender.UseQCBSEvaluation && bidEvaluations.Any(b => b.IsQualifiedTechnically == true)
                ? bidEvaluations.Where(b => b.IsQualifiedTechnically == true).Min(b => b.TotalBidAmount)
                : (decimal?)null;

            return new EvaluationReportDto
            {
                TenderId = tenderId,
                TenderNumber = tender.TenderNumber,
                TenderTitle = tender.Title,
                TenderType = tender.TenderType,
                PublishDate = tender.PublishDate,
                SubmissionDeadline = tender.SubmissionDeadline,
                EstimatedValue = tender.EstimatedValue,
                Currency = tender.Currency,
                PriceWeightage = tender.PriceWeightage,
                QualityWeightage = tender.QualityWeightage,
                DeliveryWeightage = tender.DeliveryWeightage,
                ExperienceWeightage = tender.ExperienceWeightage,
                // QCBS Configuration
                UseQCBSEvaluation = tender.UseQCBSEvaluation,
                TechnicalWeight = tender.TechnicalWeight,
                FinancialWeight = tender.FinancialWeight,
                MinimumTechnicalScore = tender.MinimumTechnicalScore,
                LowestBidAmount = lowestBidAmount,
                QualifiedBidsCount = qualifiedBidsCount,
                DisqualifiedBidsCount = disqualifiedBidsCount,
                TotalBidsReceived = bids.Count(),
                CompliantBids = compliantBids,
                NonCompliantBids = bids.Count() - compliantBids,
                EvaluatedBids = evaluatedBids,
                BidEvaluations = rankedBids,  // Use ranked list instead of unsorted list
                RecommendedBidId = topBid?.BidId,
                RecommendedBidNumber = topBid?.BidNumber,
                RecommendedBusinessPartnerName = topBid?.BusinessPartnerName,
                RecommendedBidAmount = topBid?.TotalBidAmount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluation report for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<IEnumerable<ConsolidatedEvaluationDto>> GetConsolidatedEvaluationsAsync(Guid tenderId)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);
            var scorecards = new List<EvaluationScorecardDto>();

            foreach (var bid in bids)
            {
                var scorecard = await GetBidScorecardAsync(bid.Id);
                scorecards.Add(scorecard);
            }

            // Sort by average total score (descending) and assign ranks
            var rankedScorecards = scorecards
                .OrderByDescending(s => s.AverageTotalScore ?? 0)
                .ToList();

            for (int i = 0; i < rankedScorecards.Count; i++)
            {
                rankedScorecards[i].Rank = i + 1;
            }

            return new List<ConsolidatedEvaluationDto>
            {
                new ConsolidatedEvaluationDto
                {
                    TenderId = tenderId,
                    TenderNumber = tender.TenderNumber,
                    TenderTitle = tender.Title,
                    BidScorecards = rankedScorecards,
                    GeneratedDate = DateTime.UtcNow,
                    GeneratedByName = _currentUserProvider.FullName
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving consolidated evaluations for tender {TenderId}", tenderId);
            throw;
        }
    }

    // Mapping methods
    /// <summary>
    /// Calculates total score from the EvaluationCriteriaJson by summing all weighted scores.
    /// The JSON contains an array of criteria scores with weightedScore for each criterion.
    /// </summary>
    private static decimal CalculateTotalScoreFromCriteriaJson(string criteriaJson)
    {
        try
        {
            if (string.IsNullOrEmpty(criteriaJson))
                return 0;

            // Use case-insensitive deserialization to handle camelCase from frontend
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var criteriaScores = System.Text.Json.JsonSerializer.Deserialize<List<CriteriaScoreEntry>>(criteriaJson, options);
            if (criteriaScores == null || criteriaScores.Count == 0)
                return 0;

            // Sum all weighted scores to get the total score
            return criteriaScores.Sum(c => c.WeightedScore);
        }
        catch (Exception)
        {
            // If JSON parsing fails, return 0
            return 0;
        }
    }

    /// <summary>
    /// Internal class to deserialize criteria scores from JSON
    /// </summary>
    private class CriteriaScoreEntry
    {
        public string CriterionId { get; set; } = string.Empty;
        public string CriterionName { get; set; } = string.Empty;
        public string CriterionCode { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal Weight { get; set; }
        public decimal MaxScore { get; set; }
        public decimal WeightedScore { get; set; }
    }

    /// <summary>
    /// Extracts criterion IDs from the EvaluationCriteriaJson
    /// </summary>
    private static List<Guid> ExtractCriterionIdsFromJson(string? criteriaJson)
    {
        var ids = new List<Guid>();
        if (string.IsNullOrEmpty(criteriaJson))
            return ids;

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var criteriaScores = System.Text.Json.JsonSerializer.Deserialize<List<CriteriaScoreEntry>>(criteriaJson, options);
            if (criteriaScores == null)
                return ids;

            foreach (var entry in criteriaScores)
            {
                if (Guid.TryParse(entry.CriterionId, out var id))
                {
                    ids.Add(id);
                }
            }
        }
        catch (Exception)
        {
            // If JSON parsing fails, return empty list
        }

        return ids;
    }

    /// <summary>
    /// Calculates separate Technical and Financial scores from the EvaluationCriteriaJson.
    /// Returns (technicalScore, financialScore, hasFinancialCriteria)
    /// </summary>
    private static (decimal technicalScore, decimal financialScore, bool hasFinancialCriteria) CalculateSeparateScoresFromCriteriaJson(
        string? criteriaJson,
        Dictionary<Guid, string> criteriaEvaluationTypes)
    {
        if (string.IsNullOrEmpty(criteriaJson))
            return (0, 0, false);

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var criteriaScores = System.Text.Json.JsonSerializer.Deserialize<List<CriteriaScoreEntry>>(criteriaJson, options);
            if (criteriaScores == null || criteriaScores.Count == 0)
                return (0, 0, false);

            decimal technicalWeightedSum = 0;
            decimal technicalTotalWeight = 0;
            decimal financialWeightedSum = 0;
            decimal financialTotalWeight = 0;

            foreach (var entry in criteriaScores)
            {
                if (!Guid.TryParse(entry.CriterionId, out var criterionId))
                    continue;

                // Determine evaluation type - default to Technical if not found
                var evaluationType = criteriaEvaluationTypes.TryGetValue(criterionId, out var type)
                    ? type
                    : "Technical";

                if (evaluationType == "Financial")
                {
                    financialWeightedSum += entry.WeightedScore;
                    financialTotalWeight += entry.Weight;
                }
                else // Technical (default)
                {
                    technicalWeightedSum += entry.WeightedScore;
                    technicalTotalWeight += entry.Weight;
                }
            }

            // Calculate normalized scores (0-100 scale)
            // WeightedScore is already calculated as: (score/maxScore) * 100 * (weight/100)
            // So the sum of WeightedScores gives us the total score out of 100
            decimal technicalScore = technicalWeightedSum;
            decimal financialScore = financialWeightedSum;

            // If there are financial criteria, normalize the financial score
            // to account for the fact that it's only a portion of the total weight
            if (financialTotalWeight > 0)
            {
                // Normalize to 0-100 scale based on the weight proportion
                financialScore = (financialWeightedSum / financialTotalWeight) * 100;
            }

            // Similarly normalize technical score if needed
            if (technicalTotalWeight > 0)
            {
                technicalScore = (technicalWeightedSum / technicalTotalWeight) * 100;
            }

            return (technicalScore, financialScore, financialTotalWeight > 0);
        }
        catch (Exception)
        {
            return (0, 0, false);
        }
    }

    private static TenderEvaluationDto MapToDto(TenderEvaluation evaluation)
    {
        // Debug: Log what we're getting
        var tenderBid = evaluation.TenderBid;
        var businessPartner = tenderBid?.BusinessPartner;
        var tender = tenderBid?.Tender;

        return new TenderEvaluationDto
        {
            Id = evaluation.Id,
            TenderBidId = evaluation.TenderBidId,
            TenderEvaluatorId = evaluation.TenderEvaluatorId,
            EvaluatorName = evaluation.TenderEvaluator?.User != null
                ? $"{evaluation.TenderEvaluator.User.FirstName} {evaluation.TenderEvaluator.User.LastName}".Trim()
                : string.Empty,
            EvaluationDate = evaluation.EvaluationDate,
            Status = evaluation.Status,

            // Tender and Business Partner Information
            TenderId = tenderBid?.TenderId,
            TenderNumber = tender?.TenderNumber ?? string.Empty,
            TenderTitle = tender?.Title ?? string.Empty,
            BusinessPartnerName = businessPartner?.PartnerName ?? string.Empty,
            BidNumber = tenderBid?.BidNumber ?? string.Empty,

            PriceScore = evaluation.PriceScore,
            QualityScore = evaluation.QualityScore,
            DeliveryScore = evaluation.DeliveryScore,
            ExperienceScore = evaluation.ExperienceScore,
            TechnicalScore = evaluation.TechnicalScore,
            ComplianceScore = evaluation.ComplianceScore,
            TotalScore = evaluation.TotalScore,
            EvaluationCriteriaJson = evaluation.EvaluationCriteriaJson,
            TechnicalComments = evaluation.TechnicalComments,
            CommercialComments = evaluation.CommercialComments,
            OverallComments = evaluation.OverallComments,
            IsRecommended = evaluation.IsRecommended,
            Recommendation = evaluation.Recommendation,
            SubmittedDate = evaluation.SubmittedDate
        };
    }

    /// <summary>
    /// Calculates QCBS (Quality and Cost Based Selection) scores for all bids in a tender.
    /// Formula: Combined Score = (Technical Weight × Technical Score) + (Financial Weight × Financial Score)
    /// Technical Score = Weighted average of Technical evaluation criteria scores
    /// Financial Score = Combination of price-based score and Financial evaluation criteria scores
    /// Price-based Score = (Lowest Bid Amount / Bid Amount) × 100
    /// </summary>
    public async Task<QCBSEvaluationResultDto> CalculateQCBSScoresAsync(Guid tenderId)
    {
        try
        {
            await EnsureLegacyEvaluationAllowedAsync(tenderId);
            await EnsureLegacyDecisionReadyAsync(tenderId);
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            if (!tender.UseQCBSEvaluation)
            {
                throw new InvalidOperationException("This tender is not configured for QCBS evaluation");
            }
            await TenderEvaluationConfiguration.ValidateAsync(_unitOfWork, tender);

            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);
            var evaluatedBids = bids.Where(b => b.Status == "Evaluated" || b.Status == "Opened").ToList();

            if (!evaluatedBids.Any())
            {
                throw new InvalidOperationException("No evaluated bids found for this tender");
            }

            foreach (var bid in evaluatedBids)
                await EnsurePaymentAdmissionForEvaluationAsync(bid);

            var bidScores = new List<QCBSBidScoreDto>();
            var technicalWeight = tender.TechnicalWeight / 100m;
            var financialWeight = tender.FinancialWeight / 100m;
            var minimumTechnicalScore = tender.MinimumTechnicalScore;

            // Collect all criterion IDs from all evaluations to fetch their EvaluationType
            var allCriterionIds = new HashSet<Guid>();
            var bidEvaluationsMap = new Dictionary<Guid, List<TenderEvaluation>>();

            foreach (var bid in evaluatedBids)
            {
                var evaluations = await _evaluationRepository.GetByBidIdAsync(bid.Id);
                var submittedEvaluations = evaluations.Where(e => e.Status == "Submitted").ToList();
                bidEvaluationsMap[bid.Id] = submittedEvaluations;

                foreach (var eval in submittedEvaluations)
                {
                    if (!string.IsNullOrEmpty(eval.EvaluationCriteriaJson))
                    {
                        var criteriaIds = ExtractCriterionIdsFromJson(eval.EvaluationCriteriaJson);
                        foreach (var id in criteriaIds)
                        {
                            allCriterionIds.Add(id);
                        }
                    }
                }
            }

            // Fetch all criteria to get their EvaluationType
            var criteria = await _criterionRepository.GetByIdsAsync(allCriterionIds);
            var criteriaEvaluationTypes = criteria.ToDictionary(c => c.Id, c => c.EvaluationType);

            // First pass: Calculate technical and financial criteria scores, identify qualified bids
            foreach (var bid in evaluatedBids)
            {
                var submittedEvaluations = bidEvaluationsMap[bid.Id];

                // Calculate scores from evaluations, separating Technical and Financial criteria
                decimal technicalScore = 0;
                decimal financialCriteriaScore = 0;
                bool hasFinancialCriteria = false;

                if (submittedEvaluations.Any())
                {
                    var technicalScores = new List<decimal>();
                    var financialScores = new List<decimal>();

                    foreach (var eval in submittedEvaluations)
                    {
                        var (techScore, finScore, hasFinCriteria) = CalculateSeparateScoresFromCriteriaJson(
                            eval.EvaluationCriteriaJson, criteriaEvaluationTypes);

                        technicalScores.Add(techScore);
                        if (hasFinCriteria)
                        {
                            financialScores.Add(finScore);
                            hasFinancialCriteria = true;
                        }
                    }

                    technicalScore = technicalScores.Any() ? technicalScores.Average() : 0;
                    financialCriteriaScore = financialScores.Any() ? financialScores.Average() : 0;
                }

                var isQualified = technicalScore >= minimumTechnicalScore;
                var disqualificationReason = !isQualified
                    ? $"Technical score ({technicalScore:F2}) is below minimum threshold ({minimumTechnicalScore})"
                    : null;

                bidScores.Add(new QCBSBidScoreDto
                {
                    BidId = bid.Id,
                    BidNumber = bid.BidNumber,
                    BusinessPartnerId = bid.BusinessPartnerId,
                    BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
                    TotalBidAmount = bid.TotalBidAmount,
                    Currency = bid.Currency ?? tender.Currency ?? "USD",
                    TechnicalScore = technicalScore,
                    IsQualifiedTechnically = isQualified,
                    DisqualificationReason = disqualificationReason,
                    // Store financial criteria score temporarily for second pass
                    FinancialScore = financialCriteriaScore
                });
            }

            // Find lowest bid amount among qualified bids
            var qualifiedBids = bidScores.Where(b => b.IsQualifiedTechnically).ToList();
            decimal? lowestBidAmount = qualifiedBids.Any()
                ? qualifiedBids.Min(b => b.TotalBidAmount)
                : null;

            // Check if any bid has financial criteria scores
            bool anyFinancialCriteria = bidScores.Any(b => b.FinancialScore > 0);

            // Second pass: Calculate final financial scores and combined scores for qualified bids
            foreach (var bidScore in bidScores)
            {
                if (bidScore.IsQualifiedTechnically && lowestBidAmount.HasValue && bidScore.TotalBidAmount > 0)
                {
                    // Price-based Score = (Lowest Bid / Current Bid) × 100
                    decimal priceBasedScore = (lowestBidAmount.Value / bidScore.TotalBidAmount) * 100;

                    // If there are financial criteria, combine price-based score with financial criteria score
                    // Financial Score = (Price-based Score × 50%) + (Financial Criteria Score × 50%)
                    // If no financial criteria, use only price-based score
                    if (anyFinancialCriteria && bidScore.FinancialScore > 0)
                    {
                        bidScore.FinancialScore = (priceBasedScore * 0.5m) + (bidScore.FinancialScore * 0.5m);
                    }
                    else
                    {
                        bidScore.FinancialScore = priceBasedScore;
                    }

                    // Combined Score = (Technical Weight × Technical Score) + (Financial Weight × Financial Score)
                    bidScore.CombinedScore = (technicalWeight * bidScore.TechnicalScore) +
                                            (financialWeight * bidScore.FinancialScore);
                }
                else
                {
                    bidScore.FinancialScore = 0;
                    bidScore.CombinedScore = 0;
                }
            }

            // Rank bids by combined score (qualified bids first, then by score descending)
            var rankedBids = bidScores
                .OrderByDescending(b => b.IsQualifiedTechnically)
                .ThenByDescending(b => b.CombinedScore)
                .ToList();

            for (int i = 0; i < rankedBids.Count; i++)
            {
                rankedBids[i].Rank = i + 1;
            }

            // Update bid entities with QCBS scores
            foreach (var bidScore in bidScores)
            {
                var bid = evaluatedBids.First(b => b.Id == bidScore.BidId);
                bid.TechnicalScore = bidScore.TechnicalScore;
                bid.FinancialScore = bidScore.FinancialScore;
                bid.CombinedScore = bidScore.CombinedScore;
                bid.IsQualifiedTechnically = bidScore.IsQualifiedTechnically;
                bid.DisqualificationReason = bidScore.DisqualificationReason;
                bid.Rank = bidScore.Rank;
                bid.UpdatedAt = DateTime.UtcNow;

                await _bidRepository.UpdateAsync(bid);
            }

            await _unitOfWork.SaveChangesAsync();

            var recommendedBid = rankedBids.FirstOrDefault(b => b.IsQualifiedTechnically);

            _logger.LogInformation(
                "Calculated QCBS scores for tender {TenderId}. Total bids: {TotalBids}, Qualified: {QualifiedBids}",
                tenderId, rankedBids.Count, qualifiedBids.Count);

            return new QCBSEvaluationResultDto
            {
                TenderId = tenderId,
                TenderNumber = tender.TenderNumber,
                TenderTitle = tender.Title,
                TechnicalWeight = tender.TechnicalWeight,
                FinancialWeight = tender.FinancialWeight,
                MinimumTechnicalScore = minimumTechnicalScore,
                LowestBidAmount = lowestBidAmount,
                TotalBids = rankedBids.Count,
                QualifiedBids = qualifiedBids.Count,
                DisqualifiedBids = rankedBids.Count - qualifiedBids.Count,
                BidScores = rankedBids,
                RecommendedBid = recommendedBid,
                CalculatedAt = DateTime.UtcNow,
                CalculatedByName = _currentUserProvider.FullName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating QCBS scores for tender {TenderId}", tenderId);
            throw;
        }
    }

    /// <summary>
    /// Gets the stored QCBS evaluation results for a tender without recalculating.
    /// Returns null if QCBS evaluation has not been run yet.
    /// </summary>
    public async Task<QCBSEvaluationResultDto?> GetQCBSEvaluationResultsAsync(Guid tenderId)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            if (!tender.UseQCBSEvaluation)
            {
                return null;
            }

            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);
            var evaluatedBids = bids.Where(b => b.TechnicalScore.HasValue || b.CombinedScore.HasValue).ToList();

            if (!evaluatedBids.Any())
            {
                return null; // QCBS evaluation has not been run yet
            }

            var technicalWeight = tender.TechnicalWeight / 100m;
            var financialWeight = tender.FinancialWeight / 100m;
            var minimumTechnicalScore = tender.MinimumTechnicalScore;

            var bidScores = evaluatedBids.Select(bid => new QCBSBidScoreDto
            {
                BidId = bid.Id,
                BidNumber = bid.BidNumber,
                BusinessPartnerId = bid.BusinessPartnerId,
                BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
                TotalBidAmount = bid.TotalBidAmount,
                Currency = bid.Currency ?? tender.Currency ?? "USD",
                TechnicalScore = bid.TechnicalScore ?? 0,
                FinancialScore = bid.FinancialScore ?? 0,
                CombinedScore = bid.CombinedScore ?? 0,
                IsQualifiedTechnically = bid.IsQualifiedTechnically,
                DisqualificationReason = bid.DisqualificationReason,
                Rank = bid.Rank ?? 0,
                IsRecommendedForAward = bid.Rank == 1 && bid.IsQualifiedTechnically
            }).OrderBy(b => b.Rank == 0 ? int.MaxValue : b.Rank).ToList();

            var qualifiedBids = bidScores.Where(b => b.IsQualifiedTechnically).ToList();
            var lowestBidAmount = qualifiedBids.Any() ? qualifiedBids.Min(b => b.TotalBidAmount) : (decimal?)null;
            var recommendedBid = bidScores.FirstOrDefault(b => b.IsRecommendedForAward);

            return new QCBSEvaluationResultDto
            {
                TenderId = tenderId,
                TenderNumber = tender.TenderNumber,
                TenderTitle = tender.Title,
                TechnicalWeight = tender.TechnicalWeight,
                FinancialWeight = tender.FinancialWeight,
                MinimumTechnicalScore = minimumTechnicalScore,
                LowestBidAmount = lowestBidAmount,
                TotalBids = bidScores.Count,
                QualifiedBids = qualifiedBids.Count,
                DisqualifiedBids = bidScores.Count - qualifiedBids.Count,
                BidScores = bidScores,
                RecommendedBid = recommendedBid,
                CalculatedAt = tender.UpdatedAt ?? DateTime.UtcNow,
                CalculatedByName = null // Not available from stored data
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting QCBS evaluation results for tender {TenderId}", tenderId);
            throw;
        }
    }

    private async Task EnsureCommitteeScorerAsync(Guid tenderId, Guid tenderBidId)
    {
        var result = await _evaluationCommittee.EnsureScoreSubjectEligibleAsync(
            ProcurementEvaluationSourceType.Tender,
            tenderId,
            ProcurementEvaluationPhase.Combined,
            "TenderEvaluation",
            tenderBidId,
            $"TDC0208-LEGACY-{Guid.NewGuid():N}",
            CancellationToken.None);
        if (!result.Allowed)
        {
            var message = string.Join(" ", result.BlockedReasons);
            if (result.BlockedReasons.Any(reason =>
                    reason.Contains("not appointed", StringComparison.OrdinalIgnoreCase)))
                throw new ProcurementEvaluationCommitteeAuthorizationException(message);
            throw new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_SCORER_INELIGIBLE", message);
        }
    }

    private async Task LockCommitteeScoreSheetAsync(
        Guid tenderId,
        TenderEvaluation evaluation,
        DateTime submittedAtUtc,
        string signatureReference,
        string evidenceReference,
        string idempotencyKey)
    {
        var correlationId = $"TDC0208-LEGACY-{Guid.NewGuid():N}";
        var eligibility = await _evaluationCommittee.EnsureScoreSubjectEligibleAsync(
            ProcurementEvaluationSourceType.Tender,
            tenderId,
            ProcurementEvaluationPhase.Combined,
            "TenderEvaluation",
            evaluation.TenderBidId,
            correlationId,
            CancellationToken.None);
        if (!eligibility.Allowed)
        {
            var message = string.Join(" ", eligibility.BlockedReasons);
            if (eligibility.BlockedReasons.Any(reason =>
                    reason.Contains("not appointed", StringComparison.OrdinalIgnoreCase)))
                throw new ProcurementEvaluationCommitteeAuthorizationException(message);
            throw new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_SCORER_INELIGIBLE", message);
        }
        var committee = await _evaluationCommittee.GetAsync(
            ProcurementEvaluationSourceType.Tender,
            tenderId,
            CancellationToken.None);
        var appointment = committee.Members.Single(item => item.Id == eligibility.AppointmentId);
        var meeting = committee.Meetings.Single(item => item.Id == eligibility.MeetingId);
        var snapshot = BuildLegacyScoreSnapshot(evaluation, submittedAtUtc);

        await _evaluationCommittee.LockScoreSheetAsync(
            new LockProcurementEvaluationScoreSheetRequest
            {
                SourceType = ProcurementEvaluationSourceType.Tender,
                SourceId = tenderId,
                Phase = ProcurementEvaluationPhase.Combined,
                ScoreSubjectType = "TenderEvaluation",
                ScoreSubjectId = evaluation.TenderBidId,
                MeetingId = meeting.Id,
                AppointmentId = appointment.Id,
                CommitteeRowVersion = committee.RowVersion,
                MeetingRowVersion = meeting.RowVersion,
                AppointmentRowVersion = appointment.RowVersion,
                ScoreSnapshotJson = snapshot,
                SignatureReference = signatureReference,
                EvidenceReference = evidenceReference,
                IdempotencyKey = idempotencyKey
            },
            correlationId,
            CancellationToken.None);
    }

    private async Task EnsureLegacyDecisionReadyAsync(Guid tenderId)
    {
        var committee = await _evaluationCommittee.GetAsync(
            ProcurementEvaluationSourceType.Tender,
            tenderId,
            CancellationToken.None);
        if (!committee.CompositionReady || !committee.QuorumMet)
            throw new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_COMMITTEE_NOT_READY",
                "Committee composition and signed quorum must remain complete before consolidated scoring.");
        var latest = committee.ScoreSheets
            .Where(item =>
                item.Phase == ProcurementEvaluationPhase.Combined &&
                item.ScoreSubjectType == "TenderEvaluation")
            .GroupBy(item => new { item.AppointmentId, item.ScoreSubjectId })
            .Select(group => group
                .OrderByDescending(item => item.Attempt)
                .ThenByDescending(item => item.SubmittedAtUtc)
                .First())
            .ToList();
        if (latest.Count == 0 ||
            latest.Any(item => item.Status != ProcurementEvaluationScoreSheetStatus.Locked))
            throw new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_SCORE_SHEETS_NOT_CURRENT",
                "Every consolidated score must use a current locked attempt; recalled attempts cannot satisfy readiness.");
        var currentIds = latest.Select(item => item.Id).ToHashSet();
        if (committee.Recalls.Any(item =>
                currentIds.Contains(item.ScoreSheetId) &&
                item.Status is ProcurementEvaluationScoreRecallStatus.PendingApproval or
                    ProcurementEvaluationScoreRecallStatus.Approved))
            throw new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_SCORE_RECALL_UNRESOLVED",
                "Consolidated scoring cannot proceed while the current score sheet has a pending recall or an approved recall without its authorized locked replacement attempt.");

        var bids = await _bidRepository.GetByTenderIdAsync(tenderId);
        var currentSubmitted = new List<TenderEvaluation>();
        foreach (var bid in bids)
        {
            var evaluations = await _evaluationRepository.GetByBidIdAsync(bid.Id);
            currentSubmitted.AddRange(evaluations
                .Where(item => string.Equals(item.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.TenderEvaluatorId)
                .Select(group => group
                    .OrderByDescending(item => item.SubmittedDate ?? item.UpdatedAt ?? item.EvaluationDate)
                    .ThenByDescending(item => item.CreatedAt)
                    .First()));
        }
        if (currentSubmitted.Count == 0)
            throw new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_SCORE_PROJECTION_INCOMPLETE",
                "Every current submitted tender evaluation must retain a matching current locked committee score sheet.");

        var expectedByEvaluationId = currentSubmitted.ToDictionary(item => item.Id);
        var matchedEvaluationIds = new HashSet<Guid>();
        foreach (var sheet in latest)
        {
            var evaluationId = ReadGuid(sheet.ScoreSnapshotJson, "evaluationId");
            if (!evaluationId.HasValue ||
                !expectedByEvaluationId.TryGetValue(evaluationId.Value, out var evaluation) ||
                evaluation.TenderBidId != sheet.ScoreSubjectId)
                throw new ProcurementEvaluationCommitteeConflictException(
                    "EVALUATION_SCORE_PROJECTION_MISMATCH",
                    "A current locked committee score sheet does not identify the exact current submitted tender-evaluation projection.");

            var expectedSnapshot = BuildLegacyScoreSnapshot(
                evaluation,
                evaluation.SubmittedDate ?? evaluation.UpdatedAt ?? evaluation.EvaluationDate);
            if (NormalizeJson(sheet.ScoreSnapshotJson) != NormalizeJson(expectedSnapshot))
                throw new ProcurementEvaluationCommitteeConflictException(
                    "EVALUATION_SCORE_PROJECTION_MISMATCH",
                    "A current locked committee score sheet does not match the exact current submitted tender-evaluation values.");
            matchedEvaluationIds.Add(evaluation.Id);
        }
        if (matchedEvaluationIds.Count != expectedByEvaluationId.Count ||
            expectedByEvaluationId.Keys.Any(item => !matchedEvaluationIds.Contains(item)))
            throw new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_SCORE_PROJECTION_INCOMPLETE",
                "Every current submitted tender evaluation for every bid and evaluator must have its matching current locked committee score sheet.");
    }

    private static string BuildLegacyScoreSnapshot(
        TenderEvaluation evaluation,
        DateTime submittedAtUtc) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.legacy-tender-score-sheet.v1",
            evaluationId = evaluation.Id,
            evaluation.TenderBidId,
            evaluation.TenderEvaluatorId,
            status = "Submitted",
            submittedAtUtc,
            evaluatorUserId = evaluation.TenderEvaluator?.UserId ?? Guid.Empty,
            evaluation.PriceScore,
            evaluation.QualityScore,
            evaluation.DeliveryScore,
            evaluation.ExperienceScore,
            evaluation.TechnicalScore,
            evaluation.ComplianceScore,
            evaluation.TotalScore,
            evaluation.EvaluationCriteriaJson,
            evaluation.TechnicalComments,
            evaluation.CommercialComments,
            evaluation.OverallComments,
            evaluation.IsRecommended,
            evaluation.Recommendation
        });

    private static Guid? ReadGuid(string json, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                property.TryGetGuid(out var value))
                return value;
        }
        catch (JsonException)
        {
            // Invalid JSON is rejected by the caller as a projection mismatch.
        }
        return null;
    }

    private static string NormalizeJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch (JsonException)
        {
            return value.Trim();
        }
    }

    private async Task<Guid> EnsureLegacyEvaluationAllowedForBidAsync(Guid bidId)
    {
        var bid = await _bidRepository.GetByIdAsync(bidId)
            ?? throw new InvalidOperationException($"Bid with ID {bidId} not found");
        await EnsureLegacyEvaluationAllowedAsync(bid.TenderId);
        EnsureBidEvaluationState(bid);
        await EnsurePaymentAdmissionForEvaluationAsync(bid);
        return bid.TenderId;
    }

    private async Task EnsurePaymentAdmissionForEvaluationAsync(TenderBid bid)
    {
        var fees = await _unitOfWork.Repository<TenderFee>()
            .GetQueryable(item => item.TenantId == bid.TenantId && item.TenderId == bid.TenderId && !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync();
        var feeIds = fees.Select(item => item.Id).ToList();
        var payments = feeIds.Count == 0
            ? new List<TenderPayment>()
            : await _unitOfWork.Repository<TenderPayment>()
                .GetQueryable(item => item.TenantId == bid.TenantId &&
                                      item.BusinessPartnerId == bid.BusinessPartnerId &&
                                      feeIds.Contains(item.TenderFeeId) &&
                                      !item.IsDeleted)
                .AsNoTracking()
                .ToListAsync();
        var admission = TenderBidPaymentRules.Assess(bid, fees, payments);
        if (admission.CanOpenOrEvaluate) return;

        try
        {
            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = bid.TenantId,
                EntityType = "Bid",
                Activity = "PaymentAdmissionDenied",
                Audience = "Internal",
                EntityId = bid.Id,
                TriggeredByUserId = _currentUserProvider.UserId,
                Data = new Dictionary<string, object>
                {
                    ["BidId"] = bid.Id,
                    ["TenderId"] = bid.TenderId,
                    ["BusinessPartnerId"] = bid.BusinessPartnerId,
                    ["Stage"] = "Evaluation",
                    ["Code"] = admission.Code,
                    ["PaymentAdmissionStatus"] = admission.BlockingStatus.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish evaluation payment-admission denial for bid {BidId}", bid.Id);
        }

        throw new TenderBidInitiationValidationException(admission.Code, admission.Message);
    }

    private static void EnsureBidEvaluationState(TenderBid bid)
    {
        if (!string.Equals(bid.Status, "Opened", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(bid.Status, "UnderEvaluation", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Only an opened bid can be evaluated (current status: '{bid.Status}').");
    }

    private async Task EnsureCurrentEvaluatorOwnsAsync(TenderEvaluation evaluation)
    {
        var evaluator = evaluation.TenderEvaluator ??
                        await _evaluatorRepository.GetByIdAsync(evaluation.TenderEvaluatorId)
                        ?? throw new InvalidOperationException("The evaluation assignment was not found.");
        if (evaluator.UserId != _currentUserProvider.UserId || evaluator.TenantId != _currentUserProvider.TenantId)
            throw new ProcurementEvaluationCommitteeAuthorizationException(
                "An evaluator can change or submit only their own evaluation.");
        await _evaluationCommittee.EnsureTenderEvaluatorAsync(
            evaluator.TenderId, Guid.NewGuid().ToString("N"));
    }

    private async Task EnsureEvaluationConfigurationAsync(Guid tenderId)
    {
        var tender = await _tenderRepository.GetByIdAsync(tenderId)
            ?? throw new InvalidOperationException("The source tender was not found.");
        if (tender.TenantId != _currentUserProvider.TenantId)
            throw new ProcurementEvaluationCommitteeAuthorizationException("The source tender is not available in this tenant.");
        await TenderEvaluationConfiguration.ValidateAsync(_unitOfWork, tender);
    }

    private async Task EnsureLegacyEvaluationAllowedAsync(Guid tenderId)
    {
        if (await _tenderControlService.IsControlledTenderMethodAsync(tenderId))
            throw new ProcurementTenderControlConflictException(
                "TENDER_STATUTORY_EVALUATION_REQUIRED",
                "NCT, ICT, QBS, and QCBS evaluations must use the signed controlled tender lifecycle.");
        if (await _exceptionalSourcingControlService.IsExceptionalAsync(tenderId))
            throw new ProcurementExceptionalSourcingConflictException(
                "EXCEPTIONAL_RECOMMENDATION_CONTROL_REQUIRED",
                "Restricted, single-source, and petty-purchase recommendations must follow the approved supplier identity and negotiation in the dedicated control.");
    }
}
