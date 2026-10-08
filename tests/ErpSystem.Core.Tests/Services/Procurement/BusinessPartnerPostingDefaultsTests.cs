using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class BusinessPartnerPostingDefaultsTests
{
    [Fact]
    public async Task UnrelatedPartnerEditPreservesAnUnchangedLegacyTaxExpenseAccount()
    {
        var partner = Partner();
        var fixture = Fixture(partner);
        var account = new Account { TenantId = partner.TenantId, AccountType = AccountType.Expense,
            Status = AccountStatus.Active, AllowDirectPosting = true };
        partner.DefaultTaxAccountId = account.Id;
        fixture.Accounts.Setup(repository => repository.GetByIdAsync(account.Id)).ReturnsAsync(account);
        var result = await fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = "Renamed supplier", PostingDefaults = new() { DefaultTaxAccountId = account.Id }
        });
        result.PartnerName.Should().Be("Renamed supplier");
        result.PostingDefaults.DefaultTaxAccountId.Should().Be(account.Id);
        partner.DefaultTaxAccountId.Should().Be(account.Id);
    }

    [Theory]
    [InlineData(AccountType.Asset, true)]
    [InlineData(AccountType.Liability, true)]
    [InlineData(AccountType.Expense, false)]
    [InlineData(AccountType.Revenue, false)]
    public async Task SupplierInputTaxFallbackRequiresBalanceSheetPostingAccount(AccountType type, bool accepted)
    {
        var partner = Partner();
        var fixture = Fixture(partner);
        var account = new Account { TenantId = partner.TenantId, AccountType = type,
            Status = AccountStatus.Active, IsControlAccount = accepted, AllowDirectPosting = !accepted };
        fixture.Accounts.Setup(repository => repository.GetByIdAsync(account.Id)).ReturnsAsync(account);
        var update = () => fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName,
            PostingDefaults = new() { DefaultTaxAccountId = account.Id }
        });
        if (accepted)
        {
            var result = await update();
            result.PostingDefaults.DefaultTaxAccountId.Should().Be(account.Id);
        }
        else
        {
            await update.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Input tax fallback*");
            partner.DefaultTaxAccountId.Should().BeNull();
        }
    }

    [Fact]
    public void CurrentModelMapsSupplierWithholdingDefaultsWithoutReintroducingLegacyIdentity()
    {
        // The disposable-development baseline intentionally replaced the old one-off
        // BusinessPartnerWithholdingTaxDefault migration. Assert the durable relational
        // contract instead of instantiating a migration class that no longer belongs to
        // the active migration chain.
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=NeverConnected;Integrated Security=True").Options);
        context.Database.GetDbConnection().State.Should().Be(System.Data.ConnectionState.Closed);

        var invoice = context.Model.FindEntityType(typeof(VendorInvoice))!;
        invoice.GetTableName().Should().Be(nameof(VendorInvoice));
        invoice.FindProperty(nameof(VendorInvoice.WithholdingDecisionPending))!
            .GetDefaultValue().Should().Be(false);
        invoice.FindProperty(nameof(VendorInvoice.ApplySupplierWithholdingDefaults))!
            .IsNullable.Should().BeTrue();
        invoice.FindProperty(nameof(VendorInvoice.WithholdingTaxRate))!
            .GetColumnType().Should().Be("decimal(18,4)");
        invoice.FindProperty(nameof(VendorInvoice.BusinessPartnerId)).Should().NotBeNull();
        invoice.FindProperty("SupplierId").Should().BeNull("AP identity is canonical BusinessPartnerId only");

        var partner = context.Model.FindEntityType(typeof(BusinessPartner))!;
        var defaultTaxProperty = partner.FindProperty(nameof(BusinessPartner.DefaultWithholdingTaxId))!;
        defaultTaxProperty.IsNullable.Should().BeTrue();
        partner.GetForeignKeys().Single(foreignKey => foreignKey.Properties.Contains(defaultTaxProperty))
            .PrincipalEntityType.ClrType.Should().Be(typeof(Tax));
        context.Database.GetDbConnection().State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public void SnapshotRetainsTheSelectedSupplierDefaultsAfterMasterChanges()
    {
        var partner = Partner();
        partner.PaymentTermId = Guid.NewGuid();
        partner.PaymentTerms = "Net 30";
        partner.TaxIdentificationNumber = "TIN-123";
        partner.SubjectToWithholdingDeduction = true;
        partner.WithholdingTaxRate = 7.5m;
        partner.DefaultWithholdingTaxId = Guid.NewGuid();
        var withholdingId = partner.DefaultWithholdingTaxId;
        partner.CreditLimit = 2500m;
        partner.DefaultApAccountId = Guid.NewGuid();
        var saved = BusinessPartnerPostingDefaults.SerializeSnapshot(partner);
        partner.WithholdingTaxRate = 5m;
        partner.DefaultWithholdingTaxId = Guid.NewGuid();
        partner.PaymentTerms = "Net 60";
        var snapshot = BusinessPartnerPostingDefaults.ReadSnapshot(saved)!;
        snapshot.PaymentTerms.Should().Be("Net 30");
        snapshot.Tin.Should().Be("TIN-123");
        snapshot.CreditLimit.Should().Be(2500m);
        snapshot.PostingDefaults.WithholdingTaxRate.Should().Be(7.5m);
        snapshot.PostingDefaults.DefaultWithholdingTaxId.Should().Be(withholdingId);
        snapshot.WithholdingDefault.Should().BeNull("AP resolved values are not stored in the PO snapshot");
        snapshot.PostingDefaults.DefaultApAccountId.Should().Be(partner.DefaultApAccountId);
    }

    [Fact]
    public async Task OmittedDefaultsPreserveExistingValuesForOlderClients()
    {
        var partner = Partner();
        partner.SubjectToWithholdingDeduction = true;
        partner.WithholdingTaxRate = 5m;
        partner.DefaultApAccountId = Guid.NewGuid();
        partner.CreditLimit = 100m;
        partner.PaymentTerms = "Negotiated terms";
        var fixture = Fixture(partner);
        await fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto { PartnerName = "Renamed" });
        partner.WithholdingTaxRate.Should().Be(5m);
        partner.DefaultApAccountId.Should().NotBeNull();
        partner.CreditLimit.Should().Be(100m);
        partner.PaymentTerms.Should().Be("Negotiated terms");
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("inactive")]
    [InlineData("sales")]
    [InlineData("vat")]
    [InlineData("missing-account")]
    public async Task SupplierWithholdingRuleMustBeAnEligibleTenantPurchaseRule(string invalid)
    {
        var partner = Partner(); var fixture = Fixture(partner);
        var tax = new Tax { Id = Guid.NewGuid(), TenantId = partner.TenantId, Code = "WHT", Name = "WHT", Rate = 7.5m,
            Category = TaxCategory.Withholding, Applicability = TaxApplicability.Purchases, TaxPayableAccountId = Guid.NewGuid() };
        if (invalid == "foreign") tax.TenantId = Guid.NewGuid();
        if (invalid == "inactive") tax.IsActive = false;
        if (invalid == "sales") tax.Applicability = TaxApplicability.Sales;
        if (invalid == "vat") tax.Category = TaxCategory.Standard;
        if (invalid == "missing-account") tax.TaxPayableAccountId = null;
        fixture.WithholdingTaxes.Setup(repository => repository.GetByIdAsync(tax.Id)).ReturnsAsync(tax);
        var action = () => fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName, PostingDefaults = new()
            { SubjectToWithholdingDeduction = true, WithholdingTaxRate = 7.5m, DefaultWithholdingTaxId = tax.Id }
        });
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WHT*");
        fixture.Partners.Verify(repository => repository.UpdateAsync(It.IsAny<BusinessPartner>()), Times.Never);
    }

    [Fact]
    public async Task ValidSupplierWithholdingRuleSavesAndReturnsItsIdentity()
    {
        var partner = Partner(); var fixture = Fixture(partner);
        var account = new Account { TenantId = partner.TenantId, AccountType = AccountType.Liability,
            Status = AccountStatus.Active, IsControlAccount = true, AllowDirectPosting = false };
        var tax = new Tax { TenantId = partner.TenantId, Code = "WHT", Name = "Purchase WHT", Rate = 7.5m,
            Category = TaxCategory.Withholding, Applicability = TaxApplicability.Purchases, TaxPayableAccountId = account.Id };
        fixture.WithholdingTaxes.Setup(repository => repository.GetByIdAsync(tax.Id)).ReturnsAsync(tax);
        fixture.Accounts.Setup(repository => repository.GetByIdAsync(account.Id)).ReturnsAsync(account);
        var result = await fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName, PostingDefaults = new()
            { SubjectToWithholdingDeduction = true, WithholdingTaxRate = 7.5m, DefaultWithholdingTaxId = tax.Id }
        });
        result.PostingDefaults.DefaultWithholdingTaxId.Should().Be(tax.Id);
        partner.DefaultWithholdingTaxId.Should().Be(tax.Id);
    }

    [Fact]
    public async Task UntickedWithholdingClearsTheRateWithoutChangingLegacyCreditData()
    {
        var partner = Partner();
        partner.SubjectToWithholdingDeduction = true;
        partner.WithholdingTaxRate = 7.5m;
        var fixture = Fixture(partner);
        var result = await fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName,
            PostingDefaults = new() { SubjectToWithholdingDeduction = false, WithholdingTaxRate = 7.5m }
        });
        result.CreditLimit.Should().BeNull();
        result.PostingDefaults.WithholdingTaxRate.Should().Be(0);
        result.PostingDefaults.SubjectToWithholdingDeduction.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task EnabledWithholdingRequiresAPositiveValidRate(decimal rate)
    {
        var partner = Partner();
        var fixture = Fixture(partner);
        var action = () => fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName,
            PostingDefaults = new() { SubjectToWithholdingDeduction = true, WithholdingTaxRate = rate }
        });
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WHT rate*");
        fixture.Partners.Verify(x => x.UpdateAsync(It.IsAny<BusinessPartner>()), Times.Never);
    }

    [Fact]
    public async Task PayablesAndAccruedPurchasesAcceptLiabilityControlAccounts()
    {
        var partner = Partner();
        var fixture = Fixture(partner);
        var control = new Account
        {
            Id = Guid.NewGuid(), TenantId = partner.TenantId, AccountType = AccountType.Liability,
            Status = AccountStatus.Active, IsControlAccount = true, AllowDirectPosting = false
        };
        fixture.Accounts.Setup(x => x.GetByIdAsync(control.Id)).ReturnsAsync(control);
        var result = await fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName,
            PostingDefaults = new() { DefaultApAccountId = control.Id, DefaultAccruedPurchasesAccountId = control.Id }
        });
        result.PostingDefaults.DefaultApAccountId.Should().Be(control.Id);
        result.PostingDefaults.DefaultAccruedPurchasesAccountId.Should().Be(control.Id);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CrossTenantOrInactiveAccountsAreRejected(bool otherTenant, bool inactive)
    {
        var partner = Partner();
        var fixture = Fixture(partner);
        var account = new Account
        {
            Id = Guid.NewGuid(), TenantId = otherTenant ? Guid.NewGuid() : partner.TenantId,
            AccountType = AccountType.Liability, Status = inactive ? AccountStatus.Inactive : AccountStatus.Active
        };
        fixture.Accounts.Setup(x => x.GetByIdAsync(account.Id)).ReturnsAsync(account);
        var action = () => fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName, PostingDefaults = new() { DefaultApAccountId = account.Id }
        });
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Accounts Payable*");
    }

    [Fact]
    public async Task ForeignBankAndSalesOnlyTaxCannotBecomeSupplierDefaults()
    {
        var partner = Partner();
        var fixture = Fixture(partner);
        var bank = new BankAccount { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), IsActive = true };
        fixture.Banks.Setup(x => x.GetByIdAsync(bank.Id)).ReturnsAsync(bank);
        var bankAction = () => fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName, PostingDefaults = new() { DefaultBankAccountId = bank.Id }
        });
        await bankAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ChequeBook*");
        var tax = new TaxGroup { Id = Guid.NewGuid(), TenantId = partner.TenantId, IsActive = true, Applicability = TaxApplicability.Sales };
        fixture.Taxes.Setup(x => x.GetByIdAsync(tax.Id)).ReturnsAsync(tax);
        var taxAction = () => fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName, PostingDefaults = new() { DefaultTaxGroupId = tax.Id }
        });
        await taxAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tax schedule*");
    }

    [Fact]
    public async Task NewPurchaseOrderCopiesDefaultsButPreservesNegotiatedPaymentTerms()
    {
        var partner = Partner();
        partner.PaymentTerms = "Net 30";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var context = new ApplicationDbContext(options, partner.TenantId);
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var repository = new PurchaseOrderRepository(context, Mock.Of<IProcurementSettingsRepository>());
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = partner.TenantId, BusinessPartnerId = partner.Id,
            OrderNumber = "PO-DEFAULTS-TEST", PaymentTerms = "Negotiated Net 45"
        };
        await repository.CreatePurchaseOrderAsync(order);
        await context.SaveChangesAsync();
        order.PaymentTerms.Should().Be("Negotiated Net 45");
        BusinessPartnerPostingDefaults.ReadSnapshot(order.SupplierDefaultsSnapshotJson)!.PaymentTerms.Should().Be("Net 30");
        var saved = order.SupplierDefaultsSnapshotJson;
        partner.PaymentTerms = "Net 60";
        await context.SaveChangesAsync();
        await repository.UpdatePurchaseOrderAsync(order);
        order.SupplierDefaultsSnapshotJson.Should().Be(saved);
    }

    [Fact]
    public async Task DraftSupplierChangeRefreshesSnapshotAndSameSupplierLegacyEditDoesNotBackfill()
    {
        var first = Partner();
        var second = Partner();
        second.TenantId = first.TenantId;
        second.PaymentTerms = "Net 7";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var context = new ApplicationDbContext(options, first.TenantId);
        context.BusinessPartners.AddRange(first, second);
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = first.TenantId, BusinessPartnerId = first.Id,
            OrderNumber = "PO-LEGACY-TEST"
        };
        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();
        var repository = new PurchaseOrderRepository(context, Mock.Of<IProcurementSettingsRepository>());
        await repository.UpdatePurchaseOrderAsync(order);
        order.SupplierDefaultsSnapshotJson.Should().BeNull();
        await context.SaveChangesAsync();
        order.BusinessPartnerId = second.Id;
        await repository.UpdatePurchaseOrderAsync(order);
        order.PaymentTerms.Should().Be("Net 7");
        BusinessPartnerPostingDefaults.ReadSnapshot(order.SupplierDefaultsSnapshotJson)!.BusinessPartnerId.Should().Be(second.Id);
    }

    [Fact]
    public async Task ApprovedPurchaseOrderCannotChangeSupplierThroughRepository()
    {
        var first = Partner();
        var second = Partner();
        second.TenantId = first.TenantId;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var context = new ApplicationDbContext(options, first.TenantId);
        context.BusinessPartners.AddRange(first, second);
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = first.TenantId, BusinessPartnerId = first.Id,
            OrderNumber = "PO-APPROVED-TEST", Status = "Approved"
        };
        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();
        var repository = new PurchaseOrderRepository(context, Mock.Of<IProcurementSettingsRepository>());
        order.BusinessPartnerId = second.Id;
        var action = () => repository.UpdatePurchaseOrderAsync(order);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*draft purchase order*");
    }

[Fact]
    public async Task PostingOptionsExposeOnlyCurrentTenantActiveCatalogueMetadata()
    {
        var partner = Partner();
        var fixture = Fixture(partner);
        var account = new Account { Id = Guid.NewGuid(), TenantId = partner.TenantId, AccountName = "AP", AccountNumber = "2100", AccountCode = "AP", AccountType = AccountType.Liability };
        var foreign = new Account { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), AccountName = "Foreign", AccountNumber = "9999" };
        var inactive = new Account { Id = Guid.NewGuid(), TenantId = partner.TenantId, Status = AccountStatus.Inactive };
        fixture.Accounts.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Account, bool>> predicate) => new[] { account, foreign, inactive }.Where(predicate.Compile()).ToList());
        var bank = new BankAccount { Id = Guid.NewGuid(), TenantId = partner.TenantId, AccountName = "Main Chequebook", GLAccountId = account.Id };
        var foreignBank = new BankAccount { Id = Guid.NewGuid(), TenantId = Guid.NewGuid() };
        fixture.Banks.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<BankAccount, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<BankAccount, bool>> predicate) => new[] { bank, foreignBank }.Where(predicate.Compile()).ToList());
        var purchaseTax = new TaxGroup { Id = Guid.NewGuid(), TenantId = partner.TenantId, Code = "BUY", Name = "Purchase tax", Applicability = TaxApplicability.Purchases };
        var salesTax = new TaxGroup { Id = Guid.NewGuid(), TenantId = partner.TenantId, Code = "SELL", Name = "Sales tax", Applicability = TaxApplicability.Sales };
        fixture.Taxes.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TaxGroup, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<TaxGroup, bool>> predicate) => new[] { purchaseTax, salesTax }.Where(predicate.Compile()).ToList());
        var withholding = new Tax { TenantId = partner.TenantId, Code = "WHT", Name = "Purchase WHT", Rate = 7.5m,
            Category = TaxCategory.Withholding, Applicability = TaxApplicability.Purchases };
        var foreignWithholding = new Tax { TenantId = Guid.NewGuid(), Code = "FOREIGN", Category = TaxCategory.Withholding };
        fixture.WithholdingTaxes.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Tax, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Tax, bool>> predicate) => new[] { withholding, foreignWithholding }.Where(predicate.Compile()).ToList());
        var result = await fixture.Service.GetPostingOptionsAsync("Supplier");
        result.Accounts.Should().ContainSingle(x => x.Id == account.Id);
        result.BankAccounts.Should().ContainSingle(x => x.Id == bank.Id && x.GlAccountNumber == "2100");
        result.TaxGroups.Should().ContainSingle(x => x.Id == purchaseTax.Id);
        result.WithholdingTaxes.Should().ContainSingle(x => x.Id == withholding.Id);
        System.Text.Json.JsonSerializer.Serialize(result).Should().NotContain("Balance");
    }

    [Fact]
    public async Task ExternalUserCannotReadOrChangePostingDefaults()
    {
        var partner = Partner();
        var fixture = Fixture(partner, external: true);
        var read = () => fixture.Service.GetPostingOptionsAsync();
        await read.Should().ThrowAsync<UnauthorizedAccessException>();
        var write = () => fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName, PostingDefaults = new()
        });
        await write.Should().ThrowAsync<UnauthorizedAccessException>();
        fixture.Partners.Verify(x => x.UpdateAsync(It.IsAny<BusinessPartner>()), Times.Never);
    }

    [Fact]
    public async Task OmittedCustomerCreditLimitSurvivesAnExternalLegacyProfileUpdate()
    {
        var partner = Partner();
        partner.PartnerType = "Customer";
        partner.CreditLimit = 100m;
        var fixture = Fixture(partner, external: true);
        await fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto { PartnerName = partner.PartnerName });
        partner.CreditLimit.Should().Be(100m);
    }

    [Theory]
    [InlineData(ProcurementMasterDataResourceType.SupplierProfile, "DefaultApAccountId")]
    [InlineData(ProcurementMasterDataResourceType.SupplierBankDetails, "DefaultBankAccountId")]
    [InlineData(ProcurementMasterDataResourceType.SupplierTaxDetails, "DefaultTaxGroupId")]
    [InlineData(ProcurementMasterDataResourceType.SupplierTaxDetails, "DefaultWithholdingTaxId")]
    public void StagedPostingDefaultsFlattenToOnlyTheSelectedResourceFields(ProcurementMasterDataResourceType resource, string field)
    {
        var partner = Partner();
        var account = Guid.NewGuid();
        var payload = System.Text.Json.JsonSerializer.Serialize(new { postingDefaults = new Dictionary<string, Guid> { [field] = account } });
        var normalized = NormalizeStagedPatch(partner, resource, payload);
        using var document = System.Text.Json.JsonDocument.Parse(normalized);
        document.RootElement.EnumerateObject().Should().ContainSingle();
        document.RootElement.GetProperty(field).GetGuid().Should().Be(account);
        typeof(BusinessPartner).GetProperty(field)!.GetValue(partner).Should().Be(account);
    }

    [Theory]
    [InlineData("DefaultTaxGroupId")]
    [InlineData("PartnerName")]
    public void StagedNestedDefaultsCannotEscapeTheirContractOrResourceWhitelist(string field)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { postingDefaults = new Dictionary<string, object> { [field] = Guid.NewGuid() } });
        var action = () => NormalizeStagedPatch(Partner(), ProcurementMasterDataResourceType.SupplierProfile, payload);
        action.Should().Throw<System.Reflection.TargetInvocationException>()
            .Which.InnerException.Should().BeOfType<ProcurementMasterDataChangeValidationException>()
            .Which.Code.Should().Be("FIELD_NOT_ALLOWED");
    }

    [Fact]
    public void StagedFlatAndNestedDuplicateDefaultsAreRejected()
    {
        var account = Guid.NewGuid();
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            defaultApAccountId = account, postingDefaults = new { defaultApAccountId = account }
        });
        var action = () => NormalizeStagedPatch(Partner(), ProcurementMasterDataResourceType.SupplierProfile, payload);
        action.Should().Throw<System.Reflection.TargetInvocationException>()
            .Which.InnerException.Should().BeOfType<ProcurementMasterDataChangeValidationException>()
            .Which.Code.Should().Be("PATCH_DUPLICATE_FIELD");
    }

    private static string NormalizeStagedPatch(BusinessPartner partner, ProcurementMasterDataResourceType resource, string payload) =>
        (string)typeof(ProcurementMasterDataChangeService).GetMethod("NormalizeAndValidatePatch",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null,
            [partner, resource, ProcurementMasterDataTargetKind.BusinessPartner, payload])!;

    [Theory]
    [InlineData("Supplier")]
    [InlineData("Customer")]
    public async Task LegacyRoleChangeCannotBypassCanonicalRoleWorkflow(string originalRole)
    {
        var partner = Partner();
        partner.PartnerType = originalRole;
        partner.DefaultApAccountId = Guid.NewGuid();
        partner.DefaultArAccountId = Guid.NewGuid();
        var id = partner.Id;
        var code = partner.PartnerCode;
        var ap = partner.DefaultApAccountId;
        var ar = partner.DefaultArAccountId;
        var fixture = Fixture(partner);

        var update = () => fixture.Service.UpdateAsync(id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName, PartnerType = BusinessPartnerRoles.CustomerAndSupplier
        });

        await update.Should().ThrowAsync<InvalidOperationException>().WithMessage("*canonical Finance role workflow*");
        partner.Id.Should().Be(id);
        partner.PartnerCode.Should().Be(code);
        partner.DefaultApAccountId.Should().Be(ap);
        partner.DefaultArAccountId.Should().Be(ar);
        partner.PartnerType.Should().Be(originalRole);
        fixture.Partners.Verify(x => x.CreateAsync(It.IsAny<BusinessPartner>()), Times.Never);
    }

    [Fact]
    public async Task ReceivablesEditDoesNotOverwritePayables()
    {
        var partner = Partner();
        partner.PartnerType = BusinessPartnerRoles.CustomerAndSupplier;
        partner.DefaultApAccountId = Guid.NewGuid();
        var ap = partner.DefaultApAccountId;
        var fixture = Fixture(partner);
        var ar = new Account { Id = Guid.NewGuid(), TenantId = partner.TenantId,
            AccountType = AccountType.Asset, Status = AccountStatus.Active, IsControlAccount = true };
        fixture.Accounts.Setup(x => x.GetByIdAsync(ar.Id)).ReturnsAsync(ar);

        await fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName,
            ReceivablesDefaults = new() { DefaultArAccountId = ar.Id }
        });
        partner.DefaultArAccountId.Should().Be(ar.Id);
        partner.DefaultApAccountId.Should().Be(ap);
        BusinessPartnerPostingDefaults.Apply(partner, new() { DefaultApAccountId = Guid.NewGuid() });
        partner.DefaultArAccountId.Should().Be(ar.Id);
    }

    [Fact]
    public async Task ExternalUsersCannotChangeRolesOrReceivablesDefaultsOrReadAnotherUser()
    {
        var partner = Partner();
        var fixture = Fixture(partner, external: true);
        var role = () => fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
            { PartnerName = partner.PartnerName, PartnerType = BusinessPartnerRoles.CustomerAndSupplier });
        await role.Should().ThrowAsync<UnauthorizedAccessException>();
        partner.PartnerType.Should().Be("Supplier");
        var defaults = () => fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
            { PartnerName = partner.PartnerName, ReceivablesDefaults = new() });
        await defaults.Should().ThrowAsync<UnauthorizedAccessException>();
        var lookup = () => fixture.Service.GetByUserIdAsync(Guid.NewGuid());
        await lookup.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("liability")]
    [InlineData("inactive")]
    public async Task ReceivablesAccountMustBeEligibleTenantAsset(string invalid)
    {
        var partner = Partner();
        partner.PartnerType = BusinessPartnerRoles.CustomerAndSupplier;
        var fixture = Fixture(partner);
        var account = new Account { Id = Guid.NewGuid(), TenantId = invalid == "foreign" ? Guid.NewGuid() : partner.TenantId,
            AccountType = invalid == "liability" ? AccountType.Liability : AccountType.Asset,
            Status = invalid == "inactive" ? AccountStatus.Inactive : AccountStatus.Active, IsControlAccount = true };
        fixture.Accounts.Setup(x => x.GetByIdAsync(account.Id)).ReturnsAsync(account);
        var update = () => fixture.Service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
            { PartnerName = partner.PartnerName, ReceivablesDefaults = new() { DefaultArAccountId = account.Id } });
        await update.Should().ThrowAsync<InvalidOperationException>();
        partner.DefaultArAccountId.Should().BeNull();
    }

    [Fact]
    public void RoleRemovalIsRejectedAndLegacyRolesKeepTheirMeaning()
    {
        var remove = () => BusinessPartnerRoles.ValidateRoleChange(BusinessPartnerRoles.CustomerAndSupplier, "Supplier");
        remove.Should().Throw<InvalidOperationException>();
        BusinessPartnerRoles.HasCustomer("Supplier").Should().BeFalse();
        BusinessPartnerRoles.HasSupplier("Customer").Should().BeFalse();
        BusinessPartnerRoles.CanProcure("Contractor").Should().BeTrue();
        BusinessPartnerRoles.HasCustomer("Both").Should().BeTrue("legacy AR eligibility remains compatible");
    }

    private static BusinessPartner Partner() => new()
    {
        Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), PartnerName = "Test supplier",
        PartnerCode = "SUP-TEST", PartnerType = "Supplier"
    };

    private static TestFixture Fixture(BusinessPartner partner, bool external = false)
    {
        var partners = new Mock<IBusinessPartnerRepository>();
        partners.Setup(x => x.GetByIdAsync(partner.Id)).ReturnsAsync(partner);
        partners.Setup(x => x.UpdateAsync(It.IsAny<BusinessPartner>())).ReturnsAsync((BusinessPartner value) => value);
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(x => x.TenantId).Returns(partner.TenantId);
        current.SetupGet(x => x.IsExternalUser).Returns(external);
        current.SetupGet(x => x.IsAuthenticated).Returns(true);
        current.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(x => x.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
        var accounts = new Mock<IAccountRepository>();
        var banks = new Mock<IGenericRepository<BankAccount>>();
        var taxes = new Mock<IGenericRepository<TaxGroup>>();
        var withholdingTaxes = new Mock<IGenericRepository<Tax>>();
        withholdingTaxes.Setup(repository => repository.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Tax, bool>>>()))
            .ReturnsAsync(Array.Empty<Tax>());
        var unit = new Mock<IUnitOfWork>();
        unit.SetupGet(x => x.Accounts).Returns(accounts.Object);
        unit.Setup(x => x.Repository<BankAccount>()).Returns(banks.Object);
        unit.Setup(x => x.Repository<TaxGroup>()).Returns(taxes.Object);
        unit.Setup(x => x.Repository<Tax>()).Returns(withholdingTaxes.Object);
        var service = new BusinessPartnerService(partners.Object, Mock.Of<IBusinessPartnerContactRepository>(),
            current.Object, Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IPaymentTermRepository>(), NullLogger<BusinessPartnerService>.Instance, unit.Object, access.Object);
        return new(service, partners, accounts, banks, taxes, withholdingTaxes);
    }

    private sealed record TestFixture(BusinessPartnerService Service, Mock<IBusinessPartnerRepository> Partners,
        Mock<IAccountRepository> Accounts, Mock<IGenericRepository<BankAccount>> Banks, Mock<IGenericRepository<TaxGroup>> Taxes,
        Mock<IGenericRepository<Tax>> WithholdingTaxes);
}
