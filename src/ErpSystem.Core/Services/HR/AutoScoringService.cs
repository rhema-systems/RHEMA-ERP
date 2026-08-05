using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
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
    private readonly IJobVacancyRepository _vacancyRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<AutoScoringService> _logger;

    public AutoScoringService(
        IJobApplicationService applicationService,
        IJobApplicationRepository applicationRepository,
        IJobVacancyRepository vacancyRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<AutoScoringService> logger)
    {
        _applicationService    = applicationService;
        _applicationRepository = applicationRepository;
        _vacancyRepository     = vacancyRepository;
        _currentUserProvider   = currentUserProvider;
        _logger                = logger;
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

    /// <inheritdoc/>
    public Task<ApplicationAutoScoreDto> ScoreApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        // EvaluateApplicationScoreAsync already enforces tenant ownership on the application.
        _ = GetTenantId();
        return _applicationService.EvaluateApplicationScoreAsync(applicationId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<RecruitmentScoringRunResultDto> RunScoringForVacancyAsync(
        Guid vacancyId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var vacancy = await _vacancyRepository.GetByIdAsync(vacancyId);
        if (vacancy == null || vacancy.TenantId != tenantId)
            throw new ArgumentException($"Job vacancy with ID '{vacancyId}' not found.");

        var runResult = new RecruitmentScoringRunResultDto
        {
            VacancyId = vacancyId,
            RanAt     = DateTime.UtcNow,
        };

        // Single bulk fetch — loads all navigation properties required by the scoring engine
        // in one query instead of one GetWithFullDetailsAsync call per application.
        var applications = await _applicationRepository.GetAllWithFullDetailsByVacancyIdAsync(vacancyId);

        // Exclude terminal-state applications and any row that does not belong to this tenant.
        var scoreable = applications
            .Where(a => a.TenantId == tenantId
                     && a.Status != ApplicationStatus.Withdrawn
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
