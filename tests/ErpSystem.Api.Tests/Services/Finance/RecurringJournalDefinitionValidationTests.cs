using System.Reflection;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class RecurringJournalDefinitionValidationTests
{
    [Fact]
    public async Task DefinitionValidation_AcceptsTenantBaseBookAndRejectsInactiveBook()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var debit = PostingAccount(tenantId, "6100", AccountType.Expense);
        var credit = PostingAccount(tenantId, "2100", AccountType.Liability);
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "BASE", Name = "Primary Book",
            IsActive = true, AllowsPosting = true, IsDefault = true
        };
        db.Accounts.AddRange(debit, credit);
        db.AccountingBooks.Add(book);
        db.FinanceSettings.Add(new FinanceSettings { Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS" });
        await db.SaveChangesAsync();

        var service = new RecurringJournalService(db, Mock.Of<ICurrentUserService>(),
            Mock.Of<IDocumentNumberingService>(), Mock.Of<IFinancePostingEngine>(),
            Mock.Of<IFinanceAuditService>(), Mock.Of<IWorkflowService>(), null!, null!);
        var validate = typeof(RecurringJournalService).GetMethod(
            "ValidateDefinitionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var request = Request(book.Code, debit.Id, credit.Id);

        await (Task)validate.Invoke(service, new object[] { request, tenantId, CancellationToken.None })!;

        book.IsActive = false;
        await db.SaveChangesAsync();
        Func<Task> invalid = async () =>
            await (Task)validate.Invoke(service, new object[] { request, tenantId, CancellationToken.None })!;
        await invalid.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be active and allow posting*");
    }

    private static CreateRecurringJournalTemplateDto Request(string bookCode, Guid debit, Guid credit) => new()
    {
        Name = "Monthly accrual", BookClassification = bookCode, CurrencyCode = "GHS",
        EffectiveFrom = new DateOnly(2026, 10, 1), TimeZoneId = "UTC",
        Frequency = RecurrenceFrequency.Monthly, Interval = 1,
        RecurrenceRuleJson = "{\"lastCalendarDay\":true}",
        Lines =
        [
            new() { AccountId = debit, IsDebit = true, FixedAmount = 100m },
            new() { AccountId = credit, IsDebit = false, FixedAmount = 100m }
        ]
    };

    private static Account PostingAccount(Guid tenantId, string code, AccountType type) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = code, AccountNumber = code,
        AccountName = code, AccountType = type, CurrencyCode = "GHS", Status = AccountStatus.Active,
        AllowDirectPosting = true, IsControlAccount = false
    };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"recurring-definition-{Guid.NewGuid():N}").Options;
        return new ApplicationDbContext(options);
    }
}
