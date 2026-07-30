using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Candidate self-service portal — profile management and job applications.
/// </summary>
public sealed class CandidatePortalService : ICandidatePortalService
{
    private readonly IGenericRepository<CandidatePortalAccount> _accountRepo;
    private readonly IJobCandidateRepository _candidateRepo;
    private readonly IGenericRepository<JobCandidateWorkHistory> _workHistoryRepo;
    private readonly IGenericRepository<JobCandidateQualification> _qualificationRepo;
    private readonly IGenericRepository<JobCandidateReferee> _refereeRepo;
    private readonly IGenericRepository<JobCandidateSkill> _skillRepo;
    private readonly IGenericRepository<JobCandidateLanguage> _languageRepo;
    private readonly IGenericRepository<JobCandidateInterest> _interestRepo;
    private readonly IGenericRepository<JobCandidateDocument> _documentRepo;
    private readonly IJobApplicationRepository _applicationRepo;
    private readonly IJobVacancyRepository _vacancyRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<CandidatePortalService> _logger;
    private readonly IEmailService _email;
    private readonly ITemplatedEmailService _templatedEmail;
    private readonly IApplicationSnapshotService _snapshotService;
    private readonly IApplicationPipelineService _pipelineService;

    public CandidatePortalService(
        IGenericRepository<CandidatePortalAccount> accountRepo,
        IJobCandidateRepository candidateRepo,
        IGenericRepository<JobCandidateWorkHistory> workHistoryRepo,
        IGenericRepository<JobCandidateQualification> qualificationRepo,
        IGenericRepository<JobCandidateReferee> refereeRepo,
        IGenericRepository<JobCandidateSkill> skillRepo,
        IGenericRepository<JobCandidateLanguage> languageRepo,
        IGenericRepository<JobCandidateInterest> interestRepo,
        IGenericRepository<JobCandidateDocument> documentRepo,
        IJobApplicationRepository applicationRepo,
        IJobVacancyRepository vacancyRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<CandidatePortalService> logger,
        IEmailService email,
        ITemplatedEmailService templatedEmail,
        IApplicationSnapshotService snapshotService,
        IApplicationPipelineService pipelineService)
    {
        _accountRepo      = accountRepo;
        _candidateRepo    = candidateRepo;
        _workHistoryRepo  = workHistoryRepo;
        _qualificationRepo = qualificationRepo;
        _refereeRepo      = refereeRepo;
        _skillRepo        = skillRepo;
        _languageRepo     = languageRepo;
        _interestRepo     = interestRepo;
        _documentRepo     = documentRepo;
        _applicationRepo  = applicationRepo;
        _vacancyRepo      = vacancyRepo;
        _unitOfWork       = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger           = logger;
        _snapshotService  = snapshotService;
        _pipelineService  = pipelineService;
        _email            = email;
        _templatedEmail   = templatedEmail;
    }

    // Portal callers supply tenantId via header/JWT. When ICurrentUserProvider.TenantId is set
    // (non-empty), it must match; when empty (anonymous register/login), trust the explicit tenantId.
    private Guid RequireCurrentTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));
        var current = _currentUserProvider.TenantId;
        if (current != Guid.Empty && current != tenantId)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return tenantId;
    }

    private async Task<CandidatePortalAccount> GetOwnedAccountAsync(Guid accountId, Guid tenantId)
    {
        var account = await _accountRepo.FirstOrDefaultAsync(
            a => a.Id == accountId && a.TenantId == tenantId);
        if (account == null)
            throw new InvalidOperationException("Account not found.");
        return account;
    }

    private async Task<JobApplication> GetOwnedApplicationAsync(
        Guid applicationId, Guid jobCandidateId, Guid tenantId)
    {
        var application = await _applicationRepo.FirstOrDefaultAsync(
            a => a.Id == applicationId
              && a.TenantId == tenantId
              && a.JobCandidateId == jobCandidateId);
        if (application == null)
            throw new InvalidOperationException("Application not found.");
        return application;
    }

    private async Task<JobVacancy> GetTenantVacancyAsync(Guid vacancyId, Guid tenantId)
    {
        var vacancy = await _vacancyRepo.GetByIdAsync(vacancyId);
        if (vacancy == null || vacancy.TenantId != tenantId)
            throw new InvalidOperationException("Job vacancy not found.");
        return vacancy;
    }

    // ── Get Profile ────────────────────────────────────────────────────────────
    public async Task<CandidatePortalProfileDto> GetProfileAsync(
        Guid accountId, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetOwnedAccountAsync(accountId, tenantId);

        var dto = new CandidatePortalProfileDto
        {
            AccountId       = account.Id,
            CandidateId     = account.JobCandidateId,
            Email           = account.Email,
            IsEmailVerified = account.IsEmailVerified,
        };

        if (account.JobCandidateId.HasValue)
        {
            var candidate = await _candidateRepo.GetWithFullDetailsAsync(account.JobCandidateId.Value);
            if (candidate != null && candidate.TenantId == tenantId)
                PopulateCandidateFields(dto, candidate);
        }

        return dto;
    }

    // ── Save Profile ───────────────────────────────────────────────────────────
    public async Task<CandidatePortalProfileDto> SaveProfileAsync(
        Guid accountId,
        UpdateCandidatePortalProfileDto dto,
        Guid tenantId,
        CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetOwnedAccountAsync(accountId, tenantId);

        JobCandidate candidate;

        if (!account.JobCandidateId.HasValue)
        {
            // Create a new candidate record and link it
            candidate = new JobCandidate
            {
                TenantId        = tenantId,
                CandidateNumber = await _candidateRepo.GetNextCandidateNumberAsync(),
                Email           = account.Email,
            };
            MapDtoToCandidate(dto, candidate);
            await _candidateRepo.AddAsync(candidate);
            await _unitOfWork.SaveChangesAsync(ct);

            account.JobCandidateId = candidate.Id;
            await _accountRepo.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        else
        {
            // Load only the root candidate row — no nav props — to avoid EF tracking
            // child collections during the scalar update, which would otherwise cause
            // duplicate inserts when we patch the collections below.
            candidate = await _candidateRepo.GetByIdAsync(account.JobCandidateId.Value);
            if (candidate == null || candidate.TenantId != tenantId)
                throw new InvalidOperationException("Candidate record not found.");
            MapDtoToCandidate(dto, candidate);
            await _candidateRepo.UpdateAsync(candidate);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        // ── Diff-and-patch child collections ─────────────────────────────────
        // For each collection we:
        //   1. Load the current active rows from the DB (FindAsync filters IsDeleted).
        //   2. Soft-delete rows whose Id is absent from the incoming list.
        //   3. Update rows whose Id matches an incoming item.
        //   4. Insert incoming items with Id == Guid.Empty as new rows.

        // Work histories
        {
            var existing = (await _workHistoryRepo.FindAsync(w => w.JobCandidateId == candidate.Id && w.TenantId == tenantId))
                           .ToDictionary(w => w.Id);
            var incomingIds = dto.WorkHistories.Where(w => w.Id != Guid.Empty).Select(w => w.Id).ToHashSet();

            var toDelete = existing.Values.Where(w => !incomingIds.Contains(w.Id)).ToList();
            if (toDelete.Count > 0) await _workHistoryRepo.DeleteRangeAsync(toDelete);

            foreach (var w in dto.WorkHistories)
            {
                if (w.Id == Guid.Empty)
                {
                    await _workHistoryRepo.AddAsync(new JobCandidateWorkHistory
                    {
                        TenantId         = tenantId,
                        JobCandidateId   = candidate.Id,
                        InstitutionName  = w.InstitutionName,
                        PositionHeld     = w.PositionHeld,
                        StartDate        = w.StartDate,
                        EndDate          = w.EndDate,
                        Responsibilities = w.Responsibilities,
                        ReasonForLeaving = w.ReasonForLeaving,
                    });
                }
                else if (existing.TryGetValue(w.Id, out var row))
                {
                    row.InstitutionName  = w.InstitutionName;
                    row.PositionHeld     = w.PositionHeld;
                    row.StartDate        = w.StartDate;
                    row.EndDate          = w.EndDate;
                    row.Responsibilities = w.Responsibilities;
                    row.ReasonForLeaving = w.ReasonForLeaving;
                    await _workHistoryRepo.UpdateAsync(row);
                }
            }
        }

        // Qualifications
        {
            var existing = (await _qualificationRepo.FindAsync(q => q.JobCandidateId == candidate.Id && q.TenantId == tenantId))
                           .ToDictionary(q => q.Id);
            var incomingIds = dto.Qualifications.Where(q => q.Id != Guid.Empty).Select(q => q.Id).ToHashSet();

            var toDelete = existing.Values.Where(q => !incomingIds.Contains(q.Id)).ToList();
            if (toDelete.Count > 0) await _qualificationRepo.DeleteRangeAsync(toDelete);

            foreach (var q in dto.Qualifications)
            {
                if (q.Id == Guid.Empty)
                {
                    await _qualificationRepo.AddAsync(new JobCandidateQualification
                    {
                        TenantId              = tenantId,
                        JobCandidateId        = candidate.Id,
                        QualificationType     = q.QualificationType,
                        QualificationFreeText = q.QualificationName,
                        Institution           = q.Institution,
                        DateAwarded           = q.DateAwarded,
                        Grade                 = q.Grade,
                    });
                }
                else if (existing.TryGetValue(q.Id, out var row))
                {
                    row.QualificationType     = q.QualificationType;
                    row.QualificationFreeText = q.QualificationName;
                    row.Institution           = q.Institution;
                    row.DateAwarded           = q.DateAwarded;
                    row.Grade                 = q.Grade;
                    await _qualificationRepo.UpdateAsync(row);
                }
            }
        }

        // Referees
        {
            var existing = (await _refereeRepo.FindAsync(r => r.JobCandidateId == candidate.Id && r.TenantId == tenantId))
                           .ToDictionary(r => r.Id);
            var incomingIds = dto.Referees.Where(r => r.Id != Guid.Empty).Select(r => r.Id).ToHashSet();

            var toDelete = existing.Values.Where(r => !incomingIds.Contains(r.Id)).ToList();
            if (toDelete.Count > 0) await _refereeRepo.DeleteRangeAsync(toDelete);

            foreach (var r in dto.Referees)
            {
                if (r.Id == Guid.Empty)
                {
                    await _refereeRepo.AddAsync(new JobCandidateReferee
                    {
                        TenantId       = tenantId,
                        JobCandidateId = candidate.Id,
                        FullName       = r.FullName,
                        Position       = r.Position,
                        Organization   = r.Organization,
                        Email          = r.Email,
                        Phone          = r.Phone,
                        Relationship   = r.Relationship,
                        YearsKnown     = r.YearsKnown,
                    });
                }
                else if (existing.TryGetValue(r.Id, out var row))
                {
                    row.FullName     = r.FullName;
                    row.Position     = r.Position;
                    row.Organization = r.Organization;
                    row.Email        = r.Email;
                    row.Phone        = r.Phone;
                    row.Relationship = r.Relationship;
                    row.YearsKnown   = r.YearsKnown;
                    await _refereeRepo.UpdateAsync(row);
                }
            }
        }

        // Skills
        {
            var existing = (await _skillRepo.FindAsync(s => s.JobCandidateId == candidate.Id && s.TenantId == tenantId))
                           .ToDictionary(s => s.Id);
            var incomingIds = dto.Skills.Where(s => s.Id != Guid.Empty).Select(s => s.Id).ToHashSet();

            var toDelete = existing.Values.Where(s => !incomingIds.Contains(s.Id)).ToList();
            if (toDelete.Count > 0) await _skillRepo.DeleteRangeAsync(toDelete);

            foreach (var s in dto.Skills)
            {
                if (s.Id == Guid.Empty)
                {
                    await _skillRepo.AddAsync(new JobCandidateSkill
                    {
                        TenantId          = tenantId,
                        JobCandidateId    = candidate.Id,
                        SkillName         = s.SkillName,
                        Proficiency       = s.Proficiency,
                        YearsOfExperience = s.YearsOfExperience,
                        IsCertified       = s.IsCertified,
                        CertificationName = s.CertificationName,
                    });
                }
                else if (existing.TryGetValue(s.Id, out var row))
                {
                    row.SkillName         = s.SkillName;
                    row.Proficiency       = s.Proficiency;
                    row.YearsOfExperience = s.YearsOfExperience;
                    row.IsCertified       = s.IsCertified;
                    row.CertificationName = s.CertificationName;
                    await _skillRepo.UpdateAsync(row);
                }
            }
        }

        // Languages
        {
            var existing = (await _languageRepo.FindAsync(l => l.JobCandidateId == candidate.Id && l.TenantId == tenantId))
                           .ToDictionary(l => l.Id);
            var incomingIds = dto.Languages.Where(l => l.Id != Guid.Empty).Select(l => l.Id).ToHashSet();

            var toDelete = existing.Values.Where(l => !incomingIds.Contains(l.Id)).ToList();
            if (toDelete.Count > 0) await _languageRepo.DeleteRangeAsync(toDelete);

            foreach (var l in dto.Languages)
            {
                if (l.Id == Guid.Empty)
                {
                    await _languageRepo.AddAsync(new JobCandidateLanguage
                    {
                        TenantId       = tenantId,
                        JobCandidateId = candidate.Id,
                        LanguageName   = l.LanguageName,
                        Proficiency    = l.Proficiency,
                    });
                }
                else if (existing.TryGetValue(l.Id, out var row))
                {
                    row.LanguageName = l.LanguageName;
                    row.Proficiency  = l.Proficiency;
                    await _languageRepo.UpdateAsync(row);
                }
            }
        }

        // Interests
        {
            var existing = (await _interestRepo.FindAsync(i => i.JobCandidateId == candidate.Id && i.TenantId == tenantId))
                           .ToDictionary(i => i.Id);
            var incomingIds = dto.Interests.Where(i => i.Id != Guid.Empty).Select(i => i.Id).ToHashSet();

            var toDelete = existing.Values.Where(i => !incomingIds.Contains(i.Id)).ToList();
            if (toDelete.Count > 0) await _interestRepo.DeleteRangeAsync(toDelete);

            foreach (var i in dto.Interests)
            {
                if (i.Id == Guid.Empty)
                {
                    await _interestRepo.AddAsync(new JobCandidateInterest
                    {
                        TenantId       = tenantId,
                        JobCandidateId = candidate.Id,
                        Detail         = i.Detail,
                    });
                }
                else if (existing.TryGetValue(i.Id, out var row))
                {
                    row.Detail = i.Detail;
                    await _interestRepo.UpdateAsync(row);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Portal profile saved for account {AccountId}, candidate {CandidateId}",
            accountId, candidate.Id);

        // Return fresh profile
        return await GetProfileAsync(accountId, tenantId, ct);
    }

    // ── Save Draft ──────────────────────────────────────────────────────
    public async Task<CandidatePortalApplicationSummaryDto> SaveDraftAsync(
        Guid accountId, CandidatePortalSaveDraftDto dto, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetOwnedAccountAsync(accountId, tenantId);

        if (!account.JobCandidateId.HasValue)
            throw new InvalidOperationException(
                "Please complete your candidate profile before saving a draft.");

        var vacancy = await GetTenantVacancyAsync(dto.VacancyId, tenantId);
        if (vacancy.VacancyStatus != JobVacancyStatus.Published)
            throw new InvalidOperationException("This vacancy is no longer accepting applications.");

        // Check for an existing non-withdrawn application (Draft or otherwise)
        var existing = await _applicationRepo.FirstOrDefaultAsync(
            a => a.JobCandidateId == account.JobCandidateId.Value
              && a.JobVacancyId   == dto.VacancyId
              && a.TenantId       == tenantId
              && a.Status         != ApplicationStatus.Withdrawn);

        if (existing != null && existing.Status != ApplicationStatus.Draft)
            throw new InvalidOperationException(
                "You have already submitted an application for this vacancy.");

        if (existing != null)
        {
            // Update the existing draft
            existing.CoverLetter      = dto.CoverLetter;
            existing.YearsOfExperience = dto.YearsOfExperience;
            existing.AvailableFrom    = dto.AvailableFrom;
            existing.Source           = dto.Source;
            await _applicationRepo.UpdateAsync(existing);
            await _unitOfWork.SaveChangesAsync(ct);
            _logger.LogInformation("Draft application {AppNumber} updated by account {AccountId}",
                existing.ApplicationNumber, accountId);
            return MapApplicationToSummary(existing, vacancy);
        }

        // Create a new draft
        var application = new JobApplication
        {
            TenantId              = tenantId,
            ApplicationNumber     = await _applicationRepo.GetNextApplicationNumberAsync(),
            JobVacancyId          = dto.VacancyId,
            JobCandidateId        = account.JobCandidateId.Value,
            Status                = ApplicationStatus.Draft,
            Source                = dto.Source,
            CoverLetter           = dto.CoverLetter,
            YearsOfExperience     = dto.YearsOfExperience,
            AvailableFrom         = dto.AvailableFrom,
            IsInternalCandidate   = false,
            ExternalTrackingToken = GenerateTrackingToken(),
            ApplicationDate       = DateTime.UtcNow,
        };
        await _applicationRepo.AddAsync(application);

        if (dto.AddToTalentPool)
        {
            var candidate = await _candidateRepo.GetByIdAsync(account.JobCandidateId.Value);
            if (candidate == null || candidate.TenantId != tenantId)
                throw new InvalidOperationException("Candidate record not found.");
            if (!candidate.IsInTalentPool)
            {
                candidate.IsInTalentPool      = true;
                candidate.TalentPoolAddedDate = DateTime.UtcNow;
                await _candidateRepo.UpdateAsync(candidate);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Draft application {AppNumber} created by account {AccountId} for vacancy {VacancyId}",
            application.ApplicationNumber, accountId, dto.VacancyId);
        return MapApplicationToSummary(application, vacancy);
    }

    // ── Submit Draft ──────────────────────────────────────────────────
    public async Task<CandidatePortalApplicationSummaryDto> SubmitDraftAsync(
        Guid accountId, Guid applicationId, CandidatePortalSubmitDraftDto dto,
        Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetOwnedAccountAsync(accountId, tenantId);

        if (!account.JobCandidateId.HasValue)
            throw new InvalidOperationException("Application not found.");

        var application = await GetOwnedApplicationAsync(
            applicationId, account.JobCandidateId.Value, tenantId);
        if (application.Status != ApplicationStatus.Draft)
            throw new InvalidOperationException(
                $"Only draft applications can be submitted. Current status: {application.Status}.");

        var vacancy = await GetTenantVacancyAsync(application.JobVacancyId, tenantId);
        if (vacancy.VacancyStatus != JobVacancyStatus.Published)
            throw new InvalidOperationException("This vacancy is no longer accepting applications.");
        if (vacancy.ApplicationDeadline.HasValue && vacancy.ApplicationDeadline < DateTime.UtcNow)
            throw new InvalidOperationException("The application deadline for this vacancy has passed.");

        // Apply any final edits
        if (dto.CoverLetter        != null) application.CoverLetter        = dto.CoverLetter;
        if (dto.YearsOfExperience  != null) application.YearsOfExperience  = dto.YearsOfExperience;
        if (dto.AvailableFrom      != null) application.AvailableFrom      = dto.AvailableFrom;

        application.Status = ApplicationStatus.Submitted;
        await _applicationRepo.UpdateAsync(application);

        // Capture an immutable profile snapshot so re-scoring always uses
        // the data that existed at submission time, not later profile edits.
        try
        {
            var candidate = await _candidateRepo.GetWithFullDetailsAsync(application.JobCandidateId);
            if (candidate != null && candidate.TenantId == tenantId)
            {
                var snapshot = _snapshotService.BuildSnapshot(candidate, application.YearsOfExperience);
                application.ProfileSnapshotJson = JsonSerializer.Serialize(snapshot);
                await _applicationRepo.UpdateAsync(application);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Profile snapshot capture failed for portal application {AppNumber} — scoring will use live profile.",
                application.ApplicationNumber);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Bump the vacancy's counter separately, with a reload-and-retry on conflict. JobVacancy carries
        // a RowVersion, so incrementing it inside the save above would make two simultaneous submissions
        // fail the whole application over a counter clash.
        await IncrementVacancyApplicationCountAsync(vacancy.Id, ct);

        _logger.LogInformation(
            "Draft application {AppNumber} submitted by account {AccountId} for vacancy {VacancyId}",
            application.ApplicationNumber, accountId, application.JobVacancyId);

        // Place the application into the first pipeline stage, if the vacancy has one.
        await _pipelineService.PlaceInFirstPipelineStageAsync(
            application.Id, vacancy.Id, vacancy.TenantId, ct);

        var summary = MapApplicationToSummary(application, vacancy);
        await SendApplicationReceivedEmailAsync(account.Email, account.Email, summary);
        return summary;
    }

    /// <summary>
    /// Increments the vacancy's application counter, reloading and retrying on an optimistic-concurrency
    /// clash. Mirrors <c>JobApplicationService.UpdateVacancyCounterAsync</c>.
    /// </summary>
    private async Task IncrementVacancyApplicationCountAsync(Guid vacancyId, CancellationToken ct)
    {
        const int maxRetries = 3;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var vacancy = await _vacancyRepo.GetByIdAsync(vacancyId);
                if (vacancy is null) return;

                vacancy.ApplicationCount++;
                await _vacancyRepo.UpdateAsync(vacancy);
                await _unitOfWork.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries)
            {
                foreach (var entry in ex.Entries.Where(e => e.Entity is JobVacancy))
                    await entry.ReloadAsync(ct);
            }
        }

        _logger.LogError(
            "Application-count update for vacancy {VacancyId} failed after {Max} retries.",
            vacancyId, maxRetries);
    }

    // ── Withdraw Application ───────────────────────────────────────────────────
    public async Task WithdrawApplicationAsync(
        Guid accountId, Guid applicationId, CandidatePortalWithdrawDto dto,
        Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetOwnedAccountAsync(accountId, tenantId);

        if (!account.JobCandidateId.HasValue)
            throw new InvalidOperationException("Application not found.");

        var application = await GetOwnedApplicationAsync(
            applicationId, account.JobCandidateId.Value, tenantId);

        if (application.Status is ApplicationStatus.Hired or ApplicationStatus.Withdrawn)
            throw new InvalidOperationException(
                $"This application cannot be withdrawn — current status: {application.Status}.");

        application.Status          = ApplicationStatus.Withdrawn;
        application.WithdrawnDate   = DateTime.UtcNow;
        application.WithdrawalReason = dto.Reason;
        await _applicationRepo.UpdateAsync(application);
        var withdrawVacancy = await _vacancyRepo.GetByIdAsync(application.JobVacancyId);
        if (withdrawVacancy is not null && withdrawVacancy.TenantId == tenantId)
        {
            withdrawVacancy.ApplicationCount = Math.Max(0, withdrawVacancy.ApplicationCount - 1);
            await _vacancyRepo.UpdateAsync(withdrawVacancy);
        }
        await _unitOfWork.SaveChangesAsync(ct);
    }

    // ── Get Applications ───────────────────────────────────────────────────────
    public async Task<List<CandidatePortalApplicationSummaryDto>> GetApplicationsAsync(
        Guid accountId, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetOwnedAccountAsync(accountId, tenantId);

        if (!account.JobCandidateId.HasValue)
            return new List<CandidatePortalApplicationSummaryDto>();

        var applications = await _applicationRepo.GetByCandidateIdAsync(account.JobCandidateId.Value);
        return applications
            .Where(a => a.TenantId == tenantId && a.JobCandidateId == account.JobCandidateId.Value)
            .OrderByDescending(a => a.ApplicationDate)
            .Select(a => MapApplicationToSummary(a, a.JobVacancy))
            .ToList();
    }

    // ── Get Dashboard ──────────────────────────────────────────────────────────
    public async Task<CandidatePortalDashboardDto> GetDashboardAsync(
        Guid accountId, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var profile      = await GetProfileAsync(accountId, tenantId, ct);
        var applications = await GetApplicationsAsync(accountId, tenantId, ct);

        var active = applications
            .Where(a => a.Status is not ApplicationStatus.Withdrawn and not ApplicationStatus.Rejected)
            .ToList();

        return new CandidatePortalDashboardDto
        {
            Profile             = profile,
            Applications        = applications,
            TotalApplications   = applications.Count,
            ActiveApplications  = active.Count,
            ShortlistedCount    = applications.Count(a => a.Status == ApplicationStatus.Shortlisted),
        };
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    // ── Documents ──────────────────────────────────────────────────────────────

    public async Task<List<JobCandidateDocumentDto>> GetDocumentsAsync(
        Guid accountId, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetOwnedAccountAsync(accountId, tenantId);

        if (!account.JobCandidateId.HasValue) return new();

        var docs = await _documentRepo.FindAsync(d => d.JobCandidateId == account.JobCandidateId.Value && d.TenantId == tenantId);
        return docs.OrderByDescending(d => d.UploadDate).Select(d => new JobCandidateDocumentDto
        {
            Id             = d.Id,
            TenantId       = d.TenantId,
            JobCandidateId = d.JobCandidateId,
            DocumentType   = d.DocumentType,
            FileName       = d.FileName,
            FilePath       = d.FilePath,
            UploadDate     = d.UploadDate,
        }).ToList();
    }

    public async Task<JobCandidateDocumentDto> AddDocumentAsync(
        Guid accountId, JobCandidateDocumentType documentType, string fileName, string filePath,
        Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetOwnedAccountAsync(accountId, tenantId);

        if (!account.JobCandidateId.HasValue)
            throw new InvalidOperationException("You must save your profile before uploading documents.");

        var doc = new JobCandidateDocument
        {
            TenantId       = tenantId,
            JobCandidateId = account.JobCandidateId.Value,
            DocumentType   = documentType,
            FileName       = fileName,
            FilePath       = filePath,
            UploadDate     = DateTime.UtcNow,
        };

        await _documentRepo.AddAsync(doc);
        await _unitOfWork.SaveChangesAsync(ct);

        return new JobCandidateDocumentDto
        {
            Id             = doc.Id,
            TenantId       = doc.TenantId,
            JobCandidateId = doc.JobCandidateId,
            DocumentType   = doc.DocumentType,
            FileName       = doc.FileName,
            FilePath       = doc.FilePath,
            UploadDate     = doc.UploadDate,
        };
    }

    public async Task DeleteDocumentAsync(
        Guid accountId, Guid documentId, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetOwnedAccountAsync(accountId, tenantId);

        if (!account.JobCandidateId.HasValue)
            throw new InvalidOperationException("Document not found.");

        var doc = await _documentRepo.FirstOrDefaultAsync(
            d => d.Id == documentId
              && d.TenantId == tenantId
              && d.JobCandidateId == account.JobCandidateId.Value);
        if (doc == null)
            throw new InvalidOperationException("Document not found.");

        await _documentRepo.DeleteAsync(doc);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<string> UpdateProfilePhotoAsync(
        Guid accountId, string photoUrl, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetOwnedAccountAsync(accountId, tenantId);

        if (!account.JobCandidateId.HasValue)
            throw new InvalidOperationException("You must save your profile before uploading a photo.");

        var candidate = await _candidateRepo.GetByIdAsync(account.JobCandidateId.Value);
        if (candidate == null || candidate.TenantId != tenantId)
            throw new InvalidOperationException("Candidate not found.");

        candidate.ProfilePhotoUrl = photoUrl;
        await _candidateRepo.UpdateAsync(candidate);
        await _unitOfWork.SaveChangesAsync(ct);

        return photoUrl;
    }

    private static void MapDtoToCandidate(UpdateCandidatePortalProfileDto dto, JobCandidate c)
    {
        c.FirstName       = dto.FirstName;
        c.MiddleName      = dto.MiddleName;
        c.LastName        = dto.LastName;
        c.Phone           = dto.Phone;
        c.AlternatePhone  = dto.AlternatePhone;
        c.DateOfBirth     = dto.DateOfBirth ?? c.DateOfBirth;
        c.Gender          = dto.Gender ?? c.Gender;
        c.City            = dto.City ?? string.Empty;
        if (dto.CountryId != Guid.Empty) c.CountryId = dto.CountryId;
        c.PostalAddress   = dto.PostalAddress;
        c.DigitalAddress  = dto.DigitalAddress;
        c.LinkedInProfile = dto.LinkedInProfile;
        c.PortfolioUrl    = dto.PortfolioUrl;
        c.GitHubUrl       = dto.GitHubUrl;
        // Professional profile
        c.Headline              = dto.Headline;
        c.ProfessionalSummary   = dto.ProfessionalSummary;
        c.CurrentJobTitle       = dto.CurrentJobTitle;
        c.CurrentEmployer       = dto.CurrentEmployer;
        c.TotalYearsExperience  = dto.TotalYearsExperience;
        // Availability & preferences
        c.NoticePeriodDays          = dto.NoticePeriodDays;
        c.AvailableFrom             = dto.AvailableFrom;
        c.PreferredWorkArrangement  = dto.PreferredWorkArrangement;
        // Compensation
        c.ExpectedSalaryMin      = dto.ExpectedSalaryMin;
        c.ExpectedSalaryMax      = dto.ExpectedSalaryMax;
        c.ExpectedSalaryCurrency = dto.ExpectedSalaryCurrency;
        // Compliance
        c.WorkAuthorizationStatus = dto.WorkAuthorizationStatus;
        // Documents
        c.ProfilePhotoUrl = dto.ProfilePhotoUrl;
        c.IsInTalentPool  = dto.IsInTalentPool;
        if (dto.IsInTalentPool && !c.TalentPoolAddedDate.HasValue)
            c.TalentPoolAddedDate = DateTime.UtcNow;
    }

    private static void PopulateCandidateFields(CandidatePortalProfileDto dto, JobCandidate c)
    {
        dto.CandidateId    = c.Id;
        dto.FirstName      = c.FirstName;
        dto.MiddleName     = c.MiddleName;
        dto.LastName       = c.LastName;
        dto.Phone          = c.Phone;
        dto.AlternatePhone = c.AlternatePhone;
        dto.DateOfBirth    = c.DateOfBirth == default ? null : c.DateOfBirth;
        dto.Gender          = c.Gender;
        dto.City            = c.City;
        dto.CountryId       = c.CountryId;
        dto.CountryName     = c.Country?.Name;
        dto.PostalAddress   = c.PostalAddress;
        dto.DigitalAddress  = c.DigitalAddress;
        dto.LinkedInProfile = c.LinkedInProfile;
        dto.PortfolioUrl   = c.PortfolioUrl;
        dto.GitHubUrl      = c.GitHubUrl;
        // Professional profile
        dto.Headline             = c.Headline;
        dto.ProfessionalSummary  = c.ProfessionalSummary;
        dto.CurrentJobTitle      = c.CurrentJobTitle;
        dto.CurrentEmployer      = c.CurrentEmployer;
        dto.TotalYearsExperience = c.TotalYearsExperience;
        // Availability & preferences
        dto.NoticePeriodDays         = c.NoticePeriodDays;
        dto.AvailableFrom            = c.AvailableFrom;
        dto.PreferredWorkArrangement = c.PreferredWorkArrangement;
        // Compensation
        dto.ExpectedSalaryMin      = c.ExpectedSalaryMin;
        dto.ExpectedSalaryMax      = c.ExpectedSalaryMax;
        dto.ExpectedSalaryCurrency = c.ExpectedSalaryCurrency;
        // Compliance
        dto.WorkAuthorizationStatus = c.WorkAuthorizationStatus;
        // Documents
        dto.CvFilePath       = c.CvFilePath;
        dto.ProfilePhotoUrl  = c.ProfilePhotoUrl;
        dto.IsInTalentPool   = c.IsInTalentPool;

        dto.WorkHistories = c.WorkHistories.Select(w => new JobCandidateWorkHistoryDto
        {
            Id              = w.Id,
            JobCandidateId  = w.JobCandidateId,
            InstitutionName = w.InstitutionName,
            PositionHeld    = w.PositionHeld,
            StartDate       = w.StartDate,
            EndDate         = w.EndDate,
            Responsibilities = w.Responsibilities,
            ReasonForLeaving = w.ReasonForLeaving,
        }).ToList();

        dto.Qualifications = c.Qualifications.Select(q => new JobCandidateQualificationDto
        {
            Id                    = q.Id,
            JobCandidateId        = q.JobCandidateId,
            QualificationType     = q.QualificationType,
            QualificationName     = q.QualificationFreeText ?? q.Qualification?.Name ?? string.Empty,
            Institution           = q.Institution,
            DateAwarded           = q.DateAwarded,
            Grade                 = q.Grade,
        }).ToList();

        dto.Referees = c.Referees.Select(r => new JobCandidateRefereeDto
        {
            Id             = r.Id,
            JobCandidateId = r.JobCandidateId,
            FullName       = r.FullName,
            Position       = r.Position,
            Organization   = r.Organization,
            Email          = r.Email,
            Phone          = r.Phone,
            Relationship   = r.Relationship,
            YearsKnown     = r.YearsKnown,
        }).ToList();

        dto.Skills = c.Skills.Select(s => new JobCandidateSkillDto
        {
            Id                = s.Id,
            JobCandidateId    = s.JobCandidateId,
            SkillName         = s.SkillName,
            Proficiency       = s.Proficiency,
            YearsOfExperience = s.YearsOfExperience,
            IsCertified       = s.IsCertified,
            CertificationName = s.CertificationName,
        }).ToList();

        dto.Languages = c.Languages.Select(l => new JobCandidateLanguageDto
        {
            Id             = l.Id,
            JobCandidateId = l.JobCandidateId,
            LanguageName   = l.LanguageName,
            Proficiency    = l.Proficiency,
        }).ToList();

        dto.Interests = c.Interests.Select(i => new JobCandidateInterestDto
        {
            Id             = i.Id,
            JobCandidateId = i.JobCandidateId,
            Detail         = i.Detail,
        }).ToList();

        dto.Documents = c.Documents.Select(d => new JobCandidateDocumentDto
        {
            Id             = d.Id,
            TenantId       = d.TenantId,
            JobCandidateId = d.JobCandidateId,
            DocumentType   = d.DocumentType,
            FileName       = d.FileName,
            FilePath       = d.FilePath,
            UploadDate     = d.UploadDate,
        }).OrderByDescending(d => d.UploadDate).ToList();
    }

    private static CandidatePortalApplicationSummaryDto MapApplicationToSummary(
        JobApplication app, JobVacancy? vacancy)
    {
        var withdrawable = app.Status is not (ApplicationStatus.Hired
            or ApplicationStatus.Withdrawn
            or ApplicationStatus.Rejected
            or ApplicationStatus.OfferExtended);

        return new CandidatePortalApplicationSummaryDto
        {
            ApplicationId      = app.Id,
            ApplicationNumber  = app.ApplicationNumber,
            TrackingToken      = app.ExternalTrackingToken,
            JobTitle           = vacancy?.JobTitle ?? string.Empty,
            VacancyNumber      = vacancy?.VacancyNumber ?? string.Empty,
            DepartmentName     = vacancy?.Requisition?.OrganizationUnit?.Name,
            LocationName       = vacancy?.Requisition?.Location?.Name,
            EmploymentTypeLabel = vacancy?.EmploymentType.ToString() ?? string.Empty,
            Status             = app.Status,
            StatusLabel        = GetStatusLabel(app.Status),
            ApplicationDate    = app.ApplicationDate,
            ShortlistedDate    = app.ShortlistedDate,
            WithdrawnDate      = app.WithdrawnDate,
            RejectedDate       = app.RejectedDate,
            CanWithdraw        = withdrawable,
        };
    }

    private static string GetStatusLabel(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Draft        => "Draft",
        ApplicationStatus.New          => "Application Received",
        ApplicationStatus.Submitted    => "Application Received",
        ApplicationStatus.UnderReview  => "Under Review",
        ApplicationStatus.Shortlisted  => "Shortlisted",
        ApplicationStatus.InterviewScheduled => "Interview Scheduled",
        ApplicationStatus.OfferExtended => "Offer Extended",
        ApplicationStatus.Hired        => "Position Filled",
        ApplicationStatus.Withdrawn    => "Withdrawn",
        ApplicationStatus.Rejected     => "Unsuccessful",
        _                              => status.ToString(),
    };

    private static string GenerateTrackingToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(18);
        return Convert.ToBase64String(bytes)
            .Replace("+", "").Replace("/", "").Replace("=", "")
            .ToUpperInvariant()[..16];
    }

    // ── Email helpers ─────────────────────────────────────────────────────

    private async Task SendApplicationReceivedEmailAsync(
        string toEmail,
        string candidateName,
        CandidatePortalApplicationSummaryDto summary)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var tokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CandidateName"]     = candidateName,
            ["JobTitle"]          = summary.JobTitle,
            ["VacancyNumber"]     = summary.VacancyNumber,
            ["ApplicationNumber"] = summary.ApplicationNumber,
            ["SubmittedAt"]       = summary.ApplicationDate.ToString("dd MMM yyyy HH:mm") + " UTC",
        };

        try
        {
            var emailTask = _templatedEmail.SendAsync(
                RecruitmentEmailCatalog.Module, RecruitmentEmailCatalog.Events.ApplicationReceived, toEmail, tokens);

            // Race the email send against a 10-second timeout so a slow or
            // unresponsive SMTP server never blocks the application submission
            // response. The application is already persisted at this point.
            if (await Task.WhenAny(emailTask, Task.Delay(TimeSpan.FromSeconds(10))) == emailTask)
                await emailTask; // observe result / propagate any exception to the catch below
            else
                _logger.LogWarning(
                    "Application received email timed out after 10 s for {Email} — submission was successful.",
                    toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send application received email to {Email}", toEmail);
        }
    }
}
