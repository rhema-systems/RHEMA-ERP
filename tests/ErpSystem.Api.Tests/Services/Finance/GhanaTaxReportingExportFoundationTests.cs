using ErpSystem.Api.Services.Finance.Reporting;
using ErpSystem.Api.Services.Finance.Taxation;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class GhanaTaxReportingExportFoundationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTaxReportingExport")]
    [Trait("Category", "Tax")]
    public async Task OutputInputAndNetReports_ShouldUsePostedTaxSnapshots_NotCurrentRates()
    {
        await using var db = CreateContext();
        var fixture = SeedPostedTaxFixture(db, Guid.NewGuid());
        await db.SaveChangesAsync();

        fixture.Vat.Rate = 99m;
        await db.SaveChangesAsync();

        var service = CreateTaxReportingService(db, fixture.TenantId);
        var request = new TaxReportRequestDto
        {
            FromDate = new DateTime(2026, 7, 1),
            ToDate = new DateTime(2026, 7, 31)
        };

        var output = await service.GetOutputTaxReportAsync(request);
        var input = await service.GetInputTaxReportAsync(request);
        var net = await service.GetNetVatSummaryAsync(request);

        output.Totals.TotalTaxAmount.Should().Be(20m);
        input.Totals.TotalTaxAmount.Should().Be(20m);
        net.Lines.Should().HaveCount(6);
        output.Lines.Single(l => l.TaxCode == "VAT").TaxRate.Should().Be(15m);
        output.Totals.VatAmount.Should().Be(15m);
        output.Totals.NhilAmount.Should().Be(2.5m);
        output.Totals.GetFundAmount.Should().Be(2.5m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTaxReportingExport")]
    [Trait("Category", "Tax")]
    public async Task NonTaxableSuppliesReport_ShouldExposeExplicitLineTreatments()
    {
        await using var db = CreateContext();
        var fixture = SeedPostedTaxFixture(db, Guid.NewGuid());
        var arInvoice = fixture.ArInvoice;
        arInvoice.LineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            InvoiceId = arInvoice.Id,
            LineItemType = LineItemType.GLAccount,
            Description = "Zero-rated supply",
            Quantity = 1m,
            UnitPrice = 50m,
            TaxTreatment = TaxTreatment.ZeroRated,
            TaxAmount = 0m,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var report = await CreateTaxReportingService(db, fixture.TenantId)
            .GetExemptZeroRatedOutOfScopeReportAsync(new TaxReportRequestDto
            {
                FromDate = new DateTime(2026, 7, 1),
                ToDate = new DateTime(2026, 7, 31)
            });

        report.Lines.Should().ContainSingle(l =>
            l.SourceDocumentNumber == fixture.ArInvoice.InvoiceNumber &&
            l.TaxTreatment == TaxTreatment.ZeroRated &&
            l.TaxAmount == 0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTaxReportingExport")]
    [Trait("Category", "Tax")]
    public async Task WithholdingReports_ShouldUsePostedPaymentDataAndCertificateReferences()
    {
        await using var db = CreateContext();
        var fixture = SeedPostedTaxFixture(db, Guid.NewGuid());
        SeedPostedWithholdingPayments(db, fixture);
        await db.SaveChangesAsync();

        var service = CreateTaxReportingService(db, fixture.TenantId);
        var request = new TaxReportRequestDto
        {
            FromDate = new DateTime(2026, 7, 1),
            ToDate = new DateTime(2026, 7, 31)
        };

        var vatWithholding = await service.GetVatWithholdingReportAsync(request);
        var whtPayable = await service.GetWhtPayableReportAsync(request);
        var whtReceivable = await service.GetWhtReceivableReportAsync(request);

        vatWithholding.Lines.Should().ContainSingle(l => l.WithholdingType == "VAT Withholding" && l.WithholdingAmount == 7m);
        // AP certificates are generated/issued by the payer; AR certificates are received from customers.
        whtPayable.Lines.Should().ContainSingle(l => l.WithholdingType == "AP WHT Payable" && l.CertificateStatus == "Generated");
        whtReceivable.Lines.Should().ContainSingle(l => l.WithholdingType == "AR WHT Receivable" && l.CertificateNumber == "CERT-AR-001");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTaxReportingExport")]
    [Trait("Category", "Tax")]
    public async Task TaxAccountReconciliation_ShouldTieSnapshotsAndWithholdingToPostedGl()
    {
        await using var db = CreateContext();
        var fixture = SeedPostedTaxFixture(db, Guid.NewGuid());
        SeedPostedWithholdingPayments(db, fixture);
        await db.SaveChangesAsync();

        var report = await CreateTaxReportingService(db, fixture.TenantId)
            .GetTaxAccountReconciliationReportAsync(new TaxReportRequestDto
            {
                FromDate = new DateTime(2026, 7, 1),
                ToDate = new DateTime(2026, 7, 31)
            });

        report.Lines.Should().Contain(r => r.Area == "Output Tax" && r.Variance == 0m);
        report.Lines.Should().Contain(r => r.Area == "Input Tax" && r.Variance == 0m);
        report.Lines.Should().Contain(r => r.Area == "VAT Withholding" && r.Variance == 0m);
        report.Lines.Should().Contain(r => r.Area == "WHT Payable" && r.Variance == 0m);
        report.Totals.Variance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTaxReportingExport")]
    [Trait("Category", "Tax")]
    public async Task TaxReports_ShouldRejectCrossTenantTaxAccountFilter()
    {
        await using var db = CreateContext();
        var fixture = SeedPostedTaxFixture(db, Guid.NewGuid());
        var otherTenantId = Guid.NewGuid();
        SeedTenant(db, otherTenantId, "OTH");
        var otherAccount = SeedAccount(db, otherTenantId, "2299", "Other Output VAT", AccountType.Liability);
        await db.SaveChangesAsync();

        var act = () => CreateTaxReportingService(db, fixture.TenantId)
            .GetOutputTaxReportAsync(new TaxReportRequestDto
            {
                FromDate = new DateTime(2026, 7, 1),
                ToDate = new DateTime(2026, 7, 31),
                TaxAccountId = otherAccount.Id
            });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax account filter was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTaxReportingExport")]
    [Trait("Category", "Tax")]
    public async Task TaxOutputExport_ShouldUseTaxReportServiceAndCreateAuditEvent()
    {
        await using var db = CreateContext();
        var fixture = SeedPostedTaxFixture(db, Guid.NewGuid());
        await db.SaveChangesAsync();
        var audit = new CapturingFinanceAuditService();
        var taxService = CreateTaxReportingService(db, fixture.TenantId, audit);
        var exportService = CreateExportService(fixture.TenantId, audit, taxService);

        var result = await exportService.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = FinanceReportExportTypes.TaxOutput,
            Format = FinanceReportExportFormats.Csv,
            TaxReportQuery = new TaxReportRequestDto
            {
                FromDate = new DateTime(2026, 7, 1),
                ToDate = new DateTime(2026, 7, 31)
            }
        });

        var csv = System.Text.Encoding.UTF8.GetString(result.Content);
        csv.Should().Contain("Posted tax snapshots reconciled to posted GL");
        csv.Should().Contain("INV-TAX-001");
        result.Totals["TotalTaxAmount"].Should().Be(20m);
        audit.EventTypes.Should().Contain(FinanceAuditEvents.OutputTaxReportGenerated);
        audit.EventTypes.Should().Contain(FinanceAuditEvents.OutputTaxReportExported);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTaxReportingExport")]
    [Trait("Category", "Tax")]
    public async Task CovidLevyDiagnostic_ShouldDetectCurrentActiveCovidConfiguration()
    {
        await using var db = CreateContext();
        var fixture = SeedPostedTaxFixture(db, Guid.NewGuid());
        db.Taxes.Add(new Tax
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            Code = "COVID",
            Name = "COVID-19 Health Recovery Levy",
            Rate = 1m,
            Category = TaxCategory.Levy,
            Applicability = TaxApplicability.Both,
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var report = await CreateTaxReportingService(db, fixture.TenantId)
            .GetCurrentActiveCovidLevyDiagnosticReportAsync(new TaxReportRequestDto { ToDate = new DateTime(2026, 7, 31) });

        report.Diagnostics.Should().ContainSingle(d =>
            d.Code == "CURRENT_ACTIVE_COVID_LEVY" &&
            d.Severity == "Critical");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ghana-tax-reporting-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static TaxReportingService CreateTaxReportingService(
        ApplicationDbContext db,
        Guid tenantId,
        IFinanceAuditService? auditService = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.TenantId).Returns(tenantId);
        currentUser.SetupGet(u => u.UserId).Returns("finance-user");
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);

        return new TaxReportingService(
            db,
            currentUser.Object,
            NullLogger<TaxReportingService>.Instance,
            auditService);
    }

    private static FinanceReportExportService CreateExportService(
        Guid tenantId,
        IFinanceAuditService auditService,
        ITaxReportingService taxReportingService)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.TenantId).Returns(tenantId);
        currentUser.SetupGet(u => u.UserId).Returns("finance-user");
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);

        return new FinanceReportExportService(
            Mock.Of<IGeneralLedgerService>(),
            Mock.Of<IApReportsService>(),
            Mock.Of<IArReportsService>(),
            Mock.Of<IFixedAssetReportsService>(),
            currentUser.Object,
            NullLogger<FinanceReportExportService>.Instance,
            auditService,
            taxReportingService);
    }

    private static PostedTaxFixture SeedPostedTaxFixture(ApplicationDbContext db, Guid tenantId)
    {
        SeedTenant(db, tenantId);
        var fiscalPeriod = SeedFiscalPeriod(db, tenantId);

        var outputAccount = SeedAccount(db, tenantId, "2201", "Output VAT/NHIL/GETFund", AccountType.Liability);
        var inputAccount = SeedAccount(db, tenantId, "1401", "Recoverable Input VAT/NHIL/GETFund", AccountType.Asset);
        var apControl = SeedAccount(db, tenantId, "2100", "AP Control", AccountType.Liability);
        var arControl = SeedAccount(db, tenantId, "1100", "AR Control", AccountType.Asset);
        var revenue = SeedAccount(db, tenantId, "4100", "Revenue", AccountType.Revenue);
        var expense = SeedAccount(db, tenantId, "5100", "Expense", AccountType.Expense);
        var bank = SeedAccount(db, tenantId, "1000", "Bank", AccountType.Asset);
        var whtPayable = SeedAccount(db, tenantId, "2250", "WHT Payable", AccountType.Liability);
        var whtReceivable = SeedAccount(db, tenantId, "1450", "WHT Receivable", AccountType.Asset);
        var vatWhtReceivable = SeedAccount(db, tenantId, "1475", "VAT WHT Receivable", AccountType.Asset);

        var vat = SeedTax(db, tenantId, "VAT", "Value Added Tax", 15m, TaxCategory.Standard, inputAccount.Id, outputAccount.Id);
        var nhil = SeedTax(db, tenantId, "NHIL", "National Health Insurance Levy", 2.5m, TaxCategory.Standard, inputAccount.Id, outputAccount.Id);
        var getFund = SeedTax(db, tenantId, "GETFUND", "GETFund Levy", 2.5m, TaxCategory.Standard, inputAccount.Id, outputAccount.Id);
        var apWht = SeedTax(db, tenantId, "AP-WHT", "AP Withholding Tax", 5m, TaxCategory.Withholding, whtReceivable.Id, whtPayable.Id);
        var arWht = SeedTax(db, tenantId, "AR-WHT", "AR Withholding Tax", 5m, TaxCategory.Withholding, whtReceivable.Id, whtPayable.Id);
        var vatWht = SeedTax(db, tenantId, "VAT-WHT", "VAT Withholding", 7m, TaxCategory.VatWithholding, vatWhtReceivable.Id, whtPayable.Id);

        var salesGroup = SeedTaxGroup(db, tenantId, "GH-VAT-SALES", TaxApplicability.Sales);
        var purchaseGroup = SeedTaxGroup(db, tenantId, "GH-VAT-PURCHASE", TaxApplicability.Purchases);

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierCode = "SUP-TAX",
            Name = "Tax Supplier",
            CreatedBy = "test"
        };
        db.Set<Supplier>().Add(supplier);

        var customer = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = "CUS-TAX",
            PartnerName = "Tax Customer",
            PartnerType = "Customer",
            CreatedBy = "test"
        };
        db.Set<BusinessPartner>().Add(customer);

        var apInvoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "VI-TAX-001",
            BusinessPartnerId = supplier.Id,
            SupplierName = supplier.Name,
            InvoiceDate = new DateTime(2026, 7, 6),
            SubTotal = 100m,
            TaxAmount = 20m,
            TotalAmount = 120m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = 120m,
            Status = VendorInvoiceStatus.Approved,
            ExpenseAccountId = expense.Id,
            ApAccountId = apControl.Id,
            CreatedBy = "test"
        };
        apInvoice.LineItems.Add(new VendorInvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorInvoiceId = apInvoice.Id,
            GLAccountId = expense.Id,
            Description = "Taxed AP line",
            Quantity = 1m,
            UnitPrice = 100m,
            TaxGroupId = purchaseGroup.Id,
            TaxTreatment = TaxTreatment.Standard,
            TaxAmount = 20m,
            CreatedBy = "test"
        });
        apInvoice.JournalEntryId = SeedPostedJournalAndEvent(
            db,
            tenantId,
            "AP",
            "VendorInvoice",
            apInvoice.Id,
            apInvoice.InvoiceNumber,
            apInvoice.InvoiceDate,
            fiscalPeriod.Id,
            new[]
            {
                PostingLine(inputAccount.Id, 15m, 0m, "AP-Tax-VAT"),
                PostingLine(inputAccount.Id, 2.5m, 0m, "AP-Tax-NHIL"),
                PostingLine(inputAccount.Id, 2.5m, 0m, "AP-Tax-GETFUND"),
                PostingLine(expense.Id, 100m, 0m, "AP-Expense"),
                PostingLine(apControl.Id, 0m, 120m, "AP-Control")
            });
        db.VendorInvoices.Add(apInvoice);
        AddTaxCalculations(db, tenantId, apInvoice.Id, "VendorInvoice", purchaseGroup.Id, vat, nhil, getFund);

        var arInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "INV-TAX-001",
            BusinessPartnerId = customer.Id,
            CustomerName = customer.PartnerName,
            InvoiceDate = new DateTime(2026, 7, 6),
            SubTotal = 100m,
            TaxAmount = 20m,
            TotalAmount = 120m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = 120m,
            Status = InvoiceStatus.Sent,
            CreatedBy = "test"
        };
        arInvoice.LineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = arInvoice.Id,
            LineItemType = LineItemType.GLAccount,
            GLAccountId = revenue.Id,
            Description = "Taxed AR line",
            Quantity = 1m,
            UnitPrice = 100m,
            TaxGroupId = salesGroup.Id,
            TaxTreatment = TaxTreatment.Standard,
            TaxAmount = 20m,
            CreatedBy = "test"
        });
        arInvoice.JournalEntryId = SeedPostedJournalAndEvent(
            db,
            tenantId,
            "AR",
            "CustomerInvoice",
            arInvoice.Id,
            arInvoice.InvoiceNumber,
            arInvoice.InvoiceDate,
            fiscalPeriod.Id,
            new[]
            {
                PostingLine(arControl.Id, 120m, 0m, "AR-Control"),
                PostingLine(outputAccount.Id, 0m, 15m, "AR-Tax-VAT"),
                PostingLine(outputAccount.Id, 0m, 2.5m, "AR-Tax-NHIL"),
                PostingLine(outputAccount.Id, 0m, 2.5m, "AR-Tax-GETFUND"),
                PostingLine(revenue.Id, 0m, 100m, "AR-Revenue")
            });
        db.Invoices.Add(arInvoice);
        AddTaxCalculations(db, tenantId, arInvoice.Id, "CustomerInvoice", salesGroup.Id, vat, nhil, getFund);

        return new PostedTaxFixture(
            tenantId,
            fiscalPeriod,
            apInvoice,
            arInvoice,
            supplier,
            customer,
            vat,
            nhil,
            getFund,
            apWht,
            arWht,
            vatWht,
            bank,
            apControl,
            arControl,
            whtPayable,
            whtReceivable,
            vatWhtReceivable);
    }

    private static void SeedPostedWithholdingPayments(ApplicationDbContext db, PostedTaxFixture fixture)
    {
        var apPayment = new VendorPayment
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            PaymentNumber = "VP-WHT-001",
            BusinessPartnerId = fixture.Supplier.Id,
            PaymentDate = new DateTime(2026, 7, 10),
            TotalAmount = 95m,
            AllocatedAmount = 95m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            WithholdingTaxId = fixture.ApWht.Id,
            WithholdingTaxAccountId = fixture.WhtPayable.Id,
            WithholdingTaxRate = 5m,
            WithholdingTaxAmount = 5m,
            WithholdingCertificateNumber = "CERT-AP-001",
            WithholdingCertificateDate = new DateTime(2026, 7, 11),
            Status = VendorPaymentStatus.Processed,
            CreatedBy = "test"
        };
        apPayment.JournalEntryId = SeedPostedJournalAndEvent(
            db,
            fixture.TenantId,
            "AP",
            "VendorPayment",
            apPayment.Id,
            apPayment.PaymentNumber,
            apPayment.PaymentDate,
            fixture.FiscalPeriod.Id,
            new[]
            {
                PostingLine(fixture.ApControl.Id, 100m, 0m, "AP-Control"),
                PostingLine(fixture.Bank.Id, 0m, 95m, "AP-Bank"),
                PostingLine(fixture.WhtPayable.Id, 0m, 5m, "AP-WHT")
            });
        db.Set<VendorPayment>().Add(apPayment);

        var arReceipt = new CustomerPayment
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            PaymentNumber = "CP-WHT-001",
            BusinessPartnerId = fixture.Customer.Id,
            PaymentDate = new DateTime(2026, 7, 10),
            TotalAmount = 88m,
            AllocatedAmount = 88m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            WithholdingTaxId = fixture.ArWht.Id,
            WithholdingTaxAccountId = fixture.WhtReceivable.Id,
            WithholdingTaxAmount = 5m,
            VatWithholdingTaxId = fixture.VatWht.Id,
            VatWithholdingAccountId = fixture.VatWhtReceivable.Id,
            VatWithholdingAmount = 7m,
            WithholdingCertificateNumber = "CERT-AR-001",
            WithholdingCertificateDate = new DateTime(2026, 7, 11),
            Status = "Posted",
            CreatedBy = "test"
        };
        arReceipt.JournalEntryId = SeedPostedJournalAndEvent(
            db,
            fixture.TenantId,
            "AR",
            "CustomerPayment",
            arReceipt.Id,
            arReceipt.PaymentNumber,
            arReceipt.PaymentDate,
            fixture.FiscalPeriod.Id,
            new[]
            {
                PostingLine(fixture.Bank.Id, 88m, 0m, "AR-Bank"),
                PostingLine(fixture.WhtReceivable.Id, 5m, 0m, "AR-WHT"),
                PostingLine(fixture.VatWhtReceivable.Id, 7m, 0m, "AR-VAT-WHT"),
                PostingLine(fixture.ArControl.Id, 0m, 100m, "AR-Control")
            });
        db.Set<CustomerPayment>().Add(arReceipt);
    }

    private static void AddTaxCalculations(
        ApplicationDbContext db,
        Guid tenantId,
        Guid documentId,
        string documentType,
        Guid taxGroupId,
        Tax vat,
        Tax nhil,
        Tax getFund)
    {
        db.Set<TaxCalculation>().AddRange(
            TaxCalculation(tenantId, documentId, documentType, taxGroupId, vat, 100m, 100m, 15m, 15m, 1),
            TaxCalculation(tenantId, documentId, documentType, taxGroupId, nhil, 100m, 100m, 2.5m, 2.5m, 2),
            TaxCalculation(tenantId, documentId, documentType, taxGroupId, getFund, 100m, 100m, 2.5m, 2.5m, 3));
    }

    private static TaxCalculation TaxCalculation(
        Guid tenantId,
        Guid documentId,
        string documentType,
        Guid taxGroupId,
        Tax tax,
        decimal baseAmount,
        decimal taxableAmount,
        decimal rate,
        decimal amount,
        int order)
    {
        return new TaxCalculation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            DocumentType = documentType,
            TaxId = tax.Id,
            TaxGroupId = taxGroupId,
            BaseAmount = baseAmount,
            TaxableAmount = taxableAmount,
            TaxRate = rate,
            TaxAmount = amount,
            CompoundBasis = CompoundBasis.BaseOnly,
            CalculationOrder = order,
            CalculationDate = new DateTime(2026, 7, 6),
            CreatedBy = "test"
        };
    }

    private static Guid SeedPostedJournalAndEvent(
        ApplicationDbContext db,
        Guid tenantId,
        string sourceModule,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string reference,
        DateTime date,
        Guid fiscalPeriodId,
        IEnumerable<PostingLineSeed> lines)
    {
        var lineList = lines.ToList();
        var journalId = Guid.NewGuid();
        var book = db.AccountingBooks.Local.Single(item =>
            item.TenantId == tenantId && item.Code == "IFRS" && !item.IsDeleted);
        var totalDebit = lineList.Sum(l => l.Debit);
        var totalCredit = lineList.Sum(l => l.Credit);

        db.JournalEntries.Add(new JournalEntry
        {
            Id = journalId,
            TenantId = tenantId,
            JournalEntryNumber = $"JE-{reference}",
            JournalType = sourceDocumentType,
            EntryDate = date,
            Description = $"Posted {reference}",
            ReferenceNumber = reference,
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            TotalDebitAmount = totalDebit,
            TotalCreditAmount = totalCredit,
            IsBalanced = totalDebit == totalCredit,
            FiscalPeriodId = fiscalPeriodId,
            AccountingBookId = book.Id,
            BookClassification = book.Code,
            PostingStatus = "Posted",
            ApprovalStatus = "Approved",
            PostingDate = date,
            CreatedBy = "test"
        });

        var lineNo = 1;
        foreach (var line in lineList)
        {
            db.AccountTransactions.Add(new AccountTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountId = line.AccountId,
                JournalEntryId = journalId,
                TransactionDate = date,
                Description = line.Tag,
                DebitAmount = line.Debit,
                CreditAmount = line.Credit,
                FunctionalCurrencyCode = "GHS",
                TransactionCurrency = "GHS",
                TransactionDebitAmount = line.Debit,
                TransactionCreditAmount = line.Credit,
                SourceModule = sourceModule,
                SourceDocumentType = sourceDocumentType,
                SourceDocumentId = sourceDocumentId,
                SourceReferenceNumber = reference,
                FiscalPeriodId = fiscalPeriodId,
                AccountingBookId = book.Id,
                BookClassification = book.Code,
                PostingStatus = "Posted",
                PostedDate = date,
                LineNumber = lineNo++,
                TransactionTag = line.Tag,
                CreatedBy = "test"
            });
        }

        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            PostingAction = "Post",
            SourceDocumentReference = reference,
            JournalEntryId = journalId,
            PostingStatus = "Posted",
            PostingDate = date,
            PostedAt = date,
            TotalDebitAmount = totalDebit,
            TotalCreditAmount = totalCredit,
            FunctionalCurrencyCode = "GHS",
            AccountingBookId = book.Id,
            BookClassification = book.Code,
            CreatedBy = "test"
        });

        return journalId;
    }

    private static PostingLineSeed PostingLine(Guid accountId, decimal debit, decimal credit, string tag)
        => new(accountId, debit, credit, tag);

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN")
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Code = code,
            Name = $"{code} Tenant",
            CreatedBy = "test"
        });
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "IFRS",
            Name = "IFRS Primary",
            BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active,
            FunctionalCurrencyCode = "GHS",
            IsDefault = true,
            IsActive = true,
            AllowsPosting = true,
            CreatedBy = "test"
        });
    }

    private static FiscalPeriod SeedFiscalPeriod(ApplicationDbContext db, Guid tenantId)
    {
        var fiscalYear = new FiscalYear
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearName = "FY2026",
            FiscalYearCode = "FY2026",
            Year = 2026,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            TotalDays = 365,
            IsActive = true,
            Status = "Open",
            CreatedBy = "test"
        };
        db.FiscalYears.Add(fiscalYear);

        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true,
            CreatedBy = "test"
        };
        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string number,
        string name,
        AccountType accountType)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = number,
            AccountNumber = number,
            AccountName = name,
            AccountType = accountType,
            AccountCategory = accountType.ToString(),
            AllowDirectPosting = true,
            Status = AccountStatus.Active,
            CreatedBy = "test"
        };
        db.Accounts.Add(account);
        return account;
    }

    private static Tax SeedTax(
        ApplicationDbContext db,
        Guid tenantId,
        string code,
        string name,
        decimal rate,
        TaxCategory category,
        Guid receivableAccountId,
        Guid payableAccountId)
    {
        var tax = new Tax
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Rate = rate,
            Category = category,
            Applicability = TaxApplicability.Both,
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true,
            IsInputTaxDeductible = category is TaxCategory.Standard or TaxCategory.Levy,
            TaxReceivableAccountId = receivableAccountId,
            TaxPayableAccountId = payableAccountId,
            CreatedBy = "test"
        };
        tax.RateHistory.Add(new TaxRateHistory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaxId = tax.Id,
            Rate = rate,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CreatedBy = "test"
        });
        db.Taxes.Add(tax);
        return tax;
    }

    private static TaxGroup SeedTaxGroup(ApplicationDbContext db, Guid tenantId, string code, TaxApplicability applicability)
    {
        var group = new TaxGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = code,
            Applicability = applicability,
            IsActive = true,
            CreatedBy = "test"
        };
        db.TaxGroups.Add(group);
        return group;
    }

    private sealed class CapturingFinanceAuditService : IFinanceAuditService
    {
        public List<FinanceAuditEventDto> Events { get; } = new();
        public List<string> EventTypes => Events.Select(e => e.EventType).ToList();

        public Task<AuditLog> RecordAsync(FinanceAuditEventDto auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.FromResult(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = auditEvent.TenantId,
                Action = auditEvent.EventType,
                Resource = auditEvent.Resource ?? "Finance.TaxReport",
                ResourceId = auditEvent.ResourceId
            });
        }

        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
            Guid tenantId,
            string resource,
            string resourceId,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AuditLog> auditTrail = Array.Empty<AuditLog>();
            return Task.FromResult(auditTrail);
        }
    }

    private sealed record PostingLineSeed(Guid AccountId, decimal Debit, decimal Credit, string Tag);

    private sealed record PostedTaxFixture(
        Guid TenantId,
        FiscalPeriod FiscalPeriod,
        VendorInvoice ApInvoice,
        Invoice ArInvoice,
        Supplier Supplier,
        BusinessPartner Customer,
        Tax Vat,
        Tax Nhil,
        Tax GetFund,
        Tax ApWht,
        Tax ArWht,
        Tax VatWht,
        Account Bank,
        Account ApControl,
        Account ArControl,
        Account WhtPayable,
        Account WhtReceivable,
        Account VatWhtReceivable);
}
