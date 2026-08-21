import React from 'react';

import { formatCurrency } from '@/lib/utils';
import type { Invoice, InvoiceLineItem } from '@/types/ar';

import styles from './ArInvoicePrintDocument.module.css';

export const AR_INVOICE_PRINT_BODY_CLASS = 'printing-finance-ar-invoice';

export function printArInvoiceDocument() {
  const cleanup = () => {
    document.body.classList.remove(AR_INVOICE_PRINT_BODY_CLASS);
    window.removeEventListener('afterprint', cleanup);
  };

  document.body.classList.add(AR_INVOICE_PRINT_BODY_CLASS);
  window.addEventListener('afterprint', cleanup);
  try {
    window.print();
  } finally {
    // Chromium blocks until its print dialog closes. This fallback also covers browser
    // runtimes that return without dispatching afterprint.
    window.setTimeout(cleanup, 0);
  }
}

interface ArInvoicePrintDocumentProps {
  invoice: Invoice;
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

function formatDate(value?: string | null): string {
  if (!value) return '—';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : dateFormatter.format(parsed);
}

function formatDateTime(value?: string | null): string {
  if (!value) return '—';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : `${dateTimeFormatter.format(parsed)} UTC`;
}

function formatDocumentStatus(status: Invoice['status']): string {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2').toUpperCase();
}

function lineGrossAmount(line: InvoiceLineItem): number {
  const canonicalGross = Number(line.lineTotal);
  return Number.isFinite(canonicalGross)
    ? canonicalGross
    : Number(line.quantity) * Number(line.unitPrice);
}

function lineReceivableAmount(line: InvoiceLineItem): number {
  return (
    lineGrossAmount(line) -
    Number(line.discountAmount || 0) +
    Number(line.taxAmount || 0)
  );
}

function lineAccountLabel(line: InvoiceLineItem): string {
  const glLabel = [line.glAccountCode, line.glAccountName]
    .filter(Boolean)
    .join(' · ');
  if (glLabel) return `GL ${glLabel}`;
  if (line.glAccountId) return `GL reference ${line.glAccountId}`;
  return line.lineItemType;
}

export function ArInvoicePrintDocument({
  invoice,
  tenantName,
  tenantCode,
}: ArInvoicePrintDocumentProps) {
  const currencyCode = invoice.currencyCode || 'GHS';
  const money = (value: number | undefined) =>
    formatCurrency(Number(value) || 0, currencyCode);
  const grossAmount = invoice.lineItems.reduce(
    (total, line) => total + lineGrossAmount(line),
    0
  );
  const lineDiscounts = invoice.lineItems.reduce(
    (total, line) => total + Number(line.discountAmount || 0),
    0
  );
  const canonicalSubTotal = Number.isFinite(Number(invoice.subTotal))
    ? Number(invoice.subTotal)
    : grossAmount - lineDiscounts;
  const canonicalTaxAmount = Number(invoice.taxAmount) || 0;
  const documentTitle = invoice.isOpeningBalance
    ? 'AR OPENING BALANCE VOUCHER'
    : 'CUSTOMER INVOICE';
  const printIsolationCss = `
        @media print {
            @page { size: A4 portrait; margin: 12mm; }
            body.${AR_INVOICE_PRINT_BODY_CLASS} {
                margin: 0 !important;
                background: #ffffff !important;
                color: #111827 !important;
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
            }
            body.${AR_INVOICE_PRINT_BODY_CLASS} > * {
                visibility: hidden !important;
            }
            body.${AR_INVOICE_PRINT_BODY_CLASS} .${styles.screenRoot} {
                display: none !important;
            }
            body.${AR_INVOICE_PRINT_BODY_CLASS} .${styles.printRoot},
            body.${AR_INVOICE_PRINT_BODY_CLASS} .${styles.printRoot} * {
                visibility: visible !important;
            }
            body.${AR_INVOICE_PRINT_BODY_CLASS} .${styles.printRoot} {
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
      data-testid="ar-invoice-print-document"
    >
      <style>{printIsolationCss}</style>
      <header className={styles.documentHeader}>
        <div>
          <p className={styles.tenantName}>{tenantName || 'RHEMA ERP'}</p>
          <p className={styles.tenantMeta}>
            {tenantCode ? `${tenantCode} · ` : ''}Finance · Accounts Receivable
          </p>
        </div>
        <div className={styles.headerIdentity}>
          <p className={styles.documentTitle}>{documentTitle}</p>
          <p className={styles.documentSubtitle}>
            {invoice.isOpeningBalance
              ? 'Internal cutover control record'
              : 'Accounts receivable document copy'}
          </p>
        </div>
      </header>

      <section
        className={styles.statusBand}
        aria-label="Document and ledger status"
      >
        <div>
          <span>Document status</span>
          <strong>{formatDocumentStatus(invoice.status)}</strong>
        </div>
        <div>
          <span>Ledger status</span>
          <strong>{invoice.journalEntryId ? 'POSTED' : 'NOT POSTED'}</strong>
        </div>
      </section>

      {invoice.isOpeningBalance && (
        <section
          className={styles.openingNotice}
          aria-label="Opening balance control notice"
        >
          <strong>OPENING BALANCE — CUTOVER / MIGRATION</strong>
          <p>
            This is not a current-period customer tax invoice. Opening tax and
            discount are system-controlled at zero.
          </p>
        </section>
      )}

      <section className={styles.partyAndReferenceGrid}>
        <div className={styles.panel}>
          <p className={styles.eyebrow}>Bill to</p>
          <p className={styles.primaryValue}>{invoice.customerName}</p>
          <p className={styles.addressValue}>
            {invoice.customerAddress || 'Customer address not recorded'}
          </p>
          <dl className={styles.compactList}>
            <div>
              <dt>Canonical customer ID</dt>
              <dd className={styles.auditValue}>{invoice.customerId}</dd>
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
              <dt>Customer / source reference</dt>
              <dd>{invoice.reference || '—'}</dd>
            </div>
          </dl>
        </div>
      </section>

      <section
        className={`${styles.dateGrid} ${invoice.isOpeningBalance ? styles.openingDateGrid : ''}`}
        aria-label="Invoice dates and terms"
      >
        <div>
          <span>Invoice date</span>
          <strong>{formatDate(invoice.invoiceDate)}</strong>
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
        {!invoice.isOpeningBalance && (
          <div>
            <span>Early discount due</span>
            <strong>{formatDate(invoice.earlyPaymentDiscountDueDate)}</strong>
          </div>
        )}
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
                    {lineAccountLabel(line)}
                  </div>
                </td>
                <td className={styles.numeric}>
                  {line.quantity} {line.unit || ''}
                </td>
                <td className={styles.numeric}>{money(line.unitPrice)}</td>
                <td className={styles.numeric}>{money(line.discountAmount)}</td>
                <td className={styles.numeric}>{money(line.taxAmount)}</td>
                <td className={styles.numeric}>
                  {money(lineReceivableAmount(line))}
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
        </div>

        <dl className={styles.totalsPanel}>
          <div>
            <dt>Gross line value</dt>
            <dd>{money(grossAmount)}</dd>
          </div>
          <div>
            <dt>Line discounts</dt>
            <dd>{money(lineDiscounts)}</dd>
          </div>
          <div>
            <dt>Subtotal after line discounts</dt>
            <dd>{money(canonicalSubTotal)}</dd>
          </div>
          <div>
            <dt>Document discount</dt>
            <dd>{money(invoice.discountAmount)}</dd>
          </div>
          <div>
            <dt>Tax</dt>
            <dd>{money(canonicalTaxAmount)}</dd>
          </div>
          <div className={styles.totalRow}>
            <dt>Invoice total</dt>
            <dd>{money(invoice.totalAmount)}</dd>
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
          <span>Posting journal</span>
          <strong>{invoice.journalEntryId || 'Not posted'}</strong>
        </div>
        <p>
          Controlled Finance copy. Ledger effect is governed by the posting
          status and journal reference above.
        </p>
      </footer>
    </section>
  );
}
