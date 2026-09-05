using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SLICE 17 — PART D ENVIRONMENTAL CORE (AB, AC).
//
// SheEnvironmentalReviewService — the FR-ENV-001–016 compliance review /
// screening / clearance register. Every lifecycle step appends an immutable
// action row (FR-ENV-006/011); clearance is gated on approval + whatever the
// screening determined (management approval, EPA submission). The Project
// module's auto-trigger and hard block (FR-ENV-008/010/012/013, DR-09) are a
// deliberately stubbed seam — reviews are created manually until a Project
// trigger source exists.
//
// SheMonthlyEnvironmentalReportService — FR-ENV-033/034. Reports are computed
// from the live registers at generation time; the reminder engine generates
// the prior month when missing; submission freezes the row as history.
// ============================================================================

#region Environmental Compliance Reviews

public class SheEnvironmentalReviewService : ISheEnvironmentalReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<SheEnvironmentalReviewService> _logger;

    public SheEnvironmentalReviewService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<SheEnvironmentalReviewService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private static readonly SheEnvironmentalReviewStatus[] EditableStatuses =
    {
        SheEnvironmentalReviewStatus.Submitted,
        SheEnvironmentalReviewStatus.CorrectionsRequested,
    };

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Reads scope to the authenticated tenant explicitly.
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

    private async Task<SheEnvironmentalReview> GetOwnedReviewAsync(Guid id)
    {
        var review = await _unitOfWork.Repository<SheEnvironmentalReview>().GetByIdAsync(id);
        if (review == null || review.TenantId != GetTenantId() || review.IsDeleted)
            throw new ArgumentException($"Environmental review with ID '{id}' not found.");
        return review;
    }

    private async Task<Employee> GetOwnedEmployeeAsync(Guid id)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(id);
        if (employee == null || employee.TenantId != GetTenantId() || employee.IsDeleted)
            throw new ArgumentException($"Employee with ID '{id}' not found.");
        return employee;
    }

    private async Task GuardOptionalEmployeeAsync(Guid? employeeId)
    {
        if (employeeId != null) await GetOwnedEmployeeAsync(employeeId.Value);
    }

    private async Task GuardOptionalOrgUnitAsync(Guid? orgUnitId)
    {
        if (orgUnitId == null) return;
        var unit = await _unitOfWork.Repository<OrganizationUnit>().GetByIdAsync(orgUnitId.Value);
        if (unit == null || unit.TenantId != GetTenantId() || unit.IsDeleted)
            throw new ArgumentException($"Organization unit with ID '{orgUnitId}' not found.");
    }

    private async Task AppendActionAsync(SheEnvironmentalReview review, string action, string? notes, Guid actorId)
    {
        await _unitOfWork.Repository<SheEnvironmentalReviewAction>().AddAsync(new SheEnvironmentalReviewAction
        {
            TenantId = review.TenantId,
            ReviewId = review.Id,
            Action = action,
            Notes = notes,
            ActorId = actorId,
            ActionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId.ToString(),
        });
    }

    private async Task<SheEnvironmentalReviewDto> ReadDtoAsync(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<SheEnvironmentalReview>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && r.Id == id)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.ResponsibleManager)
            .Include(r => r.SubmittedBy)
            .Include(r => r.ScreenedBy)
            .Include(r => r.ApprovedBy)
            .Include(r => r.ManagementApprovedBy)
            .Include(r => r.ClearanceIssuedBy)
            .Include(r => r.CommencementApprovedBy)
            .FirstOrDefaultAsync(ct);
        if (entity == null)
            throw new ArgumentException($"Environmental review with ID '{id}' not found.");

        var dto = ToDto(entity);
        dto.Actions = await _unitOfWork.Repository<SheEnvironmentalReviewAction>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted && a.ReviewId == id)
            .Include(a => a.Actor)
            .OrderByDescending(a => a.ActionDate)
            .Select(a => new SheEnvironmentalReviewActionDto
            {
                Id = a.Id,
                Action = a.Action,
                Notes = a.Notes,
                ActorId = a.ActorId,
                ActorName = a.Actor.FullName ?? string.Empty,
                ActionDate = a.ActionDate,
            })
            .ToListAsync(ct);
        return dto;
    }

    private static SheEnvironmentalReviewDto ToDto(SheEnvironmentalReview e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ReviewNumber = e.ReviewNumber,
        ProjectName = e.ProjectName,
        WorkClassification = e.WorkClassification,
        ProjectReference = e.ProjectReference,
        OrganizationUnitId = e.OrganizationUnitId,
        OrganizationUnitName = e.OrganizationUnit?.Name,
        ResponsibleManagerId = e.ResponsibleManagerId,
        ResponsibleManagerName = e.ResponsibleManager?.FullName,
        SubmittedById = e.SubmittedById,
        SubmittedByName = e.SubmittedBy?.FullName ?? string.Empty,
        SubmittedDate = e.SubmittedDate,
        PlannedStartDate = e.PlannedStartDate,
        Description = e.Description,
        ApplicableLaws = e.ApplicableLaws,
        PermitRequired = e.PermitRequired,
        ComplianceChecklist = e.ComplianceChecklist,
        RequiresRegistration = e.RequiresRegistration,
        RequiresEnvironmentalPermit = e.RequiresEnvironmentalPermit,
        RequiresFullEia = e.RequiresFullEia,
        RequiresRiskAssessment = e.RequiresRiskAssessment,
        RequiresEpaSubmission = e.RequiresEpaSubmission,
        RequiresManagementApproval = e.RequiresManagementApproval,
        ScreeningNotes = e.ScreeningNotes,
        ScreeningCompletedDate = e.ScreeningCompletedDate,
        ScreenedById = e.ScreenedById,
        ScreenedByName = e.ScreenedBy?.FullName,
        Status = e.Status,
        OfficerComments = e.OfficerComments,
        ApprovedDate = e.ApprovedDate,
        ApprovedById = e.ApprovedById,
        ApprovedByName = e.ApprovedBy?.FullName,
        ManagementApprovedDate = e.ManagementApprovedDate,
        ManagementApprovedById = e.ManagementApprovedById,
        ManagementApprovedByName = e.ManagementApprovedBy?.FullName,
        EpaSubmissionDate = e.EpaSubmissionDate,
        EpaSubmissionReference = e.EpaSubmissionReference,
        ClearanceIssuedDate = e.ClearanceIssuedDate,
        ClearanceIssuedById = e.ClearanceIssuedById,
        ClearanceIssuedByName = e.ClearanceIssuedBy?.FullName,
        CommencementApprovedDate = e.CommencementApprovedDate,
        CommencementApprovedById = e.CommencementApprovedById,
        CommencementApprovedByName = e.CommencementApprovedBy?.FullName,
        Notes = e.Notes,
    };

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<SheEnvironmentalReviewSummaryDto>> GetAllAsync(
        SheEnvironmentalReviewStatus? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _unitOfWork.Repository<SheEnvironmentalReview>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted);

        if (status != null) query = query.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r =>
                r.ReviewNumber.Contains(term) ||
                r.ProjectName.Contains(term) ||
                (r.ProjectReference != null && r.ProjectReference.Contains(term)));
        }

        return await query
            .Include(r => r.OrganizationUnit)
            .OrderByDescending(r => r.SubmittedDate)
            .Select(r => new SheEnvironmentalReviewSummaryDto
            {
                Id = r.Id,
                ReviewNumber = r.ReviewNumber,
                ProjectName = r.ProjectName,
                WorkClassification = r.WorkClassification,
                OrganizationUnitName = r.OrganizationUnit != null ? r.OrganizationUnit.Name : null,
                SubmittedDate = r.SubmittedDate,
                PlannedStartDate = r.PlannedStartDate,
                Status = r.Status,
                PermitRequired = r.PermitRequired,
                RequiresManagementApproval = r.RequiresManagementApproval,
                ClearanceIssued = r.ClearanceIssuedDate != null,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<SheEnvironmentalReviewDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => ReadDtoAsync(id, cancellationToken);

    // ── register writes ──────────────────────────────────────────────────────

    public async Task<SheEnvironmentalReviewDto> CreateAsync(
        CreateSheEnvironmentalReviewDto dto, Guid tenantId, Guid submittedById, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var submitter = await GetOwnedEmployeeAsync(submittedById);
        await GuardOptionalOrgUnitAsync(dto.OrganizationUnitId);
        await GuardOptionalEmployeeAsync(dto.ResponsibleManagerId);

        string reviewNumber;
        if (!string.IsNullOrWhiteSpace(dto.ReviewNumber))
        {
            reviewNumber = dto.ReviewNumber.Trim();
            var taken = await _unitOfWork.Repository<SheEnvironmentalReview>()
                .GetQueryableIncludingDeleted(r => r.TenantId == tenantId && r.ReviewNumber == reviewNumber)
                .AnyAsync(cancellationToken);
            if (taken)
                throw new InvalidOperationException($"Review number '{reviewNumber}' is already in use.");
        }
        else
        {
            var prefix = $"ECR-{DateTime.UtcNow.Year}-";
            // Numeric max including soft-deleted rows — the string-ordering re-issue trap.
            var numbers = await _unitOfWork.Repository<SheEnvironmentalReview>()
                .GetQueryableIncludingDeleted(r => r.TenantId == tenantId && r.ReviewNumber.StartsWith(prefix))
                .Select(r => r.ReviewNumber)
                .ToListAsync(cancellationToken);
            var max = numbers
                .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
                .DefaultIfEmpty(0)
                .Max();
            reviewNumber = $"{prefix}{max + 1:D4}";
        }

        var entity = new SheEnvironmentalReview
        {
            TenantId = tenantId,
            ReviewNumber = reviewNumber,
            ProjectName = dto.ProjectName.Trim(),
            WorkClassification = dto.WorkClassification,
            ProjectReference = dto.ProjectReference,
            OrganizationUnitId = dto.OrganizationUnitId,
            ResponsibleManagerId = dto.ResponsibleManagerId,
            SubmittedById = submitter.Id,
            SubmittedDate = DateTime.UtcNow,
            PlannedStartDate = dto.PlannedStartDate,
            Description = dto.Description,
            ApplicableLaws = dto.ApplicableLaws,
            PermitRequired = dto.PermitRequired,
            ComplianceChecklist = dto.ComplianceChecklist,
            Status = SheEnvironmentalReviewStatus.Submitted,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };

        await _unitOfWork.Repository<SheEnvironmentalReview>().AddAsync(entity);
        await AppendActionAsync(entity, "Submitted", $"Submitted for environmental compliance review ({entity.WorkClassification}).", submitter.Id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Environmental review submitted: {ReviewNumber}", entity.ReviewNumber);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalReviewDto> UpdateAsync(
        UpdateSheEnvironmentalReviewDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(dto.Id);
        if (!EditableStatuses.Contains(entity.Status))
            throw new InvalidOperationException("A decided environmental review cannot be edited.");

        await GuardOptionalOrgUnitAsync(dto.OrganizationUnitId);
        await GuardOptionalEmployeeAsync(dto.ResponsibleManagerId);

        entity.ProjectName = dto.ProjectName.Trim();
        entity.WorkClassification = dto.WorkClassification;
        entity.ProjectReference = dto.ProjectReference;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.ResponsibleManagerId = dto.ResponsibleManagerId;
        entity.PlannedStartDate = dto.PlannedStartDate;
        entity.Description = dto.Description;
        entity.ApplicableLaws = dto.ApplicableLaws;
        entity.PermitRequired = dto.PermitRequired;
        entity.ComplianceChecklist = dto.ComplianceChecklist;
        entity.Notes = dto.Notes;
        Touch(entity, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(id);
        if (!EditableStatuses.Contains(entity.Status))
            throw new InvalidOperationException(
                "A decided environmental review is part of the compliance archive and cannot be deleted.");

        // The trail rows go with the review — they explain a row that will no longer exist.
        var actions = await _unitOfWork.Repository<SheEnvironmentalReviewAction>()
            .GetQueryable(a => a.ReviewId == id && !a.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var action in actions)
            await _unitOfWork.Repository<SheEnvironmentalReviewAction>().DeleteAsync(action);

        await _unitOfWork.Repository<SheEnvironmentalReview>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── lifecycle (FR-ENV-004/005/009/011/016) ───────────────────────────────

    public async Task<SheEnvironmentalReviewDto> RecordScreeningAsync(
        Guid id, SheEnvironmentalScreeningDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(id);
        if (!EditableStatuses.Contains(entity.Status))
            throw new InvalidOperationException("Screening can only be recorded while the review is undecided.");

        var officer = await GetOwnedEmployeeAsync(userId);
        entity.RequiresRegistration = dto.RequiresRegistration;
        entity.RequiresEnvironmentalPermit = dto.RequiresEnvironmentalPermit;
        entity.RequiresFullEia = dto.RequiresFullEia;
        entity.RequiresRiskAssessment = dto.RequiresRiskAssessment;
        entity.RequiresEpaSubmission = dto.RequiresEpaSubmission;
        entity.RequiresManagementApproval = dto.RequiresManagementApproval;
        entity.ScreeningNotes = dto.ScreeningNotes;
        entity.ScreeningCompletedDate = DateTime.UtcNow;
        entity.ScreenedById = officer.Id;
        Touch(entity, userId);

        var determined = new List<string>();
        if (dto.RequiresRegistration) determined.Add("registration");
        if (dto.RequiresEnvironmentalPermit) determined.Add("environmental permit");
        if (dto.RequiresFullEia) determined.Add("full EIA");
        if (dto.RequiresRiskAssessment) determined.Add("risk assessment");
        if (dto.RequiresEpaSubmission) determined.Add("EPA submission");
        if (dto.RequiresManagementApproval) determined.Add("management approval");
        await AppendActionAsync(entity, "ScreeningRecorded",
            determined.Count > 0 ? $"Screening determined: {string.Join(", ", determined)}." : "Screening determined no further requirements.",
            officer.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalReviewDto> RequestCorrectionsAsync(
        Guid id, RequestSheReviewCorrectionsDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(id);
        if (entity.Status != SheEnvironmentalReviewStatus.Submitted)
            throw new InvalidOperationException("Corrections can only be requested on a submitted review.");

        var officer = await GetOwnedEmployeeAsync(userId);
        entity.Status = SheEnvironmentalReviewStatus.CorrectionsRequested;
        entity.OfficerComments = dto.Comments;
        Touch(entity, userId);
        await AppendActionAsync(entity, "CorrectionsRequested", dto.Comments, officer.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalReviewDto> ApproveAsync(
        Guid id, SheEnvironmentalReviewDecisionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(id);
        if (!EditableStatuses.Contains(entity.Status))
            throw new InvalidOperationException("Only an undecided review can be approved.");
        if (entity.ScreeningCompletedDate == null)
            throw new InvalidOperationException(
                "The screening determination must be recorded before the review can be approved.");

        var officer = await GetOwnedEmployeeAsync(userId);
        entity.Status = SheEnvironmentalReviewStatus.Approved;
        entity.ApprovedDate = DateTime.UtcNow;
        entity.ApprovedById = officer.Id;
        if (!string.IsNullOrWhiteSpace(dto.Comments)) entity.OfficerComments = dto.Comments;
        Touch(entity, userId);
        await AppendActionAsync(entity, "Approved", dto.Comments, officer.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalReviewDto> RejectAsync(
        Guid id, SheEnvironmentalReviewDecisionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(id);
        if (!EditableStatuses.Contains(entity.Status))
            throw new InvalidOperationException("Only an undecided review can be rejected.");

        var officer = await GetOwnedEmployeeAsync(userId);
        entity.Status = SheEnvironmentalReviewStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(dto.Comments)) entity.OfficerComments = dto.Comments;
        Touch(entity, userId);
        await AppendActionAsync(entity, "Rejected", dto.Comments, officer.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalReviewDto> ManagementApproveAsync(
        Guid id, SheEnvironmentalReviewDecisionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(id);
        if (entity.Status != SheEnvironmentalReviewStatus.Approved)
            throw new InvalidOperationException("Management approval follows the officer's approval.");
        if (!entity.RequiresManagementApproval)
            throw new InvalidOperationException("The screening did not route this review to management.");
        if (entity.ManagementApprovedDate != null)
            throw new InvalidOperationException("Management has already approved this review.");

        var approver = await GetOwnedEmployeeAsync(userId);
        entity.ManagementApprovedDate = DateTime.UtcNow;
        entity.ManagementApprovedById = approver.Id;
        Touch(entity, userId);
        await AppendActionAsync(entity, "ManagementApproved", dto.Comments, approver.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalReviewDto> RecordEpaSubmissionAsync(
        Guid id, RecordSheEpaSubmissionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(id);
        if (!entity.RequiresEpaSubmission)
            throw new InvalidOperationException("The screening did not determine an EPA submission for this review.");
        if (entity.Status is not (SheEnvironmentalReviewStatus.Approved or SheEnvironmentalReviewStatus.ClearanceIssued))
            throw new InvalidOperationException("An EPA submission is recorded on an approved review.");

        var officer = await GetOwnedEmployeeAsync(userId);
        entity.EpaSubmissionDate = dto.SubmissionDate;
        entity.EpaSubmissionReference = dto.ReferenceNumber;
        Touch(entity, userId);
        await AppendActionAsync(entity, "EpaSubmissionRecorded",
            string.IsNullOrWhiteSpace(dto.ReferenceNumber) ? dto.Notes : $"EPA reference {dto.ReferenceNumber}. {dto.Notes}".Trim(),
            officer.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalReviewDto> IssueClearanceAsync(
        Guid id, SheEnvironmentalReviewDecisionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(id);
        if (entity.Status == SheEnvironmentalReviewStatus.ClearanceIssued)
            throw new InvalidOperationException("Clearance has already been issued for this review.");
        if (entity.Status != SheEnvironmentalReviewStatus.Approved)
            throw new InvalidOperationException("Clearance can only be issued on an approved review.");
        if (entity.RequiresManagementApproval && entity.ManagementApprovedDate == null)
            throw new InvalidOperationException("Clearance requires management approval, which has not been given.");
        if (entity.RequiresEpaSubmission && entity.EpaSubmissionDate == null)
            throw new InvalidOperationException("Clearance requires the EPA submission to be recorded first.");

        var officer = await GetOwnedEmployeeAsync(userId);
        entity.Status = SheEnvironmentalReviewStatus.ClearanceIssued;
        entity.ClearanceIssuedDate = DateTime.UtcNow;
        entity.ClearanceIssuedById = officer.Id;
        Touch(entity, userId);
        await AppendActionAsync(entity, "ClearanceIssued", dto.Comments, officer.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Environmental clearance issued: {ReviewNumber}", entity.ReviewNumber);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalReviewDto> ApproveCommencementAsync(
        Guid id, SheEnvironmentalReviewDecisionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(id);
        if (entity.Status != SheEnvironmentalReviewStatus.ClearanceIssued)
            throw new InvalidOperationException("Commencement approval follows the issued clearance.");
        if (entity.CommencementApprovedDate != null)
            throw new InvalidOperationException("Commencement has already been approved for this review.");

        var approver = await GetOwnedEmployeeAsync(userId);
        entity.CommencementApprovedDate = DateTime.UtcNow;
        entity.CommencementApprovedById = approver.Id;
        Touch(entity, userId);
        await AppendActionAsync(entity, "CommencementApproved", dto.Comments, approver.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SheEnvironmentalClearanceReportDto> GetClearanceReportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedReviewAsync(id);
        if (entity.Status is not (SheEnvironmentalReviewStatus.Approved or SheEnvironmentalReviewStatus.ClearanceIssued))
            throw new InvalidOperationException(
                "The clearance report is available once the review has been approved.");

        return new SheEnvironmentalClearanceReportDto
        {
            Review = await ReadDtoAsync(id, cancellationToken),
            GeneratedAt = DateTime.UtcNow,
        };
    }

    private static void Touch(TenantEntity entity, Guid userId)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }
}

#endregion

#region Monthly Environmental Reports

public class SheMonthlyEnvironmentalReportService : ISheMonthlyEnvironmentalReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ISheCorrectiveActionTrackerService _correctiveActionTracker;
    private readonly IAppEventBus _appEventBus;
    private readonly ILogger<SheMonthlyEnvironmentalReportService> _logger;

    public SheMonthlyEnvironmentalReportService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ISheCorrectiveActionTrackerService correctiveActionTracker,
        IAppEventBus appEventBus,
        ILogger<SheMonthlyEnvironmentalReportService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _correctiveActionTracker = correctiveActionTracker;
        _appEventBus = appEventBus;
        _logger = logger;
    }

    private static readonly SheEnvironmentalPermitStatus[] LivePermitStatuses =
    {
        SheEnvironmentalPermitStatus.Active,
        SheEnvironmentalPermitStatus.RenewalInProgress,
        SheEnvironmentalPermitStatus.Suspended,
    };

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. User-facing reads scope to the authenticated tenant explicitly;
    // EnsureGeneratedForTenantAsync takes its tenant from the reminder engine instead.
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

    private async Task<SheMonthlyEnvironmentalReport> GetOwnedReportAsync(Guid id)
    {
        var report = await _unitOfWork.Repository<SheMonthlyEnvironmentalReport>().GetByIdAsync(id);
        if (report == null || report.TenantId != GetTenantId() || report.IsDeleted)
            throw new ArgumentException($"Monthly environmental report with ID '{id}' not found.");
        return report;
    }

    private async Task<Employee> GetOwnedEmployeeAsync(Guid id)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(id);
        if (employee == null || employee.TenantId != GetTenantId() || employee.IsDeleted)
            throw new ArgumentException($"Employee with ID '{id}' not found.");
        return employee;
    }

    private async Task<SheMonthlyEnvironmentalReportDto> ReadDtoAsync(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var entity = await _unitOfWork.Repository<SheMonthlyEnvironmentalReport>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && r.Id == id)
            .Include(r => r.GeneratedBy)
            .Include(r => r.SubmittedBy)
            .FirstOrDefaultAsync(ct);
        if (entity == null)
            throw new ArgumentException($"Monthly environmental report with ID '{id}' not found.");
        return ToDto(entity);
    }

    private static SheMonthlyEnvironmentalReportDto ToDto(SheMonthlyEnvironmentalReport e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ReportNumber = e.ReportNumber,
        Year = e.Year,
        Month = e.Month,
        PeriodStart = e.PeriodStart,
        PeriodEnd = e.PeriodEnd,
        GeneratedAt = e.GeneratedAt,
        GeneratedById = e.GeneratedById,
        GeneratedByName = e.GeneratedBy?.FullName,
        ObligationsTotal = e.ObligationsTotal,
        ObligationsCompliant = e.ObligationsCompliant,
        CompliancePercentage = e.CompliancePercentage,
        PermitsActive = e.PermitsActive,
        PermitsExpiringIn90Days = e.PermitsExpiringIn90Days,
        PermitsExpired = e.PermitsExpired,
        ProjectsReviewed = e.ProjectsReviewed,
        ClearancesIssued = e.ClearancesIssued,
        WasteGeneratedKg = e.WasteGeneratedKg,
        WasteRecycledKg = e.WasteRecycledKg,
        WasteRecyclingRate = e.WasteRecyclingRate,
        EnvironmentalIncidents = e.EnvironmentalIncidents,
        EnvironmentalIncidentsClosed = e.EnvironmentalIncidentsClosed,
        MonitoringExceedances = e.MonitoringExceedances,
        AuditFindingsRaised = e.AuditFindingsRaised,
        CorrectiveActionsOpen = e.CorrectiveActionsOpen,
        NewRegulatoryUpdates = e.NewRegulatoryUpdates,
        SustainabilityInitiativesActive = e.SustainabilityInitiativesActive,
        SustainabilityInitiativesCompleted = e.SustainabilityInitiativesCompleted,
        SustainabilityCostSavings = e.SustainabilityCostSavings,
        OfficerSummary = e.OfficerSummary,
        SubmittedToManagementAt = e.SubmittedToManagementAt,
        SubmittedById = e.SubmittedById,
        SubmittedByName = e.SubmittedBy?.FullName,
    };

    public async Task<IEnumerable<SheMonthlyEnvironmentalReportSummaryDto>> GetAllAsync(int? year = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _unitOfWork.Repository<SheMonthlyEnvironmentalReport>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted);
        if (year != null) query = query.Where(r => r.Year == year);

        return await query
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .Select(r => new SheMonthlyEnvironmentalReportSummaryDto
            {
                Id = r.Id,
                ReportNumber = r.ReportNumber,
                Year = r.Year,
                Month = r.Month,
                GeneratedAt = r.GeneratedAt,
                CompliancePercentage = r.CompliancePercentage,
                PermitsExpired = r.PermitsExpired,
                EnvironmentalIncidents = r.EnvironmentalIncidents,
                SubmittedToManagementAt = r.SubmittedToManagementAt,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<SheMonthlyEnvironmentalReportDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => ReadDtoAsync(id, cancellationToken);

    public async Task<SheMonthlyEnvironmentalReportDto> GenerateAsync(
        GenerateSheMonthlyReportDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var generator = await GetOwnedEmployeeAsync(userId);

        var periodStart = new DateTime(dto.Year, dto.Month, 1);
        if (periodStart > DateTime.UtcNow)
            throw new InvalidOperationException("A monthly environmental report cannot be generated for a future period.");

        var existing = await _unitOfWork.Repository<SheMonthlyEnvironmentalReport>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && r.Year == dto.Year && r.Month == dto.Month)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing?.SubmittedToManagementAt != null)
            throw new InvalidOperationException(
                "This period's report has been submitted to management and is retained history — it cannot be regenerated.");

        var report = existing ?? new SheMonthlyEnvironmentalReport
        {
            TenantId = tenantId,
            ReportNumber = $"ENV-RPT-{dto.Year}-{dto.Month:D2}",
            Year = dto.Year,
            Month = dto.Month,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };

        await ComputeAsync(tenantId, report, cancellationToken);
        report.GeneratedAt = DateTime.UtcNow;
        report.GeneratedById = generator.Id;
        if (existing != null)
        {
            report.UpdatedAt = DateTime.UtcNow;
            report.UpdatedBy = userId.ToString();
        }
        else
        {
            await _unitOfWork.Repository<SheMonthlyEnvironmentalReport>().AddAsync(report);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Monthly environmental report generated: {ReportNumber}", report.ReportNumber);
        return await ReadDtoAsync(report.Id, cancellationToken);
    }

    public async Task<SheMonthlyEnvironmentalReportDto> UpdateOfficerSummaryAsync(
        Guid id, UpdateSheMonthlyReportSummaryDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var report = await GetOwnedReportAsync(id);
        if (report.SubmittedToManagementAt != null)
            throw new InvalidOperationException("A submitted report is retained history and cannot be edited.");

        report.OfficerSummary = dto.OfficerSummary;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedBy = userId.ToString();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadDtoAsync(report.Id, cancellationToken);
    }

    public async Task<SheMonthlyEnvironmentalReportDto> SubmitAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var report = await GetOwnedReportAsync(id);
        if (report.SubmittedToManagementAt != null)
            throw new InvalidOperationException("This report has already been submitted to management.");

        var submitter = await GetOwnedEmployeeAsync(userId);
        report.SubmittedToManagementAt = DateTime.UtcNow;
        report.SubmittedById = submitter.Id;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedBy = userId.ToString();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // FR-ENV-034 — electronic submission to management rides the escalated
        // notification topic (seeded by the reminder engine's self-heal).
        await _appEventBus.PublishAsync(new EntityActivityEvent
        {
            TenantId = report.TenantId,
            EntityType = "SafetyCompliance",
            Activity = "EnvironmentalManagementNotice",
            Audience = "Internal",
            EntityId = report.Id,
            TriggeredByUserId = null,
            Data = new Dictionary<string, object>
            {
                ["ItemType"] = "Monthly environmental report",
                ["Reference"] = report.ReportNumber,
                ["Detail"] = $"submitted to management for {report.Year}-{report.Month:D2}",
                ["ActionPath"] = $"/hr/safety/environmental/monthly-reports/{report.Id}",
            },
        }, cancellationToken);

        return await ReadDtoAsync(report.Id, cancellationToken);
    }

    public async Task<(Guid ReportId, string ReportNumber)?> EnsureGeneratedForTenantAsync(
        Guid tenantId, int year, int month, CancellationToken cancellationToken = default)
    {
        var exists = await _unitOfWork.Repository<SheMonthlyEnvironmentalReport>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && r.Year == year && r.Month == month)
            .AnyAsync(cancellationToken);
        if (exists) return null;

        var report = new SheMonthlyEnvironmentalReport
        {
            TenantId = tenantId,
            ReportNumber = $"ENV-RPT-{year}-{month:D2}",
            Year = year,
            Month = month,
            GeneratedAt = DateTime.UtcNow,
            GeneratedById = null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System",
        };
        await ComputeAsync(tenantId, report, cancellationToken);
        await _unitOfWork.Repository<SheMonthlyEnvironmentalReport>().AddAsync(report);
        // The caller (the reminder sweep) owns the SaveChanges — the report row
        // commits atomically with the run and its dedupe keys.
        return (report.Id, report.ReportNumber);
    }

    /// <summary>All figures computed from the live registers — never hand-entered.</summary>
    private async Task ComputeAsync(Guid tenantId, SheMonthlyEnvironmentalReport report, CancellationToken ct)
    {
        var start = new DateTime(report.Year, report.Month, 1);
        var end = start.AddMonths(1);
        var today = DateTime.UtcNow.Date;
        report.PeriodStart = start;
        report.PeriodEnd = end.AddDays(-1);

        // Compliance percentage over active obligations.
        var obligations = await _unitOfWork.Repository<SheRegulatoryObligation>()
            .GetQueryable(o => o.TenantId == tenantId && !o.IsDeleted && o.IsActive)
            .Select(o => o.ComplianceStatus)
            .ToListAsync(ct);
        report.ObligationsTotal = obligations.Count;
        report.ObligationsCompliant = obligations.Count(s => s == SheComplianceStatus.Compliant);
        report.CompliancePercentage = report.ObligationsTotal > 0
            ? Math.Round(report.ObligationsCompliant * 100m / report.ObligationsTotal, 2)
            : null;

        // Permit status + upcoming renewals (FR-ENV-033 "permit status", "upcoming renewals").
        var permits = await _unitOfWork.Repository<SheEnvironmentalPermit>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted)
            .Select(p => new { p.Status, p.ExpiryDate })
            .ToListAsync(ct);
        report.PermitsActive = permits.Count(p => p.Status == SheEnvironmentalPermitStatus.Active);
        report.PermitsExpiringIn90Days = permits.Count(p =>
            LivePermitStatuses.Contains(p.Status) &&
            p.ExpiryDate.Date >= today && p.ExpiryDate.Date <= today.AddDays(90));
        report.PermitsExpired = permits.Count(p =>
            p.Status == SheEnvironmentalPermitStatus.Expired ||
            (LivePermitStatuses.Contains(p.Status) && p.ExpiryDate.Date < today));

        // Reviews submitted / clearances issued in the period.
        report.ProjectsReviewed = await _unitOfWork.Repository<SheEnvironmentalReview>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted &&
                               r.SubmittedDate >= start && r.SubmittedDate < end)
            .CountAsync(ct);
        report.ClearancesIssued = await _unitOfWork.Repository<SheEnvironmentalReview>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted &&
                               r.ClearanceIssuedDate != null &&
                               r.ClearanceIssuedDate >= start && r.ClearanceIssuedDate < end)
            .CountAsync(ct);

        // Waste, mass-based: kilogram and tonne records only (mixed units are
        // incommensurable — litres of effluent cannot be added to kilograms of
        // scrap). Diverted = recycling + composting. Same rule as the KPI engine.
        var wasteRows = await _unitOfWork.Repository<SheWasteDisposalRecord>()
            .GetQueryable(w => w.TenantId == tenantId && !w.IsDeleted &&
                               w.DisposalDate >= start && w.DisposalDate < end &&
                               (w.Unit == SheWasteMeasurementUnit.Kilograms || w.Unit == SheWasteMeasurementUnit.Tonnes))
            .Select(w => new { w.Quantity, w.Unit, w.DisposalMethod })
            .ToListAsync(ct);
        decimal Mass(decimal quantity, SheWasteMeasurementUnit unit) =>
            unit == SheWasteMeasurementUnit.Tonnes ? quantity * 1000m : quantity;
        report.WasteGeneratedKg = wasteRows.Sum(w => Mass(w.Quantity, w.Unit));
        report.WasteRecycledKg = wasteRows
            .Where(w => w.DisposalMethod is SheWasteDisposalMethod.Recycling or SheWasteDisposalMethod.Composting)
            .Sum(w => Mass(w.Quantity, w.Unit));
        report.WasteRecyclingRate = report.WasteGeneratedKg > 0
            ? Math.Round(report.WasteRecycledKg * 100m / report.WasteGeneratedKg, 2)
            : null;

        // Environmental incidents + monitoring exceedances in the period.
        report.EnvironmentalIncidents = await _unitOfWork.Repository<SheEnvironmentalIncident>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted &&
                               i.IncidentDate >= start && i.IncidentDate < end)
            .CountAsync(ct);
        report.EnvironmentalIncidentsClosed = await _unitOfWork.Repository<SheEnvironmentalIncident>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted &&
                               i.ClosedDate != null && i.ClosedDate >= start && i.ClosedDate < end)
            .CountAsync(ct);
        report.MonitoringExceedances = await _unitOfWork.Repository<SheEnvironmentalMonitoringRecord>()
            .GetQueryable(m => m.TenantId == tenantId && !m.IsDeleted &&
                               m.MeasurementDate >= start && m.MeasurementDate < end &&
                               (m.ExceedsLimit || m.ExceedsActionLevel))
            .CountAsync(ct);

        // Audit findings raised in the period; open corrective actions now
        // (from the slice-14 union tracker — all silos, one number).
        report.AuditFindingsRaised = await _unitOfWork.Repository<SheAuditFinding>()
            .GetQueryable(f => f.TenantId == tenantId && !f.IsDeleted &&
                               f.CreatedAt >= start && f.CreatedAt < end)
            .CountAsync(ct);
        report.CorrectiveActionsOpen = (await _correctiveActionTracker.GetOpenForTenantAsync(tenantId, ct)).Count;

        // New regulations recorded in the period.
        report.NewRegulatoryUpdates = await _unitOfWork.Repository<SheRegulatoryUpdate>()
            .GetQueryable(u => u.TenantId == tenantId && !u.IsDeleted &&
                               u.CreatedAt >= start && u.CreatedAt < end)
            .CountAsync(ct);

        // Sustainability performance (FR-ENV-029): initiatives active during the
        // period, completions inside it, and their estimated savings.
        var initiatives = await _unitOfWork.Repository<SheSustainabilityInitiative>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted &&
                               i.StartDate < end && (i.EndDate == null || i.EndDate >= start))
            .Select(i => new { i.Status, i.EndDate, i.EstimatedCostSavings })
            .ToListAsync(ct);
        report.SustainabilityInitiativesActive = initiatives.Count(i => i.Status == SheSustainabilityStatus.InProgress);
        report.SustainabilityInitiativesCompleted = initiatives.Count(i =>
            i.Status == SheSustainabilityStatus.Completed &&
            i.EndDate != null && i.EndDate >= start && i.EndDate < end);
        report.SustainabilityCostSavings = initiatives.Sum(i => i.EstimatedCostSavings ?? 0m);
    }
}

#endregion
