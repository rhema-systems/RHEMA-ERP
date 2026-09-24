using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Data;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Enums;
using ErpSystem.Api.Services.Finance;

namespace ErpSystem.Api.Services.Finance.GL
{
    /// <summary>
    /// Legacy implementation retained for historical migration reference only.
    /// This service is intentionally not registered in normal application DI; normal Finance posting must use IFinancePostingEngine.
    /// </summary>
    [Obsolete("Legacy subledger posting is disabled for normal runtime flows. Use IFinancePostingEngine through the owning Finance module.")]
    public class SubledgerPostingService : ISubledgerPostingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly IJournalEntryService _journalEntryService;
        private readonly ILogger<SubledgerPostingService> _logger;

        public SubledgerPostingService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            ITenantSettingsService tenantSettingsService,
            IJournalEntryService journalEntryService,
            ILogger<SubledgerPostingService> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _tenantSettingsService = tenantSettingsService;
            _journalEntryService = journalEntryService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

        private async Task<FinanceSettings> GetSettingsAsync(CancellationToken cancellationToken)
        {
            var tenantId = TenantId;
            var settings = await _context.Set<FinanceSettings>()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

            if (settings == null)
            {
                throw new InvalidOperationException("Finance settings not configured for this tenant.");
            }

            return settings;
        }

        public async Task<JournalEntryDto> PostFinancePurchaseOrderReceiptAsync(Guid receiptId, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var existingJournal = await _context.JournalEntries
                .FirstOrDefaultAsync(j =>
                    j.TenantId == tenantId &&
                    j.SourceDocumentId == receiptId &&
                    j.SourceDocumentType == "FinancePurchaseOrderReceipt" &&
                    !j.IsDeleted,
                    cancellationToken);

            if (existingJournal != null)
            {
                if (existingJournal.PostingStatus != "Posted")
                {
                    return await _journalEntryService.PostJournalEntryAsync(existingJournal.Id, cancellationToken);
                }

                return await _journalEntryService.GetJournalEntryByIdAsync(existingJournal.Id, cancellationToken)
                    ?? throw new InvalidOperationException($"Finance GRV {receiptId} references missing journal entry {existingJournal.Id}.");
            }

            var receipt = await _context.FinancePurchaseOrderReceipts
                .Include(r => r.FinancePurchaseOrder)
                    .ThenInclude(po => po.Vendor)
                .Include(r => r.Items.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.FinancePurchaseOrderItem)
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == receiptId && !r.IsDeleted, cancellationToken);

            if (receipt == null)
            {
                throw new ArgumentException($"Finance purchase receipt {receiptId} not found.");
            }

            var settings = await GetSettingsAsync(cancellationToken);
            var grvAccrualAccountId = settings.ControlAccountGRVAccrualId;
            if (grvAccrualAccountId == null)
            {
                throw new InvalidOperationException("GRV Accrual Control Account is not configured in Finance Settings.");
            }

            var transactions = new List<CreateAccountTransactionDto>();
            var purchaseOrder = receipt.FinancePurchaseOrder;
            var headerCurrencyCode = NormalizeCurrency(purchaseOrder.CurrencyCode);
            var headerExchangeRate = NormalizeExchangeRate(purchaseOrder.ExchangeRate);

            foreach (var receiptItem in receipt.Items.Where(i => !i.IsDeleted))
            {
                var poItem = receiptItem.FinancePurchaseOrderItem;
                if (poItem == null)
                {
                    throw new InvalidOperationException($"Finance GRV line {receiptItem.Id} is missing its purchase order line.");
                }

                var lineForeignAmount = CalculateFinanceReceiptLineForeignAmount(receiptItem, poItem);
                if (lineForeignAmount <= 0)
                {
                    continue;
                }

                Guid debitAccountId;
                if (poItem.LineType == 1)
                {
                    debitAccountId = settings.ControlAccountInventoryId
                        ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.");
                }
                else
                {
                    debitAccountId = poItem.GlAccountId
                        ?? throw new InvalidOperationException($"No GL account specified for finance GRV line '{poItem.Description}'.");
                }

                var lineCurrencyCode = NormalizeCurrency(poItem.CurrencyCode ?? headerCurrencyCode);
                var lineExchangeRate = NormalizeExchangeRate(poItem.ExchangeRate > 0 ? poItem.ExchangeRate : headerExchangeRate);
                var lineBaseAmount = ToBaseAmount(lineForeignAmount, lineExchangeRate);
                if (lineBaseAmount <= 0)
                {
                    continue;
                }

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = debitAccountId,
                    Description = $"GRV {receipt.ReceiptNumber} - {poItem.Description}",
                    TransactionType = "Debit",
                    Amount = lineBaseAmount,
                    Reference = receipt.ReceiptNumber,
                    CurrencyCode = lineCurrencyCode,
                    ExchangeRate = lineExchangeRate,
                    ForeignAmount = lineForeignAmount
                });

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = grvAccrualAccountId.Value,
                    Description = $"GRV accrual - {receipt.ReceiptNumber} - {poItem.Description}",
                    TransactionType = "Credit",
                    Amount = lineBaseAmount,
                    Reference = receipt.ReceiptNumber,
                    CurrencyCode = lineCurrencyCode,
                    ExchangeRate = lineExchangeRate,
                    ForeignAmount = lineForeignAmount
                });
            }

            if (transactions.Count == 0)
            {
                throw new InvalidOperationException($"Finance GRV {receipt.ReceiptNumber} has no positive-value lines to post.");
            }

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = receipt.ReceiptDate,
                JournalType = "System Generated",
                Description = $"Finance GRV {receipt.ReceiptNumber} - {purchaseOrder.Vendor?.PartnerName ?? purchaseOrder.OrderNumber}",
                Reference = receipt.ReceiptNumber,
                SourceModule = "AP",
                SourceDocumentId = receipt.Id,
                SourceDocumentType = "FinancePurchaseOrderReceipt",
                Transactions = transactions
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        [Obsolete("Normal AP invoice posting uses IVendorInvoiceService.PostAsync and IFinancePostingEngine. This legacy method is reserved for guarded opening-balance/migration paths.")]
        public async Task<JournalEntryDto> PostApInvoiceAsync(Guid vendorInvoiceId, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var invoice = await _context.Set<VendorInvoice>()
                .Include(i => i.LineItems)
                .Include(i => i.BusinessPartner)
                .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == vendorInvoiceId && !i.IsDeleted, cancellationToken);

            if (invoice == null) throw new ArgumentException($"Vendor Invoice {vendorInvoiceId} not found.");
            if (!invoice.IsOpeningBalance)
                throw new InvalidOperationException("Legacy AP invoice posting is disabled for normal invoices. Use IVendorInvoiceService.PostAsync.");

            var existingInvoiceJournal = await GetExistingSourceJournalAsync(
                tenantId,
                invoice.JournalEntryId,
                invoice.Id,
                "VendorInvoice",
                invoice.InvoiceNumber,
                cancellationToken);
            if (existingInvoiceJournal != null)
            {
                invoice.JournalEntryId = existingInvoiceJournal.Id;
                await _context.SaveChangesAsync(cancellationToken);
                return await EnsurePostedAsync(existingInvoiceJournal, cancellationToken);
            }

            var settings = await GetSettingsAsync(cancellationToken);

            // Resolve AP Control Account
            var apAccountId = settings.ControlAccountApId;
            if (apAccountId == null) throw new InvalidOperationException("AP Control Account not configured.");

            var invoiceCurrencyCode = NormalizeCurrency(invoice.CurrencyCode);
            var invoiceExchangeRate = NormalizeExchangeRate(invoice.ExchangeRate);

            if (invoice.IsOpeningBalance)
            {
                var migrationClearingAccountId = settings.MigrationClearingAccountId;
                if (migrationClearingAccountId == null) throw new InvalidOperationException("Migration Clearing Account is not configured in Finance Settings.");

                var openingBaseAmount = invoice.BaseCurrencyAmount > 0m
                    ? invoice.BaseCurrencyAmount
                    : ToBaseAmount(invoice.TotalAmount, invoiceExchangeRate);
                if (openingBaseAmount <= 0m)
                {
                    throw new InvalidOperationException($"Vendor opening balance invoice {invoice.InvoiceNumber} has no positive AP amount to post.");
                }

                var openingTransactions = new List<CreateAccountTransactionDto>
                {
                    new CreateAccountTransactionDto
                    {
                        AccountId = migrationClearingAccountId.Value,
                        Description = $"Opening AP balance - {invoice.InvoiceNumber}",
                        TransactionType = "Debit",
                        Amount = openingBaseAmount,
                        Reference = invoice.InvoiceNumber,
                        CurrencyCode = invoiceCurrencyCode,
                        ExchangeRate = invoiceExchangeRate,
                        ForeignAmount = invoice.TotalAmount
                    },
                    new CreateAccountTransactionDto
                    {
                        AccountId = apAccountId.Value,
                        Description = $"Opening AP balance - {invoice.InvoiceNumber}",
                        TransactionType = "Credit",
                        Amount = openingBaseAmount,
                        Reference = invoice.InvoiceNumber,
                        CurrencyCode = invoiceCurrencyCode,
                        ExchangeRate = invoiceExchangeRate,
                        ForeignAmount = invoice.TotalAmount
                    }
                };

                var openingJeDto = new CreateJournalEntryDto
                {
                    TransactionDate = invoice.InvoiceDate,
                    JournalType = "Opening Balance",
                    Description = $"Opening AP Balance {invoice.InvoiceNumber} - {invoice.SupplierName}",
                    Reference = invoice.InvoiceNumber,
                    SourceModule = "AP",
                    SourceDocumentId = invoice.Id,
                    SourceDocumentType = "VendorInvoice",
                    Transactions = openingTransactions
                };

                var openingJournalEntry = await _journalEntryService.CreateJournalEntryAsync(openingJeDto, cancellationToken);
                invoice.JournalEntryId = openingJournalEntry.Id;
                await _context.SaveChangesAsync(cancellationToken);

                return await _journalEntryService.PostJournalEntryAsync(openingJournalEntry.Id, cancellationToken);
            }

            var linkedFinanceReceipt = await _context.FinancePurchaseOrderReceipts
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.VendorInvoiceId == invoice.Id && !r.IsDeleted, cancellationToken);
            var clearsFinanceGrv = linkedFinanceReceipt != null;
            var grvAccrualAccountId = settings.ControlAccountGRVAccrualId;
            if (clearsFinanceGrv && grvAccrualAccountId == null)
            {
                throw new InvalidOperationException("GRV Accrual Control Account is not configured in Finance Settings.");
            }

            var transactions = new List<CreateAccountTransactionDto>();

            // Process Debits for Lines
            var documentDiscountAmount = 0m;
            foreach (var line in invoice.LineItems)
            {
                var lineGrossAmount = line.LineTotal;
                if (lineGrossAmount <= 0) continue;

                if (clearsFinanceGrv)
                {
                    var lineNetAmount = lineGrossAmount - line.DiscountAmount;
                    if (lineNetAmount <= 0) continue;

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = grvAccrualAccountId!.Value,
                        Description = $"Clear GRV accrual - {invoice.InvoiceNumber} - {line.Description}",
                        TransactionType = "Debit",
                        Amount = ToBaseAmount(lineNetAmount, invoiceExchangeRate),
                        Reference = invoice.InvoiceNumber,
                        CurrencyCode = invoiceCurrencyCode,
                        ExchangeRate = invoiceExchangeRate,
                        ForeignAmount = lineNetAmount
                    });

                    continue;
                }

                documentDiscountAmount += line.DiscountAmount;

                if (string.Equals(line.LineItemType, "Inventory", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(line.LineItemType, "Product", StringComparison.OrdinalIgnoreCase))
                {
                    var inventoryAccId = settings.ControlAccountInventoryId;
                    if (inventoryAccId == null) throw new InvalidOperationException("Inventory Control Account not configured in Finance Settings.");

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = inventoryAccId.Value,
                        Description = $"Inventory Receipt - {invoice.InvoiceNumber} - {line.Description}",
                        TransactionType = "Debit",
                        Amount = ToBaseAmount(lineGrossAmount, invoiceExchangeRate),
                        Reference = invoice.InvoiceNumber,
                        CurrencyCode = invoiceCurrencyCode,
                        ExchangeRate = invoiceExchangeRate,
                        ForeignAmount = lineGrossAmount
                    });

                    // Note: Actual Inventory movement (PurchaseReceipt) is created by the InventoryService or AP Service before calling this,
                    // or handles it alongside this. But they belong to separate modules.
                }
                else // Expense
                {
                    var expenseAccId = line.GLAccountId ?? invoice.BusinessPartner.DefaultExpenseAccountId;
                    if (expenseAccId == null) throw new InvalidOperationException($"No Expense Account specified for AP line '{line.Description}'.");

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = expenseAccId.Value,
                        Description = $"Expense - {invoice.InvoiceNumber} - {line.Description}",
                        TransactionType = "Debit",
                        Amount = ToBaseAmount(lineGrossAmount, invoiceExchangeRate),
                        Reference = invoice.InvoiceNumber,
                        CurrencyCode = invoiceCurrencyCode,
                        ExchangeRate = invoiceExchangeRate,
                        ForeignAmount = lineGrossAmount
                    });
                }
            }

            if (!clearsFinanceGrv && documentDiscountAmount > 0)
            {
                var discountReceivedAccountId = settings.DiscountReceivedAccountId;
                if (discountReceivedAccountId == null) throw new InvalidOperationException("Purchase Discounts Received Account not configured in Finance Settings.");

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = discountReceivedAccountId.Value,
                    Description = $"Purchase Discount - {invoice.InvoiceNumber}",
                    TransactionType = "Credit",
                    Amount = ToBaseAmount(documentDiscountAmount, invoiceExchangeRate),
                    Reference = invoice.InvoiceNumber,
                    CurrencyCode = invoiceCurrencyCode,
                    ExchangeRate = invoiceExchangeRate,
                    ForeignAmount = documentDiscountAmount
                });
            }

            // Handle Tax if AP lines don't already include it directly, or if it's separate.
            // Simplified: Assuming lines total subtotal + tax = total amount. 
            // In a real system, Tax has its own GL account.
            if (invoice.TaxAmount > 0)
            {
                var taxAccId = settings.ControlAccountTaxId;
                if (taxAccId == null) throw new InvalidOperationException("Tax Control Account not configured.");
                
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = taxAccId.Value,
                    Description = $"Input Tax - {invoice.InvoiceNumber}",
                    TransactionType = "Debit",
                    Amount = ToBaseAmount(invoice.TaxAmount, invoiceExchangeRate),
                    Reference = invoice.InvoiceNumber,
                    CurrencyCode = invoiceCurrencyCode,
                    ExchangeRate = invoiceExchangeRate,
                    ForeignAmount = invoice.TaxAmount
                });
            }

            var debitBaseTotal = transactions.Where(t => t.TransactionType == "Debit").Sum(t => t.Amount);
            var creditBaseTotal = transactions.Where(t => t.TransactionType == "Credit").Sum(t => t.Amount);
            var apBaseAmount = debitBaseTotal - creditBaseTotal;
            if (apBaseAmount <= 0)
            {
                throw new InvalidOperationException($"Vendor invoice {invoice.InvoiceNumber} has no positive AP amount to post.");
            }

            // AP Control Credit Line. Base amount is derived from the balanced detail lines so
            // rounding cannot create an unbalanced journal.
            transactions.Insert(0, new CreateAccountTransactionDto
            {
                AccountId = apAccountId.Value,
                Description = $"AP Invoice {invoice.InvoiceNumber}",
                TransactionType = "Credit",
                Amount = apBaseAmount,
                Reference = invoice.InvoiceNumber,
                CurrencyCode = invoiceCurrencyCode,
                ExchangeRate = invoiceExchangeRate,
                ForeignAmount = invoice.TotalAmount
            });

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = invoice.InvoiceDate,
                Description = $"Vendor Invoice {invoice.InvoiceNumber} - {invoice.SupplierName}",
                Reference = invoice.InvoiceNumber,
                SourceModule = "AP",
                SourceDocumentId = invoice.Id,
                SourceDocumentType = "VendorInvoice",
                Transactions = transactions
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            
            // Link JE to invoice
            invoice.JournalEntryId = journalEntry.Id;
            await _context.SaveChangesAsync(cancellationToken);

            // Auto-post the JE if configured, or just leave as Draft. AP invoices usually post immediately.
            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        private static decimal CalculateFinanceReceiptLineForeignAmount(
            FinancePurchaseOrderReceiptItem receiptItem,
            FinancePurchaseOrderItem poItem)
        {
            var grossAmount = receiptItem.QuantityReceived * poItem.UnitPrice;
            var discountPercentage = receiptItem.DiscountPercentage > 0m
                ? receiptItem.DiscountPercentage
                : poItem.DiscountPercentage;
            var discountAmount = receiptItem.DiscountAmount > 0m
                ? receiptItem.DiscountAmount
                : grossAmount * (discountPercentage / 100m);

            return decimal.Round(grossAmount - discountAmount, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal ToBaseAmount(decimal foreignAmount, decimal exchangeRate)
            => decimal.Round(foreignAmount * NormalizeExchangeRate(exchangeRate), 2, MidpointRounding.AwayFromZero);

        private static decimal NormalizeExchangeRate(decimal exchangeRate)
            => exchangeRate <= 0m ? 1m : exchangeRate;

        private static string NormalizeCurrency(string? currencyCode)
            => string.IsNullOrWhiteSpace(currencyCode) ? "GHS" : currencyCode.Trim().ToUpperInvariant();

        private static bool IsLegacyApPaymentPostingDisabled() => true;

        [Obsolete("Normal AP payment posting uses IVendorPaymentService.PostAsync and IFinancePostingEngine. This legacy method is reserved for migration compatibility only.")]
        public async Task<JournalEntryDto> PostApPaymentAsync(Guid vendorPaymentId, CancellationToken cancellationToken = default)
        {
            if (IsLegacyApPaymentPostingDisabled())
                throw new InvalidOperationException("Legacy AP payment posting is disabled. Use IVendorPaymentService.PostAsync.");

            var tenantId = TenantId;
            var payment = await _context.Set<VendorPayment>()
                .Include(p => p.Supplier)
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == vendorPaymentId && !p.IsDeleted, cancellationToken);

            if (payment == null) throw new ArgumentException($"Vendor Payment {vendorPaymentId} not found.");

            var existingPaymentJournal = await GetExistingSourceJournalAsync(
                tenantId,
                payment.JournalEntryId,
                payment.Id,
                "VendorPayment",
                payment.PaymentNumber,
                cancellationToken);
            if (existingPaymentJournal != null)
            {
                payment.JournalEntryId = existingPaymentJournal.Id;
                await _context.SaveChangesAsync(cancellationToken);
                return await EnsurePostedAsync(existingPaymentJournal, cancellationToken);
            }

            var settings = await GetSettingsAsync(cancellationToken);

            var apAccountId = payment.Supplier.DefaultApAccountId ?? settings.ControlAccountApId;
            if (apAccountId == null) throw new InvalidOperationException("AP Control Account not configured.");

            var bankAccountId = payment.BankAccountId ?? settings.DefaultBankAccountId;
            if (bankAccountId == null) throw new InvalidOperationException("Bank Account not configured for payment.");

            var bankAccount = await _context.Set<BankAccount>()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == bankAccountId.Value && !a.IsDeleted, cancellationToken);
            if (bankAccount == null) throw new InvalidOperationException("Bank Account not found for payment.");
            if (bankAccount.GLAccountId == null) throw new InvalidOperationException($"Bank Account '{bankAccount.AccountName}' is not linked to a GL account.");
            payment.BankAccountId ??= bankAccount.Id;

            var currencyCode = NormalizeCurrency(payment.CurrencyCode);
            var exchangeRate = NormalizeExchangeRate(payment.ExchangeRate);
            var discountTaken = payment.Allocations?
                .Where(a => !a.IsReversal)
                .Sum(a => a.DiscountAmount) ?? payment.DiscountTaken;
            var withholdingTaxAmount = payment.Allocations?
                .Where(a => !a.IsReversal)
                .Sum(a => a.WithholdingTaxAmount) ?? 0m;
            if (withholdingTaxAmount <= 0m)
            {
                withholdingTaxAmount = payment.WithholdingTaxAmount;
            }

            var apSettlementAmount = payment.TotalAmount + discountTaken + withholdingTaxAmount;

            var transactions = new List<CreateAccountTransactionDto>
            {
                new CreateAccountTransactionDto
                {
                    AccountId = apAccountId.Value,
                    Description = $"Vendor Payment {payment.PaymentNumber}",
                    TransactionType = "Debit",
                    Amount = ToBaseAmount(apSettlementAmount, exchangeRate),
                    Reference = payment.PaymentNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = apSettlementAmount
                },
                new CreateAccountTransactionDto
                {
                    AccountId = bankAccount.GLAccountId.Value,
                    Description = $"Vendor Payment {payment.PaymentNumber}",
                    TransactionType = "Credit",
                    Amount = ToBaseAmount(payment.TotalAmount, exchangeRate),
                    Reference = payment.PaymentNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = payment.TotalAmount
                }
            };

            if (discountTaken > 0)
            {
                var discountReceivedAccountId = settings.DiscountReceivedAccountId;
                if (discountReceivedAccountId == null) throw new InvalidOperationException("Purchase Discounts Received Account not configured in Finance Settings.");

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = discountReceivedAccountId.Value,
                    Description = $"Purchase Discount Taken - {payment.PaymentNumber}",
                    TransactionType = "Credit",
                    Amount = ToBaseAmount(discountTaken, exchangeRate),
                    Reference = payment.PaymentNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = discountTaken
                });
            }

            if (withholdingTaxAmount > 0)
            {
                var taxAccountId = settings.ControlAccountTaxId;
                if (taxAccountId == null) throw new InvalidOperationException("Tax Control Account not configured for AP withholding tax.");

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = taxAccountId.Value,
                    Description = $"Withholding Tax - {payment.PaymentNumber}",
                    TransactionType = "Credit",
                    Amount = ToBaseAmount(withholdingTaxAmount, exchangeRate),
                    Reference = payment.PaymentNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = withholdingTaxAmount
                });
            }

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = payment.PaymentDate,
                Description = $"Vendor Payment {payment.PaymentNumber} - {payment.Supplier.Name}",
                Reference = payment.PaymentNumber,
                SourceModule = "AP",
                SourceDocumentId = payment.Id,
                SourceDocumentType = "VendorPayment",
                Transactions = transactions
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            payment.JournalEntryId = journalEntry.Id;
            await _context.SaveChangesAsync(cancellationToken);

            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        [Obsolete("AP payment discount posting must use the central finance posting engine. This legacy method is reserved for migration compatibility only.")]
        public async Task<JournalEntryDto> PostApPaymentDiscountAdjustmentAsync(Guid allocationId, CancellationToken cancellationToken = default)
        {
            if (IsLegacyApPaymentPostingDisabled())
                throw new InvalidOperationException("Legacy AP payment discount posting is disabled. Use the central finance posting engine.");

            var tenantId = TenantId;
            var allocation = await _context.Set<VendorPaymentAllocation>()
                .Include(a => a.VendorPayment)
                    .ThenInclude(p => p.Supplier)
                .Include(a => a.VendorInvoice)
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == allocationId && !a.IsDeleted, cancellationToken);

            if (allocation == null) throw new ArgumentException($"Vendor payment allocation {allocationId} not found.");
            if (allocation.IsReversal || allocation.DiscountAmount <= 0)
                throw new InvalidOperationException("Only positive, non-reversal AP payment discount allocations can be posted.");

            var existingJournal = await GetExistingSourceJournalAsync(
                tenantId,
                null,
                allocation.Id,
                "VendorPaymentDiscountAdjustment",
                allocation.VendorPayment.PaymentNumber,
                cancellationToken);
            if (existingJournal != null)
            {
                return await EnsurePostedAsync(existingJournal, cancellationToken);
            }

            var settings = await GetSettingsAsync(cancellationToken);
            var apAccountId = allocation.VendorPayment.Supplier.DefaultApAccountId ?? settings.ControlAccountApId;
            if (apAccountId == null) throw new InvalidOperationException("AP Control Account not configured.");

            var discountReceivedAccountId = settings.DiscountReceivedAccountId;
            if (discountReceivedAccountId == null) throw new InvalidOperationException("Purchase Discounts Received Account not configured in Finance Settings.");

            var currencyCode = NormalizeCurrency(allocation.VendorPayment.CurrencyCode);
            var exchangeRate = NormalizeExchangeRate(allocation.VendorPayment.ExchangeRate);
            var baseAmount = ToBaseAmount(allocation.DiscountAmount, exchangeRate);

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = allocation.AllocationDate,
                Description = $"Purchase Discount Taken - {allocation.VendorPayment.PaymentNumber}",
                Reference = allocation.VendorPayment.PaymentNumber,
                SourceModule = "AP",
                SourceDocumentId = allocation.Id,
                SourceDocumentType = "VendorPaymentDiscountAdjustment",
                Transactions = new List<CreateAccountTransactionDto>
                {
                    new CreateAccountTransactionDto
                    {
                        AccountId = apAccountId.Value,
                        Description = $"AP discount settlement - {allocation.VendorInvoice.InvoiceNumber}",
                        TransactionType = "Debit",
                        Amount = baseAmount,
                        Reference = allocation.VendorPayment.PaymentNumber,
                        CurrencyCode = currencyCode,
                        ExchangeRate = exchangeRate,
                        ForeignAmount = allocation.DiscountAmount
                    },
                    new CreateAccountTransactionDto
                    {
                        AccountId = discountReceivedAccountId.Value,
                        Description = $"Purchase Discount Taken - {allocation.VendorInvoice.InvoiceNumber}",
                        TransactionType = "Credit",
                        Amount = baseAmount,
                        Reference = allocation.VendorPayment.PaymentNumber,
                        CurrencyCode = currencyCode,
                        ExchangeRate = exchangeRate,
                        ForeignAmount = allocation.DiscountAmount
                    }
                }
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        public async Task<JournalEntryDto> PostSupplierDebitNoteAsync(Guid supplierDebitNoteId, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var debitNote = await _context.SupplierDebitNotes
                .Include(d => d.Vendor)
                .Include(d => d.LineItems.Where(l => !l.IsDeleted))
                .Include(d => d.SupplierReturn)
                    .ThenInclude(r => r!.LineItems.Where(l => !l.IsDeleted))
                .Include(d => d.OriginalVendorInvoice)
                    .ThenInclude(i => i!.LineItems)
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == supplierDebitNoteId && !d.IsDeleted, cancellationToken);

            if (debitNote == null) throw new ArgumentException($"Supplier Debit Note {supplierDebitNoteId} not found.");

            var existingDebitNoteJournal = await GetExistingSourceJournalAsync(
                tenantId,
                debitNote.JournalEntryId,
                debitNote.Id,
                "SupplierDebitNote",
                debitNote.DebitNoteNumber,
                cancellationToken);
            if (existingDebitNoteJournal != null)
            {
                debitNote.JournalEntryId = existingDebitNoteJournal.Id;
                debitNote.Status = SupplierDebitNoteStatus.Posted;
                await _context.SaveChangesAsync(cancellationToken);
                return await EnsurePostedAsync(existingDebitNoteJournal, cancellationToken);
            }

            var supplierReturn = debitNote.SupplierReturn
                ?? throw new InvalidOperationException($"Supplier debit note {debitNote.DebitNoteNumber} is not linked to a supplier return.");
            var settings = await GetSettingsAsync(cancellationToken);
            var currencyCode = NormalizeCurrency(debitNote.CurrencyCode);
            var exchangeRate = NormalizeExchangeRate(debitNote.ExchangeRate);
            var invoiceLinked = debitNote.OriginalVendorInvoiceId.HasValue;
            var grvLinked = supplierReturn.OriginalFinancePurchaseOrderReceiptId.HasValue && !invoiceLinked;
            if (!invoiceLinked && !grvLinked)
            {
                throw new InvalidOperationException($"Supplier return {supplierReturn.ReturnNumber} must reference either a vendor invoice or a finance GRV.");
            }

            var controlAccountId = invoiceLinked
                ? debitNote.Vendor.DefaultApAccountId ?? settings.ControlAccountApId
                : settings.ControlAccountGRVAccrualId;
            if (controlAccountId == null)
            {
                throw new InvalidOperationException(invoiceLinked
                    ? "AP Control Account not configured."
                    : "GRV Accrual Control Account is not configured in Finance Settings.");
            }

            var transactions = new List<CreateAccountTransactionDto>();
            var returnLinesByIndex = supplierReturn.LineItems.Where(l => !l.IsDeleted).OrderBy(l => l.CreatedAt).ToList();
            var debitLinesByIndex = debitNote.LineItems.Where(l => !l.IsDeleted).OrderBy(l => l.CreatedAt).ToList();

            Dictionary<Guid, VendorInvoiceLineItem>? vendorInvoiceLineById = null;
            if (invoiceLinked)
            {
                vendorInvoiceLineById = debitNote.OriginalVendorInvoice?.LineItems?
                    .Where(l => !l.IsDeleted)
                    .ToDictionary(l => l.Id, l => l)
                    ?? new Dictionary<Guid, VendorInvoiceLineItem>();
            }

            Dictionary<Guid, FinancePurchaseOrderItem>? financePoLineById = null;
            if (grvLinked)
            {
                var financePoLineIds = returnLinesByIndex
                    .Where(r => r.OriginalFinancePurchaseOrderItemId.HasValue)
                    .Select(r => r.OriginalFinancePurchaseOrderItemId!.Value)
                    .ToList();

                financePoLineById = await _context.FinancePurchaseOrderItems
                    .Where(l => financePoLineIds.Contains(l.Id))
                    .ToDictionaryAsync(l => l.Id, cancellationToken);
            }

            for (var index = 0; index < debitLinesByIndex.Count; index++)
            {
                var debitLine = debitLinesByIndex[index];
                var returnLine = index < returnLinesByIndex.Count ? returnLinesByIndex[index] : null;
                var lineForeignAmount = CalculateSupplierDebitNoteLineForeignAmount(debitLine);
                if (lineForeignAmount <= 0) continue;

                Guid creditAccountId;
                if (invoiceLinked)
                {
                    VendorInvoiceLineItem? sourceLine = null;
                    if (returnLine?.OriginalVendorInvoiceLineItemId.HasValue == true && vendorInvoiceLineById != null)
                    {
                        vendorInvoiceLineById.TryGetValue(returnLine.OriginalVendorInvoiceLineItemId.Value, out sourceLine);
                    }

                    if (string.Equals(sourceLine?.LineItemType, "Inventory", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(sourceLine?.LineItemType, "Product", StringComparison.OrdinalIgnoreCase))
                    {
                        creditAccountId = settings.ControlAccountInventoryId
                            ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.");
                    }
                    else
                    {
                        creditAccountId = sourceLine?.GLAccountId
                            ?? debitNote.Vendor.DefaultExpenseAccountId
                            ?? throw new InvalidOperationException($"No expense account could be resolved for supplier return line '{debitLine.Description}'.");
                    }
                }
                else
                {
                    FinancePurchaseOrderItem? sourceLine = null;
                    if (returnLine?.OriginalFinancePurchaseOrderItemId.HasValue == true && financePoLineById != null)
                    {
                        financePoLineById.TryGetValue(returnLine.OriginalFinancePurchaseOrderItemId.Value, out sourceLine);
                    }

                    if (sourceLine == null)
                    {
                        throw new InvalidOperationException($"No finance PO line could be resolved for supplier return line '{debitLine.Description}'.");
                    }

                    creditAccountId = sourceLine.LineType == 1
                        ? settings.ControlAccountInventoryId
                            ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.")
                        : sourceLine.GlAccountId
                            ?? throw new InvalidOperationException($"No GL account specified for finance PO return line '{sourceLine.Description}'.");
                }

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = creditAccountId,
                    Description = $"Supplier return {debitNote.DebitNoteNumber} - {debitLine.Description}",
                    TransactionType = "Credit",
                    Amount = ToBaseAmount(lineForeignAmount, exchangeRate),
                    Reference = debitNote.DebitNoteNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = lineForeignAmount
                });
            }

            if (invoiceLinked && debitNote.TaxAmount > 0)
            {
                var taxAccountId = settings.ControlAccountTaxId;
                if (taxAccountId == null) throw new InvalidOperationException("Tax Control Account not configured.");

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = taxAccountId.Value,
                    Description = $"Reverse input tax - {debitNote.DebitNoteNumber}",
                    TransactionType = "Credit",
                    Amount = ToBaseAmount(debitNote.TaxAmount, exchangeRate),
                    Reference = debitNote.DebitNoteNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = debitNote.TaxAmount
                });
            }

            var creditBaseTotal = transactions.Where(t => t.TransactionType == "Credit").Sum(t => t.Amount);
            if (creditBaseTotal <= 0)
            {
                throw new InvalidOperationException($"Supplier debit note {debitNote.DebitNoteNumber} has no positive-value lines to post.");
            }

            var controlForeignAmount = invoiceLinked ? debitNote.TotalAmount : debitNote.SubTotal;
            transactions.Insert(0, new CreateAccountTransactionDto
            {
                AccountId = controlAccountId.Value,
                Description = invoiceLinked
                    ? $"Supplier debit note {debitNote.DebitNoteNumber}"
                    : $"Reverse GRV accrual - {debitNote.DebitNoteNumber}",
                TransactionType = "Debit",
                Amount = creditBaseTotal,
                Reference = debitNote.DebitNoteNumber,
                CurrencyCode = currencyCode,
                ExchangeRate = exchangeRate,
                ForeignAmount = controlForeignAmount
            });

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = debitNote.DebitNoteDate,
                Description = $"Supplier Debit Note {debitNote.DebitNoteNumber} - {debitNote.Vendor.PartnerName}",
                Reference = debitNote.DebitNoteNumber,
                SourceModule = "AP",
                SourceDocumentId = debitNote.Id,
                SourceDocumentType = "SupplierDebitNote",
                Transactions = transactions
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            debitNote.JournalEntryId = journalEntry.Id;
            debitNote.Status = SupplierDebitNoteStatus.Posted;
            await _context.SaveChangesAsync(cancellationToken);

            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        [Obsolete("Normal AR invoice posting uses IInvoiceService.PostAsync and IFinancePostingEngine. This legacy method is reserved for migration compatibility only.")]
        public async Task<JournalEntryDto> PostArInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var invoice = await _context.Set<Invoice>()
                .Include(i => i.BusinessPartner)
                .Include(i => i.LineItems.Where(l => !l.IsDeleted))
                .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId && !i.IsDeleted, cancellationToken);

            if (invoice == null) throw new ArgumentException($"AR Invoice {invoiceId} not found.");
            if (!invoice.IsOpeningBalance)
                throw new InvalidOperationException("Legacy AR invoice posting is disabled for normal invoices. Use IInvoiceService.PostAsync.");

            var existingInvoiceJournal = await GetExistingSourceJournalAsync(
                tenantId,
                invoice.JournalEntryId,
                invoice.Id,
                "CustomerInvoice",
                invoice.InvoiceNumber,
                cancellationToken);
            if (existingInvoiceJournal != null)
            {
                invoice.JournalEntryId = existingInvoiceJournal.Id;
                await _context.SaveChangesAsync(cancellationToken);
                return await EnsurePostedAsync(existingInvoiceJournal, cancellationToken);
            }

            var settings = await GetSettingsAsync(cancellationToken);
            var baseCurrencyCode = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync());
            var currencyCode = NormalizeCurrency(invoice.CurrencyCode);
            var exchangeRate = NormalizeExchangeRate(invoice.ExchangeRate);

            var arAccountId = invoice.BusinessPartner?.DefaultArAccountId ?? settings.ControlAccountArId;
            if (arAccountId == null) throw new InvalidOperationException("AR Control Account not configured.");

            var transactions = new List<CreateAccountTransactionDto>();

            if (invoice.IsOpeningBalance)
            {
                var migrationClearingAccountId = settings.MigrationClearingAccountId;
                if (migrationClearingAccountId == null) throw new InvalidOperationException("Migration Clearing Account is not configured in Finance Settings.");

                var openingBaseAmount = invoice.BaseCurrencyAmount > 0m
                    ? invoice.BaseCurrencyAmount
                    : ToBaseAmount(invoice.TotalAmount, exchangeRate);
                if (openingBaseAmount <= 0m)
                {
                    throw new InvalidOperationException($"Customer opening balance invoice {invoice.InvoiceNumber} has no positive AR amount to post.");
                }

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = arAccountId.Value,
                    Description = $"Opening AR balance - {invoice.InvoiceNumber}",
                    TransactionType = "Debit",
                    Amount = openingBaseAmount,
                    Reference = invoice.InvoiceNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = invoice.TotalAmount
                });

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = migrationClearingAccountId.Value,
                    Description = $"Opening AR balance - {invoice.InvoiceNumber}",
                    TransactionType = "Credit",
                    Amount = openingBaseAmount,
                    Reference = invoice.InvoiceNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = invoice.TotalAmount
                });

                var openingJeDto = new CreateJournalEntryDto
                {
                    TransactionDate = invoice.InvoiceDate,
                    JournalType = "Opening Balance",
                    Description = $"Opening AR Balance {invoice.InvoiceNumber} - {invoice.CustomerName}",
                    Reference = invoice.InvoiceNumber,
                    SourceModule = "AR",
                    SourceDocumentId = invoice.Id,
                    SourceDocumentType = "CustomerInvoice",
                    Transactions = transactions
                };

                var openingJournalEntry = await _journalEntryService.CreateJournalEntryAsync(openingJeDto, cancellationToken);
                invoice.JournalEntryId = openingJournalEntry.Id;
                await _context.SaveChangesAsync(cancellationToken);

                return await _journalEntryService.PostJournalEntryAsync(openingJournalEntry.Id, cancellationToken);
            }

            foreach (var line in invoice.LineItems.Where(l => !l.IsDeleted))
            {
                var lineForeignAmount = line.LineTotal;
                if (lineForeignAmount <= 0) continue;

                var revenueAccId = line.GLAccountId;
                if (revenueAccId == null)
                {
                    throw new InvalidOperationException($"No revenue account specified for AR line '{line.Description}'.");
                }

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = revenueAccId.Value,
                    Description = $"Revenue - {invoice.InvoiceNumber} - {line.Description}",
                    TransactionType = "Credit",
                    Amount = ToBaseAmount(lineForeignAmount, exchangeRate),
                    Reference = invoice.InvoiceNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = lineForeignAmount
                });

                var cogsAccId = settings.ControlAccountCOGSId;
                var inventoryAccId = settings.ControlAccountInventoryId;
                if (line.LineItemType == LineItemType.Inventory && cogsAccId != null && inventoryAccId != null && line.CostTotal.HasValue && line.CostTotal > 0)
                {
                    var costAmount = decimal.Round(line.CostTotal.Value, 2, MidpointRounding.AwayFromZero);
                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = cogsAccId.Value,
                        Description = $"COGS - {invoice.InvoiceNumber} - {line.Description}",
                        TransactionType = "Debit",
                        Amount = costAmount,
                        Reference = invoice.InvoiceNumber,
                        CurrencyCode = baseCurrencyCode,
                        ExchangeRate = 1m,
                        ForeignAmount = costAmount
                    });

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = inventoryAccId.Value,
                        Description = $"Inventory Issue - {invoice.InvoiceNumber} - {line.Description}",
                        TransactionType = "Credit",
                        Amount = costAmount,
                        Reference = invoice.InvoiceNumber,
                        CurrencyCode = baseCurrencyCode,
                        ExchangeRate = 1m,
                        ForeignAmount = costAmount
                    });
                }
            }

            var documentDiscountAmount = invoice.LineItems.Where(l => !l.IsDeleted).Sum(l => l.DiscountAmount) + invoice.DiscountAmount;
            if (documentDiscountAmount > 0)
            {
                var discountAllowedAccountId = settings.DiscountAllowedAccountId;
                if (discountAllowedAccountId == null) throw new InvalidOperationException("Sales Discounts Allowed Account not configured in Finance Settings.");

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = discountAllowedAccountId.Value,
                    Description = $"Sales Discount - {invoice.InvoiceNumber}",
                    TransactionType = "Debit",
                    Amount = ToBaseAmount(documentDiscountAmount, exchangeRate),
                    Reference = invoice.InvoiceNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = documentDiscountAmount
                });
            }

            if (invoice.TaxAmount > 0)
            {
                var taxAccId = settings.ControlAccountTaxId;
                if (taxAccId == null) throw new InvalidOperationException("Tax Control Account not configured.");

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = taxAccId.Value,
                    Description = $"Output Tax - {invoice.InvoiceNumber}",
                    TransactionType = "Credit",
                    Amount = ToBaseAmount(invoice.TaxAmount, exchangeRate),
                    Reference = invoice.InvoiceNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = invoice.TaxAmount
                });
            }

            var debitBaseTotal = transactions.Where(t => t.TransactionType == "Debit").Sum(t => t.Amount);
            var creditBaseTotal = transactions.Where(t => t.TransactionType == "Credit").Sum(t => t.Amount);
            var arBaseAmount = creditBaseTotal - debitBaseTotal;
            if (arBaseAmount <= 0)
            {
                throw new InvalidOperationException($"Customer invoice {invoice.InvoiceNumber} has no positive AR amount to post.");
            }

            transactions.Insert(0, new CreateAccountTransactionDto
            {
                AccountId = arAccountId.Value,
                Description = $"AR Invoice {invoice.InvoiceNumber}",
                TransactionType = "Debit",
                Amount = arBaseAmount,
                Reference = invoice.InvoiceNumber,
                CurrencyCode = currencyCode,
                ExchangeRate = exchangeRate,
                ForeignAmount = invoice.TotalAmount
            });

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = invoice.InvoiceDate,
                Description = $"Customer Invoice {invoice.InvoiceNumber} - {invoice.CustomerName}",
                Reference = invoice.InvoiceNumber,
                SourceModule = "AR",
                SourceDocumentId = invoice.Id,
                SourceDocumentType = "CustomerInvoice",
                Transactions = transactions
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            invoice.JournalEntryId = journalEntry.Id;
            await _context.SaveChangesAsync(cancellationToken);

            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        private static bool IsLegacyArPaymentPostingDisabled() => true;

        public async Task<JournalEntryDto> PostArPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
        {
            if (IsLegacyArPaymentPostingDisabled())
                throw new InvalidOperationException("Legacy AR payment and credit-note posting is disabled. Use IPaymentService with IFinancePostingEngine for AR receipts and customer credit notes.");

            var tenantId = TenantId;
            var payment = await _context.Set<CustomerPayment>()
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == paymentId && !p.IsDeleted, cancellationToken);

            if (payment == null) throw new ArgumentException($"AR Payment {paymentId} not found.");
            if (!payment.IsCreditNote)
                throw new InvalidOperationException("Legacy AR receipt posting is disabled. Use IPaymentService.PostAsync and IFinancePostingEngine for customer receipts.");

            var customer = await _context.Set<BusinessPartner>()
                .FirstOrDefaultAsync(p =>
                    p.TenantId == payment.TenantId &&
                    p.Id == payment.CustomerId &&
                    !p.IsDeleted &&
                    (p.PartnerType == "Customer" || p.PartnerType == "Both"),
                    cancellationToken)
                ?? throw new InvalidOperationException($"Customer business partner {payment.CustomerId} not found for AR payment.");

            var sourceDocumentType = payment.IsCreditNote ? "CustomerCreditNote" : "CustomerPayment";
            var existingPaymentJournal = await GetExistingSourceJournalAsync(
                tenantId,
                payment.JournalEntryId,
                payment.Id,
                sourceDocumentType,
                payment.PaymentNumber,
                cancellationToken);
            if (existingPaymentJournal != null)
            {
                payment.JournalEntryId = existingPaymentJournal.Id;
                await _context.SaveChangesAsync(cancellationToken);
                return await EnsurePostedAsync(existingPaymentJournal, cancellationToken);
            }

            var settings = await GetSettingsAsync(cancellationToken);

            var arAccountId = customer.DefaultArAccountId ?? settings.ControlAccountArId;
            if (arAccountId == null) throw new InvalidOperationException("AR Control Account not configured.");

            var currencyCode = NormalizeCurrency(payment.CurrencyCode);
            var exchangeRate = NormalizeExchangeRate(payment.ExchangeRate);
            var discountAllowed = payment.Allocations?
                .Where(a => !a.IsReversal)
                .Sum(a => a.DiscountAmount) ?? 0m;
            var arSettlementAmount = payment.TotalAmount + discountAllowed;
            var transactions = new List<CreateAccountTransactionDto>();

            if (payment.IsCreditNote)
            {
                var discountAllowedAccountId = settings.DiscountAllowedAccountId;
                if (discountAllowedAccountId == null) throw new InvalidOperationException("Sales Discounts Allowed Account not configured in Finance Settings.");

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = discountAllowedAccountId.Value,
                    Description = $"Customer Credit Note {payment.PaymentNumber}",
                    TransactionType = "Debit",
                    Amount = ToBaseAmount(payment.TotalAmount, exchangeRate),
                    Reference = payment.PaymentNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = payment.TotalAmount
                });

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = arAccountId.Value,
                    Description = $"Customer Credit Note {payment.PaymentNumber}",
                    TransactionType = "Credit",
                    Amount = ToBaseAmount(payment.TotalAmount, exchangeRate),
                    Reference = payment.PaymentNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = payment.TotalAmount
                });
            }
            else
            {
                var bankAccountId = payment.BankAccountId ?? settings.DefaultBankAccountId;
                if (bankAccountId == null) throw new InvalidOperationException("Bank Account not configured for AR payment.");

                var bankAccount = await _context.Set<BankAccount>()
                    .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == bankAccountId.Value && !a.IsDeleted, cancellationToken);
                if (bankAccount == null) throw new InvalidOperationException("Bank Account not found for AR payment.");
                if (bankAccount.GLAccountId == null) throw new InvalidOperationException($"Bank Account '{bankAccount.AccountName}' is not linked to a GL account.");
                payment.BankAccountId ??= bankAccount.Id;

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = bankAccount.GLAccountId.Value,
                    Description = $"Customer Payment {payment.PaymentNumber}",
                    TransactionType = "Debit",
                    Amount = ToBaseAmount(payment.TotalAmount, exchangeRate),
                    Reference = payment.PaymentNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = payment.TotalAmount
                });

                if (discountAllowed > 0)
                {
                    var discountAllowedAccountId = settings.DiscountAllowedAccountId;
                    if (discountAllowedAccountId == null) throw new InvalidOperationException("Sales Discounts Allowed Account not configured in Finance Settings.");

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = discountAllowedAccountId.Value,
                        Description = $"Sales Discount Allowed - {payment.PaymentNumber}",
                        TransactionType = "Debit",
                        Amount = ToBaseAmount(discountAllowed, exchangeRate),
                        Reference = payment.PaymentNumber,
                        CurrencyCode = currencyCode,
                        ExchangeRate = exchangeRate,
                        ForeignAmount = discountAllowed
                    });
                }

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = arAccountId.Value,
                    Description = $"Customer Payment {payment.PaymentNumber}",
                    TransactionType = "Credit",
                    Amount = ToBaseAmount(arSettlementAmount, exchangeRate),
                    Reference = payment.PaymentNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = arSettlementAmount
                });
            }

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = payment.PaymentDate,
                Description = payment.IsCreditNote
                    ? $"Customer Credit Note {payment.PaymentNumber} - {customer.PartnerName}"
                    : $"Customer Payment {payment.PaymentNumber} - {customer.PartnerName}",
                Reference = payment.PaymentNumber,
                SourceModule = "AR",
                SourceDocumentId = payment.Id,
                SourceDocumentType = sourceDocumentType,
                Transactions = transactions
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            payment.JournalEntryId = journalEntry.Id;
            await _context.SaveChangesAsync(cancellationToken);

            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        public async Task<JournalEntryDto> PostArPaymentDiscountAdjustmentAsync(Guid allocationId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Legacy AR receipt discount adjustment posting is disabled. Receipt discounts must be included in the central AR receipt posting request.");

#pragma warning disable CS0162
            var tenantId = TenantId;
            var allocation = await _context.Set<PaymentAllocation>()
                .Include(a => a.CustomerPayment)
                .Include(a => a.Invoice)
                    .ThenInclude(i => i.BusinessPartner)
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == allocationId && !a.IsDeleted, cancellationToken);

            if (allocation == null) throw new ArgumentException($"Customer payment allocation {allocationId} not found.");
            if (allocation.IsReversal || allocation.DiscountAmount <= 0)
                throw new InvalidOperationException("Only positive, non-reversal AR payment discount allocations can be posted.");

            var existingJournal = await GetExistingSourceJournalAsync(
                tenantId,
                null,
                allocation.Id,
                "CustomerPaymentDiscountAdjustment",
                allocation.CustomerPayment.PaymentNumber,
                cancellationToken);
            if (existingJournal != null)
            {
                return await EnsurePostedAsync(existingJournal, cancellationToken);
            }

            var settings = await GetSettingsAsync(cancellationToken);
            var arAccountId = allocation.Invoice.BusinessPartner?.DefaultArAccountId ?? settings.ControlAccountArId;
            if (arAccountId == null) throw new InvalidOperationException("AR Control Account not configured.");

            var discountAllowedAccountId = settings.DiscountAllowedAccountId;
            if (discountAllowedAccountId == null) throw new InvalidOperationException("Sales Discounts Allowed Account not configured in Finance Settings.");

            var currencyCode = NormalizeCurrency(allocation.CustomerPayment.CurrencyCode);
            var exchangeRate = NormalizeExchangeRate(allocation.CustomerPayment.ExchangeRate);
            var baseAmount = ToBaseAmount(allocation.DiscountAmount, exchangeRate);

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = allocation.AllocationDate,
                Description = $"Sales Discount Allowed - {allocation.CustomerPayment.PaymentNumber}",
                Reference = allocation.CustomerPayment.PaymentNumber,
                SourceModule = "AR",
                SourceDocumentId = allocation.Id,
                SourceDocumentType = "CustomerPaymentDiscountAdjustment",
                Transactions = new List<CreateAccountTransactionDto>
                {
                    new CreateAccountTransactionDto
                    {
                        AccountId = discountAllowedAccountId.Value,
                        Description = $"Sales Discount Allowed - {allocation.Invoice.InvoiceNumber}",
                        TransactionType = "Debit",
                        Amount = baseAmount,
                        Reference = allocation.CustomerPayment.PaymentNumber,
                        CurrencyCode = currencyCode,
                        ExchangeRate = exchangeRate,
                        ForeignAmount = allocation.DiscountAmount
                    },
                    new CreateAccountTransactionDto
                    {
                        AccountId = arAccountId.Value,
                        Description = $"AR discount settlement - {allocation.Invoice.InvoiceNumber}",
                        TransactionType = "Credit",
                        Amount = baseAmount,
                        Reference = allocation.CustomerPayment.PaymentNumber,
                        CurrencyCode = currencyCode,
                        ExchangeRate = exchangeRate,
                        ForeignAmount = allocation.DiscountAmount
                    }
                }
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
#pragma warning restore CS0162
        }

        private static bool IsLegacySalesCreditNotePostingDisabled() => true;

        public async Task<JournalEntryDto> PostSalesCreditNoteAsync(Guid creditNoteId, CancellationToken cancellationToken = default)
        {
            if (IsLegacySalesCreditNotePostingDisabled())
                throw new InvalidOperationException("Legacy sales credit note posting is disabled. Use IReturnOrderService.PostCreditNoteAsync and IFinancePostingEngine.");

            var tenantId = TenantId;
            var creditNote = await _context.Set<CreditNote>()
                // This disabled legacy path must still compile against the canonical AR counterparty.
                .Include(c => c.BusinessPartner)
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == creditNoteId && !c.IsDeleted, cancellationToken);

            if (creditNote == null) throw new ArgumentException($"Sales Credit Note {creditNoteId} not found.");

            var existingJournal = await GetExistingSourceJournalAsync(
                tenantId,
                null,
                creditNote.Id,
                "SalesCreditNote",
                creditNote.DocumentNumber,
                cancellationToken);
            if (existingJournal != null)
            {
                return await EnsurePostedAsync(existingJournal, cancellationToken);
            }

            var settings = await GetSettingsAsync(cancellationToken);
            var arAccountId = creditNote.BusinessPartner?.DefaultArAccountId ?? settings.ControlAccountArId;
            if (arAccountId == null) throw new InvalidOperationException("AR Control Account not configured.");

            var salesReturnsAccountId = settings.DiscountAllowedAccountId;
            if (salesReturnsAccountId == null) throw new InvalidOperationException("Sales Discounts Allowed Account not configured in Finance Settings.");

            var currencyCode = NormalizeCurrency(creditNote.Currency);
            var exchangeRate = NormalizeExchangeRate(creditNote.ExchangeRate);
            var lineSubtotal = creditNote.Lines.Where(l => !l.IsDeleted).Sum(l => l.LineTotal);
            var taxAmount = creditNote.TaxAmount > 0
                ? creditNote.TaxAmount
                : creditNote.Lines.Where(l => !l.IsDeleted).Sum(l => l.TaxAmount);
            var totalAmount = creditNote.TotalAmount > 0 ? creditNote.TotalAmount : lineSubtotal + taxAmount;

            var transactions = new List<CreateAccountTransactionDto>
            {
                new CreateAccountTransactionDto
                {
                    AccountId = salesReturnsAccountId.Value,
                    Description = $"Sales credit note {creditNote.DocumentNumber}",
                    TransactionType = "Debit",
                    Amount = ToBaseAmount(lineSubtotal, exchangeRate),
                    Reference = creditNote.DocumentNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = lineSubtotal
                }
            };

            if (taxAmount > 0)
            {
                var taxAccId = settings.ControlAccountTaxId;
                if (taxAccId == null) throw new InvalidOperationException("Tax Control Account not configured.");

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = taxAccId.Value,
                    Description = $"Reverse output tax - {creditNote.DocumentNumber}",
                    TransactionType = "Debit",
                    Amount = ToBaseAmount(taxAmount, exchangeRate),
                    Reference = creditNote.DocumentNumber,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = taxAmount
                });
            }

            transactions.Add(new CreateAccountTransactionDto
            {
                AccountId = arAccountId.Value,
                Description = $"Sales credit note {creditNote.DocumentNumber}",
                TransactionType = "Credit",
                Amount = transactions.Where(t => t.TransactionType == "Debit").Sum(t => t.Amount),
                Reference = creditNote.DocumentNumber,
                CurrencyCode = currencyCode,
                ExchangeRate = exchangeRate,
                ForeignAmount = totalAmount
            });

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = creditNote.DocumentDate,
                Description = $"Sales Credit Note {creditNote.DocumentNumber} - {creditNote.BusinessPartner?.PartnerName ?? "Customer"}",
                Reference = creditNote.DocumentNumber,
                SourceModule = "AR",
                SourceDocumentId = creditNote.Id,
                SourceDocumentType = "SalesCreditNote",
                Transactions = transactions
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        private async Task<JournalEntry?> GetExistingSourceJournalAsync(
            Guid tenantId,
            Guid? linkedJournalEntryId,
            Guid sourceDocumentId,
            string sourceDocumentType,
            string documentNumber,
            CancellationToken cancellationToken)
        {
            if (linkedJournalEntryId.HasValue)
            {
                var linkedJournal = await _context.JournalEntries
                    .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == linkedJournalEntryId.Value && !j.IsDeleted, cancellationToken);

                if (linkedJournal == null)
                {
                    throw new InvalidOperationException($"{sourceDocumentType} {documentNumber} references missing journal entry {linkedJournalEntryId.Value}.");
                }

                return linkedJournal;
            }

            return await _context.JournalEntries
                .FirstOrDefaultAsync(j =>
                    j.TenantId == tenantId &&
                    j.SourceDocumentId == sourceDocumentId &&
                    j.SourceDocumentType == sourceDocumentType &&
                    !j.IsDeleted,
                    cancellationToken);
        }

        private async Task<JournalEntryDto> EnsurePostedAsync(JournalEntry journalEntry, CancellationToken cancellationToken)
        {
            if (journalEntry.PostingStatus == "Posted")
            {
                return await _journalEntryService.GetJournalEntryByIdAsync(journalEntry.Id, cancellationToken)
                    ?? throw new InvalidOperationException($"Journal entry {journalEntry.Id} could not be loaded.");
            }

            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        private static decimal CalculateSupplierDebitNoteLineForeignAmount(SupplierDebitNoteLineItem line)
        {
            var grossAmount = line.Quantity * line.UnitPrice;
            var discountAmount = line.DiscountAmount > 0m
                ? line.DiscountAmount
                : grossAmount * (line.DiscountPercentage / 100m);
            var netAmount = grossAmount - discountAmount;

            if (line.LineTotal > 0m)
            {
                var lineTotalExcludingTax = line.LineTotal >= line.TaxAmount
                    ? line.LineTotal - line.TaxAmount
                    : line.LineTotal;
                if (lineTotalExcludingTax > 0m)
                {
                    netAmount = lineTotalExcludingTax;
                }
            }

            return decimal.Round(netAmount, 2, MidpointRounding.AwayFromZero);
        }
    }
}
