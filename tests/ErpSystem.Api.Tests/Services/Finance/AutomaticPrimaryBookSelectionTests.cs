using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AutomaticPrimaryBookSelectionTests
{
    [Fact]
    public void Controller_ExposesOnlyAutomaticResolutionAndFreeze()
    {
        var methods = typeof(AccountingBookApplicabilityController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        methods.Select(item => item.Name).Should().BeEquivalentTo(
            nameof(AccountingBookApplicabilityController.Resolve),
            nameof(AccountingBookApplicabilityController.Freeze));
        foreach (var method in methods)
        {
            method.GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(
                FinancePermissions.ResolveAccountingBookApplicability);
        }
    }

    [Fact]
    public async Task ApprovedLegacyPolicy_CannotOverrideAutomaticPrimarySelection()
    {
        await using var db = Context();
        var state = Seed(db);
        AddLegacyPolicySelectingParallel(db, state);
        await db.SaveChangesAsync();

        var result = await Service(db, state.TenantId).ResolveAsync(new ResolveAccountingBookApplicabilityDto
        {
            EffectiveDate = state.Date,
            OriginatingModuleCode = "INV",
            SourceDocumentType = "GOODS.RECEIPT",
            PostingAction = "POST"
        });

        result.Books.Should().ContainSingle().Which.AccountingBookId.Should().Be(state.Primary.Id);
        result.PolicyId.Should().BeNull();
        result.RuleId.Should().BeNull();
        result.PolicyVersion.Should().BeNull();
        result.UsedPrimaryOnlyFallback.Should().BeTrue();
    }

    private static State Seed(ApplicationDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var date = new DateTime(2026, 9, 6);
        var primary = Book(tenantId, "BASE", AccountingBookType.PrimaryFull, true);
        var parallel = Book(tenantId, "USD_PARALLEL", AccountingBookType.ParallelFull, false);
        db.AccountingBooks.AddRange(primary, parallel);

        var year = new FiscalYear
        {
            TenantId = tenantId, FiscalYearName = "FY26", FiscalYearCode = "2026", Year = 2026,
            FiscalYearType = "Calendar", StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31), TotalDays = 365, NumberOfPeriods = 12,
            Status = "Open", IsActive = true
        };
        var period = new FiscalPeriod
        {
            TenantId = tenantId, FiscalYear = year, PeriodName = "September", PeriodCode = "2026-09",
            PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30),
            PeriodDays = 30, PeriodStatus = "Open", IsOpen = true
        };
        db.FiscalPeriods.Add(period);
        db.ModuleDefinitions.Add(new ModuleDefinition
        {
            TenantId = tenantId, ModuleCode = "INV", ModuleName = "Inventory", IsActive = true
        });

        foreach (var book in new[] { primary, parallel })
        {
            var account = new Account
            {
                TenantId = tenantId, AccountCode = $"A-{book.Code}", AccountNumber = $"A-{book.Code}",
                AccountName = "Account", AccountType = AccountType.Asset, CurrencyCode = "GHS",
                Status = AccountStatus.Active
            };
            var classification = new AccountClassification
            {
                TenantId = tenantId, AccountingBookId = book.Id, Code = $"C_{book.Code}", Name = "Class",
                CoreAccountType = AccountType.Asset, Status = AccountClassificationStatus.Active,
                IsPostingClassification = true
            };
            db.Accounts.Add(account);
            db.AccountClassifications.Add(classification);
            db.AccountAccountingBooks.Add(new AccountAccountingBook
            {
                TenantId = tenantId, AccountingBookId = book.Id, AccountId = account.Id,
                AccountClassificationId = classification.Id, IsEnabled = true
            });
        }

        return new State(tenantId, primary, parallel, date);
    }

    private static void AddLegacyPolicySelectingParallel(ApplicationDbContext db, State state)
    {
        var policy = new AccountingBookApplicabilityPolicy
        {
            TenantId = state.TenantId, PolicyCode = "LEGACY_ROUTING", Version = 1, Name = "Legacy routing",
            EffectiveFrom = new DateTime(2026, 1, 1), PolicyStatus = AccountingBookApplicabilityPolicyStatus.Approved,
            Reason = "Historical evidence", CreatedByUserId = Guid.NewGuid(), PreparedByUserId = Guid.NewGuid(),
            PreparedAtUtc = DateTime.UtcNow, ApprovedByUserId = Guid.NewGuid(), ApprovedAtUtc = DateTime.UtcNow
        };
        var rule = new AccountingBookApplicabilityRule
        {
            TenantId = state.TenantId, RuleCode = "LEGACY", Priority = 100,
            OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT", PostingAction = "POST"
        };
        rule.SelectedBooks.Add(new AccountingBookApplicabilityRuleBook
        {
            TenantId = state.TenantId, AccountingBookId = state.Parallel.Id,
            AccountingBookCodeSnapshot = state.Parallel.Code, SelectionOrder = 0
        });
        policy.Rules.Add(rule);
        db.AccountingBookApplicabilityPolicies.Add(policy);
    }

    private static AccountingBook Book(Guid tenantId, string code, AccountingBookType type, bool isPrimary) => new()
    {
        TenantId = tenantId, Code = code, Name = code, Purpose = "Reporting", BookType = type,
        LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
        IsDefault = isPrimary, IsActive = true, AllowsPosting = true
    };

    private static AccountingBookApplicabilityService Service(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(item => item.UserName).Returns("automatic-selection.test");
        var initialization = new Mock<IAccountingBookInitializationService>();
        initialization.Setup(item => item.ValidateCurrentApprovedEvidenceAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingBookInitializationEvidenceValidationDto
            {
                IsValid = true, Version = 1, EvidenceFingerprint = new string('A', 64),
                ReconciliationFingerprint = new string('B', 64)
            });
        return new AccountingBookApplicabilityService(db, currentUser.Object,
            Mock.Of<IWorkflowService>(), Mock.Of<IFinanceAuditService>(), initialization.Object);
    }

    private static ApplicationDbContext Context() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"automatic-primary-{Guid.NewGuid():N}").Options);

    private sealed record State(Guid TenantId, AccountingBook Primary, AccountingBook Parallel, DateTime Date);
}
