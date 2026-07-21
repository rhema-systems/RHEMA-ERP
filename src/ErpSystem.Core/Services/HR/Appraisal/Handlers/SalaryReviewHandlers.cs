using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Handlers;

/// <summary>
/// Theme 11 base. Creates a <see cref="SalaryReviewProposal"/> (a pay-for-performance intake
/// record) from an approved compensation recommendation — handed off to HR/payroll for approval
/// before it touches actual pay.
/// </summary>
public abstract class SalaryReviewHandlerBase : IOutcomeRecommendationHandler
{
    private readonly IGenericRepository<SalaryReviewProposal> _proposalRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _logger;

    protected SalaryReviewHandlerBase(
        IGenericRepository<SalaryReviewProposal> proposalRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IUnitOfWork unitOfWork,
        ILogger logger)
    {
        _proposalRepository = proposalRepository;
        _appraisalRepository = appraisalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public abstract RecommendationType Type { get; }
    protected abstract SalaryReviewProposalType ProposalType { get; }

    public async Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(
        AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == recommendation.PerformanceAppraisalId, cancellationToken);
        if (appraisal == null)
        {
            _logger.LogWarning("SalaryReviewHandler: appraisal {Id} not found", recommendation.PerformanceAppraisalId);
            return null;
        }

        // Idempotency: if this appraisal already produced a proposal of this type, reuse it instead of
        // creating a duplicate when an Approved-but-not-Actioned recommendation is re-dispatched (Retry).
        var existing = await _proposalRepository.GetQueryable()
            .FirstOrDefaultAsync(p => p.SourceAppraisalId == appraisal.Id && p.ProposalType == ProposalType, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("Compensation: existing {ProposalType} proposal {Id} reused for appraisal {AppraisalId}",
                ProposalType, existing.Id, appraisal.Id);
            return ("SalaryReviewProposal", existing.Id);
        }

        var entity = new SalaryReviewProposal
        {
            EmployeeId = appraisal.EmployeeId,
            SourceAppraisalId = appraisal.Id,
            ProposalType = ProposalType,
            Status = SalaryReviewProposalStatus.Proposed,
            Notes = recommendation.Notes
        };

        await _proposalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Compensation: raised {ProposalType} proposal for employee {EmployeeId} from appraisal {AppraisalId}",
            ProposalType, appraisal.EmployeeId, appraisal.Id);

        return ("SalaryReviewProposal", entity.Id);
    }
}

public class MeritIncreaseHandler : SalaryReviewHandlerBase
{
    public MeritIncreaseHandler(
        IGenericRepository<SalaryReviewProposal> proposalRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IUnitOfWork unitOfWork,
        ILogger<MeritIncreaseHandler> logger)
        : base(proposalRepository, appraisalRepository, unitOfWork, logger) { }

    public override RecommendationType Type => RecommendationType.MeritIncrease;
    protected override SalaryReviewProposalType ProposalType => SalaryReviewProposalType.MeritIncrease;
}

public class BonusHandler : SalaryReviewHandlerBase
{
    public BonusHandler(
        IGenericRepository<SalaryReviewProposal> proposalRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IUnitOfWork unitOfWork,
        ILogger<BonusHandler> logger)
        : base(proposalRepository, appraisalRepository, unitOfWork, logger) { }

    public override RecommendationType Type => RecommendationType.Bonus;
    protected override SalaryReviewProposalType ProposalType => SalaryReviewProposalType.Bonus;
}
