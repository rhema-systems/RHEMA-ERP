# Rhema ERP Finance Module - API Reference for Frontend Implementation

## Overview

This document maps the Finance module backend API endpoints to frontend UI screens for agentic AI implementation.

**Backend Stack:** .NET Core, Entity Framework Core
**Frontend Stack:** Next.js 14 (App Router), React, shadcn/ui, TypeScript
**Base API URL:** `/api`

---

## Table of Contents

1. [COA Configuration (Standard vs Segmented)](#1-coa-configuration)
2. [Currency Management](#2-currency-management)
3. [Exchange Rates](#3-exchange-rates)
4. [Fiscal Year & Period Management](#4-fiscal-year--period-management)
5. [Chart of Accounts](#5-chart-of-accounts)
6. [Segment Configuration](#6-segment-configuration)
7. [Journal Entries](#7-journal-entries)
8. [Financial Reports](#8-financial-reports)
9. [Currency Revaluation](#9-currency-revaluation)
10. [TypeScript Types](#10-typescript-types)

---

## 1. COA Configuration

### Tenant Settings - COA Type

**IMPORTANT:** The COA type (Standard vs Segmented) should be stored at tenant level and cannot be changed once accounts are created.

| Setting | Values | Description |
|---------|--------|-------------|
| `coaType` | `"Standard"` \| `"Segmented"` | Chart of Accounts structure type |
| `coaConfigurationLocked` | `boolean` | True once first account is created |
| `baseCurrency` | `string` (3-char) | Home currency (e.g., "GHS") |

**UI Flow:**
```
First-Time Setup Wizard
├── Step 1: Select COA Type (Standard/Segmented)
├── Step 2: If Segmented → Configure Segments
├── Step 3: Configure Base Currency
└── Step 4: Create Initial Fiscal Year
```

**Lock Logic:**
```typescript
// Check if COA type can be changed
const canChangeCOAType = async () => {
  const accounts = await apiService.get('/finance/accounts?count=true');
  return accounts.total === 0;
};
```

---

## 2. Currency Management

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/finance/currencies` | List all currencies |
| `GET` | `/api/finance/currencies/{code}` | Get currency by code |
| `POST` | `/api/finance/currencies` | Create new currency |
| `PUT` | `/api/finance/currencies/{code}` | Update currency |
| `DELETE` | `/api/finance/currencies/{code}` | Delete currency (if no transactions) |
| `PATCH` | `/api/finance/currencies/{code}/activate` | Activate currency |
| `PATCH` | `/api/finance/currencies/{code}/deactivate` | Deactivate currency |

### UI Screen: `/finance/currencies`

**Features:**
- DataTable with search, filter, pagination
- Add/Edit currency dialog with ISO 4217 validation
- Status badges (Active/Inactive/Base Currency)
- Bulk exchange rate upload button
- Cannot delete currencies with `hasTransactionHistory: true`

### Request/Response DTOs

```typescript
// GET /api/finance/currencies
interface CurrencyListResponse {
  items: Currency[];
  total: number;
  page: number;
  pageSize: number;
}

// POST /api/finance/currencies
interface CreateCurrencyDto {
  currencyCode: string;        // 3-char ISO 4217 (e.g., "USD")
  numericCode: string;         // 3-digit ISO numeric (e.g., "840")
  currencyName: string;        // "United States Dollar"
  currencySymbol?: string;     // "$"
  decimalPlaces: number;       // 0-4, typically 2
  roundingMethod: string;      // "Standard" | "Up" | "Down" | "BankersRounding"
  roundingPrecision: number;   // 0.01 for most currencies
  symbolPosition: string;      // "Before" | "After"
  decimalSeparator: string;    // "." or ","
  thousandsSeparator?: string; // "," or "." or " "
  countryCode?: string;        // ISO 3166-1 alpha-2
  isActive: boolean;
  isBaseCurrency: boolean;
}

// PUT /api/finance/currencies/{code}
interface UpdateCurrencyDto extends CreateCurrencyDto {
  // Same fields, currencyCode cannot be changed
}
```

---

## 3. Exchange Rates

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/finance/exchange-rates` | List exchange rates (with filters) |
| `GET` | `/api/finance/exchange-rates/{id}` | Get specific rate |
| `GET` | `/api/finance/exchange-rates/current/{currencyCode}` | Get current rate for currency |
| `POST` | `/api/finance/exchange-rates` | Create new rate |
| `POST` | `/api/finance/exchange-rates/bulk` | Bulk upload rates (CSV/Excel) |
| `PUT` | `/api/finance/exchange-rates/{id}` | Update rate (metadata only if used) |
| `DELETE` | `/api/finance/exchange-rates/{id}` | Delete (if not used in transactions) |

### UI Screen: `/finance/exchange-rates`

**Features:**
- Filter by currency, date range, rate type
- Rate type tabs: Daily | Month-End | Budget | Average
- Inline rate entry for quick updates
- Historical rate chart/graph
- Variance alerts for rates exceeding threshold (>5%)

### Request/Response DTOs

```typescript
// GET /api/finance/exchange-rates?currencyCode=USD&rateType=Daily&from=2025-01-01&to=2025-01-31
interface ExchangeRateListResponse {
  items: ExchangeRate[];
  total: number;
}

// POST /api/finance/exchange-rates
interface CreateExchangeRateDto {
  baseCurrencyCode: string;    // "GHS" (typically base currency)
  targetCurrencyCode: string;  // "USD", "EUR", etc.
  rate: number;                // e.g., 15.25 (1 USD = 15.25 GHS)
  effectiveDate: string;       // ISO date
  rateType: ExchangeRateType;  // "Daily" | "Average" | "MonthEnd" | "Budget" | "YearEnd"
  rateSource: string;          // "Manual Entry" | "Bank of Ghana" | "API"
  comments?: string;
}

type ExchangeRateType = 'Daily' | 'Average' | 'MonthEnd' | 'QuarterEnd' | 'YearEnd' | 'Budget' | 'Fixed' | 'Spot';
```

---

## 4. Fiscal Year & Period Management

### Fiscal Year Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/finance/fiscal-years` | List all fiscal years |
| `GET` | `/api/finance/fiscal-years/{id}` | Get fiscal year details |
| `POST` | `/api/finance/fiscal-years` | Create fiscal year (auto-generates periods) |
| `PUT` | `/api/finance/fiscal-years/{id}` | Update fiscal year |
| `POST` | `/api/finance/fiscal-years/{id}/close` | Close fiscal year |
| `POST` | `/api/finance/fiscal-years/{id}/lock` | Lock fiscal year |
| `POST` | `/api/finance/fiscal-years/{id}/unlock` | Unlock fiscal year |

### Fiscal Period Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/finance/fiscal-periods` | List periods (optionally by year) |
| `GET` | `/api/finance/fiscal-periods/{id}` | Get period details |
| `POST` | `/api/finance/periods/{id}/validate-close` | Validate period can be closed |
| `POST` | `/api/finance/periods/{id}/close` | Close period |
| `POST` | `/api/finance/periods/{id}/reopen` | Reopen closed period |
| `POST` | `/api/finance/periods/{id}/lock` | Lock period |
| `POST` | `/api/finance/periods/{id}/unlock` | Unlock period |

### UI Screen: `/finance/fiscal-periods`

**Features:**
- Fiscal year selector dropdown
- Period grid with status badges: `Future` | `Open` | `Closed` | `Locked`
- Action buttons based on status:
  - Future → Open
  - Open → Close (with validation)
  - Closed → Lock or Reopen
  - Locked → Unlock (requires reason)
- Close checklist modal showing validation steps
- Period statistics (journal count, total debits/credits)

### Request/Response DTOs

```typescript
// POST /api/finance/fiscal-years
interface CreateFiscalYearDto {
  fiscalYearName: string;      // "Fiscal Year 2025"
  fiscalYearCode: string;      // "2025"
  year: number;                // 2025
  startDate: string;           // "2025-01-01"
  endDate: string;             // "2025-12-31"
  fiscalYearType: string;      // "Calendar" | "Custom"
  numberOfPeriods: number;     // 12 (monthly) or 4 (quarterly)
  baseCurrency: string;        // "GHS"
}

// POST /api/finance/periods/{id}/close
interface PeriodCloseRequestDto {
  fiscalPeriodId: string;
  closedByUserId?: string;
  closingNotes?: string;
  bypassValidation?: boolean;  // For admin override
}

// POST /api/finance/periods/{id}/reopen
interface PeriodReopenRequestDto {
  fiscalPeriodId: string;
  reopenReason: string;        // Required
}

// POST /api/finance/periods/{id}/lock
interface PeriodLockRequestDto {
  lockReason: string;          // Required
}

// Response from validate-close
interface PeriodCloseValidationDto {
  canClose: boolean;
  validationResults: {
    trialBalanceValidated: boolean;
    bankReconciliationComplete: boolean;
    currencyRevaluationComplete: boolean;
    depreciationComplete: boolean;
    accrualsComplete: boolean;
  };
  blockers: string[];          // List of reasons if canClose = false
  warnings: string[];          // Non-blocking warnings
}
```

---

## 5. Chart of Accounts

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/finance/accounts` | List accounts (with filters) |
| `GET` | `/api/finance/accounts/{id}` | Get account details |
| `GET` | `/api/finance/accounts/{id}/balance` | Get account balance |
| `POST` | `/api/finance/accounts` | Create account |
| `PUT` | `/api/finance/accounts/{id}` | Update account |
| `DELETE` | `/api/finance/accounts/{id}` | Delete (if no transactions) |
| `GET` | `/api/finance/accounts/{id}/currencies` | Get currency links |
| `POST` | `/api/finance/accounts/{id}/currencies` | Add currency link |
| `DELETE` | `/api/finance/accounts/{id}/currencies/{code}` | Remove currency link |
| `PATCH` | `/api/finance/accounts/{id}/currencies/{code}/inactivate` | Inactivate currency link |

### UI Screen: `/finance/chart-of-accounts`

**Features:**
- **Standard COA:** Simple tree view with Main Account → Sub Accounts
- **Segmented COA:** Account grid with segment columns, segment-based filtering
- Account type filter: Asset | Liability | Equity | Revenue | Expense
- Status filter: Active | Inactive
- Classification tabs: IFRS | Base | Local
- Expandable rows showing currency links
- Add Account dialog adapts to COA type

### Request/Response DTOs

```typescript
// GET /api/finance/accounts?accountType=Asset&status=Active&classification=IFRS
interface AccountListResponse {
  items: Account[];
  total: number;
}

// POST /api/finance/accounts
interface AccountCreateDto {
  // Core fields
  accountCode: string;         // Human-readable code
  accountNumber: string;       // Full account number (constructed if segmented)
  accountName: string;
  accountType: AccountType;    // "Asset" | "Liability" | "Equity" | "Revenue" | "Expense"
  accountCategory?: string;    // "Current Assets", "Fixed Assets", etc.
  accountSubCategory?: string;
  description?: string;
  parentAccountId?: string;    // For hierarchy
  
  // Segmented COA fields (only if isSegmented = true)
  isSegmented: boolean;
  segmentValues?: SegmentValueInput[];  // Array of segment position → value
  
  // Multi-currency
  currencyCode: string;        // Primary currency (default: "GHS")
  isMultiCurrency: boolean;
  
  // Classification
  isIFRSClassified: boolean;
  isBaseClassified: boolean;
  isLocalClassified: boolean;
  ifrsLineItem?: string;
  baseLineItem?: string;
  localLineItem?: string;
  
  // Controls
  allowDirectPosting: boolean;
  isControlAccount: boolean;
  budgetTrackingEnabled: boolean;
  status: AccountStatus;       // "Active" | "Inactive" | "Closed"
}

interface SegmentValueInput {
  segmentPosition: number;     // 1-20
  segmentValue: string;        // e.g., "100" for Department
}

// POST /api/finance/accounts/{id}/currencies
interface AddCurrencyLinkDto {
  accountId: string;
  linkedCurrencyCode: string;  // "USD", "EUR", etc.
  revaluationRequired: boolean;
  revaluationFrequency: RevaluationFrequency;
  transactionRateType: string; // "Daily" | "Average" | "Budget"
  revaluationRateType: string; // "MonthEnd" | "YearEnd"
  notes?: string;
}

// Response when trying to remove currency with history
interface CurrencyLinkRemovalResultDto {
  success: boolean;
  wasDeleted: boolean;
  wasInactivated: boolean;
  message: string;
  transactionCount?: number;   // If blocked due to history
}
```

---

## 6. Segment Configuration

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/finance/segments` | List all segment structures |
| `GET` | `/api/finance/segments/{id}` | Get segment details |
| `POST` | `/api/finance/segments` | Create segment structure |
| `PUT` | `/api/finance/segments/{id}` | Update segment structure |
| `DELETE` | `/api/finance/segments/{id}` | Delete segment (if unused) |
| `GET` | `/api/finance/segments/{id}/values` | Get lookup values for segment |
| `POST` | `/api/finance/segments/{id}/values` | Add lookup value |
| `PUT` | `/api/finance/segments/{id}/values/{valueId}` | Update lookup value |
| `DELETE` | `/api/finance/segments/{id}/values/{valueId}` | Delete lookup value |
| `POST` | `/api/finance/segments/validate` | Validate account number |
| `POST` | `/api/finance/segments/construct` | Construct account number from segments |
| `GET` | `/api/finance/segments/parse/{accountNumber}` | Parse account number into segments |

### UI Screen: `/finance/settings/segments`

**Visibility:** Only shown if COA Type = "Segmented"

**Features:**
- Segment list with drag-and-drop reordering (position)
- Add/Edit segment dialog
- Lookup values management for each segment
- Account number preview with separator
- Reporting dimension toggle per segment

### Request/Response DTOs

```typescript
// GET /api/finance/segments
interface SegmentStructureDto {
  id: string;
  segmentName: string;         // "Department", "Cost Center"
  segmentCode: string;         // "DEPT", "CC"
  segmentPosition: number;     // 1-20
  segmentLength: number;       // 1-10 characters
  dataType: string;            // "Alphanumeric"
  separatorCharacter?: string; // "-", ".", "_"
  lookupTableRequired: boolean;
  isMandatory: boolean;
  isReportingDimension: boolean;
  isNaturalAccount: boolean;
  isActive: boolean;
  description?: string;
  lookupValues?: SegmentLookupValueDto[];
}

// POST /api/finance/segments
interface SegmentStructureCreateDto {
  segmentName: string;
  segmentCode: string;
  segmentPosition: number;
  segmentLength: number;
  dataType: string;
  separatorCharacter?: string;
  lookupTableRequired: boolean;
  isMandatory: boolean;
  isReportingDimension: boolean;
  isNaturalAccount: boolean;
  description?: string;
}

// Lookup value for segment
interface SegmentLookupValueDto {
  id: string;
  segmentStructureId: string;
  segmentValue: string;        // "100", "FIN", etc.
  description: string;         // "Finance Department"
  effectiveDate: string;
  expirationDate?: string;
  isActive: boolean;
  displayOrder: number;
}

// POST /api/finance/segments/validate
// Request body: string (account number)
interface SegmentedAccountValidationDto {
  isValid: boolean;
  accountNumber: string;
  segmentValues: SegmentValueDto[];
  validationErrors: string[];
}

// POST /api/finance/segments/construct
// Request body: Dictionary<int, string> (position → value)
interface AccountNumberConstructionDto {
  accountNumber: string;
  segmentValues: SegmentValueDto[];
}

interface SegmentValueDto {
  segmentPosition: number;
  segmentCode: string;
  segmentName: string;
  value: string;
  description?: string;
  isValid: boolean;
}
```

---

## 7. Journal Entries

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/finance/journal-entries` | List journal entries (with filters) |
| `GET` | `/api/finance/journal-entries/{id}` | Get journal entry with lines |
| `POST` | `/api/finance/journal-entries` | Create & post journal entry |
| `PUT` | `/api/finance/journal-entries/{id}` | Update draft journal entry |
| `DELETE` | `/api/finance/journal-entries/{id}` | Delete draft journal entry |
| `POST` | `/api/finance/journal-entries/{id}/post` | Post draft to GL |
| `POST` | `/api/finance/journal-entries/{id}/reverse` | Reverse posted entry |
| `POST` | `/api/finance/journal-entries/{id}/approve` | Approve pending entry |
| `POST` | `/api/finance/journal-entries/{id}/reject` | Reject pending entry |

### UI Screen: `/finance/journal-entries`

**Features:**
- Journal entry list with filters (date range, status, type, period)
- Status badges: `Draft` | `Pending Approval` | `Posted` | `Reversed`
- Quick view of totals (debits/credits/balance)
- Create new journal entry button

### UI Screen: `/finance/journal-entries/new` and `/finance/journal-entries/[id]`

**Features:**
- Header section: Entry date, description, reference, journal type
- Lines editor:
  - Account selector (searchable dropdown with account tree)
  - Description per line
  - Debit/Credit amount (mutually exclusive)
  - Currency selector (if multi-currency account)
  - Foreign amount + exchange rate (if foreign currency)
- Running totals footer showing:
  - Total Debits | Total Credits | Difference
- Save as Draft / Post button
- Balance validation (cannot post if not balanced)

### Request/Response DTOs

```typescript
// GET /api/finance/journal-entries?status=Posted&periodId=xxx&from=2025-01-01
interface JournalEntryListResponse {
  items: JournalEntryDto[];
  total: number;
}

interface JournalEntryDto {
  id: string;
  journalEntryNumber: string;
  journalType: JournalType;
  entryDate: string;
  description: string;
  referenceNumber?: string;
  sourceModule?: string;
  totalDebitAmount: number;
  totalCreditAmount: number;
  isBalanced: boolean;
  isMultiCurrency: boolean;
  primaryCurrency?: string;
  bookClassification: string;
  fiscalPeriodId: string;
  postingStatus: PostingStatus;
  postingDate?: string;
  requiresApproval: boolean;
  approvalStatus?: string;
  isReversed: boolean;
  isRevaluationEntry: boolean;
  transactions: JournalEntryLineDto[];
}

type JournalType = 'General' | 'Adjusting' | 'Reversing' | 'Recurring' | 'Opening Balance' | 'Closing' | 'Revaluation' | 'System Generated';
type PostingStatus = 'Draft' | 'Pending Approval' | 'Approved' | 'Posted' | 'Rejected' | 'Reversed';

interface JournalEntryLineDto {
  id: string;
  lineNumber: number;
  accountId: string;
  accountCode: string;
  accountName: string;
  description: string;
  debitAmount: number;
  creditAmount: number;
  transactionCurrency?: string;
  foreignCurrencyAmount?: number;
  exchangeRate?: number;
  isRevaluationEntry: boolean;
}

// POST /api/finance/journal-entries
interface CreateJournalEntryDto {
  journalType: JournalType;
  entryDate: string;           // Must be in open period
  description: string;
  referenceNumber?: string;
  bookClassification: string;  // "IFRS" | "Base" | "Local"
  fiscalPeriodId?: string;     // Auto-determined if not provided
  notes?: string;
  transactions: CreateJournalEntryLineDto[];
}

interface CreateJournalEntryLineDto {
  accountId: string;
  description?: string;
  debitAmount: number;         // Either debit OR credit, not both
  creditAmount: number;
  transactionCurrency?: string;
  foreignCurrencyAmount?: number;
  exchangeRate?: number;       // Auto-fetched if not provided
}
```

---

## 8. Financial Reports

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/finance/statements/trial-balance` | Generate Trial Balance |
| `GET` | `/api/finance/statements/income-statement` | Generate Income Statement |
| `GET` | `/api/finance/statements/balance-sheet` | Generate Balance Sheet |
| `GET` | `/api/finance/statements/cash-flow` | Generate Cash Flow Statement |
| `GET` | `/api/finance/statements/multi-currency-detail` | Multi-currency detail report |

### UI Screens

#### `/finance/reports/trial-balance`
- Period selector
- Classification filter (IFRS/Base/Local)
- Include zero balances toggle
- Account type filter
- Export to Excel/PDF

#### `/finance/reports/income-statement`
- Period selector (with comparison period)
- Classification filter
- Show details toggle
- Budget vs Actual toggle
- Export to Excel/PDF

#### `/finance/reports/balance-sheet`
- As-of date selector
- Classification filter
- Comparative periods
- Export to Excel/PDF

### Request/Response DTOs

```typescript
// GET /api/finance/statements/trial-balance?periodId=xxx&classification=IFRS
interface TrialBalanceRequestDto {
  periodId?: string;
  asOfDate?: string;
  classification?: string;
  includeZeroBalances?: boolean;
  accountTypes?: string[];
  segmentFilters?: Record<string, string>;  // For segmented COA
}

interface TrialBalanceReportDto {
  reportDate: string;
  periodName: string;
  classification: string;
  accounts: TrialBalanceLineDto[];
  totalDebits: number;
  totalCredits: number;
  isBalanced: boolean;
}

interface TrialBalanceLineDto {
  accountId: string;
  accountCode: string;
  accountNumber: string;
  accountName: string;
  accountType: string;
  debitBalance: number;
  creditBalance: number;
  netBalance: number;
  segmentValues?: Record<string, string>;
}

// GET /api/finance/statements/income-statement
interface IncomeStatementRequestDto {
  periodId?: string;
  startDate?: string;
  endDate?: string;
  classification?: string;
  comparePeriodId?: string;
  includeBudget?: boolean;
}

interface IncomeStatementReportDto {
  reportDate: string;
  periodName: string;
  classification: string;
  revenue: IncomeStatementSectionDto;
  costOfSales: IncomeStatementSectionDto;
  grossProfit: number;
  operatingExpenses: IncomeStatementSectionDto;
  operatingIncome: number;
  otherIncomeExpenses: IncomeStatementSectionDto;
  netIncome: number;
  // Comparison columns if requested
  comparisonPeriod?: string;
  comparisonNetIncome?: number;
  variance?: number;
  variancePercent?: number;
}

interface IncomeStatementSectionDto {
  sectionName: string;
  lines: IncomeStatementLineDto[];
  total: number;
}

interface IncomeStatementLineDto {
  accountCode: string;
  accountName: string;
  amount: number;
  comparisonAmount?: number;
  budgetAmount?: number;
  variance?: number;
}

// GET /api/finance/statements/balance-sheet
interface BalanceSheetRequestDto {
  asOfDate: string;
  classification?: string;
  compareAsOfDate?: string;
}

interface BalanceSheetReportDto {
  reportDate: string;
  asOfDate: string;
  classification: string;
  assets: BalanceSheetSectionDto;
  liabilities: BalanceSheetSectionDto;
  equity: BalanceSheetSectionDto;
  totalAssets: number;
  totalLiabilitiesAndEquity: number;
  isBalanced: boolean;
}

interface BalanceSheetSectionDto {
  sectionName: string;
  subSections: BalanceSheetSubSectionDto[];
  total: number;
}

interface BalanceSheetSubSectionDto {
  subSectionName: string;
  lines: BalanceSheetLineDto[];
  subtotal: number;
}

interface BalanceSheetLineDto {
  accountCode: string;
  accountName: string;
  balance: number;
  comparisonBalance?: number;
}
```

---

## 9. Currency Revaluation

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/finance/revaluation` | Run currency revaluation |
| `GET` | `/api/finance/revaluation/history` | Get revaluation history |
| `GET` | `/api/finance/revaluation/{id}` | Get revaluation details |

### UI Screen: `/finance/revaluation`

**Features:**
- Revaluation date selector (typically period end)
- Currency filter (specific or all)
- Preview mode toggle (generates report without posting)
- Unrealized Gain/Loss account selector
- Revaluation type: `Month-End` | `Year-End`
- Results showing:
  - Accounts affected
  - Original vs new base currency values
  - Gain/Loss per account
  - Total adjustment
- Run / Cancel buttons

### Request/Response DTOs

```typescript
// POST /api/finance/revaluation
interface RevaluationRequestDto {
  revaluationDate: string;     // Typically period-end date
  revaluationType: string;     // "Month-End" | "Year-End"
  currencyCode?: string;       // Specific currency or null for all
  unrealizedGainLossAccountId: string;  // GL account for gain/loss
  previewOnly: boolean;        // True = generate report only, don't post
  notes?: string;
}

// Response is a JournalEntry with revaluation details
interface RevaluationResultDto {
  journalEntry: JournalEntryDto;
  revaluationDetails: RevaluationDetailDto[];
  totalAdjustment: number;
  unrealizedGain: number;
  unrealizedLoss: number;
}

interface RevaluationDetailDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  currencyCode: string;
  foreignBalance: number;
  previousBaseBalance: number;
  previousRate: number;
  newRate: number;
  newBaseBalance: number;
  adjustment: number;
  adjustmentType: 'Gain' | 'Loss';
}
```

---

## 10. TypeScript Types

### Create file: `/src/types/finance.ts`

```typescript
// ============================================
// ENUMS
// ============================================

export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Revenue' | 'Expense';
export type AccountStatus = 'Active' | 'Inactive' | 'Closed';
export type JournalType = 'General' | 'Adjusting' | 'Reversing' | 'Recurring' | 'Opening Balance' | 'Closing' | 'Revaluation' | 'System Generated';
export type PostingStatus = 'Draft' | 'Pending Approval' | 'Approved' | 'Posted' | 'Rejected' | 'Reversed';
export type PeriodStatus = 'Future' | 'Open' | 'Closed' | 'Locked';
export type FiscalYearStatus = 'Future' | 'Open' | 'Closed' | 'Locked' | 'Archived';
export type ExchangeRateType = 'Daily' | 'Average' | 'MonthEnd' | 'QuarterEnd' | 'YearEnd' | 'Budget' | 'Fixed' | 'Spot';
export type RevaluationFrequency = 'Monthly' | 'Quarterly' | 'Annually' | 'AdHoc' | 'None';
export type COAType = 'Standard' | 'Segmented';

// ============================================
// CORE ENTITIES
// ============================================

export interface Currency {
  id: string;
  currencyCode: string;
  numericCode: string;
  currencyName: string;
  currencySymbol?: string;
  decimalPlaces: number;
  roundingMethod: string;
  roundingPrecision: number;
  symbolPosition: string;
  decimalSeparator: string;
  thousandsSeparator?: string;
  countryCode?: string;
  countryName?: string;
  isActive: boolean;
  isBaseCurrency: boolean;
  hasTransactionHistory: boolean;
  transactionCount: number;
}

export interface ExchangeRate {
  id: string;
  baseCurrencyCode: string;
  targetCurrencyCode: string;
  rate: number;
  inverseRate: number;
  effectiveDate: string;
  endDate?: string;
  rateType: ExchangeRateType;
  rateSource: string;
  isActive: boolean;
  isManualEntry: boolean;
  hasBeenUsedInTransactions: boolean;
  transactionCount: number;
  comments?: string;
}

export interface FiscalYear {
  id: string;
  fiscalYearName: string;
  fiscalYearCode: string;
  year: number;
  startDate: string;
  endDate: string;
  fiscalYearType: string;
  numberOfPeriods: number;
  status: FiscalYearStatus;
  isActive: boolean;
  isLocked: boolean;
  isClosed: boolean;
  baseCurrency: string;
  totalJournalEntries: number;
  totalDebits: number;
  totalCredits: number;
  fiscalPeriods?: FiscalPeriod[];
}

export interface FiscalPeriod {
  id: string;
  fiscalYearId: string;
  periodName: string;
  periodCode: string;
  periodNumber: number;
  periodType: string;
  startDate: string;
  endDate: string;
  periodDays: number;
  periodStatus: PeriodStatus;
  isOpen: boolean;
  isLocked: boolean;
  isClosed: boolean;
  isYearEnd: boolean;
  totalJournalEntries: number;
  totalDebits: number;
  totalCredits: number;
  // Close checklist
  trialBalanceValidated: boolean;
  bankReconciliationComplete: boolean;
  currencyRevaluationComplete: boolean;
  depreciationComplete: boolean;
  accrualsComplete: boolean;
}

export interface Account {
  id: string;
  accountCode: string;
  accountNumber: string;
  accountName: string;
  accountType: AccountType;
  accountCategory?: string;
  accountSubCategory?: string;
  description?: string;
  parentAccountId?: string;
  isSegmented: boolean;
  currencyCode: string;
  isMultiCurrency: boolean;
  isIFRSClassified: boolean;
  isBaseClassified: boolean;
  isLocalClassified: boolean;
  ifrsLineItem?: string;
  baseLineItem?: string;
  localLineItem?: string;
  allowDirectPosting: boolean;
  isControlAccount: boolean;
  budgetTrackingEnabled: boolean;
  status: AccountStatus;
  balance: number;
  debitBalance: number;
  creditBalance: number;
  openingBalance: number;
  lastTransactionDate?: string;
  isSystemAccount: boolean;
  // Relations
  segmentValues?: AccountSegmentValue[];
  currencyLinks?: AccountCurrencyLink[];
  childAccounts?: Account[];
}

export interface AccountSegmentValue {
  id: string;
  accountId: string;
  segmentStructureId: string;
  segmentPosition: number;
  segmentValue: string;
  segmentCode: string;
  segmentName: string;
}

export interface AccountCurrencyLink {
  id: string;
  accountId: string;
  linkedCurrencyCode: string;
  revaluationRequired: boolean;
  revaluationFrequency: RevaluationFrequency;
  transactionRateType: string;
  revaluationRateType: string;
  isActive: boolean;
  effectiveDate: string;
  effectiveEndDate?: string;
  hasTransactionHistory: boolean;
  transactionCount: number;
  foreignCurrencyBalance: number;
  baseCurrencyEquivalent: number;
  currentExchangeRate: number;
  lastRevaluationDate?: string;
  lastRevaluationAdjustment: number;
  cumulativeRevaluationAdjustment: number;
}

export interface SegmentStructure {
  id: string;
  segmentName: string;
  segmentCode: string;
  segmentPosition: number;
  segmentLength: number;
  dataType: string;
  separatorCharacter?: string;
  lookupTableRequired: boolean;
  isMandatory: boolean;
  isReportingDimension: boolean;
  isNaturalAccount: boolean;
  isActive: boolean;
  description?: string;
  lookupValues?: SegmentLookupValue[];
}

export interface SegmentLookupValue {
  id: string;
  segmentStructureId: string;
  segmentValue: string;
  description: string;
  effectiveDate: string;
  expirationDate?: string;
  isActive: boolean;
  displayOrder: number;
}

export interface JournalEntry {
  id: string;
  journalEntryNumber: string;
  journalType: JournalType;
  entryDate: string;
  description: string;
  referenceNumber?: string;
  sourceModule?: string;
  sourceDocumentId?: string;
  sourceDocumentType?: string;
  totalDebitAmount: number;
  totalCreditAmount: number;
  balanceDifference: number;
  isBalanced: boolean;
  isMultiCurrency: boolean;
  primaryCurrency?: string;
  bookClassification: string;
  fiscalPeriodId: string;
  postingDate?: string;
  postingStatus: PostingStatus;
  requiresApproval: boolean;
  approvalStatus?: string;
  isReversed: boolean;
  reversalDate?: string;
  reversalJournalEntryId?: string;
  originalJournalEntryId?: string;
  isRevaluationEntry: boolean;
  revaluationBatchNumber?: string;
  revaluationType?: string;
  notes?: string;
  transactions: JournalEntryLine[];
}

export interface JournalEntryLine {
  id: string;
  accountId: string;
  journalEntryId: string;
  lineNumber: number;
  description?: string;
  debitAmount: number;
  creditAmount: number;
  transactionCurrency?: string;
  foreignCurrencyAmount?: number;
  exchangeRate?: number;
  transactionDate: string;
  isRevaluationEntry: boolean;
  revaluationType?: string;
  // Denormalized for display
  accountCode?: string;
  accountName?: string;
}

// ============================================
// API RESPONSE TYPES
// ============================================

export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ApiResponse<T> {
  data?: T;
  success: boolean;
  message?: string;
  errors?: string[];
}
```

---

## Navigation Structure

Add to sidebar configuration:

```typescript
// Finance Module Navigation
{
  title: "Finance",
  icon: "DollarSign",
  children: [
    {
      title: "Dashboard",
      path: "/finance/dashboard",
      icon: "LayoutDashboard"
    },
    {
      title: "Settings",
      path: "/finance/settings",
      icon: "Settings",
      children: [
        { title: "General Settings", path: "/finance/settings" },
        { title: "Segment Configuration", path: "/finance/settings/segments" }  // Only if Segmented COA
      ]
    },
    {
      title: "Master Data",
      icon: "Database",
      children: [
        { title: "Currencies", path: "/finance/currencies" },
        { title: "Exchange Rates", path: "/finance/exchange-rates" },
        { title: "Fiscal Periods", path: "/finance/fiscal-periods" },
        { title: "Chart of Accounts", path: "/finance/chart-of-accounts" }
      ]
    },
    {
      title: "Transactions",
      icon: "FileText",
      children: [
        { title: "Journal Entries", path: "/finance/journal-entries" },
        { title: "Currency Revaluation", path: "/finance/revaluation" }
      ]
    },
    {
      title: "Reports",
      icon: "BarChart",
      children: [
        { title: "Trial Balance", path: "/finance/reports/trial-balance" },
        { title: "Income Statement", path: "/finance/reports/income-statement" },
        { title: "Balance Sheet", path: "/finance/reports/balance-sheet" },
        { title: "Cash Flow", path: "/finance/reports/cash-flow" }
      ]
    }
  ]
}
```

---

## Implementation Priority

| Priority | Screen | Complexity | Dependencies |
|----------|--------|------------|--------------|
| 1 | `/finance/settings` | Low | None - needed first for COA type |
| 2 | `/finance/currencies` | Low | Settings |
| 3 | `/finance/settings/segments` | Medium | Settings (if Segmented) |
| 4 | `/finance/fiscal-periods` | Medium | None |
| 5 | `/finance/chart-of-accounts` | High | Currencies, Segments (if applicable) |
| 6 | `/finance/exchange-rates` | Low | Currencies |
| 7 | `/finance/journal-entries` | High | Accounts, Periods |
| 8 | `/finance/reports/trial-balance` | Medium | Accounts, Periods |
| 9 | `/finance/reports/income-statement` | Medium | Trial Balance working |
| 10 | `/finance/reports/balance-sheet` | Medium | Trial Balance working |
| 11 | `/finance/revaluation` | Medium | Exchange Rates, Accounts |

---

## Notes for AI Implementation

1. **Follow existing patterns** from `/src/app/administration/maintenance/` for CRUD screens
2. **Use `apiService`** from `/src/services/api.service.ts` for all API calls
3. **Use shadcn/ui components** consistently with the rest of the application
4. **Create service file** `/src/services/finance.service.ts` to wrap all finance API calls
5. **Handle loading/error states** using the patterns in existing maintenance pages
6. **COA Type lock**: Check if accounts exist before allowing COA type change
7. **Currency link protection**: Show warning when removing currency with transactions
8. **Period validation**: Show checklist modal before closing periods
9. **Journal entry balance validation**: Disable Post button until balanced
