import type { GlobalSearchRecordSource } from './global-search';

/** Read-only searches use each module's existing tenant and record-visibility owner. */
export const GLOBAL_SEARCH_ESTATE_LEGAL_SOURCES: GlobalSearchRecordSource[] = [
  {
    id: 'estate-acquisitions', module: 'Estate', label: 'Land acquisition',
    route: '/estate/land-acquisition', endpoint: '/estate/land-acquisitions/search',
    searchParam: 'search', params: { take: 5 }, itemsPath: 'data', idField: 'id',
    titleFields: ['projectReference', 'location'], statusField: 'status',
    detailPath: '/estate/land-acquisition?acquisitionId=:id', maxResults: 5,
  },
  {
    id: 'legal-cases', module: 'Legal', label: 'Legal case',
    route: '/legal', endpoint: '/procedure-cases/search', searchParam: 'search',
    params: { module: 'Legal', take: 5 }, itemsPath: 'data', idField: 'id',
    titleFields: ['referenceNumber', 'title'], subtitleFields: ['currentStageName'], statusField: 'status',
    detailPath: '/legal/cases/:id', maxResults: 5,
  },
  {
    id: 'legal-procedures', module: 'Legal', label: 'Legal procedure',
    route: '/legal', endpoint: '/legal/procedures/search', searchParam: 'search',
    params: { take: 5 }, itemsPath: 'data', idField: 'entityType', titleFields: ['title'],
    detailPath: '/legal/:id', maxResults: 5,
  },
  {
    id: 'property-enquiries', module: 'Sales', label: 'Property enquiry',
    route: '/sales/property-enquiries', endpoint: '/ehc/internal/property-enquiries/search',
    searchParam: 'search', params: { take: 5 }, itemsPath: 'data', idField: 'id',
    titleFields: ['ticketNumber', 'subject'], statusField: 'status',
    detailPath: '/sales/property-enquiries?id=:id', maxResults: 5,
  },
];
