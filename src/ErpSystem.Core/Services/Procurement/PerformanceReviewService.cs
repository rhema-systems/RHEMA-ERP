using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class PerformanceReviewService : IPerformanceReviewService
{
    private readonly IPerformanceReviewRepository _reviewRepository;
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PerformanceReviewService> _logger;

    public PerformanceReviewService(
        IPerformanceReviewRepository reviewRepository,
        IBusinessPartnerRepository partnerRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<PerformanceReviewService> logger)
    {
        _reviewRepository = reviewRepository;
        _partnerRepository = partnerRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<PerformanceReviewDto?> GetByIdAsync(Guid id)
    {
        var review = await _reviewRepository.GetByIdAsync(id);
        return review == null ? null : MapToDto(review);
    }

    public async Task<PerformanceReviewDto?> GetByReviewNumberAsync(string reviewNumber)
    {
        var review = await _reviewRepository.GetByReviewNumberAsync(reviewNumber);
        return review == null ? null : MapToDto(review);
    }

    public async Task<IEnumerable<PerformanceReviewDto>> GetByBusinessPartnerAsync(Guid businessPartnerId)
    {
        var reviews = await _reviewRepository.GetByBusinessPartnerAsync(businessPartnerId);
        return reviews.Select(MapToDto);
    }

    public async Task<PerformanceReviewDto?> GetLatestReviewAsync(Guid businessPartnerId)
    {
        var review = await _reviewRepository.GetLatestReviewAsync(businessPartnerId);
        return review == null ? null : MapToDto(review);
    }

    public async Task<IEnumerable<PerformanceReviewDto>> GetByPeriodAsync(string reviewPeriod)
    {
        var reviews = await _reviewRepository.GetByPeriodAsync(reviewPeriod);
        return reviews.Select(MapToDto);
    }

    public async Task<IEnumerable<PerformanceReviewDto>> GetByStatusAsync(string status)
    {
        var reviews = await _reviewRepository.GetByStatusAsync(status);
        return reviews.Select(MapToDto);
    }

    public async Task<PerformanceReviewDto> CreateAsync(CreatePerformanceReviewDto createDto)
    {
        _logger.LogInformation("Creating performance review for BusinessPartner {BusinessPartnerId}", createDto.BusinessPartnerId);

        var partner = await _partnerRepository.GetByIdAsync(createDto.BusinessPartnerId);
        if (partner == null)
        {
            throw new ArgumentException($"Business partner with ID {createDto.BusinessPartnerId} not found");
        }

        var reviewNumber = await _reviewRepository.GenerateReviewNumberAsync();

        // Calculate overall score as average of all scores
        var overallScore = (createDto.DeliveryPerformanceScore + createDto.QualityScore + 
                           createDto.CostCompetitivenessScore + createDto.CustomerServiceScore + 
                           createDto.ComplianceScore + createDto.InnovationScore) / 6;

        var review = new PerformanceReview
        {
            Id = Guid.NewGuid(),
            BusinessPartnerId = createDto.BusinessPartnerId,
            ReviewNumber = reviewNumber,
            ReviewDate = createDto.ReviewDate,
            ReviewPeriod = createDto.ReviewPeriod,
            PeriodStartDate = createDto.PeriodStartDate,
            PeriodEndDate = createDto.PeriodEndDate,
            ReviewedById = _currentUserProvider.UserId,
            DeliveryPerformanceScore = createDto.DeliveryPerformanceScore,
            QualityScore = createDto.QualityScore,
            CostCompetitivenessScore = createDto.CostCompetitivenessScore,
            CustomerServiceScore = createDto.CustomerServiceScore,
            ComplianceScore = createDto.ComplianceScore,
            InnovationScore = createDto.InnovationScore,
            OverallScore = overallScore,
            OverallGrade = CalculateGrade(overallScore),
            Strengths = createDto.Strengths,
            AreasForImprovement = createDto.AreasForImprovement,
            Recommendations = createDto.Recommendations,
            ActionItems = createDto.ActionItems,
            Status = "Draft",
            RequiresFollowUp = createDto.RequiresFollowUp,
            FollowUpDate = createDto.FollowUpDate,
            Notes = createDto.Notes,
            TenantId = _currentUserProvider.TenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserProvider.UserId
        };

        await _reviewRepository.AddAsync(review);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Performance review created with ID {ReviewId} and number {ReviewNumber}", review.Id, review.ReviewNumber);

        return MapToDto(review);
    }

    public async Task<PerformanceReviewDto> UpdateAsync(Guid id, CreatePerformanceReviewDto updateDto)
    {
        _logger.LogInformation("Updating performance review {ReviewId}", id);

        var review = await _reviewRepository.GetByIdAsync(id);
        if (review == null)
        {
            throw new ArgumentException($"Performance review with ID {id} not found");
        }

        // Only allow updating draft reviews
        if (review.Status != "Draft")
        {
            throw new InvalidOperationException($"Only draft reviews can be updated. Current status: {review.Status}");
        }

        // Calculate overall score as average of all scores
        var overallScore = (updateDto.DeliveryPerformanceScore + updateDto.QualityScore +
                           updateDto.CostCompetitivenessScore + updateDto.CustomerServiceScore +
                           updateDto.ComplianceScore + updateDto.InnovationScore) / 6;

        // Update review properties
        review.ReviewDate = updateDto.ReviewDate;
        review.ReviewPeriod = updateDto.ReviewPeriod;
        review.PeriodStartDate = updateDto.PeriodStartDate;
        review.PeriodEndDate = updateDto.PeriodEndDate;
        review.DeliveryPerformanceScore = updateDto.DeliveryPerformanceScore;
        review.QualityScore = updateDto.QualityScore;
        review.CostCompetitivenessScore = updateDto.CostCompetitivenessScore;
        review.CustomerServiceScore = updateDto.CustomerServiceScore;
        review.ComplianceScore = updateDto.ComplianceScore;
        review.InnovationScore = updateDto.InnovationScore;
        review.OverallScore = overallScore;
        review.OverallGrade = CalculateGrade(overallScore);
        review.Strengths = updateDto.Strengths;
        review.AreasForImprovement = updateDto.AreasForImprovement;
        review.Recommendations = updateDto.Recommendations;
        review.ActionItems = updateDto.ActionItems;
        review.RequiresFollowUp = updateDto.RequiresFollowUp;
        review.FollowUpDate = updateDto.FollowUpDate;
        review.Notes = updateDto.Notes;
        review.UpdatedAt = DateTime.UtcNow;
        review.LastModifiedById = _currentUserProvider.UserId;

        await _reviewRepository.UpdateAsync(review);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Performance review {ReviewId} updated successfully", id);

        return MapToDto(review);
    }

    public async Task<PerformanceReviewDto> SubmitAsync(Guid id)
    {
        var review = await _reviewRepository.GetByIdAsync(id);
        if (review == null)
        {
            throw new ArgumentException($"Performance review with ID {id} not found");
        }

        if (review.Status != "Draft")
        {
            throw new InvalidOperationException($"Only draft reviews can be submitted. Current status: {review.Status}");
        }

        review.Status = "Submitted";
        review.SubmittedDate = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;
        review.LastModifiedById = _currentUserProvider.UserId;

        await _reviewRepository.UpdateAsync(review);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Performance review {ReviewId} submitted", id);

        return MapToDto(review);
    }

    public async Task<PerformanceReviewDto> AcknowledgeAsync(Guid id, string? supplierComments)
    {
        var review = await _reviewRepository.GetByIdAsync(id);
        if (review == null)
        {
            throw new ArgumentException($"Performance review with ID {id} not found");
        }

        review.Status = "Acknowledged";
        review.AcknowledgedDate = DateTime.UtcNow;
        review.AcknowledgedById = _currentUserProvider.UserId;
        review.SupplierComments = supplierComments;
        review.SupplierCommentsDate = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;
        review.LastModifiedById = _currentUserProvider.UserId;

        await _reviewRepository.UpdateAsync(review);

        // Update the business partner's performance rating with the overall score from this review
        await _partnerRepository.UpdatePerformanceRatingAsync(review.BusinessPartnerId, review.OverallScore);

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Performance review {ReviewId} acknowledged and business partner rating updated to {Rating}", id, review.OverallScore);

        return MapToDto(review);
    }

    public async Task<PerformanceReviewDto> FinalizeAsync(Guid id)
    {
        var review = await _reviewRepository.GetByIdAsync(id);
        if (review == null)
        {
            throw new ArgumentException($"Performance review with ID {id} not found");
        }

        review.Status = "Finalized";
        review.UpdatedAt = DateTime.UtcNow;
        review.LastModifiedById = _currentUserProvider.UserId;

        await _reviewRepository.UpdateAsync(review);

        // Update the business partner's performance rating with the overall score from this review
        await _partnerRepository.UpdatePerformanceRatingAsync(review.BusinessPartnerId, review.OverallScore);

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Performance review {ReviewId} finalized and business partner rating updated to {Rating}", id, review.OverallScore);

        return MapToDto(review);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _reviewRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    private string CalculateGrade(decimal score)
    {
        return score switch
        {
            >= 4.7m => "A+",
            >= 4.3m => "A",
            >= 4.0m => "A-",
            >= 3.7m => "B+",
            >= 3.3m => "B",
            >= 3.0m => "B-",
            >= 2.7m => "C+",
            >= 2.3m => "C",
            >= 2.0m => "C-",
            >= 1.0m => "D",
            _ => "F"
        };
    }

    private PerformanceReviewDto MapToDto(PerformanceReview review)
    {
        // Calculate year, month, quarter from period dates
        int reviewYear = review.PeriodStartDate.Year;
        int? reviewMonth = null;
        int? reviewQuarter = null;

        if (review.ReviewPeriod == "Monthly")
        {
            reviewMonth = review.PeriodStartDate.Month;
        }
        else if (review.ReviewPeriod == "Quarterly")
        {
            reviewQuarter = ((review.PeriodStartDate.Month - 1) / 3) + 1;
        }

        return new PerformanceReviewDto
        {
            Id = review.Id,
            BusinessPartnerId = review.BusinessPartnerId,
            PartnerName = review.BusinessPartner?.PartnerName,
            ReviewNumber = review.ReviewNumber,
            ReviewDate = review.ReviewDate,
            ReviewPeriod = review.ReviewPeriod,
            ReviewYear = reviewYear,
            ReviewMonth = reviewMonth,
            ReviewQuarter = reviewQuarter,
            PeriodStartDate = review.PeriodStartDate,
            PeriodEndDate = review.PeriodEndDate,
            ReviewedByName = review.ReviewedBy?.FullName,
            DeliveryPerformanceScore = review.DeliveryPerformanceScore,
            QualityScore = review.QualityScore,
            CostCompetitivenessScore = review.CostCompetitivenessScore,
            CustomerServiceScore = review.CustomerServiceScore,
            ComplianceScore = review.ComplianceScore,
            InnovationScore = review.InnovationScore,
            OverallScore = review.OverallScore,
            OverallGrade = review.OverallGrade,
            Strengths = review.Strengths,
            AreasForImprovement = review.AreasForImprovement,
            Recommendations = review.Recommendations,
            ActionItems = review.ActionItems,
            Status = review.Status,
            SubmittedDate = review.SubmittedDate,
            AcknowledgedDate = review.AcknowledgedDate,
            SupplierComments = review.SupplierComments,
            SupplierCommentsDate = review.SupplierCommentsDate,
            RequiresFollowUp = review.RequiresFollowUp,
            FollowUpDate = review.FollowUpDate,
            Notes = review.Notes,
            CreatedAt = review.CreatedAt
        };
    }
}

