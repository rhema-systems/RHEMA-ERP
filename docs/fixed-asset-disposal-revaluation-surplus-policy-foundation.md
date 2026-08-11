# Fixed Asset Disposal Revaluation-Surplus Policy Foundation

Date: 10 August 2026

Scope: Finance-only resolution of `FIN-LIM-0041`

Requirements supported: `FR-FA-001`, `FR-FA-010`, `FR-GL-009`, `FR-GL-010`, `FR-GL-011`, `SRS-CONTROL-004`

## Stakeholder Summary

When TDC disposes of a revalued asset, the system now moves the asset-specific revaluation surplus directly to retained earnings in the same controlled disposal journal. The transfer remains wholly within equity and therefore cannot inflate or reduce the disposal gain or loss reported in profit or loss.

This closes `FIN-LIM-0041` with an auditable, maker-checker-aware policy rather than leaving Finance to prepare a separate manual journal. The disposal record retains the exact reserve account, retained-earnings account and amount approved for the transaction, even if Finance configuration changes later.

### Stakeholder wow factors

- One disposal event handles derecognition, proceeds, gain/loss and the related equity transfer without mixing equity into operating performance.
- The checker approves frozen account and amount evidence; the system cancels a stale approval if the reserve balance or policy account changes before posting.
- The resulting journal, posting event, disposal record and dedicated audit event provide a drillable chain from the asset register to the GL.
- Fixed-asset-to-GL reconciliation understands the equity transfer, so a properly cleared reserve does not appear as a false variance.
- No historic valuation, depreciation or capitalization record is rewritten.

## Policy Background

IAS 16 permits a revaluation surplus associated with an item of property, plant and equipment to be transferred directly to retained earnings when the asset is derecognised. Such a transfer is made within equity rather than through profit or loss. See the [official IAS 16 standard published by the IFRS Foundation](https://www.ifrs.org/content/dam/ifrs/publications/pdf-standards/english/2022/issued/part-a/ias-16-property-plant-and-equipment.pdf?bypass=on&ybI1BQ=JgCU2n).

The reasonable TDC default selected for this development baseline is:

1. Transfer the full remaining posted asset-specific revaluation surplus on whole-asset disposal.
2. Debit the category's active revaluation-surplus equity account.
3. Credit the tenant's configured retained-earnings equity account.
4. Include both lines in the same disposal posting event and journal.
5. Never include the transfer in disposal gain/loss.

This uses existing fixed-asset category and `FinanceSettings` account mappings. It does not create a parallel policy store or duplicate chart-of-account configuration.

## Operational Workflow

1. The maker requests a whole-asset sale, scrap, donation or damage/theft disposal through the existing disposal workspace.
2. Finance calculates the asset's disposal snapshot, including its remaining posted revaluation-surplus balance.
3. If the balance is positive, the service resolves and validates the category reserve account and tenant retained-earnings account as active, same-tenant, direct-posting equity accounts.
4. The disposal request freezes both account IDs and the transfer amount for checker review.
5. The independent workflow decision follows the existing disposal approval route.
6. Immediately before posting, Finance recalculates the reserve balance and re-resolves the policy accounts.
7. Any balance or account-policy drift cancels the stale approval and requires a fresh request. No journal is created.
8. A valid approval posts one balanced disposal journal, completes the asset disposal and records the dedicated equity-transfer audit event.

## Accounting Example

Assume a disposed asset has:

- gross asset balance: GHS 1,500;
- net book value used for disposal gain/loss: GHS 1,300;
- remaining asset-specific revaluation surplus: GHS 300; and
- no proceeds.

The disposal journal includes the ordinary derecognition and loss lines plus:

| Line | Debit | Credit | Financial-statement effect |
|---|---:|---:|---|
| Revaluation surplus | GHS 300 | - | Reduces the asset-specific equity reserve |
| Retained earnings | - | GHS 300 | Transfers the same amount within equity |

The GHS 300 transfer does not alter the GHS 1,300 disposal loss. This separation is deliberate and test-enforced.

## Data and Audit Evidence

Each applicable `AssetDisposal` stores:

- `RevaluationSurplusAtDisposal` as the disposal measurement snapshot;
- `RevaluationSurplusAccountId` as the approved source equity account;
- `RetainedEarningsAccountId` as the approved destination equity account; and
- `RevaluationSurplusTransferAmount` as the amount posted within equity.

The posting lines use the explicit tags `FA-DisposalRevaluationSurplus` and `FA-DisposalRetainedEarnings`. The dedicated `Finance.FixedAsset.DisposalRevaluationSurplusTransferred` audit action separates this policy event from the ordinary disposal-posted event.

## Controls and Failure Behaviour

- No transfer is created when the remaining asset-specific surplus is zero.
- A missing, inactive, cross-tenant, non-posting or non-equity account blocks the request before approval.
- The source and destination accounts cannot be the same.
- Balance or policy-account drift after approval cancels the approval; Finance must submit current evidence for a new checker decision.
- A normal posting failure leaves a valid unchanged approval retryable under the existing disposal workflow.
- The central posting engine retains its normal balanced-journal, period, idempotency and tenant controls.
- Existing development records receive zero/null defaults from the narrow migration; no legacy interpretation or data conversion is attempted.

## Reporting and Reconciliation

The disposal register exposes the transfer amount beside the revaluation-surplus snapshot. The fixed-asset GL reconciliation calculates the remaining subledger reserve as posted revaluation surplus less completed disposal transfers, then compares that amount with the revaluation-surplus GL account.

This means a reserve correctly cleared by disposal reconciles to zero rather than generating a false exception.

## Verification Coverage

Focused regression coverage proves:

- the full reserve transfers directly to retained earnings;
- the transfer never changes disposal gain/loss;
- missing retained-earnings setup blocks an applicable request;
- a changed retained-earnings policy account invalidates the checker decision;
- a changed revaluation-surplus balance invalidates the checker decision;
- stale approvals create no journal; and
- GL reconciliation subtracts completed disposal transfers from the asset-specific reserve subledger.

## Boundaries Still Open

This slice resolves only `FIN-LIM-0041`. The following remain separately controlled:

- `FIN-LIM-0039`: resolved by automatic final/partial-period depreciation through disposal date;
- `FIN-LIM-0040`: VAT, AR and cash/bank integration for sale proceeds;
- `FIN-LIM-0042`: proportional component or partial disposal is now resolved; and
- `FIN-LIM-0043` is now resolved for foreign-currency disposal recognition; its translated gain/loss remains separate from the within-equity reserve transfer.

The migration must be applied to the target database and representative TDC data must complete UAT before production use.
