using System.Text;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.Taxation;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
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
    public async Task Calculation_ShouldApplyTheConfiguredRateToTheWholePaymentThatCrossesTheAnnualThreshold()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedFoundation(db, tenantId);

        // Below-threshold payments carry the tax/base snapshot even though no amount was withheld.
        // That is what lets the server enforce the annual supplier aggregate on later payments.
        db.Set<VendorPayment>().Add(new VendorPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "VP-WHT-PRIOR",
            BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 3, 5),
            TotalAmount = 1_500m,
            AllocatedAmount = 1_500m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            WithholdingTaxId = fixture.Tax.Id,
            WithholdingTaxBaseAmount = 1_500m,
            Status = VendorPaymentStatus.Processed
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var below = await service.CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id,
            BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 5, 1),
            TaxableBase = 400m
        });
        var crossing = await service.CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id,
            BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2026, 5, 1),
            TaxableBase = 600m
        });
        var newYear = await service.CalculateApWithholdingAsync(new WhtCalculationRequestDto
        {
            TaxId = fixture.Tax.Id,
            BusinessPartnerId = fixture.Partner.Id,
            PaymentDate = new DateTime(2027, 1, 10),
            TaxableBase = 1_000m
        });

        below.ThresholdApplied.Should().BeFalse();
        below.WithholdingAmount.Should().Be(0m);
        crossing.CumulativeBefore.Should().Be(1_500m);
        crossing.CumulativeAfter.Should().Be(2_100m);
        crossing.ThresholdApplied.Should().BeTrue();
        crossing.WithholdingAmount.Should().Be(45m, "7.5% applies to the full GHS 600 crossing payment");
        newYear.CumulativeBefore.Should().Be(0m, "payment-date calendar years are separate statutory aggregates");
        newYear.ThresholdApplied.Should().BeFalse();
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
        return new WhtFixture(tenantId, partner, role, profile, tax, book);
    }

    private static VendorPayment SeedPostedWhtPayment(
        ApplicationDbContext db,
        WhtFixture fixture,
        string paymentNumber,
        DateTime paymentDate)
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
            TotalAmount = 925m,
            AllocatedAmount = 925m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            WithholdingTaxId = fixture.Tax.Id,
            WithholdingTaxRate = 7.5m,
            WithholdingTaxBaseAmount = 1_000m,
            WithholdingTaxAmount = 75m,
            WithholdingTaxThresholdAmount = 2_000m,
            WithholdingTaxThresholdApplied = true,
            Status = VendorPaymentStatus.Processed,
            JournalEntryId = journal.Id
        };
        journal.SourceDocumentId = payment.Id;
        db.JournalEntries.Add(journal);
        db.Set<VendorPayment>().Add(payment);
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
