using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// JOB OFFER SERVICE
// ============================================================================

public class JobOfferService : IJobOfferService
{
    private readonly IJobOfferRepository _offerRepository;
    private readonly IJobOfferBenefitRepository _benefitRepository;
    private readonly IJobOfferNoteRepository _noteRepository;
    private readonly IApplicationPipelineService _pipelineService;
    private readonly IJobApplicationRepository _applicationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobOfferService> _logger;
    private readonly IEmailService _email;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly IFileUploadService _fileUpload;
    private readonly string _portalBaseUrl;

    public JobOfferService(
        IJobOfferRepository offerRepository,
        IJobOfferBenefitRepository benefitRepository,
        IJobOfferNoteRepository noteRepository,
        IApplicationPipelineService pipelineService,
        IJobApplicationRepository applicationRepository,
        IUnitOfWork unitOfWork,
        ILogger<JobOfferService> logger,
        IEmailService email,
        ITemplatedEmailService templatedEmail,
        IFileUploadService fileUpload,
        IConfiguration configuration)
    {
        _offerRepository         = offerRepository;
        _benefitRepository       = benefitRepository;
        _noteRepository          = noteRepository;
        _pipelineService         = pipelineService;
        _applicationRepository   = applicationRepository;
        _unitOfWork              = unitOfWork;
        _logger                  = logger;
        _email                   = email;
        _templatedEmail          = templatedEmail;
        _fileUpload              = fileUpload;
        // No localhost fallback: this URL goes into offer emails sent to real candidates. A missing
        // config value must fail at startup, not silently mail every candidate a link to localhost.
        _portalBaseUrl           = configuration["CandidatePortal:PortalUrl"]
            ?? throw new InvalidOperationException(
                "CandidatePortal:PortalUrl is not configured. It is required to build the offer-response " +
                "links emailed to candidates.");
    }

    public async Task<JobOfferDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Job offer with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<JobOfferDto?> GetByOfferNumberAsync(string offerNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByOfferNumberAsync(offerNumber);
        return entity?.ToDto();
    }

    public async Task<JobOfferDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetWithFullDetailsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Job offer with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<JobOfferDto?> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var entities = await _offerRepository.GetByApplicationIdAsync(applicationId);
        return entities.OrderByDescending(e => e.CreatedAt).FirstOrDefault()?.ToDto();
    }

    public async Task<IEnumerable<JobOfferSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _offerRepository.GetAllForSummaryAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobOfferSummaryDto>> GetByStatusAsync(JobOfferStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _offerRepository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobOfferSummaryDto>> GetExpiringOffersAsync(int daysAhead = 7, CancellationToken cancellationToken = default)
    {
        var entities = await _offerRepository.GetExpiringOffersAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    public async Task<JobOfferDto> CreateAsync(CreateJobOfferDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        // Load application with position data needed for seeding the offer
        var application = await _applicationRepository.GetForOfferSeedingAsync(createDto.JobApplicationId)
            ?? throw new ArgumentException($"Application '{createDto.JobApplicationId}' not found.");

        var vacancy  = application.JobVacancy;
        var position = vacancy?.Position;

        // Build entity from the client-supplied negotiated fields
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        // --- Auto-populate position snapshot fields (always server-authoritative) ---
        if (position != null)
        {
            entity.PositionId            = position.Id;
            entity.PositionTitle         = position.Title;
            entity.ReportsToTitle        = position.ReportsToPosition?.Title ?? string.Empty;
            entity.GradeTitle            = position.SalaryGrade?.Name ?? string.Empty;
            entity.DepartmentName        = position.OrganizationUnit?.Name ?? string.Empty;
            entity.SalaryGradeMin        = position.SalaryGrade?.MinSalary;
            entity.SalaryGradeMax        = position.SalaryGrade?.MaxSalary;
            // HR override from DTO wins; fall back to position default
            entity.ProbationPeriodMonths = createDto.ProbationPeriodMonths ?? position.ProbationPeriodMonths;
            entity.NoticePeriodMonths    = createDto.NoticePeriodMonths    ?? position.NoticePeriodMonths;
        }

        if (vacancy != null)
        {
            entity.WorkMode       = vacancy.WorkMode;
            entity.EmploymentType = vacancy.EmploymentType;
        }

        // Default weekly hours: 20 for part-time, 40 for all other employment types
        entity.WeeklyHours ??= entity.EmploymentType == EmploymentType.PartTime ? 20m : 40m;

        // --- Salary within-band validation ---
        if (entity.BaseSalary.HasValue && entity.SalaryGradeMin.HasValue && entity.SalaryGradeMax.HasValue)
        {
            if (entity.BaseSalary < entity.SalaryGradeMin || entity.BaseSalary > entity.SalaryGradeMax)
                throw new InvalidOperationException(
                    $"Proposed base salary {entity.BaseSalary:N2} is outside the salary band " +
                    $"({entity.SalaryGradeMin:N2} \u2013 {entity.SalaryGradeMax:N2}) for this position.");
        }

        entity.OfferNumber = await _offerRepository.GetNextOfferNumberAsync();
        entity.OfferStatus = JobOfferStatus.Draft;

        await _offerRepository.AddAsync(entity);

        // --- Seed JobOfferBenefit records from position benefits ---
        if (position?.PositionBenefits != null)
        {
            var order = 1;
            var seededBenefits = position.PositionBenefits
                .Where(pb => !pb.IsDeleted && pb.BenefitPolicy != null)
                .Select(pb => new JobOfferBenefit
                {
                    TenantId      = tenantId,
                    JobOfferId    = entity.Id,
                    BenefitName   = pb.BenefitPolicy.PolicyName,
                    Description   = pb.BenefitPolicy.Description,
                    MonetaryValue = pb.PositionAmount,
                    CurrencyCode  = entity.CurrencyCode,
                    IsMonetary    = pb.PositionAmount.HasValue,
                    DisplayOrder  = order++,
                    CreatedBy     = createdByUserId.ToString(),
                })
                .ToList();

            if (seededBenefits.Count > 0)
                await _benefitRepository.AddRangeAsync(seededBenefits);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job offer created: {OfferNumber}", entity.OfferNumber);
        return entity.ToDto();
    }

    public async Task<JobOfferDto> UpdateAsync(UpdateJobOfferDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Job offer with ID '{updateDto.Id}' not found.");

        if (entity.OfferStatus != JobOfferStatus.Draft && entity.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException("Only draft or pending-approval offers can be edited.");

        // Validate salary against the grade band when present on the entity being updated
        if (entity.BaseSalary.HasValue && entity.SalaryGradeMin.HasValue && entity.SalaryGradeMax.HasValue)
        {
            if (entity.BaseSalary < entity.SalaryGradeMin || entity.BaseSalary > entity.SalaryGradeMax)
                throw new InvalidOperationException(
                    $"Proposed base salary {entity.BaseSalary:N2} is outside the salary band " +
                    $"({entity.SalaryGradeMin:N2} \u2013 {entity.SalaryGradeMax:N2}) for this position.");
        }

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job offer updated: {OfferNumber}", entity.OfferNumber);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Job offer with ID '{id}' not found.");

        if (entity.OfferStatus != JobOfferStatus.Draft)
            throw new InvalidOperationException("Only draft offers can be deleted.");

        await _offerRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SubmitForApprovalAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(offerId);
        if (entity == null)
            throw new ArgumentException($"Job offer with ID '{offerId}' not found.");

        var allowedStatuses = new[] { JobOfferStatus.Draft, JobOfferStatus.Rejected };
        if (!allowedStatuses.Contains(entity.OfferStatus))
            throw new InvalidOperationException(
                $"Only Draft or Rejected offers can be submitted for approval (current: {entity.OfferStatus}).");

        entity.OfferStatus = JobOfferStatus.PendingApproval;

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApproveAsync(ApproveJobOfferDto dto, Guid approvedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(dto.OfferId);
        if (entity == null)
            throw new ArgumentException($"Job offer with ID '{dto.OfferId}' not found.");

        if (entity.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Only offers in PendingApproval can be approved (current: {entity.OfferStatus}).");

        entity.OfferStatus = JobOfferStatus.Approved;
        entity.ApprovedById = approvedByUserId;
        entity.ApprovedDate = dto.ApprovedDate;

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RejectApprovalAsync(RejectJobOfferDto dto, Guid rejectedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(dto.OfferId);
        if (entity == null)
            throw new ArgumentException($"Job offer with ID '{dto.OfferId}' not found.");

        if (entity.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Only offers in PendingApproval can be rejected (current: {entity.OfferStatus}).");

        entity.OfferStatus = JobOfferStatus.Rejected;
        entity.ApprovalRejectionReason = dto.RejectionReason;

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Offer {OfferId} approval rejected by {RejectedBy}. Reason: {Reason}",
            dto.OfferId, rejectedByUserId, dto.RejectionReason);

        return true;
    }

    public async Task<bool> IssueAsync(IssueJobOfferDto dto, Guid issuedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(dto.OfferId);
        if (entity == null)
            throw new ArgumentException($"Job offer with ID '{dto.OfferId}' not found.");

        if (entity.OfferStatus != JobOfferStatus.Approved)
            throw new InvalidOperationException("Only approved offers can be issued.");

        entity.OfferStatus = JobOfferStatus.Sent;
        entity.OfferDate = dto.OfferDate;
        if (dto.ExpiryDate.HasValue)
            entity.ExpiryDate = dto.ExpiryDate;
        if (dto.OfferLetterPath != null)
            entity.OfferLetterPath = dto.OfferLetterPath;

        // Generate a secure candidate response token
        var expiresAt = entity.ExpiryDate ?? dto.ExpiryDate ?? DateTime.UtcNow.AddDays(14);
        var candidateToken = new OfferCandidateToken
        {
            TenantId   = entity.TenantId,
            JobOfferId = entity.Id,
            Token      = Guid.NewGuid(),
            ExpiresAt  = expiresAt,
            CreatedBy  = issuedByUserId.ToString(),
        };
        var tokenRepo = _unitOfWork.Repository<OfferCandidateToken>();
        await tokenRepo.AddAsync(candidateToken);

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Advance pipeline: offer issued → Offer stage (no-op if no pipeline)
        await _pipelineService.AutoAdvanceToStageTypeAsync(
            entity.JobApplicationId, RecruitmentPipelineStageType.Offer, issuedByUserId, cancellationToken);

        // Email #11 — Offer issued (with token link)
        var fullOffer = await _offerRepository.GetWithFullDetailsAsync(entity.Id);
        if (fullOffer != null)
        {
            var candidateEmail = fullOffer.Application?.JobCandidate?.Email;
            var candidateName  = fullOffer.Application?.JobCandidate?.FullName ?? "Candidate";
            await SendOfferIssuedEmailAsync(candidateEmail ?? string.Empty, candidateName, fullOffer, candidateToken.Token);
        }

        return true;
    }

    public async Task<bool> RecordResponseAsync(RecordOfferResponseDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(dto.OfferId);
        if (entity == null)
            throw new ArgumentException($"Job offer with ID '{dto.OfferId}' not found.");

        if (entity.OfferStatus != JobOfferStatus.Sent)
            throw new InvalidOperationException("Can only record a response for a sent offer.");

        entity.OfferStatus = dto.Response;
        if (dto.Response == JobOfferStatus.Accepted) entity.AcceptedDate = DateTime.UtcNow;
        else if (dto.Response == JobOfferStatus.Declined) entity.DeclinedDate = DateTime.UtcNow;
        entity.CandidateResponseNotes = dto.CandidateResponseNotes;

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Email #13 — Hired / offer accepted
        if (dto.Response == JobOfferStatus.Accepted)
        {
            var fullOffer = await _offerRepository.GetWithFullDetailsAsync(entity.Id);
            if (fullOffer != null)
            {
                var candidateEmail = fullOffer.Application?.JobCandidate?.Email;
                var candidateName  = fullOffer.Application?.JobCandidate?.FullName ?? "Candidate";
                await SendOfferAcceptedEmailAsync(candidateEmail ?? string.Empty, candidateName, fullOffer);
            }
        }

        return true;
    }

    public async Task<bool> RevokeAsync(RevokeJobOfferDto dto, Guid revokedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(dto.OfferId);
        if (entity == null)
            throw new ArgumentException($"Job offer with ID '{dto.OfferId}' not found.");

        var terminalStatuses = new[]
        {
            JobOfferStatus.Withdrawn,
            JobOfferStatus.Declined,
            JobOfferStatus.Expired,
        };
        if (terminalStatuses.Contains(entity.OfferStatus))
            throw new InvalidOperationException($"An offer in '{entity.OfferStatus}' status cannot be revoked.");

        entity.OfferStatus = JobOfferStatus.Withdrawn;
        entity.RevokedDate = DateTime.UtcNow;
        entity.RevocationReason = dto.RevocationReason;

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<JobOfferSummaryDto>> GetByPreparedByAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _offerRepository.GetByPreparedByAsync(employeeId);
        return entities.Select(e => e.ToSummaryDto());
    }

    public async Task<JobOfferDto> AcceptConditionallyAsync(Guid offerId, Guid updatedByUserId, string? candidateResponseNotes = null, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(offerId);
        if (entity == null)
            throw new ArgumentException($"Job offer with ID '{offerId}' not found.");

        var allowedStatuses = new[] { JobOfferStatus.Sent, JobOfferStatus.Negotiating };
        if (!allowedStatuses.Contains(entity.OfferStatus))
            throw new InvalidOperationException(
                $"Only sent or negotiating offers can be conditionally accepted (current: {entity.OfferStatus}).");

        entity.OfferStatus = JobOfferStatus.ConditionallyAccepted;
        entity.IsConditional = true;
        entity.AcceptedDate = DateTime.UtcNow;
        entity.CandidateResponseNotes = candidateResponseNotes;

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Offer {OfferNumber} conditionally accepted — pre-employment checks pending.",
            entity.OfferNumber);

        return entity.ToDto();
    }

    // ── Benefits ──────────────────────────────────────────────────────────────

    public async Task<JobOfferBenefitDto> AddBenefitAsync(CreateJobOfferBenefitDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetByIdAsync(createDto.JobOfferId);
        if (offer == null)
            throw new ArgumentException($"Job offer '{createDto.JobOfferId}' not found.");

        if (offer.OfferStatus != JobOfferStatus.Draft && offer.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException("Benefits can only be added to a Draft or Pending-Approval offer.");

        var benefit = new JobOfferBenefit
        {
            TenantId = tenantId,
            JobOfferId = createDto.JobOfferId,
            BenefitName = createDto.BenefitName.Trim(),
            Description = createDto.Description,
            MonetaryValue = createDto.MonetaryValue,
            CurrencyCode = createDto.CurrencyCode?.ToUpperInvariant(),
            IsMonetary = createDto.IsMonetary,
            DisplayOrder = createDto.DisplayOrder,
            CreatedBy = createdByUserId.ToString(),
        };

        await _benefitRepository.AddAsync(benefit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return benefit.ToDto();
    }

    public async Task<IEnumerable<JobOfferBenefitDto>> GetBenefitsAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        var benefits = await _benefitRepository.GetByOfferIdAsync(offerId);
        return benefits.Select(b => b.ToDto());
    }

    public async Task<JobOfferBenefitDto> UpdateBenefitAsync(UpdateJobOfferBenefitDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var benefit = await _benefitRepository.GetByIdAsync(updateDto.Id);
        if (benefit == null)
            throw new ArgumentException($"Job offer benefit '{updateDto.Id}' not found.");

        if (updateDto.BenefitName != null)  benefit.BenefitName  = updateDto.BenefitName.Trim();
        if (updateDto.Description != null)  benefit.Description  = updateDto.Description;
        if (updateDto.MonetaryValue.HasValue) benefit.MonetaryValue = updateDto.MonetaryValue;
        if (updateDto.CurrencyCode != null)  benefit.CurrencyCode  = updateDto.CurrencyCode.ToUpperInvariant();
        if (updateDto.IsMonetary.HasValue)   benefit.IsMonetary    = updateDto.IsMonetary.Value;
        if (updateDto.DisplayOrder.HasValue) benefit.DisplayOrder  = updateDto.DisplayOrder.Value;
        benefit.UpdatedBy = updatedByUserId.ToString();
        benefit.UpdatedAt = DateTime.UtcNow;

        await _benefitRepository.UpdateAsync(benefit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return benefit.ToDto();
    }

    public async Task<bool> DeleteBenefitAsync(Guid benefitId, CancellationToken cancellationToken = default)
    {
        var benefit = await _benefitRepository.GetByIdAsync(benefitId);
        if (benefit == null)
            throw new ArgumentException($"Job offer benefit '{benefitId}' not found.");

        var offer = await _offerRepository.GetByIdAsync(benefit.JobOfferId);
        if (offer != null && offer.OfferStatus != JobOfferStatus.Draft && offer.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException("Benefits can only be removed from a Draft or Pending-Approval offer.");

        await _benefitRepository.DeleteAsync(benefit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Position-benefit integration ──────────────────────────────────────────

    public async Task<IEnumerable<JobOfferBenefitDto>> SuggestBenefitsFromPositionAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetWithFullDetailsAsync(offerId);
        if (offer == null)
            throw new ArgumentException($"Job offer '{offerId}' not found.");

        var positionBenefitList = offer.Position?.PositionBenefits ?? new List<EmployeePositionBenefit>();

        // Monetary if the policy has a numeric employer contribution or position override amount
        int order = 1;
        return positionBenefitList
            .Where(pb => pb.BenefitPolicy != null)
            .Select(pb =>
            {
                var value = pb.PositionAmount ?? pb.BenefitPolicy.EmployerContribution;
                return new JobOfferBenefitDto
                {
                    Id = Guid.Empty,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = string.Empty,
                    JobOfferId = offerId,
                    BenefitName = pb.BenefitPolicy.PolicyName,
                    Description = pb.BenefitPolicy.Description,
                    MonetaryValue = value,
                    CurrencyCode = null,
                    IsMonetary = value.HasValue && value > 0,
                    DisplayOrder = order++,
                };
            });
    }

    public async Task<IEnumerable<JobOfferBenefitDto>> ImportBenefitsFromPositionAsync(Guid offerId, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetWithFullDetailsAsync(offerId);
        if (offer == null)
            throw new ArgumentException($"Job offer '{offerId}' not found.");

        if (offer.OfferStatus != JobOfferStatus.Draft && offer.OfferStatus != JobOfferStatus.PendingApproval)
            throw new InvalidOperationException("Benefits can only be imported into a Draft or Pending-Approval offer.");

        var positionBenefits = offer.Position?.PositionBenefits ?? new List<EmployeePositionBenefit>();
        if (!positionBenefits.Any())
            return Enumerable.Empty<JobOfferBenefitDto>();

        // Fetch existing names to avoid duplicates
        var existing = await _benefitRepository.GetByOfferIdAsync(offerId);
        var existingNames = existing.Select(b => b.BenefitName.ToLowerInvariant()).ToHashSet();

        int nextOrder = existing.Any() ? existing.Max(b => b.DisplayOrder) + 1 : 1;
        var newBenefits = new List<JobOfferBenefit>();

        foreach (var pb in positionBenefits.Where(pb => pb.BenefitPolicy != null))
        {
            var name = pb.BenefitPolicy.PolicyName.Trim();
            if (existingNames.Contains(name.ToLowerInvariant()))
                continue;

            var value = pb.PositionAmount ?? pb.BenefitPolicy.EmployerContribution;
            newBenefits.Add(new JobOfferBenefit
            {
                TenantId = tenantId,
                JobOfferId = offerId,
                BenefitName = name,
                Description = pb.BenefitPolicy.Description,
                MonetaryValue = value,
                IsMonetary = value.HasValue && value > 0,
                DisplayOrder = nextOrder++,
                CreatedBy = createdByUserId.ToString(),
            });
        }

        if (newBenefits.Any())
        {
            await _benefitRepository.AddRangeAsync(newBenefits);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Imported {Count} position benefit(s) into offer {OfferId}",
            newBenefits.Count, offerId);

        return newBenefits.Select(b => b.ToDto());
    }

    // ── Notes ─────────────────────────────────────────────────────────────────

    public async Task<JobOfferNoteDto> AddNoteAsync(CreateJobOfferNoteDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var offer = await _offerRepository.GetByIdAsync(createDto.JobOfferId);
        if (offer == null)
            throw new ArgumentException($"Job offer '{createDto.JobOfferId}' not found.");

        var note = new JobOfferNote
        {
            TenantId   = tenantId,
            JobOfferId = createDto.JobOfferId,
            Body       = createDto.Body.Trim(),
            AuthorName = await ResolveAuthorNameAsync(createdByUserId),
            CreatedBy  = createdByUserId.ToString(),
        };

        await _noteRepository.AddAsync(note);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return note.ToDto();
    }

    public async Task<IEnumerable<JobOfferNoteDto>> GetNotesAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        var notes = await _noteRepository.GetByOfferIdAsync(offerId);
        return notes.Select(n => n.ToDto());
    }

    private async Task<string> ResolveAuthorNameAsync(Guid employeeId)
    {
        var emp = await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId);
        if (emp == null) return employeeId.ToString();
        return string.IsNullOrWhiteSpace(emp.MiddleName)
            ? $"{emp.FirstName} {emp.LastName}".Trim()
            : $"{emp.FirstName} {emp.MiddleName} {emp.LastName}".Trim();
    }

    // ── Offer versioning ──────────────────────────────────────────────────────

    public async Task<JobOfferDto> ReviseOfferAsync(ReviseJobOfferDto dto, Guid revisedByUserId, CancellationToken cancellationToken = default)
    {
        var original = await _offerRepository.GetWithFullDetailsAsync(dto.OriginalOfferId);
        if (original == null)
            throw new ArgumentException($"Job offer '{dto.OriginalOfferId}' not found.");

        // A revision is typically triggered after the candidate counters
        if (original.OfferStatus != JobOfferStatus.Negotiating && original.OfferStatus != JobOfferStatus.Sent)
            throw new InvalidOperationException(
                "A revised offer can only be issued against a Sent or Negotiating offer.");

        // Mark the old offer as superseded
        original.IsLatestVersion = false;
        await _offerRepository.UpdateAsync(original);

        // Clone terms, apply overrides from the dto
        var revised = new JobOffer
        {
            TenantId = original.TenantId,
            JobApplicationId = original.JobApplicationId,
            OfferStatus = JobOfferStatus.Draft,
            OfferNumber = await _offerRepository.GetNextOfferNumberAsync(),
            PositionId = original.PositionId,
            PositionTitle = original.PositionTitle,
            ReportsToTitle = original.ReportsToTitle,
            GradeTitle = original.GradeTitle,
            DepartmentName = original.DepartmentName,
            WorkMode = original.WorkMode,
            SalaryGradeMin = original.SalaryGradeMin,
            SalaryGradeMax = original.SalaryGradeMax,
            ProbationPeriodMonths = original.ProbationPeriodMonths,
            NoticePeriodMonths = original.NoticePeriodMonths,
            LocationLevelId = original.LocationLevelId,
            LocationId = original.LocationId,
            EmploymentType = original.EmploymentType,
            ContractDurationMonths = original.ContractDurationMonths,
            BaseSalary = dto.NewBaseSalary ?? original.BaseSalary,
            CurrencyCode = original.CurrencyCode,
            Bonus = original.Bonus,
            BonusTerms = original.BonusTerms,
            Commission = original.Commission,
            CommissionStructure = original.CommissionStructure,
            AdditionalTerms = dto.NewAdditionalTerms ?? original.AdditionalTerms,
            ProposedStartDate = dto.NewProposedStartDate ?? original.ProposedStartDate,
            PreparedById = revisedByUserId,
            Version = original.Version + 1,
            IsLatestVersion = true,
            PreviousOfferId = original.Id,
            CreatedBy = revisedByUserId.ToString(),
        };

        await _offerRepository.AddAsync(revised);

        // Carry the existing benefits forward to the new version
        var originalBenefits = await _benefitRepository.GetByOfferIdAsync(original.Id);
        if (originalBenefits.Any())
        {
            var copiedBenefits = originalBenefits.Select(b => new JobOfferBenefit
            {
                TenantId = b.TenantId,
                JobOfferId = revised.Id,
                BenefitName = b.BenefitName,
                Description = b.Description,
                MonetaryValue = b.MonetaryValue,
                CurrencyCode = b.CurrencyCode,
                IsMonetary = b.IsMonetary,
                DisplayOrder = b.DisplayOrder,
                CreatedBy = revisedByUserId.ToString(),
            }).ToList();
            await _benefitRepository.AddRangeAsync(copiedBenefits);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Revised offer created: {NewNumber} (v{Version}) from {OldNumber} (v{OldVersion})",
            revised.OfferNumber, revised.Version, original.OfferNumber, original.Version);

        return revised.ToDto();
    }

    // ── File uploads ──────────────────────────────────────────────────────────

    private static readonly string[] AllowedDocExtensions = [".pdf", ".doc", ".docx"];
    private const long MaxDocSizeBytes = 10 * 1024 * 1024; // 10 MB

    public async Task<string> UploadOfferLetterAsync(Guid offerId, Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(offerId);
        if (entity == null)
            throw new ArgumentException($"Job offer '{offerId}' not found.");

        if (!_fileUpload.ValidateFile(fileName, fileStream.Length, AllowedDocExtensions, MaxDocSizeBytes))
            throw new InvalidOperationException("Invalid file. Allowed types: PDF, DOC, DOCX. Maximum size: 10 MB.");

        var result = await _fileUpload.UploadFileAsync(fileStream, fileName, "offers", "letters");
        entity.OfferLetterPath = result.FilePath;
        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Offer letter uploaded for offer {OfferNumber}: {Path}", entity.OfferNumber, result.FilePath);
        return _fileUpload.GetFileUrl(result.FilePath);
    }

    public async Task<string> UploadSignedLetterAsync(Guid offerId, Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var entity = await _offerRepository.GetByIdAsync(offerId);
        if (entity == null)
            throw new ArgumentException($"Job offer '{offerId}' not found.");

        if (!_fileUpload.ValidateFile(fileName, fileStream.Length, AllowedDocExtensions, MaxDocSizeBytes))
            throw new InvalidOperationException("Invalid file. Allowed types: PDF, DOC, DOCX. Maximum size: 10 MB.");

        var result = await _fileUpload.UploadFileAsync(fileStream, fileName, "offers", "signed");
        entity.SignedOfferLetterPath = result.FilePath;
        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Signed offer letter uploaded for offer {OfferNumber}: {Path}", entity.OfferNumber, result.FilePath);
        return _fileUpload.GetFileUrl(result.FilePath);
    }

    // ── Public candidate response (token-based) ───────────────────────────────

    public async Task<CandidateOfferSummaryDto> ValidateCandidateTokenAsync(Guid token, CancellationToken cancellationToken = default)
    {
        var tokenRepo = _unitOfWork.Repository<OfferCandidateToken>();
        var tokenEntity = await tokenRepo.FirstOrDefaultAsync(t => t.Token == token);

        if (tokenEntity == null)
            return new CandidateOfferSummaryDto { TokenExpired = true };

        if (tokenEntity.IsUsed)
            return new CandidateOfferSummaryDto { TokenAlreadyUsed = true };

        if (tokenEntity.ExpiresAt < DateTime.UtcNow)
            return new CandidateOfferSummaryDto { TokenExpired = true };

        var offer = await _offerRepository.GetWithFullDetailsAsync(tokenEntity.JobOfferId);
        if (offer == null)
            return new CandidateOfferSummaryDto { TokenExpired = true };

        return new CandidateOfferSummaryDto
        {
            OfferId         = offer.Id,
            OfferNumber     = offer.OfferNumber,
            PositionTitle   = offer.PositionTitle,
            BaseSalary      = offer.BaseSalary.HasValue ? $"{offer.CurrencyCode} {offer.BaseSalary.Value:N2} per annum" : null,
            StartDate       = offer.ProposedStartDate?.ToString("dddd, d MMMM yyyy"),
            ExpiryDate      = offer.ExpiryDate?.ToString("dddd, d MMMM yyyy"),
            AdditionalTerms = offer.AdditionalTerms,
            IsConditional   = offer.IsConditional,
            OfferLetterUrl  = offer.OfferLetterPath,
        };
    }

    public async Task<bool> RecordCandidateResponseAsync(CandidateResponseDto dto, CancellationToken cancellationToken = default)
    {
        var allowedResponses = new[] { JobOfferStatus.Accepted, JobOfferStatus.Negotiating, JobOfferStatus.Declined };
        if (!allowedResponses.Contains(dto.Response))
            throw new ArgumentException("Response must be Accepted, Negotiating, or Declined.");

        var tokenRepo = _unitOfWork.Repository<OfferCandidateToken>();
        var tokenEntity = await tokenRepo.FirstOrDefaultAsync(t => t.Token == dto.Token);

        if (tokenEntity == null || tokenEntity.IsUsed || tokenEntity.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("The offer response link is invalid or has expired.");

        var entity = await _offerRepository.GetByIdAsync(tokenEntity.JobOfferId);
        if (entity == null)
            throw new ArgumentException("Associated offer not found.");

        if (entity.OfferStatus != JobOfferStatus.Sent && entity.OfferStatus != JobOfferStatus.ConditionallyAccepted)
            throw new InvalidOperationException("This offer is no longer awaiting a response.");

        entity.OfferStatus = dto.Response;
        entity.CandidateResponseNotes = dto.Notes;
        if (dto.Response == JobOfferStatus.Accepted || dto.Response == JobOfferStatus.ConditionallyAccepted)
            entity.AcceptedDate = DateTime.UtcNow;
        else if (dto.Response == JobOfferStatus.Declined)
        {
            entity.DeclinedDate = DateTime.UtcNow;
            entity.DeclineReason = dto.DeclineReason;
        }

        tokenEntity.IsUsed = true;
        tokenEntity.UsedAt = DateTime.UtcNow;

        await _offerRepository.UpdateAsync(entity);
        await tokenRepo.UpdateAsync(tokenEntity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Candidate responded to offer {OfferNumber} via token: {Response}",
            entity.OfferNumber, dto.Response);

        return true;
    }

    public async Task<bool> RecordPortalCandidateResponseAsync(Guid applicationId, CandidatePortalOfferResponseDto dto, CancellationToken cancellationToken = default)
    {
        var allowedResponses = new[] { JobOfferStatus.Accepted, JobOfferStatus.Negotiating, JobOfferStatus.Declined };
        if (!allowedResponses.Contains(dto.Response))
            throw new ArgumentException("Response must be Accepted, Negotiating, or Declined.");

        var entities = await _offerRepository.GetByApplicationIdAsync(applicationId);
        var entity = entities
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefault(e => e.OfferStatus == JobOfferStatus.Sent || e.OfferStatus == JobOfferStatus.Negotiating);

        if (entity == null)
            throw new InvalidOperationException("No active offer found for this application.");

        entity.OfferStatus = dto.Response;
        entity.CandidateResponseNotes = dto.Notes;

        if (dto.Response is JobOfferStatus.Accepted or JobOfferStatus.ConditionallyAccepted)
            entity.AcceptedDate = DateTime.UtcNow;
        else if (dto.Response == JobOfferStatus.Declined)
        {
            entity.DeclinedDate = DateTime.UtcNow;
            entity.DeclineReason = dto.DeclineReason;
        }

        await _offerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Candidate responded to offer {OfferNumber} via portal: {Response}",
            entity.OfferNumber, dto.Response);

        return true;
    }

    private async Task SendOfferIssuedEmailAsync(string toEmail, string candidateName, JobOffer offer, Guid? candidateToken = null)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"] = candidateName,
            ["PositionTitle"] = offer.PositionTitle,
            ["OfferNumber"]   = offer.OfferNumber,
            ["SalaryLine"]    = offer.BaseSalary.HasValue ? $"{offer.CurrencyCode} {offer.BaseSalary:N2} per annum" : null,
            ["StartDate"]     = offer.ProposedStartDate?.ToString("dddd, d MMMM yyyy"),
            ["ExpiryDate"]    = offer.ExpiryDate?.ToString("dddd, d MMMM yyyy"),
            // Point at the header-free, token-based offer-response flow (api/offer-response, backed by
            // the globally-unique OfferCandidateToken) so a recipient can respond straight from the
            // email without logging in / supplying a tenant. Falls back to the login deep-link only if
            // no token was issued.
            ["RespondUrl"]    = candidateToken.HasValue
                ? $"{_portalBaseUrl}/careers/portal/offer-response?token={candidateToken.Value}"
                : $"{_portalBaseUrl}/careers/portal/login?returnUrl=/careers/portal/offer/{offer.JobApplicationId}",
        };

        // Best-effort: the offer is already committed when this runs, so a mail failure must not turn a
        // successful issue into an error response.
        try
        {
            await _templatedEmail.SendAsync(
                RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.OfferIssued, toEmail, tokens);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send offer issued email to {Email} — offer {OfferNumber} was issued successfully.",
                toEmail, offer.OfferNumber);
        }
    }

    private async Task SendOfferAcceptedEmailAsync(string toEmail, string candidateName, JobOffer offer)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"] = candidateName,
            ["PositionTitle"] = offer.PositionTitle,
            ["OfferNumber"]   = offer.OfferNumber,
            ["StartDate"]     = offer.ProposedStartDate?.ToString("dddd, d MMMM yyyy"),
        };

        // Best-effort with a timeout race: this is reachable from the header-free, anonymous
        // offer-response token flow, where the candidate's acceptance is already committed and an
        // unresponsive SMTP server would otherwise hold their response open.
        try
        {
            var emailTask = _templatedEmail.SendAsync(
                RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.OfferAccepted, toEmail, tokens);

            if (await Task.WhenAny(emailTask, Task.Delay(TimeSpan.FromSeconds(10))) == emailTask)
                await emailTask;
            else
                _logger.LogWarning(
                    "Offer accepted email timed out after 10 s for {Email} — offer {OfferNumber} was accepted successfully.",
                    toEmail, offer.OfferNumber);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send offer accepted email to {Email} — offer {OfferNumber} was accepted successfully.",
                toEmail, offer.OfferNumber);
        }
    }
}

// ============================================================================
// JOB HIRE SERVICE
// ============================================================================

public class JobHireService : IJobHireService
{
    private readonly IJobHireRecordRepository _hireRepository;
    private readonly IJobOfferRepository _offerRepository;
    private readonly IApplicationPipelineService _pipelineService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IEmployeeContractDetailRepository _contractDetailRepository;
    private readonly IProbationPeriodRepository _probationRepository;
    private readonly IEmployeePositionHistoryRepository _positionHistoryRepository;
    private readonly IEmployeeSalaryAssignmentRepository _salaryAssignmentRepository;
    private readonly IEmployeeQualificationRepository _qualificationRepository;
    private readonly IEmployeeWorkHistoryRepository _workHistoryRepository;
    private readonly IEmployeeRefereeRepository _refereeRepository;
    private readonly IEmployeeSkillRepository _skillRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobHireService> _logger;

    public JobHireService(
        IJobHireRecordRepository hireRepository,
        IJobOfferRepository offerRepository,
        IApplicationPipelineService pipelineService,
        IEmployeeRepository employeeRepository,
        IEmployeeContractDetailRepository contractDetailRepository,
        IProbationPeriodRepository probationRepository,
        IEmployeePositionHistoryRepository positionHistoryRepository,
        IEmployeeSalaryAssignmentRepository salaryAssignmentRepository,
        IEmployeeQualificationRepository qualificationRepository,
        IEmployeeWorkHistoryRepository workHistoryRepository,
        IEmployeeRefereeRepository refereeRepository,
        IEmployeeSkillRepository skillRepository,
        IUnitOfWork unitOfWork,
        ILogger<JobHireService> logger)
    {
        _hireRepository           = hireRepository;
        _offerRepository          = offerRepository;
        _pipelineService          = pipelineService;
        _employeeRepository       = employeeRepository;
        _contractDetailRepository = contractDetailRepository;
        _probationRepository      = probationRepository;
        _positionHistoryRepository  = positionHistoryRepository;
        _salaryAssignmentRepository = salaryAssignmentRepository;
        _qualificationRepository    = qualificationRepository;
        _workHistoryRepository      = workHistoryRepository;
        _refereeRepository          = refereeRepository;
        _skillRepository            = skillRepository;
        _unitOfWork               = unitOfWork;
        _logger                   = logger;
    }

    public async Task<JobHireRecordDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _hireRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Hire record with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<JobHireRecordDto?> GetByHireNumberAsync(string hireNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _hireRepository.GetByHireNumberAsync(hireNumber);
        return entity?.ToDto();
    }

    public async Task<JobHireRecordDto?> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var entity = await _hireRepository.GetByApplicationIdAsync(applicationId);
        return entity?.ToDto();
    }

    public async Task<JobHireRecordDto?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await _hireRepository.GetByEmployeeIdAsync(employeeId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<JobHireRecordSummaryDto>> GetByStatusAsync(JobHireStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _hireRepository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobHireRecordSummaryDto>> GetWithStartDateApproachingAsync(int daysAhead = 14, CancellationToken cancellationToken = default)
    {
        var entities = await _hireRepository.GetWithStartDateApproachingAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    public async Task<JobHireRecordDto> CreateAsync(CreateJobHireRecordDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        // Guard: for conditional offers, checks must have cleared before a hire record can be created
        var offer = await _offerRepository.GetByIdAsync(createDto.OfferId);
        if (offer != null && offer.IsConditional
            && offer.OfferStatus != JobOfferStatus.ChecksCleared
            && offer.OfferStatus != JobOfferStatus.Accepted)
        {
            throw new InvalidOperationException(
                "This offer is conditional. Pre-employment checks must be completed and cleared before a hire record can be created.");
        }

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.HireNumber = await _hireRepository.GetNextHireNumberAsync();
        entity.Status = JobHireStatus.PendingOnboarding;

        await _hireRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Hire record created: {HireNumber}", entity.HireNumber);

        // Advance pipeline: hire record created → Hired stage (no-op if no pipeline)
        await _pipelineService.AutoAdvanceToStageTypeAsync(
            entity.ApplicationId, RecruitmentPipelineStageType.Hired, createdByUserId, cancellationToken);

        return entity.ToDto();
    }

    public async Task<JobHireRecordDto> UpdateStatusAsync(UpdateJobHireRecordStatusDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _hireRepository.GetByIdAsync(updateDto.HireRecordId);
        if (entity == null)
            throw new ArgumentException($"Hire record with ID '{updateDto.HireRecordId}' not found.");

        entity.Status = updateDto.NewStatus;
        entity.Notes = updateDto.Notes;

        await _hireRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> ConfirmStartAsync(Guid hireRecordId, DateTime actualStartDate, Guid? linkedEmployeeId, Guid confirmedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _hireRepository.GetForConfirmStartAsync(hireRecordId);
        if (entity == null)
            throw new ArgumentException($"Hire record with ID '{hireRecordId}' not found.");

        // Idempotency guard. This method creates an Employee plus a contract, probation period, salary
        // assignment, position history and the candidate's qualifications/work history/referees/skills.
        // Without this check a double-click (or any retried request) would create a SECOND employee and
        // a second set of all of it, and burn another employee number.
        if (entity.EmployeeId.HasValue || entity.Status == JobHireStatus.Active)
            throw new InvalidOperationException(
                $"Hire '{entity.HireNumber}' has already been confirmed and is linked to an employee record.");

        entity.ActualStartDate = DateOnly.FromDateTime(actualStartDate);
        entity.ConfirmedById   = confirmedByUserId;
        entity.ConfirmedDate   = DateTime.UtcNow;
        entity.Status          = JobHireStatus.Active;

        if (linkedEmployeeId.HasValue)
        {
            // Internal hire or manual link — use the existing employee record
            entity.EmployeeId = linkedEmployeeId.Value;
        }
        else
        {
            // External hire — auto-create employee record and all associated profile data

            var offer     = entity.Offer;
            var candidate = entity.Offer?.Application?.JobCandidate
                         ?? entity.Application?.JobCandidate;

            if (offer == null || candidate == null)
                throw new InvalidOperationException(
                    "Cannot auto-create employee: hire record is missing offer or candidate data.");

            // 1 — Employee
            var empNumber = await _employeeRepository.GenerateEmployeeNumberAsync();
            var probDays  = offer.ProbationPeriodMonths.HasValue ? offer.ProbationPeriodMonths.Value * 30 : 90;
            var employee  = new Employee
            {
                TenantId            = entity.TenantId,
                CreatedById         = confirmedByUserId,
                HireRecordId        = entity.Id,
                EmployeeNumber      = empNumber,
                FirstName           = candidate.FirstName,
                MiddleName          = candidate.MiddleName,
                LastName            = candidate.LastName,
                EmailAddress        = candidate.Email,
                MobileNumber        = candidate.Phone,
                Gender              = candidate.Gender,
                DateOfBirth         = DateOnly.FromDateTime(candidate.DateOfBirth),
                PositionId          = offer.PositionId,
                OrganizationLevelId = offer.Position?.OrganizationLevelId,
                OrganizationUnitId  = offer.Position?.OrganizationUnitId,
                EmploymentType      = offer.EmploymentType,
                IsFullTime          = offer.EmploymentType is EmploymentType.Permanent
                                                           or EmploymentType.Contract
                                                           or EmploymentType.FixedTerm,
                DateEmployed        = DateOnly.FromDateTime(actualStartDate),
                LocationId          = offer.LocationId,
                LocationLevelId     = offer.LocationLevelId,
                Salary              = offer.BaseSalary,
                StaffStatus         = offer.ProbationPeriodMonths is > 0
                                          ? StaffStatus.Probation
                                          : StaffStatus.Active,
                IsActive            = true,
                ProbationPeriodDays = probDays,
            };
            await _employeeRepository.AddAsync(employee);
            entity.EmployeeId = employee.Id;

            // 2 — EmployeeContractDetail
            var startDate       = DateOnly.FromDateTime(actualStartDate);
            var contractEndDate = offer.ContractDurationMonths.HasValue
                ? startDate.AddMonths(offer.ContractDurationMonths.Value)
                : (DateOnly?)null;
            var contractDetail = new EmployeeContractDetail
            {
                TenantId            = entity.TenantId,
                CreatedById         = confirmedByUserId,
                EmployeeId          = employee.Id,
                ContractNumber      = $"CTR-{empNumber}",
                EmploymentType      = offer.EmploymentType,
                StartDate           = startDate,
                EffectiveDate       = startDate,
                EndDate             = contractEndDate,
                ContractEndDate     = contractEndDate,
                Salary              = offer.BaseSalary ?? 0m,
                CurrencyCode        = offer.CurrencyCode ?? "GHS",
                ProbationPeriodDays = probDays,
                WorkingHoursPerWeek = offer.WeeklyHours.HasValue ? (int)offer.WeeklyHours.Value : 40,
                WorkSchedule        = offer.EmploymentType == EmploymentType.PartTime
                                          ? WorkArrangementType.PartTime
                                          : WorkArrangementType.FullTime,
                IsCurrent           = true,
                IsActive            = true,
                ContractStatus      = ContractStatus.Active,
            };
            await _contractDetailRepository.AddAsync(contractDetail);

            // 3 — ProbationPeriod (only when offer specifies a duration)
            if (offer.ProbationPeriodMonths is > 0)
            {
                var probEnd = startDate.AddMonths(offer.ProbationPeriodMonths.Value);
                var probation = new ProbationPeriod
                {
                    TenantId         = entity.TenantId,
                    CreatedById      = confirmedByUserId,
                    EmployeeId       = employee.Id,
                    ContractDetailId = contractDetail.Id,
                    StartDate        = startDate,
                    OriginalEndDate  = probEnd,
                    CurrentEndDate   = probEnd,
                    DurationMonths   = offer.ProbationPeriodMonths.Value,
                    Status           = ProbationStatus.Active,
                };
                await _probationRepository.AddAsync(probation);
            }

            // 4 — EmployeePositionHistory (initial assignment)
            // OrganizationLevelId is guaranteed non-null because EmployeePosition always requires it.
            // LocationLevelId is nullable; skip only if Position itself is missing.
            if (offer.Position != null)
            {
                var posHistory = new EmployeePositionHistory
                {
                    TenantId            = entity.TenantId,
                    CreatedById         = confirmedByUserId,
                    EmployeeId          = employee.Id,
                    PositionId          = offer.PositionId,
                    OrganizationLevelId = offer.Position.OrganizationLevelId,
                    OrganizationUnitId  = offer.Position.OrganizationUnitId,
                    LocationLevelId     = offer.LocationLevelId,
                    LocationId          = offer.LocationId,
                    StartDate           = actualStartDate,
                    ChangeReason        = PositionChangeReason.InitialAssignment,
                };
                await _positionHistoryRepository.AddAsync(posHistory);
            }

            // 5 — EmployeeSalaryAssignment
            // Prefer the grade linked to the offer's salary level; fall back to the position's default grade.
            var gradeId = offer.SalaryLevel?.SalaryGradeId ?? offer.Position?.SalaryGradeId;
            if (gradeId.HasValue)
            {
                var salaryAssignment = new EmployeeSalaryAssignment
                {
                    TenantId         = entity.TenantId,
                    CreatedById      = confirmedByUserId,
                    EmployeeId       = employee.Id,
                    GradeId          = gradeId.Value,
                    LevelId          = offer.SalaryLevelId,
                    NotchId          = offer.SalaryNotchId,
                    EffectiveDate    = actualStartDate,
                    AssignmentReason = "Initial hire assignment",
                };
                await _salaryAssignmentRepository.AddAsync(salaryAssignment);
            }

            // 6 — EmployeeQualifications (from live candidate profile)
            foreach (var cq in candidate.Qualifications)
            {
                var eq = new EmployeeQualification
                {
                    TenantId                = entity.TenantId,
                    CreatedById             = confirmedByUserId,
                    EmployeeId              = employee.Id,
                    QualificationId         = cq.QualificationId,
                    CustomQualificationName = cq.QualificationId == null ? cq.QualificationFreeText : null,
                    Institution             = cq.Institution,
                    CompletionDate          = cq.DateAwarded,
                    Grade                   = cq.Grade,
                    IsVerified              = false,
                };
                await _qualificationRepository.AddAsync(eq);
            }

            // 7 — EmployeeWorkHistory (from live candidate profile)
            foreach (var wh in candidate.WorkHistories)
            {
                var ewh = new EmployeeWorkHistory
                {
                    TenantId         = entity.TenantId,
                    CreatedById      = confirmedByUserId,
                    EmployeeId       = employee.Id,
                    CompanyName      = wh.InstitutionName,
                    JobTitle         = wh.PositionHeld,
                    StartDate        = wh.StartDate,
                    EndDate          = wh.EndDate,
                    JobDescription   = wh.Responsibilities,
                    ReasonForLeaving = wh.ReasonForLeaving,
                    CanContact       = false,
                };
                await _workHistoryRepository.AddAsync(ewh);
            }

            // 8 — EmployeeReferees (from live candidate profile; not in snapshot)
            foreach (var cr in candidate.Referees)
            {
                var er = new EmployeeReferee
                {
                    TenantId        = entity.TenantId,
                    CreatedById     = confirmedByUserId,
                    EmployeeId      = employee.Id,
                    FullName        = cr.FullName,
                    PositionOrTitle = cr.Position,
                    Organization    = cr.Organization,
                    PhoneNumber     = cr.Phone,
                    EmailAddress    = cr.Email,
                    Relationship    = cr.Relationship,
                    RefereeType     = RefereeType.Professional,
                    IsActive        = true,
                    IsContacted     = false,
                    IsPrimary       = false,
                };
                await _refereeRepository.AddAsync(er);
            }

            // 9 — EmployeeSkills (only catalogue-linked skills; free-text skills have no SkillId FK)
            foreach (var cs in candidate.Skills.Where(s => s.SkillId.HasValue))
            {
                var es = new EmployeeSkill
                {
                    TenantId      = entity.TenantId,
                    CreatedById   = confirmedByUserId,
                    EmployeeId    = employee.Id,
                    SkillId       = cs.SkillId!.Value,
                    SkillLevel    = MapCandidateProficiency(cs.Proficiency),
                    IsCertified   = cs.IsCertified,
                    CertifyingBody = cs.CertificationName,
                    IsVerified    = false,
                };
                await _skillRepository.AddAsync(es);
            }
        }

        // Keep the originating StaffRequisition's fill counter in step with reality. Confirming a
        // hire fills one seat of the requisition that opened the vacancy; without this the
        // requisition's PositionsFilled only ever advances via a separate manual /fulfill call,
        // so requisition progress silently drifts from the number of people actually hired.
        await IncrementRequisitionFillAsync(entity, cancellationToken);

        await _hireRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Hire confirmed: {HireNumber}, Employee: {EmployeeId}",
            entity.HireNumber, entity.EmployeeId);

        return true;
    }

    /// <summary>
    /// Advances the fill counter on the StaffRequisition that opened the vacancy this hire came
    /// through (hire → offer/application → JobVacancy → Requisition). Increments PositionsFilled
    /// by one (capped at NumberOfPositions) and, only from an in-progress state, promotes the
    /// requisition to PartiallyFulfilled / Fulfilled. Never over-counts, never resurrects a
    /// cancelled/on-hold requisition, and never downgrades one already Fulfilled.
    /// </summary>
    private async Task IncrementRequisitionFillAsync(JobHireRecord hire, CancellationToken cancellationToken)
    {
        var application = hire.Offer?.Application ?? hire.Application;
        if (application is null)
            return;

        var vacancy = await _unitOfWork.Repository<JobVacancy>().GetByIdAsync(application.JobVacancyId);
        if (vacancy is null)
            return;

        var requisition = await _unitOfWork.Repository<StaffRequisition>().GetByIdAsync(vacancy.StaffRequisitionId);
        if (requisition is null)
            return;

        // Already counted for everyone requested — don't over-count on re-hires against the same req.
        if (requisition.PositionsFilled >= requisition.NumberOfPositions)
            return;

        requisition.PositionsFilled++;

        var fullyFilled = requisition.PositionsFilled >= requisition.NumberOfPositions;
        if (requisition.Status is StaffRequisitionStatus.Approved or StaffRequisitionStatus.PartiallyFulfilled)
        {
            requisition.Status = fullyFilled ? StaffRequisitionStatus.Fulfilled : StaffRequisitionStatus.PartiallyFulfilled;
            requisition.IsFulfilled = fullyFilled;
            if (fullyFilled)
                requisition.FulfilledDate = DateTime.UtcNow;
        }

        await _unitOfWork.Repository<StaffRequisition>().UpdateAsync(requisition);

        _logger.LogInformation(
            "Requisition {RequisitionNumber} fill advanced to {Filled}/{Total} by hire {HireNumber}.",
            requisition.RequisitionNumber, requisition.PositionsFilled, requisition.NumberOfPositions, hire.HireNumber);
    }

    private static SkillLevel MapCandidateProficiency(ProficiencyLevel? proficiency) => proficiency switch
    {
        ProficiencyLevel.Proficient                             => SkillLevel.Intermediate,
        ProficiencyLevel.Advanced or ProficiencyLevel.Expert   => SkillLevel.Advanced,
        _                                                       => SkillLevel.Beginner,
    };
}
