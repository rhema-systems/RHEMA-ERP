import { describe, expect, it } from 'vitest';
import { getGlobalSearchAdministrationSources } from './global-search-administration-sources';
import { accessibleSearchSources, extractSearchRecords, searchRecordRequest } from './global-search';
import { buildGlobalSearchNavigation } from './global-search-navigation';

describe('Administration identity search', () => {
  it('requires the exact owner read roles, not broad Administration aliases', () => {
    for (const role of ['Manager', 'Admin', 'SystemAdmin', 'Employee']) {
      expect(getGlobalSearchAdministrationSources(roles => roles.includes(role))).toEqual([]);
    }
    for (const role of ['TenantAdmin', 'SuperAdmin']) {
      const hasRole = (roles: string[]) => roles.includes(role);
      const sources = getGlobalSearchAdministrationSources(hasRole);
      const nav = buildGlobalSearchNavigation(hasRole, () => false);
      expect(accessibleSearchSources(sources, nav, () => false)).toHaveLength(2);
    }
  });

  it('uses bounded minimal-summary owners and exact read-only destinations', () => {
    for (const source of getGlobalSearchAdministrationSources(() => true)) {
      const request = searchRecordRequest(source, 'Finance');
      const url = new URL(request.endpoint, 'https://test.local');
      expect(request.options.method).toBe('GET');
      expect(url.searchParams.get('search')).toBe('Finance');
      expect(url.searchParams.get('take')).toBe('5');
      const result = extractSearchRecords(source, [{ id: 'record-1', username: 'finance', name: 'Finance', status: 'Active' }]);
      expect(result[0].href).toBe(`${source.route}/record-1`);
      expect(result[0].module).toBe('Administration');
    }
  });
});
