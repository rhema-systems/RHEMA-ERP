# Product Requirements Document: Unit Accounts Feature

## RhemaERP Finance Module — Phase 1 (MVP)

**Document Version:** 1.0  
**Date:** December 2024  
**Author:** Akwasi / Adullam Solutions  
**Module:** Finance Module  

---

## 1. Overview

### 1.1 Purpose

Unit Accounts is a feature that enables tracking of non-financial, quantity-based metrics alongside financial data in the general ledger. This allows organizations to measure operational quantities (headcount, square footage, production units, machine hours, etc.) and use them for ratio analysis and KPI reporting.

### 1.2 Business Value

- Enables powerful financial ratio analysis directly from the ERP (e.g., revenue per employee, cost per square foot)
- Provides operational visibility alongside financial data
- Supports management reporting and KPI dashboards
- Differentiates RhemaERP from competitors that lack this capability

### 1.3 Inspiration

This feature is modeled after Unit Accounts in Microsoft Dynamics GP, adapted for RhemaERP's Clean Architecture patterns and workflow system.

---

## 2. Functional Requirements

### 2.1 Unit Type Management

Users must be able to define custom unit types that describe what is being measured.

**Entity: UnitType**

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| Code | string (max 20) | Unique identifier code (e.g., "EMP", "SQFT", "HRS") |
| Name | string (max 100) | Display name (e.g., "Employees", "Square Footage", "Machine Hours") |
| Description | string (max 500) | Optional detailed description |
| DecimalPlaces | int (0-6) | Precision for unit quantities (default: 2) |
| IsActive | bool | Soft delete / deactivation flag |
| CreatedAt | DateTime | Audit field |
| CreatedBy | string | Audit field |
| ModifiedAt | DateTime? | Audit field |
| ModifiedBy | string? | Audit field |

**Business Rules:**
- Code must be unique across all unit types
- Code should be uppercase, alphanumeric, no spaces
- Cannot delete a unit type that has associated unit accounts
- Can deactivate unit types to prevent new usage while preserving history

**User Stories:**
- As a finance administrator, I want to create custom unit types so that I can track metrics specific to my organization
- As a finance administrator, I want to deactivate unit types that are no longer needed without losing historical data

---

### 2.2 Unit Account Register

Unit accounts live in a separate register (not the main Chart of Accounts) but follow a similar hierarchical structure.

**Entity: UnitAccount**

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| AccountNumber | string (max 20) | Unique account number |
| Name | string (max 100) | Account name |
| Description | string (max 500) | Optional description |
| UnitTypeId | Guid (FK) | Reference to UnitType |
| ParentAccountId | Guid? (FK) | Self-referencing for hierarchy |
| AccountLevel | int | Depth in hierarchy (1 = top level) |
| IsPostingAccount | bool | True = accepts postings; False = summary/parent only |
| IsActive | bool | Soft delete / deactivation flag |
| CreatedAt | DateTime | Audit field |
| CreatedBy | string | Audit field |
| ModifiedAt | DateTime? | Audit field |
| ModifiedBy | string? | Audit field |

**Business Rules:**
- AccountNumber must be unique across unit accounts
- A unit account can only have one unit type
- Parent accounts must exist before child accounts can reference them
- Cannot post to non-posting (summary) accounts
- Cannot delete accounts with transaction history; must deactivate instead
- Cannot change UnitTypeId if transactions exist

**User Stories:**
- As a finance administrator, I want to create a hierarchical structure of unit accounts for organized reporting
- As a finance user, I want to clearly distinguish between posting accounts and summary accounts

---

### 2.3 Unit Journal Entry

Unit quantities are posted through journal entries, similar to financial GL journal entries. These entries participate in the approval workflow system.

**Entity: UnitJournalEntry**

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| EntryNumber | string (max 20) | System-generated unique entry number |
| EntryDate | DateTime | Date of the entry |
| FiscalYearId | Guid (FK) | Reference to fiscal year |
| FiscalPeriodId | Guid (FK) | Reference to fiscal period |
| Description | string (max 500) | Entry description/memo |
| Status | enum | Draft, PendingApproval, Approved, Rejected, Posted, Reversed |
| SourceDocument | string? (max 100) | Optional reference to source document |
| CreatedAt | DateTime | Audit field |
| CreatedBy | string | Audit field |
| ModifiedAt | DateTime? | Audit field |
| ModifiedBy | string? | Audit field |
| ApprovedAt | DateTime? | Workflow audit |
| ApprovedBy | string? | Workflow audit |
| PostedAt | DateTime? | Posting audit |
| PostedBy | string? | Posting audit |

**Entity: UnitJournalEntryLine**

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| UnitJournalEntryId | Guid (FK) | Parent entry reference |
| LineNumber | int | Sequence number |
| UnitAccountId | Guid (FK) | Reference to unit account |
| Quantity | decimal | The unit quantity (positive or negative) |
| Description | string? (max 250) | Line-level description |

**Business Rules:**
- Entry must have at least one line
- Can only post to active, posting-level unit accounts
- Cannot modify entries with status Posted or Reversed
- EntryDate must fall within an open fiscal period
- FiscalPeriodId is derived from EntryDate based on fiscal calendar
- Quantities can be positive (increase) or negative (decrease)
- Entry number format: "UJE-{YYYY}-{sequence}" (e.g., UJE-2024-00001)

**Workflow States:**

```
Draft → PendingApproval → Approved → Posted
                ↓
            Rejected → Draft (revision)

Posted → Reversed (creates reversing entry)
```

**User Stories:**
- As a finance user, I want to post unit quantities through journal entries with full audit trail
- As a finance manager, I want to approve unit journal entries before they are posted
- As a finance user, I want to reverse posted entries when corrections are needed

---

### 2.4 Unit Account Balances

The system maintains period balances for efficient reporting.

**Entity: UnitAccountBalance**

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| UnitAccountId | Guid (FK) | Reference to unit account |
| FiscalYearId | Guid (FK) | Reference to fiscal year |
| FiscalPeriodId | Guid (FK) | Reference to fiscal period |
| OpeningBalance | decimal | Balance at period start |
| PeriodActivity | decimal | Net change during period |
| ClosingBalance | decimal | Balance at period end (Opening + Activity) |

**Business Rules:**
- One record per account per period
- Balances are updated when journal entries are posted
- Closing balance of one period = Opening balance of next period
- Year-end processing may reset balances or carry forward based on configuration

---

### 2.5 Ratio Definitions

Users can define custom ratio formulas that combine financial and unit account data.

**Entity: RatioDefinition**

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| Code | string (max 20) | Unique ratio code |
| Name | string (max 100) | Display name (e.g., "Revenue Per Employee") |
| Description | string (max 500) | Explanation of the ratio |
| NumeratorType | enum | FinancialAccount, UnitAccount, Constant |
| NumeratorAccountId | Guid? | FK to GL Account or Unit Account |
| NumeratorConstant | decimal? | Constant value if type is Constant |
| DenominatorType | enum | FinancialAccount, UnitAccount, Constant |
| DenominatorAccountId | Guid? | FK to GL Account or Unit Account |
| DenominatorConstant | decimal? | Constant value if type is Constant |
| ResultFormat | enum | Decimal, Percentage, Currency |
| DecimalPlaces | int | Display precision |
| IsActive | bool | Soft delete flag |
| CreatedAt | DateTime | Audit field |
| CreatedBy | string | Audit field |
| ModifiedAt | DateTime? | Audit field |
| ModifiedBy | string? | Audit field |

**Business Rules:**
- Code must be unique
- Either AccountId or Constant must be provided based on Type
- Denominator cannot be zero (handle in calculation logic)
- Can reference summary accounts (system aggregates child accounts)

**Example Ratios:**
- Revenue Per Employee = Revenue Account (Financial) ÷ Employee Count (Unit)
- Cost Per Square Foot = Total Costs (Financial) ÷ Square Footage (Unit)
- Units Produced Per Hour = Production Units (Unit) ÷ Machine Hours (Unit)

**User Stories:**
- As a finance manager, I want to define custom KPI ratios combining financial and unit data
- As a finance user, I want to calculate ratios for any period or date range

---

### 2.6 Reporting Requirements

**2.6.1 Unit Account Reports**

| Report | Description |
|--------|-------------|
| Unit Account Listing | List of all unit accounts with hierarchy, type, and status |
| Unit Account Balance Report | Period-end balances for selected accounts and date range |
| Unit Account Activity Report | Transaction detail for selected accounts and date range |
| Unit Journal Entry Register | List of journal entries with filtering by status, date, account |

**2.6.2 Ratio Reports**

| Report | Description |
|--------|-------------|
| Ratio Analysis Report | Calculated ratios for selected definitions and periods |
| Ratio Trend Report | Ratio values over multiple periods for trend analysis |
| KPI Dashboard Data | Structured data output for dashboard consumption |

**Report Parameters (common):**
- Date range (From/To) or Fiscal Period selection
- Account selection (single, multiple, or all)
- Include inactive accounts (yes/no)
- Output format (screen, PDF, Excel export)

---

## 3. Non-Functional Requirements

### 3.1 Architecture

This feature must follow the established Clean Architecture patterns used in RhemaERP Finance Module:

```
├── RhemaERP.Finance.Domain
│   └── Entities
│       ├── UnitType.cs
│       ├── UnitAccount.cs
│       ├── UnitJournalEntry.cs
│       ├── UnitJournalEntryLine.cs
│       ├── UnitAccountBalance.cs
│       └── RatioDefinition.cs
│
├── RhemaERP.Finance.Application
│   ├── Features
│   │   ├── UnitTypes
│   │   │   ├── Commands (Create, Update, Delete, Activate, Deactivate)
│   │   │   └── Queries (GetById, GetAll, GetActive)
│   │   ├── UnitAccounts
│   │   │   ├── Commands (Create, Update, Delete, Activate, Deactivate)
│   │   │   └── Queries (GetById, GetAll, GetHierarchy, GetByUnitType)
│   │   ├── UnitJournalEntries
│   │   │   ├── Commands (Create, Update, Delete, Submit, Approve, Reject, Post, Reverse)
│   │   │   └── Queries (GetById, GetAll, GetByStatus, GetByAccount, GetByPeriod)
│   │   ├── UnitAccountBalances
│   │   │   └── Queries (GetByAccount, GetByPeriod, GetBalanceSheet)
│   │   └── RatioDefinitions
│   │       ├── Commands (Create, Update, Delete, Activate, Deactivate)
│   │       └── Queries (GetById, GetAll, CalculateRatio, CalculateRatioTrend)
│   ├── Interfaces
│   │   ├── IUnitTypeRepository.cs
│   │   ├── IUnitAccountRepository.cs
│   │   ├── IUnitJournalEntryRepository.cs
│   │   ├── IUnitAccountBalanceRepository.cs
│   │   └── IRatioDefinitionRepository.cs
│   └── Services
│       ├── UnitAccountBalanceService.cs
│       └── RatioCalculationService.cs
│
├── RhemaERP.Finance.Infrastructure
│   └── Persistence
│       ├── Configurations (EF Core configurations)
│       └── Repositories (Repository implementations)
│
└── RhemaERP.Finance.API
    └── Controllers
        ├── UnitTypesController.cs
        ├── UnitAccountsController.cs
        ├── UnitJournalEntriesController.cs
        └── RatioDefinitionsController.cs
```

### 3.2 Integration Points

| Integration | Description |
|-------------|-------------|
| Fiscal Calendar | Unit entries use the same fiscal year/period structure as financial entries |
| Approval Workflow | Unit journal entries participate in the existing workflow system |
| GL Accounts | Ratio definitions can reference financial GL accounts for calculations |
| User/Security | Standard RhemaERP authentication and role-based authorization |

### 3.3 Database Considerations

- Use appropriate indexes on frequently queried columns (AccountNumber, EntryDate, Status, FiscalPeriodId)
- Consider partitioning UnitJournalEntryLine and UnitAccountBalance tables for large datasets
- Implement soft deletes to preserve audit history
- Use decimal(18,6) for quantity fields to support high precision

### 3.4 Performance Requirements

- Unit account listing should load within 2 seconds for up to 1,000 accounts
- Balance calculations should complete within 5 seconds for a full fiscal year
- Ratio calculations should support real-time dashboard updates

### 3.5 Security Requirements

- Role-based access control for:
  - Unit Type management (Admin only)
  - Unit Account management (Admin, Finance Manager)
  - Journal Entry creation (Finance User, Finance Manager)
  - Journal Entry approval (Finance Manager only)
  - Journal Entry posting (Finance Manager only)
  - Ratio Definition management (Admin, Finance Manager)
  - Report viewing (All finance roles)
- All changes must be audit logged

---

## 4. API Endpoints

### 4.1 Unit Types

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/unit-types | Get all unit types |
| GET | /api/unit-types/{id} | Get unit type by ID |
| POST | /api/unit-types | Create unit type |
| PUT | /api/unit-types/{id} | Update unit type |
| DELETE | /api/unit-types/{id} | Delete unit type (if no dependencies) |
| PATCH | /api/unit-types/{id}/activate | Activate unit type |
| PATCH | /api/unit-types/{id}/deactivate | Deactivate unit type |

### 4.2 Unit Accounts

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/unit-accounts | Get all unit accounts |
| GET | /api/unit-accounts/{id} | Get unit account by ID |
| GET | /api/unit-accounts/hierarchy | Get accounts in hierarchical structure |
| GET | /api/unit-accounts/by-type/{unitTypeId} | Get accounts by unit type |
| POST | /api/unit-accounts | Create unit account |
| PUT | /api/unit-accounts/{id} | Update unit account |
| DELETE | /api/unit-accounts/{id} | Delete unit account (if no transactions) |
| PATCH | /api/unit-accounts/{id}/activate | Activate unit account |
| PATCH | /api/unit-accounts/{id}/deactivate | Deactivate unit account |
| GET | /api/unit-accounts/{id}/balance | Get current balance |
| GET | /api/unit-accounts/{id}/balance/{periodId} | Get balance for specific period |
| GET | /api/unit-accounts/{id}/transactions | Get transaction history |

### 4.3 Unit Journal Entries

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/unit-journal-entries | Get all entries (with filtering) |
| GET | /api/unit-journal-entries/{id} | Get entry by ID |
| POST | /api/unit-journal-entries | Create entry (Draft status) |
| PUT | /api/unit-journal-entries/{id} | Update draft entry |
| DELETE | /api/unit-journal-entries/{id} | Delete draft entry |
| POST | /api/unit-journal-entries/{id}/submit | Submit for approval |
| POST | /api/unit-journal-entries/{id}/approve | Approve entry |
| POST | /api/unit-journal-entries/{id}/reject | Reject entry (with reason) |
| POST | /api/unit-journal-entries/{id}/post | Post approved entry |
| POST | /api/unit-journal-entries/{id}/reverse | Reverse posted entry |

### 4.4 Ratio Definitions

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/ratio-definitions | Get all ratio definitions |
| GET | /api/ratio-definitions/{id} | Get ratio by ID |
| POST | /api/ratio-definitions | Create ratio definition |
| PUT | /api/ratio-definitions/{id} | Update ratio definition |
| DELETE | /api/ratio-definitions/{id} | Delete ratio definition |
| PATCH | /api/ratio-definitions/{id}/activate | Activate ratio |
| PATCH | /api/ratio-definitions/{id}/deactivate | Deactivate ratio |
| GET | /api/ratio-definitions/{id}/calculate | Calculate ratio for period |
| GET | /api/ratio-definitions/{id}/trend | Calculate ratio trend over periods |

---

## 5. User Interface Requirements

### 5.1 Screens Required

| Screen | Description |
|--------|-------------|
| Unit Type List | Grid view of unit types with CRUD actions |
| Unit Type Form | Create/Edit form for unit types |
| Unit Account List | Hierarchical tree/grid view of unit accounts |
| Unit Account Form | Create/Edit form with parent selection and unit type dropdown |
| Unit Journal Entry List | Grid with status filters, date range, search |
| Unit Journal Entry Form | Header/Lines entry form similar to GL journal entry |
| Unit Journal Entry Approval | Queue view for pending approvals |
| Ratio Definition List | Grid view of defined ratios |
| Ratio Definition Form | Create/Edit with account pickers and formula preview |
| Ratio Calculator | Interactive ratio calculation with period selection |
| Unit Balance Inquiry | Account balance lookup with drill-down to transactions |

### 5.2 UI/UX Considerations

- Consistent with existing RhemaERP Finance Module UI patterns
- Support keyboard navigation for efficient data entry
- Real-time validation on forms
- Confirmation dialogs for destructive actions
- Toast notifications for success/error feedback
- Loading indicators for async operations

---

## 6. Testing Requirements

### 6.1 Unit Tests

- Domain entity validation rules
- Business rule enforcement
- Balance calculation logic
- Ratio calculation logic

### 6.2 Integration Tests

- Repository operations
- API endpoint responses
- Workflow state transitions
- Fiscal period validation

### 6.3 Test Scenarios

| Scenario | Expected Result |
|----------|-----------------|
| Create unit type with duplicate code | Validation error |
| Post entry to inactive account | Validation error |
| Post entry to summary account | Validation error |
| Post entry to closed period | Validation error |
| Delete unit type with accounts | Validation error |
| Approve entry without permission | Authorization error |
| Calculate ratio with zero denominator | Handle gracefully (return null or infinity indicator) |
| Reverse already reversed entry | Validation error |

---

## 7. Migration & Data Seeding

### 7.1 Database Migrations

Create EF Core migrations for all new entities with:
- Appropriate indexes
- Foreign key constraints
- Default values where applicable

### 7.2 Seed Data

Provide optional seed data for common unit types:

```csharp
new UnitType { Code = "EMP", Name = "Employees", DecimalPlaces = 0 },
new UnitType { Code = "SQFT", Name = "Square Footage", DecimalPlaces = 2 },
new UnitType { Code = "HRS", Name = "Hours", DecimalPlaces = 2 },
new UnitType { Code = "UNITS", Name = "Units", DecimalPlaces = 0 },
new UnitType { Code = "KG", Name = "Kilograms", DecimalPlaces = 3 },
new UnitType { Code = "LTR", Name = "Litres", DecimalPlaces = 3 }
```

---

## 8. Future Considerations (Post-MVP)

The following items are explicitly out of scope for Phase 1 but documented for future phases:

- **Automatic unit capture:** Integration with other modules (HR, Production) to auto-post unit quantities
- **Budget vs Actual:** Unit account budgeting and variance analysis
- **Allocations:** Distribute unit quantities across cost centers
- **Multi-company:** Unit accounts across multiple companies
- **Import/Export:** Bulk import of unit transactions from external sources
- **Advanced formulas:** Complex ratio formulas with multiple operands and functions
- **Dashboard widgets:** Pre-built KPI widgets for dashboards
- **Alerts/Notifications:** Threshold-based alerts when ratios exceed limits

---

## 9. Acceptance Criteria

The Unit Accounts feature is considered complete when:

1. ✅ Users can create, edit, and manage custom unit types
2. ✅ Users can create a hierarchical chart of unit accounts
3. ✅ Users can post unit quantities via journal entries
4. ✅ Journal entries follow the approval workflow before posting
5. ✅ Posted entries update account balances correctly
6. ✅ Users can reverse posted entries
7. ✅ Users can define custom ratio formulas
8. ✅ Ratios calculate correctly using financial and unit account data
9. ✅ All required reports are functional
10. ✅ API endpoints return correct responses
11. ✅ Role-based security is enforced
12. ✅ All audit fields are populated correctly
13. ✅ Unit tests achieve minimum 80% code coverage

---

## 10. Glossary

| Term | Definition |
|------|------------|
| Unit Account | A non-financial account that tracks quantities rather than currency amounts |
| Unit Type | A classification defining what a unit account measures (e.g., employees, square feet) |
| Posting Account | An account that can receive direct journal entry postings |
| Summary Account | A parent account that aggregates child account balances; cannot receive direct postings |
| Ratio | A calculated metric that divides one value by another (numerator ÷ denominator) |
| Unit Journal Entry | A transaction document that records unit quantities to unit accounts |

---

## Appendix A: Sample Workflow

**Scenario: Tracking Monthly Employee Headcount**

1. Admin creates Unit Type: Code="EMP", Name="Employees", DecimalPlaces=0
2. Admin creates Unit Account: Number="U-1000", Name="Total Employees", Type=EMP, IsPosting=true
3. At month-end, Finance User creates Unit Journal Entry:
   - Date: 2024-12-31
   - Description: "December 2024 Headcount"
   - Line: Account=U-1000, Quantity=45
4. Finance User submits entry for approval
5. Finance Manager reviews and approves
6. Finance Manager posts the entry
7. System updates UnitAccountBalance for December 2024
8. Admin creates Ratio: "Revenue Per Employee" = Revenue Account ÷ U-1000
9. Finance User runs Ratio Analysis Report for December 2024
10. Report shows: Revenue $450,000 ÷ 45 Employees = $10,000 per employee

---

*End of Document*
