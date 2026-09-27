using System.Text.Json;
using ClosedXML.Excel;
using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Estate;

public sealed class EstateManagedAssetImportTests
{
    [Fact]
    public async Task LandTemplateImportsUnverifiedLandBankRecord()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        var controller = new EstateManagedAssetImportsController(db, currentUser.Object);
        var template = Assert.IsType<FileContentResult>(controller.Template("land"));
        using var templateStream = new MemoryStream(template.FileContents);
        using var workbook = new XLWorkbook(templateStream);
        var sheet = workbook.Worksheet(1);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["assetCode"] = "LAND-001", ["recordType"] = "Land", ["name"] = "North parcel",
            ["location"] = "Accra", ["currency"] = "GHS", ["purpose"] = "Residential",
            ["zoningClassification"] = "Residential", ["planningComplianceStatus"] = "Pending",
            ["gisLayerReference"] = "LAND-LAYER", ["cadastreDescription"] = "North boundary",
            ["region"] = "Greater Accra", ["district"] = "Accra Metro", ["town"] = "Accra",
            ["areaValue"] = "1000", ["areaUnit"] = "sqm", ["surveyorName"] = "Surveyor A",
            ["surveyDate"] = "2026-09-20", ["surveyPlanNumber"] = "SURVEY-001",
            ["mapSheetNumber"] = "MAP-1", ["beaconCount"] = "4",
            ["boundaryCoordinates"] = "0,0; 0,1; 1,1; 1,0", ["valuationAmount"] = "500000",
            ["ownerName"] = "Rhema", ["ownershipType"] = "Freehold", ["interestHeld"] = "Full",
            ["identificationType"] = "Registration", ["identificationNumber"] = "REG-1",
            ["contactNumber"] = "0200000000", ["ownerAddress"] = "Accra",
            ["ownershipStartDate"] = "2020-01-01", ["ownershipPercentage"] = "100"
        };
        var headers = sheet.Row(1).CellsUsed().Select(cell => cell.GetString()).ToArray();
        for (var index = 0; index < headers.Length; index++)
            if (values.TryGetValue(headers[index], out var value)) sheet.Cell(2, index + 1).Value = value;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var file = new FormFile(stream, 0, stream.Length, "file", "land.xlsx");
        var mapping = JsonSerializer.Serialize(headers.ToDictionary(key => key, key => key));

        Assert.IsType<OkObjectResult>(await controller.Commit(file, mapping));
        var asset = Assert.Single(await db.EstateManagedAssets.ToListAsync());
        Assert.Equal(ErpSystem.Core.Enums.EstateManagedAssetType.Land, asset.AssetType);
        Assert.Equal(ErpSystem.Core.Enums.EstateManagedAssetStatus.LandBank, asset.Status);
        Assert.Equal(1000m, asset.AreaSquareMeters);
        Assert.False(asset.BoundaryVerified);
        Assert.False(asset.IsReadyForProjectManagement);
        Assert.False(asset.IsPublishedToExternalPortal);
        Assert.Contains("Rhema", asset.OwnershipHistoryJson);
    }

    [Fact]
    public async Task ValidApartmentImportsIntoPropertyRegisterAsAvailable()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserName).Returns("estate-tester");
        var controller = new EstateManagedAssetImportsController(db, currentUser.Object);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Properties");
        var headers = new[] { "assetCode", "recordType", "name", "status", "location" };
        for (var index = 0; index < headers.Length; index++) sheet.Cell(1, index + 1).Value = headers[index];
        sheet.Cell(2, 1).Value = "APT-001";
        sheet.Cell(2, 2).Value = "Apartment";
        sheet.Cell(2, 3).Value = "First apartment";
        sheet.Cell(2, 4).Value = "Available";
        sheet.Cell(2, 5).Value = "Accra";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var file = new FormFile(stream, 0, stream.Length, "file", "properties.xlsx");
        var mapping = JsonSerializer.Serialize(headers.ToDictionary(key => key, key => key));

        var result = Assert.IsType<OkObjectResult>(await controller.Commit(file, mapping));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(1, json.RootElement.GetProperty("importedCount").GetInt32());
        var asset = Assert.Single(await db.EstateManagedAssets.ToListAsync());
        Assert.Equal("Apartment", asset.UnitType);
        Assert.Equal(ErpSystem.Core.Enums.EstateManagedAssetSourceType.Imported, asset.SourceType);
        Assert.Equal(ErpSystem.Core.Enums.EstateManagedAssetStatus.Available, asset.Status);
        Assert.False(asset.IsPublishedToExternalPortal);
    }

    [Fact]
    public async Task InvalidRowRejectsEntireWorkbookWithoutSavingAnyAssets()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserName).Returns("estate-tester");
        var controller = new EstateManagedAssetImportsController(db, currentUser.Object);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Properties");
        var headers = new[] { "assetCode", "recordType", "name", "status", "location" };
        for (var index = 0; index < headers.Length; index++) sheet.Cell(1, index + 1).Value = headers[index];
        sheet.Cell(2, 1).Value = "APT-001";
        sheet.Cell(2, 2).Value = "Apartment";
        sheet.Cell(2, 3).Value = "First apartment";
        sheet.Cell(2, 4).Value = "Available";
        sheet.Cell(2, 5).Value = "Accra";
        sheet.Cell(3, 1).Value = "OFF-002";
        sheet.Cell(3, 2).Value = "Office";
        sheet.Cell(3, 4).Value = "Available";
        sheet.Cell(3, 5).Value = "Accra";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var file = new FormFile(stream, 0, stream.Length, "file", "properties.xlsx");
        var mapping = JsonSerializer.Serialize(headers.ToDictionary(key => key, key => key));

        var preview = Assert.IsType<OkObjectResult>(await controller.Preview(file, mapping));
        using var previewJson = JsonDocument.Parse(JsonSerializer.Serialize(preview.Value));
        Assert.False(previewJson.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("Row 3", previewJson.RootElement.GetProperty("errors")[0].GetString());

        Assert.IsType<BadRequestObjectResult>(await controller.Commit(file, mapping));
        Assert.Empty(await db.EstateManagedAssets.ToListAsync());
    }
}
