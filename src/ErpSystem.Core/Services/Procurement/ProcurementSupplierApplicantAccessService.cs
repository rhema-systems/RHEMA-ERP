using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementSupplierApplicantAccessService :
    IProcurementSupplierApplicantAccessService
{
    private const string SourceType = "ProcurementSupplierApplicantAccess";
    private const string EventType = "ProcurementSupplierApplicantAccessControl";
    private const string ReviewPermission = "procurement.supplier.review";
    private const string ApprovePermission = "procurement.supplier.approve";
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToArray();
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcurementSupplierOnboardingTokenService _tokenService;
    private readonly IBusinessPartnerRegistrationService _registrationService;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly ICurrentUserProvider _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly INotificationService _notifications;
    private readonly SupplierApplicantAccessOptions _options;
    private readonly string _supplierLoginUrl;
    private readonly ILogger<ProcurementSupplierApplicantAccessService> _logger;

    public ProcurementSupplierApplicantAccessService(
        IUnitOfWork unitOfWork,
        IProcurementSupplierOnboardingTokenService tokenService,
        IBusinessPartnerRegistrationService registrationService,
        IProcurementControlEventService controlEvents,
        IProcurementAccessControlService accessControl,
        ICurrentUserProvider currentUser,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        INotificationService notifications,
        IConfiguration configuration,
        IOptions<SupplierApplicantAccessOptions> options,
        ILogger<ProcurementSupplierApplicantAccessService> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _registrationService = registrationService;
        _controlEvents = controlEvents;
        _accessControl = accessControl;
        _currentUser = currentUser;
        _userManager = userManager;
        _roleManager = roleManager;
        _notifications = notifications;
        _options = options.Value;
        _supplierLoginUrl = BuildSupplierLoginUrl(configuration["FrontendUrl"]);
        _logger = logger;
    }

    private IGenericRepository<ProcurementSupplierApplicantAccess> Accesses =>
        _unitOfWork.Repository<ProcurementSupplierApplicantAccess>();
    private IGenericRepository<ProcurementSupplierApplicantSession> Sessions =>
        _unitOfWork.Repository<ProcurementSupplierApplicantSession>();
    private IGenericRepository<BusinessPartnerRegistration> Registrations =>
        _unitOfWork.Repository<BusinessPartnerRegistration>();
    private IGenericRepository<ProcurementSupplierOnboardingToken> Tokens =>
        _unitOfWork.Repository<ProcurementSupplierOnboardingToken>();
    private IGenericRepository<BusinessPartner> BusinessPartners =>
        _unitOfWork.Repository<BusinessPartner>();
    private IGenericRepository<BusinessPartnerUser> BusinessPartnerUsers =>
        _unitOfWork.Repository<BusinessPartnerUser>();
    private IGenericRepository<UserTenant> UserTenants =>
        _unitOfWork.Repository<UserTenant>();

    public async Task<SupplierApplicantVerificationPreparationDto>
        PrepareVerificationChallengeAsync(
            Guid tenantId,
            ProcurementSupplierApplicantVerificationChannel channel,
            string contact,
            CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw Error("SUPPLIER_APPLICANT_TENANT_REQUIRED", "A tenant is required.", 400);

        var normalizedContact = SupplierApplicantContactNormalizer.Normalize(channel, contact);
        ValidateContact(channel, normalizedContact);
        var contactHashes = SupplierApplicantContactNormalizer
            .GetLookupAliases(channel, normalizedContact)
            .Select(Hash)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existingMatches = await Accesses.GetQueryable(item =>
                item.TenantId == tenantId &&
                !item.IsDeleted &&
                contactHashes.Contains(item.VerifiedContactHashSha256) &&
                item.Status != ProcurementSupplierApplicantAccessStatus.Rejected)
            .Include(item => item.Registration)
            .Include(item => item.Token)
            .OrderByDescending(item => item.VerifiedAtUtc)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (existingMatches.Count > 1)
            throw Error(
                "SUPPLIER_APPLICANT_CONTACT_AMBIGUOUS",
                "This contact is linked to more than one supplier record. Contact procurement support before continuing.",
                409);

        var resumable = existingMatches.SingleOrDefault();
        if (resumable is not null)
        {
            var registrationIsResumable =
                string.Equals(resumable.Registration.Status, "Draft", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resumable.Registration.Status, "MoreInfoRequired", StringComparison.OrdinalIgnoreCase);
            var canResume =
                resumable.Status == ProcurementSupplierApplicantAccessStatus.ApplicationInProgress &&
                !resumable.TerminalAtUtc.HasValue &&
                resumable.Token.Status != ProcurementSupplierOnboardingTokenStatus.Expired &&
                registrationIsResumable;
            if (!canResume)
                throw Error(
                    "SUPPLIER_APPLICANT_CONTACT_ALREADY_REGISTERED",
                    "This contact is already assigned to a supplier or completed application. Sign in with the existing account or contact procurement support.",
                    409);

            return new SupplierApplicantVerificationPreparationDto
            {
                TenantId = tenantId,
                Channel = channel,
                NormalizedContact = normalizedContact,
                MaskedContact = MaskContact(channel, normalizedContact),
                ResumesExistingApplication = true
            };
        }

        if (await FindConflictingIdentityAsync(
                tenantId, channel, normalizedContact, cancellationToken) is not null)
            throw Error(
                "SUPPLIER_APPLICANT_CONTACT_ALREADY_REGISTERED",
                "This contact is already assigned to an ERP account. Sign in with that account or use a separate supplier contact.",
                409);

        return new SupplierApplicantVerificationPreparationDto
        {
            TenantId = tenantId,
            Channel = channel,
            NormalizedContact = normalizedContact,
            MaskedContact = MaskContact(channel, normalizedContact),
            ResumesExistingApplication = false
        };
    }

    public async Task<SupplierApplicantTokenIssueDto> CreateVerifiedApplicationAsync(
        VerifyAndIssueSupplierApplicantTokenRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                var result = await CreateVerifiedApplicationCoreAsync(
                    request,
                    correlationId,
                    cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateException exception)
                when (IsActiveContactUniquenessViolation(exception))
            {
                if (_unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                // Another verified request may have won the active-contact race.
                // Re-read in a new serializable transaction and recover that
                // application instead of exposing a transient duplicate error.
                await _unitOfWork.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                try
                {
                    var resumed = await CreateVerifiedApplicationCoreAsync(
                        request,
                        correlationId,
                        cancellationToken);
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return resumed;
                }
                catch
                {
                    if (_unitOfWork.HasActiveTransaction)
                        await _unitOfWork.RollbackAsync(cancellationToken);
                    _unitOfWork.ClearTrackedChanges();
                    throw;
                }
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private async Task<SupplierApplicantTokenIssueDto> CreateVerifiedApplicationCoreAsync(
        VerifyAndIssueSupplierApplicantTokenRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty)
            throw Error("SUPPLIER_APPLICANT_TENANT_REQUIRED", "A tenant is required.", 400);
        var contact = SupplierApplicantContactNormalizer.Normalize(
            request.Channel, request.Contact);
        ValidateContact(request.Channel, contact);
        var correlation = NormalizeCorrelation(correlationId);
        var contactHash = Hash(contact);
        var contactHashes = SupplierApplicantContactNormalizer
            .GetLookupAliases(request.Channel, contact)
            .Select(Hash)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existingMatches = await Accesses.GetQueryable(item =>
                item.TenantId == request.TenantId && !item.IsDeleted &&
                contactHashes.Contains(item.VerifiedContactHashSha256) &&
                item.Status != ProcurementSupplierApplicantAccessStatus.Rejected &&
                item.Status != ProcurementSupplierApplicantAccessStatus.Activated)
            .Include(item => item.Registration)
            .Include(item => item.Token)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (existingMatches.Count > 1)
            throw Error(
                "SUPPLIER_APPLICANT_CONTACT_AMBIGUOUS",
                "More than one active supplier application is bound to this verified contact. Contact procurement support before continuing.",
                409);
        if (existingMatches.Count == 1)
        {
            return await ResumeVerifiedApplicationAsync(
                existingMatches[0], request, correlation, cancellationToken);
        }

        if (await FindConflictingIdentityAsync(
                request.TenantId,
                request.Channel,
                contact,
                cancellationToken) is not null)
        {
            throw Error(
                "SUPPLIER_APPLICANT_CONTACT_ALREADY_REGISTERED",
                "This contact is already assigned to an ERP account. Sign in with that account or use a separate supplier contact.",
                409);
        }

        // A verified applicant is not an ERP user until approval. Use the
        // applicant-access identity as the external actor instead of borrowing
        // an administrator account for ownership and audit lineage.
        var applicantActorId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var retainedApplication = request.RetainedRegistrationId.HasValue;
        Guid? originalCreatedById = null;
        BusinessPartnerRegistration registration;
        if (retainedApplication)
        {
            var retainedRegistrationId = request.RetainedRegistrationId.GetValueOrDefault();
            registration = await Registrations.GetQueryable(item =>
                    item.Id == retainedRegistrationId &&
                    item.TenantId == request.TenantId &&
                    !item.IsDeleted)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw RetainedApplicationNotEligible();
            if (registration.PartnerType != "Supplier" ||
                registration.Status is not ("Draft" or "MoreInfoRequired") ||
                !VerifiedContactMatchesRegistration(
                    registration, request.Channel, contact))
            {
                throw RetainedApplicationNotEligible();
            }

            var alreadyBound = await Accesses.GetQueryable(item =>
                    item.TenantId == request.TenantId &&
                    item.RegistrationId == registration.Id &&
                    !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (alreadyBound)
                throw RetainedApplicationNotEligible();

            originalCreatedById = registration.CreatedById;
        }
        else
        {
            var companyName = request.CompanyName?.Trim();
            if (string.IsNullOrWhiteSpace(companyName))
                throw Error("SUPPLIER_APPLICANT_COMPANY_REQUIRED",
                    "A company name is required for a new supplier application.", 400);
            registration = new BusinessPartnerRegistration
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                RegistrationNumber = $"APP{now:yy}{Guid.NewGuid():N}"[..13].ToUpperInvariant(),
                ApplicantName = companyName,
                ApplicantEmail = request.Channel == ProcurementSupplierApplicantVerificationChannel.Email
                    ? contact : null,
                ApplicantPhone = request.Channel == ProcurementSupplierApplicantVerificationChannel.Sms
                    ? contact : null,
                PartnerType = "Supplier",
                RegistrationCategory = request.RegistrationCategory,
                Status = "Draft",
                RegistrationDataJson = JsonSerializer.Serialize(new
                {
                    companyName,
                    partnerType = "Supplier",
                    registrationCategory = request.RegistrationCategory.ToString(),
                    email = request.Channel == ProcurementSupplierApplicantVerificationChannel.Email
                        ? contact : null,
                    phone = request.Channel == ProcurementSupplierApplicantVerificationChannel.Sms
                        ? contact : null
                }, JsonOptions),
                CreatedAt = now,
                CreatedBy = "Verified Supplier Applicant",
                CreatedById = applicantActorId
            };
            await Registrations.AddAsync(registration);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        ProcurementSupplierOnboardingTokenIssueResultDto issued;
        try
        {
            issued = await _tokenService.IssueForVerifiedApplicantAsync(
                request.TenantId,
                registration.Id,
                correlation,
                cancellationToken);
        }
        catch
        {
            if (!retainedApplication)
            {
                registration.IsDeleted = true;
                registration.DeletedAt = DateTime.UtcNow;
                registration.DeletedBy = "Verified Supplier Applicant";
                await Registrations.UpdateAsync(registration);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            throw;
        }

        var access = new ProcurementSupplierApplicantAccess
        {
            Id = applicantActorId,
            TenantId = request.TenantId,
            RegistrationId = registration.Id,
            TokenId = issued.Token.Id,
            VerifiedChannel = request.Channel,
            VerifiedContactHashSha256 = contactHash,
            VerifiedContactMasked = MaskContact(request.Channel, contact),
            VerifiedContact = contact,
            VerifiedAtUtc = now,
            Status = ProcurementSupplierApplicantAccessStatus.ApplicationInProgress,
            CreatedAt = now,
            CreatedBy = "Verified Supplier Applicant",
            CreatedById = applicantActorId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        Capture(access);
        await Accesses.AddAsync(access);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordSystemEventAsync(
            access,
            issued.Token,
            "ApplicantContactVerified",
            ProcurementControlEventResult.Succeeded,
            new
            {
                Channel = request.Channel.ToString(),
                access.VerifiedContactMasked,
                registration.RegistrationNumber,
                RetainedApplicationMigrated = retainedApplication,
                OriginalCreatedById = originalCreatedById,
                TokenStatus = issued.Token.Status.ToString(),
                PaymentStatus = issued.Token.PaymentStatus.ToString()
            },
            retainedApplication
                ? "The retained supplier draft was migrated to verified applicant-token access."
                : "The applicant contact was verified and bound to one application token.",
            correlation,
            cancellationToken);

        SupplierApplicantSessionDto? restrictedSession = null;
        if (issued.Token.Status ==
            ProcurementSupplierOnboardingTokenStatus.AwaitingPayment)
        {
            restrictedSession = await CreateSessionCoreAsync(
                access,
                issued.Token,
                correlation,
                "ApplicantPaymentSessionStarted",
                cancellationToken);
        }

        return new SupplierApplicantTokenIssueDto
        {
            RegistrationId = registration.Id,
            RegistrationNumber = registration.RegistrationNumber,
            TokenId = issued.Token.Id,
            TokenReference = issued.Token.TokenReference,
            PlaintextToken = issued.PlaintextToken,
            FeeMode = issued.Token.FeeMode,
            TokenStatus = issued.Token.Status,
            PaymentStatus = issued.Token.PaymentStatus,
            TotalAmount = issued.Token.TotalAmount,
            CurrencyCode = issued.Token.CurrencyCode,
            ResumedExistingApplication = false,
            RestrictedSession = restrictedSession
        };
    }

    private async Task<SupplierApplicantTokenIssueDto> ResumeVerifiedApplicationAsync(
        ProcurementSupplierApplicantAccess access,
        VerifyAndIssueSupplierApplicantTokenRequest request,
        string correlation,
        CancellationToken cancellationToken)
    {
        if (request.RetainedRegistrationId.HasValue &&
            request.RetainedRegistrationId.Value != access.RegistrationId)
            throw RetainedApplicationNotEligible();

        var registrationTerminal =
            string.Equals(access.Registration.Status, "Approved",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(access.Registration.Status, "Rejected",
                StringComparison.OrdinalIgnoreCase);
        if (access.Status != ProcurementSupplierApplicantAccessStatus.ApplicationInProgress ||
            access.TerminalAtUtc.HasValue ||
            registrationTerminal ||
            access.Token.Status == ProcurementSupplierOnboardingTokenStatus.Expired)
        {
            throw Error(
                "SUPPLIER_APPLICANT_APPLICATION_NOT_RESUMABLE",
                "This supplier application is no longer eligible for verified-contact recovery.",
                409);
        }

        var now = DateTime.UtcNow;
        var activeSessions = await Sessions.GetQueryable(item =>
                item.TenantId == access.TenantId &&
                item.ApplicantAccessId == access.Id &&
                !item.IsDeleted &&
                item.Status == ProcurementSupplierApplicantSessionStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var activeSession in activeSessions)
        {
            activeSession.Status = ProcurementSupplierApplicantSessionStatus.Revoked;
            activeSession.RevokedAtUtc = now;
            activeSession.RevocationReason =
                "Replaced after the applicant re-verified the application contact.";
            Touch(activeSession);
            Capture(activeSession);
            await Sessions.UpdateAsync(activeSession);
        }

        var token = MapToken(access.Token);
        var session = await CreateSessionCoreAsync(
            access,
            token,
            correlation,
            "ApplicantVerifiedSessionResumed",
            cancellationToken);

        return new SupplierApplicantTokenIssueDto
        {
            RegistrationId = access.RegistrationId,
            RegistrationNumber = access.Registration.RegistrationNumber,
            TokenId = access.TokenId,
            TokenReference = access.Token.TokenReference,
            PlaintextToken = null,
            FeeMode = access.Token.FeeMode,
            TokenStatus = access.Token.Status,
            PaymentStatus = access.Token.PaymentStatus,
            TotalAmount = access.Token.TotalAmount,
            CurrencyCode = access.Token.CurrencyCode,
            ResumedExistingApplication = true,
            RestrictedSession = session
        };
    }

    private static bool IsActiveContactUniquenessViolation(
        DbUpdateException exception)
    {
        var detail = exception.ToString();
        return detail.Contains(
                   "UX_ProcurementSupplierApplicantAccesses_ActiveContact",
                   StringComparison.OrdinalIgnoreCase) ||
               (detail.Contains(
                    "ProcurementSupplierApplicantAccesses",
                    StringComparison.OrdinalIgnoreCase) &&
                detail.Contains(
                    "VerifiedContactHashSha256",
                    StringComparison.OrdinalIgnoreCase) &&
                detail.Contains(
                    "TenantId",
                    StringComparison.OrdinalIgnoreCase) &&
                detail.Contains(
                    "UNIQUE",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool VerifiedContactMatchesRegistration(
        BusinessPartnerRegistration registration,
        ProcurementSupplierApplicantVerificationChannel channel,
        string verifiedContact)
    {
        var recordedContact = channel == ProcurementSupplierApplicantVerificationChannel.Email
            ? registration.ApplicantEmail
            : registration.ApplicantPhone;
        if (string.IsNullOrWhiteSpace(recordedContact))
            return false;
        return string.Equals(
            SupplierApplicantContactNormalizer.Normalize(channel, recordedContact),
            verifiedContact,
            StringComparison.Ordinal);
    }

    private static ProcurementSupplierApplicantAccessException RetainedApplicationNotEligible() =>
        Error(
            "SUPPLIER_APPLICANT_RETAINED_APPLICATION_NOT_ELIGIBLE",
            "The retained application cannot be migrated with the verified contact.",
            409);

    public async Task<SupplierApplicantSessionDto> StartSessionAsync(
        StartSupplierApplicantSessionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var token = await _tokenService.ValidateApplicantTokenAsync(
            request.TenantId,
            request.ApplicationToken,
            correlation,
            cancellationToken);
        var access = await Accesses.GetQueryable(item =>
                item.TenantId == request.TenantId && item.TokenId == token.Id && !item.IsDeleted)
            .Include(item => item.Registration)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error("SUPPLIER_APPLICANT_ACCESS_NOT_FOUND",
                "The verified applicant access record was not found.", 404);
        if (access.TerminalAtUtc.HasValue)
            throw Error("SUPPLIER_APPLICANT_APPLICATION_COMPLETE",
                "This application is complete and cannot start another applicant session.", 401);

        return await CreateSessionCoreAsync(
            access,
            token,
            correlation,
            "ApplicantSessionStarted",
            cancellationToken);
    }

    private async Task<SupplierApplicantSessionDto> CreateSessionCoreAsync(
        ProcurementSupplierApplicantAccess access,
        ProcurementSupplierOnboardingTokenDto token,
        string correlation,
        string action,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var session = new ProcurementSupplierApplicantSession
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            ApplicantAccessId = access.Id,
            SessionReference = Guid.NewGuid(),
            Status = ProcurementSupplierApplicantSessionStatus.Active,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(Math.Clamp(
                _options.ApplicantSessionMinutes, 15, 1440)),
            LastUsedAtUtc = now,
            CreatedAt = now,
            CreatedBy = "Verified Supplier Applicant",
            CreatedById = access.Id,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        Capture(session);
        await Sessions.AddAsync(session);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordSystemEventAsync(
            access,
            token,
            action,
            ProcurementControlEventResult.Allowed,
            new
            {
                session.SessionReference,
                session.ExpiresAtUtc,
                PaymentOnly = token.Status == ProcurementSupplierOnboardingTokenStatus.AwaitingPayment
            },
            "A restricted applicant-only session was started.",
            correlation,
            cancellationToken);
        return MapSession(session, access, token);
    }

    public async Task<SupplierApplicantTokenDeliveryDto> DeliverApplicationTokenAsync(
        Guid tokenId,
        string plaintextToken,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plaintextToken))
            throw Error(
                "SUPPLIER_APPLICANT_TOKEN_SECRET_REQUIRED",
                "An application-token secret is required for delivery.",
                400);

        var access = await Accesses.GetQueryable(item =>
                item.TokenId == tokenId && !item.IsDeleted)
            .Include(item => item.Registration)
            .Include(item => item.Token)
            .SingleOrDefaultAsync(cancellationToken);
        if (access is null)
        {
            return new SupplierApplicantTokenDeliveryDto
            {
                ApplicantAccessFound = false,
                Delivered = false,
                Status = "NotApplicable"
            };
        }

        if (access.TerminalAtUtc.HasValue ||
            access.Token.Status != ProcurementSupplierOnboardingTokenStatus.Active)
        {
            throw Error(
                "SUPPLIER_APPLICANT_TOKEN_NOT_ACTIVE",
                "The application token can be delivered only after activation and before application completion.");
        }
        if (!MatchesSha256Secret(plaintextToken, access.Token.TokenHashSha256))
        {
            throw Error(
                "SUPPLIER_APPLICANT_TOKEN_SECRET_INVALID",
                "The application-token secret does not match the active token.",
                400);
        }

        var correlation = NormalizeCorrelation(correlationId);
        var now = DateTime.UtcNow;
        try
        {
            var message =
                $"Supplier application {access.Registration.RegistrationNumber}. " +
                $"Application token: {plaintextToken}. " +
                "Keep this token secure; it remains valid until the application is approved or rejected.";
            if (access.VerifiedChannel ==
                ProcurementSupplierApplicantVerificationChannel.Email)
            {
                await _notifications.SendEmailAsync(
                    access.VerifiedContact,
                    "Your supplier application token",
                    $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>",
                    isHtml: true);
            }
            else
            {
                await _notifications.SendSmsAsync(access.VerifiedContact, message);
            }

            access.NotificationAttemptCount++;
            access.LastNotificationAtUtc = now;
            access.LastNotificationStatus = "ApplicationTokenSent";
            access.LastNotificationFailure = null;
            Touch(access);
            Capture(access);
            await Accesses.UpdateAsync(access);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordApplicationTokenDeliveryEventAsync(
                access,
                "ApplicationTokenDelivered",
                ProcurementControlEventResult.Succeeded,
                "The application token was delivered through the verified channel.",
                correlation,
                cancellationToken);
            return new SupplierApplicantTokenDeliveryDto
            {
                ApplicantAccessFound = true,
                Delivered = true,
                Status = "Sent"
            };
        }
        catch (Exception exception)
        {
            access.NotificationAttemptCount++;
            access.LastNotificationAtUtc = DateTime.UtcNow;
            access.LastNotificationStatus = "ApplicationTokenDeliveryFailed";
            access.LastNotificationFailure = Trim(exception.Message, 1000);
            Touch(access);
            Capture(access);
            await Accesses.UpdateAsync(access);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordApplicationTokenDeliveryEventAsync(
                access,
                "ApplicationTokenDeliveryFailed",
                ProcurementControlEventResult.Failed,
                "Application-token delivery failed; no token value was logged.",
                correlation,
                cancellationToken);
            _logger.LogWarning(
                "Supplier application-token delivery failed for applicant access {AccessId}",
                access.Id);
            return new SupplierApplicantTokenDeliveryDto
            {
                ApplicantAccessFound = true,
                Delivered = false,
                Status = "Failed",
                FailureMessage =
                    "Token delivery failed. An authorized operator can reissue it to retry delivery."
            };
        }
    }

    private async Task RecordApplicationTokenDeliveryEventAsync(
        ProcurementSupplierApplicantAccess access,
        string action,
        ProcurementControlEventResult result,
        string reason,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var after = new
        {
            Channel = access.VerifiedChannel.ToString(),
            access.VerifiedContactMasked,
            access.NotificationAttemptCount,
            access.LastNotificationStatus
        };
        if (_currentUser.IsAuthenticated && !_currentUser.IsExternalUser)
        {
            await RecordUserEventAsync(
                access, access.Token, action, result, after, reason,
                correlationId, cancellationToken);
            return;
        }

        await RecordSystemEventAsync(
            access, access.Token, action, result, after, reason,
            correlationId, cancellationToken);
    }

    public async Task<SupplierApplicantSessionDto> ValidateSessionAsync(
        Guid sessionReference,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var session = await Sessions.GetQueryable(item =>
                item.SessionReference == sessionReference && !item.IsDeleted)
            .Include(item => item.ApplicantAccess)
                .ThenInclude(item => item.Registration)
            .Include(item => item.ApplicantAccess)
                .ThenInclude(item => item.Token)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error("SUPPLIER_APPLICANT_SESSION_INVALID",
                "The applicant session is invalid.", 401);
        if (_currentUser.IsAuthenticated &&
            (_currentUser.TenantId != session.TenantId ||
             !_currentUser.IsExternalUser))
            throw Error("SUPPLIER_APPLICANT_SESSION_TENANT_MISMATCH",
                "The applicant session does not belong to this restricted tenant context.",
                403);
        var now = DateTime.UtcNow;
        if (session.Status != ProcurementSupplierApplicantSessionStatus.Active ||
            session.ExpiresAtUtc <= now ||
            session.ApplicantAccess.TerminalAtUtc.HasValue ||
            session.ApplicantAccess.Token.Status == ProcurementSupplierOnboardingTokenStatus.Expired)
        {
            if (session.Status == ProcurementSupplierApplicantSessionStatus.Active)
            {
                session.Status = ProcurementSupplierApplicantSessionStatus.Revoked;
                session.RevokedAtUtc = now;
                session.RevocationReason = "Expired or application completed.";
                Touch(session);
                Capture(session);
                await Sessions.UpdateAsync(session);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            throw Error("SUPPLIER_APPLICANT_SESSION_EXPIRED",
                "The applicant session has expired or the application is complete.", 401);
        }

        session.LastUsedAtUtc = now;
        Touch(session);
        Capture(session);
        await Sessions.UpdateAsync(session);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapSession(
            session,
            session.ApplicantAccess,
            MapToken(session.ApplicantAccess.Token));
    }

    public async Task<SupplierApplicantPortalDto> GetPortalAsync(
        Guid sessionReference,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var session = await ValidateSessionAsync(
            sessionReference, correlationId, cancellationToken);
        var detail = await _registrationService.GetByIdAsync(session.RegistrationId)
            ?? throw Error("SUPPLIER_APPLICANT_REGISTRATION_NOT_FOUND",
                "The supplier application was not found.", 404);
        var token = await _tokenService.GetForRegistrationAsync(
            session.RegistrationId, cancellationToken)
            ?? throw Error("SUPPLIER_APPLICANT_TOKEN_NOT_FOUND",
                "The supplier application token was not found.", 404);
        var portal = MapPortal(detail, token);
        var access = await LoadAccessAsync(session.RegistrationId, cancellationToken);
        await RecordSystemEventAsync(
            access,
            token,
            "ApplicantStatusAccessed",
            ProcurementControlEventResult.Allowed,
            new
            {
                detail.Status,
                TokenStatus = token.Status.ToString(),
                PaymentStatus = token.PaymentStatus.ToString()
            },
            "The applicant viewed current application progress.",
            NormalizeCorrelation(correlationId),
            cancellationToken);
        return portal;
    }

    public async Task<SupplierApplicantPortalDto> UpdateApplicationAsync(
        Guid sessionReference,
        UpdateSupplierApplicantApplicationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var session = await ValidateSessionAsync(
            sessionReference, correlationId, cancellationToken);
        if (session.PaymentOnly)
            throw Error("SUPPLIER_APPLICANT_PAYMENT_REQUIRED",
                "Payment or an approved exemption is required before editing the application.", 409);
        var access = await LoadAccessAsync(session.RegistrationId, cancellationToken);
        EnsureVerifiedContactUnchanged(access, request);
        var updated = await _registrationService.UpdateAsync(
            session.RegistrationId,
            new UpdateBusinessPartnerRegistrationDto
            {
                CompanyName = request.CompanyName.Trim(),
                RegistrationCategory = request.RegistrationCategory,
                Email = request.Email?.Trim(),
                Phone = request.Phone?.Trim(),
                RegistrationData = request.RegistrationData
            },
            session.ApplicantActorId);
        var token = await _tokenService.GetForRegistrationAsync(
            session.RegistrationId, cancellationToken)
            ?? throw Error("SUPPLIER_APPLICANT_TOKEN_NOT_FOUND",
                "The supplier application token was not found.", 404);
        await RecordSystemEventAsync(
            access,
            token,
            "ApplicantApplicationUpdated",
            ProcurementControlEventResult.Succeeded,
            new { updated.Status, updated.RegistrationCategory },
            "The verified applicant updated the draft application.",
            NormalizeCorrelation(correlationId),
            cancellationToken);
        return MapPortal(updated, token);
    }

    public async Task<SupplierApplicantPortalDto> SubmitAsync(
        Guid sessionReference,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var session = await ValidateSessionAsync(
            sessionReference, correlationId, cancellationToken);
        if (session.PaymentOnly)
            throw Error("SUPPLIER_APPLICANT_PAYMENT_REQUIRED",
                "Payment or an approved exemption is required before submission.", 409);
        await _registrationService.SubmitExternalApplicantForReviewAsync(
            session.RegistrationId,
            session.ApplicantActorId);
        var detail = await _registrationService.GetByIdAsync(session.RegistrationId)
            ?? throw Error("SUPPLIER_APPLICANT_REGISTRATION_NOT_FOUND",
                "The supplier application was not found.", 404);
        var token = await _tokenService.GetForRegistrationAsync(
            session.RegistrationId, cancellationToken)
            ?? throw Error("SUPPLIER_APPLICANT_TOKEN_NOT_FOUND",
                "The supplier application token was not found.", 404);
        var access = await LoadAccessAsync(session.RegistrationId, cancellationToken);
        await RecordSystemEventAsync(
            access,
            token,
            "ApplicantApplicationSubmitted",
            ProcurementControlEventResult.Succeeded,
            new { detail.Status, detail.EvidenceReadiness },
            "The verified applicant submitted the application for review.",
            NormalizeCorrelation(correlationId),
            cancellationToken);
        return MapPortal(detail, token);
    }

    public async Task CloseForTerminalRegistrationAsync(
        Guid registrationId,
        string terminalStatus,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalTenant();
        EnsureAuthenticatedActor(actorUserId);
        await EnsureApprovalPermissionAsync(
            registrationId, correlationId, cancellationToken);
        if (terminalStatus is not ("Approved" or "Rejected"))
            throw Error("SUPPLIER_APPLICANT_TERMINAL_STATUS_INVALID",
                "Applicant access closes only for Approved or Rejected applications.", 400);
        var access = await Accesses.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.RegistrationId == registrationId && !item.IsDeleted)
            .Include(item => item.Token)
            .Include(item => item.Sessions)
            .SingleOrDefaultAsync(cancellationToken);
        if (access is null) return;
        if (access.TerminalAtUtc.HasValue &&
            string.Equals(access.TerminalOutcome, terminalStatus,
                StringComparison.OrdinalIgnoreCase))
            return;

        var now = DateTime.UtcNow;
        access.TerminalAtUtc = now;
        access.TerminalOutcome = terminalStatus;
        access.Status = terminalStatus == "Rejected"
            ? ProcurementSupplierApplicantAccessStatus.Rejected
            : ProcurementSupplierApplicantAccessStatus.ApprovedPendingCredentialDelivery;
        foreach (var session in access.Sessions.Where(item =>
                     item.Status == ProcurementSupplierApplicantSessionStatus.Active))
        {
            session.Status = ProcurementSupplierApplicantSessionStatus.Revoked;
            session.RevokedAtUtc = now;
            session.RevocationReason = $"Application {terminalStatus}.";
            Touch(session);
            Capture(session);
            await Sessions.UpdateAsync(session);
        }
        Touch(access);
        Capture(access);
        await Accesses.UpdateAsync(access);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordUserEventAsync(
            access,
            access.Token,
            "ApplicantAccessTerminallyClosed",
            ProcurementControlEventResult.Succeeded,
            new { terminalStatus, ClosedAtUtc = now },
            $"Applicant sessions closed because the application was {terminalStatus}.",
            NormalizeCorrelation(correlationId),
            cancellationToken);
    }

    public async Task ValidateApprovedSupplierProvisioningAsync(
        Guid registrationId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalTenant();
        EnsureAuthenticatedActor(actorUserId);
        await EnsureApprovalPermissionAsync(
            registrationId, correlationId, cancellationToken);

        var access = await Accesses.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.RegistrationId == registrationId &&
                !item.IsDeleted)
            .Include(item => item.Registration)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (access is null)
            return;

        var roles = NormalizeApprovedRoles();
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                throw Error(
                    "SUPPLIER_APPLICANT_ROLE_NOT_CONFIGURED",
                    $"The approved supplier role '{role}' is not configured.",
                    409);
            }
        }
        _ = NormalizeBusinessPartnerRole();

        var existing = await FindConflictingIdentityAsync(
            access.TenantId,
            access.VerifiedChannel,
            access.VerifiedContact,
            cancellationToken);
        if (existing is not null && access.ApprovedUserId != existing.Id)
        {
            throw LoginAlreadyExists();
        }
    }

    public async Task ProvisionApprovedSupplierAsync(
        Guid registrationId,
        Guid businessPartnerId,
        Guid approvedById,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalTenant();
        EnsureAuthenticatedActor(approvedById);
        await EnsureApprovalPermissionAsync(
            registrationId, correlationId, cancellationToken);
        var access = await Accesses.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.RegistrationId == registrationId && !item.IsDeleted)
            .Include(item => item.Registration)
            .Include(item => item.Token)
            .SingleOrDefaultAsync(cancellationToken);
        if (access is null) return;
        if (!string.Equals(access.Registration.Status, "Approved",
                StringComparison.OrdinalIgnoreCase) ||
            access.Registration.BusinessPartnerId != businessPartnerId)
            throw Error("SUPPLIER_APPLICANT_APPROVAL_NOT_CURRENT",
                "The approved supplier-account subject is not current.", 409);
        var businessPartner = await BusinessPartners.GetQueryable(item =>
                item.Id == businessPartnerId &&
                item.TenantId == access.TenantId &&
                !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error(
                "SUPPLIER_APPLICANT_BUSINESS_PARTNER_NOT_FOUND",
                "The approved business partner was not found in the current tenant.",
                409);
        if (access.ApprovedUserId.HasValue &&
            access.Status is ProcurementSupplierApplicantAccessStatus.CredentialDelivered or
                ProcurementSupplierApplicantAccessStatus.Activated)
        {
            var approvedUserId = access.ApprovedUserId.Value;
            var approvedUser = await _userManager.FindByIdAsync(approvedUserId.ToString())
                ?? throw Error(
                    "SUPPLIER_APPLICANT_ACCOUNT_NOT_FOUND",
                    "The provisioned supplier account was not found.",
                    409);
            EnsureApprovedUserTenant(approvedUser, access);
            var repaired = await EnsureApprovedSupplierIdentityLinksAsync(
                businessPartner,
                approvedUserId,
                approvedById,
                DateTime.UtcNow,
                cancellationToken);
            if (repaired)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordUserEventAsync(
                    access,
                    access.Token,
                    "SupplierAccountLinkReconciled",
                    ProcurementControlEventResult.Succeeded,
                    new
                    {
                        UserId = approvedUserId,
                        BusinessPartnerId = businessPartner.Id,
                        PrimaryPortalUserLinked = true,
                        BusinessPartnerMembershipLinked = true
                    },
                    "The approved supplier account links were reconciled idempotently.",
                    NormalizeCorrelation(correlationId),
                    cancellationToken);
            }
            return;
        }

        await CloseForTerminalRegistrationAsync(
            registrationId,
            "Approved",
            approvedById,
            $"{correlationId}-close",
            cancellationToken);
        access = await LoadAccessAsync(registrationId, cancellationToken, tracked: true);
        var roles = NormalizeApprovedRoles();
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                throw Error("SUPPLIER_APPLICANT_ROLE_NOT_CONFIGURED",
                    $"The approved supplier role '{role}' is not configured.", 409);
        }
        var loginIdentifier = BuildLoginIdentifier(access);
        var existing = await FindConflictingIdentityAsync(
            access.TenantId,
            access.VerifiedChannel,
            access.VerifiedContact,
            cancellationToken);
        var resumedPartialIdentity = existing is not null &&
            access.ApprovedUserId != existing.Id &&
            await IsRecoverablePartialSupplierIdentityAsync(
                existing,
                access,
                roles,
                cancellationToken);
        if (existing is not null &&
            access.ApprovedUserId != existing.Id &&
            !resumedPartialIdentity)
            throw LoginAlreadyExists();

        var temporaryPassword = GenerateTemporaryPassword();
        var now = DateTime.UtcNow;
        var expiry = now.AddDays(Math.Clamp(
            _options.TemporaryPasswordExpiryDays, 1, 30));
        ApplicationUser user;
        if (access.ApprovedUserId.HasValue || resumedPartialIdentity)
        {
            user = resumedPartialIdentity
                ? existing!
                : await _userManager.FindByIdAsync(
                    access.ApprovedUserId!.Value.ToString())
                  ?? throw Error("SUPPLIER_APPLICANT_ACCOUNT_NOT_FOUND",
                      "The provisioned supplier account was not found.", 409);
            EnsureApprovedUserTenant(user, access);
            var assignedRoles = await _userManager.GetRolesAsync(user);
            var missingRoles = roles.Except(
                    assignedRoles,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missingRoles.Length > 0)
            {
                EnsureIdentitySucceeded(
                    await _userManager.AddToRolesAsync(user, missingRoles));
            }
            var reset = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(
                user, reset, temporaryPassword);
            EnsureIdentitySucceeded(resetResult);
        }
        else
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = loginIdentifier,
                Email = access.VerifiedChannel ==
                    ProcurementSupplierApplicantVerificationChannel.Email
                    ? access.VerifiedContact : null,
                PhoneNumber = access.VerifiedChannel ==
                    ProcurementSupplierApplicantVerificationChannel.Sms
                    ? access.VerifiedContact : access.Registration.ApplicantPhone,
                EmailConfirmed = access.VerifiedChannel ==
                    ProcurementSupplierApplicantVerificationChannel.Email,
                PhoneNumberConfirmed = access.VerifiedChannel ==
                    ProcurementSupplierApplicantVerificationChannel.Sms,
                FirstName = access.Registration.ApplicantName,
                LastName = "Supplier",
                TenantId = access.TenantId,
                IsActive = true,
                AuthenticationProvider = AuthenticationProvider.Local,
                MustChangePassword = true,
                TemporaryPasswordExpiresAtUtc = expiry,
                CreatedAt = now,
                CreatedBy = approvedById.ToString()
            };
            EnsureIdentitySucceeded(await _userManager.CreateAsync(user, temporaryPassword));
            EnsureIdentitySucceeded(await _userManager.AddToRolesAsync(user, roles));
        }

        user.MustChangePassword = true;
        user.TemporaryPasswordExpiresAtUtc = expiry;
        user.PasswordChangedAtUtc = null;
        EnsureIdentitySucceeded(await _userManager.UpdateAsync(user));

        if (!await UserTenants.GetQueryable(item =>
                item.UserId == user.Id && item.TenantId == access.TenantId &&
                !item.IsDeleted).AnyAsync(cancellationToken))
        {
            await UserTenants.AddAsync(new UserTenant
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = access.TenantId,
                AccessLevel = UserTenantAccessLevel.Standard,
                Status = UserTenantStatus.Active,
                IsDefault = true,
                GrantedAt = now,
                GrantedBy = approvedById.ToString(),
                CreatedAt = now,
                CreatedBy = approvedById.ToString(),
                CreatedById = approvedById
            });
        }
        await EnsureApprovedSupplierIdentityLinksAsync(
            businessPartner,
            user.Id,
            approvedById,
            now,
            cancellationToken);

        access.ApprovedUserId = user.Id;
        access.BusinessPartnerId = businessPartnerId;
        access.ApprovedIdentityRolesJson = JsonSerializer.Serialize(roles, JsonOptions);
        access.ApprovedBusinessPartnerRole = NormalizeBusinessPartnerRole();
        access.LoginIdentifier = loginIdentifier;
        access.TemporaryCredentialIssuedAtUtc = now;
        access.TemporaryCredentialExpiresAtUtc = expiry;
        access.Status =
            ProcurementSupplierApplicantAccessStatus.ApprovedPendingCredentialDelivery;
        Touch(access);
        Capture(access);
        await Accesses.UpdateAsync(access);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (resumedPartialIdentity)
        {
            await RecordUserEventAsync(
                access,
                access.Token,
                "SupplierAccountProvisioningResumed",
                ProcurementControlEventResult.Succeeded,
                new
                {
                    UserId = user.Id,
                    BusinessPartnerId = businessPartner.Id,
                    Roles = roles,
                    RecoveredFromPartialIdentity = true
                },
                "A strictly matched, unbound partial supplier identity was reconciled during activation retry.",
                NormalizeCorrelation(correlationId),
                cancellationToken);
        }
        await RecordUserEventAsync(
            access,
            access.Token,
            "SupplierAccountProvisioned",
            ProcurementControlEventResult.Succeeded,
            new
            {
                UserId = user.Id,
                Roles = roles,
                BusinessPartnerRole = access.ApprovedBusinessPartnerRole,
                BusinessPartnerId = businessPartner.Id,
                PrimaryPortalUserLinked = businessPartner.UserId == user.Id,
                access.TemporaryCredentialExpiresAtUtc,
                MustChangePassword = true
            },
            "The approved supplier account was provisioned with only configured roles.",
            NormalizeCorrelation(correlationId),
            cancellationToken);
        await DeliverCredentialAsync(
            access, temporaryPassword, resend: false, correlationId, cancellationToken);
    }

    private async Task<bool> EnsureApprovedSupplierIdentityLinksAsync(
        BusinessPartner businessPartner,
        Guid approvedUserId,
        Guid approvedById,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var changed = false;
        if (businessPartner.UserId != approvedUserId)
        {
            businessPartner.UserId = approvedUserId;
            businessPartner.UpdatedAt = now;
            businessPartner.UpdatedBy = approvedById.ToString();
            businessPartner.LastModifiedById = approvedById;
            await BusinessPartners.UpdateAsync(businessPartner);
            changed = true;
        }

        var role = NormalizeBusinessPartnerRole();
        var membership = await BusinessPartnerUsers
            .GetQueryableIncludingDeleted(item =>
                item.TenantId == businessPartner.TenantId &&
                item.BusinessPartnerId == businessPartner.Id &&
                item.UserId == approvedUserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null)
        {
            await BusinessPartnerUsers.AddAsync(new BusinessPartnerUser
            {
                Id = Guid.NewGuid(),
                TenantId = businessPartner.TenantId,
                BusinessPartnerId = businessPartner.Id,
                UserId = approvedUserId,
                Role = role,
                IsActive = true,
                GrantedAt = now,
                GrantedById = approvedById,
                Notes = "Provisioned from approved token-gated supplier application.",
                CreatedAt = now,
                CreatedBy = approvedById.ToString(),
                CreatedById = approvedById
            });
            return true;
        }

        if (membership.IsDeleted ||
            !membership.IsActive ||
            !string.Equals(membership.Role, role, StringComparison.Ordinal))
        {
            membership.IsDeleted = false;
            membership.DeletedAt = null;
            membership.DeletedBy = null;
            membership.IsActive = true;
            membership.Role = role;
            membership.GrantedAt = now;
            membership.GrantedById = approvedById;
            membership.UpdatedAt = now;
            membership.UpdatedBy = approvedById.ToString();
            membership.LastModifiedById = approvedById;
            membership.Notes =
                "Provisioned from approved token-gated supplier application.";
            await BusinessPartnerUsers.UpdateAsync(membership);
            changed = true;
        }

        return changed;
    }

    private async Task<bool> IsRecoverablePartialSupplierIdentityAsync(
        ApplicationUser user,
        ProcurementSupplierApplicantAccess access,
        IReadOnlyCollection<string> approvedRoles,
        CancellationToken cancellationToken)
    {
        var registration = access.Registration;
        if (!registration.ApprovedDate.HasValue ||
            !registration.ApprovedById.HasValue ||
            !Guid.TryParse(user.CreatedBy, out var createdById) ||
            createdById != registration.ApprovedById.Value ||
            user.CreatedAt < registration.ApprovedDate.Value.AddMinutes(-1) ||
            user.TenantId != access.TenantId ||
            !user.IsActive ||
            user.AuthenticationProvider != AuthenticationProvider.Local ||
            !user.MustChangePassword ||
            !user.TemporaryPasswordExpiresAtUtc.HasValue ||
            user.PasswordChangedAtUtc.HasValue ||
            user.EmployeeId.HasValue ||
            !string.Equals(
                user.FirstName,
                registration.ApplicantName,
                StringComparison.Ordinal) ||
            !string.Equals(user.LastName, "Supplier", StringComparison.Ordinal))
        {
            return false;
        }

        var contactMatches = access.VerifiedChannel ==
            ProcurementSupplierApplicantVerificationChannel.Email
            ? string.Equals(
                user.Email,
                access.VerifiedContact,
                StringComparison.OrdinalIgnoreCase)
            : string.Equals(
                SupplierApplicantContactNormalizer.Normalize(
                    ProcurementSupplierApplicantVerificationChannel.Sms,
                    user.PhoneNumber ?? string.Empty),
                SupplierApplicantContactNormalizer.Normalize(
                    ProcurementSupplierApplicantVerificationChannel.Sms,
                    access.VerifiedContact),
                StringComparison.Ordinal);
        if (!contactMatches)
            return false;

        var assignedRoles = await _userManager.GetRolesAsync(user);
        if (assignedRoles.Any(role => !approvedRoles.Contains(
                role,
                StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        var hasApplicantAccess = await Accesses
            .GetQueryableIncludingDeleted(item =>
                item.Id != access.Id &&
                item.ApprovedUserId == user.Id)
            .AnyAsync(cancellationToken);
        var hasTenantAccess = await UserTenants
            .GetQueryableIncludingDeleted(item => item.UserId == user.Id)
            .AnyAsync(cancellationToken);
        var ownsBusinessPartner = await BusinessPartners
            .GetQueryableIncludingDeleted(item => item.UserId == user.Id)
            .AnyAsync(cancellationToken);
        var hasBusinessPartnerMembership = await BusinessPartnerUsers
            .GetQueryableIncludingDeleted(item => item.UserId == user.Id)
            .AnyAsync(cancellationToken);

        return !hasApplicantAccess &&
               !hasTenantAccess &&
               !ownsBusinessPartner &&
               !hasBusinessPartnerMembership;
    }

    private static void EnsureApprovedUserTenant(
        ApplicationUser user,
        ProcurementSupplierApplicantAccess access)
    {
        if (user.TenantId != access.TenantId)
            throw Error(
                "SUPPLIER_APPLICANT_ACCOUNT_TENANT_MISMATCH",
                "The provisioned supplier account is not assigned to the application tenant.",
                409);
    }

    public async Task RetryApprovedSupplierActivationAsync(
        Guid registrationId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalTenant();
        EnsureAuthenticatedActor(actorUserId);
        await EnsureApprovalPermissionAsync(
            registrationId, correlationId, cancellationToken);
        var registration = await Registrations.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == registrationId && !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error(
                "SUPPLIER_APPLICANT_REGISTRATION_NOT_FOUND",
                "The supplier registration was not found.",
                404);
        if (!string.Equals(
                registration.Status, "Approved", StringComparison.OrdinalIgnoreCase) ||
            !registration.BusinessPartnerId.HasValue)
        {
            throw Error(
                "SUPPLIER_APPLICANT_APPROVAL_NOT_CURRENT",
                "Only a current approved supplier can be activated.",
                409);
        }

        await ProvisionApprovedSupplierAsync(
            registrationId,
            registration.BusinessPartnerId.Value,
            actorUserId,
            correlationId,
            cancellationToken);
    }

    public async Task ResendTemporaryCredentialAsync(
        Guid registrationId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalTenant();
        await EnsureApprovalPermissionAsync(
            registrationId, correlationId, cancellationToken);
        var access = await LoadAccessAsync(registrationId, cancellationToken, tracked: true);
        if (!access.ApprovedUserId.HasValue)
            throw Error("SUPPLIER_APPLICANT_CREDENTIAL_NOT_PROVISIONED",
                "No supplier login has been provisioned for this application. Use Retry to resolve the provisioning failure before resending credentials.", 409);
        if (access.Status == ProcurementSupplierApplicantAccessStatus.Activated)
            throw Error("SUPPLIER_APPLICANT_RESEND_NOT_ALLOWED",
                "Temporary credentials can be resent only before credential activation.", 409);
        var user = await _userManager.FindByIdAsync(access.ApprovedUserId.Value.ToString())
            ?? throw Error("SUPPLIER_APPLICANT_ACCOUNT_NOT_FOUND",
                "The provisioned supplier account was not found.", 404);
        var temporaryPassword = GenerateTemporaryPassword();
        var reset = await _userManager.GeneratePasswordResetTokenAsync(user);
        EnsureIdentitySucceeded(await _userManager.ResetPasswordAsync(
            user, reset, temporaryPassword));
        var now = DateTime.UtcNow;
        var expiry = now.AddDays(Math.Clamp(
            _options.TemporaryPasswordExpiryDays, 1, 30));
        user.MustChangePassword = true;
        user.TemporaryPasswordExpiresAtUtc = expiry;
        EnsureIdentitySucceeded(await _userManager.UpdateAsync(user));
        access.TemporaryCredentialIssuedAtUtc = now;
        access.TemporaryCredentialExpiresAtUtc = expiry;
        access.Status =
            ProcurementSupplierApplicantAccessStatus.ApprovedPendingCredentialDelivery;
        Touch(access);
        Capture(access);
        await Accesses.UpdateAsync(access);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DeliverCredentialAsync(
            access, temporaryPassword, resend: true, correlationId, cancellationToken);
    }

    public async Task CompleteCredentialActivationAsync(
        Guid approvedUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty ||
            _currentUser.UserId != approvedUserId)
        {
            throw Error(
                "SUPPLIER_APPLICANT_ACTIVATION_ACTOR_MISMATCH",
                "Credential activation must be completed by the authenticated supplier account.",
                403);
        }
        var access = await Accesses.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ApprovedUserId == approvedUserId && !item.IsDeleted)
            .Include(item => item.Token)
            .SingleOrDefaultAsync(cancellationToken);
        if (access is null) return;
        if (access.Status == ProcurementSupplierApplicantAccessStatus.Activated) return;
        access.Status = ProcurementSupplierApplicantAccessStatus.Activated;
        access.CredentialActivatedAtUtc = DateTime.UtcNow;
        Touch(access);
        Capture(access);
        await Accesses.UpdateAsync(access);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordUserEventAsync(
            access,
            access.Token,
            "TemporaryCredentialActivated",
            ProcurementControlEventResult.Succeeded,
            new
            {
                approvedUserId,
                access.CredentialActivatedAtUtc,
                MustChangePassword = false
            },
            "The supplier replaced the one-time temporary password.",
            NormalizeCorrelation(correlationId),
            cancellationToken);
    }

    public async Task<SupplierApplicantAccessSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalTenant();
        await EnsureReviewPermissionAsync(
            "summary", "supplier-applicant-summary", cancellationToken);
        var query = Accesses.GetQueryable(item =>
            item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        return new SupplierApplicantAccessSummaryDto
        {
            TotalApplications = await query.CountAsync(cancellationToken),
            ApplicationInProgress = await query.CountAsync(item =>
                item.Status == ProcurementSupplierApplicantAccessStatus.ApplicationInProgress,
                cancellationToken),
            PendingCredentialDelivery = await query.CountAsync(item =>
                item.Status == ProcurementSupplierApplicantAccessStatus.ApprovedPendingCredentialDelivery,
                cancellationToken),
            CredentialDelivered = await query.CountAsync(item =>
                item.Status == ProcurementSupplierApplicantAccessStatus.CredentialDelivered,
                cancellationToken),
            Activated = await query.CountAsync(item =>
                item.Status == ProcurementSupplierApplicantAccessStatus.Activated,
                cancellationToken),
            Rejected = await query.CountAsync(item =>
                item.Status == ProcurementSupplierApplicantAccessStatus.Rejected,
                cancellationToken),
            ActivationFailed = await query.CountAsync(item =>
                item.Status == ProcurementSupplierApplicantAccessStatus.ActivationFailed,
                cancellationToken)
        };
    }

    public async Task<IReadOnlyList<SupplierApplicantAccessListItemDto>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalTenant();
        await EnsureReviewPermissionAsync(
            "history", "supplier-applicant-history", cancellationToken);
        return await Accesses.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Registration)
            .AsNoTracking()
            .OrderByDescending(item => item.VerifiedAtUtc)
            .Select(item => new SupplierApplicantAccessListItemDto
            {
                Id = item.Id,
                RegistrationId = item.RegistrationId,
                RegistrationNumber = item.Registration.RegistrationNumber,
                CompanyName = item.Registration.ApplicantName,
                VerifiedChannel = item.VerifiedChannel.ToString(),
                VerifiedContactMasked = item.VerifiedContactMasked,
                Status = item.Status,
                LoginIdentifier = item.LoginIdentifier,
                VerifiedAtUtc = item.VerifiedAtUtc,
                TemporaryCredentialExpiresAtUtc = item.TemporaryCredentialExpiresAtUtc,
                CredentialActivatedAtUtc = item.CredentialActivatedAtUtc,
                NotificationAttemptCount = item.NotificationAttemptCount,
                LastNotificationStatus = item.LastNotificationStatus
            }).ToListAsync(cancellationToken);
    }

    public async Task<SupplierApplicantContactCorrectionPreparationDto>
        PrepareVerifiedContactCorrectionAsync(
            Guid registrationId,
            PrepareSupplierApplicantContactCorrectionRequest request,
            Guid actorUserId,
            string correlationId,
            CancellationToken cancellationToken = default)
    {
        EnsureInternalTenant();
        EnsureAuthenticatedActor(actorUserId);
        await EnsureApprovalPermissionAsync(
            registrationId, NormalizeCorrelation(correlationId), cancellationToken);

        var contact = SupplierApplicantContactNormalizer.Normalize(
            request.Channel, request.Contact);
        ValidateContact(request.Channel, contact);
        var access = await LoadAccessAsync(registrationId, cancellationToken);
        EnsureContactCorrectionEligible(access);
        await EnsureContactAvailableForCorrectionAsync(
            access, request.Channel, contact, cancellationToken);

        return new SupplierApplicantContactCorrectionPreparationDto
        {
            TenantId = access.TenantId,
            Channel = request.Channel,
            NormalizedContact = contact,
            MaskedContact = MaskContact(request.Channel, contact)
        };
    }

    public async Task<SupplierApplicantContactCorrectionResultDto>
        CorrectVerifiedContactAndRetryAsync(
            Guid registrationId,
            CorrectSupplierApplicantVerifiedContactRequest request,
            Guid actorUserId,
            string correlationId,
            CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var prepared = await PrepareVerifiedContactCorrectionAsync(
            registrationId, request, actorUserId, correlation, cancellationToken);
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 10 or > 500)
            throw Error(
                "SUPPLIER_APPLICANT_CONTACT_CORRECTION_REASON_INVALID",
                "Enter a correction reason between 10 and 500 characters.",
                400);

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            try
            {
                var access = await LoadAccessAsync(
                    registrationId, cancellationToken, tracked: true);
                EnsureContactCorrectionEligible(access);
                await EnsureContactAvailableForCorrectionAsync(
                    access,
                    prepared.Channel,
                    prepared.NormalizedContact,
                    cancellationToken);
                var auditReason = RedactContacts(
                    reason,
                    access.VerifiedContact,
                    request.Contact,
                    prepared.NormalizedContact);
                var previousChannel = access.VerifiedChannel;
                var previousContact = access.VerifiedContact;

                access.VerifiedChannel = prepared.Channel;
                access.VerifiedContact = prepared.NormalizedContact;
                access.VerifiedContactMasked = prepared.MaskedContact;
                access.VerifiedContactHashSha256 = Hash(prepared.NormalizedContact);
                access.VerifiedAtUtc = DateTime.UtcNow;
                access.LastNotificationFailure = null;
                Touch(access);
                Capture(access);
                await Accesses.UpdateAsync(access);

                var registration = access.Registration;
                if (previousChannel != prepared.Channel)
                {
                    if (previousChannel ==
                            ProcurementSupplierApplicantVerificationChannel.Email &&
                        ContactMatches(
                            previousChannel,
                            registration.ApplicantEmail,
                            previousContact))
                        registration.ApplicantEmail = null;
                    if (previousChannel ==
                            ProcurementSupplierApplicantVerificationChannel.Sms &&
                        ContactMatches(
                            previousChannel,
                            registration.ApplicantPhone,
                            previousContact))
                        registration.ApplicantPhone = null;
                }
                if (prepared.Channel ==
                    ProcurementSupplierApplicantVerificationChannel.Email)
                    registration.ApplicantEmail = prepared.NormalizedContact;
                else
                    registration.ApplicantPhone = prepared.NormalizedContact;
                registration.RegistrationDataJson = UpdateRegistrationContactJson(
                    registration.RegistrationDataJson,
                    previousChannel,
                    previousContact,
                    prepared.Channel,
                    prepared.NormalizedContact);
                registration.UpdatedAt = DateTime.UtcNow;
                registration.UpdatedBy = "Supplier Applicant Contact Recovery";
                registration.LastModifiedById = actorUserId;
                await Registrations.UpdateAsync(registration);

                if (registration.BusinessPartnerId.HasValue)
                {
                    var businessPartner = await BusinessPartners.GetQueryable(item =>
                            item.TenantId == access.TenantId &&
                            item.Id == registration.BusinessPartnerId.Value &&
                            !item.IsDeleted)
                        .SingleOrDefaultAsync(cancellationToken)
                        ?? throw Error(
                            "SUPPLIER_APPLICANT_BUSINESS_PARTNER_NOT_FOUND",
                            "The approved business partner was not found in the current tenant.",
                            409);
                    if (previousChannel != prepared.Channel)
                    {
                        if (previousChannel ==
                                ProcurementSupplierApplicantVerificationChannel.Email &&
                            ContactMatches(
                                previousChannel,
                                businessPartner.PrimaryEmail,
                                previousContact))
                            businessPartner.PrimaryEmail = null;
                        if (previousChannel ==
                                ProcurementSupplierApplicantVerificationChannel.Sms &&
                            ContactMatches(
                                previousChannel,
                                businessPartner.PrimaryPhone,
                                previousContact))
                            businessPartner.PrimaryPhone = null;
                    }
                    if (prepared.Channel ==
                        ProcurementSupplierApplicantVerificationChannel.Email)
                        businessPartner.PrimaryEmail = prepared.NormalizedContact;
                    else
                        businessPartner.PrimaryPhone = prepared.NormalizedContact;
                    businessPartner.UpdatedAt = DateTime.UtcNow;
                    businessPartner.UpdatedBy = actorUserId.ToString();
                    businessPartner.LastModifiedById = actorUserId;
                    await BusinessPartners.UpdateAsync(businessPartner);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordUserEventAsync(
                    access,
                    access.Token,
                    "ApplicantVerifiedContactCorrected",
                    ProcurementControlEventResult.Succeeded,
                    new
                    {
                        Channel = prepared.Channel.ToString(),
                        ContactChanged = true,
                        ReverificationMethod = "OneTimePassword",
                        CorrectedAtUtc = access.VerifiedAtUtc,
                        ProvisioningRetryRequested = true
                    },
                    auditReason,
                    correlation,
                    cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);

        string? retryFailure = null;
        var provisioningCorrelation = ChildCorrelation(
            correlation, "provision", reservedCharacters: 6);
        try
        {
            await RetryApprovedSupplierActivationAsync(
                registrationId,
                actorUserId,
                provisioningCorrelation,
                cancellationToken);
        }
        catch (ProcurementSupplierApplicantAccessException exception)
        {
            retryFailure = exception.Message;
            var access = await LoadAccessAsync(registrationId, cancellationToken);
            await RecordUserEventAsync(
                access,
                access.Token,
                "ApplicantContactCorrectionProvisioningRetryFailed",
                ProcurementControlEventResult.Failed,
                new
                {
                    ContactCorrectionPersisted = true,
                    FailureCode = exception.Code,
                    RetryAvailable = true
                },
                "The verified contact was corrected, but supplier-account provisioning still requires recovery.",
                ChildCorrelation(correlation, "provision-failed"),
                cancellationToken);
        }

        var current = await LoadAccessAsync(registrationId, cancellationToken);
        var delivered = current.Status ==
            ProcurementSupplierApplicantAccessStatus.CredentialDelivered;
        return new SupplierApplicantContactCorrectionResultDto
        {
            RegistrationId = registrationId,
            MaskedContact = current.VerifiedContactMasked,
            Status = current.Status,
            ContactCorrected = true,
            ProvisioningRetried = true,
            CredentialDelivered = delivered,
            Message = delivered
                ? "The verified supplier contact was corrected and temporary credentials were sent."
                : retryFailure is null
                    ? "The verified supplier contact was corrected, but credential delivery still requires Retry."
                    : $"The verified supplier contact was corrected. {retryFailure}"
        };
    }

    private async Task EnsureContactAvailableForCorrectionAsync(
        ProcurementSupplierApplicantAccess access,
        ProcurementSupplierApplicantVerificationChannel channel,
        string contact,
        CancellationToken cancellationToken)
    {
        if (channel == access.VerifiedChannel && string.Equals(
                contact,
                SupplierApplicantContactNormalizer.Normalize(
                    access.VerifiedChannel, access.VerifiedContact),
                StringComparison.OrdinalIgnoreCase))
            throw Error(
                "SUPPLIER_APPLICANT_CONTACT_UNCHANGED",
                "Enter a different supplier contact before requesting verification.",
                409);
        if (await FindConflictingIdentityAsync(
                access.TenantId, channel, contact, cancellationToken) is not null)
            throw Error(
                "SUPPLIER_APPLICANT_CONTACT_ALREADY_REGISTERED",
                "This contact is already assigned to an ERP account. Use a separate supplier contact.",
                409);

        var aliases = SupplierApplicantContactNormalizer
            .GetLookupAliases(channel, contact)
            .Select(Hash)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var conflict = await Accesses.GetQueryable(item =>
                item.TenantId == access.TenantId &&
                item.Id != access.Id &&
                !item.IsDeleted &&
                aliases.Contains(item.VerifiedContactHashSha256) &&
                item.Status != ProcurementSupplierApplicantAccessStatus.Rejected)
            .AnyAsync(cancellationToken);
        if (conflict)
            throw Error(
                "SUPPLIER_APPLICANT_CONTACT_IN_USE",
                "This contact is already assigned to another supplier application.",
                409);
    }

    private static void EnsureContactCorrectionEligible(
        ProcurementSupplierApplicantAccess access)
    {
        if (!string.Equals(
                access.Registration.Status,
                "Approved",
                StringComparison.OrdinalIgnoreCase) ||
            !access.Registration.BusinessPartnerId.HasValue)
            throw Error(
                "SUPPLIER_APPLICANT_CONTACT_CORRECTION_NOT_APPROVED",
                "Verified-contact correction is available only for an approved supplier application.",
                409);
        if (access.ApprovedUserId.HasValue ||
            access.Status is not (
                ProcurementSupplierApplicantAccessStatus.ApprovedPendingCredentialDelivery or
                ProcurementSupplierApplicantAccessStatus.ActivationFailed))
            throw Error(
                "SUPPLIER_APPLICANT_CONTACT_CORRECTION_NOT_ALLOWED",
                "The verified contact can be corrected only before a supplier login has been provisioned.",
                409);
    }

    private static string UpdateRegistrationContactJson(
        string? json,
        ProcurementSupplierApplicantVerificationChannel previousChannel,
        string previousContact,
        ProcurementSupplierApplicantVerificationChannel channel,
        string contact)
    {
        JsonObject root;
        try
        {
            root = string.IsNullOrWhiteSpace(json)
                ? new JsonObject()
                : JsonNode.Parse(json) as JsonObject
                  ?? throw new JsonException("The registration data must be a JSON object.");
        }
        catch (JsonException)
        {
            throw Error(
                "SUPPLIER_APPLICANT_REGISTRATION_DATA_INVALID",
                "The approved registration data cannot be safely updated. Correct its invalid JSON before retrying.",
                409);
        }

        UpdateRegistrationContactObject(
            root, previousChannel, previousContact, channel, contact);
        var nestedProperty = root
            .Select(item => item.Key)
            .FirstOrDefault(item => string.Equals(
                item, "registrationData", StringComparison.OrdinalIgnoreCase));
        if (nestedProperty is not null && root[nestedProperty] is JsonObject nested)
        {
            UpdateRegistrationContactObject(
                nested, previousChannel, previousContact, channel, contact);
        }
        else if (nestedProperty is not null &&
                 root[nestedProperty] is JsonValue nestedValue &&
                 nestedValue.TryGetValue<string>(out var nestedJson) &&
                 !string.IsNullOrWhiteSpace(nestedJson))
        {
            try
            {
                if (JsonNode.Parse(nestedJson) is JsonObject nestedObject)
                {
                    UpdateRegistrationContactObject(
                        nestedObject,
                        previousChannel,
                        previousContact,
                        channel,
                        contact);
                    root[nestedProperty] = nestedObject.ToJsonString(JsonOptions);
                }
            }
            catch (JsonException)
            {
                throw Error(
                    "SUPPLIER_APPLICANT_REGISTRATION_DATA_INVALID",
                    "The approved registration data contains invalid nested JSON and cannot be safely updated.",
                    409);
            }
        }
        return root.ToJsonString(JsonOptions);
    }

    private static void UpdateRegistrationContactObject(
        JsonObject root,
        ProcurementSupplierApplicantVerificationChannel previousChannel,
        string previousContact,
        ProcurementSupplierApplicantVerificationChannel channel,
        string contact)
    {
        var previousPropertyName = previousChannel ==
            ProcurementSupplierApplicantVerificationChannel.Email
            ? "email"
            : "phone";
        var previousProperty = root
            .Select(item => item.Key)
            .FirstOrDefault(item => string.Equals(
                item, previousPropertyName, StringComparison.OrdinalIgnoreCase));
        if (previousChannel != channel &&
            previousProperty is not null &&
            root[previousProperty] is JsonValue previousValue &&
            previousValue.TryGetValue<string>(out var recordedPreviousContact) &&
            ContactMatches(
                previousChannel,
                recordedPreviousContact,
                previousContact))
            root[previousProperty] = null;

        var propertyName = channel ==
            ProcurementSupplierApplicantVerificationChannel.Email
            ? "email"
            : "phone";
        var existingProperty = root
            .Select(item => item.Key)
            .FirstOrDefault(item => string.Equals(
                item, propertyName, StringComparison.OrdinalIgnoreCase));
        root[existingProperty ?? propertyName] = contact;
    }

    private static bool ContactMatches(
        ProcurementSupplierApplicantVerificationChannel channel,
        string? candidate,
        string expected)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        return string.Equals(
            SupplierApplicantContactNormalizer.Normalize(channel, candidate),
            SupplierApplicantContactNormalizer.Normalize(channel, expected),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string RedactContacts(
        string reason,
        params string?[] contacts)
    {
        var redacted = reason;
        foreach (var contact in contacts
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Select(item => item!.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            redacted = Regex.Replace(
                redacted,
                Regex.Escape(contact),
                "[redacted contact]",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
        }
        return redacted;
    }

    private async Task DeliverCredentialAsync(
        ProcurementSupplierApplicantAccess access,
        string temporaryPassword,
        bool resend,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var action = resend ? "TemporaryCredentialResent" : "TemporaryCredentialNotified";
        try
        {
            var expiresAt =
                $"{access.TemporaryCredentialExpiresAtUtc:yyyy-MM-dd HH:mm} UTC";
            var message =
                $"Your supplier application is approved. Open {_supplierLoginUrl}. " +
                $"Login: {access.LoginIdentifier}. Temporary password: {temporaryPassword}. " +
                $"It expires {expiresAt} and must be changed at first login.";
            if (access.VerifiedChannel ==
                ProcurementSupplierApplicantVerificationChannel.Email)
            {
                var encodedUrl = System.Net.WebUtility.HtmlEncode(_supplierLoginUrl);
                var encodedLogin =
                    System.Net.WebUtility.HtmlEncode(access.LoginIdentifier);
                var encodedPassword =
                    System.Net.WebUtility.HtmlEncode(temporaryPassword);
                var encodedExpiry = System.Net.WebUtility.HtmlEncode(expiresAt);
                await _notifications.SendEmailAsync(
                    access.VerifiedContact,
                    resend
                        ? "Supplier portal temporary credential reissued"
                        : "Supplier portal account approved",
                    $"<p>Your supplier application is approved.</p>" +
                    $"<p><strong>Portal login:</strong> " +
                    $"<a href=\"{encodedUrl}\">{encodedUrl}</a></p>" +
                    $"<p><strong>Login identifier:</strong> {encodedLogin}<br/>" +
                    $"<strong>Temporary password:</strong> {encodedPassword}<br/>" +
                    $"<strong>Expires:</strong> {encodedExpiry}</p>" +
                    "<p>You must change this password at first login.</p>",
                    isHtml: true);
            }
            else
            {
                await _notifications.SendSmsAsync(access.VerifiedContact, message);
            }
            access.NotificationAttemptCount++;
            access.LastNotificationAtUtc = DateTime.UtcNow;
            access.LastNotificationStatus = "Sent";
            access.LastNotificationFailure = null;
            access.Status = ProcurementSupplierApplicantAccessStatus.CredentialDelivered;
            Touch(access);
            Capture(access);
            await Accesses.UpdateAsync(access);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordUserEventAsync(
                access,
                access.Token,
                action,
                ProcurementControlEventResult.Succeeded,
                new
                {
                    Channel = access.VerifiedChannel.ToString(),
                    access.VerifiedContactMasked,
                    access.NotificationAttemptCount,
                    access.TemporaryCredentialExpiresAtUtc
                },
                "Temporary credentials were delivered through the verified channel.",
                NormalizeCorrelation(correlationId),
                cancellationToken);
        }
        catch (Exception exception)
        {
            access.NotificationAttemptCount++;
            access.LastNotificationAtUtc = DateTime.UtcNow;
            access.LastNotificationStatus = "Failed";
            access.LastNotificationFailure = Trim(exception.Message, 1000);
            access.Status = ProcurementSupplierApplicantAccessStatus.ActivationFailed;
            Touch(access);
            Capture(access);
            await Accesses.UpdateAsync(access);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordUserEventAsync(
                access,
                access.Token,
                action,
                ProcurementControlEventResult.Failed,
                new
                {
                    Channel = access.VerifiedChannel.ToString(),
                    access.VerifiedContactMasked,
                    access.NotificationAttemptCount
                },
                "Temporary credential delivery failed; no credential value was logged.",
                NormalizeCorrelation(correlationId),
                cancellationToken);
            _logger.LogWarning(
                "Supplier temporary credential delivery failed for applicant access {AccessId}",
                access.Id);
        }
    }

    private static string BuildSupplierLoginUrl(string? frontendUrl)
    {
        var configuredUrl = string.IsNullOrWhiteSpace(frontendUrl)
            ? "http://localhost:3000"
            : frontendUrl.Trim();
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var parsedUrl) ||
            (parsedUrl.Scheme != Uri.UriSchemeHttp &&
             parsedUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "FrontendUrl must be configured with an absolute HTTP or HTTPS URL.");
        }

        return $"{configuredUrl.TrimEnd('/')}/login";
    }

    private async Task<ProcurementSupplierApplicantAccess> LoadAccessAsync(
        Guid registrationId,
        CancellationToken cancellationToken,
        bool tracked = false)
    {
        IQueryable<ProcurementSupplierApplicantAccess> query =
            Accesses.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.RegistrationId == registrationId && !item.IsDeleted)
            .Include(item => item.Registration)
            .Include(item => item.Token);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw Error("SUPPLIER_APPLICANT_ACCESS_NOT_FOUND",
                "The verified applicant access record was not found.", 404);
    }

    private async Task EnsureApprovalPermissionAsync(
        Guid registrationId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = ApprovePermission,
                SourceType = SourceType,
                SourceReference = $"registration:{registrationId:N}"
            },
            correlationId,
            cancellationToken);
        if (!decision.Allowed)
            throw Error("SUPPLIER_APPLICANT_APPROVE_DENIED", decision.Message, 403);
    }

    private void EnsureAuthenticatedActor(Guid actorUserId)
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.UserId == Guid.Empty ||
            actorUserId != _currentUser.UserId)
        {
            throw Error(
                "SUPPLIER_APPLICANT_ACTOR_MISMATCH",
                "The supplier action actor must be derived from the authenticated user.",
                403);
        }
    }

    private async Task EnsureReviewPermissionAsync(
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = ReviewPermission,
                SourceType = SourceType,
                SourceReference = sourceReference
            },
            correlationId,
            cancellationToken);
        if (!decision.Allowed)
            throw Error("SUPPLIER_APPLICANT_REVIEW_DENIED", decision.Message, 403);
    }

    private async Task RecordSystemEventAsync(
        ProcurementSupplierApplicantAccess access,
        ProcurementSupplierOnboardingTokenDto token,
        string action,
        ProcurementControlEventResult result,
        object after,
        string reason,
        string correlationId,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordSystemAsync(
            access.TenantId,
            "Verified Supplier Applicant",
            BuildEvent(access, token.SourceConfigurationProfileCode,
                token.SourceConfigurationDecisionId,
                token.SourceConfigurationProfileVersion,
                action, result, after, reason, correlationId),
            cancellationToken);

    private async Task RecordSystemEventAsync(
        ProcurementSupplierApplicantAccess access,
        ProcurementSupplierOnboardingToken token,
        string action,
        ProcurementControlEventResult result,
        object after,
        string reason,
        string correlationId,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordSystemAsync(
            access.TenantId,
            "Verified Supplier Applicant",
            BuildEvent(access, token.SourceConfigurationProfileCode,
                token.SourceConfigurationDecisionId,
                token.SourceConfigurationProfileVersion,
                action, result, after, reason, correlationId),
            cancellationToken);

    private async Task RecordUserEventAsync(
        ProcurementSupplierApplicantAccess access,
        ProcurementSupplierOnboardingToken token,
        string action,
        ProcurementControlEventResult result,
        object after,
        string reason,
        string correlationId,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(
            BuildEvent(access, token.SourceConfigurationProfileCode,
                token.SourceConfigurationDecisionId,
                token.SourceConfigurationProfileVersion,
                action, result, after, reason, correlationId),
            cancellationToken);

    private static ProcurementControlEventWriteRequest BuildEvent(
        ProcurementSupplierApplicantAccess access,
        string ruleCode,
        Guid ruleId,
        int ruleVersion,
        string action,
        ProcurementControlEventResult result,
        object after,
        string reason,
        string correlationId) =>
        new()
        {
            EventKey = ProcurementControlEventKey.Create(
                "supplier-applicant-access",
                access.TenantId,
                access.Id,
                $"{action}-{correlationId}"),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = ruleCode,
            RuleId = ruleId,
            RuleVersion = ruleVersion.ToString(),
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = SourceType,
            SourceId = access.Id,
            SourceReference = $"registration:{access.RegistrationId:N}",
            Reason = reason,
            After = after,
            CorrelationId = correlationId,
            CausationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow
        };

    private static SupplierApplicantSessionDto MapSession(
        ProcurementSupplierApplicantSession session,
        ProcurementSupplierApplicantAccess access,
        ProcurementSupplierOnboardingTokenDto token) =>
        new()
        {
            SessionId = session.Id,
            SessionReference = session.SessionReference,
            ApplicantActorId = access.Id,
            TenantId = session.TenantId,
            RegistrationId = access.RegistrationId,
            TokenId = access.TokenId,
            ExpiresAtUtc = session.ExpiresAtUtc,
            PaymentOnly = token.Status ==
                ProcurementSupplierOnboardingTokenStatus.AwaitingPayment
        };

    private static SupplierApplicantPortalDto MapPortal(
        BusinessPartnerRegistrationDetailDto registration,
        ProcurementSupplierOnboardingTokenDto token) =>
        new()
        {
            RegistrationId = registration.Id,
            TokenId = token.Id,
            RegistrationNumber = registration.ApplicationNumber,
            CompanyName = registration.CompanyName,
            Email = registration.Email,
            Phone = registration.Phone,
            PartnerType = registration.PartnerType,
            RegistrationCategory = registration.RegistrationCategory,
            Status = registration.Status,
            RegistrationData = registration.RegistrationData,
            RejectionReason = registration.RejectionReason,
            TokenStatus = token.Status,
            PaymentStatus = token.PaymentStatus,
            FeeMode = token.FeeMode,
            TotalAmount = token.TotalAmount,
            CurrencyCode = token.CurrencyCode,
            TokenRowVersion = token.RowVersion,
            PaymentOnly = token.Status ==
                ProcurementSupplierOnboardingTokenStatus.AwaitingPayment,
            CanEdit = token.Status == ProcurementSupplierOnboardingTokenStatus.Active &&
                registration.Status is "Draft" or "MoreInfoRequired",
            CanSubmit = token.Status == ProcurementSupplierOnboardingTokenStatus.Active &&
                registration.Status is "Draft" or "MoreInfoRequired",
            Documents = registration.Documents,
            StatusHistory = registration.StatusHistory,
            EvidenceReadiness = registration.EvidenceReadiness
        };

    private static ProcurementSupplierOnboardingTokenDto MapToken(
        ProcurementSupplierOnboardingToken token) =>
        new()
        {
            Id = token.Id,
            RegistrationId = token.RegistrationId,
            TokenReference = token.TokenReference,
            Status = token.Status,
            PaymentStatus = token.PaymentStatus,
            FeeMode = token.FeeMode,
            TotalAmount = token.TotalAmount,
            CurrencyCode = token.CurrencyCode,
            SourceConfigurationProfileCode = token.SourceConfigurationProfileCode,
            SourceConfigurationProfileVersion = token.SourceConfigurationProfileVersion,
            SourceConfigurationDecisionId = token.SourceConfigurationDecisionId
        };

    private void EnsureInternalTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.TenantId == Guid.Empty ||
            _currentUser.UserId == Guid.Empty || _currentUser.IsExternalUser)
            throw Error("SUPPLIER_APPLICANT_INTERNAL_ACCESS_REQUIRED",
                "An internal tenant user is required.", 403);
    }

    private bool IsAdministrator() =>
        _currentUser.HasRole("TenantAdmin") || _currentUser.HasRole("SuperAdmin");

    private string[] NormalizeApprovedRoles()
    {
        var roles = (_options.ApprovedIdentityRoles ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (roles.Length == 0 ||
            roles.Any(item => !string.Equals(
                item, Constants.Roles.ExternalUser, StringComparison.OrdinalIgnoreCase)))
            throw Error("SUPPLIER_APPLICANT_ROLE_CONFIGURATION_INVALID",
                "Approved supplier identity roles must contain only ExternalUser.", 409);
        return roles;
    }

    private string NormalizeBusinessPartnerRole()
    {
        var role = (_options.ApprovedBusinessPartnerRole ?? "Admin").Trim();
        if (role is not ("Admin" or "User" or "Viewer"))
            throw Error("SUPPLIER_APPLICANT_BP_ROLE_CONFIGURATION_INVALID",
                "Approved supplier business-partner role must be Admin, User, or Viewer.", 409);
        return role;
    }

    private static void EnsureVerifiedContactUnchanged(
        ProcurementSupplierApplicantAccess access,
        UpdateSupplierApplicantApplicationRequest request)
    {
        var candidate = access.VerifiedChannel ==
            ProcurementSupplierApplicantVerificationChannel.Email
            ? request.Email
            : request.Phone;
        if (string.IsNullOrWhiteSpace(candidate) ||
            !string.Equals(
                SupplierApplicantContactNormalizer.Normalize(
                    access.VerifiedChannel, candidate),
                SupplierApplicantContactNormalizer.Normalize(
                    access.VerifiedChannel, access.VerifiedContact),
                StringComparison.OrdinalIgnoreCase))
            throw Error("SUPPLIER_APPLICANT_VERIFIED_CONTACT_IMMUTABLE",
                "The verified application contact cannot be removed or changed.", 409);
    }

    private static string BuildLoginIdentifier(
        ProcurementSupplierApplicantAccess access) =>
        BuildLoginIdentifier(
            access.TenantId,
            access.VerifiedChannel,
            access.VerifiedContact);

    private static string BuildLoginIdentifier(
        Guid tenantId,
        ProcurementSupplierApplicantVerificationChannel channel,
        string contact) =>
        channel == ProcurementSupplierApplicantVerificationChannel.Email
            ? contact
            : $"{contact}.{tenantId.ToString("N")[..8]}";

    private async Task<ApplicationUser?> FindConflictingIdentityAsync(
        Guid tenantId,
        ProcurementSupplierApplicantVerificationChannel channel,
        string contact,
        CancellationToken cancellationToken)
    {
        var users = _userManager.Users.IgnoreQueryFilters();
        var loginIdentifier = BuildLoginIdentifier(tenantId, channel, contact);
        var normalizedLogin = loginIdentifier.ToUpperInvariant();
        var loginConflict = await users.FirstOrDefaultAsync(item =>
            item.NormalizedUserName == normalizedLogin ||
            item.UserName == loginIdentifier,
            cancellationToken);
        if (loginConflict is not null)
            return loginConflict;

        if (channel == ProcurementSupplierApplicantVerificationChannel.Email)
        {
            var normalizedEmail = contact.ToUpperInvariant();
            return await users.FirstOrDefaultAsync(item =>
                    item.NormalizedEmail == normalizedEmail ||
                    item.Email == contact,
                cancellationToken);
        }

        var phoneAliases = SupplierApplicantContactNormalizer
            .GetLookupAliases(channel, contact)
            .ToArray();
        return await users.FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.PhoneNumber != null &&
                phoneAliases.Contains(
                    item.PhoneNumber
                        .Replace(" ", string.Empty)
                        .Replace("-", string.Empty)
                        .Replace("(", string.Empty)
                        .Replace(")", string.Empty)
                        .Replace("/", string.Empty)
                        .Replace(".", string.Empty)),
            cancellationToken);
    }

    private static ProcurementSupplierApplicantAccessException LoginAlreadyExists() =>
        Error(
            "SUPPLIER_APPLICANT_LOGIN_ALREADY_EXISTS",
            "The verified contact belongs to an existing ERP account and cannot be used to provision a supplier login. Use a separate verified supplier contact.",
            409);

    private static void ValidateContact(
        ProcurementSupplierApplicantVerificationChannel channel,
        string contact)
    {
        if (channel == ProcurementSupplierApplicantVerificationChannel.Email)
        {
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute()
                .IsValid(contact))
                throw Error("SUPPLIER_APPLICANT_EMAIL_INVALID",
                    "Enter a valid email address.", 400);
            return;
        }
        var phoneDigits = contact.Count(char.IsDigit);
        if (!contact.StartsWith('+') || phoneDigits is < 8 or > 15)
            throw Error("SUPPLIER_APPLICANT_PHONE_INVALID",
                "Enter a valid phone number in Ghana national or E.164 format.", 400);
    }

    private static string MaskContact(
        ProcurementSupplierApplicantVerificationChannel channel,
        string contact)
    {
        if (channel == ProcurementSupplierApplicantVerificationChannel.Email)
        {
            var parts = contact.Split('@', 2);
            var local = parts[0];
            return $"{local[..Math.Min(2, local.Length)]}***@{parts[1]}";
        }
        return contact.Length <= 4
            ? "****"
            : $"{new string('*', Math.Min(8, contact.Length - 4))}{contact[^4..]}";
    }

    private static string GenerateTemporaryPassword() =>
        $"Aa1!{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}z!";

    private static void EnsureIdentitySucceeded(IdentityResult result)
    {
        if (result.Succeeded) return;
        throw Error("SUPPLIER_APPLICANT_IDENTITY_PROVISION_FAILED",
            string.Join(" ", result.Errors.Select(item => item.Description)), 409);
    }

    private static void Capture(ProcurementSupplierApplicantAccess access)
    {
        access.IntegrityHash = Hash(JsonSerializer.Serialize(new
        {
            access.Id,
            access.TenantId,
            access.RegistrationId,
            access.TokenId,
            access.VerifiedChannel,
            access.VerifiedContactHashSha256,
            access.VerifiedAtUtc,
            access.Status,
            access.ApprovedUserId,
            access.BusinessPartnerId,
            access.ApprovedIdentityRolesJson,
            access.ApprovedBusinessPartnerRole,
            access.LoginIdentifier,
            access.TemporaryCredentialIssuedAtUtc,
            access.TemporaryCredentialExpiresAtUtc,
            access.CredentialActivatedAtUtc,
            access.NotificationAttemptCount,
            access.LastNotificationStatus,
            access.TerminalAtUtc,
            access.TerminalOutcome
        }, JsonOptions));
    }

    private static void Capture(ProcurementSupplierApplicantSession session)
    {
        session.IntegrityHash = Hash(JsonSerializer.Serialize(new
        {
            session.Id,
            session.TenantId,
            session.ApplicantAccessId,
            session.SessionReference,
            session.Status,
            session.IssuedAtUtc,
            session.ExpiresAtUtc,
            session.LastUsedAtUtc,
            session.RevokedAtUtc,
            session.RevocationReason
        }, JsonOptions));
    }

    private static void Touch(ProcurementSupplierApplicantAccess entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = "Supplier Applicant Access";
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private static void Touch(ProcurementSupplierApplicantSession entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = "Supplier Applicant Access";
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static bool MatchesSha256Secret(string plaintext, string storedHash)
    {
        byte[] storedBytes;
        try
        {
            storedBytes = Convert.FromHexString(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var suppliedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return storedBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(suppliedBytes, storedBytes);
    }

    private static string NormalizeCorrelation(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length is < 8 or > 100)
            throw Error("SUPPLIER_APPLICANT_CORRELATION_INVALID",
                "A correlation ID between 8 and 100 characters is required.", 400);
        return normalized;
    }

    private static string ChildCorrelation(
        string parent,
        string suffix,
        int reservedCharacters = 0)
    {
        var normalized = NormalizeCorrelation(parent);
        var maxParentLength = 100 - suffix.Length - 1 - reservedCharacters;
        return $"{normalized[..Math.Min(normalized.Length, maxParentLength)]}-{suffix}";
    }

    private static string Trim(string value, int length) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim()[..Math.Min(value.Trim().Length, length)];

    private static ProcurementSupplierApplicantAccessException Error(
        string code,
        string message,
        int statusCode = 409) =>
        new(code, message, statusCode);
}
