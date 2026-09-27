import { describe, expect, it } from 'vitest';
import { GLOBAL_SEARCH_ESTATE_LEGAL_SOURCES } from './global-search-estate-legal-sources';
import { accessibleSearchSources, extractSearchRecords, searchRecordRequest } from './global-search';
import { buildGlobalSearchNavigation } from './global-search-navigation';

describe('Estate, Legal and property enquiry global search providers', () => {
  it('exposes all four providers through actual administrator navigation leaves', () => {
    const navigation = buildGlobalSearchNavigation(roles => roles.includes('SuperAdmin'), () => true);
    const available = accessibleSearchSources(GLOBAL_SEARCH_ESTATE_LEGAL_SOURCES, navigation, () => true);
    expect(available.map(source => source.id).sort()).toEqual(GLOBAL_SEARCH_ESTATE_LEGAL_SOURCES.map(source => source.id).sort());
    expect(navigation.some(item => item.href === '/legal')).toBe(true);
    expect(navigation.some(item => item.href === '/sales/property-enquiries')).toBe(true);
  });

  it('uses bounded read-only search endpoints, never the acquisition synchronization board', () => {
    for (const source of GLOBAL_SEARCH_ESTATE_LEGAL_SOURCES) {
      const request = searchRecordRequest(source, 'REF 42');
      const url = new URL(request.endpoint, 'https://test.local');
      expect(request.options.method).toBe('GET');
      expect(url.searchParams.get('search')).toBe('REF 42');
      expect(url.searchParams.get('take')).toBe('5');
      expect(url.pathname.endsWith('/search')).toBe(true);
    }
    const legal = GLOBAL_SEARCH_ESTATE_LEGAL_SOURCES.find(source => source.id === 'legal-cases')!;
    expect(new URL(searchRecordRequest(legal, 'court').endpoint, 'https://test.local').searchParams.get('module')).toBe('Legal');
  });

  it.each([
    ['estate-acquisitions', { id: 'a1', projectReference: 'ACQ-42', location: 'Tema' }, '/estate/land-acquisition?acquisitionId=a1'],
    ['legal-cases', { id: 'c1', referenceNumber: 'CASE-42', title: 'Boundary dispute' }, '/legal/cases/c1'],
    ['legal-procedures', { entityType: 'LegalMortgage', title: 'Mortgages' }, '/legal/LegalMortgage'],
    ['property-enquiries', { id: 'p1', ticketNumber: 'PE-42', subject: 'Plot enquiry' }, '/sales/property-enquiries?id=p1'],
  ])('opens the selected %s record', (sourceId, row, href) => {
    const source = GLOBAL_SEARCH_ESTATE_LEGAL_SOURCES.find(item => item.id === sourceId)!;
    const results = extractSearchRecords(source, { data: [row] });
    expect(results).toHaveLength(1);
    expect(results[0].href).toBe(href);
    expect(results[0].kind).toBe(source.label);
  });
});
