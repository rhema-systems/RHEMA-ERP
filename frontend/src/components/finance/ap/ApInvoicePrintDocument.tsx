import React from 'react';

import { formatCurrency } from '@/lib/utils';
import type { VendorInvoice, VendorInvoiceLineItem } from '@/types/ap';

import styles from './ApInvoicePrintDocument.module.css';

export const AP_INVOICE_PRINT_BODY_CLASS = 'printing-finance-ap-invoice';

export function printApInvoiceDocument() {
  const cleanup = () => {
    document.body.classList.remove(AP_INVOICE_PRINT_BODY_CLASS);
    window.removeEventListener('afterprint', cleanup);
  };

  document.body.classList.add(AP_INVOICE_PRINT_BODY_CLASS);
  window.addEventListener('afterprint', cleanup);
  try {
    window.print();
  } finally {
    // Chromium blocks until its print dialog closes. This fallback also covers browser
    // runtimes that return without dispatching afterprint.
    window.setTimeout(cleanup, 0);
  }
}

interface ApInvoicePrintDocumentProps {
  invoice: VendorInvoice;
  tenantName?: string | null;
  tenantCode?: string | null;
}

const dateFormatter = new Intl.DateTimeFormat('en-GB', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
});

const dateTimeFormatter = new Intl.DateTimeFormat('en-GB', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
  timeZone: 'UTC',
});

function formatDate(value?: string): string {
  if (!value) return '—';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : dateFormatter.format(parsed);
}

function formatDateTime(value?: string): string {
  if (!value) return '—';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : `${dateTimeFormatter.format(parsed)} UTC`;
}

function resolvePrintStatus(invoice: VendorInvoice): string {
  if (invoice.status === 'Voided') return 'VOID';
  if (invoice.journalEntryId) return 'POSTED';
  if (invoice.status === 'Approved') return 'APPROVED — NOT POSTED';
  if (invoice.status === 'PendingApproval')
    return 'PENDING APPROVAL — UNPOSTED';
  if (invoice.status === 'Draft') return 'DRAFT — UNPOSTED';
  return invoice.status.replace(/([a-z])([A-Z])/g, '$1 $2').toUpperCase();
}

function lineGrossAmount(line: VendorInvoiceLineItem): number {
  return Number.isFinite(line.lineTotal)
    ? line.lineTotal
    : line.quantity * line.unitPrice;
}

function linePayableAmount(line: VendorInvoiceLineItem): number {
  return (
    lineGrossAmount(line) - (line.discountAmount ?? 0) + (line.taxAmount ?? 0)
  );
}

export function ApInvoicePrintDocument({
  invoice,
  tenantName,
  tenantCode,
}: ApInvoicePrintDocumentProps) {
  const currencyCode = invoice.currencyCode || 'GHS';
  const money = (value: number | undefined) =>
    formatCurrency(value ?? 0, currencyCode);
  const grossAmount = invoice.lineItems.reduce(
    (total, line) => total + lineGrossAmount(line),
    0
  );
  const documentTitle = invoice.isOpeningBalance
    ? 'AP OPENING BALANCE VOUCHER'
    : 'ACCOUNTS PAYABLE INVOICE RECORD';
  const printStatus = resolvePrintStatus(invoice);
  const printIsolationCss = `
        @media print {
            @page { size: A4 portrait; margin: 12mm; }
            body.${AP_INVOICE_PRINT_BODY_CLASS} {
                margin: 0 !important;
                background: #ffffff !important;
                color: #111827 !important;
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
            }
            body.${AP_INVOICE_PRINT_BODY_CLASS} > * {
                visibility: hidden !important;
            }
            body.${AP_INVOICE_PRINT_BODY_CLASS} .${styles.screenRoot} {
                display: none !important;
            }
            body.${AP_INVOICE_PRINT_BODY_CLASS} .${styles.printRoot},
            body.${AP_INVOICE_PRINT_BODY_CLASS} .${styles.printRoot} * {
                visibility: visible !important;
            }
            body.${AP_INVOICE_PRINT_BODY_CLASS} .${styles.printRoot} {
                position: absolute;
                inset: 0;
                display: block !important;
                width: 100%;
                min-height: 100%;
                margin: 0;
                padding: 0;
                overflow: visible;
                background: #ffffff !important;
                color: #111827 !important;
            }
        }
    `;

  return (
    <section
      className={styles.printRoot}
      aria-label={`${documentTitle} ${invoice.invoiceNumber}`}
      data-testid="ap-invoice-print-document"
    >
      <style>{printIsolationCss}</style>
      <header className={styles.documentHeader}>
        <div>
          <p className={styles.tenantName}>{tenantName || 'RHEMA ERP'}</p>
          <p className={styles.tenantMeta}>
            {tenantCode ? `${tenantCode} · ` : ''}Finance · Accounts Payable
          </p>
        </div>
        <div className={styles.headerIdentity}>
          <p className={styles.documentTitle}>{documentTitle}</p>
          <p className={styles.documentSubtitle}>
            Internal ERP register copy — not the supplier&apos;s source tax
            invoice
          </p>
        </div>
      </header>

      <div className={styles.statusBand}>
        <span>Document status</span>
        <strong>{printStatus}</strong>
      </div>

      {invoice.isOpeningBalance && (
        <section
          className={styles.openingNotice}
          aria-label="Opening balance control notice"
        >
          <strong>OPENING BALANCE — CUTOVER / MIGRATION</strong>
          <p>
            This is not a current supplier tax invoice. Opening tax, WHT and
            discount are system-controlled at zero.
          </p>
        </section>
      )}

      <section className={styles.partyAndReferenceGrid}>
        <div className={styles.panel}>
          <p className={styles.eyebrow}>Supplier</p>
          <p className={styles.primaryValue}>{invoice.supplierName}</p>
          <dl className={styles.compactList}>
            <div>
              <dt>Supplier reference</dt>
              <dd>{invoice.supplierInvoiceNumber || '—'}</dd>
            </div>
            <div>
              <dt>Canonical supplier ID</dt>
              <dd className={styles.auditValue}>{invoice.supplierId}</dd>
            </div>
          </dl>
        </div>

        <div className={styles.panel}>
          <p className={styles.eyebrow}>Document references</p>
          <dl className={styles.referenceList}>
            <div>
              <dt>System invoice</dt>
              <dd>{invoice.invoiceNumber}</dd>
            </div>
            <div>
              <dt>Source reference</dt>
              <dd>{invoice.reference || '—'}</dd>
            </div>
            <div>
              <dt>Purchase order</dt>
              <dd>{invoice.purchaseOrderNumber || '—'}</dd>
            </div>
            <div>
              <dt>Accepted supply</dt>
              <dd>{invoice.acceptedSupplySourceReference || '—'}</dd>
            </div>
          </dl>
        </div>
      </section>

      <section className={styles.dateGrid} aria-label="Invoice dates and terms">
        <div>
          <span>Invoice date</span>
          <strong>{formatDate(invoice.invoiceDate)}</strong>
        </div>
        <div>
          <span>Received date</span>
          <strong>{formatDate(invoice.receivedDate)}</strong>
        </div>
        <div>
          <span>Due date</span>
          <strong>{formatDate(invoice.dueDate)}</strong>
        </div>
        <div>
          <span>Currency</span>
          <strong>{currencyCode}</strong>
        </div>
        <div>
          <span>Payment terms</span>
          <strong>{invoice.paymentTermsDays || 0} days</strong>
        </div>
        <div>
          <span>Exchange rate</span>
          <strong>{invoice.exchangeRate || 1}</strong>
        </div>
      </section>

      <section className={styles.linesSection} aria-label="Invoice line items">
        <table className={styles.linesTable}>
          <thead>
            <tr>
              <th className={styles.lineNumber}>#</th>
              <th>Description / account</th>
              <th className={styles.numeric}>Qty</th>
              <th className={styles.numeric}>Unit price</th>
              <th className={styles.numeric}>Discount</th>
              <th className={styles.numeric}>Tax</th>
              <th className={styles.numeric}>Line amount</th>
            </tr>
          </thead>
          <tbody>
            {invoice.lineItems.map((line, index) => (
              <tr className={styles.lineRow} key={line.id || index}>
                <td className={styles.lineNumber}>{index + 1}</td>
                <td>
                  <div className={styles.lineDescription}>
                    {line.description}
                  </div>
                  <div className={styles.lineMeta}>
                    {line.lineItemType || 'Expense'}
                    {line.glAccountName
                      ? ` · GL ${line.glAccountName}`
                      : line.glAccountId
                        ? ` · GL reference ${line.glAccountId}`
                        : ''}
                  </div>
                </td>
                <td className={styles.numeric}>
                  {line.quantity} {line.unit || ''}
                </td>
                <td className={styles.numeric}>{money(line.unitPrice)}</td>
                <td className={styles.numeric}>{money(line.discountAmount)}</td>
                <td className={styles.numeric}>{money(line.taxAmount)}</td>
                <td className={styles.numeric}>
                  {money(linePayableAmount(line))}
                </td>
              </tr>
            ))}
            {invoice.lineItems.length === 0 && (
              <tr>
                <td className={styles.emptyLines} colSpan={7}>
                  No line items recorded.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </section>

      <section className={styles.summarySection}>
        <div className={styles.notesPanel}>
          <p className={styles.eyebrow}>Notes / source schedule</p>
          <p>{invoice.notes || '—'}</p>
          {!invoice.isOpeningBalance && invoice.matchingNotes && (
            <>
              <p className={styles.eyebrow}>Matching note</p>
              <p>{invoice.matchingNotes}</p>
            </>
          )}
        </div>

        <dl className={styles.totalsPanel}>
          <div>
            <dt>Gross line value</dt>
            <dd>{money(grossAmount)}</dd>
          </div>
          <div>
            <dt>Line discounts</dt>
            <dd>{money(invoice.discountAmount)}</dd>
          </div>
          <div>
            <dt>Subtotal after discount</dt>
            <dd>{money(invoice.subTotal)}</dd>
          </div>
          <div>
            <dt>Tax</dt>
            <dd>{money(invoice.taxAmount)}</dd>
          </div>
          <div className={styles.totalRow}>
            <dt>Invoice total</dt>
            <dd>{money(invoice.totalAmount)}</dd>
          </div>
          <div>
            <dt>Expected WHT at settlement</dt>
            <dd>{money(invoice.withholdingTaxAmount)}</dd>
          </div>
          <div>
            <dt>Amount settled / applied</dt>
            <dd>{money(invoice.paidAmount)}</dd>
          </div>
          <div className={styles.balanceRow}>
            <dt>Balance due</dt>
            <dd>{money(invoice.balanceAmount)}</dd>
          </div>
        </dl>
      </section>

      <footer className={styles.auditFooter}>
        <div>
          <span>Created</span>
          <strong>{formatDateTime(invoice.createdAt)}</strong>
        </div>
        <div>
          <span>Last updated</span>
          <strong>{formatDateTime(invoice.updatedAt)}</strong>
        </div>
        <div>
          <span>Posting journal</span>
          <strong>{invoice.journalEntryId || 'Not posted'}</strong>
        </div>
        <p>
          Internal control copy. Ledger effect is governed by the posting status
          and journal reference above.
        </p>
      </footer>
    </section>
  );
}
