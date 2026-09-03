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
using ClosedXML.Excel;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinancialStatementLayoutImportServiceTests
{
    [Fact]
    [Trait("Category", "Reporting")]
    public async Task ClassificationImport_ShouldRequireV2StableCodeAndRoundTripIt()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var classification = new AccountClassification
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = book.Id,
            Code = "CASH", Name = "Cash and cash equivalents", CoreAccountType = AccountType.Asset,
            Status = AccountClassificationStatus.Active, IsPostingClassification = true
        };
        context.AccountClassifications.Add(classification);
        await context.SaveChangesAsync();
        var fixture = CreateServices(context, tenantId);
        var definition = new FinancialStatementLayoutImportDefinitionDto
        {
            TemplateVersion = "2", Code = "BS_CLASS_IMPORT", Name = "Classified Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet, AccountingBookId = book.Id,
            Rows =
            {
                new FinancialStatementRowInputDto
                {
                    RowCode = "CASH", Label = "Cash", RowType = FinancialStatementRowType.Account,
                    DisplayOrder = 10, SignMultiplier = 1, IsVisible = true,
                    Mappings = { new FinancialStatementRowMappingInputDto
                    {
                        MappingType = FinancialStatementRowMappingType.Classification,
                        AccountClassificationCode = "CASH", IncludeClassificationDescendants = true
                    } }
                }
            }
        };

        var preview = await fixture.Import.PreviewDefinitionAsync(definition);
        preview.Validation.IsValid.Should().BeTrue();
        preview.Definition.Rows.Single().Mappings.Single().AccountClassificationId.Should().Be(classification.Id);
        preview.Definition.Rows.Single().Mappings.Single().AccountClassificationCode.Should().Be("CASH");

        definition.TemplateVersion = "1";
        definition.Rows.Single().Mappings.Single().AccountClassificationCode = "Cash and cash equivalents";
        var incompatible = await fixture.Import.PreviewDefinitionAsync(definition);
        incompatible.Validation.Issues.Should().Contain(item => item.Code == "TEMPLATE_VERSION_INVALID");
        incompatible.Validation.Issues.Should().Contain(item => item.Code == "MAPPING_CLASSIFICATION_UNKNOWN");
    }

    [Fact]
    [Trait("Category", "Reporting")]
    [Trait("Category", "Spreadsheet")]
    public async Task JsonImport_ShouldRequirePreviewHashAndCreateDraftOnly()
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
            AccountType.Asset,
            "Cash and cash equivalents");
        await context.SaveChangesAsync();
        var fixture = CreateServices(context, tenantId);
        var definition = Definition(
            book.Id,
            "BS_IMPORT",
            "Imported Balance Sheet",
            cash.Id);

        var preview = await fixture.Import.PreviewDefinitionAsync(
            definition);

        preview.Validation.IsValid.Should().BeTrue(
            string.Join(
                "; ",
                preview.Validation.Issues.Select(issue =>
                    $"{issue.Code}: {issue.Message}")));
        preview.WillCreateLayout.Should().BeTrue();
        preview.DefinitionHash.Should().HaveLength(64);

        var invalidHash = () => fixture.Import.CommitDefinitionAsync(
            new FinancialStatementLayoutImportCommitDto
            {
                Definition = definition,
                ExpectedDefinitionHash = new string('0', 64)
            });
        await invalidHash.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*changed after preview*");

        var result = await fixture.Import.CommitDefinitionAsync(
            new FinancialStatementLayoutImportCommitDto
            {
                Definition = definition,
                ExpectedDefinitionHash = preview.DefinitionHash
            });

        result.CreatedLayout.Should().BeTrue();
        result.Layout.IsDefault.Should().BeFalse();
        result.Layout.Versions.Should().ContainSingle(version =>
            version.Status ==
                FinancialStatementLayoutVersionStatus.Draft &&
            version.Rows.Count == 1);
        result.Layout.Versions.Should().NotContain(version =>
            version.Status ==
            FinancialStatementLayoutVersionStatus.Published);
        fixture.Audit.Verify(service => service.RecordAsync(
            It.Is<FinanceAuditEventDto>(audit =>
                audit.EventType ==
                FinanceAuditEvents.FinancialStatementLayoutImported),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "TenantIsolation")]
    public async Task PreviewDefinition_ShouldRejectCrossTenantBookAndAccount()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var ownAccount = SeedAccount(
            context,
            tenantId,
            book.Id,
            "1000",
            "Cash",
            AccountType.Asset,
            "Cash");
        var otherBook = SeedTenantAndBook(context, otherTenantId);
        var otherAccount = SeedAccount(
            context,
            otherTenantId,
            otherBook.Id,
            "9999",
            "Other tenant cash",
            AccountType.Asset,
            "Cash");
        await context.SaveChangesAsync();
        var fixture = CreateServices(context, tenantId);

        var crossAccount = Definition(
            book.Id,
            "BS_CROSS_ACCOUNT",
            "Cross Account",
            otherAccount.Id);
        var accountPreview = await fixture.Import
            .PreviewDefinitionAsync(crossAccount);
        accountPreview.Validation.Issues.Should().Contain(issue =>
            issue.Code == "ACCOUNT_NOT_AVAILABLE");

        var crossBook = Definition(
            otherBook.Id,
            "BS_CROSS_BOOK",
            "Cross Book",
            ownAccount.Id);
        var bookPreview = await fixture.Import
            .PreviewDefinitionAsync(crossBook);
        bookPreview.Validation.Issues.Should().Contain(issue =>
            issue.Code == "ACCOUNTING_BOOK_INVALID");
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task LegacyMigration_ShouldGenerateReviewableRowsAndDeferDefault()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        SeedAccount(
            context,
            tenantId,
            book.Id,
            "1000",
            "Cash",
            AccountType.Asset,
            "Cash and cash equivalents");
        SeedAccount(
            context,
            tenantId,
            book.Id,
            "3000",
            "Retained Earnings",
            AccountType.Equity,
            "Retained earnings");
        await context.SaveChangesAsync();
        var fixture = CreateServices(context, tenantId);
        var request =
            new LegacyFinancialStatementLayoutMigrationRequestDto
            {
                Code = "BS_MIGRATED",
                Name = "Migrated Balance Sheet",
                StatementType =
                    FinancialStatementType.BalanceSheet,
                AccountingBookId = book.Id,
                IsDefault = true,
                EffectiveFrom = new DateTime(2026, 1, 1)
            };

        var preview = await fixture.Import
            .PreviewLegacyMigrationAsync(request);

        preview.Validation.IsValid.Should().BeTrue(
            string.Join(
                "; ",
                preview.Validation.Issues.Select(issue =>
                    $"{issue.Code}: {issue.Message}")));
        preview.Definition.IsDefault.Should().BeFalse();
        preview.Validation.Issues.Should().Contain(issue =>
            issue.Code == "DEFAULT_DEFERRED");
        preview.Definition.Rows.Should().Contain(row =>
            row.RowType == FinancialStatementRowType.Header &&
            row.Label == "Assets");
        preview.Definition.Rows.Should().Contain(row =>
            row.RowType == FinancialStatementRowType.Account &&
            row.Label == "Cash and cash equivalents" &&
            row.Mappings.Count == 1);
        preview.Definition.Rows.Should().Contain(row =>
            row.RowType == FinancialStatementRowType.Formula &&
            row.Label == "Total Assets");

        var result = await fixture.Import
            .CommitLegacyMigrationAsync(
                new LegacyFinancialStatementLayoutMigrationCommitDto
                {
                    Request = request,
                    ExpectedDefinitionHash =
                        preview.DefinitionHash
                });

        result.Layout.IsDefault.Should().BeFalse();
        result.Layout.Versions.Single().Status.Should()
            .Be(FinancialStatementLayoutVersionStatus.Draft);
        fixture.Audit.Verify(service => service.RecordAsync(
            It.Is<FinanceAuditEventDto>(audit =>
                audit.EventType ==
                FinanceAuditEvents
                    .FinancialStatementLegacyLayoutMigrated),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Spreadsheet")]
    public async Task WorkbookPreview_ShouldResolveTenantBookAccountsAndRejectCellFormulas()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        SeedAccount(
            context,
            tenantId,
            book.Id,
            "1000",
            "Cash",
            AccountType.Asset,
            "Cash");
        await context.SaveChangesAsync();
        var fixture = CreateServices(context, tenantId);
        var template = await fixture.Import
            .CreateWorkbookTemplateAsync();

        byte[] bytes;
        using (var package = new XLWorkbook(
                   new MemoryStream(template.Content)))
        {
            var metadata = package.Worksheet("Metadata");
            metadata.Cell(2, 6).Value = "BS_WORKBOOK";
            metadata.Cell(2, 7).Value = "Workbook Balance Sheet";
            metadata.Cell(2, 9).Value = "BalanceSheet";
            metadata.Cell(2, 10).Value = book.Id.ToString();

            var rows = package.Worksheet("Rows");
            rows.Cell(2, 1).Value = "ASSETS";
            rows.Cell(2, 3).Value = "Assets";
            rows.Cell(2, 4).Value = "Header";
            rows.Cell(2, 5).Value = 10;
            rows.Cell(3, 1).Value = "CASH";
            rows.Cell(3, 2).Value = "ASSETS";
            rows.Cell(3, 3).Value =
                "Cash and cash equivalents";
            rows.Cell(3, 4).Value = "Account";
            rows.Cell(3, 5).Value = 10;
            rows.Cell(3, 7).Value = 1;
            rows.Cell(3, 8).Value = true;

            var mappings = package.Worksheet("Mappings");
            mappings.Cell(2, 1).Value = "CASH";
            mappings.Cell(2, 2).Value = "Account";
            mappings.Cell(2, 3).Value = "1000";
            using var output = new MemoryStream();
            package.SaveAs(output);
            bytes = output.ToArray();
        }

        var preview = await fixture.Import.PreviewWorkbookAsync(
            new MemoryStream(bytes),
            "layout.xlsx");

        preview.Validation.IsValid.Should().BeTrue(
            string.Join(
                "; ",
                preview.Validation.Issues.Select(issue =>
                    $"{issue.Code}: {issue.Message}")));
        preview.Definition.Rows.Single(row =>
                row.RowCode == "CASH")
            .Mappings.Should().ContainSingle(mapping =>
                mapping.AccountId.HasValue);

        using var formulaPackage = new XLWorkbook(
            new MemoryStream(bytes));
        formulaPackage.Worksheet("Metadata")
            .Cell(2, 14).FormulaA1 = "NOW()";
        using var formulaStream = new MemoryStream();
        formulaPackage.SaveAs(formulaStream);
        var formulaPreview =
            await fixture.Import.PreviewWorkbookAsync(
                new MemoryStream(formulaStream.ToArray()),
                "layout.xlsx");
        formulaPreview.Validation.Issues.Should().Contain(issue =>
            issue.Code == "WORKBOOK_FORMULA_NOT_ALLOWED");
        formulaPreview.Validation.IsValid.Should().BeFalse();
    }

    private static FinancialStatementLayoutImportDefinitionDto
        Definition(
            Guid bookId,
            string code,
            string name,
            Guid accountId)
        => new()
        {
            Code = code,
            Name = name,
            StatementType = FinancialStatementType.BalanceSheet,
            AccountingBookId = bookId,
            Rows = new List<FinancialStatementRowInputDto>
            {
                new()
                {
                    RowCode = "CASH",
                    Label = "Cash",
                    RowType = FinancialStatementRowType.Account,
                    DisplayOrder = 10,
                    SignMultiplier = 1,
                    IsVisible = true,
                    Mappings =
                        new List<
                            FinancialStatementRowMappingInputDto>
                        {
                            new()
                            {
                                MappingType =
                                    FinancialStatementRowMappingType
                                        .Account,
                                AccountId = accountId
                            }
                        }
                }
            }
        };

    private static ApplicationDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(
                    $"financial-layout-import-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(
                    InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        return new ApplicationDbContext(options);
    }

    private static ImportFixture CreateServices(
        ApplicationDbContext context,
        Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId)
            .Returns(tenantId);
        currentUser.SetupGet(service => service.Claims)
            .Returns(new Dictionary<string, string>());
        currentUser.SetupGet(service => service.UserId)
            .Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(service => service.UserName)
            .Returns("layout.importer");
        currentUser.SetupGet(service => service.IpAddress)
            .Returns("127.0.0.1");
        currentUser.SetupGet(service => service.UserAgent)
            .Returns("layout-import-tests");

        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(service => service.RecordAsync(
                It.IsAny<FinanceAuditEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId
            });
        var layouts = new FinancialStatementLayoutService(
            context,
            currentUser.Object,
            audit.Object,
            Mock.Of<ILogger<
                FinancialStatementLayoutService>>());
        var import =
            new FinancialStatementLayoutImportService(
                context,
                currentUser.Object,
                layouts,
                audit.Object);
        return new ImportFixture(import, audit);
    }

    private static AccountingBook SeedTenantAndBook(
        ApplicationDbContext context,
        Guid tenantId)
    {
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Import Tenant",
            Code = $"IMP-{tenantId:N}"[..12],
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
        Guid bookId,
        string accountNumber,
        string accountName,
        AccountType accountType,
        string lineItem)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = accountName,
            AccountType = accountType,
            Status = AccountStatus.Active,
            AllowDirectPosting = true,
            IFRSLineItem = lineItem
        };
        context.Accounts.Add(account);
        context.AccountAccountingBooks.Add(
            new AccountAccountingBook
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountId = account.Id,
                AccountingBookId = bookId,
                IsEnabled = true,
                FinancialStatementLineItem = lineItem
            });
        return account;
    }

    private sealed record ImportFixture(
        FinancialStatementLayoutImportService Import,
        Mock<IFinanceAuditService> Audit);
}
