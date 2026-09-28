# QS VPS UAT preparation — 28 September 2026

Target: the existing Test VPS application and its configured database. Local acceptance records are not evidence of VPS readiness. Preserve existing users, passwords, settings and transactions; inspect existing UAT records before creating new ones.

## What the deployment currently checks

`scripts/Deploy-RhemaVps.ps1` invokes the versioned VPS helper's `SeedOperational` action after application deployment. The `seed-operational-uat` command requires migration parity and reconciles Procurement/QS access, dedicated actors, Finance baseline and Inventory master data. `scripts/vps/OperationalUatVerification.ps1` then checks the configured DEFAULT tenant, actor memberships and roles, Inventory categories/UOM/items/warehouse/bin lineage, canonical suppliers with approved AP profiles, and warehouse responsibility scopes. It saves verification evidence on the VPS. This is an operational baseline gate, not complete QS business-scenario acceptance.

Dedicated QS actors checked by that baseline are `uat.qs.preparer`, `uat.qs.reviewer` and `uat.qs.approver`. Existing passwords are preserved. Any initial password for missing users belongs in the secure deployment prompt or protected process setting, never this document.

The operational baseline alone does not provision `uat.qs.contractor` or its own-business-partner portal link. The opt-in QS preparation below addresses those additional starting prerequisites; the named committee and Finance users still need actual stage assignments verified.

## Deploy and prepare the QS test prerequisites

Run from the updated VPS release checkout:

```powershell
& {
    $ErrorActionPreference = 'Stop'
    Set-Location 'C:\Users\Administrator\Documents\ERP\RHEMA-ERP'
    powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File .\scripts\Deploy-QsUatVps.ps1 `
        -ExpectedDatabase 'RhemaERP_VpsTest_20260926_173800' -PrepareQsUat
    if ($LASTEXITCODE -ne 0) {
        throw 'Deployment or QS preparation stopped. Keep the reported evidence before retrying.'
    }
}
```

The wrapper deploys and verifies the release, then explicitly runs test preparation, then generates the read-only readiness report. If a stage fails, later stages do not run. Enter the shared password only at the protected prompt for new UAT accounts; existing passwords are preserved.

Preparation adds missing test contractor/consultant/engineer access, a fictional Estate parcel through its owner, QS catalogue/location masters, named UAT workflow configurations and templates, and populated **draft** QS configuration proposals. It preserves existing user-owned configuration and records conflicts or unresolved controlled selections. It does not create approved projects, BOQs, contracts, invoices or payments.

Inspect the path printed by `QS_UAT_PREPARATION_REPORT`; the report is under `artifacts\qs-uat\preparation-<timestamp>\qs-uat-preparation.json`. Resolve `Preparation.Unresolved` and follow [the 17-decision reviewer checklist](QS_UAT_CONFIGURATION_REVIEW.md). The remaining gate is genuine current published DMS evidence, submission by the preparer, independent approval of all 17 decisions and profile publication. Preparation deliberately reports `QS_CONFIGURATION|INDEPENDENT_REVIEW_REQUIRED` and never certifies the business walkthrough.

If deployment succeeded but preparation stopped, rerun only the additive preparation after resolving its reported blocker:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
    -File .\scripts\vps\Initialize-QsUat.ps1 `
    -ExpectedDatabase 'RhemaERP_VpsTest_20260926_173800'
```

New Project's **Estimated Budget** is optional. A blank estimate does not bypass later approved-budget, Procurement, Finance or commitment controls; prepare those at the corresponding A–F stages.

### Verified deployment coverage versus tomorrow's prerequisites

This comparison comes from `OperationalUatBaselineSeeder.cs`, `OperationalUatVerification.ps1` and the QS UAT data pack; it is source inspection, not a query of the VPS.

| Prerequisite | Existing deployment coverage | Evidence still needed on the VPS |
| --- | --- | --- |
| QS preparation, review and approval users | Three dedicated actors, active DEFAULT membership and named role links | Actual permission grants, distinct workflow performers, and project/contract assignments |
| Engineering confirmation, Finance validation, AP/payment and audit | Some existing operational users receive named roles | Match each QS stage to an authorized distinct user; a role name alone does not prove stage eligibility |
| Inventory masters | Five UOM codes, six categories, nine items, DEMO-PM/WH-02 warehouses, three bins and responsibility scopes | Select the Works material and prove its relevant opening balance or movement; master data does not seed available stock |
| Supplier and Finance setup | Canonical supplier/AP-profile checks and Finance baseline reconciliation | Effective contractor profile, usable accounts, taxes/payment terms, open fiscal period and sufficient approved budget |
| Approved project, Works source, contract and BOQ | Not asserted by the operational verifier | Fresh UAT source records and their actual approval/lineage evidence |
| Rates, QS configuration and approval routes | QS access-control seeding is invoked | Approved/effective rate sources, authority limits, route stages, retention rules and document requirements |
| Document upload and reports | Not certified by the operational SQL checks | VPS scanner/DMS health, clean upload/download authorization, templates and report access |

Finance reconciliation deliberately preserves an existing user-owned mapping when it reports `FINANCE_CLASSIFICATION_ENABLED_MAPPING_LINEAGE_INVALID`. Such a warning requires Finance review; successful operational actor/master seeding must not be presented as proof that every posting mapping is ready.

Local deployment regression checks passed on 27 September: operational-seed wiring/credential protection/repeat-fingerprint guards, and canonical preflight coverage/stale-review/retained-data rejection. They do not prove remote seed completion.

## Required VPS follow-up

1. Capture the deployed commit, configured database identity, migration parity and `OPERATIONAL_SEED|PASS` evidence from the actual release.
2. Reconcile the required cases in `QUANTITY_SURVEY_END_TO_END_UAT.md` and `TDC_QS_END_TO_END_UAT_WALKTHROUGH.html` against that VPS database. Verify active project/site/contractor records, controlled units/rates, Finance accounts/tax/payment terms, scanner/DMS, and governed QS configuration and workflow routes.
3. Verify distinct actors and current permissions for QS preparation/review, engineering confirmation, Finance validation, independent approval, AP/payment and audit. Check actual assignments, not just role names.
4. Identify which scenarios create estimates, BOQs, contracts, measurements and certificates during the walkthrough and which require starting records. Seed only the identified missing prerequisites through a reviewed, repeatable path; do not fabricate approved lifecycle outcomes to claim the walkthrough passed.
5. Record VPS sign-in, selector, document-upload and approval-route checks, plus the exact user and expected outcome for each scenario. Execute the relevant QS-to-Finance handoff and reconciliation before marking it verified.

## Existing fixture boundary

`scripts/quantity-survey/Invoke-QuantitySurveyE2ESeed.ps1` belongs to the documented disposable acceptance workflow. Its SQL prepares governed scenario state directly. Do not substitute it blindly for persistent VPS preparation or rebuild/drop the running VPS database. The older local IDs in `TDC_QS_CLOSEOUT_HANDOFF.md` must not be assumed to exist on the VPS.

Status: **The supplied VPS prerequisite report dated 28 September 2026 00:31:15 UTC identified starting setup gaps. The new opt-in preparation and independent configuration review must be verified on the VPS before the fresh business walkthrough.** No completion of that walkthrough is claimed by this document.

## Read-only VPS evidence command

For a release upgrade followed only by this report, run
`powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-QsUatVps.ps1`
from the updated VPS checkout. Add `-PrepareQsUat` for the explicit setup above. Without that switch the wrapper does not run QS preparation. It runs the report only after the normal
deployment succeeds. It uses the existing deployment preflight; normal `-DryRun`
is a current-release parity check and is not a prerequisite for pending migrations.
Windows PowerShell 5.1 `-File` report invocation and failure sequencing have dedicated
regressions, including default output path resolution after parameter binding.

After this follow-up is available in the release checkout, run this on the VPS. The explicit database below is the name from the user's successful fresh cutover; the script refuses to run if the current API service points elsewhere.

```powershell
Set-Location 'C:\Users\Administrator\Documents\ERP\RHEMA-ERP'
powershell.exe -NoProfile -ExecutionPolicy Bypass `
    -File .\scripts\vps\Get-QsUatReadiness.ps1 `
    -ExpectedDatabase 'RhemaERP_VpsTest_20260926_173800'
```

This reads the existing WinSW/NSSM API connection privately, executes the existing operational baseline verifier and inventories the named actors, partner links, project-ready Estate land, QS profile/decision state and workflow prerequisites. It writes a JSON report and a VPS-linked HTML walkthrough under `artifacts/qs-uat`. It never changes database records, passwords, permissions or services. Inspect actual decision bindings, stage performers and assignments; the report never labels QS end-to-end readiness Passed.

The command was exercised against an isolated local copy, including wrong-database rejection and generated HTML links. That is command/schema verification only. The user supplied the VPS report captured at 00:31:15 UTC on 28 September; rerun the report after preparation and real configuration approval to capture the resulting state.
