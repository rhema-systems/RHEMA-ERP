# Canonical Business Partner Finance UAT data

Use this pack after a fresh Finance seed. Replace `<OPEN_DATE>` with a date in an open tenant fiscal period. Use two different users for preparation and approval so maker-checker controls are genuinely exercised.

## 1. Seeded controls to verify before testing

| Control | Expected seed |
|---|---|
| Functional currency | GHS |
| Cash and cash equivalents | `1000` — Cash and Cash Equivalents |
| AR control | `1100` — Accounts Receivable |
| WHT receivable | `1130` — Withholding Tax Receivable |
| AP control | `2000` — Accounts Payable |
| WHT payable/control | `2200` — Tax/VAT Control |
| Services WHT | `WHT-SERV` — 7.5%, purchases, GHS 2,000 threshold |
| Goods WHT | `WHT-GOODS` — 3%, purchases, GHS 2,000 threshold |
| Works WHT | `WHT-WORKS` — 5%, purchases, GHS 2,000 threshold |
| AR services WHT | `WHT-REC-SERV` — 7.5%, sales/receipt side |
| AR VAT withholding | `VAT-WHT-REC` — 7%, sales/receipt side |
| Payment terms | Net 30 |

Finance settings remain authoritative for accounts `1100` and `2000`. Any similarly named Business Partner or Procurement posting fields must not replace them.

## 2. Existing seeded partners for baseline tests

| Code | Name | Role/use | Currency | TIN | Expected default |
|---|---|---|---|---|---|
| `TDC-DEMO-SUP-001` | Tema Engineering Services Ltd | Supplier/services | GHS | `C0000000010` | WHT-SERV, 7.5%; expense `300-6500-P101` |
| `TDC-DEMO-SUP-002` | Volta Office Solutions Ltd | Supplier/goods | GHS | `C0000000029` | WHT-GOODS, 3%; expense `100-6200-0000` |
| `TDC-DEMO-SUP-003` | Global Infrastructure Systems Inc | Supplier/FX | USD | `FOREIGN-DEMO-003` | No WHT; expense `300-6500-P101` |
| `TDC-DEMO-CUS-001` | Tema Industrial Estate Residents Association | Customer | GHS | `C1000000011` | Not a withholding agent; credit limit GHS 500,000 |
| `TDC-DEMO-CUS-002` | Meridian Property Holdings Ltd | Customer | GHS | `C1000000020` | Withholding agent; credit limit GHS 750,000 |
| `TDC-DEMO-CUS-003` | Atlantic Development Partners Ltd | Customer/FX | USD | `FOREIGN-DEMO-C03` | Not a withholding agent; credit limit USD 1,000,000 |

## 3. Partners to create manually

### UAT-BP-001 — multi-role positive case

- Legal name: Apex Integrated Services Ltd
- Roles: Supplier, Contractor and Customer, all active
- Currency: GHS
- TIN: `C-UAT-000001`
- Address: 14 Independence Avenue, Accra
- Email: `finance@apex-uat.invalid`
- Phone: `+233 30 200 0101`
- Payment terms: Net 30
- AP profile effective from: `<OPEN_DATE>`
- AP subject to withholding: Yes
- AP defaults:
  - Services: `WHT-SERV`, default for AP
  - Works: `WHT-WORKS`, additional category default
- AR profile effective from: `<OPEN_DATE>`
- AR credit limit: GHS 250,000
- AR withholding agent: Yes

Create the record and profile drafts as the profile maker. Submit them, then approve them as a different Finance approver. Confirm all three roles remain under one `BusinessPartnerId` and each role has its own role row.

### UAT-BP-002 — goods supplier override case

- Legal name: Accra Office Mart Ltd
- Role: Supplier
- Currency: GHS
- TIN: `C-UAT-000002`
- Payment terms: Net 30
- AP subject to withholding: Yes
- Default: `WHT-GOODS`, 3%
- Effective from: `<OPEN_DATE>`

### UAT-BP-003 — no-WHT supplier

- Legal name: Coastal Equipment Ghana Ltd
- Role: Supplier
- Currency: GHS
- TIN: `C-UAT-000003`
- Payment terms: Net 30
- AP subject to withholding: No
- Effective from: `<OPEN_DATE>`

### Negative readiness records

| Code | Setup | Expected result |
|---|---|---|
| `UAT-BP-NEG-01` | Supplier role; WHT enabled; no TIN | Disabled for AP with `AP_WHT_TIN_REQUIRED` |
| `UAT-BP-NEG-02` | Supplier role; no approved effective AP profile | Disabled for AP with `AP_PROFILE_REQUIRED` |
| `UAT-BP-NEG-03` | Supplier role; WHT enabled; TIN present; no default WHT configuration | Disabled for AP with `AP_WHT_DEFAULT_REQUIRED` |
| `UAT-BP-NEG-04` | Customer role; no approved effective AR profile | Disabled for AR with `AR_PROFILE_REQUIRED` |

## 4. Transaction pack and expected results

Use zero-rated/no-tax invoice lines unless the test explicitly concerns VAT. This isolates the WHT amounts.

### T01 — AP default from canonical partner

- Partner: `TDC-DEMO-SUP-001`
- Supplier invoice number: `SUP-UAT-001`
- Invoice date/accounting date: `<OPEN_DATE>`
- Description: UAT engineering advisory services
- Line amount: GHS 10,000.00
- Expected WHT default: `WHT-SERV`, 7.5%
- Expected captured WHT: GHS 750.00
- Expected gross AP liability at invoice posting: GHS 10,000.00
- Expected posting: debit expense 10,000.00; credit AP control `2000` 10,000.00
- Expected identity evidence: populated `BusinessPartnerId`, supplier `BusinessPartnerRoleId`, approved `BusinessPartnerApProfileVersionId`, and partner code/name/TIN snapshots

### T02 — authorized WHT disable override

- Partner: `UAT-BP-002`
- Supplier invoice number: `SUP-UAT-002`
- Amount: GHS 10,000.00
- Default before override: `WHT-GOODS`, 3%, expected GHS 300.00
- Override: disable WHT
- Controlled reason: Statutory exemption
- Notes: `UAT exemption certificate EX-2026-001`
- Expected result: a user without `Finance.AP.OverrideWithholding` is blocked; an authorized user can submit the reason; invoice approval records the exception; captured WHT becomes zero

### T03 — WHT configuration/rate override requiring approval

- Partner: `UAT-BP-002`
- Supplier invoice number: `SUP-UAT-003`
- Amount: GHS 10,000.00
- Default: `WHT-GOODS`, 3%, GHS 300.00
- Override to: `WHT-SERV`, 7.5%, GHS 750.00
- Controlled reason: Correction of supplier default
- Notes: `UAT service invoice supplied by goods-default partner`
- Expected result: mandatory reason plus approval; the approved invoice snapshots the overridden configuration and rate

### T04 — AP partial-payment WHT

Use posted invoice `SUP-UAT-001` with gross liability GHS 10,000 and captured WHT GHS 750.

First settlement:

- Gross invoice allocation: GHS 4,000.00
- Proportional WHT: GHS 300.00
- Cash paid: GHS 3,700.00
- Expected posting: debit AP 4,000.00; credit cash 3,700.00; credit WHT payable/control `2200` 300.00
- Remaining gross invoice balance: GHS 6,000.00
- Remaining WHT capacity: GHS 450.00

Final settlement:

- Gross invoice allocation: GHS 6,000.00
- Proportional WHT: GHS 450.00
- Cash paid: GHS 5,550.00
- Expected cumulative WHT: GHS 750.00, never more
- Expected invoice balance: zero

Verify the AP WHT report and certificate evidence show both settlements and the cumulative cap.

### T05 — AR invoice and customer withholding receipt

- Partner: `TDC-DEMO-CUS-002`
- Customer invoice number/reference: `CUS-UAT-001`
- Description: UAT property management services
- Invoice amount: GHS 20,000.00
- Expected invoice posting: debit AR control `1100` 20,000.00; credit revenue 20,000.00
- Receipt settlement amount: GHS 20,000.00
- Customer WHT configuration: `WHT-REC-SERV`, 7.5%
- WHT suffered: GHS 1,500.00
- Cash received: GHS 18,500.00
- Certificate number: `WHT-CERT-UAT-001`
- Certificate date: `<OPEN_DATE>`
- Expected receipt posting: debit cash 18,500.00; debit WHT receivable `1130` 1,500.00; credit AR `1100` 20,000.00
- Expected result: receipt owns the actual withheld amount and certificate; the partner profile only supplied withholding-agent status

### T06 — opening-balance invoice behavior

- Partner: `TDC-DEMO-SUP-001`
- Supplier invoice number: `SUP-UAT-OPEN-001`
- Opening balance: Yes
- Amount: GHS 5,000.00
- Expected result: supplier WHT default is not applied automatically

### T07 — FX partner identity continuity

- Partner: `TDC-DEMO-SUP-003`
- Supplier invoice number: `SUP-UAT-USD-001`
- Currency: USD
- Amount: USD 1,000.00
- Use an approved exchange rate effective on `<OPEN_DATE>`
- Expected result: no WHT default; AP transaction, FX evidence and any resulting Primary/Parallel postings all retain the same canonical `BusinessPartnerId`

## 5. Identity and non-regression assertions

For AP invoices/payments verify:

- `BusinessPartnerId` is populated.
- `BusinessPartnerRoleId` identifies the Supplier or Contractor role used.
- `BusinessPartnerApProfileVersionId` is the approved profile effective on the accounting date.
- Partner code, legal name and TIN snapshots are retained.
- No Finance Supplier master or `SupplierId` identity is created.

For AR invoices/receipts verify:

- `BusinessPartnerId`, Customer `BusinessPartnerRoleId` and `BusinessPartnerArProfileVersionId` are populated.
- Partner snapshots and receipt WHT certificate details are retained.
- No Finance Customer master or `CustomerId` identity is created.

Control-account assertions:

- AP always uses Finance account `2000`, regardless of Procurement partner account values.
- AR always uses Finance account `1100`.
- AP WHT payment recognition uses `2200`.
- AR WHT suffered uses `1130`.

## 6. Recommended execution order

1. Verify seeded controls and seeded partners.
2. Create `UAT-BP-001` through `UAT-BP-003` and the four negative partners.
3. Complete maker-checker AP/AR profile approval.
4. Run negative readiness selections before correcting any negative record.
5. Run T01, T02 and T03.
6. Run T04 and reconcile AP aging, WHT reporting and GL entries.
7. Run T05 and reconcile AR aging, receipt allocation, WHT receivable and certificate data.
8. Run T06 and T07.
9. Inspect API/backend identity fields and confirm no legacy master identities were created.
10. Run the Finance regression/build suite and archive screenshots, journal numbers, profile version IDs and posting IDs as UAT evidence.

