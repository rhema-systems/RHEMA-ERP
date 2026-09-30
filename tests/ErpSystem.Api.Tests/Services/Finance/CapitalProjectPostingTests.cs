using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CapitalProjectPostingTests
{
    [Fact]
    public async Task Capitalization_reconciles_primary_and_parallel_registers_and_replay_is_idempotent()
    {
        await using var fixture = new Fixture();

        var result = await fixture.Service.CapitalizeProjectAsync(fixture.Project.Id);

        Assert.Equal(ProjectStatus.Completed, result.Status);
        Assert.Equal(10_000m, result.CapitalizedAmount);
        var posting = Assert.Single(fixture.Postings);
        Assert.Equal("BASE", posting.AccountingBookCode);
        Assert.Equal("GHS", posting.FunctionalCurrencyCode);
        Assert.Equal(fixture.SourceJournal.EntryDate.Date, posting.PostingDate.Date);
        Assert.Equal(fixture.SourceJournal.EntryDate.Date, fixture.CapturedAuthorityRequest!.EffectiveDate.Date);
        Assert.Equal(fixture.ProjectAuthorityId, fixture.BoundAuthorityId);
        Assert.True(posting.ReturnExistingOnDuplicate);
        Assert.Equal(10_000m, posting.Lines.Sum(line => line.DebitAmount));
        Assert.Equal(10_000m, posting.Lines.Sum(line => line.CreditAmount));
        Assert.Equal(fixture.AssetAccount.Id,
            Assert.Single(posting.Lines.Where(line => line.DebitAmount > 0m)).AccountId);
        Assert.Equal(fixture.CwcAccount.Id,
            Assert.Single(posting.Lines.Where(line => line.CreditAmount > 0m)).AccountId);
        Assert.Null(fixture.CapturedDimensionInput);
        Assert.NotNull(fixture.InheritedJournalBySourceLine);
        Assert.All(fixture.InheritedJournalBySourceLine!.Values,
            journalId => Assert.Equal(fixture.SourceJournal.Id, journalId));

        var asset = await fixture.Context.FixedAssets.Include(item => item.BookValues).SingleAsync();
        Assert.Equal(FixedAssetStatus.Capitalized, asset.Status);
        Assert.Equal(fixture.Project.Id, asset.SourceDocumentId);
        Assert.Equal(fixture.PostedJournalEntryId, asset.JournalEntryId);
        Assert.Equal(fixture.PostingEventId, asset.PostingEventId);
        Assert.Equal(2, asset.BookValues.Count);
        var primaryValue = Assert.Single(asset.BookValues, value => value.AccountingBookId == fixture.Book.Id);
        Assert.Equal(10_000m, primaryValue.AcquisitionCost);
        Assert.Equal(10_000m, primaryValue.NetBookValue);
        Assert.Equal(fixture.PostedJournalEntryId, primaryValue.CapitalizationJournalEntryId);
        var parallelValue = Assert.Single(asset.BookValues,
            value => value.AccountingBookId == fixture.ParallelBook.Id);
        Assert.Equal(800m, parallelValue.AcquisitionCost);
        Assert.Equal(800m, parallelValue.NetBookValue);
        Assert.Equal(fixture.ParallelPostedJournalEntryId, parallelValue.CapitalizationJournalEntryId);
        Assert.DoesNotContain(asset.BookValues, value => value.AccountingBookId == fixture.DeltaBook.Id);
        var assetTransactions = await fixture.Context.AssetTransactions.OrderBy(item => item.Amount).ToListAsync();
        Assert.Equal(new[] { 800m, 10_000m }, assetTransactions.Select(item => item.Amount).ToArray());

        await fixture.Service.CapitalizeProjectAsync(fixture.Project.Id);
        Assert.Single(fixture.Postings);
        Assert.Equal(1, fixture.CreatedAssetCount);
        Assert.Single(fixture.Context.FixedAssets);
        Assert.Equal(2, await fixture.Context.AssetTransactions.CountAsync());
    }

    [Fact]
    public async Task Capitalization_submission_rejects_any_cost_without_exact_retained_authority()
    {
        await using var fixture = new Fixture();
        fixture.Project.Status = ProjectStatus.InProgress;
        fixture.Project.CapitalizationWorkflowInstanceId = null;
        fixture.Project.CapitalizationSourceBookAuthorityId = null;
        fixture.Project.CapitalizationEvidenceHash = null;
        var cost = Assert.Single(fixture.Project.CostLines);
        cost.SourceBookAuthorityId = null;
        cost.SourceFinancePostingEventId = null;
        await fixture.Context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.SubmitForCapitalizationApprovalAsync(fixture.Project.Id));

        Assert.Contains("Every capital-project cost", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Capitalization_rejects_stale_source_book_before_asset_or_gl_mutation()
    {
        await using var fixture = new Fixture();
        fixture.SourceJournal.BookClassification = "IFRS";
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CapitalizeProjectAsync(fixture.Project.Id));

        Assert.Empty(fixture.Postings);
        Assert.Equal(0, fixture.CreatedAssetCount);
        Assert.Empty(fixture.Context.FixedAssets);
        var project = await fixture.Context.CapitalProjects.SingleAsync(item => item.Id == fixture.Project.Id);
        Assert.Equal(ProjectStatus.Approved, project.Status);
    }

    [Fact]
    public async Task Nonmanual_source_uses_primary_journal_when_parallel_replica_shares_document_identity()
    {
        await using var fixture = new Fixture();
        fixture.UseVendorInvoiceSource(includeParallelReplica: true);
        fixture.SourceJournal.PostingDate = new DateTime(2027, 1, 1);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CapitalizeProjectAsync(fixture.Project.Id);

        Assert.Equal(ProjectStatus.Completed, result.Status);
        Assert.Single(fixture.Postings);
        Assert.Single(fixture.Context.FixedAssets);
    }

    [Fact]
    public async Task Nonmanual_source_rejects_inconsistent_document_type()
    {
        await using var fixture = new Fixture();
        fixture.UseVendorInvoiceSource(includeParallelReplica: false);
        fixture.SourceJournal.SourceDocumentType = "ExpenseRecord";
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CapitalizeProjectAsync(fixture.Project.Id));

        Assert.Empty(fixture.Postings);
        Assert.Empty(fixture.Context.FixedAssets);
    }

    [Fact]
    public async Task Capitalization_rejects_caller_dimension_override_before_asset_or_gl_mutation()
    {
        await using var fixture = new Fixture();
        var request = new CapitalizeCapitalProjectDto
        {
            FinanceDimensions = new FinanceSourceDocumentDimensionInputDto
            {
                DefaultDimensions = new[]
                {
                    new FinancePostingDimensionValueDto
                    {
                        DimensionCode = "COST_CENTER",
                        ValueCode = "OVERRIDE"
                    }
                },
                ApplyDefaultToEligibleLines = true
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CapitalizeProjectAsync(fixture.Project.Id, request));

        Assert.Empty(fixture.Postings);
        Assert.Equal(0, fixture.CreatedAssetCount);
        Assert.Empty(fixture.Context.FixedAssets);
    }

    [Fact]
    public async Task Capitalization_rejects_journal_whose_dimension_loader_would_select_non_cwc_debit()
    {
        await using var fixture = new Fixture();
        fixture.AddEarlierDimensionedNonCwcDebit();
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CapitalizeProjectAsync(fixture.Project.Id));

        Assert.Empty(fixture.Postings);
        Assert.Equal(0, fixture.CreatedAssetCount);
        Assert.Empty(fixture.Context.FixedAssets);
    }

    [Fact]
    public async Task Missing_capitalization_parallel_replica_rolls_back_project_and_new_asset_register()
    {
        await using var fixture = new Fixture { EmitCapitalizationParallelReplica = false };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CapitalizeProjectAsync(fixture.Project.Id));

        var project = await fixture.Context.CapitalProjects
            .Include(item => item.SettlementRules)
            .SingleAsync(item => item.Id == fixture.Project.Id);
        Assert.Equal(ProjectStatus.Approved, project.Status);
        Assert.Equal(0m, project.CapitalizedAmount);
        Assert.All(project.SettlementRules, rule =>
        {
            Assert.Null(rule.ResultingFixedAssetId);
            Assert.Equal(0m, rule.AllocatedAmount);
        });
        Assert.Empty(fixture.Context.FixedAssets);
        Assert.Empty(fixture.Context.FixedAssetBookValues);
        Assert.Empty(fixture.Context.AssetTransactions);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private const decimal ParallelRate = 0.08m;
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly Account _offsetAccount;
        private readonly Guid _dimensionSetId = Guid.NewGuid();
        private readonly Guid _dimensionSnapshotId = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public CapitalProjectService Service { get; }
        public AccountingBook Book { get; }
        public AccountingBook ParallelBook { get; }
        public AccountingBook DeltaBook { get; }
        public Account AssetAccount { get; }
        public Account CwcAccount { get; }
        public CapitalProject Project { get; }
        public JournalEntry SourceJournal { get; }
        public Guid PostedJournalEntryId { get; } = Guid.NewGuid();
        public Guid ParallelPostedJournalEntryId { get; } = Guid.NewGuid();
        public Guid PostingEventId { get; } = Guid.NewGuid();
        public Guid SourcePostingEventId { get; } = Guid.NewGuid();
        public Guid SourceAuthorityId { get; } = Guid.NewGuid();
        public Guid ProjectAuthorityId { get; } = Guid.NewGuid();
        public Guid WorkflowInstanceId { get; } = Guid.NewGuid();
        public Guid ParallelPostingEventId { get; } = Guid.NewGuid();
        public List<FinancePostingRequestV2Dto> Postings { get; } = [];
        public FinanceSourceDocumentDimensionInputDto? CapturedDimensionInput { get; private set; }
        public IReadOnlyDictionary<Guid, Guid>? InheritedJournalBySourceLine { get; private set; }
        public int CreatedAssetCount { get; private set; }
        public bool EmitCapitalizationParallelReplica { get; set; } = true;
        public FinanceSourceBookAuthorityFreezeRequest? CapturedAuthorityRequest { get; private set; }
        public Guid? BoundAuthorityId { get; private set; }

        public Fixture()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            var user = new Mock<ICurrentUserService>();
            user.SetReturnsDefault<Guid>(_tenantId);
            user.SetReturnsDefault<Guid?>(_tenantId);
            user.SetReturnsDefault(_tenantId.ToString());
            user.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());

            Book = new AccountingBook
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, Code = "BASE", Name = "Primary base book",
                BookType = AccountingBookType.PrimaryFull,
                LifecycleStatus = AccountingBookLifecycleStatus.Active,
                FunctionalCurrencyCode = "GHS",
                IsDefault = true, IsActive = true, AllowsPosting = true
            };
            ParallelBook = new AccountingBook
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, Code = "USD_PARALLEL", Name = "USD parallel",
                BookType = AccountingBookType.ParallelFull,
                LifecycleStatus = AccountingBookLifecycleStatus.Active,
                FunctionalCurrencyCode = "USD", BaseAccountingBookId = Book.Id,
                IsActive = true, AllowsPosting = true
            };
            DeltaBook = new AccountingBook
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, Code = "IFRS_ADJUSTMENTS", Name = "Adjustments",
                BookType = AccountingBookType.Delta,
                LifecycleStatus = AccountingBookLifecycleStatus.Active,
                FunctionalCurrencyCode = "GHS", BaseAccountingBookId = Book.Id,
                IsActive = true, AllowsPosting = true
            };
            AssetAccount = NewAccount("1600", "Fixed asset");
            CwcAccount = NewAccount("1590", "Capital work in progress");
            _offsetAccount = NewAccount("2100", "Source offset");
            var category = new FixedAssetCategory
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Buildings", Code = "BLD",
                AssetAccountId = AssetAccount.Id, AucAccountId = CwcAccount.Id,
                AccumulatedDepreciationAccountId = Guid.NewGuid(),
                DepreciationExpenseAccountId = Guid.NewGuid(),
                DefaultUsefulLifeMonths = 120,
                DefaultResidualValuePercent = 5m,
                DefaultMethod = DepreciationMethod.StraightLine
            };
            SourceJournal = new JournalEntry
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, JournalEntryNumber = "JE-CWC-10000",
                JournalType = "System Generated", EntryDate = new DateTime(2026, 9, 20),
                PostingDate = new DateTime(2026, 9, 20), Description = "Posted CWC source",
                ReferenceNumber = "CWC-10000", SourceModule = "GL", AccountingBookId = Book.Id,
                AccountingBook = Book, BookClassification = Book.Code, FiscalPeriodId = Guid.NewGuid(),
                PostingStatus = "Posted", IsBalanced = true,
                TotalDebitAmount = 10_000m, TotalCreditAmount = 10_000m
            };
            var cwcLine = NewTransaction(SourceJournal, Book, CwcAccount.Id, 10_000m, 0m, 1);
            cwcLine.FinanceDimensionSetId = _dimensionSetId;
            cwcLine.FinanceDimensionSnapshotId = _dimensionSnapshotId;
            SourceJournal.Transactions.Add(cwcLine);
            SourceJournal.Transactions.Add(NewTransaction(SourceJournal, Book, _offsetAccount.Id, 0m, 10_000m, 2));
            Project = new CapitalProject
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, ProjectCode = "CP-10000", Name = "Demo building",
                StartDate = new DateTime(2026, 9, 1), TotalBudgetAmount = 10_000m,
                TotalAccumulatedCost = 10_000m, Status = ProjectStatus.Approved,
                RowVersion = Array.Empty<byte>()
            };
            Project.CostLines.Add(new ProjectCostLine
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, CapitalProjectId = Project.Id,
                SourceDocumentType = ProjectCostSourceType.ManualJournal,
                SourceDocumentId = SourceJournal.Id, SourceDocumentReference = SourceJournal.ReferenceNumber,
                SourceFinancePostingEventId = SourcePostingEventId,
                SourceBookAuthorityId = SourceAuthorityId,
                Amount = 10_000m, TransactionDate = SourceJournal.EntryDate
            });
            Project.SettlementRules.Add(new ProjectSettlementRule
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, CapitalProjectId = Project.Id,
                TargetFixedAssetCategoryId = category.Id, TargetFixedAssetCategory = category,
                ProposedAssetName = "Demo Building", AllocationPercentage = 100m
            });
            Project.CapitalizationWorkflowInstanceId = WorkflowInstanceId;
            Project.CapitalizationSourceBookAuthorityId = ProjectAuthorityId;
            Project.CapitalizationEvidenceHash = ProjectEvidenceHash(Project);
            var sourceEvent = new FinancePostingEvent
            {
                Id = SourcePostingEventId, TenantId = _tenantId, SourceModule = "GL",
                OriginModuleCode = FinanceModuleLockCatalog.Finance,
                SourceDocumentType = ProjectCostSourceType.ManualJournal.ToString(),
                SourceDocumentId = SourceJournal.Id, SourceDocumentReference = SourceJournal.ReferenceNumber,
                PostingAction = "Post", PostingStatus = "Posted", PostingDate = SourceJournal.EntryDate,
                JournalEntryId = SourceJournal.Id, AccountingBookId = Book.Id,
                BookClassification = Book.Code, FunctionalCurrencyCode = "GHS",
                TotalDebitAmount = 10_000m, TotalCreditAmount = 10_000m
            };
            var projectAuthority = new FinanceSourceBookAuthority
            {
                Id = ProjectAuthorityId, TenantId = _tenantId,
                OriginModuleCode = FinanceModuleLockCatalog.Finance,
                SourceDocumentType = "CAPITALPROJECT", SourceDocumentId = Project.Id,
                PostingAction = "Capitalize", AuthorityVersion = 1,
                SourceWorkflowInstanceId = WorkflowInstanceId, SourceWorkflowEntityType = "CapitalProject",
                FreezeStage = FinanceSourceBookAuthorityFreezeStages.Submitted,
                EffectiveDate = SourceJournal.EntryDate.Date, AccountingBookId = Book.Id,
                AccountingBookCode = Book.Code, FunctionalCurrencyCode = "GHS",
                TransactionCurrencyCode = "GHS", SelectionBasis = FinanceSourceBookAuthoritySelectionBases.InheritedOriginal,
                AuthorityFingerprint = new string('A', 64), FrozenAtUtc = DateTime.UtcNow
            };
            Context.AddRange(Book, ParallelBook, DeltaBook, AssetAccount, CwcAccount, _offsetAccount,
                category, SourceJournal, sourceEvent, projectAuthority, Project,
                new FinanceSettings { Id = Guid.NewGuid(), TenantId = _tenantId, BaseCurrency = "GHS" });
            Context.SaveChanges();

            var fixedAssets = new Mock<IFixedAssetService>();
            fixedAssets.Setup(service => service.GenerateAssetCodeAsync(category.Id))
                .ReturnsAsync("FA-BLD-2026-0001");
            fixedAssets.Setup(service => service.CreateAsync(It.IsAny<CreateFixedAssetDto>()))
                .ReturnsAsync((CreateFixedAssetDto dto) => CreateAsset(dto));
            var engine = new Mock<IFinancePostingEngine>();
            engine.Setup(service => service.PostAsync(
                    It.IsAny<FinancePostingRequestV2Dto>(),
                    It.IsAny<FinancePostingProducerContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((FinancePostingRequestV2Dto request,
                    FinancePostingProducerContext _, CancellationToken _) => RecordPosting(request));
            var dimensions = new Mock<IFixedAssetDimensionService>();
            dimensions.Setup(service => service.SynchronizeAsync(
                    It.IsAny<FinancePostingProducerContext>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                    It.IsAny<IReadOnlyList<FinancePostingLineDto>>(),
                    It.IsAny<FinanceSourceDocumentDimensionInputDto?>(),
                    It.IsAny<IReadOnlyDictionary<Guid, Guid>?>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback((FinancePostingProducerContext _, Guid _, DateTime _,
                    IReadOnlyList<FinancePostingLineDto> _, FinanceSourceDocumentDimensionInputDto? input,
                    IReadOnlyDictionary<Guid, Guid>? inherited, string _, CancellationToken _) =>
                {
                    CapturedDimensionInput = input;
                    InheritedJournalBySourceLine = inherited;
                })
                .ReturnsAsync(new FinanceSourceDocumentDimensionDto());
            dimensions.Setup(service => service.ValidateFreezeAndApplyAsync(
                    It.IsAny<FinancePostingProducerContext>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                    It.IsAny<IList<FinancePostingLineDto>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FinanceSourceDocumentDimensionDto());
            dimensions.Setup(service => service.GetAsync(
                    It.IsAny<FinancePostingProducerContext>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                    It.IsAny<IReadOnlyList<FinancePostingLineDto>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FinanceSourceDocumentDimensionDto());
            var sourceAuthority = new Mock<IFinanceSourceBookAuthorityService>();
            var frozen = AuthorityResult(ProjectAuthorityId, Project.Id, WorkflowInstanceId, SourceJournal.EntryDate.Date);
            var origin = AuthorityResult(SourceAuthorityId, SourceJournal.Id, null, SourceJournal.EntryDate.Date,
                SourcePostingEventId, SourceJournal.Id);
            sourceAuthority.Setup(service => service.RequireForPostingAsync(
                    It.IsAny<FinanceSourceBookAuthorityFreezeRequest>(), It.IsAny<CancellationToken>()))
                .Callback((FinanceSourceBookAuthorityFreezeRequest request, CancellationToken _) => CapturedAuthorityRequest = request)
                .ReturnsAsync(frozen);
            sourceAuthority.Setup(service => service.RequireBoundOriginalAsync(
                    SourceAuthorityId, It.IsAny<CancellationToken>())).ReturnsAsync(origin);
            sourceAuthority.Setup(service => service.BindOriginalPostingAsync(
                    ProjectAuthorityId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Callback((Guid authorityId, Guid _, Guid _, CancellationToken _) => BoundAuthorityId = authorityId)
                .ReturnsAsync(frozen);
            Service = new CapitalProjectService(
                Context, user.Object, fixedAssets.Object, engine.Object, dimensions.Object,
                new Mock<IWorkflowService>().Object, sourceAuthority.Object);
        }

        private FinanceSourceBookAuthorityResult AuthorityResult(
            Guid authorityId, Guid sourceId, Guid? workflowId, DateTime effectiveDate,
            Guid? postingEventId = null, Guid? journalId = null) => new(
                authorityId, 1, FinanceModuleLockCatalog.Finance, "CAPITALPROJECT", sourceId,
                "Capitalize", effectiveDate, FinanceSourceBookAuthorityFreezeStages.Authorized,
                workflowId, workflowId.HasValue ? "CapitalProject" : string.Empty,
                Book.Id, Book.Code, "GHS", "GHS", FinanceSourceBookAuthoritySelectionBases.InheritedOriginal,
                new string('A', 64), postingEventId, journalId);

        private static string ProjectEvidenceHash(CapitalProject project)
        {
            var canonical = string.Join("|", project.CostLines.OrderBy(item => item.Id).Select(item =>
                    $"C:{item.Id:N}:{item.SourceBookAuthorityId:N}:{item.SourceFinancePostingEventId:N}:{item.Amount:0.00}")) +
                "|" + string.Join("|", project.SettlementRules.OrderBy(item => item.Id).Select(item =>
                    $"R:{item.Id:N}:{item.TargetFixedAssetCategoryId:N}:{item.AllocationPercentage:0.00}:{item.ProposedAssetName}"));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        }

        public void UseVendorInvoiceSource(bool includeParallelReplica)
        {
            var sourceDocumentId = Guid.NewGuid();
            var cost = Project.CostLines.Single();
            cost.SourceDocumentType = ProjectCostSourceType.VendorInvoice;
            cost.SourceDocumentId = sourceDocumentId;
            SourceJournal.SourceDocumentId = sourceDocumentId;
            SourceJournal.SourceDocumentType = ProjectCostSourceType.VendorInvoice.ToString();
            if (!includeParallelReplica)
                return;

            var replica = new JournalEntry
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, JournalEntryNumber = "JE-CWC-10000-USD",
                JournalType = "System Generated", EntryDate = SourceJournal.EntryDate,
                PostingDate = DateTime.UtcNow, Description = "Posted CWC source replica",
                ReferenceNumber = SourceJournal.ReferenceNumber, SourceModule = SourceJournal.SourceModule,
                SourceDocumentId = sourceDocumentId,
                SourceDocumentType = ProjectCostSourceType.VendorInvoice.ToString(),
                AccountingBookId = ParallelBook.Id, AccountingBook = ParallelBook,
                BookClassification = ParallelBook.Code, FiscalPeriodId = Guid.NewGuid(),
                PostingStatus = "Posted", IsBalanced = true,
                TotalDebitAmount = 800m, TotalCreditAmount = 800m,
                ReplicatedFromJournalEntryId = SourceJournal.Id
            };
            replica.Transactions.Add(NewTransaction(replica, ParallelBook, CwcAccount.Id, 800m, 0m, 1));
            replica.Transactions.Add(NewTransaction(replica, ParallelBook, _offsetAccount.Id, 0m, 800m, 2));
            Context.JournalEntries.Add(replica);
        }

        public void AddEarlierDimensionedNonCwcDebit()
        {
            var otherSetId = Guid.NewGuid();
            var otherSnapshotId = Guid.NewGuid();
            var debit = NewTransaction(SourceJournal, Book, _offsetAccount.Id, 1m, 0m, 0);
            debit.FinanceDimensionSetId = otherSetId;
            debit.FinanceDimensionSnapshotId = otherSnapshotId;
            Context.Set<AccountTransaction>().AddRange(
                debit,
                NewTransaction(SourceJournal, Book, _offsetAccount.Id, 0m, 1m, 3));
        }

        private FixedAssetDto CreateAsset(CreateFixedAssetDto dto)
        {
            CreatedAssetCount++;
            var asset = new FixedAsset
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, AssetCode = dto.AssetCode, Name = dto.Name,
                Description = dto.Description, FixedAssetCategoryId = dto.FixedAssetCategoryId,
                PurchaseDate = dto.PurchaseDate, PurchasePrice = dto.PurchasePrice,
                AcquisitionCost = dto.AcquisitionCost ?? dto.PurchasePrice,
                NetBookValue = dto.AcquisitionCost ?? dto.PurchasePrice,
                DepreciationMethod = dto.DepreciationMethod, UsefulLifeMonths = dto.UsefulLifeMonths,
                ResidualValue = dto.ResidualValue, Status = FixedAssetStatus.Draft
            };
            foreach (var book in new[] { Book, ParallelBook, DeltaBook })
            {
                asset.BookValues.Add(new FixedAssetBookValue
                {
                    Id = Guid.NewGuid(), TenantId = _tenantId, FixedAssetId = asset.Id,
                    AccountingBookId = book.Id, AccountingBook = book, BookClassification = book.Code,
                    AcquisitionCost = asset.AcquisitionCost, NetBookValue = asset.AcquisitionCost,
                    ResidualValue = asset.ResidualValue,
                    UsefulLifeMonths = asset.UsefulLifeMonths,
                    RemainingUsefulLifeMonths = asset.UsefulLifeMonths,
                    DepreciationMethod = asset.DepreciationMethod
                });
            }
            Context.FixedAssets.Add(asset);
            return new FixedAssetDto { Id = asset.Id, AssetCode = asset.AssetCode, Name = asset.Name };
        }

        private FinancePostingResultDto RecordPosting(FinancePostingRequestV2Dto request)
        {
            Postings.Add(request);
            AddRepresentation(request, Book, PostedJournalEntryId, PostingEventId, 1m, replicatedFrom: null);
            if (EmitCapitalizationParallelReplica)
                AddRepresentation(request, ParallelBook, ParallelPostedJournalEntryId,
                    ParallelPostingEventId, ParallelRate, PostedJournalEntryId);
            return new FinancePostingResultDto
            {
                JournalEntryId = PostedJournalEntryId,
                PostingEventId = PostingEventId,
                PostingDate = request.PostingDate,
                PostingStatus = "Posted",
                FunctionalCurrencyCode = request.FunctionalCurrencyCode
            };
        }

        private void AddRepresentation(
            FinancePostingRequestV2Dto request,
            AccountingBook book,
            Guid journalId,
            Guid eventId,
            decimal rate,
            Guid? replicatedFrom)
        {
            var journal = new JournalEntry
            {
                Id = journalId, TenantId = _tenantId,
                JournalEntryNumber = replicatedFrom.HasValue ? "CAP-USD" : "CAP-BASE",
                JournalType = request.JournalType, EntryDate = request.PostingDate,
                PostingDate = DateTime.UtcNow, Description = request.Description,
                ReferenceNumber = request.SourceDocumentReference, SourceModule = request.SourceModule,
                OriginModuleCode = request.OriginModuleCode, SourceDocumentId = request.SourceDocumentId,
                SourceDocumentType = request.SourceDocumentType, AccountingBookId = book.Id,
                AccountingBook = book, BookClassification = book.Code, FiscalPeriodId = Guid.NewGuid(),
                PostingStatus = "Posted", IsBalanced = true,
                TotalDebitAmount = Math.Round(request.Lines.Sum(line => line.DebitAmount) * rate, 2),
                TotalCreditAmount = Math.Round(request.Lines.Sum(line => line.CreditAmount) * rate, 2),
                ReplicatedFromJournalEntryId = replicatedFrom
            };
            foreach (var source in request.Lines)
            {
                journal.Transactions.Add(new AccountTransaction
                {
                    Id = Guid.NewGuid(), TenantId = _tenantId, JournalEntryId = journal.Id,
                    AccountId = source.AccountId, AccountingBookId = book.Id,
                    BookClassification = book.Code, FiscalPeriodId = journal.FiscalPeriodId,
                    TransactionDate = request.PostingDate, PostedDate = journal.PostingDate,
                    PostingStatus = "Posted", FunctionalCurrencyCode = book.FunctionalCurrencyCode!,
                    TransactionCurrency = source.TransactionCurrency,
                    DebitAmount = Math.Round(source.DebitAmount * rate, 2),
                    CreditAmount = Math.Round(source.CreditAmount * rate, 2),
                    TransactionDebitAmount = source.DebitAmount,
                    TransactionCreditAmount = source.CreditAmount,
                    SourceDocumentId = request.SourceDocumentId,
                    SourceDocumentType = request.SourceDocumentType,
                    SourceDocumentLineId = source.SourceDocumentLineId,
                    SourceReferenceNumber = source.SourceReferenceNumber,
                    LineNumber = source.LineNumber ?? 0
                });
            }
            Context.JournalEntries.Add(journal);
            Context.FinancePostingEvents.Add(new FinancePostingEvent
            {
                Id = eventId, TenantId = _tenantId, SourceModule = request.SourceModule,
                OriginModuleCode = request.OriginModuleCode,
                SourceDocumentType = request.SourceDocumentType,
                SourceDocumentId = request.SourceDocumentId,
                SourceDocumentReference = request.SourceDocumentReference,
                PostingAction = request.PostingAction,
                PostingStatus = "Posted", PostingDate = request.PostingDate,
                JournalEntryId = journal.Id, AccountingBookId = book.Id,
                BookClassification = book.Code, FunctionalCurrencyCode = book.FunctionalCurrencyCode!,
                TotalDebitAmount = journal.TotalDebitAmount,
                TotalCreditAmount = journal.TotalCreditAmount
            });
        }

        private Account NewAccount(string code, string name) => new()
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, AccountCode = code, AccountNumber = code,
            AccountName = name, AccountType = AccountType.Asset, Status = AccountStatus.Active,
            CurrencyCode = "GHS", AllowDirectPosting = true
        };

        private AccountTransaction NewTransaction(
            JournalEntry journal, AccountingBook book, Guid accountId,
            decimal debit, decimal credit, int lineNumber) => new()
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, JournalEntryId = journal.Id,
            AccountId = accountId, AccountingBookId = book.Id, BookClassification = book.Code,
            FiscalPeriodId = journal.FiscalPeriodId, TransactionDate = journal.EntryDate,
            PostedDate = journal.PostingDate, PostingStatus = "Posted",
            FunctionalCurrencyCode = book.FunctionalCurrencyCode!,
            DebitAmount = debit, CreditAmount = credit, LineNumber = lineNumber
        };

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
