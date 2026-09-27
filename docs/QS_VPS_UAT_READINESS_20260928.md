# QS VPS UAT preparation — 28 September 2026

Target: the existing Test VPS application and its configured database. Local acceptance records are not evidence of VPS readiness. Preserve existing users, passwords, settings and transactions; inspect existing UAT records before creating new ones.

## What the deployment currently checks

`scripts/Deploy-RhemaVps.ps1` invokes the versioned VPS helper's `SeedOperational` action after application deployment. The `seed-operational-uat` command requires migration parity and reconciles Procurement/QS access, dedicated actors, Finance baseline and Inventory master data. `scripts/vps/OperationalUatVerification.ps1` then checks the configured DEFAULT tenant, actor memberships and roles, Inventory categories/UOM/items/warehouse/bin lineage, canonical suppliers with approved AP profiles, and warehouse responsibility scopes. It saves verification evidence on the VPS. This is an operational baseline gate, not complete QS business-scenario acceptance.

Dedicated QS actors checked by that baseline are `uat.qs.preparer`, `uat.qs.reviewer` and `uat.qs.approver`. Existing passwords are preserved. Any initial password for missing users belongs in the secure deployment prompt or protected process setting, never this document.

Local deployment regression checks passed on 27 September: operational-seed wiring/credential protection/repeat-fingerprint guards, and canonical preflight coverage/stale-review/retained-data rejection. They do not prove remote seed completion.

## Required VPS follow-up

1. Capture the deployed commit, configured database identity, migration parity and `OPERATIONAL_SEED|PASS` evidence from the actual release.
2. Reconcile the required cases in `QUANTITY_SURVEY_END_TO_END_UAT.md` and `TDC_QS_END_TO_END_UAT_WALKTHROUGH.html` against that VPS database. Verify active project/site/contractor records, controlled units/rates, Finance accounts/tax/payment terms, scanner/DMS, and governed QS configuration and workflow routes.
3. Verify distinct actors and current permissions for QS preparation/review, engineering confirmation, Finance validation, independent approval, AP/payment and audit. Check actual assignments, not just role names.
4. Identify which scenarios create estimates, BOQs, contracts, measurements and certificates during the walkthrough and which require starting records. Seed only the identified missing prerequisites through a reviewed, repeatable path; do not fabricate approved lifecycle outcomes to claim the walkthrough passed.
5. Record VPS sign-in, selector, document-upload and approval-route checks, plus the exact user and expected outcome for each scenario. Execute the relevant QS-to-Finance handoff and reconciliation before marking it verified.

## Existing fixture boundary

`scripts/quantity-survey/Invoke-QuantitySurveyE2ESeed.ps1` belongs to the documented disposable acceptance workflow. Its SQL prepares governed scenario state directly. Do not substitute it blindly for persistent VPS preparation or rebuild/drop the running VPS database. The older local IDs in `TDC_QS_CLOSEOUT_HANDOFF.md` must not be assumed to exist on the VPS.

Status: **VPS scenario inventory and live QS readiness verification pending.** No remote deployment or seed completion is claimed by this document.
