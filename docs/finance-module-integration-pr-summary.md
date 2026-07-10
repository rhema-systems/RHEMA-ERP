# Finance module integration foundation

## Summary

This PR hardens the Finance module for dev/UAT integration. It centralizes normal operational posting through `IFinancePostingEngine`, adds posting back-references and audit coverage, hardens AP/AR settlement reporting, completes backend fixed asset lifecycle foundations, adds Ghana statutory tax reporting/export foundations, locks down legacy posting bypasses, and adds controlled opening-balance/migration sign-off tooling.

This is not production go-live approval. `FIN-LIM-0017` remains open until representative tenant dry-run and accountant-reviewed evidence are accepted.

## Developer Contract

- Use `IFinancePostingEngine` for AP, AR, cash/bank, tax, FX, fixed asset, opening-balance, and subledger operational postings.
- Use `JournalEntryService` only for controlled manual journal lifecycle.
- Do not call legacy direct GL/subledger posting paths.
- Persist returned `JournalEntryId` and `FinancePostingEventId` on source documents.
- Treat stored balances as read-side snapshots; posted GL is the accounting source of truth.
- Use tenant-scoped IDs only; cross-tenant source, account, bank, tax, segment, AP/AR, or fixed asset references must fail.

See `docs/finance-module-developer-integration-guide.md` for service/controller entry points and integration rules.

## Major Areas Included

- Finance posting engine and posting-event back-reference foundation
- AP invoice/payment posting migration
- AR invoice/receipt/credit-note posting migration
- Cash/bank transaction, reconciliation, and operational balance hardening
- FX functional currency, realized FX, and revaluation foundations
- Fixed asset acquisition, depreciation, revaluation/impairment, transfers, disposals, reports, and GL reconciliation
- AP/AR settlement read model and aging/control reconciliation
- Backend reporting/export/print audit and financial statement presentation hardening
- Ghana VAT/NHIL/GETFund, VAT withholding, WHT, certificate/reference, and tax reconciliation reporting
- Legacy posting path lockdown
- Workflow approval routing for high-risk Finance actions
- Controlled opening-balance posting and migration sign-off diagnostics
- UAT synthetic cutover smoke pack and AR schema alignment

## Migrations

This PR includes multiple Finance migrations through:

- `20260710100000_EnsureCurrentArInvoiceTables`

For existing UAT clones, apply migrations before running sign-off diagnostics. Do not apply migrations to production without an approved migration window and backup/rollback plan.

## Verification

Latest focused verification:

- Data build passed.
- API build passed.
- Test project build passed.
- Focused opening/sign-off tests passed `28/28`.
- Finance go-live regression slice passed `398/398`.
- Synthetic UAT smoke run `SIGNOFF-20260710010353-7bd1d05b` passed with accepted limitations, `0` blockers, and `1` workflow warning.

## Known Limitations

- `FIN-LIM-0017` remains open until real tenant cutover evidence and accountant sign-off exist.
- `FIN-LIM-0048` remains globally open unless the tenant cutover has no AP/AR/fixed-asset source-level openings or those openings are loaded through proper source-document/import paths.
- Accepted non-blocking limitations remain in `docs/finance-go-live-limitations-register.md`.

## Review Notes

Reviewers should focus on:

- posting-engine boundary and idempotency
- tenant isolation
- source document journal/posting-event references
- workflow/permission checks
- migration ordering
- report/export source-of-truth behavior
- limitations register accuracy
