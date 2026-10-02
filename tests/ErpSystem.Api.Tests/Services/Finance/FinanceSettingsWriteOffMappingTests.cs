using System.Text.Json;
using ErpSystem.Api.Services.Finance.Settings;
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

public sealed class FinanceSettingsWriteOffMappingTests
{
    [Fact]
    public async Task CustomerAdvance_RequiresTenantActiveDirectPostingLiability()
    {
        await using var fixture = await Fixture.CreateAsync();
        await FluentActions.Awaiting(() => fixture.Service.UpdateSettingsAsync(new UpdateFinanceSettingsDto { CustomerAdvanceAccountId = fixture.Expense.Id }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Customer advance account*Liability*");
        fixture.Expense.AccountType = AccountType.Liability;
        await fixture.Context.SaveChangesAsync();
        var result = await fixture.Service.UpdateSettingsAsync(new UpdateFinanceSettingsDto { CustomerAdvanceAccountId = fixture.Expense.Id });
        result.CustomerAdvanceAccountId.Should().Be(fixture.Expense.Id);
        fixture.Expense.AllowDirectPosting = false;
        await fixture.Context.SaveChangesAsync();
        await FluentActions.Awaiting(() => fixture.Service.UpdateSettingsAsync(new UpdateFinanceSettingsDto { CustomerAdvanceAccountId = fixture.Expense.Id }))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Get_returns_persisted_tenant_COGS_mapping_in_the_read_contract_without_fallback_or_mutation(bool configured)
    {
        await using var fixture = await Fixture.CreateAsync();
        var expected = configured ? fixture.Expense.Id : (Guid?)null;
        fixture.Settings.ControlAccountCOGSId = expected;
        fixture.Context.FinanceSettings.Add(new FinanceSettings
        {
            TenantId = Guid.NewGuid(), BaseCurrency = "USD", ControlAccountCOGSId = Guid.NewGuid()
        });
        await fixture.Context.SaveChangesAsync();
        var inventoryAccount = fixture.Settings.ControlAccountInventoryId;
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.GetSettingsAsync();

        result.TenantId.Should().Be(fixture.TenantId);
        result.ControlAccountCOGSId.Should().Be(expected);
        result.ControlAccountInventoryId.Should().Be(inventoryAccount);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var exposed = json.RootElement.GetProperty("controlAccountCOGSId");
        if (configured) exposed.GetGuid().Should().Be(expected!.Value);
        else exposed.ValueKind.Should().Be(JsonValueKind.Null);
        fixture.Context.ChangeTracker.HasChanges().Should().BeFalse();
        fixture.Audits.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_persists_both_write_off_mappings_and_Get_returns_them_without_changing_other_settings()
    {
        await using var fixture = await Fixture.CreateAsync();
        var inventoryAccount = fixture.Settings.ControlAccountInventoryId;
        var result = await fixture.Service.UpdateSettingsAsync(new UpdateFinanceSettingsDto
        {
            WriteOffExpenseAccountId = fixture.Expense.Id,
            WriteOffRecoveryAccountId = fixture.Recovery.Id
        });

        result.WriteOffExpenseAccountId.Should().Be(fixture.Expense.Id);
        result.WriteOffRecoveryAccountId.Should().Be(fixture.Recovery.Id);
        fixture.Context.ChangeTracker.Clear();
        var reread = await fixture.Service.GetSettingsAsync();
        reread.WriteOffExpenseAccountId.Should().Be(fixture.Expense.Id);
        reread.WriteOffRecoveryAccountId.Should().Be(fixture.Recovery.Id);
        reread.ControlAccountInventoryId.Should().Be(inventoryAccount);
        reread.AccountSeparator.Should().Be("-");
        reread.ApInvoicePriceTolerancePercent.Should().Be(3m);
        reread.BaseCurrency.Should().Be("GHS");
        var audit = fixture.Audits.Should().ContainSingle().Which;
        audit.EventType.Should().Be(FinanceAuditEvents.FinanceControlPolicyChanged);
        audit.SourceModule.Should().Be("Finance");
        audit.SourceDocumentType.Should().Be("FinanceSettings");
        audit.TenantId.Should().Be(fixture.TenantId);
        JsonSerializer.Serialize(audit.BeforeValues).Should().Contain("WriteOffExpenseAccountId").And.Contain("null");
        JsonSerializer.Serialize(audit.AfterValues).Should().Contain(fixture.Expense.Id.ToString()).And.Contain(fixture.Recovery.Id.ToString());
    }

    [Fact]
    public async Task Partial_update_preserves_an_omitted_mapping_and_repeating_same_values_does_not_duplicate_audit()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.UpdateSettingsAsync(new UpdateFinanceSettingsDto
        {
            WriteOffExpenseAccountId = fixture.Expense.Id,
            WriteOffRecoveryAccountId = fixture.Recovery.Id
        });
        fixture.Audits.Clear();

        var unchanged = await fixture.Service.UpdateSettingsAsync(new UpdateFinanceSettingsDto
        {
            WriteOffExpenseAccountId = fixture.Expense.Id
        });
        var unrelated = await fixture.Service.UpdateSettingsAsync(new UpdateFinanceSettingsDto { AccountSeparator = "." });

        unchanged.WriteOffRecoveryAccountId.Should().Be(fixture.Recovery.Id);
        unrelated.WriteOffExpenseAccountId.Should().Be(fixture.Expense.Id);
        unrelated.WriteOffRecoveryAccountId.Should().Be(fixture.Recovery.Id);
        fixture.Audits.Should().BeEmpty();
    }

    [Fact]
    public async Task Precision_patch_distinguishes_omitted_fields_from_explicit_null_and_audits_clear()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Settings.TaxRoundingIncrement = 0.05m;
        fixture.Settings.InvoiceRoundingIncrement = 0.10m;
        fixture.Settings.InvoiceRoundingGainAccountId = fixture.Recovery.Id;
        fixture.Settings.InvoiceRoundingLossAccountId = fixture.Expense.Id;
        await fixture.Context.SaveChangesAsync();

        var omitted = JsonSerializer.Deserialize<UpdateFinanceSettingsDto>(
            "{}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        omitted.TaxRoundingIncrementSpecified.Should().BeFalse();
        omitted.InvoiceRoundingIncrementSpecified.Should().BeFalse();
        omitted.InvoiceRoundingGainAccountIdSpecified.Should().BeFalse();
        omitted.InvoiceRoundingLossAccountIdSpecified.Should().BeFalse();

        var preserved = await fixture.Service.UpdateSettingsAsync(omitted);
        preserved.TaxRoundingIncrement.Should().Be(0.05m);
        preserved.InvoiceRoundingIncrement.Should().Be(0.10m);
        preserved.InvoiceRoundingGainAccountId.Should().Be(fixture.Recovery.Id);
        preserved.InvoiceRoundingLossAccountId.Should().Be(fixture.Expense.Id);
        fixture.Audits.Should().BeEmpty();

        var clear = JsonSerializer.Deserialize<UpdateFinanceSettingsDto>(
            """{"taxRoundingIncrement":null,"invoiceRoundingIncrement":null,"invoiceRoundingGainAccountId":null,"invoiceRoundingLossAccountId":null}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        clear.TaxRoundingIncrementSpecified.Should().BeTrue();
        clear.InvoiceRoundingIncrementSpecified.Should().BeTrue();
        clear.InvoiceRoundingGainAccountIdSpecified.Should().BeTrue();
        clear.InvoiceRoundingLossAccountIdSpecified.Should().BeTrue();

        var cleared = await fixture.Service.UpdateSettingsAsync(clear);
        cleared.TaxRoundingIncrement.Should().BeNull();
        cleared.InvoiceRoundingIncrement.Should().BeNull();
        cleared.InvoiceRoundingGainAccountId.Should().BeNull();
        cleared.InvoiceRoundingLossAccountId.Should().BeNull();

        var audit = fixture.Audits.Should().ContainSingle().Which;
        audit.EventType.Should().Be(FinanceAuditEvents.FinanceControlPolicyChanged);
        JsonSerializer.Serialize(audit.BeforeValues).Should().Contain("0.05").And.Contain("0.10");
        JsonSerializer.Serialize(audit.AfterValues).Should().Contain("null");
    }

    [Theory]
    [InlineData("expense", "foreign-tenant")]
    [InlineData("expense", "deleted")]
    [InlineData("expense", "inactive")]
    [InlineData("expense", "summary")]
    [InlineData("expense", "control")]
    [InlineData("expense", "wrong-type")]
    [InlineData("expense", "empty")]
    [InlineData("expense", "missing")]
    [InlineData("recovery", "foreign-tenant")]
    [InlineData("recovery", "deleted")]
    [InlineData("recovery", "inactive")]
    [InlineData("recovery", "summary")]
    [InlineData("recovery", "control")]
    [InlineData("recovery", "wrong-type")]
    [InlineData("recovery", "empty")]
    [InlineData("recovery", "missing")]
    public async Task Mapping_rejects_an_ineligible_account_before_changing_any_settings(string mapping, string defect)
    {
        await using var fixture = await Fixture.CreateAsync();
        var account = mapping == "expense" ? fixture.Expense : fixture.Recovery;
        if (defect == "foreign-tenant")
        {
            // TenantId is part of the immutable account key; seed a foreign row rather than mutate it.
            account = new Account { TenantId = Guid.NewGuid(), AccountCode = "FOREIGN", AccountNumber = "FOREIGN",
                AccountName = "Foreign account", AccountType = account.AccountType, Status = AccountStatus.Active,
                AllowDirectPosting = true, CurrencyCode = "GHS" };
            fixture.Context.Accounts.Add(account);
        }
        if (defect == "deleted") account.IsDeleted = true;
        if (defect == "inactive") account.Status = AccountStatus.Inactive;
        if (defect == "summary") account.AllowDirectPosting = false;
        if (defect == "control") account.IsControlAccount = true;
        if (defect == "wrong-type") account.AccountType = AccountType.Asset;
        await fixture.Context.SaveChangesAsync();
        var requested = defect == "empty" ? Guid.Empty : defect == "missing" ? Guid.NewGuid() : account.Id;
        var dto = new UpdateFinanceSettingsDto { AccountSeparator = "changed" };
        if (mapping == "expense") dto.WriteOffExpenseAccountId = requested;
        else dto.WriteOffRecoveryAccountId = requested;

        var action = () => fixture.Service.UpdateSettingsAsync(dto);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*current tenant*");
        fixture.Settings.WriteOffExpenseAccountId.Should().BeNull();
        fixture.Settings.WriteOffRecoveryAccountId.Should().BeNull();
        fixture.Settings.AccountSeparator.Should().Be("-");
        fixture.Audits.Should().BeEmpty();
        fixture.Context.ChangeTracker.Clear();
        (await fixture.Context.FinanceSettings.SingleAsync()).AccountSeparator.Should().Be("-");
    }

    [Fact]
    public async Task Invalid_recovery_mapping_does_not_apply_the_valid_expense_mapping_first()
    {
        await using var fixture = await Fixture.CreateAsync();
        var action = () => fixture.Service.UpdateSettingsAsync(new UpdateFinanceSettingsDto
        {
            WriteOffExpenseAccountId = fixture.Expense.Id,
            WriteOffRecoveryAccountId = fixture.Expense.Id
        });

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Recovery*Revenue*");
        fixture.Settings.WriteOffExpenseAccountId.Should().BeNull();
        fixture.Settings.WriteOffRecoveryAccountId.Should().BeNull();
        fixture.Audits.Should().BeEmpty();
    }

    [Fact]
    public async Task Invoice_rounding_activation_requires_and_exposes_valid_direct_posting_accounts()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.UpdateSettingsAsync(new UpdateFinanceSettingsDto
        {
            InvoiceRoundingEnabled = true,
            InvoiceRoundingIncrement = 0.05m,
            InvoiceRoundingGainAccountId = fixture.Recovery.Id,
            InvoiceRoundingLossAccountId = fixture.Expense.Id
        });

        result.InvoiceRoundingEnabled.Should().BeTrue();
        result.InvoiceRoundingIncrement.Should().Be(0.05m);
        result.InvoiceRoundingGainAccountId.Should().Be(fixture.Recovery.Id);
        result.InvoiceRoundingLossAccountId.Should().Be(fixture.Expense.Id);

        fixture.Recovery.AllowDirectPosting = false;
        await fixture.Context.SaveChangesAsync();
        await FluentActions.Awaiting(() => fixture.Service.UpdateSettingsAsync(
                new UpdateFinanceSettingsDto { InvoiceRoundingEnabled = true }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*rounding gain account*active*Revenue*");
    }

    [Fact]
    public async Task Invoice_rounding_policy_cannot_be_activated_after_posted_accounting_activity()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.JournalEntries.Add(new JournalEntry
        {
            TenantId = fixture.TenantId,
            JournalEntryNumber = "LOCK-1",
            Description = "Precision lifecycle lock",
            PostingStatus = "Posted"
        });
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.UpdateSettingsAsync(new UpdateFinanceSettingsDto
        {
            InvoiceRoundingEnabled = true,
            InvoiceRoundingIncrement = 0.05m,
            InvoiceRoundingGainAccountId = fixture.Recovery.Id,
            InvoiceRoundingLossAccountId = fixture.Expense.Id
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be changed after posted usage*");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        public FinanceSettings Settings { get; private set; } = null!;
        public Account Expense { get; private set; } = null!;
        public Account Recovery { get; private set; } = null!;
        public FinanceSettingsService Service { get; private set; } = null!;
        public List<FinanceAuditEventDto> Audits { get; } = [];

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            fixture.Settings = new FinanceSettings
            {
                TenantId = fixture.TenantId, BaseCurrency = "GHS", CoaType = "Segmented", AccountSeparator = "-",
                ControlAccountInventoryId = Guid.NewGuid(), ApInvoicePriceTolerancePercent = 3m
            };
            fixture.Expense = fixture.Account("LOSS", AccountType.Expense);
            fixture.Recovery = fixture.Account("RECOVERY", AccountType.Revenue);
            fixture.Context.AddRange(fixture.Settings, fixture.Expense, fixture.Recovery);
            await fixture.Context.SaveChangesAsync();
            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(user => user.TenantId).Returns(fixture.TenantId);
            currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid().ToString());
            currentUser.SetupGet(user => user.Claims).Returns(new Dictionary<string, string>());
            var tenantSettings = new Mock<ITenantSettingsService>();
            tenantSettings.Setup(service => service.GetBaseCurrencyReferenceAsync()).ReturnsAsync(new BaseCurrencyReferenceDto
            {
                CurrencyCode = "GHS", CurrencyName = "Ghana Cedi", CurrencySymbol = "GH₵", DecimalPlaces = 2
            });
            var audit = new Mock<IFinanceAuditService>();
            audit.Setup(service => service.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FinanceAuditEventDto value, CancellationToken _) =>
                {
                    fixture.Audits.Add(value);
                    return new AuditLog();
                });
            fixture.Service = new FinanceSettingsService(fixture.Context, currentUser.Object, tenantSettings.Object, audit.Object);
            return fixture;
        }

        private Account Account(string name, AccountType type) => new()
        {
            TenantId = TenantId, AccountCode = name, AccountNumber = name, AccountName = name,
            AccountType = type, Status = AccountStatus.Active, AllowDirectPosting = true, CurrencyCode = "GHS"
        };

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
