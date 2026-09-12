# GRN-only receipt screen

User request: hide the MRN feature after confirming that MRN is not specified in the TDC architecture DOCX.

## Implemented scope

- Receipt detail navigation now says **GRN** and **GRN register**.
- MRN cards, signature/issue/cancel/download controls and card history are not rendered on the operational receipt screen. Numeric and string document-kind representations are supported.
- The GRN retains its existing signatures, issue, download and history actions. Server checks and authorization are unchanged. A failed check remains visible and prevents issue.
- The displayed reconciliation is explicitly the **GRN reconciliation**, not a claim that a retained pending MRN or the whole server register is reconciled.
- If a historical register contains no GRN, the screen explains the missing GRN and does not offer to create a duplicate register.
- Waybill upload guidance points to **GRN → Supplier delivery evidence**.
- Customer walkthrough Markdown and HTML now instruct the operator to issue the GRN only. Historical test outcomes mentioning MRN are retained as historical evidence.

This is a presentation change, not a deletion or a rewrite of published document policy/snapshots. Existing MRNs remain in the database and central document/audit records. The backend may still maintain its configured document set; no MRN was automatically issued, cancelled, approved or removed. Administrative configuration and historical document retrieval remain intact. No Finance changes or migrations were made.

## Verification

- 51 tests passed across ReceiptDocumentControl, ReceiptInspectionControl, receipt-document enum display and receipt-scoped inspection evidence.
- Focused TypeScript check passed, including the receipt detail page.
- `git diff --check` passed for the edited frontend and walkthrough files.
- The three changed UI files were mirrored to the isolated rehearsal preview; normalized source diff is empty.
- Live visible browser verification passed on rehearsal receipt `490b5d42-1a70-438a-9240-08de2223df82`: clicked the **GRN** tab; only **GRN-2026-0004** is rendered, with **Issued**, both retained signatures, **Open PDF**, and four recorded history events. No MRN card or actions are rendered. Receipt document checks show **6 of 6 checks passed / Ready**. No business operation was submitted during this check.

## Runtime boundary

The regular frontend source and rehearsal preview source both include this change. Main UAT's existing production frontend on **3000** has not been rebuilt or restarted. The rehearsal frontend on **3002** stopped responding to requests during verification and was restarted using the guarded preview startup script; main UAT and both API/database processes were not targeted. The receipt URL subsequently returned HTTP 200, and live post-restart browser verification passed as recorded above.
