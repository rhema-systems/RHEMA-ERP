"use client";

import React, { useMemo, useState, useCallback, useEffect } from 'react';
import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  ColumnDef,
  SortingState,
  ColumnFiltersState,
  VisibilityState,
  RowSelectionState,
  PaginationState,
  Table as TanStackTable,
  Row,
  Updater,
  flexRender,
} from '@tanstack/react-table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useDeviceType } from '@/hooks/useResponsive';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuTrigger,
  DropdownMenuSeparator,
  DropdownMenuLabel,
} from '@/components/ui/dropdown-menu';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
import {
  ChevronDownIcon,
  ChevronUpIcon,
  ChevronsUpDownIcon,
  DownloadIcon,
  EyeIcon,
  FilterIcon,
  RefreshCwIcon,
  SearchIcon,
  SettingsIcon,
  Trash2Icon,
  PlusIcon,
  EditIcon,
} from 'lucide-react';
import { DataTableExport } from './DataTableExport';
import { DataTablePagination } from './DataTablePagination';
import { DataTableColumnFilter } from './DataTableColumnFilter';
import { DataTableToolbar } from './DataTableToolbar';

export type DataTableColumn<TData> = ColumnDef<TData> & {
  accessorKey?: string;
  title?: string;
  searchable?: boolean;
  filterable?: boolean;
  sortable?: boolean;
  exportable?: boolean;
  width?: number | string;
  minWidth?: number;
  maxWidth?: number;
};

export interface DataTableAction<TData = any> {
  id: string;
  label: string;
  icon?: React.ComponentType<{ className?: string }>;
  onClick: (row: Row<TData>) => void;
  variant?: 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link';
  size?: 'default' | 'sm' | 'lg' | 'icon';
  disabled?: (row: Row<TData>) => boolean;
  hidden?: (row: Row<TData>) => boolean;
}

export interface DataTableProps<TData> {
  // Data and columns
  data: TData[];
  columns: DataTableColumn<TData>[];
  
  // Basic configuration
  title?: string;
  description?: string;
  loading?: boolean;
  error?: string | null;
  
  // Selection
  enableRowSelection?: boolean;
  onRowSelectionChange?: (selectedRows: TData[]) => void;
  rowSelectionState?: RowSelectionState;
  
  // Pagination
  enablePagination?: boolean;
  pageSize?: number;
  pageSizeOptions?: number[];
  serverSidePagination?: boolean;
  totalRows?: number;
  onPaginationChange?: (pagination: PaginationState) => void;
  
  // Sorting
  enableSorting?: boolean;
  serverSideSorting?: boolean;
  onSortingChange?: (sorting: SortingState) => void;
  
  // Filtering
  enableGlobalFilter?: boolean;
  enableColumnFilters?: boolean;
  serverSideFiltering?: boolean;
  onGlobalFilterChange?: (filter: string) => void;
  onColumnFiltersChange?: (filters: ColumnFiltersState) => void;
  
  // Export
  enableExport?: boolean;
  exportFileName?: string;
  exportFormats?: ('csv' | 'excel' | 'json')[];
  
  // Actions
  rowActions?: DataTableAction<TData>[];
  toolbarActions?: {
    create?: () => void;
    refresh?: () => void;
    delete?: (selectedRows: TData[]) => void;
    customActions?: Array<{
      id: string;
      label: string;
      icon?: React.ComponentType<{ className?: string }>;
      onClick: (selectedRows: TData[]) => void;
      variant?: 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link';
      requiresSelection?: boolean;
    }>;
  };
  
  // Styling
  className?: string;
  tableClassName?: string;
  showBorder?: boolean;
  striped?: boolean;
  hoverable?: boolean;
  compact?: boolean;
  
  // Advanced features
  enableColumnVisibility?: boolean;
  initialColumnVisibility?: VisibilityState;
  enableSearch?: boolean;
  searchPlaceholder?: string;
  emptyStateMessage?: string;
  onRowClick?: (row: Row<TData>) => void;
  onRowDoubleClick?: (row: Row<TData>) => void;
  getRowId?: (row: TData, index: number) => string;
  
  // Mobile/responsive features
  mobileBreakpoint?: number;
  enableMobileCards?: boolean;
  mobileCardRenderer?: (row: Row<TData>) => React.ReactNode;
  hideColumnsOnMobile?: string[];
  enableHorizontalScroll?: boolean;
}

export function DataTable<TData>({
  data,
  columns,
  title,
  description,
  loading = false,
  error = null,
  
  // Selection
  enableRowSelection = false,
  onRowSelectionChange,
  rowSelectionState,
  
  // Pagination
  enablePagination = true,
  pageSize = 10,
  pageSizeOptions = [5, 10, 20, 50, 100],
  serverSidePagination = false,
  totalRows,
  onPaginationChange,
  
  // Sorting
  enableSorting = true,
  serverSideSorting = false,
  onSortingChange,
  
  // Filtering
  enableGlobalFilter = true,
  enableColumnFilters = true,
  serverSideFiltering = false,
  onGlobalFilterChange,
  onColumnFiltersChange,
  
  // Export
  enableExport = true,
  exportFileName = 'export',
  exportFormats = ['csv', 'excel'],
  
  // Actions
  rowActions = [],
  toolbarActions,
  
  // Styling
  className = '',
  tableClassName = '',
  showBorder = true,
  striped = true,
  hoverable = true,
  compact = false,
  
  // Advanced
  enableColumnVisibility = true,
  initialColumnVisibility,
  enableSearch = true,
  searchPlaceholder = 'Search all columns...',
  emptyStateMessage = 'No data available',
  onRowClick,
  onRowDoubleClick,
  getRowId,
  
  // Mobile/responsive
  mobileBreakpoint = 768,
  enableMobileCards = true,
  mobileCardRenderer,
  hideColumnsOnMobile = [],
  enableHorizontalScroll = true,
}: DataTableProps<TData>) {
  const { isMobile, isTablet, needsMoreSpacing } = useDeviceType();
  const isCompact = compact;
  
  const [sorting, setSorting] = useState<SortingState>([]);
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([]);
  const [columnVisibility, setColumnVisibility] = useState<VisibilityState>(initialColumnVisibility ?? {});
  const [rowSelection, setRowSelection] = useState<RowSelectionState>(rowSelectionState || {});
  const [globalFilter, setGlobalFilter] = useState('');
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: pageSize,
  });

  const handleSortingChange = useCallback((updater: Updater<SortingState>) => {
    if (serverSideSorting) {
      const nextSorting = typeof updater === 'function' ? updater(sorting) : updater;
      setSorting(nextSorting);
      onSortingChange?.(nextSorting);
      return;
    }

    setSorting(updater);
  }, [onSortingChange, serverSideSorting, sorting]);

  const handleColumnFiltersChange = useCallback((updater: Updater<ColumnFiltersState>) => {
    if (serverSideFiltering) {
      const nextFilters = typeof updater === 'function' ? updater(columnFilters) : updater;
      setColumnFilters(nextFilters);
      onColumnFiltersChange?.(nextFilters);
      return;
    }

    setColumnFilters(updater);
  }, [columnFilters, onColumnFiltersChange, serverSideFiltering]);

  const handlePaginationChange = useCallback((updater: Updater<PaginationState>) => {
    if (serverSidePagination) {
      const nextPagination = typeof updater === 'function' ? updater(pagination) : updater;
      setPagination(nextPagination);
      onPaginationChange?.(nextPagination);
      return;
    }

    setPagination(updater);
  }, [onPaginationChange, pagination, serverSidePagination]);
  
  // Responsive behavior
  const shouldUseMobileView = enableMobileCards && (isMobile || isTablet);
  const shouldHideColumns = isMobile && hideColumnsOnMobile.length > 0;

  // Apply responsive column visibility
  useEffect(() => {
    if (shouldHideColumns) {
      const newVisibility: VisibilityState = {};
      hideColumnsOnMobile.forEach(columnId => {
        newVisibility[columnId] = false;
      });
      setColumnVisibility(prev => ({ ...prev, ...newVisibility }));
    }
  }, [shouldHideColumns, hideColumnsOnMobile]);
  
  // Mobile card renderer
  const renderMobileCard = (row: Row<TData>) => {
    if (mobileCardRenderer) {
      return mobileCardRenderer(row);
    }
    
    // Default mobile card layout
    return (
      <Card className={`${needsMoreSpacing ? 'p-4' : 'p-3'} mb-3`}>
        <CardContent className="p-0">
          <div className="space-y-2">
            {table.getAllColumns()
              .filter(column => column.getIsVisible() && column.id !== 'select' && column.id !== 'actions')
              .slice(0, 3) // Show only first 3 columns in card
              .map(column => {
                const matchingCell = row.getVisibleCells().find(cell => cell.column.id === column.id);
                if (!matchingCell) {
                  return null;
                }

                const cellValue = flexRender(
                  matchingCell.column.columnDef.cell,
                  matchingCell.getContext(),
                );
                
                return (
                  <div key={column.id} className="flex justify-between items-start">
                    <span className="text-sm font-medium text-muted-foreground min-w-0 flex-shrink-0 mr-3">
                      {column.columnDef.header as string || column.id}
                    </span>
                    <span className="text-sm text-right min-w-0 flex-grow">
                      {cellValue}
                    </span>
                  </div>
                );
              })}
              
            {/* Row actions for mobile */}
            {rowActions.length > 0 && (
              <div className="flex items-center justify-end space-x-1 pt-2 border-t">
                {rowActions.slice(0, 2).map((action) => {
                  const isHidden = action.hidden?.(row);
                  const isDisabled = action.disabled?.(row);
                  
                  if (isHidden) return null;

                  const IconComponent = action.icon || EditIcon;
                  
                  return (
                    <Button
                      key={action.id}
                      variant={action.variant || 'ghost'}
                      size="sm"
                      disabled={isDisabled}
                      onClick={(e) => {
                        e.stopPropagation();
                        action.onClick(row);
                      }}
                      className="h-8 px-2"
                      title={action.label}
                    >
                      <IconComponent className="h-3 w-3 mr-1" />
                      {action.label}
                    </Button>
                  );
                })}
              </div>
            )}
          </div>
        </CardContent>
      </Card>
    );
  };
  
  // Memoize table columns with action column
  const tableColumns = useMemo<ColumnDef<TData>[]>(() => {
    const cols: ColumnDef<TData>[] = [];

    // Add selection column if enabled
    if (enableRowSelection) {
      cols.push({
        id: 'select',
        header: ({ table }) => (
          <Checkbox
            checked={table.getIsAllPageRowsSelected() || (table.getIsSomePageRowsSelected() && 'indeterminate')}
            onCheckedChange={(value) => table.toggleAllPageRowsSelected(!!value)}
            aria-label="Select all"
            className="translate-y-[2px]"
          />
        ),
        cell: ({ row }: { row: Row<TData> }) => (
          <Checkbox
            checked={row.getIsSelected()}
            onCheckedChange={(value) => row.toggleSelected(!!value)}
            aria-label="Select row"
            className="translate-y-[2px]"
          />
        ),
        enableSorting: false,
        enableHiding: false,
        size: 40,
      });
    }

    // Add data columns
    cols.push(...columns);

    // Add actions column if there are row actions
    if (rowActions.length > 0) {
      cols.push({
        id: 'actions',
        header: 'Actions',
        cell: ({ row }: { row: Row<TData> }) => (
          <div className="flex items-center space-x-1">
            {rowActions.map((action) => {
              const isHidden = action.hidden?.(row);
              const isDisabled = action.disabled?.(row);
              
              if (isHidden) return null;

              const IconComponent = action.icon || EditIcon;
              
              return (
                <Button
                  key={action.id}
                  variant={action.variant || 'ghost'}
                  size={action.size || 'sm'}
                  disabled={isDisabled}
                  onClick={(e) => {
                    e.stopPropagation();
                    action.onClick(row);
                  }}
                  className="h-7 w-7 p-0"
                  title={action.label}
                >
                  <IconComponent className="h-3 w-3" />
                </Button>
              );
            })}
          </div>
        ),
        enableSorting: false,
        enableHiding: false,
        size: rowActions.length * 35 + 10,
      });
    }

    return cols;
  }, [columns, enableRowSelection, rowActions]);

  // Create the table instance
  const table = useReactTable({
    data,
    columns: tableColumns,
    onSortingChange: handleSortingChange,
    onColumnFiltersChange: handleColumnFiltersChange,
    onColumnVisibilityChange: setColumnVisibility,
    onRowSelectionChange: setRowSelection,
    onGlobalFilterChange: serverSideFiltering ? onGlobalFilterChange : setGlobalFilter,
    onPaginationChange: handlePaginationChange,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: serverSideSorting ? undefined : getSortedRowModel(),
    getFilteredRowModel: serverSideFiltering ? undefined : getFilteredRowModel(),
    getPaginationRowModel: serverSidePagination ? undefined : getPaginationRowModel(),
    getRowId,
    state: {
      sorting,
      columnFilters,
      columnVisibility,
      rowSelection,
      globalFilter,
      pagination,
    },
    // Server-side pagination configuration
    ...(serverSidePagination && totalRows !== undefined && {
      pageCount: Math.ceil(totalRows / pagination.pageSize),
      manualPagination: true,
    }),
    // Server-side sorting configuration
    ...(serverSideSorting && {
      manualSorting: true,
    }),
    // Server-side filtering configuration
    ...(serverSideFiltering && {
      manualFiltering: true,
    }),
    enableRowSelection,
    enableSorting,
    enableColumnFilters,
    enableGlobalFilter,
  });

  // Handle row selection changes
  const selectedRows = useMemo(() => {
    return table.getFilteredSelectedRowModel().rows.map(row => row.original);
  }, [table, rowSelection]);

  React.useEffect(() => {
    onRowSelectionChange?.(selectedRows);
  }, [selectedRows, onRowSelectionChange]);

  // Handle row clicks
  const handleRowClick = useCallback((row: Row<TData>, event: React.MouseEvent) => {
    if (event.detail === 2) {
      onRowDoubleClick?.(row);
    } else {
      onRowClick?.(row);
    }
  }, [onRowClick, onRowDoubleClick]);

  if (error) {
    return (
      <Card className={className}>
        <CardContent className="py-6">
          <div className="text-center text-destructive">
            Error loading data: {error}
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className={`${className} ${showBorder ? 'border' : 'border-0'}`}>
      {(title || description) && (
        <CardHeader className={compact ? 'py-4' : ''}>
          {title && <CardTitle>{title}</CardTitle>}
          {description && <p className="text-sm text-muted-foreground">{description}</p>}
        </CardHeader>
      )}
      
      <CardContent className="p-0">
        {/* Toolbar */}
        <DataTableToolbar
          table={table}
          compact={compact}
          enableGlobalFilter={enableGlobalFilter}
          enableColumnFilters={enableColumnFilters}
          enableColumnVisibility={enableColumnVisibility}
          enableExport={enableExport}
          enableSearch={enableSearch}
          searchPlaceholder={searchPlaceholder}
          globalFilter={globalFilter}
          onGlobalFilterChange={setGlobalFilter}
          selectedRows={selectedRows}
          toolbarActions={toolbarActions}
          exportFileName={exportFileName}
          exportFormats={exportFormats}
          loading={loading}
        />

        {/* Table or Mobile Cards */}
        <div className="relative">
          {loading && (
            <div className="absolute inset-0 z-10 flex items-center justify-center bg-background/50">
              <RefreshCwIcon className="h-6 w-6 animate-spin" />
            </div>
          )}
          
          {shouldUseMobileView ? (
            // Mobile card view
            <div className={`${isCompact ? 'p-2' : 'p-4'}`}>
              {table.getRowModel().rows?.length ? (
                <div className="space-y-3">
                  {table.getRowModel().rows.map((row) => (
                    <div
                      key={row.id}
                      className={`
                        ${onRowClick || onRowDoubleClick ? 'cursor-pointer' : ''}
                        ${row.getIsSelected() ? 'ring-2 ring-primary ring-offset-2' : ''}
                      `}
                      onClick={(event) => handleRowClick(row, event)}
                    >
                      {/* Selection checkbox for mobile */}
                      {enableRowSelection && (
                        <div className="flex items-center mb-2">
                          <Checkbox
                            checked={row.getIsSelected()}
                            onCheckedChange={(value) => row.toggleSelected(!!value)}
                            aria-label="Select row"
                            className="mr-2"
                          />
                          <span className="text-sm text-muted-foreground">Select this item</span>
                        </div>
                      )}
                      {renderMobileCard(row)}
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-center py-8 text-muted-foreground">
                  {emptyStateMessage}
                </div>
              )}
            </div>
          ) : (
            // Desktop table view
            <div className={enableHorizontalScroll ? 'overflow-x-auto' : ''}>
              <Table className={`${tableClassName} ${compact ? 'text-sm' : ''} ${isMobile ? 'min-w-[600px]' : ''}`}>
            <TableHeader>
              {table.getHeaderGroups().map((headerGroup) => (
                <TableRow key={headerGroup.id}>
                  {headerGroup.headers.map((header) => (
                    <TableHead
                      key={header.id}
                      className={`${
                        header.column.getCanSort() ? 'cursor-pointer select-none' : ''
                      }`}
                      style={{
                        width: header.getSize() !== 150 ? header.getSize() : 'auto',
                      }}
                      onClick={header.column.getToggleSortingHandler()}
                    >
                      <div className="flex items-center space-x-2">
                        {header.isPlaceholder
                          ? null
                          : flexRender(header.column.columnDef.header, header.getContext())}
                        
                        {header.column.getCanSort() && (
                          <div className="flex flex-col">
                            {header.column.getIsSorted() === 'desc' ? (
                              <ChevronDownIcon className="h-3 w-3" />
                            ) : header.column.getIsSorted() === 'asc' ? (
                              <ChevronUpIcon className="h-3 w-3" />
                            ) : (
                              <ChevronsUpDownIcon className="h-3 w-3 text-muted-foreground/50" />
                            )}
                          </div>
                        )}
                        
                        {enableColumnFilters && header.column.getCanFilter() && (
                          <DataTableColumnFilter column={header.column} />
                        )}
                      </div>
                    </TableHead>
                  ))}
                </TableRow>
              ))}
            </TableHeader>
            
            <TableBody>
              {table.getRowModel().rows?.length ? (
                table.getRowModel().rows.map((row) => (
                  <TableRow
                    key={row.id}
                    data-state={row.getIsSelected() && 'selected'}
                    className={`
                      ${striped ? 'even:bg-muted/50' : ''}
                      ${hoverable ? 'hover:bg-muted/50' : ''}
                      ${onRowClick || onRowDoubleClick ? 'cursor-pointer' : ''}
                    `}
                    onClick={(event) => handleRowClick(row, event)}
                  >
                    {row.getVisibleCells().map((cell) => (
                      <TableCell
                        key={cell.id}
                        className={compact ? 'py-2' : ''}
                      >
                        {flexRender(cell.column.columnDef.cell, cell.getContext())}
                      </TableCell>
                    ))}
                  </TableRow>
                ))
              ) : (
                <TableRow>
                  <TableCell
                    colSpan={tableColumns.length}
                    className="h-24 text-center text-muted-foreground"
                  >
                    {emptyStateMessage}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
            </div>
          )}
        </div>

        {/* Pagination */}
        {enablePagination && (
          <DataTablePagination
            table={table}
            pageSizeOptions={pageSizeOptions}
            selectedRowsCount={selectedRows.length}
            totalRows={serverSidePagination ? totalRows : table.getFilteredRowModel().rows.length}
          />
        )}
      </CardContent>
    </Card>
  );
}

export default DataTable;
