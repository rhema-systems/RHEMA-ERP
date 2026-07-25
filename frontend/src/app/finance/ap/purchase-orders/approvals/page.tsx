'use client';

import { useMemo } from 'react';
import { ApprovalWorkbench } from '@/components/approvals/approval-workbench';
import { FINANCE_APPROVAL_QUEUE_IDS, getFinanceApprovalQueueDefinitions } from '@/lib/finance/approval-queue-definitions';

export default function FinancePurchaseOrderApprovalsPage() {
    const definitions = useMemo(
        () => getFinanceApprovalQueueDefinitions([FINANCE_APPROVAL_QUEUE_IDS.purchaseOrders]),
        []
    );

    return (
        <ApprovalWorkbench
            title="Finance PO Approval Queue"
            description="Purchase orders submitted for finance approval."
            definitions={definitions}
            breadcrumbs={[
                { label: 'Dashboard', href: '/dashboard' },
                { label: 'Finance', href: '/finance' },
                { label: 'Accounts Payable', href: '/finance/ap' },
                { label: 'Purchase Orders', href: '/finance/ap/purchase-orders' },
                { label: 'Approvals' },
            ]}
        />
    );
}
