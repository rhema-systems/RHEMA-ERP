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
using ErpSystem.Shared;
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
            ("1010", "PAY-1010", AccountType.Asset, "CASH"),
            ("1120", "PAY-1120", AccountType.Asset, "RECEIVABLE_CONTROL"),
            ("2120", "PAY-2120", AccountType.Liability, "PAYABLE_CONTROL"),
            ("4920", "PAY-4920", AccountType.Revenue, "OTHER_INCOME"),
            ("6020", "PAY-6020", AccountType.Expense, "EXPENSE")
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
            repeated.AccountingBookCodes.Should().BeEmpty(
                "fresh manifest books are Configuring/non-posting and seeding cannot auto-enable their applicability");
        }

        var accounts = await db.Accounts.Include(item => item.SegmentValues).ToListAsync();
        accounts.Should().HaveCount(5);
        foreach (var intent in intents)
        {
            var account = accounts.Single(item => item.AccountCode == intent.Item1);
            account.AccountNumber.Should().Be(intent.Item2);
            account.IsSegmented.Should().BeTrue();
            account.SegmentValues.Where(value => !value.IsDeleted)
                .Select(value => (value.SegmentPosition, value.SegmentValue))
                .Should().BeEquivalentTo([(1, "PAY"), (2, intent.Item1)]);
        }

        var mappings = await db.AccountAccountingBooks
            .Include(item => item.Account)
            .Include(item => item.AccountingBook)
            .Include(item => item.AccountClassification)
            .ToListAsync();
        mappings.Should().HaveCount(15);
        mappings.Should().OnlyContain(item => !item.IsEnabled
            && item.TenantId == tenantId
            && item.Account.TenantId == tenantId
            && item.AccountingBook.TenantId == tenantId
            && !item.AccountingBook.IsActive
            && !item.AccountingBook.AllowsPosting
            && item.AccountClassification != null
            && item.AccountClassification.TenantId == tenantId
            && item.AccountClassification.AccountingBookId == item.AccountingBookId
            && item.AccountClassification.Status == AccountClassificationStatus.Active
            && item.AccountClassification.IsPostingClassification
            && item.AccountClassification.CoreAccountType == item.Account.AccountType);

        var beforeWrongType = await RelevantFinanceStateAsync(db, tenantId);
        await service.Invoking(item => item.ProvisionAsync(new ProvisionFinanceAccountDto
        {
            TenantId = tenantId, AccountCode = "2120", AccountName = "Wrong type",
            CoreAccountType = AccountType.Asset, CurrencyCode = "GHS"
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*different core account type*");
        (await RelevantFinanceStateAsync(db, tenantId)).Should().Be(beforeWrongType);

        var beforeCrossTenant = await RelevantFinanceStateAsync(db, tenantId);
        await service.Invoking(item => item.ProvisionAsync(new ProvisionFinanceAccountDto
        {
            TenantId = Guid.NewGuid(), AccountCode = "1010", AccountName = "Wrong tenant",
            CoreAccountType = AccountType.Asset, CurrencyCode = "GHS"
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*tenant context is invalid*");
        (await RelevantFinanceStateAsync(db, tenantId)).Should().Be(beforeCrossTenant);
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

    private static async Task<string> RelevantFinanceStateAsync(ApplicationDbContext db, Guid tenantId)
    {
        var accounts = await db.Accounts.AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Id).Select(item => $"{item.Id}|{item.AccountCode}|{item.AccountNumber}|{item.AccountType}|{item.IsDeleted}").ToListAsync();
        var segments = await db.AccountSegmentValues.AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Id).Select(item => $"{item.Id}|{item.AccountId}|{item.SegmentStructureId}|{item.SegmentPosition}|{item.SegmentValue}|{item.SegmentLookupValueId}|{item.IsDeleted}").ToListAsync();
        var mappings = await db.AccountAccountingBooks.AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Id).Select(item => $"{item.Id}|{item.AccountId}|{item.AccountingBookId}|{item.AccountClassificationId}|{item.IsEnabled}|{item.IsDeleted}").ToListAsync();
        var classifications = await db.AccountClassifications.AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Id).Select(item => $"{item.Id}|{item.AccountingBookId}|{item.Code}|{item.CoreAccountType}|{item.Status}|{item.IsPostingClassification}|{item.IsDeleted}").ToListAsync();
        var audits = await db.AuditLogs.AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Id).Select(item => $"{item.Id}|{item.Action}|{item.Resource}|{item.ResourceId}|{item.OldValues}|{item.NewValues}").ToListAsync();
        return string.Join("\n", accounts.Concat(segments).Concat(mappings).Concat(classifications).Concat(audits));
    }
}
