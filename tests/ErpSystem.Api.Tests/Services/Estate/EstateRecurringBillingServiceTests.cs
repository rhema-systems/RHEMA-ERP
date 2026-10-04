using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class EstateRecurringBillingServiceTests
{
    [Fact]
    public async Task GroundRent_UsesPaymentTermsToCreateInvoiceBeforeDueDate()
    {
        var tenantId = Guid.NewGuid();
        var due = DateTime.UtcNow.Date.AddDays(20);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var account = new EstateGroundRentAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            EstateManagedAssetId = Guid.NewGuid(), CustomerBusinessPartnerId = Guid.NewGuid(),
            GroundRentIncomeAccountId = Guid.NewGuid(), NextDueDate = due,
            PaymentTermsDays = 30, Status = "Active"
        };
        db.EstateGroundRentAccounts.Add(account);
        await db.SaveChangesAsync();
        var groundRent = new Mock<IGroundRentAdministrationService>();
        groundRent.Setup(item => item.GenerateInvoiceAsync(account.Id,
                It.IsAny<GenerateEstateGroundRentInvoiceDto>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, GenerateEstateGroundRentInvoiceDto, CancellationToken>((_, request, _) =>
            {
                request.AllowFutureDueDate.Should().BeTrue();
                account.NextDueDate = due.AddYears(1);
                db.SaveChanges();
            })
            .ReturnsAsync((EstateGroundRentActionResultDto)null!);
        var lockService = new Mock<IDistributedLockService>();
        lockService.Setup(item => item.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lease());
        var runner = new EstateRecurringBillingService(db, Mock.Of<IInvoiceService>(),
            groundRent.Object, lockService.Object, NullLogger<EstateRecurringBillingService>.Instance);

        var result = await runner.RunForTenantAsync(tenantId, CancellationToken.None);

        result.GroundRentInvoices.Should().Be(1);
        result.Failures.Should().Be(0);
        groundRent.Verify(item => item.GenerateInvoiceAsync(account.Id,
            It.IsAny<GenerateEstateGroundRentInvoiceDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClosedGroundRentAccount_DoesNotGenerateAnotherInvoice()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        db.EstateGroundRentAccounts.Add(new EstateGroundRentAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            EstateManagedAssetId = Guid.NewGuid(), CustomerBusinessPartnerId = Guid.NewGuid(),
            GroundRentIncomeAccountId = Guid.NewGuid(), NextDueDate = DateTime.UtcNow.Date.AddDays(-1),
            Status = "Closed"
        });
        await db.SaveChangesAsync();
        var groundRent = new Mock<IGroundRentAdministrationService>();
        var lockService = new Mock<IDistributedLockService>();
        lockService.Setup(item => item.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lease());
        var runner = new EstateRecurringBillingService(db, Mock.Of<IInvoiceService>(),
            groundRent.Object, lockService.Object, NullLogger<EstateRecurringBillingService>.Instance);

        var result = await runner.RunForTenantAsync(tenantId, CancellationToken.None);

        result.GroundRentInvoices.Should().Be(0);
        groundRent.Verify(item => item.GenerateInvoiceAsync(It.IsAny<Guid>(),
            It.IsAny<GenerateEstateGroundRentInvoiceDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(EstateManagedAssetStatus.Reserved)]
    [InlineData(EstateManagedAssetStatus.Blocked)]
    [InlineData(EstateManagedAssetStatus.Retired)]
    public async Task IneligibleProperty_DoesNotGenerateGroundRentInvoice(EstateManagedAssetStatus status)
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var asset = new EstateManagedAsset
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "UNIT-1",
            Name = "Unit 1", Status = status
        };
        db.EstateManagedAssets.Add(asset);
        db.EstateGroundRentAccounts.Add(new EstateGroundRentAccount
        {
            TenantId = tenantId, EstateManagedAssetId = asset.Id,
            CustomerBusinessPartnerId = Guid.NewGuid(),
            GroundRentIncomeAccountId = Guid.NewGuid(),
            NextDueDate = DateTime.UtcNow.Date.AddDays(-1), Status = "Active"
        });
        await db.SaveChangesAsync();
        var groundRent = new Mock<IGroundRentAdministrationService>();
        var lockService = new Mock<IDistributedLockService>();
        lockService.Setup(item => item.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lease());
        var runner = new EstateRecurringBillingService(db, Mock.Of<IInvoiceService>(),
            groundRent.Object, lockService.Object, NullLogger<EstateRecurringBillingService>.Instance);

        var result = await runner.RunForTenantAsync(tenantId, CancellationToken.None);

        result.GroundRentInvoices.Should().Be(0);
        groundRent.Verify(item => item.GenerateInvoiceAsync(It.IsAny<Guid>(),
            It.IsAny<GenerateEstateGroundRentInvoiceDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DueMonthlyRent_CreatesFinanceInvoiceAndAdvancesScheduleOnce()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var due = DateTime.UtcNow.Date.AddDays(-1);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var asset = new EstateManagedAsset
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "APT-101", Name = "Apartment 101",
            AssetType = EstateManagedAssetType.Property, Status = EstateManagedAssetStatus.Occupied,
            ExternalListingType = "Rent", ExternalMonthlyRent = 2500m,
            CustomerBusinessPartnerId = customerId, PropertyFileReference = "LEASE-101",
            AutoGenerateRentInvoices = true, NextRentBillingDate = due, Currency = "GHS"
        };
        db.EstateManagedAssets.Add(asset);
        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "4110",
            AccountNumber = "4110", AccountName = "Rental Income",
            AccountType = AccountType.Revenue, Status = AccountStatus.Active,
            AllowDirectPosting = true
        });
        await db.SaveChangesAsync();

        var invoice = new InvoiceDto
        {
            Id = Guid.NewGuid(), InvoiceNumber = "AR-101", BusinessPartnerId = customerId,
            CurrencyCode = "GHS"
        };
        var invoices = new Mock<IInvoiceService>();
        InvoiceCreateDto? created = null;
        invoices.Setup(item => item.CreateAsync(It.IsAny<InvoiceCreateDto>(), It.IsAny<CancellationToken>()))
            .Callback<InvoiceCreateDto, CancellationToken>((request, _) => created = request)
            .ReturnsAsync(invoice);
        invoices.Setup(item => item.SendInvoiceAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        var lockService = new Mock<IDistributedLockService>();
        lockService.Setup(item => item.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lease());
        var runner = new EstateRecurringBillingService(db, invoices.Object,
            Mock.Of<IGroundRentAdministrationService>(), lockService.Object,
            NullLogger<EstateRecurringBillingService>.Instance);

        var result = await runner.RunForTenantAsync(tenantId, CancellationToken.None);
        var second = await runner.RunForTenantAsync(tenantId, CancellationToken.None);

        result.RentInvoices.Should().Be(1);
        result.Failures.Should().Be(0);
        second.RentInvoices.Should().Be(0);
        created.Should().NotBeNull();
        created!.Reference.Should().Be($"RENT-APT-101-{due:yyyyMM}");
        created.LineItems.Single().UnitPrice.Should().Be(2500m);
        asset.NextRentBillingDate.Should().Be(due.AddMonths(1));
        asset.LastRentInvoiceId.Should().Be(invoice.Id);
        invoices.Verify(item => item.CreateAsync(It.IsAny<InvoiceCreateDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FormerRentalProperty_DoesNotGenerateAnotherInvoice()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantId);
        db.EstateManagedAssets.Add(new EstateManagedAsset
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "APT-FORMER", Name = "Former rental",
            AssetType = EstateManagedAssetType.Property, Status = EstateManagedAssetStatus.Available,
            ExternalListingType = "Rent", ExternalMonthlyRent = 2500m,
            CustomerBusinessPartnerId = Guid.NewGuid(), PropertyFileReference = "OLD-AGREEMENT",
            AutoGenerateRentInvoices = true, NextRentBillingDate = DateTime.UtcNow.Date.AddDays(-1)
        });
        await db.SaveChangesAsync();
        var invoices = new Mock<IInvoiceService>();
        var lockService = new Mock<IDistributedLockService>();
        lockService.Setup(item => item.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lease());
        var runner = new EstateRecurringBillingService(db, invoices.Object,
            Mock.Of<IGroundRentAdministrationService>(), lockService.Object,
            NullLogger<EstateRecurringBillingService>.Instance);

        var result = await runner.RunForTenantAsync(tenantId, CancellationToken.None);

        result.RentInvoices.Should().Be(0);
        result.Failures.Should().Be(0);
        invoices.Verify(item => item.CreateAsync(It.IsAny<InvoiceCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GroundRent_RejectsAccountForFormerCustomer()
    {
        var tenantId = Guid.NewGuid();
        var asset = new EstateManagedAsset
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "LAND-2", Name = "Land 2",
            AssetType = EstateManagedAssetType.Land, Status = EstateManagedAssetStatus.Leased,
            CustomerBusinessPartnerId = Guid.NewGuid(), PropertyFileReference = "LEASE-2",
            DateOfTenancy = DateTime.UtcNow.Date.AddYears(-1)
        };
        var account = new EstateGroundRentAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, EstateManagedAssetId = asset.Id,
            CustomerBusinessPartnerId = Guid.NewGuid(), GroundRentIncomeAccountId = Guid.NewGuid(),
            NextDueDate = DateTime.UtcNow.Date, Status = "Active"
        };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        db.EstateManagedAssets.Add(asset);
        db.EstateGroundRentAccounts.Add(account);
        await db.SaveChangesAsync();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        var lockService = new Mock<IDistributedLockService>();
        lockService.Setup(item => item.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lease());
        var service = new GroundRentAdministrationService(db, Mock.Of<IInvoiceService>(),
            Mock.Of<IPaymentService>(), currentUser.Object, lockService.Object);

        var action = () => service.GenerateInvoiceAsync(account.Id,
            new GenerateEstateGroundRentInvoiceDto(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no longer matches the current property holder*");
    }

    private sealed class Lease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
