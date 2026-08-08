# TDC Finance Subledger Close Controls and Baseline Seeding

**Implementation date:** 2026-08-02  
**Work package:** WP3 — Finance period close, third controlled vertical slice  
**Migration:** `20260802190000_SeedFinanceCloseControlSetV2`  
**RHEMAERP deployment:** Applied and verified on 2026-08-02

## Outcome

The Finance close workspace now reuses the existing AP/AR settlement read model to produce four
additional period-end checks:

- mandatory AP subledger-to-control-account reconciliation;
- mandatory AR subledger-to-control-account reconciliation;
- warning-level review of unapplied supplier payments and advances; and
- warning-level review of unapplied customer receipts and advances.

No parallel AP/AR balance calculation was introduced. Each control report rebuilds the existing
tenant-scoped settlement projection at the fiscal period end date, compares it to posted account
transactions for the configured control account, and preserves the report values and diagnostics
inside the immutable close-check snapshot.

## TDC control decisions

- AP and AR control accounts must be configured and must reconcile to the subledger with no
  material cent-level difference. A missing control account, a variance of at least `0.01`, or a
  settlement diagnostic blocks preparation and final close.
- An unapplied payment or receipt can be a legitimate advance. Its existence therefore creates a
  review warning, not an automatic blocker. Evidence retains classification totals and item-level
  source references so Finance can allocate, refund, or carry forward the balance deliberately.
- AP/AR control checks are non-waivable in approved templates. Unapplied-balance tasks remain
  optional warnings in the TDC system baseline, but a tenant may make them mandatory in a newly
  approved custom version.
- A cycle already started from an older immutable template continues with that template's provider
  set. New providers begin with the next cycle using the upgraded baseline.

## Seeded close templates

`FinanceCloseTemplateBaselineCatalog` is the single source for all three system templates and
their ten tasks. `FinanceCloseTemplateBaselineSeeder` installs that catalogue through:

- startup reconciliation for every active tenant;
- immediate provisioning after a tenant is created;
- the Finance development-data fixture; and
- lazy close-template initialization in the close service.

The seed is idempotent. If an active TDC system baseline is older, it is marked `Superseded` and a
new approved version is activated; the prior row and any cycle reference remain intact. If a
tenant has a custom active template, the seed does not replace it because that would bypass the
tenant's maker-checker approval decision.

Migration `20260802190000_SeedFinanceCloseControlSetV2` performs the same controlled upgrade for
active tenants already present when the deployment chain is applied. Future tenants do not depend
on the migration because the provisioning and startup seed paths remain active.

## Evidence presented to reviewers

The close workspace now displays the check category, exception count, and exception amount beside
the persisted result. AP/AR control snapshots include:

- control-account identity;
- read-model outstanding amount;
- posted GL control balance;
- variance and document count; and
- all settlement diagnostic codes and source references.

Unapplied-balance snapshots include the total, diagnostic count, classification aggregates, and
item-level settlement source, counterparty, date, currency, amount, and diagnostic flag.

## Verification

- Core, Data, and API builds complete with zero errors. Existing unrelated nullable/obsolete API
  and Data warnings remain unchanged.
- Thirty-five focused close, seed, and settlement tests pass.
- Sixty-five Finance controller security tests pass.
- ESLint passes for the changed close-workspace component.
- TypeScript checking reports only the two pre-existing Syncfusion PDF-viewer module-resolution
  errors and no error in the changed component.
- EF reports no model changes after the data-only migration.
- RHEMAERP reports 204 applied migrations and is up to date at
  `20260802190000_SeedFinanceCloseControlSetV2`.
- The active `DEFAULT` tenant has three approved active system templates, each with ten tasks and
  all four new AP/AR provider tasks.
- A checksum backup was completed before deployment at
  `RHEMAERP_PreCloseControlSetV2_20260802_184100.bak`.

## Follow-on WP3 status

The budget-adoption and recurring-journal exception providers were completed in control set v3;
see `docs/Finance/tdc-finance-recurring-journal-and-budget-close-controls.md`. Binary evidence,
approved exception waivers, and aging/escalation delivery are also implemented; see
`docs/Finance/tdc-finance-close-aging-and-escalations.md`. Remaining work is printable close packs
and higher-tier reopen approval.
