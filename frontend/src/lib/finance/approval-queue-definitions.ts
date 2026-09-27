import { ClipboardCheck, FileText, ShoppingCart } from 'lucide-react';
import type { ApprovalQueueDefinition, ApprovalQueueItem } from '@/components/approvals/approval-workbench';
import apiService from '@/services/api.service';
import { businessPartnerFinanceProfileService } from '@/services/businessPartnerFinanceProfileService';

export const FINANCE_APPROVAL_QUEUE_IDS = {
    financeWorkflows: 'finance-workflows',
    journalEntries: 'journal-entries',
    purchaseOrders: 'finance-purchase-orders',
    businessPartnerProfiles: 'business-partner-finance-profiles',
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
    decisionOnDetailPage?: boolean;
    approveDisabledReason?: string | null;
    rejectDisabledReason?: string | null;
    metadata?: Record<string, string>;
}

function normalizeEntityType(entityType?: string | null): string {
    return (entityType || '').replace(/[^a-zA-Z0-9]/g, '').toUpperCase();
}

const DEFAULT_FINANCE_APPROVAL_CURRENCY = 'GHS';

export function normalizeFinanceApprovalCurrencyCode(currencyCode?: string | null): string {
    const normalized = currencyCode?.trim().toUpperCase();
    if (!normalized || !/^[A-Z]{3}$/.test(normalized)) {
        return DEFAULT_FINANCE_APPROVAL_CURRENCY;
    }

    try {
        if (typeof Intl.supportedValuesOf === 'function' && !Intl.supportedValuesOf('currency').includes(normalized)) {
            return DEFAULT_FINANCE_APPROVAL_CURRENCY;
        }

        new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: normalized,
        });
        return normalized;
    } catch {
        return DEFAULT_FINANCE_APPROVAL_CURRENCY;
    }
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
        currencyCode: normalizeFinanceApprovalCurrencyCode(row.currencyCode),
        submittedBy: row.submittedBy,
        canApprove: row.canApprove,
        canReject: row.canReject,
        decisionOnDetailPage: row.decisionOnDetailPage,
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
    documentLabel: 'Finance Items',
    description: 'Finance documents and accounting-book approvals awaiting your current workflow step.',
    emptyMessage: 'No finance items are awaiting your approval.',
    accessDeniedMessage: 'You need a finance approver role to review finance workflow approvals.',
    icon: ClipboardCheck,
    accentClassName: 'border-sky-100 bg-sky-50/50',
    load: () => loadFinanceWorkflowApprovals(),
    approve: approveFinanceWorkflowApproval,
    reject: rejectFinanceWorkflowApproval,
};

function profileAction(item: ApprovalQueueItem): { partnerId: string; ledger: 'ap' | 'ar'; profileId: string } {
    const [ledger, partnerId, profileId] = item.id.split(':');
    if ((ledger !== 'ap' && ledger !== 'ar') || !partnerId || !profileId) throw new Error('Invalid Business Partner profile approval reference.');
    return { partnerId, ledger, profileId };
}

const businessPartnerProfileDefinition: ApprovalQueueDefinition = {
    id: FINANCE_APPROVAL_QUEUE_IDS.businessPartnerProfiles,
    title: 'Business Partner Finance Profiles',
    documentLabel: 'AP/AR Profiles',
    description: 'Submitted effective-dated AP and AR defaults awaiting an independent Finance decision.',
    emptyMessage: 'No Business Partner Finance profiles are awaiting your approval.',
    accessDeniedMessage: 'You need Approve Business Partner Finance Profiles permission to review these profiles.',
    icon: ClipboardCheck,
    accentClassName: 'border-indigo-100 bg-indigo-50/50',
    load: async () => (await businessPartnerFinanceProfileService.getPendingApprovals()).map(row => ({
        id: `${row.ledger}:${row.businessPartnerId}:${row.profileId}`,
        reference: `${row.partnerCode}/${row.ledger.toUpperCase()}/V${row.versionNumber}`,
        title: `${row.partnerName} — ${row.ledger.toUpperCase()} profile`,
        detailHref: `/procurement/business-partners/${row.businessPartnerId}/edit`,
        documentType: 'Business Partner Finance Profile', module: 'Finance Settings',
        statusLabel: 'Submitted', date: row.submittedAtUtc, submittedBy: row.submittedBy,
        canApprove: true, canReject: true,
        metadata: [
            { label: 'Ledger', value: row.ledger.toUpperCase() },
            { label: 'Effective from', value: row.effectiveFrom.slice(0, 10) },
            { label: 'Effective through', value: row.effectiveTo?.slice(0, 10) || 'Open-ended' },
        ],
    })),
    approve: async (item, comments) => {
        const action = profileAction(item);
        await businessPartnerFinanceProfileService.decide(action.partnerId, action.ledger, action.profileId, 'approve', comments);
    },
    reject: async (item, reason) => {
        const action = profileAction(item);
        await businessPartnerFinanceProfileService.decide(action.partnerId, action.ledger, action.profileId, 'reject', reason);
    },
};

const definitions: ApprovalQueueDefinition[] = [
    allFinanceWorkflowDefinition,
    businessPartnerProfileDefinition,
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
        return [allFinanceWorkflowDefinition, businessPartnerProfileDefinition];
    }

    const selected = new Set(queueIds);
    return definitions.filter(definition => selected.has(definition.id));
}
