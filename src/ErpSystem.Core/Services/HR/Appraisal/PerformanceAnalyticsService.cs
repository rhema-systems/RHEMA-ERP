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

/// <summary>
/// Read-only performance analytics (Themes 13-14): cycle rating distribution (calibration
/// leniency/skew) and per-employee multi-year score trend. Derived from finalized appraisals.
/// </summary>
public class PerformanceAnalyticsService : IPerformanceAnalyticsService
{
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IPerformanceRatingResolver _ratingResolver;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PerformanceAnalyticsService> _logger;

    public PerformanceAnalyticsService(
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<Employee> employeeRepository,
        IPerformanceRatingResolver ratingResolver,
        ICurrentUserProvider currentUserProvider,
        ILogger<PerformanceAnalyticsService> logger)
    {
        _appraisalRepository = appraisalRepository;
        _cycleRepository = cycleRepository;
        _employeeRepository = employeeRepository;
        _ratingResolver = ratingResolver;
        _currentUserProvider = currentUserProvider;
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

    public async Task<CalibrationDistributionDto> GetCycleRatingDistributionAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var cycle = await _cycleRepository.GetByIdAsync(cycleId);
        if (cycle == null || cycle.TenantId != tenantId)
            throw new ArgumentException($"Appraisal cycle with ID '{cycleId}' not found.");

        var scores = await _appraisalRepository.GetQueryable()
            .Where(a => a.AppraisalCycleId == cycleId && a.TenantId == tenantId && a.OverallScore != null)
            .Select(a => a.OverallScore!.Value)
            .ToListAsync(cancellationToken);

        var total = scores.Count;
        var dto = new CalibrationDistributionDto
        {
            CycleId = cycleId,
            CycleName = cycle?.CycleName,
            TotalRated = total,
            AverageScore = total > 0 ? Math.Round(scores.Average(), 2) : null
        };

        var mapRating = await _ratingResolver.GetMapperAsync(cancellationToken);

        // Build a bucket for every rating band, highest first, so the bell-curve shape is visible.
        foreach (var rating in new[]
                 {
                     PerformanceRating.Outstanding, PerformanceRating.ExceedsExpectations,
                     PerformanceRating.MeetsExpectations, PerformanceRating.BelowExpectations,
                     PerformanceRating.Unsatisfactory
                 })
        {
            var count = scores.Count(s => mapRating(s) == rating);
            dto.Buckets.Add(new RatingBucketDto
            {
                Rating = rating,
                RatingLabel = Humanize(rating.ToString()),
                Count = count,
                Percent = total > 0 ? Math.Round((decimal)count * 100 / total, 1) : 0
            });
        }

        return dto;
    }

    public async Task<EmployeePerformanceTrendDto> GetEmployeeTrendAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != tenantId)
            throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        var appraisals = await _appraisalRepository.GetQueryable()
            .Where(a => a.EmployeeId == employeeId && a.TenantId == tenantId)
            .Include(a => a.AppraisalCycle)
            .ToListAsync(cancellationToken);

        var mapRating = await _ratingResolver.GetMapperAsync(cancellationToken);

        var points = appraisals
            .OrderBy(a => a.AppraisalCycle != null ? a.AppraisalCycle.Year : 0)
            .Select(a => new PerformanceTrendPointDto
            {
                Year = a.AppraisalCycle?.Year ?? 0,
                AppraisalId = a.Id,
                CycleName = a.AppraisalCycle?.CycleName,
                OverallScore = a.OverallScore,
                Rating = mapRating(a.OverallScore),
                Status = a.Status.ToString()
            })
            .ToList();

        return new EmployeePerformanceTrendDto
        {
            EmployeeId = employeeId,
            EmployeeName = employee?.FullName,
            Points = points
        };
    }

    private static string Humanize(string pascal) =>
        System.Text.RegularExpressions.Regex.Replace(pascal, "(\\B[A-Z])", " $1");
}
