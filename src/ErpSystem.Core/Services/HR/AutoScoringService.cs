using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Delegates per-application scoring to <see cref="IJobApplicationService.EvaluateApplicationScoreAsync"/>
/// (the scoring engine lives there alongside all other application mutations) and wraps the vacancy-level
/// bulk runner in a <see cref="RecruitmentScoringRunResultDto"/> that captures per-item errors without
/// aborting the batch.
/// </summary>
public sealed class AutoScoringService : IAutoScoringService
{
    private readonly IJobApplicationService _applicationService;
    private readonly IJobApplicationRepository _applicationRepository;
    private readonly ILogger<AutoScoringService> _logger;

    public AutoScoringService(
        IJobApplicationService applicationService,
        IJobApplicationRepository applicationRepository,
        ILogger<AutoScoringService> logger)
    {
        _applicationService    = applicationService;
        _applicationRepository = applicationRepository;
        _logger                = logger;
    }

    /// <inheritdoc/>
    public Task<ApplicationAutoScoreDto> ScoreApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
        => _applicationService.EvaluateApplicationScoreAsync(applicationId, cancellationToken);

    /// <inheritdoc/>
    public async Task<RecruitmentScoringRunResultDto> RunScoringForVacancyAsync(
        Guid vacancyId,
        CancellationToken cancellationToken = default)
    {
        var runResult = new RecruitmentScoringRunResultDto
        {
            VacancyId = vacancyId,
            RanAt     = DateTime.UtcNow,
        };

        // Single bulk fetch — loads all navigation properties required by the scoring engine
        // in one query instead of one GetWithFullDetailsAsync call per application.
        var applications = await _applicationRepository.GetAllWithFullDetailsByVacancyIdAsync(vacancyId);

        // Exclude terminal-state applications — scoring them would be pointless
        var scoreable = applications
            .Where(a => a.Status != ApplicationStatus.Withdrawn
                     && a.Status != ApplicationStatus.Rejected)
            .ToList();

        runResult.Total = scoreable.Count;

        foreach (var app in scoreable)
        {
            try
            {
                var dto = await _applicationService.EvaluateLoadedApplicationScoreAsync(app, cancellationToken);
                runResult.Succeeded++;
                runResult.Results.Add(dto);
            }
            catch (Exception ex)
            {
                runResult.Failed++;
                runResult.Errors.Add(new RecruitmentScoringRunErrorDto
                {
                    ApplicationId = app.Id,
                    Error         = ex.Message,
                });

                _logger.LogWarning(
                    "Scoring failed for application {ApplicationId} (vacancy {VacancyId}): {Error}",
                    app.Id, vacancyId, ex.Message);
            }
        }

        _logger.LogInformation(
            "Scoring run for vacancy {VacancyId} complete — {Succeeded}/{Total} succeeded, {Failed} failed.",
            vacancyId, runResult.Succeeded, runResult.Total, runResult.Failed);

        return runResult;
    }
}
