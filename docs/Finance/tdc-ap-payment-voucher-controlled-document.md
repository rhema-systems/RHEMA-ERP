# TDC AP Payment Voucher - Controlled Document Slice

**Implemented:** 2026-08-03  
**Work package:** WP4 - AP vouchers, supporting evidence, statements, and WHT  
**Scope:** Finance-owned AP payment document output only; no Procurement, bank, mobile-money, or external tax interface

## Outcome

RHEMA ERP now produces a controlled A4 AP payment voucher directly from the existing
`VendorPayment` transaction. The voucher is a document view of the canonical payment and is not
a second payment, allocation, approval, numbering, or posting engine.

The existing payment number is intentionally used as the voucher number. This gives Finance one
reference across the payment, supplier allocation, WHT, GL journal, reversal, audit trail, and
printed voucher.

## Controls implemented

- Tenant isolation is applied explicitly before the payment is loaded.
- The existing Finance bank-account read scope is enforced before voucher data is disclosed.
- The existing document endpoint requires `Finance.Reports.Export` for
  `Finance.AP.PaymentVoucher`.
- Only `Authorized`, `Processed`, `Cleared`, `Reconciled`, and `Reversed` payments can render.
  Draft, pending, failed, and voided records cannot produce a document that might be mistaken for
  authority to release funds.
- Every generated copy records `Finance.AP.PaymentVoucherGenerated`, including copy type, payment
  status, linked journal, evidence counts, and SHA-256 hash of the emitted PDF.
- A reversed payment remains printable as retained audit evidence but carries an explicit red
  reversal notice, reversal date, linked compensating journal, actor, and reason.
- The printable layout includes:
  - tenant identity and canonical voucher/payment number;
  - supplier/payee and masked bank-account details;
  - method, references, amount, amount in words, exchange rate, and WHT withheld;
  - invoice allocations or supplier-advance state;
  - linked central-posting journal and account coding;
  - direct or payment-batch approval identity and workflow approval rows;
  - linked workflow evidence names, versions, verification state, and shortened hashes;
  - source identifier, generation actor/time, and page numbering.
- Standard font ligatures are disabled so archived voucher text remains searchable and copies
  cleanly without changing the rendered layout.

## Existing foundations reused

- `VendorPayment` and `VendorPaymentAllocation` remain the operational source.
- `IDocumentNumberingService` remains the only payment/voucher sequence owner.
- `FinancePostingEvent` / `JournalEntry` remain the only GL posting source.
- `PaymentBatch` and the workflow engine remain the approval source.
- `WorkflowEvidenceDocument` remains the controlled file/evidence source.
- `IFinanceAccessScopeService` remains the Finance data-scope owner.
- `IFinanceAuditService` remains the audit owner.
- The existing document-output service and QuestPDF pipeline remain the rendering owner.
- The existing posted-payment compensating reversal remains the correction path (`FIN-LIM-0009`).

## User workflow

On an eligible AP payment detail page, a user with `Finance.Reports.Export` sees **Print Voucher**.
The action requests an original controlled PDF through the document-output API and opens the
browser print flow. Browser-printing the React screen is no longer the AP voucher path.

Direct API route:

```text
GET /api/documents/Finance.AP.PaymentVoucher/{vendorPaymentId}?format=pdf&copyType=Original
```

Supported copy labels are `Original`, `Reprint`, and `Copy`. Each generation is audited; it does
not mutate the payment.

## Verification

- API solution build: passed with zero errors.
- Focused automated tests cover:
  - valid PDF and generation audit;
  - draft-payment rejection;
  - cross-tenant concealment;
  - Finance bank-scope enforcement;
  - explicit document export policy.
- The generated sample is A4, one page, PDF 1.7, visually inspected at 150 DPI, and contains no
  clipped or overlapping content.
- Text extraction confirms the final PDF has no ligature/null-character artifacts and retains the
  posting and allocation labels used for archive search.

## Limitation disposition preserved

- `FIN-LIM-0002`: the resolved backend print/export foundation is reused and regression-protected.
- `FIN-LIM-0004` and `FIN-LIM-0019`: current WHT/tax values remain visible without replacing the
  existing tax calculation or certificate services.
- `FIN-LIM-0009`: the implemented AP compensating reversal is shown as one traceable voucher chain.
- `FIN-LIM-0014`: existing Finance bank-account data scopes are enforced on document output.
- `FIN-LIM-0018`: the voucher never writes directly to GL or bypasses the posting engine.
- `FIN-LIM-0045`: supplier advances and unapplied allocation state remain visible.
- `FIN-LIM-0054`: the voucher exposes current WHT data, but the full threshold/remittance/
  certificate lifecycle remains open for a later WP4 slice.

## Explicitly remaining in WP4

The direct-payment submission, policy-driven evidence/exception controls, Managing Director
authority route, and payment-detail upload/review workspace are now implemented in the next WP4
slice documented in `tdc-ap-payment-evidence-exception-and-md-authority.md`.

Supplier statement PDF/native spreadsheet output is now implemented by
`tdc-ap-supplier-statement-controlled-output.md`.

WP4 still needs to provide the full WHT threshold, remittance, certificate
issue/reissue/cancel, and statutory register flows.

No database migration was required for this slice because it extends the existing payment,
workflow-evidence, journal, access-scope, audit, and document-output models.
