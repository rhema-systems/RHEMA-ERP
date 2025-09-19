'use client';

import React, { useState } from 'react';
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
import { Search, MoreHorizontal, Plus, Trash2, Edit, Eye } from 'lucide-react';

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
}: DataTableProps<T>) {
  const [searchTerm, setSearchTerm] = useState('');
  const [sortColumn, setSortColumn] = useState<string | null>(null);
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');

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

  const handleSort = (columnKey: string) => {
    if (sortColumn === columnKey) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortColumn(columnKey);
      setSortDirection('asc');
    }
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
        <Badge variant={value ? 'default' : 'secondary'}>
          {value ? 'Yes' : 'No'}
        </Badge>
      );
    }

    if (value instanceof Date) {
      return (value as Date).toLocaleDateString();
    }

    if (Array.isArray(value)) {
      return (
        <div className="flex flex-wrap gap-1">
          {(value as any[]).map((item, index) => (
            <Badge key={index} variant="outline" className="text-xs">
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
          {onAdd && (
            <Button onClick={onAdd} className="flex items-center gap-2">
              <Plus className="h-4 w-4" />
              Add New
            </Button>
          )}
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
      <CardContent>
        {loading ? (
          <div className="flex items-center justify-center h-32">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-primary"></div>
          </div>
        ) : (
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  {columns.map((column) => (
                    <TableHead
                      key={String(column.key)}
                      className={column.sortable ? 'cursor-pointer hover:bg-muted/50' : ''}
                      onClick={column.sortable ? () => handleSort(String(column.key)) : undefined}
                    >
                      <div className="flex items-center gap-2">
                        {column.label}
                        {column.sortable && sortColumn === column.key && (
                          <span className="text-xs">
                            {sortDirection === 'asc' ? '↑' : '↓'}
                          </span>
                        )}
                      </div>
                    </TableHead>
                  ))}
                  {actions && <TableHead className="w-[100px]">Actions</TableHead>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {sortedData.length === 0 ? (
                  <TableRow>
                    <TableCell
                      colSpan={columns.length + (actions ? 1 : 0)}
                      className="h-24 text-center text-muted-foreground"
                    >
                      No data found.
                    </TableCell>
                  </TableRow>
                ) : (
                  sortedData.map((row, index) => (
                    <TableRow key={index}>
                      {columns.map((column) => (
                        <TableCell key={String(column.key)}>
                          {renderCellValue(column, row)}
                        </TableCell>
                      ))}
                      {actions && (
                        <TableCell>
                          {customActions ? (
                            customActions(row)
                          ) : (
                            <DropdownMenu>
                              <DropdownMenuTrigger asChild>
                                <Button variant="ghost" className="h-8 w-8 p-0">
                                  <MoreHorizontal className="h-4 w-4" />
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
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}