# Isolated PO rehearsal - 9 September 2026

## Which environment to use

| Purpose | Frontend | Database |
| --- | --- | --- |
| Original customer UAT - preserve | http://localhost:3000 | RhemaERP |
| Rehearsal only | http://127.0.0.1:3002 | RhemaERP_PO_Rehearsal_20260909 |

The rehearsal API listens only on `127.0.0.1:5002`. Original API/frontend processes are not stopped, rebuilt or switched by the rehearsal scripts.

## Starting point

- Sign in on the rehearsal address using the same account/password that existed in UAT when the copy was made. Use `procurementofficer` to inspect the PO; approval still requires the appropriate independent approver.
- The tenant name in the copy begins with **REHEARSAL -**.
- Open PO-2026-0003: `/procurement/purchase-orders/a7393eec-565c-4ff5-8346-184315d8bc95`.
- Supplier PO amount: **GHS 52,000**. Barcode Device Kit has **GHS 200** planned freight, scoped to that line only. The other line is PVC Pipe 50mm. The linked contract, source records, accounts, approval evidence, budget and inventory are copied too.
- Keep the same record identifiers in the copied database. This is a full isolated snapshot, not a duplicate PO competing with the original for approved-source capacity.

## Test sequence (not yet accepted)

### Receiving-account preparation corrected — 9 September, 02:08 UTC

The user explicitly authorized fixing `manager` in **both** local copies. Each database already had an effective `TDC_STORES_OFFICER` responsibility restricted to DEMO-PM, but the matching Security role membership was missing. `Repair-LocalUatManagerStoresRole.sql` restored that one membership per database. Existing roles, role-permission definitions and warehouse/location assignments were preserved; no Finance permissions were added. The existing location scope includes LOC-001.

Verification: effective `procurement.inventory.read` and `procurement.inventory.receive` access for DEMO-PM / LOC-001 was confirmed from current configuration in each database. On the visible rehearsal browser, refreshing receipt-source readiness changed **Blocked — unavailable / RCV_SOURCE_FORBIDDEN** to **Ready**. The form was not submitted. Re-running the guarded repair added zero rows in both copies.

Main UAT's PO remains **Draft**; the rehearsal PO is **Approved**. Both still have zero receipt, GRN and invoice rows. Before/after fingerprints for this PO, its items, receipts, GRNs and invoices were unchanged during the repair. Main UAT therefore still needs its normal PO approval before receiving; permission setup does not bypass that stage. No server restart was performed.

Fresh manager API sign-in probes were rejected in both copies with the previously supplied credential; no password was reset or guessed and retries were stopped. Main-UAT browser sign-in/readiness remains to be checked with the user's current login. The rehearsal readiness result above used its already authenticated visible session.

1. Inspect the PO and all pre-submit controls. Resolve only genuine rehearsal prerequisites through the supported screens; do not bypass approval, signature or source checks.
2. Submit and approve the PO using its configured workflow and independent account. Obtain confirmation before final approval.
3. Receipt the intended quantities, perform independent inspection where required, and verify accepted quantities and GRN creation.
4. Verify the GHS 200 planned line cost is assigned only to the Barcode Device Kit. Check proportional treatment if the rehearsal receipt is partial and avoid duplicating a planned cost as both shared and line-specific.
5. Use the existing Finance invoice-generation flow against accepted, unbilled receipt quantities. Verify PO/GRN/invoice matching and report any Finance blockage without changing Finance code.
6. Compare the original PO's status, receipt and invoice state afterward. Rehearsal changes must exist only in the copied database.

Creating this environment does not mean approval, receipt, landed-cost allocation or invoice matching has passed. These UI tests remain to be performed.

## Isolation and evidence

- SQL COPY_ONLY backup was checksum-verified, restored to new physical files, and passed `DBCC CHECKDB ... PHYSICAL_ONLY`. All 457 migration-history rows were retained.
- The copied PO's row fingerprint matched the source snapshot, and the original PO fingerprint was unchanged after setup. Its commercial amount remained GHS 52,000.
- Only the copy's SMTP/SMS settings and LDAP access were disabled; its tenant display name was marked REHEARSAL.
- Rehearsal files are physical copies in `local-artifacts/po-rehearsal-20260909`; writable upload storage does not use the original UAT upload junction.
- Generated frontend assets in the copy were mechanically retargeted to the rehearsal API. Original frontend assets and Finance source code are unchanged.
- Browser `connect-src` permits only the rehearsal frontend and API. The rehearsal has separate JWT signing keys/audiences; keys and SQL credentials are supplied only through the child process environment, not saved in scripts or configuration files.
- All hosted background services are disabled. HTTP integrations use a loopback deny proxy; a probe returned HTTP 403. SMTP/SMS are disabled independently in the copied database. Real email delivery and background scheduling are not part of this rehearsal acceptance.
- Runtime verification passed: frontend HTTP 200 with the rehearsal CSP/environment header; API `/health/ready` HTTP 200 with Healthy database, virus-scanner and startup checks; login security-settings HTTP 200 with CORS restricted to the rehearsal origin. The visible rehearsal login tab was opened successfully. Authenticated business-flow tests remain pending.
- If a workflow depends on a background queue, diagnose that dependency in the copy before proceeding; do not silently bypass it or enable outbound production integrations.

## Restart and retained files

### PO PDF correction — rehearsal preview

The PO detail page now builds a supplier-facing A4 PDF from the complete saved PO, rather than taking a screenshot of the active tab. It includes company/supplier identity, delivery details, every item and the saved commercial totals/currency. Internal readiness cards, hashes, private notes and planned landed-cost estimates are not supplier-document content. Unapproved/cancelled/unknown-status documents are marked NOT FOR ISSUE. Print opens the same PDF and the user prints from its viewer.

As of 10 September, rehearsal on **127.0.0.1:3002** runs the updated source as a production build (`next start`), including the requisition cost-centre, searchable-item and fixed-height tab changes. The current frontend is under `local-artifacts/rehearsal-requisition-height-20260910/frontend`; the API remains under `local-artifacts/rehearsal-production-20260910/api`. The production launcher selects the updated frontend automatically. Main UAT on port 3000 remains separate and has not received these requisition changes.

Use `./scripts/procurement/Start-LocalPoRehearsalProduction.ps1` to restart the prepared production frontend; it requires the rehearsal API on 5002. `Start-LocalPoPdfPreview.ps1 -StartOnly` is the older development preview and is no longer the normal testing launcher.

Validation: 10 PDF/export regression tests passed, including eight-page long-description/terms pagination, all-item output, ad hoc service lines, currency and draft warnings. A4 fixture pages were rendered and visually inspected; no off-page text was detected across the eight-page stress test. The live rehearsal PO Export PDF action completed with its success message, and Print opened a PDF tab titled PO-2026-0003 - Purchase Order. The browser automation cannot inspect blob-URL PDF tabs; inspection of the actual user's downloaded file is tracked separately from these checks.

From the repository root, `./scripts/procurement/Start-LocalPoRehearsal.ps1` starts the prepared rehearsal only when ports 3002, 5002 and 5519 are free. It refuses to stop existing processes and now selects the prepared production runtime when available. `-ApiOnly` starts the updated rehearsal API when the deny proxy is already running and the API has stopped. Neither launcher targets ports 3000 or 5000.

`New-LocalPoRehearsalDatabase.ps1` and `Prepare-LocalPoRehearsalRuntime.ps1` are one-time setup helpers and refuse to overwrite existing targets. Do not rerun them to reset a tested copy.

The COPY_ONLY backup is retained in SQL Server's default backup folder as `RhemaERP_PO_Rehearsal_20260909.bak`. Do not restore it over `RhemaERP` or delete rehearsal data until it is no longer needed.
