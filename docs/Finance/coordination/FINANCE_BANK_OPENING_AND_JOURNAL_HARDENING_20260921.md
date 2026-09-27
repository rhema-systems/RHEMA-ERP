# Finance bank opening and journal hardening — 2026-09-21

## Objective

Remove the obsolete ungoverned bank-opening inputs and harden long and foreign-currency manual journal entry behavior without mutating the demonstration database.

## Delivered

- Bank-account creation no longer accepts an opening balance, opening exchange rate, or opening date.
- New bank masters retain zero values in the legacy persistence columns for schema compatibility; governed opening-balance posting is the sole financial opening path.
- Bank details expose the date of posted governed bank-opening evidence as a read-only value.
- Bank reconciliation derives the bank ledger balance solely from posted GL activity, preventing governed openings from being counted twice.
- The journal-entry table provides an additional line action beside the totals footer for long journals.
- Posted and approved audit values are displayed as date and time; the underlying timestamps already retained time.
- Manual-journal functional amounts are normalized to two-decimal posting precision on both client and server.
- Foreign-currency evidence is validated against the approved rate after the same posting-precision rounding, preventing misleading client-side balance followed by a server-side rounding rejection.

## Compatibility boundary

No database migration was added or applied. Legacy bank opening columns remain in the entity and database temporarily, are written as zero/default for newly created bank masters, and are no longer part of the public creation contract. Their physical removal should be a separate migration after existing-data verification.

## Verification

- Frontend focused suite: 27/27 passed.
- Backend focused suite: 19/19 passed.
- Scoped `git diff --check`: clean; only repository line-ending notices were emitted.
- Frontend repository-wide type-check remains blocked by unrelated pre-existing errors in Inventory, Procurement, Projects, and an existing Finance trial-balance test.
- A broader Finance backend run passed 112/142 tests; the remaining 30 failures are existing fixtures that do not provision the newer exact-book authority required by earlier accounting-book hardening.

## Repository state

- Baseline commit: `f734091bd38e866b9af7a2aa8e0ff8dbac01e0a1`
- Work remains uncommitted in the shared, pre-existing dirty worktree.
