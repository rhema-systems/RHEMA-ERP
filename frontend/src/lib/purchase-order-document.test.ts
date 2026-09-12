import { describe, expect, it } from 'vitest';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { buildPurchaseOrderPdf } from './purchase-order-document';
import type { PurchaseOrderDetailDto, PurchaseOrderItemDto } from '@/services/purchasingService';

const company = { name: 'REHEARSAL - Rhema Systems & Associates Ltd', address: 'Tema, Ghana', contactEmail: 'purchasing@example.test' };
const order: PurchaseOrderDetailDto = {
  id: 'test-po', orderNumber: 'PO-2026-0003', orderType: 'Standard', supplierId: 'test-supplier',
  supplierName: 'Harbourline Goods Supply Ltd', supplierAddress: 'LOCAL UAT ONLY - 1 Test Warehouse Road, Tema, Ghana',
  supplierEmail: 'supplier@example.test', supplierPhone: '+233000000001',
  orderDate: '2026-09-08T00:00:00', requiredDate: '2026-09-30', status: 'Draft', currency: 'GHS',
  subTotal: 52000, totalAmount: 52000, taxAmount: 0, shippingCost: 0, miscellaneousCost: 0,
  totalAdditionalCost: 0, discountAmount: 0, costsAllocated: false, costAllocationMethod: 'SpreadToItemCost', costApportionmentBasis: 'Value',
  requestedByName: 'Procurement Officer', itemCount: 2, receipts: [],
  deliveryAddress: 'LOCAL UAT ONLY - 1 Test Warehouse Road, Tema, Ghana',
  paymentTerms: 'Payment following acceptance and invoice matching.',
  procurementSourceReference: 'CTR-2026-00002',
  sourceIntegrityHash: 'MUST-NOT-PRINT-INTERNAL-HASH',
  notes: 'MUST-NOT-PRINT-INTERNAL-NOTE',
  items: [
    { id: 'line-1', purchaseOrderId: 'test-po', inventoryItemId: 'device', itemCode: 'PM-BARCODE-DEVICE', itemName: 'Barcode Device Kit', itemDescription: 'Barcode Device Kit', orderedQuantity: 20, unitOfMeasure: 'EA', receivedQuantity: 0, remainingQuantity: 20, unitPrice: 700, lineTotal: 14000, allocatedAdditionalCost: 0, allocatedCostPerUnit: 0, landedUnitCost: 700 },
    { id: 'line-2', purchaseOrderId: 'test-po', inventoryItemId: 'pipe', itemCode: 'SKU-001', itemName: 'PVC Pipe 50mm', itemDescription: 'PVC Pipe 50mm', orderedQuantity: 20, unitOfMeasure: 'EACH', receivedQuantity: 0, remainingQuantity: 20, unitPrice: 1900, lineTotal: 38000, allocatedAdditionalCost: 0, allocatedCostPerUnit: 0, landedUnitCost: 1900 },
  ],
};

describe('purchase order business PDF', () => {
  it('prints every ordered line on one readable A4 page, independent of the selected screen tab', () => {
    const pdf = buildPurchaseOrderPdf(order, company);
    const content = pdf.output();
    expect(pdf.getNumberOfPages()).toBe(1);
    expect(pdf.internal.pageSize.getWidth()).toBeCloseTo(210, 1);
    for (const value of ['PURCHASE ORDER', 'PO-2026-0003', 'Harbourline', 'Barcode Device Kit', 'PVC Pipe 50mm', 'EACH', '52,000.00', 'GHS', 'Page 1 of 1']) expect(content).toContain(value);
    expect(content).not.toContain('/Subtype /Image');
    expect(new TextEncoder().encode(content).length).toBeLessThan(60_000);
    if (process.env.PO_PDF_QA_DIR) {
      mkdirSync(process.env.PO_PDF_QA_DIR, { recursive: true });
      writeFileSync(join(process.env.PO_PDF_QA_DIR, 'purchase-order-layout-fixture.pdf'), Buffer.from(pdf.output('arraybuffer')));
    }
  });

  it('excludes internal readiness, accounting allocation metadata and private notes', () => {
    const content = buildPurchaseOrderPdf(order, company).output();
    for (const value of ['MUST-NOT-PRINT', 'Budget commitment', 'Award SOD', 'Contract signatures', 'Ready to progress', 'costsAllocated']) expect(content).not.toContain(value);
    expect(content).toContain('Draft - NOT FOR ISSUE');
  });

  it('retains currency and authoritative commercial totals without adding inventory/planned landed costs', () => {
    const pdf = buildPurchaseOrderPdf({ ...order, currency: 'EUR', taxAmount: 1000, shippingCost: 50, miscellaneousCost: 25, discountAmount: 75, totalAmount: 53000 }, company);
    const content = pdf.output();
    for (const value of ['EUR', '53,000.00', 'Shipping', 'Other order charges', 'Discount', '-75.00']) expect(content).toContain(value);
    expect(content).not.toContain('USD');
  });

  it('shows actual approval data only on issued/approved states', () => {
    const content = buildPurchaseOrderPdf({ ...order, status: 'Approved', approvedByName: 'Independent Approver', approvedAt: '2026-09-09' }, company).output();
    expect(content).toContain('Independent Approver');
    expect(content).not.toContain('NOT FOR ISSUE');
  });

  it('handles ad-hoc service lines without a catalogue code', () => {
    const content = buildPurchaseOrderPdf({ ...order, items: [{ ...order.items[0], inventoryItemId: '', itemCode: '', itemName: 'Installation service', unitOfMeasure: 'JOB' }] }, company).output();
    expect(content).toContain('Installation service');
    expect(content).toContain('JOB');
  });

  it('paginates long descriptions and terms, repeats table headings and preserves the final line', () => {
    const items: PurchaseOrderItemDto[] = Array.from({ length: 28 }, (_, index) => ({
      ...order.items[0], id: `line-${index}`, itemCode: `SERVICE-${index + 1}`, itemName: `Service package ${index + 1}`,
      itemDescription: index === 5 ? 'A very long approved specification that must wrap safely across printed pages. '.repeat(100) : 'Supply, delivery and commissioning according to the approved specification.',
    }));
    const pdf = buildPurchaseOrderPdf({ ...order, items, terms: 'Approved contractual conditions remain applicable. '.repeat(90) }, company);
    expect(pdf.getNumberOfPages()).toBeGreaterThan(2);
    const content = pdf.output();
    expect(content).toContain('SERVICE-28');
    expect(content.match(/ITEM \/ DESCRIPTION/g)!.length).toBeGreaterThan(1);
    expect(content).toContain(`Page ${pdf.getNumberOfPages()} of ${pdf.getNumberOfPages()}`);
    if (process.env.PO_PDF_QA_DIR) writeFileSync(join(process.env.PO_PDF_QA_DIR, 'purchase-order-pagination-stress.pdf'), Buffer.from(pdf.output('arraybuffer')));
  });

  it('rejects missing currency, company and invalid numbers rather than printing misleading values', () => {
    expect(() => buildPurchaseOrderPdf({ ...order, currency: '' }, company)).toThrow('currency');
    expect(() => buildPurchaseOrderPdf(order, { name: '' })).toThrow('Company');
    expect(() => buildPurchaseOrderPdf({ ...order, totalAmount: Number.NaN }, company)).toThrow('invalid');
  });
});
