# TDC Finance Requirements: Current Disposition

Date assessed: 2026-08-12

Baseline: TDC Finance Requirements Issue Log and Action Tracker, version 2.0 dated 2026-07-17,
`Requirements Traceability` sheet; updated Finance SRS/management document dated 2026-07-17.

## Executive conclusion

The current repository has implemented the genuine, sufficiently defined, Finance-only functional gaps
selected from the July baseline. It is not accurate to state that every TDC software requirement or every
documented limitation is fully resolved and accepted.

Three different completion concepts must remain separate:

1. **Software implementation**: code, migrations where needed, focused tests, and implementation notes exist.
2. **Representative UAT/acceptance**: TDC data, configured authority/rules, operating scenarios, and named
   business reviewers prove that the implementation fits TDC's process.
3. **Enterprise dependency/operations**: another module, external provider, infrastructure owner, or formal
   management decision supplies the contract and operating evidence.

## July `Gap - Finance` rows now implemented as Finance features

| Baseline requirement | Current disposition | Implementation evidence |
|---|---|---|
| FR-GL-006 | Implemented; representative UAT pending | Production recurring journals |
| FR-PC-001, FR-PC-002, FR-PC-006, FR-PC-007 | Implemented; representative UAT pending | Period-close calendar, checklist, dashboard, certification and escalation |
| FR-AR-009 | Implemented; representative UAT pending | AR collection follow-up workspace |
| FR-CB-008 | Implemented; representative UAT pending | Controlled payment slips and official receipts |
| FR-FA-007 | Implemented; representative UAT pending | Fixed-asset physical verification |
| FR-BG-012, FR-BG-013 | Implemented; representative UAT pending | Controlled virements and supplementary budgets |
| FR-RP-009, FR-RP-010, FR-RP-012 | Implemented; representative UAT pending | Versioned templates, report automation and authorised ad hoc builder |
| SRS-CONTROL-007 | Implemented for Finance-owned exceptions; cross-module escalations remain with their owners | Close alerts/escalation and governed Finance exception workflows |
| NFR-PER | Software assurance foundation implemented; representative SQL Server UAT pending | Performance profiles, central thresholds, measured index and runbook |
| NFR-AVL, NFR-BCK, NFR-SUP | Software evidence foundation implemented; operational sign-off pending | Separated health probes, evidence evaluator and backup/restore/retry/support runbook |

## Requirements not closed by Finance-only implementation

### Undefined scope requiring a TDC decision

`SRS-INT-004` names SH Fund, PF, ESB, and fuel allocation modules/ledgers without defining the business
process, source system, accounting rules, users, reports, or ownership. It remains a genuine requirement
clarification item. Building guessed ledgers would risk creating parallel or incorrect products.

### Cross-module and external dependencies

The baseline explicitly blocks or assigns outside Finance the PMS/property/revenue billing master and
interfaces, legal debtor case management, banking/mobile-money integrations, procurement/GHANEPS and
commitment events, payroll calculation/journal interface, stores/GRN sources, and final Navision/cutover
decisions. Finance has implemented controlled posting/import/reconciliation boundaries where possible,
but cannot truthfully mark the source workflows or external integrations complete.

### Configuration, migration, UAT, and operational acceptance

Many baseline rows were already classified as `Implemented - Pending UAT` or `Partial - Design/UAT Needed`.
They still require TDC chart/dimension catalogues, approval thresholds, statutory presentation decisions,
representative data, reconciliation, role/segregation review, accountant sign-off, and final operating
evidence. `FIN-LIM-0017` remains the umbrella cutover/sign-off gate.

## Current limitation position

The following entries remain open, partially resolved, accepted with a remainder, or implementation-complete
but awaiting external acceptance:

- `FIN-LIM-0014`: tenant-aware role assignment / central authorization product decision;
- `FIN-LIM-0016`: fixed-asset umbrella should be reassessed, but its remaining production blockers include
  cross-module `FIN-LIM-0028` and `FIN-LIM-0040`;
- `FIN-LIM-0017`: representative migration, reconciliation, and accountant sign-off;
- `FIN-LIM-0028`: procurement-origin fixed-asset capitalization;
- `FIN-LIM-0040`: fixed-asset disposal proceeds into AR/cash and statutory sale tax;
- `FIN-LIM-0046`: accepted non-blocking remainder for rich formatted packs not selected as product outputs;
- `FIN-LIM-0047`: external Ghana portal/direct-submission and distribution interfaces;
- `FIN-LIM-0048`: PR #66 implements specialised advance/WHT opening support, but merge, migration, rehearsal,
  reconciliation, and accountant acceptance remain separate gates;
- `FIN-LIM-0050`, `FIN-LIM-0051`, and `FIN-LIM-0053`: cross-module payment-method, Sales-to-AR invoice, and
  asymmetric Procurement/Finance commercial-term decisions; and
- `FIN-LIM-0055`: software resilience foundation implemented; TDC operational rehearsal and acceptance pending.

## Claim that is safe to make

> The identified and sufficiently defined Finance-only functional gaps in the July TDC baseline have been
> implemented in the current development programme. Final TDC acceptance, representative migration/UAT,
> operational resilience evidence, and explicitly cross-module/external requirements remain tracked and
> must be completed or formally accepted before production go-live.
