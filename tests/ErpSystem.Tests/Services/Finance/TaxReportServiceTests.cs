using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using MockQueryable.Moq;
using Xunit;
using FluentAssertions;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;

namespace ErpSystem.Tests.Services.Finance
{
    public class TaxReportServiceTests
    {
        private readonly Mock<IFinancialRepository> _mockRepo;
        private readonly Mock<ICurrentUserService> _mockUserService;
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly TaxReportService _sut;

        public TaxReportServiceTests()
        {
            _mockRepo = new Mock<IFinancialRepository>();
            _mockUserService = new Mock<ICurrentUserService>();
            _mockUserService.Setup(x => x.TenantId).Returns(_tenantId);

            _sut = new TaxReportService(_mockRepo.Object, _mockUserService.Object);
        }

        [Fact]
        public async Task OutputVatReport_ShouldIncludePostedSalesInvoices()
        {
            // Arrange
            var invoices = new List<Invoice>
            {
                new Invoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = InvoiceStatus.Paid, InvoiceNumber = "INV-01", InvoiceDate = DateTime.UtcNow, TaxAmount = 10, SubTotal = 100, ExchangeRate = 1m },
                new Invoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = InvoiceStatus.Draft, InvoiceNumber = "INV-02", InvoiceDate = DateTime.UtcNow, TaxAmount = 20, SubTotal = 200, ExchangeRate = 1m }
            };

            _mockRepo.Setup(x => x.Query<Invoice>()).Returns(invoices.BuildMock());

            // Act
            var result = await _sut.GetOutputVatAsync(new TaxReportFilterDto());

            // Assert
            result.Should().HaveCount(1);
            result.First().InvoiceNumber.Should().Be("INV-01");
            result.First().VatAmount.Should().Be(10);
        }

        [Fact]
        public async Task InputVatReport_ShouldIncludePostedVendorInvoices()
        {
            // Arrange
            var vendorInvoices = new List<VendorInvoice>
            {
                new VendorInvoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = VendorInvoiceStatus.Approved, InvoiceNumber = "VINV-01", InvoiceDate = DateTime.UtcNow, TaxAmount = 15, SubTotal = 150, ExchangeRate = 1m },
                new VendorInvoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = VendorInvoiceStatus.Draft, InvoiceNumber = "VINV-02", InvoiceDate = DateTime.UtcNow, TaxAmount = 25, SubTotal = 250, ExchangeRate = 1m }
            };

            _mockRepo.Setup(x => x.Query<VendorInvoice>()).Returns(vendorInvoices.BuildMock());

            // Act
            var result = await _sut.GetInputVatAsync(new TaxReportFilterDto());

            // Assert
            result.Should().HaveCount(1);
            result.First().InvoiceNumber.Should().Be("VINV-01");
            result.First().VatAmount.Should().Be(15);
        }

        [Fact]
        public async Task WhtSummary_ShouldAggregateWithholdingTaxFromVendorPaymentAllocations()
        {
            // Arrange
            var paymentId = Guid.NewGuid();
            var payment = new VendorPayment 
            { 
                Id = paymentId, 
                TenantId = _tenantId, 
                PaymentNumber = "PAY-01", 
                PaymentDate = DateTime.UtcNow, 
                TotalAmount = 500, 
                WithholdingTaxRate = 5, 
                WithholdingTaxAmount = 25, 
                ExchangeRate = 1m,
                BusinessPartner = new BusinessPartner { PartnerName = "Vendor A" }
            };

            var invoice = new VendorInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                InvoiceNumber = "VINV-100",
                WithholdingTaxRate = 5
            };

            var allocations = new List<VendorPaymentAllocation>
            {
                new VendorPaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    VendorPaymentId = paymentId,
                    VendorPayment = payment,
                    VendorInvoiceId = invoice.Id,
                    VendorInvoice = invoice,
                    AllocatedAmount = 500,
                    WithholdingTaxAmount = 25,
                    IsReversal = false
                }
            };

            _mockRepo.Setup(x => x.Query<VendorPaymentAllocation>()).Returns(allocations.BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPayment>()).Returns(new List<VendorPayment> { payment }.BuildMock());

            // Act
            var result = await _sut.GetWhtSummaryAsync(new TaxReportFilterDto());

            // Assert
            result.Should().HaveCount(1);
            result.First().PaymentNumber.Should().Be("PAY-01");
            result.First().VendorInvoiceNumber.Should().Be("VINV-100");
            result.First().WhtAmount.Should().Be(25);
        }

        [Fact]
        public async Task WhtSummary_ShouldExcludeUnpaidVendorInvoices()
        {
            // Arrange
            // VendorInvoice has WHT but NO VendorPaymentAllocation / VendorPayment exists
            var invoice = new VendorInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                InvoiceNumber = "VINV-999",
                WithholdingTaxRate = 10,
                WithholdingTaxAmount = 100, // Accrued WHT, should be ignored by Cash basis report
                Status = VendorInvoiceStatus.Approved
            };

            _mockRepo.Setup(x => x.Query<VendorPaymentAllocation>()).Returns(new List<VendorPaymentAllocation>().BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPayment>()).Returns(new List<VendorPayment>().BuildMock());

            // Act
            var result = await _sut.GetWhtSummaryAsync(new TaxReportFilterDto());

            // Assert
            result.Should().BeEmpty("Because WHT summary is Cash Basis, an invoice without payment should not be included.");
        }
        [Fact]
        public async Task TaxReports_ShouldRespectDateRangeFilters()
        {
            var filter = new TaxReportFilterDto { StartDate = new DateTime(2023, 1, 1), EndDate = new DateTime(2023, 1, 31) };
            
            var invoices = new List<Invoice>
            {
                new Invoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = InvoiceStatus.Sent, InvoiceDate = new DateTime(2023, 1, 15), TaxAmount = 10, ExchangeRate = 1m },
                new Invoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = InvoiceStatus.Sent, InvoiceDate = new DateTime(2023, 2, 1), TaxAmount = 10, ExchangeRate = 1m }
            };
            
            var vendorInvoices = new List<VendorInvoice>
            {
                new VendorInvoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = VendorInvoiceStatus.Approved, InvoiceDate = new DateTime(2023, 1, 15), TaxAmount = 10, ExchangeRate = 1m },
                new VendorInvoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = VendorInvoiceStatus.Approved, InvoiceDate = new DateTime(2023, 2, 1), TaxAmount = 10, ExchangeRate = 1m }
            };

            var payments = new List<VendorPayment>
            {
                new VendorPayment { Id = Guid.NewGuid(), TenantId = _tenantId, PaymentDate = new DateTime(2023, 1, 15), WithholdingTaxAmount = 10, ExchangeRate = 1m },
                new VendorPayment { Id = Guid.NewGuid(), TenantId = _tenantId, PaymentDate = new DateTime(2023, 2, 1), WithholdingTaxAmount = 10, ExchangeRate = 1m }
            };

            _mockRepo.Setup(x => x.Query<Invoice>()).Returns(invoices.BuildMock());
            _mockRepo.Setup(x => x.Query<VendorInvoice>()).Returns(vendorInvoices.BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPaymentAllocation>()).Returns(new List<VendorPaymentAllocation>().BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPayment>()).Returns(payments.BuildMock());

            var outputVat = await _sut.GetOutputVatAsync(filter);
            var inputVat = await _sut.GetInputVatAsync(filter);
            var wht = await _sut.GetWhtSummaryAsync(filter);

            outputVat.Should().HaveCount(1);
            inputVat.Should().HaveCount(1);
            wht.Should().HaveCount(1);
        }

        [Fact]
        public async Task TaxReports_ShouldRespectTenantIsolation()
        {
            var otherTenantId = Guid.NewGuid();
            var invoices = new List<Invoice>
            {
                new Invoice { Id = Guid.NewGuid(), TenantId = otherTenantId, Status = InvoiceStatus.Sent, TaxAmount = 10, ExchangeRate = 1m }
            };
            var vendorInvoices = new List<VendorInvoice>
            {
                new VendorInvoice { Id = Guid.NewGuid(), TenantId = otherTenantId, Status = VendorInvoiceStatus.Approved, TaxAmount = 10, ExchangeRate = 1m }
            };
            var payments = new List<VendorPayment>
            {
                new VendorPayment { Id = Guid.NewGuid(), TenantId = otherTenantId, WithholdingTaxAmount = 10, ExchangeRate = 1m }
            };

            _mockRepo.Setup(x => x.Query<Invoice>()).Returns(invoices.BuildMock());
            _mockRepo.Setup(x => x.Query<VendorInvoice>()).Returns(vendorInvoices.BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPaymentAllocation>()).Returns(new List<VendorPaymentAllocation>().BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPayment>()).Returns(payments.BuildMock());

            var outputVat = await _sut.GetOutputVatAsync(new TaxReportFilterDto());
            var inputVat = await _sut.GetInputVatAsync(new TaxReportFilterDto());
            var wht = await _sut.GetWhtSummaryAsync(new TaxReportFilterDto());

            outputVat.Should().BeEmpty();
            inputVat.Should().BeEmpty();
            wht.Should().BeEmpty();
        }

        [Fact]
        public async Task TaxReports_ShouldReturnBaseCurrencyAmounts()
        {
            var invoice = new Invoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = InvoiceStatus.Sent, TaxAmount = 10, SubTotal = 100, ExchangeRate = 1.5m };
            var vendorInvoice = new VendorInvoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = VendorInvoiceStatus.Approved, TaxAmount = 10, SubTotal = 100, ExchangeRate = 2.0m };
            var payment = new VendorPayment { Id = Guid.NewGuid(), TenantId = _tenantId, WithholdingTaxAmount = 10, ExchangeRate = 2.5m };

            _mockRepo.Setup(x => x.Query<Invoice>()).Returns(new List<Invoice> { invoice }.BuildMock());
            _mockRepo.Setup(x => x.Query<VendorInvoice>()).Returns(new List<VendorInvoice> { vendorInvoice }.BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPaymentAllocation>()).Returns(new List<VendorPaymentAllocation>().BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPayment>()).Returns(new List<VendorPayment> { payment }.BuildMock());

            var outputVat = await _sut.GetOutputVatAsync(new TaxReportFilterDto());
            var inputVat = await _sut.GetInputVatAsync(new TaxReportFilterDto());
            var wht = await _sut.GetWhtSummaryAsync(new TaxReportFilterDto());

            outputVat.First().BaseVatAmount.Should().Be(15m); // 10 * 1.5
            outputVat.First().BaseTaxableAmount.Should().Be(150m); // 100 * 1.5

            inputVat.First().BaseVatAmount.Should().Be(20m); // 10 * 2.0
            inputVat.First().BaseTaxableAmount.Should().Be(200m); // 100 * 2.0

            wht.First().BaseWhtAmount.Should().Be(25m); // 10 * 2.5
        }

        [Fact]
        public async Task OutputVatReport_ShouldAggregateMultipleTaxCodesPerInvoice()
        {
            var invoice = new Invoice 
            { 
                Id = Guid.NewGuid(), 
                TenantId = _tenantId, 
                Status = InvoiceStatus.Sent, 
                ExchangeRate = 1m,
                LineItems = new List<InvoiceLineItem>
                {
                    new InvoiceLineItem { TaxCode = "VAT15", UnitPrice = 100, Quantity = 1, TaxRate = 15, TaxAmount = 15, DiscountAmount = 0 },
                    new InvoiceLineItem { TaxCode = "VAT15", UnitPrice = 200, Quantity = 1, TaxRate = 15, TaxAmount = 30, DiscountAmount = 0 },
                    new InvoiceLineItem { TaxCode = "NHIL", UnitPrice = 100, Quantity = 1, TaxRate = 5, TaxAmount = 5, DiscountAmount = 0 }
                }
            };

            _mockRepo.Setup(x => x.Query<Invoice>()).Returns(new List<Invoice> { invoice }.BuildMock());

            var result = await _sut.GetOutputVatAsync(new TaxReportFilterDto());

            result.Should().HaveCount(2);
            var vat15 = result.Single(x => x.TaxCode == "VAT15");
            vat15.VatAmount.Should().Be(45);
            vat15.TaxableAmount.Should().Be(300); 
            var nhil = result.Single(x => x.TaxCode == "NHIL");
            nhil.VatAmount.Should().Be(5);
        }

        [Fact]
        public async Task InputVatReport_ShouldAggregateMultipleTaxCodesPerVendorInvoice()
        {
            var vendorInvoice = new VendorInvoice 
            { 
                Id = Guid.NewGuid(), 
                TenantId = _tenantId, 
                Status = VendorInvoiceStatus.Approved, 
                ExchangeRate = 1m,
                LineItems = new List<VendorInvoiceLineItem>
                {
                    new VendorInvoiceLineItem { TaxCode = "VAT15", UnitPrice = 100, Quantity = 1, TaxRate = 15, TaxAmount = 15, DiscountAmount = 0 },
                    new VendorInvoiceLineItem { TaxCode = "VAT15", UnitPrice = 200, Quantity = 1, TaxRate = 15, TaxAmount = 30, DiscountAmount = 0 },
                    new VendorInvoiceLineItem { TaxCode = "NHIL", UnitPrice = 100, Quantity = 1, TaxRate = 5, TaxAmount = 5, DiscountAmount = 0 }
                }
            };

            _mockRepo.Setup(x => x.Query<VendorInvoice>()).Returns(new List<VendorInvoice> { vendorInvoice }.BuildMock());

            var result = await _sut.GetInputVatAsync(new TaxReportFilterDto());

            result.Should().HaveCount(2);
            var vat15 = result.Single(x => x.TaxCode == "VAT15");
            vat15.VatAmount.Should().Be(45);
            var nhil = result.Single(x => x.TaxCode == "NHIL");
            nhil.VatAmount.Should().Be(5);
        }

        [Fact]
        public async Task WhtSummary_ShouldFallbackToVendorPayment_WhenNoAllocationExists()
        {
            var payment = new VendorPayment 
            { 
                Id = Guid.NewGuid(), 
                TenantId = _tenantId, 
                PaymentNumber = "PAY-NO-ALLOC", 
                WithholdingTaxAmount = 50, 
                ExchangeRate = 1m 
            };

            _mockRepo.Setup(x => x.Query<VendorPaymentAllocation>()).Returns(new List<VendorPaymentAllocation>().BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPayment>()).Returns(new List<VendorPayment> { payment }.BuildMock());

            var result = await _sut.GetWhtSummaryAsync(new TaxReportFilterDto());

            result.Should().HaveCount(1);
            result.First().PaymentNumber.Should().Be("PAY-NO-ALLOC");
            result.First().WhtAmount.Should().Be(50);
            result.First().VendorInvoiceNumber.Should().BeNull();
        }

        [Fact]
        public async Task WhtSummary_ShouldExcludeZeroWithholdingTaxPayments()
        {
            var paymentAlloc = new VendorPaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                VendorPayment = new VendorPayment { PaymentDate = DateTime.UtcNow, ExchangeRate = 1m },
                WithholdingTaxAmount = 0 // Zero WHT
            };

            var paymentUnalloc = new VendorPayment 
            { 
                Id = Guid.NewGuid(), 
                TenantId = _tenantId, 
                WithholdingTaxAmount = 0, // Zero WHT
                ExchangeRate = 1m 
            };

            _mockRepo.Setup(x => x.Query<VendorPaymentAllocation>()).Returns(new List<VendorPaymentAllocation> { paymentAlloc }.BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPayment>()).Returns(new List<VendorPayment> { paymentUnalloc }.BuildMock());

            var result = await _sut.GetWhtSummaryAsync(new TaxReportFilterDto());

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task TaxReports_ShouldExcludeCancelledOrReversedInvoices()
        {
            var invoices = new List<Invoice>
            {
                new Invoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = InvoiceStatus.Cancelled, TaxAmount = 10, ExchangeRate = 1m }
            };
            
            var vendorInvoices = new List<VendorInvoice>
            {
                new VendorInvoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = VendorInvoiceStatus.Voided, TaxAmount = 10, ExchangeRate = 1m },
                new VendorInvoice { Id = Guid.NewGuid(), TenantId = _tenantId, Status = VendorInvoiceStatus.Rejected, TaxAmount = 10, ExchangeRate = 1m }
            };

            var allocations = new List<VendorPaymentAllocation>
            {
                new VendorPaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    VendorPayment = new VendorPayment { ExchangeRate = 1m },
                    WithholdingTaxAmount = 10,
                    IsReversal = true // Reversed allocation
                }
            };

            _mockRepo.Setup(x => x.Query<Invoice>()).Returns(invoices.BuildMock());
            _mockRepo.Setup(x => x.Query<VendorInvoice>()).Returns(vendorInvoices.BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPaymentAllocation>()).Returns(allocations.BuildMock());
            _mockRepo.Setup(x => x.Query<VendorPayment>()).Returns(new List<VendorPayment>().BuildMock());

            var outputVat = await _sut.GetOutputVatAsync(new TaxReportFilterDto());
            var inputVat = await _sut.GetInputVatAsync(new TaxReportFilterDto());
            var wht = await _sut.GetWhtSummaryAsync(new TaxReportFilterDto());

            outputVat.Should().BeEmpty();
            inputVat.Should().BeEmpty();
            wht.Should().BeEmpty();
        }
    }
}
