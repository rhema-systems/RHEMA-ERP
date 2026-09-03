# Phase 4: book-scoped FX revaluation policy

## Authority model

Closing revaluation is governed by the exact tuple `Tenant + AccountAccountingBook + AccountCurrencyLink`.
`AccountBookCurrencyPolicy.RevaluationOverride` has three meanings:

- `null`: inherit the selected book classification's `DefaultRevaluationTreatment`;
- `true`: explicitly include this account/book/currency exposure;
- `false`: explicitly exclude it.

The effective decision is calculated, never cached on the currency link. The service validates that the
account is active, its account/book mapping is enabled, the selected book is active and posting-enabled,
the classification is an active posting leaf in that same tenant/book, and the currency link is active.
The database permits only one non-deleted policy for the exact mapping/link tuple and uses row-version
optimistic concurrency.

The deterministic manifest defaults to `Exclude`. Only `CASH`, `BANK`, `RECEIVABLE_CONTROL`,
`PAYABLE_CONTROL`, `ACCRUED_LIABILITY`, and `DEBT` are seeded as `Include`. The seeder updates this
default only for untouched system-managed classifications; an administrator-edited row is authoritative.
No Asset/Liability or display-name inference is used.

## Override governance

`Finance.FX.Policy.Override` is required for every explicit decision, including returning to inherited
treatment. A reason and the current row version are required when a policy already exists. The mutation
and Finance audit event share a serializable transaction.

An explicit inclusion of Equity, Revenue, or Expense is non-standard. The UI shows an amber warning and
requires confirmation. The request remains `PendingApproval`; its pending value is not effective until a
different user with `Finance.FX.Policy.Approve` completes the repository's published
`AccountBookCurrencyPolicy` workflow. Rejection leaves the prior effective override unchanged. Request,
approval step, approval, and rejection events retain the account, exact book, currency, classification,
actor, timestamp, reason, and before/after values. Classification-level Include is rejected for these core
types so the heavier governance cannot be bypassed with a broad default.

## Revaluation execution and evidence

The caller must provide an exact active posting `AccountingBookCode`; the service does not select IFRS or
another default. Candidate exposure is built only from posted transaction lines carrying that exact book
code and an effective Include decision for the same account/currency/book. AR/AP source identity respects
the governed Finance control-account settings, while classification SystemRole provides the stable
behavioural-family identity; bank identity also uses the governed bank-to-GL link.

All core account types use one signed coordinate:

```text
signed foreign balance = transaction debit - transaction credit
signed carrying value  = functional debit - functional credit
target value            = signed foreign balance * closing rate
delta                   = target value - (signed carrying value + prior unreversed adjustment)
```

Normal balance affects labels, not arithmetic. Each line freezes exact book, classification ID/code/name,
core type, classification default, nullable override, effective source, warning, signed balances, prior
unreversed adjustment, and the approved closing-rate ID/value/date/type/quote side. The canonical SHA-256
preview fingerprint covers this evidence. A posting request carrying `ExpectedPreviewFingerprint` fails
closed if policy, mapping, classification, exposure, rate, or prior adjustment changed after preview.

Batches and prior adjustments are book-scoped. Posting and exact reversal use the V2 central posting
engine and the frozen `AccountingBookCode`; idempotency includes tenant, book, scope, date, and period.
History exposes a non-standard-policy count, preview displays the full warning, and period-close evidence
lists the affected account/classification/currency lines per posted book batch.

## Forward migration and reset

`20260903190453_AddBookScopedFxRevaluationPolicy` is additive except for retiring the ambiguous
`AccountCurrencyLinks.RevaluationRequired` Boolean. It creates normalized policy and frozen batch/line
evidence, exact-book keys and indexes, and the filtered uniqueness constraint. It remains unapplied.

Before dropping the old Boolean, the migration fails if:

1. existing non-deleted revaluation batches need an approved evidence backfill; or
2. an active currency link lacks a valid posting mapping, or its old account-wide Boolean cannot be
   represented unambiguously by every applicable book classification default.

For the approved pre-live clean reset, migrate an empty database and run the deterministic Finance seed.
For any database that must be retained, stop on this preflight and use a separately reviewed book-specific
backfill; do not infer one policy across books. No permanent purge or legacy compatibility schema is
required.

The migration does not apply itself, reseed a persistent database, or modify the historical generated SQL
reference snapshots.
