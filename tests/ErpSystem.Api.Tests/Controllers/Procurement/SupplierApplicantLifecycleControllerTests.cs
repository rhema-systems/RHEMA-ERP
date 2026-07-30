using System.Security.Claims;
using System.Text;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Controllers.Procurement;
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
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class SupplierApplicantLifecycleControllerTests
{
    [Fact]
    public async Task RetainedDraftIdentifierIsForwardedAfterContactVerification()
    {
        var tenantId = Guid.NewGuid();
        var retainedRegistrationId = Guid.NewGuid();
        VerifyAndIssueSupplierApplicantTokenRequest? captured = null;
        var access = new Mock<IProcurementSupplierApplicantAccessService>();
        access.Setup(item => item.CreateVerifiedApplicationAsync(
                It.IsAny<VerifyAndIssueSupplierApplicantTokenRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<VerifyAndIssueSupplierApplicantTokenRequest, string, CancellationToken>(
                (request, _, _) => captured = request)
            .ReturnsAsync(new SupplierApplicantTokenIssueDto
            {
                RegistrationId = retainedRegistrationId,
                RegistrationNumber = "LEGACY-APP-004",
                TokenId = Guid.NewGuid(),
                TokenReference = "TOK-LEGACY-004",
                PlaintextToken = "one-time-application-token",
                FeeMode = ProcurementSupplierOnboardingFeeMode.Free,
                TokenStatus = ProcurementSupplierOnboardingTokenStatus.Active,
                PaymentStatus =
                    ProcurementSupplierOnboardingPaymentStatus.NotRequired,
                CurrencyCode = "GHS"
            });
        var tenants = new Mock<ITenantService>();
        tenants.Setup(item => item.GetTenantByCodeAsync("TDC"))
            .ReturnsAsync(new Tenant
            {
                Id = tenantId,
                Code = "TDC",
                Name = "TDC",
                Status = TenantStatus.Active,
                AllowSelfRegistration = true
            });
        var otp = new Mock<IOtpService>();
        otp.Setup(item => item.VerifyOtpAsync(
                tenantId,
                OtpPurpose.SupplierApplicantVerification,
                OtpChannel.Email,
                "retained@example.test",
                "123456",
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OtpVerifyResult(true, null));
        var notifications = new Mock<INotificationService>();
        notifications.Setup(item => item.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        var captcha = new Mock<ICaptchaVerificationService>();
        captcha.Setup(item => item.EnsureCaptchaValidAsync(
                tenantId,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = new SupplierApplicantAccessController(
            access.Object,
            Mock.Of<IProcurementSupplierOnboardingTokenService>(),
            Mock.Of<IBusinessPartnerRegistrationService>(),
            tenants.Object,
            otp.Object,
            Mock.Of<ITenantSmsSender>(),
            notifications.Object,
            captcha.Object,
            Mock.Of<IProcurementSupplierApplicantJwtService>(),
            Mock.Of<IControlledFileUploadService>(),
            Mock.Of<ICentralDocumentRepositoryFileService>(),
            Mock.Of<IFileStorageService>(),
            TransactionalUnitOfWork().Object,
            NullLogger<SupplierApplicantAccessController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.VerifyAndIssue(
            new SupplierApplicantVerifyAndIssueRequest
            {
                TenantCode = "TDC",
                Channel = "Email",
                Contact = "RETAINED@EXAMPLE.TEST",
                OtpCode = "123456",
                RetainedRegistrationId = retainedRegistrationId
            },
            CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(tenantId);
        captured.Contact.Should().Be("retained@example.test");
        captured.CompanyName.Should().BeEmpty();
        captured.RetainedRegistrationId.Should().Be(retainedRegistrationId);
    }

    [Fact]
    public async Task RestrictedApplicantExecutesPaymentAndControlledDocumentLifecycle()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var sessionReference = Guid.NewGuid();
        var fileRecordId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var centralDocumentRecordId = Guid.NewGuid();
        var centralDocumentVersionId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var session = new SupplierApplicantSessionDto
        {
            SessionReference = sessionReference,
            SystemActorUserId = actorId,
            TenantId = tenantId,
            RegistrationId = registrationId,
            TokenId = tokenId,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            PaymentOnly = false
        };
        var access = new Mock<IProcurementSupplierApplicantAccessService>();
        access.Setup(item => item.ValidateSessionAsync(
                sessionReference,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var paymentMethods =
            new List<ProcurementSupplierOnboardingPaymentMethodOptionDto>
            {
                new()
                {
                    Id = paymentMethodId,
                    Code = "MOMO",
                    Name = "Mobile Money",
                    RequiresReference = true,
                    IsPostingReady = true
                }
            };
        var pendingToken = new ProcurementSupplierOnboardingTokenDto
        {
            Id = tokenId,
            RegistrationId = registrationId,
            Status = ProcurementSupplierOnboardingTokenStatus.AwaitingPayment,
            PaymentStatus = ProcurementSupplierOnboardingPaymentStatus.Pending,
            RowVersion = "pending-verification-row-version"
        };
        var tokens = new Mock<IProcurementSupplierOnboardingTokenService>();
        tokens.Setup(item => item.GetPaymentMethodsAsync(
                tokenId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentMethods);
        tokens.Setup(item => item.RecordPaymentAsync(
                tokenId,
                It.IsAny<RecordProcurementSupplierOnboardingPaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSupplierOnboardingTokenIssueResultDto
            {
                Token = pendingToken
            });

        ControlledFileUploadRequest? capturedUpload = null;
        var controlledFiles = new Mock<IControlledFileUploadService>();
        controlledFiles.Setup(item => item.UploadAsync(
                It.IsAny<ControlledFileUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<ControlledFileUploadRequest, CancellationToken>(
                (request, _) => capturedUpload = request)
            .ReturnsAsync(new ControlledFileUploadResult
            {
                Record = new FileUploadRecord
                {
                    Id = fileRecordId,
                    TenantId = tenantId,
                    Category = "supplier-registration-evidence",
                    FilePath = "supplier-registration-evidence/evidence.pdf",
                    StoredFileName = "evidence.pdf",
                    OriginalFileName = "evidence.pdf",
                    ContentType = "application/pdf",
                    FileSize = 14,
                    StorageProvider = "test",
                    UploadedByUserId = actorId,
                    VirusScanStatus = FileVirusScanStatus.Clean
                },
                ChecksumSha256 = new string('a', 64),
                PublicUrl = "/files/evidence.pdf"
            });
        var centralDocuments = new Mock<ICentralDocumentRepositoryFileService>();
        centralDocuments.Setup(item => item.RegisterAsync(
                It.IsAny<CentralDocumentRepositoryRegistration>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CentralDocumentRepositoryLink
            {
                DocumentRecordId = centralDocumentRecordId,
                DocumentVersionId = centralDocumentVersionId,
                FileUploadRecordId = fileRecordId,
                DocumentReference = "DMS-PROCUREMENT-TEST",
                VersionNumber = "v1.0"
            });
        centralDocuments.Setup(item => item.DeleteAsync(
                tenantId,
                centralDocumentRecordId,
                actorId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        CreateBusinessPartnerDocumentDto? capturedDocument = null;
        var registrations = new Mock<IBusinessPartnerRegistrationService>();
        var document = new BusinessPartnerRegistrationDocumentDto
        {
            Id = documentId,
            RegistrationId = registrationId,
            FileUploadRecordId = fileRecordId,
            CentralDocumentRecordId = centralDocumentRecordId,
            CentralDocumentVersionId = centralDocumentVersionId,
            DocumentType = "TaxClearance",
            DocumentName = "evidence.pdf",
            FilePath = "supplier-registration-evidence/evidence.pdf",
            FileSize = 14,
            MimeType = "application/pdf",
            ChecksumSha256 = new string('a', 64)
        };
        registrations.Setup(item => item.UploadDocumentAsync(
                registrationId,
                It.IsAny<CreateBusinessPartnerDocumentDto>(),
                actorId))
            .Callback<Guid, CreateBusinessPartnerDocumentDto, Guid>(
                (_, request, _) => capturedDocument = request)
            .ReturnsAsync(document);
        registrations.Setup(item => item.GetDocumentByIdAsync(
                registrationId, documentId))
            .ReturnsAsync(document);
        registrations.Setup(item => item.DeleteDocumentAsync(
                registrationId, documentId, actorId))
            .Returns(Task.CompletedTask);

        var unitOfWork = TransactionalUnitOfWork();
        var controller = Controller(
            access.Object, tokens.Object, registrations.Object,
            controlledFiles.Object, sessionReference, unitOfWork.Object,
            centralDocuments.Object);

        var methodsResult = await controller.GetPaymentMethods(CancellationToken.None);
        methodsResult.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(paymentMethods);

        var payment = new RecordProcurementSupplierOnboardingPaymentRequest
        {
            PaymentMethodId = paymentMethodId,
            PaymentReference = "MOMO-12345",
            RowVersion = "unpaid-row-version"
        };
        var paymentResult = await controller.RecordPayment(
            payment, CancellationToken.None);
        paymentResult.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(pendingToken);
        tokens.Verify(item => item.RecordPaymentAsync(
            tokenId,
            It.Is<RecordProcurementSupplierOnboardingPaymentRequest>(request =>
                request.PaymentMethodId == paymentMethodId &&
                request.PaymentReference == "MOMO-12345"),
            It.Is<string>(value => value.Length >= 8),
            It.IsAny<CancellationToken>()), Times.Once);

        var bytes = Encoding.UTF8.GetBytes("clean evidence");
        await using var stream = new MemoryStream(bytes);
        var file = new FormFile(
            stream, 0, bytes.Length, "file", "../evidence.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
        var uploadResult = await controller.UploadDocument(
            file,
            "TaxClearance",
            "SUP-TAX",
            "Restricted",
            new DateTime(2026, 7, 1),
            new DateTime(2027, 7, 1),
            CancellationToken.None);
        uploadResult.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeSameAs(document);
        capturedUpload.Should().NotBeNull();
        capturedUpload!.TenantId.Should().Be(tenantId);
        capturedUpload.ActorUserId.Should().Be(actorId);
        capturedUpload.Category.Should().Be(ControlledFileUploadCategories.DocumentManagement);
        capturedUpload.FileName.Should().Be("evidence.pdf");
        capturedDocument.Should().NotBeNull();
        capturedDocument!.FileUploadRecordId.Should().Be(fileRecordId);
        capturedDocument.CentralDocumentRecordId.Should().Be(centralDocumentRecordId);
        capturedDocument.CentralDocumentVersionId.Should().Be(centralDocumentVersionId);
        capturedDocument.ChecksumSha256.Should().Be(new string('a', 64));
        capturedDocument.EvidenceRequirementCode.Should().Be("SUP-TAX");
        capturedDocument.ClassificationCode.Should().Be("Restricted");
        capturedDocument.IssueDate.Should().Be(new DateTime(2026, 7, 1));
        capturedDocument.ExpiryDate.Should().Be(new DateTime(2027, 7, 1));

        var deleteResult = await controller.DeleteDocument(
            documentId, CancellationToken.None);
        deleteResult.Should().BeOfType<NoContentResult>();
        registrations.Verify(item => item.DeleteDocumentAsync(
            registrationId, documentId, actorId), Times.Once);
        centralDocuments.Verify(item => item.DeleteAsync(
            tenantId,
            centralDocumentRecordId,
            actorId,
            It.IsAny<CancellationToken>()),
            Times.Once);
        unitOfWork.Verify(item => item.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(item => item.CommitAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(item => item.RollbackAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MetadataDeleteFailureRollsBackDocumentDeleteAndCanBeRetried()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var sessionReference = Guid.NewGuid();
        var fileRecordId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var access = new Mock<IProcurementSupplierApplicantAccessService>();
        access.Setup(item => item.ValidateSessionAsync(
                sessionReference,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupplierApplicantSessionDto
            {
                SessionReference = sessionReference,
                SystemActorUserId = actorId,
                TenantId = tenantId,
                RegistrationId = registrationId,
                TokenId = Guid.NewGuid(),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                PaymentOnly = false
            });
        var document = new BusinessPartnerRegistrationDocumentDto
        {
            Id = documentId,
            RegistrationId = registrationId,
            FileUploadRecordId = fileRecordId,
            DocumentType = "TaxClearance",
            DocumentName = "evidence.pdf",
            FilePath = "supplier-registration-evidence/evidence.pdf",
            FileSize = 14
        };
        var registrations = new Mock<IBusinessPartnerRegistrationService>();
        registrations.Setup(item => item.GetDocumentByIdAsync(
                registrationId, documentId))
            .ReturnsAsync(document);
        registrations.Setup(item => item.DeleteDocumentAsync(
                registrationId, documentId, actorId))
            .Returns(Task.CompletedTask);
        var controlledFiles = new Mock<IControlledFileUploadService>();
        controlledFiles.SetupSequence(item => item.DeleteAsync(
                tenantId,
                fileRecordId,
                actorId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ControlledFileUploadException(
                "FILE_METADATA_DELETE_FAILED",
                "The controlled file metadata could not be deleted.",
                StatusCodes.Status502BadGateway))
            .Returns(Task.CompletedTask);
        var unitOfWork = TransactionalUnitOfWork();
        var controller = Controller(
            access.Object,
            Mock.Of<IProcurementSupplierOnboardingTokenService>(),
            registrations.Object,
            controlledFiles.Object,
            sessionReference,
            unitOfWork.Object);

        var failed = await controller.DeleteDocument(
            documentId, CancellationToken.None);
        failed.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);

        var retried = await controller.DeleteDocument(
            documentId, CancellationToken.None);
        retried.Should().BeOfType<NoContentResult>();

        registrations.Verify(item => item.DeleteDocumentAsync(
            registrationId, documentId, actorId), Times.Exactly(2));
        controlledFiles.Verify(item => item.DeleteAsync(
            tenantId, fileRecordId, actorId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        unitOfWork.Verify(item => item.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        unitOfWork.Verify(item => item.RollbackAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(item => item.CommitAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaymentOnlyApplicantCannotUploadDocuments()
    {
        var sessionReference = Guid.NewGuid();
        var access = new Mock<IProcurementSupplierApplicantAccessService>();
        access.Setup(item => item.ValidateSessionAsync(
                sessionReference,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupplierApplicantSessionDto
            {
                SessionReference = sessionReference,
                SystemActorUserId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                RegistrationId = Guid.NewGuid(),
                TokenId = Guid.NewGuid(),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                PaymentOnly = true
            });
        var controlledFiles = new Mock<IControlledFileUploadService>();
        var controller = Controller(
            access.Object,
            Mock.Of<IProcurementSupplierOnboardingTokenService>(),
            Mock.Of<IBusinessPartnerRegistrationService>(),
            controlledFiles.Object,
            sessionReference);
        var bytes = Encoding.UTF8.GetBytes("evidence");
        await using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "evidence.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await controller.UploadDocument(
            file, "TaxClearance", null, null, null, null,
            CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        controlledFiles.Verify(item => item.UploadAsync(
            It.IsAny<ControlledFileUploadRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PaymentOnlyApplicantCannotDeleteDocuments()
    {
        var sessionReference = Guid.NewGuid();
        var access = new Mock<IProcurementSupplierApplicantAccessService>();
        access.Setup(item => item.ValidateSessionAsync(
                sessionReference,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupplierApplicantSessionDto
            {
                SessionReference = sessionReference,
                SystemActorUserId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                RegistrationId = Guid.NewGuid(),
                TokenId = Guid.NewGuid(),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                PaymentOnly = true
            });
        var registrations = new Mock<IBusinessPartnerRegistrationService>();
        var controlledFiles = new Mock<IControlledFileUploadService>();
        var unitOfWork = TransactionalUnitOfWork();
        var controller = Controller(
            access.Object,
            Mock.Of<IProcurementSupplierOnboardingTokenService>(),
            registrations.Object,
            controlledFiles.Object,
            sessionReference,
            unitOfWork.Object);

        var result = await controller.DeleteDocument(
            Guid.NewGuid(), CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().BeEquivalentTo(new
        {
            code = "SUPPLIER_APPLICANT_PAYMENT_REQUIRED",
            message = "Payment or an approved exemption is required before deleting documents."
        });
        registrations.Verify(item => item.GetDocumentByIdAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        registrations.Verify(item => item.DeleteDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        controlledFiles.Verify(item => item.DeleteAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(item => item.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuccessfulPasswordReplacementClearsForcedStateAndActivatesSupplier()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string temporaryPassword = "Temporary#Password123";
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "approved@example.test",
            FirstName = "Approved",
            LastName = "Supplier",
            IsActive = true,
            MustChangePassword = true,
            TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };
        user.PasswordHash =
            new PasswordHasher<ApplicationUser>().HashPassword(
                user, temporaryPassword);

        var users = new Mock<IUserService>();
        users.Setup(item => item.GetUserByIdAsync(userId)).ReturnsAsync(user);
        users.Setup(item => item.UpdateUserAsync(user)).ReturnsAsync(user);
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.UserId).Returns(userId.ToString());
        current.SetupGet(item => item.UserName).Returns(user.UserName);
        var settings = new Mock<ISettingsService>();
        settings.Setup(item => item.GetSecuritySettingsAsync(tenantId))
            .ReturnsAsync((Security?)null);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(item => item.LogUserActionAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        var applicantAccess =
            new Mock<IProcurementSupplierApplicantAccessService>();
        applicantAccess.Setup(item => item.CompleteCredentialActivationAsync(
                userId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(item => item.ExecuteInStrategyAsync(
                It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> operation, CancellationToken _) => operation());
        unitOfWork.Setup(item => item.BeginTransactionAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.Setup(item => item.CommitAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = new UserController(
            users.Object,
            NullLogger<UserController>.Instance,
            audit.Object,
            current.Object,
            settings.Object,
            applicantAccess.Object,
            unitOfWork.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthenticatedContext(userId)
            }
        };

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = temporaryPassword,
            NewPassword = "Permanent#Password456"
        });

        result.Should().BeOfType<OkObjectResult>();
        user.MustChangePassword.Should().BeFalse();
        user.TemporaryPasswordExpiresAtUtc.Should().BeNull();
        user.PasswordChangedAtUtc.Should().NotBeNull();
        new PasswordHasher<ApplicationUser>().VerifyHashedPassword(
                user, user.PasswordHash!, "Permanent#Password456")
            .Should().NotBe(PasswordVerificationResult.Failed);
        users.Verify(item => item.UpdateUserAsync(user), Times.Once);
        applicantAccess.Verify(item => item.CompleteCredentialActivationAsync(
            userId,
            It.Is<string>(value =>
                value == $"supplier-credential-activation-{userId:N}"),
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(item => item.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(item => item.CommitAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(item => item.RollbackAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivationFailureRollsBackPasswordReplacementTransaction()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string temporaryPassword = "Temporary#Password123";
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "approved@example.test",
            FirstName = "Approved",
            LastName = "Supplier",
            IsActive = true,
            MustChangePassword = true,
            TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };
        user.PasswordHash =
            new PasswordHasher<ApplicationUser>().HashPassword(
                user, temporaryPassword);

        var users = new Mock<IUserService>();
        users.Setup(item => item.GetUserByIdAsync(userId)).ReturnsAsync(user);
        users.Setup(item => item.UpdateUserAsync(user)).ReturnsAsync(user);
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.UserId).Returns(userId.ToString());
        current.SetupGet(item => item.UserName).Returns(user.UserName);
        var settings = new Mock<ISettingsService>();
        settings.Setup(item => item.GetSecuritySettingsAsync(tenantId))
            .ReturnsAsync((Security?)null);
        var applicantAccess =
            new Mock<IProcurementSupplierApplicantAccessService>();
        applicantAccess.Setup(item => item.CompleteCredentialActivationAsync(
                userId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("activation audit failed"));
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(item => item.ExecuteInStrategyAsync(
                It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> operation, CancellationToken _) => operation());
        unitOfWork.Setup(item => item.BeginTransactionAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.Setup(item => item.RollbackAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = new UserController(
            users.Object,
            NullLogger<UserController>.Instance,
            Mock.Of<IAuditLogService>(),
            current.Object,
            settings.Object,
            applicantAccess.Object,
            unitOfWork.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthenticatedContext(userId)
            }
        };

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = temporaryPassword,
            NewPassword = "Permanent#Password456"
        });

        var failure = result.Should().BeOfType<ObjectResult>().Subject;
        failure.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        unitOfWork.Verify(item => item.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(item => item.RollbackAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(item => item.CommitAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TemporaryCredentialExpiryIsRevalidatedInsideTransaction()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string temporaryPassword = "Temporary#Password123";
        var outerUser = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "approved@example.test",
            FirstName = "Approved",
            LastName = "Supplier",
            IsActive = true,
            MustChangePassword = true,
            TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        outerUser.PasswordHash =
            new PasswordHasher<ApplicationUser>().HashPassword(
                outerUser, temporaryPassword);
        var transactionalUser = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = outerUser.UserName,
            FirstName = outerUser.FirstName,
            LastName = outerUser.LastName,
            IsActive = true,
            MustChangePassword = true,
            TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1),
            PasswordHash = outerUser.PasswordHash
        };

        var users = new Mock<IUserService>();
        users.SetupSequence(item => item.GetUserByIdAsync(userId))
            .ReturnsAsync(outerUser)
            .ReturnsAsync(transactionalUser);
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.UserId).Returns(userId.ToString());
        current.SetupGet(item => item.UserName).Returns(outerUser.UserName);
        var settings = new Mock<ISettingsService>();
        settings.Setup(item => item.GetSecuritySettingsAsync(tenantId))
            .ReturnsAsync((Security?)null);
        var applicantAccess =
            new Mock<IProcurementSupplierApplicantAccessService>();
        var unitOfWork = TransactionalUnitOfWork();
        var controller = new UserController(
            users.Object,
            NullLogger<UserController>.Instance,
            Mock.Of<IAuditLogService>(),
            current.Object,
            settings.Object,
            applicantAccess.Object,
            unitOfWork.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthenticatedContext(userId)
            }
        };

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = temporaryPassword,
            NewPassword = "Permanent#Password456"
        });

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status410Gone);
        users.Verify(item => item.UpdateUserAsync(
            It.IsAny<ApplicationUser>()), Times.Never);
        applicantAccess.Verify(item => item.CompleteCredentialActivationAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(item => item.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(item => item.RollbackAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(item => item.CommitAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SupplierApplicantAccessController Controller(
        IProcurementSupplierApplicantAccessService access,
        IProcurementSupplierOnboardingTokenService tokens,
        IBusinessPartnerRegistrationService registrations,
        IControlledFileUploadService controlledFiles,
        Guid sessionReference,
        IUnitOfWork? unitOfWork = null,
        ICentralDocumentRepositoryFileService? centralDocuments = null,
        IFileStorageService? fileStorage = null) =>
        new(
            access,
            tokens,
            registrations,
            Mock.Of<ITenantService>(),
            Mock.Of<IOtpService>(),
            Mock.Of<ITenantSmsSender>(),
            Mock.Of<INotificationService>(),
            Mock.Of<ICaptchaVerificationService>(),
            Mock.Of<IProcurementSupplierApplicantJwtService>(),
            controlledFiles,
            centralDocuments ?? Mock.Of<ICentralDocumentRepositoryFileService>(),
            fileStorage ?? Mock.Of<IFileStorageService>(),
            unitOfWork ?? TransactionalUnitOfWork().Object,
            NullLogger<SupplierApplicantAccessController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ApplicantContext(sessionReference)
            }
        };

    private static Mock<IUnitOfWork> TransactionalUnitOfWork()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(item => item.ExecuteInStrategyAsync(
                It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> operation, CancellationToken _) => operation());
        unitOfWork.Setup(item => item.BeginTransactionAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.Setup(item => item.CommitAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.Setup(item => item.RollbackAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return unitOfWork;
    }

    private static DefaultHttpContext ApplicantContext(Guid sessionReference)
    {
        var context = AuthenticatedContext(Guid.NewGuid());
        context.User.AddIdentity(new ClaimsIdentity(
        [
            new Claim(
                "supplier_applicant_session",
                sessionReference.ToString())
        ]));
        return context;
    }

    private static DefaultHttpContext AuthenticatedContext(Guid userId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], authenticationType: "test"));
        context.Response.Body = new MemoryStream();
        return context;
    }
}
