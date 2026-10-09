import type { MobilePosReceipt } from "@/src/types/api";

export function buildReceiptHtml(receipt: MobilePosReceipt): string {
  const copyMark = receipt.copyType === "REPRINT"
    ? `<div class="copy-mark">REPRINT &middot; COPY ${receipt.copyNumber}</div>`
    : "";
  const lineRows = receipt.lines.map(line => `
    <tr>
      <td>
        <strong>${escapeHtml(line.description)}</strong>
        <span>${formatQuantity(line.quantity)} ${escapeHtml(line.unitOfMeasureCode)} &times; ${receiptMoney(line.unitPrice, receipt.currencyCode)}</span>
      </td>
      <td class="amount">${receiptMoney(line.lineTotal, receipt.currencyCode)}</td>
    </tr>`).join("");
  const tenderRows = receipt.tenders.map(tender => `
    <tr>
      <td>
        <strong>${escapeHtml(tender.paymentMethodName)}</strong>
        <span>${escapeHtml(tender.paymentNumber)}${tender.externalReference ? ` &middot; ${escapeHtml(tender.externalReference)}` : ""}</span>
      </td>
      <td class="amount">${receiptMoney(tender.amount, receipt.currencyCode)}</td>
    </tr>`).join("");

  return `<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <style>
    @page { margin: 12mm; }
    * { box-sizing: border-box; }
    body { margin: 0; color: #101828; font-family: Arial, Helvetica, sans-serif; font-size: 12px; }
    .receipt { width: 100%; max-width: 520px; margin: 0 auto; }
    .copy-mark { margin-bottom: 12px; color: #b42318; font-size: 13px; font-weight: 700; letter-spacing: 1.5px; text-align: center; }
    h1 { margin: 0; color: #0b2a5b; font-size: 21px; text-align: center; }
    .center { margin-top: 4px; color: #475467; text-align: center; }
    .rule { margin: 14px 0; border-top: 1px dashed #98a2b3; }
    .facts { width: 100%; border-collapse: collapse; }
    .facts td { padding: 3px 0; vertical-align: top; }
    .facts td:first-child { width: 34%; color: #667085; }
    .facts td:last-child { font-weight: 600; text-align: right; }
    .items { width: 100%; border-collapse: collapse; }
    .items td { padding: 6px 0; border-bottom: 1px solid #eaecf0; vertical-align: top; }
    .items strong, .items span { display: block; }
    .items span { margin-top: 2px; color: #667085; font-size: 10px; }
    .items .amount { white-space: nowrap; font-weight: 600; text-align: right; }
    .total td { padding-top: 8px; font-size: 15px; font-weight: 700; }
    .reference { margin-top: 16px; color: #667085; font-family: monospace; font-size: 9px; overflow-wrap: anywhere; text-align: center; }
    .footer { margin-top: 14px; color: #475467; font-size: 10px; text-align: center; }
  </style>
</head>
<body>
  <main class="receipt">
    ${copyMark}
    <h1>${escapeHtml(receipt.tenantName || "RHEMA ERP")}</h1>
    <div class="center">${escapeHtml(receipt.storeName)} &middot; ${escapeHtml(receipt.locationName || receipt.storeCode)}</div>
    <div class="center">${escapeHtml(receipt.tillNumber)} &middot; ${escapeHtml(receipt.tillSessionNumber)}</div>
    <div class="rule"></div>
    <table class="facts">
      <tr><td>Customer</td><td>${escapeHtml(receipt.customerName)} (${escapeHtml(receipt.customerCode)})</td></tr>
      <tr><td>Cashier</td><td>${escapeHtml(receipt.cashierName)}</td></tr>
      <tr><td>Invoice</td><td>${escapeHtml(receipt.invoiceNumber)}</td></tr>
      <tr><td>Status</td><td>${escapeHtml(receipt.invoiceStatus)}</td></tr>
      <tr><td>Reference</td><td>${escapeHtml(receipt.localReference)}</td></tr>
      <tr><td>Date</td><td>${escapeHtml(formatReceiptDate(receipt.occurredAtUtc))}</td></tr>
    </table>
    <div class="rule"></div>
    <table class="items">${lineRows}</table>
    <table class="facts">
      <tr><td>Subtotal</td><td>${receiptMoney(receipt.subTotal, receipt.currencyCode)}</td></tr>
      <tr><td>Discount</td><td>${receiptMoney(receipt.discountAmount, receipt.currencyCode)}</td></tr>
      <tr><td>Tax</td><td>${receiptMoney(receipt.taxAmount, receipt.currencyCode)}</td></tr>
      <tr class="total"><td>Total</td><td>${receiptMoney(receipt.totalAmount, receipt.currencyCode)}</td></tr>
    </table>
    <div class="rule"></div>
    <table class="items">${tenderRows}</table>
    <div class="reference">${escapeHtml(receipt.qrReference)}</div>
    <div class="footer">Generated from the canonical RHEMA invoice and allocated payments.</div>
  </main>
</body>
</html>`;
}

export function receiptPdfFileName(receipt: MobilePosReceipt): string {
  const invoice = receipt.invoiceNumber.replace(/[^A-Za-z0-9._-]+/g, "-").replace(/^-+|-+$/g, "") || "receipt";
  const copy = receipt.copyType === "REPRINT" ? `reprint-${receipt.copyNumber}` : "original";
  return `${invoice}-${copy}.pdf`;
}

function receiptMoney(value: number, currency: string): string {
  const normalized = Number.isFinite(value) ? value : 0;
  return `${escapeHtml(currency)} ${normalized.toLocaleString("en-GH", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function formatQuantity(value: number): string {
  return Number.isInteger(value) ? value.toString() : value.toLocaleString("en-GH", { maximumFractionDigits: 4 });
}

function formatReceiptDate(value: string): string {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString("en-GH");
}

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
