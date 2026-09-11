using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
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
/// Candidate self-service — profile management and job applications for self-registered careers
/// accounts (main-scheme Identity users in the Candidate role). The caller's candidate is
/// resolved through <c>JobCandidate.UserId</c>; the retired portal's account table is gone.
/// </summary>
public sealed class CandidatePortalService : ICandidatePortalService
{
    private readonly IJobCandidateRepository _candidateRepo;
    private readonly IGenericRepository<JobCandidateWorkHistory> _workHistoryRepo;
    private readonly IGenericRepository<JobCandidateQualification> _qualificationRepo;
    private readonly IGenericRepository<JobCandidateReferee> _refereeRepo;
    private readonly IGenericRepository<JobCandidateSkill> _skillRepo;
    private readonly IGenericRepository<JobCandidateLanguage> _languageRepo;
    private readonly IGenericRepository<Language> _languageMasterRepo;
    private readonly IGenericRepository<IdentificationType> _identificationTypeRepo;
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
        IJobCandidateRepository candidateRepo,
        IGenericRepository<JobCandidateWorkHistory> workHistoryRepo,
        IGenericRepository<JobCandidateQualification> qualificationRepo,
        IGenericRepository<JobCandidateReferee> refereeRepo,
        IGenericRepository<JobCandidateSkill> skillRepo,
        IGenericRepository<JobCandidateLanguage> languageRepo,
        IGenericRepository<Language> languageMasterRepo,
        IGenericRepository<IdentificationType> identificationTypeRepo,
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
        _candidateRepo    = candidateRepo;
        _workHistoryRepo  = workHistoryRepo;
        _qualificationRepo = qualificationRepo;
        _refereeRepo      = refereeRepo;
        _skillRepo        = skillRepo;
        _languageRepo     = languageRepo;
        _languageMasterRepo = languageMasterRepo;
        _identificationTypeRepo = identificationTypeRepo;
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

    /// <summary>The caller's own candidate profile, or null when they have not completed one.</summary>
    private async Task<JobCandidate?> FindOwnCandidateAsync(Guid userId, Guid tenantId)
    {
        return await _candidateRepo.FirstOrDefaultAsync(
            c => c.UserId == userId && c.TenantId == tenantId);
    }

    private async Task<JobCandidate> RequireOwnCandidateAsync(Guid userId, Guid tenantId, string act)
    {
        return await FindOwnCandidateAsync(userId, tenantId)
            ?? throw new InvalidOperationException($"Please complete your candidate profile before {act}.");
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
        CandidateAccountContext account, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var dto = new CandidatePortalProfileDto
        {
            AccountId       = account.UserId,
            Email           = account.Email,
            IsEmailVerified = account.EmailConfirmed,
        };

        var own = await FindOwnCandidateAsync(account.UserId, tenantId);
        if (own != null)
        {
            dto.CandidateId = own.Id;
            var candidate = await _candidateRepo.GetWithFullDetailsAsync(own.Id);
            if (candidate != null && candidate.TenantId == tenantId)
                PopulateCandidateFields(dto, candidate);
        }

        return dto;
    }

    // ── Save Profile ───────────────────────────────────────────────────────────
    public async Task<CandidatePortalProfileDto> SaveProfileAsync(
        CandidateAccountContext account,
        UpdateCandidatePortalProfileDto dto,
        Guid tenantId,
        CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // Round 3, lane C1: an identity document type must be one the tenant accepts.
        await RequireIdentificationTypeAsync(dto.NationalIdTypeId, tenantId);

        // Loaded without nav props deliberately — EF tracking child collections during the
        // scalar update would cause duplicate inserts when the collections are patched below.
        var candidate = await FindOwnCandidateAsync(account.UserId, tenantId);

        if (candidate == null)
        {
            // An unlinked candidate with this email may already exist — HR-created, or minted by
            // an application before the account did. Adopting it hands over that application
            // history, so it takes MAILBOX proof, not just a matching string: anyone can type
            // someone else's address at registration.
            var byEmail = await _candidateRepo.GetByEmailAsync(account.Email, tenantId);
            if (byEmail != null)
            {
                if (byEmail.UserId.HasValue && byEmail.UserId.Value != account.UserId)
                    throw new InvalidOperationException(
                        "A candidate profile with this email address is already linked to another account.");
                if (!account.EmailConfirmed)
                    throw new InvalidOperationException(
                        "A candidate profile with your email address already exists. Confirm your email address to link it to your account.");

                candidate = byEmail;
                candidate.UserId = account.UserId;
                MapDtoToCandidate(dto, candidate);
                await _candidateRepo.UpdateAsync(candidate);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            else
            {
                candidate = new JobCandidate
                {
                    TenantId        = tenantId,
                    CandidateNumber = await _candidateRepo.GetNextCandidateNumberAsync(),
                    Email           = account.Email,
                    UserId          = account.UserId,
                };
                MapDtoToCandidate(dto, candidate);
                await _candidateRepo.AddAsync(candidate);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }
        else
        {
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
                        CertificationName = s.IsCertified ? s.CertificationName : null,
                        CertificationNumber = s.IsCertified ? s.CertificationNumber : null,
                        CertifyingBody = s.IsCertified ? s.CertifyingBody : null,
                        CertificationExpiryDate = s.IsCertified ? s.CertificationExpiryDate : null,
                    });
                }
                else if (existing.TryGetValue(s.Id, out var row))
                {
                    row.SkillName         = s.SkillName;
                    row.Proficiency       = s.Proficiency;
                    row.YearsOfExperience = s.YearsOfExperience;
                    row.IsCertified       = s.IsCertified;
                    row.CertificationName = s.IsCertified ? s.CertificationName : null;
                    row.CertificationNumber = s.IsCertified ? s.CertificationNumber : null;
                    row.CertifyingBody = s.IsCertified ? s.CertifyingBody : null;
                    row.CertificationExpiryDate = s.IsCertified ? s.CertificationExpiryDate : null;
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

            // Round 3, lane C1: a language is a catalogue row or a typed name. With a row the name
            // is mirrored from the catalogue, so the free-text column stays readable by everything
            // that only knows the name (the shortlisting engine among them).
            var master = (await _languageMasterRepo.FindAsync(x => x.TenantId == tenantId && !x.IsDeleted))
                         .ToDictionary(x => x.Id);

            foreach (var l in dto.Languages)
            {
                var (languageId, languageName) = ResolveLanguage(l.LanguageId, l.LanguageName, master);
                if (l.Id == Guid.Empty)
                {
                    await _languageRepo.AddAsync(new JobCandidateLanguage
                    {
                        TenantId       = tenantId,
                        JobCandidateId = candidate.Id,
                        LanguageId     = languageId,
                        LanguageName   = languageName,
                        Proficiency    = l.Proficiency,
                    });
                }
                else if (existing.TryGetValue(l.Id, out var row))
                {
                    row.LanguageId   = languageId;
                    row.LanguageName = languageName;
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
            "Candidate profile saved for user {UserId}, candidate {CandidateId}",
            account.UserId, candidate.Id);

        // Return fresh profile
        return await GetProfileAsync(account, tenantId, ct);
    }

    // ── Save Draft ──────────────────────────────────────────────────────
    public async Task<CandidatePortalApplicationSummaryDto> SaveDraftAsync(
        Guid userId, CandidatePortalSaveDraftDto dto, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var ownCandidate = await RequireOwnCandidateAsync(userId, tenantId, "saving a draft");

        var vacancy = await GetTenantVacancyAsync(dto.VacancyId, tenantId);
        if (vacancy.VacancyStatus != JobVacancyStatus.Published)
            throw new InvalidOperationException("This vacancy is no longer accepting applications.");

        // Check for an existing non-withdrawn application (Draft or otherwise)
        var existing = await _applicationRepo.FirstOrDefaultAsync(
            a => a.JobCandidateId == ownCandidate.Id
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
            _logger.LogInformation("Draft application {AppNumber} updated by user {UserId}",
                existing.ApplicationNumber, userId);
            return MapApplicationToSummary(existing, vacancy);
        }

        // Create a new draft
        var application = new JobApplication
        {
            TenantId              = tenantId,
            ApplicationNumber     = await _applicationRepo.GetNextApplicationNumberAsync(),
            JobVacancyId          = dto.VacancyId,
            JobCandidateId        = ownCandidate.Id,
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

        if (dto.AddToTalentPool && !ownCandidate.IsInTalentPool)
        {
            ownCandidate.IsInTalentPool      = true;
            ownCandidate.TalentPoolAddedDate = DateTime.UtcNow;
            await _candidateRepo.UpdateAsync(ownCandidate);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Draft application {AppNumber} created by user {UserId} for vacancy {VacancyId}",
            application.ApplicationNumber, userId, dto.VacancyId);
        return MapApplicationToSummary(application, vacancy);
    }

    // ── Submit Draft ──────────────────────────────────────────────────
    public async Task<CandidatePortalApplicationSummaryDto> SubmitDraftAsync(
        CandidateAccountContext account, Guid applicationId, CandidatePortalSubmitDraftDto dto,
        Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var ownCandidate = await FindOwnCandidateAsync(account.UserId, tenantId)
            ?? throw new InvalidOperationException("Application not found.");

        var application = await GetOwnedApplicationAsync(
            applicationId, ownCandidate.Id, tenantId);
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
        await AdjustVacancyApplicationCountAsync(vacancy.Id, +1, tenantId, ct);

        _logger.LogInformation(
            "Draft application {AppNumber} submitted by user {UserId} for vacancy {VacancyId}",
            application.ApplicationNumber, account.UserId, application.JobVacancyId);

        // Place the application into the first pipeline stage, if the vacancy has one.
        await _pipelineService.PlaceInFirstPipelineStageAsync(
            application.Id, vacancy.Id, vacancy.TenantId, ct);

        var summary = MapApplicationToSummary(application, vacancy);
        await SendApplicationReceivedEmailAsync(account.Email, account.Email, summary);
        return summary;
    }

    /// <summary>
    /// Adjusts the vacancy's denormalised application counter by <paramref name="delta"/>,
    /// reloading and retrying on an optimistic-concurrency clash. Mirrors
    /// <c>JobApplicationService.UpdateVacancyCounterAsync</c>.
    /// </summary>
    /// <remarks>
    /// Callers have ALREADY committed the owning application change before this runs, so a
    /// conflict on the final attempt is logged and swallowed here rather than thrown. The
    /// previous <c>when (attempt &lt; maxRetries)</c> filter let the exception escape on the
    /// last attempt, so the portal reported a failure for a submission that had in fact
    /// succeeded — and the candidate's retry then hit the duplicate-application guard. The
    /// swallow lives inside this method so no future caller can reintroduce that.
    /// </remarks>
    private async Task AdjustVacancyApplicationCountAsync(
        Guid vacancyId, int delta, Guid tenantId, CancellationToken ct)
    {
        const int maxRetries = 3;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var vacancy = await _vacancyRepo.GetByIdAsync(vacancyId);
                if (vacancy is null || vacancy.TenantId != tenantId) return;

                vacancy.ApplicationCount = Math.Max(0, vacancy.ApplicationCount + delta);
                await _vacancyRepo.UpdateAsync(vacancy);
                await _unitOfWork.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt == maxRetries)
                {
                    _logger.LogError(ex,
                        "Application-count adjustment ({Delta}) for vacancy {VacancyId} failed after {Max} retries.",
                        delta, vacancyId, maxRetries);
                    return;
                }

                // A reload can itself throw when the row was deleted concurrently; that must
                // not escape and mask this loop's own error handling.
                try
                {
                    foreach (var entry in ex.Entries.Where(e => e.Entity is JobVacancy))
                        await entry.ReloadAsync(ct);
                }
                catch (Exception reloadEx)
                {
                    _logger.LogWarning(reloadEx,
                        "Reload failed while adjusting the counter for vacancy {VacancyId}.",
                        vacancyId);
                    return;
                }
            }
        }
    }

    // ── Withdraw Application ───────────────────────────────────────────────────
    public async Task WithdrawApplicationAsync(
        Guid userId, Guid applicationId, CandidatePortalWithdrawDto dto,
        Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var ownCandidate = await FindOwnCandidateAsync(userId, tenantId)
            ?? throw new InvalidOperationException("Application not found.");

        var application = await GetOwnedApplicationAsync(
            applicationId, ownCandidate.Id, tenantId);

        if (application.Status is ApplicationStatus.Hired or ApplicationStatus.Withdrawn)
            throw new InvalidOperationException(
                $"This application cannot be withdrawn — current status: {application.Status}.");

        application.Status          = ApplicationStatus.Withdrawn;
        application.WithdrawnDate   = DateTime.UtcNow;
        application.WithdrawalReason = dto.Reason;
        await _applicationRepo.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync(ct);

        // Denormalised counter only, and deliberately NOT part of the save above: JobVacancy
        // carries a RowVersion, so a concurrent vacancy edit would make that save throw and
        // lose the candidate's withdrawal entirely. Same retry-then-swallow contract as
        // submission — the withdrawal is already durable.
        await AdjustVacancyApplicationCountAsync(
            application.JobVacancyId, -1, tenantId, ct);
    }

    // ── Get Applications ───────────────────────────────────────────────────────
    public async Task<List<CandidatePortalApplicationSummaryDto>> GetApplicationsAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var ownCandidate = await FindOwnCandidateAsync(userId, tenantId);
        if (ownCandidate == null)
            return new List<CandidatePortalApplicationSummaryDto>();

        var applications = await _applicationRepo.GetByCandidateIdAsync(ownCandidate.Id);
        return applications
            .Where(a => a.TenantId == tenantId && a.JobCandidateId == ownCandidate.Id)
            .OrderByDescending(a => a.ApplicationDate)
            .Select(a => MapApplicationToSummary(a, a.JobVacancy))
            .ToList();
    }

    // ── Get Dashboard ──────────────────────────────────────────────────────────
    public async Task<CandidatePortalDashboardDto> GetDashboardAsync(
        CandidateAccountContext account, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var profile      = await GetProfileAsync(account, tenantId, ct);
        var applications = await GetApplicationsAsync(account.UserId, tenantId, ct);

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
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var ownCandidate = await FindOwnCandidateAsync(userId, tenantId);
        if (ownCandidate == null) return new();

        var candidateId = ownCandidate.Id;
        var docs = await _documentRepo.FindAsync(d => d.JobCandidateId == candidateId && d.TenantId == tenantId);
        return docs.OrderByDescending(d => d.UploadDate).Select(d => new JobCandidateDocumentDto
        {
            Id             = d.Id,
            TenantId       = d.TenantId,
            JobCandidateId = d.JobCandidateId,
            DocumentType   = d.DocumentType,
            FileName       = d.FileName,
            FilePath       = d.FilePath,
            UploadDate     = d.UploadDate,
            Description    = d.Description,
        }).ToList();
    }

    /// <summary>
    /// Resolves the candidate profile behind the account, so the caller can name it as the
    /// source record when registering a document in the central DMS.
    /// </summary>
    public async Task<Guid> RequireCandidateIdAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var ownCandidate = await FindOwnCandidateAsync(userId, tenantId);
        return ownCandidate?.Id
            ?? throw new InvalidOperationException(
                "You must save your profile before uploading documents.");
    }

    /// <summary>
    /// A candidate language is a catalogue row or a typed name (round 3, lane C1). An id that is
    /// not in the tenant's catalogue is refused rather than silently dropped; neither id nor
    /// name is refused too — an empty language row tells nobody anything.
    /// </summary>
    internal static (Guid? LanguageId, string LanguageName) ResolveLanguage(
        Guid? languageId, string? languageName, IReadOnlyDictionary<Guid, Language> master)
    {
        if (languageId is { } id)
        {
            if (!master.TryGetValue(id, out var row))
                throw new InvalidOperationException("The language chosen is not in the catalogue. Pick one from the list or type the name.");
            return (row.Id, row.Name);
        }
        var name = languageName?.Trim();
        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException("A language needs either a catalogue entry or a name.");
        return (null, name);
    }

    /// <summary>Round 3, lane C1: the identity document type must be one of the tenant's active types.</summary>
    private async Task RequireIdentificationTypeAsync(Guid? typeId, Guid tenantId)
    {
        if (typeId is not { } id) return;
        var ok = await _identificationTypeRepo.GetQueryable()
            .AnyAsync(x => x.Id == id && x.TenantId == tenantId && x.IsActive && !x.IsDeleted);
        if (!ok)
            throw new InvalidOperationException("The identification type chosen is not one this organisation accepts.");
    }

    public async Task<JobCandidateDocumentDto> AddDocumentAsync(
        Guid userId, JobCandidateDocumentType documentType, string fileName, string filePath,
        Guid tenantId, CancellationToken ct = default,
        Guid? fileUploadRecordId = null,
        Guid? documentRecordId = null,
        Guid? documentVersionId = null,
        string? description = null)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var ownCandidate = await FindOwnCandidateAsync(userId, tenantId)
            ?? throw new InvalidOperationException("You must save your profile before uploading documents.");

        var doc = new JobCandidateDocument
        {
            TenantId       = tenantId,
            JobCandidateId = ownCandidate.Id,
            DocumentType   = documentType,
            Description    = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            FileName       = fileName,
            FilePath       = filePath,
            UploadDate     = DateTime.UtcNow,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId   = documentRecordId,
            DocumentVersionId  = documentVersionId,
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
            Description    = doc.Description,
        };
    }

    public async Task DeleteDocumentAsync(
        Guid userId, Guid documentId, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var ownCandidate = await FindOwnCandidateAsync(userId, tenantId)
            ?? throw new InvalidOperationException("Document not found.");

        var doc = await _documentRepo.FirstOrDefaultAsync(
            d => d.Id == documentId
              && d.TenantId == tenantId
              && d.JobCandidateId == ownCandidate.Id);
        if (doc == null)
            throw new InvalidOperationException("Document not found.");

        await _documentRepo.DeleteAsync(doc);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Points the candidate profile at a stored, scanned photo.
    /// </summary>
    /// <remarks>
    /// Photos used to be written to the public web root and the resulting URL saved on the
    /// profile. A photograph of a named job applicant is personal data, so it now lives in
    /// private storage and is only reachable through the authorizing photo endpoint —
    /// <c>ProfilePhotoUrl</c> stays null on new rows because there is no public URL to store.
    /// </remarks>
    public async Task UpdateProfilePhotoAsync(
        Guid userId, Guid fileUploadRecordId, Guid tenantId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var candidate = await FindOwnCandidateAsync(userId, tenantId)
            ?? throw new InvalidOperationException("You must save your profile before uploading a photo.");

        candidate.ProfilePhotoFileUploadRecordId = fileUploadRecordId;
        candidate.ProfilePhotoUrl = null;
        await _candidateRepo.UpdateAsync(candidate);
        await _unitOfWork.SaveChangesAsync(ct);
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
        // National identity (round 3, lane C1) — the type is checked by the caller
        c.NationalIdTypeId     = dto.NationalIdTypeId;
        c.NationalIdNumber     = string.IsNullOrWhiteSpace(dto.NationalIdNumber) ? null : dto.NationalIdNumber.Trim();
        c.NationalIdExpiryDate = dto.NationalIdExpiryDate;
        // Documents
        // ⚠ ProfilePhotoUrl is NOT taken from the payload. It is the legacy public URL, and
        // UpdateProfilePhotoAsync above deliberately nulls it when a photo is uploaded through the
        // gate — so accepting it here let an EXTERNAL candidate put an arbitrary 500-character
        // string back on their own record, and every profile save undid the upload's own cleanup.
        // The photo is set by uploading it; there is no public URL to store.
        c.IsInTalentPool  = dto.IsInTalentPool;
        if (dto.IsInTalentPool && !c.TalentPoolAddedDate.HasValue)
            c.TalentPoolAddedDate = DateTime.UtcNow;
    }

    // ⚠ Every child mapping below filters IsDeleted even though the repository read already
    // uses filtered includes: SaveProfileAsync soft-deletes replace-set leavers and then builds
    // its response in the SAME DbContext, and EF's relationship fixup re-attaches the tracked,
    // just-deleted children to the navigation regardless of what the SQL returned. Without the
    // mapper-side filter, a row the save had just removed came straight back on the response.
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
        // National identity (round 3, lane C1)
        dto.NationalIdTypeId     = c.NationalIdTypeId;
        dto.NationalIdTypeName   = c.NationalIdTypeRef?.Name;
        dto.NationalIdNumber     = c.NationalIdNumber;
        dto.NationalIdExpiryDate = c.NationalIdExpiryDate;
        // Documents
        dto.CvFilePath       = c.CvFilePath;
        dto.ProfilePhotoUrl  = c.ProfilePhotoUrl;
        dto.IsInTalentPool   = c.IsInTalentPool;

        dto.WorkHistories = c.WorkHistories.Where(w => !w.IsDeleted).Select(w => new JobCandidateWorkHistoryDto
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

        dto.Qualifications = c.Qualifications.Where(q => !q.IsDeleted).Select(q => new JobCandidateQualificationDto
        {
            Id                    = q.Id,
            JobCandidateId        = q.JobCandidateId,
            QualificationType     = q.QualificationType,
            QualificationName     = q.QualificationFreeText ?? q.Qualification?.Name ?? string.Empty,
            Institution           = q.Institution,
            DateAwarded           = q.DateAwarded,
            Grade                 = q.Grade,
        }).ToList();

        dto.Referees = c.Referees.Where(r => !r.IsDeleted).Select(r => new JobCandidateRefereeDto
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

        dto.Skills = c.Skills.Where(s => !s.IsDeleted).Select(s => new JobCandidateSkillDto
        {
            Id                = s.Id,
            JobCandidateId    = s.JobCandidateId,
            SkillName         = s.SkillName,
            Proficiency       = s.Proficiency,
            YearsOfExperience = s.YearsOfExperience,
            IsCertified       = s.IsCertified,
            CertificationName = s.CertificationName,
            CertificationNumber = s.CertificationNumber,
            CertifyingBody = s.CertifyingBody,
            CertificationExpiryDate = s.CertificationExpiryDate,
        }).ToList();

        dto.Languages = c.Languages.Where(l => !l.IsDeleted).Select(l => new JobCandidateLanguageDto
        {
            Id             = l.Id,
            JobCandidateId = l.JobCandidateId,
            LanguageId     = l.LanguageId,
            LanguageCode   = l.Language?.Code,
            LanguageName   = l.LanguageName,
            Proficiency    = l.Proficiency,
        }).ToList();

        dto.Interests = c.Interests.Where(i => !i.IsDeleted).Select(i => new JobCandidateInterestDto
        {
            Id             = i.Id,
            JobCandidateId = i.JobCandidateId,
            Detail         = i.Detail,
        }).ToList();

        dto.Documents = c.Documents.Where(d => !d.IsDeleted).Select(d => new JobCandidateDocumentDto
        {
            Id             = d.Id,
            TenantId       = d.TenantId,
            JobCandidateId = d.JobCandidateId,
            DocumentType   = d.DocumentType,
            FileName       = d.FileName,
            FilePath       = d.FilePath,
            UploadDate     = d.UploadDate,
            Description    = d.Description,
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
