using ErpSystem.Api.Services.HR;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.HR;

public sealed class PayrollFinanceAccountProvisioningTests
{
    [Fact]
    public async Task FinanceProvisioning_PayrollIntentsAreCanonicalMappedAndFailClosed()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"payroll-finance-boundary-{Guid.NewGuid():N}").Options);
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "PAY", Name = "Payroll", Status = TenantStatus.Active });
        await db.SaveChangesAsync();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserName).Returns("payroll.tests");
        var service = new FinanceAccountProvisioningService(
            db, currentUser.Object, NullLogger<FinanceAccountProvisioningService>.Instance);
        var intents = new[]
        {
            ("1010", "000-1010-0000", AccountType.Asset, "CASH"),
            ("1120", "000-1120-0000", AccountType.Asset, "RECEIVABLE_CONTROL"),
            ("2120", "000-2120-0000", AccountType.Liability, "PAYABLE_CONTROL"),
            ("4920", "000-4920-0000", AccountType.Revenue, "OTHER_INCOME"),
            ("6020", "000-6020-0000", AccountType.Expense, "EXPENSE")
        };

        foreach (var intent in intents)
        {
            var request = new ProvisionFinanceAccountDto
            {
                TenantId = tenantId, AccountCode = intent.Item1, AccountNumber = intent.Item2,
                AccountName = $"Payroll {intent.Item1}", CoreAccountType = intent.Item3, CurrencyCode = "GHS"
            };
            var created = await service.ProvisionAsync(request);
            var repeated = await service.ProvisionAsync(request);
            created.WasCreated.Should().BeTrue();
            repeated.WasCreated.Should().BeFalse();
            repeated.AccountId.Should().Be(created.AccountId);
            repeated.ClassificationCode.Should().Be(intent.Item4);
            repeated.AccountingBookCodes.Should().BeEquivalentTo("IFRS", "LOCAL_STATUTORY", "MANAGEMENT");
        }

        var accounts = await db.Accounts.Include(item => item.SegmentValues).ToListAsync();
        accounts.Should().HaveCount(5);
        accounts.Should().OnlyContain(item => item.IsSegmented && item.SegmentValues.Count(value => !value.IsDeleted) == 2);
        (await db.AccountAccountingBooks.CountAsync()).Should().Be(15);
        await service.Invoking(item => item.ProvisionAsync(new ProvisionFinanceAccountDto
        {
            TenantId = tenantId, AccountCode = "2120", AccountName = "Wrong type",
            CoreAccountType = AccountType.Asset, CurrencyCode = "GHS"
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*different core account type*");
        await service.Invoking(item => item.ProvisionAsync(new ProvisionFinanceAccountDto
        {
            TenantId = Guid.NewGuid(), AccountCode = "1010", AccountName = "Wrong tenant",
            CoreAccountType = AccountType.Asset, CurrencyCode = "GHS"
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*tenant context is invalid*");
        (await db.Accounts.CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task SeedOracleJournalMappings_UsesStableFinanceProvisioningIntentsOnEveryRepeat()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"payroll-provisioning-{Guid.NewGuid():N}").Options);
        var provisioner = new Mock<IFinanceAccountProvisioningService>(MockBehavior.Strict);
        var requests = new List<ProvisionFinanceAccountDto>();
        provisioner.Setup(item => item.ProvisionAsync(It.IsAny<ProvisionFinanceAccountDto>(), It.IsAny<CancellationToken>()))
            .Callback<ProvisionFinanceAccountDto, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(new ProvisionedFinanceAccountDto());
        var service = new PayrollService(
            db,
            new Mock<IJournalEntryService>().Object,
            provisioner.Object,
            new Mock<IWorkflowIntegrationService>().Object,
            new Mock<INotificationTopicPublisher>().Object,
            NullLogger<PayrollService>.Instance);

        await service.SeedOracleJournalMappingsAsync(tenantId);
        await service.SeedOracleJournalMappingsAsync(tenantId);

        requests.Should().HaveCount(10);
        var expected = new[]
        {
            ("1010", "000-1010-0000"), ("1120", "000-1120-0000"), ("2120", "000-2120-0000"),
            ("4920", "000-4920-0000"), ("6020", "000-6020-0000")
        };
        requests.Select(item => (item.AccountCode, item.AccountNumber)).Should().BeEquivalentTo(expected.Concat(expected));
        requests.Should().OnlyContain(item => item.TenantId == tenantId && item.CurrencyCode == "GHS" && item.IsSegmented);
        (await db.PayrollJournalMappings.CountAsync()).Should().BeGreaterThan(0);
        provisioner.Verify(item => item.ProvisionAsync(It.IsAny<ProvisionFinanceAccountDto>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    [Fact]
    public void PayrollService_HasNoDirectFinanceAccountWriterOrDisplayClassificationInference()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ErpSystem.Api", "Services", "HR", "PayrollService.cs");
        var source = File.ReadAllText(Path.GetFullPath(sourcePath));

        source.Should().Contain("IFinanceAccountProvisioningService");
        source.Should().NotContain("_context.Accounts.Add(");
        source.Should().NotContain("AccountCategory = seed.");
        source.Should().NotContain("AccountSubCategory = seed.");
    }
}
