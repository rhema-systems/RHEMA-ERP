# Stock return review and optional comments — 10 September 2026

## Changes

- The selected return reason is mandatory; typed details and operational notes are optional. When details are blank, the server retains the selected reason label in the immutable voucher. This preserves the existing SQL reason constraint without a migration.
- Approval comments are optional and entered in a separate confirmation dialog. They are never copied from a new-return draft. Rejection and reversal require their own reason; rejection is validated before calling the shared workflow.
- Return approval, rejection, posting and reversal controls require the approval permission and an identified actor other than the voucher requester. The existing server and SQL independence guards remain in place.
- An approver reviewing another user's pending/approved return sees saved voucher information rather than an editable new-return form. Submit Return For Approval is hidden, including when the reviewer has a broad issue permission. The review session stays read-only after its decision.
- Required evidence, scope, active workflow eligibility, row versions and idempotency controls are retained. Server errors keep the decision dialog and comment open.
- UAT walkthrough section B19 is updated in Markdown and HTML.

## Verification

- Focused backend return/adjustment controls: 24 passed. Tests include optional details, optional approval comments, required rejection reasons, SQL lifecycle constraints and server independence guards.
- Frontend return screen: 14 tests passed, including requester independence, review-only approver view even with broad issue permission, optional approval comments, separate rejection reasons and preserving server errors. Existing issue dialog regressions: 25 passed. Scoped inventory TypeScript check passed.
- Rehearsal voucher `SRV-20260910095700-210B7B3` / `ae835fec-dfd4-40d9-8ab5-3f507b008f49` was read directly: requested by `manager`, PendingApproval, one action, no approval or posting actor. No return was submitted, approved, rejected, posted or reversed for this fix.
- Rehearsal API and frontend rollout passed. Visible browser verification as `procurementapprover` showed **Review Stock Returns**, the saved requester/reason/quantity, and Approve/Reject. No new-return inputs or Submit Return For Approval button were present. Opening Approve showed an empty **Comments (optional)** field and an enabled **Approve return** button; it was cancelled without approving. The review screen is left open for the user. Creator action visibility is covered by automated tests; no live creator approval was attempted.
- SQL was rechecked after deployment: the same voucher is still PendingApproval with exactly one action and null approval/posting actors. Main UAT ports 3000/5000 and its database were not changed by this rehearsal rollout.

## Rehearsal deployment

Artifacts and rollback scripts: `local-artifacts/requisition-return-20260910/`.
The updated Core assembly is loaded into the existing isolated rehearsal API only; no Finance assembly or configuration is replaced. Frontend runs with `next start` on port 3002 and the existing API isolation guard.

API port 5002: PID 7148. Tested and loaded Core SHA-256 both equal `43428D504B02855A015BE4BD0061BB10CBAA828554AD99C06248B1666CEACB2C`. The first switch encountered a file lock and restored the prior binary; the guarded retry succeeded. `rollback-core/` retains the original files. No migrations or database repair scripts were run. Main API PID 42148 and frontend PID 13628 were preserved.

Frontend port 3002: PID 63280, build `UEy2n_T7xlhn5VIjIh7CQ`, runtime `local-artifacts/rehearsal-requisition-height-20260910/frontend`. Production compilation succeeded; scoped TypeScript checks were run separately. HTTP 200 and the `RhemaERP_PO_Rehearsal_20260909` isolation header were confirmed after the cold start. Final browser review and sign-in succeeded. The previous frontend build is preserved under `local-artifacts/requisition-return-20260910/frontend-serving`.

Main UAT needs its normal reviewed frontend/Core rollout to receive this change; shared source and the walkthrough have been updated, but its running servers were intentionally not restarted.

## Label-only follow-up

The return posting button is renamed from **Post stock** to **Post** in shared source and both walkthrough formats. Posting behaviour and permissions are unchanged. All 14 return dialog tests pass, including the new Post label after approval. This label-only follow-up has not been rebuilt/deployed to the running rehearsal frontend yet; include it in the next frontend build. No API restart or migration is needed for the label change.
