# Ghana AP WHT — implementation basis and verification gate

Status: focused automated verification passed; final independent review and relational/UAT gates remain. Not a tax filing approval or production certification.

The user authorized Ghana-specific research as implementation direction for D07. Finance remains responsible for approving supplier classification, exemptions, contract evidence and configured effective tax definitions.

## Primary-source basis

- [GRA withholding practice note DT/2016/001](https://gra.gov.gh/wp-content/uploads/2020/09/Practice-Note-on-Withholding-of-Tax.pdf), sections 4.6 and 4.9: the qualifying contract threshold is exceeded, not merely reached; related contracts in the statutory period are aggregated and the crossing event requires catch-up. VAT/CST are excluded from the income-WHT base.
- [GRA current withholding guidance](https://gra.gov.gh/domestic-tax/tax-types/withholding-tax/) distinguishes goods, works, entity services, individual services and other treatments. The configured GHS2,000 contract threshold must not be indiscriminately applied to every supplier/payment type.
- [GRA 2026 VAT guidance under Act1151](https://gra.gov.gh/wp-content/uploads/2026/01/VAT-Guidelines-for-VAT-ACT-1151.pdf) supports the current standard VAT/levy configuration used in the examples below. Do not reuse obsolete VAT-withholding examples from older web pages to configure current standard VAT.

The earlier explanation that only the current payment is subject to WHT after threshold crossing was incomplete and is superseded by cumulative catch-up.

## Server-authoritative calculation

For supported ordinary AP invoices, net supply is the persisted post-trade-discount subtotal; invoice tax is not silently folded into income WHT. Partial gross settlement is allocated proportionately to net supply. A payment-time cash discount does not rewrite the approved invoice or its VAT: a tax-base adjustment requires governed source correction/credit evidence.

The public calculation endpoint requires approved invoice settlement evidence. It ignores a client-entered taxable base for those settlements. The payment posting service recalculates and compares the deduction before posting. A displayed estimate is not an authority to post.

Contract qualification includes other governed invoices in the supplier/tax/supply-category/statutory-year scope, including unpaid invoices not selected in the payment form. A new reference number does not reset the scope. Foreign-currency scope fails closed rather than treating commercial settlement or invoice book FX as statutory authority; see the additional research below.

Historical cumulative payment bases are frozen per allocation. Legacy allocations without that evidence fail closed pending Finance reconciliation; they are not reconstructed from today's invoice values. Certificates cannot substitute gross settlement for a missing frozen statutory base. Mixed historical rates requiring rate-specific catch-up and backdated settlements before later posted WHT payments require governed correction.

## Executable examples to verify

| Scenario | Net supply settled | Invoice tax settled | Income WHT | Cash before any cash discount |
|---|---:|---:|---:|---:|
| GHS10,000 service invoice; standard tax GHS2,000; 7.5% | 10,000.00 | 2,000.00 | 750.00 | 11,250.00 |
| Half of that gross invoice settled | 5,000.00 | 1,000.00 | 375.00 | 5,625.00 |
| Trade discount GHS500 already reduces net supply; 7.5% | 9,500.00 | 1,900.00 | 712.50 | 10,687.50 |
| Goods threshold crossing: prior net1,000 +900, current900; 3% | 900.00 current | 0.00 | 84.00, including57.00 catch-up | 816.00 |
| Cumulative qualifying amount exactly GHS2,000 | 2,000.00 cumulative | 0.00 | 0.00 | Depends on current allocation |

## Explicit limitations and release gates

- The three supply categories are the application's current aggregation granularity. They do not prove that all legal contracts or like supplies have been fully captured. Finance must reconcile unbilled/external contracts and supplier exemptions; missing external evidence must not be presented as a verified statutory register.
- A GHS-functional tenant may settle a foreign-currency invoice under Ghana WHT only when an active, approved `GhanaStatutory` Mid/Reference rate exists for the exact recognition date, identifies Bank of Ghana as the source and retains a source reference. Selected payment bases use the payment date; governed contract-qualification bases use each invoice date. A missing, inactive, unapproved, wrong-date or non-BoG rate fails closed.
- A foreign payment settling a GHS invoice does not require a second statutory conversion because its WHT supply base is already GHS. Non-GHS functional-currency tenants remain blocked pending a separately governed Ghana statutory-reporting currency design.
- Each foreign allocation freezes the statutory rate ID, GHS conversion factor, recognition date, source and source reference. Posted legacy foreign-invoice WHT without this evidence remains readable/cancellable but cannot create or alter certificates/remittances until Finance reconciles it.
- The ordinary net-supply formula requires reconciled subtotal + tax = gross. Nonstandard excise-inclusive bases, special exemptions, historical amendments and tax-adjusting cash discounts need explicit Finance treatment; no generic legal conclusion is inferred from a tax code's name.
- New migration `20260930000200_ApWithholdingNetBasisEvidence` is additive and does not fabricate historical backfill. It remains unapplied pending the combined release/rehearsal gate.
- New migration `20260930000700_GhanaStatutoryWhtFxEvidence` adds the immutable allocation evidence and blocks downgrade after retained evidence exists; it requires relational migration verification before release.
- Latest focused verification: WHT lifecycle 31/31 passed, statutory-rate import 3/3 passed, and targeted frontend ESLint passed. Relational migration replay and authenticated end-to-end execution remain release gates until separately recorded.

## Additional currency research and explicit safety boundary

[GRA-hosted Revenue Administration Act915, section21](https://gra.gov.gh/wp-content/uploads/2023/01/Revenue-Administration-Act-2016-.pdf) requires GHS conversion using the Bank of Ghana inter-bank rate at the relevant tax recognition date, subject to statutory exceptions or Commissioner-General authority. This establishes that an approved commercial Buy/Mid rate and its accounting carrying value are not automatically sufficient statutory evidence.

`GhanaStatutory` is deliberately separate from commercial Daily/Buy/Sell/Month-End rates. It is exact-date, Mid/Reference-only and requires Bank of Ghana provenance. The AP calculation converts net-of-invoice-tax supply into GHS, freezes the evidence on each allocation, recalculates before posting, and reuses the frozen GHS amounts for certificates and remittances. Commercial invoice, payment and book FX continue to govern accounting carrying values and realized FX; they cannot satisfy the statutory-rate predicate.

This is an engineering implementation of the cited general conversion rule, not a GRA ruling. Finance/tax owners remain accountable for exemptions, alternative Commissioner-General directions, source-bulletin retention and the legal recognition date for exceptional transactions.
