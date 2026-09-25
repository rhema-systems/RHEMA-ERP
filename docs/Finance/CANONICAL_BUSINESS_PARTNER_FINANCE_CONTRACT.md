# Canonical Business Partner Finance Contract

## Decision

`BusinessPartner.Id` is the only forward counterparty identity consumed by Finance. A supplier,
contractor and customer are roles of that identity; they are not separate master records.

The legacy Procurement `Supplier`, Sales `Customer`, `ApSupplierIdentityLink`, and projection
service remain migration targets only until the approved Finance reset. New Finance contracts must
not introduce another ID bridge or compatibility alias.

## Role model

- One partner can hold Supplier, Contractor and Customer roles simultaneously.
- A role can be made inactive for new documents without removing identity or blocking settlement,
  reversal, credit, audit or reporting of existing transactions.
- `PartnerType` is transitional source data only. New behavior must use `BusinessPartnerRole`.
- `PartnerCode` is the permanent shared identity code. AP/AR reference numbers are optional role
  profile attributes, not alternate identities.

## Finance profile ownership

AP and AR defaults are effective-dated, governed versions. Draft, submitted, approved, rejected
and superseded states are retained for audit. The document accounting date—not the current date—
selects the one approved effective profile.

Only Finance-authorized users maintain Finance profile fields. Approval uses an independent
maker-checker. Transactions must snapshot the profile version and material counterparty evidence
used when they leave draft.

## Account authority

The tenant Finance settings are the only authority for AP and AR control accounts. In particular:

- `BusinessPartner.DefaultApAccountId` and `Supplier.DefaultApAccountId` do not select the AP
  control account for a Finance posting.
- `BusinessPartner.DefaultArAccountId` and legacy customer account fields do not select the AR
  control account for a Finance posting.
- the selected Finance cheque book/bank account owns the cash/bank GL account.
- approved AP profile defaults may supply expense, tax group, payment term and WHT treatment, but
  never the AP control account.

Procurement's existing Accounts-tab fields remain visible for its owner to rationalize later.
Fields without an implemented Finance consumer have no posting effect. Their continued presence
must not be interpreted as a promise that they override Finance settings.

## Transaction identity evidence

- New Finance AP, AR and subledger-adjustment commands accept `BusinessPartnerId`; they do not
  accept legacy `SupplierId` or `CustomerId` identity aliases.
- A posting resolves and stores the exact active role plus the approved AP or AR profile version
  effective on its accounting date. A partner identity without governed role/profile readiness is
  rejected with the policy's corrective readiness code and message.
- Posted transactions retain immutable partner code, display name, legal name and TIN snapshots.
  Reports group by `BusinessPartnerId` while displaying those historical snapshots, so later master
  data edits do not rewrite accounting evidence.
- Reversals reuse the original canonical partner and role, then revalidate the profile effective on
  the reversal date. They never create or look up a legacy Supplier/Customer shadow record.
- AR receipts use `BusinessPartnerId`, the selected Customer `BusinessPartnerRoleId`, and the
  approved `BusinessPartnerArProfileVersionId` effective on the receipt date. The receipt stores
  immutable partner code, name, legal name and TIN snapshots; allocation, banking settlement,
  controlled-document, tax and reporting paths consume that evidence rather than a parallel
  Finance Customer identity.
- Specialized customer-advance and AR-withholding opening balances use the same governed Customer
  role/profile resolution. A cutover row cannot bypass canonical readiness merely because it is
  classified as opening evidence.
- AR invoices use `BusinessPartnerId`, the selected Customer role and the approved AR profile
  effective on invoice date. They freeze the role/profile IDs plus partner code, display name,
  legal name and TIN. Posting revalidates that captured evidence and takes the AR control account
  only from tenant Finance settings; Procurement-owned partner account fields have no posting
  effect.
- Tax-calculation requests carry one `BusinessPartnerId` and an explicit `BusinessPartnerRole`.
  Threshold and customer-type rules no longer infer identity from mutually exclusive legacy
  `SupplierId`/`CustomerId` fields.
- Tax-report filters follow the same contract. Source documents and payments expose one canonical
  `BusinessPartnerId`; `BusinessPartnerRole` distinguishes Supplier, Contractor and Customer use.
  Tax reporting never validates or resolves a legacy Finance Supplier/Customer master.
- AP aging, unapplied settlements, supplier statements, detailed ledgers, WHT summaries and
  match-exception reports expose canonical `BusinessPartnerId` fields. “Supplier” remains a UI and
  accounting-role label only; report filters and grouping never fall back to a Supplier master ID.
- AR aging, unapplied receipts, customer statements, detailed ledgers, collections and sales
  summaries likewise expose canonical `BusinessPartnerId` fields. Historical reports resolve the
  governed Customer role even when that role is inactive for new business; they do not use the
  legacy `PartnerType` string as accounting identity.
- Finance purchase-order commands, landed-cost invoice charges and returned-cheque cases expose
  `BusinessPartnerId` at the Finance API boundary. Procurement-owned source records may retain
  domain labels such as `SupplierId`, but Finance must not translate those values through or create
  a parallel Supplier/Customer identity.

## Withholding contract

- Supplier/Contractor AP profiles may hold several category-specific WHT defaults and exactly one
  active default for AP.
- WHT applicability defaults from the approved partner profile effective on invoice accounting
  date.
- A WHT-applicable partner requires a TIN and an active approved WHT configuration before invoice
  submission.
- `Finance.AP.OverrideWithholding` permits a two-way invoice override. A reason is mandatory; a
  configuration/rate change also requires approval.
- One WHT configuration applies to one invoice in the current design.
- Opening-balance invoices do not automatically create WHT.
- AP records gross liability at invoice posting. WHT is recognized proportionally at payment and
  cumulative recognition must never exceed the invoice snapshot.
- The separate AR profile only marks the customer as a withholding agent. The receipt owns actual
  withheld amount and certificate evidence.

## Readiness and error behavior

Selectors keep incomplete partners visible but disabled and expose the exact readiness reason.
Examples include `AP_PROFILE_REQUIRED`, `AP_WHT_TIN_REQUIRED`, `AP_WHT_DEFAULT_REQUIRED`, and
`AR_PROFILE_REQUIRED`. Submission repeats the same server-side validation and cannot rely on UI
filtering.

## Migration and reset rule

No migration or reset is applied merely because it exists in source control. Local development,
rehearsal and UAT are separate gated executions. Each requires a backup, verified restore,
mapping/readiness manifest, retained configuration counts, deletion counts, and explicit user
review. The complete boundary is recorded in the Finance coordination ledger.
