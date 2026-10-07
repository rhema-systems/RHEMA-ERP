# TDC Quantity Survey SRS Reconciliation

**Decision date:** 6 October 2026
**Status:** Approved implementation interpretation

## Sources and precedence

This decision reconciles the repository's architecture baseline with *TDC Quantity Survey Management SRS Module.docx* and the earlier Quantity Survey questionnaire.

1. `docs/TDC_QUANTITY_SURVEY_ARCHITECTURE_REQUIREMENTS.md` remains the governing architecture and acceptance baseline.
2. The SRS supplies functional hierarchy, BOQ data vocabulary, and user-facing requirements that complement the architecture.
3. Questionnaire-only extensions such as SMM7/CESMM controls remain useful policy-driven capabilities. They do not become mandatory for every transaction unless an approved effective QS configuration requires them.
4. Shared platform ownership remains unchanged: Projects owns project structure, Finance owns cost centres and accounting, Procurement owns sourcing and contracts, and the central DMS, workflow, authorization, and audit services retain their existing authority.

## Canonical hierarchy and field ownership

| SRS concept | RHEMA ERP implementation | Decision |
| --- | --- | --- |
| Project | Project | Existing shared Projects record remains the root. |
| Phase | Project Phase | Selected on the Work Component / Work Package. Phase dates are maintained on the Phases tab. |
| Section | BOQ line Section | Controlled QS catalogue selector on the BOQ line. |
| Trade | BOQ line Trade | Controlled QS catalogue selector on the BOQ line. |
| Cost Code | BOQ line Cost Code | Controlled QS catalogue selector on the BOQ line. |
| Work Package | Project Package | The existing Work Component is the implementation container and is labelled **Work Component / Work Package** where the SRS term needs to be clear. |
| Activity | Project Work Item | A BOQ line may link to an existing non-Phase project work item. The activity must belong to the same project and, when it has a work-component assignment, to the same selected Work Component / Work Package. |
| Line Item | Project BOQ Item | Existing versioned BOQ line remains the commercial record. |
| Work Category | BOQ Item Type | The existing item-type control is labelled **Work Category / BOQ Item Type**. It retains measured item, provisional sum, prime cost, variation, and allowance behaviour. |

## Decisions applied to the UI and data model

- Keep the Work Component / Work Package dialog separate from the BOQ Item dialog. The records have different ownership, lifecycle, and cardinality.
- Do not add a Phase Weight input to the Work Component / Work Package dialog. The user enters **Work Component Completion Weight (%)**, which represents the component's share of progress inside the selected phase. Components in a phase may not exceed 100 percent in total.
- Keep Section, Trade, and Cost Code on the BOQ line.
- Keep Measurement Code optional unless the effective QS policy requires a controlled code or measurement standard.
- Do not add a free BOQ Cost Centre field. Finance owns cost-centre assignment and accounting dimensions.
- Add an optional **Activity / Project Task** selector to the BOQ line. Phase nodes and activities assigned to another work component are excluded.
- Store the Activity / Project Task reference on the working BOQ item and preserve its ID, node type, and title in every immutable BOQ version snapshot.
- Treat SRS “Work Category” as the current BOQ Item Type rather than creating a duplicate classification.

## SRS BOQ fields

The SRS line fields map as follows:

| SRS field | Implementation |
| --- | --- |
| Item Code | `ItemCode` |
| Description | `Description` |
| Unit of Measure | `UnitOfMeasure` |
| Quantity | `Quantity` |
| Unit Rate | `UnitRate` |
| Total Amount | Server-derived line amount |
| Cost Code | Controlled Cost Code catalogue reference and snapshot |
| Work Category | BOQ Item Type |

Section, Trade, Measurement Code, Work Component / Work Package, and Activity / Project Task are retained as controlled implementation detail that improves project, measurement, procurement, and reporting traceability.

## Validation and acceptance

- Tenant and project scope apply to every work-component, activity, and BOQ relationship.
- A Phase cannot be selected as a BOQ Activity / Project Task.
- An activity assigned to one work component cannot be attached to a BOQ line in another work component.
- Immutable BOQ versions preserve the activity snapshot so later task renaming does not rewrite approved history.
- The end-to-end UAT walkthrough creates the Work Component / Work Package, creates a project Task in the Plan tab, links that task to the BOQ line, and verifies the activity in the immutable snapshot.
- `scripts/vps/Get-QsUatReadiness.ps1` refuses to publish a stale walkthrough that omits the approved hierarchy and field-ownership guidance.

## Explicit non-decisions

This reconciliation does not make a measurement standard mandatory for every tenant, move cost-centre ownership into Quantity Survey, merge project phases with work packages, or replace the existing approval, Procurement, Finance, DMS, authorization, audit, and tenant controls.
