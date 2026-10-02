# Finance reporting and partner-register hardening — 2 October 2026

## Authority and exact base

- Objective: investigate and remediate liquidity-card totals, detailed-ledger presentation, and AR/AP Business Partner eligibility/display gaps as a separate follow-up to PR #323.
- Exact base: `4ad3a54f4a03f2fd56da733c6a63081a947c4505` (`origin/master`).
- Branch: `codex/finance-reporting-partner-hardening`.
- Worktree: `C:/Users/Akwas/Documents/DEV_WORK/RHEMA-ERP-finance-reporting-partner-hardening`.
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

- Phase: implementation and focused regression coverage.
- Review: independent accounting/read-model review required before remote integration.
