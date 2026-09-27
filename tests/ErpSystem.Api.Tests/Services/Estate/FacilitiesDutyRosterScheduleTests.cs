using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class FacilitiesDutyRosterScheduleTests
{
    [Fact]
    public async Task SiteOfficerAssignment_UsesActiveHrEmployee()
    {
        var tenantId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantId);
        db.EstateManagedAssets.Add(new EstateManagedAsset
        {
            Id = assetId, TenantId = tenantId, AssetCode = "FAC-SITE", Name = "Site"
        });
        db.Employees.Add(new Employee
        {
            Id = employeeId, TenantId = tenantId, EmployeeNumber = "EMP-001",
            FirstName = "Ama", LastName = "Mensah", IsActive = true
        });
        await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        var controller = new FacilitiesSiteOfficersController(db, user.Object);

        var result = await controller.AssignResponsibleOfficer(
            assetId, new AssignFacilitiesSiteOfficerRequest(employeeId), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var asset = await db.EstateManagedAssets.SingleAsync();
        asset.ResponsibleOfficerEmployeeId.Should().Be(employeeId);
        asset.ResponsibleOfficerEmployeeNumber.Should().Be("EMP-001");
        asset.ResponsibleOfficerName.Should().Be("Ama Mensah");
    }

    [Fact]
    public async Task CleanerDuty_UsesIssuedInventoryVoucherSummary()
    {
        var tenantId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantId);
        var inventoryItem = new InventoryItem
        {
            Id = itemId, TenantId = tenantId, ItemCode = "SOAP", Name = "Liquid soap"
        };
        db.Employees.Add(new Employee
        {
            Id = Guid.NewGuid(), TenantId = tenantId, EmployeeNumber = "CL-001",
            FirstName = "Ama", LastName = "Cleaner", IsActive = true
        });
        db.EstateManagedAssets.Add(new EstateManagedAsset
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "SITE-1", Name = "Site One"
        });
        db.InventoryItems.Add(inventoryItem);
        db.InventoryIssueVouchers.Add(new InventoryIssueVoucher
        {
            Id = voucherId, TenantId = tenantId, VoucherNumber = "ISS-001",
            Status = InventoryIssueVoucherStatus.Issued,
            Lines = new List<InventoryIssueVoucherLine>
            {
                new()
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = itemId,
                    InventoryItem = inventoryItem, Quantity = 2m, UnitOfMeasure = "L"
                }
            }
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, tenantId);

        var result = await controller.CreateRosterItem(new UpsertEstateFacilityDutyRosterDto
        {
            EmployeeNumber = "CL-001", StaffName = "Cleaner", PropertyReference = "SITE-1",
            ServiceAreaName = "Lobby", StartDate = DateTime.UtcNow.Date,
            InventoryIssueVoucherId = voucherId, SuppliesIssued = "User-entered value"
        }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var duty = await db.EstateFacilityDutyRosters.SingleAsync();
        duty.InventoryIssueVoucherNumber.Should().Be("ISS-001");
        duty.SuppliesIssued.Should().Be("Liquid soap x2 L");
    }

    [Theory]
    [InlineData("Mon-Fri", "2026-09-23", true)]
    [InlineData("Mon-Fri", "2026-09-26", false)]
    [InlineData("Sat", "2026-09-26", true)]
    [InlineData("Mon, Wed, Fri", "2026-09-24", false)]
    public void DayPattern_SelectsOnlyScheduledDays(string pattern, string date, bool expected)
    {
        var item = new EstateFacilityDutyRoster
        {
            Frequency = "Daily",
            DayPattern = pattern,
            StartDate = new DateTime(2026, 9, 1)
        };

        FacilitiesDutyRosterController.IsScheduledOn(item, DateTime.Parse(date)).Should().Be(expected);
    }

    [Fact]
    public void OneOffDuty_DoesNotRepeat()
    {
        var item = new EstateFacilityDutyRoster
        {
            Frequency = "One-off",
            StartDate = new DateTime(2026, 9, 23)
        };

        FacilitiesDutyRosterController.IsScheduledOn(item, new DateTime(2026, 9, 23)).Should().BeTrue();
        FacilitiesDutyRosterController.IsScheduledOn(item, new DateTime(2026, 9, 24)).Should().BeFalse();
    }

    [Fact]
    public void ShiftEnd_MustBeAfterStart()
    {
        var request = new UpsertEstateFacilityDutyRosterDto
        {
            EmployeeNumber = "CL-001", StaffName = "Cleaner", PropertyReference = "SITE-1",
            ServiceAreaName = "Block A",
            Frequency = "Daily",
            DayPattern = "Mon-Fri",
            ShiftStart = "17:00",
            ShiftEnd = "08:00"
        };

        FacilitiesDutyRosterController.ValidateRequest(request).Should().Contain("end time after");
    }

    [Fact]
    public void CleanerDuty_RequiresHrSelection()
    {
        var request = new UpsertEstateFacilityDutyRosterDto
        {
            StaffName = "Typed cleaner", ServiceAreaName = "Lobby"
        };

        FacilitiesDutyRosterController.ValidateRequest(request).Should().Be("Select a cleaner from HR.");

        request.EmployeeNumber = "CL-001";
        FacilitiesDutyRosterController.ValidateRequest(request).Should().Be("Select a property or site from Estate.");
    }

    [Fact]
    public async Task AttendanceHistory_RemainsVisibleForItsOwnDate()
    {
        var tenantId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantId);
        var roster = new EstateFacilityDutyRoster
        {
            Id = Guid.NewGuid(), TenantId = tenantId, RosterReference = "FAC-DR-001",
            StaffName = "Cleaner", ServiceAreaName = "Block A", Frequency = "Daily",
            StartDate = today.AddDays(-2)
        };
        db.EstateFacilityDutyRosters.Add(roster);
        db.EstateFacilityDutyAttendances.Add(new EstateFacilityDutyAttendance
        {
            Id = Guid.NewGuid(), TenantId = tenantId, DutyRosterId = roster.Id,
            DutyDate = today.AddDays(-1), AttendanceStatus = "Absent",
            CompletionStatus = "Missed", QualityStatus = "Not inspected",
            RecordedAt = today.AddDays(-1)
        });
        await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        user.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        var controller = new FacilitiesDutyRosterController(db, user.Object);

        await controller.UpdateAttendance(roster.Id, new UpdateEstateFacilityDutyAttendanceDto(), CancellationToken.None);
        (await db.EstateFacilityDutyAttendances.CountAsync()).Should().Be(2);

        var yesterday = await controller.GetRoster(today.AddDays(-1), today.AddDays(-1), null, CancellationToken.None);
        var current = await controller.GetRoster(today, today, null, CancellationToken.None);

        RosterItem(yesterday).AttendanceStatus.Should().Be("Absent");
        RosterItem(current).AttendanceStatus.Should().Be("Present");
        RosterItem(current).CompletionStatus.Should().Be("Completed");
    }

    [Fact]
    public async Task StaffLookup_ReturnsOnlyActiveEmployeesFromCurrentTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantId);
        db.Employees.AddRange(
            new Employee { Id = Guid.NewGuid(), TenantId = tenantId, EmployeeNumber = "CL-001", FirstName = "Ama", LastName = "Cleaner", IsActive = true },
            new Employee { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), EmployeeNumber = "CL-002", FirstName = "Ama", LastName = "Elsewhere", IsActive = true },
            new Employee { Id = Guid.NewGuid(), TenantId = tenantId, EmployeeNumber = "CL-003", FirstName = "Ama", LastName = "Inactive", IsActive = false });
        await db.SaveChangesAsync();
        var controller = CreateController(db, tenantId);

        var result = await controller.SearchStaff("Ama", CancellationToken.None);

        var staff = Data(result);
        staff.Should().ContainSingle();
        staff.Single().GetType().GetProperty("EmployeeNumber")!.GetValue(staff.Single()).Should().Be("CL-001");
    }

    [Fact]
    public async Task UnitLookup_OnlyReturnsUnitsFromSelectedPropertyProjectAndTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantId);
        var propertyId = Guid.NewGuid();
        db.EstateManagedAssets.AddRange(
            new EstateManagedAsset { Id = propertyId, TenantId = tenantId, AssetCode = "SITE-1", Name = "Site One", ProjectCode = "PROJECT-A" },
            new EstateManagedAsset { Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "UNIT-1", Name = "Apartment One", ProjectCode = "PROJECT-A" },
            new EstateManagedAsset { Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "UNIT-2", Name = "Apartment Two", ProjectCode = "PROJECT-B" },
            new EstateManagedAsset { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), AssetCode = "UNIT-3", Name = "Other Tenant", ProjectCode = "PROJECT-A" });
        await db.SaveChangesAsync();
        var controller = CreateController(db, tenantId);

        var result = await controller.SearchUnits(propertyId, "Apartment", CancellationToken.None);

        var units = Data(result);
        units.Should().ContainSingle();
        units.Single().GetType().GetProperty("AssetCode")!.GetValue(units.Single()).Should().Be("UNIT-1");
    }

    [Fact]
    public async Task SavingDuty_UsesHrNameAndRejectsUnitFromAnotherProject()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantId);
        db.Employees.Add(new Employee
        {
            Id = Guid.NewGuid(), TenantId = tenantId, EmployeeNumber = "CL-001",
            FirstName = "Ama", LastName = "Cleaner", IsActive = true
        });
        db.EstateManagedAssets.AddRange(
            new EstateManagedAsset { Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "SITE-1", Name = "Site One", ProjectCode = "PROJECT-A" },
            new EstateManagedAsset { Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "UNIT-2", Name = "Other Site", ProjectCode = "PROJECT-B" });
        await db.SaveChangesAsync();
        var controller = CreateController(db, tenantId);
        var request = new UpsertEstateFacilityDutyRosterDto
        {
            EmployeeNumber = "CL-001", StaffName = "Wrong Name", PropertyReference = "SITE-1",
            PropertyUnit = "UNIT-2", ServiceAreaName = "Common area"
        };

        var rejected = await controller.CreateRosterItem(request, CancellationToken.None);
        rejected.Should().BeOfType<BadRequestObjectResult>();
        (await db.EstateFacilityDutyRosters.CountAsync()).Should().Be(0);

        request.PropertyUnit = null;
        var accepted = await controller.CreateRosterItem(request, CancellationToken.None);
        accepted.Should().BeOfType<OkObjectResult>();
        (await db.EstateFacilityDutyRosters.SingleAsync()).StaffName.Should().Be("Ama Cleaner");
    }

    private static FacilitiesDutyRosterController CreateController(ApplicationDbContext db, Guid tenantId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        return new FacilitiesDutyRosterController(db, user.Object);
    }

    private static object[] Data(IActionResult result)
    {
        var payload = ((OkObjectResult)result).Value!;
        return ((IEnumerable<object>)payload.GetType().GetProperty("data")!.GetValue(payload)!).ToArray();
    }

    private static EstateFacilityDutyRosterDto RosterItem(IActionResult result)
    {
        var payload = ((OkObjectResult)result).Value!;
        return ((IEnumerable<EstateFacilityDutyRosterDto>)payload.GetType()
            .GetProperty("data")!.GetValue(payload)!).Single();
    }
}
