using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class LeaseActivationGovernanceTests
{
    [Fact]
    public async Task Submit_then_independent_approval_posts_and_materializes_only_full_book_representations()
    {
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var checkerId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"lease-activation-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var seeded = await SeedAsync(db, tenantId);
        var workflow = Workflow(workflowId);
        var dimensions = Dimensions();
        var assets = new Mock<IFixedAssetService>();
        assets.Setup(item => item.GenerateAssetCodeAsync(seeded.Category.Id)).ReturnsAsync("ROU-0001");
        var posting = PostingEngine(db, tenantId, seeded);
        var maker = Service(db, tenantId, makerId, workflow.Object, dimensions.Object, assets.Object, posting.Object);

        var submitted = await maker.ActivateLeaseAsync(seeded.Lease.Id);

        submitted.Status.Should().Be(LeaseStatus.PendingApproval);
        submitted.ActivationSubmittedByUserId.Should().Be(makerId);
        submitted.ActivationWorkflowInstanceId.Should().Be(workflowId);
        (await db.FixedAssets.CountAsync()).Should().Be(0);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);

        await SeedCompletedWorkflowAsync(db, tenantId, seeded.Lease.Id, workflowId, makerId, checkerId);
        var makerApproval = () => maker.CompleteApprovedActivationAsync(seeded.Lease.Id, makerId);
        await makerApproval.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("LEASE_ACTIVATION_SOD:*");
        (await db.FixedAssets.CountAsync()).Should().Be(0);

        var checker = Service(db, tenantId, checkerId, workflow.Object, dimensions.Object, assets.Object, posting.Object);
        await checker.CompleteApprovedActivationAsync(seeded.Lease.Id, checkerId);

        var lease = await db.LeaseContracts.AsNoTracking().SingleAsync(item => item.Id == seeded.Lease.Id);
        lease.Status.Should().Be(LeaseStatus.Active);
        lease.ActivationApprovedByUserId.Should().Be(checkerId);
        lease.RecognitionJournalEntryId.Should().NotBeNull();
        var asset = await db.FixedAssets.AsNoTracking().SingleAsync(item => item.Id == lease.RouAssetId);
        asset.Status.Should().Be(FixedAssetStatus.Active);
        asset.PostingEventId.Should().Be(lease.RecognitionPostingEventId);
        asset.JournalEntryId.Should().Be(lease.RecognitionJournalEntryId);
        asset.CapitalizationApprovalWorkflowInstanceId.Should().Be(workflowId);
        var values = await db.FixedAssetBookValues.AsNoTracking()
            .Where(item => item.FixedAssetId == asset.Id).OrderBy(item => item.BookClassification).ToListAsync();
        values.Select(item => item.AccountingBookId).Should().BeEquivalentTo(
            [seeded.Primary.Id, seeded.Parallel.Id]);
        values.Should().NotContain(item => item.AccountingBookId == seeded.Delta.Id);
        var events = await db.FinancePostingEvents.AsNoTracking()
            .Where(item => item.SourceDocumentType == "LeaseRecognition" &&
                item.SourceDocumentId == seeded.Lease.Id && item.PostingStatus == "Posted")
            .ToListAsync();
        events.Should().HaveCount(2);
        events.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        foreach (var value in values)
        {
            value.CapitalizationJournalEntryId.Should().NotBeNull();
            value.CapitalizationPostingEventId.Should().NotBeNull();
            value.SourceDocumentType.Should().Be("LeaseRecognition");
            var postingEvent = events.Single(item => item.Id == value.CapitalizationPostingEventId);
            postingEvent.AccountingBookId.Should().Be(value.AccountingBookId);
            postingEvent.BookClassification.Should().Be(value.BookClassification);
            postingEvent.FunctionalCurrencyCode.Should().Be("GHS");
            postingEvent.JournalEntryId.Should().Be(value.CapitalizationJournalEntryId);
            postingEvent.SourceDocumentId.Should().Be(seeded.Lease.Id);
        }
        values.Single(item => item.AccountingBookId == seeded.Primary.Id)
            .CapitalizationPostingEventId.Should().Be(lease.RecognitionPostingEventId);
        values.Single(item => item.AccountingBookId == seeded.Parallel.Id)
            .CapitalizationPostingEventId!.Value.Should().NotBe(lease.RecognitionPostingEventId!.Value);
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("Duplicate")]
    [InlineData("Mismatch")]
    public async Task Replica_event_evidence_faults_fail_closed_before_asset_materialization(string eventFault)
    {
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var checkerId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"lease-activation-event-fault-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var seeded = await SeedAsync(db, tenantId);
        var workflow = Workflow(workflowId);
        var dimensions = Dimensions();
        var assets = new Mock<IFixedAssetService>();
        assets.Setup(item => item.GenerateAssetCodeAsync(seeded.Category.Id)).ReturnsAsync("ROU-0001");
        var posting = PostingEngine(db, tenantId, seeded, eventFault);
        var maker = Service(db, tenantId, makerId, workflow.Object, dimensions.Object, assets.Object, posting.Object);
        await maker.ActivateLeaseAsync(seeded.Lease.Id);
        await SeedCompletedWorkflowAsync(db, tenantId, seeded.Lease.Id, workflowId, makerId, checkerId);
        var checker = Service(db, tenantId, checkerId, workflow.Object, dimensions.Object, assets.Object, posting.Object);

        var approve = () => checker.CompleteApprovedActivationAsync(seeded.Lease.Id, checkerId);

        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*posting*event*");
        (await db.FixedAssets.CountAsync()).Should().Be(0);
        (await db.LeaseContracts.AsNoTracking().SingleAsync(item => item.Id == seeded.Lease.Id)).Status
            .Should().Be(LeaseStatus.PendingApproval);
    }

    private static LeaseAccountingService Service(
        ApplicationDbContext db,
        Guid tenantId,
        Guid userId,
        IWorkflowService workflow,
        IFixedAssetDimensionService dimensions,
        IFixedAssetService assets,
        IFinancePostingEngine posting)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        user.SetupGet(item => item.UserId).Returns(userId.ToString());
        user.SetupGet(item => item.UserName).Returns($"user-{userId:N}");
        user.SetupGet(item => item.IsAuthenticated).Returns(true);
        return new LeaseAccountingService(db, user.Object, assets, Mock.Of<IDocumentNumberingService>(),
            posting, dimensions, Mock.Of<IVendorInvoiceService>(), workflow);
    }

    private static Mock<IWorkflowService> Workflow(Guid workflowId)
    {
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("LeaseContract")).ReturnsAsync(true);
        workflow.Setup(item => item.StartApprovalWorkflowAsync("LeaseContract", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = workflowId
            });
        return workflow;
    }

    private static Mock<IFixedAssetDimensionService> Dimensions()
    {
        var dimensions = new Mock<IFixedAssetDimensionService>();
        dimensions.Setup(item => item.SynchronizeAsync(
                It.IsAny<FinancePostingProducerContext>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyList<FinancePostingLineDto>>(),
                It.IsAny<FinanceSourceDocumentDimensionInputDto?>(),
                It.IsAny<IReadOnlyDictionary<Guid, Guid>?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceSourceDocumentDimensionDto());
        dimensions.Setup(item => item.ValidateFreezeAndApplyAsync(
                It.IsAny<FinancePostingProducerContext>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                It.IsAny<IList<FinancePostingLineDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceSourceDocumentDimensionDto());
        return dimensions;
    }

    private static Mock<IFinancePostingEngine> PostingEngine(
        ApplicationDbContext db,
        Guid tenantId,
        Seeded seeded,
        string? eventFault = null)
    {
        var engine = new Mock<IFinancePostingEngine>();
        engine.Setup(item => item.PostAsync(
                It.IsAny<FinancePostingRequestV2Dto>(),
                It.IsAny<FinancePostingProducerContext>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (FinancePostingRequestV2Dto request, FinancePostingProducerContext _, CancellationToken ct) =>
            {
                var primaryJournalId = Guid.NewGuid();
                var postingEventId = Guid.NewGuid();
                FinancePostingEvent CreateEvent(
                    AccountingBook book,
                    JournalEntry journal,
                    Guid eventId,
                    bool mismatch = false) => new()
                {
                    Id = eventId,
                    TenantId = tenantId,
                    SourceModule = request.SourceModule,
                    OriginModuleCode = request.OriginModuleCode,
                    SourceDocumentType = request.SourceDocumentType,
                    SourceDocumentId = request.SourceDocumentId,
                    PostingAction = "Post",
                    PostingStatus = "Posted",
                    PostingDate = request.PostingDate,
                    PostedAt = DateTime.UtcNow,
                    JournalEntryId = journal.Id,
                    AccountingBookId = book.Id,
                    BookClassification = mismatch ? "WRONG_BOOK" : book.Code,
                    FunctionalCurrencyCode = book.FunctionalCurrencyCode!,
                    TotalDebitAmount = request.Lines.Sum(line => line.DebitAmount),
                    TotalCreditAmount = request.Lines.Sum(line => line.CreditAmount)
                };
                foreach (var book in new[] { seeded.Primary, seeded.Parallel })
                {
                    var journal = new JournalEntry
                    {
                        Id = book.Id == seeded.Primary.Id ? primaryJournalId : Guid.NewGuid(),
                        TenantId = tenantId,
                        JournalEntryNumber = $"JE-{book.Code}-{Guid.NewGuid():N}",
                        JournalType = "System Generated",
                        EntryDate = request.PostingDate,
                        PostingDate = request.PostingDate,
                        Description = request.Description,
                        SourceModule = request.SourceModule,
                        SourceDocumentType = request.SourceDocumentType,
                        SourceDocumentId = request.SourceDocumentId,
                        TotalDebitAmount = request.Lines.Sum(line => line.DebitAmount),
                        TotalCreditAmount = request.Lines.Sum(line => line.CreditAmount),
                        IsBalanced = true,
                        PostingStatus = "Posted",
                        FiscalPeriodId = seeded.Period.Id,
                        AccountingBookId = book.Id,
                        BookClassification = book.Code,
                        ReplicatedFromJournalEntryId = book.Id == seeded.Primary.Id ? null : primaryJournalId
                    };
                    db.JournalEntries.Add(journal);
                    foreach (var line in request.Lines)
                    {
                        db.AccountTransactions.Add(new AccountTransaction
                        {
                            TenantId = tenantId,
                            AccountId = line.AccountId,
                            JournalEntryId = journal.Id,
                            TransactionDate = request.PostingDate,
                            DebitAmount = line.DebitAmount,
                            CreditAmount = line.CreditAmount,
                            FunctionalCurrencyCode = book.FunctionalCurrencyCode!,
                            TransactionCurrency = book.FunctionalCurrencyCode,
                            TransactionDebitAmount = line.DebitAmount,
                            TransactionCreditAmount = line.CreditAmount,
                            SourceModule = request.SourceModule,
                            SourceDocumentType = request.SourceDocumentType,
                            SourceDocumentId = request.SourceDocumentId,
                            SourceDocumentLineId = line.SourceDocumentLineId,
                            PostingStatus = "Posted",
                            FiscalPeriodId = seeded.Period.Id,
                            AccountingBookId = book.Id,
                            BookClassification = book.Code
                        });
                    }
                    var isParallel = book.Id == seeded.Parallel.Id;
                    if (!(isParallel && eventFault == "Missing"))
                    {
                        db.FinancePostingEvents.Add(CreateEvent(
                            book,
                            journal,
                            book.Id == seeded.Primary.Id ? postingEventId : Guid.NewGuid(),
                            mismatch: isParallel && eventFault == "Mismatch"));
                        if (isParallel && eventFault == "Duplicate")
                            db.FinancePostingEvents.Add(CreateEvent(book, journal, Guid.NewGuid()));
                    }
                }
                await db.SaveChangesAsync(ct);
                return new FinancePostingResultDto
                {
                    PostingEventId = postingEventId,
                    JournalEntryId = primaryJournalId,
                    JournalEntryNumber = "JE-LEASE-PRIMARY",
                    PostingStatus = "Posted",
                    TotalDebitAmount = request.Lines.Sum(line => line.DebitAmount),
                    TotalCreditAmount = request.Lines.Sum(line => line.CreditAmount),
                    FunctionalCurrencyCode = "GHS",
                    PostingDate = request.PostingDate,
                    SourceModule = request.SourceModule,
                    OriginModuleCode = request.OriginModuleCode ?? request.SourceModule,
                    SourceDocumentType = request.SourceDocumentType,
                    SourceDocumentId = request.SourceDocumentId,
                    PostingAction = "Post"
                };
            });
        return engine;
    }

    private static async Task SeedCompletedWorkflowAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Guid leaseId,
        Guid workflowId,
        Guid makerId,
        Guid checkerId)
    {
        var entityType = new WorkflowEntityType
        {
            TenantId = tenantId, Code = "LeaseContract", Name = "Lease contract"
        };
        var instance = new WorkflowInstance
        {
            Id = workflowId,
            TenantId = tenantId,
            EntityId = leaseId,
            EntityTypeId = entityType.Id,
            EntityType = entityType,
            WorkflowDefinitionId = Guid.NewGuid(),
            Status = WorkflowInstanceStatus.Completed,
            InitiatedById = makerId,
            CompletedDate = DateTime.UtcNow
        };
        var step = new WorkflowStepInstance
        {
            TenantId = tenantId,
            WorkflowInstanceId = workflowId,
            WorkflowInstance = instance,
            WorkflowStepId = Guid.NewGuid(),
            Status = WorkflowStepInstanceStatus.Completed
        };
        step.Approvals.Add(new WorkflowApproval
        {
            TenantId = tenantId,
            StepInstanceId = step.Id,
            StepInstance = step,
            Status = WorkflowApprovalStatus.Approved,
            ProcessedById = checkerId,
            ProcessedDate = DateTime.UtcNow
        });
        db.WorkflowEntityTypes.Add(entityType);
        db.WorkflowInstances.Add(instance);
        db.WorkflowStepInstances.Add(step);
        await db.SaveChangesAsync();
    }

    private static async Task<Seeded> SeedAsync(ApplicationDbContext db, Guid tenantId)
    {
        var rouAccount = Account(tenantId, "ROU", AccountType.Asset);
        var liability = Account(tenantId, "LEASE-LIABILITY", AccountType.Liability);
        var interest = Account(tenantId, "LEASE-INTEREST", AccountType.Expense);
        var accumulated = Account(tenantId, "ROU-ACCUM", AccountType.Asset);
        var depreciation = Account(tenantId, "ROU-DEPR", AccountType.Expense);
        var category = new FixedAssetCategory
        {
            TenantId = tenantId, Code = "ROU", Name = "Right of use",
            AssetAccountId = rouAccount.Id,
            AccumulatedDepreciationAccountId = accumulated.Id,
            DepreciationExpenseAccountId = depreciation.Id
        };
        var primary = Book(tenantId, "LEASE_PRIMARY", AccountingBookType.PrimaryFull, isDefault: true);
        var parallel = Book(tenantId, "LOCAL_FULL", AccountingBookType.ParallelFull, baseBookId: primary.Id);
        var delta = Book(tenantId, "TAX_DELTA", AccountingBookType.Delta, baseBookId: primary.Id);
        var period = new FiscalPeriod
        {
            TenantId = tenantId, FiscalYearId = Guid.NewGuid(), PeriodName = "September 2026",
            PeriodCode = "2026-09", PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30), PeriodDays = 30, PeriodStatus = "Open", IsOpen = true
        };
        var lessor = new BusinessPartner
        {
            TenantId = tenantId, PartnerCode = "LESSOR-001", PartnerName = "Lease Supplier",
            LegalName = "Lease Supplier Limited", PartnerType = "Supplier", RegistrationStatus = "Approved",
            ApprovalStatus = "Approved", IsActive = true, Currency = "GHS"
        };
        var lease = new LeaseContract
        {
            TenantId = tenantId, ContractNumber = "LEASE-ACT-001", Description = "Head office lease",
            LessorId = lessor.Id, Lessor = lessor, StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2027, 8, 31), MonthlyPaymentAmount = 100m,
            PaymentFrequency = PaymentFrequency.Monthly, AnnualDiscountRate = 0.08m,
            TotalPeriods = 12, PresentValue = 1100m, Status = LeaseStatus.Draft, RowVersion = []
        };
        lease.ScheduleLines.Add(new LeaseScheduleLine
        {
            TenantId = tenantId, LeaseContractId = lease.Id, LeaseContract = lease,
            PeriodNumber = 1, PeriodDate = new DateTime(2026, 9, 30), PaymentAmount = 100m,
            PrincipalReduction = 90m, InterestExpense = 10m, RemainingLiability = 1010m
        });
        db.Tenants.Add(new ErpSystem.Core.Entities.Tenant
        {
            Id = tenantId, Name = "Lease tenant", Code = "LEASE", Status = TenantStatus.Active, BaseCurrency = "GHS"
        });
        db.Accounts.AddRange(rouAccount, liability, interest, accumulated, depreciation);
        db.FixedAssetCategories.Add(category);
        db.AccountingBooks.AddRange(primary, parallel, delta);
        db.FiscalPeriods.Add(period);
        db.BusinessPartners.Add(lessor);
        db.FinanceSettings.Add(new FinanceSettings
        {
            TenantId = tenantId, BaseCurrency = "GHS", LeaseRouAssetAccountId = rouAccount.Id,
            LeaseLiabilityAccountId = liability.Id, LeaseInterestExpenseAccountId = interest.Id
        });
        db.LeaseContracts.Add(lease);
        await db.SaveChangesAsync();
        return new Seeded(lease, category, primary, parallel, delta, period);
    }

    private static AccountingBook Book(
        Guid tenantId, string code, AccountingBookType type, bool isDefault = false, Guid? baseBookId = null) => new()
    {
        TenantId = tenantId, Code = code, Name = code, Purpose = "Test", BookType = type,
        LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
        IsDefault = isDefault, IsActive = true, AllowsPosting = true, BaseAccountingBookId = baseBookId,
        ReplicationStartDate = type == AccountingBookType.ParallelFull ? new DateTime(2026, 1, 1) : null
    };

    private static Account Account(Guid tenantId, string code, AccountType type) => new()
    {
        TenantId = tenantId, AccountCode = code, AccountNumber = code, AccountName = code,
        AccountType = type, CurrencyCode = "GHS", Status = AccountStatus.Active, AllowDirectPosting = true
    };

    private sealed record Seeded(
        LeaseContract Lease,
        FixedAssetCategory Category,
        AccountingBook Primary,
        AccountingBook Parallel,
        AccountingBook Delta,
        FiscalPeriod Period);
}
