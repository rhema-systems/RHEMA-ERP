'use client';

import { Download } from 'lucide-react';

import { Button } from '@/components/ui/button';

export type PayrollGridExportColumn<T> = {
  header: string;
  value: (row: T) => string | number | boolean | null | undefined;
};

type PayrollGridExportButtonProps<T> = {
  rows: T[];
  columns: PayrollGridExportColumn<T>[];
  fileName: string;
  label?: string;
  disabled?: boolean;
};

function csvCell(value: string | number | boolean | null | undefined) {
  const text = String(value ?? '');
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

function exportFileName(fileName: string) {
  return fileName.replace(/[\\/:*?"<>|]+/g, '-');
}

export function PayrollGridExportButton<T>({
  rows,
  columns,
  fileName,
  label = 'Export',
  disabled,
}: PayrollGridExportButtonProps<T>) {
  const exportRows = () => {
    const header = columns.map((column) => csvCell(column.header)).join(',');
    const body = rows.map((row) =>
      columns.map((column) => csvCell(column.value(row))).join(','),
    );
    const csv = [header, ...body].join('\r\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');

    link.href = url;
    link.download = `${exportFileName(fileName)}-${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <Button
      type="button"
      variant="outline"
      size="sm"
      onClick={exportRows}
      disabled={disabled || rows.length === 0}
    >
      <Download className="mr-2 h-4 w-4" />
      {label}
    </Button>
  );
}
