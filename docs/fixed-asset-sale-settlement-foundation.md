# Fixed Asset Sale Settlement Foundation

## Scope and intent

This slice resolves `FIN-LIM-0040` for TDC Finance-owned fixed-asset sales. An approved disposal can now create and post its statutory Accounts Receivable invoice and, when the proceeds were collected immediately, create and post the fully allocated customer receipt.

The implementation composes the existing Finance owners:

- `AssetDisposalService` owns disposal approval, derecognition and orchestration.
- `InvoiceService` owns customer credit, effective-dated sales tax and AR posting.
- `PaymentService` owns receipt methods, bank/liquidity destinations, allocation and receipt posting.
- `IFinancePostingEngine` remains the only GL posting authority.

No parallel invoice, receipt, tax or cash ledger was introduced.

## Operator workflow

For a sale with positive proceeds, the maker must select:

1. an active same-tenant `Customer`/`Both` business partner;
2. `CreditSale` or `ImmediateReceipt`;
3. `Standard`, `ZeroRated`, `Exempt` or `OutOfScope` tax treatment;
4. an active Sales/Both tax group for Standard treatment; and
5. for immediate collection, a configured payment method, exactly one appropriate bank/liquidity destination and any required transaction reference.

The checker approves those fields with the existing disposal-date depreciation, carrying-value, currency and gain/loss evidence. Completion then executes the entire accounting chain in one serializable SQL transaction.

## Accounting flow

Assume gross consideration of GHS 100, buyer/auctioneer deduction of GHS 5 and statutory tax of GHS 15.

The fixed-asset disposal posting includes:

- Dr Disposal Proceeds Clearing: GHS 95
- the established asset cost, accumulated depreciation/impairment and gain/loss lines

The linked AR invoice posting includes:

- Dr Accounts Receivable: GHS 110
- Cr Disposal Proceeds Clearing: GHS 100
- Dr Disposal Proceeds Clearing: GHS 5
- Cr Output Tax: GHS 15

The clearing account nets to zero. For immediate settlement, the canonical receipt adds:

- Dr selected Bank or Receipt Holding Account: GHS 110
- Cr Accounts Receivable: GHS 110

Tax is calculated on gross sale consideration. The evidenced deduction is an out-of-scope contra line, not a reduction of the statutory tax base.

## Controls and failure behavior

- A free-text buyer is no longer sufficient for a sale; the canonical customer ID is mandatory and the customer name is frozen as display evidence.
- Non-sale retirements reject dormant AR, tax or collection fields.
- Standard treatment requires an active same-tenant sales tax group; non-standard treatments reject a standard group.
- The public AR API cannot obtain the negative disposal-adjustment privilege by sending the enum name. It must match a completed same-tenant disposal, buyer and immutable disposal reference.
- Legacy bulk sales are rejected because their shared DTO cannot express per-asset legal buyer, tax and settlement evidence safely.
- If invoice creation/posting, tax calculation, receipt allocation/posting or any GL step fails, the outer transaction rolls everything back. The approved disposal is retained as retryable failure evidence.
- Disposal history links directly to the normal AR invoice and receipt workspaces.

## Persistence and deployment

Migration `20260813103000_AddFixedAssetDisposalSettlement` adds the customer, tax, settlement mode/status, payment destination and linked-document evidence to `AssetDisposals`, together with restrictive foreign keys, unique document links and state check constraints.

The migration is intentionally hand-authored and scoped. The shared multi-module EF snapshot currently contains unrelated drift and cannot safely scaffold this change without proposing existing tables as new objects.

## Verification and release gates

Automated coverage includes:

- credit-sale invoice construction and linkage;
- immediate receipt construction, allocation, posting and linkage;
- AR posting of the controlled buyer/auctioneer deduction;
- rejection of a spoofed disposal reference;
- existing disposal gain/loss, depreciation, partial/component, FX and reserve regressions.

Before production acceptance, apply the migration to the UAT/dry-run database and execute at least:

1. one standard-rated GHS credit sale;
2. one standard-rated immediate bank receipt;
3. one non-bank immediate receipt through its correct holding account;
4. one foreign-currency sale with an approved rate and matching-currency destination;
5. one injected receipt/tax failure proving the entire chain rolls back; and
6. one maker/checker and cross-tenant denial scenario.

Migration application, representative-data UAT, configured accounts/tax/payment methods and permission sign-off remain deployment gates rather than implementation gaps.
