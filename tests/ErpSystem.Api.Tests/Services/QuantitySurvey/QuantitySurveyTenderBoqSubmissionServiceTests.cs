using System.Text.Json;
using ClosedXML.Excel;
using ErpSystem.Api.Services.QuantitySurvey;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyTenderBoqSubmissionServiceTests
{
    [Fact]
    public async Task Protected_template_preserves_approved_publication_lineage_and_editable_price_cells()
    {
        await using var fixture = await CreateFixtureAsync();

        var file = await fixture.Service.CreateExternalTemplateAsync(fixture.BidId);

        file.FileName.Should().EndWith("-tenderer-boq.xlsx");
        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        var sheet = workbook.Worksheet("Tenderer BOQ");
        sheet.Protection.IsProtected.Should().BeTrue();
        sheet.Column(1).IsHidden.Should().BeTrue();
        sheet.Column(2).IsHidden.Should().BeTrue();
        sheet.Column(3).IsHidden.Should().BeTrue();
        sheet.Cell(6, 8).Style.Protection.Locked.Should().BeTrue();
        sheet.Cell(6, 9).Style.Protection.Locked.Should().BeFalse();
        sheet.Cell(6, 10).Style.Protection.Locked.Should().BeFalse();
        sheet.Cell(6, 11).FormulaA1.Should().Be("ROUND(I6*J6,2)");
        workbook.Worksheet("Control").Visibility.Should().Be(XLWorksheetVisibility.VeryHidden);
    }

    [Fact]
    public async Task Portal_submission_gate_is_arithmetic_checked_and_idempotent_on_retry()
    {
        await using var fixture = await CreateFixtureAsync();

        await fixture.Service.EnsureReadyForTenderSubmissionAsync(fixture.BidId, "first-attempt");
        await fixture.Service.EnsureReadyForTenderSubmissionAsync(fixture.BidId, "retry-attempt");

        var submissions = await fixture.Db.QuantitySurveyTenderBoqSubmissions
            .Include(item => item.Lines)
            .ToListAsync();
        submissions.Should().ContainSingle();
        var submission = submissions.Single();
        submission.Channel.Should().Be(QuantitySurveyExternalSubmissionChannel.ExternalPortal);
        submission.Status.Should().Be(QuantitySurveyTenderBoqSubmissionStatus.Committed);
        submission.TenderBoqVersionId.Should().Be(fixture.PublicationId);
        submission.SubmittedTotal.Should().Be(100m);
        submission.Lines.Should().ContainSingle(item =>
            item.TenderItemId == fixture.TenderItemId &&
            item.ProjectBoqVersionLineId == fixture.PublicationLineId &&
            item.CalculatedLineTotal == 100m);
    }

    [Fact]
    public async Task External_owner_boundary_rejects_a_different_authenticated_user()
    {
        await using var fixture = await CreateFixtureAsync(userOwnsPartner: false);

        var action = () => fixture.Service.GetExternalContextAsync(fixture.BidId);

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*business-partner identity*");
    }

    private static async Task<Fixture> CreateFixtureAsync(bool userOwnsPartner = true)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var requisitionId = Guid.NewGuid();
        var tenderId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var bidId = Guid.NewGuid();
        var tenderItemId = Guid.NewGuid();
        var bidItemId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();
        var publicationLineId = Guid.NewGuid();

        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"qs-tender-boq-{Guid.NewGuid():N}")
                .Options);

        var project = new Project
        {
            Id = projectId,
            TenantId = tenantId,
            ProjectCode = "PRJ-QS-001",
            Title = "Controlled tender project",
            Status = ProjectStatuses.InProgress,
        };
        var requisition = new PurchaseRequisition
        {
            Id = requisitionId,
            TenantId = tenantId,
            RequisitionNumber = "PR-QS-001",
            RequestedById = userId,
            ProjectId = projectId,
            ProjectCode = project.ProjectCode,
            ProjectName = project.Title,
        };
        var tender = new Tender
        {
            Id = tenderId,
            TenantId = tenantId,
            TenderNumber = "TND-QS-001",
            Title = "QS works tender",
            TenderType = "ITB",
            Status = "Published",
            Currency = "GHS",
            SourcePurchaseRequisitionId = requisitionId,
            SourcePurchaseRequisition = requisition,
        };
        var partner = new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CON-001",
            PartnerName = "Controlled Contractor",
            PartnerType = "Contractor",
            UserId = userOwnsPartner ? userId : Guid.NewGuid(),
        };
        var bid = new TenderBid
        {
            Id = bidId,
            TenantId = tenantId,
            TenderId = tenderId,
            Tender = tender,
            BusinessPartnerId = partnerId,
            BusinessPartner = partner,
            BidNumber = "BID-QS-001",
            Status = "Draft",
            Currency = "GHS",
            TotalBidAmount = 100m,
        };
        var tenderItem = new TenderItem
        {
            Id = tenderItemId,
            TenantId = tenantId,
            TenderId = tenderId,
            Tender = tender,
            LineNumber = 1,
            ItemCode = "BOQ-001",
            Description = "Excavate foundation trench",
            Quantity = 2m,
            UnitOfMeasure = "m3",
        };
        var bidItem = new TenderBidItem
        {
            Id = bidItemId,
            TenantId = tenantId,
            TenderBidId = bidId,
            TenderBid = bid,
            TenderItemId = tenderItemId,
            TenderItem = tenderItem,
            OfferedQuantity = 2m,
            UnitPrice = 50m,
            TotalPrice = 100m,
        };
        var publication = new ProjectBoqVersion
        {
            Id = publicationId,
            TenantId = tenantId,
            ProjectId = projectId,
            Project = project,
            VersionNumber = 3,
            VersionType = QuantitySurveyBoqVersionType.Approved,
            Status = ProjectBoqVersionStatuses.Approved,
            ApprovalStatus = ProjectBoqVersionStatuses.Approved,
            PublishedAt = DateTime.UtcNow,
            ChangeSummary = "Approved tender publication",
            AuditAction = "PublishBoqVersion",
            SnapshotHash = new string('A', 64),
            LineCount = 1,
            SnapshotAt = DateTime.UtcNow,
            ActorRoles = "TDC_SUPERVISING_QUANTITY_SURVEYOR",
            CorrelationId = "test",
        };
        var publicationLine = new ProjectBoqVersionLine
        {
            Id = publicationLineId,
            TenantId = tenantId,
            ProjectId = projectId,
            ProjectBoqVersionId = publicationId,
            Version = publication,
            LineKey = Guid.NewGuid(),
            LineNumber = "1",
            ItemCode = tenderItem.ItemCode,
            Description = tenderItem.Description,
            Quantity = tenderItem.Quantity,
            UnitOfMeasure = tenderItem.UnitOfMeasure,
            UnitRate = 45m,
            LineAmount = 90m,
            Currency = "GHS",
            SortOrder = 1,
        };
        publication.Lines.Add(publicationLine);

        db.AddRange(project, requisition, tender, partner, bid, tenderItem, bidItem, publication);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId.ToString());
        currentUser.SetupGet(item => item.UserName).Returns("contractor.user");
        currentUser.SetupGet(item => item.Roles).Returns(["TDC_EXTERNAL_CONTRACTOR"]);

        var policy = new QsExternalSubmissionValue
        {
            EffectiveFrom = DateTime.UtcNow.Date.AddDays(-1),
            Channels =
            [
                QuantitySurveyExternalSubmissionChannel.ExternalPortal,
                QuantitySurveyExternalSubmissionChannel.ControlledExcel,
            ],
            AllowedFileExtensions = [".xlsx"],
            MaximumFileSizeMb = 20,
            RequirePortalIdentity = true,
            RequireEvidence = false,
            RequireSignature = true,
        };
        var configuration = new Mock<IQuantitySurveyConfigurationService>();
        configuration.Setup(item => item.GetEffectiveProfileAsync(
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuantitySurveyProfileDto
            {
                Decisions =
                [
                    new QuantitySurveyDecisionDto
                    {
                        DecisionKey = "QS-DEC-013",
                        IsComplete = true,
                        Value = JsonSerializer.SerializeToElement(policy),
                    },
                ],
            });

        var service = new QuantitySurveyTenderBoqSubmissionService(
            db,
            currentUser.Object,
            configuration.Object,
            Mock.Of<IControlledFileUploadService>(),
            Mock.Of<ICentralDocumentRepositoryFileService>(),
            new EphemeralDataProtectionProvider());
        return new Fixture(
            db,
            service,
            bidId,
            tenderItemId,
            publicationId,
            publicationLineId);
    }

    private sealed record Fixture(
        ApplicationDbContext Db,
        QuantitySurveyTenderBoqSubmissionService Service,
        Guid BidId,
        Guid TenderItemId,
        Guid PublicationId,
        Guid PublicationLineId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
