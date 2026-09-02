# Finance guided fiscal-year mastery progress

## Purpose

This checkpoint distinguishes the authored Guided Fiscal-Year Mastery Lab from Finance capabilities
and UAT evidence that have advanced beyond the generated handbook. It is not a substitute for the
lab source or an operator's signed execution record.

Checkpoint date: **30 August 2026**.

## Current authored handbook

The current generated artifact contains nine labs and ends at `GFM-110`:

| Status in generated artifact | Count |
| --- | ---: |
| Reference only | 2 |
| Partially implemented | 5 |
| Executable | 1 |
| Blocked | 1 |
| Total authored labs | 9 |

Only `GFM-040`, the fiscal-calendar and close-design foundation, is currently labelled executable.
The generated status labels are stale and must not be treated as the current implementation or UAT
truth.

## Evidence completed after the generated snapshot

The following controlled Finance work is materially ahead of the generated handbook:

- governed AP, AR, fixed-asset, bank, Inventory-opening and residual-GL cutover paths were exercised;
- Migration Clearing was reconciled through the controlled opening-balance sequence;
- the dimensional FY2026 budget scenario and its return completed separate approval workflows and
  the scenario was adopted;
- independent FIN/P100 and OPS/P200 budget cells were persisted and reloaded with immutable
  dimension-set evidence;
- the consolidated budget now drills from account-period totals into exact dimension-cell Budget,
  Actual, Reserved and Available positions, including the live FIN/P100 GHS 10,000 / GHS 500 /
  GHS 0 / GHS 9,500 reconciliation;
- manual-journal budget availability, insufficient-budget blocking and the governed override path
  were exercised;
- journal `JE-2026-000006` proved future-date policy, override approval, separate journal approval,
  posting, actor attribution and retained budget evidence.

The detailed budget evidence is retained in
`docs/Finance/finance-dimension-budget-uat-evidence-pack.md`.

## What remains before the handbook is complete

The artifact is not yet a complete guided fiscal-year story. Remaining content and evidence include:

1. synchronize existing lab statuses, screenshots and identifiers with current merged runtime and
   authenticated UAT evidence;
2. incorporate dimensional budgeting, manual-journal budget control, overrides and future-date
   policy into the lab sequence;
3. complete the direct AP budget-reservation/consumption cases and their correction/reversal cases;
4. certify supplier debit-note, supplier-return and retained-remittance deployment gates without
   crossing Procurement/Inventory ownership boundaries;
5. author and execute the ordinary monthly AP, AR, cash/bank, fixed-asset, foreign-currency and
   reporting cycles for the remainder of the fiscal year;
6. execute month-end controls, reconciliations, period close/reopen negatives and retained close-pack
   evidence;
7. execute year-end close, retained-earnings transfer, opening of the next fiscal year and controlled
   roll-forward;
8. replace every placeholder with authenticated evidence and complete operator/mastery sign-off.

## Immediate sequencing

Journal approval withdrawal has passed its first functional UAT cycle: the journal returned to
Draft, workflow tasks closed, the reservation released and approvers were notified. Evidence review
found and regression-tested both a cancellation-date gap and a stale sibling-approval gap. The
pre-fix resubmission was withdrawn cleanly after its first decision: the new cancellation timestamp
was retained, all remaining approvals closed and the reservation released exactly once. A fresh
post-fix submission proved that the unused sibling approval expires immediately before the workflow
advances, and the Finance Manager stage passed. Final approval was stopped because the journal maker
is the tenant's only Financial Controller; self-approval or administrative bypass is not acceptable
evidence. Workflow startup is now hardened to detect that mandatory actor shortage before creating
an instance. Focused shared-workflow and tenant-role/repository suites pass 6/6, and authenticated
UAT proves the controller-made submission is rejected while the journal remains Draft with no
retained workflow, reservation, or submission audit. A fresh accounts-maker cycle remains
outstanding. The remaining sequence is:

1. finish the withdrawal case's unauthorized-actor, resubmission/idempotency and final-posting
   checks on a fresh cycle;
2. execute direct AP budget consumption cases `UAT-APB-001` through `UAT-APB-008` now that the
   Phase B migration and safe runtime gates pass;
3. synchronize the generated handbook's status metadata from retained evidence;
4. continue month by month toward the close and roll-forward chapters.

Do not claim handbook completion from source-code presence or from a generated page alone. Each lab
requires the named deployment gate, authenticated execution evidence and explicit sign-off.
