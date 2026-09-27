import { describe, expect, it, vi, beforeEach } from 'vitest';
import { accessibleSearchSources, extractSearchRecords, searchGlobalRecords, searchRecordRequest, type GlobalSearchRecordSource } from './global-search';
const api = vi.hoisted(() => ({ silentRequest: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));
const source: GlobalSearchRecordSource = { id: 'invoices', module: 'Finance', label: 'Invoice', route: '/invoices',
  endpoint: '/invoices', searchParam: 'search', params: { pageSize: 5 }, idField: 'id', titleFields: ['number'],
  subtitleFields: ['customer'], statusField: 'status', detailPath: '/invoices/:id' };

describe('global record search', () => {
  beforeEach(() => api.silentRequest.mockReset());
  it('retains filtered navigation scopes instead of expanding them to all records', () => {
    const customer = { ...source, route: '/partners' };
    expect(accessibleSearchSources([customer], [{ href: '/partners?partnerType=Customer' }], () => true)[0].params)
      .toEqual({ pageSize: 5, partnerType: 'Customer' });
    expect(accessibleSearchSources([{ ...customer, params: { partnerType: 'Supplier' } }],
      [{ href: '/partners?partnerType=Customer' }], () => true)).toEqual([]);
    expect(accessibleSearchSources([customer], [{ href: '/other' }], () => true)).toEqual([]);
  });
  it('encodes the query and only selects reviewed summary fields', () => {
    expect(searchRecordRequest(source, 'A&B').endpoint).toBe('/invoices?pageSize=5&search=A%26B');
    expect(extractSearchRecords(source, { items: [{ id: 'a/b', number: 'INV-1', customer: 'Example', status: 'Draft', bankAccount: 'secret' }] }))
      .toEqual([{ id: 'invoices:a/b', title: 'INV-1', subtitle: 'Example', module: 'Finance', kind: 'Invoice', href: '/invoices/a%2Fb', status: 'Draft' }]);
  });
  it('retains paging on POST search contracts', () => {
    const request = searchRecordRequest({ ...source, method: 'POST', searchIn: 'body', body: { active: true } }, 'Ada');
    expect(request.endpoint).toBe('/invoices?pageSize=5');
    expect(JSON.parse(request.options.body!)).toEqual({ active: true, search: 'Ada' });
  });
  it('rejects invalid shapes and unsafe links, and bounds suggestions', () => {
    expect(() => extractSearchRecords(source, {})).toThrow();
    expect(extractSearchRecords({ ...source, detailPath: '//outside/:id' }, { items: [{ id: '1', number: 'A' }] })).toEqual([]);
    expect(extractSearchRecords(source, { items: Array.from({ length: 20 }, (_, id) => ({ id, number: `A${id}` })) })).toHaveLength(5);
  });
  it('keeps successes when one source fails, without exposing denied sources', async () => {
    api.silentRequest.mockResolvedValueOnce({ items: [{ id: '1', number: 'INV-1' }] })
      .mockRejectedValueOnce({ status: 403 }).mockRejectedValueOnce({ status: 500 });
    const progress = vi.fn();
    await searchGlobalRecords([source, { ...source, id: 'denied' }, { ...source, id: 'failed' }], 'INV', new AbortController().signal, progress);
    expect(progress.mock.lastCall?.[0]).toHaveLength(1);
    expect(progress.mock.lastCall?.[1]).toBe(1);
  });
  it('does not publish a late response after cancellation', async () => {
    let resolve!: (value: unknown) => void;
    api.silentRequest.mockReturnValue(new Promise(done => { resolve = done; }));
    const controller = new AbortController(); const progress = vi.fn();
    const run = searchGlobalRecords([source], 'INV', controller.signal, progress);
    controller.abort(); resolve({ items: [{ id: '1', number: 'INV-1' }] }); await run;
    expect(progress).not.toHaveBeenCalled();
  });
  it('does not search for empty or excessive input', async () => {
    await searchGlobalRecords([source], 'a', new AbortController().signal, vi.fn());
    await searchGlobalRecords([source], 'a'.repeat(101), new AbortController().signal, vi.fn());
    expect(api.silentRequest).not.toHaveBeenCalled();
  });
});
