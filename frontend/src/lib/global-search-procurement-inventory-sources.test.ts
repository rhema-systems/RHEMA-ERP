import { existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it, vi } from 'vitest';
import { GLOBAL_SEARCH_PROCUREMENT_SOURCES } from './global-search-procurement-sources';
import { GLOBAL_SEARCH_INVENTORY_SOURCES } from './global-search-inventory-sources';
import { GLOBAL_SEARCH_RECORD_SOURCES } from './global-search-record-sources';
import { extractSearchRecords, searchRecordRequest } from './global-search';

vi.mock('@/services/api.service', () => ({ apiService: { silentRequest: vi.fn() } }));

const sources = [...GLOBAL_SEARCH_PROCUREMENT_SOURCES, ...GLOBAL_SEARCH_INVENTORY_SOURCES];
const source = (id: string) => {
  const match = sources.find(value => value.id === id);
  if (!match) throw new Error(`Missing source ${id}`);
  return match;
};

describe('Procurement and Inventory search API contracts', () => {
  it('uses the AP ProcurementOnly filter instead of the unsupported source parameter', () => {
    const request = new URL(searchRecordRequest(source('procurement-supplier-invoices'), 'VI-001').endpoint, 'http://test');
    expect(request.pathname).toBe('/ap/invoices');
    expect(request.searchParams.get('procurementOnly')).toBe('true');
    expect(request.searchParams.get('searchTerm')).toBe('VI-001');
    expect(request.searchParams.has('source')).toBe(false);
    // Finance's existing register includes all AP documents; the server exposes no negative source filter.
    const finance = GLOBAL_SEARCH_RECORD_SOURCES.find(value => value.id === 'finance-ap-invoices')!;
    const financeRequest = new URL(searchRecordRequest(finance, 'VI-001').endpoint, 'http://test');
    expect(financeRequest.searchParams.has('procurementOnly')).toBe(false);
    expect(financeRequest.searchParams.has('source')).toBe(false);
  });

  // Raw API DTO fields: ProcurementPlanningDtos, ContractDTOs, BusinessPartnerDtos,
  // PurchaseRequisition/PurchaseOrder controller summary projections, TenderDTOs and RfqDtos.
  it.each([
    ['procurement-plans', { id: 'record', planNumber: 'PP-001', title: 'Annual plan', organizationUnitName: 'Works' }, 'PP-001 · Annual plan', 'Works'],
    ['procurement-budgets', { id: 'record', budgetCode: 'PB-001', title: 'Materials', organizationUnitName: 'Works' }, 'PB-001 · Materials', 'Works'],
    ['procurement-contracts', { id: 'record', contractNumber: 'CT-001', contractTitle: 'Supply contract', businessPartnerName: 'Vendor' }, 'CT-001 · Supply contract', 'Vendor'],
    ['procurement-supplier-invoices', { id: 'record', invoiceNumber: 'VI-001', supplierInvoiceNumber: 'SUP-101' }, 'VI-001', 'SUP-101'],
    ['business-partners', { id: 'record', partnerCode: 'BP-001', partnerName: 'Vendor', partnerType: 'Supplier' }, 'BP-001 · Vendor', 'Supplier'],
    ['purchase-requisitions', { id: 'record', requisitionNumber: 'PR-001', organizationUnitName: 'Works', sourcePlanNumber: 'PP-001' }, 'PR-001', 'Works · PP-001'],
    ['purchase-orders', { id: 'record', orderNumber: 'PO-001', supplierName: 'Vendor', procurementSourceReference: 'RFQ-001' }, 'PO-001', 'Vendor · RFQ-001'],
    ['purchase-receipts', { id: 'record', receiptNumber: 'GR-001', purchaseOrderNumber: 'PO-001', supplierName: 'Vendor' }, 'GR-001', 'PO-001 · Vendor'],
    ['tenders', { id: 'record', tenderNumber: 'TN-001', title: 'Paving' }, 'TN-001 · Paving', ''],
    ['rfqs', { id: 'record', rfqNumber: 'RFQ-001', title: 'Materials' }, 'RFQ-001 · Materials', ''],
  ] as const)('extracts paged raw %s records', (id, record, title, subtitle) => {
    const descriptor = source(id);
    const results = extractSearchRecords(descriptor, { items: [{ ...record, status: 'Draft' }], totalCount: 100, page: 1, pageSize: 5 });
    expect(results).toHaveLength(1);
    expect(results[0]).toMatchObject({ title, subtitle, status: 'Draft' });
    const request = new URL(searchRecordRequest(descriptor, 'materials').endpoint, 'http://test');
    expect(request.searchParams.get('page')).toBe('1');
    expect(request.searchParams.get('pageSize')).toBe('5');
  });

  // Raw InventoryEnhancedDTOs, InventoryDisposalDtos and InventoryIssueSearchDto.
  it.each([
    ['inventory-issue-vouchers', { id: 'record', voucherNumber: 'SIV-001', requisitionNumber: 'IR-001' }, 'SIV-001', 'IR-001'],
    ['inventory-items', { id: 'record', itemCode: 'IT-001', name: 'Cement', description: 'Bag' }, 'IT-001 · Cement', 'Bag'],
    ['inventory-warehouses', { id: 'record', code: 'WH-001', name: 'Main' }, 'WH-001 · Main', ''],
    ['inventory-transfers', { id: 'record', transferNumber: 'TR-001', sourceWarehouseName: 'Main', destinationWarehouseName: 'Site' }, 'TR-001', 'Main · Site'],
    ['inventory-physical-counts', { id: 'record', countNumber: 'PC-001', warehouseName: 'Main' }, 'PC-001', 'Main'],
    ['inventory-requisitions', { id: 'record', requisitionNumber: 'IR-001', description: 'Materials', warehouseName: 'Main', projectCode: 'PJ-001' }, 'IR-001', 'Materials · Main · PJ-001'],
    ['inventory-disposals', { id: 'record', disposalNumber: 'DS-001', reason: 'Damaged', warehouseName: 'Main' }, 'DS-001', 'Damaged · Main'],
  ] as const)('extracts raw array %s records and uses its bounded owner search', (id, record, title, subtitle) => {
    const descriptor = source(id);
    const results = extractSearchRecords(descriptor, [record]);
    expect(results).toHaveLength(1);
    expect(results[0]).toMatchObject({ title, subtitle });
    const request = new URL(searchRecordRequest(descriptor, 'materials').endpoint, 'http://test');
    expect(request.pathname.endsWith('/search')).toBe(true);
    expect(request.searchParams.get(id === 'inventory-items' ? 'searchTerm' : 'search')).toBe('materials');
    expect(Number(request.searchParams.get('take'))).toBeGreaterThan(0);
    expect(Number(request.searchParams.get('take'))).toBeLessThanOrEqual(8);
  });

  it('opens existing read surfaces, including RFQ controls and issue voucher details', () => {
    for (const descriptor of sources) {
      const request = searchRecordRequest(descriptor, 'search term');
      expect(request.options.method ?? 'GET').toBe('GET');
      expect(descriptor.detailPath).not.toMatch(/\/(edit|create|approve|post)(?:\/|\?|$)/);
      const route = descriptor.detailPath!.split('?')[0].slice(1).replace(':id', '[id]');
      expect(existsSync(resolve(process.cwd(), 'src/app', route, 'page.tsx')), descriptor.id).toBe(true);
    }
    expect(extractSearchRecords(source('rfqs'), { items: [{ id: 'rfq-1', rfqNumber: 'RFQ-001' }] })[0].href)
      .toBe('/procurement/rfqs/rfq-1/controls');
  });
});
