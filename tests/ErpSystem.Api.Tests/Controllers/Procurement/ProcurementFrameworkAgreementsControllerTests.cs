using System.Reflection;
using System.Text;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementFrameworkAgreementsControllerTests
{
    [Fact]
    public void ControllerRequiresInternalAccessAndExposesTheCompleteLifecycle()
    {
        var type = typeof(ProcurementFrameworkAgreementsController);

        type.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should()
            .Be("InternalOnly");
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/framework-agreements");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "Summary",
                "Search",
                "Get",
                "WorkflowOptions",
                "CategoryOptions",
                "ItemOptions",
                "SourceOptions",
                "Create",
                "Update",
                "Clone",
                "Submit",
                "Approve",
                "Reject",
                "Terminate",
                "AddDocument",
                "RetireDocument",
                "DownloadDocument",
                "RequestExtension",
                "DecideExtension",
                "ProcessLifecycle"
            ]);
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementFrameworkAgreementService>();
        service.Setup(item => item.GetAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementFrameworkAgreementNotFoundException(
                "FRAMEWORK_NOT_FOUND", "Missing."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementFrameworkAgreementAuthorizationException(
                "Forbidden."));
        service.Setup(item => item.UpdateAsync(
                Guid.Empty,
                It.IsAny<UpdateProcurementFrameworkAgreementRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementFrameworkAgreementConflictException(
                "FRAMEWORK_STALE", "Conflict."));
        service.Setup(item => item.CreateAsync(
                It.IsAny<CreateProcurementFrameworkAgreementRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementFrameworkAgreementValidationException(
                "DEC001_REQUIRED", "Invalid."));
        var controller = Controller(service);

        AssertProblem(await controller.Get(Guid.Empty, default), 404, "FRAMEWORK_NOT_FOUND");
        AssertProblem(await controller.Summary(default), 403,
            "FRAMEWORK_AGREEMENT_ACCESS_FORBIDDEN");
        AssertProblem(await controller.Update(
            Guid.Empty, new UpdateProcurementFrameworkAgreementRequest(), default),
            409, "FRAMEWORK_STALE");
        AssertProblem(await controller.Create(
            new CreateProcurementFrameworkAgreementRequest(), default),
            422, "DEC001_REQUIRED");
    }

    [Fact]
    public async Task DocumentUploadUsesCentralControlledBoundaryAndCompensatesOnDomainFailure()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var agreementId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        ControlledFileUploadRequest? captured = null;
        var controlledFiles = new Mock<IControlledFileUploadService>();
        controlledFiles.Setup(item => item.UploadAsync(
                It.IsAny<ControlledFileUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<ControlledFileUploadRequest, CancellationToken>(
                (request, _) => captured = request)
            .ReturnsAsync(new ControlledFileUploadResult
            {
                Record = new FileUploadRecord
                {
                    Id = uploadId,
                    TenantId = tenantId,
                    Category = ControlledFileUploadCategories.DocumentManagement,
                    FilePath = "document-management/agreement.pdf",
                    StoredFileName = "agreement.pdf",
                    OriginalFileName = "agreement.pdf",
                    ContentType = "application/pdf",
                    FileSize = 9,
                    StorageProvider = "test",
                    UploadedByUserId = actorId,
                    VirusScanStatus = FileVirusScanStatus.Clean
                },
                ChecksumSha256 = new string('a', 64),
                PublicUrl = "/files/agreement.pdf"
            });
        controlledFiles.Setup(item => item.DeleteAsync(
                tenantId, uploadId, actorId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new Mock<IProcurementFrameworkAgreementService>();
        service.Setup(item => item.AddDocumentAsync(
                agreementId,
                It.IsAny<AddProcurementFrameworkAgreementDocumentRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementFrameworkAgreementConflictException(
                "FRAMEWORK_DOCUMENT_CONFLICT", "The draft changed."));
        var controller = Controller(
            service, controlledFiles, CurrentUser(tenantId, actorId));
        var bytes = Encoding.UTF8.GetBytes("clean pdf");
        await using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "../agreement.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await controller.AddDocument(
            agreementId, file, "ExecutedAgreement", null, true, default);

        AssertProblem(result, 409, "FRAMEWORK_DOCUMENT_CONFLICT");
        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(tenantId);
        captured.ActorUserId.Should().Be(actorId);
        captured.ActorName.Should().Be("Procurement Officer");
        captured.Category.Should().Be(ControlledFileUploadCategories.DocumentManagement);
        captured.FileName.Should().Be("agreement.pdf");
        service.Verify(item => item.AddDocumentAsync(
            agreementId,
            It.Is<AddProcurementFrameworkAgreementDocumentRequest>(request =>
                request.FileUploadRecordId == uploadId &&
                request.DocumentType == "ExecutedAgreement" &&
                request.Title == "agreement.pdf" &&
                request.IsRequired),
            "corr-0401",
            It.IsAny<CancellationToken>()), Times.Once);
        controlledFiles.Verify(item => item.DeleteAsync(
            tenantId, uploadId, actorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DocumentDownloadIsPrivateNonExecutableAndNonRange()
    {
        var agreementId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var service = new Mock<IProcurementFrameworkAgreementService>();
        service.Setup(item => item.OpenDocumentAsync(
                agreementId, documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpSystem.Core.Interfaces.DocumentManagement.CentralDocumentRepositoryContent
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("agreement")),
                FileName = "agreement.pdf",
                ContentType = "application/pdf",
                FileSize = 9,
                UploadRecord = new FileUploadRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = Guid.NewGuid(),
                    Category = ControlledFileUploadCategories.DocumentManagement,
                    FilePath = "document-management/agreement.pdf",
                    StoredFileName = "agreement.pdf",
                    OriginalFileName = "agreement.pdf",
                    StorageProvider = "test"
                }
            });
        var controller = Controller(service);

        var result = await controller.DownloadDocument(
            agreementId, documentId, default);

        var file = result.Should().BeOfType<FileStreamResult>().Subject;
        file.EnableRangeProcessing.Should().BeFalse();
        file.FileDownloadName.Should().Be("agreement.pdf");
        controller.Response.Headers.CacheControl.ToString().Should()
            .Be("no-store, private");
        controller.Response.Headers.Pragma.ToString().Should().Be("no-cache");
        controller.Response.Headers.XContentTypeOptions.ToString().Should()
            .Be("nosniff");
        await file.FileStream.DisposeAsync();
    }

    private static void AssertProblem(
        IActionResult result,
        int expectedStatus,
        string expectedCode)
    {
        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(expectedStatus);
        var details = problem.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        details.Extensions["code"].Should().Be(expectedCode);
        details.Extensions["correlationId"].Should().Be("corr-0401");
    }

    private static ProcurementFrameworkAgreementsController Controller(
        Mock<IProcurementFrameworkAgreementService> service,
        Mock<IControlledFileUploadService>? controlledFiles = null,
        Mock<ICurrentUserProvider>? currentUser = null)
    {
        var controller = new ProcurementFrameworkAgreementsController(
            service.Object,
            (controlledFiles ?? new Mock<IControlledFileUploadService>()).Object,
            (currentUser ?? CurrentUser(Guid.NewGuid(), Guid.NewGuid())).Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "trace-0401"
                }
            }
        };
        controller.Request.Headers["X-Correlation-ID"] = "corr-0401";
        return controller;
    }

    private static Mock<ICurrentUserProvider> CurrentUser(
        Guid tenantId,
        Guid actorId)
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(actorId);
        currentUser.SetupGet(item => item.Username).Returns("officer@tdc.test");
        currentUser.SetupGet(item => item.FullName).Returns("Procurement Officer");
        return currentUser;
    }
}
