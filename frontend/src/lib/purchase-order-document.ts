import { jsPDF } from 'jspdf';
import type { PurchaseOrderDetailDto } from '@/services/purchasingService';
import { getPurchaseOrderStatusPresentation } from '@/lib/purchase-order-status';

export interface PurchaseOrderDocumentCompany {
  name: string;
  address?: string;
  contactEmail?: string;
  contactPhone?: string;
}

const clean = (value?: string | null) => (value || '').replace(/[\u2010-\u2015]/g, '-').replace(/\u00a0/g, ' ').trim();
const dateText = (value?: string) => {
  if (!value) return '-';
  const date = new Date(`${value.slice(0, 10)}T12:00:00Z`);
  return Number.isNaN(date.getTime()) ? '-' : date.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' });
};
const numberText = (amount: number, digits = 2) => {
  if (!Number.isFinite(amount)) throw new Error('The purchase order contains an invalid amount or quantity. Refresh it before exporting.');
  return amount.toLocaleString('en-GB', { minimumFractionDigits: digits, maximumFractionDigits: digits || 4 });
};

/** Supplier-facing document, built from the complete PO DTO, never from a dashboard/tab screenshot. */
export function buildPurchaseOrderPdf(order: PurchaseOrderDetailDto, company: PurchaseOrderDocumentCompany): jsPDF {
  if (!clean(company.name) || !clean(order.orderNumber)) throw new Error('Company and purchase-order details are required before exporting.');
  const currency = clean(order.currency).toUpperCase();
  if (!/^[A-Z]{3}$/.test(currency)) throw new Error('The purchase order needs a valid currency before exporting.');
  [order.subTotal, order.totalAmount, order.taxAmount, order.discountAmount, order.shippingCost, order.miscellaneousCost].forEach(amount => numberText(amount));
  const pdf = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4', compress: false });
  pdf.setProperties({ title: `${order.orderNumber} - Purchase Order`, subject: `Purchase order - ${order.status}`, author: clean(company.name), creator: 'Rhema ERP' });
  const left = 16, right = 194, width = right - left, bottom = 274;
  const ink: [number, number, number] = [24, 39, 58];
  const muted: [number, number, number] = [91, 105, 120];
  let y = 16;
  const font = (size = 10, bold = false, color = ink) => {
    pdf.setFont('helvetica', bold ? 'bold' : 'normal');
    pdf.setFontSize(size);
    pdf.setTextColor(...color);
  };
  const wrap = (value: string, maxWidth: number) => pdf.splitTextToSize(clean(value), maxWidth) as string[];
  const rule = (at: number) => { pdf.setDrawColor(213, 221, 229); pdf.setLineWidth(0.25); pdf.line(left, at, right, at); };
  const newPage = () => {
    pdf.addPage();
    font(10, true);
    pdf.text('PURCHASE ORDER', left, 17);
    font(9, false, muted);
    pdf.text(clean(order.orderNumber), right, 17, { align: 'right' });
    rule(21);
    y = 28;
  };
  const ensure = (height: number) => { if (y + height > bottom) newPage(); };
  const paragraph = (value: string, x = left, maxWidth = width, size = 9.5, bold = false) => {
    font(size, bold);
    for (const line of wrap(value, maxWidth)) {
      ensure(5);
      font(size, bold);
      pdf.text(line, x, y);
      y += 4.8;
    }
  };

  // Fixed A4 letterhead. Keep business identity separate from order metadata.
  font(13, true);
  const companyLines = wrap(company.name, 91);
  pdf.text(companyLines, left, y, { lineHeightFactor: 1.2 });
  font(20, true);
  pdf.text('PURCHASE ORDER', right, 17, { align: 'right' });
  font(11, true);
  pdf.text(clean(order.orderNumber), right, 25, { align: 'right' });
  const status = getPurchaseOrderStatusPresentation(order.status);
  const caution = !['Approved', 'Sent', 'Acknowledged', 'Partially Received', 'Received', 'Closed'].includes(status.key);
  font(9, true, caution ? [158, 57, 35] : muted);
  pdf.text(`${status.label}${caution ? ' - NOT FOR ISSUE' : ''}`, right, 32, { align: 'right' });
  y += companyLines.length * 5.5 + 2;
  for (const detail of [company.address, company.contactPhone, company.contactEmail]) {
    if (clean(detail)) paragraph(clean(detail), left, 91, 8.5);
  }
  y = Math.max(y, 37) + 3;
  rule(y);
  y += 8;
  const metadata = [
    ['ORDER DATE', dateText(order.orderDate)],
    ['REQUIRED DATE', dateText(order.requiredDate)],
    ['CURRENCY', currency],
  ];
  metadata.forEach(([label, value], index) => {
    const x = left + index * 62;
    font(7.5, true, muted); pdf.text(label, x, y);
    font(10); pdf.text(value, x, y + 5.5);
  });
  y += 17;

  const supplierLines = [order.supplierName, order.supplierAddress, order.supplierPhone, order.supplierEmail].filter(Boolean) as string[];
  const deliveryLines = [order.deliveryAddress || 'Delivery address not recorded', order.promisedDate ? `Promised: ${dateText(order.promisedDate)}` : '', order.referenceNumber ? `Reference: ${order.referenceNumber}` : '', order.procurementSourceReference ? `Source: ${order.procurementSourceReference}` : ''].filter(Boolean);
  ensure(18);
  font(8, true, muted); pdf.text('SUPPLIER', left, y); pdf.text('DELIVER TO', left + 94, y); y += 6;
  font(9.5);
  const partyColumns = [supplierLines, deliveryLines].map(lines => lines.flatMap(line => wrap(line, 84)));
  for (let line = 0; line < Math.max(...partyColumns.map(lines => lines.length)); line++) {
    ensure(5);
    font(9.5);
    partyColumns.forEach((lines, column) => { if (lines[line]) pdf.text(lines[line], left + column * 94, y); });
    y += 4.8;
  }
  y += 8;

  const columnWidths = [8, 84, 15, 15, 28, 28];
  const tableHeader = () => {
    ensure(21);
    pdf.setFillColor(...ink); pdf.rect(left, y, width, 10, 'F');
    let x = left;
    ['#', 'ITEM / DESCRIPTION', 'QTY', 'UOM', 'UNIT PRICE', 'AMOUNT'].forEach((label, index) => {
      font(7.5, true, [255, 255, 255]);
      const numeric = [2, 4, 5].includes(index);
      pdf.text(label, numeric ? x + columnWidths[index] - 2 : x + 2, y + 6.2, { align: numeric ? 'right' : 'left' });
      x += columnWidths[index];
    });
    y += 10;
  };
  font(8, false, muted); ensure(25); pdf.text(`Order items - all amounts in ${currency}`, left, y); y += 4;
  tableHeader();
  order.items.forEach((item, index) => {
    const description = [item.itemCode, item.itemName, clean(item.itemDescription) !== clean(item.itemName) ? item.itemDescription : '', item.expectedDeliveryDate ? `Delivery: ${dateText(item.expectedDeliveryDate)}` : ''].filter(Boolean).join('\n');
    font(9);
    const cells = [String(index + 1), description, numberText(item.orderedQuantity, 0), item.unitOfMeasure || '-', numberText(item.unitPrice), numberText(item.lineTotal)]
      .map((value, column) => wrap(value, columnWidths[column] - 4));
    const lineCount = Math.max(...cells.map(lines => lines.length));
    let offset = 0;
    while (offset < lineCount) {
      const fullHeight = Math.max(13, (lineCount - offset) * 4.5 + 6);
      if (y + Math.min(fullHeight, 25) > bottom || (fullHeight <= 220 && y + fullHeight > bottom)) { newPage(); tableHeader(); }
      const take = Math.min(lineCount - offset, Math.max(1, Math.floor((bottom - y - 6) / 4.5)));
      const height = Math.max(13, take * 4.5 + 6);
      if (index % 2 === 0) { pdf.setFillColor(246, 248, 251); pdf.rect(left, y, width, height, 'F'); }
      let x = left;
      cells.forEach((lines, column) => {
        font(9, column === 5);
        const numeric = [2, 4, 5].includes(column);
        lines.slice(offset, offset + take).forEach((line, lineIndex) => pdf.text(line, numeric ? x + columnWidths[column] - 2 : x + 2, y + 5 + lineIndex * 4.5, { align: numeric ? 'right' : 'left' }));
        x += columnWidths[column];
      });
      rule(y + height);
      y += height;
      offset += take;
      if (offset < lineCount) { newPage(); tableHeader(); }
    }
  });
  if (!order.items.length) { y += 7; paragraph('No order items recorded.'); }
  y += 8;
  const totals: Array<[string, number]> = [['Subtotal', order.subTotal]];
  if (order.discountAmount) totals.push(['Discount', -order.discountAmount]);
  if (order.taxAmount) totals.push(['Tax', order.taxAmount]);
  if (order.shippingCost) totals.push(['Shipping', order.shippingCost]);
  if (order.miscellaneousCost) totals.push(['Other order charges', order.miscellaneousCost]);
  ensure(totals.length * 6 + 17);
  totals.forEach(([label, amount]) => {
    font(9, false, muted); pdf.text(label, 118, y);
    font(9); pdf.text(numberText(amount), right, y, { align: 'right' }); y += 6;
  });
  pdf.setFillColor(233, 239, 246); pdf.rect(114, y - 2, 80, 12, 'F');
  font(10, true); pdf.text(`PO TOTAL (${currency})`, 118, y + 5);
  pdf.text(numberText(order.totalAmount), right - 3, y + 5, { align: 'right' });
  y += 20;

  const terms: Array<[string, string | undefined]> = [
    ['Payment terms', order.paymentTerms], ['Shipping terms', order.shippingTerms],
    ['Delivery instructions', order.deliveryInstructions], ['Terms and conditions', order.terms],
  ];
  for (const [label, value] of terms) {
    if (!clean(value)) continue;
    ensure(17); font(8, true, muted); pdf.text(label.toUpperCase(), left, y); y += 5;
    paragraph(clean(value)); y += 5;
  }
  if (order.requestedByName || order.approvedByName) {
    ensure(18); rule(y - 2); y += 5;
    if (order.requestedByName) paragraph(`Prepared by: ${order.requestedByName}`, left, width, 8.5);
    if (order.approvedByName && order.approvedAt && !caution) paragraph(`Approved by: ${order.approvedByName} | ${dateText(order.approvedAt)}`, left, width, 8.5);
  }
  const pageCount = pdf.getNumberOfPages();
  for (let page = 1; page <= pageCount; page++) {
    pdf.setPage(page); rule(281); font(8, false, muted);
    pdf.text(clean(order.orderNumber), left, 287);
    pdf.text(`Page ${page} of ${pageCount}`, right, 287, { align: 'right' });
  }
  return pdf;
}

export function downloadPurchaseOrderPdf(order: PurchaseOrderDetailDto, company: PurchaseOrderDocumentCompany) {
  buildPurchaseOrderPdf(order, company).save(`${order.orderNumber.replace(/[^a-z0-9._-]+/gi, '-')}.pdf`);
}

export function openPurchaseOrderPrintPdf(order: PurchaseOrderDetailDto, company: PurchaseOrderDocumentCompany, printWindow: Window) {
  const pdf = buildPurchaseOrderPdf(order, company);
  const url = URL.createObjectURL(pdf.output('blob'));
  printWindow.opener = null;
  printWindow.location.href = url;
  window.setTimeout(() => URL.revokeObjectURL(url), 120_000);
}
