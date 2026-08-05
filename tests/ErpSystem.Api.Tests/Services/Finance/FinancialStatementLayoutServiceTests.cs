using ErpSystem.Api.Services.Finance.Reporting;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinancialStatementLayoutServiceTests
{
    [Fact]
    [Trait("Category", "Reporting")]
    public async Task DraftLifecycle_ShouldReplaceValidateAndPublishVersionedRows()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var cash = SeedAccount(context, tenantId, book.Id, "1000", "Cash", AccountType.Asset);
        var receivables = SeedAccount(context, tenantId, book.Id, "1100", "Receivables", AccountType.Asset);
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);

        var layout = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "bs_main",
            Name = "Main Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet,
            AccountingBookId = book.Id,
            IsDefault = true
        });
        var draft = layout.Versions.Single();

        var replaced = await service.ReplaceDraftRowsAsync(
            draft.Id,
            new ReplaceFinancialStatementRowsDto
            {
                ExpectedVersionRevision = draft.Revision,
                Rows =
                {
                    Row("1000", "Assets", FinancialStatementRowType.Header, 100),
                    Row("1100", "Cash and cash equivalents", FinancialStatementRowType.Account, 110, "1000",
                        Mapping(FinancialStatementRowMappingType.Account, cash.Id)),
                    Row("1200", "Trade receivables", FinancialStatementRowType.Account, 120, "1000",
                        Mapping(FinancialStatementRowMappingType.Account, receivables.Id)),
                    Row("1900", "Total assets", FinancialStatementRowType.Formula, 190, formula: "1100 + 1200")
                }
            });

        var validation = await service.ValidateVersionAsync(replaced.Id);
        validation.IsValid.Should().BeTrue();
        validation.Issues.Should().NotContain(issue =>
            issue.Severity == FinancialStatementLayoutValidationSeverity.Error);

        var published = await service.PublishVersionAsync(
            replaced.Id,
            new PublishFinancialStatementLayoutVersionDto
            {
                ExpectedVersionRevision = replaced.Revision,
                EffectiveFrom = new DateTime(2026, 7, 1)
            });

        published.Status.Should().Be(FinancialStatementLayoutVersionStatus.Published);
        published.Rows.Select(row => row.RowCode)
            .Should().ContainInOrder("1000", "1100", "1200", "1900");
        published.PublishedAt.Should().NotBeNull();

        var persisted = await service.GetLayoutAsync(layout.Id);
        persisted.Should().NotBeNull();
        persisted!.PublishedVersionNumber.Should().Be(1);
        persisted.IsDefault.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task ReplaceDraftRows_ShouldRejectDuplicateAccountContributionAndFormulaCycle()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var cash = SeedAccount(context, tenantId, book.Id, "1000", "Cash", AccountType.Asset);
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);
        var layout = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "BS_VALIDATION",
            Name = "Balance Sheet Validation",
            StatementType = FinancialStatementType.BalanceSheet,
            AccountingBookId = book.Id
        });
        var draft = layout.Versions.Single();

        var action = () => service.ReplaceDraftRowsAsync(
            draft.Id,
            new ReplaceFinancialStatementRowsDto
            {
                ExpectedVersionRevision = draft.Revision,
                Rows =
                {
                    Row("1100", "Cash A", FinancialStatementRowType.Account, 110,
                        mappings: Mapping(FinancialStatementRowMappingType.Account, cash.Id)),
                    Row("1200", "Cash B", FinancialStatementRowType.Account, 120,
                        mappings: Mapping(FinancialStatementRowMappingType.Account, cash.Id)),
                    Row("1900", "Formula A", FinancialStatementRowType.Formula, 190, formula: "1950"),
                    Row("1950", "Formula B", FinancialStatementRowType.Formula, 195, formula: "1900")
                }
            });

        var exception = await action.Should()
            .ThrowAsync<FinancialStatementLayoutValidationException>();
        exception.Which.Validation.Issues.Should().Contain(issue =>
            issue.Code == "DUPLICATE_ACCOUNT_CONTRIBUTION");
        exception.Which.Validation.Issues.Should().Contain(issue =>
            issue.Code == "FORMULA_CYCLE");
    }

    [Fact]
    [Trait("Category", "TenantIsolation")]
    public async Task ReplaceDraftRows_ShouldRejectCrossTenantAccountMapping()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        SeedTenant(context, otherTenantId, "OTH");
        var otherCash = SeedAccount(context, otherTenantId, null, "1000", "Other cash", AccountType.Asset);
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);
        var layout = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "BS_TENANT",
            Name = "Tenant Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet,
            AccountingBookId = book.Id
        });
        var draft = layout.Versions.Single();

        var action = () => service.ReplaceDraftRowsAsync(
            draft.Id,
            new ReplaceFinancialStatementRowsDto
            {
                ExpectedVersionRevision = draft.Revision,
                Rows =
                {
                    Row("1100", "Cash", FinancialStatementRowType.Account, 110,
                        mappings: Mapping(FinancialStatementRowMappingType.Account, otherCash.Id))
                }
            });

        var exception = await action.Should()
            .ThrowAsync<FinancialStatementLayoutValidationException>();
        exception.Which.Validation.Issues.Should().Contain(issue =>
            issue.Code == "ACCOUNT_NOT_AVAILABLE");
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task ReplaceDraftRows_ShouldRejectAccountFromDifferentAccountingBook()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var reportingBook = SeedTenantAndBook(context, tenantId);
        var otherBook = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "MANAGEMENT",
            Name = "Management",
            Purpose = "Management",
            IsActive = true,
            AllowsPosting = true,
            SortOrder = 20
        };
        context.AccountingBooks.Add(otherBook);
        var managementCash = SeedAccount(
            context,
            tenantId,
            otherBook.Id,
            "1000",
            "Management cash",
            AccountType.Asset);
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId);
        var layout = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "BS_BOOK",
            Name = "Book-specific Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet,
            AccountingBookId = reportingBook.Id
        });
        var draft = layout.Versions.Single();

        var action = () => service.ReplaceDraftRowsAsync(
            draft.Id,
            new ReplaceFinancialStatementRowsDto
            {
                ExpectedVersionRevision = draft.Revision,
                Rows =
                {
                    Row("1100", "Cash", FinancialStatementRowType.Account, 110,
                        mappings: Mapping(
                            FinancialStatementRowMappingType.Account,
                            managementCash.Id))
                }
            });

        var exception = await action.Should()
            .ThrowAsync<FinancialStatementLayoutValidationException>();
        exception.Which.Validation.Issues.Should().Contain(issue =>
            issue.Code == "ACCOUNT_NOT_AVAILABLE");
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task PublishedVersion_ShouldBeImmutableAndCloneIntoNextDraft()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var cash = SeedAccount(context, tenantId, book.Id, "1000", "Cash", AccountType.Asset);
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);
        var layout = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "BS_VERSION",
            Name = "Versioned Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet,
            AccountingBookId = book.Id
        });
        var draft = layout.Versions.Single();
        var replaced = await service.ReplaceDraftRowsAsync(
            draft.Id,
            new ReplaceFinancialStatementRowsDto
            {
                ExpectedVersionRevision = draft.Revision,
                Rows =
                {
                    Row("1100", "Cash", FinancialStatementRowType.Account, 110,
                        mappings: Mapping(FinancialStatementRowMappingType.Account, cash.Id))
                }
            });
        var published = await service.PublishVersionAsync(
            replaced.Id,
            new PublishFinancialStatementLayoutVersionDto
            {
                ExpectedVersionRevision = replaced.Revision
            });

        var replacePublished = () => service.ReplaceDraftRowsAsync(
            published.Id,
            new ReplaceFinancialStatementRowsDto
            {
                ExpectedVersionRevision = published.Revision,
                Rows = { Row("1200", "Changed", FinancialStatementRowType.Header, 120) }
            });
        await replacePublished.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable*");

        var cloned = await service.CreateDraftVersionAsync(
            layout.Id,
            new CreateFinancialStatementLayoutVersionDto
            {
                SourceVersionId = published.Id
            });
        cloned.VersionNumber.Should().Be(2);
        cloned.Status.Should().Be(FinancialStatementLayoutVersionStatus.Draft);
        cloned.Rows.Should().ContainSingle(row =>
            row.RowCode == "1100" &&
            row.Mappings.Single().AccountId == cash.Id);
    }

    private static FinancialStatementRowInputDto Row(
        string code,
        string label,
        FinancialStatementRowType rowType,
        int displayOrder,
        string? parentCode = null,
        FinancialStatementRowMappingInputDto? mappings = null,
        string? formula = null)
        => new()
        {
            RowCode = code,
            Label = label,
            RowType = rowType,
            DisplayOrder = displayOrder,
            ParentRowCode = parentCode,
            Formula = formula,
            SignMultiplier = 1,
            IsVisible = true,
            Mappings = mappings == null
                ? new List<FinancialStatementRowMappingInputDto>()
                : new List<FinancialStatementRowMappingInputDto> { mappings }
        };

    private static FinancialStatementRowMappingInputDto Mapping(
        FinancialStatementRowMappingType mappingType,
        Guid accountId)
        => new()
        {
            MappingType = mappingType,
            AccountId = accountId
        };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"financial-layouts-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static FinancialStatementLayoutService CreateService(
        ApplicationDbContext context,
        Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(service => service.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(service => service.UserName).Returns("layout.accountant");
        currentUser.SetupGet(service => service.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(service => service.UserAgent).Returns("layout-tests");

        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(service => service.RecordAsync(
                It.IsAny<FinanceAuditEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid(), TenantId = tenantId });

        return new FinancialStatementLayoutService(
            context,
            currentUser.Object,
            audit.Object,
            Mock.Of<ILogger<FinancialStatementLayoutService>>());
    }

    private static AccountingBook SeedTenantAndBook(
        ApplicationDbContext context,
        Guid tenantId)
    {
        SeedTenant(context, tenantId, "TEN");
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "IFRS",
            Name = "IFRS",
            Purpose = "Primary",
            IsActive = true,
            IsDefault = true,
            AllowsPosting = true,
            SortOrder = 10
        };
        context.AccountingBooks.Add(book);
        return book;
    }

    private static void SeedTenant(
        ApplicationDbContext context,
        Guid tenantId,
        string code)
        => context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });

    private static Account SeedAccount(
        ApplicationDbContext context,
        Guid tenantId,
        Guid? accountingBookId,
        string number,
        string name,
        AccountType accountType)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = number,
            AccountNumber = number,
            AccountName = name,
            AccountType = accountType,
            Status = AccountStatus.Active,
            AllowDirectPosting = true
        };
        context.Accounts.Add(account);
        if (accountingBookId.HasValue)
        {
            context.AccountAccountingBooks.Add(new AccountAccountingBook
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountId = account.Id,
                AccountingBookId = accountingBookId.Value,
                IsEnabled = true
            });
        }
        return account;
    }
}
