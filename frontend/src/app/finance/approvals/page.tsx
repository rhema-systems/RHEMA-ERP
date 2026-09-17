'use client';

import { useMemo } from 'react';
import { ApprovalWorkbench } from '@/components/approvals/approval-workbench';
import { getFinanceApprovalQueueDefinitions } from '@/lib/finance/approval-queue-definitions';

export default function FinanceApprovalsPage() {
    const definitions = useMemo(() => getFinanceApprovalQueueDefinitions(), []);

    return (
        <ApprovalWorkbench
            title="Finance Approval Workbench"
            description="One inbox for finance documents and assigned accounting-book transitions."
            definitions={definitions}
            breadcrumbs={[
                { label: 'Dashboard', href: '/dashboard' },
                { label: 'Finance', href: '/finance' },
                { label: 'Approvals' },
            ]}
        />
    );
}
