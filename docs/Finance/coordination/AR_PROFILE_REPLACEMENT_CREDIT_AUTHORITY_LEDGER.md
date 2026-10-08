# AR Profile Replacement and Credit Authority

## Objective

Resolve the UAT `PROFILE_EFFECTIVE_PERIOD_OVERLAP` failure when approving a later Customer AR profile, and make the approved effective-dated AR profile the sole source of customer credit-limit authority.

## Repository state

- Branch: `codex/ar-profile-replacement-credit-authority`
- Worktree: `.w/ar-profile-replacement-credit-authority`
- Exact base: `dd6fb224a1eaa7512087ed3bf0dd767d490c0f12` (`origin/master`)
- Implementation commit: `3077f18d0` — Fix AR profile replacement and credit authority

## Authorized scope

- Remove legacy Business Partner credit-limit editing/writes.
- Use the effective approved AR profile for customer credit enforcement.
- Approving a later AR profile may end-date and supersede the one prior approved version in the same save.
- Add regression coverage for replacement, historical resolution, and legacy-value non-authority.

## Incident evidence

- UAT request: `POST /api/finance/business-partner-profiles/43a92487-f256-476e-9035-b043b3a0b6fb/ar/78014eb0-b9a2-4f1c-9c45-44e67a455b0d/approve`
- Result: HTTP 409, `PROFILE_EFFECTIVE_PERIOD_OVERLAP`
- Observed: 08 Oct 2026 12:01 GMT, user `financial.controller`

## Implemented

- Replaced a single earlier approved AR profile by closing it on the day before the new version starts and marking it `Superseded` in the same `SaveChangesAsync`.
- Added fail-closed rejection for ambiguous/multiple overlaps and same-date or retroactive replacement.
- Preserved historical selection of an end-dated superseded profile.
- Removed the legacy credit-limit control, create/edit payload fields, write DTO members, validation, and ordinary Business Partner service writes.
- Changed Sales credit checks to resolve the active Customer role and effective approved AR profile; the legacy Business Partner value is ignored.
- Added controller, policy, Sales authority, service-contract, and frontend regressions.

## Migrations

- None. The legacy database column is retained for compatibility/history but becomes non-authoritative and non-writable through ordinary Business Partner maintenance.

## Verification

- PASS: `ErpSystem.Api.Tests` Finance profile controller + policy filters — 28 passed.
- PASS: `SalesOrderCreditAuthorityTests` — 1 passed.
- PASS: affected frontend suites — 47 passed across 4 files.
- PASS: `git diff --check`.
- EXPECTED/UNRELATED: broader `BusinessPartnerPostingDefaultsTests` filter produced 42 passes and one existing `PostingOptionsExposeOnlyCurrentTenantActiveCatalogueMetadata` null-reference failure in `BusinessPartnerService.GetPostingOptionsAsync`.
- ENVIRONMENT/Baseline: frontend `tsc --noEmit` stops at `src/lib/phone-number.ts(7,8)` because the shared installed dependency tree cannot resolve `libphonenumber-js/max`; no changed file is implicated.
- Existing package audit warnings: `SixLabors.ImageSharp 3.1.11` has published moderate/high advisories.

## Remaining work

- Review and commit the local change set.
- Push/create a pull request only after explicit authorization.
- Deploy only after merge through the normal deployment process, then retry approval of the submitted version-2 AR profile.

## Authorization boundaries

- No push, pull request, deployment, production/UAT database mutation, or remote-server change is authorized in this workstream yet.
