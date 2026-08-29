# Finance cash-flow methods

The Finance cash-flow report supports two distinct presentations over the same posted, tenant-scoped general ledger evidence.

## Direct method

The Direct method reports actual cash receipts and payments. A journal is eligible only when it touches a cash account, where cash accounts are resolved from active bank-account GL mappings plus the existing petty-cash and cash-equivalent classifications. Each non-cash counterpart must be classified as `Operating`, `Investing`, or `Financing` in Chart of Accounts.

Cash-flow classification is presentation metadata. It does not change the account type, posting rules, journal, or ledger balance.

## Indirect method

The Indirect method starts with ledger-derived profit for the reporting period and adjusts it for:

- changes in Operating-classified asset and liability accounts, using asset increases as cash outflows and liability increases as cash inflows; and
- a visible ledger-derived residual labelled `Other non-cash and classification adjustments` when the configured working-capital classifications do not fully explain the difference between profit and direct operating cash.

The residual is not hidden or guessed from account names. Its presence creates a presentation warning on screen and in PDF output. Finance should review working-capital classifications and non-cash journals before signing off a report that carries this warning.

The indirect operating total is reconciled to the Direct method’s operating cash total. Investing and financing sections remain actual cash movements under both methods.

## Fail-closed controls

The report refuses to generate when:

- the requested method is not `Direct` or `Indirect`;
- a non-cash counterpart in a cash-touching journal has no valid cash-flow classification; or
- a cash-touching journal references a counterpart account that is unavailable to the active tenant report.

These controls prevent a balanced GL from producing an incomplete cash-flow statement.

## Opening-balance cutover

A governed `OpeningBalanceBatch` cash leg posted on the first report date is included in beginning cash and excluded from operating, investing, and financing activity. Ordinary receipts posted on that same date remain period cash flow. This treatment is identical under Direct and Indirect methods.

## Operator checklist

1. Classify every account that can be the non-cash counterpart of a bank or cash posting.
2. Select the reporting dates, accounting book, and method.
3. Run the report and confirm beginning cash plus net movement equals ending cash.
4. For Indirect reports, resolve or formally review every presentation warning before sign-off.
5. Retain the selected method and any warnings in the exported PDF evidence.

No database migration is required: `Accounts.CashFlowClassification` already exists in the schema.
