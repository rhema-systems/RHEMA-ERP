using ErpSystem.Api.Services.Finance.Reporting;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Reporting;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinancialStatementLayoutServiceTests
{
    [Fact]
    [Trait("Category", "Reporting")]
    public async Task GetLayouts_ShouldReturnVersionSummaryWithoutLoadingLayoutDetails()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var layout = new FinancialStatementLayout
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = book.Id,
            Code = "BS_SUMMARY", Name = "Balance sheet summary",
            StatementType = FinancialStatementType.BalanceSheet, IsActive = true
        };
        layout.Versions.Add(new FinancialStatementLayoutVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, VersionNumber = 1,
            Status = FinancialStatementLayoutVersionStatus.Published
        });
        layout.Versions.Add(new FinancialStatementLayoutVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, VersionNumber = 2,
            Status = FinancialStatementLayoutVersionStatus.Draft
        });
        context.FinancialStatementLayouts.Add(layout);
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var summaries = await CreateService(context, tenantId).GetLayoutsAsync();

        summaries.Should().ContainSingle().Which.Should().Match<FinancialStatementLayoutSummaryDto>(item =>
            item.Code == "BS_SUMMARY"
            && item.AccountingBookCode == "IFRS"
            && item.LatestVersionNumber == 2
            && item.PublishedVersionNumber == 1);
        context.ChangeTracker.Entries<FinancialStatementLayoutVersion>().Should().BeEmpty(
            "the register query should project version aggregates instead of materializing the detail graph");
    }

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

        await PrepareForIndependentPublishAsync(context, replaced.Id, new DateTime(2026, 7, 1));

        var published = await service.PublishVersionAsync(
            replaced.Id,
            new PublishFinancialStatementLayoutVersionDto
            {
                ExpectedVersionRevision = replaced.Revision
            });

        published.Status.Should().Be(FinancialStatementLayoutVersionStatus.Published);
        published.Rows.Select(row => row.RowCode)
            .Should().ContainInOrder("1000", "1100", "1200", "1900");
        published.PublishedAt.Should().NotBeNull();
        published.PublicationSnapshotSchemaVersion.Should().Be(
            FinancialStatementPublicationFingerprint.SnapshotSchemaVersion);
        published.PublicationAccountCount.Should().Be(2);
        published.HierarchyFingerprint.Should().HaveLength(64);
        published.ResolutionFingerprint.Should().HaveLength(64);

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
    [Trait("Category", "Governance")]
    public async Task SubmittedLayout_ShouldAppearInWorkbenchAndRequireIndependentDecision()
    {
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var checkerId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var cash = SeedAccount(context, tenantId, book.Id, "1000", "Cash", AccountType.Asset);
        await context.SaveChangesAsync();

        var maker = CreateService(context, tenantId, userId: makerId, userName: "layout.maker");
        var layout = await maker.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "BS_APPROVAL", Name = "Governed Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet, AccountingBookId = book.Id
        });
        var draft = layout.Versions.Single();
        var prepared = await maker.ReplaceDraftRowsAsync(draft.Id, new ReplaceFinancialStatementRowsDto
        {
            ExpectedVersionRevision = draft.Revision,
            Rows = { Row("CASH", "Cash", FinancialStatementRowType.Account, 10,
                mappings: Mapping(FinancialStatementRowMappingType.Account, cash.Id)) }
        });
        var submitted = await maker.SubmitVersionForApprovalAsync(prepared.Id,
            new SubmitFinancialStatementLayoutVersionDto { ExpectedVersionRevision = prepared.Revision });

        submitted.Status.Should().Be(FinancialStatementLayoutVersionStatus.Submitted);
        (await maker.GetPendingApprovalsAsync()).Should().ContainSingle().Which.CanDecide.Should().BeFalse();
        var selfApproval = () => maker.DecideVersionApprovalAsync(submitted.Id,
            new DecideFinancialStatementLayoutVersionDto
            {
                ExpectedVersionRevision = submitted.Revision,
                Decision = FinancialStatementLayoutApprovalDecision.Approve
            });
        await selfApproval.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*submitter from approving or rejecting*");

        context.ChangeTracker.Clear();
        var checker = CreateService(context, tenantId, userId: checkerId, userName: "layout.checker");
        (await checker.GetPendingApprovalsAsync()).Should().ContainSingle().Which.CanDecide.Should().BeTrue();
        var rejected = await checker.DecideVersionApprovalAsync(submitted.Id,
            new DecideFinancialStatementLayoutVersionDto
            {
                ExpectedVersionRevision = submitted.Revision,
                Decision = FinancialStatementLayoutApprovalDecision.Reject,
                Reason = "Mapping requires correction."
            });

        rejected.Status.Should().Be(FinancialStatementLayoutVersionStatus.Draft);
        rejected.LastDecisionById.Should().Be(checkerId);
        rejected.LastDecisionReason.Should().Be("Mapping requires correction.");
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
        await PrepareForIndependentPublishAsync(context, replaced.Id);
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

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task ClassificationMapping_ShouldExpandDescendantsFreezeMembershipAndRejectOverlap()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        var assets = SeedClassification(context, tenantId, book.Id, "ASSET", "Assets", AccountType.Asset);
        var otherAssets = SeedClassification(context, tenantId, book.Id, "OTHER_ASSET", "Other assets", AccountType.Asset);
        var cashClass = SeedClassification(context, tenantId, book.Id, "CASH", "Cash", AccountType.Asset, assets.Id);
        var receivableClass = SeedClassification(context, tenantId, book.Id, "RECEIVABLE", "Receivable", AccountType.Asset, assets.Id);
        var cash = SeedAccount(context, tenantId, book.Id, "1000", "Cash", AccountType.Asset);
        var receivable = SeedAccount(context, tenantId, book.Id, "1100", "Receivable", AccountType.Asset);
        await context.SaveChangesAsync();
        context.AccountAccountingBooks.Single(item => item.AccountId == cash.Id).AccountClassificationId = cashClass.Id;
        context.AccountAccountingBooks.Single(item => item.AccountId == receivable.Id).AccountClassificationId = receivableClass.Id;
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);
        var layout = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "BS_CLASS", Name = "Classification Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet, AccountingBookId = book.Id
        });
        var draft = layout.Versions.Single();

        var overlap = () => service.ReplaceDraftRowsAsync(draft.Id, new ReplaceFinancialStatementRowsDto
        {
            ExpectedVersionRevision = draft.Revision,
            Rows =
            {
                Row("ASSETS", "Assets", FinancialStatementRowType.Account, 10,
                    mappings: ClassificationMapping(assets.Id, true),
                    additionalMappings: new[] { ClassificationMapping(cashClass.Id, false) })
            }
        });
        var overlapError = await overlap.Should().ThrowAsync<FinancialStatementLayoutValidationException>();
        overlapError.Which.Validation.Issues.Should().Contain(item => item.Code == "OVERLAPPING_ROW_MAPPINGS");

        var replaced = await service.ReplaceDraftRowsAsync(draft.Id, new ReplaceFinancialStatementRowsDto
        {
            ExpectedVersionRevision = draft.Revision,
            Rows = { Row("ASSETS", "Assets", FinancialStatementRowType.Account, 10,
                mappings: ClassificationMapping(assets.Id, true)) }
        });
        await PrepareForIndependentPublishAsync(context, replaced.Id);
        var published = await service.PublishVersionAsync(replaced.Id, new PublishFinancialStatementLayoutVersionDto
        {
            ExpectedVersionRevision = replaced.Revision
        });
        published.PublicationAccountCount.Should().Be(2);
        var frozen = await context.FinancialStatementPublicationAccounts.AsNoTracking().Where(item => item.FinancialStatementLayoutVersionId == published.Id).ToListAsync();
        frozen.Select(item => item.AccountId).Should().BeEquivalentTo(new[] { cash.Id, receivable.Id });
        frozen.Select(item => item.ClassificationCode).Should().OnlyContain(code => code == "RECEIVABLE" || code == "CASH");

        context.AccountAccountingBooks.Single(item => item.AccountId == cash.Id).AccountClassificationId = otherAssets.Id;
        cashClass.Name = "Renamed cash classification";
        await context.SaveChangesAsync();
        var liveDraft = await service.CreateDraftVersionAsync(layout.Id, new CreateFinancialStatementLayoutVersionDto
        {
            SourceVersionId = published.Id
        });
        var liveValidation = await service.ValidateVersionAsync(liveDraft.Id);
        liveValidation.Issues.Should().Contain(item => item.Code == "UNMAPPED_ACCOUNTS"
            && item.Message.StartsWith("1 eligible", StringComparison.Ordinal));
        (await context.FinancialStatementPublicationAccounts.CountAsync(item =>
            item.FinancialStatementLayoutVersionId == published.Id)).Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task ProtectedStandard_ShouldBeCloneOnly()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);
        var created = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "STD_BS", Name = "Standard Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet, AccountingBookId = book.Id
        });
        var entity = await context.FinancialStatementLayouts.SingleAsync(item => item.Id == created.Id);
        entity.IsProtectedStandard = true;
        await context.SaveChangesAsync();

        var edit = () => service.UpdateLayoutAsync(entity.Id, new UpdateFinancialStatementLayoutDto
        {
            Name = "Changed", IsActive = true, ExpectedRevision = entity.Revision
        });
        await edit.Should().ThrowAsync<InvalidOperationException>().WithMessage("*protected standard*");

        var clone = await service.CloneProtectedStandardAsync(entity.Id, new CloneFinancialStatementLayoutDto
        {
            Code = "BS_TENANT", Name = "Tenant Balance Sheet", AccountingBookId = book.Id
        });
        clone.IsProtectedStandard.Should().BeFalse();
        clone.StandardSourceLayoutId.Should().Be(entity.Id);
        clone.Versions.Should().ContainSingle(item => item.Status == FinancialStatementLayoutVersionStatus.Draft);
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task InitializeFromStandards_ShouldCreateMissingDraftsIdempotentlyAndExposeReadiness()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);
        var balanceSheet = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "STD_BS", Name = "Standard Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet, AccountingBookId = book.Id
        });
        var incomeStatement = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "STD_IS", Name = "Standard Income Statement",
            StatementType = FinancialStatementType.IncomeStatement, AccountingBookId = book.Id
        });
        (await context.FinancialStatementLayouts.SingleAsync(item => item.Id == balanceSheet.Id)).IsProtectedStandard = true;
        (await context.FinancialStatementLayouts.SingleAsync(item => item.Id == incomeStatement.Id)).IsProtectedStandard = true;
        await context.SaveChangesAsync();

        var first = await service.InitializeFromProtectedStandardsAsync(
            new InitializeFinancialStatementLayoutsDto());
        var second = await service.InitializeFromProtectedStandardsAsync(
            new InitializeFinancialStatementLayoutsDto());

        first.CreatedCount.Should().Be(2);
        first.Items.Should().OnlyContain(item => item.TenantLayoutId.HasValue);
        first.Items.Should().OnlyContain(item => item.Validation != null);
        first.Readiness.IsReady.Should().BeFalse("Drafts must be reviewed, published, and made default explicitly");
        second.CreatedCount.Should().Be(0);
        (await context.FinancialStatementLayouts.CountAsync(item =>
            !item.IsProtectedStandard && item.StandardSourceLayoutId.HasValue)).Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task DiscardUnusedDraft_ShouldDeleteEligibleGraphAndWriteAuditTombstone()
    {
        var tenantId = Guid.NewGuid();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        await CreateSqliteLayoutSchemaAsync(context);
        var book = SeedTenantAndBook(context, tenantId);
        await context.SaveChangesAsync();
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(
                It.IsAny<FinanceAuditEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid(), TenantId = tenantId });
        var service = CreateService(context, tenantId, audit);
        var layout = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "ACCIDENTAL_BS", Name = "Accidental Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet, AccountingBookId = book.Id
        });
        context.FinancialStatementRows.Add(new FinancialStatementRow
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinancialStatementLayoutVersionId = layout.Versions.Single().Id,
            RowCode = "HEADING",
            Label = "Unused heading",
            RowType = FinancialStatementRowType.Header,
            DisplayOrder = 10,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "layout.accountant"
        });
        await context.SaveChangesAsync();

        await service.DiscardUnusedDraftAsync(layout.Id, new DiscardFinancialStatementLayoutDto
        {
            ExpectedRevision = layout.Revision,
            Reason = "Accidental duplicate created during tenant setup."
        });

        (await context.FinancialStatementLayouts.AnyAsync(item => item.Id == layout.Id)).Should().BeFalse();
        (await context.FinancialStatementLayoutVersions.AnyAsync(item =>
            item.FinancialStatementLayoutId == layout.Id)).Should().BeFalse();
        (await context.FinancialStatementRows.AnyAsync(item =>
            item.FinancialStatementLayoutVersionId == layout.Versions.Single().Id)).Should().BeFalse();
        audit.Verify(item => item.RecordAsync(
            It.Is<FinanceAuditEventDto>(entry =>
                entry.EventType == FinanceAuditEvents.FinancialStatementLayoutDiscarded
                && entry.SourceDocumentId == layout.Id
                && entry.Reason == "Accidental duplicate created during tenant setup."),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task DiscardUnusedDraft_ShouldRejectDefaultAndPublishedLayouts()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);
        var layout = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "DEFAULT_BS", Name = "Default Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet, AccountingBookId = book.Id,
            IsDefault = true
        });

        var discardDefault = () => service.DiscardUnusedDraftAsync(layout.Id,
            new DiscardFinancialStatementLayoutDto
            {
                ExpectedRevision = layout.Revision,
                Reason = "Attempting an unsafe discard."
            });
        await discardDefault.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*default layout*");

        var entity = await context.FinancialStatementLayouts
            .Include(item => item.Versions)
            .SingleAsync(item => item.Id == layout.Id);
        entity.IsDefault = false;
        entity.Versions.Single().Status = FinancialStatementLayoutVersionStatus.Published;
        await context.SaveChangesAsync();
        var discardPublished = () => service.DiscardUnusedDraftAsync(layout.Id,
            new DiscardFinancialStatementLayoutDto
            {
                ExpectedRevision = entity.Revision,
                Reason = "Attempting another unsafe discard."
            });
        await discardPublished.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*never-published*");
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task Publish_ShouldRollBackSnapshotAndStatusWhenAuditFails()
    {
        var tenantId = Guid.NewGuid();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        await CreateSqliteLayoutSchemaAsync(context);
        var book = SeedTenantAndBook(context, tenantId);
        var cash = SeedAccount(context, tenantId, book.Id, "1000", "Cash", AccountType.Asset);
        await context.SaveChangesAsync();
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid(), TenantId = tenantId });
        audit.Setup(item => item.RecordAsync(
                It.Is<FinanceAuditEventDto>(entry => entry.EventType == FinanceAuditEvents.FinancialStatementLayoutVersionPublished),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));
        var service = CreateService(context, tenantId, audit);
        var layout = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "BS_ATOMIC", Name = "Atomic Balance Sheet",
            StatementType = FinancialStatementType.BalanceSheet, AccountingBookId = book.Id
        });
        var draft = layout.Versions.Single();
        var replaced = await service.ReplaceDraftRowsAsync(draft.Id, new ReplaceFinancialStatementRowsDto
        {
            ExpectedVersionRevision = draft.Revision,
            Rows = { Row("CASH", "Cash", FinancialStatementRowType.Account, 10,
                mappings: Mapping(FinancialStatementRowMappingType.Account, cash.Id)) }
        });
        await PrepareForIndependentPublishAsync(context, replaced.Id);

        var publish = () => service.PublishVersionAsync(replaced.Id, new PublishFinancialStatementLayoutVersionDto
        {
            ExpectedVersionRevision = replaced.Revision
        });
        await publish.Should().ThrowAsync<InvalidOperationException>().WithMessage("audit unavailable");

        context.ChangeTracker.Clear();
        (await context.FinancialStatementLayoutVersions.SingleAsync(item => item.Id == replaced.Id)).Status
            .Should().Be(FinancialStatementLayoutVersionStatus.Submitted);
        (await context.FinancialStatementPublicationAccounts.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Reporting")]
    public async Task Publish_ShouldNotPersistStatusWhenSnapshotWriteFails()
    {
        var tenantId = Guid.NewGuid();
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"financial-layout-snapshot-failure-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new RejectPublicationSnapshotInterceptor())
            .Options);
        var book = SeedTenantAndBook(context, tenantId);
        var cash = SeedAccount(context, tenantId, book.Id, "1000", "Cash", AccountType.Asset);
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);
        var layout = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "BS_SNAPSHOT_FAIL", Name = "Snapshot failure",
            StatementType = FinancialStatementType.BalanceSheet, AccountingBookId = book.Id
        });
        var draft = layout.Versions.Single();
        var replaced = await service.ReplaceDraftRowsAsync(draft.Id, new ReplaceFinancialStatementRowsDto
        {
            ExpectedVersionRevision = draft.Revision,
            Rows = { Row("CASH", "Cash", FinancialStatementRowType.Account, 10,
                mappings: Mapping(FinancialStatementRowMappingType.Account, cash.Id)) }
        });
        await PrepareForIndependentPublishAsync(context, replaced.Id);

        var publish = () => service.PublishVersionAsync(replaced.Id, new PublishFinancialStatementLayoutVersionDto
        {
            ExpectedVersionRevision = replaced.Revision
        });
        await publish.Should().ThrowAsync<InvalidOperationException>().WithMessage("snapshot write rejected");

        context.ChangeTracker.Clear();
        (await context.FinancialStatementLayoutVersions.SingleAsync(item => item.Id == replaced.Id)).Status
            .Should().Be(FinancialStatementLayoutVersionStatus.Submitted);
        (await context.FinancialStatementPublicationAccounts.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task ReplaceDraftRows_ShouldRejectStaleDraftRevision()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var book = SeedTenantAndBook(context, tenantId);
        await context.SaveChangesAsync();
        var service = CreateService(context, tenantId);
        var layout = await service.CreateLayoutAsync(new CreateFinancialStatementLayoutDto
        {
            Code = "BS_STALE", Name = "Stale Draft", StatementType = FinancialStatementType.BalanceSheet,
            AccountingBookId = book.Id
        });

        var action = () => service.ReplaceDraftRowsAsync(layout.Versions.Single().Id, new ReplaceFinancialStatementRowsDto
        {
            ExpectedVersionRevision = layout.Versions.Single().Revision + 1,
            Rows = { Row("HEADER", "Header", FinancialStatementRowType.Header, 10) }
        });

        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static FinancialStatementRowInputDto Row(
        string code,
        string label,
        FinancialStatementRowType rowType,
        int displayOrder,
        string? parentCode = null,
        FinancialStatementRowMappingInputDto? mappings = null,
        string? formula = null,
        IReadOnlyCollection<FinancialStatementRowMappingInputDto>? additionalMappings = null)
    {
        var row = new FinancialStatementRowInputDto
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
        if (additionalMappings != null) row.Mappings.AddRange(additionalMappings);
        return row;
    }

    private static FinancialStatementRowMappingInputDto Mapping(
        FinancialStatementRowMappingType mappingType,
        Guid accountId)
        => new()
        {
            MappingType = mappingType,
            AccountId = accountId
        };

    private static FinancialStatementRowMappingInputDto ClassificationMapping(Guid classificationId, bool descendants)
        => new()
        {
            MappingType = FinancialStatementRowMappingType.Classification,
            AccountClassificationId = classificationId,
            IncludeClassificationDescendants = descendants
        };

    private static AccountClassification SeedClassification(
        ApplicationDbContext context, Guid tenantId, Guid bookId, string code, string name,
        AccountType accountType, Guid? parentId = null)
    {
        var classification = new AccountClassification
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = bookId,
            ParentClassificationId = parentId, Code = code, Name = name,
            CoreAccountType = accountType, Status = AccountClassificationStatus.Active,
            IsPostingClassification = parentId.HasValue, DisplayOrder = 10
        };
        context.AccountClassifications.Add(classification);
        return classification;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"financial-layouts-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task CreateSqliteLayoutSchemaAsync(ApplicationDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        var tableNames = new[]
        {
            "Tenants", "AccountingBooks", "Accounts", "AccountAccountingBooks", "AccountClassifications",
            "FinancialStatementLayouts", "FinancialStatementLayoutVersions", "FinancialStatementRows",
            "FinancialStatementRowMappings", "FinancialStatementPublicationAccounts"
        };
        var statements = context.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(statement => tableNames.Any(table => statement.Contains($"CREATE TABLE \"{table}\"", StringComparison.Ordinal)));
        foreach (var statement in statements)
            await context.Database.ExecuteSqlRawAsync(statement.Replace(
                "\"RowVersion\" BLOB NOT NULL", "\"RowVersion\" BLOB NOT NULL DEFAULT X''", StringComparison.Ordinal));
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
    }

    private static FinancialStatementLayoutService CreateService(
        ApplicationDbContext context,
        Guid tenantId,
        Mock<IFinanceAuditService>? auditOverride = null,
        Guid? userId = null,
        string userName = "layout.accountant")
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(service => service.UserId).Returns((userId ?? Guid.NewGuid()).ToString());
        currentUser.SetupGet(service => service.UserName).Returns(userName);
        currentUser.SetupGet(service => service.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(service => service.UserAgent).Returns("layout-tests");

        var audit = auditOverride ?? new Mock<IFinanceAuditService>();
        if (auditOverride == null)
        {
            audit.Setup(service => service.RecordAsync(
                    It.IsAny<FinanceAuditEventDto>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuditLog { Id = Guid.NewGuid(), TenantId = tenantId });
        }

        return new FinancialStatementLayoutService(
            context,
            currentUser.Object,
            audit.Object,
            Mock.Of<ILogger<FinancialStatementLayoutService>>());
    }

    private static async Task PrepareForIndependentPublishAsync(
        ApplicationDbContext context,
        Guid versionId,
        DateTime? effectiveFrom = null)
    {
        var version = await context.FinancialStatementLayoutVersions.SingleAsync(item => item.Id == versionId);
        version.Status = FinancialStatementLayoutVersionStatus.Submitted;
        version.SubmittedAt = DateTime.UtcNow;
        version.SubmittedById = Guid.NewGuid();
        version.SubmittedByName = "layout.maker";
        version.EffectiveFrom = effectiveFrom;
        await context.SaveChangesAsync();
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
            BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active,
            FunctionalCurrencyCode = "GHS",
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

    private sealed class RejectPublicationSnapshotInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<FinancialStatementPublicationAccount>()
                .Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new InvalidOperationException("snapshot write rejected");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
