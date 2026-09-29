using System.Text;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.Taxation;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Release-gate coverage for the Finance-owned WHT compliance lifecycle. These tests deliberately
/// start from canonical AP payments and posted journals so a future change cannot quietly turn a
/// certificate or remittance record into a second accounting source.
/// </summary>
public sealed class WhtComplianceLifecycleTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-WHT")]
    [Trait("Category", "Tax")]
    public async Task ApInvoice_ShouldResolveExpectedWhtRateAndAccountFromTenantConfiguration()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        var payable = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = "2200",
            AccountNumber = "2200",
            AccountName = "WHT Payable",
            AccountType = AccountType.Liability,
            CurrencyCode = "GHS"
        };
        fixture.Tax.TaxPayableAccountId = payable.Id;
        db.Accounts.Add(payable);
        await db.SaveChangesAsync();

        var currentUser = CreateCurrentUser(tenantId);
        var service = new VendorInvoiceService(
            new UnitOfWork(db),
            currentUser.Object,
            Mock.Of<IInventoryValuationService>(),
            Mock.Of<ILogger<VendorInvoiceService>>(),
            CreateDocumentNumberingService(),
            Mock.Of<IWorkflowService>());

        var invoice = await service.CreateAsync(new VendorInvoiceCreateDto
        {
            BusinessPartnerId = fixture.Partner.Id,
            SupplierInvoiceNumber = "SUP-WHT-CONFIG-001",
            InvoiceDate = new DateTime(2026, 7, 10),
            DueDate = new DateTime(2026, 8, 9),
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            WithholdingTaxId = fixture.Tax.Id,
            WithholdingContractReference = "CONTRACT-001",
            WithholdingSupplyCategory = WhtSupplyCategory.Services,
            // Deliberately hostile compatibility values prove that server configuration wins.
            WithholdingTaxRate = 99m,
            WithholdingTaxAccountId = Guid.NewGuid(),
            LineItems = new List<VendorInvoiceLineItemCreateDto>
            {
                new()
                {
                    LineItemType = "Expense",
                    Description = "Configured WHT invoice",
                    Quantity = 1m,
                    UnitPrice = 1_000m,
                    TaxTreatment = TaxTreatment.Exempt
                }
            }
        });

        invoice.WithholdingTaxId.Should().Be(fixture.Tax.Id);
        invoice.WithholdingTaxRate.Should().Be(7.5m);
        invoice.WithholdingTaxAccountId.Should().Be(payable.Id);
        invoice.WithholdingTaxAmount.Should().Be(75m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-WHT")]
    [Trait("Category", "Tax")]
    public async Task Calculation_ShouldCatchUpTheUnwithheldAggregateWhenTheAnnualThresholdIsExceeded()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);

        // Below-threshold payments carry the tax/base snapshot even though no amount was withheld.
        // That is what lets the server enforce the annual supplier aggregate on later payments.
        SeedPostedWhtPayment(db, fixture, "VP-WHT-PRIOR", new DateTime(2026, 3, 5), 1_500m, 0m);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var below = await service.CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id,
            BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 5, 1),
            TaxableBase = 400m,
            ContractReference = "CONTRACT-001",
            SupplyCategory = WhtSupplyCategory.Services
        });
        var crossing = await service.CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id,
            BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 5, 1),
            TaxableBase = 600m,
            ContractReference = "CONTRACT-001",
            SupplyCategory = WhtSupplyCategory.Services
        });
        var newYear = await service.CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id,
            BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2027, 1, 10),
            TaxableBase = 1_000m,
            ContractReference = "CONTRACT-001",
            SupplyCategory = WhtSupplyCategory.Services
        });

        below.ThresholdApplied.Should().BeFalse();
        below.WithholdingAmount.Should().Be(0m);
        crossing.CumulativeBefore.Should().Be(1_500m);
        crossing.CumulativeAfter.Should().Be(2_100m);
        crossing.ThresholdApplied.Should().BeTrue();
        crossing.WithholdingAmount.Should().Be(157.50m, "GRA requires catch-up on the full qualifying 2,100 aggregate");
        crossing.CurrentPaymentTaxableBase.Should().Be(600m);
        crossing.CatchUpTaxableBase.Should().Be(1_500m);
        crossing.CatchUpWithholdingAmount.Should().Be(112.50m);
        newYear.CumulativeBefore.Should().Be(0m, "payment-date calendar years are separate statutory aggregates");
        newYear.ThresholdApplied.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-WHT")]
    [Trait("Category", "Tax")]
    public async Task Calculation_ShouldExcludeDraftsButAggregateRelatedSupplyAcrossContractNumbers()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);

        SeedPostedWhtPayment(db, fixture, "VP-WHT-IN-SCOPE", new DateTime(2026, 3, 5), 500m, 0m);
        SeedPostedWhtPayment(
            db,
            fixture,
            "VP-WHT-OTHER-CONTRACT",
            new DateTime(2026, 3, 6),
            5_000m,
            375m,
            "CONTRACT-OTHER",
            WhtSupplyCategory.Services);
        SeedUnpostedWhtPayment(db, fixture, "VP-WHT-DRAFT", new DateTime(2026, 3, 7), 10_000m);
        await db.SaveChangesAsync();

        var result = await CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id,
            BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 5, 1),
            TaxableBase = 1_000m,
            ContractReference = "contract-001",
            SupplyCategory = WhtSupplyCategory.Services
        });

        result.CumulativeBefore.Should().Be(5_500m);
        result.CumulativeAfter.Should().Be(6_500m);
        result.ThresholdApplied.Should().BeTrue();
        result.WithholdingAmount.Should().Be(112.50m, "487.50 cumulative liability less 375 already withheld");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-WHT")]
    [Trait("Category", "Tax")]
    public async Task Calculation_ShouldUseTenantConfiguredStatutoryYearBoundary()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        await db.SaveChangesAsync();
        var settings = await db.FinanceSettings.SingleAsync(item => item.TenantId == tenantId);
        settings.WhtStatutoryYearStartMonth = 7;
        settings.WhtStatutoryYearStartDay = 1;
        SeedPostedWhtPayment(db, fixture, "VP-WHT-JUNE", new DateTime(2026, 6, 30), 1_500m, 0m);
        SeedPostedWhtPayment(db, fixture, "VP-WHT-JULY", new DateTime(2026, 7, 1), 400m, 0m);
        await db.SaveChangesAsync();

        var result = await CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id,
            BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 7, 2),
            TaxableBase = 1_000m,
            ContractReference = "CONTRACT-001",
            SupplyCategory = WhtSupplyCategory.Services
        });

        result.StatutoryPeriodStart.Should().Be(new DateTime(2026, 7, 1));
        result.StatutoryPeriodEnd.Should().Be(new DateTime(2027, 6, 30));
        result.CumulativeBefore.Should().Be(400m, "the June payment belongs to the prior statutory year");
        result.ThresholdApplied.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-WHT")]
    [Trait("Category", "Tax")]
    public async Task CertificateLifecycle_ShouldRetainImmutableVersionsOnReissueAndCancellation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        var payment = SeedPostedWhtPayment(db, fixture, "VP-WHT-001", new DateTime(2026, 7, 18));
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var issued = await service.GenerateApCertificateAsync(payment.Id, new GenerateWhtCertificateDto
        {
            CertificateNumber = "TDC-WHT-2026-0001",
            CertificateDate = new DateTime(2026, 7, 19)
        });
        var reissued = await service.ReissueApCertificateAsync(payment.Id, new ReissueWhtCertificateDto
        {
            CertificateDate = new DateTime(2026, 7, 20),
            Reason = "Supplier requested corrected certificate evidence."
        });
        var cancelled = await service.CancelApCertificateAsync(payment.Id, new CancelWhtCertificateDto
        {
            Reason = "Certificate cancelled after Finance compliance review."
        });

        issued.VersionNumber.Should().Be(1);
        reissued.VersionNumber.Should().Be(2);
        reissued.Versions.Should().Contain(version => version.VersionNumber == 1 && version.Status == "Superseded");
        cancelled.CertificateStatus.Should().Be("Cancelled");
        cancelled.Versions.Should().HaveCount(2);
        (await db.WithholdingTaxCertificates.CountAsync(item => item.VendorPaymentId == payment.Id)).Should().Be(2);

        var originalHtml = await service.GetApCertificateHtmlAsync(payment.Id, issued.CertificateId);
        var cancelledHtml = await service.GetApCertificateHtmlAsync(payment.Id, cancelled.CertificateId);
        originalHtml.Should().Contain("SUPERSEDED");
        originalHtml.Should().Contain("TDC-WHT-2026-0001", "the original issued snapshot remains printable for audit");
        cancelledHtml.Should().Contain("CANCELLED");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-WHT")]
    [Trait("Category", "Tax")]
    public async Task RemittanceLifecycle_ShouldSnapshotLiabilityEvidenceAndPreventCertificateDrift()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        var payment = SeedPostedWhtPayment(db, fixture, "VP-WHT-002", new DateTime(2026, 7, 22));
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        await service.GenerateApCertificateAsync(payment.Id, new GenerateWhtCertificateDto());

        var draft = await service.CreateRemittanceAsync(new CreateWhtRemittanceDto
        {
            PeriodFrom = new DateTime(2026, 7, 1),
            PeriodTo = new DateTime(2026, 7, 31),
            CurrencyCode = "GHS",
            VendorPaymentIds = new List<Guid> { payment.Id },
            Notes = "July 2026 WHT evidence batch"
        });

        draft.Status.Should().Be("Draft");
        draft.TotalWithholdingAmount.Should().Be(75m);
        draft.Lines.Should().ContainSingle(line => line.VendorPaymentId == payment.Id && line.CertificateId.HasValue);
        var reissue = () => service.ReissueApCertificateAsync(payment.Id, new ReissueWhtCertificateDto
        {
            Reason = "Attempted change after remittance snapshot creation."
        });
        await reissue.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*active WHT remittance*");

        var submitted = await service.SubmitRemittanceAsync(draft.Id, new SubmitWhtRemittanceDto
        {
            SubmissionReference = "GRA-WHT-JUL-2026"
        });
        var paid = await service.MarkRemittancePaidAsync(draft.Id, new PayWhtRemittanceDto
        {
            PaymentDate = new DateTime(2026, 8, 14),
            PaymentReference = "BANK-PAY-260814-01",
            AuthorityReceiptReference = "GRA-RCT-260814-01"
        });

        submitted.Status.Should().Be("Submitted");
        paid.Status.Should().Be("Paid");
        (await service.GetUnremittedLiabilitiesAsync(null, null, "GHS")).Should().BeEmpty();
        var register = Encoding.UTF8.GetString((await service.ExportRegisterAsync(null, null)).Content);
        register.Should().Contain("GRA-WHT-JUL-2026");
        register.Should().Contain("BANK-PAY-260814-01");
    }

    private static WithholdingTaxCertificateService CreateService(ApplicationDbContext db, Guid tenantId)
        => new(db, CreateCurrentUser(tenantId).Object);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"wht-compliance-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(user => user.UserName).Returns("tdc.finance.tester");
        currentUser.SetupGet(user => user.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(user => user.Roles).Returns(Array.Empty<string>());
        currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
        return currentUser;
    }

    private static IDocumentNumberingService CreateDocumentNumberingService()
    {
        var numbering = new Mock<IDocumentNumberingService>();
        numbering
            .Setup(service => service.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("VI-WHT-CONFIG-001");
        return numbering.Object;
    }

    private static WhtFixture SeedFoundation(ApplicationDbContext db, Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Tema Development Corporation",
            Code = "TDC",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = "SUP-WHT-001",
            PartnerName = "TDC Test Supplier",
            LegalName = "TDC Test Supplier Limited",
            TaxIdentificationNumber = "C0000000001",
            PartnerType = "Supplier",
            RegistrationStatus = "Approved",
            ApprovalStatus = "Approved",
            Currency = "GHS",
            IsActive = true
        };
        var role = new BusinessPartnerRole
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = partner.Id,
            RoleType = BusinessPartnerRoleType.Supplier, Status = BusinessPartnerRoleStatus.Active,
            ActiveFromUtc = new DateTime(2025, 1, 1)
        };
        var profile = new BusinessPartnerApProfileVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerRoleId = role.Id,
            VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Approved,
            EffectiveFrom = new DateTime(2025, 1, 1), SubjectToWithholding = true,
            ApprovedAtUtc = new DateTime(2025, 1, 1), ApprovedById = Guid.NewGuid()
        };
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "BASE", Name = "Ghana Statutory Primary",
            Purpose = "Ghana Statutory", BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
            IsDefault = true, IsActive = true, AllowsPosting = true
        };
        var tax = new Tax
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "WHT-SERV",
            Name = "Resident Services WHT",
            Rate = 7.5m,
            EffectiveFrom = new DateTime(2026, 1, 1),
            Applicability = TaxApplicability.Purchases,
            Category = TaxCategory.Withholding,
            ThresholdAmount = 2_000m,
            IsActive = true
        };
        db.BusinessPartners.Add(partner);
        db.Set<BusinessPartnerRole>().Add(role);
        db.Set<BusinessPartnerApProfileVersion>().Add(profile);
        db.Set<BusinessPartnerApWhtDefault>().Add(new BusinessPartnerApWhtDefault
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ApProfileVersionId = profile.Id,
            CategoryCode = "SERVICES", CategoryName = "Services",
            WithholdingTaxId = tax.Id, IsDefaultForAp = true, IsActive = true
        });
        db.AccountingBooks.Add(book);
        db.Taxes.Add(tax);
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS",
            WhtStatutoryYearStartMonth = 1, WhtStatutoryYearStartDay = 1
        });
        return new WhtFixture(tenantId, partner, role, profile, tax, book);
    }

    [Theory]
    [InlineData(10000, 2000, 12000, 10000, 750)]
    [InlineData(10000, 2000, 6000, 5000, 375)]
    [InlineData(9500, 1900, 11400, 9500, 712.5)]
    [InlineData(10000, 2000, 1200, 1000, 75)]
    public async Task Calculation_ShouldExcludeInvoiceTaxesAndProrateTheNetSupply(
        decimal net, decimal taxAmount, decimal grossSettled, decimal expectedBase, decimal expectedWht)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, InvoiceNumber = "WHT-VAT-2026",
            BusinessPartnerId = fixture.Partner.Id, BusinessPartnerRoleId = fixture.Role.Id,
            BusinessPartnerApProfileVersionId = fixture.Profile.Id, SupplierName = fixture.Partner.PartnerName,
            InvoiceDate = new DateTime(2026, 9, 1), CurrencyCode = "GHS", ExchangeRate = 1m,
            SubTotal = net, TaxAmount = taxAmount, TotalAmount = net + taxAmount,
            BaseCurrencyAmount = net + taxAmount, Status = VendorInvoiceStatus.Approved,
            WithholdingTaxId = fixture.Tax.Id, WithholdingTaxRate = 7.5m,
            WithholdingContractReference = "SERVICES-2026", WithholdingSupplyCategory = WhtSupplyCategory.Services
        };
        db.VendorInvoices.Add(invoice);
        await db.SaveChangesAsync();
        var result = await CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id, BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 9, 29), ContractReference = "SERVICES-2026",
            SupplyCategory = WhtSupplyCategory.Services, VendorInvoiceIds = new() { invoice.Id },
            TaxableBase = 999999m, // Client-supplied base is not authoritative for invoice settlements.
            InvoiceSettlements = new() { new() { VendorInvoiceId = invoice.Id, GrossSettlementAmount = grossSettled } }
        });
        result.CurrentPaymentTaxableBase.Should().Be(expectedBase);
        result.WithholdingAmount.Should().Be(expectedWht);
        result.CatchUpWithholdingAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Calculation_ExactlyAtThreshold_ShouldNotWithhold()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        await db.SaveChangesAsync();
        var result = await CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id, BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 9, 29), TaxableBase = 2000m,
            ContractReference = "EXACT-THRESHOLD", SupplyCategory = WhtSupplyCategory.Services
        });
        result.ThresholdApplied.Should().BeFalse();
        result.WithholdingAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Calculation_GraGoodsExample_ShouldWithhold84OnThirdContract()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        fixture.Tax.Rate = 3m;
        var first = SeedPostedWhtPayment(db, fixture, "VP-GOODS-1", new DateTime(2026, 1, 5), 1000m, 0m, "FIRST", WhtSupplyCategory.Goods);
        var second = SeedPostedWhtPayment(db, fixture, "VP-GOODS-2", new DateTime(2026, 3, 5), 900m, 0m, "SECOND", WhtSupplyCategory.Goods);
        first.WithholdingTaxRate = second.WithholdingTaxRate = 3m;
        await db.SaveChangesAsync();
        var result = await CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id, BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 8, 16), TaxableBase = 900m,
            ContractReference = "THIRD", SupplyCategory = WhtSupplyCategory.Goods
        });
        result.CumulativeBefore.Should().Be(1900m);
        result.WithholdingAmount.Should().Be(84m);
        result.CatchUpWithholdingAmount.Should().Be(57m);
        result.TaxableBase.Should().Be(2800m);
    }

    [Fact]
    public async Task Calculation_ShouldRejectBackdatedScopeAfterLaterPostedPayment()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        SeedPostedWhtPayment(db, fixture, "VP-LATER", new DateTime(2026, 9, 29));
        await db.SaveChangesAsync();
        var act = () => CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id, BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 9, 28), TaxableBase = 3000m,
            ContractReference = "BACKDATED", SupplyCategory = WhtSupplyCategory.Services
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*backdating*");
    }

    private static VendorPayment SeedPostedWhtPayment(
        ApplicationDbContext db,
        WhtFixture fixture,
        string paymentNumber,
        DateTime paymentDate,
        decimal taxableBase = 1_000m,
        decimal withholdingAmount = 75m,
        string contractReference = "CONTRACT-001",
        WhtSupplyCategory supplyCategory = WhtSupplyCategory.Services)
    {
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            JournalEntryNumber = $"JE-{paymentNumber}",
            JournalType = "System Generated",
            EntryDate = paymentDate,
            Description = $"Posted AP payment {paymentNumber}",
            SourceModule = "AP",
            SourceDocumentType = "VendorPayment",
            PostingStatus = "Posted",
            AccountingBookId = fixture.Book.Id,
            BookClassification = fixture.Book.Code
        };
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            InvoiceNumber = $"INV-{paymentNumber}", BusinessPartnerId = fixture.Partner.Id,
            BusinessPartnerRoleId = fixture.Role.Id,
            BusinessPartnerApProfileVersionId = fixture.Profile.Id,
            SupplierName = fixture.Partner.PartnerName, InvoiceDate = paymentDate,
            CurrencyCode = "GHS", ExchangeRate = 1m, SubTotal = taxableBase,
            TotalAmount = taxableBase, BaseCurrencyAmount = taxableBase,
            Status = VendorInvoiceStatus.Approved, ApprovalStatus = "Approved",
            WithholdingTaxId = fixture.Tax.Id,
            WithholdingContractReference = contractReference,
            WithholdingSupplyCategory = supplyCategory
        };
        var payment = new VendorPayment
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            PaymentNumber = paymentNumber,
            BusinessPartnerId = fixture.Partner.Id,
            BusinessPartnerRoleId = fixture.Role.Id,
            BusinessPartnerApProfileVersionId = fixture.Profile.Id,
            BusinessPartnerCode = fixture.Partner.PartnerCode,
            BusinessPartnerName = fixture.Partner.PartnerName,
            BusinessPartnerLegalName = fixture.Partner.LegalName,
            BusinessPartnerTaxIdentificationNumber = fixture.Partner.TaxIdentificationNumber,
            PaymentDate = paymentDate,
            TotalAmount = taxableBase - withholdingAmount,
            AllocatedAmount = taxableBase - withholdingAmount,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            WithholdingTaxId = fixture.Tax.Id,
            WithholdingTaxRate = 7.5m,
            WithholdingTaxBaseAmount = taxableBase,
            WithholdingTaxAmount = withholdingAmount,
            WithholdingTaxThresholdAmount = 2_000m,
            WithholdingTaxThresholdApplied = true,
            Status = VendorPaymentStatus.Processed,
            JournalEntryId = journal.Id
        };
        journal.SourceDocumentId = payment.Id;
        var allocation = new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            VendorPaymentId = payment.Id, VendorPayment = payment,
            VendorInvoiceId = invoice.Id, VendorInvoice = invoice,
            AllocatedAmount = taxableBase - withholdingAmount,
            PaymentCurrencyAmount = taxableBase - withholdingAmount,
            SettlementFunctionalAmount = taxableBase,
            WithholdingTaxBaseFunctionalAmount = taxableBase,
            WithholdingTaxAmount = withholdingAmount,
            WithholdingTaxFunctionalAmount = withholdingAmount,
            AllocationDate = paymentDate
        };
        db.JournalEntries.Add(journal);
        db.VendorInvoices.Add(invoice);
        db.Set<VendorPayment>().Add(payment);
        db.Set<VendorPaymentAllocation>().Add(allocation);
        return payment;
    }

    [Fact]
    public async Task Calculation_UnselectedApprovedContract_ShouldQualifyThresholdWithoutPriorPayment()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        SeedUnpostedWhtPayment(db, fixture, "CURRENT", new DateTime(2026, 9, 1), 1200m);
        SeedUnpostedWhtPayment(db, fixture, "OTHER", new DateTime(2026, 9, 2), 900m);
        await db.SaveChangesAsync();
        var invoice = await db.VendorInvoices.SingleAsync(row => row.InvoiceNumber == "INV-CURRENT");
        invoice.WithholdingTaxRate = 7.5m;
        await db.SaveChangesAsync();
        var result = await CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id, BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 9, 29), ContractReference = "CONTRACT-001",
            SupplyCategory = WhtSupplyCategory.Services,
            InvoiceSettlements = new() { new() { VendorInvoiceId = invoice.Id, GrossSettlementAmount = 1200m } }
        });
        result.CumulativeBefore.Should().Be(0m);
        result.ThresholdApplied.Should().BeTrue();
        result.WithholdingAmount.Should().Be(90m);
    }

    [Fact]
    public async Task Calculation_LegacyPostedBaseMissing_ShouldRequireReconciliationNotCurrentInvoiceRecalculation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        SeedPostedWhtPayment(db, fixture, "LEGACY", new DateTime(2026, 9, 1), 1000m, 0m);
        await db.SaveChangesAsync();
        var allocation = await db.Set<VendorPaymentAllocation>().SingleAsync();
        allocation.WithholdingTaxBaseFunctionalAmount = null;
        allocation.VendorInvoice.SubTotal = 800m;
        allocation.VendorInvoice.TaxAmount = 200m;
        await db.SaveChangesAsync();
        var act = () => CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id, BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 9, 29), TaxableBase = 1200m,
            ContractReference = "CONTRACT-001", SupplyCategory = WhtSupplyCategory.Services
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*frozen net-supply basis*");
    }

    [Fact]
    public async Task Calculation_NonGhsFunctionalCurrency_ShouldNotApplyGhsThresholdAsLocalUnits()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        await db.SaveChangesAsync();
        (await db.FinanceSettings.SingleAsync()).BaseCurrency = "USD";
        await db.SaveChangesAsync();
        var act = () => CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id, BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 9, 29), TaxableBase = 3000m,
            ContractReference = "CONTRACT-001", SupplyCategory = WhtSupplyCategory.Services
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*denominated in GHS*");
    }

    [Fact]
    public async Task PublicPreview_WithoutInvoices_ShouldRejectUntrustedClientBaseWithoutCallingService()
    {
        var service = new Mock<IWithholdingTaxCertificateService>(MockBehavior.Strict);
        var result = await new WithholdingTaxCertificatesController(service.Object)
            .CalculateApWithholding(new WhtCalculationRequestDto { TaxableBase = 5000m }, CancellationToken.None);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Calculation_ForeignSelectedOrUnselectedContract_ShouldRequireStatutoryNotBookFx(bool selectForeign)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        SeedUnpostedWhtPayment(db, fixture, "GHS", new DateTime(2026, 9, 1), 1000m);
        SeedUnpostedWhtPayment(db, fixture, "USD", new DateTime(2026, 9, 2), 100m);
        await db.SaveChangesAsync();
        var invoices = await db.VendorInvoices.ToListAsync();
        var foreign = invoices.Single(row => row.InvoiceNumber == "INV-USD");
        foreign.CurrencyCode = "USD";
        foreign.ExchangeRateId = Guid.NewGuid();
        foreign.ExchangeRate = 10m;
        foreign.BaseCurrencyAmount = 1000m;
        invoices.ForEach(row => row.WithholdingTaxRate = 7.5m);
        await db.SaveChangesAsync();
        var selected = selectForeign ? foreign : invoices.Single(row => row.InvoiceNumber == "INV-GHS");
        var act = () => CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id, BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 9, 29), ContractReference = "CONTRACT-001",
            SupplyCategory = WhtSupplyCategory.Services,
            InvoiceSettlements = new() { new() { VendorInvoiceId = selected.Id, GrossSettlementAmount = selected.TotalAmount } }
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*statutory conversion evidence*");
    }

    [Fact]
    public async Task Calculation_ForeignHistoricalPayment_ShouldNotTreatCommercialBaseAsStatutoryEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        var payment = SeedPostedWhtPayment(db, fixture, "FOREIGN-HISTORY", new DateTime(2026, 9, 1), 1000m, 0m);
        payment.CurrencyCode = "USD";
        await db.SaveChangesAsync();
        var act = () => CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id, BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 9, 29), TaxableBase = 1200m,
            ContractReference = "CONTRACT-001", SupplyCategory = WhtSupplyCategory.Services
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*statutory conversion evidence*");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForeignHistory_ShouldBlockNewIssueReissueAndRemittanceButAllowReadAndCancellation(bool foreignPayment)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        var payment = SeedPostedWhtPayment(db, fixture, "LEGACY-FX", new DateTime(2026, 7, 22));
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var issued = await service.GenerateApCertificateAsync(payment.Id, new GenerateWhtCertificateDto());
        // Simulate pre-existing foreign history; no production history is rewritten by the service.
        if (foreignPayment) payment.CurrencyCode = "USD";
        else (await db.VendorInvoices.SingleAsync()).CurrencyCode = "USD";
        await db.SaveChangesAsync();
        (await service.GetApCertificateAsync(payment.Id)).Should().NotBeNull();
        var reissue = () => service.ReissueApCertificateAsync(payment.Id, new ReissueWhtCertificateDto { Reason = "Foreign statutory evidence requires review." });
        await reissue.Should().ThrowAsync<InvalidOperationException>().WithMessage("*statutory conversion evidence*");
        var remittance = () => service.CreateRemittanceAsync(new CreateWhtRemittanceDto
        {
            PeriodFrom = new DateTime(2026, 7, 1), PeriodTo = new DateTime(2026, 7, 31), CurrencyCode = "GHS",
            VendorPaymentIds = new() { payment.Id }
        });
        await remittance.Should().ThrowAsync<InvalidOperationException>().WithMessage("*statutory conversion evidence*");
        var cancelled = await service.CancelApCertificateAsync(payment.Id, new CancelWhtCertificateDto { Reason = "Preserve history while statutory evidence is reconciled." });
        cancelled.CertificateStatus.Should().Be("Cancelled");
        (await db.WithholdingTaxCertificates.CountAsync()).Should().Be(1);
        (await db.WithholdingTaxRemittances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ForeignHistory_ShouldBlockFirstCertificateAndExistingRemittanceTransitions()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        var payment = SeedPostedWhtPayment(db, fixture, "LEGACY-REMIT-FX", new DateTime(2026, 7, 22));
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var draft = await service.CreateRemittanceAsync(new CreateWhtRemittanceDto
        {
            PeriodFrom = new DateTime(2026, 7, 1), PeriodTo = new DateTime(2026, 7, 31), CurrencyCode = "GHS",
            VendorPaymentIds = new() { payment.Id }
        });
        payment.CurrencyCode = "USD";
        await db.SaveChangesAsync();
        var issue = () => service.GenerateApCertificateAsync(payment.Id, new GenerateWhtCertificateDto());
        await issue.Should().ThrowAsync<InvalidOperationException>().WithMessage("*statutory conversion evidence*");
        var submit = () => service.SubmitRemittanceAsync(draft.Id, new SubmitWhtRemittanceDto { SubmissionReference = "GRA-LEGACY-FX" });
        await submit.Should().ThrowAsync<InvalidOperationException>().WithMessage("*statutory conversion evidence*");
        (await db.WithholdingTaxRemittances.SingleAsync()).Status = WhtRemittanceStatus.Submitted;
        await db.SaveChangesAsync();
        var pay = () => service.MarkRemittancePaidAsync(draft.Id, new PayWhtRemittanceDto { PaymentDate = new DateTime(2026, 8, 14), PaymentReference = "BANK-LEGACY-FX" });
        await pay.Should().ThrowAsync<InvalidOperationException>().WithMessage("*statutory conversion evidence*");
        await service.CancelRemittanceAsync(draft.Id, new CancelWhtRemittanceDto { Reason = "Reconcile the legacy foreign statutory evidence." });
        (await db.WithholdingTaxRemittances.SingleAsync()).Status.Should().Be(WhtRemittanceStatus.Cancelled);
        (await db.WithholdingTaxCertificates.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Calculation_ShouldIncludeWholePaymentDayInContractQualification(bool selectedInvoiceHasTime)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        var paymentDate = new DateTime(2026, 9, 29);
        SeedUnpostedWhtPayment(db, fixture, "SAME-DAY-CURRENT",
            selectedInvoiceHasTime ? paymentDate.AddHours(12) : paymentDate,
            selectedInvoiceHasTime ? 3000m : 1000m);
        if (!selectedInvoiceHasTime)
            SeedUnpostedWhtPayment(db, fixture, "SAME-DAY-OTHER", paymentDate.AddHours(12), 2000m);
        await db.SaveChangesAsync();
        var invoice = await db.VendorInvoices.SingleAsync(item => item.InvoiceNumber == "INV-SAME-DAY-CURRENT");
        invoice.WithholdingTaxRate = 7.5m;
        await db.SaveChangesAsync();
        var result = await CreateService(db, tenantId).CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id, BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = paymentDate, ContractReference = "CONTRACT-001",
            SupplyCategory = WhtSupplyCategory.Services,
            InvoiceSettlements = new() { new() { VendorInvoiceId = invoice.Id, GrossSettlementAmount = 500m } }
        });
        result.CumulativeBefore.Should().Be(0m);
        result.ThresholdApplied.Should().BeTrue();
        result.WithholdingAmount.Should().Be(37.5m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GlReversedPayment_ShouldNotEnterHistoryIssueOrNewRemittance(bool reversedFlag)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        var payment = SeedPostedWhtPayment(db, fixture, "GL-REVERSED", new DateTime(2026, 7, 22), 1500m, 112.5m);
        var journal = db.JournalEntries.Local.Single();
        if (reversedFlag) journal.IsReversed = true;
        else journal.ReversalJournalEntryId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var calculation = await service.CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id, BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 7, 23), ContractReference = "CONTRACT-001",
            SupplyCategory = WhtSupplyCategory.Services, TaxableBase = 600m
        });
        calculation.CumulativeBefore.Should().Be(0m);
        calculation.WithholdingAmount.Should().Be(0m);
        (await service.GetUnremittedLiabilitiesAsync(null, null, "GHS")).Should().BeEmpty();
        var readWithoutHistory = () => service.GetApCertificateAsync(payment.Id);
        await readWithoutHistory.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not linked to a posted journal*");
        var issue = () => service.GenerateApCertificateAsync(payment.Id, new GenerateWhtCertificateDto());
        await issue.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not linked to a posted journal*");
        var create = () => service.CreateRemittanceAsync(new CreateWhtRemittanceDto
        {
            PeriodFrom = new DateTime(2026, 7, 1), PeriodTo = new DateTime(2026, 7, 31), CurrencyCode = "GHS",
            VendorPaymentIds = new() { payment.Id }
        });
        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*reversed*");
        (await db.WithholdingTaxCertificates.CountAsync()).Should().Be(0);
        (await db.WithholdingTaxRemittances.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GlReversedPayment_ShouldBlockReissueAndExistingRemittanceButPreserveCancellation(bool reversedFlag)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);
        var payment = SeedPostedWhtPayment(db, fixture, "GL-REVERSED-EXISTING", new DateTime(2026, 7, 22));
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        await service.GenerateApCertificateAsync(payment.Id, new GenerateWhtCertificateDto());
        var draft = await service.CreateRemittanceAsync(new CreateWhtRemittanceDto
        {
            PeriodFrom = new DateTime(2026, 7, 1), PeriodTo = new DateTime(2026, 7, 31), CurrencyCode = "GHS",
            VendorPaymentIds = new() { payment.Id }
        });
        // Simulate retained Posted status with independently reversed GL evidence.
        var journal = await db.JournalEntries.SingleAsync();
        if (reversedFlag) journal.IsReversed = true;
        else journal.ReversalJournalEntryId = Guid.NewGuid();
        await db.SaveChangesAsync();
        (await service.GetApCertificateAsync(payment.Id)).Should().NotBeNull();
        (await service.GetApCertificateHtmlAsync(payment.Id)).Should().NotBeNullOrWhiteSpace();
        (await service.GenerateApCertificateAsync(payment.Id, new GenerateWhtCertificateDto()))
            .CertificateStatus.Should().Be("Issued");
        (await db.WithholdingTaxCertificates.CountAsync()).Should().Be(1);
        var reissue = () => service.ReissueApCertificateAsync(payment.Id,
            new ReissueWhtCertificateDto { Reason = "Reconcile independently reversed source journal." });
        await reissue.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not linked to a posted journal*");
        var submit = () => service.SubmitRemittanceAsync(draft.Id,
            new SubmitWhtRemittanceDto { SubmissionReference = "GRA-REVERSED-SOURCE" });
        await submit.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not linked to a posted journal*");
        (await db.WithholdingTaxRemittances.SingleAsync()).Status = WhtRemittanceStatus.Submitted;
        await db.SaveChangesAsync();
        var pay = () => service.MarkRemittancePaidAsync(draft.Id,
            new PayWhtRemittanceDto { PaymentDate = new DateTime(2026, 8, 14), PaymentReference = "BANK-REVERSED-SOURCE" });
        await pay.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not linked to a posted journal*");
        // The service's serializable boundary clears tracking after each lifecycle mutation.
        // Reload the payment before retiring it so this test changes persisted source evidence
        // rather than a detached seeding instance.
        var paymentToRetire = await db.Set<VendorPayment>().SingleAsync(item => item.Id == payment.Id);
        paymentToRetire.Status = reversedFlag ? VendorPaymentStatus.Reversed : VendorPaymentStatus.Voided;
        paymentToRetire.PaymentDate = new DateTime(2026, 8, 31);
        paymentToRetire.BusinessPartnerName = "MUTATED SOURCE NAME";
        paymentToRetire.WithholdingTaxRate = 99m;
        paymentToRetire.WithholdingTaxBaseAmount = 9_999m;
        paymentToRetire.WithholdingTaxAmount = 999m;
        paymentToRetire.CurrencyCode = "USD";
        await db.SaveChangesAsync();
        (await service.GetApCertificatesAsync(new WhtCertificateQueryDto())).Items.Should().ContainSingle();
        (await service.GetApCertificateAsync(payment.Id)).Should().NotBeNull();
        (await service.GetApCertificateHtmlAsync(payment.Id)).Should().NotBeNullOrWhiteSpace();
        (await service.GenerateApCertificateAsync(payment.Id, new GenerateWhtCertificateDto()))
            .CertificateStatus.Should().Be("Issued");
        (await service.GetUnremittedLiabilitiesAsync(null, null, "GHS")).Should().BeEmpty();
        var retainedRegister = Encoding.UTF8.GetString((await service.ExportRegisterAsync(null, null)).Content);
        retainedRegister.Should().Contain("GL-REVERSED-EXISTING");
        retainedRegister.Should().Contain("2026-07-22");
        retainedRegister.Should().Contain(fixture.Partner.PartnerName);
        retainedRegister.Should().Contain("\"7.5\",\"1000.00\",\"75.00\",\"GHS\"");
        retainedRegister.Should().NotContain("MUTATED SOURCE NAME");
        retainedRegister.Should().NotContain("\"99\",\"9999.00\",\"999.00\",\"USD\"");
        await service.CancelRemittanceAsync(draft.Id,
            new CancelWhtRemittanceDto { Reason = "Reconcile independently reversed source evidence." });
        var cancelled = await service.CancelApCertificateAsync(payment.Id,
            new CancelWhtCertificateDto { Reason = "Preserve original certificate after source reversal." });
        cancelled.CertificateStatus.Should().Be("Cancelled");
        (await db.WithholdingTaxCertificates.CountAsync()).Should().Be(1);
        var issueAfterCancellation = () => service.GenerateApCertificateAsync(
            payment.Id, new GenerateWhtCertificateDto());
        await issueAfterCancellation.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reversed or voided*");
    }

    private static VendorPayment SeedUnpostedWhtPayment(
        ApplicationDbContext db,
        WhtFixture fixture,
        string paymentNumber,
        DateTime paymentDate,
        decimal taxableBase)
    {
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            InvoiceNumber = $"INV-{paymentNumber}", BusinessPartnerId = fixture.Partner.Id,
            BusinessPartnerRoleId = fixture.Role.Id,
            BusinessPartnerApProfileVersionId = fixture.Profile.Id,
            SupplierName = fixture.Partner.PartnerName, InvoiceDate = paymentDate,
            CurrencyCode = "GHS", ExchangeRate = 1m, SubTotal = taxableBase,
            TotalAmount = taxableBase, BaseCurrencyAmount = taxableBase,
            Status = VendorInvoiceStatus.Approved, ApprovalStatus = "Approved",
            WithholdingTaxId = fixture.Tax.Id,
            WithholdingContractReference = "CONTRACT-001",
            WithholdingSupplyCategory = WhtSupplyCategory.Services
        };
        var payment = new VendorPayment
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            PaymentNumber = paymentNumber,
            BusinessPartnerId = fixture.Partner.Id,
            BusinessPartnerRoleId = fixture.Role.Id,
            BusinessPartnerApProfileVersionId = fixture.Profile.Id,
            BusinessPartnerCode = fixture.Partner.PartnerCode,
            BusinessPartnerName = fixture.Partner.PartnerName,
            PaymentDate = paymentDate, TotalAmount = taxableBase,
            AllocatedAmount = taxableBase, CurrencyCode = "GHS", ExchangeRate = 1m,
            WithholdingTaxId = fixture.Tax.Id,
            WithholdingTaxRate = 7.5m,
            WithholdingTaxBaseAmount = taxableBase,
            Status = VendorPaymentStatus.Draft
        };
        db.VendorInvoices.Add(invoice);
        db.Set<VendorPayment>().Add(payment);
        db.Set<VendorPaymentAllocation>().Add(new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            VendorPaymentId = payment.Id, VendorPayment = payment,
            VendorInvoiceId = invoice.Id, VendorInvoice = invoice,
            AllocatedAmount = taxableBase, PaymentCurrencyAmount = taxableBase,
            SettlementFunctionalAmount = taxableBase, AllocationDate = paymentDate
        });
        return payment;
    }

    private sealed record WhtFixture(
        Guid TenantId,
        BusinessPartner Partner,
        BusinessPartnerRole Role,
        BusinessPartnerApProfileVersion Profile,
        Tax Tax,
        AccountingBook Book);
}
