# Fixed Assets hardening backlog

Integration baseline: `955921178bef25d63c56fbf64d81877d52250fad`.

This document records the gaps discovered during the Finance Fixed Assets review so they remain visible after the demo-hardening package. The current assignment addresses the first six demo-critical areas only.

## In the demo-hardening assignment

1. Governed fixed-asset opening replacement after a successfully posted reversal.
2. Approved depreciation-run register and posting action.
3. Projection records must not consume real depreciation eligibility.
4. Safe import lifecycle, opening-posting gate, and depreciation-method validation.
5. Tenant-wide valuation history with filters, pagination, and export.
6. Posted-book dashboard KPIs and functional previews/exports for additions, depreciation, accumulated depreciation, valuations, roll-forward, and GL reconciliation.

## Deferred accounting and lifecycle gaps

- The HR/Payroll-backed location selector is restored through a tenant-scoped Finance read adapter,
  but this integration base does not contain the stable `LocationId` migration authored in checkpoint
  `8ab059db`. The demo-safe correction stores the selected hierarchy display name in the existing
  snapshot field. Restore the foreign key and legacy-data reconciliation as a separately approved
  migration unit; do not apply that checkpoint wholesale because it contains unrelated changes.
- Capital-project settlement posts Fixed Asset/AUC but leaves generated register assets as drafts without capitalization journal lineage. Manual project cost lines are tracking-only even though settlement credits AUC.
- Lease activation posts ROU asset/liability but leaves the generated ROU register asset as an uncapitalized draft. Lease modification, remeasurement, termination, impairment, and controlled reversal remain incomplete.
- Procurement accepted-supply capitalization has backend Finance adapter endpoints but no complete browser journey.
- Verification sessions can complete with unchecked items, directly rewrite registered location outside the transfer workflow, and do not create the security/write-off follow-up promised for missing assets.
- Gain-on-disposal accounts are constrained to Expense; policy redesign and existing-data remediation are explicitly deferred.
- Category setup permits an Expense impairment-reversal account while valuation posting currently requires Revenue; broader valuation-account policy redesign is deferred.
- Rich report charts, comparative graphics, advanced drilldowns, scheduled delivery, and advanced printable layouts are deferred.
- Full Units of Production spreadsheet import is deferred if reliable usage/capacity evidence cannot be added within the bounded safety correction.

## Demonstration cautions until the deferred work is completed

- Do not capitalize a Capital Project or activate a Lease in a stakeholder demo.
- Treat Verification as a checklist only; do not change location or mark an asset missing.
- Do not describe Procurement-to-asset capitalization as an end-to-end browser workflow.
- Use the governed Finance correction/reversal actions; never edit posted journals or book balances directly.
