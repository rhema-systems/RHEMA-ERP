using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Handlers;

/// <summary>
/// Theme 12 base. Resolves the appraised employee's active probation period so a probation
/// appraisal's outcome (confirm / extend) updates the real onboarding record.
/// </summary>
/// <remarks>
/// ⚠ <b>These handlers delegate to <see cref="IProbationService"/>; they do not mutate the
/// probation themselves.</b> They used to, and that made area 5 a second, divergent writer of
/// probation status: confirming from an appraisal recommendation flipped the status and nothing
/// else, so the employee record was never updated, no extension audit row was written, and the
/// outcome-notes field was overwritten by a path that had no business owning it. Area 15b slice 5
/// converged them. Anything added to confirm/extend belongs in the service, once, where both
/// callers reach it.
/// </remarks>
public abstract class ProbationHandlerBase : IOutcomeRecommendationHandler
{
    protected readonly IGenericRepository<ProbationPeriod> ProbationRepository;
    protected readonly IGenericRepository<PerformanceAppraisal> AppraisalRepository;
    protected readonly IProbationService ProbationService;
    protected readonly ICurrentUserProvider CurrentUserProvider;
    protected readonly IUnitOfWork UnitOfWork;
    protected readonly ILogger Logger;

    protected ProbationHandlerBase(
        IGenericRepository<ProbationPeriod> probationRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IProbationService probationService,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger logger)
    {
        ProbationRepository = probationRepository;
        AppraisalRepository = appraisalRepository;
        ProbationService = probationService;
        CurrentUserProvider = currentUserProvider;
        UnitOfWork = unitOfWork;
        Logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    protected Guid GetTenantId()
    {
        var tenantId = CurrentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // Callers supply the tenant alongside the recommendation. Reject anything other than
    // the authenticated tenant so a supplied id can never widen the scope of a write.
    protected Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    public abstract RecommendationType Type { get; }

    protected async Task<ProbationPeriod?> GetActiveProbationAsync(AppraisalOutcomeRecommendation rec, CancellationToken cancellationToken)
    {
        var tenantId = RequireCurrentTenant(rec.TenantId);
        var appraisal = await AppraisalRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == rec.PerformanceAppraisalId && a.TenantId == tenantId, cancellationToken);
        if (appraisal == null) return null;

        return await ProbationRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId && p.EmployeeId == appraisal.EmployeeId && p.Status == ProbationStatus.Active)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public abstract Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(
        AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default);
}

/// <summary>Confirms probation: marks the active probation period Completed.</summary>
public class ConfirmProbationHandler : ProbationHandlerBase
{
    public ConfirmProbationHandler(
        IGenericRepository<ProbationPeriod> probationRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IProbationService probationService,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ConfirmProbationHandler> logger)
        : base(probationRepository, appraisalRepository, probationService, currentUserProvider, unitOfWork, logger) { }

    public override RecommendationType Type => RecommendationType.ConfirmProbation;

    public override async Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(
        AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        var probation = await GetActiveProbationAsync(recommendation, cancellationToken);
        if (probation == null)
        {
            Logger.LogWarning("ConfirmProbationHandler: no active probation period for appraisal {Id}", recommendation.PerformanceAppraisalId);
            return null;
        }

        // Through the service, so the employee record is confirmed too - see the class remarks.
        await ProbationService.ConfirmAsync(
            probation.Id,
            CurrentUserProvider.UserId,
            recommendation.Notes ?? "Confirmed via appraisal recommendation.",
            cancellationToken);

        Logger.LogInformation("Probation {Id} confirmed (Completed) from appraisal recommendation", probation.Id);
        return ("ProbationPeriod", probation.Id);
    }
}

/// <summary>Extends probation: pushes the end date out and increments the extension count.</summary>
public class ExtendProbationHandler : ProbationHandlerBase
{
    private const int DefaultExtensionMonths = 3;

    public ExtendProbationHandler(
        IGenericRepository<ProbationPeriod> probationRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IProbationService probationService,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ExtendProbationHandler> logger)
        : base(probationRepository, appraisalRepository, probationService, currentUserProvider, unitOfWork, logger) { }

    public override RecommendationType Type => RecommendationType.ExtendProbation;

    public override async Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(
        AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireCurrentTenant(recommendation.TenantId);
        var probation = await GetActiveProbationAsync(recommendation, cancellationToken);
        if (probation == null)
        {
            Logger.LogWarning("ExtendProbationHandler: no active probation period for appraisal {Id}", recommendation.PerformanceAppraisalId);
            return null;
        }

        // Configurable extension length (AppraisalSettings.ProbationExtensionMonths), defaulting to 3.
        var configuredMonths = await AppraisalRepository.GetQueryable()
            .Where(a => a.Id == recommendation.PerformanceAppraisalId && a.TenantId == tenantId)
            .Select(a => (int?)a.AppraisalCycle.AppraisalSettings.ProbationExtensionMonths)
            .FirstOrDefaultAsync(cancellationToken);
        var months = configuredMonths is > 0 ? configuredMonths.Value : DefaultExtensionMonths;

        // Through the service, so the extension is AUDITED - the direct write here left the
        // ProbationExtension trail empty and clobbered OutcomeNotes. Stays Active either way.
        await ProbationService.ExtendAsync(
            probation.Id,
            new CreateProbationExtensionDto
            {
                ProbationPeriodId = probation.Id,
                NewEndDate = probation.CurrentEndDate.AddMonths(months),
                ExtensionMonths = months,
                Reason = recommendation.Notes ?? $"Extended by {months} months via appraisal recommendation.",
                Comments = "Raised from an appraisal outcome recommendation.",
            },
            CurrentUserProvider.UserId,
            cancellationToken);

        Logger.LogInformation("Probation {Id} extended by {Months}m from appraisal recommendation", probation.Id, months);
        return ("ProbationPeriod", probation.Id);
    }
}
