/* eslint-disable @typescript-eslint/no-non-null-assertion */
"use client";

import React from 'react';
import { Table } from '@tanstack/react-table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuTrigger,
  DropdownMenuSeparator,
  DropdownMenuLabel,
} from '@/components/ui/dropdown-menu';
import {
  SearchIcon,
  PlusIcon,
  RefreshCwIcon,
  Trash2Icon,
  DownloadIcon,
  EyeIcon,
  FilterIcon,
  XIcon,
} from 'lucide-react';
import { DataTableExport } from './DataTableExport';
import { useDeviceType } from '@/hooks/useResponsive';

export interface DataTableToolbarProps<TData> {
  table: Table<TData>;
  compact?: boolean;
  enableGlobalFilter?: boolean;
  enableColumnFilters?: boolean;
  enableColumnVisibility?: boolean;
  enableExport?: boolean;
  enableSearch?: boolean;
  searchPlaceholder?: string;
  globalFilter?: string;
  onGlobalFilterChange?: (value: string) => void;
  selectedRows: TData[];
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
  exportFileName?: string;
  exportFormats?: ('csv' | 'excel' | 'json')[];
  loading?: boolean;
}

export function DataTableToolbar<TData>({
  table,
  compact = false,
  enableGlobalFilter = true,
  enableColumnFilters = true,
  enableColumnVisibility = true,
  enableExport = true,
  enableSearch = true,
  searchPlaceholder = 'Search all columns...',
  globalFilter = '',
  onGlobalFilterChange,
  selectedRows,
  toolbarActions,
  exportFileName = 'export',
  exportFormats = ['csv', 'excel'],
  loading = false,
}: DataTableToolbarProps<TData>) {
  const { isMobile, isTablet } = useDeviceType();
  const isFiltered = table.getState().columnFilters.length > 0 || globalFilter.length > 0;
  const hasSelection = selectedRows.length > 0;

  const handleGlobalFilterChange = (value: string) => {
    onGlobalFilterChange?.(value);
    if (!onGlobalFilterChange) {
      table.setGlobalFilter(value);
    }
  };

  return (
    <div className={compact ? 'flex flex-col space-y-3 p-3' : 'flex flex-col space-y-4 p-4'}>
      {/* Top row - Main actions */}
      <div className={`${isMobile ? 'flex-col space-y-3' : 'flex items-center justify-between'}`}>
        <div className={`${isMobile ? 'flex flex-col space-y-2' : 'flex items-center space-x-2'}`}>
          {/* Search */}
          {enableSearch && enableGlobalFilter && (
            <div className="relative">
              <SearchIcon className="absolute left-2 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder={searchPlaceholder}
                value={globalFilter}
                onChange={(event) => handleGlobalFilterChange(event.target.value)}
                className={`${isMobile ? 'w-full' : compact ? 'w-56' : 'w-64'} pl-8`}
                disabled={loading}
              />
              {globalFilter && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="absolute right-1 top-1/2 h-6 w-6 -translate-y-1/2 p-0 hover:bg-transparent"
                  onClick={() => handleGlobalFilterChange('')}
                >
                  <XIcon className="h-3 w-3" />
                </Button>
              )}
            </div>
          )}

          {/* Column filters indicator */}
          {enableColumnFilters && table.getState().columnFilters.length > 0 && (
            <Badge variant="secondary" className="px-2 py-1">
              <FilterIcon className="mr-1 h-3 w-3" />
              {table.getState().columnFilters.length} filter(s) active
            </Badge>
          )}

          {/* Clear all filters */}
          {isFiltered && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                table.resetColumnFilters();
                handleGlobalFilterChange('');
              }}
              disabled={loading}
            >
              <XIcon className="mr-1 h-3 w-3" />
              Clear filters
            </Button>
          )}
        </div>

        <div className={`${isMobile ? 'flex flex-wrap gap-2' : 'flex items-center space-x-2'}`}>
          {/* Column visibility */}
          {enableColumnVisibility && !isMobile && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" size={compact ? 'sm' : 'default'} disabled={loading}>
                  <EyeIcon className="mr-1 h-3 w-3" />
                  {isMobile ? '' : 'View'}
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-48">
                <DropdownMenuLabel>Toggle columns</DropdownMenuLabel>
                <DropdownMenuSeparator />
                {table
                  .getAllColumns()
                  .filter((column) => column.getCanHide())
                  .map((column) => {
                    return (
                      <DropdownMenuCheckboxItem
                        key={column.id}
                        className="capitalize"
                        checked={column.getIsVisible()}
                        onCheckedChange={(value) => column.toggleVisibility(!!value)}
                      >
                        {column.columnDef.header as string || column.id}
                      </DropdownMenuCheckboxItem>
                    );
                  })}
              </DropdownMenuContent>
            </DropdownMenu>
          )}

          {/* Export */}
          {enableExport && (
            <DataTableExport
              table={table}
              fileName={exportFileName}
              formats={exportFormats}
              disabled={loading}
            />
          )}

          {/* Refresh */}
          {toolbarActions?.refresh && (
            <Button
              variant="outline"
              size="sm"
              onClick={toolbarActions.refresh}
              disabled={loading}
            >
              <RefreshCwIcon className={`mr-1 h-3 w-3 ${loading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
          )}

          {/* Create */}
          {toolbarActions?.create && (
            <Button
              size="sm"
              onClick={toolbarActions.create}
              disabled={loading}
            >
              <PlusIcon className="mr-1 h-3 w-3" />
              Create
            </Button>
          )}
        </div>
      </div>

      {/* Bottom row - Selection actions */}
      {hasSelection && (
        <div className="flex items-center justify-between rounded-lg border bg-muted/50 p-3">
          <div className="flex items-center space-x-2">
            <Badge variant="default" className="px-2 py-1">
              {selectedRows.length} row(s) selected
            </Badge>
          </div>

          <div className="flex items-center space-x-2">
            {/* Custom actions */}
            {toolbarActions?.customActions?.map((action) => {
              const IconComponent = action.icon || React.Fragment;
              const isDisabled = action.requiresSelection && selectedRows.length === 0;

              return (
                <Button
                  key={action.id}
                  variant={action.variant || 'outline'}
                  size="sm"
                  disabled={isDisabled || loading}
                  onClick={() => action.onClick(selectedRows)}
                >
                  {action.icon && <IconComponent className="mr-1 h-3 w-3" />}
                  {action.label}
                </Button>
              );
            })}

            {/* Delete selected */}
            {toolbarActions?.delete && (
              <Button
                variant="destructive"
                size="sm"
                onClick={() => toolbarActions.delete?.(selectedRows)}
                disabled={loading}
              >
                <Trash2Icon className="mr-1 h-3 w-3" />
                Delete ({selectedRows.length})
              </Button>
            )}

            {/* Clear selection */}
            <Button
              variant="ghost"
              size="sm"
              onClick={() => table.resetRowSelection()}
              disabled={loading}
            >
              <XIcon className="mr-1 h-3 w-3" />
              Clear selection
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

export default DataTableToolbar;
