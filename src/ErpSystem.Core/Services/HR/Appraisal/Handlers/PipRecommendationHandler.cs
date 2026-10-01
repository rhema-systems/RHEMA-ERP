using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Handlers;

/// <summary>
/// Production-readiness Phase B handler. On approval of a PerformanceImprovementPlan recommendation,
/// creates a real <see cref="PerformanceImprovementPlan"/> anchored to the appraisal — the PIP
/// module is fully built, so a PIP is the natural, self-contained artifact for this outcome.
///
/// <para>The plan is raised in <see cref="PipStatus.Draft"/>, which is what this handler always
/// meant by "draft" — its placeholder issues and standards ("to be agreed at the kick-off") are
/// not something to serve on an employee unread. HR fills it in and puts it through the
/// <c>PerformanceImprovementPlan</c> approval workflow, which is what makes it Active.</para>
/// </summary>
public class PipRecommendationHandler : IOutcomeRecommendationHandler
{
    private readonly IGenericRepository<PerformanceImprovementPlan> _pipRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PipRecommendationHandler> _logger;

    public PipRecommendationHandler(
        IGenericRepository<PerformanceImprovementPlan> pipRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PipRecommendationHandler> logger)
    {
        _pipRepository = pipRepository;
        _appraisalRepository = appraisalRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    public RecommendationType Type => RecommendationType.PerformanceImprovementPlan;

    public async Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(
        AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireCurrentTenant(recommendation.TenantId);
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.Id == recommendation.PerformanceAppraisalId && a.TenantId == tenantId, cancellationToken);
        if (appraisal == null)
        {
            _logger.LogWarning("PipRecommendationHandler: appraisal {Id} not found", recommendation.PerformanceAppraisalId);
            return null;
        }

        // Idempotency: reuse the PIP already raised from this appraisal on a re-dispatch.
        var existing = await _pipRepository.GetQueryable()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.AppraisalId == appraisal.Id, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("PIP: existing plan {Id} reused for appraisal {AppraisalId}", existing.Id, appraisal.Id);
            return ("PerformanceImprovementPlan", existing.Id);
        }

        // Decision D-75: the rules a plan created on the PIP screen is held to. One live or in-flight
        // plan per employee — a second one is not raised; the recommendation stays Approved-not-
        // Actioned and HR sees why in the log.
        var blocking = await _pipRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId && p.EmployeeId == appraisal.EmployeeId
                     && (p.Status == PipStatus.Active || p.Status == PipStatus.InProgress
                         || p.Status == PipStatus.PendingApproval))
            .Select(p => p.PipNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (blocking != null)
        {
            _logger.LogWarning(
                "PipRecommendationHandler: employee {EmployeeId} is already on plan {PipNumber}; no second plan raised.",
                appraisal.EmployeeId, blocking);
            return null;
        }

        // PIP.SupervisorId is a required Employee FK. Prefer the employee's manager, then the
        // recommendation approver/recommender — never the employee themselves (D-75: the fallback
        // could name them). If none can be resolved, leave Approved-not-Actioned.
        Guid? NotTheEmployee(Guid? id) => id is Guid g && g != Guid.Empty && g != appraisal.EmployeeId ? g : null;
        var supervisorId = NotTheEmployee(appraisal.Employee?.ManagerId)
                           ?? NotTheEmployee(recommendation.ApprovedById)
                           ?? NotTheEmployee(recommendation.RecommendedById);
        if (supervisorId is null)
        {
            _logger.LogWarning(
                "PipRecommendationHandler: cannot resolve a supervisor for employee {EmployeeId}; PIP not created.",
                appraisal.EmployeeId);
            return null;
        }

        var now = DateTime.UtcNow;
        var entity = new PerformanceImprovementPlan
        {
            TenantId = tenantId,
            // The tenant's own sequence, shared with the PIP screen (D-75).
            PipNumber = await PipNumbering.NextAsync(_pipRepository, tenantId, cancellationToken),
            EmployeeId = appraisal.EmployeeId,
            AppraisalId = appraisal.Id,
            SupervisorId = supervisorId.Value,
            // The HR officer who approved the outcome owns the plan they set in motion (D-75): the
            // approval is the desk's. Left empty when that would be the employee.
            HROwnerId = NotTheEmployee(recommendation.ApprovedById),
            StartDate = now,
            EndDate = now.AddDays(90),
            Status = PipStatus.Draft,
            PerformanceIssues = !string.IsNullOrWhiteSpace(recommendation.Notes)
                ? recommendation.Notes!
                : "Performance concerns identified during the appraisal review.",
            ExpectedStandards = "To be defined with the employee at the PIP kick-off.",
            ImprovementActions = "To be agreed with the employee at the PIP kick-off."
        };

        await _pipRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIP {PipNumber} created for employee {EmployeeId} from appraisal {AppraisalId}",
            entity.PipNumber, appraisal.EmployeeId, appraisal.Id);

        return ("PerformanceImprovementPlan", entity.Id);
    }
}
