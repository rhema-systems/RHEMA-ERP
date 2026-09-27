import { existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it, vi } from 'vitest';
import { GLOBAL_SEARCH_RECORD_SOURCES } from './global-search-record-sources';
import { extractSearchRecords, searchRecordRequest } from './global-search';

vi.mock('@/services/api.service', () => ({ apiService: { silentRequest: vi.fn() } }));

const source = (id: string) => {
  const result = GLOBAL_SEARCH_RECORD_SOURCES.find(item => item.id === id);
  if (!result) throw new Error(`Missing reviewed source: ${id}`);
  return result;
};

describe('reviewed global record sources', () => {
  it('uses bounded searches and links only to existing pages', () => {
    expect(new Set(GLOBAL_SEARCH_RECORD_SOURCES.map(item => item.id)).size).toBe(GLOBAL_SEARCH_RECORD_SOURCES.length);
    for (const item of GLOBAL_SEARCH_RECORD_SOURCES) {
      const bound = item.params?.pageSize ?? item.params?.take ?? item.params?.limit;
      expect(bound, item.id).toBe(5);
      expect(item.endpoint.startsWith('/api/'), item.id).toBe(false);
      for (const route of [item.route, item.detailPath].filter(Boolean) as string[]) {
        const routePath = route.split('?')[0].slice(1);
        const dynamicSegment = item.id === 'crm-accounts' ? '[businessPartnerId]' : '[id]';
        const page = resolve(process.cwd(), 'src/app', routePath.replace(':id', dynamicSegment), 'page.tsx');
        expect(existsSync(page), `${item.id}: ${route}`).toBe(true);
      }
    }
  });

  it('sends HR search in the POST body and shows directory fields only', () => {
    const staff = source('hr-employees');
    const request = searchRecordRequest(staff, 'EMP-042');
    expect(request.endpoint).toBe('/hr/Employees/paged?page=1&pageSize=5');
    expect(request.options.method).toBe('POST');
    expect(JSON.parse(request.options.body!)).toEqual({ searchTerm: 'EMP-042' });
    const results = extractSearchRecords(staff, { items: [{ id: 'staff-42', employeeNumber: 'EMP-042',
      fullName: 'Alex Example', email: 'private@example.test', salary: 15000, bankAccountNumber: 'secret' }] });
    expect(results[0]).toMatchObject({ title: 'EMP-042 · Alex Example', href: '/hr/employees/staff-42', subtitle: '' });
    expect(JSON.stringify(results)).not.toMatch(/private@|15000|secret/);
  });

  it('searches the authorized leave register without displaying leave reasons or medical details', () => {
    const leave = source('hr-leave-requests');
    const request = new URL(searchRecordRequest(leave, 'LR-001').endpoint, 'http://test');
    expect(request.pathname).toBe('/Leaves/register');
    expect(request.searchParams.get('pageNumber')).toBe('1');
    expect(request.searchParams.get('pageSize')).toBe('5');
    expect(request.searchParams.get('search')).toBe('LR-001');
    expect(leave.permissions).toEqual(['HR.Leave.Read', 'HR.Leave.Write', 'HR.Leave.Admin']);
    const results = extractSearchRecords(leave, { items: [{ id: 'leave-1', requestNumber: 'LR-001', status: 'Pending',
      employeeName: 'Private Name', reason: 'Private medical reason', leaveTypeName: 'Medical', totalDays: 12 }] });
    expect(results[0]).toMatchObject({ title: 'LR-001', subtitle: '', status: 'Pending', href: '/hr/leave/requests/leave-1' });
    expect(JSON.stringify(results)).not.toMatch(/Private|Medical|totalDays/);
  });

  it.each(['finance-ar-invoices', 'finance-ar-payments'])('uses AR PageNumber rather than AP Page for %s', id => {
    const request = new URL(searchRecordRequest(source(id), 'REF-1').endpoint, 'http://test');
    expect(request.searchParams.get('pageNumber')).toBe('1');
    expect(request.searchParams.has('page')).toBe(false);
    expect(request.searchParams.get('searchTerm')).toBe('REF-1');
  });

  it('unwraps existing lookup envelopes and array responses', () => {
    expect(extractSearchRecords(source('helpdesk-tickets'), { success: true, data: [
      { id: 'ticket-1', ticketNumber: 'EHC-26-0001', subject: 'Printer', status: 'Open' },
    ] })[0]).toMatchObject({ title: 'EHC-26-0001 · Printer', href: '/helpdesk/tickets/ticket-1' });
    expect(extractSearchRecords(source('projects'), [
      { id: 'project-1', projectCode: 'PRJ-001', title: 'Housing', status: 'Active' },
    ])[0]).toMatchObject({ title: 'PRJ-001 · Housing', href: '/development/projects/project-1' });
    expect(extractSearchRecords(source('estate-land'), { success: true, data: [
      { id: 'land-1', assetCode: 'LAND-001', name: 'East plot', ownershipHistory: ['private'] },
    ] })[0]).toMatchObject({ title: 'LAND-001 · East plot', href: '/estate/land-management?recordId=land-1' });
  });

  it('reads raw Sales DTO names before frontend service alias normalization', () => {
    expect(extractSearchRecords(source('sales-orders'), { items: [
      { id: 'order-1', documentNumber: 'SO-001', orderStatus: 'Confirmed' },
    ] })[0]).toMatchObject({ title: 'SO-001', status: 'Confirmed', href: '/sales/orders/order-1' });
    expect(extractSearchRecords(source('sales-deliveries'), { items: [
      { id: 'delivery-1', documentNumber: 'DN-001', deliveryStatus: 'Shipped' },
    ] })[0]).toMatchObject({ title: 'DN-001', status: 'Shipped', href: '/sales/deliveries/delivery-1' });
  });

  // Payloads use backend DTO names, independently of the frontend service aliases.
  // AccountsPayableDtos, InvoiceDtos, PaymentDtos and AccountDtos.
  it.each([
    ['finance-journals', [{ id: '1', number: 'JE-001', title: 'General', status: 'Posted' }], 'JE-001 · General'],
    ['finance-fixed-assets', [{ id: '1', number: 'FA-001', title: 'Office building', status: 'Active' }], 'FA-001 · Office building'],
    ['finance-ap-invoices', { items: [{ id: '1', invoiceNumber: 'VI-001', supplierInvoiceNumber: 'SUP-001' }] }, 'VI-001'],
    ['finance-ap-payments', { items: [{ id: '1', paymentNumber: 'VP-001' }] }, 'VP-001'],
    ['finance-ar-invoices', { items: [{ id: '1', invoiceNumber: 'AR-001' }] }, 'AR-001'],
    ['finance-ar-payments', { items: [{ id: '1', paymentNumber: 'RC-001' }] }, 'RC-001'],
    ['finance-accounts', [{ id: '1', accountCode: '1200', accountName: 'Receivables' }], '1200 · Receivables'],
    // SalesAgreementDTOs; CrmDtos (the CRM primary keys are not named id).
    ['sales-agreements', { items: [{ id: '1', documentNumber: 'SA-001', agreementTitle: 'Maintenance agreement' }] }, 'SA-001 · Maintenance agreement'],
    ['crm-accounts', { items: [{ businessPartnerId: '1', partnerCode: 'BP-001', partnerName: 'Example Ltd' }] }, 'BP-001 · Example Ltd'],
    ['crm-leads', { items: [{ leadId: '1', fullName: 'Alex Example', companyName: 'Example Ltd' }] }, 'Alex Example · Example Ltd'],
    ['crm-opportunities', { items: [{ opportunityId: '1', name: 'New development' }] }, 'New development'],
    ['crm-quotes', { items: [{ quoteId: '1', documentNumber: 'QT-001', quoteName: 'Development quote' }] }, 'QT-001 · Development quote'],
    // MaintenanceDTOs, JobCardDTOs and FleetDtos.
    ['maintenance-assets', { items: [{ id: '1', assetNumber: 'AS-001', name: 'Pump' }] }, 'AS-001 · Pump'],
    ['maintenance-job-cards', { items: [{ id: '1', jobCardNumber: 'JC-001', title: 'Repair pump' }] }, 'JC-001 · Repair pump'],
    ['fleet-vehicles', { items: [{ id: '1', assetNumber: 'VH-001', name: 'Truck' }] }, 'VH-001 · Truck'],
    ['fleet-trips', { items: [{ id: '1', vehicleAssetNumber: 'VH-001', vehicleName: 'Truck' }] }, 'VH-001 · Truck'],
    // DocumentManagementController.ToRecordDto wraps records in data, unlike paged DTOs.
    ['documents', { success: true, data: [{ id: '1', documentReference: 'DOC-001', title: 'Survey plan' }] }, 'DOC-001 · Survey plan'],
  ] as const)('extracts the raw backend response for %s', (id, payload, title) => {
    const result = extractSearchRecords(source(id), payload);
    expect(result).toHaveLength(1);
    expect(result[0].title).toBe(title);
  });

  it('deep-links to the selected maintenance record without triggering a write action', () => {
    const workOrders = source('maintenance-work-orders');
    expect(extractSearchRecords(workOrders, { items: [
      { id: 'wo-1', workOrderNumber: 'WO-001', title: 'Service pump', assetNumber: 'PUMP-01' },
    ] })[0]).toMatchObject({ href: '/maintenance/work-orders?recordId=wo-1', subtitle: 'PUMP-01' });
    expect(workOrders.detailPath).not.toMatch(/edit|create|approve/);
  });

  it('keeps fleet records in the Fleet module filter despite their maintenance API paths', () => {
    expect(source('fleet-vehicles').module).toBe('Fleet');
    expect(source('fleet-trips').module).toBe('Fleet');
  });
});
