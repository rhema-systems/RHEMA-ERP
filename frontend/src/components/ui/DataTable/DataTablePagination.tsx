"use client";

import React from 'react';
import { Table } from '@tanstack/react-table';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  ChevronsLeftIcon,
  ChevronsRightIcon,
} from 'lucide-react';

export interface DataTablePaginationProps<TData> {
  table: Table<TData>;
  pageSizeOptions?: number[];
  selectedRowsCount?: number;
  totalRows?: number;
}

export function DataTablePagination<TData>({
  table,
  pageSizeOptions = [5, 10, 20, 50, 100],
  selectedRowsCount = 0,
  totalRows,
}: DataTablePaginationProps<TData>) {
  const pageCount = table.getPageCount();
  const currentPage = table.getState().pagination.pageIndex + 1;
  const pageSize = table.getState().pagination.pageSize;
  const canPreviousPage = table.getCanPreviousPage();
  const canNextPage = table.getCanNextPage();

  // Calculate display information
  const startRow = table.getState().pagination.pageIndex * pageSize + 1;
  const endRow = Math.min(startRow + pageSize - 1, totalRows || table.getFilteredRowModel().rows.length);
  const totalRowsCount = totalRows || table.getFilteredRowModel().rows.length;

  return (
    <div className="flex flex-col gap-4 px-4 py-3 border-t md:flex-row md:items-center md:justify-between">
      {/* Selection info and page size selector */}
      <div className="flex flex-col gap-4 text-sm text-muted-foreground md:flex-row md:items-center md:gap-8">
        {/* Selection count */}
        <div className="flex items-center gap-2">
          {selectedRowsCount > 0 && (
            <span>
              {selectedRowsCount} of {table.getFilteredRowModel().rows.length} row(s) selected
            </span>
          )}
        </div>

        {/* Page size selector */}
        <div className="flex items-center gap-2">
          <span className="whitespace-nowrap">Rows per page:</span>
          <Select
            value={pageSize.toString()}
            onValueChange={(value) => table.setPageSize(Number(value))}
          >
            <SelectTrigger className="w-16 h-8">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {pageSizeOptions.map((size) => (
                <SelectItem key={size} value={size.toString()}>
                  {size}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Row range info */}
        <div className="whitespace-nowrap">
          Showing {startRow} to {endRow} of {totalRowsCount.toLocaleString()} entries
        </div>
      </div>

      {/* Pagination controls */}
      <div className="flex flex-col gap-4 md:flex-row md:items-center md:gap-2">
        {/* Page info */}
        <div className="text-sm text-muted-foreground whitespace-nowrap">
          Page {currentPage} of {pageCount}
        </div>

        {/* Navigation buttons */}
        <div className="flex items-center gap-1">
          {/* First page */}
          <Button
            variant="outline"
            size="sm"
            onClick={() => table.setPageIndex(0)}
            disabled={!canPreviousPage}
            className="h-8 w-8 p-0"
            title="Go to first page"
          >
            <ChevronsLeftIcon className="h-3 w-3" />
          </Button>

          {/* Previous page */}
          <Button
            variant="outline"
            size="sm"
            onClick={() => table.previousPage()}
            disabled={!canPreviousPage}
            className="h-8 w-8 p-0"
            title="Go to previous page"
          >
            <ChevronLeftIcon className="h-3 w-3" />
          </Button>

          {/* Page numbers (show current page and surrounding pages) */}
          {renderPageNumbers(currentPage, pageCount, table.setPageIndex)}

          {/* Next page */}
          <Button
            variant="outline"
            size="sm"
            onClick={() => table.nextPage()}
            disabled={!canNextPage}
            className="h-8 w-8 p-0"
            title="Go to next page"
          >
            <ChevronRightIcon className="h-3 w-3" />
          </Button>

          {/* Last page */}
          <Button
            variant="outline"
            size="sm"
            onClick={() => table.setPageIndex(pageCount - 1)}
            disabled={!canNextPage}
            className="h-8 w-8 p-0"
            title="Go to last page"
          >
            <ChevronsRightIcon className="h-3 w-3" />
          </Button>
        </div>
      </div>
    </div>
  );
}

// Helper function to render page numbers
function renderPageNumbers(
  currentPage: number,
  pageCount: number,
  setPageIndex: (index: number) => void
) {
  if (pageCount <= 1) return null;

  const pages: (number | string)[] = [];
  
  // Always show first page
  if (currentPage > 3) {
    pages.push(1);
    if (currentPage > 4) {
      pages.push('...');
    }
  }
  
  // Show pages around current page
  for (let i = Math.max(1, currentPage - 2); i <= Math.min(pageCount, currentPage + 2); i++) {
    pages.push(i);
  }
  
  // Always show last page
  if (currentPage < pageCount - 2) {
    if (currentPage < pageCount - 3) {
      pages.push('...');
    }
    pages.push(pageCount);
  }

  return (
    <>
      {pages.map((page, index) => (
        <React.Fragment key={index}>
          {typeof page === 'number' ? (
            <Button
              variant={page === currentPage ? 'default' : 'outline'}
              size="sm"
              onClick={() => setPageIndex(page - 1)}
              className="h-8 w-8 p-0"
            >
              {page}
            </Button>
          ) : (
            <span className="flex h-8 w-8 items-center justify-center text-sm text-muted-foreground">
              {page}
            </span>
          )}
        </React.Fragment>
      ))}
    </>
  );
}

export default DataTablePagination;