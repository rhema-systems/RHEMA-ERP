using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Estate;
using FluentAssertions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class EstateTerminationReleaseTests
{
    [Theory]
    [InlineData(EstateManagedAssetStatus.Leased)]
    [InlineData(EstateManagedAssetStatus.Occupied)]
    public async Task ActiveOccupantCannotBeReleasedWithoutCompletedLegalCase(EstateManagedAssetStatus status)
    {
        var fixture = Fixture(status);
        var request = ReleaseRequest();

        var action = () => fixture.Service.UpdateOccupancyAsync(fixture.Asset.Id, request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*completed Legal termination case*");
        fixture.Asset.AutoGenerateRentInvoices.Should().BeTrue();
    }

    [Fact]
    public async Task LegalCaseForAnotherOccupantCannotReleaseAndStopBilling()
    {
        var fixture = Fixture(EstateManagedAssetStatus.Occupied);
        var legalCase = LegalCase(fixture.TenantId);
        fixture.LegalCases.Setup(item => item.FirstOrDefaultAsync(It.IsAny<Expression<Func<ProcedureCase, bool>>>()))
            .ReturnsAsync(legalCase);
        fixture.LegalFields.Setup(item => item.FindAsync(It.IsAny<Expression<Func<ProcedureCaseField, bool>>>()))
            .ReturnsAsync([
                LegalField(fixture.TenantId, legalCase.Id, "estateManagedAssetId", fixture.Asset.Id.ToString()),
                LegalField(fixture.TenantId, legalCase.Id, "customerReference", Guid.NewGuid().ToString())
            ]);

        var action = () => fixture.Service.UpdateOccupancyAsync(fixture.Asset.Id,
            ReleaseRequest(legalCase.Id));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*current occupant*");
        fixture.Asset.Status.Should().Be(EstateManagedAssetStatus.Occupied);
    }

    [Fact]
    public async Task CompletedMatchingLegalCaseReleasesOccupantAndStopsRentBilling()
    {
        var fixture = Fixture(EstateManagedAssetStatus.Occupied);
        var legalCase = LegalCase(fixture.TenantId);
        fixture.LegalCases.Setup(item => item.FirstOrDefaultAsync(It.IsAny<Expression<Func<ProcedureCase, bool>>>()))
            .ReturnsAsync(legalCase);
        fixture.LegalFields.Setup(item => item.FindAsync(It.IsAny<Expression<Func<ProcedureCaseField, bool>>>()))
            .ReturnsAsync([
                LegalField(fixture.TenantId, legalCase.Id, "estateManagedAssetId", fixture.Asset.Id.ToString()),
                LegalField(fixture.TenantId, legalCase.Id, "customerReference", fixture.Asset.CustomerBusinessPartnerId!.Value.ToString())
            ]);
        var groundRentAccounts = new Mock<IGenericRepository<EstateGroundRentAccount>>();
        groundRentAccounts.Setup(item => item.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<EstateGroundRentAccount, bool>>>()))
            .ReturnsAsync((EstateGroundRentAccount?)null);
        fixture.Unit.Setup(item => item.Repository<EstateGroundRentAccount>())
            .Returns(groundRentAccounts.Object);

        await fixture.Service.UpdateOccupancyAsync(fixture.Asset.Id, ReleaseRequest(legalCase.Id));

        fixture.Asset.Status.Should().Be(EstateManagedAssetStatus.Available);
        fixture.Asset.CustomerBusinessPartnerId.Should().BeNull();
        fixture.Asset.AutoGenerateRentInvoices.Should().BeFalse();
        fixture.Asset.NextRentBillingDate.Should().BeNull();
        fixture.Asset.Notes.Should().Contain(legalCase.Id.ToString());
    }

    [Theory]
    [InlineData(EstateManagedAssetStatus.Reserved)]
    [InlineData(EstateManagedAssetStatus.Blocked)]
    [InlineData(EstateManagedAssetStatus.Retired)]
    public async Task IneligibleStatusPausesFutureBillingAndStaffDuties(EstateManagedAssetStatus nextStatus)
    {
        var fixture = Fixture(EstateManagedAssetStatus.Available);
        fixture.Asset.IsPublishedToExternalPortal = true;
        fixture.Asset.ExternalListingStatus = "Published";
        fixture.Asset.RentBillingActivatedAt = DateTime.UtcNow.AddMonths(-1);
        var account = new EstateGroundRentAccount
        {
            TenantId = fixture.TenantId, EstateManagedAssetId = fixture.Asset.Id,
            Status = "Active"
        };
        var roster = new EstateFacilityDutyRoster
        {
            TenantId = fixture.TenantId, PropertyReference = fixture.Asset.AssetCode,
            PropertyUnit = "CHILD-UNIT", StartDate = DateTime.UtcNow.Date,
            CompletionStatus = "Scheduled"
        };
        var completedRoster = new EstateFacilityDutyRoster
        {
            TenantId = fixture.TenantId, PropertyReference = fixture.Asset.AssetCode,
            StartDate = DateTime.UtcNow.Date.AddDays(-5), CompletionStatus = "Completed"
        };
        var accounts = new Mock<IGenericRepository<EstateGroundRentAccount>>();
        accounts.Setup(item => item.FindAsync(It.IsAny<Expression<Func<EstateGroundRentAccount, bool>>>() ))
            .ReturnsAsync([account]);
        accounts.Setup(item => item.UpdateAsync(It.IsAny<EstateGroundRentAccount>()))
            .Returns(Task.CompletedTask);
        var rosters = new Mock<IGenericRepository<EstateFacilityDutyRoster>>();
        rosters.Setup(item => item.FindAsync(It.IsAny<Expression<Func<EstateFacilityDutyRoster, bool>>>() ))
            .ReturnsAsync((Expression<Func<EstateFacilityDutyRoster, bool>> predicate) =>
                new[] { roster, completedRoster }.Where(predicate.Compile()).ToList());
        rosters.Setup(item => item.UpdateAsync(It.IsAny<EstateFacilityDutyRoster>()))
            .Returns(Task.CompletedTask);
        fixture.Unit.Setup(item => item.Repository<EstateGroundRentAccount>()).Returns(accounts.Object);
        fixture.Unit.Setup(item => item.Repository<EstateFacilityDutyRoster>()).Returns(rosters.Object);

        await fixture.Service.UpdateOccupancyAsync(fixture.Asset.Id,
            new UpdateEstateManagedAssetOccupancyDto { Status = nextStatus });

        fixture.Asset.AutoGenerateRentInvoices.Should().BeFalse();
        fixture.Asset.NextRentBillingDate.Should().BeNull();
        fixture.Asset.RentBillingActivatedAt.Should().NotBeNull();
        fixture.Asset.IsPublishedToExternalPortal.Should().BeFalse();
        account.Status.Should().Be("Held");
        roster.CompletionStatus.Should().Be("Cancelled");
        roster.EndDate.Should().BeBefore(DateTime.UtcNow.Date);
        completedRoster.CompletionStatus.Should().Be("Completed");
        completedRoster.EndDate.Should().BeNull();
    }

    private static (EstateManagedAssetService Service, EstateManagedAsset Asset,
        Guid TenantId, Mock<IUnitOfWork> Unit,
        Mock<IGenericRepository<ProcedureCase>> LegalCases,
        Mock<IGenericRepository<ProcedureCaseField>> LegalFields) Fixture(EstateManagedAssetStatus status)
    {
        var tenantId = Guid.NewGuid();
        var asset = new EstateManagedAsset
        {
            TenantId = tenantId, Status = status, AssetCode = "UNIT-1", Name = "Unit 1",
            CustomerBusinessPartnerId = Guid.NewGuid(), LesseeName = "Current tenant",
            DateOfTenancy = DateTime.UtcNow.AddMonths(-2),
            PropertyFileReference = "AGR-1", AutoGenerateRentInvoices = true,
            NextRentBillingDate = DateTime.UtcNow.AddDays(5)
        };
        var assetRepo = new Mock<IGenericRepository<EstateManagedAsset>>();
        assetRepo.Setup(item => item.FirstOrDefaultAsync(It.IsAny<Expression<Func<EstateManagedAsset, bool>>>()))
            .ReturnsAsync(asset);
        assetRepo.Setup(item => item.UpdateAsync(It.IsAny<EstateManagedAsset>())).Returns(Task.CompletedTask);
        var legalCases = new Mock<IGenericRepository<ProcedureCase>>();
        var legalFields = new Mock<IGenericRepository<ProcedureCaseField>>();
        var unit = new Mock<IUnitOfWork>();
        unit.Setup(item => item.Repository<EstateManagedAsset>()).Returns(assetRepo.Object);
        unit.Setup(item => item.Repository<ProcedureCase>()).Returns(legalCases.Object);
        unit.Setup(item => item.Repository<ProcedureCaseField>()).Returns(legalFields.Object);
        unit.Setup(item => item.ExecuteInStrategyAsync(
                It.IsAny<Func<Task<EstateManagedAssetDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<EstateManagedAssetDto>> action, CancellationToken _) => action());
        unit.Setup(item => item.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        unit.Setup(item => item.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        unit.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var user = new Mock<ICurrentUserProvider>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        user.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
        user.SetupGet(item => item.Username).Returns("estate.test");
        return (new EstateManagedAssetService(unit.Object, user.Object), asset,
            tenantId, unit, legalCases, legalFields);
    }

    private static UpdateEstateManagedAssetOccupancyDto ReleaseRequest(Guid? legalCaseId = null) => new()
    {
        Status = EstateManagedAssetStatus.Available,
        ActualDate = DateTime.UtcNow.Date,
        ReleaseOccupant = true,
        TerminationCaseId = legalCaseId
    };

    private static ProcedureCase LegalCase(Guid tenantId) => new()
    {
        TenantId = tenantId, Module = "Legal", EntityType = "LegalTerminationRecognition",
        Title = "Termination", Status = "Completed", CompletedAt = DateTime.UtcNow
    };

    private static ProcedureCaseField LegalField(Guid tenantId, Guid caseId, string key, string value) => new()
    {
        TenantId = tenantId, ProcedureCaseId = caseId, Key = key, Label = key, Value = value
    };
}
