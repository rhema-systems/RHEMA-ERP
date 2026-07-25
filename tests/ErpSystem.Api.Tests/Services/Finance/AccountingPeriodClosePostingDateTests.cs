using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.Fiscal;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingPeriodClosePostingDateTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ClosePeriod_ShouldCloseOnlyCurrentTenantPeriodAndAudit()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var period = SeedPeriod(db, tenantId);
        var otherPeriod = SeedPeriod(db, otherTenantId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);

        var result = await service.ClosePeriodAsync(new PeriodCloseRequestDto
        {
            FiscalPeriodId = period.Id,
            ClosingNotes = "Month-end close complete"
        });

        result.Success.Should().BeTrue();
        period.IsClosed.Should().BeTrue();
        period.IsOpen.Should().BeFalse();
        period.PeriodStatus.Should().Be("Closed");
        otherPeriod.IsOpen.Should().BeTrue();
        otherPeriod.IsClosed.Should().BeFalse();

        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.AccountingPeriodCloseRequested)).Should().Be(1);
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.AccountingPeriodClosed)).Should().Be(1);
        (await db.AuditLogs.AnyAsync(a => a.TenantId == otherTenantId)).Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ClosePeriod_ShouldFailValidation_WhenApprovedApInvoiceIsUnposted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        db.VendorInvoices.Add(new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "AP-001",
            SupplierId = Guid.NewGuid(),
            SupplierName = "Supplier",
            InvoiceDate = new DateTime(2026, 7, 10),
            CurrencyCode = "GHS",
            Status = VendorInvoiceStatus.Approved,
            ApprovalStatus = "Approved",
            SubTotal = 100m,
            TotalAmount = 100m
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);

        var result = await service.ClosePeriodAsync(new PeriodCloseRequestDto
        {
            FiscalPeriodId = period.Id,
            ClosingNotes = "Attempt close with unposted AP"
        });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("approved Finance source documents", StringComparison.OrdinalIgnoreCase));
        period.IsOpen.Should().BeTrue();
        period.IsClosed.Should().BeFalse();
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.AccountingPeriodCloseValidationFailed)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ReopenPeriod_ShouldRequireReasonAndAuditSuccessfulReopen()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId, isOpen: false, isClosed: true);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var missingReason = () => service.ReopenPeriodAsync(new PeriodReopenRequestDto
        {
            FiscalPeriodId = period.Id,
            Reason = " "
        });

        await missingReason.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A reason is required to reopen an accounting period.");

        var reopened = await service.ReopenPeriodAsync(new PeriodReopenRequestDto
        {
            FiscalPeriodId = period.Id,
            Reason = "Audit adjustment required"
        });

        reopened.IsOpen.Should().BeTrue();
        reopened.IsClosed.Should().BeFalse();
        reopened.ReopenCount.Should().Be(1);
        period.ReopenReason.Should().Be("Audit adjustment required");
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.AccountingPeriodReopened)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ValidatePeriodClose_ShouldDetectOrphanedPostingEvents()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = "TEST",
            SourceDocumentType = "TestDocument",
            SourceDocumentId = Guid.NewGuid(),
            PostingAction = "Post",
            PostingStatus = "Posted",
            PostingDate = new DateTime(2026, 7, 10),
            FunctionalCurrencyCode = "GHS",
            BookClassification = "IFRS",
            TotalDebitAmount = 100m,
            TotalCreditAmount = 100m
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var validation = await service.ValidatePeriodCloseAsync(period.Id);

        validation.CanClose.Should().BeFalse();
        validation.ValidationErrors.Should().Contain(e => e.Contains("posting events", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "FiscalPeriod")]
    public async Task ValidatePeriodClose_ShouldDetectPostedJournalMissingPostingEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        db.JournalEntries.Add(new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = "JE-001",
            Description = "Posted journal without posting event",
            EntryDate = new DateTime(2026, 7, 10),
            FiscalPeriodId = period.Id,
            PostingStatus = "Posted",
            ApprovalStatus = "Approved",
            TotalDebitAmount = 100m,
            TotalCreditAmount = 100m
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var validation = await service.ValidatePeriodCloseAsync(period.Id);

        validation.CanClose.Should().BeFalse();
        validation.ValidationErrors.Should().Contain(e => e.Contains("posted journal entries are missing posting events", StringComparison.OrdinalIgnoreCase));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"accounting-period-close-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static FiscalPeriodService CreateService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(db, currentUser.Object, new HttpContextAccessor());
        return new FiscalPeriodService(
            new UnitOfWork(db),
            currentUser.Object,
            Mock.Of<ILogger<FiscalPeriodService>>(),
            auditService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("finance.close");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("period-close-tests");
        return currentUser;
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

    private static FiscalPeriod SeedPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpen = true,
        bool isClosed = false,
        bool isLocked = false)
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
            PeriodStatus = isLocked ? "Locked" : isClosed ? "Closed" : isOpen ? "Open" : "Future",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = isLocked
        };

        db.FiscalPeriods.Add(period);
        return period;
    }
}
