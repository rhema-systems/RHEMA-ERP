'use client';

import React, { useState } from 'react';
import { Download, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import { accountsPayableService } from '@/services/accountsPayableService';

interface ApAgingExportButtonProps {
  asOfDate: string;
  isReportLoading: boolean;
}

export function saveApAgingCsv(blob: Blob, asOfDate: string) {
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement('a');

  try {
    link.href = url;
    link.download = `ap-aging-${asOfDate}.csv`;
    link.click();
  } finally {
    window.URL.revokeObjectURL(url);
  }
}

export function ApAgingExportButton({
  asOfDate,
  isReportLoading,
}: ApAgingExportButtonProps) {
  const { toast } = useToast();
  const [isExporting, setIsExporting] = useState(false);
  const accessibleLabel = isExporting
    ? 'Exporting AP aging CSV'
    : 'Export AP aging CSV';

  const handleExport = async () => {
    setIsExporting(true);

    try {
      const blob =
        await accountsPayableService.downloadAgingReportCsv(asOfDate);
      saveApAgingCsv(blob, asOfDate);
      toast({
        title: 'AP aging export ready',
        description: `Downloaded ap-aging-${asOfDate}.csv.`,
      });
    } catch (error) {
      toast({
        title: 'AP aging export failed',
        description:
          error instanceof Error
            ? error.message
            : 'The AP aging CSV could not be exported.',
        variant: 'destructive',
      });
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <Button
      variant="outline"
      onClick={() => void handleExport()}
      disabled={isReportLoading || isExporting}
      aria-label={accessibleLabel}
      title={accessibleLabel}
    >
      {isExporting ? (
        <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />
      ) : (
        <Download className="mr-2 h-4 w-4" aria-hidden="true" />
      )}
      {isExporting ? 'Exporting...' : 'Export CSV'}
    </Button>
  );
}
