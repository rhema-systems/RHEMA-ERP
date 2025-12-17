using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderEvaluationService : ITenderEvaluationService
{
    private readonly ITenderEvaluationRepository _evaluationRepository;
    private readonly ITenderBidRepository _bidRepository;
    private readonly ITenderEvaluatorRepository _evaluatorRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderNotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<TenderEvaluationService> _logger;

    public TenderEvaluationService(
        ITenderEvaluationRepository evaluationRepository,
        ITenderBidRepository bidRepository,
        ITenderEvaluatorRepository evaluatorRepository,
        ITenderRepository tenderRepository,
        ITenderNotificationService notificationService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<TenderEvaluationService> logger)
    {
        _evaluationRepository = evaluationRepository;
        _bidRepository = bidRepository;
        _evaluatorRepository = evaluatorRepository;
        _tenderRepository = tenderRepository;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
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

            // Get evaluator assignment for current user
            var evaluators = await _evaluatorRepository.GetByUserIdAsync(_currentUserProvider.UserId);
            var evaluator = evaluators.FirstOrDefault();
            if (evaluator == null)
            {
                throw new InvalidOperationException("Current user is not assigned as an evaluator");
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
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created evaluation {EvaluationId} for bid {BidId}", evaluation.Id, dto.TenderBidId);

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

            if (evaluation.Status == "Submitted")
            {
                throw new InvalidOperationException("Evaluation already submitted");
            }

            if (!dto.ConfirmSubmission)
            {
                throw new InvalidOperationException("Submission must be confirmed");
            }

            evaluation.Status = "Submitted";
            evaluation.SubmittedDate = DateTime.UtcNow;
            evaluation.UpdatedAt = DateTime.UtcNow;

            await _evaluationRepository.UpdateAsync(evaluation);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Submitted evaluation {EvaluationId}", id);

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
            var assignedEvaluatorIds = evaluators.Select(e => e.Id).ToList();

            if (!assignedEvaluatorIds.Any()) return;

            // Get all evaluations for this bid
            var evaluations = await _evaluationRepository.GetByBidIdAsync(bidId);
            var submittedEvaluations = evaluations.Where(e => e.Status == "Submitted").ToList();

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
            if (evaluation != null && evaluation.Status != "Submitted")
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
            var totalEvaluators = evaluators.Count();

            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);
            var bidEvaluations = new List<BidEvaluationSummaryDto>();

            foreach (var bid in bids)
            {
                var evaluations = await _evaluationRepository.GetByBidIdAsync(bid.Id);
                var submittedEvaluations = evaluations.Where(e => e.Status == "Submitted").ToList();
                var avgTotalScore = submittedEvaluations.Any() ? (submittedEvaluations.Average(e => e.TotalScore) ?? 0m) : 0m;
                var recommendationCount = submittedEvaluations.Count(e => e.IsRecommended);

                // Determine if bid is fully evaluated (all evaluators have submitted)
                var allEvaluatorsSubmitted = totalEvaluators > 0 &&
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

            return new EvaluationReportDto
            {
                TenderId = tenderId,
                TenderNumber = tender.TenderNumber,
                TenderTitle = tender.Title,
                TenderType = tender.TenderType,
                PublishDate = tender.PublishDate,
                SubmissionDeadline = tender.SubmissionDeadline,
                EstimatedValue = tender.EstimatedValue,
                PriceWeightage = tender.PriceWeightage,
                QualityWeightage = tender.QualityWeightage,
                DeliveryWeightage = tender.DeliveryWeightage,
                ExperienceWeightage = tender.ExperienceWeightage,
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
}


