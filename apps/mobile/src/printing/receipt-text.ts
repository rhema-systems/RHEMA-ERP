import type { MobilePosPrintableReceipt } from "@/src/types/api";

const defaultWidth = 32;

export function buildReceiptText(receipt: MobilePosPrintableReceipt, width = defaultWidth): string {
  if (!Number.isInteger(width) || width < 24 || width > 48) throw new Error("Receipt width must be an integer between 24 and 48 characters.");

  const isSale = receipt.receiptKind === "SALE";
  const isProvisional = receipt.receiptKind === "COLLECTION_PROVISIONAL";
  const lines: string[] = [];
  if (isProvisional) {
    lines.push(center("PROVISIONAL", width), center("PENDING SYNCHRONIZATION", width));
  } else if (receipt.copyType === "REPRINT") {
    lines.push(center(`REPRINT COPY ${receipt.copyNumber}`, width));
  }
  lines.push(...wrap(receipt.tenantName || "RHEMA ERP", width).map(line => center(line, width)));
  lines.push(center(isSale ? "SALES RECEIPT" : "COLLECTION RECEIPT", width));
  lines.push(...wrap(receipt.storeName, width).map(line => center(line, width)));
  lines.push(center(`${receipt.tillNumber} / ${receipt.tillSessionNumber}`, width));
  lines.push(separator(width));
  if (isSale) lines.push(...labelValue("Invoice", receipt.invoiceNumber, width));
  lines.push(...labelValue("Customer", `${receipt.customerName} (${receipt.customerCode})`, width));
  lines.push(...labelValue("Cashier", receipt.cashierName, width));
  lines.push(...labelValue("Date", formatReceiptDate(receipt.occurredAtUtc), width));
  lines.push(...labelValue("Reference", receipt.localReference, width));
  lines.push(separator(width));

  if (isSale) {
    for (const item of receipt.lines) {
      lines.push(...wrap(item.description, width));
      const quantity = `${number(item.quantity)} ${item.unitOfMeasureCode} x ${money(item.unitPrice, receipt.currencyCode)}`;
      lines.push(leftRight(quantity, money(item.lineTotal, receipt.currencyCode), width));
      if (item.discountAmount !== 0) lines.push(leftRight("  Discount", money(-item.discountAmount, receipt.currencyCode), width));
    }
    lines.push(separator(width));
    lines.push(leftRight("Subtotal", money(receipt.subTotal, receipt.currencyCode), width));
    lines.push(leftRight("Discount", money(-receipt.discountAmount, receipt.currencyCode), width));
    lines.push(leftRight("Tax", money(receipt.taxAmount, receipt.currencyCode), width));
  } else {
    for (const allocation of receipt.allocations) {
      lines.push(...wrap(allocation.invoiceNumber, width));
      lines.push(leftRight("  Allocation", money(allocation.amount, receipt.currencyCode), width));
    }
    lines.push(separator(width));
  }
  lines.push(leftRight("TOTAL", money(receipt.totalAmount, receipt.currencyCode), width));
  lines.push(separator(width));

  for (const tender of receipt.tenders) {
    lines.push(leftRight(tender.paymentMethodName, money(tender.amount, receipt.currencyCode), width));
    const paymentNumber = "paymentNumber" in tender ? tender.paymentNumber : "Pending server number";
    lines.push(...wrap([paymentNumber, tender.externalReference].filter(Boolean).join(" / "), width).map(line => `  ${line}`.slice(0, width)));
  }

  lines.push(separator(width));
  lines.push(...wrap(receipt.qrReference, width).map(line => center(line, width)));
  lines.push(center(isProvisional ? "NOT A FINAL FINANCE RECEIPT" : "Thank you", width));
  return lines.join("\n");
}

function labelValue(label: string, value: string, width: number): string[] {
  const prefix = `${label}: `;
  const parts = wrap(value, Math.max(8, width - prefix.length));
  return parts.map((part, index) => `${index === 0 ? prefix : " ".repeat(prefix.length)}${part}`.slice(0, width));
}

function leftRight(left: string, right: string, width: number): string {
  const gap = width - left.length - right.length;
  if (gap >= 1) return `${left}${" ".repeat(gap)}${right}`;
  const rightWidth = Math.min(right.length, Math.floor(width / 2));
  const leftWidth = width - rightWidth - 1;
  return `${left.slice(0, leftWidth)} ${right.slice(-rightWidth)}`;
}

function wrap(value: string, width: number): string[] {
  const clean = value.replace(/[\u0000-\u001f\u007f]/g, " ").replace(/\s+/g, " ").trim();
  if (!clean) return [""];
  const output: string[] = [];
  let remaining = clean;
  while (remaining.length > width) {
    const candidate = remaining.slice(0, width + 1);
    const breakAt = candidate.lastIndexOf(" ");
    const take = breakAt > 0 ? breakAt : width;
    output.push(remaining.slice(0, take).trimEnd());
    remaining = remaining.slice(take).trimStart();
  }
  if (remaining) output.push(remaining);
  return output;
}

function center(value: string, width: number): string {
  const clean = value.slice(0, width);
  return `${" ".repeat(Math.max(0, Math.floor((width - clean.length) / 2)))}${clean}`;
}

function separator(width: number): string { return "-".repeat(width); }
function money(value: number, currency: string): string { return `${currency} ${value.toFixed(2)}`; }
function number(value: number): string { return Number.isInteger(value) ? String(value) : value.toFixed(2).replace(/0+$/, "").replace(/\.$/, ""); }
function formatReceiptDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toISOString().replace("T", " ").slice(0, 19) + "Z";
}
