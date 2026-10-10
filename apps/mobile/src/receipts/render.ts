import type { MobilePosPrintableReceipt } from "@/src/types/api";

export function buildReceiptHtml(receipt: MobilePosPrintableReceipt): string {
  const isSale = receipt.receiptKind === "SALE" || receipt.receiptKind === "SALE_PROVISIONAL";
  const isProvisional = receipt.receiptKind === "SALE_PROVISIONAL" || receipt.receiptKind === "COLLECTION_PROVISIONAL";
  const copyMark = isProvisional
    ? `<div class="provisional-mark">PROVISIONAL &middot; PENDING SYNCHRONIZATION</div>`
    : receipt.copyType === "REPRINT"
      ? `<div class="copy-mark">REPRINT &middot; COPY ${receipt.copyNumber}</div>`
      : "";
  const detailRows = isSale
    ? receipt.lines.map(line => `
      <tr><td><strong>${escapeHtml(line.description)}</strong><span>${formatQuantity(line.quantity)} ${escapeHtml(line.unitOfMeasureCode)} &times; ${receiptMoney(line.unitPrice, receipt.currencyCode)}</span></td><td class="amount">${receiptMoney(line.lineTotal, receipt.currencyCode)}</td></tr>`).join("")
    : receipt.allocations.map(allocation => `
      <tr><td><strong>${escapeHtml(allocation.invoiceNumber)}</strong><span>Invoice allocation</span></td><td class="amount">${receiptMoney(allocation.amount, receipt.currencyCode)}</td></tr>`).join("");
  const tenderRows = receipt.tenders.map(tender => {
    const paymentNumber = "paymentNumber" in tender ? tender.paymentNumber : "Pending server payment number";
    return `<tr><td><strong>${escapeHtml(tender.paymentMethodName)}</strong><span>${escapeHtml(paymentNumber)}${tender.externalReference ? ` &middot; ${escapeHtml(tender.externalReference)}` : ""}</span></td><td class="amount">${receiptMoney(tender.amount, receipt.currencyCode)}</td></tr>`;
  }).join("");
  const sourceFacts = receipt.receiptKind === "SALE"
    ? `<tr><td>Invoice</td><td>${escapeHtml(receipt.invoiceNumber)}</td></tr><tr><td>Status</td><td>${escapeHtml(receipt.invoiceStatus)}</td></tr>`
    : receipt.receiptKind === "SALE_PROVISIONAL"
      ? `<tr><td>Document</td><td>Provisional cash sale</td></tr><tr><td>Status</td><td>Pending synchronization</td></tr>`
    : `<tr><td>Document</td><td>${isProvisional ? "Provisional collection" : "Customer collection"}</td></tr><tr><td>Status</td><td>${isProvisional ? "Pending synchronization" : "Payments posted"}</td></tr>`;
  const totals = isSale
    ? `<tr><td>Subtotal</td><td>${receiptMoney(receipt.subTotal, receipt.currencyCode)}</td></tr><tr><td>Discount</td><td>${receiptMoney(receipt.discountAmount, receipt.currencyCode)}</td></tr><tr><td>Tax</td><td>${receiptMoney(receipt.taxAmount, receipt.currencyCode)}</td></tr>`
    : "";
  const footer = isProvisional
    ? "This is not a final Finance receipt. RHEMA payment numbers will be assigned after successful synchronization."
    : isSale
      ? "Generated from the canonical RHEMA invoice and allocated payments."
      : "Generated from canonical RHEMA customer payments and invoice allocations.";

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
    .copy-mark, .provisional-mark { margin-bottom: 12px; font-size: 13px; font-weight: 700; letter-spacing: 1.5px; text-align: center; }
    .copy-mark { color: #b42318; }
    .provisional-mark { padding: 8px; border: 2px solid #b54708; color: #b54708; }
    h1 { margin: 0; color: #0b2a5b; font-size: 21px; text-align: center; }
    h2 { margin: 6px 0 0; color: #344054; font-size: 13px; letter-spacing: 1px; text-align: center; }
    .center { margin-top: 4px; color: #475467; text-align: center; }
    .rule { margin: 14px 0; border-top: 1px dashed #98a2b3; }
    .facts, .items { width: 100%; border-collapse: collapse; }
    .facts td { padding: 3px 0; vertical-align: top; }
    .facts td:first-child { width: 34%; color: #667085; }
    .facts td:last-child { font-weight: 600; text-align: right; }
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
    <h2>${isSale ? "SALES RECEIPT" : "CUSTOMER COLLECTION RECEIPT"}</h2>
    <div class="center">${escapeHtml(receipt.storeName)} &middot; ${escapeHtml(receipt.locationName || receipt.storeCode)}</div>
    <div class="center">${escapeHtml(receipt.tillNumber)} &middot; ${escapeHtml(receipt.tillSessionNumber)}</div>
    <div class="rule"></div>
    <table class="facts">
      <tr><td>Customer</td><td>${escapeHtml(receipt.customerName)} (${escapeHtml(receipt.customerCode)})</td></tr>
      <tr><td>Cashier</td><td>${escapeHtml(receipt.cashierName)}</td></tr>
      ${sourceFacts}
      <tr><td>Reference</td><td>${escapeHtml(receipt.localReference)}</td></tr>
      <tr><td>Date</td><td>${escapeHtml(formatReceiptDate(receipt.occurredAtUtc))}</td></tr>
    </table>
    <div class="rule"></div>
    <table class="items">${detailRows}</table>
    <table class="facts">${totals}<tr class="total"><td>Total</td><td>${receiptMoney(receipt.totalAmount, receipt.currencyCode)}</td></tr></table>
    <div class="rule"></div>
    <table class="items">${tenderRows}</table>
    <div class="reference">${escapeHtml(receipt.qrReference)}</div>
    <div class="footer">${footer}</div>
  </main>
</body>
</html>`;
}

export function receiptPdfFileName(receipt: MobilePosPrintableReceipt): string {
  const source = (receipt.receiptKind === "SALE" ? receipt.invoiceNumber : receipt.localReference)
    .replace(/[^A-Za-z0-9._-]+/g, "-").replace(/^-+|-+$/g, "") || "receipt";
  const kind = receipt.receiptKind === "SALE" || receipt.receiptKind === "SALE_PROVISIONAL" ? "sale" : "collection";
  const copy = receipt.copyType === "REPRINT" ? `reprint-${receipt.copyNumber}` : receipt.copyType === "PROVISIONAL" ? "provisional" : "original";
  return `${source}-${kind}-${copy}.pdf`;
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
  return value.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#039;");
}
