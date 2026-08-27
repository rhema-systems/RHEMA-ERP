# AP budget correction lifecycle audit

## Verdict

The direct AP expense adapter is safe for create, submit, reject, approve, and post. The current AP
correction paths are not yet fully certified for dimension-aware budget control.

Do not extend the AP adapter promotion PR `#120` with these changes. Implement the correction
lifecycle as a separate Finance-owned PR only after the promoted adapter is in `master` and its
positive reservation/consumption UAT is complete. Supplier-return producer changes remain
Procurement/Inventory-owned.

Primary source evidence:

- `VendorInvoiceService.RejectCoreAsync` releases reservations at terminal rejection;
- `VendorInvoiceService.VoidAsync` owns invoice void and central journal reversal;
- `FinancePostingEngine.GetReversalPlanAsync` copies historical `FinanceDimensionSetId`;
- `FinanceBudgetCommitmentService.BuildPositionAsync` derives actual by account, fiscal period, and
  dimension set while counting only active reservations;
- `SupplierDebitNoteService.BuildPostingRequestAsync` resolves the historical source transaction but
  its `PostingLine` helper currently omits the source dimension set;
- `SupplierReturnFinanceAdapter` leaves `FIN-INT-012/013` non-posting with `DecisionRequired`.

## Accounting rule

Finance budget availability is derived as:

`adopted amount - net posted expense actual - active reservations`

A correction must therefore preserve two different evidence streams:

1. **GL actual:** a governed compensating journal using the same expense account and immutable
   Finance dimension set as the original posting;
2. **commitment audit:** an immutable operation showing why a reservation was released before
   posting or why a consumed posting was later reversed.

Never reopen or delete a consumed reservation. Posted corrections must create compensating evidence;
pre-post lifecycle termination must release the active reservation.

## Current lifecycle assessment

| Lifecycle | Current accounting behavior | Budget result | Assessment |
| --- | --- | --- | --- |
| Workflow rejection before posting | Releases active AP reservation before marking the invoice rejected | Available amount is restored | Implemented in PR `#116`; promotion/UAT pending through PR `#120` |
| Workflow-start failure | Releases reservations and returns the invoice to Draft | No orphan reservation | Implemented in PR `#116`; promotion/UAT pending through PR `#120` |
| Void of Draft/Pending/Approved invoice without a journal | Marks the invoice Voided, but does not release an existing reservation | Active reservation can remain indefinitely | **Release blocker** |
| Full void of posted invoice | Central reversal plan swaps debit/credit and preserves `FinanceDimensionSetId` | Net posted actual returns to zero and availability recovers | Arithmetic is correct; immutable budget-reversal operation is missing |
| Linked supplier debit note | Credits the exact historical expense account and tax lineage | Legacy account-period budgets net correctly; dimension-controlled budgets may not because the original dimension set is not copied | **Release blocker** |
| Reversal of posted supplier debit note | Central reversal preserves the debit note’s dimension set | Correct only after debit-note posting preserves the source dimension set | Blocked by the preceding gap |
| Cancellation of an unposted supplier debit note | No GL posting and no direct AP reservation | No budget mutation expected | Correct |
| Standalone supplier debit note | Re-resolves an account without an original BudgetEntry/dimension lineage | Budget treatment is ambiguous | Policy required; fail closed for budget-tracked expense accounts |
| AP payment void/reversal | Restores AP settlement; it does not reverse expense recognition | No budget-actual change expected | Correct boundary |
| Supplier return dispatch/commercial resolution | `FIN-INT-012/013` returns `DecisionRequired` and creates no journal | No budget mutation | Correct quarantine; producer integration remains pending |

## Finding AP-BUD-CORR-001 — pre-post void leaks reservations

`VendorInvoiceService.VoidAsync` accepts the controlled void permission, reverses a journal when one
exists, and otherwise marks the invoice Voided. It does not constrain the no-journal path to Draft or
Rejected invoices and does not call the existing AP reservation-release boundary.

Required design:

- Draft with no reservation: allow a document-only void.
- PendingApproval or Approved with no journal: terminate/cancel the active workflow using the
  workflow service’s governed lifecycle, release every active invoice reservation in the same
  serializable transaction, then mark Voided.
- Posted: retain the existing journal-reversal path; do not change a consumed reservation back to
  Released.
- Rejected: normally no active reservation should exist; verify that invariant before voiding.
- PostingFailed/ambiguous durable event: resolve the ledger fact before choosing pre-post release or
  posted reversal. Never infer from `JournalEntryId` alone.

Required tests:

1. pending budget-controlled invoice void releases exactly one reservation and ends the workflow;
2. approved/unposted void does the same;
3. retry is idempotent and writes no second release operation;
4. release failure rolls back invoice status and workflow mutation;
5. event-only posted evidence forces the posted-reversal path;
6. tenant, actor, and permission boundaries remain enforced.

## Finding AP-BUD-CORR-002 — supplier debit notes lose dimension lineage

For a linked supplier debit note, Finance resolves the original `AccountTransaction`, account, amount,
tax, and exchange-rate snapshot. The expense posting line does not currently copy the original
`FinanceDimensionSetId`. A dimension-controlled budget position filters actuals by that set, so the
credit may be invisible to the exact budget cell even though the account balance is correct.

Required design:

- for every linked principal, discount, and tax correction line, copy the immutable source
  transaction’s `FinanceDimensionSetId` into the Finance-internal posting line;
- require one exact source transaction for each corrected source line and fail closed on missing,
  cross-tenant, deleted, or conflicting dimension evidence;
- for an original direct-AP expense line, retain the original VendorInvoiceLineItem `BudgetEntryId` as
  correction provenance; do not allow the client to select a different cell;
- validate that the source BudgetEntry and source dimension set still correspond, without
  reinterpreting the historical posting under today’s dimension configuration;
- let the compensating GL credit reduce actuals; do not manufacture a negative reservation.

Required tests:

1. partial linked debit note credits the exact expense account and original dimension set;
2. the exact budget cell’s posted actual decreases by the functional correction amount;
3. another cell on the same account/period is unchanged;
4. a full correction cannot exceed the uncorrected source quantity/value;
5. foreign-currency correction reuses the approved historical rate while budget actual changes in
   functional currency;
6. debit-note reversal restores the original actual using the same dimension set;
7. duplicate post/reverse attempts return existing evidence;
8. missing or mismatched source dimension lineage fails before journal creation.

## Finding AP-BUD-CORR-003 — posted reversal lacks explicit budget-operation evidence

The budget position is arithmetically correct after a full invoice void because the central reversal
plan preserves the original dimension set and the actual query nets posted debits and credits. The
immutable commitment log, however, records `ConsumeForPosting` but no corresponding correction event.

Required design:

- add a Finance-internal, idempotent `RecordActualReversal` operation linked to the original consumed
  reservation, original journal/event, and reversal journal/event;
- stage that operation in the same transaction as the reversal journal;
- leave the consumed reservation immutable;
- reject a reversal operation unless durable source, posting action, tenant, and journal lineage all
  match;
- make replay return the existing operation and verify its payload hash.

This operation is audit evidence, not an alternative actuals ledger. Budget actual remains derived
solely from posted AccountTransactions.

Required tests:

1. posted invoice void creates one balanced reversal and one `RecordActualReversal` operation;
2. retry creates neither again;
3. an audit-operation failure prevents the reversal transaction from committing;
4. a journal reversal without matching consumed reservation evidence fails closed for a
   budget-controlled direct AP expense invoice;
5. non-budget-controlled invoices retain existing reversal behavior.

## Finding AP-BUD-CORR-004 — standalone debit-note policy is undefined

A standalone supplier debit note has no authoritative original invoice line, BudgetEntry, or Finance
dimension set. Allowing it to credit a budget-tracked expense account would alter actuals without a
controlled budget-cell identity.

Recommended policy:

- linked corrections derive all budget and dimension evidence from the posted source invoice;
- standalone debit notes against a budget-tracked expense account must explicitly select an eligible
  adopted BudgetEntry and provide a complete dimension combination, or be rejected;
- standalone corrections to AP control, tax, advances, discounts, or other non-expense accounts do
  not consume/release an expense reservation merely because they are AP documents;
- any override needs a separate approval policy and reason; never infer a cell from account/period
  when multiple dimension combinations exist.

## Supplier-return boundary

Physical return and commercial correction are distinct milestones:

1. Procurement/Inventory owns approval, dispatch, warehouse/item quantity, and valuation evidence.
2. Finance alone owns AP, tax, budget actual, and GL mutation.
3. An invoiced return does not reduce expense merely because goods were dispatched.
4. The supplier’s evidenced credit document should create the linked Finance supplier debit note.
5. That debit note, not a second Inventory journal, adjusts the expense actual.
6. Replacement, repair, rejected claim, future credit, and cash refund require their explicit
   commercial-resolution policies.

The current `FIN-INT-012/013` adapter correctly returns `DecisionRequired` for these paths. Do not
remove that quarantine as part of the AP budget-correction PR.

## Proposed implementation order

1. Fix and test pre-post invoice void reservation/workflow release.
2. Preserve source dimension and BudgetEntry lineage on linked supplier debit notes.
3. Add immutable, transaction-bound actual-reversal operation evidence.
4. Define and enforce standalone debit-note budget policy.
5. Certify linked partial/full correction and reversal UAT.
6. Only then extend the Procurement/Inventory supplier-return integration contract.

## Definition of done

- every pre-post terminal path releases active reservation evidence atomically;
- every posted correction uses a compensating central-Finance journal;
- every linked expense correction preserves the original immutable dimension set and BudgetEntry
  provenance;
- budget availability reconciles to adopted amount minus net dimension-matched posted actual minus
  active reservations;
- consumed reservations remain immutable and reversal operations are append-only/idempotent;
- corrections cannot exceed remaining source quantity/value or cross tenant/account/currency/period
  lineage;
- supplier returns remain non-posting until the authoritative producer contracts are certified;
- focused service, SQL Server transaction, migration, permission, concurrency, and retry tests pass.
