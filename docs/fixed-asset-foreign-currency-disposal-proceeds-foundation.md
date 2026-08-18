# Fixed Asset Foreign-Currency Disposal Proceeds Foundation

Date: 2026-08-11

Scope: Finance-only resolution of `FIN-LIM-0043`

## Intent

TDC may dispose of an asset for proceeds denominated in a currency other than its functional currency. Finance must retain the commercial amount, translate it using controlled tenant exchange-rate policy, and calculate the disposal gain or loss in functional currency without mixing currencies.

## Implemented workflow

1. The maker enters sale proceeds, disposal costs, ISO currency, disposal date and the normal disposal evidence.
2. Finance confirms that the proceeds clearing account accepts the currency, then selects the latest active approved Daily exchange rate for the functional/transaction pair and the account-link or tenant FixedAssets quote-side policy. An eligible rate can also be selected explicitly by ID.
3. The request freezes native net proceeds, functional proceeds, rate ID/value/source/effective date/type/quote side, allocated NBV and gain/loss for checker approval.
4. Completion revalidates the exact rate and current tenant policy. Rate withdrawal or policy/evidence drift cancels the stale approval before posting.
5. The central posting engine records functional debit/credit values while the proceeds line preserves native currency, amount and exchange-rate evidence.

## Accounting treatment

`Functional proceeds = round(native net proceeds × approved rate)`.

`Disposal gain or loss = functional proceeds − functional NBV derecognised`.

The proceeds-clearing line carries the native transaction amount and currency, but the journal and fixed-asset transaction register use functional currency. This is translation at disposal recognition, not a later realized-FX settlement. The subsequent AR/cash settlement and sale-tax workflow is now resolved under `FIN-LIM-0040`; see `docs/fixed-asset-sale-settlement-foundation.md`.

## Controls and audit evidence

- Tenant, currency pair, Daily type, quote side, date effectiveness, active state and Approved/AutoApproved state are mandatory.
- A foreign-currency request cannot enter approval unless its clearing account is denominated in that currency or has an active effective currency link.
- Functional-currency proceeds cannot carry an exchange-rate ID and use a documented 1.0 snapshot.
- Explicit rate IDs are filtered by the same tenant and policy predicates as automatic selection.
- Maker-checker, period lock, idempotency, final depreciation, proportional disposal and revaluation-surplus controls remain unchanged.
- Disposal request, configuration-used and sale-proceeds audit evidence includes both native and functional values plus rate provenance.

## Deployment and verification

Apply migration `20260811160000_AddFixedAssetForeignCurrencyDisposalProceeds`, run the focused fixed-asset disposal regressions, and execute representative TDC UAT for at least one functional-currency sale and one approved foreign-currency sale. Migration application and UAT sign-off remain release gates.
