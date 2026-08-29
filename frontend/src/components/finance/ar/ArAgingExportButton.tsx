'use client';

import React, { useState } from 'react';
import { Download, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import { arService } from '@/services/ar-service';

interface ArAgingExportButtonProps {
    asOfDate: string;
    isReportLoading: boolean;
}

export function saveArAgingCsv(blob: Blob, asOfDate: string) {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');

    try {
        link.href = url;
        link.download = `ar-aging-${asOfDate}.csv`;
        link.click();
    } finally {
        window.URL.revokeObjectURL(url);
    }
}

export function ArAgingExportButton({
    asOfDate,
    isReportLoading,
}: ArAgingExportButtonProps) {
    const { toast } = useToast();
    const [isExporting, setIsExporting] = useState(false);
    const accessibleLabel = isExporting
        ? 'Exporting AR aging CSV'
        : 'Export AR aging CSV';

    const handleExport = async () => {
        setIsExporting(true);

        try {
            const blob = await arService.downloadAgingReportCsv(asOfDate);
            saveArAgingCsv(blob, asOfDate);
            toast({
                title: 'AR aging export ready',
                description: `Downloaded ar-aging-${asOfDate}.csv.`,
            });
        } catch (error) {
            toast({
                title: 'AR aging export failed',
                description:
                    error instanceof Error
                        ? error.message
                        : 'The AR aging CSV could not be exported.',
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
