using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceAuditFoundationTests
{
    [Fact]
    [Trait("Batch", "FinanceAuditFoundation")]
    [Trait("Category", "AuditTrail")]
    public async Task ManualJournalSubmit_ShouldCreateTenantAwareFinanceAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var service = CreateJournalService(db, tenantId);
        var journal = await service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));

        await service.UpdateApprovalStatusAsync(journal.Id, "Pending Approval", "Pending");

        var audit = await db.AuditLogs.SingleAsync(a => a.Action == FinanceAuditEvents.JournalSubmitted);
        audit.TenantId.Should().Be(tenantId);
        audit.UserId.Should().NotBeEmpty();
        audit.Resource.Should().Be("Finance.JournalEntry");
        audit.ResourceId.Should().Be(journal.Id.ToString());
        audit.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        audit.NewValues.Should().Contain(journal.Id.ToString());
        audit.NewValues.Should().Contain("ManualJournalEntry");
        audit.NewValues.Should().Contain("trace-finance-audit");
    }

    [Fact]
    [Trait("Batch", "FinanceAuditFoundation")]
    [Trait("Category", "AuditTrail")]
    public async Task ManualJournalPost_ShouldCreateAuditEventLinkedToPostingEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var service = CreateJournalService(db, tenantId);
        var journal = await service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));
        await service.UpdateApprovalStatusAsync(journal.Id, "Approved", "Approved", Guid.NewGuid());

        await service.PostJournalEntryAsync(journal.Id);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceDocumentType == "ManualJournalEntry" &&
            e.SourceDocumentId == journal.Id &&
            e.PostingAction == "Post");

        var journalAudit = await db.AuditLogs.SingleAsync(a => a.Action == FinanceAuditEvents.JournalPosted);
        journalAudit.TenantId.Should().Be(tenantId);
        journalAudit.Resource.Should().Be("Finance.JournalEntry");
        journalAudit.ResourceId.Should().Be(journal.Id.ToString());
        journalAudit.NewValues.Should().Contain(postingEvent.Id.ToString());

        var postingAudit = await db.AuditLogs.SingleAsync(a => a.Action == FinanceAuditEvents.PostingEventCreated);
        postingAudit.TenantId.Should().Be(tenantId);
        postingAudit.Resource.Should().Be("Finance.PostingEvent");
        postingAudit.ResourceId.Should().Be(postingEvent.Id.ToString());
        postingAudit.NewValues.Should().Contain(postingEvent.Id.ToString());
        postingAudit.NewValues.Should().Contain(journal.Id.ToString());
    }

    [Fact]
    [Trait("Batch", "FinanceAuditFoundation")]
    [Trait("Category", "AuditTrail")]
    public async Task ManualJournalReversal_ShouldCreateAuditEventsLinkedToOriginalAndReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var service = CreateJournalService(db, tenantId);
        var journal = await service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));
        await service.UpdateApprovalStatusAsync(journal.Id, "Approved", "Approved", Guid.NewGuid());
        await service.PostJournalEntryAsync(journal.Id);

        var reversal = await service.ReverseJournalEntryAsync(journal.Id, "Correction required", new DateTime(2026, 7, 5));
        var reversalPosting = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceDocumentType == "ManualJournalReversal" &&
            e.SourceDocumentId == journal.Id &&
            e.PostingAction == "Reverse");

        var originalAudit = await db.AuditLogs.SingleAsync(a => a.Action == FinanceAuditEvents.JournalReversed);
        originalAudit.ResourceId.Should().Be(journal.Id.ToString());
        originalAudit.NewValues.Should().Contain(reversal.Id.ToString());
        originalAudit.NewValues.Should().Contain(reversalPosting.Id.ToString());
        originalAudit.NewValues.Should().Contain("Correction required");

        var reversalAudit = await db.AuditLogs.SingleAsync(a => a.Action == FinanceAuditEvents.JournalReversalCreated);
        reversalAudit.ResourceId.Should().Be(reversal.Id.ToString());
        reversalAudit.NewValues.Should().Contain(journal.Id.ToString());
        reversalAudit.NewValues.Should().Contain(reversalPosting.Id.ToString());
    }

    [Fact]
    [Trait("Batch", "FinanceAuditFoundation")]
    [Trait("Category", "AuditTrail")]
    public async Task DuplicateManualJournalPostingAttempt_ShouldBeAudited()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var service = CreateJournalService(db, tenantId);
        var journal = await service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));
        await service.UpdateApprovalStatusAsync(journal.Id, "Approved", "Approved", Guid.NewGuid());
        await service.PostJournalEntryAsync(journal.Id);

        await service.PostJournalEntryAsync(journal.Id);

        var duplicateAudit = await db.AuditLogs.SingleAsync(a => a.Action == FinanceAuditEvents.DuplicatePostingAttempt);
        duplicateAudit.TenantId.Should().Be(tenantId);
        duplicateAudit.Resource.Should().Be("Finance.JournalEntry");
        duplicateAudit.ResourceId.Should().Be(journal.Id.ToString());
        duplicateAudit.NewValues.Should().Contain(journal.Id.ToString());
        duplicateAudit.NewValues.Should().Contain("Post");
    }

    [Fact]
    [Trait("Batch", "FinanceAuditFoundation")]
    [Trait("Category", "AuditTrail")]
    public async Task FinanceAuditRecord_ShouldRejectCrossTenantAssociation()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        await db.SaveChangesAsync();
        var auditService = CreateFinanceAuditService(db, tenantId);

        var act = () => auditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.JournalSubmitted,
            TenantId = otherTenantId,
            SourceModule = "GL",
            SourceDocumentType = "ManualJournalEntry",
            SourceDocumentId = Guid.NewGuid(),
            JournalEntryId = Guid.NewGuid()
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Finance audit event tenant does not match the current tenant context.");
    }

    [Fact]
    [Trait("Batch", "FinanceAuditFoundation")]
    [Trait("Category", "AuditTrail")]
    public async Task FinanceAuditTrailRead_ShouldRejectAnotherTenantHistory()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var journalId = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            UserId = Guid.NewGuid(),
            Username = "other",
            Action = FinanceAuditEvents.JournalPosted,
            Resource = "Finance.JournalEntry",
            ResourceId = journalId.ToString(),
            IpAddress = "127.0.0.1",
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var auditService = CreateFinanceAuditService(db, tenantId);

        var act = () => auditService.GetAuditTrailAsync(
            otherTenantId,
            "Finance.JournalEntry",
            journalId.ToString());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Finance audit trail tenant does not match the current tenant context.");
    }

    [Fact]
    [Trait("Batch", "FinanceAuditHardening")]
    [Trait("Category", "AuditTrail")]
    public async Task ManualJournalCreateAuditFailure_ShouldNotPersistSourceMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var service = CreateJournalService(db, tenantId, new FailingFinanceAuditService());

        var act = () => service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("audit failed");
        db.ChangeTracker.Clear();
        (await db.JournalEntries.CountAsync(j => j.TenantId == tenantId)).Should().Be(0);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceAuditHardening")]
    [Trait("Category", "AuditTrail")]
    public async Task ManualJournalUpdateAuditFailure_ShouldNotPersistSourceMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var createService = CreateJournalService(db, tenantId);
        var journal = await createService.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));
        var failingService = CreateJournalService(db, tenantId, new FailingFinanceAuditService());

        var act = () => failingService.UpdateJournalEntryAsync(journal.Id, new UpdateJournalEntryDto
        {
            Description = "Should not persist"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("audit failed");
        db.ChangeTracker.Clear();
        var stored = await db.JournalEntries.SingleAsync(j => j.Id == journal.Id);
        stored.Description.Should().Be("Manual audit journal");
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.JournalUpdated)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceAuditHardening")]
    [Trait("Category", "AuditTrail")]
    public async Task ManualJournalDeleteAuditFailure_ShouldNotPersistSourceMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var createService = CreateJournalService(db, tenantId);
        var journal = await createService.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));
        var failingService = CreateJournalService(db, tenantId, new FailingFinanceAuditService());

        var act = () => failingService.DeleteJournalEntryAsync(journal.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("audit failed");
        db.ChangeTracker.Clear();
        var stored = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == journal.Id);
        stored.IsDeleted.Should().BeFalse();
        stored.Transactions.Should().OnlyContain(t => !t.IsDeleted);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.JournalDeleted)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceAuditHardening")]
    [Trait("Category", "AuditTrail")]
    public async Task GenericAuditEndpoint_ShouldNotExposeOtherTenantFinanceAuditRecords()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var visibleId = Guid.NewGuid();
        var hiddenId = Guid.NewGuid();
        SeedAuditLog(db, tenantId, visibleId, FinanceAuditEvents.JournalCreated);
        SeedAuditLog(db, otherTenantId, hiddenId, FinanceAuditEvents.JournalPosted);
        await db.SaveChangesAsync();

        var controller = CreateAuditLogController(db, tenantId);

        var result = await controller.GetAuditLogsByResource("Finance.JournalEntry");

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<IEnumerable<AuditLogDto>>().Subject.ToList();
        rows.Should().ContainSingle();
        rows[0].ResourceId.Should().Be(visibleId.ToString());
        rows.Should().NotContain(row => row.ResourceId == hiddenId.ToString());
    }

    [Fact]
    [Trait("Batch", "FinanceAuditHardening")]
    [Trait("Category", "AuditTrail")]
    public async Task GenericAuditEndpoint_ShouldReturnAuthorizedSameTenantFinanceAuditRecordById()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var visibleResourceId = Guid.NewGuid();
        var hiddenResourceId = Guid.NewGuid();
        var visibleAudit = SeedAuditLog(db, tenantId, visibleResourceId, FinanceAuditEvents.JournalCreated);
        var hiddenAudit = SeedAuditLog(db, otherTenantId, hiddenResourceId, FinanceAuditEvents.JournalPosted);
        await db.SaveChangesAsync();

        var controller = CreateAuditLogController(db, tenantId);

        var sameTenantResult = await controller.GetAuditLog(visibleAudit.Id);
        var sameTenantOk = sameTenantResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var sameTenantDto = sameTenantOk.Value.Should().BeOfType<AuditLogDto>().Subject;
        sameTenantDto.ResourceId.Should().Be(visibleResourceId.ToString());

        var otherTenantResult = await controller.GetAuditLog(hiddenAudit.Id);
        otherTenantResult.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-audit-foundation-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static JournalEntryService CreateJournalService(
        ApplicationDbContext db,
        Guid tenantId,
        IFinanceAuditService? financeAuditService = null)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = financeAuditService ?? new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-finance-audit" }
            });
        var engine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);

        var gl = new Mock<IGeneralLedgerService>();
        var sequence = 1;
        gl.Setup(x => x.GenerateJournalEntryNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"JE-2026-{sequence++:0000}");

        var legacyAudit = new Mock<IAuditLogService>();
        legacyAudit.Setup(x => x.LogUserActionAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        return new JournalEntryService(
            db,
            currentUser.Object,
            gl.Object,
            legacyAudit.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<IAccountingBookService>(),
            engine,
            auditService);
    }

    private static AuditLogController CreateAuditLogController(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var service = new AuditLogService(
            new UnitOfWork(db),
            Mock.Of<ILogger<AuditLogService>>(),
            currentUser.Object);

        return new AuditLogController(service, Mock.Of<ILogger<AuditLogController>>());
    }

    private static FinanceAuditService CreateFinanceAuditService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        return new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-finance-audit" }
            });
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId.ToString());
        currentUser.SetupGet(x => x.UserName).Returns("finance.audit");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("tests");
        return currentUser;
    }

    private static async Task<(Account DebitAccount, Account CreditAccount)> SeedTenantPeriodAndAccountsAsync(
        ApplicationDbContext db,
        Guid tenantId)
    {
        SeedTenant(db, tenantId);
        SeedPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();
        return (debitAccount, creditAccount);
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN")
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
    }

    private static FiscalPeriod SeedPeriod(ApplicationDbContext db, Guid tenantId)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true,
            IsClosed = false,
            IsLocked = false
        };

        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = AccountStatus.Active,
            AllowDirectPosting = true,
            CurrencyCode = "GHS"
        };

        db.Accounts.Add(account);
        return account;
    }

    private static CreateJournalEntryDto CreateJournalDto(Guid debitAccountId, Guid creditAccountId)
    {
        return new CreateJournalEntryDto
        {
            TransactionDate = new DateTime(2026, 7, 5),
            JournalType = "General",
            Description = "Manual audit journal",
            Reference = "AUD-001",
            BookClassification = "IFRS",
            Transactions = new List<CreateAccountTransactionDto>
            {
                new()
                {
                    AccountId = debitAccountId,
                    TransactionType = "Debit",
                    Amount = 100m,
                    Description = "Debit line"
                },
                new()
                {
                    AccountId = creditAccountId,
                    TransactionType = "Credit",
                    Amount = 100m,
                    Description = "Credit line"
                }
            }
        };
    }

    private static AuditLog SeedAuditLog(
        ApplicationDbContext db,
        Guid tenantId,
        Guid resourceId,
        string action)
    {
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            Username = $"tenant-{tenantId:N}",
            Action = action,
            Resource = "Finance.JournalEntry",
            ResourceId = resourceId.ToString(),
            IpAddress = "127.0.0.1",
            Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        db.AuditLogs.Add(audit);
        return audit;
    }

    private sealed class FailingFinanceAuditService : IFinanceAuditService
    {
        public Task<AuditLog> RecordAsync(
            FinanceAuditEventDto auditEvent,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("audit failed");
        }

        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
            Guid tenantId,
            string resource,
            string resourceId,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
