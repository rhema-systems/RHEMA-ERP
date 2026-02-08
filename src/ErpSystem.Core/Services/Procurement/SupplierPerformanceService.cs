using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class SupplierPerformanceService : ISupplierPerformanceService
{
    private readonly ISupplierPerformanceMetricRepository _metricRepository;
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly IQualityIncidentRepository _incidentRepository;
    private readonly IPerformanceReviewRepository _reviewRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPurchaseOrderReceiptRepository _receiptRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<SupplierPerformanceService> _logger;

    public SupplierPerformanceService(
        ISupplierPerformanceMetricRepository metricRepository,
        IBusinessPartnerRepository partnerRepository,
        IQualityIncidentRepository incidentRepository,
        IPerformanceReviewRepository reviewRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseOrderReceiptRepository receiptRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<SupplierPerformanceService> logger)
    {
        _metricRepository = metricRepository;
        _partnerRepository = partnerRepository;
        _incidentRepository = incidentRepository;
        _reviewRepository = reviewRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _receiptRepository = receiptRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<SupplierPerformanceMetricDto?> GetByIdAsync(Guid id)
    {
        var metric = await _metricRepository.GetByIdAsync(id);
        return metric == null ? null : MapToDto(metric);
    }

    public async Task<IEnumerable<SupplierPerformanceMetricDto>> GetByBusinessPartnerAsync(Guid businessPartnerId)
    {
        var metrics = await _metricRepository.GetByBusinessPartnerAsync(businessPartnerId);
        return metrics.Select(MapToDto);
    }

    public async Task<SupplierPerformanceMetricDto?> GetByBusinessPartnerAndPeriodAsync(Guid businessPartnerId, string metricPeriod, int year, int? month, int? quarter)
    {
        var metric = await _metricRepository.GetByBusinessPartnerAndPeriodAsync(businessPartnerId, metricPeriod, year, month, quarter);
        return metric == null ? null : MapToDto(metric);
    }

    public async Task<IEnumerable<PerformanceTrendDto>> GetTrendsAsync(Guid businessPartnerId, int numberOfPeriods = 12)
    {
        var metrics = await _metricRepository.GetTrendsAsync(businessPartnerId, numberOfPeriods);
        return metrics.Select(m => new PerformanceTrendDto
        {
            Period = FormatPeriod(m.MetricPeriod, m.Year, m.Month, m.Quarter),
            OverallScore = m.OverallPerformanceScore,
            DeliveryScore = m.OnTimeDeliveryRate,
            QualityScore = m.QualityAcceptanceRate,
            CostScore = m.CostCompetitivenessScore,
            ServiceScore = m.CustomerServiceRating
        });
    }

    public async Task<SupplierPerformanceMetricDto> CalculateMetricsAsync(Guid businessPartnerId, string metricPeriod, int year, int? month, int? quarter)
    {
        _logger.LogInformation("Calculating performance metrics for BusinessPartner {BusinessPartnerId}, Period: {Period} {Year}/{Month}/{Quarter}",
            businessPartnerId, metricPeriod, year, month, quarter);

        var partner = await _partnerRepository.GetByIdAsync(businessPartnerId);
        if (partner == null)
        {
            throw new ArgumentException($"Business partner with ID {businessPartnerId} not found");
        }

        // Check if metric already exists
        var existingMetric = await _metricRepository.GetByBusinessPartnerAndPeriodAsync(businessPartnerId, metricPeriod, year, month, quarter);
        if (existingMetric != null)
        {
            _logger.LogWarning("Performance metric already exists for this period. Returning existing metric.");
            return MapToDto(existingMetric);
        }

        // Calculate date range for the period
        var (startDate, endDate) = GetPeriodDateRange(metricPeriod, year, month, quarter);

        // Get all purchase orders for this supplier in the period
        var allOrders = await _purchaseOrderRepository.GetOrdersByDateRangeAsync(startDate, endDate);
        var orders = allOrders.Where(po => po.BusinessPartnerId == businessPartnerId).ToList();

        // Get all receipts for these orders
        var orderIds = orders.Select(o => o.Id).ToList();
        var allReceipts = new List<PurchaseOrderReceipt>();
        foreach (var orderId in orderIds)
        {
            var receipts = await _receiptRepository.GetReceiptsByOrderAsync(orderId);
            allReceipts.AddRange(receipts);
        }

        // Get quality incidents for this period
        var allIncidents = await _incidentRepository.GetByBusinessPartnerAsync(businessPartnerId);
        var incidents = allIncidents.Where(i => i.IncidentDate >= startDate && i.IncidentDate <= endDate).ToList();

        // Calculate delivery metrics
        var totalOrders = orders.Count;
        var completedOrders = orders.Where(o => o.Status == "Received" || o.Status == "PartiallyReceived").ToList();
        var onTimeDeliveries = completedOrders.Count(o => o.ReceivedDate.HasValue && o.PromisedDate.HasValue && o.ReceivedDate.Value <= o.PromisedDate.Value);
        var lateDeliveries = completedOrders.Count(o => o.ReceivedDate.HasValue && o.PromisedDate.HasValue && o.ReceivedDate.Value > o.PromisedDate.Value);
        var onTimeDeliveryRate = completedOrders.Count > 0 ? (decimal)onTimeDeliveries / completedOrders.Count * 100 : 0;

        var averageDelayDays = lateDeliveries > 0
            ? completedOrders
                .Where(o => o.ReceivedDate.HasValue && o.PromisedDate.HasValue && o.ReceivedDate.Value > o.PromisedDate.Value)
                .Average(o => (o.ReceivedDate!.Value - o.PromisedDate!.Value).TotalDays)
            : 0;

        // Calculate quality metrics
        var totalItemsReceived = (int)allReceipts.SelectMany(r => r.Items).Sum(i => i.ReceivedQuantity);
        var rejectedItems = (int)allReceipts.SelectMany(r => r.Items).Sum(i => i.RejectedQuantity);
        var acceptedItems = (int)allReceipts.SelectMany(r => r.Items).Sum(i => i.AcceptedQuantity);
        var qualityAcceptanceRate = totalItemsReceived > 0 ? ((decimal)acceptedItems / totalItemsReceived) * 100 : 100;
        var defectRate = totalItemsReceived > 0 ? ((decimal)rejectedItems / totalItemsReceived) * 100 : 0;

        // Calculate cost metrics
        var totalPurchaseValue = orders.Sum(o => o.TotalAmount);

        // Calculate service metrics
        var complaintsReceived = incidents.Count;
        var complaintsResolved = incidents.Count(i => i.Status == "Resolved" || i.Status == "Closed");

        // Calculate compliance metrics
        var contractViolations = incidents.Count(i => i.IncidentType == "Contract Violation");

        // Calculate overall performance score (weighted average)
        var deliveryScore = onTimeDeliveryRate;
        var qualityScore = qualityAcceptanceRate;
        var serviceScore = complaintsReceived > 0 ? ((decimal)complaintsResolved / complaintsReceived * 100) : 100;
        var complianceScore = contractViolations == 0 ? 100 : Math.Max(0, 100 - (contractViolations * 10));

        var overallScore = (deliveryScore * 0.3m) + (qualityScore * 0.3m) + (serviceScore * 0.2m) + (complianceScore * 0.2m);
        var grade = CalculateGrade(overallScore);

        // Create the metric
        var metric = new SupplierPerformanceMetric
        {
            Id = Guid.NewGuid(),
            BusinessPartnerId = businessPartnerId,
            MetricPeriod = metricPeriod,
            Year = year,
            Month = month,
            Quarter = quarter,

            // Delivery metrics
            TotalOrders = totalOrders,
            OnTimeDeliveries = onTimeDeliveries,
            LateDeliveries = lateDeliveries,
            OnTimeDeliveryRate = onTimeDeliveryRate,
            AverageDeliveryDelayDays = (decimal)averageDelayDays,

            // Quality metrics
            TotalItemsReceived = totalItemsReceived,
            DefectiveItems = rejectedItems,
            RejectedItems = rejectedItems,
            QualityAcceptanceRate = qualityAcceptanceRate,
            DefectRate = defectRate,

            // Cost metrics
            TotalPurchaseValue = totalPurchaseValue,

            // Service metrics
            ComplaintsReceived = complaintsReceived,
            ComplaintsResolved = complaintsResolved,

            // Compliance metrics
            ContractViolations = contractViolations,
            ComplianceScore = complianceScore,

            // Overall
            OverallPerformanceScore = overallScore,
            PerformanceGrade = grade,

            TenantId = _currentUserProvider.TenantId,
            CalculatedById = _currentUserProvider.UserId,
            CalculatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserProvider.UserId
        };

        await _metricRepository.AddAsync(metric);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Performance metric calculated and saved with ID {MetricId}. Overall Score: {Score}, Grade: {Grade}",
            metric.Id, overallScore, grade);

        return MapToDto(metric);
    }

    public async Task<PerformanceReportCardDto> GetReportCardAsync(Guid businessPartnerId, string reportPeriod)
    {
        var partner = await _partnerRepository.GetByIdAsync(businessPartnerId);
        if (partner == null)
        {
            throw new ArgumentException($"Business partner with ID {businessPartnerId} not found");
        }

        var currentMetrics = (await _metricRepository.GetByBusinessPartnerAsync(businessPartnerId)).FirstOrDefault();
        var trends = await GetTrendsAsync(businessPartnerId, 12);
        var recentIncidents = await _incidentRepository.GetRecentIncidentsAsync(businessPartnerId, 90);
        var latestReview = await _reviewRepository.GetLatestReviewAsync(businessPartnerId);
        var allMetrics = await _metricRepository.GetByBusinessPartnerAsync(businessPartnerId);
        var openIncidents = await _incidentRepository.GetOpenIncidentCountAsync(businessPartnerId);

        return new PerformanceReportCardDto
        {
            BusinessPartnerId = businessPartnerId,
            PartnerName = partner.PartnerName,
            PartnerCode = partner.PartnerCode,
            PartnerType = partner.PartnerType,
            ReportPeriod = reportPeriod,
            GeneratedAt = DateTime.UtcNow,
            CurrentMetrics = currentMetrics == null ? null : MapToDto(currentMetrics),
            Trends = trends.ToList(),
            RecentIncidents = recentIncidents.Select(MapIncidentToDto).ToList(),
            LatestReview = latestReview == null ? null : MapReviewToDto(latestReview),
            TotalOrdersAllTime = allMetrics.Sum(m => m.TotalOrders),
            AverageOnTimeDeliveryRate = allMetrics.Any() ? allMetrics.Average(m => m.OnTimeDeliveryRate) : 0,
            AverageQualityRate = allMetrics.Any() ? allMetrics.Average(m => m.QualityAcceptanceRate) : 0,
            TotalIncidentsAllTime = (await _incidentRepository.GetByBusinessPartnerAsync(businessPartnerId)).Count(),
            OpenIncidents = openIncidents
        };
    }

    public async Task<SupplierPerformanceMetricDto> CreateAsync(CreateSupplierPerformanceMetricDto createDto)
    {
        return await CalculateMetricsAsync(createDto.BusinessPartnerId, createDto.MetricPeriod, createDto.Year, createDto.Month, createDto.Quarter);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _metricRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    // Helper methods
    private string FormatPeriod(string metricPeriod, int year, int? month, int? quarter)
    {
        return metricPeriod switch
        {
            "Monthly" => $"{year}-{month:D2}",
            "Quarterly" => $"Q{quarter} {year}",
            "Yearly" => year.ToString(),
            _ => $"{year}"
        };
    }

    private (DateTime startDate, DateTime endDate) GetPeriodDateRange(string metricPeriod, int year, int? month, int? quarter)
    {
        return metricPeriod switch
        {
            "Monthly" => (
                new DateTime(year, month ?? 1, 1),
                new DateTime(year, month ?? 1, DateTime.DaysInMonth(year, month ?? 1), 23, 59, 59)
            ),
            "Quarterly" => quarter switch
            {
                1 => (new DateTime(year, 1, 1), new DateTime(year, 3, 31, 23, 59, 59)),
                2 => (new DateTime(year, 4, 1), new DateTime(year, 6, 30, 23, 59, 59)),
                3 => (new DateTime(year, 7, 1), new DateTime(year, 9, 30, 23, 59, 59)),
                4 => (new DateTime(year, 10, 1), new DateTime(year, 12, 31, 23, 59, 59)),
                _ => (new DateTime(year, 1, 1), new DateTime(year, 12, 31, 23, 59, 59))
            },
            "Yearly" => (new DateTime(year, 1, 1), new DateTime(year, 12, 31, 23, 59, 59)),
            _ => (new DateTime(year, 1, 1), new DateTime(year, 12, 31, 23, 59, 59))
        };
    }

    private string CalculateGrade(decimal score)
    {
        return score switch
        {
            >= 97 => "A+",
            >= 93 => "A",
            >= 90 => "A-",
            >= 87 => "B+",
            >= 83 => "B",
            >= 80 => "B-",
            >= 77 => "C+",
            >= 73 => "C",
            >= 70 => "C-",
            >= 60 => "D",
            _ => "F"
        };
    }

    private SupplierPerformanceMetricDto MapToDto(SupplierPerformanceMetric metric)
    {
        return new SupplierPerformanceMetricDto
        {
            Id = metric.Id,
            BusinessPartnerId = metric.BusinessPartnerId,
            PartnerName = metric.BusinessPartner?.PartnerName,
            PartnerCode = metric.BusinessPartner?.PartnerCode,
            MetricPeriod = metric.MetricPeriod,
            Year = metric.Year,
            Month = metric.Month,
            Quarter = metric.Quarter,
            TotalOrders = metric.TotalOrders,
            OnTimeDeliveries = metric.OnTimeDeliveries,
            LateDeliveries = metric.LateDeliveries,
            OnTimeDeliveryRate = metric.OnTimeDeliveryRate,
            AverageDeliveryDelayDays = metric.AverageDeliveryDelayDays,
            TotalItemsReceived = metric.TotalItemsReceived,
            DefectiveItems = metric.DefectiveItems,
            QualityAcceptanceRate = metric.QualityAcceptanceRate,
            DefectRate = metric.DefectRate,
            TotalPurchaseValue = metric.TotalPurchaseValue,
            CostCompetitivenessScore = metric.CostCompetitivenessScore,
            CustomerServiceRating = metric.CustomerServiceRating,
            ComplaintsReceived = metric.ComplaintsReceived,
            ComplaintsResolved = metric.ComplaintsResolved,
            ContractViolations = metric.ContractViolations,
            ComplianceScore = metric.ComplianceScore,
            InnovationSuggestions = metric.InnovationSuggestions,
            EstimatedCostSavings = metric.EstimatedCostSavings,
            OverallPerformanceScore = metric.OverallPerformanceScore,
            PerformanceGrade = metric.PerformanceGrade,
            CalculatedAt = metric.CalculatedAt,
            Notes = metric.Notes
        };
    }

    private QualityIncidentDto MapIncidentToDto(QualityIncident incident)
    {
        return new QualityIncidentDto
        {
            Id = incident.Id,
            BusinessPartnerId = incident.BusinessPartnerId,
            PartnerName = incident.BusinessPartner?.PartnerName,
            PurchaseOrderId = incident.PurchaseOrderId,
            IncidentNumber = incident.IncidentNumber,
            IncidentDate = incident.IncidentDate,
            IncidentType = incident.IncidentType,
            Severity = incident.Severity,
            Description = incident.Description,
            QuantityAffected = incident.QuantityAffected,
            FinancialImpact = incident.FinancialImpact,
            Status = incident.Status,
            ReportedDate = incident.ReportedDate,
            ReportedByName = incident.ReportedBy?.FullName,
            ResolvedDate = incident.ResolvedDate,
            Resolution = incident.Resolution,
            RootCause = incident.RootCause,
            CorrectiveAction = incident.CorrectiveAction,
            RequiresSupplierResponse = incident.RequiresSupplierResponse,
            SupplierResponseDate = incident.SupplierResponseDate,
            SupplierResponse = incident.SupplierResponse,
            Notes = incident.Notes,
            CreatedAt = incident.CreatedAt
        };
    }

    private PerformanceReviewDto MapReviewToDto(PerformanceReview review)
    {
        return new PerformanceReviewDto
        {
            Id = review.Id,
            BusinessPartnerId = review.BusinessPartnerId,
            PartnerName = review.BusinessPartner?.PartnerName,
            ReviewNumber = review.ReviewNumber,
            ReviewDate = review.ReviewDate,
            ReviewPeriod = review.ReviewPeriod,
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

