import { describe, expect, it } from 'vitest';
import { GLOBAL_SEARCH_OPERATIONS_SOURCES } from './global-search-operations-sources';
import { accessibleSearchSources, extractSearchRecords, searchRecordRequest } from './global-search';
import { buildGlobalSearchNavigation } from './global-search-navigation';

describe('Safety record search sources', () => {
  it('uses real accessible navigation leaves and still requires SHE read permission', () => {
    const navigation = buildGlobalSearchNavigation(roles => roles.includes('SuperAdmin'), () => true);
    expect(accessibleSearchSources(GLOBAL_SEARCH_OPERATIONS_SOURCES, navigation, () => true)).toHaveLength(5);
    expect(accessibleSearchSources(GLOBAL_SEARCH_OPERATIONS_SOURCES, navigation, () => false)).toEqual([]);
    expect(accessibleSearchSources(GLOBAL_SEARCH_OPERATIONS_SOURCES, [], () => true)).toEqual([]);
  });

  it('sends text and a small response limit through read-only owner endpoints', () => {
    for (const source of GLOBAL_SEARCH_OPERATIONS_SOURCES.filter(item => item.module === 'Safety (SHE)')) {
      const request = searchRecordRequest(source, 'Safety plan');
      const url = new URL(request.endpoint, 'https://test.local');
      expect(request.options.method).toBe('GET');
      expect(url.searchParams.get('search')).toBe('Safety plan');
      expect(url.searchParams.get('take')).toBe('5');
    }
  });

  it('keeps internal and external enquiries in the Enquiry module and their own permission scope', () => {
    const permitsInternal = (permissions: string[]) => permissions.includes('enquiry.internal.access');
    const navigation = buildGlobalSearchNavigation(() => false, permitsInternal);
    const available = accessibleSearchSources(GLOBAL_SEARCH_OPERATIONS_SOURCES, navigation, permitsInternal);
    expect(available.map(source => source.id)).toEqual(['enquiry-internal']);
    for (const source of GLOBAL_SEARCH_OPERATIONS_SOURCES.filter(item => item.module === 'Enquiry')) {
      const url = new URL(searchRecordRequest(source, 'Boundary').endpoint, 'https://test.local');
      expect(url.searchParams.get('q')).toBe('Boundary');
      expect(url.searchParams.get('limit')).toBe('5');
      expect(url.searchParams.get('scope')).toBe(source.id);
      expect(extractSearchRecords(source, { data: [{ id: 'e1', ticketNumber: 'ENQ-42', subject: 'Boundary' }] })[0].href)
        .toBe(`/helpdesk/tickets/e1?scope=${source.id}`);
    }
  });

  it.each([
    ['she-permits', { id: 'p1', registerNumber: 'PER-42', permitName: 'Operating licence' }, '/hr/safety/environmental/permits/p1'],
    ['she-documents', { id: 'd1', documentNumber: 'DOC-42', title: 'Safety plan' }, '/hr/safety/documents/d1'],
    ['she-environmental-reviews', { id: 'r1', reviewNumber: 'ENV-42', projectName: 'Tema' }, '/hr/safety/environmental/reviews/r1'],
  ])('opens the selected %s through its existing detail route', (id, row, href) => {
    const source = GLOBAL_SEARCH_OPERATIONS_SOURCES.find(item => item.id === id)!;
    const results = extractSearchRecords(source, [row]);
    expect(results).toHaveLength(1);
    expect(results[0].href).toBe(href);
  });
});
