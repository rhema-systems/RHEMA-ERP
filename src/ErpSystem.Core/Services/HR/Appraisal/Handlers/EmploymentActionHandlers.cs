using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Handlers;

/// <summary>
/// Production-readiness Phase B base. Routes an approved employment-action recommendation
/// (Promotion / Demotion / ContractRenewal / Termination / Recognition) to a lightweight
/// <see cref="EmploymentActionProposal"/> intake record — avoiding the need to supply a heavyweight
/// target container (target position + salary for a StaffMovement, an AwardType for an AwardNomination,
/// etc.) at approval time. HR then actions the proposal in the destination module.
/// </summary>
public abstract class EmploymentActionHandlerBase : IOutcomeRecommendationHandler
{
    private readonly IGenericRepository<EmploymentActionProposal> _proposalRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _logger;

    protected EmploymentActionHandlerBase(
        IGenericRepository<EmploymentActionProposal> proposalRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger logger)
    {
        _proposalRepository = proposalRepository;
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

    public abstract RecommendationType Type { get; }
    protected abstract EmploymentActionType ActionType { get; }

    public async Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(
        AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireCurrentTenant(recommendation.TenantId);
        var appraisal = await _appraisalRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == recommendation.PerformanceAppraisalId && a.TenantId == tenantId, cancellationToken);
        if (appraisal == null)
        {
            _logger.LogWarning("EmploymentActionHandler: appraisal {Id} not found", recommendation.PerformanceAppraisalId);
            return null;
        }

        // Idempotency: a re-dispatch (worklist Retry) of an Approved-but-not-Actioned recommendation reuses
        // the existing proposal of this type rather than creating a duplicate.
        var existing = await _proposalRepository.GetQueryable()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.SourceAppraisalId == appraisal.Id && p.ActionType == ActionType, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("EmploymentAction: existing {ActionType} proposal {Id} reused for appraisal {AppraisalId}",
                ActionType, existing.Id, appraisal.Id);
            return ("EmploymentActionProposal", existing.Id);
        }

        var entity = new EmploymentActionProposal
        {
            TenantId = tenantId,
            EmployeeId = appraisal.EmployeeId,
            SourceAppraisalId = appraisal.Id,
            ActionType = ActionType,
            Status = EmploymentActionProposalStatus.Proposed,
            Notes = recommendation.Notes
        };

        await _proposalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("EmploymentAction: raised {ActionType} proposal for employee {EmployeeId} from appraisal {AppraisalId}",
            ActionType, appraisal.EmployeeId, appraisal.Id);

        return ("EmploymentActionProposal", entity.Id);
    }
}

public class PromotionActionHandler : EmploymentActionHandlerBase
{
    public PromotionActionHandler(
        IGenericRepository<EmploymentActionProposal> proposalRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PromotionActionHandler> logger)
        : base(proposalRepository, appraisalRepository, currentUserProvider, unitOfWork, logger) { }

    public override RecommendationType Type => RecommendationType.Promotion;
    protected override EmploymentActionType ActionType => EmploymentActionType.Promotion;
}

public class DemotionActionHandler : EmploymentActionHandlerBase
{
    public DemotionActionHandler(
        IGenericRepository<EmploymentActionProposal> proposalRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<DemotionActionHandler> logger)
        : base(proposalRepository, appraisalRepository, currentUserProvider, unitOfWork, logger) { }

    public override RecommendationType Type => RecommendationType.Demotion;
    protected override EmploymentActionType ActionType => EmploymentActionType.Demotion;
}

public class ContractRenewalActionHandler : EmploymentActionHandlerBase
{
    public ContractRenewalActionHandler(
        IGenericRepository<EmploymentActionProposal> proposalRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ContractRenewalActionHandler> logger)
        : base(proposalRepository, appraisalRepository, currentUserProvider, unitOfWork, logger) { }

    public override RecommendationType Type => RecommendationType.ContractRenewal;
    protected override EmploymentActionType ActionType => EmploymentActionType.ContractRenewal;
}

public class TerminationActionHandler : EmploymentActionHandlerBase
{
    public TerminationActionHandler(
        IGenericRepository<EmploymentActionProposal> proposalRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TerminationActionHandler> logger)
        : base(proposalRepository, appraisalRepository, currentUserProvider, unitOfWork, logger) { }

    public override RecommendationType Type => RecommendationType.Termination;
    protected override EmploymentActionType ActionType => EmploymentActionType.Termination;
}

public class RecognitionActionHandler : EmploymentActionHandlerBase
{
    public RecognitionActionHandler(
        IGenericRepository<EmploymentActionProposal> proposalRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<RecognitionActionHandler> logger)
        : base(proposalRepository, appraisalRepository, currentUserProvider, unitOfWork, logger) { }

    public override RecommendationType Type => RecommendationType.Recognition;
    protected override EmploymentActionType ActionType => EmploymentActionType.Recognition;
}
