'use client';

import { useMemo } from 'react';
import { ApprovalWorkbench } from '@/components/approvals/approval-workbench';
import { getFinanceApprovalQueueDefinitions } from '@/lib/finance/approval-queue-definitions';
import Link from 'next/link';
import { FileCog } from 'lucide-react';
import { Button } from '@/components/ui/button';

export default function FinanceApprovalsPage() {
    const definitions = useMemo(() => getFinanceApprovalQueueDefinitions(), []);

    return (
        <ApprovalWorkbench
            title="Finance Approval Workbench"
            description="One inbox for finance documents and assigned accounting-book approvals."
            definitions={definitions}
            breadcrumbs={[
                { label: 'Dashboard', href: '/dashboard' },
                { label: 'Finance', href: '/finance' },
                { label: 'Approvals' },
            ]}
            headerActions={(
                <Button variant="outline" asChild>
                    <Link href="/finance/reports/layouts">
                        <FileCog className="mr-2 h-4 w-4" />
                        Initialize / manage statement layouts
                    </Link>
                </Button>
            )}
        />
    );
}
