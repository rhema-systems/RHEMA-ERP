'use client';

import React, { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Printer, Download, CheckCircle, XCircle, FileText, SendHorizontal, ShieldCheck, ShieldX, Loader2, RotateCcw, Paperclip, Upload as UploadIcon, Trash2, History, Users, AlertTriangle, CircleDollarSign } from 'lucide-react';
import { useRouter, useParams } from 'next/navigation';
import type { FinanceBudgetControlEvaluation, FinanceJournalAuditLog, JournalEntry, JournalEntryAttachment, PostingStatus } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { workflowApiService } from '@/services/workflow-api.service';
import { WorkflowStepType, type WorkflowEntitySummaryDto, type WorkflowPendingApproverDto } from '@/types/workflow';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import {
    DEFAULT_ACCOUNTING_BOOKS,
    getAccountingBookName,
    getPostingTargetBooks,
    isAllActiveBooksCode,
} from '@/lib/finance/accounting-books';

export default function JournalEntryDetailPage() {
    const router = useRouter();
    const params = useParams();
    const { toast } = useToast();
    const { user, hasAnyPermission, hasPermission } = useAuth();
    const id = params.id as string;

    const [entry, setEntry] = useState<JournalEntry | null>(null);
    const [loading, setLoading] = useState(true);
    const [actionLoading, setActionLoading] = useState<string | null>(null);
    const [rejectionReason, setRejectionReason] = useState('');
    const [showRejectForm, setShowRejectForm] = useState(false);
    
    // Attachments State
    const [isUploading, setIsUploading] = useState(false);
    const [attachments, setAttachments] = useState<JournalEntryAttachment[]>([]);
    const [workflowSummary, setWorkflowSummary] = useState<WorkflowEntitySummaryDto | null>(null);
    const [auditTrail, setAuditTrail] = useState<FinanceJournalAuditLog[]>([]);
    const [accountingBooks, setAccountingBooks] = useState(DEFAULT_ACCOUNTING_BOOKS);
    const [budgetControl, setBudgetControl] = useState<FinanceBudgetControlEvaluation | null>(null);
    const [budgetControlError, setBudgetControlError] = useState<string | null>(null);
    const [overrideReason, setOverrideReason] = useState('');
    const [showOverrideForm, setShowOverrideForm] = useState(false);
    
    // Reversal State
    const [showReverseForm, setShowReverseForm] = useState(false);
    const [reverseReason, setReverseReason] = useState('');
    const [reverseDate, setReverseDate] = useState(() => {
        const today = new Date();
        return today.toISOString().split('T')[0];
    });

    const fetchEntry = useCallback(async () => {
        try {
            setLoading(true);
            const [data, attachmentData, books] = await Promise.all([
                financeDataService.getJournalEntryById(id),
                financeDataService.getJournalEntryAttachments(id),
                financeDataService.getAccountingBooks().catch(() => DEFAULT_ACCOUNTING_BOOKS),
            ]);
            setEntry(data);
            setAttachments(attachmentData || []);
            if (books.length > 0) {
                setAccountingBooks(books);
            }

            try {
                const summary = await workflowApiService.getWorkflowEntitySummary('JournalEntry', id);
                setWorkflowSummary(summary);
            } catch {
                setWorkflowSummary(null);
            }

            try {
                const auditEvents = await financeDataService.getJournalEntryAuditTrail(id);
                setAuditTrail(auditEvents || []);
            } catch {
                setAuditTrail([]);
            }

            try {
                setBudgetControl(await financeDataService.getJournalEntryBudgetControl(id));
                setBudgetControlError(null);
            } catch (error: any) {
                setBudgetControl(null);
                setBudgetControlError(error?.message || 'Budget control could not be evaluated.');
            }
        } catch (err) {
            toast({ title: 'Error', description: 'Failed to load journal entry', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    }, [id, toast]);

    useEffect(() => {
        fetchEntry();
    }, [fetchEntry]);

    const getStatusBadge = (status: PostingStatus) => {
        const variants: Record<PostingStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
            Posted: 'default',
            Draft: 'secondary',
            'Pending Approval': 'outline',
            Approved: 'outline',
            Rejected: 'destructive',
            Reversed: 'destructive',
        };
        return <Badge variant={variants[status]}>{status}</Badge>;
    };

    const getAuditActionLabel = (action: string) => {
        return action
            .replace(/^Finance\.JournalEntry\./, '')
            .replace(/([a-z])([A-Z])/g, '$1 $2');
    };

    const getAuditLocationLabel = (ipAddress?: string | null) => {
        if (!ipAddress) return '';

        const normalizedIp = ipAddress.trim().toLowerCase();
        if (
            normalizedIp === '::1' ||
            normalizedIp === '127.0.0.1' ||
            normalizedIp === 'localhost' ||
            normalizedIp.startsWith('::ffff:127.0.0.1')
        ) {
            return 'local device';
        }

        return ipAddress;
    };

    const getAuditActorLine = (event: FinanceJournalAuditLog) => {
        const username = event.username || 'Unknown user';
        const location = getAuditLocationLabel(event.ipAddress);

        return location ? `${username} from ${location}` : username;
    };

    const getPendingApproverLabel = (approver: WorkflowPendingApproverDto) => {
        return approver.approverName || approver.approverRole || approver.approverId || 'Approver';
    };

    const formatPendingApprovers = (pendingApprovers: WorkflowPendingApproverDto[]) => {
        const labels = pendingApprovers
            .map(getPendingApproverLabel)
            .filter(Boolean);

        return labels.length > 0 ? labels.join(', ') : '';
    };

    const getWorkflowStepTypeLabel = (stepType?: WorkflowStepType | string | number | null) => {
        if (stepType === undefined || stepType === null) return '';

        if (typeof stepType === 'number') {
            return WorkflowStepType[stepType] || String(stepType);
        }

        const numericStepType = Number(stepType);
        if (!Number.isNaN(numericStepType)) {
            return WorkflowStepType[numericStepType] || String(stepType);
        }

        return String(stepType).replace(/([a-z])([A-Z])/g, '$1 $2');
    };

    const formatCurrency = (amount: number, currencyCode?: string) => {
        return new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: currencyCode || entry?.primaryCurrency || 'GHS',
        }).format(amount);
    };

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('en-GB', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
        });
    };

    const formatDateTime = (dateString: string) => {
        return new Date(dateString).toLocaleString('en-GB', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
        });
    };

    const formatFileSize = (size?: number) => {
        if (!size || size <= 0) return '';
        const units = ['B', 'KB', 'MB', 'GB'];
        let value = size;
        let unitIndex = 0;
        while (value >= 1024 && unitIndex < units.length - 1) {
            value /= 1024;
            unitIndex++;
        }
        const fixed = unitIndex === 0 ? value.toFixed(0) : value.toFixed(1);
        return `${fixed} ${units[unitIndex]}`;
    };

    const handlePrint = async () => {
        if (!entry) return;

        try {
            setActionLoading('print');
            await documentOutputService.printDocument(DOCUMENT_TYPES.financeJournalVoucher, entry.id);
            toast({ title: 'Print ready', description: 'Journal voucher PDF opened for printing.' });
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to print journal voucher', variant: 'destructive' });
        } finally {
            setActionLoading(null);
        }
    };

    const handleExport = async () => {
        if (!entry) return;

        try {
            setActionLoading('export');
            await documentOutputService.downloadDocument(DOCUMENT_TYPES.financeJournalVoucher, entry.id);
            toast({ title: 'Exported', description: 'Journal voucher PDF downloaded.' });
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to export journal voucher', variant: 'destructive' });
        } finally {
            setActionLoading(null);
        }
    };

    // ============================================================
    // APPROVAL WORKFLOW ACTIONS
    // ============================================================

    const handleRequestApproval = async () => {
        if (!entry) return;
        try {
            setActionLoading('request-approval');
            await financeDataService.requestJournalEntryApproval(entry.id);
            toast({ title: 'Submitted', description: 'Journal entry submitted for approval.' });
            await fetchEntry();
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to submit for approval', variant: 'destructive' });
        } finally {
            setActionLoading(null);
        }
    };

    const handleRequestBudgetOverride = async () => {
        if (!entry || overrideReason.trim().length < 10) {
            toast({ title: 'Reason required', description: 'Enter at least 10 characters explaining the budget exception.', variant: 'destructive' });
            return;
        }
        try {
            setActionLoading('budget-override');
            await financeDataService.requestJournalEntryBudgetOverride(entry.id, overrideReason.trim());
            toast({ title: 'Override requested', description: 'The Finance Budget Override workflow has started.' });
            setOverrideReason('');
            setShowOverrideForm(false);
            await fetchEntry();
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to request budget override', variant: 'destructive' });
        } finally {
            setActionLoading(null);
        }
    };

    const handleWithdrawApproval = async () => {
        if (!entry) return;

        const confirmed = window.confirm(
            'Withdraw this approval request and return the journal entry to Draft? You can delete it after withdrawal.'
        );
        if (!confirmed) return;

        try {
            setActionLoading('withdraw-approval');
            await financeDataService.withdrawJournalEntryApproval(entry.id, 'Approval request withdrawn by user.');
            toast({ title: 'Approval withdrawn', description: 'Journal entry returned to Draft. You can now delete it.' });
            await fetchEntry();
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to withdraw approval', variant: 'destructive' });
        } finally {
            setActionLoading(null);
        }
    };

    const handleApprove = async () => {
        if (!entry) return;
        try {
            setActionLoading('approve');
            await financeDataService.approveJournalEntry(entry.id);
            toast({ title: 'Approved', description: 'Journal entry has been approved. It can now be posted.' });
            await fetchEntry();
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to approve', variant: 'destructive' });
        } finally {
            setActionLoading(null);
        }
    };

    const handleReject = async () => {
        if (!entry || !rejectionReason.trim()) return;
        try {
            setActionLoading('reject');
            await financeDataService.rejectJournalEntry(entry.id, rejectionReason);
            toast({ title: 'Rejected', description: 'Journal entry has been rejected.' });
            setShowRejectForm(false);
            setRejectionReason('');
            await fetchEntry();
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to reject', variant: 'destructive' });
        } finally {
            setActionLoading(null);
        }
    };

    const handlePost = async () => {
        if (!entry) return;
        try {
            setActionLoading('post');
            await financeDataService.postJournalEntry(entry.id);
            toast({
                title: 'Posted',
                description: isAllActiveBooksCode(entry.bookClassification)
                    ? 'Opening balance journal posted to all active books.'
                    : 'Journal entry posted to the General Ledger.',
            });
            await fetchEntry();
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to post', variant: 'destructive' });
        } finally {
            setActionLoading(null);
        }
    };

    const handleReverse = async () => {
        if (!entry || !reverseReason.trim()) return;
        try {
            setActionLoading('reverse');
            await financeDataService.reverseJournalEntry(entry.id, reverseReason, reverseDate);
            toast({ title: 'Reversed', description: 'Journal entry has been successfully reversed.' });
            setShowReverseForm(false);
            setReverseReason('');
            await fetchEntry();
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to reverse', variant: 'destructive' });
        } finally {
            setActionLoading(null);
        }
    };

    const handleDelete = async () => {
        if (!entry) return;
        try {
            setActionLoading('delete');
            await financeDataService.deleteJournalEntry(entry.id);
            toast({ title: 'Deleted', description: 'Journal entry deleted.' });
            router.push('/finance/journal-entries');
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to delete', variant: 'destructive' });
        } finally {
            setActionLoading(null);
        }
    };

    const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
        if (!entry || !e.target.files || e.target.files.length === 0) return;
        
        const file = e.target.files[0];
        try {
            setIsUploading(true);
            const uploadResult = await financeDataService.uploadAttachment(file, 'JournalEntry', entry.id);
            await financeDataService.linkJournalEntryAttachment(entry.id, uploadResult.fileId);
            
            toast({ title: 'Success', description: 'Attachment uploaded successfully.' });
            await fetchEntry();
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to upload attachment', variant: 'destructive' });
        } finally {
            setIsUploading(false);
            if (e.target) e.target.value = ''; // Reset input
        }
    };

    const handleRemoveAttachment = async (fileId: string) => {
        if (!entry) return;
        try {
            setActionLoading(`remove-attachment-${fileId}`);
            await financeDataService.unlinkJournalEntryAttachment(entry.id, fileId);
            toast({ title: 'Success', description: 'Attachment removed successfully.' });
            await fetchEntry();
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to remove attachment', variant: 'destructive' });
        } finally {
            setActionLoading(null);
        }
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[200px]">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    if (!entry) {
        return (
            <div className="flex flex-col items-center justify-center min-h-[200px] space-y-4">
                <p className="text-muted-foreground">Journal entry not found.</p>
                <Button variant="outline" onClick={() => router.back()}>
                    <ArrowLeft className="mr-2 h-4 w-4" /> Go Back
                </Button>
            </div>
        );
    }

    const isBatchOwned = !!entry.journalBatchId;
    const canEdit = !isBatchOwned && hasAnyPermission(['Finance.JournalEntries.Edit', 'Finance.JournalEntries.Write']);
    const canDelete = !isBatchOwned && hasAnyPermission(['Finance.JournalEntries.Delete', 'Finance.JournalEntries.Write']);
    const canPost = !isBatchOwned && hasPermission('Finance.JournalEntries.Post');
    const canReverse = !isBatchOwned && hasPermission('Finance.JournalEntries.Reverse');
    const canSubmitForApproval = !isBatchOwned && hasAnyPermission(['Finance.JournalEntries.SubmitForApproval', 'Finance.JournalEntries.Approve']);
    const canApprovePermission = !isBatchOwned && hasPermission('Finance.JournalEntries.Approve');
    const canAttach = canEdit;
    const isCreator = !!entry.createdById && !!user?.id && entry.createdById === user.id;
    const hasActiveWorkflowAssignment = workflowSummary?.hasActiveInstance === true;
    const canApproveWorkflow = !hasActiveWorkflowAssignment || workflowSummary?.canCurrentUserApprove === true;
    const canApproveNow = canApprovePermission && !isCreator && canApproveWorkflow;
    const canWithdrawApproval = !isBatchOwned && entry.postingStatus === 'Pending Approval' && (isCreator || canSubmitForApproval || canEdit || canDelete);
    const requiresApprovalBeforePost = entry.requiresApproval || entry.postingStatus === 'Pending Approval';
    const pendingApproverText = workflowSummary ? formatPendingApprovers(workflowSummary.pendingApprovers || []) : '';
    const isAllActiveBooks = isAllActiveBooksCode(entry.bookClassification);
    const selectedBookName = getAccountingBookName(accountingBooks, entry.bookClassification);
    const targetAccountingBooks = getPostingTargetBooks(accountingBooks, entry.bookClassification);
    const targetBookListText = targetAccountingBooks.map(book => book.name).join(', ');

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">
                        {entry.journalEntryNumber}
                    </h1>
                    <p className="text-muted-foreground">
                        {entry.description}
                    </p>
                </div>
                <div className="flex gap-2">
                    <Button variant="outline" onClick={() => router.back()}>
                        <ArrowLeft className="mr-2 h-4 w-4" />
                        Back
                    </Button>
                    <Button variant="outline" onClick={handlePrint} disabled={actionLoading === 'print' || actionLoading === 'export'}>
                        {actionLoading === 'print' ? (
                            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                            <Printer className="mr-2 h-4 w-4" />
                        )}
                        Print
                    </Button>
                    <Button variant="outline" onClick={handleExport} disabled={actionLoading === 'print' || actionLoading === 'export'}>
                        {actionLoading === 'export' ? (
                            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                            <Download className="mr-2 h-4 w-4" />
                        )}
                        Export PDF
                    </Button>
                </div>
            </div>

            {/* Breadcrumbs */}
            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance/journal-entries">Journal Entries</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{entry.journalEntryNumber}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {isBatchOwned && (
                <Alert>
                    <FileText className="h-4 w-4" />
                    <AlertTitle>Controlled by journal batch {entry.journalBatchNumber}</AlertTitle>
                    <AlertDescription className="flex flex-wrap items-center justify-between gap-3">
                        Approval, posting, editing, attachments, and reversal are managed from the batch to preserve its control totals and audit trail.
                        <Button size="sm" variant="outline" asChild>
                            <Link href={`/finance/journal-batches/${entry.journalBatchId}`}>Open batch</Link>
                        </Button>
                    </AlertDescription>
                </Alert>
            )}

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Main Content - Lines */}
                <div className="lg:col-span-2 space-y-6">
                    <Card>
                        <CardHeader>
                            <CardTitle>Transaction Lines</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="rounded-md border">
                                <table className="w-full">
                                    <thead>
                                        <tr className="border-b bg-muted/50">
                                            <th className="p-3 text-left font-medium">Account</th>
                                            <th className="p-3 text-left font-medium">Description</th>
                                            <th className="p-3 text-left font-medium">Coding dimensions</th>
                                            <th className="p-3 text-right font-medium">Debit</th>
                                            <th className="p-3 text-right font-medium">Credit</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {entry.transactions.map((line) => {
                                            const isSystemClearing = line.description?.startsWith('System Clearing');
                                            const debitAmount = line.debitAmount ?? 0;
                                            const creditAmount = line.creditAmount ?? 0;
                                            return (
                                            <tr key={line.id} className="border-b last:border-0 hover:bg-muted/50">
                                                <td className="p-3">
                                                    <div className="flex items-center gap-2">
                                                        <div className="font-mono font-semibold">{line.accountCode}</div>
                                                        {isSystemClearing && (
                                                            <Badge variant="outline" className="text-xs bg-amber-100 text-amber-800 border-amber-200">
                                                                SYS-SPLIT
                                                            </Badge>
                                                        )}
                                                    </div>
                                                    <div className="text-sm text-muted-foreground">{line.accountName}</div>
                                                </td>
                                                <td className="p-3">
                                                    {line.description}
                                                    {isSystemClearing && <div className="text-xs text-amber-600 mt-1">Auto-generated balancing line</div>}
                                                </td>
                                                <td className="p-3">
                                                    {line.dimensions?.length ? (
                                                        <div className="flex flex-wrap gap-1">
                                                            {line.dimensions.map(item => (
                                                                <Badge key={`${item.definitionId}-${item.valueId}`} variant="secondary">
                                                                    {item.dimensionCode}: {item.valueCode}
                                                                </Badge>
                                                            ))}
                                                        </div>
                                                    ) : <span className="text-sm text-muted-foreground">—</span>}
                                                </td>
                                                <td className="p-3 text-right font-mono">
                                                    {debitAmount > 0 ? formatCurrency(debitAmount) : '-'}
                                                </td>
                                                <td className="p-3 text-right font-mono">
                                                    {creditAmount > 0 ? formatCurrency(creditAmount) : '-'}
                                                </td>
                                            </tr>
                                        )})}
                                    </tbody>
                                    <tfoot>
                                        <tr className="bg-muted/50 font-bold">
                                            <td colSpan={3} className="p-3 text-right">Totals:</td>
                                            <td className="p-3 text-right">{formatCurrency(entry.totalDebitAmount)}</td>
                                            <td className="p-3 text-right">{formatCurrency(entry.totalCreditAmount)}</td>
                                        </tr>
                                    </tfoot>
                                </table>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Attachments Section */}
                    <Card>
                        <CardHeader className="flex flex-row items-center justify-between">
                            <CardTitle className="flex items-center gap-2">
                                <Paperclip className="h-5 w-5" />
                                Attachments
                            </CardTitle>
                            {entry.postingStatus !== 'Posted' && entry.postingStatus !== 'Reversed' && canAttach && (
                                <div>
                                    <input 
                                        type="file" 
                                        id="file-upload" 
                                        className="hidden" 
                                        onChange={handleFileUpload} 
                                        disabled={isUploading} 
                                    />
                                    <label htmlFor="file-upload">
                                        <Button asChild variant="outline" size="sm" className="cursor-pointer">
                                            <span>
                                                {isUploading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <UploadIcon className="mr-2 h-4 w-4" />}
                                                Upload File
                                            </span>
                                        </Button>
                                    </label>
                                </div>
                            )}
                        </CardHeader>
                        <CardContent>
                            {attachments.length === 0 ? (
                                <div className="text-center py-6 text-muted-foreground border-2 border-dashed rounded-md">
                                    <Paperclip className="mx-auto h-8 w-8 mb-2 opacity-50" />
                                    <p>No attachments found</p>
                                </div>
                            ) : (
                                <ul className="divide-y divide-border border rounded-md">
                                    {attachments.map((attachment) => (
                                        <li key={attachment.id} className="p-3 flex items-center justify-between hover:bg-muted/50">
                                            <div className="flex items-center gap-3 overflow-hidden">
                                                <FileText className="h-8 w-8 text-blue-500 shrink-0" />
                                                <div className="overflow-hidden">
                                                    <p className="font-medium text-sm truncate">{attachment.fileName}</p>
                                                    <p className="text-xs text-muted-foreground">
                                                        Uploaded {formatDate(attachment.uploadedAt)}
                                                        {attachment.uploadedBy ? ` by ${attachment.uploadedBy}` : ''}
                                                        {formatFileSize(attachment.fileSize) ? ` • ${formatFileSize(attachment.fileSize)}` : ''}
                                                    </p>
                                                </div>
                                            </div>
                                            <div className="flex gap-2 shrink-0 ml-4">
                                                <Button 
                                                    variant="ghost" 
                                                    size="sm" 
                                                    onClick={() => window.open(attachment.fileUrl, '_blank')}
                                                    disabled={!attachment.fileUrl}
                                                    title={attachment.fileUrl ? 'Open attachment' : 'Attachment linked; file URL not returned by API'}
                                                >
                                                    <Download className="h-4 w-4" />
                                                </Button>
                                                {entry.postingStatus !== 'Posted' && entry.postingStatus !== 'Reversed' && canAttach && (
                                                    <Button 
                                                        variant="ghost" 
                                                        size="sm" 
                                                        className="text-destructive hover:text-destructive"
                                                        onClick={() => handleRemoveAttachment(attachment.fileId)}
                                                        disabled={actionLoading === `remove-attachment-${attachment.fileId}`}
                                                    >
                                                        {actionLoading === `remove-attachment-${attachment.fileId}` ? (
                                                            <Loader2 className="h-4 w-4 animate-spin" />
                                                        ) : (
                                                            <Trash2 className="h-4 w-4" />
                                                        )}
                                                    </Button>
                                                )}
                                            </div>
                                        </li>
                                    ))}
                                </ul>
                            )}
                        </CardContent>
                    </Card>

                    {/* Rejection reason display */}
                    {entry.postingStatus === 'Rejected' && entry.rejectionReason && (
                        <Card className="border-destructive/50">
                            <CardHeader>
                                <CardTitle className="text-destructive">Rejection Reason</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <p className="text-sm">{entry.rejectionReason}</p>
                            </CardContent>
                        </Card>
                    )}
                </div>

                {/* Sidebar - Header Info */}
                <div className="space-y-6">
                    <Card>
                        <CardHeader>
                            <CardTitle>Entry Details</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div>
                                <p className="text-sm text-muted-foreground">Status</p>
                                <div className="mt-1">{getStatusBadge(entry.postingStatus)}</div>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Entry Date</p>
                                <p className="font-medium">{formatDate(entry.entryDate)}</p>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Type</p>
                                <Badge variant="outline">{entry.journalType}</Badge>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Book Classification</p>
                                <p className="font-medium">{selectedBookName}</p>
                            </div>
                            <div>
                                <p className="text-sm text-muted-foreground">Currency</p>
                                <p className="font-mono font-semibold">{entry.primaryCurrency}</p>
                            </div>
                            {entry.postingDate && (
                                <div>
                                    <p className="text-sm text-muted-foreground">Posted Date</p>
                                    <p className="font-medium">{formatDate(entry.postingDate)}</p>
                                </div>
                            )}
                            {entry.approvedDate && (
                                <div>
                                    <p className="text-sm text-muted-foreground">Approved Date</p>
                                    <p className="font-medium">{formatDate(entry.approvedDate)}</p>
                                </div>
                            )}
                            <div>
                                <p className="text-sm text-muted-foreground">Created By</p>
                                <p className="font-medium">{entry.createdBy}</p>
                            </div>
                        </CardContent>
                    </Card>

                    {entry.journalType === 'Opening Balance' && isAllActiveBooks && entry.postingStatus !== 'Posted' && entry.postingStatus !== 'Reversed' && (
                        <Card className="border-blue-500/40">
                            <CardHeader>
                                <CardTitle>All Active Books Posting</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                <p className="text-sm text-muted-foreground">
                                    Posting will duplicate this opening balance journal across the active posting books.
                                </p>
                                <div className="flex flex-wrap gap-2">
                                    {(targetBookListText ? targetAccountingBooks : []).map((book) => (
                                        <Badge key={book.code} variant="outline">{book.name}</Badge>
                                    ))}
                                    {!targetBookListText && (
                                        <span className="text-sm text-destructive">No active posting books configured.</span>
                                    )}
                                </div>
                                <p className="text-xs text-muted-foreground">
                                    The active book list is resolved again at posting time.
                                </p>
                            </CardContent>
                        </Card>
                    )}

                    {(budgetControl?.hasTrackedExpenseLines || budgetControlError) && (
                        <Card className={budgetControl?.isAllowed ? 'border-emerald-500/40' : 'border-amber-500/60'}>
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <CircleDollarSign className="h-5 w-5" />
                                    Finance Budget Control
                                </CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                {budgetControlError ? (
                                    <Alert variant="destructive">
                                        <AlertTriangle className="h-4 w-4" />
                                        <AlertTitle>Budget evaluation unavailable</AlertTitle>
                                        <AlertDescription>{budgetControlError}</AlertDescription>
                                    </Alert>
                                ) : budgetControl && (
                                    <>
                                        <Alert variant={budgetControl.isAllowed ? 'default' : 'destructive'}>
                                            <AlertTriangle className="h-4 w-4" />
                                            <AlertTitle>
                                                {budgetControl.hasApprovedOverride
                                                    ? 'Approved budget override applies'
                                                    : budgetControl.overrideStatus === 'PendingApproval'
                                                        ? 'Budget override pending approval'
                                                    : budgetControl.isAllowed
                                                        ? 'Budget available'
                                                        : budgetControl.requiresOverride
                                                            ? 'Budget override required'
                                                            : 'Budget setup blocks submission'}
                                            </AlertTitle>
                                            <AlertDescription>
                                                Controlled expense request: {formatCurrency(budgetControl.totalRequestedAmount, budgetControl.currencyCode)}
                                                {budgetControl.totalShortfallAmount > 0 && `; shortfall: ${formatCurrency(budgetControl.totalShortfallAmount, budgetControl.currencyCode)}`}.
                                            </AlertDescription>
                                        </Alert>

                                        <div className="space-y-3">
                                            {budgetControl.lines.map((line) => (
                                                <div key={`${line.accountId}-${line.fiscalPeriodId}`} className="rounded-md border p-3 text-sm">
                                                    <div className="flex items-start justify-between gap-3">
                                                        <div>
                                                            <p className="font-medium">{line.accountNumber} · {line.accountName}</p>
                                                            <p className="text-xs text-muted-foreground">
                                                                {line.budgetScenarioName || 'No adopted scenario'} · {line.fiscalPeriodCode}
                                                                {line.segmentValue ? ` · ${line.segmentValue}` : ''}
                                                            </p>
                                                        </div>
                                                        <Badge variant={line.decisionCode === 'AVAILABLE' ? 'default' : 'destructive'}>
                                                            {line.decisionCode.replaceAll('_', ' ')}
                                                        </Badge>
                                                    </div>
                                                    <div className="mt-2 grid grid-cols-2 gap-2 text-xs text-muted-foreground">
                                                        <span>Budget: {formatCurrency(line.budgetAmount, budgetControl.currencyCode)}</span>
                                                        <span>Posted: {formatCurrency(line.postedActualAmount, budgetControl.currencyCode)}</span>
                                                        <span>Reserved: {formatCurrency(line.reservedAmount, budgetControl.currencyCode)}</span>
                                                        <span>Available: {formatCurrency(line.availableAmount, budgetControl.currencyCode)}</span>
                                                        <span>Requested: {formatCurrency(line.requestedAmount, budgetControl.currencyCode)}</span>
                                                        <span>Shortfall: {formatCurrency(line.shortfallAmount, budgetControl.currencyCode)}</span>
                                                    </div>
                                                    <p className="mt-2 text-xs">{line.message}</p>
                                                </div>
                                            ))}
                                        </div>

                                        {entry.postingStatus === 'Draft' && budgetControl.requiresOverride && !budgetControl.hasApprovedOverride && budgetControl.overrideStatus !== 'PendingApproval' && (
                                            <div className="space-y-2 border-t pt-3">
                                                {!showOverrideForm ? (
                                                    <Button variant="outline" className="w-full" onClick={() => setShowOverrideForm(true)}>
                                                        Request Budget Override
                                                    </Button>
                                                ) : (
                                                    <>
                                                        <textarea
                                                            className="min-h-[90px] w-full rounded-md border bg-background p-2 text-sm"
                                                            placeholder="Explain the operational need and why the adopted budget is insufficient..."
                                                            value={overrideReason}
                                                            onChange={(event) => setOverrideReason(event.target.value)}
                                                        />
                                                        <div className="flex gap-2">
                                                            <Button
                                                                className="flex-1"
                                                                onClick={handleRequestBudgetOverride}
                                                                disabled={actionLoading === 'budget-override' || overrideReason.trim().length < 10}
                                                            >
                                                                {actionLoading === 'budget-override' && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                                                Submit Override
                                                            </Button>
                                                            <Button variant="outline" onClick={() => setShowOverrideForm(false)}>Cancel</Button>
                                                        </div>
                                                    </>
                                                )}
                                            </div>
                                        )}
                                    </>
                                )}
                            </CardContent>
                        </Card>
                    )}

                    {workflowSummary?.hasActiveInstance && (
                        <Card>
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <Users className="h-5 w-5" />
                                    Workflow Assignment
                                </CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div>
                                    <p className="text-sm text-muted-foreground">Workflow</p>
                                    <p className="font-medium">{workflowSummary.workflowName || 'Active workflow'}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-muted-foreground">Current Step</p>
                                    <div className="mt-1 flex flex-wrap items-center gap-2">
                                        <Badge variant="outline">{workflowSummary.currentStepName || 'Current step'}</Badge>
                                        {workflowSummary.currentStepType !== undefined && workflowSummary.currentStepType !== null && (
                                            <Badge variant="secondary">{getWorkflowStepTypeLabel(workflowSummary.currentStepType)}</Badge>
                                        )}
                                    </div>
                                </div>
                                <div>
                                    <p className="text-sm text-muted-foreground">Pending Approval With</p>
                                    {workflowSummary.pendingApprovers?.length ? (
                                        <div className="mt-2 flex flex-wrap gap-2">
                                            {workflowSummary.pendingApprovers.map((approver, index) => (
                                                <Badge
                                                    key={`${approver.approverId || approver.approverRole || approver.approverName || 'approver'}-${index}`}
                                                    variant={workflowSummary.canCurrentUserApprove ? 'default' : 'outline'}
                                                >
                                                    {getPendingApproverLabel(approver)}
                                                </Badge>
                                            ))}
                                        </div>
                                    ) : (
                                        <p className="mt-1 text-sm text-amber-700">
                                            No explicit approver is recorded for this workflow step.
                                        </p>
                                    )}
                                </div>
                                <p className="text-xs text-muted-foreground">
                                    {workflowSummary.canCurrentUserApprove
                                        ? 'The current user can act on this workflow step.'
                                        : 'The current user cannot act on this workflow step.'}
                                </p>
                            </CardContent>
                        </Card>
                    )}

                    <Card>
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <History className="h-5 w-5" />
                                Audit Trail
                            </CardTitle>
                        </CardHeader>
                        <CardContent>
                            {auditTrail.length === 0 ? (
                                <p className="text-sm text-muted-foreground">No finance audit events recorded yet.</p>
                            ) : (
                                <div className="space-y-3">
                                    {auditTrail.slice(0, 8).map((event) => (
                                        <div key={event.id} className="border-l-2 border-blue-200 pl-3">
                                            <div className="flex items-center justify-between gap-3">
                                                <p className="text-sm font-medium">{getAuditActionLabel(event.action)}</p>
                                                <span className="text-xs text-muted-foreground whitespace-nowrap">
                                                    {formatDateTime(event.timestamp)}
                                                </span>
                                            </div>
                                            <p className="text-xs text-muted-foreground">
                                                {getAuditActorLine(event)}
                                            </p>
                                        </div>
                                    ))}
                                </div>
                            )}
                        </CardContent>
                    </Card>

                    {/* ================================================================== */}
                    {/* ACTIONS CARD - Context-sensitive by posting status */}
                    {/* ================================================================== */}

                    {/* Draft Actions: Edit, Submit for Approval, Post, Delete */}
                    {entry.postingStatus === 'Draft' && (canEdit || canSubmitForApproval || canPost || canDelete) && (
                        <Card>
                            <CardHeader>
                                <CardTitle>Actions</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-2">
                                {canEdit && (
                                    <Button className="w-full" variant="secondary" onClick={() => router.push(`/finance/journal-entries/${id}/edit`)}>
                                        <FileText className="mr-2 h-4 w-4" />
                                        Edit Entry
                                    </Button>
                                )}
                                {canSubmitForApproval && (
                                    <Button
                                        className="w-full"
                                        variant="outline"
                                        onClick={handleRequestApproval}
                                        disabled={actionLoading === 'request-approval' || Boolean(budgetControlError) || budgetControl?.isAllowed === false}
                                    >
                                        {actionLoading === 'request-approval' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <SendHorizontal className="mr-2 h-4 w-4" />}
                                        Submit for Approval
                                    </Button>
                                )}
                                {canPost && (
                                    <Button className="w-full" onClick={handlePost} disabled={actionLoading === 'post' || requiresApprovalBeforePost}>
                                        {actionLoading === 'post' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CheckCircle className="mr-2 h-4 w-4" />}
                                        Post Entry
                                    </Button>
                                )}
                                {canDelete && (
                                    <Button variant="outline" className="w-full text-destructive hover:text-destructive" onClick={handleDelete} disabled={actionLoading === 'delete'}>
                                        {actionLoading === 'delete' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <XCircle className="mr-2 h-4 w-4" />}
                                        Delete Entry
                                    </Button>
                                )}
                            </CardContent>
                        </Card>
                    )}

                    {/* Pending Approval Actions: Approve, Reject, Withdraw */}
                    {entry.postingStatus === 'Pending Approval' && (canApprovePermission || canWithdrawApproval) && (
                        <Card className="border-amber-500/50">
                            <CardHeader>
                                <CardTitle className="text-amber-600">Approval Required</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                <p className="text-sm text-muted-foreground">
                                    This journal entry is awaiting approval before it can be posted.
                                </p>
                                {!canApproveWorkflow && (
                                    <p className="text-xs text-amber-700">
                                        You have finance approval permission, but this workflow task is assigned to {pendingApproverText || 'another approver'}.
                                    </p>
                                )}
                                {canApprovePermission && (
                                    <Button
                                        className="w-full bg-green-600 hover:bg-green-700 text-white"
                                        onClick={handleApprove}
                                        disabled={actionLoading === 'approve' || !canApproveNow}
                                    >
                                        {actionLoading === 'approve' ? (
                                            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                        ) : (
                                            <ShieldCheck className="mr-2 h-4 w-4" />
                                        )}
                                        Approve
                                    </Button>
                                )}

                                {canApprovePermission && (
                                    !showRejectForm ? (
                                        <Button
                                            variant="outline"
                                            className="w-full text-destructive hover:text-destructive border-destructive/50"
                                            onClick={() => setShowRejectForm(true)}
                                            disabled={!canApproveNow}
                                        >
                                            <ShieldX className="mr-2 h-4 w-4" />
                                            Reject
                                        </Button>
                                    ) : (
                                        <div className="space-y-2">
                                            <textarea
                                                className="w-full rounded-md border p-2 text-sm min-h-[80px] bg-background"
                                                placeholder="Enter rejection reason (required)..."
                                                value={rejectionReason}
                                                onChange={(e) => setRejectionReason(e.target.value)}
                                            />
                                            <div className="flex gap-2">
                                                <Button
                                                    variant="destructive"
                                                    className="flex-1"
                                                    onClick={handleReject}
                                                    disabled={!rejectionReason.trim() || actionLoading === 'reject' || !canApproveNow}
                                                >
                                                    {actionLoading === 'reject' ? (
                                                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                                    ) : (
                                                        <ShieldX className="mr-2 h-4 w-4" />
                                                    )}
                                                    Confirm Reject
                                                </Button>
                                                <Button
                                                    variant="ghost"
                                                    className="flex-1"
                                                    onClick={() => { setShowRejectForm(false); setRejectionReason(''); }}
                                                >
                                                    Cancel
                                                </Button>
                                            </div>
                                        </div>
                                    )
                                )}

                                {canWithdrawApproval && (
                                    <Button
                                        variant="outline"
                                        className="w-full"
                                        onClick={handleWithdrawApproval}
                                        disabled={actionLoading === 'withdraw-approval'}
                                    >
                                        {actionLoading === 'withdraw-approval' ? (
                                            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                        ) : (
                                            <RotateCcw className="mr-2 h-4 w-4" />
                                        )}
                                        Withdraw Approval
                                    </Button>
                                )}
                            </CardContent>
                        </Card>
                    )}

                    {/* Approved Actions: Post */}
                    {entry.postingStatus === 'Approved' && canPost && (
                        <Card className="border-green-500/50">
                            <CardHeader>
                                <CardTitle className="text-green-600">Approved — Ready to Post</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-2">
                                <p className="text-sm text-muted-foreground">
                                    This entry has been approved and can now be posted to the General Ledger.
                                </p>
                                <Button
                                    className="w-full"
                                    onClick={handlePost}
                                    disabled={actionLoading === 'post' || !canPost}
                                >
                                    {actionLoading === 'post' ? (
                                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                    ) : (
                                        <CheckCircle className="mr-2 h-4 w-4" />
                                    )}
                                    Post to General Ledger
                                </Button>
                            </CardContent>
                        </Card>
                    )}

                    {/* Posted Actions: Reverse */}
                    {entry.postingStatus === 'Posted' && !entry.isReversed && canReverse && (
                        <Card className="border-destructive/50">
                            <CardHeader>
                                <CardTitle className="text-destructive">Posted Actions</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                <p className="text-sm text-muted-foreground">
                                    This entry is posted. Reversing it will create a new offsetting journal entry.
                                </p>
                                {!showReverseForm ? (
                                    <Button
                                        variant="outline"
                                        className="w-full text-destructive hover:text-destructive border-destructive/50"
                                        onClick={() => setShowReverseForm(true)}
                                        disabled={!canReverse}
                                    >
                                        <RotateCcw className="mr-2 h-4 w-4" />
                                        Reverse Entry
                                    </Button>
                                ) : (
                                    <div className="space-y-3">
                                        <div>
                                            <label className="text-xs font-medium text-muted-foreground">Reversal Date</label>
                                            <input
                                                type="date"
                                                className="w-full rounded-md border p-2 text-sm mt-1 bg-background"
                                                value={reverseDate}
                                                onChange={(e) => setReverseDate(e.target.value)}
                                            />
                                        </div>
                                        <div>
                                            <label className="text-xs font-medium text-muted-foreground">Reversal Reason (required)</label>
                                            <textarea
                                                className="w-full rounded-md border p-2 text-sm mt-1 min-h-[60px] bg-background"
                                                placeholder="Why are you reversing this?"
                                                value={reverseReason}
                                                onChange={(e) => setReverseReason(e.target.value)}
                                            />
                                        </div>
                                        <div className="flex gap-2">
                                            <Button
                                                variant="destructive"
                                                className="flex-1"
                                                onClick={handleReverse}
                                                disabled={!reverseReason.trim() || actionLoading === 'reverse' || !canReverse}
                                            >
                                                {actionLoading === 'reverse' ? (
                                                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                                ) : (
                                                    <RotateCcw className="mr-2 h-4 w-4" />
                                                )}
                                                Confirm Reversal
                                            </Button>
                                            <Button
                                                variant="ghost"
                                                className="flex-1"
                                                onClick={() => { setShowReverseForm(false); setReverseReason(''); }}
                                            >
                                                Cancel
                                            </Button>
                                        </div>
                                    </div>
                                )}
                            </CardContent>
                        </Card>
                    )}

                    {/* Reversed State Display */}
                    {entry.isReversed && (
                        <Card className="border-purple-500/50">
                            <CardHeader>
                                <CardTitle className="text-purple-600">Reversed</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-2">
                                <p className="text-sm text-muted-foreground">
                                    This journal entry was reversed on <span className="font-medium">{entry.reversalDate ? formatDate(entry.reversalDate) : 'Unknown'}</span>.
                                </p>
                                {entry.reversalReason && (
                                    <p className="text-sm italic">
                                        "{entry.reversalReason}"
                                    </p>
                                )}
                            </CardContent>
                        </Card>
                    )}

                    {/* Rejected Actions: Edit and resubmit */}
                    {entry.postingStatus === 'Rejected' && canEdit && (
                        <Card className="border-destructive/50">
                            <CardHeader>
                                <CardTitle className="text-destructive">Entry Rejected</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-2">
                                <p className="text-sm text-muted-foreground">
                                    This entry was rejected. Edit the entry and resubmit for approval.
                                </p>
                                <Button className="w-full" variant="secondary" onClick={() => router.push(`/finance/journal-entries/${id}/edit`)}>
                                    <FileText className="mr-2 h-4 w-4" />
                                    Edit and Resubmit
                                </Button>
                            </CardContent>
                        </Card>
                    )}
                </div>
            </div>
        </div>
    );
}
