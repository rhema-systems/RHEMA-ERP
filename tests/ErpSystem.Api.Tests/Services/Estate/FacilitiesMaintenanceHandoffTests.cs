using ErpSystem.Api.Services;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Estate;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class FacilitiesMaintenanceHandoffTests
{
    [Fact]
    public void CatalogExposesSupervisorJobCardFields()
    {
        var workspace = new FacilitiesProcedureCatalogService()
            .GetProcedureWorkspace("EstateFacilityMaintenance");

        workspace.Should().NotBeNull();
        workspace!.IntakeFields.Select(field => field.Key).Should().Contain([
            "estateManagedAssetId", "priority", "maintenanceTypeId", "handoffDescription",
            "estimatedHours", "estimatedCost"
        ]);
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    public void ValidHandoffFieldsAreAccepted(string priority)
    {
        var procedureCase = Case(priority);

        var action = () => ProcedureCaseService.EnsureFacilitiesMaintenanceHandoffFields(procedureCase);

        action.Should().NotThrow();
    }

    [Fact]
    public void MissingEstimateBlocksHandoff()
    {
        var procedureCase = Case("High");
        procedureCase.Fields.First(field => field.Key == "estimatedHours").Value = "";

        var action = () => ProcedureCaseService.EnsureFacilitiesMaintenanceHandoffFields(procedureCase);

        action.Should().Throw<InvalidOperationException>().WithMessage("*estimated hours*");
    }

    [Fact]
    public void CustomerUrgencyCannotSubstituteForFacilitiesPriority()
    {
        var procedureCase = Case("");
        procedureCase.Fields.Add(Field("customerReportedUrgency", "Urgent"));

        var action = () => ProcedureCaseService.EnsureFacilitiesMaintenanceHandoffFields(procedureCase);

        action.Should().Throw<InvalidOperationException>().WithMessage("*priority*");
    }

    [Fact]
    public async Task MaintenanceIntakeRequiresSelectedEstateProperty()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);

        var action = () => ProcedureCaseService.EnsureFacilitiesMaintenancePropertyForIntakeAsync(
            db, tenantId, new Dictionary<string, string?> { ["propertyUnit"] = "ASR-001" });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Select a property from the Estate property register*");
    }

    [Fact]
    public async Task MaintenanceIntakeAcceptsOnlyMatchingEstateProperty()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var property = new EstateManagedAsset
        {
            TenantId = tenantId, AssetCode = "ASR-005", ProjectUnitCode = "UNIT-5", Name = "Apartment 5"
        };
        db.EstateManagedAssets.Add(property);
        await db.SaveChangesAsync();

        var fields = new Dictionary<string, string?>
        {
            ["propertyUnit"] = "DIFFERENT-UNIT",
            ["estateManagedAssetId"] = property.Id.ToString()
        };
        var mismatched = () => ProcedureCaseService.EnsureFacilitiesMaintenancePropertyForIntakeAsync(
            db, tenantId, fields);
        await mismatched.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not match*");

        fields["propertyUnit"] = property.ProjectUnitCode;
        var matching = () => ProcedureCaseService.EnsureFacilitiesMaintenancePropertyForIntakeAsync(
            db, tenantId, fields);
        await matching.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SelectedEstatePropertyCreatesAndReusesMaintenanceAsset()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var property = new EstateManagedAsset
        {
            TenantId = tenantId, AssetCode = "ASR-001", ProjectUnitCode = "UNIT-1",
            Name = "Apartment 1", Location = "East Wing"
        };
        db.EstateManagedAssets.Add(property);
        await db.SaveChangesAsync();

        var first = await ProcedureCaseService.EnsureFacilitiesMaintenanceAssetAsync(
            db, tenantId, "UNIT-1", property.Id.ToString(), "Facilities");
        var second = await ProcedureCaseService.EnsureFacilitiesMaintenanceAssetAsync(
            db, tenantId, "UNIT-1", property.Id.ToString(), "Facilities");

        second.Id.Should().Be(first.Id);
        first.AssetNumber.Should().Be($"EST-{property.Id:N}");
        first.Name.Should().Be("ASR-001 - Apartment 1");
        first.Location.Should().Be("East Wing");
        first.AssetCategory.Code.Should().Be("EST-PROP");
        (await db.MaintenanceAssets.CountAsync()).Should().Be(1);
        (await db.MaintenanceAssetCategories.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task InactiveLinkedMaintenanceAssetBlocksHandoff()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var property = new EstateManagedAsset
        {
            TenantId = tenantId, AssetCode = "ASR-002", Name = "Apartment 2"
        };
        db.EstateManagedAssets.Add(property);
        await db.SaveChangesAsync();
        var asset = await ProcedureCaseService.EnsureFacilitiesMaintenanceAssetAsync(
            db, tenantId, property.AssetCode, property.Id.ToString(), "Facilities");
        asset.Status = AssetStatus.Inactive;
        await db.SaveChangesAsync();

        var action = () => ProcedureCaseService.EnsureFacilitiesMaintenanceAssetAsync(
            db, tenantId, property.AssetCode, property.Id.ToString(), "Facilities");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive*");
        (await db.MaintenanceAssets.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ConflictingMaintenanceReferenceDoesNotCreateAnotherAsset()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var property = new EstateManagedAsset
        {
            TenantId = tenantId, AssetCode = "ASR-003", Name = "Apartment 3"
        };
        db.EstateManagedAssets.Add(property);
        db.MaintenanceAssetCategories.Add(new MaintenanceAssetCategory
        {
            TenantId = tenantId, Name = "Buildings", Code = "BLD"
        });
        await db.SaveChangesAsync();
        var categoryId = await db.MaintenanceAssetCategories.Select(item => item.Id).SingleAsync();
        db.MaintenanceAssets.Add(new MaintenanceAsset
        {
            TenantId = tenantId, AssetNumber = property.AssetCode,
            Name = "Unrelated building", AssetCategoryId = categoryId
        });
        await db.SaveChangesAsync();

        var action = () => ProcedureCaseService.EnsureFacilitiesMaintenanceAssetAsync(
            db, tenantId, property.AssetCode, property.Id.ToString(), "Facilities");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*different Maintenance asset*");
        (await db.MaintenanceAssets.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SelectedPropertyIdentityMustMatchCaseReference()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var property = new EstateManagedAsset
        {
            TenantId = tenantId, AssetCode = "ASR-004", Name = "Apartment 4"
        };
        db.EstateManagedAssets.Add(property);
        await db.SaveChangesAsync();

        var action = () => ProcedureCaseService.EnsureFacilitiesMaintenanceAssetAsync(
            db, tenantId, "DIFFERENT-UNIT", property.Id.ToString(), "Facilities");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no longer matches*");
        (await db.MaintenanceAssets.CountAsync()).Should().Be(0);
    }

    private static ProcedureCase Case(string priority) => new()
    {
        Fields =
        [
            Field("propertyUnit", "ASR-001"),
            Field("issueDescription", "Water leaking into the kitchen"),
            Field("handoffDescription", "Repair kitchen supply pipe and test"),
            Field("maintenanceTypeId", Guid.NewGuid().ToString()),
            Field("priority", priority),
            Field("estimatedHours", "3.5"),
            Field("estimatedCost", "400")
        ]
    };

    private static ProcedureCaseField Field(string key, string value) => new()
    {
        Key = key,
        Label = key,
        Value = value
    };
}
