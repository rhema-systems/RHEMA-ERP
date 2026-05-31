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
using ErpSystem.Core.Constants;

namespace ErpSystem.Api.Services.Finance.GL
{
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

            // Load all tax calculations recorded for this Supplier Invoice
            var taxCalculations = await _repository.Query<TaxCalculation>()
                .Include(tc => tc.Tax)
                .Where(tc => tc.DocumentId == vendorInvoiceId && !tc.IsReversed)
                .ToListAsync(cancellationToken);

            var whtTaxCalculation = taxCalculations.FirstOrDefault(tc => tc.Tax.Category == TaxCategory.Withholding);
            Guid? whtAccountId = null;
            if (whtTaxCalculation?.Tax != null)
            {
                whtAccountId = whtTaxCalculation.Tax.TaxPayableAccountId; // WHT is a payable liability
            }
            if (whtAccountId == null && invoice.WithholdingTaxAmount > 0)
            {
                var defaultWhtTax = await _repository.Query<Tax>()
                    .FirstOrDefaultAsync(t => t.Category == TaxCategory.Withholding && t.IsActive && !t.IsDeleted, cancellationToken);
                whtAccountId = defaultWhtTax?.TaxPayableAccountId ?? settings.ControlAccountTaxId;
            }

            var apCreditForeign = invoice.TotalAmount;
            if (invoice.WithholdingTaxAmount > 0)
            {
                apCreditForeign = invoice.TotalAmount - invoice.WithholdingTaxAmount;

                if (whtAccountId != null)
                {
                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = whtAccountId.Value,
                        Description = $"Withholding Tax - {invoice.InvoiceNumber}",
                        TransactionType = "Credit",
                        Amount = invoice.WithholdingTaxAmount * invoice.ExchangeRate,
                        CurrencyCode = invoice.CurrencyCode,
                        ExchangeRate = invoice.ExchangeRate,
                        ForeignAmount = invoice.WithholdingTaxAmount
                    });
                }
            }

            // AP Control Credit Line
            transactions.Add(new CreateAccountTransactionDto
            {
                AccountId = apAccountId.Value,
                Description = $"AP Invoice {invoice.InvoiceNumber}",
                TransactionType = "Credit",
                Amount = apCreditForeign * invoice.ExchangeRate,
                CurrencyCode = invoice.CurrencyCode,
                ExchangeRate = invoice.ExchangeRate,
                ForeignAmount = apCreditForeign
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

            // Handle Granular Tax Postings (VAT & Levies)
            var standardTaxes = taxCalculations.Where(tc => tc.Tax.Category != TaxCategory.Withholding).ToList();
            if (standardTaxes.Any())
            {
                foreach (var tc in standardTaxes)
                {
                    var tcAccId = tc.Tax.TaxReceivableAccountId ?? settings.ControlAccountTaxId;
                    if (tcAccId == null) throw new InvalidOperationException($"Tax account not configured for component '{tc.Tax.Name}'.");

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = tcAccId.Value,
                        Description = $"{tc.Tax.Name} - {invoice.InvoiceNumber}",
                        TransactionType = "Debit",
                        Amount = tc.TaxAmount * invoice.ExchangeRate,
                        CurrencyCode = invoice.CurrencyCode,
                        ExchangeRate = invoice.ExchangeRate,
                        ForeignAmount = tc.TaxAmount
                    });
                }
            }
            else if (invoice.TaxAmount > 0)
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

        public async Task<List<JournalEntryDto>> PostSupplierDebitNoteAsync(Guid debitNoteId, CancellationToken cancellationToken = default)
        {
            var debitNote = await _repository.Query<SupplierDebitNote>()
                .Include(dn => dn.LineItems)
                .Include(dn => dn.Vendor)
                .FirstOrDefaultAsync(dn => dn.Id == debitNoteId, cancellationToken);

            if (debitNote == null) throw new ArgumentException($"Supplier Debit Note {debitNoteId} not found.");

            var settings = await GetSettingsAsync(cancellationToken);
            
            // Resolve AP Control Account
            var apAccountId = debitNote.Vendor.DefaultApAccountId ?? settings.ControlAccountApId;
            if (apAccountId == null) throw new InvalidOperationException("AP Control Account not configured.");

            var transactions = new List<CreateAccountTransactionDto>();

            // Debit AP Control (reverses vendor liability)
            transactions.Add(new CreateAccountTransactionDto
            {
                AccountId = apAccountId.Value,
                Description = $"Supplier Debit Note {debitNote.DebitNoteNumber}",
                TransactionType = "Debit",
                Amount = debitNote.TotalAmount * debitNote.ExchangeRate,
                CurrencyCode = debitNote.CurrencyCode,
                ExchangeRate = debitNote.ExchangeRate,
                ForeignAmount = debitNote.TotalAmount
            });

            // Credit standard tax proportionally if original invoice is present
            if (debitNote.OriginalVendorInvoiceId.HasValue && debitNote.TaxAmount > 0)
            {
                var originalTaxCalcs = await _repository.Query<TaxCalculation>()
                    .Include(tc => tc.Tax)
                    .Where(tc => tc.DocumentId == debitNote.OriginalVendorInvoiceId.Value)
                    .ToListAsync(cancellationToken);

                var originalInvoice = await _repository.Query<VendorInvoice>()
                    .FirstOrDefaultAsync(i => i.Id == debitNote.OriginalVendorInvoiceId.Value, cancellationToken);

                var standardTaxes = originalTaxCalcs.Where(tc => tc.Tax.Category != TaxCategory.Withholding).ToList();
                if (standardTaxes.Any() && originalInvoice != null && originalInvoice.SubTotal > 0)
                {
                    var ratio = debitNote.SubTotal / originalInvoice.SubTotal;
                    foreach (var tc in standardTaxes)
                    {
                        var componentReversedAmount = tc.TaxAmount * ratio;
                        if (componentReversedAmount <= 0) continue;

                        var taxAccId = tc.Tax.TaxReceivableAccountId ?? settings.ControlAccountTaxId;
                        if (taxAccId == null) throw new InvalidOperationException($"Tax account not configured for component '{tc.Tax.Name}'.");

                        transactions.Add(new CreateAccountTransactionDto
                        {
                            AccountId = taxAccId.Value,
                            Description = $"Reversal {tc.Tax.Name} - Debit Note {debitNote.DebitNoteNumber}",
                            TransactionType = "Credit",
                            Amount = componentReversedAmount * debitNote.ExchangeRate,
                            CurrencyCode = debitNote.CurrencyCode,
                            ExchangeRate = debitNote.ExchangeRate,
                            ForeignAmount = componentReversedAmount
                        });
                    }
                }
                else
                {
                    var taxAccId = settings.ControlAccountTaxId;
                    if (taxAccId == null) throw new InvalidOperationException("Tax Control Account not configured.");

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = taxAccId.Value,
                        Description = $"Reversal Input Tax - Debit Note {debitNote.DebitNoteNumber}",
                        TransactionType = "Credit",
                        Amount = debitNote.TaxAmount * debitNote.ExchangeRate,
                        CurrencyCode = debitNote.CurrencyCode,
                        ExchangeRate = debitNote.ExchangeRate,
                        ForeignAmount = debitNote.TaxAmount
                    });
                }
            }
            else if (debitNote.TaxAmount > 0)
            {
                var taxAccId = settings.ControlAccountTaxId;
                if (taxAccId == null) throw new InvalidOperationException("Tax Control Account not configured.");

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = taxAccId.Value,
                    Description = $"Reversal Input Tax - Debit Note {debitNote.DebitNoteNumber}",
                    TransactionType = "Credit",
                    Amount = debitNote.TaxAmount * debitNote.ExchangeRate,
                    CurrencyCode = debitNote.CurrencyCode,
                    ExchangeRate = debitNote.ExchangeRate,
                    ForeignAmount = debitNote.TaxAmount
                });
            }

            // Credit Expense/Inventory clearing for lines
            foreach (var line in debitNote.LineItems)
            {
                var lineAmount = line.LineTotal;
                if (lineAmount <= 0) continue;

                Guid? originalGLAccountId = null;
                bool isInventory = false;

                if (debitNote.SupplierReturnId.HasValue)
                {
                    var returnLine = await _repository.Query<SupplierReturnLineItem>()
                        .FirstOrDefaultAsync(rl => rl.SupplierReturnId == debitNote.SupplierReturnId.Value && rl.Description == line.Description, cancellationToken);
                    
                    if (returnLine?.OriginalVendorInvoiceLineItemId != null)
                    {
                        var originalInvoiceLine = await _repository.Query<VendorInvoiceLineItem>()
                            .FirstOrDefaultAsync(il => il.Id == returnLine.OriginalVendorInvoiceLineItemId.Value, cancellationToken);

                        if (originalInvoiceLine != null)
                        {
                            isInventory = originalInvoiceLine.LineItemType == "Inventory";
                            originalGLAccountId = originalInvoiceLine.GLAccountId;
                        }
                    }
                }

                if (isInventory)
                {
                    var inventoryAccId = settings.ControlAccountInventoryId;
                    if (inventoryAccId == null) throw new InvalidOperationException("Inventory Control Account not configured in Finance Settings.");

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = inventoryAccId.Value,
                        Description = $"Reversal Inventory Receipt - Debit Note {debitNote.DebitNoteNumber} - {line.Description}",
                        TransactionType = "Credit",
                        Amount = lineAmount * debitNote.ExchangeRate,
                        CurrencyCode = debitNote.CurrencyCode,
                        ExchangeRate = debitNote.ExchangeRate,
                        ForeignAmount = lineAmount
                    });
                }
                else
                {
                    var expenseAccId = originalGLAccountId ?? debitNote.Vendor.DefaultExpenseAccountId;
                    if (expenseAccId == null) throw new InvalidOperationException($"No Expense Account specified for line '{line.Description}'.");

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = expenseAccId.Value,
                        Description = $"Reversal Expense - Debit Note {debitNote.DebitNoteNumber} - {line.Description}",
                        TransactionType = "Credit",
                        Amount = lineAmount * debitNote.ExchangeRate,
                        CurrencyCode = debitNote.CurrencyCode,
                        ExchangeRate = debitNote.ExchangeRate,
                        ForeignAmount = lineAmount
                    });
                }
            }

            var accountIds = transactions.Select(t => t.AccountId).Distinct().ToList();
            var targetBooks = await _bookValidationService.ResolveTargetBooksAsync(settings.SubledgerPostingMode, accountIds, cancellationToken);
            
            var postedEntries = new List<JournalEntryDto>();
            
            foreach (var book in targetBooks)
            {
                await _bookValidationService.ValidateAccountsForBookAsync(accountIds, book, cancellationToken);
                
                var jeDto = new CreateJournalEntryDto
                {
                    TransactionDate = debitNote.DebitNoteDate,
                    Description = $"Supplier Debit Note {debitNote.DebitNoteNumber} - {debitNote.Vendor.PartnerName} [{book}]",
                    Reference = debitNote.DebitNoteNumber,
                    SourceModule = "AP",
                    SourceDocumentId = debitNote.Id,
                    SourceDocumentType = "SupplierDebitNote",
                    BookClassification = book,
                    Transactions = transactions
                };

                var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
                
                if (postedEntries.Count == 0)
                {
                    debitNote.JournalEntryId = journalEntry.Id;
                    await _repository.SaveChangesAsync(cancellationToken);
                }

                var posted = await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
                postedEntries.Add(posted);
            }
            
            return postedEntries;
        }

        public async Task<List<JournalEntryDto>> PostApPaymentAsync(Guid vendorPaymentId, CancellationToken cancellationToken = default)
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
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            var invoiceCurrencyCode = string.IsNullOrWhiteSpace(invoice.CurrencyCode)
                ? baseCurrencyCode
                : invoice.CurrencyCode.Trim().ToUpperInvariant();
            var invoiceExchangeRate = invoice.ExchangeRate <= 0 ? 1m : invoice.ExchangeRate;
            
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
                    CurrencyCode = invoiceCurrencyCode,
                    ExchangeRate = invoiceExchangeRate,
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
                            CurrencyCode = invoiceCurrencyCode,
                            ExchangeRate = invoiceExchangeRate,
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
                            CurrencyCode = invoiceCurrencyCode,
                            ExchangeRate = invoiceExchangeRate,
                            ForeignAmount = costAmount
                        });

                        transactions.Add(new CreateAccountTransactionDto
                        {
                            AccountId = inventoryAccId.Value,
                            Description = $"Inventory Issue - {invoice.InvoiceNumber} - {line.Description}",
                            TransactionType = "Credit",
                            Amount = costAmount,
                            CurrencyCode = invoiceCurrencyCode,
                            ExchangeRate = invoiceExchangeRate,
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
                        CurrencyCode = invoiceCurrencyCode,
                        ExchangeRate = invoiceExchangeRate,
                        ForeignAmount = lineAmount
                    });
                }
            }

            // Load all tax calculations recorded for this Customer Invoice
            var taxCalculations = await _repository.Query<TaxCalculation>()
                .Include(tc => tc.Tax)
                .Where(tc => tc.DocumentId == invoiceId && !tc.IsReversed)
                .ToListAsync(cancellationToken);

            // Handle Granular Tax Postings (VAT & Levies)
            var standardTaxes = taxCalculations.Where(tc => tc.Tax.Category != TaxCategory.Withholding).ToList();
            if (standardTaxes.Any())
            {
                foreach (var tc in standardTaxes)
                {
                    var tcAccId = tc.Tax.TaxPayableAccountId ?? settings.ControlAccountTaxId;
                    if (tcAccId == null) throw new InvalidOperationException($"Tax payable account not configured for component '{tc.Tax.Name}'.");

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = tcAccId.Value,
                        Description = $"{tc.Tax.Name} - {invoice.InvoiceNumber}",
                        TransactionType = "Credit",
                        Amount = tc.TaxAmount * invoiceExchangeRate,
                        CurrencyCode = invoiceCurrencyCode,
                        ExchangeRate = invoiceExchangeRate,
                        ForeignAmount = tc.TaxAmount
                    });
                }
            }
            else if (invoice.TaxAmount > 0)
            {
                var taxAccId = settings.ControlAccountTaxId;
                if (taxAccId == null) throw new InvalidOperationException("Tax Control Account not configured.");
                
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = taxAccId.Value,
                    Description = $"Output Tax - {invoice.InvoiceNumber}",
                    TransactionType = "Credit",
                    Amount = invoice.TaxAmount,
                    CurrencyCode = invoiceCurrencyCode,
                    ExchangeRate = invoiceExchangeRate,
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
                await _bookValidationService.ValidateAccountsForBookAsync(accountIds, book, cancellationToken);

                var jeDto = new CreateJournalEntryDto
                {
                    TransactionDate = payment.PaymentDate,
                    Description = $"Customer Payment {payment.PaymentNumber} from {payment.BusinessPartner.PartnerName} [{book}]",
                    Reference = payment.PaymentNumber,
                    SourceModule = "AR",
                    SourceDocumentId = payment.Id,
                    SourceDocumentType = "CustomerPayment",
                    BookClassification = book,
                    Transactions = transactions
                };

                var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
                
                if (postedEntries.Count == 0)
                {
                    payment.JournalEntryId = journalEntry.Id;
                    await _repository.SaveChangesAsync(cancellationToken);
                }

                var posted = await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
                postedEntries.Add(posted);
            }

            // Reduce customer outstanding balance at the point of receipt posting
            var now = DateTime.UtcNow;
            var userName = _currentUserService.UserName ?? "system";
            payment.BusinessPartner.OutstandingBalance -= payment.TotalAmount;
            payment.BusinessPartner.UpdatedAt = now;
            payment.BusinessPartner.UpdatedBy = userName;

            await _repository.SaveChangesAsync(cancellationToken);
            
            return postedEntries;
        }

        public async Task<List<JournalEntryDto>> ReverseArPaymentAsync(Guid paymentId, string reason, CancellationToken cancellationToken = default)
        {
            var entries = await _journalEntryService.GetJournalEntriesBySourceAsync("AR", paymentId, cancellationToken);
            var reversedEntries = new List<JournalEntryDto>();
            
            var now = DateTime.UtcNow;
            
            foreach (var entry in entries)
            {
                if (entry.PostingStatus != "Voided" && entry.PostingStatus != "Reversed")
                {
                    var reversed = await _journalEntryService.ReverseJournalEntryAsync(entry.Id, reason, now, cancellationToken);
                    reversedEntries.Add(reversed);
                }
            }
            
            return reversedEntries;
        }

        public async Task<List<JournalEntryDto>> PostAllocationFxVarianceAsync(Guid allocationId, CancellationToken cancellationToken = default)
        {
            var allocation = await _repository.Query<PaymentAllocation>()
                .Include(a => a.CustomerPayment).ThenInclude(p => p.BusinessPartner)
                .Include(a => a.Invoice)
                .FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken);

            if (allocation == null) throw new ArgumentException($"Allocation {allocationId} not found.");

            var payment = allocation.CustomerPayment;
            var invoice = allocation.Invoice;

            var paymentExchangeRate = payment.ExchangeRate > 0 ? payment.ExchangeRate : 1m;
            var invoiceExchangeRate = invoice.ExchangeRate > 0 ? invoice.ExchangeRate : 1m;

            var paymentAllocatedBaseValue = allocation.AllocatedAmount * paymentExchangeRate;
            var invoiceBaseAmount = invoice.TotalAmount * invoiceExchangeRate;
            var allocatedForeignAmount = allocation.AllocatedAmount;
            var invoiceTotalForeignAmount = invoice.TotalAmount;
            var variance = 0m;
            
            if (invoiceTotalForeignAmount > 0)
            {
                variance = Math.Round(paymentAllocatedBaseValue - (invoiceBaseAmount * (allocatedForeignAmount / invoiceTotalForeignAmount)), 2);
            }

            if (variance == 0) return new List<JournalEntryDto>();

            var settings = await GetSettingsAsync(cancellationToken);
            var arAccountId = payment.BusinessPartner.DefaultArAccountId ?? settings.ControlAccountArId;
            if (arAccountId == null) throw new InvalidOperationException("AR Control Account not configured.");

            Guid varianceAccountId;
            if (variance > 0)
            {
                if (settings.RealizedFxGainAccountId == null) throw new InvalidOperationException("Realized FX Gain Account is missing in Finance Settings. Cannot post FX variance.");
                varianceAccountId = settings.RealizedFxGainAccountId.Value;
            }
            else
            {
                if (settings.RealizedFxLossAccountId == null) throw new InvalidOperationException("Realized FX Loss Account is missing in Finance Settings. Cannot post FX variance.");
                varianceAccountId = settings.RealizedFxLossAccountId.Value;
            }

            var absVariance = Math.Abs(variance);
            var transactions = new List<CreateAccountTransactionDto>();

            if (variance > 0)
            {
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = arAccountId.Value,
                    Description = $"FX Variance (Gain) on Allocation {invoice.InvoiceNumber}",
                    TransactionType = "Debit",
                    Amount = absVariance,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = 1m,
                    ForeignAmount = 0m
                });
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = varianceAccountId,
                    Description = $"FX Variance (Gain) on Allocation {invoice.InvoiceNumber}",
                    TransactionType = "Credit",
                    Amount = absVariance,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = 1m,
                    ForeignAmount = 0m
                });
            }
            else
            {
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = varianceAccountId,
                    Description = $"FX Variance (Loss) on Allocation {invoice.InvoiceNumber}",
                    TransactionType = "Debit",
                    Amount = absVariance,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = 1m,
                    ForeignAmount = 0m
                });
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = arAccountId.Value,
                    Description = $"FX Variance (Loss) on Allocation {invoice.InvoiceNumber}",
                    TransactionType = "Credit",
                    Amount = absVariance,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = 1m,
                    ForeignAmount = 0m
                });
            }

            var accountIds = transactions.Select(t => t.AccountId).Distinct().ToList();
            var targetBooks = await _bookValidationService.ResolveTargetBooksAsync(settings.SubledgerPostingMode, accountIds, cancellationToken);
            
            var postedEntries = new List<JournalEntryDto>();
            
            foreach (var book in targetBooks)
            {
                await _bookValidationService.ValidateAccountsForBookAsync(accountIds, book, cancellationToken);

                var jeDto = new CreateJournalEntryDto
                {
                    TransactionDate = allocation.AllocationDate,
                    Description = $"FX Variance - Payment {payment.PaymentNumber} / Invoice {invoice.InvoiceNumber} [{book}]",
                    Reference = payment.PaymentNumber,
                    SourceModule = "AR",
                    SourceDocumentId = payment.Id, // Link to payment
                    SourceDocumentType = "CustomerPayment",
                    BookClassification = book,
                    Transactions = transactions
                };

                var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
                var posted = await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
                postedEntries.Add(posted);
            }
            
            return postedEntries;
        }

        public async Task<List<JournalEntryDto>> PostVendorAllocationFxVarianceAsync(Guid allocationId, CancellationToken cancellationToken = default)
        {
            var allocation = await _repository.Query<VendorPaymentAllocation>()
                .Include(a => a.VendorPayment).ThenInclude(p => p.BusinessPartner)
                .Include(a => a.VendorInvoice)
                .FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken);

            if (allocation == null) throw new ArgumentException($"Allocation {allocationId} not found.");

            var payment = allocation.VendorPayment;
            var invoice = allocation.VendorInvoice;

            var paymentExchangeRate = payment.ExchangeRate > 0 ? payment.ExchangeRate : 1m;
            var invoiceExchangeRate = invoice.ExchangeRate > 0 ? invoice.ExchangeRate : 1m;

            var paymentAllocatedBaseValue = allocation.AllocatedAmount * paymentExchangeRate;
            var invoiceBaseAmount = invoice.TotalAmount * invoiceExchangeRate;
            var allocatedForeignAmount = allocation.AllocatedAmount;
            var invoiceTotalForeignAmount = invoice.TotalAmount;
            var variance = 0m;
            
            if (invoiceTotalForeignAmount > 0)
            {
                variance = Math.Round(paymentAllocatedBaseValue - (invoiceBaseAmount * (allocatedForeignAmount / invoiceTotalForeignAmount)), 2);
            }

            if (variance == 0) return new List<JournalEntryDto>();

            var settings = await GetSettingsAsync(cancellationToken);
            var apAccountId = payment.BusinessPartner.DefaultApAccountId ?? settings.ControlAccountApId;
            if (apAccountId == null) throw new InvalidOperationException("AP Control Account not configured.");

            Guid varianceAccountId;
            if (variance > 0)
            {
                if (settings.RealizedFxLossAccountId == null) throw new InvalidOperationException("Realized FX Loss Account is missing in Finance Settings. Cannot post FX variance.");
                varianceAccountId = settings.RealizedFxLossAccountId.Value;
            }
            else
            {
                if (settings.RealizedFxGainAccountId == null) throw new InvalidOperationException("Realized FX Gain Account is missing in Finance Settings. Cannot post FX variance.");
                varianceAccountId = settings.RealizedFxGainAccountId.Value;
            }

            var absVariance = Math.Abs(variance);
            var transactions = new List<CreateAccountTransactionDto>();

            if (variance > 0)
            {
                // Positive variance = Loss (Paid more base currency than expected)
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = varianceAccountId,
                    Description = $"FX Variance (Loss) on Allocation {invoice.InvoiceNumber}",
                    TransactionType = "Debit",
                    Amount = absVariance,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = 1m,
                    ForeignAmount = 0m
                });
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = apAccountId.Value,
                    Description = $"FX Variance (Loss) on Allocation {invoice.InvoiceNumber}",
                    TransactionType = "Credit",
                    Amount = absVariance,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = 1m,
                    ForeignAmount = 0m
                });
            }
            else
            {
                // Negative variance = Gain (Paid less base currency than expected)
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = apAccountId.Value,
                    Description = $"FX Variance (Gain) on Allocation {invoice.InvoiceNumber}",
                    TransactionType = "Debit",
                    Amount = absVariance,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = 1m,
                    ForeignAmount = 0m
                });
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = varianceAccountId,
                    Description = $"FX Variance (Gain) on Allocation {invoice.InvoiceNumber}",
                    TransactionType = "Credit",
                    Amount = absVariance,
                    CurrencyCode = payment.CurrencyCode,
                    ExchangeRate = 1m,
                    ForeignAmount = 0m
                });
            }

            var accountIds = transactions.Select(t => t.AccountId).Distinct().ToList();
            var targetBooks = await _bookValidationService.ResolveTargetBooksAsync(settings.SubledgerPostingMode, accountIds, cancellationToken);
            
            var postedEntries = new List<JournalEntryDto>();
            
            foreach (var book in targetBooks)
            {
                await _bookValidationService.ValidateAccountsForBookAsync(accountIds, book, cancellationToken);

                var jeDto = new CreateJournalEntryDto
                {
                    TransactionDate = allocation.AllocationDate,
                    Description = $"FX Variance - Vendor Payment {payment.PaymentNumber} / Invoice {invoice.InvoiceNumber} [{book}]",
                    Reference = payment.PaymentNumber,
                    SourceModule = "AP",
                    SourceDocumentId = payment.Id, // Link to payment
                    SourceDocumentType = "VendorPayment",
                    BookClassification = book,
                    Transactions = transactions
                };

                var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
                var posted = await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
                postedEntries.Add(posted);
            }
            
            return postedEntries;
        }

        public async Task<List<JournalEntryDto>> PostSubledgerJournalAsync(Guid subledgerJournalId, CancellationToken cancellationToken = default)
        {
            var journal = await _repository.Query<SubledgerJournalEntry>()
                .Include(j => j.Lines).ThenInclude(l => l.BusinessPartner)
                .FirstOrDefaultAsync(j => j.Id == subledgerJournalId, cancellationToken);

            if (journal == null) throw new ArgumentException($"Subledger Journal {subledgerJournalId} not found.");

            var settings = await GetSettingsAsync(cancellationToken);
            var transactions = new List<CreateAccountTransactionDto>();
            var autoRoutingEnabled = settings.OpeningBalanceAutoRoutingEnabled;
            var isOpeningBalance = string.Equals(journal.EntryType, SubledgerEntryType.OpeningBalance, StringComparison.OrdinalIgnoreCase);

            if (isOpeningBalance && autoRoutingEnabled && settings.MigrationClearingAccountId == null)
            {
                throw new InvalidOperationException(
                    "Migration Clearing Account is not configured in Finance Settings. " +
                    "It is required for Opening Balance subledger postings.");
            }

            // Generate transactions for each batch line
            foreach (var line in journal.Lines)
            {
                var bp = line.BusinessPartner;
                var controlAccountId = journal.SubledgerType == "AR" 
                    ? (bp.DefaultArAccountId ?? settings.ControlAccountArId)
                    : (bp.DefaultApAccountId ?? settings.ControlAccountApId);
                    
                if (controlAccountId == null) throw new InvalidOperationException($"No control account configured for {bp.PartnerName}");
                var offsetAccountId = (isOpeningBalance && autoRoutingEnabled)
                    ? settings.MigrationClearingAccountId!.Value
                    : line.AccountId;

                if (line.BaseDebitAmount > 0)
                {
                    // Control Account Side
                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = controlAccountId.Value,
                        Description = $"{journal.SubledgerType} Control - {line.Description}",
                        TransactionType = "Debit",
                        Amount = line.BaseDebitAmount,
                        CurrencyCode = line.CurrencyCode,
                        ExchangeRate = line.ExchangeRate,
                        ForeignAmount = line.ForeignDebitAmount ?? line.BaseDebitAmount
                    });
                    
                    // Offset Account Side
                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = offsetAccountId,
                        Description = $"Offset - {line.Description}",
                        TransactionType = "Credit",
                        Amount = line.BaseDebitAmount,
                        CurrencyCode = line.CurrencyCode,
                        ExchangeRate = line.ExchangeRate,
                        ForeignAmount = line.ForeignDebitAmount ?? line.BaseDebitAmount
                    });
                }
                if (line.BaseCreditAmount > 0)
                {
                    // Control Account Side
                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = controlAccountId.Value,
                        Description = $"{journal.SubledgerType} Control - {line.Description}",
                        TransactionType = "Credit",
                        Amount = line.BaseCreditAmount,
                        CurrencyCode = line.CurrencyCode,
                        ExchangeRate = line.ExchangeRate,
                        ForeignAmount = line.ForeignCreditAmount ?? line.BaseCreditAmount
                    });
                    
                    // Offset Account Side
                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = offsetAccountId,
                        Description = $"Offset - {line.Description}",
                        TransactionType = "Debit",
                        Amount = line.BaseCreditAmount,
                        CurrencyCode = line.CurrencyCode,
                        ExchangeRate = line.ExchangeRate,
                        ForeignAmount = line.ForeignCreditAmount ?? line.BaseCreditAmount
                    });
                }
            }

            var accountIds = transactions.Select(t => t.AccountId).Distinct().ToList();
            var targetBooks = await _bookValidationService.ResolveTargetBooksAsync(settings.SubledgerPostingMode, accountIds, cancellationToken);
            
            var postedEntries = new List<JournalEntryDto>();
            
            foreach (var book in targetBooks)
            {
                await _bookValidationService.ValidateAccountsForBookAsync(accountIds, book, cancellationToken);

                var jeDto = new CreateJournalEntryDto
                {
                    TransactionDate = journal.TransactionDate,
                    Description = $"{journal.Description} [{book}]",
                    Reference = journal.JournalNumber,
                    SourceModule = journal.SubledgerType, // "AR" or "AP"
                    SourceDocumentId = journal.Id,
                    SourceDocumentType = "SubledgerJournal",
                    BookClassification = book,
                    Transactions = transactions
                };

                var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
                var posted = await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
                postedEntries.Add(posted);
            }
            
            return postedEntries;
        }

        public async Task<List<JournalEntryDto>> PostCreditNoteGLAsync(Guid creditNoteId, CancellationToken cancellationToken = default)
        {
            var creditNote = await _repository.Query<ErpSystem.Core.Entities.Sales.CreditNote>()
                .Include(cn => cn.BusinessPartner)
                .Include(cn => cn.Lines)
                .Include(cn => cn.OriginalInvoice)
                    .ThenInclude(i => i!.LineItems)
                .FirstOrDefaultAsync(cn => cn.Id == creditNoteId, cancellationToken);

            if (creditNote == null) throw new ArgumentException($"Credit Note {creditNoteId} not found.");

            var settings = await GetSettingsAsync(cancellationToken);
            var arAccountId = creditNote.BusinessPartner.DefaultArAccountId ?? settings.ControlAccountArId;
            if (arAccountId == null) throw new InvalidOperationException("AR Control Account not configured.");

            // Determine exchange rate: linked credit notes use the original invoice rate
            var exchangeRate = creditNote.ExchangeRate > 0 ? creditNote.ExchangeRate : 1m;
            if (creditNote.OriginalInvoice != null)
            {
                exchangeRate = creditNote.OriginalInvoice.ExchangeRate > 0 
                    ? creditNote.OriginalInvoice.ExchangeRate : 1m;
            }

            var transactions = new List<CreateAccountTransactionDto>();
            decimal totalBaseRevenue = 0;

            // Build per-line revenue reversal transactions
            // Try to resolve revenue GL account from original invoice lines, otherwise skip (standalone CN lines have no GL mapping yet)
            var originalInvoiceLines = creditNote.OriginalInvoice?.LineItems?.ToList();

            foreach (var cnLine in creditNote.Lines)
            {
                var lineNetAmount = cnLine.Quantity * cnLine.UnitPrice;
                if (lineNetAmount <= 0) continue;

                // Attempt to find matching original invoice line for GL account resolution
                Guid? revenueAccountId = null;
                if (originalInvoiceLines != null)
                {
                    var matchingInvLine = originalInvoiceLines
                        .FirstOrDefault(il => il.Description == cnLine.Description)
                        ?? originalInvoiceLines.FirstOrDefault(il => il.GLAccountId.HasValue);
                    revenueAccountId = matchingInvLine?.GLAccountId;
                }

                if (revenueAccountId == null)
                {
                    // Fallback: use suspense account if no revenue account can be resolved
                    revenueAccountId = settings.SuspenseAccountId;
                    if (revenueAccountId == null)
                        throw new InvalidOperationException($"No Revenue Account resolved for credit note line '{cnLine.Description}' and no Suspense Account configured.");
                }

                var baseLineAmount = Math.Round(lineNetAmount * exchangeRate, 2);
                totalBaseRevenue += baseLineAmount;

                // Dr Revenue (reversal of original revenue)
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = revenueAccountId.Value,
                    Description = $"Credit Note {creditNote.DocumentNumber} - Revenue Reversal - {cnLine.Description}",
                    TransactionType = "Debit",
                    Amount = baseLineAmount,
                    CurrencyCode = creditNote.Currency,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = lineNetAmount
                });
            }

            // Dr Output VAT (reversal of original tax) — using ControlAccountTaxId, same as PostArInvoiceAsync
            decimal baseTaxAmount = 0;
            if (creditNote.TaxAmount > 0)
            {
                var taxAccId = settings.ControlAccountTaxId;
                if (taxAccId == null) throw new InvalidOperationException("Tax Control Account not configured.");

                baseTaxAmount = Math.Round(creditNote.TaxAmount * exchangeRate, 2);

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = taxAccId.Value,
                    Description = $"Credit Note {creditNote.DocumentNumber} - Output Tax Reversal",
                    TransactionType = "Debit",
                    Amount = baseTaxAmount,
                    CurrencyCode = creditNote.Currency,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = creditNote.TaxAmount
                });
            }

            // Cr AR Control (reduction of customer receivable)
            var baseTotalAmount = totalBaseRevenue + baseTaxAmount;
            transactions.Add(new CreateAccountTransactionDto
            {
                AccountId = arAccountId.Value,
                Description = $"Credit Note {creditNote.DocumentNumber} - AR Reduction",
                TransactionType = "Credit",
                Amount = baseTotalAmount,
                CurrencyCode = creditNote.Currency,
                ExchangeRate = exchangeRate,
                ForeignAmount = creditNote.TotalAmount
            });

            var accountIds = transactions.Select(t => t.AccountId).Distinct().ToList();
            var targetBooks = await _bookValidationService.ResolveTargetBooksAsync(settings.SubledgerPostingMode, accountIds, cancellationToken);

            var postedEntries = new List<JournalEntryDto>();

            foreach (var book in targetBooks)
            {
                await _bookValidationService.ValidateAccountsForBookAsync(accountIds, book, cancellationToken);

                var jeDto = new CreateJournalEntryDto
                {
                    TransactionDate = creditNote.DocumentDate,
                    Description = $"Credit Note {creditNote.DocumentNumber} - {creditNote.BusinessPartner.PartnerName} [{book}]",
                    Reference = creditNote.DocumentNumber,
                    SourceModule = "AR",
                    SourceDocumentId = creditNote.Id,
                    SourceDocumentType = "CreditNote",
                    BookClassification = book,
                    Transactions = transactions
                };

                var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);

                // Link journal to credit note
                if (postedEntries.Count == 0)
                {
                    creditNote.JournalEntryId = journalEntry.Id;
                    await _repository.SaveChangesAsync(cancellationToken);
                }

                var posted = await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
                postedEntries.Add(posted);
            }

            return postedEntries;
        }

        public async Task<List<JournalEntryDto>> ReverseCreditNoteGLAsync(Guid creditNoteId, string reason, CancellationToken cancellationToken = default)
        {
            var entries = await _journalEntryService.GetJournalEntriesBySourceAsync("AR", creditNoteId, cancellationToken);
            var reversedEntries = new List<JournalEntryDto>();

            var now = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                if (entry.PostingStatus != "Voided" && entry.PostingStatus != "Reversed")
                {
                    var reversed = await _journalEntryService.ReverseJournalEntryAsync(entry.Id, reason, now, cancellationToken);
                    reversedEntries.Add(reversed);
                }
            }

            return reversedEntries;
        }

        public async Task<List<JournalEntryDto>> PostReturnOrderCOGSGLAsync(Guid returnOrderId, CancellationToken cancellationToken = default)
        {
            var returnOrder = await _repository.Query<ReturnOrder>()
                .Include(r => r.Lines)
                .Include(r => r.BusinessPartner)
                .FirstOrDefaultAsync(r => r.Id == returnOrderId, cancellationToken);

            if (returnOrder == null) throw new ArgumentException($"Return Order {returnOrderId} not found.");
            
            var settings = await GetSettingsAsync(cancellationToken);
            var transactions = new List<CreateAccountTransactionDto>();

            var cogsAccId = settings.ControlAccountCOGSId;
            var inventoryAccId = settings.ControlAccountInventoryId;

            if (cogsAccId == null || inventoryAccId == null)
            {
                _logger.LogWarning("COGS or Inventory control accounts not configured. Skipping COGS reversal for Return {DocNum}", returnOrder.DocumentNumber);
                return new List<JournalEntryDto>();
            }

            foreach (var line in returnOrder.Lines.Where(l => l.IsRestockable))
            {
                // We need the original unit cost from the sales order to reverse the exact COGS
                decimal unitCost = 0;
                if (line.SalesOrderLineId.HasValue)
                {
                    var soLine = await _repository.Query<SalesOrderLine>()
                        .FirstOrDefaultAsync(l => l.Id == line.SalesOrderLineId.Value, cancellationToken);
                    if (soLine != null)
                    {
                        unitCost = soLine.UnitCost ?? soLine.UnitPrice;
                    }
                }
                
                if (unitCost <= 0) unitCost = line.UnitPrice; // Fallback

                var costAmount = line.QuantityReturned * unitCost;

                if (costAmount > 0)
                {
                    // Reversal: Dr Inventory / Cr COGS
                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = inventoryAccId.Value,
                        Description = $"Inventory Return - {returnOrder.DocumentNumber} - {line.Description}",
                        TransactionType = "Debit",
                        Amount = costAmount,
                        CurrencyCode = settings.BaseCurrency ?? "GHS",
                        ExchangeRate = 1m,
                        ForeignAmount = costAmount
                    });

                    transactions.Add(new CreateAccountTransactionDto
                    {
                        AccountId = cogsAccId.Value,
                        Description = $"COGS Reversal - {returnOrder.DocumentNumber} - {line.Description}",
                        TransactionType = "Credit",
                        Amount = costAmount,
                        CurrencyCode = settings.BaseCurrency ?? "GHS",
                        ExchangeRate = 1m,
                        ForeignAmount = costAmount
                    });
                }
            }

            if (!transactions.Any())
                return new List<JournalEntryDto>();

            var accountIds = transactions.Select(t => t.AccountId).Distinct().ToList();
            var targetBooks = await _bookValidationService.ResolveTargetBooksAsync(settings.SubledgerPostingMode, accountIds, cancellationToken);
            var postedEntries = new List<JournalEntryDto>();

            foreach (var book in targetBooks)
            {
                await _bookValidationService.ValidateAccountsForBookAsync(accountIds, book, cancellationToken);

                var jeDto = new CreateJournalEntryDto
                {
                    TransactionDate = returnOrder.ReceivedDate ?? DateTime.UtcNow,
                    Description = $"Return Order Receipt {returnOrder.DocumentNumber} - {returnOrder.BusinessPartner.PartnerName} [{book}]",
                    Reference = returnOrder.DocumentNumber,
                    SourceModule = "AR",
                    SourceDocumentId = returnOrder.Id,
                    SourceDocumentType = "ReturnOrder",
                    BookClassification = book,
                    Transactions = transactions
                };

                var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);
                var posted = await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
                postedEntries.Add(posted);
            }

            return postedEntries;
        }
        public async Task<List<JournalEntryDto>> PostRefundGLAsync(Guid refundId, CancellationToken cancellationToken = default)
        {
            var refund = await _repository.Query<Refund>()
                .Include(r => r.BusinessPartner)
                .Include(r => r.BankAccount)
                .FirstOrDefaultAsync(r => r.Id == refundId, cancellationToken);

            if (refund == null)
                throw new KeyNotFoundException($"Refund {refundId} not found.");

            if (!refund.BankAccountId.HasValue || refund.BankAccount == null)
                throw new InvalidOperationException("Bank account is required for refund posting.");

            var settings = await GetSettingsAsync(cancellationToken);

            var arAccId = refund.BusinessPartner.DefaultArAccountId ?? settings.ControlAccountArId;
            if (!arAccId.HasValue)
                throw new InvalidOperationException($"AR Control account not configured for Partner {refund.BusinessPartner.PartnerName} or system settings.");

            var bankAccId = refund.BankAccount.GLAccountId;
            if (!bankAccId.HasValue)
                throw new InvalidOperationException($"GL Account not configured for Bank Account {refund.BankAccount.AccountName}.");

            var transactions = new List<CreateAccountTransactionDto>
            {
                // Debit AR Control (increases customer balance back to zero, consuming unapplied credit)
                new CreateAccountTransactionDto
                {
                    AccountId = arAccId.Value,
                    Description = $"Refund - {refund.DocumentNumber} - {refund.BusinessPartner.PartnerName}",
                    TransactionType = "Debit",
                    Amount = refund.BaseRefundAmount,
                    CurrencyCode = refund.CurrencyCode,
                    ExchangeRate = refund.ExchangeRate,
                    ForeignAmount = refund.RefundAmount
                },
                // Credit Bank (reduces bank balance)
                new CreateAccountTransactionDto
                {
                    AccountId = bankAccId.Value,
                    Description = $"Refund - {refund.DocumentNumber} - {refund.BusinessPartner.PartnerName}",
                    TransactionType = "Credit",
                    Amount = refund.BaseRefundAmount,
                    CurrencyCode = refund.CurrencyCode,
                    ExchangeRate = refund.ExchangeRate,
                    ForeignAmount = refund.RefundAmount
                }
            };

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(jeDto, cancellationToken);

            // Link JE to payment
            payment.JournalEntryId = journalEntry.Id;
            await _context.SaveChangesAsync(cancellationToken);

            return await _journalEntryService.PostJournalEntryAsync(journalEntry.Id, cancellationToken);
        }
    }
}
