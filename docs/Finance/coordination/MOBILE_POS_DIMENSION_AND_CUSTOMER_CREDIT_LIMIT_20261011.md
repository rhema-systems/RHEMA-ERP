# Mobile POS Dimension and Customer Credit Limit Ledger

## Objective

Clarify the source and meaning of the Mobile POS store's Finance coding selector, expose all valid Finance dimension values in a usable searchable control, and make customer credit-limit enforcement consistent: a null or zero approved AR profile credit limit means unlimited credit, while only a positive limit is enforced.

## Scope and authorization boundaries

- Authorized changes are limited to Mobile POS administration references/UI and customer credit-limit behavior in Sales Orders, AR customer inquiries/checks, and AR invoice creation.
- The Mobile POS store's Cash Till remains a separate GL liquidity-account selection. Finance dimension defaults must not be replaced with GL accounts.
- Credit hold, approved Customer role, approved/effective AR profile, tenant ownership, and other readiness controls remain mandatory even when the numeric credit limit is unlimited.
- No unrelated dirty files or sibling worktrees are part of this workstream.
- One local commit is authorized. Push, merge, workflow dispatch, deployment, database mutation, and VPS changes are not authorized.

## Git state

- Branch: `codex/mobile-pos-cost-center-credit-limit`
- Worktree: `D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2\.worktrees\mobile-pos-cost-center-credit-limit`
- Exact base: `ed768bc0f5cd1177c4a60af66fa35791555aae67`
- Base source: current `origin/master` at worktree creation.
- Commits: one local implementation commit contains this ledger and all intended changes. Its immutable hash is recorded in the handoff because a commit cannot embed its own final hash.

## Findings and implementation

### Mobile POS Finance coding

- The store dialog field described as a cost centre is backed by `FinanceDimensionDefinition` and `FinanceDimensionValue`, through `MobilePosStoreDimensionDefault`; it is not backed by the chart of accounts.
- The administration reference service already filtered values to the current tenant and to active, currently effective rows. Seeing one option therefore means that only one value currently meets those rules for that Finance dimension.
- The Mobile POS sales service inherits these dimension defaults into transaction coding. The Cash Till selector independently loads active `LiquidityAccount` rows of type `CashTill`.
- The API now includes dimension description and value-source metadata. A focused test proves that all and only active/effective values are returned.
- The store dialog now labels each selector as a Finance dimension, provides a searchable option list, displays the returned active/effective count, and directs administrators to `Finance -> Settings -> Transaction dimensions` to maintain values.

### Customer credit limits

- `SalesOrderService` already treated null and zero as unlimited, but the rule was local and based on an exact zero comparison.
- `CustomerService.CheckCreditLimitAsync` converted null to zero and then calculated zero available credit, incorrectly rejecting every positive request for customers with an unlimited limit.
- `InvoiceService` compared against the nullable limit and only logged a positive-limit overrun instead of enforcing it.
- `BusinessPartnerFinanceProfilePolicy.IsCreditLimitEnforced` is now the shared positive-only rule. Sales Order authorization, customer balance/check responses, and AR invoice creation use it consistently.
- Customer DTOs/API/frontend expose `isUnlimitedCredit`; list and detail screens show `Unlimited` and avoid a misleading zero-limit utilization calculation.
- AR invoice creation now rejects an invoice that would exceed a positive approved limit. Null and zero allow creation, subject to the existing readiness and hold controls.
- Business Partner Finance Profile help text states that blank or zero means unlimited and only positive values are enforced.

## Migrations and application status

- Database migrations: none required or added.
- Database application: none performed.
- Local or VPS application deployment: none performed.
- Existing data is unchanged. Administrators must create/activate additional dimension values in Finance settings if a store needs more choices.

## Verification evidence

- `git diff --check`: passed.
- Frontend TypeScript: `tsc --noEmit` passed.
- Focused frontend test `BusinessPartnerFinanceProfilesPanel.test.tsx`: 13/13 passed.
- Focused Core tests `SalesOrderCreditAuthorityTests`: 3/3 passed, covering null, zero, and positive limits.
- Focused API tests: 21/21 passed across Mobile POS administration references, canonical customer profile reads and credit checks, the shared profile policy, and AR invoice null/zero/positive credit-limit behavior.
- The API and Core builds completed as part of the focused test runs. Output retained the repository's existing compiler warnings and ImageSharp package advisory warnings, with no new compilation error.
- Verification used .NET SDK 9.0.315 from `C:\Users\USER\.dotnet` and build artifacts under `C:\Temp` because the repository drive had limited free space.

## Known failures and risks

- The first Core test run exposed integer `InlineData` conversion to `decimal?`; the tests were corrected to typed `TheoryData<decimal?>`, and the rerun passed.
- An earlier ordinary build attempt exhausted free space on the repository drive, and an intermediate migration compiler invocation selected an incompatible .NET 10 host. No source defect was inferred from either environment failure. Generated output was redirected to `C:\Temp` and the repository-pinned .NET 9 SDK was selected for the passing runs.
- The Mobile POS store dialog is also being changed in a separate workstream for currency, offline-policy, and concurrency behavior. Integration may require a small manual merge in `frontend/src/app/administration/mobile-pos/page.tsx` while preserving both sets of behavior.
- Visible authenticated browser acceptance and deployment-environment validation remain outstanding.

## Remaining work

1. Integrate this local commit with the Mobile POS store-dialog workstream, resolving the expected dialog overlap without dropping either change.
2. In an authenticated browser, confirm that a dimension with multiple active/effective values shows all values and that searching/selecting/saving works.
3. Confirm null, zero, and positive credit-limit behavior against representative approved customers in UAT.
4. Push, merge, and deploy only after explicit authorization.
