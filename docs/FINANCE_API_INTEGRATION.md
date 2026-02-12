# Finance API Integration Guide

## Table of Contents
1. [Quick Start](#quick-start)
2. [Authentication & Authorization](#authentication--authorization)
3. [Master File Integration](#master-file-integration)
4. [Transaction Posting Workflows](#transaction-posting-workflows)
5. [Module-Specific Integration](#module-specific-integration)
6. [Error Handling](#error-handling)
7. [Testing & Validation](#testing--validation)
8. [Best Practices](#best-practices)

---

## Quick Start

### Prerequisites
- API Base URL: `https://localhost:53484/api/Finance`
- Authentication: Bearer token required
- Content-Type: `application/json`

### 5-Minute Integration Checklist

```
✅ 1. Get Finance settings (base currency, COA type)
✅ 2. Cache Chart of Accounts
✅ 3. Cache fiscal periods
✅ 4. Cache currencies (if multi-currency)
✅ 5. Test posting a journal entry
```

### Your First API Call

```http
GET https://localhost:53484/api/Finance/settings
Authorization: Bearer {your-token}
```

**Response:**
```json
{
  "baseCurrency": "GHS",
  "coaType": "Standard",
  "isMultiCurrencyEnabled": true,
  "fiscalYearStartMonth": 1
}
```

---

## Authentication & Authorization

### Required Headers
```http
Authorization: Bearer {jwt-token}
Content-Type: application/json
```

### Permission Levels

| Permission | Allows |
|------------|--------|
| `Finance.Read` | View accounts, periods, rates, settings |
| `Finance.Write` | Create/update accounts, currencies, rates |
| `Finance.Post` | Post and reverse journal entries |
| `Finance.PeriodClose` | Close/reopen fiscal periods |
| `Finance.Admin` | All operations including settings changes |

### Tenant Isolation
All Finance API endpoints automatically filter data by the authenticated user's tenant. You cannot access data from other tenants.

---

## Master File Integration

### 1. Chart of Accounts

**Endpoint:** `GET /api/Finance/accounts`

**When to Call:**
- Module initialization
- Account selection dropdowns
- Account validation

**Caching Strategy:**
```csharp
// Cache on startup, refresh daily or on demand
public class FinanceCache
{
    private static List<AccountDto> _accounts;
    private static DateTime _lastRefresh;
    
    public static async Task<List<AccountDto>> GetAccountsAsync()
    {
        if (_accounts == null || DateTime.Now - _lastRefresh > TimeSpan.FromHours(24))
        {
            _accounts = await _apiClient.GetAsync<List<AccountDto>>("/finance/accounts");
            _lastRefresh = DateTime.Now;
        }
        return _accounts;
    }
}
```

**Filtering Accounts:**
```csharp
// Get only revenue accounts for sales module
var revenueAccounts = await GetAccountsAsync();
revenueAccounts = revenueAccounts
    .Where(a => a.AccountType == "Revenue" && a.Status == "Active")
    .ToList();
```

---

### 2. Fiscal Periods

**Endpoint:** `GET /api/Finance/fiscal-periods`

**When to Call:**
- Before posting any transaction
- Transaction date validation
- Period selection dropdowns

**Date Validation:**
```csharp
public async Task<bool> ValidateTransactionDate(DateTime transactionDate)
{
    var periods = await _apiClient.GetAsync<List<FiscalPeriodDto>>("/finance/fiscal-periods");
    
    var period = periods.FirstOrDefault(p => 
        transactionDate >= p.StartDate && 
        transactionDate <= p.EndDate &&
        p.Status == "Open");
    
    if (period == null)
    {
        throw new InvalidOperationException(
            $"No open fiscal period found for date {transactionDate:yyyy-MM-dd}");
    }
    
    return true;
}
```

---

### 3. Currencies (Multi-Currency Modules)

**Endpoint:** `GET /api/Finance/currencies`

**When to Call:**
- Module initialization
- Currency selection dropdowns
- Currency validation

**Get Active Currencies:**
```csharp
var currencies = await _apiClient.GetAsync<List<CurrencyDto>>("/finance/currencies");
var activeCurrencies = currencies.Where(c => c.IsActive).ToList();
```

**Identify Base Currency:**
```csharp
var baseCurrency = currencies.FirstOrDefault(c => c.IsBaseCurrency);
```

---

### 4. Exchange Rates (Multi-Currency Modules)

**Endpoint:** `GET /api/Finance/exchange-rates/current/{currencyCode}`

**When to Call:**
- Before posting foreign currency transactions
- Currency conversion calculations
- Multi-currency reporting

**Get Current Rate:**
```csharp
public async Task<decimal> GetExchangeRate(string currencyCode)
{
    var rate = await _apiClient.GetAsync<ExchangeRateDto>(
        $"/finance/exchange-rates/current/{currencyCode}");
    
    if (rate == null)
    {
        throw new InvalidOperationException(
            $"No exchange rate found for {currencyCode}");
    }
    
    return rate.Rate;
}
```

**Currency Conversion:**
```csharp
// Convert foreign currency to base currency
decimal foreignAmount = 1000.00m; // USD
string foreignCurrency = "USD";
decimal exchangeRate = await GetExchangeRate(foreignCurrency);
decimal baseAmount = foreignAmount * exchangeRate; // GHS 14,500
```

---

## Transaction Posting Workflows

### Standard Workflow

```
┌─────────────────────────────────────────┐
│ 1. Validate Transaction Date           │
│    GET /api/Finance/fiscal-periods      │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│ 2. Get Exchange Rate (if multi-currency)│
│    GET /api/Finance/exchange-rates/...  │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│ 3. Create Journal Entry                 │
│    POST /api/Finance/journal-entries    │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│ 4. Post Entry (optional)                │
│    POST /api/Finance/journal-entries/   │
│         {id}/post                       │
└─────────────────────────────────────────┘
```

### Auto-Post Pattern

```csharp
public async Task<Guid> PostTransactionAsync(CreateJournalEntryDto entry)
{
    // 1. Validate period
    await ValidateTransactionDate(entry.EntryDate);
    
    // 2. Create entry
    var createdEntry = await _apiClient.PostAsync<JournalEntryDto>(
        "/finance/journal-entries", entry);
    
    // 3. Post immediately
    await _apiClient.PostAsync($"/finance/journal-entries/{createdEntry.Id}/post", null);
    
    return createdEntry.Id;
}
```

### Manual Approval Pattern

```csharp
public async Task<Guid> CreateDraftEntryAsync(CreateJournalEntryDto entry)
{
    // 1. Validate period
    await ValidateTransactionDate(entry.EntryDate);
    
    // 2. Create entry (stays in Draft status)
    var createdEntry = await _apiClient.PostAsync<JournalEntryDto>(
        "/finance/journal-entries", entry);
    
    // Finance team will review and post later
    return createdEntry.Id;
}
```

---

## Module-Specific Integration

### Inventory Module

#### COGS Posting

**Scenario:** Post cost of goods sold when inventory is sold

```csharp
public async Task PostCOGSAsync(string invoiceNumber, decimal cogsAmount)
{
    var entry = new CreateJournalEntryDto
    {
        EntryDate = DateTime.Today,
        Description = $"COGS for Sales Invoice {invoiceNumber}",
        ReferenceNumber = invoiceNumber,
        SourceModule = "Inventory",
        FiscalPeriodId = await GetCurrentPeriodId(),
        Transactions = new List<JournalEntryLineDto>
        {
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("COGS"),
                DebitAmount = cogsAmount,
                Description = "Cost of Goods Sold"
            },
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("Inventory"),
                CreditAmount = cogsAmount,
                Description = "Inventory reduction"
            }
        }
    };
    
    await PostTransactionAsync(entry);
}
```

---

### Sales Module

#### Revenue Recognition

**Scenario:** Post revenue when invoice is created

```csharp
public async Task PostInvoiceAsync(Invoice invoice)
{
    var entry = new CreateJournalEntryDto
    {
        EntryDate = invoice.InvoiceDate,
        Description = $"Revenue for Invoice {invoice.InvoiceNumber}",
        ReferenceNumber = invoice.InvoiceNumber,
        SourceModule = "Sales",
        FiscalPeriodId = await GetCurrentPeriodId(),
        Transactions = new List<JournalEntryLineDto>
        {
            // Debit: Accounts Receivable
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("AccountsReceivable"),
                DebitAmount = invoice.TotalAmount,
                Description = "Accounts Receivable"
            },
            // Credit: Revenue
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("SalesRevenue"),
                CreditAmount = invoice.SubTotal,
                Description = "Sales Revenue"
            },
            // Credit: Tax
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("VATPayable"),
                CreditAmount = invoice.TaxAmount,
                Description = "VAT Payable"
            }
        }
    };
    
    await PostTransactionAsync(entry);
}
```

#### Foreign Currency Invoice

```csharp
public async Task PostForeignCurrencyInvoiceAsync(Invoice invoice)
{
    // Get exchange rate
    decimal exchangeRate = await GetExchangeRate(invoice.CurrencyCode);
    decimal baseAmount = invoice.TotalAmount * exchangeRate;
    
    var entry = new CreateJournalEntryDto
    {
        EntryDate = invoice.InvoiceDate,
        Description = $"Revenue for Invoice {invoice.InvoiceNumber} ({invoice.CurrencyCode})",
        ReferenceNumber = invoice.InvoiceNumber,
        SourceModule = "Sales",
        FiscalPeriodId = await GetCurrentPeriodId(),
        TransactionCurrency = invoice.CurrencyCode,
        ExchangeRate = exchangeRate,
        Transactions = new List<JournalEntryLineDto>
        {
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("AccountsReceivable"),
                DebitAmount = baseAmount,
                ForeignCurrencyAmount = invoice.TotalAmount,
                Description = $"AR - {invoice.CurrencyCode} {invoice.TotalAmount:N2}"
            },
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("SalesRevenue"),
                CreditAmount = baseAmount,
                ForeignCurrencyAmount = invoice.TotalAmount,
                Description = "Sales Revenue"
            }
        }
    };
    
    await PostTransactionAsync(entry);
}
```

---

### Purchasing Module

#### Purchase Order Posting

```csharp
public async Task PostPurchaseAsync(PurchaseOrder po)
{
    var entry = new CreateJournalEntryDto
    {
        EntryDate = po.OrderDate,
        Description = $"Purchase Order {po.PONumber}",
        ReferenceNumber = po.PONumber,
        SourceModule = "Purchasing",
        FiscalPeriodId = await GetCurrentPeriodId(),
        Transactions = new List<JournalEntryLineDto>
        {
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("Expenses"),
                DebitAmount = po.TotalAmount,
                Description = po.Description
            },
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("AccountsPayable"),
                CreditAmount = po.TotalAmount,
                Description = "Accounts Payable"
            }
        }
    };
    
    await PostTransactionAsync(entry);
}
```

---

### Payroll Module

#### Payroll Run Posting

```csharp
public async Task PostPayrollAsync(PayrollRun payroll)
{
    var entry = new CreateJournalEntryDto
    {
        EntryDate = payroll.PayDate,
        Description = $"Payroll for {payroll.Period}",
        ReferenceNumber = $"PAYROLL-{payroll.Period}",
        SourceModule = "Payroll",
        FiscalPeriodId = await GetCurrentPeriodId(),
        Transactions = new List<JournalEntryLineDto>
        {
            // Debit: Salary Expense
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("SalaryExpense"),
                DebitAmount = payroll.GrossSalary,
                Description = "Gross Salaries"
            },
            // Credit: Tax Withholding
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("TaxWithheld"),
                CreditAmount = payroll.TaxWithheld,
                Description = "Tax Withholding"
            },
            // Credit: Net Pay
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("PayrollPayable"),
                CreditAmount = payroll.NetPay,
                Description = "Net Pay"
            }
        }
    };
    
    await PostTransactionAsync(entry);
}
```

---

### Fixed Assets Module

#### Depreciation Posting

```csharp
public async Task PostDepreciationAsync(string period, decimal amount)
{
    var entry = new CreateJournalEntryDto
    {
        EntryDate = DateTime.Parse($"{period}-01").AddMonths(1).AddDays(-1),
        Description = $"Monthly depreciation - {period}",
        ReferenceNumber = $"DEP-{period}",
        SourceModule = "FixedAssets",
        FiscalPeriodId = await GetCurrentPeriodId(),
        Transactions = new List<JournalEntryLineDto>
        {
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("DepreciationExpense"),
                DebitAmount = amount,
                Description = "Depreciation Expense"
            },
            new JournalEntryLineDto
            {
                AccountId = await GetAccountId("AccumulatedDepreciation"),
                CreditAmount = amount,
                Description = "Accumulated Depreciation"
            }
        }
    };
    
    await PostTransactionAsync(entry);
}
```

---

## Error Handling

### Common Error Scenarios

#### 1. Unbalanced Entry
```json
{
  "error": "Journal entry is not balanced. Debits: 5000, Credits: 4500"
}
```

**Solution:** Ensure total debits equal total credits

#### 2. Closed Period
```json
{
  "error": "Cannot post to closed fiscal period"
}
```

**Solution:** Check period status before posting

#### 3. Missing Exchange Rate
```json
{
  "error": "No exchange rate found for USD on 2024-12-15"
}
```

**Solution:** Ensure exchange rates are configured

#### 4. Invalid Account
```json
{
  "error": "Account does not allow direct posting"
}
```

**Solution:** Use posting-allowed accounts only

### Error Handling Pattern

```csharp
public async Task<Result> PostTransactionSafelyAsync(CreateJournalEntryDto entry)
{
    try
    {
        // Validate period
        await ValidateTransactionDate(entry.EntryDate);
        
        // Validate balance
        var totalDebits = entry.Transactions.Sum(t => t.DebitAmount ?? 0);
        var totalCredits = entry.Transactions.Sum(t => t.CreditAmount ?? 0);
        
        if (totalDebits != totalCredits)
        {
            return Result.Failure($"Entry not balanced. Debits: {totalDebits}, Credits: {totalCredits}");
        }
        
        // Post
        var result = await _apiClient.PostAsync<JournalEntryDto>(
            "/finance/journal-entries", entry);
        
        return Result.Success(result.Id);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
    {
        return Result.Failure($"Validation error: {ex.Message}");
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        return Result.Failure($"Resource not found: {ex.Message}");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error posting journal entry");
        return Result.Failure("An unexpected error occurred");
    }
}
```

---

## Testing & Validation

### Integration Test Checklist

- [ ] Can retrieve Chart of Accounts
- [ ] Can validate transaction dates
- [ ] Can create balanced journal entry
- [ ] Can post journal entry
- [ ] Can reverse posted entry
- [ ] Multi-currency conversion works correctly
- [ ] Error handling works as expected
- [ ] Tenant isolation is enforced

### Sample Test

```csharp
[Test]
public async Task Should_Post_Simple_Journal_Entry()
{
    // Arrange
    var entry = new CreateJournalEntryDto
    {
        EntryDate = DateTime.Today,
        Description = "Test Entry",
        ReferenceNumber = "TEST-001",
        SourceModule = "TestModule",
        FiscalPeriodId = await GetCurrentPeriodId(),
        Transactions = new List<JournalEntryLineDto>
        {
            new() { AccountId = _debitAccountId, DebitAmount = 100, Description = "Test Debit" },
            new() { AccountId = _creditAccountId, CreditAmount = 100, Description = "Test Credit" }
        }
    };
    
    // Act
    var result = await _financeService.PostTransactionAsync(entry);
    
    // Assert
    Assert.IsNotNull(result);
    Assert.IsTrue(result != Guid.Empty);
}
```

---

## Best Practices

### 1. Always Validate Before Posting
```csharp
// ✅ Good
await ValidateTransactionDate(entry.EntryDate);
await PostTransactionAsync(entry);

// ❌ Bad
await PostTransactionAsync(entry); // May fail if period closed
```

### 2. Cache Master Files
```csharp
// ✅ Good - Cache accounts on startup
private static List<AccountDto> _cachedAccounts;

// ❌ Bad - Fetch accounts every time
var accounts = await GetAccountsAsync(); // Slow!
```

### 3. Use Source Module
```csharp
// ✅ Good - Identifies origin
entry.SourceModule = "Sales";
entry.ReferenceNumber = invoice.InvoiceNumber;

// ❌ Bad - No audit trail
entry.SourceModule = null;
```

### 4. Store Journal Entry IDs
```csharp
// ✅ Good - Can reverse later
invoice.JournalEntryId = await PostTransactionAsync(entry);

// ❌ Bad - Cannot reverse
await PostTransactionAsync(entry);
```

### 5. Handle Multi-Currency Properly
```csharp
// ✅ Good - Store both amounts
entry.TransactionCurrency = "USD";
entry.ExchangeRate = 14.5m;
line.ForeignCurrencyAmount = 1000m;
line.DebitAmount = 14500m; // Base currency

// ❌ Bad - Missing foreign amount
line.DebitAmount = 14500m; // Lost original amount!
```

---

## Support & Resources

### API Documentation
- Swagger UI: `https://localhost:53484/swagger`
- API Base URL: `https://localhost:53484/api/Finance`

### Key Endpoints Reference

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/accounts` | GET | Get Chart of Accounts |
| `/fiscal-periods` | GET | Get fiscal periods |
| `/journal-entries` | POST | Create journal entry |
| `/journal-entries/{id}/post` | POST | Post entry to GL |
| `/journal-entries/{id}/reverse` | POST | Reverse posted entry |
| `/currencies` | GET | Get currencies |
| `/exchange-rates/current/{code}` | GET | Get current rate |
| `/settings` | GET | Get Finance settings |

### Contact
For integration support, contact: finance-api@example.com
