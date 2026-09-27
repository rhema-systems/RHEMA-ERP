import type { GlobalSearchRecordSource } from './global-search';

/** Match the identity owners' exact read roles, which are narrower than Administration navigation. */
export function getGlobalSearchAdministrationSources(hasAnyRole: (roles: string[]) => boolean): GlobalSearchRecordSource[] {
  if (!hasAnyRole(['TenantAdmin', 'SuperAdmin'])) return [];
  return [
    {
      id: 'administration-users', module: 'Administration', label: 'Tenant user',
      route: '/administration/identity-management/users', endpoint: '/administration/user-tenant-mappings/current/users/search',
      searchParam: 'search', params: { take: 5 }, itemsPath: '', idField: 'id',
      titleFields: ['username', 'name'], statusField: 'status',
      detailPath: '/administration/identity-management/users/:id', maxResults: 5,
    },
    {
      id: 'administration-roles', module: 'Administration', label: 'Role',
      route: '/administration/identity-management/roles', endpoint: '/Role/search-summaries',
      searchParam: 'search', params: { take: 5 }, itemsPath: '', idField: 'id',
      titleFields: ['name'], statusField: 'status',
      detailPath: '/administration/identity-management/roles/:id', maxResults: 5,
    },
  ];
}
