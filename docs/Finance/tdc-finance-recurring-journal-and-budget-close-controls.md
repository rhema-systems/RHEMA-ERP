# TDC Finance Recurring-Journal and Budget Close Controls

**Implementation date:** 2026-08-02  
**Work package:** WP3 — Finance period close, fourth controlled vertical slice  
**Migration:** `20260802203000_SeedFinanceCloseControlSetV3`  
**RHEMAERP deployment:** Applied and verified on 2026-08-03

## Outcome

The persisted Finance close workspace now evaluates two additional Finance-owned controls without
creating parallel lifecycle or exception tables:

- unresolved recurring-journal generation exceptions; and
- approved fiscal-year budget scenarios awaiting an explicit adoption decision.

Both providers append their result and supporting record references to the immutable close-check
snapshot on every evaluation. A cycle already created from an older approved template retains its
older provider set; new checks start only through a newly approved baseline or custom version.

## FR-GL-006 production recurring-journal workflow (2026-08-08)

The original schema and close check are now complemented by a complete Finance-owned operating
workflow. The browser demonstration and local-storage records have been removed. Finance users can
create balanced functional-currency standing instructions, submit them for independent approval,
pause/resume approved schedules, generate due occurrences, independently approve each accounting
event, and post approved occurrences through `IFinancePostingEngine`.

The design deliberately separates three authorities:

1. **Template maker** defines the accounting intent, fixed balanced lines, effective dates,
   business-day convention, optional reversal rule, and reference pattern.
2. **Template checker** independently activates the standing instruction. The maker cannot approve
   it, and all reasons form part of the Finance audit trail.
3. **Occurrence checker and poster** review the immutable generated snapshot before it reaches the
   GL. The scheduler only creates `PendingApproval` work; it cannot post money. The occurrence
   approver cannot also post that occurrence.

Every occurrence stores the versioned template snapshot, scheduled/effective dates, business-day
adjustment explanation, review evidence, posting evidence, failure diagnostics, and any reversal
due date. Posting uses an occurrence-specific idempotency key, preventing an API retry or two
application nodes from duplicating the journal.

The first production release intentionally permits only the tenant functional currency. A foreign-
currency standing instruction requires rate-source and snapshot policy that is not safe to infer;
it is rejected explicitly instead of generating an untraceable FX journal. Auto-reversal currently
records a controlled reversal due date for follow-up and never silently posts a reversal.

Operational routes are under `/api/finance/recurring-journals` and reuse existing Finance journal
permissions for viewing, creation, editing, submission, approval, and posting. The recurring-
journal background service runs every 15 minutes and can also be invoked through the workspace's
controlled “Process due schedules” action for UAT/catch-up.

Schema support for the maker-checker and posting evidence is introduced by
`20260808131857_AddRecurringJournalProductionLifecycle`. The migration is deliberately limited to
the recurring template and occurrence audit fields; it does not alter unrelated Finance precision
or seed monetary standing instructions. TDC Finance should create its own approved recurring
templates so that account choices, amounts, ownership, and effective dates are conscious business
decisions rather than development defaults.

### Recurring-journal verification (2026-08-08)

- Five focused recurrence/generation tests pass, including idempotent repeated processing and the
  guarantee that generation creates one `PendingApproval` occurrence without a journal entry.
- Both Finance frontend/backend route-contract tests pass, and targeted ESLint is clean for the
  recurring-journal workspace and data service.
- Core/Data/API compile with zero errors; reported warnings are existing cross-module warnings.
- Local RHEMAERP has migration `20260808131857_AddRecurringJournalProductionLifecycle` recorded,
  with all five template evidence columns and seven occurrence evidence columns present.

## Recurring-journal generation control

`RECURRING_JOURNAL_EXCEPTIONS` is mandatory and cannot be downgraded in an approved template.
It uses the existing `RecurringJournalOccurrence.Status` lifecycle as its authority and fails for
period occurrences in `Due`, `Generating`, `SubmissionFailed`, `Failed`, or `WaiverPending`.
It also detects active templates whose `NextDueDate` remains on or before period end, covering a
scheduler failure that occurred before an occurrence row could be created.

The provider deliberately does not treat `PendingApproval` or `Approved` as generation failures:
the journal was generated and the existing journal approval/posting integrity checks govern the
remaining lifecycle. `Waived` and `Superseded` are resolved controlled outcomes and do not fail the
close check. This avoids a second, informal waiver route in the close workspace.

Evidence includes occurrence/template identifiers, schedule and effective dates, status, attempt
count, error text, journal/workflow links, and overdue-template cursor/failure details.

## Budget-adoption review

`BUDGET_ADOPTION_REVIEW` is an optional TDC baseline warning. It identifies scenarios for the
closing period's fiscal year that were approved and locked by period end but remain inactive and
unadopted.

Approval and adoption are intentionally separate in the existing budget model. An approved
scenario can remain a valid planning or comparison scenario, so the system does not post it,
adopt it automatically, or block the ledger close. The warning prompts Finance to adopt the
scenario as the official variance-reporting baseline or retain it deliberately. A tenant may make
this task mandatory in a newly authored and independently approved custom template.

Evidence includes scenario identity/name, approval lock date/user, active/adoption state, and
effective adoption date where present.

## TDC baseline v3

The system baseline now contains twelve tasks and eleven automated providers. Version 3 adds the
two controls above while preserving the existing GL, trial-balance, AP/AR reconciliation,
unapplied-balance, cash/bank, depreciation, FX, and preparer-certification controls.

The data migration upgrades only active system baselines. It supersedes the prior approved version
and creates a new immutable approved version for month-, quarter-, and year-end. A custom active
template is never displaced because it represents an explicit tenant governance decision.

## Close-control verification (2026-08-03)

- Fifteen focused period-close and baseline-seeder tests pass. They cover a failed recurring-journal submission, approved inactive
  budget warning, baseline task/provider presence, immutable evidence counts, and existing close
  behavior.
- The baseline seeder tests cover initial installation, idempotency, system-version supersession,
  and preservation of custom active policy.
- Core, Data, API, and the test assembly build with zero errors; existing unrelated warnings remain.
- EF reports no model changes after the data-only migration.
- RHEMAERP reports 205 applied migrations and is current at
  `20260802203000_SeedFinanceCloseControlSetV3`.
- The active `DEFAULT` tenant has three approved system templates at version 2. Each has twelve
  tasks and eleven automated providers; recurring-journal exceptions are mandatory and
  budget-adoption review is optional in all three close types.
- The pre-deployment checksum backup was verified at
  `RHEMAERP_PreCloseControlSetV3_20260803_050922.bak`.

## Remaining WP3 work

Binary evidence attachments and approved exception waivers are implemented in the fifth slice; see
`docs/Finance/tdc-finance-close-evidence-and-exception-waivers.md`. Aging/escalation delivery is
implemented in the sixth slice; see `docs/Finance/tdc-finance-close-aging-and-escalations.md`.
Remaining WP3 work is signed/printable close packs and higher-tier reopen approval.
