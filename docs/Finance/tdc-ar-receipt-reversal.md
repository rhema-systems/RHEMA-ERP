# TDC AR Customer Receipt Reversal

**Implementation date:** 2026-08-02  
**Plan coverage:** AR subset of WP2 and continued WP9 enforcement  
**Limitation coverage:** `FIN-LIM-0010`

## Outcome

This slice resolves the documented inability to correct a posted Accounts Receivable customer receipt. It extends the existing `PaymentService`, shared Finance reversal policy, `FinancePostingEngine`, payment allocations, cash transactions, liquidity holding entries, FX settlements, permissions, and audit service. It does not introduce a parallel posting or bank-settlement path.

The original receipt, allocation, operational entry, and journal remain immutable. Reversal creates linked compensating evidence and changes the source status to `Reversed` only after the complete correction commits.

## Eligibility and control rules

A posted customer receipt can be reversed only when all of the following are true:

- The caller has `Finance.AR.Payments.Reverse` and an effective Finance scope at `Approve` level.
- The source is a normal posted customer receipt, not a customer credit note, cancelled item, bounced item, or already-corrected receipt.
- The original Finance posting event and bank/liquidity operational footprint both exist.
- A direct-bank receipt has not been included in bank reconciliation.
- A liquidity-held cash, cheque, card, or mobile-money receipt has not been allocated to a deposit or settlement.
- A posted customer advance has no downstream application journal. Advance applications must be reversed before their source cash receipt.
- The reason meets the tenant's configured minimum and the correction date satisfies the shared Finance reversal-period policy.

These guards prevent Finance from correcting the cash receipt while leaving a later banking or advance-application transaction orphaned.

## Atomic correction

The service performs the following work under one serializable transaction:

1. Reloads and revalidates the receipt, data scope, allocations, and operational footprint.
2. Posts an idempotent compensating AR journal through `FinancePostingEngine` with a durable link to the original journal.
3. Reverses every related realized-FX event through the posting engine and records settlement-level reversal lineage.
4. Restores invoice `PaidAmount` and recalculates invoice status while respecting posted sales credit notes.
5. Adds negative `PaymentAllocation` rows linked by `OriginalAllocationId`; it never rewrites the original allocations.
6. Restores the Business Partner customer outstanding balance.
7. Creates either:
   - a posted bank `CashTransaction` payment that offsets the original receipt and reduces the bank balance; or
   - a decreasing `LiquidityAccountEntry` linked to the original holding-account entry.
8. Stores the reversal journal, posting event, operational entry, date, user, reason, and audit event on the source receipt.

Retries return the existing correction. Deterministic idempotency keys also prevent duplicate journals if concurrent requests reach the posting engine.

## Returned cheques and banking workflow

General receipt reversal deliberately does not replace returned-cheque processing. A cheque already deposited or returned is corrected through the dedicated Banking & Settlement workflow because that workflow owns the deposit, bank debit, charges, and customer recovery evidence. Likewise, a reconciled direct-bank receipt must first be removed from reconciliation.

This separation avoids two workflows creating competing bank or customer-balance corrections for the same receipt.

## Trace and UI

`GET /api/ar/payments/{id}/trace` returns:

- source receipt and reversal lineage;
- original and negative allocations;
- original, realized-FX, advance-application, and reversal posting events with journal lines;
- direct-bank or liquidity operational entries; and
- Finance audit history.

`POST /api/ar/payments/{id}/reverse` accepts the reason and optional preferred reversal date. The receipt detail page at `/finance/ar/receipts/{id}` presents the evidence chain and shows the reversal action only to users with the dedicated permission. API permission and scope checks remain authoritative.

## Finance access-scope behavior

AR receipt routes now apply the same first-slice access model as AP:

- Tenant grants authorize all tenant receipts up to their access level.
- Bank-account grants restrict direct-bank receipts to the selected bank.
- Liquidity-held receipts require a tenant-wide grant while enforcement is active because liquidity account is not yet an assignable scope dimension.

The last rule is intentionally fail-closed. Pretending that a bank grant covers unbanked cash or cheque holdings would create a false control.

## Deployment

1. Apply `20260802120000_AddFinanceAccessScopesAndApPaymentReversals` if it is not already applied.
2. Apply `20260802150000_AddArReceiptReversals`.
3. Assign `Finance.AR.Payments.Reverse` only to the approved Finance correction/authorization role.
4. Test direct-bank, liquidity-held, reconciled, deposited, customer-advance, retry, closed-period, and foreign-currency scenarios.
5. Retain the trace output and accounting reconciliation as UAT evidence.

## Verification completed

- Focused AR receipt posting/reversal suite: 20 tests passed.
- Test project and API compile succeeded.
- EF Core runtime model matches the migration snapshot with no pending changes.
- Frontend type-check reaches only the repository's pre-existing missing Syncfusion PDF viewer declarations; no AR/AP reversal type errors were reported.

The next common-reversal-contract slice is cash/bank transaction reversal (`FIN-LIM-0011`).
