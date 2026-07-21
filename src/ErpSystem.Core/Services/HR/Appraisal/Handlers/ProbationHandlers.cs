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
public abstract class ProbationHandlerBase : IOutcomeRecommendationHandler
{
    protected readonly IGenericRepository<ProbationPeriod> ProbationRepository;
    protected readonly IGenericRepository<PerformanceAppraisal> AppraisalRepository;
    protected readonly IUnitOfWork UnitOfWork;
    protected readonly ILogger Logger;

    protected ProbationHandlerBase(
        IGenericRepository<ProbationPeriod> probationRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IUnitOfWork unitOfWork,
        ILogger logger)
    {
        ProbationRepository = probationRepository;
        AppraisalRepository = appraisalRepository;
        UnitOfWork = unitOfWork;
        Logger = logger;
    }

    public abstract RecommendationType Type { get; }

    protected async Task<ProbationPeriod?> GetActiveProbationAsync(AppraisalOutcomeRecommendation rec, CancellationToken cancellationToken)
    {
        var appraisal = await AppraisalRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == rec.PerformanceAppraisalId, cancellationToken);
        if (appraisal == null) return null;

        return await ProbationRepository.GetQueryable()
            .Where(p => p.EmployeeId == appraisal.EmployeeId && p.Status == ProbationStatus.Active)
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
        IUnitOfWork unitOfWork,
        ILogger<ConfirmProbationHandler> logger)
        : base(probationRepository, appraisalRepository, unitOfWork, logger) { }

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

        probation.Status = ProbationStatus.Completed;
        probation.OutcomeNotes = recommendation.Notes ?? "Confirmed via appraisal recommendation.";

        await ProbationRepository.UpdateAsync(probation);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

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
        IUnitOfWork unitOfWork,
        ILogger<ExtendProbationHandler> logger)
        : base(probationRepository, appraisalRepository, unitOfWork, logger) { }

    public override RecommendationType Type => RecommendationType.ExtendProbation;

    public override async Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(
        AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        var probation = await GetActiveProbationAsync(recommendation, cancellationToken);
        if (probation == null)
        {
            Logger.LogWarning("ExtendProbationHandler: no active probation period for appraisal {Id}", recommendation.PerformanceAppraisalId);
            return null;
        }

        // Configurable extension length (AppraisalSettings.ProbationExtensionMonths), defaulting to 3.
        var configuredMonths = await AppraisalRepository.GetQueryable()
            .Where(a => a.Id == recommendation.PerformanceAppraisalId)
            .Select(a => (int?)a.AppraisalCycle.AppraisalSettings.ProbationExtensionMonths)
            .FirstOrDefaultAsync(cancellationToken);
        var months = configuredMonths is > 0 ? configuredMonths.Value : DefaultExtensionMonths;

        probation.CurrentEndDate = probation.CurrentEndDate.AddMonths(months);
        probation.ExtensionCount += 1;
        probation.OutcomeNotes = recommendation.Notes ?? $"Extended by {months} months via appraisal recommendation.";
        // Stays Active.

        await ProbationRepository.UpdateAsync(probation);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Probation {Id} extended by {Months}m from appraisal recommendation", probation.Id, months);
        return ("ProbationPeriod", probation.Id);
    }
}
