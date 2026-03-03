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
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Enums;

namespace ErpSystem.Api.Services.Finance.GL
{
    public class SubledgerPostingService : ISubledgerPostingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IJournalEntryService _journalEntryService;
        private readonly ILogger<SubledgerPostingService> _logger;

        public SubledgerPostingService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IJournalEntryService journalEntryService,
            ILogger<SubledgerPostingService> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _journalEntryService = journalEntryService;
            _logger = logger;
        }

        private async Task<FinanceSettings> GetSettingsAsync(CancellationToken cancellationToken)
        {
            var tenantId = _currentUserService.TenantId;
            var settings = await _context.Set<FinanceSettings>()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

            if (settings == null)
            {
                throw new InvalidOperationException("Finance settings not configured for this tenant.");
            }

            return settings;
        }

        public async Task<JournalEntryDto> PostApInvoiceAsync(Guid vendorInvoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _context.Set<VendorInvoice>()
                .Include(i => i.LineItems)
                .Include(i => i.Supplier) // BusinessPartner
                .FirstOrDefaultAsync(i => i.Id == vendorInvoiceId, cancellationToken);

            if (invoice == null) throw new ArgumentException($"Vendor Invoice {vendorInvoiceId} not found.");

            var settings = await GetSettingsAsync(cancellationToken);
            
            // Resolve AP Control Account
            var apAccountId = invoice.Supplier.DefaultApAccountId ?? settings.ControlAccountApId;
            if (apAccountId == null) throw new InvalidOperationException("AP Control Account not configured.");

            var transactions = new List<CreateAccountTransactionDto>();

            // AP Control Credit Line
            transactions.Add(new CreateAccountTransactionDto
            {
                AccountId = apAccountId.Value,
                Description = $"AP Invoice {invoice.InvoiceNumber}",
                TransactionType = "Credit",
                Amount = invoice.TotalAmount,
                CurrencyCode = invoice.CurrencyCode,
                ExchangeRate = invoice.ExchangeRate,
                ForeignAmount = invoice.TotalAmount
            });

            // Process Debits for Lines
            foreach (var line in invoice.LineItems)
            {
                var lineAmount = line.LineTotal;
                if (lineAmount <= 0) continue;

                if (line.LineItemType == "Inventory")
                {
                    var inventoryAccId = settings.ControlAccountInventoryId;
                    if (inventoryAccId == null) throw new InvalidOperationException("Inventory Control Account not configured in Finance Settings.");

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = inventoryAccId.Value,
                        Description = $"Inventory Receipt - {invoice.InvoiceNumber} - {line.Description}",
                        TransactionType = "Debit",
                        Amount = lineAmount,
                        CurrencyCode = invoice.CurrencyCode,
                        ExchangeRate = invoice.ExchangeRate,
                        ForeignAmount = lineAmount
                    });

                    // Note: Actual Inventory movement (PurchaseReceipt) is created by the InventoryService or AP Service before calling this,
                    // or handles it alongside this. But they belong to separate modules.
                }
                else // Expense
                {
                    var expenseAccId = line.GLAccountId ?? invoice.Supplier.DefaultExpenseAccountId;
                    if (expenseAccId == null) throw new InvalidOperationException($"No Expense Account specified for AP line '{line.Description}'.");

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = expenseAccId.Value,
                        Description = $"Expense - {invoice.InvoiceNumber} - {line.Description}",
                        TransactionType = "Debit",
                        Amount = lineAmount,
                        CurrencyCode = invoice.CurrencyCode,
                        ExchangeRate = invoice.ExchangeRate,
                        ForeignAmount = lineAmount
                    });
                }
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
                    Amount = invoice.TaxAmount,
                    CurrencyCode = invoice.CurrencyCode,
                    ExchangeRate = invoice.ExchangeRate,
                    ForeignAmount = invoice.TaxAmount
                });
            }

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = invoice.InvoiceDate,
                Description = $"Vendor Invoice {invoice.InvoiceNumber} - {invoice.SupplierName}",
                Reference = invoice.InvoiceNumber,
                SourceModule = "AP",
                Transactions = transactions
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            
            // Link JE to invoice
            invoice.JournalEntryId = journalEntry.Id;
            await _context.SaveChangesAsync(cancellationToken);

            // Auto-post the JE if configured, or just leave as Draft. AP invoices usually post immediately.
            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        public async Task<JournalEntryDto> PostApPaymentAsync(Guid vendorPaymentId, CancellationToken cancellationToken = default)
        {
            var payment = await _context.Set<VendorPayment>()
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == vendorPaymentId, cancellationToken);

            if (payment == null) throw new ArgumentException($"Vendor Payment {vendorPaymentId} not found.");

            var settings = await GetSettingsAsync(cancellationToken);
            
            var apAccountId = payment.Supplier.DefaultApAccountId ?? settings.ControlAccountApId;
            if (apAccountId == null) throw new InvalidOperationException("AP Control Account not configured.");

            var bankAccountId = payment.BankAccountId ?? settings.DefaultBankAccountId;
            if (bankAccountId == null) throw new InvalidOperationException("Bank Account not configured for payment.");

            var transactions = new List<CreateAccountTransactionDto>
            {
                new CreateAccountTransactionDto
                {
                    AccountId = apAccountId.Value,
                    Description = $"Vendor Payment {payment.PaymentNumber}",
                    TransactionType = "Debit",
                    Amount = payment.TotalAmount,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = payment.ExchangeRate,
                    ForeignAmount = payment.TotalAmount
                },
                new CreateAccountTransactionDto
                {
                    AccountId = bankAccountId.Value,
                    Description = $"Vendor Payment {payment.PaymentNumber}",
                    TransactionType = "Credit",
                    Amount = payment.TotalAmount,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = payment.ExchangeRate,
                    ForeignAmount = payment.TotalAmount
                }
            };

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = payment.PaymentDate,
                Description = $"Vendor Payment {payment.PaymentNumber} - {payment.Supplier.Name}",
                Reference = payment.PaymentNumber,
                SourceModule = "AP",
                Transactions = transactions
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            
            // Link JE to payment
            payment.JournalEntryId = journalEntry.Id;
            await _context.SaveChangesAsync(cancellationToken);

            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        public async Task<JournalEntryDto> PostArInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _context.Set<Invoice>()
                .Include(i => i.LineItems)
                // BusinessPartner is added in Phase 3, falling back to Customer for now or handling if mapped
                .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

            if (invoice == null) throw new ArgumentException($"AR Invoice {invoiceId} not found.");

            var settings = await GetSettingsAsync(cancellationToken);
            
            var arAccountId = settings.ControlAccountArId; // Or BusinessPartner.DefaultArAccountId
            if (arAccountId == null) throw new InvalidOperationException("AR Control Account not configured.");

            var transactions = new List<CreateAccountTransactionDto>
            {
                new CreateAccountTransactionDto
                {
                    AccountId = arAccountId.Value,
                    Description = $"AR Invoice {invoice.InvoiceNumber}",
                    TransactionType = "Debit",
                    Amount = invoice.TotalAmount,
                    CurrencyCode = "USD", // Example
                    ExchangeRate = 1m,
                    ForeignAmount = invoice.TotalAmount
                }
            };

            foreach (var line in invoice.LineItems)
            {
                var lineAmount = line.LineTotal;
                if (lineAmount <= 0) continue;

                if (line.LineItemType == LineItemType.Inventory)
                {
                    // Credit Revenue
                    var revenueAccId = line.GLAccountId ?? settings.ControlAccountInventoryId; // specific logic needed
                    if (revenueAccId != null)
                    {
                        transactions.Add(new CreateAccountTransactionDto
                        {
                            AccountId = revenueAccId.Value,
                            Description = $"Revenue - {invoice.InvoiceNumber} - {line.Description}",
                            TransactionType = "Credit",
                            Amount = lineAmount,
                            CurrencyCode = "USD",
                            ExchangeRate = 1m,
                            ForeignAmount = lineAmount
                        });
                    }

                    // COGS Journal
                    var cogsAccId = settings.ControlAccountCOGSId;
                    var inventoryAccId = settings.ControlAccountInventoryId;
                    
                    if (cogsAccId != null && inventoryAccId != null && line.CostTotal.HasValue && line.CostTotal > 0)
                    {
                        var costAmount = line.CostTotal.Value;
                        transactions.Add(new CreateAccountTransactionDto
                        {
                            AccountId = cogsAccId.Value,
                            Description = $"COGS - {invoice.InvoiceNumber} - {line.Description}",
                            TransactionType = "Debit",
                            Amount = costAmount,
                            CurrencyCode = "USD",
                            ExchangeRate = 1m,
                            ForeignAmount = costAmount
                        });

                        transactions.Add(new CreateAccountTransactionDto
                        {
                            AccountId = inventoryAccId.Value,
                            Description = $"Inventory Issue - {invoice.InvoiceNumber} - {line.Description}",
                            TransactionType = "Credit",
                            Amount = costAmount,
                            CurrencyCode = "USD",
                            ExchangeRate = 1m,
                            ForeignAmount = costAmount
                        });
                    }
                }
                else // Product / GLAccount
                {
                    var revenueAccId = line.GLAccountId;
                    if (revenueAccId == null) throw new InvalidOperationException($"No Revenue Account specified for AR line '{line.Description}'.");

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = revenueAccId.Value,
                        Description = $"Revenue - {invoice.InvoiceNumber} - {line.Description}",
                        TransactionType = "Credit",
                        Amount = lineAmount,
                        CurrencyCode = "USD",
                        ExchangeRate = 1m,
                        ForeignAmount = lineAmount
                    });
                }
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
                    Amount = invoice.TaxAmount,
                    CurrencyCode = "USD",
                    ExchangeRate = 1m,
                    ForeignAmount = invoice.TaxAmount
                });
            }

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = invoice.InvoiceDate,
                Description = $"Customer Invoice {invoice.InvoiceNumber} - {invoice.CustomerName}",
                Reference = invoice.InvoiceNumber,
                SourceModule = "AR",
                Transactions = transactions
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }

        public async Task<JournalEntryDto> PostArPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
        {
            var payment = await _context.Set<CustomerPayment>()
                .Include(p => p.Customer) // Will be BusinessPartner eventually
                .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

            if (payment == null) throw new ArgumentException($"AR Payment {paymentId} not found.");

            // Prevent duplicate posting
            if (payment.JournalEntryId.HasValue)
            {
                return await _journalEntryService.GetJournalEntryByIdAsync(payment.JournalEntryId.Value, cancellationToken);
            }

            var settings = await GetSettingsAsync(cancellationToken);
            
            var arAccountId = settings.ControlAccountArId; // Fallback or Customer.DefaultArAccountId
            if (arAccountId == null) throw new InvalidOperationException("AR Control Account not configured.");

            var bankAccountId = payment.BankAccountId ?? settings.DefaultBankAccountId;
            if (bankAccountId == null) throw new InvalidOperationException("Bank Account not configured for AR payment.");

            var transactions = new List<CreateAccountTransactionDto>
            {
                new CreateAccountTransactionDto
                {
                    AccountId = bankAccountId.Value,
                    Description = $"Customer Payment {payment.PaymentNumber}",
                    TransactionType = "Debit",
                    Amount = payment.TotalAmount,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = payment.ExchangeRate,
                    ForeignAmount = payment.TotalAmount
                },
                new CreateAccountTransactionDto
                {
                    AccountId = arAccountId.Value,
                    Description = $"Customer Payment {payment.PaymentNumber}",
                    TransactionType = "Credit",
                    Amount = payment.TotalAmount,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = payment.ExchangeRate,
                    ForeignAmount = payment.TotalAmount
                }
            };

            var jeDto = new CreateJournalEntryDto
            {
                TransactionDate = payment.PaymentDate,
                Description = $"Customer Payment {payment.PaymentNumber} from {payment.Customer.CustomerName}",
                Reference = payment.PaymentNumber,
                SourceModule = "AR",
                Transactions = transactions
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);

            // Link JE to payment
            payment.JournalEntryId = journalEntry.Id;
            await _context.SaveChangesAsync(cancellationToken);

            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }
    }
}
