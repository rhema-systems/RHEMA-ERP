using ErpSystem.Api.Services.Finance.Reporting;
using ErpSystem.Api.Services.Finance.GL;
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
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinancialStatementLayoutExecutionServiceTests
{
    [Fact]
    [Trait("Category", "Reporting")]
    public async Task PreviewVersion_ShouldCalculateRowsFormulasAndReconciliation()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var cash = SeedAccount(context, tenantId, book.Id, "1000", "Cash", AccountType.Asset);
        var receivables = SeedAccount(context, tenantId, book.Id, "1100", "Receivables", AccountType.Asset);
        var unmapped = SeedAccount(context, tenantId, book.Id, "1200", "Inventory", AccountType.Asset);
        var payables = SeedAccount(context, tenantId, book.Id, "2000", "Payables", AccountType.Liability);
        var layout = SeedLayout(
            context,
            tenantId,
            book,
            FinancialStatementType.BalanceSheet,
            FinancialStatementLayoutVersionStatus.Draft);
        var version = layout.Versions.Single();

        var assets = AddRow(version, tenantId, "ASSETS", "Assets", FinancialStatementRowType.Header, 10);
        AddRow(
            version,
            tenantId,
            "CASH",
            "Cash",
            FinancialStatementRowType.Account,
            10,
            assets,
            mapping: ExactMapping(tenantId, cash.Id));
        AddRow(
            version,
            tenantId,
            "RECEIVABLES",
            "Receivables",
            FinancialStatementRowType.Account,
            20,
            assets,
            mapping: RangeMapping(tenantId, "1100", "1199"));
        AddRow(
            version,
            tenantId,
            "TOTAL_ASSETS",
            "Total assets",
            FinancialStatementRowType.Formula,
            20,
            formula: "SUM(CASH:RECEIVABLES)");
        AddRow(
            version,
            tenantId,
            "PAYABLES",
            "Payables",
            FinancialStatementRowType.Account,
            30,
            mapping: ExactMapping(tenantId, payables.Id));
        AddRow(
            version,
            tenantId,
            "NET_POSITION",
            "Net position",
            FinancialStatementRowType.Formula,
            40,
            formula: "TOTAL_ASSETS - PAYABLES");

        SeedPostedTransaction(context, tenantId, cash.Id, new DateTime(2026, 7, 10), 100m, 0m);
        SeedPostedTransaction(context, tenantId, receivables.Id, new DateTime(2026, 7, 11), 50m, 0m);
        SeedPostedTransaction(context, tenantId, unmapped.Id, new DateTime(2026, 7, 12), 25m, 0m);
        SeedPostedTransaction(context, tenantId, payables.Id, new DateTime(2026, 7, 13), 0m, 40m);
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId);
        var result = await service.PreviewVersionAsync(
            version.Id,
            new FinancialStatementLayoutPreviewRequestDto
            {
                PeriodEnd = new DateTime(2026, 7, 31),
                IncludeAccountDetails = true,
                IncludeHiddenRows = true
            });

        result.IsPreview.Should().BeTrue();
        result.Rows.Select(row => row.RowCode).Should().ContainInOrder(
            "ASSETS",
            "CASH",
            "RECEIVABLES",
            "TOTAL_ASSETS",
            "PAYABLES",
            "NET_POSITION");
        result.Rows.Single(row => row.RowCode == "CASH").Amount.Should().Be(100m);
        result.Rows.Single(row => row.RowCode == "RECEIVABLES").Amount.Should().Be(50m);
        result.Rows.Single(row => row.RowCode == "TOTAL_ASSETS").Amount.Should().Be(150m);
        result.Rows.Single(row => row.RowCode == "PAYABLES").Amount.Should().Be(40m);
        result.Rows.Single(row => row.RowCode == "NET_POSITION").Amount.Should().Be(110m);
        result.Rows.Single(row => row.RowCode == "CASH").Accounts
            .Should().ContainSingle(account =>
                account.AccountId == cash.Id &&
                account.PresentedAmount == 100m);

        result.Reconciliation.EligibleAccountCount.Should().Be(4);
        result.Reconciliation.MappedAccountCount.Should().Be(3);
        result.Reconciliation.UnmappedAccountCount.Should().Be(1);
        result.Reconciliation.UnmappedNonZeroAccountCount.Should().Be(1);
        result.Reconciliation.UnmappedNormalBalance.Should().Be(25m);
        result.Reconciliation.AccountCoveragePercent.Should().Be(75m);
        result.Reconciliation.UnmappedAccounts.Should().ContainSingle(account =>
            account.AccountId == unmapped.Id);
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task PreviewVersion_ShouldUsePeriodMovementHierarchyMappingsAndAutomaticTotals()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var expenseRoot = SeedAccount(context, tenantId, book.Id, "5000", "Expenses", AccountType.Expense);
        var utilities = SeedAccount(
            context,
            tenantId,
            book.Id,
            "5100",
            "Utilities",
            AccountType.Expense,
            expenseRoot.Id);
        var layout = SeedLayout(
            context,
            tenantId,
            book,
            FinancialStatementType.IncomeStatement,
            FinancialStatementLayoutVersionStatus.Draft);
        var version = layout.Versions.Single();

        var total = AddRow(
            version,
            tenantId,
            "OPERATING_EXPENSES",
            "Operating expenses",
            FinancialStatementRowType.Total,
            10);
        var heading = AddRow(
            version,
            tenantId,
            "EXPENSE_HEADING",
            "Expense accounts",
            FinancialStatementRowType.Header,
            10,
            total);
        AddRow(
            version,
            tenantId,
            "EXPENSE_ACCOUNTS",
            "Expenses",
            FinancialStatementRowType.Account,
            10,
            heading,
            mapping: HierarchyMapping(tenantId, expenseRoot.Id));

        SeedPostedTransaction(context, tenantId, utilities.Id, new DateTime(2026, 6, 30), 100m, 0m);
        SeedPostedTransaction(context, tenantId, utilities.Id, new DateTime(2026, 7, 15), 30m, 0m);
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId);
        var result = await service.PreviewVersionAsync(
            version.Id,
            new FinancialStatementLayoutPreviewRequestDto
            {
                PeriodStart = new DateTime(2026, 7, 1),
                PeriodEnd = new DateTime(2026, 7, 31),
                IncludeHiddenRows = true
            });

        result.Rows.Single(row => row.RowCode == "EXPENSE_ACCOUNTS")
            .Amount.Should().Be(30m);
        result.Rows.Single(row => row.RowCode == "OPERATING_EXPENSES")
            .Amount.Should().Be(30m);
        result.PeriodStart.Should().Be(new DateTime(2026, 7, 1));
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task ExecutePublished_ShouldResolveVersionEffectiveOnReportDate()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var cash = SeedAccount(context, tenantId, book.Id, "1000", "Cash", AccountType.Asset);
        var layout = new FinancialStatementLayout
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "BS_DEFAULT",
            Name = "Default Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet,
            AccountingBookId = book.Id,
            IsDefault = true,
            IsActive = true,
            Revision = 1
        };
        var historical = AddVersion(
            layout,
            tenantId,
            1,
            FinancialStatementLayoutVersionStatus.Retired,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 6, 30));
        AddRow(
            historical,
            tenantId,
            "CASH",
            "Cash",
            FinancialStatementRowType.Account,
            10,
            mapping: ExactMapping(tenantId, cash.Id));
        var current = AddVersion(
            layout,
            tenantId,
            2,
            FinancialStatementLayoutVersionStatus.Published,
            new DateTime(2026, 7, 1),
            null);
        AddRow(
            current,
            tenantId,
            "CASH",
            "Cash",
            FinancialStatementRowType.Account,
            10,
            signMultiplier: -1,
            mapping: ExactMapping(tenantId, cash.Id));
        context.FinancialStatementLayouts.Add(layout);
        SeedPostedTransaction(context, tenantId, cash.Id, new DateTime(2026, 2, 1), 100m, 0m);
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId);
        var june = await service.ExecutePublishedAsync(
            new FinancialStatementLayoutExecutionRequestDto
            {
                StatementType = FinancialStatementType.BalanceSheet,
                AccountingBookId = book.Id,
                PeriodEnd = new DateTime(2026, 6, 30),
                IncludeHiddenRows = true
            });
        var august = await service.ExecutePublishedAsync(
            new FinancialStatementLayoutExecutionRequestDto
            {
                StatementType = FinancialStatementType.BalanceSheet,
                AccountingBookId = book.Id,
                PeriodEnd = new DateTime(2026, 8, 31),
                IncludeHiddenRows = true
            });

        june.VersionNumber.Should().Be(1);
        june.Rows.Single().Amount.Should().Be(100m);
        august.VersionNumber.Should().Be(2);
        august.Rows.Single().Amount.Should().Be(-100m);
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task PreviewVersion_ShouldApplySelectedAccountFilters()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var cash = SeedAccount(
            context,
            tenantId,
            book.Id,
            "1000",
            "Cash",
            AccountType.Asset);
        var receivables = SeedAccount(
            context,
            tenantId,
            book.Id,
            "1100",
            "Receivables",
            AccountType.Asset);
        var layout = SeedLayout(
            context,
            tenantId,
            book,
            FinancialStatementType.BalanceSheet,
            FinancialStatementLayoutVersionStatus.Draft);
        var version = layout.Versions.Single();
        AddRow(
            version,
            tenantId,
            "CURRENT_ASSETS",
            "Current assets",
            FinancialStatementRowType.Account,
            10,
            mapping: RangeMapping(tenantId, "1000", "1199"));

        SeedPostedTransaction(
            context,
            tenantId,
            cash.Id,
            new DateTime(2026, 7, 10),
            100m,
            0m);
        SeedPostedTransaction(
            context,
            tenantId,
            receivables.Id,
            new DateTime(2026, 7, 11),
            50m,
            0m);
        await context.SaveChangesAsync();

        var result = await CreateService(context, tenantId).PreviewVersionAsync(
            version.Id,
            new FinancialStatementLayoutPreviewRequestDto
            {
                PeriodEnd = new DateTime(2026, 7, 31),
                IncludeAccountDetails = true,
                AccountIds = new List<Guid> { receivables.Id }
            });

        result.Rows.Single().Amount.Should().Be(50m);
        result.Rows.Single().Accounts.Should().ContainSingle(account =>
            account.AccountId == receivables.Id);
        result.Reconciliation.EligibleAccountCount.Should().Be(1);
        result.Reconciliation.AccountCoveragePercent.Should().Be(100m);
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task ExecutePublished_ShouldApplyImmutableTransactionDimensionFilters()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var cash = SeedAccount(context, tenantId, book.Id, "1000", "Cash", AccountType.Asset);
        var layout = SeedLayout(
            context,
            tenantId,
            book,
            FinancialStatementType.BalanceSheet,
            FinancialStatementLayoutVersionStatus.Published);
        AddRow(
            layout.Versions.Single(),
            tenantId,
            "CASH",
            "Cash",
            FinancialStatementRowType.Account,
            10,
            mapping: ExactMapping(tenantId, cash.Id));

        var department = new FinanceDimensionDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "DEPT",
            Name = "Department",
            Classification = "Analytical",
            ValueSourceType = "Lookup",
            IsActive = true
        };
        var finance = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceDimensionDefinitionId = department.Id,
            Code = "FIN",
            Name = "Finance",
            EffectiveDate = new DateTime(2025, 1, 1),
            IsActive = true
        };
        var operations = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceDimensionDefinitionId = department.Id,
            Code = "OPS",
            Name = "Operations",
            EffectiveDate = new DateTime(2025, 1, 1),
            IsActive = true
        };
        var financeSet = SeedDimensionSet(context, tenantId, department, finance);
        var operationsSet = SeedDimensionSet(context, tenantId, department, operations);
        context.FinanceDimensionDefinitions.Add(department);
        context.FinanceDimensionValues.AddRange(finance, operations);
        SeedPostedTransaction(
            context,
            tenantId,
            cash.Id,
            new DateTime(2026, 7, 10),
            100m,
            0m,
            financeSet.Id);
        SeedPostedTransaction(
            context,
            tenantId,
            cash.Id,
            new DateTime(2026, 7, 11),
            50m,
            0m,
            operationsSet.Id);
        await context.SaveChangesAsync();

        var result = await CreateService(context, tenantId).ExecutePublishedAsync(
            new FinancialStatementLayoutExecutionRequestDto
            {
                StatementType = FinancialStatementType.BalanceSheet,
                AccountingBookId = book.Id,
                PeriodEnd = new DateTime(2026, 7, 31),
                DimensionFilters =
                {
                    new FinanceDimensionFilterDto
                    {
                        FinanceDimensionDefinitionId = department.Id,
                        ValueCodes = { "FIN" }
                    }
                }
            });

        result.Rows.Single().Amount.Should().Be(100m);
    }

    private static FinancialStatementLayoutExecutionService CreateService(
        ApplicationDbContext context,
        Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.Claims)
            .Returns(new Dictionary<string, string>());

        var settings = new Mock<ITenantSettingsService>();
        settings.Setup(service => service.GetCompanyNameAsync()).ReturnsAsync("Tenant Co");
        settings.Setup(service => service.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

        var layoutService = new Mock<IFinancialStatementLayoutService>();
        layoutService.Setup(service => service.ValidateVersionAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialStatementLayoutValidationResultDto());

        return new FinancialStatementLayoutExecutionService(
            context,
            currentUser.Object,
            settings.Object,
            layoutService.Object,
            new FinanceDimensionReportingFilterService(context, currentUser.Object));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"financial-layout-execution-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AccountingBook SeedTenantAndBook(
        ApplicationDbContext context,
        Guid tenantId)
    {
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Tenant",
            Code = "TEN",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
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

    private static Account SeedAccount(
        ApplicationDbContext context,
        Guid tenantId,
        Guid accountingBookId,
        string number,
        string name,
        AccountType accountType,
        Guid? parentAccountId = null)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = number,
            AccountNumber = number,
            AccountName = name,
            AccountType = accountType,
            ParentAccountId = parentAccountId,
            Status = AccountStatus.Active,
            AllowDirectPosting = true
        };
        context.Accounts.Add(account);
        context.AccountAccountingBooks.Add(new AccountAccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountId = account.Id,
            AccountingBookId = accountingBookId,
            IsEnabled = true
        });
        return account;
    }

    private static FinancialStatementLayout SeedLayout(
        ApplicationDbContext context,
        Guid tenantId,
        AccountingBook book,
        FinancialStatementType statementType,
        FinancialStatementLayoutVersionStatus status)
    {
        var layout = new FinancialStatementLayout
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = statementType == FinancialStatementType.BalanceSheet ? "BS_TEST" : "IS_TEST",
            Name = "Test layout",
            StatementType = statementType,
            AccountingBookId = book.Id,
            IsDefault = true,
            IsActive = true,
            Revision = 1
        };
        AddVersion(layout, tenantId, 1, status, new DateTime(2026, 1, 1), null);
        context.FinancialStatementLayouts.Add(layout);
        return layout;
    }

    private static FinancialStatementLayoutVersion AddVersion(
        FinancialStatementLayout layout,
        Guid tenantId,
        int versionNumber,
        FinancialStatementLayoutVersionStatus status,
        DateTime? effectiveFrom,
        DateTime? effectiveTo)
    {
        var version = new FinancialStatementLayoutVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinancialStatementLayoutId = layout.Id,
            VersionNumber = versionNumber,
            Status = status,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            Revision = 1
        };
        layout.Versions.Add(version);
        return version;
    }

    private static FinancialStatementRow AddRow(
        FinancialStatementLayoutVersion version,
        Guid tenantId,
        string code,
        string label,
        FinancialStatementRowType rowType,
        int displayOrder,
        FinancialStatementRow? parent = null,
        string? formula = null,
        int signMultiplier = 1,
        FinancialStatementRowMapping? mapping = null)
    {
        var row = new FinancialStatementRow
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinancialStatementLayoutVersionId = version.Id,
            ParentRowId = parent?.Id,
            RowCode = code,
            Label = label,
            RowType = rowType,
            DisplayOrder = displayOrder,
            Formula = formula,
            SignMultiplier = signMultiplier,
            IsVisible = true
        };
        if (mapping != null)
        {
            mapping.FinancialStatementRowId = row.Id;
            row.Mappings.Add(mapping);
        }
        version.Rows.Add(row);
        return row;
    }

    private static FinancialStatementRowMapping ExactMapping(
        Guid tenantId,
        Guid accountId)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MappingType = FinancialStatementRowMappingType.Account,
            AccountId = accountId
        };

    private static FinancialStatementRowMapping HierarchyMapping(
        Guid tenantId,
        Guid rootAccountId)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MappingType = FinancialStatementRowMappingType.AccountHierarchy,
            AccountId = rootAccountId
        };

    private static FinancialStatementRowMapping RangeMapping(
        Guid tenantId,
        string from,
        string to)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MappingType = FinancialStatementRowMappingType.AccountRange,
            FromAccountNumber = from,
            ToAccountNumber = to
        };

    private static void SeedPostedTransaction(
        ApplicationDbContext context,
        Guid tenantId,
        Guid accountId,
        DateTime date,
        decimal debit,
        decimal credit,
        Guid? financeDimensionSetId = null)
    {
        var journalId = Guid.NewGuid();
        context.JournalEntries.Add(new JournalEntry
        {
            Id = journalId,
            TenantId = tenantId,
            JournalEntryNumber = $"JE-{journalId:N}",
            EntryDate = date,
            Description = "Execution test posting",
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            FiscalPeriodId = Guid.NewGuid(),
            TotalDebitAmount = debit,
            TotalCreditAmount = credit,
            IsBalanced = debit == credit
        });
        context.AccountTransactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountId = accountId,
            JournalEntryId = journalId,
            TransactionDate = date,
            DebitAmount = debit,
            CreditAmount = credit,
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            FiscalPeriodId = Guid.NewGuid(),
            FunctionalCurrencyCode = "GHS",
            FinanceDimensionSetId = financeDimensionSetId
        });
    }

    private static FinanceDimensionSet SeedDimensionSet(
        ApplicationDbContext context,
        Guid tenantId,
        FinanceDimensionDefinition definition,
        FinanceDimensionValue value)
    {
        var set = new FinanceDimensionSet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CombinationHash = Guid.NewGuid().ToString("N"),
            DisplayValue = $"{definition.Code}={value.Code}"
        };
        set.Items.Add(new FinanceDimensionSetItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceDimensionSetId = set.Id,
            FinanceDimensionDefinitionId = definition.Id,
            FinanceDimensionValueId = value.Id,
            DimensionCodeSnapshot = definition.Code,
            DimensionValueCodeSnapshot = value.Code,
            DimensionValueNameSnapshot = value.Name
        });
        context.FinanceDimensionSets.Add(set);
        return set;
    }
}
