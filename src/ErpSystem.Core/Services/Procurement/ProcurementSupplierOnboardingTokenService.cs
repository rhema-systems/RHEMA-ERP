using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Numbering;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementSupplierOnboardingTokenService :
    IProcurementSupplierOnboardingTokenService
{
    private const string SourceType = "ProcurementSupplierOnboardingToken";
    private const string EventType = "ProcurementSupplierOnboardingTokenControl";
    private const string ManagePermission = "procurement.supplier.manage";
    private const string ReviewPermission = "procurement.supplier.review";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToArray();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IWorkflowInstanceService _workflowInstances;
    private readonly IFinancePostingEngine _financePosting;
    private readonly IDocumentNumberingService _documentNumbering;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly ILogger<ProcurementSupplierOnboardingTokenService> _logger;

    public ProcurementSupplierOnboardingTokenService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IWorkflowInstanceService workflowInstances,
        IFinancePostingEngine financePosting,
        IDocumentNumberingService documentNumbering,
        INotificationTopicPublisher notificationTopics,
        ILogger<ProcurementSupplierOnboardingTokenService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _workflowInstances = workflowInstances;
        _financePosting = financePosting;
        _documentNumbering = documentNumbering;
        _notificationTopics = notificationTopics;
        _logger = logger;
    }

    private IGenericRepository<ProcurementSupplierOnboardingToken> Tokens =>
        _unitOfWork.Repository<ProcurementSupplierOnboardingToken>();
    private IGenericRepository<ProcurementSupplierOnboardingPayment> Payments =>
        _unitOfWork.Repository<ProcurementSupplierOnboardingPayment>();
    private IGenericRepository<ProcurementSupplierOnboardingExemption> Exemptions =>
        _unitOfWork.Repository<ProcurementSupplierOnboardingExemption>();
    private IGenericRepository<ProcurementSupplierApplicantAccess> ApplicantAccesses =>
        _unitOfWork.Repository<ProcurementSupplierApplicantAccess>();
    private IGenericRepository<ProcurementSupplierApplicantSession> ApplicantSessions =>
        _unitOfWork.Repository<ProcurementSupplierApplicantSession>();
    private IGenericRepository<BusinessPartnerRegistration> Registrations =>
        _unitOfWork.Repository<BusinessPartnerRegistration>();
    private IGenericRepository<ProcurementConfigurationProfile> ConfigurationProfiles =>
        _unitOfWork.Repository<ProcurementConfigurationProfile>();
    private IGenericRepository<FinancePaymentMethod> PaymentMethods =>
        _unitOfWork.Repository<FinancePaymentMethod>();
    private IGenericRepository<Account> Accounts =>
        _unitOfWork.Repository<Account>();
    private IGenericRepository<FinanceSettings> FinanceSettingsRows =>
        _unitOfWork.Repository<FinanceSettings>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions =>
        _unitOfWork.Repository<WorkflowDefinition>();

    public async Task<ProcurementSupplierOnboardingTokenSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var query = TokenQuery();
        return new ProcurementSupplierOnboardingTokenSummaryDto
        {
            TotalCount = await query.CountAsync(cancellationToken),
            AwaitingPaymentCount = await query.CountAsync(item =>
                item.Status == ProcurementSupplierOnboardingTokenStatus.AwaitingPayment,
                cancellationToken),
            ActiveCount = await query.CountAsync(item =>
                item.Status == ProcurementSupplierOnboardingTokenStatus.Active,
                cancellationToken),
            ExpiredCount = await query.CountAsync(item =>
                item.Status == ProcurementSupplierOnboardingTokenStatus.Expired,
                cancellationToken),
            PendingReconciliationCount = await query.CountAsync(item =>
                (item.PaymentStatus == ProcurementSupplierOnboardingPaymentStatus.Pending &&
                 item.Payments.Any(payment =>
                     !payment.IsDeleted &&
                     payment.Status == ProcurementSupplierOnboardingPaymentStatus.Pending)) ||
                item.PaymentStatus == ProcurementSupplierOnboardingPaymentStatus.Posted,
                cancellationToken),
            PostedAmount = await Payments.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                    (item.Status == ProcurementSupplierOnboardingPaymentStatus.Posted ||
                     item.Status == ProcurementSupplierOnboardingPaymentStatus.Reconciled))
                .SumAsync(item => (decimal?)item.TotalAmount, cancellationToken) ?? 0m
        };
    }

    public async Task<ProcurementSupplierOnboardingTokenPageDto> SearchAsync(
        ProcurementSupplierOnboardingTokenSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        request.Page = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = TokenQuery().Include(item => item.Registration).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.TokenReference.Contains(search) ||
                item.Registration.RegistrationNumber.Contains(search) ||
                item.Registration.ApplicantName.Contains(search));
        }
        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);
        if (request.PaymentStatus.HasValue)
            query = query.Where(item => item.PaymentStatus == request.PaymentStatus.Value);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.IssuedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return new ProcurementSupplierOnboardingTokenPageDto
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total,
            Items = rows.Select(MapList).ToList()
        };
    }

    public async Task<ProcurementSupplierOnboardingTokenDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await LoadAsync(id, tracked: false, cancellationToken);
        await EnsureReaderAsync(entity, cancellationToken);
        return Map(entity);
    }

    public async Task<ProcurementSupplierOnboardingTokenDto?> GetForRegistrationAsync(
        Guid registrationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var registration = await Registrations.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == registrationId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (registration is null)
            throw NotFound("SUPPLIER_ONBOARDING_REGISTRATION_NOT_FOUND",
                "The supplier registration was not found for this tenant.");
        await EnsureRegistrationReadAccessAsync(registration, cancellationToken);

        var token = await TokenQuery()
            .Include(item => item.Registration)
            .Include(item => item.Payments)
            .Include(item => item.Exemptions)
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.RegistrationId == registrationId, cancellationToken);
        return token is null ? null : Map(token);
    }

    public async Task<IReadOnlyList<ProcurementSupplierOnboardingPaymentMethodOptionDto>>
        GetPaymentMethodsAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var token = await LoadAsync(tokenId, tracked: false, cancellationToken);
        await EnsureReaderAsync(token, cancellationToken);
        var allowed = Deserialize<List<string>>(token.PaymentChannelsJson)
            .Select(NormalizeCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return await PaymentMethods.GetQueryable(item =>
                item.TenantId == token.TenantId && !item.IsDeleted && item.IsActive &&
                item.Code != null && allowed.Contains(item.Code))
            .AsNoTracking().OrderBy(item => item.Name)
            .Select(item => new ProcurementSupplierOnboardingPaymentMethodOptionDto
            {
                Id = item.Id,
                Code = item.Code!,
                Name = item.Name,
                RequiresReference = item.RequiresReference,
                IsPostingReady = item.DefaultGLAccountId.HasValue
            }).ToListAsync(cancellationToken);
    }

    public async Task<ProcurementSupplierOnboardingTokenIssueResultDto> IssueAsync(
        IssueProcurementSupplierOnboardingTokenRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var replay = await TokenQuery().Include(item => item.Registration)
            .Include(item => item.Payments).Include(item => item.Exemptions)
            .AsNoTracking().SingleOrDefaultAsync(item =>
                item.CreationCorrelationId == correlation, cancellationToken);
        if (replay is not null)
        {
            if (replay.RegistrationId != request.RegistrationId)
                throw Conflict(
                    "SUPPLIER_ONBOARDING_IDEMPOTENCY_MISMATCH",
                    "The supplied correlation id was already used for a different supplier registration.");
            await EnsureReaderAsync(replay, cancellationToken);
            return new ProcurementSupplierOnboardingTokenIssueResultDto { Token = Map(replay) };
        }

        var registration = await Registrations.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == request.RegistrationId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("SUPPLIER_ONBOARDING_REGISTRATION_NOT_FOUND",
                "The supplier registration was not found for this tenant.");
        await EnsureApplicantOwnerOrCapabilityAsync(
            registration, ManagePermission, $"registration:{registration.Id:N}",
            correlation, cancellationToken);
        EnsureRegistrationActive(registration);
        if (await TokenQuery().AnyAsync(item => item.RegistrationId == registration.Id,
                cancellationToken))
            throw Conflict("SUPPLIER_ONBOARDING_TOKEN_EXISTS",
                "This supplier registration already has an application-bound token.");

        return await IssueCoreAsync(
            registration,
            _currentUser.TenantId,
            correlation,
            DateTime.UtcNow,
            systemEvent: false,
            cancellationToken: cancellationToken);
    }

    public async Task<ProcurementSupplierOnboardingTokenIssueResultDto>
        IssueForVerifiedApplicantAsync(
            Guid tenantId,
            Guid registrationId,
            string correlationId,
            CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw Validation("SUPPLIER_ONBOARDING_TENANT_REQUIRED",
                "A tenant is required for verified applicant token issue.");
        var correlation = NormalizeCorrelation(correlationId);
        var replay = await Tokens.GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                item.CreationCorrelationId == correlation)
            .Include(item => item.Registration)
            .Include(item => item.Payments)
            .Include(item => item.Exemptions)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (replay is not null)
        {
            if (replay.RegistrationId != registrationId)
                throw Conflict(
                    "SUPPLIER_ONBOARDING_IDEMPOTENCY_MISMATCH",
                    "The supplied correlation id was already used for a different supplier registration.");
            return new ProcurementSupplierOnboardingTokenIssueResultDto { Token = Map(replay) };
        }

        var registration = await Registrations.GetQueryable(item =>
                item.TenantId == tenantId && item.Id == registrationId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("SUPPLIER_ONBOARDING_REGISTRATION_NOT_FOUND",
                "The supplier registration was not found for this tenant.");
        EnsureRegistrationActive(registration);
        if (await Tokens.GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                item.RegistrationId == registration.Id)
            .AnyAsync(cancellationToken))
            throw Conflict("SUPPLIER_ONBOARDING_TOKEN_EXISTS",
                "This supplier registration already has an application-bound token.");

        return await IssueCoreAsync(
            registration,
            tenantId,
            correlation,
            DateTime.UtcNow,
            systemEvent: true,
            cancellationToken: cancellationToken);
    }

    public async Task<ProcurementSupplierOnboardingTokenDto> ValidateApplicantTokenAsync(
        Guid tenantId,
        string plaintextToken,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw Validation("SUPPLIER_ONBOARDING_TENANT_REQUIRED",
                "A tenant is required for applicant token authentication.");
        if (string.IsNullOrWhiteSpace(plaintextToken))
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "The application token is required.");

        var correlation = NormalizeCorrelation(correlationId);
        var hash = Hash(plaintextToken.Trim());
        var entity = await Tokens.GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                item.TokenHashSha256 == hash)
            .Include(item => item.Registration)
            .Include(item => item.Payments)
            .Include(item => item.Exemptions)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (entity is null)
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "The application token is invalid.");

        var terminal = entity.Status == ProcurementSupplierOnboardingTokenStatus.Expired ||
            string.Equals(entity.Registration.Status, "Approved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entity.Registration.Status, "Rejected", StringComparison.OrdinalIgnoreCase);
        if (terminal)
        {
            await RecordEventAsync(
                entity,
                "ApplicantAccessDeniedAfterCompletion",
                ProcurementControlEventResult.Denied,
                null,
                new
                {
                    entity.RegistrationId,
                    RegistrationStatus = entity.Registration.Status,
                    TokenStatus = entity.Status.ToString()
                },
                "Application-token access was denied after the application completed.",
                [],
                correlation,
                DateTime.UtcNow,
                cancellationToken,
                systemEvent: true);
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "This application is complete and its token can no longer be used.");
        }

        await RecordEventAsync(
            entity,
            "ApplicantTokenAuthenticated",
            ProcurementControlEventResult.Allowed,
            null,
            new
            {
                entity.RegistrationId,
                RegistrationStatus = entity.Registration.Status,
                TokenStatus = entity.Status.ToString(),
                PaymentStatus = entity.PaymentStatus.ToString()
            },
            "Application token accepted for a restricted applicant session.",
            [],
            correlation,
            DateTime.UtcNow,
            cancellationToken,
            systemEvent: true);
        return Map(entity);
    }

    private async Task<ProcurementSupplierOnboardingTokenIssueResultDto> IssueCoreAsync(
        BusinessPartnerRegistration registration,
        Guid tenantId,
        string correlation,
        DateTime effectiveAt,
        bool systemEvent,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveDecisionAsync(effectiveAt, tenantId, cancellationToken);
        var tokenValue = GenerateTokenValue();
        var now = DateTime.UtcNow;
        var taxAmount = RoundMoney(resolved.Value.Amount * resolved.Value.TaxPercent / 100m);
        var free = resolved.Value.Mode == ProcurementSupplierOnboardingFeeMode.Free;
        var entity = new ProcurementSupplierOnboardingToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegistrationId = registration.Id,
            TokenReference = $"TOK-{now:yyyy}-{Guid.NewGuid():N}"[..22].ToUpperInvariant(),
            TokenHashSha256 = Hash(tokenValue),
            TokenLastFour = tokenValue[^4..],
            Generation = 1,
            Status = free
                ? ProcurementSupplierOnboardingTokenStatus.Active
                : ProcurementSupplierOnboardingTokenStatus.AwaitingPayment,
            PaymentStatus = free
                ? ProcurementSupplierOnboardingPaymentStatus.NotRequired
                : ProcurementSupplierOnboardingPaymentStatus.Pending,
            IssuedAtUtc = now,
            ActivatedAtUtc = free ? now : null,
            SourceConfigurationProfileId = resolved.Profile.Id,
            SourceConfigurationProfileCode = resolved.Profile.ProfileCode,
            SourceConfigurationProfileVersion = resolved.Profile.Version,
            SourceConfigurationDecisionId = resolved.Decision.Id,
            FeeMode = resolved.Value.Mode,
            FeeType = resolved.Value.FeeType.Trim(),
            FeeAmount = RoundMoney(resolved.Value.Amount),
            TaxPercent = resolved.Value.TaxPercent,
            TaxAmount = taxAmount,
            TotalAmount = RoundMoney(resolved.Value.Amount + taxAmount),
            CurrencyCode = resolved.Value.CurrencyCode.Trim().ToUpperInvariant(),
            RevenueAccountId = resolved.Value.RevenueAccountId,
            TaxAccountId = resolved.Value.TaxAccountId,
            ExemptionWorkflowDefinitionId = resolved.Value.ExemptionWorkflowDefinitionId,
            PaymentChannelsJson = Serialize(resolved.Value.PaymentChannels
                .Select(NormalizeCode).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item).ToList()),
            ReceiptNumberFormat = resolved.Value.ReceiptNumberFormat.Trim(),
            ExemptionRule = resolved.Value.ExemptionRule.Trim(),
            RefundRule = resolved.Value.RefundRule.Trim(),
            RenewalRule = resolved.Value.RenewalRule.Trim(),
            DecisionSnapshotJson = resolved.SnapshotJson,
            DecisionSnapshotHash = Hash(resolved.SnapshotJson),
            CreationCorrelationId = correlation,
            LastOperationCorrelationId = correlation,
            LastOperation = "Issued",
            CreatedAt = now,
            CreatedBy = systemEvent ? "Verified Supplier Applicant" : ActorName,
            CreatedById = systemEvent ? null : _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        Capture(entity);

        await ExecuteAsync(async () =>
        {
            await Tokens.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Issued",
                ProcurementControlEventResult.Succeeded, null, Snapshot(entity),
                free ? "Free token activated from effective DEC-007." :
                    "Paid token issued and awaiting payment.",
                [], correlation, now, cancellationToken, systemEvent);
        }, cancellationToken, joinCallerTransaction: systemEvent);
        if (!systemEvent)
        {
            await PublishNotificationAsync("procurement.supplier-onboarding-token.issued",
                entity, cancellationToken);
        }

        return new ProcurementSupplierOnboardingTokenIssueResultDto
        {
            Token = Map(entity),
            PlaintextToken = free || !systemEvent ? tokenValue : null
        };
    }

    public async Task<ProcurementSupplierOnboardingTokenIssueResultDto> ReissueAsync(
        Guid id,
        ReissueProcurementSupplierOnboardingTokenRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, tracked: true, cancellationToken);
        await EnsureReaderAsync(entity, cancellationToken);
        await EnsureApplicantOwnerOrCapabilityAsync(
            entity.Registration, ManagePermission, entity.TokenReference,
            correlation, cancellationToken);
        if (IsReplay(entity.LastOperation, entity.LastOperationCorrelationId, "Reissued", correlation))
            return new ProcurementSupplierOnboardingTokenIssueResultDto { Token = Map(entity) };
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status == ProcurementSupplierOnboardingTokenStatus.Expired)
            throw Conflict("SUPPLIER_ONBOARDING_TOKEN_TERMINAL",
                "A token cannot be reissued after the linked application is Approved or Rejected.");
        if (entity.FeeMode == ProcurementSupplierOnboardingFeeMode.Paid &&
            entity.Status == ProcurementSupplierOnboardingTokenStatus.AwaitingPayment)
            throw Conflict("SUPPLIER_ONBOARDING_TOKEN_PAYMENT_REQUIRED",
                "A paid application token cannot be generated or reissued before trusted payment verification or approved exemption.");

        var tokenValue = GenerateTokenValue();
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.TokenHashSha256 = Hash(tokenValue);
        entity.TokenLastFour = tokenValue[^4..];
        entity.Generation++;
        entity.ReissuedAtUtc = now;
        entity.ReissuedById = _currentUser.UserId;
        entity.ReissueReason = request.Reason.Trim();
        Touch(entity, "Reissued", correlation, now);
        Capture(entity);
        await ExecuteAsync(async () =>
        {
            var activeSessions = await ApplicantSessions.GetQueryable(item =>
                    item.TenantId == entity.TenantId &&
                    !item.IsDeleted &&
                    item.Status == ProcurementSupplierApplicantSessionStatus.Active &&
                    item.ApplicantAccess.TenantId == entity.TenantId &&
                    !item.ApplicantAccess.IsDeleted &&
                    item.ApplicantAccess.TokenId == entity.Id)
                .ToListAsync(cancellationToken);
            foreach (var session in activeSessions)
            {
                session.Status = ProcurementSupplierApplicantSessionStatus.Revoked;
                session.RevokedAtUtc = now;
                session.RevocationReason = "Application token reissued.";
                Touch(session, now);
                Capture(session);
                await ApplicantSessions.UpdateAsync(session);
            }

            await Tokens.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Reissued",
                ProcurementControlEventResult.Succeeded, before, new
                {
                    Token = Snapshot(entity),
                    RevokedApplicantSessions = activeSessions.Select(item => new
                    {
                        item.SessionReference,
                        item.RevokedAtUtc,
                        item.RevocationReason
                    }).ToList()
                },
                request.Reason, [], correlation, now, cancellationToken);
        }, cancellationToken);
        return new ProcurementSupplierOnboardingTokenIssueResultDto
        {
            Token = Map(entity),
            PlaintextToken = tokenValue
        };
    }

    public Task<ProcurementSupplierOnboardingTokenIssueResultDto> RecordPaymentAsync(
        Guid id,
        RecordProcurementSupplierOnboardingPaymentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        RecordPaymentCoreAsync(
            id, null, request, correlationId, cancellationToken);

    public async Task<ProcurementSupplierOnboardingTokenIssueResultDto>
        RecordApplicantPaymentAsync(
            Guid id,
            Guid applicantSessionId,
            RecordProcurementSupplierOnboardingPaymentRequest request,
            string correlationId,
            CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.IsExternalUser ||
            !string.Equals(
                _currentUser.AuthenticationProvider,
                "ApplicantToken",
                StringComparison.OrdinalIgnoreCase) ||
            _currentUser.Claims is null ||
            !_currentUser.Claims.TryGetValue(
                "supplier_applicant_session",
                out var sessionReferenceClaim) ||
            !Guid.TryParse(sessionReferenceClaim, out var sessionReference))
        {
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "A validated applicant-only session is required to submit this payment claim.");
        }

        var now = DateTime.UtcNow;
        var session = await ApplicantSessions.GetQueryable(item =>
                item.Id == applicantSessionId &&
                item.TenantId == _currentUser.TenantId &&
                item.SessionReference == sessionReference &&
                item.Status == ProcurementSupplierApplicantSessionStatus.Active &&
                item.ExpiresAtUtc > now &&
                !item.IsDeleted &&
                item.ApplicantAccess.TokenId == id &&
                item.ApplicantAccess.Status ==
                    ProcurementSupplierApplicantAccessStatus.ApplicationInProgress &&
                !item.ApplicantAccess.TerminalAtUtc.HasValue &&
                !item.ApplicantAccess.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "The applicant session is not active or is not bound to this token.");

        return await RecordPaymentCoreAsync(
            id, session.Id, request, correlationId, cancellationToken);
    }

    private async Task<ProcurementSupplierOnboardingTokenIssueResultDto>
        RecordPaymentCoreAsync(
            Guid id,
            Guid? submittedByApplicantSessionId,
            RecordProcurementSupplierOnboardingPaymentRequest request,
            string correlationId,
            CancellationToken cancellationToken)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, tracked: true, cancellationToken);
        await EnsureReaderAsync(entity, cancellationToken);
        await EnsureApplicantOwnerOrCapabilityAsync(
            entity.Registration, ManagePermission, entity.TokenReference,
            correlation, cancellationToken);
        var replay = entity.Payments.SingleOrDefault(item =>
            item.CreationCorrelationId == correlation);
        if (replay is not null)
        {
            if (replay.PaymentMethodId != request.PaymentMethodId ||
                !string.Equals(
                    replay.PaymentReference,
                    Trim(request.PaymentReference, 200),
                    StringComparison.Ordinal))
            {
                throw Conflict(
                    "SUPPLIER_ONBOARDING_IDEMPOTENCY_MISMATCH",
                    "The supplied correlation id was already used for a different payment claim.");
            }
            return new ProcurementSupplierOnboardingTokenIssueResultDto { Token = Map(entity) };
        }
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        if (entity.FeeMode != ProcurementSupplierOnboardingFeeMode.Paid)
            throw Conflict("SUPPLIER_ONBOARDING_PAYMENT_NOT_REQUIRED",
                "The effective DEC-007 decision configured this token as free.");
        if (entity.Status == ProcurementSupplierOnboardingTokenStatus.Expired)
            throw Conflict("SUPPLIER_ONBOARDING_TOKEN_TERMINAL",
                "Payment cannot be recorded after the application is Approved or Rejected.");
        if (entity.PaymentStatus is ProcurementSupplierOnboardingPaymentStatus.Posted or
            ProcurementSupplierOnboardingPaymentStatus.Reconciled or
            ProcurementSupplierOnboardingPaymentStatus.Exempt)
            throw Conflict("SUPPLIER_ONBOARDING_PAYMENT_COMPLETE",
                "This token is already paid or exempt.");
        if (entity.Payments.Any(item =>
                item.Status == ProcurementSupplierOnboardingPaymentStatus.Pending))
            throw Conflict("SUPPLIER_ONBOARDING_PAYMENT_VERIFICATION_PENDING",
                "A submitted payment is already awaiting trusted cashier or provider verification.");

        var method = await PaymentMethods.GetQueryable(item =>
                item.TenantId == entity.TenantId && item.Id == request.PaymentMethodId &&
                !item.IsDeleted && item.IsActive)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("SUPPLIER_ONBOARDING_PAYMENT_METHOD_INVALID",
                "The selected payment method is not active for this tenant.");
        var methodCode = NormalizeCode(method.Code);
        var allowed = Deserialize<List<string>>(entity.PaymentChannelsJson)
            .Select(NormalizeCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!allowed.Contains(methodCode))
            throw Validation("SUPPLIER_ONBOARDING_PAYMENT_CHANNEL_NOT_ALLOWED",
                "The selected payment method is not allowed by the effective DEC-007 decision.");
        if (!method.DefaultGLAccountId.HasValue)
            throw Validation("SUPPLIER_ONBOARDING_PAYMENT_METHOD_GL_MISSING",
                "The selected payment method has no default receiving GL account.");
        if (method.RequiresReference && string.IsNullOrWhiteSpace(request.PaymentReference))
            throw Validation("SUPPLIER_ONBOARDING_PAYMENT_REFERENCE_REQUIRED",
                "The selected payment method requires a payment reference.");
        if (!entity.RevenueAccountId.HasValue)
            throw Validation("SUPPLIER_ONBOARDING_REVENUE_GL_MISSING",
                "The effective DEC-007 decision has no revenue GL account.");
        if (entity.TaxAmount > 0 && !entity.TaxAccountId.HasValue)
            throw Validation("SUPPLIER_ONBOARDING_TAX_GL_MISSING",
                "The effective DEC-007 decision has no tax payable GL account.");
        var now = DateTime.UtcNow;
        var payment = new ProcurementSupplierOnboardingPayment
        {
            Id = Guid.NewGuid(),
            TenantId = entity.TenantId,
            TokenId = entity.Id,
            SubmittedByApplicantSessionId = submittedByApplicantSessionId,
            PaymentMethodId = method.Id,
            PaymentMethodCode = methodCode,
            PaymentMethodName = method.Name,
            PaymentReference = Trim(request.PaymentReference, 200),
            FeeAmount = entity.FeeAmount,
            TaxAmount = entity.TaxAmount,
            TotalAmount = entity.TotalAmount,
            CurrencyCode = entity.CurrencyCode,
            Status = ProcurementSupplierOnboardingPaymentStatus.Pending,
            // This timestamp is server-derived. It represents the submitted
            // payment claim until a trusted internal verifier confirms funds.
            PaidAtUtc = now,
            CreationCorrelationId = correlation,
            LastOperationCorrelationId = correlation,
            LastOperation = "Recorded",
            CreatedAt = now,
            CreatedBy = submittedByApplicantSessionId.HasValue
                ? "Verified Supplier Applicant"
                : ActorName,
            CreatedById = submittedByApplicantSessionId.HasValue
                ? null
                : _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        Capture(payment);
        var before = Snapshot(entity);

        await ExecuteAsync(async () =>
        {
            await Payments.AddAsync(payment);
            entity.PaymentStatus = ProcurementSupplierOnboardingPaymentStatus.Pending;
            Touch(entity, "PaymentSubmitted", correlation, now);
            Capture(entity);
            await Tokens.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "PaymentSubmitted",
                ProcurementControlEventResult.Succeeded, before,
                new { Token = Snapshot(entity), Payment = PaymentSnapshot(payment) },
                "Applicant payment claim is pending trusted cashier or provider verification.",
                [], correlation, now, cancellationToken,
                systemEvent: submittedByApplicantSessionId.HasValue);
        }, cancellationToken);
        await PublishNotificationAsync(
            "procurement.supplier-onboarding-token.payment-submitted",
            entity,
            cancellationToken,
            systemEvent: submittedByApplicantSessionId.HasValue);
        return new ProcurementSupplierOnboardingTokenIssueResultDto { Token = Map(entity) };
    }

    public async Task<ProcurementSupplierOnboardingTokenIssueResultDto> ReconcilePaymentAsync(
        Guid tokenId,
        Guid paymentId,
        ReconcileProcurementSupplierOnboardingPaymentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(tokenId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(
            ProcurementAccessControlRegistry.SupplierPaymentVerifyPermission,
            entity.TokenReference,
            correlation,
            cancellationToken);
        var payment = entity.Payments.SingleOrDefault(item => item.Id == paymentId)
            ?? throw NotFound("SUPPLIER_ONBOARDING_PAYMENT_NOT_FOUND",
                "The token payment was not found.");
        var reconciliationReference = request.ReconciliationReference?.Trim();
        if (string.IsNullOrWhiteSpace(reconciliationReference))
            throw Validation(
                "SUPPLIER_ONBOARDING_RECONCILIATION_REFERENCE_REQUIRED",
                "A trusted cashier receipt or provider transaction reference is required.");
        var reconciliationNotes = Trim(request.Notes, 1000);
        if (IsReplay(payment.LastOperation, payment.LastOperationCorrelationId,
                "Verified", correlation))
        {
            if (!string.Equals(
                    payment.ReconciliationReference,
                    reconciliationReference,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    payment.ReconciliationNotes,
                    reconciliationNotes,
                    StringComparison.Ordinal))
            {
                throw Conflict(
                    "SUPPLIER_ONBOARDING_IDEMPOTENCY_MISMATCH",
                    "The supplied correlation id was already used for a different payment-verification decision.");
            }
            return new ProcurementSupplierOnboardingTokenIssueResultDto
            {
                Token = Map(entity)
            };
        }
        EnsureRowVersion(payment.RowVersion, request.RowVersion);
        if (payment.Status is not (
                ProcurementSupplierOnboardingPaymentStatus.Pending or
                ProcurementSupplierOnboardingPaymentStatus.Posted))
            throw Conflict("SUPPLIER_ONBOARDING_PAYMENT_NOT_VERIFIABLE",
                "Only a pending payment claim or legacy Finance-posted payment can be verified.");
        if (!payment.SubmittedByApplicantSessionId.HasValue)
        {
            await EnsureIndependentActorAsync(payment.CreatedById, entity.TokenReference,
                correlation, cancellationToken);
        }

        var applicantBound = await ApplicantAccesses.GetQueryable(item =>
                item.TenantId == entity.TenantId && item.TokenId == entity.Id &&
                !item.IsDeleted)
            .AnyAsync(cancellationToken);
        var before = PaymentSnapshot(payment);
        var now = DateTime.UtcNow;
        var tokenValue = applicantBound ? GenerateTokenValue() : null;
        await ExecuteAsync(async () =>
        {
            var revokedApplicantSessions = tokenValue is null
                ? []
                : await RevokeActiveApplicantSessionsAsync(
                    entity,
                    "Payment verified; sign in with the newly delivered application token.",
                    now,
                    cancellationToken);
            if (payment.Status == ProcurementSupplierOnboardingPaymentStatus.Pending)
            {
                var method = await PaymentMethods.GetQueryable(item =>
                        item.TenantId == entity.TenantId &&
                        item.Id == payment.PaymentMethodId &&
                        !item.IsDeleted && item.IsActive)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw Validation("SUPPLIER_ONBOARDING_PAYMENT_METHOD_INVALID",
                        "The submitted payment method is no longer active for this tenant.");
                if (!method.DefaultGLAccountId.HasValue)
                    throw Validation("SUPPLIER_ONBOARDING_PAYMENT_METHOD_GL_MISSING",
                        "The submitted payment method has no default receiving GL account.");
                if (!entity.RevenueAccountId.HasValue)
                    throw Validation("SUPPLIER_ONBOARDING_REVENUE_GL_MISSING",
                        "The effective DEC-007 decision has no revenue GL account.");
                if (entity.TaxAmount > 0 && !entity.TaxAccountId.HasValue)
                    throw Validation("SUPPLIER_ONBOARDING_TAX_GL_MISSING",
                        "The effective DEC-007 decision has no tax payable GL account.");
                await ValidatePostingAccountsAsync(entity, method, cancellationToken);
                await ValidateFunctionalCurrencyAsync(entity, cancellationToken);

                var posting = await _financePosting.PostAsync(BuildPostingRequest(
                    entity,
                    payment,
                    method.DefaultGLAccountId.Value,
                    reconciliationReference,
                    now), cancellationToken);
                var receipt = await _documentNumbering.GenerateConfiguredAsync(
                    DocumentNumberingModules.Procurement,
                    $"SupplierOnboardingReceipt-{entity.SourceConfigurationProfileId:N}",
                    $"Supplier onboarding receipt {entity.SourceConfigurationProfileCode} v{entity.SourceConfigurationProfileVersion}",
                    entity.ReceiptNumberFormat,
                    ResolveResetPolicy(entity.ReceiptNumberFormat),
                    entity.TenantId,
                    now,
                    nameof(ProcurementSupplierOnboardingPayment),
                    payment.Id,
                    cancellationToken);
                if (receipt.Length > 50)
                    throw Validation("SUPPLIER_ONBOARDING_RECEIPT_NUMBER_TOO_LONG",
                        "The configured DEC-007 receipt format produced a number longer than 50 characters.");

                // Do not mutate protected payment lineage before Finance has
                // completed validation and posting. Finance audit writes share
                // this unit of work and may save tracked entities while
                // reporting a blocked posting. The verified payment timestamp
                // must therefore be persisted only with Pending -> Posted.
                payment.PaidAtUtc = now;
                payment.PostingEventId = posting.PostingEventId;
                payment.JournalEntryId = posting.JournalEntryId;
                payment.PostedAtUtc = now;
                payment.ReceiptNumber = receipt;
                payment.ReceiptIssuedAtUtc = now;
                payment.FailureReason = null;
                // Persist the trusted Finance-posted state before completing
                // reconciliation. SQL protects the lifecycle as Pending ->
                // Posted -> Reconciled and must never observe a direct jump.
                payment.Status = ProcurementSupplierOnboardingPaymentStatus.Posted;
                Touch(payment, "Posted", correlation, now);
                Capture(payment);
                // The parent token has its own SQL-protected payment lifecycle.
                // Persist it in lockstep with the payment row so the database
                // observes Pending -> Posted before the final Posted ->
                // Reconciled transition below.
                entity.PaymentStatus = ProcurementSupplierOnboardingPaymentStatus.Posted;
                Touch(entity, "PaymentPosted", correlation, now);
                Capture(entity);
                await Payments.UpdateAsync(payment);
                await Tokens.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            payment.ReconciledAtUtc = now;
            payment.ReconciledById = _currentUser.UserId;
            payment.ReconciliationReference = reconciliationReference;
            payment.ReconciliationNotes = reconciliationNotes;
            payment.Status = ProcurementSupplierOnboardingPaymentStatus.Reconciled;
            Touch(payment, "Verified", correlation, now);
            Capture(payment);
            entity.Status = ProcurementSupplierOnboardingTokenStatus.Active;
            entity.PaymentStatus = ProcurementSupplierOnboardingPaymentStatus.Reconciled;
            entity.ActivatedAtUtc ??= now;
            if (tokenValue is not null)
            {
                entity.TokenHashSha256 = Hash(tokenValue);
                entity.TokenLastFour = tokenValue[^4..];
                entity.Generation++;
            }
            Touch(entity, "PaymentVerified", correlation, now);
            Capture(entity);
            await Payments.UpdateAsync(payment);
            await Tokens.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "PaymentVerified",
                ProcurementControlEventResult.Succeeded, before, new
                {
                    Payment = PaymentSnapshot(payment),
                    TokenGeneration = entity.Generation,
                    TokenLastFour = entity.TokenLastFour,
                    RevokedApplicantSessions = revokedApplicantSessions
                },
                request.Notes, [], correlation, now, cancellationToken);
        }, cancellationToken);
        await PublishNotificationAsync(
            "procurement.supplier-onboarding-token.payment-verified",
            entity, cancellationToken);
        return new ProcurementSupplierOnboardingTokenIssueResultDto
        {
            Token = Map(entity),
            PlaintextToken = tokenValue
        };
    }

    public async Task<ProcurementSupplierOnboardingTokenDto> RequestExemptionAsync(
        Guid id,
        RequestProcurementSupplierOnboardingExemptionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, tracked: true, cancellationToken);
        await EnsureReaderAsync(entity, cancellationToken);
        await EnsureApplicantOwnerOrCapabilityAsync(
            entity.Registration, ManagePermission, entity.TokenReference,
            correlation, cancellationToken);
        if (entity.Exemptions.Any(item => item.CreationCorrelationId == correlation))
            return Map(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        if (entity.FeeMode != ProcurementSupplierOnboardingFeeMode.Paid)
            throw Conflict("SUPPLIER_ONBOARDING_EXEMPTION_NOT_REQUIRED",
                "A free token does not require a fee exemption.");
        if (entity.Status == ProcurementSupplierOnboardingTokenStatus.Expired)
            throw Conflict("SUPPLIER_ONBOARDING_TOKEN_TERMINAL",
                "An exemption cannot be requested after the application is Approved or Rejected.");
        if (!entity.ExemptionWorkflowDefinitionId.HasValue)
            throw Validation("SUPPLIER_ONBOARDING_EXEMPTION_WORKFLOW_MISSING",
                "The effective DEC-007 decision does not configure an exemption workflow.");
        EnsureEvidence(request.Evidence);
        if (entity.Exemptions.Any(item =>
            item.Status == ProcurementSupplierOnboardingExemptionStatus.PendingApproval))
            throw Conflict("SUPPLIER_ONBOARDING_EXEMPTION_PENDING",
                "This token already has an exemption awaiting a decision.");

        var workflow = await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == entity.TenantId &&
                item.Id == entity.ExemptionWorkflowDefinitionId.Value &&
                !item.IsDeleted && item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("SUPPLIER_ONBOARDING_EXEMPTION_WORKFLOW_INVALID",
                "The configured exemption workflow is not an active Published workflow.");
        var now = DateTime.UtcNow;
        var evidenceJson = Serialize(request.Evidence);
        var exemption = new ProcurementSupplierOnboardingExemption
        {
            Id = Guid.NewGuid(),
            TenantId = entity.TenantId,
            TokenId = entity.Id,
            Reason = request.Reason.Trim(),
            Status = ProcurementSupplierOnboardingExemptionStatus.PendingApproval,
            WorkflowDefinitionId = workflow.Id,
            RequestedById = _currentUser.UserId,
            RequestedAtUtc = now,
            EvidenceJson = evidenceJson,
            EvidenceHash = Hash(evidenceJson),
            CreationCorrelationId = correlation,
            LastOperationCorrelationId = correlation,
            LastOperation = "Requested",
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        Capture(exemption);
        await ExecuteAsync(async () =>
        {
            await Exemptions.AddAsync(exemption);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var instance = await _workflowInstances.StartWorkflowAsync(
                workflow.Id, workflow.EntityTypeId, exemption.Id.ToString(),
                _currentUser.UserId,
                new
                {
                    entity.TokenReference,
                    entity.RegistrationId,
                    entity.TotalAmount,
                    entity.CurrencyCode,
                    exemption.Reason
                }, cancellationToken);
            exemption.WorkflowInstanceId = instance.Id;
            Touch(exemption, "Requested", correlation, now);
            Capture(exemption);
            Touch(entity, "ExemptionRequested", correlation, now);
            Capture(entity);
            await Exemptions.UpdateAsync(exemption);
            await Tokens.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "ExemptionRequested",
                ProcurementControlEventResult.Succeeded, null, ExemptionSnapshot(exemption),
                request.Reason, request.Evidence, correlation, now, cancellationToken);
        }, cancellationToken);
        await PublishNotificationAsync(
            "procurement.supplier-onboarding-token.exemption-requested",
            entity, cancellationToken);
        return Map(entity);
    }

    public async Task<ProcurementSupplierOnboardingTokenIssueResultDto> DecideExemptionAsync(
        Guid tokenId,
        Guid exemptionId,
        DecideProcurementSupplierOnboardingExemptionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(tokenId, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ReviewPermission, entity.TokenReference, correlation,
            cancellationToken);
        var exemption = entity.Exemptions.SingleOrDefault(item => item.Id == exemptionId)
            ?? throw NotFound("SUPPLIER_ONBOARDING_EXEMPTION_NOT_FOUND",
                "The token exemption was not found.");
        var action = request.Approve ? "ExemptionApproved" : "ExemptionRejected";
        if (IsReplay(exemption.LastOperation, exemption.LastOperationCorrelationId,
                action, correlation))
            return new ProcurementSupplierOnboardingTokenIssueResultDto
            {
                Token = Map(entity)
            };
        EnsureRowVersion(exemption.RowVersion, request.RowVersion);
        EnsureEvidence(request.Evidence);
        if (exemption.Status != ProcurementSupplierOnboardingExemptionStatus.PendingApproval)
            throw Conflict("SUPPLIER_ONBOARDING_EXEMPTION_DECIDED",
                "This exemption already has a terminal decision.");
        await EnsureIndependentActorAsync(exemption.RequestedById, entity.TokenReference,
            correlation, cancellationToken);
        var instance = exemption.WorkflowInstanceId.HasValue
            ? await _workflowInstances.GetInstanceAsync(
                exemption.WorkflowInstanceId.Value, cancellationToken)
            : null;
        if (instance is null || instance.TenantId != entity.TenantId ||
            instance.WorkflowDefinitionId != exemption.WorkflowDefinitionId ||
            instance.EntityId != exemption.Id)
            throw Conflict("SUPPLIER_ONBOARDING_EXEMPTION_WORKFLOW_INVALID",
                "The exemption workflow instance is missing or does not match this request.");
        if (request.Approve && instance.Status != WorkflowInstanceStatus.Completed)
            throw Conflict("SUPPLIER_ONBOARDING_EXEMPTION_WORKFLOW_NOT_APPROVED",
                "Only a Completed shared workflow permits exemption approval.");
        if (!request.Approve &&
            instance.Status is not (WorkflowInstanceStatus.Cancelled or
                WorkflowInstanceStatus.Failed))
            throw Conflict("SUPPLIER_ONBOARDING_EXEMPTION_WORKFLOW_NOT_REJECTED",
                "Only a Cancelled or Failed shared workflow permits exemption rejection.");

        var applicantBound = request.Approve &&
            await ApplicantAccesses.GetQueryable(item =>
                    item.TenantId == entity.TenantId && item.TokenId == entity.Id &&
                    !item.IsDeleted)
                .AnyAsync(cancellationToken);
        var before = ExemptionSnapshot(exemption);
        var now = DateTime.UtcNow;
        var tokenValue = applicantBound ? GenerateTokenValue() : null;
        exemption.Status = request.Approve
            ? ProcurementSupplierOnboardingExemptionStatus.Approved
            : ProcurementSupplierOnboardingExemptionStatus.Rejected;
        exemption.DecidedById = _currentUser.UserId;
        exemption.DecidedAtUtc = now;
        exemption.DecisionComment = request.Comment.Trim();
        exemption.EvidenceJson = Serialize(request.Evidence);
        exemption.EvidenceHash = Hash(exemption.EvidenceJson);
        Touch(exemption, action, correlation, now);
        Capture(exemption);
        if (request.Approve)
        {
            entity.Status = ProcurementSupplierOnboardingTokenStatus.Active;
            entity.PaymentStatus = ProcurementSupplierOnboardingPaymentStatus.Exempt;
            entity.ActivatedAtUtc ??= now;
            if (tokenValue is not null)
            {
                entity.TokenHashSha256 = Hash(tokenValue);
                entity.TokenLastFour = tokenValue[^4..];
                entity.Generation++;
            }
        }
        Touch(entity, action, correlation, now);
        Capture(entity);
        await ExecuteAsync(async () =>
        {
            var revokedApplicantSessions = tokenValue is null
                ? []
                : await RevokeActiveApplicantSessionsAsync(
                    entity,
                    "Exemption approved; sign in with the newly delivered application token.",
                    now,
                    cancellationToken);
            await Exemptions.UpdateAsync(exemption);
            await Tokens.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, action,
                request.Approve
                    ? ProcurementControlEventResult.Succeeded
                    : ProcurementControlEventResult.Rejected,
                before, new
                {
                    Exemption = ExemptionSnapshot(exemption),
                    TokenGeneration = entity.Generation,
                    TokenLastFour = request.Approve ? entity.TokenLastFour : null,
                    RevokedApplicantSessions = revokedApplicantSessions
                }, request.Comment,
                request.Evidence, correlation, now, cancellationToken);
        }, cancellationToken);
        await PublishNotificationAsync(
            request.Approve
                ? "procurement.supplier-onboarding-token.exemption-approved"
                : "procurement.supplier-onboarding-token.exemption-rejected",
            entity, cancellationToken);
        return new ProcurementSupplierOnboardingTokenIssueResultDto
        {
            Token = Map(entity),
            PlaintextToken = tokenValue
        };
    }

    private async Task<List<Guid>> RevokeActiveApplicantSessionsAsync(
        ProcurementSupplierOnboardingToken entity,
        string reason,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var sessions = await ApplicantSessions.GetQueryable(item =>
                item.TenantId == entity.TenantId &&
                !item.IsDeleted &&
                item.Status == ProcurementSupplierApplicantSessionStatus.Active &&
                item.ApplicantAccess.TenantId == entity.TenantId &&
                !item.ApplicantAccess.IsDeleted &&
                item.ApplicantAccess.TokenId == entity.Id)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Status = ProcurementSupplierApplicantSessionStatus.Revoked;
            session.RevokedAtUtc = now;
            session.RevocationReason = reason;
            Touch(session, now);
            Capture(session);
            await ApplicantSessions.UpdateAsync(session);
        }

        return sessions.Select(item => item.SessionReference).ToList();
    }

    public async Task ExpireForTerminalRegistrationAsync(
        Guid registrationId,
        string terminalStatus,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(terminalStatus, "Approved", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(terminalStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            throw Validation("SUPPLIER_ONBOARDING_TERMINAL_STATUS_INVALID",
                "A token expires only when its application is Approved or Rejected.");
        EnsureAuthenticatedTenant();
        if (actorUserId != _currentUser.UserId)
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "The terminal registration actor does not match the authenticated actor.");

        var correlation = NormalizeCorrelation(correlationId);
        var entity = await TokenQuery().Include(item => item.Registration)
            .Include(item => item.Payments).Include(item => item.Exemptions)
            .SingleOrDefaultAsync(item => item.RegistrationId == registrationId,
                cancellationToken);
        if (entity is null)
        {
            // Persist the caller's tracked registration/status-history changes even when
            // the legacy application predates application-bound tokens.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }
        if (entity.Status == ProcurementSupplierOnboardingTokenStatus.Expired)
        {
            if (entity.LastOperationCorrelationId == correlation ||
                (string.Equals(entity.Registration.Status, terminalStatus,
                     StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(entity.ExpiryReason, $"Application {terminalStatus}.",
                     StringComparison.OrdinalIgnoreCase)))
                return;
            throw Conflict("SUPPLIER_ONBOARDING_TOKEN_ALREADY_EXPIRED",
                "The application-bound token is already terminal.");
        }
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementSupplierOnboardingTokenStatus.Expired;
        entity.ExpiredAtUtc = now;
        entity.ExpiryReason = $"Application {terminalStatus}.";
        Touch(entity, "Expired", correlation, now);
        Capture(entity);
        await ExecuteAsync(async () =>
        {
            await Tokens.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Expired",
                ProcurementControlEventResult.Succeeded, before, Snapshot(entity),
                entity.ExpiryReason, [], correlation, now, cancellationToken);
        }, cancellationToken);
        await PublishNotificationAsync(
            "procurement.supplier-onboarding-token.expired", entity, cancellationToken);
    }

    private IQueryable<ProcurementSupplierOnboardingToken> TokenQuery() =>
        Tokens.GetQueryable(item =>
            item.TenantId == _currentUser.TenantId && !item.IsDeleted);

    private async Task<ProcurementSupplierOnboardingToken> LoadAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        IQueryable<ProcurementSupplierOnboardingToken> query = TokenQuery()
            .Include(item => item.Registration)
            .Include(item => item.Payments.Where(payment => !payment.IsDeleted))
            .Include(item => item.Exemptions.Where(exemption => !exemption.IsDeleted));
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFound("SUPPLIER_ONBOARDING_TOKEN_NOT_FOUND",
                "The supplier-onboarding token was not found for this tenant.");
    }

    private async Task<ResolvedDecision> ResolveDecisionAsync(
        DateTime atUtc,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var profiles = await ConfigurationProfiles.GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                item.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
                item.EffectiveFrom <= atUtc &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= atUtc))
            .Include(item => item.Decisions)
            .AsNoTracking()
            .OrderByDescending(item => item.IsDefault)
            .ThenByDescending(item => item.Version)
            .ToListAsync(cancellationToken);
        if (profiles.Count == 0)
            throw Validation("SUPPLIER_ONBOARDING_DEC007_NOT_EFFECTIVE",
                "No Published effective procurement configuration profile is available.");
        var defaults = profiles.Where(item => item.IsDefault).ToList();
        if (defaults.Count > 1 || (defaults.Count == 0 && profiles.Count > 1))
            throw Conflict("SUPPLIER_ONBOARDING_DEC007_AMBIGUOUS",
                "The effective procurement configuration profile is ambiguous.");
        var profile = defaults.SingleOrDefault() ?? profiles.Single();
        var decisions = profile.Decisions.Where(item =>
                !item.IsDeleted &&
                item.DecisionKey == "DEC-007" &&
                item.Status == ProcurementConfigurationDecisionStatus.Approved &&
                item.ApprovalStatus == ProcurementConfigurationApprovalStatus.Approved &&
                item.EvidenceStatus == ProcurementConfigurationEvidenceStatus.Verified &&
                (!item.EffectiveFrom.HasValue || item.EffectiveFrom.Value <= atUtc) &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= atUtc))
            .ToList();
        if (decisions.Count != 1)
            throw Validation("SUPPLIER_ONBOARDING_DEC007_NOT_EFFECTIVE",
                "The effective profile must contain exactly one approved applicable DEC-007 decision.");
        var decision = decisions[0];
        var value = Deserialize<ProcurementSupplierFeeDecisionValueDto>(decision.ValueJson);
        ValidateDecisionValue(value, atUtc);
        await ValidateDecisionReferencesAsync(value, tenantId, cancellationToken);
        var snapshotJson = Serialize(new
        {
            Profile = new
            {
                profile.Id,
                profile.ProfileKey,
                profile.ProfileCode,
                profile.Version,
                profile.EffectiveFrom,
                profile.EffectiveTo,
                profile.IsDefault
            },
            Decision = new
            {
                decision.Id,
                decision.DecisionKey,
                decision.SchemaVersion,
                decision.Status,
                decision.ApprovalStatus,
                decision.EvidenceStatus,
                decision.EffectiveFrom,
                decision.EffectiveTo,
                decision.ApprovalReference,
                decision.SourceLineage
            },
            Value = value
        });
        return new ResolvedDecision(profile, decision, value, snapshotJson);
    }

    private async Task ValidateDecisionReferencesAsync(
        ProcurementSupplierFeeDecisionValueDto value,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (value.Mode == ProcurementSupplierOnboardingFeeMode.Paid)
        {
            var accountIds = new[] { value.RevenueAccountId, value.TaxAccountId }
                .Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToList();
            var accounts = await Accounts.GetQueryable(item =>
                    item.TenantId == tenantId &&
                    accountIds.Contains(item.Id) && !item.IsDeleted)
                .AsNoTracking().ToListAsync(cancellationToken);
            if (accounts.Count != accountIds.Count)
                throw Validation("SUPPLIER_ONBOARDING_DEC007_GL_INVALID",
                    "One or more DEC-007 GL accounts do not belong to this tenant.");
            var revenue = accounts.Single(item => item.Id == value.RevenueAccountId);
            if (revenue.Status != AccountStatus.Active ||
                revenue.AccountType != AccountType.Revenue ||
                !revenue.AllowDirectPosting)
                throw Validation("SUPPLIER_ONBOARDING_DEC007_REVENUE_GL_INVALID",
                    "DEC-007 requires an active direct-posting revenue account.");
            if (value.TaxPercent > 0)
            {
                var tax = accounts.Single(item => item.Id == value.TaxAccountId);
                if (tax.Status != AccountStatus.Active ||
                    tax.AccountType != AccountType.Liability ||
                    !tax.AllowDirectPosting)
                    throw Validation("SUPPLIER_ONBOARDING_DEC007_TAX_GL_INVALID",
                        "DEC-007 requires an active direct-posting tax-liability account.");
            }
        }
        if (value.ExemptionWorkflowDefinitionId.HasValue)
        {
            var valid = await WorkflowDefinitions.GetQueryable(item =>
                    item.TenantId == tenantId &&
                    item.Id == value.ExemptionWorkflowDefinitionId.Value &&
                    !item.IsDeleted && item.IsActive &&
                    item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
                .AnyAsync(cancellationToken);
            if (!valid)
                throw Validation("SUPPLIER_ONBOARDING_DEC007_EXEMPTION_WORKFLOW_INVALID",
                    "The DEC-007 exemption workflow is not an active Published workflow.");
        }
    }

    private static void ValidateDecisionValue(
        ProcurementSupplierFeeDecisionValueDto value,
        DateTime atUtc)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), results, true))
            throw Validation("SUPPLIER_ONBOARDING_DEC007_INVALID",
                string.Join(" ", results.Select(item => item.ErrorMessage)));
        if (value.EffectiveFrom > atUtc ||
            (value.EffectiveTo.HasValue && value.EffectiveTo.Value < atUtc))
            throw Validation("SUPPLIER_ONBOARDING_DEC007_NOT_APPLICABLE",
                "The DEC-007 value is not effective at token issue time.");
    }

    private async Task ValidatePostingAccountsAsync(
        ProcurementSupplierOnboardingToken token,
        FinancePaymentMethod method,
        CancellationToken cancellationToken)
    {
        var ids = new[] { method.DefaultGLAccountId, token.RevenueAccountId, token.TaxAccountId }
            .Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToList();
        var accounts = await Accounts.GetQueryable(item =>
                item.TenantId == token.TenantId && ids.Contains(item.Id) && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (accounts.Count != ids.Count)
            throw Validation("SUPPLIER_ONBOARDING_POSTING_ACCOUNT_INVALID",
                "One or more payment posting accounts do not belong to this tenant.");
        var receipt = accounts.Single(item => item.Id == method.DefaultGLAccountId);
        if (receipt.Status != AccountStatus.Active ||
            receipt.AccountType != AccountType.Asset ||
            !receipt.AllowDirectPosting)
            throw Validation("SUPPLIER_ONBOARDING_PAYMENT_METHOD_GL_INVALID",
                "The payment method requires an active direct-posting asset account.");
    }

    private async Task ValidateFunctionalCurrencyAsync(
        ProcurementSupplierOnboardingToken token,
        CancellationToken cancellationToken)
    {
        var settings = await FinanceSettingsRows.GetQueryable(item =>
                item.TenantId == token.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var functional = settings?.BaseCurrency?.Trim().ToUpperInvariant() ?? "GHS";
        if (!string.Equals(functional, token.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw Validation("SUPPLIER_ONBOARDING_FOREIGN_CURRENCY_NOT_SUPPORTED",
                "Supplier-onboarding token payments currently require the tenant functional currency.");
    }

    private static FinancePostingRequestDto BuildPostingRequest(
        ProcurementSupplierOnboardingToken token,
        ProcurementSupplierOnboardingPayment payment,
        Guid receiptAccountId,
        string trustedReference,
        DateTime verifiedAtUtc)
    {
        var lines = new List<FinancePostingLineDto>
        {
            new()
            {
                AccountId = receiptAccountId,
                Description = $"Supplier onboarding receipt {token.TokenReference}",
                DebitAmount = payment.TotalAmount,
                CreditAmount = 0,
                TransactionCurrency = payment.CurrencyCode,
                SourceReferenceNumber = trustedReference,
                LineNumber = 1,
                TransactionTag = "SupplierOnboardingReceipt"
            },
            new()
            {
                AccountId = token.RevenueAccountId!.Value,
                Description = $"Supplier onboarding fee {token.TokenReference}",
                DebitAmount = 0,
                CreditAmount = payment.FeeAmount,
                TransactionCurrency = payment.CurrencyCode,
                SourceReferenceNumber = trustedReference,
                LineNumber = 2,
                TransactionTag = "SupplierOnboardingFee"
            }
        };
        if (payment.TaxAmount > 0)
        {
            lines.Add(new FinancePostingLineDto
            {
                AccountId = token.TaxAccountId!.Value,
                Description = $"Supplier onboarding fee tax {token.TokenReference}",
                DebitAmount = 0,
                CreditAmount = payment.TaxAmount,
                TransactionCurrency = payment.CurrencyCode,
                SourceReferenceNumber = trustedReference,
                LineNumber = 3,
                TransactionTag = "SupplierOnboardingTax"
            });
        }
        return new FinancePostingRequestDto
        {
            SourceModule = "Procurement",
            OriginModuleCode = "PROC",
            SourceDocumentType = "SupplierOnboardingTokenPayment",
            SourceDocumentId = payment.Id,
            SourceDocumentTenantId = token.TenantId,
            SourceDocumentReference = trustedReference,
            PostingAction = "Post",
            PostingDate = verifiedAtUtc.Date,
            Description = $"Supplier onboarding token payment {token.TokenReference}",
            JournalType = "System Generated",
            FunctionalCurrencyCode = payment.CurrencyCode,
            IdempotencyKey = $"PROCUREMENT|SUPPLIER-ONBOARDING|{payment.Id:N}|POST",
            ReturnExistingOnDuplicate = true,
            Lines = lines
        };
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "Supplier applicants cannot perform internal token-control actions.");
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                SourceType = SourceType,
                SourceReference = sourceReference
            }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(decision.Message);
    }

    private async Task EnsureApplicantOwnerOrCapabilityAsync(
        BusinessPartnerRegistration registration,
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
        {
            await EnsureRegistrationReaderAsync(registration, cancellationToken);
            return;
        }
        await EnsureCapabilityAsync(
            permission, sourceReference, correlationId, cancellationToken);
    }

    private async Task EnsureIndependentActorAsync(
        Guid? prohibitedActor,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!prohibitedActor.HasValue || prohibitedActor == Guid.Empty)
            throw Conflict("SUPPLIER_ONBOARDING_INITIATOR_NOT_RECORDED",
                "The initiating actor was not recorded.");
        var decision = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = SourceType,
            SourceReference = sourceReference,
            ProhibitedActorUserIds = [prohibitedActor.Value]
        }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(decision.Message);
    }

    private async Task EnsureReaderAsync(
        ProcurementSupplierOnboardingToken token,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
        {
            await EnsureRegistrationReaderAsync(token.Registration, cancellationToken);
            return;
        }
        EnsureInternalReader();
    }

    private async Task EnsureRegistrationReaderAsync(
        BusinessPartnerRegistration registration,
        CancellationToken cancellationToken)
    {
        if (registration.TenantId != _currentUser.TenantId)
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "The supplier registration belongs to another tenant.");

        if (!_currentUser.IsExternalUser)
            return;

        if (string.Equals(
                _currentUser.AuthenticationProvider,
                "ApplicantToken",
                StringComparison.OrdinalIgnoreCase))
        {
            if (await HasRestrictedApplicantAccessAsync(registration, cancellationToken))
                return;
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "Supplier applicants can access only their bound application token.");
        }

        if (registration.CreatedById != _currentUser.UserId)
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "Supplier applicants can access only their own application-bound token.");
    }

    private async Task EnsureRegistrationReadAccessAsync(
        BusinessPartnerRegistration registration,
        CancellationToken cancellationToken)
    {
        if (_currentUser.IsExternalUser)
        {
            await EnsureRegistrationReaderAsync(registration, cancellationToken);
            return;
        }
        EnsureInternalReader();
    }

    private async Task<bool> HasRestrictedApplicantAccessAsync(
        BusinessPartnerRegistration registration,
        CancellationToken cancellationToken)
    {
        if (_currentUser.Claims is null ||
            !_currentUser.Claims.TryGetValue(
                "supplier_applicant_registration",
                out var registrationClaim) ||
            !Guid.TryParse(registrationClaim, out var claimedRegistrationId) ||
            claimedRegistrationId != registration.Id ||
            !_currentUser.Claims.TryGetValue(
                "supplier_applicant_token",
                out var tokenClaim) ||
            !Guid.TryParse(tokenClaim, out var claimedTokenId) ||
            !_currentUser.Claims.TryGetValue(
                "supplier_applicant_session",
                out var sessionClaim) ||
            !Guid.TryParse(sessionClaim, out var claimedSessionReference))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        return await ApplicantAccesses.GetQueryable(item =>
                item.TenantId == registration.TenantId &&
                item.RegistrationId == registration.Id &&
                item.TokenId == claimedTokenId &&
                item.Status ==
                    ProcurementSupplierApplicantAccessStatus.ApplicationInProgress &&
                !item.TerminalAtUtc.HasValue &&
                !item.IsDeleted &&
                item.Sessions.Any(session =>
                    session.TenantId == registration.TenantId &&
                    session.SessionReference == claimedSessionReference &&
                    session.Status == ProcurementSupplierApplicantSessionStatus.Active &&
                    session.ExpiresAtUtc > now &&
                    !session.IsDeleted))
            .AnyAsync(cancellationToken);
    }

    private void EnsureInternalReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "Supplier applicants cannot access token administration.");
        if (IsAdministrator() ||
            _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role =>
                ProcurementAccessControlRegistry.FindRole(role) is not null))
            return;
        throw new ProcurementSupplierOnboardingTokenAuthorizationException(
            "A TDC procurement role or tenant-administration role is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw new ProcurementSupplierOnboardingTokenAuthorizationException(
                "An authenticated tenant context is required.");
    }

    private bool IsAdministrator() =>
        _currentUser.HasRole("Admin") || _currentUser.HasRole("Administrator") ||
        _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");

    private static void EnsureRegistrationActive(BusinessPartnerRegistration registration)
    {
        if (string.Equals(registration.Status, "Approved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(registration.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            throw Conflict("SUPPLIER_ONBOARDING_REGISTRATION_TERMINAL",
                "A token cannot be issued for an Approved or Rejected application.");
    }

    private async Task RecordEventAsync(
        ProcurementSupplierOnboardingToken entity,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlationId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken,
        bool systemEvent = false)
    {
        var request = new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "supplier-onboarding-token", entity.TenantId, entity.Id,
                $"{action}-{correlationId}"),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = entity.SourceConfigurationProfileCode,
            RuleId = entity.SourceConfigurationDecisionId,
            RuleVersion = entity.SourceConfigurationProfileVersion.ToString(),
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = SourceType,
            SourceId = entity.Id,
            SourceReference = entity.TokenReference,
            Reason = Trim(reason, 1000),
            Before = before,
            After = after,
            CorrelationId = correlationId,
            CausationId = correlationId,
            OccurredAtUtc = occurredAtUtc,
            Evidence = evidence.Select(item => new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = item.ReferenceKind,
                ReferenceId = item.ReferenceId,
                Reference = item.Reference,
                Label = item.Label,
                RequirementKey = item.RequirementKey
            }).ToList()
        };
        if (systemEvent)
        {
            await _controlEvents.RecordSystemAsync(
                entity.TenantId,
                "Verified Supplier Applicant",
                request,
                cancellationToken);
            return;
        }

        await _controlEvents.RecordAsync(request, cancellationToken);
    }

    private async Task PublishNotificationAsync(
        string topic,
        ProcurementSupplierOnboardingToken entity,
        CancellationToken cancellationToken,
        bool systemEvent = false)
    {
        try
        {
            await _notificationTopics.PublishAsync(new NotificationTopicEvent
            {
                TenantId = entity.TenantId,
                TopicKey = topic,
                NotificationType = "ProcurementSupplierOnboardingTokenControl",
                EntityType = SourceType,
                EntityId = entity.Id,
                TriggeredByUserId = systemEvent ? null : _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["tokenReference"] = entity.TokenReference,
                    ["registrationId"] = entity.RegistrationId,
                    ["status"] = entity.Status.ToString(),
                    ["paymentStatus"] = entity.PaymentStatus.ToString(),
                    ["feeMode"] = entity.FeeMode.ToString(),
                    ["totalAmount"] = entity.TotalAmount,
                    ["currencyCode"] = entity.CurrencyCode
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish supplier-onboarding token notification {Topic} for {TokenId}",
                topic, entity.Id);
        }
    }

    private async Task ExecuteAsync(
        Func<Task> action,
        CancellationToken cancellationToken,
        bool joinCallerTransaction = false)
    {
        if (joinCallerTransaction && _unitOfWork.HasActiveTransaction)
        {
            await action();
            return;
        }

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await action();
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private static ProcurementSupplierOnboardingTokenDto Map(
        ProcurementSupplierOnboardingToken entity)
    {
        var list = MapList(entity);
        return new ProcurementSupplierOnboardingTokenDto
        {
            Id = list.Id,
            RegistrationId = list.RegistrationId,
            RegistrationNumber = list.RegistrationNumber,
            ApplicantName = list.ApplicantName,
            TokenReference = list.TokenReference,
            MaskedToken = list.MaskedToken,
            Generation = list.Generation,
            Status = list.Status,
            PaymentStatus = list.PaymentStatus,
            FeeMode = list.FeeMode,
            FeeAmount = list.FeeAmount,
            TaxAmount = list.TaxAmount,
            TotalAmount = list.TotalAmount,
            CurrencyCode = list.CurrencyCode,
            IssuedAtUtc = list.IssuedAtUtc,
            ActivatedAtUtc = list.ActivatedAtUtc,
            ExpiredAtUtc = list.ExpiredAtUtc,
            SourceConfigurationProfileCode = list.SourceConfigurationProfileCode,
            SourceConfigurationProfileVersion = list.SourceConfigurationProfileVersion,
            RowVersion = list.RowVersion,
            FeeType = entity.FeeType,
            TaxPercent = entity.TaxPercent,
            PaymentChannels = Deserialize<List<string>>(entity.PaymentChannelsJson),
            ReceiptNumberFormat = entity.ReceiptNumberFormat,
            ExemptionRule = entity.ExemptionRule,
            RefundRule = entity.RefundRule,
            RenewalRule = entity.RenewalRule,
            SourceConfigurationProfileId = entity.SourceConfigurationProfileId,
            SourceConfigurationDecisionId = entity.SourceConfigurationDecisionId,
            RevenueAccountId = entity.RevenueAccountId,
            TaxAccountId = entity.TaxAccountId,
            ExemptionWorkflowDefinitionId = entity.ExemptionWorkflowDefinitionId,
            DecisionSnapshotHash = entity.DecisionSnapshotHash,
            IntegrityHash = entity.IntegrityHash,
            ExpiryReason = entity.ExpiryReason,
            ReissueReason = entity.ReissueReason,
            ReissuedAtUtc = entity.ReissuedAtUtc,
            Payments = entity.Payments.OrderByDescending(item => item.PaidAtUtc)
                .Select(MapPayment).ToList(),
            Exemptions = entity.Exemptions.OrderByDescending(item => item.RequestedAtUtc)
                .Select(MapExemption).ToList()
        };
    }

    private static ProcurementSupplierOnboardingTokenListItemDto MapList(
        ProcurementSupplierOnboardingToken entity) => new()
    {
        Id = entity.Id,
        RegistrationId = entity.RegistrationId,
        RegistrationNumber = entity.Registration?.RegistrationNumber ?? string.Empty,
        ApplicantName = entity.Registration?.ApplicantName ?? string.Empty,
        TokenReference = entity.TokenReference,
        MaskedToken = $"••••••••{entity.TokenLastFour}",
        Generation = entity.Generation,
        Status = entity.Status,
        PaymentStatus = entity.PaymentStatus,
        FeeMode = entity.FeeMode,
        FeeAmount = entity.FeeAmount,
        TaxAmount = entity.TaxAmount,
        TotalAmount = entity.TotalAmount,
        CurrencyCode = entity.CurrencyCode,
        IssuedAtUtc = entity.IssuedAtUtc,
        ActivatedAtUtc = entity.ActivatedAtUtc,
        ExpiredAtUtc = entity.ExpiredAtUtc,
        SourceConfigurationProfileCode = entity.SourceConfigurationProfileCode,
        SourceConfigurationProfileVersion = entity.SourceConfigurationProfileVersion,
        RowVersion = Convert.ToBase64String(entity.RowVersion)
    };

    private static ProcurementSupplierOnboardingPaymentDto MapPayment(
        ProcurementSupplierOnboardingPayment item) => new()
    {
        Id = item.Id,
        SubmittedByApplicantSessionId = item.SubmittedByApplicantSessionId,
        PaymentMethodId = item.PaymentMethodId,
        PaymentMethodCode = item.PaymentMethodCode,
        PaymentMethodName = item.PaymentMethodName,
        PaymentReference = item.PaymentReference,
        FeeAmount = item.FeeAmount,
        TaxAmount = item.TaxAmount,
        TotalAmount = item.TotalAmount,
        CurrencyCode = item.CurrencyCode,
        Status = item.Status,
        PaidAtUtc = item.PaidAtUtc,
        PostedAtUtc = item.PostedAtUtc,
        PostingEventId = item.PostingEventId,
        JournalEntryId = item.JournalEntryId,
        ReceiptNumber = item.ReceiptNumber,
        ReceiptIssuedAtUtc = item.ReceiptIssuedAtUtc,
        ReconciledAtUtc = item.ReconciledAtUtc,
        ReconciliationReference = item.ReconciliationReference,
        ReconciliationNotes = item.ReconciliationNotes,
        FailureReason = item.FailureReason,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementSupplierOnboardingExemptionDto MapExemption(
        ProcurementSupplierOnboardingExemption item) => new()
    {
        Id = item.Id,
        Reason = item.Reason,
        Status = item.Status,
        WorkflowDefinitionId = item.WorkflowDefinitionId,
        WorkflowInstanceId = item.WorkflowInstanceId,
        RequestedById = item.RequestedById,
        RequestedAtUtc = item.RequestedAtUtc,
        DecidedById = item.DecidedById,
        DecidedAtUtc = item.DecidedAtUtc,
        DecisionComment = item.DecisionComment,
        Evidence = Deserialize<List<ProcurementControlEventEvidenceReference>>(item.EvidenceJson),
        RowVersion = Convert.ToBase64String(item.RowVersion),
        IntegrityHash = item.IntegrityHash
    };

    private static object Snapshot(ProcurementSupplierOnboardingToken item) => new
    {
        item.Id,
        item.RegistrationId,
        item.TokenReference,
        item.TokenLastFour,
        item.Generation,
        item.Status,
        item.PaymentStatus,
        item.FeeMode,
        item.FeeType,
        item.FeeAmount,
        item.TaxPercent,
        item.TaxAmount,
        item.TotalAmount,
        item.CurrencyCode,
        item.SourceConfigurationProfileId,
        item.SourceConfigurationProfileCode,
        item.SourceConfigurationProfileVersion,
        item.SourceConfigurationDecisionId,
        item.DecisionSnapshotHash,
        item.IssuedAtUtc,
        item.ActivatedAtUtc,
        item.ExpiredAtUtc,
        item.ExpiryReason,
        item.ReissuedAtUtc,
        item.ReissueReason
    };

    private static object PaymentSnapshot(ProcurementSupplierOnboardingPayment item) => new
    {
        item.Id,
        item.TokenId,
        item.SubmittedByApplicantSessionId,
        item.PaymentMethodId,
        item.PaymentMethodCode,
        item.PaymentReference,
        item.FeeAmount,
        item.TaxAmount,
        item.TotalAmount,
        item.CurrencyCode,
        item.Status,
        item.PaidAtUtc,
        item.PostedAtUtc,
        item.PostingEventId,
        item.JournalEntryId,
        item.ReceiptNumber,
        item.ReconciledAtUtc,
        item.ReconciliationReference
    };

    private static object ExemptionSnapshot(ProcurementSupplierOnboardingExemption item) => new
    {
        item.Id,
        item.TokenId,
        item.Status,
        item.WorkflowDefinitionId,
        item.WorkflowInstanceId,
        item.RequestedById,
        item.RequestedAtUtc,
        item.DecidedById,
        item.DecidedAtUtc,
        item.Reason,
        item.DecisionComment,
        item.EvidenceHash
    };

    private static void Capture(ProcurementSupplierOnboardingToken entity) =>
        entity.IntegrityHash = Hash(Serialize(Snapshot(entity)));

    private static void Capture(ProcurementSupplierOnboardingPayment entity) =>
        entity.IntegrityHash = Hash(Serialize(PaymentSnapshot(entity)));

    private static void Capture(ProcurementSupplierOnboardingExemption entity) =>
        entity.IntegrityHash = Hash(Serialize(ExemptionSnapshot(entity)));

    private static void Capture(ProcurementSupplierApplicantSession entity) =>
        entity.IntegrityHash = Hash(Serialize(new
        {
            entity.Id,
            entity.TenantId,
            entity.ApplicantAccessId,
            entity.SessionReference,
            entity.Status,
            entity.IssuedAtUtc,
            entity.ExpiresAtUtc,
            entity.LastUsedAtUtc,
            entity.RevokedAtUtc,
            entity.RevocationReason
        }));

    private void Touch(
        ProcurementSupplierOnboardingToken entity,
        string operation,
        string correlation,
        DateTime now)
    {
        entity.LastOperation = operation;
        entity.LastOperationCorrelationId = correlation;
        entity.UpdatedAt = now;
        entity.UpdatedBy = ActorName;
        entity.LastModifiedById = _currentUser.UserId;
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private void Touch(
        ProcurementSupplierOnboardingPayment entity,
        string operation,
        string correlation,
        DateTime now)
    {
        entity.LastOperation = operation;
        entity.LastOperationCorrelationId = correlation;
        entity.UpdatedAt = now;
        entity.UpdatedBy = ActorName;
        entity.LastModifiedById = _currentUser.UserId;
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private void Touch(
        ProcurementSupplierOnboardingExemption entity,
        string operation,
        string correlation,
        DateTime now)
    {
        entity.LastOperation = operation;
        entity.LastOperationCorrelationId = correlation;
        entity.UpdatedAt = now;
        entity.UpdatedBy = ActorName;
        entity.LastModifiedById = _currentUser.UserId;
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private void Touch(ProcurementSupplierApplicantSession entity, DateTime now)
    {
        entity.UpdatedAt = now;
        entity.UpdatedBy = ActorName;
        entity.LastModifiedById = _currentUser.UserId;
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private static bool IsReplay(
        string operation,
        string operationCorrelationId,
        string expectedOperation,
        string correlation) =>
        string.Equals(operation, expectedOperation, StringComparison.Ordinal) &&
        string.Equals(operationCorrelationId, correlation, StringComparison.Ordinal);

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException)
        {
            throw Conflict("SUPPLIER_ONBOARDING_ROW_VERSION_INVALID",
                "The supplied row version is invalid.");
        }
        if (!CryptographicOperations.FixedTimeEquals(current, parsed))
            throw Conflict("SUPPLIER_ONBOARDING_CONCURRENCY_CONFLICT",
                "The supplier-onboarding record changed. Refresh and retry.");
    }

    private static void EnsureEvidence(
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence)
    {
        if (evidence.Count == 0 || evidence.Any(item =>
            !item.ReferenceId.HasValue && string.IsNullOrWhiteSpace(item.Reference)))
            throw Validation("SUPPLIER_ONBOARDING_EVIDENCE_REQUIRED",
                "At least one valid shared evidence reference is required.");
    }

    private static string GenerateTokenValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string ResolveResetPolicy(string format) =>
        format.Contains("{MM}", StringComparison.OrdinalIgnoreCase)
            ? DocumentSequenceResetPolicies.Monthly
            : format.Contains("{YYYY}", StringComparison.OrdinalIgnoreCase) ||
              format.Contains("{YY}", StringComparison.OrdinalIgnoreCase)
                ? DocumentSequenceResetPolicies.Yearly
                : DocumentSequenceResetPolicies.Never;

    private string ActorName =>
        string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.Username
            : _currentUser.FullName;

    private static string NormalizeCorrelation(string? correlation)
    {
        var value = string.IsNullOrWhiteSpace(correlation)
            ? Guid.NewGuid().ToString("N")
            : correlation.Trim();
        if (value.Length > 100)
            throw Validation("SUPPLIER_ONBOARDING_CORRELATION_INVALID",
                "Correlation id cannot exceed 100 characters.");
        return value;
    }

    private static string NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw Validation("SUPPLIER_ONBOARDING_JSON_INVALID",
            "A supplier-onboarding configuration snapshot is invalid.");

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static ProcurementSupplierOnboardingTokenNotFoundException NotFound(
        string code,
        string message) => new(code, message);

    private static ProcurementSupplierOnboardingTokenConflictException Conflict(
        string code,
        string message) => new(code, message);

    private static ProcurementSupplierOnboardingTokenValidationException Validation(
        string code,
        string message) => new(code, message);

    private sealed record ResolvedDecision(
        ProcurementConfigurationProfile Profile,
        ProcurementConfigurationDecision Decision,
        ProcurementSupplierFeeDecisionValueDto Value,
        string SnapshotJson);
}
