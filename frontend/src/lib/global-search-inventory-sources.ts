import type { GlobalSearchRecordSource } from './global-search';

// These Inventory screens load the requested record into their existing dialog.
export const GLOBAL_SEARCH_INVENTORY_SOURCES: GlobalSearchRecordSource[] = [
  {
    id: 'inventory-issue-vouchers', module: 'Inventory', label: 'Store issue voucher',
    route: '/inventory/requisitions', endpoint: '/inventory/requisitions/issue-vouchers/search', searchParam: 'search',
    detailPath: '/inventory/issue-vouchers/:id', params: { take: 5 }, itemsPath: '', idField: 'id',
    titleFields: ['voucherNumber'], subtitleFields: ['requisitionNumber'], statusField: 'status',
  },
  {
    id: 'inventory-items', module: 'Inventory', label: 'Inventory items',
    route: '/inventory/items', endpoint: '/InventoryItems/search', searchParam: 'searchTerm',
    detailPath: '/inventory/items?recordId=:id',
    params: { take: 8 }, itemsPath: '', idField: 'id', titleFields: ['itemCode', 'name'],
    subtitleFields: ['description'],
  },
  {
    id: 'inventory-warehouses', module: 'Inventory', label: 'Warehouses',
    route: '/administration/inventory/warehouses', endpoint: '/inventory/warehouses/search', searchParam: 'search',
    detailPath: '/administration/inventory/warehouses?recordId=:id',
    params: { take: 8 }, itemsPath: '', idField: 'id', titleFields: ['code', 'name'],
  },
  {
    id: 'inventory-transfers', module: 'Inventory', label: 'Inventory transfers',
    route: '/inventory/transfers', endpoint: '/inventory/transfers/search', searchParam: 'search',
    detailPath: '/inventory/transfers?recordId=:id',
    params: { take: 8 }, itemsPath: '', idField: 'id', titleFields: ['transferNumber'],
    subtitleFields: ['sourceWarehouseName', 'destinationWarehouseName'], statusField: 'status',
  },
  {
    id: 'inventory-physical-counts', module: 'Inventory', label: 'Physical counts',
    route: '/inventory/physical-counts', endpoint: '/inventory/physical-counts/search', searchParam: 'search',
    detailPath: '/inventory/physical-counts?recordId=:id',
    params: { take: 8 }, itemsPath: '', idField: 'id', titleFields: ['countNumber'],
    subtitleFields: ['warehouseName'], statusField: 'status',
  },
  {
    id: 'inventory-requisitions', module: 'Inventory', label: 'Inventory requisitions',
    route: '/inventory/requisitions', endpoint: '/inventory/requisitions/search', searchParam: 'search',
    detailPath: '/inventory/requisitions?recordId=:id',
    params: { take: 8 }, itemsPath: '', idField: 'id', titleFields: ['requisitionNumber'],
    subtitleFields: ['description', 'warehouseName', 'projectCode'], statusField: 'status',
  },
  {
    id: 'inventory-disposals', module: 'Inventory', label: 'Inventory disposals',
    route: '/inventory/disposals', endpoint: '/inventory/disposals/search', searchParam: 'search',
    detailPath: '/inventory/disposals?recordId=:id',
    params: { take: 8 }, itemsPath: '', idField: 'id', titleFields: ['disposalNumber'],
    subtitleFields: ['reason', 'warehouseName'], statusField: 'status',
  },
];
