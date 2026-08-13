using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;

namespace ErpSystem.Core.Services.HR;

public class OrientationDashboardService : IOrientationDashboardService
{
    private readonly IOrientationProgramRepository _programRepository;
    private readonly IEmployeeOrientationRepository _enrollmentRepository;
    private readonly IOrientationSessionRepository _sessionRepository;
    private readonly IOrientationCertificateRepository _certificateRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public OrientationDashboardService(
        IOrientationProgramRepository programRepository,
        IEmployeeOrientationRepository enrollmentRepository,
        IOrientationSessionRepository sessionRepository,
        IOrientationCertificateRepository certificateRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _programRepository = programRepository;
        _enrollmentRepository = enrollmentRepository;
        _sessionRepository = sessionRepository;
        _certificateRepository = certificateRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<OrientationDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var programs = (await _programRepository.GetAllAsync()).Where(p => p.TenantId == tenantId).ToList();
        var enrollments = (await _enrollmentRepository.GetAllAsync()).Where(e => e.TenantId == tenantId).ToList();
        var overdue = (await _enrollmentRepository.GetOverdueAsync()).Where(e => e.TenantId == tenantId).ToList();
        var upcoming = (await _sessionRepository.GetUpcomingAsync(30)).Where(s => s.TenantId == tenantId).ToList();
        var expiring = (await _certificateRepository.GetExpiringAsync(30)).Where(c => c.TenantId == tenantId).ToList();

        var completed = enrollments.Count(e => e.CompletionStatus == OrientationCompletionStatus.Completed);

        var dto = new OrientationDashboardDto
        {
            TotalPrograms = programs.Count,
            ActivePrograms = programs.Count(p => p.Status == OrientationProgramStatus.Active),
            DraftPrograms = programs.Count(p => p.Status == OrientationProgramStatus.Draft),

            TotalEnrollments = enrollments.Count,
            NotStartedEnrollments = enrollments.Count(e => e.CompletionStatus == OrientationCompletionStatus.NotStarted),
            InProgressEnrollments = enrollments.Count(e => e.CompletionStatus == OrientationCompletionStatus.InProgress),
            CompletedEnrollments = completed,
            OverdueEnrollments = overdue.Count,
            OverallCompletionRate = enrollments.Count == 0 ? 0 : Math.Round((decimal)completed / enrollments.Count * 100, 1),

            UpcomingSessions = upcoming.Count,
            ExpiringCertificates = expiring.Count,

            ProgramsByStatus = programs
                .GroupBy(p => p.Status)
                .Select(g => new OrientationStatusCountDto { Status = g.Key, Count = g.Count() })
                .OrderBy(x => x.Status)
                .ToList(),

            EnrollmentsByCompletionStatus = enrollments
                .GroupBy(e => e.CompletionStatus)
                .Select(g => new OrientationCompletionCountDto { Status = g.Key, Count = g.Count() })
                .OrderBy(x => x.Status)
                .ToList(),

            OverdueList = overdue.Take(10).ToSummaryDtoList().ToList(),
            UpcomingSessionList = upcoming.Take(10).ToSummaryDtoList().ToList(),
            ExpiringCertificateList = expiring.Take(10).Select(c => c.ToDto()).ToList(),
        };

        // Hydrate employee display names (entities reference employees by id only).
        var map = await _unitOfWork.ResolveEmployeesAsync(
            tenantId,
            dto.OverdueList.EmployeeIds().Concat(dto.ExpiringCertificateList.EmployeeIds()));
        dto.OverdueList.FillNames(map);
        dto.ExpiringCertificateList.FillNames(map);

        // Seat counts come from a grouped query for the same reason as the session list: the upcoming
        // read does not include the enrollments collection, and an empty collection counts as 0.
        var seats = await _sessionRepository.GetEnrolledCountsAsync(tenantId, dto.UpcomingSessionList.Select(s => s.Id));
        foreach (var session in dto.UpcomingSessionList)
            session.EnrolledCount = seats.TryGetValue(session.Id, out var c) ? c : 0;

        return dto;
    }
}
