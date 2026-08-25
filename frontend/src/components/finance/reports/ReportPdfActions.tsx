'use client';

import React, { useState } from 'react';
import { Download, Loader2, Printer } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';

interface ReportPdfActionsProps {
    reportName: string;
    onDownloadPdf: () => Promise<void>;
    onPrint: () => Promise<void>;
    disabled?: boolean;
}

/**
 * Standard Finance report PDF actions. Both buttons obtain the same server-rendered artifact;
 * Print opens that PDF in the shared print pipeline and never prints the application shell.
 */
export function ReportPdfActions({ reportName, onDownloadPdf, onPrint, disabled = false }: ReportPdfActionsProps) {
    const [action, setAction] = useState<'pdf' | 'print' | null>(null);

    const run = async (nextAction: 'pdf' | 'print', callback: () => Promise<void>) => {
        setAction(nextAction);
        try {
            await callback();
        } catch (error) {
            console.error(`Failed to ${nextAction === 'print' ? 'print' : 'download'} ${reportName}`, error);
            toast.error(`Unable to ${nextAction === 'print' ? 'print' : 'download'} ${reportName}.`);
        } finally {
            setAction(null);
        }
    };

    return (
        <>
            <Button
                variant="outline"
                onClick={() => run('pdf', onDownloadPdf)}
                disabled={disabled || action !== null}
                aria-label={`Download ${reportName} PDF`}
            >
                {action === 'pdf' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}
                PDF
            </Button>
            <Button
                variant="outline"
                onClick={() => run('print', onPrint)}
                disabled={disabled || action !== null}
                aria-label={`Print ${reportName}`}
            >
                {action === 'print' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Printer className="mr-2 h-4 w-4" />}
                Print
            </Button>
        </>
    );
}
