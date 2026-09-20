# Issue-time stock location — 10 September 2026

## Cause and change

Rehearsal requisition `REQ-20260910-0001` was approved for two PVC units with no requested location. Stock existed at `LOC-001`, but Issue Items sent a null location. The valuation owner correctly looked for unbinned stock and rejected the request: `Insufficient inventory for item SKU-001. Available: 0, Requested: 2`.

- Requesters may leave the location unspecified.
- The issuer selects an active location in the approved warehouse for each issue line. A previously requested location is preselected but can be changed by the issuer.
- Unspecified is distinct from explicitly selecting **Unbinned stock (warehouse level)**. The form requires a deliberate choice before issuing a positive quantity.
- The existing issue API receives the selected location. It retains warehouse/location access, approval, independence, quantity, tracking, valuation and accounting checks. No approved requisition is rewritten to work around those checks.
- Changing location clears any previously chosen location-specific tracking exception.
- Server failure details remain visible on the form; entered quantities are preserved.
- Receiver-only voucher viewing does not load the issuer's warehouse-location list.

## Validation

- 29 focused issue-dialog and requester-access tests passed, including four new location/error tests.
- Focused requester TypeScript check passed.
- Walkthrough B17/B18 now makes the requester's location optional and tells the manager to choose LOC-001 when issuing.
- No backend code, migration, Finance configuration or main UAT data was changed for this fix.

## Live rehearsal result

- Deployed to rehearsal port 3002 in production (`next start`) mode: build `iFfbdEx4cZxTpaox-Vemt`, frontend PID 38112 at verification. The existing rehearsal API was not restarted.
- Through the visible browser as John Manager, selected **LOC-001 - Main** and issued exactly **2 PVC Pipe 50mm units** against `REQ-20260910-0001`.
- Created **SIV-20260910-00001**. The voucher shows **Awaiting receiver** and **Finance posted**; Jane Employee must acknowledge receipt separately. No receiver acknowledgement was performed.
- Read-only rehearsal SQL verification confirmed one issue voucher and LOC-001 quantity on hand changed from **39 to 37**. The original requisition approval was preserved.
- Main UAT data and runtime remain untouched; this frontend change is not yet deployed there.

## Guided requester and receiver rehearsal

- The issue form now displays the eligible requester as **Receiver: Jane Employee (requester)** instead of presenting a mandatory-looking receiver dropdown. **Change receiver** opens the existing eligible-person selector only when another person will collect. Cancelling the change preserves the current receiver. If the requester is not an eligible active receiver, an explicit alternative is required.
- The independent receiver acknowledgement and server-side issuer/receiver checks are unchanged. No backend change or migration is needed.
- All 33 focused issue-dialog/access tests and the requester TypeScript check passed, including default requester, alternate collector, cancel and unavailable-requester cases.
- Created **REQ-20260910-0002** (`536f130c-798f-48bf-9840-9110af706eab`) through the visible rehearsal UI as **employee**, for **2 PVC Pipe 50mm units**, Operations / Project Demo Warehouse, required date **11 September 2026**. Header and item location were intentionally left unspecified.
- Submitted and approved through the configured Stores Manager Approval as **procurementapprover**. Read-only SQL confirms approved quantity **2**, issued quantity **0**, no issue voucher for this new request, and LOC-001 stock remains **37**.
- Stop on the manager issue screen for the user to inspect the location selector and automatic receiver. Do not issue this new request as part of preparing the walkthrough.
- Deployed and visually verified on port 3002 using production build `_qOcQjTmVW5Snmn4DsKrd` (frontend PID 62788 at verification). The new request's issue screen shows **Jane Employee (requester)**, **Change receiver**, **Select issue location**, quantity **0**, and no posted voucher. Verified that **Change receiver → Cancel change** keeps Jane and that the location menu offers **LOC-001 - Main**. Left the menu open for the user; no stock was issued.
- HTTP 200 and the rehearsal database isolation response header were verified. Rehearsal API PID 12356 and main UAT frontend/API PIDs 13628/42148 remain unchanged. The Markdown and HTML walkthroughs now explicitly say that receiver selection is automatic.

## Advanced tracking display and default issue location

- Moved the issue line's exception selector and its empty-state guidance inside **Advanced tracking options**. The section is collapsed for normal issues; lot, batch and serial inputs remain visible.
- Existing selected exception evidence opens the section initially. A visible **Exception selected** badge remains when it is collapsed, and collapsing does not clear the selected exception or change the issue payload.
- The existing location-change invalidation and backend tracking checks remain unchanged.
- Added **Default issue location** above the grid. Choosing a default fills only pending lines without an explicit location; requested locations, per-line overrides and explicit unbinned selections remain unchanged. Changing or clearing the default does not overwrite existing choices. The default resets when the form reloads.
- Renamed **Issue All Remaining** to **Fill remaining quantities**. It fills outstanding approved quantities only; neither this action nor location selection posts stock. The final **Issue** button remains separate.
- All 41 focused issue-dialog/access tests and the requester TypeScript check passed, covering multiple lines, partial/completed quantities, default location, overrides, explicit unbinned stock, reset and advanced exception persistence. No backend code or migration is required.
- Both walkthrough formats now include the default location and quantity-helper sequence. Production build/deployment verification follows below; no stock may be posted during UI verification.

### Combined rehearsal deployment

- Production build **`o_KoZ7jtJDSRFSeNaC6a-`** passed and was deployed to **http://127.0.0.1:3002**, frontend PID **59868** at verification. Regular checkout and isolated build source hashes matched. The API and main UAT processes were unchanged.
- HTTP 200 and the `RhemaERP_PO_Rehearsal_20260909` isolation response header were verified.
- Visible browser verification on **REQ-20260910-0002** as manager: choosing **Default issue location: LOC-001 - Main** populated the blank PVC line; **Fill remaining quantities** set exactly **2** without posting. Jane Employee remained the automatic receiver.
- **Advanced tracking options** was collapsed initially, revealed the exception selector when expanded, and was collapsed again for the handoff. The default controls fit the dialog; wide line details scroll within their table.
- Left the form prepared with quantity **2** and **LOC-001 - Main**, without clicking **Issue 2 Items**. No new SIV, stock posting, acknowledgement, migration or Finance change was performed for this update.
