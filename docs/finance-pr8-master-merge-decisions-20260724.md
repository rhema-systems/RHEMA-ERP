# Finance PR #8 Master Merge Decisions

Date: 2026-07-24

This note records the cross-module decisions made while merging `origin/master` into
`final-finance-hardening-integration`.

## Sidebar navigation

The Finance branch navigation remains the base because it contains the complete Finance
feature routes and the role/permission filtering introduced by PR #8. Replacing it with the
shorter target-branch Finance menu would hide implemented Finance workflows.

The Procurement additions from `master` were retained explicitly:

- sourcing cases and prequalification;
- APP submissions, specification templates, and the annual procurement calendar;
- procurement policy profiles, executable policies, policy simulation, SOD controls,
  access/committee controls, master-data changes, and control events.

The merged sidebar therefore preserves both modules without duplicating older Finance aliases.

## Tenant provisioning

New tenant provisioning invokes all applicable baseline seeders:

- Finance payment terms;
- Procurement configuration profiles;
- Procurement access controls.

The Finance payment-term seeder remains required. Procurement seeders remain optional because
their registrations may not be present in every host/test composition.

## Workflow administration

The workflow definition request keeps the Finance branch page size of 100 because the current
page has no definition pagination control and reports the returned count against the total.
This avoids silently hiding definitions beyond the target branch's smaller page size.

## Verification

The merge is not considered complete until frontend lint/build checks and the relevant .NET
build/test suites pass on the combined source.
