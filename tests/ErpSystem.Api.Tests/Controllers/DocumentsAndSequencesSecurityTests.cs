using System.Reflection;
using System.Security.Claims;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Numbering;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers;

public sealed class DocumentsAndSequencesSecurityTests
{
    [Fact]
    [Trait("Category", "FinanceSecurity")]
    public void DocumentSequencesController_ShouldRequireAuthentication()
    {
        typeof(DocumentSequencesController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should()
            .NotBeEmpty("document sequence generation advances tenant numbering state");
    }

    [Theory]
    [InlineData(nameof(DocumentSequencesController.GetDefinitions), FinancePermissions.ViewFinance)]
    [InlineData(nameof(DocumentSequencesController.Generate), FinancePermissions.MaintainFinance)]
    [InlineData(nameof(DocumentSequencesController.EnsureDefaults), FinancePermissions.AdministerFinance)]
    [InlineData(nameof(DocumentSequencesController.Update), FinancePermissions.AdministerFinance)]
    [Trait("Category", "FinanceSecurity")]
    public void DocumentSequencesController_Actions_ShouldRequireExplicitFinancePolicies(string actionName, string expectedPolicy)
    {
        var action = typeof(DocumentSequencesController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == actionName);

        action
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(attribute => attribute.Policy)
            .Should()
            .Contain(expectedPolicy, "document sequence endpoints mutate or expose tenant numbering state outside the Finance controller convention");
    }

    [Fact]
    [Trait("Category", "TenantIsolation")]
    public async Task GenerateDocumentNumber_ShouldIgnoreClientTenantAndUseCurrentTenant()
    {
        var currentTenantId = Guid.NewGuid();
        var requestedTenantId = Guid.NewGuid();
        var numbering = new Mock<IDocumentNumberingService>(MockBehavior.Strict);
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);

        currentUser.SetupGet(service => service.TenantId).Returns(currentTenantId);
        numbering
            .Setup(service => service.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.APInvoice,
                currentTenantId,
                It.IsAny<DateTime?>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("VI-2026-00001");

        var controller = new DocumentSequencesController(numbering.Object, currentUser.Object);

        var result = await controller.Generate(new GenerateDocumentNumberRequestDto
        {
            Module = DocumentNumberingModules.Finance,
            DocumentType = FinanceDocumentTypes.APInvoice,
            TenantId = requestedTenantId
        }, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new GeneratedDocumentNumberDto { DocumentNumber = "VI-2026-00001" });
        numbering.Verify(service => service.GenerateAsync(
            DocumentNumberingModules.Finance,
            FinanceDocumentTypes.APInvoice,
            currentTenantId,
            It.IsAny<DateTime?>(),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        numbering.Verify(service => service.GenerateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            requestedTenantId,
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "FinanceSecurity")]
    public async Task FinanceDocumentRender_ShouldRequireFinanceExportPermission()
    {
        var documents = new Mock<IDocumentOutputService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>(MockBehavior.Strict);
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                null,
                FinancePermissions.ExportFinanceReports))
            .ReturnsAsync(AuthorizationResult.Failed());

        var controller = CreateDocumentsController(documents, authorization);

        var result = await controller.RenderDocument(DocumentTypes.FinanceTrialBalance, Guid.NewGuid());

        result.Should().BeOfType<ForbidResult>();
        documents.Verify(service => service.RenderAsync(
            It.IsAny<DocumentRenderRequestDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "FinanceSecurity")]
    public async Task JournalVoucherRender_ShouldUseTheJournalReadBoundary()
    {
        var journalEntryId = Guid.NewGuid();
        var documents = new Mock<IDocumentOutputService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>(MockBehavior.Strict);
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                null,
                FinancePermissions.ViewFinance))
            .ReturnsAsync(AuthorizationResult.Success());
        documents
            .Setup(service => service.RenderAsync(
                It.Is<DocumentRenderRequestDto>(request =>
                    request.DocumentType == DocumentTypes.FinanceJournalVoucher &&
                    request.EntityId == journalEntryId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenderedDocumentDto
            {
                Content = new byte[] { 1, 2, 3 },
                ContentType = "application/pdf",
                FileName = "journal-voucher.pdf",
                DocumentType = DocumentTypes.FinanceJournalVoucher,
                EntityId = journalEntryId,
                Format = "pdf"
            });

        var controller = CreateDocumentsController(documents, authorization);

        var result = await controller.RenderDocument(DocumentTypes.FinanceJournalVoucher, journalEntryId);

        result.Should().BeOfType<FileContentResult>();
        authorization.Verify(service => service.AuthorizeAsync(
            It.IsAny<ClaimsPrincipal>(),
            null,
            FinancePermissions.ViewFinance), Times.Once);
        authorization.Verify(service => service.AuthorizeAsync(
            It.IsAny<ClaimsPrincipal>(),
            It.IsAny<object?>(),
            FinancePermissions.ExportFinanceReports), Times.Never);
    }

    [Fact]
    [Trait("Category", "FinanceSecurity")]
    public async Task NonFinanceDocumentRender_ShouldNotRequireFinanceExportPermission()
    {
        var documents = new Mock<IDocumentOutputService>(MockBehavior.Strict);
        var authorization = new Mock<IAuthorizationService>(MockBehavior.Strict);
        documents
            .Setup(service => service.RenderAsync(
                It.Is<DocumentRenderRequestDto>(request => request.DocumentType == "Sales.Quote"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenderedDocumentDto
            {
                Content = new byte[] { 1, 2, 3 },
                ContentType = "application/pdf",
                FileName = "quote.pdf",
                DocumentType = "Sales.Quote",
                EntityId = Guid.NewGuid(),
                Format = "pdf"
            });

        var controller = CreateDocumentsController(documents, authorization);

        var result = await controller.RenderDocument("Sales.Quote", Guid.NewGuid());

        result.Should().BeOfType<FileContentResult>();
        authorization.Verify(service => service.AuthorizeAsync(
            It.IsAny<ClaimsPrincipal>(),
            It.IsAny<object?>(),
            It.IsAny<string>()), Times.Never);
    }

    private static DocumentsController CreateDocumentsController(
        Mock<IDocumentOutputService> documents,
        Mock<IAuthorizationService> authorization)
    {
        return new DocumentsController(
            documents.Object,
            authorization.Object,
            NullLogger<DocumentsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "tester") }, "Test"))
                }
            }
        };
    }
}
