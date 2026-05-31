'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Printer, Download, CheckCircle, XCircle, FileText, SendHorizontal, ShieldCheck, ShieldX, Loader2, RotateCcw, Paperclip, Upload as UploadIcon, Trash2 } from 'lucide-react';
import { useRouter, useParams } from 'next/navigation';
import type { JournalEntry, JournalEntryAttachment, PostingStatus } from '@/types/finance';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { workflowApiService } from '@/services/workflow-api.service';
import type { WorkflowEntitySummaryDto } from '@/types/workflow';

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
            const [data, attachmentData] = await Promise.all([
                financeDataService.getJournalEntryById(id),
                financeDataService.getJournalEntryAttachments(id),
            ]);
            setEntry(data);
            setAttachments(attachmentData || []);

            try {
                const summary = await workflowApiService.getWorkflowEntitySummary('JournalEntry', id);
                setWorkflowSummary(summary);
            } catch {
                setWorkflowSummary(null);
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

    const formatCurrency = (amount: number) => {
        return new Intl.NumberFormat('en-GH', {
            style: 'currency',
            currency: entry?.primaryCurrency || 'GHS',
        }).format(amount);
    };

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('en-GB', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
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
            toast({ title: 'Posted', description: 'Journal entry posted to the General Ledger.' });
            await fetchEntry();
        } catch (err: any) {
            toast({ title: 'Error', description: err?.message || 'Failed to post', variant: 'destructive' });
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

    const canEdit = hasAnyPermission(['Finance.JournalEntries.Edit', 'Finance.JournalEntries.Write']);
    const canDelete = hasAnyPermission(['Finance.JournalEntries.Delete', 'Finance.JournalEntries.Write']);
    const canPost = hasPermission('Finance.JournalEntries.Post');
    const canReverse = hasPermission('Finance.JournalEntries.Reverse');
    const canSubmitForApproval = hasAnyPermission(['Finance.JournalEntries.SubmitForApproval', 'Finance.JournalEntries.Approve']);
    const canApprovePermission = hasPermission('Finance.JournalEntries.Approve');
    const canAttach = canEdit;
    const isCreator = !!entry.createdById && !!user?.id && entry.createdById === user.id;
    const canApproveNow = canApprovePermission && !isCreator && (workflowSummary?.canCurrentUserApprove ?? true);
    const requiresApprovalBeforePost = entry.requiresApproval || entry.postingStatus === 'Pending Approval';

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
                    <Button variant="outline">
                        <Printer className="mr-2 h-4 w-4" />
                        Print
                    </Button>
                    <Button variant="outline">
                        <Download className="mr-2 h-4 w-4" />
                        Export
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
                                            <th className="p-3 text-right font-medium">Debit</th>
                                            <th className="p-3 text-right font-medium">Credit</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {entry.transactions.map((line) => (
                                            <tr key={line.id} className="border-b last:border-0 hover:bg-muted/50">
                                                <td className="p-3">
                                                    <div className="font-mono font-semibold">{line.accountCode}</div>
                                                    <div className="text-sm text-muted-foreground">{line.accountName}</div>
                                                </td>
                                                <td className="p-3">{line.description}</td>
                                                <td className="p-3 text-right font-mono">
                                                    {line.debitAmount > 0 ? formatCurrency(line.debitAmount) : '-'}
                                                </td>
                                                <td className="p-3 text-right font-mono">
                                                    {line.creditAmount > 0 ? formatCurrency(line.creditAmount) : '-'}
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                    <tfoot>
                                        <tr className="bg-muted/50 font-bold">
                                            <td colSpan={2} className="p-3 text-right">Totals:</td>
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
                                    <Button className="w-full" variant="outline" onClick={handleRequestApproval} disabled={actionLoading === 'request-approval'}>
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

                    {/* Pending Approval Actions: Approve, Reject */}
                    {entry.postingStatus === 'Pending Approval' && canApprovePermission && (
                        <Card className="border-amber-500/50">
                            <CardHeader>
                                <CardTitle className="text-amber-600">Approval Required</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                <p className="text-sm text-muted-foreground">
                                    This journal entry is awaiting approval before it can be posted.
                                </p>
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

                                {!showRejectForm ? (
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
