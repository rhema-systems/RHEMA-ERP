# Governed Finance opening balances

## Purpose and ownership

This change introduces Finance-owned, server-derived opening sources for bank balances and residual GL/equity balances. It deliberately does not weaken the existing free-form opening-balance endpoint or allow protected account IDs to be submitted by a browser.

Inventory opening stock remains owned by Inventory. Finance consumes only the approved, immutable `INITIAL_STOCK` evidence and its resulting Finance posting boundary established by PR #81. The Inventory module continues to own item, warehouse, location, quantity, unit-cost, workflow and inventory-movement lifecycle decisions.

## Controlled sequence

The cutover pack must be executed in dependency order:

1. Post the canonical AP, AR and fixed-asset opening sources.
2. Prepare, approve and post Inventory `INITIAL_STOCK` through the Inventory-owned workflow.
3. Prepare, approve and post each eligible functional-currency bank opening.
4. Prepare the residual GL/equity opening only after all upstream postings are durable.
5. Reconcile Migration Clearing to zero and retain the source IDs, workflow evidence, posting events and journal IDs.

The residual debit is not entered or authorized from a demo amount. The server derives it from the posted Migration Clearing net credit for the selected tenant, book and cutover date, and revalidates that amount at create, submit and post.

## Server controls

- Governed options require an explicit opening date, fiscal period and book. The period must cover the date and be open and unlocked.
- Account eligibility is evaluated as of the selected cutover date, including effective and expiry dates.
- Bank opening lines are derived from the selected active tenant bank account, its mapped GL account and the configured Migration Clearing account.
- A bank master must have a zero opening snapshot before the governed source is created. Posting updates the bank snapshot and GL evidence atomically and exactly once.
- Bank GL remapping, deactivation and deletion are blocked while active, posted or corrupt governed opening evidence exists.
- Residual lines are derived from configured Migration Clearing and Retained Earnings accounts plus server-classified accrued-expense and share-capital choices. Inventory is never represented as a free-form Finance line.
- Generated batches are immutable in the UI and service. Mapping, source and ledger drift invalidate approval or posting.
- Governed creation and posting are serialized and idempotent. A durable posting event remains authoritative even if local batch backlinks are missing.
- A terminal unposted Rejected batch may be superseded without deleting its evidence. Rejected records with any durable posting evidence remain blocking.
- Retried submit calls do not regress Pending, Approved, PostingFailed or Posted batches into a new workflow.
- A governed batch must enter an active approval workflow; an immediately completed/no-review workflow is rejected.
- A success-audit failure cannot demote a durably Posted batch to PostingFailed.

## UI and approval behavior

The Opening Balances workspace is available under Finance > General Ledger. Its governed preflight presents AP, AR, fixed-asset, Finance-option and Inventory-option readiness without claiming that a browser is the accounting authority. Each source action remains subject to its server controls.

Posted Finance opening batches expose a **Controlled correction** panel. The maker records the reversal date, reason, and impact assessment; a different authorized reviewer approves or rejects the immutable request; and only an approved request can post. The original batch and journal remain audit evidence, while the correction links the compensating journal and posting event and marks affected source read models as reversed or ready for corrected reposting.

Opening-balance approval rows deep-link to `/finance/opening-balances?batchId=<id>`. Approval amounts use the batch line functional currency; period codes and book classifications must never be passed to a currency formatter. The frontend also normalizes unexpected Finance approval currency values as a defensive rendering boundary.

Posting is available only for `Approved` and retryable `PostingFailed` batches. A workflow-start `Failed` batch is not postable.

## Current scope and exclusions

- Bank openings in this change are functional-currency openings. Foreign-currency bank opening and revaluation evidence require a separate, explicit accounting contract.
- AP and AR foreign-currency opening balances require transaction-currency, functional-currency, exchange-rate and later revaluation UAT before they are claimed as supported by the mastery lab.
- Supplier returns, supplier debit notes and remittance advice are separate business lifecycles and are not expanded by this PR.
- The public free-form GL opening endpoint continues to reject bank, AP/AR control, Inventory control, tax, retained-earnings and fixed-asset-derived accounts.

## Merge and rollout order

This Finance branch is intentionally stacked on PR #81. Merge the reviewed Inventory opening-stock contract first. Then rebase this branch onto the resulting `master`, rerun the Finance and Inventory boundary tests, and open the Finance PR. Do not copy the Inventory implementation into this PR or merge the Finance branch while its PR #81 dependency is unresolved.
