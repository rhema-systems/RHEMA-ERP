# QS UAT configuration review

This checklist follows the explicitly requested test preparation. The bootstrap creates configuration proposals and named workflow routes. It does not approve a person's decisions, create completed project/BOQ/contract records, or claim the VPS UAT passed.

## Start with the generated preparation report

Open `qs-uat-preparation.json` from the API command output. `PreparedDecisions` lists populated drafts; `Unresolved` identifies controlled selections that must be completed. `Proposals` contains the proposed values and any missing reference names. The command preserves existing user-authored decisions and conflicting master records.

Deploy and prepare from the updated VPS checkout with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-QsUatVps.ps1 -PrepareQsUat`. The report is written beneath `artifacts\qs-uat\preparation-<timestamp>`. Its decision fields are under `Preparation`. `Get-QsUatReadiness.ps1` only inspects state; it does not run this preparation or approve configuration.

The exact Finance selections in the proposal are active controlled lookup records matching `DEFAULT-5000`, `DEFAULT-2000`, `NET30`, `GH-PURCH-STD` and `WHT-WORKS` where those codes exist; a selector with exactly one eligible record can supply that sole record. Multiple unmatched choices are left unresolved. These are test proposals requiring Finance review, not a statement that those account codes exist or are appropriate for the VPS. Review the account, purchase tax and payment-term labels in decision QS-DEC-008 before approval.

## Shortest supported review sequence

1. As `uat.qs.preparer`, open Administration → Project Management → Quantity Survey Configuration (`/administration/project-management/quantity-survey-config`) and open the existing draft. Review QS-DEC-001 through QS-DEC-017. Populated proposals have source lineage `QS-UAT-PREPARATION-V1`; do not replace legitimate existing tenant configuration merely to obtain that marker. Complete any selections listed in `Unresolved`, then use **Save draft** only for edited decisions.
2. Prepare one real UAT policy memorandum covering the proposed configuration and the authorized test-only scope. The generated JSON is a configuration manifest, not a signed approval. If using it as an attachment, render it to an allowed document format and retain the exact preparation report with the evidence. In Central DMS (`/document-management/records`), create the record and upload the document using normal upload/scan/version controls. Complete the configured DMS route if required. The version must be the current **Published** version with clean scan state; metadata template publication alone is insufficient.
3. For each of the 17 decisions, use its **Central DMS evidence** controls to select that actual record/version, choose **Policy** as the evidence type where applicable, and link it. Use **Submit** for each complete decision. The UI currently submits decisions separately; no bulk submit action is provided.
4. Sign in as the independent `uat.qs.approver`. Reopen the same draft and inspect each **Proposed** decision. Enter the real approval/reference for this UAT configuration and select **Approve** only after review. The preparer/submitter cannot approve their own decision. The UI currently approves decisions separately; no bulk approval action is provided.
5. Review the profile validation results. With all 17 decisions approved, current and evidenced, use **Publish** at the top and enter a meaningful publication reason. The owner validates again; follow any remaining error instead of changing statuses in the database.
6. Open `/administration/project-management/quantity-survey-rate-library`. Rate values and source publication require the now-effective QS rate-library policy. Create the representative controlled source/rate through this owner, retain its real evidence where required, and publish through the normal lifecycle. A source record is not created or approved by the bootstrap merely to satisfy a selector.

## Decision checklist

Use the same draft at `/administration/project-management/quantity-survey-config/<profile-id>`. For each row: review the proposed selections → link the current published policy evidence → **Submit** as preparer → **Approve** with a real reference as the independent approver. There is no bulk approval action. Seed preparation does not satisfy these actions.

| Decision | Confirm before approving |
| --- | --- |
| QS-DEC-001 · Roles and authority | Correct QS preparation/approval/audit roles, GHS authority limits and project/contract/section scope. The proposed limits are test values. |
| QS-DEC-002 · BOQ standards | Applicable standards, default CESMM4 and the QS UAT construction type; trade, cost code and work package requirements. |
| QS-DEC-003 · BOQ versions | Correct BOQ and estimate routes, independent reviewers, immutable approved versions and approval before use. |
| QS-DEC-004 · Rate build-up | Allowed cost components; proposed overhead/profit/contingency/wastage caps and rounding. |
| QS-DEC-005 · Rate library | QS UAT construction type and fictional QS UAT rate site; rate dimensions, refresh interval and market-evidence requirement. |
| QS-DEC-006 · Price adjustment | Index sources, formula, coefficients totalling 100 and the escalation approval route. |
| QS-DEC-007 · Measurements | Measurement route and evidence template; contractor/consultant attendance and signature requirements. |
| QS-DEC-008 · Valuations/certificates | Four distinct stage performers; report/evidence templates; Finance's actual expense, AP, terms, purchase tax/WHT selections; advance and retention treatment. |
| QS-DEC-009 · Retention | Proposed retention ceiling, completion/defects releases, defects period, bond option and approval route. |
| QS-DEC-010 · Materials | On/off-site policy, valuation basis, TDC-supplied material deductions and Inventory reconciliation. |
| QS-DEC-011 · Variations/claims | Variation and claim routes, evidence template, allowed change types and intended contract/budget/forecast/certificate effects. |
| QS-DEC-012 · Commercial controls | Provisional sums, contingencies, defects, takeover and charges; final-account/subcontract routes and terms-document setting. |
| QS-DEC-013 · External submissions | Allowed intake channels, document types/size and portal identity/evidence/signature controls. |
| QS-DEC-014 · External tools | Permitted tools/exchange modes, DMS templates and staging/reconciliation requirement. |
| QS-DEC-015 · Reports | Actual QS report catalogue, scoped viewer roles and permitted export formats; scheduling setting. |
| QS-DEC-016 · Interfaces | Correct owner modules, controlled posting events, idempotency, reconciliation and duplicate-posting prevention. |
| QS-DEC-017 · Migration | Source types, preparer/reviewer/sign-off roles and staging/reconciliation/signed-acceptance requirements. This approves policy, not a migration outcome. |

The final configuration gate is **17/17 approved, effective, evidenced decisions plus successful profile validation and publication**. A published workflow, seeded actor or populated draft alone does not meet it. If a reviewer changes a submitted value, resubmit and complete the genuine review again.

## Fresh walkthrough

Use `QS-UAT-CONSTRUCTION` and the `QS-UAT-*` catalogue entries where those test masters were prepared. The `EA` unit comes from the existing operational Inventory baseline. Create and approve the fresh project and BOQ in HTML steps A2–A8; prepare the budget, procurement source and Works contract in B1–B5. Add the relevant project members and external access while creating those records. Do not substitute completed seeded transactions for those UAT steps.

The New Project **Estimated Budget** field is optional: leave it blank if the estimate is not yet known. This does not waive later Procurement/Finance approval, available-budget or commitment controls. The bootstrap does not invent an approved budget.

Valuation/certificate routes use distinct `uat.qs.reviewer`, `uat.qs.engineer`, `financereviewer` and `uat.qs.approver` actors. Variation also includes `procurementapprover`. Each still requires the proper permission and project/contract assignment. Contractor/consultant identities must use only their own linked business-partner/project access.

Finance posting, scanner/DMS operation, effective closeout rules, available stock and sufficient adopted budget remain actual environment checks. Successful preparation is not evidence that those checks or the A–F business walkthrough completed.
