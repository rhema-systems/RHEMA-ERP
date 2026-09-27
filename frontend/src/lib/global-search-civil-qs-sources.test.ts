import { describe, expect, it } from 'vitest';
import { GLOBAL_SEARCH_CIVIL_QS_SOURCES } from './global-search-civil-qs-sources';

describe('Civil and QS search source contracts', () => {
  it('uses the interim valuation identity accepted by the valuation owner Get route', () => {
    const valuation = GLOBAL_SEARCH_CIVIL_QS_SOURCES.find(source => source.id === 'qs-valuation-worksheets');
    expect(valuation?.idField).toBe('projectInterimValuationId');
    expect(valuation?.detailPath).toContain('kind=valuation-worksheets&recordId=:id');
  });
  it('uses bounded queries and exact record links for all eleven owners', () => {
    expect(GLOBAL_SEARCH_CIVIL_QS_SOURCES).toHaveLength(11);
    for (const source of GLOBAL_SEARCH_CIVIL_QS_SOURCES) {
      if (source.itemsPath === 'items') {
        expect(source.params).toEqual({ page: 1, pageSize: 5 });
      } else {
        expect(source.itemsPath).toBe('');
        expect(source.params?.take).toBe(5);
      }
      expect(source.detailPath).toContain(':id');
      expect(source.permissions?.length).toBeGreaterThan(0);
    }
  });
  it('opens existing profile detail routes and keeps catalogue search distinct from paged registers', () => {
    expect(GLOBAL_SEARCH_CIVIL_QS_SOURCES.find(source => source.id === 'qs-configuration-profiles')?.detailPath)
      .toBe('/administration/project-management/quantity-survey-config/:id');
    expect(GLOBAL_SEARCH_CIVIL_QS_SOURCES.find(source => source.id === 'civil-configuration-profiles')?.detailPath)
      .toBe('/administration/project-management/civil-engineering-config/:id');
    expect(GLOBAL_SEARCH_CIVIL_QS_SOURCES.find(source => source.id === 'qs-catalogues')?.endpoint)
      .toBe('/quantity-survey/catalogues/search');
  });
});
