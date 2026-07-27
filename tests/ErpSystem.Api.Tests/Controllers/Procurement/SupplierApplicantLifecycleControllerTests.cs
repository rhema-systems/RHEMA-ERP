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
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services;
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
    public async Task RestrictedApplicantExecutesPaymentAndControlledDocumentLifecycle()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var sessionReference = Guid.NewGuid();
        var fileRecordId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
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

        CreateBusinessPartnerDocumentDto? capturedDocument = null;
        var registrations = new Mock<IBusinessPartnerRegistrationService>();
        var document = new BusinessPartnerRegistrationDocumentDto
        {
            Id = documentId,
            RegistrationId = registrationId,
            FileUploadRecordId = fileRecordId,
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

        var controller = Controller(
            access.Object, tokens.Object, registrations.Object,
            controlledFiles.Object, sessionReference);

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
        capturedUpload.Category.Should().Be("supplier-registration-evidence");
        capturedUpload.FileName.Should().Be("evidence.pdf");
        capturedDocument.Should().NotBeNull();
        capturedDocument!.FileUploadRecordId.Should().Be(fileRecordId);
        capturedDocument.ChecksumSha256.Should().Be(new string('a', 64));
        capturedDocument.EvidenceRequirementCode.Should().Be("SUP-TAX");

        var deleteResult = await controller.DeleteDocument(
            documentId, CancellationToken.None);
        deleteResult.Should().BeOfType<NoContentResult>();
        registrations.Verify(item => item.DeleteDocumentAsync(
            registrationId, documentId, actorId), Times.Once);
        controlledFiles.Verify(item => item.DeleteAsync(
            tenantId, fileRecordId, actorId, It.IsAny<CancellationToken>()),
            Times.Once);
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

    private static SupplierApplicantAccessController Controller(
        IProcurementSupplierApplicantAccessService access,
        IProcurementSupplierOnboardingTokenService tokens,
        IBusinessPartnerRegistrationService registrations,
        IControlledFileUploadService controlledFiles,
        Guid sessionReference) =>
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
            NullLogger<SupplierApplicantAccessController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ApplicantContext(sessionReference)
            }
        };

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
