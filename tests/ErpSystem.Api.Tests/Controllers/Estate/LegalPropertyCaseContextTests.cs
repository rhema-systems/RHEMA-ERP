using System.Text.Json;
using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Estate;

public sealed class LegalPropertyCaseContextTests
{
    [Fact]
    public async Task LinkedLandReviewShowsParcelDocumentsInquiryAndOpportunity()
    {
        var tenantId = Guid.NewGuid();
        var legalCaseId = Guid.NewGuid();
        var sourceCaseId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var demarcationId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        db.ProcedureCases.Add(new ProcedureCase
        {
            Id = sourceCaseId, TenantId = tenantId, Module = "PropertyManagement",
            EntityType = "EstatePropertyManagementListingApplication", Title = "Land sale",
            OpenedById = Guid.NewGuid(),
            Fields =
            [
                new ProcedureCaseField { TenantId = tenantId, Key = "listingId", Label = "Listing", Value = demarcationId.ToString() },
                new ProcedureCaseField { TenantId = tenantId, Key = "listingRecordType", Label = "Listing type", Value = "EstateLandDemarcation" },
                new ProcedureCaseField { TenantId = tenantId, Key = "salesOpportunityId", Label = "Opportunity", Value = opportunityId.ToString() },
                new ProcedureCaseField { TenantId = tenantId, Key = "ehcTicketId", Label = "Inquiry", Value = ticketId.ToString() }
            ],
            Documents = [new ProcedureCaseDocument { TenantId = tenantId, Name = "Survey plan", ProvidedBy = "Estate", FileName = "survey.pdf", FileUrl = "private/survey.pdf" }]
        });
        db.EstateManagedAssets.Add(new EstateManagedAsset
        {
            Id = assetId, TenantId = tenantId, AssetCode = "LAND-001", Name = "North parcel",
            AssetType = EstateManagedAssetType.Land, Status = EstateManagedAssetStatus.LandBank
        });
        db.EstateLandDemarcations.Add(new EstateLandDemarcation
        {
            Id = demarcationId, TenantId = tenantId, EstateManagedAssetId = assetId,
            Description = "Whole parcel", BoundaryCoordinates = "0,0;1,1"
        });
        db.EstateManagedAssetDocuments.Add(new EstateManagedAssetDocument
        {
            TenantId = tenantId, EstateManagedAssetId = assetId,
            FileName = "title.pdf", FilePath = "private/title.pdf", DocumentType = "Title"
        });
        db.Opportunities.Add(new Opportunity
        {
            Id = opportunityId, TenantId = tenantId, Name = "North parcel sale",
            Stage = "Closed Won", Amount = 500000m, Currency = "GHS"
        });
        db.EhcTickets.Add(new EhcTicket
        {
            Id = ticketId, TenantId = tenantId, TicketNumber = "INQ-001",
            Subject = "North parcel inquiry", Description = "Customer asked about the parcel",
            RequesterUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        var cases = new Mock<IProcedureCaseService>();
        cases.Setup(item => item.GetCaseAsync(legalCaseId)).ReturnsAsync(new ProcedureCaseDetailDto(
            legalCaseId, "Legal", "LegalPropertyAgreementReview", "Review", null, null, null,
            null, null, "Open", 0, "Legal review", null, null, false, null, true,
            [], [new ProcedureCaseFieldDto(Guid.NewGuid(), "sourceProcedureCaseId", "Source case", "text", sourceCaseId.ToString(), null)],
            [], [], []));
        var controller = new LegalPropertyCaseContextController(db, currentUser.Object, cases.Object, Mock.Of<IFileStorageService>());

        var response = Assert.IsType<OkObjectResult>(await controller.GetContext(legalCaseId));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("LAND-001", data.GetProperty("asset").GetProperty("AssetCode").GetString());
        Assert.Equal("title.pdf", data.GetProperty("assetDocuments")[0].GetProperty("FileName").GetString());
        Assert.Equal("North parcel sale", data.GetProperty("opportunity").GetProperty("Name").GetString());
        Assert.Equal("INQ-001", data.GetProperty("inquiry").GetProperty("ticket").GetProperty("TicketNumber").GetString());
        Assert.Equal("survey.pdf", data.GetProperty("sourceCase").GetProperty("documents")[0].GetProperty("FileName").GetString());
    }
}
