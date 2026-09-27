import type { GlobalSearchRecordSource } from './global-search';

/** Existing SHE owners retain their read permissions and tenant filtering.
 * The optional take bounds responses; these legacy owners materialize matches first. */
export const GLOBAL_SEARCH_OPERATIONS_SOURCES: GlobalSearchRecordSource[] = [
  {
    id: 'she-permits', module: 'Safety (SHE)', label: 'Environmental permit',
    route: '/hr/safety/environmental/permits', endpoint: '/safety/environmental/permits/search', searchParam: 'search',
    params: { take: 5 }, itemsPath: '', idField: 'id',
    titleFields: ['registerNumber', 'permitName'], statusField: 'status',
    permissions: ['HR.She.Read'], detailPath: '/hr/safety/environmental/permits/:id', maxResults: 5,
  },
  {
    id: 'she-documents', module: 'Safety (SHE)', label: 'Controlled document',
    route: '/hr/safety/documents', endpoint: '/safety/documents', searchParam: 'search',
    params: { take: 5 }, itemsPath: '', idField: 'id',
    titleFields: ['documentNumber', 'title'], statusField: 'status',
    permissions: ['HR.She.Read'], detailPath: '/hr/safety/documents/:id', maxResults: 5,
  },
  {
    id: 'she-environmental-reviews', module: 'Safety (SHE)', label: 'Environmental review',
    route: '/hr/safety/environmental/reviews', endpoint: '/safety/environmental/reviews', searchParam: 'search',
    params: { take: 5 }, itemsPath: '', idField: 'id',
    titleFields: ['reviewNumber', 'projectName'], statusField: 'status',
    permissions: ['HR.She.Read'], detailPath: '/hr/safety/environmental/reviews/:id', maxResults: 5,
  },
  {
    id: 'enquiry-internal', module: 'Enquiry', label: 'Internal enquiry',
    route: '/helpdesk/enquiry/internal', endpoint: '/ehc/internal/lookups/tickets', searchParam: 'q',
    params: { limit: 5, scope: 'enquiry-internal' }, itemsPath: 'data', idField: 'id',
    titleFields: ['ticketNumber', 'subject'], statusField: 'status',
    permissions: ['enquiry.internal.access'], detailPath: '/helpdesk/tickets/:id?scope=enquiry-internal', maxResults: 5,
  },
  {
    id: 'enquiry-external', module: 'Enquiry', label: 'External enquiry',
    route: '/helpdesk/enquiry/external', endpoint: '/ehc/internal/lookups/tickets', searchParam: 'q',
    params: { limit: 5, scope: 'enquiry-external' }, itemsPath: 'data', idField: 'id',
    titleFields: ['ticketNumber', 'subject'], statusField: 'status',
    permissions: ['enquiry.external.access'], detailPath: '/helpdesk/tickets/:id?scope=enquiry-external', maxResults: 5,
  },
];
