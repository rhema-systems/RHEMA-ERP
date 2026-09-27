using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CanonicalVendorInvoiceBusinessPartnerTests
{
    [Fact]
    public async Task Create_captures_canonical_partner_role_profile_and_identity_snapshot()
    {
        await using var fixture = new Fixture();
        var (partner, role, profile) = await fixture.AddReadyPartnerAsync();

        var created = await fixture.Service.CreateAsync(fixture.Request(partner.Id));

        created.BusinessPartnerId.Should().Be(partner.Id);
        created.BusinessPartnerRoleId.Should().Be(role.Id);
        created.BusinessPartnerApProfileVersionId.Should().Be(profile.Id);
        created.BusinessPartnerCode.Should().Be(partner.PartnerCode);
        created.SupplierName.Should().Be(partner.PartnerName);
        created.BusinessPartnerLegalName.Should().Be(partner.LegalName);
        created.BusinessPartnerTaxIdentificationNumber.Should().Be(partner.TaxIdentificationNumber);
        fixture.Context.Suppliers.Should().BeEmpty("Finance must not create a parallel supplier identity");
    }

    [Fact]
    public async Task Create_fails_closed_when_no_approved_profile_is_effective_on_invoice_date()
    {
        await using var fixture = new Fixture();
        var (partner, _, profile) = await fixture.AddReadyPartnerAsync();
        profile.EffectiveFrom = new DateTime(2027, 1, 1);
        await fixture.Context.SaveChangesAsync();

        var create = () => fixture.Service.CreateAsync(fixture.Request(partner.Id));

        await create.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP_PROFILE_REQUIRED: No approved AP profile is effective on 24/09/2026.");
        fixture.Context.VendorInvoices.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_requires_explicit_role_when_supplier_and_contractor_roles_are_both_active()
    {
        await using var fixture = new Fixture();
        var (partner, supplierRole, _) = await fixture.AddReadyPartnerAsync();
        var contractorRole = fixture.Role(partner.Id, BusinessPartnerRoleType.Contractor);
        fixture.Context.Set<BusinessPartnerRole>().Add(contractorRole);
        fixture.Context.Set<BusinessPartnerApProfileVersion>().Add(fixture.Profile(contractorRole.Id));
        await fixture.Context.SaveChangesAsync();

        var ambiguous = () => fixture.Service.CreateAsync(fixture.Request(partner.Id));
        await ambiguous.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Select the Supplier or Contractor role*");

        var request = fixture.Request(partner.Id);
        request.BusinessPartnerRoleId = supplierRole.Id;
        (await fixture.Service.CreateAsync(request)).BusinessPartnerRoleId.Should().Be(supplierRole.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_accounting_date_captures_effective_profile_and_preserves_explicit_choices(bool explicitTerms)
    {
        await using var fixture = new Fixture();
        var (partner, role, oldProfile) = await fixture.AddReadyPartnerAsync();
        var oldTerm = new PaymentTerm
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Code = "OLD30", Name = "Old 30 days",
            DueDays = 30, ApplicableTo = "Supplier"
        };
        var newTerm = new PaymentTerm
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Code = "NEW60", Name = "New 60 days",
            DueDays = 60, DiscountPercent = 2m, DiscountDays = 10, ApplicableTo = "Supplier"
        };
        oldProfile.EffectiveTo = new DateTime(2026, 9, 30);
        oldProfile.PaymentTermId = oldTerm.Id;
        var nextProfile = fixture.Profile(role.Id);
        nextProfile.VersionNumber = 2;
        nextProfile.EffectiveFrom = new DateTime(2026, 10, 1);
        nextProfile.PaymentTermId = newTerm.Id;
        fixture.Context.Set<PaymentTerm>().AddRange(oldTerm, newTerm);
        fixture.Context.Set<BusinessPartnerApProfileVersion>().Add(nextProfile);
        await fixture.Context.SaveChangesAsync();
        var request = fixture.Request(partner.Id);
        var explicitAccountId = Guid.NewGuid();
        request.LineItems.Single().GLAccountId = explicitAccountId;
        var created = await fixture.Service.CreateAsync(request);
        created.BusinessPartnerApProfileVersionId.Should().Be(oldProfile.Id);
        var newDate = new DateTime(2026, 10, 2);

        var updated = await fixture.Service.UpdateAsync(new VendorInvoiceUpdateDto
        {
            Id = created.Id, InvoiceDate = newDate, CurrencyCode = "GHS",
            PaymentTermId = explicitTerms ? oldTerm.Id : null,
            ApplySupplierWithholdingDefaults = false,
            LineItems =
            [
                new VendorInvoiceLineItemCreateDto
                {
                    Id = created.LineItems.Single().Id, Description = "Changed accounting date",
                    Quantity = 1m, UnitPrice = 125m, GLAccountId = explicitAccountId,
                    TaxTreatment = TaxTreatment.OutOfScope
                }
            ]
        });

        var expectedTerm = explicitTerms ? oldTerm : newTerm;
        updated.BusinessPartnerRoleId.Should().Be(role.Id);
        updated.BusinessPartnerApProfileVersionId.Should().Be(nextProfile.Id);
        updated.PaymentTermId.Should().Be(expectedTerm.Id);
        updated.PaymentTermsDays.Should().Be(expectedTerm.DueDays);
        updated.DueDate.Should().Be(newDate.AddDays(expectedTerm.DueDays));
        updated.EarlyPaymentDiscountPercentage.Should().Be(expectedTerm.DiscountPercent);
        updated.LineItems.Single().GLAccountId.Should().Be(explicitAccountId);
        var persisted = await fixture.Context.VendorInvoices.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        persisted.BusinessPartnerApProfileVersionId.Should().Be(nextProfile.Id);
    }

    [Theory]
    [InlineData(BusinessPartnerRoleType.Supplier, "Supplier", true)]
    [InlineData(BusinessPartnerRoleType.Supplier, "Vendor", true)]
    [InlineData(BusinessPartnerRoleType.Contractor, "Contractor", true)]
    [InlineData(BusinessPartnerRoleType.Contractor, "All", true)]
    [InlineData(BusinessPartnerRoleType.Supplier, "Supplier", false)]
    [InlineData(BusinessPartnerRoleType.Supplier, "Vendor", false)]
    [InlineData(BusinessPartnerRoleType.Contractor, "Contractor", false)]
    public async Task Defaults_create_and_update_accept_terms_for_the_actual_ap_role(
        BusinessPartnerRoleType roleType, string applicability, bool profileTerm)
    {
        await using var fixture = new Fixture();
        var (partner, role, profile) = await fixture.AddReadyPartnerAsync();
        role.RoleType = roleType;
        var term = new PaymentTerm
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Code = "ROLE45", Name = "Role 45 days",
            DueDays = 45, ApplicableTo = applicability, IsDefault = !profileTerm
        };
        profile.PaymentTermId = profileTerm ? term.Id : null;
        fixture.Context.Set<PaymentTerm>().Add(term);
        await fixture.Context.SaveChangesAsync();

        var defaults = await fixture.Service.GetSupplierDefaultsAsync(partner.Id,
            invoiceDate: new DateTime(2026, 9, 24), businessPartnerRoleId: role.Id);
        var created = await fixture.Service.CreateAsync(fixture.Request(partner.Id));
        var updated = await fixture.Service.UpdateAsync(new VendorInvoiceUpdateDto
        {
            Id = created.Id, InvoiceDate = new DateTime(2026, 9, 25), CurrencyCode = "GHS",
            PaymentTermId = term.Id, ApplySupplierWithholdingDefaults = false,
            LineItems =
            [
                new VendorInvoiceLineItemCreateDto
                {
                    Id = created.LineItems.Single().Id, Description = "Role applicable terms",
                    Quantity = 1m, UnitPrice = 125m, TaxTreatment = TaxTreatment.OutOfScope
                }
            ]
        });

        defaults!.PaymentTermsDays.Should().Be(45);
        created.PaymentTermId.Should().Be(term.Id);
        created.PaymentTermsDays.Should().Be(45);
        updated.PaymentTermId.Should().Be(term.Id);
        updated.PaymentTermsDays.Should().Be(45);
        updated.BusinessPartnerRoleId.Should().Be(role.Id);
    }

    [Theory]
    [InlineData(BusinessPartnerRoleType.Supplier, "Customer")]
    [InlineData(BusinessPartnerRoleType.Supplier, "Contractor")]
    [InlineData(BusinessPartnerRoleType.Contractor, "Vendor")]
    public async Task Create_rejects_payment_terms_for_a_different_role(BusinessPartnerRoleType roleType, string applicability)
    {
        await using var fixture = new Fixture();
        var (partner, role, profile) = await fixture.AddReadyPartnerAsync();
        role.RoleType = roleType;
        var term = new PaymentTerm
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Code = "WRONGROLE", Name = "Other role",
            DueDays = 45, ApplicableTo = applicability
        };
        profile.PaymentTermId = term.Id;
        fixture.Context.Set<PaymentTerm>().Add(term);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.CreateAsync(fixture.Request(partner.Id));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not applicable*");
        fixture.Context.VendorInvoices.Should().BeEmpty();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public VendorInvoiceService Service { get; }

        public Fixture()
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            var current = new Mock<ICurrentUserService>();
            current.SetupGet(x => x.TenantId).Returns(TenantId);
            current.SetupGet(x => x.UserName).Returns("canonical-ap-test");
            var numbering = new Mock<IDocumentNumberingService>();
            numbering.Setup(x => x.GenerateAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(),
                    It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => $"VI-{Guid.NewGuid():N}");
            Service = new VendorInvoiceService(
                new UnitOfWork(Context), current.Object, Mock.Of<IInventoryValuationService>(),
                NullLogger<VendorInvoiceService>.Instance, numbering.Object, Mock.Of<IWorkflowService>());
        }

        public async Task<(BusinessPartner Partner, BusinessPartnerRole Role, BusinessPartnerApProfileVersion Profile)>
            AddReadyPartnerAsync()
        {
            var partner = new BusinessPartner
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PartnerCode = "BP-AP-001",
                PartnerName = "Canonical Supplier", LegalName = "Canonical Supplier Limited",
                TaxIdentificationNumber = "TIN-100200", PartnerType = "Supplier",
                RegistrationStatus = "Approved", ApprovalStatus = "Approved", IsActive = true,
                Currency = "GHS"
            };
            var role = Role(partner.Id, BusinessPartnerRoleType.Supplier);
            var profile = Profile(role.Id);
            Context.BusinessPartners.Add(partner);
            Context.Set<BusinessPartnerRole>().Add(role);
            Context.Set<BusinessPartnerApProfileVersion>().Add(profile);
            await Context.SaveChangesAsync();
            return (partner, role, profile);
        }

        public BusinessPartnerRole Role(Guid partnerId, BusinessPartnerRoleType type) => new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId, BusinessPartnerId = partnerId,
            RoleType = type, Status = BusinessPartnerRoleStatus.Active,
            ActiveFromUtc = new DateTime(2025, 1, 1)
        };

        public BusinessPartnerApProfileVersion Profile(Guid roleId) => new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId, BusinessPartnerRoleId = roleId,
            VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Approved,
            EffectiveFrom = new DateTime(2025, 1, 1), SubjectToWithholding = false,
            ApprovedAtUtc = new DateTime(2025, 1, 1), ApprovedById = Guid.NewGuid()
        };

        public VendorInvoiceCreateDto Request(Guid partnerId) => new()
        {
            BusinessPartnerId = partnerId,
            InvoiceDate = new DateTime(2026, 9, 24),
            CurrencyCode = "GHS",
            ApplySupplierWithholdingDefaults = false,
            LineItems =
            [
                new VendorInvoiceLineItemCreateDto
                {
                    Description = "Canonical AP test", Quantity = 1m, UnitPrice = 125m,
                    TaxTreatment = TaxTreatment.OutOfScope
                }
            ]
        };

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
