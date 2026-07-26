'use client';

import { useMemo } from 'react';
import { ApprovalWorkbench } from '@/components/approvals/approval-workbench';
import { FINANCE_APPROVAL_QUEUE_IDS, getFinanceApprovalQueueDefinitions } from '@/lib/finance/approval-queue-definitions';

export default function JournalEntryApprovalsPage() {
    const definitions = useMemo(
        () => getFinanceApprovalQueueDefinitions([FINANCE_APPROVAL_QUEUE_IDS.journalEntries]),
        []
    );

    return (
        <ApprovalWorkbench
            title="Journal Approval Queue"
            description="Journal entries awaiting your finance approval."
            definitions={definitions}
            breadcrumbs={[
                { label: 'Dashboard', href: '/dashboard' },
                { label: 'Finance', href: '/finance' },
                { label: 'Journal Entries', href: '/finance/journal-entries' },
                { label: 'Approvals' },
            ]}
        />
    );
}
