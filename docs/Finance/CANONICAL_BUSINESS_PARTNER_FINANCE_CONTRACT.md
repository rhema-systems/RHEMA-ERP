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
