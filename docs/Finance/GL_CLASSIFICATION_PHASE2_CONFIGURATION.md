# Configurable GL classifications — Phase 2

Status: Finance implementation baseline

## Authority model

`AccountingBook` identifies the accounting basis. `AccountClassification` is a tenant-owned,
book-specific presentation hierarchy. `AccountAccountingBook` is the authoritative assignment of a
GL account to one book and one classification.

Administrators manage classifications at **Finance → Settings → Account Classifications**. The UI
loads books and classifications from the API; no IFRS/GAAP classification list is embedded in the
browser. Account create/edit asks for the invariant core account type, then requires an active,
compatible posting leaf for every enabled book.

## Lifecycle and controls

- Codes are normalized to uppercase and unique inside a tenant/book.
- A parent and child must share the same book and core account type.
- Posting classifications cannot have children; account assignment additionally verifies the node
  has no children.
- Active children require an active parent, and hierarchy cycles are rejected.
- Once any account mapping uses a classification, its book, code and core type are immutable.
- A node used by an enabled mapping cannot become Draft, Retired or non-posting.
- Retirement requires a reason and is blocked by enabled mappings or non-retired children.
- Existing edits require the original row version. Create, update and retirement emit Finance audit
  evidence with before/after values and the actor context.
- Where-used returns both enabled and historical account/book mappings; historical usage remains
  visible after a mapping is disabled.

## Seed manifest 2.0

The deterministic manifest seeds the canonical `IFRS`, `LOCAL_STATUTORY`, and `MANAGEMENT` books.
Each receives non-posting roots for Asset, Liability, Equity, Revenue and Expense, with stable posting
leaves below them. Existing Phase 1 stable leaf codes are retained. Added report-oriented stable codes
include `REVENUE_DEDUCTIONS`, `OTHER_INCOME`, `COST_OF_SALES`, `OTHER_EXPENSE`, and `TAX_EXPENSE`.

The default revaluation treatment remains `Exclude`; only reviewed monetary families such as Cash,
Bank, ReceivableControl, PayableControl, accrued liabilities and debt are seeded as `Include`.

`SystemRole` is restricted to the approved behavioural vocabulary. Asset-only and liability-only
roles are validated by the service. It is not a substitute for report headings or exact control-account
settings.

## Finance consumers

- Balance-sheet presentation uses the configured parent and leaf names for the selected book.
- Income-statement grouping uses stable classification codes, never editable display captions.
- Cash-flow and cash-account selection use bank-master evidence or configured `Cash`/`Bank` roles;
  account-name and legacy-category guessing is prohibited.
- Dashboard labels use the default enabled book mapping.
- The chart-of-accounts ad-hoc dataset reads the default book classification mapping.

The legacy `AccountCategory` and `AccountSubCategory` columns remain only because V1 and external
producer conversion is deliberately deferred. New Finance UI writes do not populate them and no
Finance runtime classification decision depends on them. They should be removed only with the
coordinated V1 producer gate and clean-development reset.

## Deferred work

Published report-layout mapping snapshots are Phase 3. Currency-level revaluation overrides and
revaluation arithmetic are Phase 4. Segment enforcement and journal/account-inquiry UX remain in
their approved later phases.
