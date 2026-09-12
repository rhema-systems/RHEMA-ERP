# Required walkthrough revision after 8 September UAT

Status: **Pending final walkthrough revision and rehearsal.**

User requirement: before final handover, update both
`TDC_CUSTOMER_PROCUREMENT_STORES_WALKTHROUGH.md` and its HTML counterpart
to incorporate the errors, corrections and missing steps encountered during UAT.
Do not report this checklist as the completed revised guide.

## Presentation requirements

- Follow one chronological customer-facing route; separate optional branches and troubleshooting.
- For every step state: account and verified role, starting page, exact button/tab,
  fields to complete, document type/file required, save/submit action, expected status,
  next responsible account, and the checkpoint that permits continuation.
- Do not skip intermediate dialogs, independent document review, approvals,
  signatures, publication, or evidence that a later stage requires.
- Keep the normal path detailed but readable; put date-expiry recovery and technical
  diagnostics outside the main walkthrough. Do not prescribe SQL changes or bypasses.
- Distinguish historical fixtures from the current demonstration and clearly label
  simulated documents. Do not include passwords.
- Recheck account assignments and temporary access; do not reuse historical temporary
  reviewer permissions as though they are still available.

## Corrections and missing stages to reconcile

1. Supplier onboarding: invitation/token, application, fees if applicable, document upload,
   independent review/registration and supplier-portal access.
2. Budget, plan and PR: exact approval handoffs and the transition from approved PR to sourcing.
3. Tender method before the compatible evaluation template; proposal templates/responses
   separate from the tender's required supporting-document checklist.
4. Controlled documents: draft/clone, start preparation, upload content, send for review,
   independent verification/approval, attach eligible content, publish document version,
   bind it to the tender, then publish the tender. Explain each distinct submission.
5. Bind dialog: approved version, approved validity period, calculated validity date,
   fee/currency and optional clause note; a short separate path for expired dates.
6. Supplier document issue/addendum dispatch and acknowledgement before bid submission;
   submission closure and opening time before bid opening.
7. Committee acceptance, individual attendance and separate quorum confirmation;
   clarify that acceptance alone does not confirm quorum.
8. Evaluation, retained award readiness, bidder verification, optional comments,
   any explicitly required evidence, Complete Verification and independent award approval.
9. Award notification, performance-bond request, supplier upload and authorized review.
10. Contract-first and PO-first branches: explain source links, exact approved quantities,
    descriptions, units and GHS values. Stock/ad hoc/service selection does not waive
    approved-source limits; stock mapping is needed before stock posting, not every PO save.
11. PO approval, independent receipt/inspection, GRN, invoice against accepted unbilled
    quantities, three-way matching, inventory and Stores issue/acknowledgement/return/count.
    Respect Finance ownership and distinguish missing setup from code defects.
12. Keep Petty Purchase, RFQ, Emergency, QBS and QCBS routes and test status explicit;
    do not present unexecuted branches as passed.

## Contract upload and signature detail (expand section B12)

- The current B12 compresses several operations and instructs PO approval before the
  required contract is ready. Reconcile it with the current PO compliance gate before handover.
- Explain ordinary Contract attachments versus signed-contract evidence.
- Exact observed path: contract → **Documents → Upload Document → Document Type**.
  **Signed copy** is the second dropdown option below **Contract**, not a separate tab.
- Explain when to upload draft/supporting documents, how independent approval proceeds,
  how both parties' signatory names/dates are recorded, and where the signed copy is retained.
- Explain that Active status alone does not satisfy the separate PO signature-evidence check.
- Include the return path to the PO and **Refresh compliance readiness** before submission.
- For the current record, both signatories/dates were present but only an ordinary Contract
  upload was recorded; do not claim signed-copy upload or PO submission has passed until verified.

## Current evidence and final acceptance

- Include line-specific planned landed costs: item **… → Planned landed costs → Save line costs**,
  then save the PO. Distinguish this from PO-wide estimates and from the supplier PO total.
  Cover cancellation, removal, reload, and targeted receipt allocation. See
  [the line-cost walkthrough](PO_LINE_PLANNED_LANDED_COSTS_20260908.md).

- User confirmed existing PO-2026-0003 **Save Changes works** after the backend retry fix.
- ContentType migration is applied locally; the existing three contract document rows were preserved.
- The Signed copy option was verified in the live upload dropdown; no file was uploaded by that check.
- These checkpoints do not establish an uninterrupted end-to-end pass.
- Update Markdown and HTML together, check links/labels/account handoffs, and rehearse the
  customer route in the visible browser before calling the final guide ready. Record any
  remaining blocker or untested branch explicitly.
