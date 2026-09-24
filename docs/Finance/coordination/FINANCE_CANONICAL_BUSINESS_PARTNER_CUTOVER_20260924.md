# Canonical Business Partner Cutover

## Objective

Make `BusinessPartner` the only forward counterparty identity used by Finance, Sales and CRM.
Retire Finance AP `Supplier`, legacy Finance/Sales `Customer`, and the AP supplier identity-link
bridge from operational use without rewriting historical evidence. Because the application is
still in development, reset Finance transactions after the replacement implementation and reset
tooling have passed local verification.

## Approved product decisions

- A Business Partner has independently activatable Supplier, Contractor and Customer roles.
- One permanent `PartnerCode` identifies the partner across every role.
- AP and AR role profiles are governed, effective-dated versions using the existing workflow
  engine, independent maker-checker and Finance approval permissions.
- Document accounting date selects the effective approved profile. Transactions retain immutable
  partner/profile snapshots.
- Finance settings remain the sole authority for AP and AR control accounts. Procurement-owned
  posting-account fields remain unchanged for now and have no Finance control-account effect.
- The AP profile supports multiple category-specific WHT defaults and one default configuration.
  The invoice starts from the partner default, permits a permission-controlled two-way override
  with a required reason, and uses one WHT configuration per invoice.
- WHT is recognized proportionally at payment, subject to current approved thresholds and a
  cumulative cap. Opening-balance invoices do not auto-create WHT.
- AR has a distinct customer withholding-agent profile; receipts own actual withheld amounts and
  certificate evidence.
- Inactive roles block new activity but remain available for settlement, reversal, credit and
  historical reporting.
- Existing Sales/CRM customer records are mapped only through explicit references, verified links,
  or reviewed tenant-scoped registration/TIN evidence. Names, email addresses and phone numbers
  are never identity evidence on their own.
- Existing migration history is retained. Current API contracts replace `SupplierId`/`CustomerId`
  with `BusinessPartnerId`; compatibility aliases are not permitted.

## Reset boundary and escalation gate

- Environments: local development, rehearsal and UAT, in that order.
- Delete: Finance transactions and legacy Finance Supplier/Customer masters.
- Preserve: Business Partners, Sales/CRM data, users, other-module data, and verified/reseeded
  Finance configuration (chart, fiscal calendar, currencies, tax/WHT, exchange rates and settings).
- Before every execution: full backup, restore verification, migration/readiness manifest,
  retained configuration counts, deletion counts, mapping report and explicit user review.
- This package may author and test scripts and migrations. It must not apply a migration, reset a
  database, or mutate accounting data without a separate explicit authorization at the recorded
  gate.

## Repository state

- Branch: `codex/finance-accounting-book-model-v2`
- Exact base: `7b2d4855`
- Worktree: `RHEMA-ERP-finance-accounting-book-model-v2`
- Database state: unchanged; no migration applied.

## Ordered phases

1. **Architecture and dependency inventory** — document ownership, enumerate legacy identity
   dependencies, define schema/contracts and exact reset boundary.
2. **Canonical role/profile foundation** — Business Partner roles, AP/AR profile versions, WHT
   profile lines, permissions, workflow and readiness projections; migration remains unapplied.
3. **Finance AP/AR conversion** — replace current document and service identity with
   `BusinessPartnerId`; preserve immutable counterparty/profile evidence; remove identity bridge.
4. **Sales/CRM conversion** — replace legacy customer operational identity and produce guarded
   reviewed mappings for preserved data.
5. **Frontend conversion** — multi-role Business Partner UI, Finance-filtered AP/AR partner views,
   readiness explanations and WHT override controls.
6. **Reset/reseed tooling** — read-only manifest, backup/restore gates, Finance-only deletion,
   configuration verification and representative canonical demo seeds.
7. **Independent review and local rehearsal** — migration generation/build, focused/full tests,
   fresh database rehearsal and exact target report. Database execution remains a user gate.

## Current status

`PHASE_2_FOUNDATION_IMPLEMENTED_NOT_APPLIED`

## Phase 1 dependency findings

- Finance AP still keys invoices, payments, debit notes, WHT certificates, reports and document
  builders to the legacy Procurement `Supplier` master.
- `ApSupplierIdentityLink` and `ApSupplierIdentityService` are a projection bridge rather than a
  canonical identity and must be deleted after AP conversion.
- Procurement purchase orders already use `BusinessPartnerId`; the old `Supplier` master remains
  in Inventory, Procurement planning/reporting, Estate and Quantity Survey dependencies.
- AR is mixed: newer integrations use `BusinessPartnerId`, while Finance invoice/payment and
  Sales/CRM legacy records still expose `CustomerId`.
- Existing partner and supplier AP/AR account defaults currently compete with tenant Finance
  settings. The canonical profile schema intentionally contains no AP/AR control-account field.
- The normative developer contract is `docs/Finance/CANONICAL_BUSINESS_PARTNER_FINANCE_CONTRACT.md`.

## Verification ledger

- Core build: passed, zero errors (existing repository warnings remain).
- Data/EF model build: passed, zero errors (existing repository warnings remain).
- Focused profile-policy tests: 4 passed, 0 failed.
- Migration `CanonicalBusinessPartnerFinanceProfiles` generated and inspected; not applied.
- AP invoice and payment posting now resolve the AP control account exclusively from tenant
  Finance settings. Partner, legacy Supplier and document-level AP overrides are ignored.
- Focused AP control-authority regressions: 4 passed, 0 failed (invoice posting, payment posting,
  draft defaults and explicit-default preservation).
- No database or accounting data was mutated.
