"use client";

import React from 'react';
import { Table } from '@tanstack/react-table';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  DownloadIcon,
  FileTextIcon,
  FileSpreadsheetIcon,
  FileJsonIcon,
} from 'lucide-react';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';

export interface DataTableExportProps<TData> {
  table: Table<TData>;
  fileName?: string;
  formats?: ('csv' | 'excel' | 'json')[];
  disabled?: boolean;
}

export function DataTableExport<TData>({
  table,
  fileName = 'export',
  formats = ['csv', 'excel'],
  disabled = false,
}: DataTableExportProps<TData>) {
  
  const exportToCSV = () => {
    const headers = table.getAllColumns()
      .filter(column => column.getCanHide() && column.getIsVisible())
      .map(column => column.columnDef.header as string || column.id);
    
    const rows = table.getFilteredRowModel().rows.map(row => 
      table.getAllColumns()
        .filter(column => column.getCanHide() && column.getIsVisible())
        .map(column => {
          const cellValue = row.getValue(column.id);
          // Convert cell value to string and handle special characters
          if (cellValue === null || cellValue === undefined) return '';
          return String(cellValue).replace(/"/g, '""'); // Escape quotes
        })
    );

    // Create CSV content
    const csvContent = [
      headers.map(header => `"${header}"`).join(','),
      ...rows.map(row => row.map(cell => `"${cell}"`).join(','))
    ].join('\n');

    // Create blob and download
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    saveAs(blob, `${fileName}.csv`);
  };

  const exportToExcel = () => {
    const headers = table.getAllColumns()
      .filter(column => column.getCanHide() && column.getIsVisible())
      .map(column => column.columnDef.header as string || column.id);
    
    const rows = table.getFilteredRowModel().rows.map(row => 
      table.getAllColumns()
        .filter(column => column.getCanHide() && column.getIsVisible())
        .reduce((acc, column) => {
          const cellValue = row.getValue(column.id);
          acc[column.columnDef.header as string || column.id] = cellValue;
          return acc;
        }, {} as Record<string, any>)
    );

    // Create workbook
    const ws = XLSX.utils.json_to_sheet(rows);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Data');

    // Auto-adjust column widths
    const colWidths = headers.map(header => ({ wch: Math.max(header.length, 10) }));
    ws['!cols'] = colWidths;

    // Save file
    XLSX.writeFile(wb, `${fileName}.xlsx`);
  };

  const exportToJSON = () => {
    const rows = table.getFilteredRowModel().rows.map(row => 
      table.getAllColumns()
        .filter(column => column.getCanHide() && column.getIsVisible())
        .reduce((acc, column) => {
          const cellValue = row.getValue(column.id);
          acc[column.columnDef.header as string || column.id] = cellValue;
          return acc;
        }, {} as Record<string, any>)
    );

    const jsonContent = JSON.stringify(rows, null, 2);
    const blob = new Blob([jsonContent], { type: 'application/json;charset=utf-8;' });
    saveAs(blob, `${fileName}.json`);
  };

  const handleExport = (format: 'csv' | 'excel' | 'json') => {
    switch (format) {
      case 'csv':
        exportToCSV();
        break;
      case 'excel':
        exportToExcel();
        break;
      case 'json':
        exportToJSON();
        break;
    }
  };

  const getFormatIcon = (format: 'csv' | 'excel' | 'json') => {
    switch (format) {
      case 'csv':
        return FileTextIcon;
      case 'excel':
        return FileSpreadsheetIcon;
      case 'json':
        return FileJsonIcon;
    }
  };

  const getFormatLabel = (format: 'csv' | 'excel' | 'json') => {
    switch (format) {
      case 'csv':
        return 'Export as CSV';
      case 'excel':
        return 'Export as Excel';
      case 'json':
        return 'Export as JSON';
    }
  };

  const visibleRowCount = table.getFilteredRowModel().rows.length;
  const selectedRowCount = table.getSelectedRowModel().rows.length;

  if (formats.length === 1) {
    // Single format - render as simple button
    const format = formats[0];
    const IconComponent = getFormatIcon(format);
    
    return (
      <Button
        variant="outline"
        size="sm"
        onClick={() => handleExport(format)}
        disabled={disabled || visibleRowCount === 0}
        title={getFormatLabel(format)}
      >
        <IconComponent className="mr-1 h-3 w-3" />
        Export
        {selectedRowCount > 0 && ` (${selectedRowCount})`}
      </Button>
    );
  }

  // Multiple formats - render as dropdown
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          disabled={disabled || visibleRowCount === 0}
        >
          <DownloadIcon className="mr-1 h-3 w-3" />
          Export
          {selectedRowCount > 0 && ` (${selectedRowCount})`}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel>Export Options</DropdownMenuLabel>
        <DropdownMenuSeparator />
        
        {formats.map((format) => {
          const IconComponent = getFormatIcon(format);
          return (
            <DropdownMenuItem
              key={format}
              onClick={() => handleExport(format)}
              className="cursor-pointer"
            >
              <IconComponent className="mr-2 h-4 w-4" />
              {getFormatLabel(format)}
            </DropdownMenuItem>
          );
        })}
        
        <DropdownMenuSeparator />
        <div className="px-2 py-1 text-xs text-muted-foreground">
          {selectedRowCount > 0 
            ? `Exporting ${selectedRowCount} selected rows`
            : `Exporting ${visibleRowCount} rows`
          }
        </div>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export default DataTableExport;