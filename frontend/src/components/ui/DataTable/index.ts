// Main DataTable component
export { DataTable, type DataTableProps, type DataTableColumn, type DataTableAction } from './DataTable';

// Sub-components
export { DataTableToolbar, type DataTableToolbarProps } from './DataTableToolbar';
export { DataTablePagination, type DataTablePaginationProps } from './DataTablePagination';
export { DataTableColumnFilter, type DataTableColumnFilterProps } from './DataTableColumnFilter';
export { DataTableExport, type DataTableExportProps } from './DataTableExport';

// Re-export useful types from TanStack Table
export type {
  ColumnDef,
  SortingState,
  ColumnFiltersState,
  VisibilityState,
  RowSelectionState,
  PaginationState,
  Row,
  Table,
} from '@tanstack/react-table';