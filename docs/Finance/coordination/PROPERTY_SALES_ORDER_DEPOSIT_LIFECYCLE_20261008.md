# Property Sales Order Deposit Lifecycle Ledger

## Objective

Audit and incrementally align the existing Property Listing -> Enquiry -> Opportunity -> Quote -> approved Customer -> Sales Order -> Deposit -> Finance -> Customer Detailed Ledger lifecycle. Reuse the existing HR Identification Type master, CRM, Sales, Business Partner, approval, Finance posting, and reporting infrastructure. Preserve historical prospect deposits and all tenant, authorization, audit, and accounting controls.

## Scope and authorization boundaries

- Authorized source: user attachment reviewed on 2026-10-08.
- Audit-first delivery is mandatory. Do not create parallel customer, quote, sales-order, approval, identification-type, or accounting implementations.
- New property-sale deposits must move to an actual Sales Order only after the approved-customer gate is satisfied.
- Historical prospect deposits must remain readable, auditable, reversible where already supported, and financially unchanged.
- Identification numbers are sensitive. Do not place full values in URLs, application logs, analytics, or descriptive audit text.
- Unrelated worktrees, branches, generated artifacts, and UAT observations are excluded.
- No production/VPS database change or deployment is authorized by this ledger entry alone.

## Git state

- Branch: `codex/property-sales-order-deposit`
- Worktree: `D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2\.worktrees\property-sales-order-deposit`
- Exact base: `dd6fb224a1eaa7512087ed3bf0dd767d490c0f12`
- Base description: merge of PR #383 on `origin/master`.
- Existing older worktree `codex/public-property-enquiry-sales-crm` was preserved because it is 289 commits behind current master and contains 12 divergent commits.

## Current phase

`Audit in progress`

No runtime implementation has started. The repository model, APIs, UI, workflows, Finance posting, ledger projection, migrations, permissions, tenant boundaries, and existing tests are being mapped first.

## Audit deliverable checklist

- [ ] Identification Type model, edit/save behavior, HR consumers, permissions, and validation metadata.
- [ ] TenantModule model, canonical stable identity, tenant scoping, and existing mapping conventions.
- [ ] Property Listing -> Enquiry capture, public lookup safety, and current identification fields.
- [ ] Enquiry -> Opportunity -> Quote behavior, print support, idempotency, and authorization.
- [ ] Customer/Business Partner linkage, duplicate controls, approval state, and Sales Order gate.
- [ ] Property Sales Order prefill, locking, lineage, and existing-order idempotency.
- [ ] Prospect-deposit persistence, Finance posting, reversal/refund, and historical behavior.
- [ ] Sales-order payment/deposit, tender metadata, CustomerPayment/Allocation, invoice boundary, and accounting treatment.
- [ ] Customer Detailed Ledger reference/description source, query shape, and property lineage.
- [ ] Exact gaps, expected files, migration decision, tests, and implementation slices.

## Commits

None yet.

## Migrations and application status

- Migration required: under audit; no migration created or applied.
- Local application: not changed or started for this workstream.
- VPS/test application: unchanged by this workstream.
- Database: no writes performed.

## Verification evidence

- Confirmed the isolated worktree starts clean from `origin/master` at the exact base above.
- Inspected existing Sales/CRM tracker `docs/tdc-sales-marketing-crm-gap-implementation-tracker.md` to avoid duplicating prior requirements analysis.
- Confirmed the older property-enquiry worktree is divergent and unsuitable as the implementation base.

## Known failures and risks

- The attachment spans HR configuration, a public Estate form, CRM, Sales, Business Partner approval, Finance posting, and reporting. Each existing ownership boundary must be verified before schema or service changes.
- Existing prospect deposits may already be posted in production. Any prospective Sales Order deposit path must coexist with them without rewriting history.
- The canonical stable identity available on `TenantModule` is still under audit; frontend display-name matching is prohibited.

## Remaining work

1. Complete and record the 29-point audit requested by the user.
2. Identify any genuine business-rule blockers.
3. Add protection tests for existing behavior and agreed prospective boundaries.
4. Implement only the required controlled slices.
5. Apply and verify any forward migration against an explicit non-production database.
6. Run focused backend/frontend tests, migration checks, tenant/authorization tests, and visible browser acceptance.
7. Update this ledger before every handoff or stop while work remains incomplete.
