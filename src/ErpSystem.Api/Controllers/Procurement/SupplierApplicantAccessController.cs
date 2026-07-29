using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ErpSystem.Api.Services;
using ErpSystem.Api.Services.Otp;
using ErpSystem.Api.Services.Sms;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/supplier-applicant-access")]
public sealed class SupplierApplicantAccessController : ControllerBase
{
    private readonly IProcurementSupplierApplicantAccessService _applicantAccess;
    private readonly IProcurementSupplierOnboardingTokenService _tokens;
    private readonly IBusinessPartnerRegistrationService _registrations;
    private readonly ITenantService _tenants;
    private readonly IOtpService _otp;
    private readonly ITenantSmsSender _sms;
    private readonly INotificationService _notifications;
    private readonly ICaptchaVerificationService _captcha;
    private readonly IProcurementSupplierApplicantJwtService _jwt;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SupplierApplicantAccessController> _logger;

    public SupplierApplicantAccessController(
        IProcurementSupplierApplicantAccessService applicantAccess,
        IProcurementSupplierOnboardingTokenService tokens,
        IBusinessPartnerRegistrationService registrations,
        ITenantService tenants,
        IOtpService otp,
        ITenantSmsSender sms,
        INotificationService notifications,
        ICaptchaVerificationService captcha,
        IProcurementSupplierApplicantJwtService jwt,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        IUnitOfWork unitOfWork,
        ILogger<SupplierApplicantAccessController> logger)
    {
        _applicantAccess = applicantAccess;
        _tokens = tokens;
        _registrations = registrations;
        _tenants = tenants;
        _otp = otp;
        _sms = sms;
        _notifications = notifications;
        _captcha = captcha;
        _jwt = jwt;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpPost("verification-challenges")]
    [AllowAnonymous]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<IActionResult> RequestVerificationChallenge(
        [FromBody] SupplierApplicantVerificationChallengeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await ResolveTenantAsync(request.TenantCode);
            await EnsureCaptchaAsync(tenant.Id, request.RecaptchaToken, cancellationToken);
            var channel = ParseChannel(request.Channel);
            var contact = NormalizeContact(channel, request.Contact);
            ValidateContact(channel, contact);
            var code = await _otp.CreateOtpAsync(
                tenant.Id,
                OtpPurpose.SupplierApplicantVerification,
                channel == ProcurementSupplierApplicantVerificationChannel.Email
                    ? OtpChannel.Email
                    : OtpChannel.Sms,
                contact,
                TimeSpan.FromMinutes(10),
                maxAttempts: 5,
                cancellationToken);
            if (channel == ProcurementSupplierApplicantVerificationChannel.Email)
            {
                await _notifications.SendEmailAsync(
                    contact,
                    "Supplier application verification code",
                    $"<p>Your verification code is <strong>{code}</strong>. " +
                    "It expires in 10 minutes.</p>",
                    isHtml: true);
            }
            else
            {
                await _sms.SendAsync(
                    tenant.Id,
                    contact,
                    $"Your supplier application verification code is {code}. It expires in 10 minutes.",
                    cancellationToken);
            }
            return Accepted(new
            {
                success = true,
                message = "A verification code was sent through the selected channel.",
                channel = channel.ToString(),
                maskedContact = MaskContact(channel, contact)
            });
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpPost("verified-applications")]
    [AllowAnonymous]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<IActionResult> VerifyAndIssue(
        [FromBody] SupplierApplicantVerifyAndIssueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await ResolveTenantAsync(request.TenantCode);
            await EnsureCaptchaAsync(tenant.Id, request.RecaptchaToken, cancellationToken);
            var channel = ParseChannel(request.Channel);
            var contact = NormalizeContact(channel, request.Contact);
            var verification = await _otp.VerifyOtpAsync(
                tenant.Id,
                OtpPurpose.SupplierApplicantVerification,
                channel == ProcurementSupplierApplicantVerificationChannel.Email
                    ? OtpChannel.Email
                    : OtpChannel.Sms,
                contact,
                request.OtpCode,
                consumeOnSuccess: true,
                cancellationToken);
            if (!verification.Success)
                return Unauthorized(new
                {
                    code = "SUPPLIER_APPLICANT_VERIFICATION_FAILED",
                    message = verification.FailureReason ?? "The verification code is invalid."
                });

            var issued = await _applicantAccess.CreateVerifiedApplicationAsync(
                new VerifyAndIssueSupplierApplicantTokenRequest
                {
                    TenantId = tenant.Id,
                    Channel = channel,
                    Contact = contact,
                    CompanyName = request.CompanyName,
                    RegistrationCategory = request.RegistrationCategory,
                    RetainedRegistrationId = request.RetainedRegistrationId
                },
                Correlation("verify-issue"),
                cancellationToken);
            var deliveryStatus = "Sent";
            try
            {
                var message =
                    $"Supplier application {issued.RegistrationNumber}. " +
                    $"Application token: {issued.PlaintextToken}. " +
                    "Keep this token secure; it remains valid until the application is approved or rejected.";
                if (channel == ProcurementSupplierApplicantVerificationChannel.Email)
                {
                    await _notifications.SendEmailAsync(
                        contact,
                        "Your supplier application token",
                        $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>",
                        isHtml: true);
                }
                else
                {
                    await _sms.SendAsync(tenant.Id, contact, message, cancellationToken);
                }
            }
            catch
            {
                deliveryStatus = "Failed";
                _logger.LogWarning(
                    "Supplier application-token delivery failed for registration {RegistrationId}",
                    issued.RegistrationId);
            }

            return Created(
                $"/api/procurement/supplier-applicant-access/verified-applications/{issued.RegistrationId}",
                new
                {
                    issued.RegistrationId,
                    issued.RegistrationNumber,
                    issued.TokenId,
                    issued.TokenReference,
                    applicationToken = issued.PlaintextToken,
                    issued.FeeMode,
                    issued.TokenStatus,
                    issued.PaymentStatus,
                    issued.TotalAmount,
                    issued.CurrencyCode,
                    deliveryStatus,
                    message =
                        "The token is shown once and can be used repeatedly until the application is approved or rejected."
                });
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpPost("sessions")]
    [AllowAnonymous]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<IActionResult> StartSession(
        [FromBody] SupplierApplicantLoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await ResolveTenantAsync(request.TenantCode);
            await EnsureCaptchaAsync(tenant.Id, request.RecaptchaToken, cancellationToken);
            var session = await _applicantAccess.StartSessionAsync(
                new StartSupplierApplicantSessionRequest
                {
                    TenantId = tenant.Id,
                    ApplicationToken = request.ApplicationToken
                },
                Correlation("session"),
                cancellationToken);
            return Ok(new
            {
                sessionToken = _jwt.Issue(session),
                session.ExpiresAtUtc,
                session.PaymentOnly,
                session.RegistrationId
            });
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpGet("portal")]
    [Authorize(Policy = "SupplierApplicantOnly")]
    public async Task<IActionResult> GetPortal(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _applicantAccess.GetPortalAsync(
                SessionReference(), Correlation("status"), cancellationToken));
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpPut("portal/application")]
    [Authorize(Policy = "SupplierApplicantOnly")]
    public async Task<IActionResult> UpdateApplication(
        [FromBody] UpdateSupplierApplicantApplicationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _applicantAccess.UpdateApplicationAsync(
                SessionReference(), request, Correlation("update"), cancellationToken));
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpPost("portal/submit")]
    [Authorize(Policy = "SupplierApplicantOnly")]
    public async Task<IActionResult> Submit(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _applicantAccess.SubmitAsync(
                SessionReference(), Correlation("submit"), cancellationToken));
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpGet("portal/payment-methods")]
    [Authorize(Policy = "SupplierApplicantOnly")]
    public async Task<IActionResult> GetPaymentMethods(CancellationToken cancellationToken)
    {
        try
        {
            var session = await _applicantAccess.ValidateSessionAsync(
                SessionReference(), Correlation("payment-methods"), cancellationToken);
            return Ok(await _tokens.GetPaymentMethodsAsync(
                session.TokenId, cancellationToken));
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpPost("portal/payments")]
    [Authorize(Policy = "SupplierApplicantOnly")]
    public async Task<IActionResult> RecordPayment(
        [FromBody] RecordProcurementSupplierOnboardingPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _applicantAccess.ValidateSessionAsync(
                SessionReference(), Correlation("payment-session"), cancellationToken);
            var result = await _tokens.RecordPaymentAsync(
                session.TokenId, request, Correlation("payment"), cancellationToken);
            return Ok(result.Token);
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpPost("portal/documents")]
    [Authorize(Policy = "SupplierApplicantOnly")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadDocument(
        IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? evidenceRequirementCode,
        [FromForm] string? classificationCode,
        [FromForm] DateTime? issueDate,
        [FromForm] DateTime? expiryDate,
        CancellationToken cancellationToken)
    {
        try
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { code = "SUPPLIER_APPLICANT_FILE_REQUIRED" });
            var session = await _applicantAccess.ValidateSessionAsync(
                SessionReference(), Correlation("document-session"), cancellationToken);
            if (session.PaymentOnly)
                return Conflict(new
                {
                    code = "SUPPLIER_APPLICANT_PAYMENT_REQUIRED",
                    message = "Payment or an approved exemption is required before uploading documents."
                });
            var safeName = Path.GetFileName(file.FileName);
            var upload = await _controlledFiles.UploadAsync(
                new ControlledFileUploadRequest
                {
                    TenantId = session.TenantId,
                    ActorUserId = session.SystemActorUserId,
                    ActorName = "Supplier Applicant",
                    Category = ControlledFileUploadCategories.DocumentManagement,
                    FileName = safeName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    OpenReadStream = file.OpenReadStream
                },
                cancellationToken);
            CentralDocumentRepositoryLink centralDocument;
            try
            {
                centralDocument = await _centralDocuments.RegisterAsync(
                    new CentralDocumentRepositoryRegistration
                    {
                        TenantId = session.TenantId,
                        ActorUserId = session.SystemActorUserId,
                        ActorName = "Supplier Applicant",
                        FileUploadRecordId = upload.Record.Id,
                        SourceModule = "Procurement",
                        SourceLabel = "Supplier registration evidence",
                        SourceEntityType = "BusinessPartnerRegistration",
                        SourceRecordId = session.RegistrationId,
                        SourceRecordReference = session.RegistrationId.ToString(),
                        Title = safeName,
                        DocumentType = documentType.Trim(),
                        MetadataTemplateCode = "PROC-SUP-EVD",
                        AccessProfile = "Procurement restricted",
                        ChangeSummary = "Supplier registration evidence uploaded through applicant access."
                    },
                    cancellationToken);
            }
            catch
            {
                await _controlledFiles.DeleteAsync(
                    session.TenantId,
                    upload.Record.Id,
                    session.SystemActorUserId,
                    cancellationToken);
                throw;
            }

            BusinessPartnerRegistrationDocumentDto document;
            try
            {
                document = await _registrations.UploadDocumentAsync(
                    session.RegistrationId,
                    new CreateBusinessPartnerDocumentDto
                    {
                        FileUploadRecordId = upload.Record.Id,
                        CentralDocumentRecordId = centralDocument.DocumentRecordId,
                        CentralDocumentVersionId = centralDocument.DocumentVersionId,
                        DocumentType = documentType.Trim(),
                        DocumentName = safeName,
                        DocumentPath = null,
                        FilePath = string.Empty,
                        FileSize = upload.Record.FileSize,
                        MimeType = upload.Record.ContentType,
                        EvidenceRequirementCode = evidenceRequirementCode,
                        ClassificationCode = classificationCode,
                        IssueDate = issueDate,
                        ExpiryDate = expiryDate,
                        ChecksumSha256 = upload.ChecksumSha256
                    },
                    session.SystemActorUserId);
            }
            catch
            {
                await _centralDocuments.DeleteAsync(
                    session.TenantId,
                    centralDocument.DocumentRecordId,
                    session.SystemActorUserId,
                    cancellationToken);
                throw;
            }
            return Created(
                $"/api/procurement/supplier-applicant-access/portal/documents/{document.Id}",
                document);
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpGet("portal/documents/{documentId:guid}/download")]
    [Authorize(Policy = "SupplierApplicantOnly")]
    public async Task<IActionResult> DownloadDocument(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _applicantAccess.ValidateSessionAsync(
                SessionReference(),
                Correlation("document-download-session"),
                cancellationToken);
            if (session.PaymentOnly)
            {
                return Conflict(new
                {
                    code = "SUPPLIER_APPLICANT_PAYMENT_REQUIRED",
                    message = "Payment or an approved exemption is required before downloading documents."
                });
            }

            var document = await _registrations.GetDocumentByIdAsync(
                session.RegistrationId, documentId);
            if (document is null)
            {
                return NotFound(new
                {
                    code = "SUPPLIER_APPLICANT_DOCUMENT_NOT_FOUND",
                    message = "The registration document was not found."
                });
            }

            ApplySensitiveDownloadHeaders();
            if (document.CentralDocumentRecordId.HasValue &&
                document.CentralDocumentVersionId.HasValue)
            {
                var centralContent = await _centralDocuments.OpenAsync(
                    session.TenantId,
                    document.CentralDocumentRecordId.Value,
                    document.CentralDocumentVersionId.Value,
                    cancellationToken);
                if (centralContent is null)
                {
                    return NotFound(new
                    {
                        code = "SUPPLIER_APPLICANT_DOCUMENT_CONTENT_NOT_FOUND",
                        message = "The document content was not found in the central DMS."
                    });
                }

                return File(
                    centralContent.Content,
                    centralContent.ContentType,
                    centralContent.FileName);
            }

            if (string.IsNullOrWhiteSpace(document.InternalStoragePath))
            {
                return NotFound(new
                {
                    code = "SUPPLIER_APPLICANT_DOCUMENT_CONTENT_NOT_FOUND",
                    message = "The document content was not found."
                });
            }

            var legacyContent = await _fileStorage.DownloadFileAsync(
                document.InternalStoragePath,
                document.FileUploadRecordId ?? document.Id);
            return File(
                legacyContent,
                document.MimeType ?? "application/octet-stream",
                document.DocumentName);
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpDelete("portal/documents/{documentId:guid}")]
    [Authorize(Policy = "SupplierApplicantOnly")]
    public async Task<IActionResult> DeleteDocument(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _applicantAccess.ValidateSessionAsync(
                SessionReference(), Correlation("document-delete-session"), cancellationToken);
            if (session.PaymentOnly)
                return Conflict(new
                {
                    code = "SUPPLIER_APPLICANT_PAYMENT_REQUIRED",
                    message = "Payment or an approved exemption is required before deleting documents."
                });
            var document = await _registrations.GetDocumentByIdAsync(
                session.RegistrationId, documentId);
            if (document is null)
                return NotFound(new
                {
                    code = "SUPPLIER_APPLICANT_DOCUMENT_NOT_FOUND",
                    message = "The registration document was not found."
                });
            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    await _registrations.DeleteDocumentAsync(
                        session.RegistrationId,
                        documentId,
                        session.SystemActorUserId);
                    if (document.CentralDocumentRecordId.HasValue)
                    {
                        await _centralDocuments.DeleteAsync(
                            session.TenantId,
                            document.CentralDocumentRecordId.Value,
                            session.SystemActorUserId,
                            cancellationToken);
                    }
                    else if (document.FileUploadRecordId.HasValue)
                    {
                        await _controlledFiles.DeleteAsync(
                            session.TenantId,
                            document.FileUploadRecordId.Value,
                            session.SystemActorUserId,
                            cancellationToken);
                    }
                    await _unitOfWork.CommitAsync(cancellationToken);
                }
                catch
                {
                    try
                    {
                        await _unitOfWork.RollbackAsync(cancellationToken);
                    }
                    catch (InvalidOperationException)
                    {
                        // CommitAsync already rolled back and released the
                        // transaction after a persistence failure.
                    }
                    throw;
                }
            }, cancellationToken);
            return NoContent();
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    private void ApplySensitiveDownloadHeaders()
    {
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    [HttpGet("admin/summary")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _applicantAccess.GetSummaryAsync(cancellationToken));
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpGet("admin/history")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _applicantAccess.GetHistoryAsync(cancellationToken));
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpPost("admin/registrations/{registrationId:guid}/credential/resend")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> ResendCredential(
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _applicantAccess.ResendTemporaryCredentialAsync(
                registrationId, Correlation("credential-resend"), cancellationToken);
            return NoContent();
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    [HttpPost("admin/registrations/{registrationId:guid}/activation/retry")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> RetryActivation(
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var actorUserId = AuthenticatedUserId();
            await _applicantAccess.RetryApprovedSupplierActivationAsync(
                registrationId,
                actorUserId,
                Correlation("activation-retry"),
                cancellationToken);
            return NoContent();
        }
        catch (Exception exception)
        {
            return Problem(exception);
        }
    }

    private Guid AuthenticatedUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var actorUserId) || actorUserId == Guid.Empty)
            throw new ProcurementSupplierApplicantAccessException(
                "SUPPLIER_APPLICANT_ACTOR_REQUIRED",
                "A valid authenticated actor is required.",
                StatusCodes.Status403Forbidden);
        return actorUserId;
    }

    private Guid SessionReference()
    {
        var value = User.FindFirstValue("supplier_applicant_session");
        if (!Guid.TryParse(value, out var sessionReference))
            throw new ProcurementSupplierApplicantAccessException(
                "SUPPLIER_APPLICANT_SESSION_INVALID",
                "The applicant session claim is invalid.",
                401);
        return sessionReference;
    }

    private async Task<Tenant> ResolveTenantAsync(string? tenantCode)
    {
        Tenant? tenant = null;
        if (!string.IsNullOrWhiteSpace(tenantCode))
            tenant = await _tenants.GetTenantByCodeAsync(tenantCode.Trim());
        if (tenant is null)
        {
            var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            var host = (!string.IsNullOrWhiteSpace(forwardedHost)
                ? forwardedHost.Split(',')[0].Trim()
                : Request.Host.Host).Split(':')[0];
            if (!string.IsNullOrWhiteSpace(host))
                tenant = await _tenants.GetTenantByDomainAsync(host);
        }
        tenant ??= await _tenants.GetTenantByIdAsync(Constants.Tenants.DefaultTenantId);
        if (tenant is null || tenant.Status != TenantStatus.Active ||
            !tenant.AllowSelfRegistration)
            throw new ProcurementSupplierApplicantAccessException(
                "SUPPLIER_APPLICANT_TENANT_UNAVAILABLE",
                "Supplier applications are not available for this tenant.",
                404);
        return tenant;
    }

    private async Task EnsureCaptchaAsync(
        Guid tenantId,
        string? recaptchaToken,
        CancellationToken cancellationToken)
    {
        var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        var host = !string.IsNullOrWhiteSpace(forwardedHost)
            ? forwardedHost.Split(',')[0].Trim()
            : Request.Host.Host;
        await _captcha.EnsureCaptchaValidAsync(
            tenantId,
            recaptchaToken,
            host,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
    }

    private string Correlation(string action)
    {
        var supplied = Request.Headers["X-Correlation-ID"].FirstOrDefault()?.Trim();
        return !string.IsNullOrWhiteSpace(supplied) && supplied.Length is >= 8 and <= 100
            ? supplied
            : $"supplier-applicant-{action}-{Guid.NewGuid():N}";
    }

    private IActionResult Problem(Exception exception)
    {
        _logger.LogWarning(
            "Supplier applicant access request failed with {ExceptionType}",
            exception.GetType().Name);
        var (status, code) = exception switch
        {
            ProcurementSupplierApplicantAccessException applicant =>
                (applicant.StatusCode, applicant.Code),
            ProcurementSupplierOnboardingTokenAuthorizationException =>
                (StatusCodes.Status401Unauthorized, "SUPPLIER_APPLICANT_TOKEN_DENIED"),
            ProcurementSupplierOnboardingTokenValidationException token =>
                (StatusCodes.Status422UnprocessableEntity, token.Code),
            ProcurementSupplierOnboardingTokenConflictException token =>
                (StatusCodes.Status409Conflict, token.Code),
            ProcurementSupplierOnboardingTokenNotFoundException token =>
                (StatusCodes.Status404NotFound, token.Code),
            CaptchaVerificationException =>
                (StatusCodes.Status400BadRequest, "CAPTCHA_INVALID"),
            ControlledFileUploadException upload =>
                (upload.StatusCode, upload.Code),
            UnauthorizedAccessException =>
                (StatusCodes.Status403Forbidden, "SUPPLIER_APPLICANT_FORBIDDEN"),
            _ => (StatusCodes.Status500InternalServerError,
                "SUPPLIER_APPLICANT_UNEXPECTED")
        };
        return StatusCode(status, new ProblemDetails
        {
            Type = $"https://tdc.gov.gh/problems/{code.ToLowerInvariant()}",
            Title = "Supplier applicant access request failed",
            Status = status,
            Detail = status == StatusCodes.Status500InternalServerError
                ? "The supplier applicant request could not be completed."
                : exception.Message,
            Extensions =
            {
                ["code"] = code,
                ["correlationId"] = Correlation("failure")
            }
        });
    }

    private static ProcurementSupplierApplicantVerificationChannel ParseChannel(
        string channel) =>
        channel.Trim().ToLowerInvariant() switch
        {
            "email" => ProcurementSupplierApplicantVerificationChannel.Email,
            "sms" or "phone" or "text" =>
                ProcurementSupplierApplicantVerificationChannel.Sms,
            _ => throw new ProcurementSupplierApplicantAccessException(
                "SUPPLIER_APPLICANT_CHANNEL_INVALID",
                "Use Email or Sms as the verification channel.",
                400)
        };

    private static string NormalizeContact(
        ProcurementSupplierApplicantVerificationChannel channel,
        string contact)
    {
        var value = contact.Trim();
        if (channel == ProcurementSupplierApplicantVerificationChannel.Email)
            return value.ToLowerInvariant();
        var prefix = value.StartsWith('+') ? "+" : string.Empty;
        return prefix + string.Concat(value.Where(char.IsDigit));
    }

    private static void ValidateContact(
        ProcurementSupplierApplicantVerificationChannel channel,
        string contact)
    {
        if (channel == ProcurementSupplierApplicantVerificationChannel.Email &&
            !new EmailAddressAttribute().IsValid(contact))
            throw new ProcurementSupplierApplicantAccessException(
                "SUPPLIER_APPLICANT_EMAIL_INVALID",
                "Enter a valid email address.",
                400);
        if (channel == ProcurementSupplierApplicantVerificationChannel.Sms &&
            contact.Count(char.IsDigit) < 8)
            throw new ProcurementSupplierApplicantAccessException(
                "SUPPLIER_APPLICANT_PHONE_INVALID",
                "Enter a valid phone number.",
                400);
    }

    private static string MaskContact(
        ProcurementSupplierApplicantVerificationChannel channel,
        string contact)
    {
        if (channel == ProcurementSupplierApplicantVerificationChannel.Email)
        {
            var parts = contact.Split('@', 2);
            return $"{parts[0][..Math.Min(2, parts[0].Length)]}***@{parts[1]}";
        }
        return contact.Length <= 4 ? "****" : $"***{contact[^4..]}";
    }
}

public class SupplierApplicantVerificationChallengeRequest
{
    [StringLength(50)]
    public string? TenantCode { get; set; }

    [Required, StringLength(10)]
    public string Channel { get; set; } = "Email";

    [Required, StringLength(200)]
    public string Contact { get; set; } = string.Empty;

    public string? RecaptchaToken { get; set; }
}

public sealed class SupplierApplicantVerifyAndIssueRequest :
    SupplierApplicantVerificationChallengeRequest
{
    [Required, StringLength(6, MinimumLength = 6)]
    public string OtpCode { get; set; } = string.Empty;

    [StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    public ProcurementSupplierRegistrationCategory RegistrationCategory { get; set; } =
        ProcurementSupplierRegistrationCategory.Goods;

    public Guid? RetainedRegistrationId { get; set; }
}

public sealed class SupplierApplicantLoginRequest
{
    [StringLength(50)]
    public string? TenantCode { get; set; }

    [Required, StringLength(200)]
    public string ApplicationToken { get; set; } = string.Empty;

    public string? RecaptchaToken { get; set; }
}
