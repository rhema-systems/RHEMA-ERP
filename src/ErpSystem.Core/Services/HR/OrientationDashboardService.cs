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
    private readonly IUnitOfWork _unitOfWork;

    public OrientationDashboardService(
        IOrientationProgramRepository programRepository,
        IEmployeeOrientationRepository enrollmentRepository,
        IOrientationSessionRepository sessionRepository,
        IOrientationCertificateRepository certificateRepository,
        IUnitOfWork unitOfWork)
    {
        _programRepository = programRepository;
        _enrollmentRepository = enrollmentRepository;
        _sessionRepository = sessionRepository;
        _certificateRepository = certificateRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrientationDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var programs = (await _programRepository.GetAllAsync()).ToList();
        var enrollments = (await _enrollmentRepository.GetAllAsync()).ToList();
        var overdue = (await _enrollmentRepository.GetOverdueAsync()).ToList();
        var upcoming = (await _sessionRepository.GetUpcomingAsync(30)).ToList();
        var expiring = (await _certificateRepository.GetExpiringAsync(30)).ToList();

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
            dto.OverdueList.EmployeeIds().Concat(dto.ExpiringCertificateList.EmployeeIds()));
        dto.OverdueList.FillNames(map);
        dto.ExpiringCertificateList.FillNames(map);

        return dto;
    }
}
