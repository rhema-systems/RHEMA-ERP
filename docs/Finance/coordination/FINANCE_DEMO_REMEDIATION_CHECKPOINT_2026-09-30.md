# Finance demo remediation restart checkpoint — 30 September 2026

## Resume point

- Branch: `codex/finance-demo-remediation-20260929`
- Local checkpoint commit: `7dead8c56`
- Do not push or open a PR until the user completes the current manual-test pass.
- Resume by verifying the checkpoint changes before expanding scope.

## Implemented but not yet fully verified

1. Segment configuration UI now exposes activation for Draft segments and deletion for unused Draft segments, with a confirmation dialog.
2. Currency creation now uses `IUnitOfWork.ExecuteInTransactionAsync` instead of a user-initiated transaction that fails under `SqlServerRetryingExecutionStrategy`.
3. `git diff --check` passed before the checkpoint commit.

## Required verification on resume

1. Build the API and run focused currency-creation tests, including creation with and without an initial rate and rollback on invalid rate evidence.
2. Run targeted frontend type/lint/tests for the segment page.
3. Manually re-test currency creation against SQL Server.
4. Manually re-test Draft segment activation and deletion.
5. Design and implement the governed account-identity remediation required before a new mandatory segment can be activated when accounts already exist.

## Confirmed remediation queue

- CUR-01: prepopulate a structured Country/Region only where an ISO currency maps unambiguously; keep it editable.
- CUR-02: govern decimal places using ISO 4217 minor units and prevent unsafe changes after use.
- CUR-03: remove unused Display Order from the form or implement a real ordering contract; preferred UI order is functional currency first, then code.
- CUR-04: retry-safe atomic currency creation (checkpoint implementation present; tests pending).
- CUR-05: remove stale open-ended 15 December 2024 demo exchange rates from the active 2025/2026 scenario. Demo data must not claim Bank of Ghana evidence unless the source and reference are real.
- SEG-01: expose and verify Draft segment activation.
- SEG-02: expose and verify deletion of unused Draft segments.
- SEG-03: add a governed existing-account migration/backfill workflow before activating a new mandatory identity segment.
- TAX-01: Ghana VAT Act 2025 effective 1 January 2026 requires VAT 15%, NHIL 2.5%, and GETFund 2.5% on the same taxable base; remove obsolete tax-on-tax wording and repair persisted group components.
- TAX-02: retain tax categories only where they drive supported calculation, posting, and reporting behavior; review whether Excise is operationally implemented.
- TAX-03: seed and map separate output-tax payable and recoverable input-tax receivable/control accounts.
- TAX-04: the Recoverable flag is operational in AP posting and reporting, but configuration must fail closed when a recoverable tax lacks a valid receivable account.
- TAX-05: Tax Rules are called by the calculation engine when no tax group is explicitly supplied; repair seeded rule labels and customer classifications, and remove or implement conditions that the request cannot evaluate.
- TAX-06: align Customer Type choices with the canonical Business Partner `CustomerType` classification values, not the partner role values.

## Evidence already established
