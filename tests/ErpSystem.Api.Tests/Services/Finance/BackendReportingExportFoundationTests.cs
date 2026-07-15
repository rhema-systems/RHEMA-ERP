using ErpSystem.Api.Services.Finance.Reporting;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class BackendReportingExportFoundationTests
{
    private static readonly DateTime AsOfDate = new(2026, 7, 31);
    private static readonly DateTime PeriodStart = new(2026, 7, 1);

    [Fact]
    [Trait("Batch", "FinanceGoLive-BackendReportingExport")]
    [Trait("Category", "ReportingExport")]
    public async Task TrialBalanceExport_UsesPostedGlAndMatchesViewTotals()
    {
        var fixture = CreateFixture();
        fixture.GeneralLedger
            .Setup(service => service.GenerateTrialBalanceAsync(It.IsAny<TrialBalanceRequestDto>()))
            .ReturnsAsync(new TrialBalanceDto
            {
                AsAtDate = AsOfDate,
                Lines =
                {
                    new TrialBalanceLineDto
                    {
                        AccountId = Guid.NewGuid(),
                        AccountNumber = "1000",
                        AccountName = "Cash",
                        AccountType = "Asset",
                        PeriodDebits = 100m,
                        DebitBalance = 100m
                    },
                    new TrialBalanceLineDto
                    {
                        AccountId = Guid.NewGuid(),
                        AccountNumber = "3000",
                        AccountName = "Equity",
                        AccountType = "Equity",
                        PeriodCredits = 100m,
                        CreditBalance = 100m
                    }
                },
                TotalDebits = 100m,
                TotalCredits = 100m
            });

        var result = await fixture.Service.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = FinanceReportExportTypes.TrialBalance,
            AsOfDate = AsOfDate,
            PeriodStart = PeriodStart
        });

        result.SourceOfTruthMode.Should().Be("Posted GL");
        result.Totals["TotalDebits"].Should().Be(100m);
        result.Totals["TotalCredits"].Should().Be(100m);
        Csv(result).Should().Contain("Cash").And.Contain("TOTAL").And.Contain("100.00");
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.ReportExported);
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.TrialBalanceExported);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BackendReportingExport")]
    [Trait("Category", "ReportingExport")]
    public async Task IncomeStatementExport_FlagsDisposalGainPresentationWarning()
    {
        var fixture = CreateFixture();
        fixture.GeneralLedger
            .Setup(service => service.GenerateIncomeStatementAsync(It.IsAny<IncomeStatementRequestDto>()))
            .ReturnsAsync(new IncomeStatementDto
            {
                PeriodStart = PeriodStart,
                PeriodEnd = AsOfDate,
                Sections =
                {
                    new IncomeStatementSectionDto
                    {
                        SectionName = "Other Income",
                        SectionOrder = 40,
                        SectionTotal = 25m,
                        LineItems =
                        {
                            new IncomeStatementLineItemDto
                            {
                                LineItemName = "Disposal gains",
                                Amount = 25m,
                                LineOrder = 1,
                                AccountNumbers = new List<string> { "5999" }
                            }
                        }
                    }
                },
                TotalOtherIncome = 25m,
                ProfitBeforeTax = 25m,
                NetProfit = 25m,
                PresentationWarnings =
                {
                    "Disposal gain presentation account 5999 is mapped as non-operating disposal gain for reporting/export."
                }
            });

        var result = await fixture.Service.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = FinanceReportExportTypes.IncomeStatement,
            PeriodStart = PeriodStart,
            PeriodEnd = AsOfDate
        });

        result.SourceOfTruthMode.Should().Contain("non-operating disposal gain presentation");
        result.Warnings.Should().Contain(warning => warning.Contains("Disposal gain", StringComparison.OrdinalIgnoreCase));
        Csv(result).Should().Contain("Warning").And.Contain("Other Income").And.Contain("Disposal gains");
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.IncomeStatementExported);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BackendReportingExport")]
    [Trait("Category", "ReportingExport")]
    public async Task DetailedLedgerExport_UsesPostedGlAndPreservesFilters()
    {
        var fixture = CreateFixture();
        var accountId = Guid.NewGuid();
        fixture.GeneralLedger
            .Setup(service => service.GenerateDetailedLedgerAsync(It.Is<DetailedLedgerRequestDto>(request =>
                request.AccountIds.Contains(accountId) &&
                request.StartDate == PeriodStart &&
                request.EndDate == AsOfDate)))
            .ReturnsAsync(new DetailedLedgerReportDto
            {
                StartDate = PeriodStart,
                EndDate = AsOfDate,
                TotalDebits = 75m,
                TotalCredits = 75m,
                Accounts =
                {
                    new DetailedLedgerAccountDto
                    {
                        AccountId = accountId,
                        AccountNumber = "1000",
                        AccountName = "Cash",
                        AccountType = "Asset",
                        Lines =
                        {
                            new DetailedLedgerLineDto
                            {
                                JournalEntryNumber = "JE-001",
                                TransactionDate = new DateTime(2026, 7, 10),
                                Description = "Posted movement",
                                SourceModule = "GL",
                                DebitAmount = 75m,
                                RunningBalance = 75m,
                                RunningBalanceType = "Debit"
                            }
                        }
                    }
                }
            });

        var result = await fixture.Service.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = FinanceReportExportTypes.DetailedLedger,
            PeriodStart = PeriodStart,
            PeriodEnd = AsOfDate,
            AccountIds = new List<Guid> { accountId }
        });

        result.SourceOfTruthMode.Should().Be("Posted GL");
        result.Totals["TotalDebits"].Should().Be(75m);
        Csv(result).Should().Contain("JE-001").And.Contain("Posted movement");
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.DetailedLedgerExported);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BackendReportingExport")]
    [Trait("Category", "CashBank")]
    public async Task CashBankLedgerExport_UsesPostedGlAndExposesSnapshotVariance()
    {
        var fixture = CreateFixture();
        fixture.GeneralLedger
            .Setup(service => service.GenerateCashBankLedgerAsync(It.IsAny<CashBankLedgerRequestDto>()))
            .ReturnsAsync(new CashBankLedgerReportDto
            {
                StartDate = PeriodStart,
                EndDate = AsOfDate,
                TotalReceipts = 100m,
                TotalClosingBalance = 100m,
                Accounts =
                {
                    new CashBankLedgerAccountDto
                    {
                        BankAccountNumber = "BANK-001",
                        BankAccountName = "Operating Bank",
                        GlAccountNumber = "1000",
                        Receipts = 100m,
                        ClosingBalance = 100m,
                        StoredSnapshotBalance = 120m,
                        Lines =
                        {
                            new CashBankLedgerLineDto
                            {
                                JournalEntryNumber = "JE-CASH",
                                TransactionDate = new DateTime(2026, 7, 12),
                                Description = "Posted receipt",
                                SourceDocumentType = "CashTransaction",
                                DebitAmount = 100m,
                                RunningBalance = 100m
                            }
                        }
                    }
                }
            });

        var result = await fixture.Service.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = FinanceReportExportTypes.CashBankLedger,
            PeriodStart = PeriodStart,
            PeriodEnd = AsOfDate
        });

        result.SourceOfTruthMode.Should().Be("Posted GL with stored bank snapshot variance only");
        Csv(result).Should().Contain("StoredSnapshotBalance").And.Contain("SnapshotVariance").And.Contain("20.00");
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.CashBankLedgerExported);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BackendReportingExport")]
    [Trait("Category", "AccountsPayable")]
    public async Task ApAgingExport_RequiresSettlementReadModel()
    {
        var fixture = CreateFixture();
        fixture.ApReports
            .Setup(service => service.GetDetailedAgingReportAsync(AsOfDate, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApAgingReportDto
            {
                AsOfDate = AsOfDate,
                UsesSettlementReadModel = true,
                TotalOutstanding = 60m,
                Current = 60m,
                SupplierDetails =
                {
                    new SupplierAgingDetailDto
                    {
                        SupplierName = "Supplier A",
                        Invoices =
                        {
                            new ApAgingInvoiceDto
                            {
                                InvoiceNumber = "AP-001",
                                InvoiceDate = new DateTime(2026, 7, 10),
                                DueDate = AsOfDate,
                                TotalAmount = 100m,
                                SettledAmount = 40m,
                                BalanceAmount = 60m,
                                AgingBucket = "Current",
                                SettlementStatus = "PartiallySettled",
                                SourcePostingEventId = Guid.NewGuid(),
                                SourceJournalEntryId = Guid.NewGuid()
                            }
                        }
                    }
                }
            });

        var result = await fixture.Service.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = FinanceReportExportTypes.ApAging,
            AsOfDate = AsOfDate
        });

        result.UsesSettlementReadModel.Should().BeTrue();
        result.SourceOfTruthMode.Should().Contain("AP settlement read model");
        Csv(result).Should().Contain("UsesSettlementReadModel,True").And.Contain("AP-001").And.Contain("60.00");
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.ApAgingExported);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BackendReportingExport")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ArAgingExport_RejectsLegacyOperationalFieldFallback()
    {
        var fixture = CreateFixture();
        fixture.ArReports
            .Setup(service => service.GetDetailedAgingReportAsync(AsOfDate, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DetailedAgingReportDto
            {
                AsOfDate = AsOfDate,
                UsesSettlementReadModel = false
            });

        var act = () => fixture.Service.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = FinanceReportExportTypes.ArAging,
            AsOfDate = AsOfDate
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR aging export requires the AR settlement read model*");
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.ReportExportFailed);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BackendReportingExport")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ArAgingExport_UsesSettlementReadModel()
    {
        var fixture = CreateFixture();
        fixture.ArReports
            .Setup(service => service.GetDetailedAgingReportAsync(AsOfDate, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DetailedAgingReportDto
            {
                AsOfDate = AsOfDate,
                UsesSettlementReadModel = true,
                Summary = new AgingSummaryDto { GrandTotal = 80m, TotalCurrent = 80m },
                Customers =
                {
                    new CustomerDetailedAgingDto
                    {
                        CustomerName = "Customer A",
                        Invoices =
                        {
                            new InvoiceAgingDto
                            {
                                InvoiceNumber = "AR-001",
                                InvoiceDate = new DateTime(2026, 7, 10),
                                DueDate = AsOfDate,
                                TotalAmount = 100m,
                                PaidAmount = 20m,
                                BalanceAmount = 80m,
                                AgingBucket = "Current",
                                SettlementStatus = "PartiallySettled",
                                SourcePostingEventId = Guid.NewGuid(),
                                SourceJournalEntryId = Guid.NewGuid()
                            }
                        }
                    }
                }
            });

        var result = await fixture.Service.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = FinanceReportExportTypes.ArAging,
            AsOfDate = AsOfDate
        });

        result.UsesSettlementReadModel.Should().BeTrue();
        result.SourceOfTruthMode.Should().Contain("AR settlement read model");
        Csv(result).Should().Contain("UsesSettlementReadModel,True").And.Contain("AR-001").And.Contain("80.00");
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.ArAgingExported);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BackendReportingExport")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CustomerStatementExport_UsesArSubledgerDetailedLedger()
    {
        var fixture = CreateFixture();
        var customerId = Guid.NewGuid();
        fixture.ArReports
            .Setup(service => service.GetCustomerDetailedLedgerAsync(
                PeriodStart,
                AsOfDate,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(customerId)),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerDetailedLedgerReportDto
            {
                FromDate = PeriodStart,
                ToDate = AsOfDate,
                CurrencyCode = "Customer Currency",
                ShowCustomerCurrency = true,
                TotalOpeningBalance = 10m,
                TotalDebits = 100m,
                TotalCredits = 40m,
                TotalClosingBalance = 70m,
                Customers =
                {
                    new CustomerDetailedLedgerAccountDto
                    {
                        CustomerId = customerId,
                        CustomerCode = "CUST-001",
                        CustomerName = "Customer A",
                        CurrencyCode = "GHS",
                        OpeningBalance = 10m,
                        TotalDebits = 100m,
                        TotalCredits = 40m,
                        ClosingBalance = 70m,
                        Lines =
                        {
                            new CustomerDetailedLedgerLineDto
                            {
                                SourceDocumentId = Guid.NewGuid(),
                                TransactionDate = new DateTime(2026, 7, 5),
                                TransactionType = "Invoice",
                                DocumentNumber = "AR-INV-001",
                                Description = "Posted customer invoice",
                                TransactionCurrencyCode = "GHS",
                                ExchangeRate = 1m,
                                Debit = 100m,
                                RunningBalance = 110m
                            },
                            new CustomerDetailedLedgerLineDto
                            {
                                SourceDocumentId = Guid.NewGuid(),
                                TransactionDate = new DateTime(2026, 7, 20),
                                TransactionType = "Payment",
                                DocumentNumber = "AR-PAY-001",
                                Reference = "BANK-REF",
                                Description = "Customer receipt",
                                TransactionCurrencyCode = "GHS",
                                ExchangeRate = 1m,
                                Credit = 40m,
                                RunningBalance = 70m
                            }
                        }
                    }
                }
            });

        var result = await fixture.Service.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = "customer-statement",
            PeriodStart = PeriodStart,
            PeriodEnd = AsOfDate,
            CustomerIds = new List<Guid> { customerId },
            ShowCustomerCurrency = true
        });

        result.ReportType.Should().Be(FinanceReportExportTypes.CustomerStatement);
        result.SourceOfTruthMode.Should().Contain("AR subledger statement");
        result.Totals["TotalClosingBalance"].Should().Be(70m);
        Csv(result).Should().Contain("CustomerCode").And.Contain("Opening Balance").And.Contain("AR-INV-001").And.Contain("Closing Balance");
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.CustomerStatementExported);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BackendReportingExport")]
    [Trait("Category", "AccountsPayable")]
    public async Task SupplierStatementExport_UsesApSubledgerDetailedLedger()
    {
        var fixture = CreateFixture();
        var supplierId = Guid.NewGuid();
        fixture.ApReports
            .Setup(service => service.GetSupplierDetailedLedgerAsync(
                PeriodStart,
                AsOfDate,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(supplierId)),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupplierDetailedLedgerReportDto
            {
                FromDate = PeriodStart,
                ToDate = AsOfDate,
                CurrencyCode = "GHS",
                TotalOpeningBalance = 25m,
                TotalDebits = 30m,
                TotalCredits = 90m,
                TotalClosingBalance = 85m,
                Suppliers =
                {
                    new SupplierDetailedLedgerAccountDto
                    {
                        SupplierId = supplierId,
                        SupplierCode = "SUP-001",
                        SupplierName = "Supplier A",
                        CurrencyCode = "GHS",
                        OpeningBalance = 25m,
                        TotalDebits = 30m,
                        TotalCredits = 90m,
                        ClosingBalance = 85m,
                        Lines =
                        {
                            new SupplierDetailedLedgerLineDto
                            {
                                SourceDocumentId = Guid.NewGuid(),
                                TransactionDate = new DateTime(2026, 7, 8),
                                TransactionType = "Invoice",
                                DocumentNumber = "AP-INV-001",
                                Description = "Supplier invoice",
                                TransactionCurrencyCode = "GHS",
                                ExchangeRate = 1m,
                                Credit = 90m,
                                RunningBalance = 115m
                            },
                            new SupplierDetailedLedgerLineDto
                            {
                                SourceDocumentId = Guid.NewGuid(),
                                TransactionDate = new DateTime(2026, 7, 24),
                                TransactionType = "Payment",
                                DocumentNumber = "AP-PAY-001",
                                Description = "Supplier payment",
                                TransactionCurrencyCode = "GHS",
                                ExchangeRate = 1m,
                                Debit = 30m,
                                RunningBalance = 85m
                            }
                        }
                    }
                }
            });

        var result = await fixture.Service.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = "supplier-statement",
            PeriodStart = PeriodStart,
            PeriodEnd = AsOfDate,
            SupplierIds = new List<Guid> { supplierId }
        });

        result.ReportType.Should().Be(FinanceReportExportTypes.SupplierStatement);
        result.SourceOfTruthMode.Should().Contain("AP subledger statement");
        result.Totals["TotalClosingBalance"].Should().Be(85m);
        Csv(result).Should().Contain("SupplierCode").And.Contain("Opening Balance").And.Contain("AP-INV-001").And.Contain("Closing Balance");
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.SupplierStatementExported);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BackendReportingExport")]
    [Trait("Category", "FixedAssets")]
    public async Task FixedAssetGlReconciliationExport_ExposesVarianceAndPresentationWarning()
    {
        var fixture = CreateFixture();
        fixture.FixedAssetReports
            .Setup(service => service.GetGlReconciliationReportAsync(It.IsAny<FixedAssetReportQueryDto>()))
            .ReturnsAsync(new FixedAssetGlReconciliationReportDto
            {
                TotalGlBalance = 100m,
                TotalSubledgerBalance = 95m,
                TotalVariance = 5m,
                Rows =
                {
                    new FixedAssetGlReconciliationRowDto
                    {
                        Area = "Disposal Gain",
                        AccountNumber = "5999",
                        AccountName = "Disposal gain presentation",
                        AccountType = AccountType.Expense,
                        GlBalance = 100m,
                        SubledgerBalance = 95m,
                        Variance = 5m,
                        SourceDocumentCount = 1,
                        PresentationWarning = "Disposal gain account is presented as non-operating disposal gain."
                    }
                }
            });

        var result = await fixture.Service.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = FinanceReportExportTypes.FixedAssetGlReconciliation,
            AsOfDate = AsOfDate
        });

        result.SourceOfTruthMode.Should().Be("Posted GL reconciled to fixed asset subledger snapshots");
        result.Warnings.Should().Contain(warning => warning.Contains("non-operating disposal gain", StringComparison.OrdinalIgnoreCase));
        result.Totals["TotalVariance"].Should().Be(5m);
        Csv(result).Should().Contain("Variance").And.Contain("5.00").And.Contain("Disposal Gain");
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.FixedAssetGlReconciliationExported);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BackendReportingExport")]
    [Trait("Category", "ReportingExport")]
    public async Task UnsupportedExportFormat_FailsClearlyAndCreatesAuditEvent()
    {
        var fixture = CreateFixture();

        var act = () => fixture.Service.ExportAsync(new FinanceReportExportRequestDto
        {
            ReportType = FinanceReportExportTypes.TrialBalance,
            Format = "Pdf",
            AsOfDate = AsOfDate
        });

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("Finance report export format 'Pdf' is not supported*");
        fixture.Audit.EventTypes.Should().Contain(FinanceAuditEvents.ReportExportFailed);
    }

    private static TestFixture CreateFixture()
    {
        var tenantId = Guid.NewGuid();
        var generalLedger = new Mock<IGeneralLedgerService>(MockBehavior.Strict);
        var apReports = new Mock<IApReportsService>(MockBehavior.Strict);
        var arReports = new Mock<IArReportsService>(MockBehavior.Strict);
        var fixedAssetReports = new Mock<IFixedAssetReportsService>(MockBehavior.Strict);
        var currentUser = new Mock<ICurrentUserService>();
        var audit = new CapturingFinanceAuditService();

        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserId).Returns("finance-user");
        currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);

        var service = new FinanceReportExportService(
            generalLedger.Object,
            apReports.Object,
            arReports.Object,
            fixedAssetReports.Object,
            currentUser.Object,
            NullLogger<FinanceReportExportService>.Instance,
            audit);

        return new TestFixture(service, generalLedger, apReports, arReports, fixedAssetReports, audit);
    }

    private static string Csv(FinanceReportExportResultDto result)
        => System.Text.Encoding.UTF8.GetString(result.Content);

    private sealed record TestFixture(
        FinanceReportExportService Service,
        Mock<IGeneralLedgerService> GeneralLedger,
        Mock<IApReportsService> ApReports,
        Mock<IArReportsService> ArReports,
        Mock<IFixedAssetReportsService> FixedAssetReports,
        CapturingFinanceAuditService Audit);

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
                Action = auditEvent.EventType,
                Resource = auditEvent.Resource ?? "Finance.ReportExport",
                TenantId = auditEvent.TenantId
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
}
