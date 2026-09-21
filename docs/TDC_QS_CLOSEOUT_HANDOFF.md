# QS UAT closeout handoff

Verified 20 September 2026. Continue the existing UAT records; do not recreate transactions or restore the database. Finance changes remain deferred by the user.

## Preserved records

| Record | Reference / state |
| --- | --- |
| Project | PRJ-2026-0003, `cb23b1c6-d663-4017-b86a-976933fa98b7` |
| Works contract | CTR-2026-00003, `9c957633-fc63-44f4-b3ed-5f47fbd21016`, Active, GHS 11,000 after the approved variation |
| Certificate | IPC-2026-00001, Approved, GHS 12,000 net; in-app PDF preview verified |
| Finance invoice | VI-2026-00004, Draft, GHS 12,000, paid zero; leave unchanged |
| Claim | CLM-2026-00001, independently Approved: GHS 80 approved, GHS 20 rejected, settlement Pending |
| Practical completion | UAT-QS-PC-20260920, Completed; explicitly simulated UAT evidence |
| Contract documents | UAT-QS-practical-completion.pdf and UAT-QS-takeover-inspection.pdf; both central-DMS linked and scan-clean |

## Closeout configuration requiring owner decisions

`TDC-PROCUREMENT` profile `35147034-f84e-4a02-989d-6c7a22501bc7` is Draft. All fourteen decisions currently contain `{}` and have no approval or verified evidence. This requires preparing actual decision values and evidence, not merely publishing an otherwise completed profile.

| Decision | Subject | Existing owner group |
| --- | --- | --- |
| DEC-001 | Procurement method thresholds | Procurement + Legal/PPA |
| DEC-002 | Approval authority matrix | Procurement + Legal/PPA |
| DEC-003 | Workflow selection | MD + Procurement + Finance |
| DEC-004 | Authority and committee stages | Procurement + Legal/Internal Audit |
| DEC-005 | Petty purchase and waiver | Procurement + Finance |
| DEC-006 | Restricted/single-source prerequisites | Procurement + Legal/PPA |
| DEC-007 | Supplier fees | Procurement + Finance |
| DEC-008 | Document signatures | Legal + ICT + Procurement |
| DEC-009 | GHANEPS exchange profile | Procurement + ICT/PPA |
| DEC-010 | Negative stock policy | Stores + Finance + Internal Audit |
| DEC-011 | Supplier AVL and risk | Procurement + Internal Audit |
| DEC-012 | Deployment cutover gate | Steering Committee |
| DEC-013 | GRN/MRN receipt documents | Stores + Procurement + Finance |
| DEC-014 | Non-functional acceptance | ICT + Procurement + Stores |

The existing Works closeout service requires this published, complete profile. Publishing it affects shared Procurement behavior and includes Finance-owned rules, so it has not been populated with invented values or published under the current restriction.

An additional authority limit needs review: the published `TDC-F05B-NCT` policy v9 has one enabled Works authority rule, `UAT-QS-AUTH-WORKS`, covering GHS 0-10,000. Initial/final takeover and closeout use the current contract value, GHS 11,000. Its coverage must be resolved through the existing policy controls and the resulting authority route verified before submission. Do not reduce the contract value or submit a false amount to fit the limit.

## Remaining task list

- [x] Certificate Save PDF as path completed; the user confirmed the file arrived after a delay. The original toolbar download is not claimed fixed.
- [x] Dedicated uat.qs.preparer recorded and closed one synthetic snag and one zero-cost defect case; edit, resolution, closure, refresh and SQL readback passed. No maintenance cost, charge or settlement.
- [ ] Procurement and the listed owners provide the fourteen configuration decisions and evidence, then use independent approval, validation and publication.
- [ ] Procurement resolves Works authority coverage for the revised GHS 11,000 contract and verifies the correct approver/workflow.
- [ ] Use `procurementapprover` for the existing contract closeout page; select the existing practical-completion item and both uploaded evidence documents. Verify readiness before submitting initial takeover. Use the actual workflow-assigned independent approver for the decision.
- [ ] Complete applicable defects/final-takeover/project-closure prerequisites through their existing owners. Do not fabricate retention releases: this fixture holds zero retention.
- [ ] Finance separately resolves adopted-budget/accounting setup and validates invoice posting, payment and reconciliation when authorized.

Detailed test evidence and chronological checkpoints remain in `docs/TDC_QS_STREAMLINING_TRACKER.md`. Overall acceptance remains In progress.
