# Procurement control accordions — 9 September 2026

## Behaviour

Policy, compliance, lineage and gate details start collapsed behind **Show details**. The title, concise current status, first actionable blocker and separate refresh/recovery controls stay visible. Expanding reveals the retained check evidence and history. Primary business actions, amounts, item tables and data-entry forms remain in their existing places.

This is presentation-only: readiness components remain mounted while collapsed, callbacks still run, entered values survive toggling, and server permissions, validation and approval rules are unchanged. Expanded content has no maximum-height clipping. Draft-submission checks on a non-Draft PR are labelled as reference information rather than a new submission blocker.

## Coverage

- PR: submission, budget, authority guidance, sourcing release, planning/governance linkage and four audit-history sections.
- PO: approved-source lineage, compliance, role independence and commitment details. Shared PO controls also cover their existing framework/receipt consumers.
- Procurement Budget: control level, warning threshold and effective/expiry dates; financial totals remain visible.
- Procurement Plan: linked-budget controls and retained review information; plan details/items remain visible.
- Contract: activation-check details and activation history; evidence upload, review and activation forms remain available separately.
- Receipt: governed-source capacity and policy evidence; errors/retry remain visible.

## Checks

- Focused TypeScript check: `npx tsc -p tsconfig.procurement-controls.json --pretty false` passed.
- All 14 tests across six focused suites passed: collapsed visibility, focusable native-button/ARIA behaviour, persistent mounted controls/input values, visible blockers/retry, failed-readiness handling, PO commitment/SOD/receipt regressions and budget workflow/revision actions.
- Visible rehearsal browser: expanded/collapsed PO compliance (all 10 checks), PR budget evidence, Budget policy controls and Plan linked-budget details. Compact PO layout visually reviewed. These were read-only UI checks, not new approval/receipt transactions.

## Runtime boundary

The updated files are loaded into the isolated preview at **http://127.0.0.1:3002**. The original UAT frontend on **localhost:3000** and backend were not restarted. Original UAT needs a deliberately scheduled frontend build/switch to load this change. No Finance-module or backend code was changed for the accordion work.

## Receipt-tab follow-up — loaded into rehearsal

The preceding runtime statement covers the earlier PR/PO/Budget/Plan/source-card rollout. The following receipt-tab changes are in the regular source checkout and were loaded into the rehearsal on 9 September after the user approved the restart. They are **not** loaded into the original UAT runtime:

- **GRN / MRN:** receipt checks and each document's technical/audit history start collapsed. Blockers, signature status, delivery evidence uploads and permitted document actions remain visible. Redundant explanatory text and the disabled create-documents action are removed from the main view when documents already exist.
- **Quality Inspection:** quantities, required evidence, quality holds and permitted actions remain visible. DEC references and posting/audit details move into **Inspection history & technical details**. Irrelevant supplier/exception badges are omitted.
- **Inspection actions** replaces **Lifecycle action**. Comments are optional for **Save inspection** and **Submit** in both the UI and request DTOs/service. Blank comments are stored as absent user notes, while audit events still record an explicit automatic action summary. Independent decisions, supplier responses and exception resolution/closure still require their existing reasons. Evidence, quantity, workflow, SOD, concurrency and permission checks are unchanged.
- The UAT walkthrough's B13 and B14 instructions are synchronized in Markdown and HTML. No Finance code or database records were changed for this follow-up; no migration is required.

Verification: **40 frontend tests** passed across six focused suites; the focused TypeScript check passed; **43 backend receipt-inspection rules tests** passed using isolated build artifacts, including optional-comment validation, length limits, retained decision reasons and deterministic audit fingerprints. Five preview source files match the tested regular checkout, and the deployed Core assembly matches the isolated tested build. API liveness returned HTTP 200/Healthy; the updated receipt route compiled and served HTTP 200. These checks do not claim a live Save/Submit transaction on the updated version.

The rehearsal API was restarted with the existing database/outbound-isolation startup script and fresh signing keys. Its new process is 21436 on port 5002; the preview remains on port 3002. The browser was refreshed and opened at login with a return link to REC260003. The user must sign in again before authenticated visual verification or any Save/Submit test. Original UAT process IDs remained 42148 (API 5000) and 57476 (frontend 3000). No receipt/signature/approval records were changed by this deployment. Previous runtime files are retained in `receipt-ux-backup-20260909-162402` under the rehearsal runtime for rollback.

Live API verification also passed: `/health/ready` returned HTTP 200, and the running Swagger schemas show nullable, non-required comments on Save/Submit while the Decide request still requires its comment and row version. The login page was visibly confirmed after the expired session cleared.

## Receipt inspection evidence selector — 9 September, 17:03 UTC

- Fixed the **Quality Inspection → Controlled evidence → Published DMS document** selector, which previously offered every accessible active/published Procurement DMS record. The GRN/MRN `…0003` entries seen there were existing library records, not a new MRN created by opening Quality Inspection. The current receipt's configured document register `…0004` was not changed.
- Eligible choices now require a source link to the exact receipt or inspection case, or membership in that receipt's current saved source-evidence attachments. Generated `ProcurementReceiptDocument` GRN/MRN records are excluded. Display references/filenames alone never establish ownership. Internal delivery-evidence requirements use the current ready waybill attachment and its exact published version, linked automatically. External users are not sent to the internal-only attachment API.
- Selection detail is rechecked before linking; loading failures show a retry/refresh path and do not fall back to an unrestricted list. Comments and quantities are retained on evidence-load failure. Existing backend tenant/source/version/scan validation remains unchanged; this is a frontend selection fix, not a server-policy or database change.
- **52 frontend tests** passed across seven suites, including wrong-receipt/GRN/MRN/superseded/unpublished exclusions, exact-version matching, automatic attachment linking, optional-comment request handling and failure-state retention. Focused TypeScript and diff-whitespace checks passed.
- The helper and component are hash-matched into the port-3002 preview. Browser verification on REC260003 showed only its attached file plus the empty selection option; no GRN/MRN or unrelated documents remained. Accepted 40, rejected 0, Draft status and two recorded inspection events remained unchanged. No live Submit/approval/signature was performed. No backend restart or original UAT runtime change was needed. Previous component retained in `receipt-evidence-backup-20260909-170110`.
- B13 step 10 in the Markdown/HTML UAT walkthrough now describes receipt-scoped choices, automatic waybill linking, refresh and separately required inspection reports.
