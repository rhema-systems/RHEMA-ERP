# Finance reporting and partner-register hardening — 2 October 2026

## Authority and exact base

- Objective: investigate and remediate liquidity-card totals, detailed-ledger presentation, and AR/AP Business Partner eligibility/display gaps as a separate follow-up to PR #323.
- Original investigation base: `4ad3a54f4a03f2fd56da733c6a63081a947c4505`.
- Final integration base after synchronization: `a8a17615113de45e80b5e4bafe09ef1f437d5349` (`origin/master`, including Finance precision PR #323).
- Branch: `codex/finance-reporting-partner-hardening`.
- Worktree: `C:/Users/Akwas/Documents/DEV WORK/RHEMA ERP/RHEMA-ERP/.codex-worktrees/finance-reporting-partner-hardening`.
- The primary dirty UAT checkout is untouched.

## Confirmed baseline

1. Liquidity-account cards derive `CurrentBalance`, `AvailableToSettle`, and `OpenEntryCount` exclusively from unreversed `LiquidityAccountEntry` rows. Posted GL activity against the mapped control account is therefore absent from the displayed current balance unless the same producer also created a liquidity entry.
2. Exact-book detailed-ledger filtering and selected-book currency resolution were merged in PR #313. The page still displays `FinanceSettings.BaseCurrency` before the report's selected-book `CurrencyCode`, so a USD parallel-book report can be labelled GHS even when the backend correctly returns USD.
3. AP supplier entry options on current `master` already use canonical Business Partner Supplier/Contractor roles and the shared effective-profile readiness policy. The AP page displays ready and unready options but has no readiness filter.
4. AR customer reads use canonical Customer roles and the same readiness policy, but the API maps readiness into `IsActive`, omits readiness code/message, returns the stored `BusinessPartner.OutstandingBalance`, and does not implement the client's `IncludeBalances` or `Status` query contract. The page renders a nonexistent `status` field and its filter button has no action.

## Intended remediation

- Make the liquidity card's current balance authoritative to posted rows in the exact default Primary book and the liquidity account currency; retain settlement availability/open-item values and expose visible GL-versus-subledger reconciliation state.
- Display detailed-ledger monetary totals in the backend-returned selected-book currency.
- Give AR customer DTOs explicit partner-operational and transaction-readiness fields, readiness reasons, and an authoritative settlement-balance option; make filtering functional and align UI semantics with AP.
- Add an AP readiness filter without weakening the existing canonical policy.

## Boundaries

- No database or accounting-data mutation.
- No migration unless a schema change becomes unavoidable; any authored migration remains unapplied.
- No Procurement master-data mutation and no weakening of Business Partner approval/blacklist/profile rules.
- Posted `AccountTransaction` evidence remains the GL authority; operational liquidity entries remain the settlement-availability authority.

## Status

- Implemented:
  - liquidity headline balances now derive from posted/reversed rows in the exact default `PrimaryFull` book, using functional amounts for the book currency and transaction amounts for a matching foreign liquidity currency;
  - the response and UI expose settlement balance, GL/subledger variance, reconciliation state, and an explicit fallback when no primary-book authority exists;
  - detailed-ledger UI displays the report's selected-book currency before the tenant base-currency fallback;
  - AR exposes partner activity/blacklist separately from transaction readiness, supports `Ready`/`NotReady` filtering, and honours `IncludeBalances` through the settlement read model;
  - AR and AP registers provide working readiness filters without weakening canonical partner/profile policy.
- Focused regressions added for liquidity GL/subledger variance and AR readiness/read-model balances.
- Verification:
  - API build: passed (`0` errors; existing warnings remain).
  - changed-file ESLint: passed.
  - repository-wide lint: blocked by 146 pre-existing errors outside this scope.
  - focused backend tests: passed (`7/7`) for canonical AR profile/readiness/balance behavior and liquidity GL/subledger reconciliation.
- Accounting/read-model review:
  - posted exact-book `AccountTransaction` rows remain the monetary authority for the liquidity headline;
  - settlement entries remain the authority for availability and open-item workflow;
  - historical partner visibility is retained, while readiness is explicit and new-transaction actions remain fail-closed;
  - no schema, migration, posted evidence, or Business Partner master data is mutated.
- Phase: verified and ready for remote integration.
- Rebase: clean onto `a8a17615113de45e80b5e4bafe09ef1f437d5349`; post-rebase focused verification passed (`7/7`).
