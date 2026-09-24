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

`PHASE_3_FINANCE_AP_AR_CONVERSION_IN_PROGRESS`

On 24 September 2026 the product owner authorized execution of every remaining implementation,
verification, migration/reseed-tooling and ordered local/rehearsal/UAT step through completion.
That authorization does not waive the control evidence below: each destructive environment step
must still resolve the exact database target, create and restore-verify a backup, produce the
readiness/deletion manifest and pass its environment gate. Unknown targets must never be guessed.

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
- AP invoice creation now accepts only canonical `BusinessPartnerId`, resolves an active
  Supplier/Contractor role and the approved AP profile effective on the invoice date, and stores
  immutable partner, role and profile evidence. It never creates a parallel Supplier identity.
- Estate, Quantity Survey, landed-cost and Finance purchase-order AP producers now pass canonical
  Business Partner identity. Estate no longer manufactures shadow Supplier rows for payables;
  missing governed AP setup fails with a corrective configuration message.
- Focused canonical AP-invoice regressions: 3 passed, 0 failed (identity snapshot, effective-date
  fail-closed behavior, and explicit dual-role selection).
- Migration `CanonicalVendorInvoiceBusinessPartnerIdentity` generated and inspected; not applied.
  It refuses to run while `VendorInvoice` contains data, preventing a legacy Supplier GUID from
  being silently reinterpreted as a Business Partner GUID before the verified Finance reset.
- Release API build: passed, zero errors (existing repository warnings remain).
- Vendor payments now use canonical `BusinessPartnerId`, governed role/profile references and
  immutable code/name/legal-name/TIN snapshots. Batch-created payments inherit the exact
  canonical evidence frozen on their source invoices.
- Supplier debit notes now use their Business Partner (`VendorId`) plus governed role/profile
  references and immutable identity snapshots. Payment application ownership compares canonical
  Business Partner identity; it no longer compares a legacy Supplier GUID with a Business Partner
  GUID. Inventory-return credits also use the canonical partner directly rather than the identity
  bridge.
- Opening AP balances, AP reports, WHT certificate/calculation flows, payment vouchers, workflow
  display, audit reporting and settlement read models consume the canonical payment identity.
- Migration `CanonicalApSettlementBusinessPartnerIdentity` generated and inspected; not applied.
  It refuses to run while either `VendorPayment` or `SupplierDebitNotes` contains data, enforcing
  the approved verified Finance-reset prerequisite instead of reinterpreting legacy identifiers.
- Release API build: passed, zero errors. Default test-project build: passed, zero errors.
- Focused canonical AP settlement regressions: 14 passed, 0 failed (canonical invoice identity,
  payment batch creation/reservation, and supplier debit-note model mapping). EF reports no
  pending model changes and `git diff --check` reports no whitespace errors.
- Broader legacy Finance fixtures still expose pre-existing accounting-book setup assumptions and
  old Supplier-based WHT fixtures. They are not being hidden with compatibility aliases; fixture
  conversion and bridge removal remain part of Phase 3.
- WHT certificates, calculation requests, remittance liabilities and remittance lines now expose
  `BusinessPartnerId` end to end in the entity model, API DTOs, service filters and frontend
  contracts. The old `SupplierId` column labels no longer conceal canonical Business Partner
  values.
- Migration `CanonicalWhtBusinessPartnerIdentity` generated and inspected; not applied. It refuses
  to run while either WHT certificate or remittance-line data exists, preserving the verified
  Finance-reset gate before the columns are renamed.
- WHT lifecycle and controlled-certificate fixtures now use canonical Business Partners, active
  Supplier roles, approved AP profiles/defaults and a valid Primary-book lineage. Focused WHT
  regressions: 9 passed, 0 failed. EF reports no pending model changes.
- The repository-wide frontend type-check remains red on pre-existing unrelated Inventory,
  Procurement, Civil Engineering and stale Next generated-type errors; no WHT contract error was
  reported from the files changed in this slice.
- The transitional `ApSupplierIdentityLink` entity, service, controller, DI registration and AP
  runtime lookups have been removed. Invoice/payment supplier selection and the Finance AP
  supplier register now project canonical active Supplier/Contractor roles and their effective
  AP-profile readiness directly; incomplete partners remain visible with a corrective reason and
  cannot start an AP transaction.
- The DEBUG GL integration fixture now provisions a canonical Business Partner Supplier role and
  approved AP profile instead of constructing a legacy Supplier-linked invoice.
- Migration `DropApSupplierIdentityBridge` is intentionally limited to dropping the obsolete
  `ApSupplierIdentityLinks` table; its rollback recreates only that table and its constraints. It
  has not been applied. The migration designer and current model snapshot contain no bridge entity,
  and EF reports no pending model changes.
- Focused bridge-retirement regressions: 30 passed, 0 failed across canonical invoice identity,
  payment batching, supplier debit-note architecture and WHT lifecycle. API/test builds pass with
  zero errors (existing repository warnings remain); `git diff --check` is clean.
- Frontend type-check still reports the pre-existing Next App Router named-export error on the AP
  invoice page plus unrelated repository baseline errors. No new contract/type error was reported
  for the bridge-removal changes in the AP service/types, debit-note form, payment page or supplier
  register.
- No database or accounting data was mutated.
- Subledger adjustment journals now accept one canonical `BusinessPartnerId`, resolve the exact
  active Customer or Supplier/Contractor role plus the approved effective AR/AP profile, and retain
  role/profile IDs and immutable partner identity snapshots. The runtime no longer accepts or
  resolves separate `CustomerId`/`SupplierId` fields and does not create legacy Supplier shadows.
- AP/AR aging and detailed-ledger projections now group subledger adjustments by canonical Business
  Partner identity and display the transaction snapshot name. The creation UI uses the canonical AP
  readiness selector and refuses disabled/incomplete role options.
- Migration `CanonicalSubledgerAdjustmentBusinessPartnerIdentity` has been generated but not
  applied. It explicitly refuses to run while any subledger adjustment row exists; after the
  approved Finance reset it drops the legacy identity columns and creates clean canonical partner,
  role, profile and snapshot columns without GUID reinterpretation.
- The focused canonical subledger architecture/migration regression passes (1/1). The complete
  `LegacyPostingPathLockdownTests` class is 12/13: its one failure is the pre-existing
  `MigrationOnlyCommand_ShouldPreserveAspNetCoreProductionDefault` source assertion, unrelated to
  this slice. API and test-project compilation pass with zero errors; existing warnings remain.
- Repository-wide frontend type-check remains red on the documented baseline (including the AP
  invoice App Router export and unrelated Inventory/Procurement/Civil Engineering fixtures). After
  correcting the nullable AP currency option, it reports no error in the changed subledger page or
  Finance type contract.
- The rebuilt Data and API assemblies compile the guarded migration with zero errors, and EF reports
  no model changes pending after `CanonicalSubledgerAdjustmentBusinessPartnerIdentity`.
- AR receipts now accept `BusinessPartnerId` plus an optional explicit Customer role, resolve the
  approved AR profile effective on the receipt date, and freeze the role/profile IDs and immutable
  partner code/name/legal-name/TIN evidence. Receipt allocation, banking settlement, controlled
  documents, tax reports, AR reports, subledger posting, Estate and fixed-asset producers now use
  the canonical identity.
- The legacy startup SQL that recreated `FK_CustomerPayment_BusinessPartners_CustomerId` has been
  removed. Migration `CanonicalCustomerPaymentBusinessPartnerIdentity` is generated but not
  applied; it refuses any non-empty `CustomerPayment` table and creates clean canonical identity
  columns after the approved Finance reset rather than reinterpreting an old GUID as a role ID.
- Customer-advance and AR-withholding opening balances now resolve and snapshot the governed
  Customer role/profile too. Frontend receipt and opening-balance commands send
  `businessPartnerId`; no legacy receipt `customerId` API alias remains.
- API and default API-test-project builds pass with zero errors. The two focused canonical receipt
  architecture/model tests pass (2/2), and the AR receipt posting suite is 28/30: the remaining two
  failures are its pre-existing foreign-exchange snapshot-direction mismatch, reached only after
  canonical role/profile readiness passed. EF reports no pending model changes.
- Repository-wide frontend type-check remains red on its documented baseline; filtering the output
  to the changed AR receipt, AR service/type and opening-balance files returns no errors. The Core
  test project remains uncompilable on pre-existing legacy Supplier fixtures removed by the earlier
  AP bridge cutover; this receipt slice adds no Core-project compile failure.
- No migration or database reset has been executed in this checkpoint.
- AR invoice commands and query filters now expose `BusinessPartnerId`, not `CustomerId`. Creation
  resolves the Customer role and approved AR profile effective on invoice date, and the invoice
  freezes the selected role/profile plus immutable partner identity snapshots. Posting revalidates
  that evidence and uses only the Finance-controlled tenant AR control account.
- Migration `CanonicalInvoiceBusinessPartnerEvidence` is fail-closed and remains unapplied. It
  refuses a non-empty `Invoices` table because legacy rows cannot be assigned trustworthy governed
  Customer role/profile evidence during an automated migration.
- Customer shortcuts, invoice creation/filtering and receipt links use the canonical browser/API
  field. The repository-wide frontend type-check retains its unrelated baseline failures; filtering
  to the changed AR invoice/customer files returns no errors.
- API and API-test-project builds pass with zero errors. The focused canonical-invoice architecture
  test passes, 37/37 AR invoice regressions unrelated to the known FX-direction defect pass, and EF
  reports no pending model changes. The excluded foreign-opening invoice test remains the previously
  documented approved-rate snapshot-direction mismatch.
- Tax-calculation commands now use one canonical `BusinessPartnerId` plus an explicit
  `BusinessPartnerRole`; AP, AR and debit-note producers no longer send separate supplier/customer
  identity fields into the Finance tax engine.
