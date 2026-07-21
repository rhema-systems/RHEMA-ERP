using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingDashboardService : ITrainingDashboardService
{
    private readonly ITrainingProgramRepository _programRepository;
    private readonly ITrainingScheduleRepository _scheduleRepository;
    private readonly ITrainingNominationRepository _nominationRepository;
    private readonly ITrainingCompletionRepository _completionRepository;
    private readonly ITrainingCertificateRepository _certificateRepository;
    private readonly IEmployeeComplianceRecordRepository _complianceRepository;
    private readonly ITrainingBudgetRepository _budgetRepository;
    private readonly IMentoringPairRepository _mentoringPairRepository;
    private readonly IEmployeeLearningPathRepository _learningPathEnrollmentRepository;
    private readonly ITrainingFeedbackRepository _feedbackRepository;
    private readonly ITrainingFollowUpAssessmentRepository _followUpRepository;
    private readonly ITrainingAttendanceRepository _attendanceRepository;
    private readonly ITrainerProfileRepository _trainerRepository;
    private readonly ILogger<TrainingDashboardService> _logger;

    public TrainingDashboardService(
        ITrainingProgramRepository programRepository,
        ITrainingScheduleRepository scheduleRepository,
        ITrainingNominationRepository nominationRepository,
        ITrainingCompletionRepository completionRepository,
        ITrainingCertificateRepository certificateRepository,
        IEmployeeComplianceRecordRepository complianceRepository,
        ITrainingBudgetRepository budgetRepository,
        IMentoringPairRepository mentoringPairRepository,
        IEmployeeLearningPathRepository learningPathEnrollmentRepository,
        ITrainingFeedbackRepository feedbackRepository,
        ITrainingFollowUpAssessmentRepository followUpRepository,
        ITrainingAttendanceRepository attendanceRepository,
        ITrainerProfileRepository trainerRepository,
        ILogger<TrainingDashboardService> logger)
    {
        _programRepository = programRepository;
        _scheduleRepository = scheduleRepository;
        _nominationRepository = nominationRepository;
        _completionRepository = completionRepository;
        _certificateRepository = certificateRepository;
        _complianceRepository = complianceRepository;
        _budgetRepository = budgetRepository;
        _mentoringPairRepository = mentoringPairRepository;
        _learningPathEnrollmentRepository = learningPathEnrollmentRepository;
        _feedbackRepository = feedbackRepository;
        _followUpRepository = followUpRepository;
        _attendanceRepository = attendanceRepository;
        _trainerRepository = trainerRepository;
        _logger = logger;
    }

    public async Task<TrainingDashboardDto> GetDashboardAsync(int? year = null, CancellationToken cancellationToken = default)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;

        // ── Programs ──────────────────────────────────────────────────────────
        var totalProgramsActive = await _programRepository.GetQueryable()
            .CountAsync(p => p.IsActive, cancellationToken);

        var categoryBreakdown = await _programRepository.GetQueryable()
            .Where(p => p.IsActive)
            .GroupBy(p => new { p.CategoryOptionId, CategoryName = p.CategoryOption != null ? p.CategoryOption.Name : "Uncategorised", Color = p.CategoryOption != null ? p.CategoryOption.ColorHex : null })
            .Select(g => new TrainingCategoryBreakdownDto
            {
                CategoryOptionId = g.Key.CategoryOptionId,
                CategoryName = g.Key.CategoryName,
                CategoryColor = g.Key.Color,
                ProgramsCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        // ── Schedules ─────────────────────────────────────────────────────────
        var totalSchedulesThisYear = await _scheduleRepository.GetQueryable()
            .CountAsync(s => s.StartDate.Year == targetYear, cancellationToken);

        var upcomingSchedules = (await _scheduleRepository.GetUpcomingSchedulesAsync(90))
            .Take(10)
            .ToSummaryDtoList();

        var upcomingSchedulesCount = upcomingSchedules.Count();

        // ── Nominations ───────────────────────────────────────────────────────
        var totalNominationsThisYear = await _nominationRepository.GetQueryable()
            .CountAsync(n => n.NominationDate.Year == targetYear, cancellationToken);

        var pendingStatuses = new[]
        {
            NominationStatus.Submitted,
            NominationStatus.SupervisorReview,
            NominationStatus.HrReview
        };

        var pendingNominationsCount = await _nominationRepository.GetQueryable()
            .CountAsync(n => pendingStatuses.Contains(n.Status), cancellationToken);

        var recentNominations = await _nominationRepository.GetQueryable()
            .OrderByDescending(n => n.NominationDate)
            .Take(10)
            .ToListAsync(cancellationToken);

        // ── Completions ───────────────────────────────────────────────────────
        var completionsThisYear = await _completionRepository.GetQueryable()
            .Where(c => c.CreatedAt.Year == targetYear)
            .ToListAsync(cancellationToken);

        var completedTrainingsThisYear = completionsThisYear.Count(c => c.IsPassed);
        var overallPassRate = completionsThisYear.Count > 0
            ? Math.Round((double)completedTrainingsThisYear / completionsThisYear.Count * 100, 1)
            : 0.0;

        // ── Certificates ──────────────────────────────────────────────────────
        var employeesCertifiedThisYear = await _certificateRepository.GetQueryable()
            .CountAsync(c => c.IssuedDate.Year == targetYear && c.Status == CertificateStatus.Active, cancellationToken);

        var expiringCertificates = await _certificateRepository.GetExpiringAsync(30);
        var expiringCertificatesIn30Days = expiringCertificates.Count();

        // ── Compliance ────────────────────────────────────────────────────────
        var allComplianceRecords = await _complianceRepository.GetQueryable()
            .ToListAsync(cancellationToken);

        var totalComplianceRecords = allComplianceRecords.Count;

        var compliantCount = allComplianceRecords.Count(r => r.Status == ComplianceStatus.Compliant);
        var overallComplianceRate = totalComplianceRecords > 0
            ? Math.Round((double)compliantCount / totalComplianceRecords * 100, 1)
            : 0.0;

        var nonCompliantEmployeesCount = allComplianceRecords
            .Where(r => r.Status == ComplianceStatus.NonCompliant)
            .Select(r => r.EmployeeId)
            .Distinct()
            .Count();

        // ── Mentoring ─────────────────────────────────────────────────────────
        var activeMentoringPairsCount = await _mentoringPairRepository.GetQueryable()
            .CountAsync(p => p.Status == MentoringStatus.Active, cancellationToken);

        // ── Learning paths ────────────────────────────────────────────────────
        var activeLearningPathEnrollmentsCount = await _learningPathEnrollmentRepository.GetQueryable()
            .CountAsync(e => !e.IsCompleted, cancellationToken);

        // ── Budget ────────────────────────────────────────────────────────────
        var approvedBudgets = await _budgetRepository.GetQueryable()
            .Where(b => b.Year == targetYear && b.Status == TrainingBudgetStatus.Approved)
            .ToListAsync(cancellationToken);

        var budgetAllocated = approvedBudgets.Sum(b => b.AllocatedAmount);
        var budgetSpent = approvedBudgets.Sum(b => b.SpentAmount);
        var budgetUtilizationRate = budgetAllocated > 0
            ? Math.Round((double)(budgetSpent / budgetAllocated) * 100, 1)
            : 0.0;

        return new TrainingDashboardDto
        {
            TotalProgramsActive = totalProgramsActive,
            TotalSchedulesThisYear = totalSchedulesThisYear,
            UpcomingSchedulesCount = upcomingSchedulesCount,
            TotalNominationsThisYear = totalNominationsThisYear,
            PendingNominationsCount = pendingNominationsCount,
            CompletedTrainingsThisYear = completedTrainingsThisYear,
            OverallPassRate = (decimal)overallPassRate,
            EmployeesCertifiedThisYear = employeesCertifiedThisYear,
            ExpiringCertificatesIn30Days = expiringCertificatesIn30Days,
            OverallComplianceRate = (decimal)overallComplianceRate,
            NonCompliantEmployeesCount = nonCompliantEmployeesCount,
            ActiveMentoringPairsCount = activeMentoringPairsCount,
            ActiveLearningPathEnrollmentsCount = activeLearningPathEnrollmentsCount,
            BudgetAllocated = budgetAllocated,
            BudgetSpent = budgetSpent,
            BudgetUtilizationRate = (decimal)budgetUtilizationRate,
            CategoryBreakdown = categoryBreakdown,
            RecentNominations = recentNominations.ToSummaryDtoList().ToList(),
            UpcomingSchedules = upcomingSchedules.ToList()
        };
    }

    public async Task<TrainingAnalyticsDto> GetAnalyticsAsync(int? year = null, CancellationToken cancellationToken = default)
    {
        var y = year ?? DateTime.UtcNow.Year;

        // ── Operations ──────────────────────────────────────────────────────────
        var activePrograms = await _programRepository.GetQueryable().CountAsync(p => p.IsActive, cancellationToken);

        var completions = await _completionRepository.GetQueryable()
            .Where(c => c.CompletionDate.Year == y)
            .ToListAsync(cancellationToken);
        var completionsYtd = completions.Count;
        var passedYtd = completions.Count(c => c.IsPassed);
        var passRate = completionsYtd > 0 ? Math.Round((decimal)passedYtd / completionsYtd * 100, 1) : 0m;

        var compliance = await _complianceRepository.GetQueryable().ToListAsync(cancellationToken);
        var complianceRate = compliance.Count > 0
            ? Math.Round((decimal)compliance.Count(r => r.Status == ComplianceStatus.Compliant) / compliance.Count * 100, 1)
            : 0m;

        var certsIssued = await _certificateRepository.GetQueryable()
            .CountAsync(c => c.IssuedDate.Year == y && c.Status == CertificateStatus.Active, cancellationToken);

        var budgets = await _budgetRepository.GetQueryable()
            .Where(b => b.Year == y && b.Status == TrainingBudgetStatus.Approved)
            .ToListAsync(cancellationToken);
        var allocated = budgets.Sum(b => b.AllocatedAmount);
        var spent = budgets.Sum(b => b.SpentAmount);
        var util = allocated > 0 ? Math.Round(spent / allocated * 100, 1) : 0m;
        var currency = budgets.FirstOrDefault()?.Currency ?? "GHS";

        // Category breakdown: active programs + completions per category
        var catPrograms = await _programRepository.GetQueryable()
            .Where(p => p.IsActive)
            .GroupBy(p => new { p.CategoryOptionId, Name = p.CategoryOption != null ? p.CategoryOption.Name : "Uncategorised", Color = p.CategoryOption != null ? p.CategoryOption.ColorHex : null })
            .Select(g => new { g.Key.CategoryOptionId, g.Key.Name, g.Key.Color, Programs = g.Count() })
            .ToListAsync(cancellationToken);

        var catCompletions = await _completionRepository.GetQueryable()
            .Where(c => c.CompletionDate.Year == y)
            .GroupBy(c => c.Nomination.Schedule.Program.CategoryOptionId)
            .Select(g => new { CategoryOptionId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var categoryBreakdown = catPrograms.Select(c => new TrainingCategoryBreakdownDto
        {
            CategoryOptionId = c.CategoryOptionId,
            CategoryName = c.Name,
            CategoryColor = c.Color,
            ProgramsCount = c.Programs,
            CompletionsCount = catCompletions.FirstOrDefault(cc => cc.CategoryOptionId == c.CategoryOptionId)?.Count ?? 0,
        }).OrderByDescending(c => c.ProgramsCount).ToList();

        var topTrainers = await _trainerRepository.GetQueryable()
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.TotalSessionsDelivered).ThenByDescending(t => t.TotalTrainingHoursDelivered)
            .Take(8)
            .Select(t => new TrainerUtilizationDto
            {
                TrainerId = t.Id,
                Name = t.Name,
                SessionsDelivered = t.TotalSessionsDelivered,
                HoursDelivered = t.TotalTrainingHoursDelivered,
                AverageRating = t.AverageRating,
                RatingsCount = t.TotalRatingsCount,
            })
            .ToListAsync(cancellationToken);

        var monthly = Enumerable.Range(1, 12).Select(m => new MonthlyCompletionPointDto
        {
            Month = m,
            MonthName = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m),
            Completed = completions.Count(c => c.CompletionDate.Month == m),
            Passed = completions.Count(c => c.CompletionDate.Month == m && c.IsPassed),
        }).ToList();

        // ── Effectiveness (Kirkpatrick) ──────────────────────────────────────────
        var nominated = await _nominationRepository.GetQueryable()
            .CountAsync(n => n.NominationDate.Year == y && (n.Status == NominationStatus.Approved || n.Status == NominationStatus.Confirmed), cancellationToken);

        var attended = await _attendanceRepository.GetQueryable()
            .Where(a => a.IsPresent && a.AttendanceDate.Year == y)
            .Select(a => new { a.EmployeeId, a.ScheduleId })
            .Distinct()
            .CountAsync(cancellationToken);

        var feedback = await _feedbackRepository.GetQueryable()
            .Where(f => f.FeedbackDate.Year == y)
            .ToListAsync(cancellationToken);
        var followUps = await _followUpRepository.GetQueryable()
            .CountAsync(f => f.AssessmentDate.Year == y, cancellationToken);

        var satisfactionVals = feedback.Where(f => f.OverallSatisfactionRating.HasValue).Select(f => (decimal)f.OverallSatisfactionRating!.Value).ToList();
        var applyVals = feedback.Where(f => f.LikelihoodToApply.HasValue).Select(f => (decimal)f.LikelihoodToApply!.Value).ToList();
        var preVals = completions.Where(c => c.PreAssessmentScore.HasValue).Select(c => c.PreAssessmentScore!.Value).ToList();
        var postVals = completions.Where(c => c.PostAssessmentScore.HasValue).Select(c => c.PostAssessmentScore!.Value).ToList();
        var gainVals = completions.Where(c => c.PreAssessmentScore.HasValue && c.PostAssessmentScore.HasValue)
            .Select(c => c.PostAssessmentScore!.Value - c.PreAssessmentScore!.Value).ToList();

        return new TrainingAnalyticsDto
        {
            Year = y,
            Currency = currency,
            ActiveProgramsCount = activePrograms,
            CompletionsYtd = completionsYtd,
            PassedYtd = passedYtd,
            PassRate = passRate,
            ComplianceRate = complianceRate,
            CertificatesIssuedYtd = certsIssued,
            BudgetAllocated = allocated,
            BudgetSpent = spent,
            BudgetUtilizationRate = util,
            CategoryBreakdown = categoryBreakdown,
            TopTrainers = topTrainers,
            MonthlyCompletions = monthly,
            Funnel = new KirkpatrickFunnelDto
            {
                Nominated = nominated,
                Attended = attended,
                Completed = completionsYtd,
                FeedbackL1 = feedback.Count,
                FollowUpL23 = followUps,
            },
            FeedbackResponses = feedback.Count,
            AvgSatisfaction = satisfactionVals.Count > 0 ? Math.Round(satisfactionVals.Average(), 2) : null,
            WouldRecommendRate = feedback.Count > 0 ? Math.Round((decimal)feedback.Count(f => f.WouldRecommend) / feedback.Count * 100, 1) : 0m,
            AvgLikelihoodToApply = applyVals.Count > 0 ? Math.Round(applyVals.Average(), 2) : null,
            AvgPreScore = preVals.Count > 0 ? Math.Round(preVals.Average(), 1) : null,
            AvgPostScore = postVals.Count > 0 ? Math.Round(postVals.Average(), 1) : null,
            AvgScoreGain = gainVals.Count > 0 ? Math.Round(gainVals.Average(), 1) : null,
            FollowUpsCompleted = followUps,
            CostPerCompletion = completionsYtd > 0 ? Math.Round(spent / completionsYtd, 2) : null,
        };
    }

    public async Task<EmployeeTrainingSummaryDto> GetEmployeeSummaryAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var completions = await _completionRepository.GetByEmployeeIdAsync(employeeId);
        var completionsList = completions.ToList();

        var certificates = await _certificateRepository.GetByEmployeeIdAsync(employeeId);
        var certList = certificates.ToList();

        var complianceRecords = await _complianceRepository.GetByEmployeeIdAsync(employeeId);
        var complianceList = complianceRecords.ToList();

        var learningPathEnrollments = await _learningPathEnrollmentRepository.GetByEmployeeIdAsync(employeeId);
        var enrollmentList = learningPathEnrollments.ToList();

        var activeMentoringPairs = await _mentoringPairRepository.GetQueryable()
            .CountAsync(p => (p.MentorId == employeeId || p.MenteeId == employeeId) && p.Status == MentoringStatus.Active, cancellationToken);

        return new EmployeeTrainingSummaryDto
        {
            EmployeeId = employeeId,
            TotalTrainingsCompleted = completionsList.Count(c => c.IsPassed),
            ActiveCertificatesCount = certList.Count(c => c.Status == CertificateStatus.Active),
            ExpiringCertificatesCount = certList.Count(c => c.Status == CertificateStatus.Active && c.ExpiryDate.HasValue && c.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30)),
            ComplianceRequirementsCount = complianceList.Count,
            CompliantRequirementsCount = complianceList.Count(r => r.Status == ComplianceStatus.Compliant),
            LearningPathsEnrolledCount = enrollmentList.Count,
            LearningPathsCompletedCount = enrollmentList.Count(e => e.IsCompleted),
            HasActiveMentoringPair = activeMentoringPairs > 0
        };
    }
}
