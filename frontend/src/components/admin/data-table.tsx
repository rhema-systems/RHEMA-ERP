'use client';

import React, { useEffect, useMemo, useState } from 'react';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../ui/table';
import { Button } from '../ui/button';
import { Input } from '../ui/input';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '../ui/dropdown-menu';
import { Badge } from '../ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { Search, MoreHorizontal, Plus, Trash2, Edit, Eye, Download, ChevronLeft, ChevronRight } from 'lucide-react';

export interface Column<T> {
  key: keyof T | string;
  label: string;
  sortable?: boolean;
  render?: (value: any, row: T) => React.ReactNode;
}

export interface DataTableProps<T> {
  title: string;
  description?: string;
  data: T[];
  columns: Column<T>[];
  loading?: boolean;
  searchable?: boolean;
  searchPlaceholder?: string;
  onAdd?: () => void;
  onEdit?: (row: T) => void;
  onDelete?: (row: T) => void;
  onView?: (row: T) => void;
  actions?: boolean;
  customActions?: (row: T) => React.ReactNode;
  exportable?: boolean;
  exportFileName?: string;
  selectable?: boolean;
  onSelectionChange?: (selectedRows: T[]) => void;
  pageSize?: number;
  getRowId?: (row: T) => string;
  emptyMessage?: string;
}

export function DataTable<T extends Record<string, any>>({
  title,
  description,
  data,
  columns,
  loading = false,
  searchable = true,
  searchPlaceholder = "Search...",
  onAdd,
  onEdit,
  onDelete,
  onView,
  actions = true,
  customActions,
  exportable = true,
  exportFileName,
  selectable = false,
  onSelectionChange,
  pageSize,
  getRowId,
  emptyMessage = 'No data found.',
}: DataTableProps<T>) {
  const [searchTerm, setSearchTerm] = useState('');
  const [sortColumn, setSortColumn] = useState<string | null>(null);
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [selectedRows, setSelectedRows] = useState<Set<string>>(new Set());
  const [currentPage, setCurrentPage] = useState(1);

  // Filter data based on search term
  const filteredData = data.filter((row) =>
    Object.values(row).some((value) =>
      String(value).toLowerCase().includes(searchTerm.toLowerCase())
    )
  );

  // Sort data
  const sortedData = [...filteredData].sort((a, b) => {
    if (!sortColumn) return 0;
    
    const aValue = a[sortColumn];
    const bValue = b[sortColumn];
    
    if (aValue < bValue) return sortDirection === 'asc' ? -1 : 1;
    if (aValue > bValue) return sortDirection === 'asc' ? 1 : -1;
    return 0;
  });

  const effectivePageSize = pageSize && pageSize > 0 ? pageSize : sortedData.length || 1;
  const totalPages = Math.max(1, Math.ceil(sortedData.length / effectivePageSize));
  const pagedData = pageSize
    ? sortedData.slice((currentPage - 1) * effectivePageSize, currentPage * effectivePageSize)
    : sortedData;

  useEffect(() => {
    setCurrentPage(1);
  }, [searchTerm, data, pageSize]);

  useEffect(() => {
    if (currentPage > totalPages) setCurrentPage(totalPages);
  }, [currentPage, totalPages]);

  const rowKey = useMemo(
    () => (row: T, index: number) => getRowId?.(row) ?? String(row.id ?? index),
    [getRowId]
  );

  const handleSort = (columnKey: string) => {
    if (sortColumn === columnKey) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortColumn(columnKey);
      setSortDirection('asc');
    }
  };

  // Selection handlers
  const handleRowSelect = (key: string, checked: boolean) => {
    const newSelectedRows = new Set(selectedRows);
    if (checked) {
      newSelectedRows.add(key);
    } else {
      newSelectedRows.delete(key);
    }
    setSelectedRows(newSelectedRows);
    
    // Call selection change callback
    const selectedData = sortedData.filter((row, index) => newSelectedRows.has(rowKey(row, index)));
    onSelectionChange?.(selectedData);
  };

  const handleExport = () => {
    // Convert data to CSV format
    const headers = columns.map(col => col.label).join(',');
    const csvData = sortedData.map(row => 
      columns.map(col => {
        const keyString = String(col.key);
        const value = keyString.includes('.') 
          ? keyString.split('.').reduce((obj, key) => obj?.[key], row)
          : row[col.key as keyof T];
        
        // Handle different data types for CSV export
        if (Array.isArray(value)) {
          return `"${(value as any[]).join('; ')}"`;
        }
        if (value instanceof Date) {
          return `"${(value as Date).toLocaleDateString()}"`;
        }
        if (typeof value === 'boolean') {
          return value ? 'Yes' : 'No';
        }
        // Escape quotes and wrap in quotes if contains comma
        const stringValue = String(value || '');
        return stringValue.includes(',') || stringValue.includes('"') 
          ? `"${stringValue.replace(/"/g, '""')}"` 
          : stringValue;
      }).join(',')
    ).join('\n');
    
    const csv = headers + '\n' + csvData;
    
    // Create and download the file
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);
    link.setAttribute('href', url);
    link.setAttribute('download', exportFileName || `${title.toLowerCase().replace(/\s+/g, '_')}_export.csv`);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const renderCellValue = (column: Column<T>, row: T) => {
    const keyString = String(column.key);
    const value = keyString.includes('.') 
      ? keyString.split('.').reduce((obj, key) => obj?.[key], row)
      : row[column.key as keyof T];

    if (column.render) {
      return column.render(value, row);
    }

    // Handle common data types
    if (typeof value === 'boolean') {
      return (
        <Badge variant={value ? 'default' : 'secondary'} className="text-xs px-2 py-0 h-5">
          {value ? 'Yes' : 'No'}
        </Badge>
      );
    }

    if (value instanceof Date) {
      return (value as Date).toLocaleDateString();
    }

    if (Array.isArray(value)) {
      return (
        <div className="flex flex-wrap gap-0.5">
          {(value as any[]).map((item, index) => (
            <Badge key={index} variant="outline" className="text-xs px-1.5 py-0 h-4 leading-none">
              {String(item)}
            </Badge>
          ))}
        </div>
      );
    }

    return String(value || '');
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-xl font-semibold">{title}</CardTitle>
            {description && (
              <p className="text-sm text-muted-foreground mt-1">{description}</p>
            )}
          </div>
          <div className="flex items-center gap-2">
            {exportable && sortedData.length > 0 && (
              <Button variant="outline" onClick={handleExport} className="flex items-center gap-2">
                <Download className="h-4 w-4" />
                Export CSV
              </Button>
            )}
            {onAdd && (
              <Button onClick={onAdd} className="flex items-center gap-2">
                <Plus className="h-4 w-4" />
                Add New
              </Button>
            )}
          </div>
        </div>
        {searchable && (
          <div className="flex items-center gap-4">
            <div className="relative flex-1 max-w-sm">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-muted-foreground h-4 w-4" />
              <Input
                placeholder={searchPlaceholder}
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="pl-10"
              />
            </div>
          </div>
        )}
      </CardHeader>
      <CardContent className="p-4">
        {loading ? (
          <div className="flex items-center justify-center h-24">
            <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-primary"></div>
          </div>
        ) : (
          <div className="rounded-md overflow-hidden border border-border">
            <Table>
              <TableHeader>
                <TableRow className="h-8 bg-slate-100/80 border-b border-border/60">
                  {columns.map((column) => (
                    <TableHead
                      key={String(column.key)}
                      className={`py-1.5 font-semibold border-r border-border/40 last:border-r-0 ${column.sortable ? 'cursor-pointer hover:bg-muted/70' : ''}`}
                      onClick={column.sortable ? () => handleSort(String(column.key)) : undefined}
                    >
                      <div className="flex items-center gap-1">
                        <span className="text-xs font-medium">{column.label}</span>
                        {column.sortable && sortColumn === column.key && (
                          <span className="text-xs">
                            {sortDirection === 'asc' ? '↑' : '↓'}
                          </span>
                        )}
                      </div>
                    </TableHead>
                  ))}
                  {actions && <TableHead className="w-[84px] py-1.5 text-xs font-semibold border-r-0">Actions</TableHead>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {sortedData.length === 0 ? (
                  <TableRow className="border-b border-border/40">
                    <TableCell
                      colSpan={columns.length + (actions ? 1 : 0)}
                      className="h-14 text-center text-muted-foreground text-sm py-3"
                    >
                      {emptyMessage}
                    </TableCell>
                  </TableRow>
                ) : (
                  pagedData.map((row, index) => {
                    const absoluteIndex = pageSize ? (currentPage - 1) * effectivePageSize + index : index;
                    const key = rowKey(row, absoluteIndex);
                    return (
                    <TableRow 
                      key={key}
                      className={`h-9 transition-colors hover:bg-blue-50 dark:hover:bg-blue-900/20 cursor-pointer ${
                        selectedRows.has(key)
                          ? 'bg-blue-100 dark:bg-blue-900/30' 
                          : index % 2 === 0
                            ? 'bg-white dark:bg-slate-950'
                            : 'bg-slate-50/80 dark:bg-slate-900/35'
                      } border-b border-border/40`}
                      onClick={() => selectable && handleRowSelect(key, !selectedRows.has(key))}
                    >
                      {columns.map((column) => (
                        <TableCell key={String(column.key)} className="py-1.5 text-[13px] border-r border-border/30 last:border-r-0">
                          {renderCellValue(column, row)}
                        </TableCell>
                      ))}
                      {actions && (
                        <TableCell className="py-1.5 border-r-0" onClick={(e) => e.stopPropagation()}>
                          {customActions ? (
                            customActions(row)
                          ) : (
                            <DropdownMenu>
                              <DropdownMenuTrigger asChild>
                                <Button variant="ghost" className="h-7 w-7 p-0">
                                  <MoreHorizontal className="h-3.5 w-3.5" />
                                </Button>
                              </DropdownMenuTrigger>
                              <DropdownMenuContent align="end">
                                {onView && (
                                  <DropdownMenuItem onClick={() => onView(row)}>
                                    <Eye className="mr-2 h-4 w-4" />
                                    View
                                  </DropdownMenuItem>
                                )}
                                {onEdit && (
                                  <DropdownMenuItem onClick={() => onEdit(row)}>
                                    <Edit className="mr-2 h-4 w-4" />
                                    Edit
                                  </DropdownMenuItem>
                                )}
                                {onDelete && (
                                  <DropdownMenuItem
                                    onClick={() => onDelete(row)}
                                    className="text-destructive"
                                  >
                                    <Trash2 className="mr-2 h-4 w-4" />
                                    Delete
                                  </DropdownMenuItem>
                                )}
                              </DropdownMenuContent>
                            </DropdownMenu>
                          )}
                        </TableCell>
                      )}
                    </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
        )}
        {!loading && pageSize && sortedData.length > 0 && (
          <div className="mt-4 flex flex-wrap items-center justify-between gap-3 text-sm">
            <span className="text-muted-foreground">
              Showing {(currentPage - 1) * effectivePageSize + 1}–{Math.min(currentPage * effectivePageSize, sortedData.length)} of {sortedData.length}
            </span>
            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
                disabled={currentPage === 1}
              >
                <ChevronLeft className="mr-1 h-4 w-4" /> Previous
              </Button>
              <span className="min-w-20 text-center text-muted-foreground">
                Page {currentPage} of {totalPages}
              </span>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
                disabled={currentPage === totalPages}
              >
                Next <ChevronRight className="ml-1 h-4 w-4" />
              </Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
