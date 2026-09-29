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
- Ghana WHT settlement is blocked for non-GHS functional-currency tenants and scopes containing foreign invoices/payments until governed statutory-currency conversion is implemented. This does not block foreign-currency deposits/returned cheques in D09. GHS-only WHT remains the executable demo scope; foreign WHT is an open release gap, not a completed feature.
- The ordinary net-supply formula requires reconciled subtotal + tax = gross. Nonstandard excise-inclusive bases, special exemptions, historical amendments and tax-adjusting cash discounts need explicit Finance treatment; no generic legal conclusion is inferred from a tax code's name.
- New migration `20260930000200_ApWithholdingNetBasisEvidence` is additive and does not fabricate historical backfill. It remains unapplied pending the combined release/rehearsal gate.
- Focused frontend arithmetic/allocation tests:20/20 passed, focused ESLint passed. Combined WHT lifecycle, AP payment, certificate-document and Ghana tax-engine tests:154/154 passed,0 skipped (TRX `outputs/finance-demo/test-results/wht-reviewed/Akwas_RHEMA-AKWASI_2026-09-29_20_24_15.trx`). Fast-mode compilation excluded historical EF target models and snapshot; SQL concurrency, migration replay and authenticated end-to-end execution remain unverified.

## Additional currency research and explicit safety boundary

[GRA-hosted Revenue Administration Act915, section21](https://gra.gov.gh/wp-content/uploads/2023/01/Revenue-Administration-Act-2016-.pdf) requires GHS conversion using the Bank of Ghana inter-bank rate at the relevant tax recognition date, subject to statutory exceptions or Commissioner-General authority. This establishes that an approved commercial Buy/Mid rate and its accounting carrying value are not automatically sufficient statutory evidence.

The current exchange-rate model has a free-text source, but no separately governed statutory-purpose/date linkage for contract qualification and deduction. Neither source-book FX nor current commercial settlement FX is therefore asserted as the legal threshold basis. The release guard rejects selected foreign invoices, other foreign governed invoices in the same threshold scope, and foreign prior-payment history. A proper extension must retain approved statutory rate identity/date, GHS contract and payment bases, reconciliation to accounting currency, and immutable certificate/correction evidence. Finance/tax-owner validation is required before enabling it; no GRA ruling is implied by this engineering decision.
