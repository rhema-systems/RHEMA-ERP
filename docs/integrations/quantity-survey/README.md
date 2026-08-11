# TDC Quantity Survey Design Extraction Exchange Contract

- Contract ID: `tdc.qs.design-extraction.v1`
- Reconciliation ID: `tdc.qs.design-extraction.reconciliation.v1`
- Status: implementation-ready contract; business scope approval remains under `QS-CFG-014`
- Owner: Quantity Survey with ICT and Development
- Tracker: `QS-0405`

## Purpose And Boundary

This contract defines how Candy, PlanSwift, Bani Estimation, IFC/CAD/BIM adapters, and other approved estimating adapters exchange normalized design-comparison quantities with the ERP. It is an adapter contract, not a native-file parser.

The ERP accepts only the normalized JSON/CSV/XLSX profiles described below. Native design files remain immutable source evidence in the central Document Management System (DMS). An approved adapter may read a native file and produce this normalized contract, but the adapter must never write directly to the ERP database or update a BoQ.

This contract does not implement a live connector, native DWG/Revit/IFC parser, automatic BoQ mutation, variation creation, valuation, certificate, Procurement, Inventory, or Finance posting.

## Existing Owners Reused

| Responsibility | Authoritative owner | Contract rule |
| --- | --- | --- |
| Project, access and drawing register | Projects | The route-selected project and controlled drawing revisions are authoritative. Client-supplied tenant or user IDs are forbidden. |
| BoQ versions, stable line keys and publication | Projects / Quantity Survey BoQ | Only an approved, published BoQ version can be reconciled. Imports never update it directly. |
| Workbook template, preview and signed commit | Existing QS BoQ spreadsheet service | Third-party estimating exports that need BoQ staging must be transformed into the controlled `.xlsx` template. |
| File security and malware scanning | Central controlled-file upload service | Every retained source and normalized file must receive a real `Clean` outcome. `Skipped`, `Pending`, `Failed`, and `Infected` fail closed. |
| Physical file/version retention | Central DMS | Feature records retain DMS document/version references; no QS-owned physical file store is allowed. |
| Design-impact routing | `QS-0404` design revision impact | Approved reconciliation candidates select affected BoQ lines and enter the configured measurement or variation workflow. |
| Workflow and maker/checker | Shared workflow engine | Adapters cannot approve, reconcile, or apply their own output. |
| Authorization and project scope | Central permissions plus Projects access | Read, stage, reconcile and approve operations are separately authorized and tenant/project scoped. |
| Audit and exceptions | Central audit and exception middleware | Correlation, actor, request hash, DMS lineage and reconciliation history are retained centrally. |

## Exchange Profiles

| Profile | Media type / extension | Use | Processing status |
| --- | --- | --- | --- |
| Normalized manifest | `application/json`, `.json` | Envelope, design lineage, adapter identity, control totals and extracted lines | Required for adapter/API exchange |
| Normalized line file | `text/csv`, `.csv`, UTF-8 | Tabular line exchange using the exact header in `examples/tdc-qs-design-extraction-lines-v1.csv` | Optional companion to the manifest |
| Controlled BoQ workbook | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `.xlsx` | Existing signed BoQ preview/reconciliation flow | Existing ERP owner; macros and external links prohibited |
| IFC source | `.ifc` for approved IFC 2x3, IFC4 or IFC4.3 adapters | Immutable source evidence and adapter input | Evidence-only to the ERP; normalized output is required |
| Proprietary CAD/BIM/estimating source | DWG, DXF, RVT/RFA, NWD/NWC, PDF and approved vendor-native formats | Immutable source evidence | Evidence-only; never parsed by the ERP API |

Executable/scriptable files, macro-enabled workbooks, HTML/SVG active content, password-protected archives, generic ZIP uploads, external workbook links, and files whose extension, MIME signature and declared profile disagree are rejected.

Vendor support is an allowlisted adapter profile, not a user-entered string. Initial business candidates are Candy, PlanSwift, Bani Estimation, IFC, AutoCAD and Revit. ICT must register the adapter code, owner, public signing key or service credential, supported source versions and active period before use.

## Normalized Manifest

The authoritative schema is `schemas/tdc-qs-design-extraction-manifest-v1.schema.json`; a conforming payload is in `examples/tdc-qs-design-extraction-manifest-v1.example.json`.

Important rules:

- `schemaVersion` is exactly `tdc.qs.design-extraction.v1`.
- `exchangeId` is globally unique and is the unchanged retry identity.
- `adapterProfileCode` must resolve to an active tenant-controlled adapter profile.
- `projectReference` is compared with the route-selected project; it is not a substitute for project authorization.
- Previous and revised drawing numbers/revisions must resolve to the same controlled Projects drawing family, with the revised record superseding the previous record.
- `sourceArtifact.sha256` and `normalizedPayloadSha256` are uppercase 64-character SHA-256 hashes. The payload hash is calculated over canonical UTF-8 JSON excluding the `normalizedPayloadSha256` property.
- `boqLineKey` is the stable key exported by the approved/published BoQ. Item code alone is never sufficient for an automatic match.
- Quantities use invariant decimal notation and at most six decimal places. NaN, infinity, scientific notation and locale-formatted values are rejected.
- `unitCode`, `measurementCode`, `changeType` and `extractionMethod` are controlled values. Unit conversion is never inferred silently.
- Source element identifiers provide traceability but are not trusted as ERP identifiers.
- Control totals must exactly recompute from the normalized lines before staging succeeds.

## CSV Line Profile

The CSV file is RFC 4180-style UTF-8 with comma delimiters, a single header row, CRLF or LF line endings, and no extra columns. The exact ordered header is:

```text
ExternalLineId,BoqLineKey,BoqItemCode,Description,UnitCode,PreviousQuantity,RevisedQuantity,ChangeType,MeasurementCode,SourceElementIds,ExtractionMethod,ConfidencePercent,Comment
```

Rules:

- Maximum 2,000 data rows and 10 MiB per normalized CSV file.
- Fields are quoted when they contain commas, quotes or line breaks; embedded quotes are doubled.
- Spreadsheet-formula prefixes (`=`, `+`, `-`, `@`) are rejected in textual fields. A negative quantity remains permitted only after strict decimal parsing in the numeric column.
- `SourceElementIds` is a pipe-delimited list with no empty or duplicate member.
- Empty optional values remain empty; sentinel strings such as `N/A`, `NULL` and `0` are not substitutes.
- The manifest and CSV line count, ordered line identity and recomputed totals must agree.

## Security And Trust Model

1. An authenticated caller selects the tenant/project through server context and a controlled project selector.
2. The caller must have project access and the existing QS manage permission to stage. Independent approval uses the existing QS approval permission.
3. The server resolves the active adapter profile and never trusts a client-provided tenant, actor, permission, DMS path or approval state.
4. Source and normalized files are inspected for size, signature, archive expansion, macros, external links, active content and malware. Clean scan is mandatory.
5. The central DMS stores the original source, normalized manifest/CSV/XLSX, checksum, adapter version, project and drawing lineage as separate immutable versions.
6. Idempotency binds `exchangeId`, file hashes and the normalized request hash. Reusing the ID with different content is a conflict.
7. Parsing is staged and non-mutating. Validation and reconciliation complete before any workflow can begin.
8. Maker/checker, project scope, current BoQ/drawing/policy revalidation and row-version concurrency are enforced at every transition.
9. Logs and API errors do not expose storage paths, credentials, source file contents or stack traces. Unexpected errors are recorded by central exception logging with a correlation ID.

## Reconciliation

The authoritative response schema is `schemas/tdc-qs-design-extraction-reconciliation-v1.schema.json`; a conforming response is in `examples/tdc-qs-design-extraction-reconciliation-v1.example.json`.

Every external line receives one outcome:

- `ExactMatch`: stable BoQ line key, unit and quantity baseline match.
- `ToleranceMatch`: the configured, recorded tolerance permits the variance.
- `NewExternalLine`: no BoQ line is claimed; independent mapping is required.
- `MissingExternalLine`: an expected BoQ line is absent.
- `AmbiguousMapping`: more than one internal candidate exists.
- `UnitMismatch`: the unit differs and no approved conversion is configured.
- `QuantityVariance`: the baseline or revised quantity is outside tolerance.
- `DuplicateExternalLine`: external identity, BoQ key or source element is duplicated contrary to policy.
- `Rejected`: the source is invalid, unsafe, stale or unauthorized.

Only `ExactMatch` and explicitly approved `ToleranceMatch` lines may become workflow candidates. Every other outcome is blocking until resolved and re-reconciled. Resolutions must record an enumerated resolution code and reason, the independent actor, timestamp, before/after mapping and row version.

Reconciliation recomputes line counts and quantity totals, compares the manifest with the retained files, verifies the approved/published BoQ version and drawing lineage are still current, and produces a hash-bound immutable result. A successful result may create a `QS-0404` design-impact draft using controlled affected-line selection. It never updates a BoQ, creates a variation, or records a measurement directly.

## Reserved API Shape

These paths define a future connector boundary; `QS-0405` does not expose them yet:

```text
POST /api/quantity-survey/design-extractions/{projectId}/stage
GET  /api/quantity-survey/design-extractions/{projectId}/{exchangeId}
POST /api/quantity-survey/design-extractions/{projectId}/{exchangeId}/reconcile
POST /api/quantity-survey/design-extractions/{projectId}/{exchangeId}/submit-impact
GET  /api/quantity-survey/design-extractions/{projectId}/{exchangeId}/history
```

The stage request uses multipart form data for the manifest and optional normalized/source evidence. All lifecycle commands require an idempotency key, row version and non-empty reason. Expected failures return correlation-bearing Problem Details (`400`, `401`, `403`, `404`, `409`, `413`, `415`, `422`); unexpected failures remain with central exception handling.

## Versioning And Compatibility

- Contract versions are immutable. Breaking changes require `v2` schemas and a separate adapter activation.
- Additive optional fields require documentation, schema update, examples and regression tests without changing the version's meaning.
- Adapters declare one exact version; silent downgrade or best-effort parsing is forbidden.
- The ERP retains source contract version, adapter version, schema checksum and payload checksum with every staged exchange.
- Retiring an adapter prevents new stages but does not invalidate historical evidence.

## Acceptance And Sign-Off

Technical contract acceptance requires:

- machine-readable schemas and examples pass the focused repository contract tests;
- the CSV header and example remain synchronized;
- native files are explicitly evidence-only and central DMS remains the physical owner;
- no direct BoQ or variation mutation path is specified;
- security, idempotency, tenancy, project scope, reconciliation and maker/checker controls are explicit; and
- QS, ICT and Development approve the adapter allowlist, supported native source versions, tolerance rules, unit-conversion catalogue and operational ownership under `QS-CFG-014`.

Until that business sign-off and a representative adapter conformance run exist, `QS-0405` remains `In progress` even when the contract artifacts pass automated validation.
