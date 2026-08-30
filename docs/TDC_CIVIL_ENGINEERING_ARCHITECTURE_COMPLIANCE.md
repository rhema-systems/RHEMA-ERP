# TDC Civil Engineering Architecture Compliance Evidence

## Baseline

Authoritative source: **TDC ERP Architecture and Design Document (002)**, principally sections 16.5, 16.5.1, 16.5.2, 16.6, 18 through 22 and the cross-cutting NFR catalogue referenced by section 19.2.

The architecture document does not define `FR-CE-*` identifiers. This matrix assigns local trace IDs `CE-ARCH-001` through `CE-ARCH-034` without changing the source requirement. The separate Civil Engineering section response is supplementary operating detail only.

Status meanings:

- **Implemented**: the owning code path exists and has focused automated evidence.
- **Partial**: implementation exists, but a required integration, output or acceptance result remains open.
- **Configuration**: TDC must publish tenant-specific workflows, roles or controlled values.
- **UAT required**: automated evidence is not a substitute for authenticated business acceptance.

## Architecture requirement matrix

| Trace | Architecture outcome | Current owner/evidence | Status |
| --- | --- | --- | --- |
| `CE-ARCH-001` | Works planning, technical assessment, execution oversight, inspection, progress, completion, defects and handover | Projects/Civil design, supervision, inspection, progress, maintenance and closeout services | Implemented; UAT required |
| `CE-ARCH-002` | Integrate Planning/GIS, Property, Maintenance, QS, Procurement, Finance, DMS and Reporting | Owner-scoped links and read-only projections; no duplicate Finance, QS, Procurement, DMS or Maintenance owner | Partial; integrated UAT required |
| `CE-ARCH-003` | Works request records project/site, category, scope, constraints, risks and recommendation | Governed Civil works/design case initiation | Implemented; UAT required |
| `CE-ARCH-004` | Initiate from approved capital work, maintenance escalation, property need, planning condition, directive, defect or infrastructure improvement | Controlled works-initiation source catalogue | Implemented; UAT required |
| `CE-ARCH-005` | Technical assessment conditionally routes Engineering, Planning/GIS, QS, Finance, Procurement and approving authority | Shared workflow binding and controlled configuration decisions | Partial; configuration and UAT required |
| `CE-ARCH-006` | Validate site, spatial reference, layout conformity and development constraints | Planning/GIS validation with append-only revisions | Implemented; UAT required |
| `CE-ARCH-007` | Site instructions retain responsible engineer, contractor response, drawings, corrective action and version history | Governed site-instruction routing, evidence, response and revision history | Implemented; UAT required |
| `CE-ARCH-008` | Inspections retain findings, photos, drawings, GPS/spatial reference and corrective action | Governed inspection control and central-DMS evidence | Implemented; UAT required |
| `CE-ARCH-009` | Progress records milestones, dates, percentage, site status, contractor update, delay, risk, dependency and management action | Weekly supervision and milestone snapshot controls | Implemented; UAT required |
| `CE-ARCH-010` | Execution timeline combines instructions, RFIs, tests, progress, risks, defects and corrective actions | Civil site controls and immutable owner histories | Implemented; UAT required |
| `CE-ARCH-011` | Defects, snag lists, corrective actions and clearance govern completion | Projects non-conformance/snag/DLP owners plus Civil inspection overlay | Partial; closeout UAT required |
| `CE-ARCH-012` | Completion inspection, certificate, handover, retention/DLP and closure approval | Project handover/DLP, Civil inspection and Works Closeout integration | Partial; closeout UAT required |
| `CE-ARCH-013` | Closeout requires inspection, defect clearance, certificate, handover, current as-builts, QS/Finance and asset/property update | Closeout rules and project-to-asset reconciliation | Partial; exactly-once integrated UAT required |
| `CE-ARCH-014` | Civil measurements/progress link to BOQ, valuation, variation, certificate and payment recommendation | Read-only Quantity Survey commercial-readiness and IPC links | Implemented; integrated UAT required |
| `CE-ARCH-015` | Contractors, contracts, POs and service-delivery evidence link to Civil works | Procurement/Business Partner owner selectors and source revalidation | Implemented; integrated UAT required |
| `CE-ARCH-016` | Civil uses Finance budget, commitment, cost-impact and posting references without duplicating the ledger | Project budget/Finance owner projections | Partial; Finance-integrated UAT required |
| `CE-ARCH-017` | Variations, EOT, additional works and cost impact are approved before execution | QS variation owner plus Civil EOT control | Implemented; integrated UAT required |
| `CE-ARCH-018` | Variation approval validates project, contract, instruction, evidence, reason, time/cost, QS value, contract and budget | QS, Civil, Procurement, Finance and DMS lineage | Partial; exact sequence UAT required |
| `CE-ARCH-019` | Completion/handover validates inspections, defects, photos, as-builts, QS certificate, contractor obligations and takeover | Civil, Projects, Procurement Works Closeout and asset/property links | Partial; independent takeover UAT required |
| `CE-ARCH-020` | Completed works and defects update relevant Maintenance, Property and Fixed Asset history | Maintenance owner links and project-asset reconciliation | Implemented; exactly-once UAT required |
| `CE-ARCH-021` | Work, inspection, instruction, progress, defect, completion and engineering-audit reports and dashboard | Civil report catalogue and dashboard | Partial until every named output reconciles in UAT |
| `CE-ARCH-022` | Enforce assessment approval, site evidence, versioning, progress confirmation, contractor response, defect control and handover approval | Civil configuration, workflow, DMS, revision and SQL controls | Implemented; configuration and UAT required |
| `CE-ARCH-023` | Acceptance proves registration through assessment, approval, spatial/procurement linkage, monitoring, certification, handover, reporting and audit | Architecture UAT script | UAT required |
| `CE-ARCH-024` | Support both capital works and operational interventions with complete property, parcel, budget, contract, contractor, document and approval lineage | Controlled initiation and owner projections | Partial; two-path UAT required |
| `CE-ARCH-025` | Permit integration covers Civil technical/structural/infrastructure review, inspection, comments and compliance outcome | Development approval file, section handoff, SCE review and HOD decision | Implemented; UAT required |
| `CE-ARCH-026` | Every workflow defines initiator, validation, review, approval, exception, posting point, evidence and output; unauthorized mutation is denied | Civil access registry, shared workflow, SOD, row-version and audit controls | Implemented; configuration and UAT required |
| `CE-ARCH-027` | Civil owns engineering inspection, instructions, oversight, progress, certification and technical reports | Civil interface ownership and owner-specific overlays | Implemented; UAT required |
| `CE-ARCH-028` | Reports filter by project/period/dimension, export Excel/PDF and retain drawing/photo/certificate access/version/audit controls | Shared reporting/export and central DMS | Partial; export/access UAT required |
| `CE-ARCH-029` | RBAC, least privilege, SOD, immutable audit, restricted amendment, controlled reversal and safe exceptions | Civil permission catalogue, workflow maker-checker, SQL guards and shared exception/audit services | Implemented; negative UAT required |
| `CE-ARCH-030` | Reuse common master data, workflow, DMS, security, audit, reporting and integrations | Civil extends Development/Projects and central owners | Implemented |
| `CE-ARCH-031` | Web, mobile field execution, dashboards and notifications | Civil web workspace, Project Mobile and central notifications | Partial; mobile/offline/notification UAT required |
| `CE-ARCH-032` | VM10 CAD/Engineering services support engineering files and project-record integration | Central DMS controlled file exchange; no verified VM10 service integration | Architecture deployment dependency |
| `CE-ARCH-033` | Trace requirements to configuration, workflow, report, integration, data, verification and acceptance | This matrix, tracker, interface-control document and UAT script | Partial until runtime evidence is attached |
| `CE-ARCH-034` | Civil satisfies the architecture security, audit, performance, availability, reliability, data, usability, scale, integration, backup, maintenance, compliance, reporting and support NFRs | Shared platform controls | NFR certification required |

## Owner boundaries

- Civil Engineering owns technical assessment, engineering review, site instructions, technical inspections, progress oversight, defect clearance recommendation, completion certification and technical reporting.
- Projects owns the project, stage, member, milestone, handover and project-asset relationship.
- Planning/GIS owns authoritative parcel, spatial and planning records.
- Quantity Survey owns BOQ, valuation, variation and payment-certificate values.
- Procurement owns supplier/contractor eligibility, sourcing, awards, contracts, purchase orders and works-closeout commercial controls.
- Finance owns budgets, commitments, journals, invoices and payments.
- Maintenance owns job cards, work orders and maintenance-asset lifecycle.
- Property/Fixed Assets own authoritative property and asset records.
- Central DMS owns file storage, scan status, versions, retention and access.
- Shared Workflow, Security, Audit, Notifications and Reporting remain central platform owners.

## Automated verification checkpoint — 2026-08-29

The current workspace passed the following serialized Civil Engineering gates:

- Core/shared Civil rules, policy, workflow, audit, migration-guard and report-catalogue tests: `236` passed, `6` SQL-only tests skipped in the non-SQL run, and `0` failed (`242` total).
- Civil API controller, authorization, route and service-contract tests: `60/60` passed after a successful API compilation with `0` errors.
- Civil frontend catalogue, component, workflow and service tests: `29/29` files and `58/58` tests passed.
- Focused ESLint: `115` changed/new Civil, workflow and report TypeScript files passed.
- Real SQL Server: `6/6` disposable Civil integration suites passed against SQL Server 2022 using integrated authentication. They exercised the actual migration SQL, foreign keys, tenant lineage, lifecycle triggers and append-only history. Cleanup verification found `0` remaining `RhemaERP_Civil*` databases.
- The release-candidate Data build completed with `0` errors. EF discovered all `38/38` new Civil migrations and `has-pending-model-changes` returned `0`, confirming model/snapshot parity.
- A full empty-database rehearsal remains blocked before the Civil migrations by the pre-existing historical migration `20260304155434_RecreateHRTables`, whose `AspNetRoles` data operation lacks an entity mapping/explicit column types. The disposable rehearsal database was removed. This is a repository bootstrap defect outside the Civil slice and must be resolved without rewriting an already-applied migration.
- The repository's full frontend `tsc --noEmit` gate did not return source diagnostics because the TypeScript compiler terminated with its own `Debug Failure: No error for last overload signature` under both Node 22 and Node 20. Focused lint and all selected frontend tests remain green; this tooling failure is retained as an explicit release limitation.

These checks establish an automated implementation baseline. They do not replace published tenant configuration, authenticated browser/API UAT, deployed integration reconciliation or TDC sign-off.

## Release gate

Civil Engineering is not architecture-accepted until:

1. a genuine tenant Civil profile and required workflows/roles are independently approved and published;
2. focused backend, frontend and migration tests pass;
3. the Civil migration chain and trigger behavior pass on a disposable real SQL Server database and leave no residue;
4. the architecture UAT script is executed with distinct users against deployed code;
5. capital-work and operational-work journeys both reach controlled completion/handover;
6. QS, Finance, Procurement, Maintenance, Property/Fixed Asset and DMS links reconcile to their authoritative owners;
7. reports, exports, audit, security, tenant-isolation, retry, stale-row, mobile/offline and NFR checks have retained evidence.
8. the combined EF model and migration snapshot report no pending changes in the release candidate worktree;
9. the historical empty-database bootstrap and full frontend compiler gates complete without infrastructure/tool failures.

Deployment alone is not compliance evidence.
