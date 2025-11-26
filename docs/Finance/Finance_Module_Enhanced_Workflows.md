# 3.2 FINANCE MODULE - ENHANCED WORKFLOW DOCUMENTATION

## Overview

This document extends the Finance Module workflows with four critical enhancements designed to support complex financial operations and international accounting standards (IFRS, IAS 21).

### Core Features

1. **Advanced Segmented Account Structures** - Up to 20 segments with lookup validation and reporting dimension control
2. **Multi-Currency General Ledger Accounts** - Parallel currency tracking and historical protection
3. **Automated Currency Revaluation Processing** - Per IAS 21 standards with automatic reversal mechanism
4. **IFRS/Base/Local Classification** - Comprehensive reporting frameworks

*Each workflow follows international accounting standards and software development best practices to ensure data integrity, auditability, and regulatory compliance.*

---

## 3.2.3 ENHANCED SEGMENTED ACCOUNTS WORKFLOW

### Segment Structure Configuration

#### Initial Setup and Authorization

**Navigation:** Finance → Setup → Chart of Accounts → Segmented Account Configuration

**Authorization Check:**
- System validates user has `SegmentedAccounts_Admin` privileges
- System checks for existing GL accounts:
  - **If accounts exist:** System displays warning that changes will affect existing account structure
  - **If no accounts:** System proceeds to configuration

#### Segment Definition Process

**Segment Count:** Minimum 2, Maximum 20

**For each segment, define:**

| Property | Description | Options/Format |
|----------|-------------|----------------|
| **Segment Name** | Descriptive name | e.g., 'Company', 'Department', 'Account Type', 'Sub-Account', 'Cost Center', 'Project' |
| **Segment Code** | Unique identifier for system reference | e.g., 'SEG01', 'DEPT', 'ACCT' |
| **Segment Position** | Sequential order in account number | 1 to 20 |
| **Segment Length** | Fixed number of characters | 1-10 alphanumeric characters |
| **Data Type** | Character type | Alphanumeric (A-Z, 0-9) |
| **Separator Character** | Delimiter between segments | Dash (-), Dot (.), Underscore (_), or None |
| **Lookup Table Required** | Determines if values must exist in reference table | Yes/No |
| **Mandatory** | Determines if segment value required for all accounts | Yes/No |
| **Is Reporting Dimension** | **NEW** - Determines if segment appears in reports and BI tools | Yes/No |

#### Structure Validation

System performs comprehensive validation:

- ✓ Total account number length must not exceed 100 characters (including separators)
- ✓ Segment positions must be unique and sequential
- ✓ Segment codes must be unique within the chart of accounts
- ✓ At least one segment must be designated as the 'Natural Account' segment
- ✓ Natural Account segment must have lookup table enabled
- ✓ **At least one segment must be flagged as reporting dimension**

**Validation Results:**
- **If validation fails:** System displays specific error messages with remediation guidance
- **If validation passes:** System creates segment structure and generates `AccountSegmentStructure` table

---

### Reporting Dimension Configuration

> **NEW FEATURE:** This section enables administrators to control which segments appear as reporting dimensions in financial reports, dashboards, and BI tools, preventing report clutter while maintaining analytical flexibility.

#### Reporting Dimension Selection Strategy

**Example Scenario:**
- **Total segments:** 10 (Company, Department, Account Type, Sub-Account, Cost Center, Project, Product Line, Region, Division, Fund)
- **Key reporting needs:** Department, Cost Center, Project, Region
- **Solution:** Flag only Department, Cost Center, Project, and Region as 'Reporting Dimensions'

**Benefits:**
- Reports display only relevant dimension filters (not all 10 segments)
- Simplified user interface for report parameter selection
- Easier pivot table and dashboard configuration in Excel/Power BI
- Reduced complexity in drill-down analysis
- Better performance in BI queries (fewer dimension tables to join)

#### Reporting Dimension Activation Process

**During segment definition or modification:**

1. Administrator reviews each segment
2. For segments needed in reports, checks 'Is Reporting Dimension' checkbox
3. System validates at least one reporting dimension selected
4. System stores `IsReportingDimension` flag in `AccountSegmentStructure` table

#### Impact on Reporting Systems

**Trial Balance Reports:**
- System displays only reporting dimension segments in filter dropdown
- User selects: 'Group by Department' or 'Filter by Project'
- Non-reporting dimensions hidden from selection but remain in account detail view

**Income Statement and Balance Sheet:**
- Report parameters show: 'Report by: [Department] [Cost Center] [Project] [Region]'
- User can select single dimension or multi-dimensional view (e.g., Department × Project)
- System generates subtotals and cross-tabs based on selected reporting dimensions

**BI Tool Integration (Power BI, Tableau, Excel):**
- System exports only reporting dimension segments to BI dimension tables
- BI users see simplified dimension lists in pivot configuration
- Faster query performance due to reduced dimension complexity
- Excel pivot tables automatically include reporting dimensions as row/column options

#### Modifying Reporting Dimension Flags

**Navigation:** Finance → Setup → Segment Configuration

**Process:**
1. Select segment to modify
2. Toggle 'Is Reporting Dimension' flag

**Impact of Changes:**

| Action | Effect |
|--------|--------|
| **Adding Reporting Dimension** | Segment becomes immediately available in report filters and BI tools |
| **Removing Reporting Dimension** | System warns if segment is actively used in saved reports, requires confirmation |

- System logs all reporting dimension changes for audit purposes

---

### Segment Lookup Table Management

#### Lookup Table Creation

**For each segment with 'Lookup Table Required' = Yes:**

**Navigation:** Finance → Setup → Segment Lookup Values → [Segment Name]

1. System displays lookup value management interface
2. User clicks 'Add Lookup Value'

#### Lookup Value Entry

| Field | Description | Example |
|-------|-------------|---------|
| **Segment Value** | Code that will appear in account number (must match segment length and data type) | 'HR', '100', 'SALES' |
| **Description** | Full descriptive name | 'Human Resources Department', 'Sales Division' |
| **Parent Value** | Optional reference to parent segment value for hierarchical structures | 'CORPORATE' |
| **Effective Date** | Date from which value becomes available for use | 2025-01-01 |
| **Expiry Date** | Optional date after which value cannot be used for new accounts | 2025-12-31 |
| **Active Status** | Enabled/Disabled (affects availability for new accounts) | Enabled |

#### Lookup Value Validation and Storage

**System validates lookup value:**
- ✓ Segment value matches defined length and data type
- ✓ Segment value is unique within the segment
- ✓ Parent value exists if specified
- ✓ Effective date is not in the future if immediate use required

**On successful validation:**
- System stores value in `SegmentLookupValues` table
- System generates audit trail with creator, timestamp, and action

#### Segment Value Hierarchy (Optional)

For segments requiring hierarchical relationships:
1. User defines parent-child relationships between segment values
2. System validates that circular references do not exist
3. System creates `SegmentHierarchy` table to track relationships
4. System enables rollup reporting by parent segments

---

### Segmented Account Creation Workflow

#### Account Creation Initiation

**Navigation:** Finance → Chart of Accounts → Add Segmented Account

**Authorization:**
- System validates user has `Account_Create` privileges
- System retrieves configured segment structure from `AccountSegmentStructure` table
- System displays dynamic account creation form with fields for each segment

#### Segment Value Selection

**For each segment in sequential order:**

**If segment has lookup table:**
- System presents dropdown with active lookup values from `SegmentLookupValues` table
- System displays both code and description for user selection
- System filters values based on effective date and active status

**If segment has no lookup table:**
- System displays text input field with character limit based on segment length
- System validates input matches alphanumeric data type and fixed length

**Real-time Features:**
- System automatically formats account number with defined separators in real-time
- System displays preview of complete account number as segments are selected

#### Account Attributes Entry

After segment values are complete, user enters standard account attributes:

| Attribute | Description | Notes |
|-----------|-------------|-------|
| **Account Name** | Descriptive name combining segment descriptions | Auto-populated, user can override |
| **Account Type** | Asset, Liability, Equity, Revenue, Expense | Based on Natural Account segment |
| **Account Category** | Subcategory within account type | Required |
| **Currency Settings** | Base currency and multi-currency enablement | See Multi-Currency Workflow |
| **Classification** | IFRS/Base/Local (mandatory) | See Classification Workflow |
| **Posting Controls** | Allow direct posting, require project code, etc. | Optional |

#### Segmented Account Validation

**System performs comprehensive validation:**

- ✓ Complete account number (all segments combined) is unique across all GL accounts
- ✓ All mandatory segments have values
- ✓ All segment values match defined length and data type
- ✓ All segment values with lookup tables exist in `SegmentLookupValues` and are active
- ✓ Account type alignment with Natural Account segment classification
- ✓ Hierarchical relationships are valid if defined

**Validation Results:**

- **If validation fails:** System displays specific error messages identifying invalid segments
- **If validation passes:**
  - System creates account in `ChartOfAccounts` table
  - System creates segment detail records in `AccountSegmentValues` table
  - System generates audit trail with timestamp and user ID
  - System displays confirmation with complete account number

---

### Segment-Based Reporting and Analysis

#### Dynamic Segment Filtering (Reporting Dimensions Only)

All financial reports support segment-level filtering using **only reporting dimensions:**

- Users see only segments flagged as `Is Reporting Dimension = Yes`
- System supports multi-select for each reporting dimension
- System enables hierarchical rollup if segment hierarchy defined
- System saves reporting dimension filter combinations as user preferences
- **Example:** If only Department and Project are reporting dimensions, users only see these two options in report filters

#### Segment-Level Reporting

System provides segment analysis reports:

- **Trial Balance by Reporting Dimension** - Any dimension or combination
- **Income Statement by Department/Cost Center** - If flagged as reporting dimensions
- **Balance Sheet by Company/Division** - If flagged as reporting dimensions
- **Cross-dimension Analysis** - Variance reporting
- **Dimension Trend Analysis** - Over multiple periods

---

## 3.2.4 MULTI-CURRENCY GENERAL LEDGER ACCOUNTS WORKFLOW

### System Currency Configuration

#### Base Currency Setup

**Navigation:** Finance → Setup → Currency Configuration

**Authorization:** User must have `SystemConfiguration_Admin` privileges

**Administrator defines:**

| Setting | Description | Example |
|---------|-------------|---------|
| **Base/Home Currency** | Primary currency for the organization | GHS - Ghanaian Cedi |
| **Currency Code** | ISO 4217 three-letter code | GHS, USD, EUR, GBP |
| **Currency Symbol** | Display symbol (based on currency code selected) | GH₵, $, €, £ |
| **Decimal Precision** | Number of decimal places | Typically 2 |
| **Rounding Method** | Standard rounding, round up, round down | Standard |

> **⚠️ CRITICAL:** Base currency cannot be changed once transactions are posted

- System stores base currency in `SystemConfiguration` table

#### Foreign Currency Activation

**Navigation:** Finance → Setup → Foreign Currencies → Add Currency

**Process:**
1. Selects & confirms currency details (same attributes as base currency)
2. Defines initial exchange rate to base currency
3. Sets rate type (Daily, Average, Fixed)
4. Activates currency for use in transactions

**Validation:**
- System validates currency code against ISO 4217 standard
- System creates currency record in `CurrencyMaster` table

---

### Exchange Rate Management

#### Daily Rate Entry Workflow

**Navigation:** Finance → Currencies → Exchange Rates

**Authorization:** User must have `ExchangeRate_Maintain` privileges

**User selects rate entry method:**

| Method | Description |
|--------|-------------|
| **Manual Entry** | User enters rates for each currency |
| **Bulk Upload** | User uploads CSV/Excel file with multiple rates |
| **API Integration** | System fetches rates from external service (if configured) |

#### Rate Entry Details

For each exchange rate entry:

| Field | Description | Example |
|-------|-------------|---------|
| **From Currency** | Foreign currency code | USD |
| **To Currency** | Base currency (auto-populated) | GHS |
| **Rate Type** | Daily, Average, Month-End, Year-End, Budget | Daily |
| **Rate Date** | Effective date of exchange rate | 2025-11-25 |
| **Exchange Rate** | Rate expressed as 1 foreign currency unit = X base currency units | 1 USD = 15.25 GHS |
| **Inverse Rate** | System auto-calculates (1 base currency = Y foreign currency units) | 1 GHS = 0.0656 USD |
| **Rate Source** | Bank of Ghana, Bloomberg, Reuters, Manual, etc. | Bank of Ghana |

#### Rate Validation and Storage

**System validates exchange rate entry:**

- ✓ Rate date cannot be in the future
- ✓ Rate must be greater than zero
- ✓ System checks for duplicate rate (same currency, type, and date)
  - **If duplicate exists:** System prompts user to confirm rate update
- ✓ System performs reasonability check (warns if rate variance exceeds 10% from previous day - configurable deviation range)

**On successful validation:**
- System stores rate in `ExchangeRates` table
- System maintains complete history of exchange rate changes
- System creates audit trail with entry source and user ID

---

### Multi-Currency GL Account Setup

#### Multi-Currency Account Creation

1. User creates GL account following standard account creation workflow
2. In Currency Configuration section:
   - System displays base currency (GHS) as default
   - User enables 'Multi-Currency Account' checkbox
   - System displays list of available foreign currencies
   - User selects currencies to link to this account (multiple selection allowed)

#### Currency Linkage Configuration

For each selected foreign currency:

- System creates `AccountCurrencyLink` record
- System defines currency-specific attributes:

| Attribute | Options | Description |
|-----------|---------|-------------|
| **Linked Currency Code** | USD, EUR, GBP, etc. | Foreign currency identifier |
| **Revaluation Required** | Yes/No | See Currency Revaluation Workflow |
| **Revaluation Frequency** | Monthly, Quarterly, Annually | When to revalue |
| **Rate Type for Transactions** | Daily, Average | Which rate to use for transactions |
| **Rate Type for Revaluation** | Month-End, Year-End | Which rate to use for revaluation |

#### Multi-Currency Account Validation

**System validates multi-currency configuration:**

- ✓ Base currency must always be linked
- ✓ All selected currencies must be active in `CurrencyMaster`
- ✓ Exchange rates must exist for all selected currencies
- ✓ Account type must allow multi-currency (typically Balance Sheet accounts)
- ✓ **Recommended:** Cash, Bank, Receivables, Payables, and Loan accounts

**If validation passes:**
- System creates account with multi-currency flag enabled
- System creates currency linkage records
- System initializes currency balance tracking tables

---

### Currency Link Protection (Transaction History Rule)

> **NEW FEATURE:** This section prevents accidental removal of currency links that would corrupt historical transactions and foreign currency sub-ledgers.

#### Currency Link Modification Attempt

**User attempts to modify existing multi-currency account:**

**Navigation:** Chart of Accounts → Select Account → Edit

1. Clicks on Currency Configuration section
2. Attempts to uncheck/remove a linked currency

#### System Validation Process

**Before allowing currency link removal, system checks:**

**Step 1 - Transaction History Check:**
- System queries `CurrencyLedger` table for account and currency combination
- System checks for any posted transactions in that currency

**Step 2 - Balance Verification:**
- System queries `CurrencyAccountBalance` table
- System checks current foreign currency balance

**Step 3 - Pending Transaction Check:**
- System checks for draft/pending journal entries using this currency
- System checks for recurring transactions templates

#### Validation Outcomes

##### Scenario 1: No Transaction History (Allow Removal)

**If no transactions ever posted in this currency AND balance = 0:**

- System allows currency link removal
- System displays confirmation: *"Currency [USD] will be unlinked from this account. Continue?"*
- Upon confirmation, system deletes `AccountCurrencyLink` record
- System logs removal in audit trail

##### Scenario 2: Non-Zero Balance (Block Removal)

**If currency balance ≠ 0:**

- System blocks removal and displays error:

```
⛔ Cannot remove currency link: Account has non-zero balance of [1,000 USD]. 
Current balance must be zero before removing currency link.
```

- System provides balance details:
  - Foreign currency balance: 1,000 USD
  - Base currency equivalent: 15,250 GHS
  - Number of transactions in this currency: 15

##### Scenario 3: Zero Balance with Transaction History (Require Migration)

**If balance = 0 BUT transaction history exists:**

- System blocks removal and displays warning:

```
⚠️ Cannot remove currency link: Account has historical transactions in [USD]. 
Removing this link will break historical currency sub-ledger reports. 
To proceed, you must first migrate historical transactions.
```

- System provides two options:
  - **Option A - Mark Currency as Inactive:** Prevents new transactions but preserves history
  - **Option B - Data Migration Process:** Contact system administrator to migrate historical data

#### Currency Inactivation (Alternative to Removal)

**If user selects 'Mark Currency as Inactive':**

- System updates `AccountCurrencyLink.IsActive = False`
- System sets `EffectiveEndDate = Current Date`

**Effects of Inactivation:**
- ✓ Currency no longer available for new transactions
- ✓ Historical transactions remain intact and reportable
- ✓ Balance inquiry still shows currency (with 'Inactive' label)
- ✓ Currency reports include historical data
- ✓ Revaluation process skips inactive currencies

#### Data Migration Process (Administrator Only)

**If complete currency link removal is required:**

**Step 1 - Backup:**
- Administrator creates full database backup
- Administrator exports currency transaction history to archive

**Step 2 - Migration:**
- Administrator runs currency migration utility
- System migrates `CurrencyLedger` transactions to historical archive table
- System maintains referential integrity with base currency GL

**Step 3 - Removal:**
- After successful migration, system allows currency link deletion
- System creates detailed audit log of migration and removal

---

### Multi-Currency Transaction Processing

#### Foreign Currency Transaction Entry

1. User creates journal entry with multi-currency account
2. When account is selected:
   - System detects multi-currency account
   - System displays currency selection dropdown with linked currencies
   - **System shows only active currencies (`IsActive = True`)**
   - User selects transaction currency

#### Dual Amount Entry

System presents dual amount entry fields:

| Field | Description | Example |
|-------|-------------|---------|
| **Foreign Currency Amount** | Amount in transaction currency | 1,000 USD |
| **Exchange Rate** | System retrieves rate from `ExchangeRates` table based on transaction date | 15.25 |
| | User can override rate if authorized (requires `ExchangeRate_Override` privilege) | |
| **Base Currency Equivalent** | System auto-calculates | 1,000 USD × 15.25 = 15,250 GHS |

- System displays both amounts clearly on the entry screen

#### Transaction Posting and Storage

**Upon journal entry posting:**

**Base Currency Ledger (`GeneralLedger` table):**
- Stores base currency amount (15,250 GHS)

**Foreign Currency Sub-Ledger (`CurrencyLedger` table):**
- Stores transaction details:
  - Foreign currency code (USD)
  - Foreign currency amount (1,000 USD)
  - Exchange rate used (15.25)
  - Base currency equivalent (15,250 GHS)
  - Rate source and date
  - Link to GeneralLedger transaction ID

> **⚠️ CRITICAL:** Base currency GL always balances (Debits = Credits in GHS)

- Foreign currency sub-ledger maintains parallel tracking

#### Currency Balance Tracking

System maintains separate balance records:

| Balance Type | Example |
|--------------|---------|
| **Account Balance in Base Currency** | 15,250 GHS (from `GeneralLedger`) |
| **Account Balance in Each Foreign Currency** | 1,000 USD (from `CurrencyLedger`) |

- System updates `CurrencyAccountBalance` table after each transaction
- Balances tracked separately for each currency linked to the account

---

### Multi-Currency Reporting

#### Account Balance Inquiry

User selects account for balance inquiry

**System displays balance breakdown:**

```
Base Currency Total: 15,250 GHS

Foreign Currency Balances:
- USD: 1,000 USD (at rate 15.25 = 15,250 GHS)
- EUR: 500 EUR (at rate 17.50 = 8,750 GHS)

Total in Base Currency: 24,000 GHS (sum of all currency equivalents)
```

#### Currency-Specific Reports

System provides currency-focused reports:

| Report | Description |
|--------|-------------|
| **Foreign Currency Trial Balance** | Shows accounts with foreign currency balances |
| **Currency Exposure Report** | Summarizes total exposure by currency |
| **Exchange Gain/Loss Report** | Shows realized and unrealized gains/losses |
| **Account Activity by Currency** | Transaction detail in original currency |

- All reports display amounts in both foreign currency and base currency equivalent

---

## 3.2.5 CURRENCY REVALUATION WORKFLOW (IAS 21 COMPLIANT)

> This workflow implements currency revaluation per **IAS 21 - The Effects of Changes in Foreign Exchange Rates**. Revaluation adjusts foreign currency monetary assets and liabilities to reflect current exchange rates, recognizing unrealized gains and losses with automatic reversal of prior period adjustments.

### Revaluation Configuration

#### Revaluation Account Setup

**Navigation:** Finance → Setup → Revaluation Configuration

**Authorization:** User must have `CurrencyRevaluation_Admin` privileges

**Administrator configures revaluation accounts:**

| Account Type | Purpose | Example Account |
|--------------|---------|-----------------|
| **Unrealized Gain Account** | Income statement account | 7500-Foreign Exchange Gain |
| **Unrealized Loss Account** | Expense account | 8500-Foreign Exchange Loss |
| **Realized Gain Account** | Income statement account | 7510-Realized FX Gain |
| **Realized Loss Account** | Expense account | 8510-Realized FX Loss |
| **Revaluation Reserve Account** | Equity account for OCI items (optional, for available-for-sale securities) | 3500-Revaluation Reserve |

**Validation:**
- System validates accounts exist and are of correct account type
- System stores configuration in `RevaluationConfiguration` table

#### Revaluation Scope Definition

**Administrator defines which accounts require revaluation:**

| Account Category | Examples |
|------------------|----------|
| **Monetary Assets** | Cash, bank accounts, receivables, loans receivable |
| **Monetary Liabilities** | Payables, loans payable, bonds |
| **Exclusions** | Non-monetary items (inventory, PPE, equity investments carried at cost) |

- System uses 'Revaluation Required' flag in `AccountCurrencyLink` to identify accounts

#### Revaluation Frequency Setup

**Administrator sets revaluation schedule:**

| Frequency | Timing |
|-----------|--------|
| **Monthly** | End of each calendar month |
| **Quarterly** | End of fiscal quarters |
| **Annually** | Fiscal year-end |
| **Ad-hoc** | User-initiated as needed |

- System can automate revaluation posting at scheduled intervals

---

### Revaluation Process Execution

#### Revaluation Initiation

**Navigation:** Finance → Currency Management → Run Revaluation

**Authorization:** User must have `CurrencyRevaluation_Execute` privileges

**User enters revaluation parameters:**

| Parameter | Options | Description |
|-----------|---------|-------------|
| **Revaluation Date** | Date | As-of date for revaluation (typically period-end date) |
| **Revaluation Type** | Month-End, Quarter-End, Year-End, Ad-hoc | Purpose of revaluation |
| **Currency Selection** | All currencies or specific currency | Scope of revaluation |
| **Account Filter** | Optional filter by account range or segment | Limit accounts to revalue |

#### Exchange Rate Entry for Revaluation

System displays exchange rate entry screen for revaluation date

**For each active foreign currency:**

- System checks for existing Month-End/Year-End rate in `ExchangeRates` table
  - **If rate exists:** System auto-populates rate field, user can review
  - **If rate missing:** System prompts user to enter revaluation rate

**User Input:** User enters or confirms exchange rate for each currency

**System displays rate comparison:**
- Current Rate (period-end)
- Previous Rate (last revaluation)
- Rate Change (percentage and amount)

---

### Prior Period Revaluation Check and Reversal

> **NEW FEATURE:** Before calculating new revaluation adjustments, system automatically checks for and reverses prior period unrealized entries to prevent stacking of adjustments.

#### Prior Revaluation Detection

**System searches for previous revaluation entries:**

- Queries `RevaluationHistory` table for accounts in scope
- Identifies last revaluation date for each account-currency combination
- Retrieves prior revaluation batch numbers and journal entry IDs
- Checks if prior entries have been manually reversed or adjusted

#### Automatic Reversal Process

**For each account with prior revaluation:**

**Step 1 - Identify Reversal Amounts:**
- System retrieves previous unrealized gain/loss amount from `RevaluationHistory`
- Example: Previous month had unrealized gain of 500 GHS

**Step 2 - Create Reversal Entry:**
- System creates automatic reversal journal entry
- Entry date: First day of current revaluation period
- Entry type: Revaluation Reversal (system-generated)
- Reference: Links to original revaluation batch

**Step 3 - Reversal Entry Lines:**
- Exact opposite of prior revaluation entry:
  - If prior was DEBIT Asset, CREDIT Gain → Reversal is DEBIT Gain, CREDIT Asset
- Description: *"Reversal of prior period unrealized gain/loss on [Account] - [Currency]"*

#### Reversal Example (Detailed)

**Scenario:**

**Account:** 1010-Cash-USD

**Prior Month (January 31) Revaluation:**
- Foreign balance: 1,000 USD
- Rate: 15.25 → Base value: 15,250 GHS
- Unrealized gain recorded: 500 GHS
- Original entry: `DR 1010-Cash-USD 500 / CR 7500-FX Gain 500`

**Current Month (February 28) Revaluation Process:**

**Feb 1 - Automatic Reversal Entry:**
```
DR 7500-FX Gain         500
CR 1010-Cash-USD        500
Description: "Reversal of Jan 31 revaluation - REVAL-2025-01-001"
```

**Feb 28 - New Revaluation Entry:**
- Foreign balance: Still 1,000 USD (no transactions during Feb)
- New rate: 15.75
- Calculation: 1,000 USD × 15.75 = 15,750 GHS
- After reversal, base value was back to 14,750 GHS (15,250 - 500)
- New adjustment needed: 15,750 - 14,750 = 1,000 GHS gain
- Entry: `DR 1010-Cash-USD 1,000 / CR 7500-FX Gain 1,000`

**Result:**
- Net effect in February P&L: 1,000 GHS gain (not 1,500 stacked)
- Books are clean without redundant adjustments
- Cumulative revaluation effect properly tracked

#### Reversal Posting and Logging

**System posts reversal entries:**

- Updates `GeneralLedger` table with reversal amounts
- Creates `RevaluationReversal` table records linking to original revaluation
- Updates `RevaluationHistory` with reversal status
- Generates audit trail with reversal date, user, and reference
- Displays reversal summary to user before proceeding with new revaluation

---

### Current Period Revaluation Calculation

#### Account Balance Retrieval

**After reversals complete, system queries current balances:**

System queries `CurrencyAccountBalance` table for accounts marked for revaluation

**For each multi-currency account:**

| Balance Component | Example |
|-------------------|---------|
| **Foreign Currency Balance** | Current balance in foreign currency (e.g., 1,000 USD) |
| **Current Base Currency Balance** | Balance after prior reversal (e.g., 14,750 GHS) |
| **Current Period-End Rate** | User-entered rate (e.g., 15.75) |

#### Revaluation Calculation

**System performs IAS 21 calculation for each account and currency:**

**Step 1 - Calculate Required Base Currency Value:**
```
Foreign Currency Balance × Current Rate = Required Base Value
Example: 1,000 USD × 15.75 = 15,750 GHS
```

**Step 2 - Calculate Revaluation Adjustment:**
```
Revaluation Adjustment = Required Base Value - Current Base Balance
Example: 15,750 GHS - 14,750 GHS = 1,000 GHS (Gain)
```

**Step 3 - Determine Gain or Loss:**
- If Adjustment > 0: Unrealized Gain
- If Adjustment < 0: Unrealized Loss

#### Revaluation Preview and Validation

**System generates revaluation preview report showing:**

- Account number and name
- Foreign currency and balance
- Previous rate vs. current rate
- Prior period unrealized amount (reversed)
- Current base value vs. revalued base value
- New revaluation adjustment amount and type (gain/loss)

**System calculates summary totals:**
- Total Prior Period Reversals
- Total Current Period Unrealized Gains
- Total Current Period Unrealized Losses
- Net Revaluation Impact

**User reviews preview and can:**
- Export to Excel for detailed analysis
- Modify exchange rates if adjustments needed
- Exclude specific accounts from revaluation batch
- Proceed to post revaluation entries

---

### Automatic Revaluation Posting

#### Journal Entry Generation

**Upon user confirmation to post revaluation:**

System creates automatic journal entry:

| Entry Component | Details |
|-----------------|---------|
| **Entry Date** | Revaluation date (period-end date) |
| **Entry Type** | Revaluation Entry (system-generated) |
| **Entry Reference** | Revaluation Batch Number (e.g., REVAL-2025-03-001) |
| **Entry Description** | "Currency Revaluation as of [Date] per IAS 21" |

#### Journal Entry Lines - Unrealized Gains

**For accounts with positive revaluation adjustment (gains):**

```
DEBIT:   Asset or Liability Account (revalued account)
         Amount: Revaluation gain amount (e.g., 1,000 GHS)
         Description: "Revaluation gain on [Currency] balance"

CREDIT:  Unrealized Gain Account (7500-Foreign Exchange Gain)
         Amount: Revaluation gain amount (e.g., 1,000 GHS)
         Description: "Unrealized FX gain on [Account] - [Currency]"
```

#### Journal Entry Lines - Unrealized Losses

**For accounts with negative revaluation adjustment (losses):**

```
DEBIT:   Unrealized Loss Account (8500-Foreign Exchange Loss)
         Amount: Revaluation loss amount (absolute value)
         Description: "Unrealized FX loss on [Account] - [Currency]"

CREDIT:  Asset or Liability Account (revalued account)
         Amount: Revaluation loss amount (absolute value)
         Description: "Revaluation loss on [Currency] balance"
```

#### Entry Posting and Balance Update

**System posts revaluation journal entry:**

- Updates `GeneralLedger` table with base currency adjustments
- Updates `CurrencyAccountBalance` table with:
  - New revalued base currency balance
  - Current revaluation rate
  - Revaluation date and batch number
- **IMPORTANT:** Foreign currency balance remains unchanged (1,000 USD stays 1,000 USD)
- Creates revaluation history record in `RevaluationHistory` table
- Links revaluation entry to original account transactions

---

### Realized Gains and Losses Upon Settlement

> **NEW FEATURE:** When foreign currency items are settled, system automatically converts unrealized gains/losses to realized amounts.

#### Settlement Transaction Processing

**When foreign currency transaction is settled (payment/receipt):**

1. User posts settlement transaction (e.g., payment to supplier, receipt from customer)
2. System detects multi-currency account involved in settlement
3. System triggers realized gain/loss calculation routine

#### Realized Gain/Loss Calculation

**System calculates realized amount:**

**Step 1 - Identify Original Transaction:**
- System traces settlement back to original transaction (invoice, purchase order)
- Retrieves original exchange rate used

**Step 2 - Calculate Realized Gain/Loss:**
```
Realized Amount = (Foreign Currency Amount × Settlement Rate) - (Foreign Currency Amount × Original Rate)

Example:
- Original invoice: 1,000 USD at rate 15.25 = 15,250 GHS
- Settlement payment: 1,000 USD at rate 15.75 = 15,750 GHS
- Realized gain: 15,750 - 15,250 = 500 GHS
```

#### Unrealized to Realized Conversion

**System manages unrealized balance adjustment:**

**Step 1 - Check for Unrealized Amount:**
- System queries `RevaluationHistory` for settled foreign currency amount
- Calculates portion of unrealized gain/loss attributable to settled amount

**Step 2 - Reclassification Entry:**

System creates automatic reclassification entry:

```
DEBIT:   Unrealized Gain Account (reverse portion)
CREDIT:  Realized Gain Account
Description: "Reclassification of unrealized to realized gain on settlement"
```

**Step 3 - Foreign Currency Balance Reduction:**
- System reduces foreign currency balance in `CurrencyAccountBalance`
- Updates base currency balance accordingly
- Marks settled portion so it won't be revalued in future periods

#### Realized Gain/Loss Reporting

**System maintains separation between unrealized and realized:**

**Income Statement Presentation:**

| Line Item | Purpose |
|-----------|---------|
| **Unrealized FX Gain/Loss** | Shows period-end revaluation effects |
| **Realized FX Gain/Loss** | Shows actual settlement gains/losses |

- Reported separately in financial statements for transparency

**Audit Trail:**

System maintains detailed links between:
- Original transaction and exchange rate
- Revaluation entries (unrealized)
- Settlement transaction (realized)
- Reclassification entries

---

### Revaluation Reports and Analysis

#### Comprehensive Revaluation Reporting

**System provides enhanced revaluation reports:**

**1. Revaluation History Report:**
- Shows all revaluations and reversals over time
- Displays cumulative unrealized gain/loss trend
- Identifies realized portions upon settlement

**2. Unrealized vs Realized Comparison:**
- Side-by-side comparison by period
- Reconciliation of movements
- Impact on profit and loss

---

## 3.2.6 IFRS/BASE/LOCAL CLASSIFICATION WORKFLOW

> This workflow enables organizations to maintain multiple reporting frameworks simultaneously - International Financial Reporting Standards (IFRS), statutory/base reporting, and local/tax reporting - within a single general ledger structure.

### Classification Framework Setup

#### Classification Types Configuration

**Navigation:** Finance → Setup → Account Classification

**Authorization:** User must have `AccountingStandards_Admin` privileges

**System presents three classification types:**

### 1. IFRS (International Financial Reporting Standards)

**Purpose:** Accounts following IFRS for international consolidation and reporting

**Use Cases:**
- Multinational parent company reporting
- Investor relations
- International compliance

### 2. Base (Statutory/Primary Reporting)

**Purpose:** Primary reporting framework for the entity, typically follows local GAAP

### 3. Local (Tax/Management Reporting)

**Purpose:** Accounts specific to tax calculations or local management requirements

---

## Database Tables Reference

### Core Tables

| Table Name | Purpose |
|------------|---------|
| `AccountSegmentStructure` | Stores segment definitions and configuration |
| `SegmentLookupValues` | Contains valid values for segments with lookup tables |
| `SegmentHierarchy` | Tracks parent-child relationships between segment values |
| `AccountSegmentValues` | Links accounts to their segment values |
| `ChartOfAccounts` | Main GL account master table |
| `CurrencyMaster` | Active currencies in the system |
| `ExchangeRates` | Historical exchange rate data |
| `AccountCurrencyLink` | Links accounts to currencies they can transact in |
| `GeneralLedger` | Base currency transactions |
| `CurrencyLedger` | Foreign currency transaction details |
| `CurrencyAccountBalance` | Current balances by account and currency |
| `RevaluationConfiguration` | System-wide revaluation settings |
| `RevaluationHistory` | Historical revaluation entries |
| `RevaluationReversal` | Links reversals to original revaluation entries |

---

## User Privileges Reference

### Required Privileges

| Privilege | Grants Access To |
|-----------|------------------|
| `SegmentedAccounts_Admin` | Segment structure configuration |
| `Account_Create` | Creating new GL accounts |
| `SystemConfiguration_Admin` | Currency system setup |
| `ExchangeRate_Maintain` | Exchange rate entry and management |
| `ExchangeRate_Override` | Override system exchange rates |
| `CurrencyRevaluation_Admin` | Revaluation configuration |
| `CurrencyRevaluation_Execute` | Run revaluation process |
| `AccountingStandards_Admin` | Classification framework setup |

---

## Best Practices Summary

### Segmented Accounts
- ✓ Define clear naming conventions for segments before implementation
- ✓ Flag only essential segments as reporting dimensions (4-6 max)
- ✓ Use hierarchical segment structures for rollup reporting
- ✓ Test segment structure with sample accounts before going live

### Multi-Currency
- ✓ Never change base currency after posting transactions
- ✓ Always verify exchange rates before posting transactions
- ✓ Use API integration for daily rates to reduce manual entry errors
- ✓ Never remove currency links from accounts with transaction history
- ✓ Use currency inactivation instead of removal when possible

### Currency Revaluation
- ✓ Run revaluations at consistent intervals (monthly recommended)
- ✓ Always review revaluation preview before posting
- ✓ Ensure period-end exchange rates are entered before revaluation
- ✓ Monitor unrealized vs realized gain/loss separately
- ✓ Document revaluation methodology for auditors

### General
- ✓ Maintain comprehensive audit trails for all configuration changes
- ✓ Test workflows in a sandbox environment before production
- ✓ Train users on proper currency transaction entry procedures
- ✓ Regular reconciliation of foreign currency sub-ledgers to GL
- ✓ Implement maker-checker approval for critical transactions

---

## Compliance Notes

### IAS 21 Compliance

This implementation follows IAS 21 requirements:

- ✓ Monetary items revalued at closing rate
- ✓ Exchange differences recognized in profit or loss
- ✓ Automatic reversal of prior period revaluation
- ✓ Separate tracking of realized vs unrealized gains/losses
- ✓ Proper foreign currency transaction settlement recognition

### IFRS Compliance

- ✓ Support for multiple reporting frameworks
- ✓ Comprehensive audit trail for all transactions
- ✓ Proper classification and presentation
- ✓ Disclosure support through detailed reporting

---

*Document Version: 2.0*  
*Last Updated: For RHEMA ERP Finance Module*  
*Status: Enhanced with Advanced Features*
