import { ClipboardCheck, FileText, ShoppingCart } from 'lucide-react';
import type { ApprovalQueueDefinition, ApprovalQueueItem } from '@/components/approvals/approval-workbench';
import apiService from '@/services/api.service';

export const FINANCE_APPROVAL_QUEUE_IDS = {
    financeWorkflows: 'finance-workflows',
    journalEntries: 'journal-entries',
    purchaseOrders: 'finance-purchase-orders',
} as const;

interface FinanceWorkflowApprovalQueueItem {
    approvalId: string;
    entityId: string;
    entityType: string;
    reference: string;
    title: string;
    detailHref: string;
    documentType: string;
    module: string;
    currentStep: string;
    statusLabel: string;
    submittedAt?: string | null;
    amount?: number | null;
    currencyCode?: string | null;
    submittedBy?: string | null;
    approverRole?: string | null;
    workflowName?: string | null;
    canApprove: boolean;
    canReject: boolean;
    approveDisabledReason?: string | null;
    rejectDisabledReason?: string | null;
    metadata?: Record<string, string>;
}

function normalizeEntityType(entityType?: string | null): string {
    return (entityType || '').replace(/[^a-zA-Z0-9]/g, '').toUpperCase();
}

function toMetadata(row: FinanceWorkflowApprovalQueueItem): ApprovalQueueItem['metadata'] {
    const metadata: ApprovalQueueItem['metadata'] = [
        { label: 'Step', value: row.currentStep || 'Approval' },
        { label: 'Workflow', value: row.workflowName || 'Finance workflow' },
    ];

    if (row.approverRole) {
        metadata.push({ label: 'Role', value: row.approverRole });
    }

    for (const [label, value] of Object.entries(row.metadata || {})) {
        if (value) {
            metadata.push({ label, value });
        }
    }

    return metadata;
}

function toWorkflowApprovalItem(row: FinanceWorkflowApprovalQueueItem): ApprovalQueueItem {
    const amount = row.amount === null || row.amount === undefined ? null : Number(row.amount);
    const reference = row.reference || row.entityId;

    return {
        id: row.approvalId,
        reference,
        title: row.title || reference,
        detailHref: row.detailHref || '#',
        documentType: row.documentType || row.entityType,
        module: row.module || 'Finance',
        statusLabel: row.statusLabel || 'Pending Approval',
        date: row.submittedAt,
        amount: Number.isFinite(amount) ? amount : null,
        currencyCode: row.currencyCode || 'GHS',
        submittedBy: row.submittedBy,
        canApprove: row.canApprove,
        canReject: row.canReject,
        approveDisabledReason: row.approveDisabledReason,
        rejectDisabledReason: row.rejectDisabledReason,
        metadata: toMetadata(row),
    };
}

async function loadFinanceWorkflowApprovals(
    predicate?: (item: FinanceWorkflowApprovalQueueItem) => boolean
): Promise<ApprovalQueueItem[]> {
    const approvals = await apiService.get<FinanceWorkflowApprovalQueueItem[]>('/finance/approvals/pending');
    return (approvals || [])
        .filter(item => (predicate ? predicate(item) : true))
        .map(toWorkflowApprovalItem);
}

async function approveFinanceWorkflowApproval(item: ApprovalQueueItem, comments?: string): Promise<void> {
    await apiService.post(`/finance/approvals/${item.id}/approve`, {
        comments: comments || 'Approved from finance approval queue',
    });
}

async function rejectFinanceWorkflowApproval(item: ApprovalQueueItem, reason: string): Promise<void> {
    await apiService.post(`/finance/approvals/${item.id}/reject`, {
        reason,
    });
}

const allFinanceWorkflowDefinition: ApprovalQueueDefinition = {
    id: FINANCE_APPROVAL_QUEUE_IDS.financeWorkflows,
    title: 'Finance Workflow Approvals',
    documentLabel: 'Finance Documents',
    description: 'Finance transactions and documents awaiting your current workflow step.',
    emptyMessage: 'No finance documents are awaiting your approval.',
    accessDeniedMessage: 'You need a finance approver role to review finance workflow approvals.',
    icon: ClipboardCheck,
    accentClassName: 'border-sky-100 bg-sky-50/50',
    load: () => loadFinanceWorkflowApprovals(),
    approve: approveFinanceWorkflowApproval,
    reject: rejectFinanceWorkflowApproval,
};

const definitions: ApprovalQueueDefinition[] = [
    allFinanceWorkflowDefinition,
    {
        id: FINANCE_APPROVAL_QUEUE_IDS.journalEntries,
        title: 'Journal Entry Approvals',
        documentLabel: 'Journal Entries',
        description: 'General Ledger journals awaiting your current workflow step.',
        emptyMessage: 'No journal entries are awaiting your approval.',
        accessDeniedMessage: 'You need Finance.JournalEntries.Approve permission to review journal approvals.',
        icon: FileText,
        accentClassName: 'border-orange-100 bg-orange-50/50',
        load: () => loadFinanceWorkflowApprovals(item => normalizeEntityType(item.entityType) === 'JOURNALENTRY'),
        approve: approveFinanceWorkflowApproval,
        reject: rejectFinanceWorkflowApproval,
    },
    {
        id: FINANCE_APPROVAL_QUEUE_IDS.purchaseOrders,
        title: 'Finance PO Approvals',
        documentLabel: 'Purchase Orders',
        description: 'Finance purchase orders awaiting your current AP workflow step.',
        emptyMessage: 'No finance purchase orders are awaiting your approval.',
        accessDeniedMessage: 'You need a finance approver role to review Finance PO approvals.',
        icon: ShoppingCart,
        accentClassName: 'border-emerald-100 bg-emerald-50/50',
        load: () => loadFinanceWorkflowApprovals(item => normalizeEntityType(item.entityType) === 'FINANCEPURCHASEORDER'),
        approve: approveFinanceWorkflowApproval,
        reject: rejectFinanceWorkflowApproval,
    },
];

export function getFinanceApprovalQueueDefinitions(queueIds?: string[]): ApprovalQueueDefinition[] {
    if (!queueIds || queueIds.length === 0) {
        return [allFinanceWorkflowDefinition];
    }

    const selected = new Set(queueIds);
    return definitions.filter(definition => selected.has(definition.id));
}
