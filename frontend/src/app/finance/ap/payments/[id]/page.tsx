'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
    ArrowLeft,
    AlertTriangle,
    BookOpen,
    CheckCircle2,
    Printer,
    FileText,
    Loader2,
    ShieldCheck,
    Undo2,
    Upload,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { accountsPayableService } from '@/services/accountsPayableService';
import { financeDataService } from '@/services/finance/finance-data.service';
import { formatCurrency } from '@/lib/utils';
import { format } from 'date-fns';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';
import { useAuth } from '@/hooks/use-auth';
import { DOCUMENT_TYPES, documentOutputService } from '@/services/document-output.service';
import { workflowApiService } from '@/services/workflow-api.service';

export default function VendorPaymentDetailsPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;
    const { toast } = useToast();
    const { user, hasPermission } = useAuth();
    // The API remains authoritative, but hiding this high-risk action for users who lack the
    // dedicated permission prevents the normal payment-processing role from implying reversal rights.
    const canReversePayment = hasPermission('Finance.AP.Payments.Reverse');
    // Vouchers expose approval identities, bank references and accounting coding. Match the
    // explicit backend document policy instead of treating any authenticated payment reader as a
    // document-export user.
    const canPrintVoucher = hasPermission('Finance.Reports.Export');
    const canSubmitPayment = hasPermission('Finance.AP.Payments.Process');
    const canReviewEvidence = hasPermission('Finance.Workflow.Approve');
    const [isPosting, setIsPosting] = useState(false);
    const [isPrintingVoucher, setIsPrintingVoucher] = useState(false);
    const [isReversing, setIsReversing] = useState(false);
    const [reverseDialogOpen, setReverseDialogOpen] = useState(false);
    const [reversalReason, setReversalReason] = useState('');
    const [reversalDate, setReversalDate] = useState('');
    const [submitDialogOpen, setSubmitDialogOpen] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [isExceptionalPayment, setIsExceptionalPayment] = useState(false);
    const [exceptionalPaymentReason, setExceptionalPaymentReason] = useState('');
    const [requestEvidenceException, setRequestEvidenceException] = useState(false);
    const [evidenceExceptionReason, setEvidenceExceptionReason] = useState('');
    const [uploadingRequirementKey, setUploadingRequirementKey] = useState<string | null>(null);
    const [verifyingEvidenceId, setVerifyingEvidenceId] = useState<string | null>(null);

    const { data: payment, isLoading, refetch } = useQuery({
        queryKey: ['vendor-payment', id],
        queryFn: () => accountsPayableService.getPayment(id),
    });

    const { data: paymentControl, isLoading: controlLoading, refetch: refetchControl } = useQuery({
        queryKey: ['vendor-payment-control', id],
        queryFn: () => accountsPayableService.getPaymentControl(id),
        // The endpoint also returns a Draft policy preview. That gives the maker visibility into
        // authority and evidence consequences before the immutable submission snapshot is taken.
        enabled: Boolean(payment),
    });

    const { data: financeSettings } = useQuery({
        queryKey: ['finance-settings'],
        queryFn: () => financeDataService.getFinanceSettings(),
        // The same setting is enforced by the API. Loading it here gives the operator an exact
        // validation rule instead of making them discover a tenant-specific minimum by submission.
        staleTime: 5 * 60 * 1000,
    });
    const minimumReversalReasonLength = financeSettings?.minimumReversalReasonLength ?? 20;

    const { data: trace, isLoading: traceLoading, refetch: refetchTrace } = useQuery({
        queryKey: ['vendor-payment-trace', id],
        queryFn: () => accountsPayableService.getPaymentTrace(id),
        // Draft payments have no ledger evidence yet; deferring avoids a noisy empty trace call.
        enabled: Boolean(payment?.journalEntryId),
    });

    const handlePost = async () => {
        if (!payment) return;

        setIsPosting(true);
        try {
            await accountsPayableService.postPayment(payment.id);
            toast({ title: 'Success', description: 'Vendor payment posted successfully.' });
            await refetch();
        } catch (error: any) {
            toast({
                title: 'Posting failed',
                description: error.message || 'Unable to post vendor payment.',
                variant: 'destructive',
            });
        } finally {
            setIsPosting(false);
        }
    };

    const handleSubmit = async () => {
        if (!payment) return;

        setIsSubmitting(true);
        try {
            await accountsPayableService.submitPayment(payment.id, {
                isExceptionalPayment,
                exceptionalPaymentReason: exceptionalPaymentReason.trim() || undefined,
                requestEvidenceException,
                evidenceExceptionReason: evidenceExceptionReason.trim() || undefined,
            });
            toast({
                title: 'Payment submitted',
                description: 'The applicable evidence and approval policy has been snapshotted for review.',
            });
            setSubmitDialogOpen(false);
            await Promise.all([refetch(), refetchControl()]);
        } catch (error: any) {
            toast({
                title: 'Submission failed',
                description: error?.message || 'Unable to submit this vendor payment.',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleEvidenceUpload = async (
        requirement: NonNullable<typeof paymentControl>['evidenceRequirements'][number],
        file?: File
    ) => {
        if (!file || !paymentControl?.currentStepInstanceId) return;

        setUploadingRequirementKey(requirement.requirementKey);
        try {
            // Requirement metadata is sent explicitly and validated by the workflow step. This
            // prevents a generic attachment from being counted against an unrelated policy key.
            await workflowApiService.uploadStepAttachment(
                paymentControl.currentStepInstanceId,
                file,
                requirement.requirementKey,
                requirement.documentName,
                requirement.documentType
            );
            toast({ title: 'Evidence uploaded', description: `${file.name} is ready for independent verification.` });
            await refetchControl();
        } catch (error: any) {
            toast({
                title: 'Evidence upload failed',
                description: error?.message || 'Unable to upload payment evidence.',
                variant: 'destructive',
            });
        } finally {
            setUploadingRequirementKey(null);
        }
    };

    const handleVerifyEvidence = async (evidenceId: string) => {
        setVerifyingEvidenceId(evidenceId);
        try {
            await workflowApiService.verifyWorkflowEvidence(
                evidenceId,
                true,
                'Reviewed against the AP payment control requirement.'
            );
            toast({ title: 'Evidence verified', description: 'The independent review is recorded in workflow history.' });
            await refetchControl();
        } catch (error: any) {
            toast({
                title: 'Verification failed',
                description: error?.message || 'Unable to verify this evidence.',
                variant: 'destructive',
            });
        } finally {
            setVerifyingEvidenceId(null);
        }
    };

    const handleReverse = async () => {
        if (!payment) return;

        setIsReversing(true);
        try {
            await accountsPayableService.reversePayment(payment.id, {
                reason: reversalReason.trim(),
                reversalDate: reversalDate || undefined,
            });
            toast({
                title: 'Payment reversed',
                description: 'A linked compensating journal was posted and the invoice settlement was restored.',
            });
            setReverseDialogOpen(false);
            setReversalReason('');
            setReversalDate('');
            await Promise.all([refetch(), refetchTrace()]);
        } catch (error: any) {
            toast({
                title: 'Reversal failed',
                description: error?.message || 'Unable to reverse this vendor payment.',
                variant: 'destructive',
            });
        } finally {
            setIsReversing(false);
        }
    };

    const handlePrintVoucher = async () => {
        if (!payment) return;

        setIsPrintingVoucher(true);
        try {
            // The document builder reads the canonical VendorPayment, its allocations, workflow
            // evidence and linked GL journal. This replaces browser-printing the screen, which did
            // not enforce the controlled-document status or retain a generated-copy audit event.
            await documentOutputService.printDocument(
                DOCUMENT_TYPES.financeApPaymentVoucher,
                payment.id,
                { copyType: 'Original' }
            );
        } catch (error: any) {
            toast({
                title: 'Voucher printing failed',
                description: error?.message || 'Unable to generate the controlled payment voucher.',
                variant: 'destructive',
            });
        } finally {
            setIsPrintingVoucher(false);
        }
    };

    if (isLoading) {
        return <PaymentDetailsSkeleton />;
    }

    if (!payment) {
        return (
            <div className="p-8 text-center">
                <h2 className="text-xl font-semibold">Vendor Payment not found</h2>
                <Button variant="link" onClick={() => router.push('/finance/ap/payments')}>
                    Return to list
                </Button>
            </div>
        );
    }

    const getStatusBadge = (status: string) => {
        switch (status) {
            case 'Draft': return <Badge variant="secondary">Draft</Badge>;
            case 'PendingAuthorization': return <Badge className="bg-amber-600">Pending authorization</Badge>;
            case 'Authorized': return <Badge className="bg-emerald-600">Authorized</Badge>;
            case 'Processed': return <Badge className="bg-blue-600">Processed</Badge>;
            case 'Cleared': return <Badge className="bg-green-600">Cleared</Badge>;
            case 'Voided': return <Badge variant="outline" className="text-muted-foreground">Voided</Badge>;
            case 'Reversed': return <Badge className="bg-amber-600">Reversed</Badge>;
            case 'Failed': return <Badge variant="destructive">Failed</Badge>;
            default: return <Badge variant="secondary">{status}</Badge>;
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1000px] mx-auto">
            {/* Header Actions */}
            <div className="flex items-center justify-between no-print">
                <div className="flex items-center space-x-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/ap/payments')}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div className="flex items-center space-x-2">
                        <h1 className="text-2xl font-bold tracking-tight">Payment {payment.paymentNumber}</h1>
                        {getStatusBadge(payment.status)}
                    </div>
                </div>
                <div className="flex space-x-2">
                    {canSubmitPayment && payment.status === 'Draft' && !payment.paymentBatchId && (
                        <Button size="sm" onClick={() => setSubmitDialogOpen(true)}>
                            <ShieldCheck className="mr-2 h-4 w-4" /> Submit for Approval
                        </Button>
                    )}
                    {!payment.journalEntryId && ['Authorized', 'Processed'].includes(payment.status) && (
                        <Button size="sm" onClick={handlePost} disabled={isPosting}>
                            {isPosting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Post Payment
                        </Button>
                    )}
                    {canReversePayment && payment.journalEntryId && !payment.reversalJournalEntryId && ['Processed', 'Cleared'].includes(payment.status) && (
                        <Button variant="destructive" size="sm" onClick={() => setReverseDialogOpen(true)}>
                            <Undo2 className="mr-2 h-4 w-4" /> Reverse Payment
                        </Button>
                    )}
                    {canPrintVoucher && ['Authorized', 'Processed', 'Cleared', 'Reconciled', 'Reversed'].includes(payment.status) && (
                        <Button variant="outline" size="sm" onClick={handlePrintVoucher} disabled={isPrintingVoucher}>
                            {isPrintingVoucher
                                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                : <Printer className="mr-2 h-4 w-4" />}
                            Print Voucher
                        </Button>
                    )}
                    {payment.unallocatedAmount > 0 && payment.status === 'Processed' && (
                        <Button size="sm" onClick={() => router.push(`/finance/ap/payments/create?supplierId=${payment.supplierId}&paymentId=${payment.id}`)}>
                            <FileText className="mr-2 h-4 w-4" /> Allocate Options
                        </Button>
                    )}
                </div>
            </div>

            <Card className="print:shadow-none print:border-none">
                <CardHeader className="flex flex-row justify-between items-start border-b pb-8">
                    <div className="space-y-2">
                        <div className="flex items-center space-x-2">
                            <span className="text-xl font-bold">{payment.supplierName}</span>
                        </div>
                        <p className="text-sm text-muted-foreground mt-1">
                            Supplier ID: {payment.supplierId}
                        </p>
                    </div>
                    <div className="text-right space-y-1">
                        <h2 className="text-3xl font-bold text-gray-200 uppercase tracking-widest">PAYMENT</h2>
                        <div className="flex justify-end space-x-4 pt-4">
                            <div className="text-left">
                                <p className="text-xs text-muted-foreground uppercase font-bold">Payment #</p>
                                <p className="font-medium">{payment.paymentNumber}</p>
                            </div>
                            <div className="text-left">
                                <p className="text-xs text-muted-foreground uppercase font-bold">Date</p>
                                <p className="font-medium">{format(new Date(payment.paymentDate), 'MMM dd, yyyy')}</p>
                            </div>
                        </div>
                    </div>
                </CardHeader>
                <CardContent className="pt-8 space-y-8">
                    {/* Payment Info */}
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-8">
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Amount Paid</p>
                            <p className="font-bold text-2xl">{formatCurrency(payment.totalAmount, payment.currencyCode)}</p>
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Bank Account</p>
                            <p className="font-medium">{payment.bankAccountName || 'Tenant default'}</p>
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Method</p>
                            <p className="font-medium">{payment.paymentMethod}</p>
                            {payment.transactionReference && <p className="text-sm text-muted-foreground mt-1">Ref: {payment.transactionReference}</p>}
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Allocated</p>
                            <p className="font-semibold text-green-600">{formatCurrency(payment.allocatedAmount, payment.currencyCode)}</p>
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">WHT Withheld</p>
                            <p className={payment.withholdingTaxAmount > 0 ? 'font-semibold text-orange-600' : 'font-semibold text-muted-foreground'}>
                                {payment.withholdingTaxAmount > 0 ? formatCurrency(payment.withholdingTaxAmount, payment.currencyCode) : '-'}
                            </p>
                        </div>
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Unallocated</p>
                            <p className={`font-semibold ${payment.unallocatedAmount > 0 ? 'text-amber-600' : 'text-muted-foreground'}`}>
                                {formatCurrency(payment.unallocatedAmount, payment.currencyCode)}
                            </p>
                        </div>
                    </div>

                    {/* Allocations Table */}
                    <div className="pt-8 border-t">
                        <h3 className="text-lg font-semibold mb-4">Invoices Paid</h3>
                        <div className="rounded-md border">
                            <div className="grid grid-cols-12 gap-4 p-4 bg-muted/50 text-xs font-bold uppercase text-muted-foreground border-b">
                                <div className="col-span-3">Invoice #</div>
                                <div className="col-span-3">Allocation Date</div>
                                <div className="col-span-2 text-right">Discount Taken</div>
                                <div className="col-span-2 text-right">WHT Withheld</div>
                                <div className="col-span-2 text-right">Amount Applied</div>
                            </div>
                            {payment.allocations?.map((alloc, index) => (
                                <div key={alloc.id || index} className={`grid grid-cols-12 gap-4 p-4 border-b last:border-0 text-sm items-center ${alloc.isReversal ? 'bg-amber-50/60' : ''}`}>
                                    <div className="col-span-3 font-medium text-blue-600 hover:underline cursor-pointer" onClick={() => router.push(`/finance/ap/invoices/${alloc.vendorInvoiceId}`)}>
                                        {alloc.invoiceNumber} {alloc.isReversal && <Badge variant="outline" className="ml-2">Reversal</Badge>}
                                    </div>
                                    <div className="col-span-3 text-muted-foreground">
                                        {format(new Date(alloc.allocationDate), 'MMM dd, yyyy')}
                                    </div>
                                    <div className="col-span-2 text-right text-muted-foreground">
                                        {alloc.discountAmount > 0 ? formatCurrency(alloc.discountAmount, payment.currencyCode) : '-'}
                                    </div>
                                    <div className="col-span-2 text-right text-orange-600">
                                        {alloc.withholdingTaxAmount > 0 ? formatCurrency(alloc.withholdingTaxAmount, payment.currencyCode) : '-'}
                                    </div>
                                    <div className="col-span-2 text-right font-medium">
                                        {formatCurrency(alloc.allocatedAmount, payment.currencyCode)}
                                    </div>
                                </div>
                            ))}
                            {(!payment.allocations || payment.allocations.length === 0) && (
                                <div className="p-4 text-center text-muted-foreground text-sm">
                                    No invoices have been allocated to this payment yet.
                                </div>
                            )}
                        </div>
                    </div>

                    {/* Notes */}
                    {payment.notes && (
                        <div className="pt-8 border-t">
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Notes</p>
                            <p className="text-sm text-muted-foreground">{payment.notes}</p>
                        </div>
                    )}

                    {payment.reversalJournalEntryId && (
                        <div className="rounded-md border border-amber-300 bg-amber-50 p-4 text-sm">
                            <div className="flex items-center gap-2 font-semibold text-amber-900">
                                <Undo2 className="h-4 w-4" /> Posted-payment reversal
                            </div>
                            <div className="mt-3 grid gap-3 md:grid-cols-2">
                                <div><span className="text-muted-foreground">Reversal date:</span> {payment.reversalDate ? format(new Date(payment.reversalDate), 'MMM dd, yyyy') : '-'}</div>
                                <div><span className="text-muted-foreground">Reversal journal:</span> <span className="font-mono text-xs">{payment.reversalJournalEntryId}</span></div>
                                <div className="md:col-span-2"><span className="text-muted-foreground">Reason:</span> {payment.reversalReason}</div>
                            </div>
                        </div>
                    )}
                </CardContent>
            </Card>

            <Card className="no-print">
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        <ShieldCheck className="h-5 w-5" /> Payment approval controls
                    </CardTitle>
                    <p className="text-sm text-muted-foreground">
                        Effective-dated authority and independently verified evidence required before authorization.
                    </p>
                </CardHeader>
                <CardContent className="space-y-5">
                    {controlLoading && <Skeleton className="h-32 w-full" />}
                    {paymentControl && (
                        <>
                            <div className="grid gap-4 rounded-md border bg-muted/30 p-4 md:grid-cols-3">
                                <div>
                                    <p className="text-xs font-semibold uppercase text-muted-foreground">Applied policy</p>
                                    <p className="mt-1 font-medium">{paymentControl.policyCode || 'No matching policy'}</p>
                                </div>
                                <div>
                                    <p className="text-xs font-semibold uppercase text-muted-foreground">Current stage</p>
                                    <p className="mt-1 font-medium">{paymentControl.currentStepName || (payment.status === 'Draft' ? 'Not submitted' : paymentControl.workflowStatus || '-')}</p>
                                </div>
                                <div>
                                    <p className="text-xs font-semibold uppercase text-muted-foreground">Authority route</p>
                                    <p className="mt-1 font-medium">
                                        Chief Accountant{paymentControl.requiresManagingDirectorApproval ? ' then Managing Director' : ''}
                                    </p>
                                </div>
                            </div>

                            {(paymentControl.isExceptionalPayment || paymentControl.evidenceExceptionRequested) && (
                                <div className="rounded-md border border-amber-300 bg-amber-50 p-4 text-sm text-amber-950">
                                    <div className="flex items-center gap-2 font-semibold">
                                        <AlertTriangle className="h-4 w-4" /> Exceptional authority required
                                    </div>
                                    <p className="mt-1">
                                        {paymentControl.evidenceExceptionRequested
                                            ? 'An evidence exception is recorded; it does not waive executive approval or the audit trail.'
                                            : 'The maker classified this as an exceptional payment, so Managing Director approval is mandatory.'}
                                    </p>
                                </div>
                            )}

                            <div className="space-y-3">
                                <div className="flex items-center justify-between gap-3">
                                    <h3 className="font-semibold">Evidence requirements</h3>
                                    <Badge variant={paymentControl.evidenceRequirementsSatisfied ? 'default' : 'secondary'}>
                                        {paymentControl.evidenceRequirementsSatisfied ? 'Complete' : 'Outstanding'}
                                    </Badge>
                                </div>
                                {paymentControl.evidenceRequirements.map(requirement => {
                                    const documents = paymentControl.evidenceDocuments.filter(
                                        document => document.requirementKey === requirement.requirementKey
                                    );
                                    return (
                                        <div key={requirement.requirementKey} className="rounded-md border p-4">
                                            <div className="flex flex-wrap items-start justify-between gap-3">
                                                <div>
                                                    <div className="flex items-center gap-2">
                                                        {requirement.isSatisfied
                                                            ? <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                                                            : <AlertTriangle className="h-4 w-4 text-amber-600" />}
                                                        <span className="font-medium">{requirement.documentName}</span>
                                                    </div>
                                                    <p className="mt-1 text-xs text-muted-foreground">
                                                        {requirement.verifiedDocumentCount} of {requirement.minimumDocuments} independently verified
                                                        {requirement.documentType ? ` · ${requirement.documentType}` : ''}
                                                    </p>
                                                </div>
                                                {payment.status === 'PendingAuthorization' && paymentControl.currentStepInstanceId && (
                                                    <Label className="cursor-pointer">
                                                        <Input
                                                            type="file"
                                                            className="hidden"
                                                            disabled={uploadingRequirementKey !== null}
                                                            onChange={event => handleEvidenceUpload(requirement, event.target.files?.[0])}
                                                        />
                                                        <span className="inline-flex h-9 items-center rounded-md border bg-background px-3 text-sm font-medium shadow-sm hover:bg-accent">
                                                            {uploadingRequirementKey === requirement.requirementKey
                                                                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                                                : <Upload className="mr-2 h-4 w-4" />}
                                                            Upload
                                                        </span>
                                                    </Label>
                                                )}
                                            </div>

                                            {documents.length > 0 && (
                                                <div className="mt-3 space-y-2 border-t pt-3">
                                                    {documents.map(document => {
                                                        const isVerified = document.verificationStatus === 'Verified';
                                                        const isOwnUpload = document.uploadedById === user?.id;
                                                        return (
                                                            <div key={document.id} className="flex flex-wrap items-center justify-between gap-3 text-sm">
                                                                <div>
                                                                    <p className="font-medium">{document.fileName}</p>
                                                                    <p className="text-xs text-muted-foreground">
                                                                        {document.verificationStatus} · malware scan {document.malwareScanStatus}
                                                                    </p>
                                                                </div>
                                                                {canReviewEvidence && !isVerified && !isOwnUpload && (
                                                                    <Button
                                                                        type="button"
                                                                        size="sm"
                                                                        variant="outline"
                                                                        disabled={verifyingEvidenceId !== null}
                                                                        onClick={() => handleVerifyEvidence(document.id)}
                                                                    >
                                                                        {verifyingEvidenceId === document.id && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                                                        Verify evidence
                                                                    </Button>
                                                                )}
                                                                {isVerified && <Badge className="bg-emerald-600">Verified</Badge>}
                                                                {!isVerified && isOwnUpload && (
                                                                    <span className="text-xs text-muted-foreground">Requires another reviewer</span>
                                                                )}
                                                            </div>
                                                        );
                                                    })}
                                                </div>
                                            )}
                                        </div>
                                    );
                                })}
                            </div>

                            {paymentControl.blockingReasons.length > 0 && payment.status !== 'Draft' && (
                                <div className="rounded-md border border-amber-200 bg-amber-50/70 p-4">
                                    <p className="font-medium text-amber-950">Authorization blockers</p>
                                    <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-amber-900">
                                        {paymentControl.blockingReasons.map(reason => <li key={reason}>{reason}</li>)}
                                    </ul>
                                </div>
                            )}

                            {paymentControl.policySnapshotHash && (
                                <p className="break-all font-mono text-[11px] text-muted-foreground">
                                    Policy snapshot SHA-256: {paymentControl.policySnapshotHash}
                                </p>
                            )}
                        </>
                    )}
                </CardContent>
            </Card>

            {payment.journalEntryId && (
                <Card className="no-print">
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2"><BookOpen className="h-5 w-5" /> Source-to-ledger trace</CardTitle>
                        <p className="text-sm text-muted-foreground">
                            Immutable posting, journal-line, and audit evidence for this payment and any correction.
                        </p>
                    </CardHeader>
                    <CardContent className="space-y-5">
                        {traceLoading && <Skeleton className="h-40 w-full" />}
                        {trace?.postings.map(posting => (
                            <div key={posting.postingEventId} className="rounded-md border">
                                <div className="flex flex-wrap items-center justify-between gap-3 border-b bg-muted/40 p-4">
                                    <div>
                                        <div className="flex items-center gap-2">
                                            <Badge variant={posting.postingAction.startsWith('Reverse') ? 'outline' : 'secondary'}>{posting.postingAction}</Badge>
                                            <span className="font-medium">{posting.journalEntryNumber || posting.journalEntryId}</span>
                                        </div>
                                        <p className="mt-1 text-xs text-muted-foreground">
                                            {format(new Date(posting.postingDate), 'MMM dd, yyyy')} · Event {posting.postingEventId}
                                        </p>
                                    </div>
                                    <div className="text-right text-sm">
                                        <div>Debit {formatCurrency(posting.totalDebitAmount, posting.functionalCurrencyCode)}</div>
                                        <div>Credit {formatCurrency(posting.totalCreditAmount, posting.functionalCurrencyCode)}</div>
                                    </div>
                                </div>
                                <div className="overflow-x-auto">
                                    <table className="w-full text-sm">
                                        <thead className="text-left text-xs uppercase text-muted-foreground">
                                            <tr><th className="p-3">Account</th><th className="p-3">Description</th><th className="p-3 text-right">Debit</th><th className="p-3 text-right">Credit</th></tr>
                                        </thead>
                                        <tbody>
                                            {posting.lines.map(line => (
                                                <tr key={line.transactionId} className="border-t">
                                                    <td className="p-3"><span className="font-mono">{line.accountNumber}</span><br /><span className="text-xs text-muted-foreground">{line.accountName}</span></td>
                                                    <td className="p-3">{line.description}</td>
                                                    <td className="p-3 text-right">{line.debitAmount ? formatCurrency(line.debitAmount, posting.functionalCurrencyCode) : '-'}</td>
                                                    <td className="p-3 text-right">{line.creditAmount ? formatCurrency(line.creditAmount, posting.functionalCurrencyCode) : '-'}</td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        ))}
                        {trace && trace.postings.length === 0 && <p className="text-sm text-muted-foreground">No posting evidence was found.</p>}

                        {trace && trace.auditEvents.length > 0 && (
                            <div>
                                <h3 className="mb-3 font-semibold">Audit history</h3>
                                <div className="space-y-2">
                                    {trace.auditEvents.map(event => (
                                        <div key={event.auditLogId} className="flex flex-wrap justify-between gap-2 rounded-md border p-3 text-sm">
                                            <div><span className="font-medium">{event.eventType}</span><span className="ml-2 text-muted-foreground">by {event.username}</span></div>
                                            <span className="text-muted-foreground">{format(new Date(event.timestamp), 'MMM dd, yyyy HH:mm')}</span>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        )}
                    </CardContent>
                </Card>
            )}

            <Dialog open={submitDialogOpen} onOpenChange={setSubmitDialogOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Submit vendor payment for approval</DialogTitle>
                        <DialogDescription>
                            Submission freezes policy {paymentControl?.policyCode || '(not resolved)'} for this payment. Supporting evidence is uploaded on the resulting approval step and must be independently verified.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-5 py-2">
                        <div className="flex items-start gap-3 rounded-md border p-4">
                            <Checkbox
                                id="exceptionalPayment"
                                checked={isExceptionalPayment}
                                onCheckedChange={checked => setIsExceptionalPayment(checked === true)}
                            />
                            <div className="space-y-1">
                                <Label htmlFor="exceptionalPayment">Exceptional payment</Label>
                                <p className="text-xs text-muted-foreground">
                                    Use for an unusual payment needing explicit executive authority even when it is below the normal high-value band.
                                </p>
                            </div>
                        </div>
                        {isExceptionalPayment && (
                            <div className="space-y-2">
                                <Label htmlFor="exceptionalPaymentReason">Exceptional-payment reason</Label>
                                <Textarea
                                    id="exceptionalPaymentReason"
                                    rows={4}
                                    value={exceptionalPaymentReason}
                                    onChange={event => setExceptionalPaymentReason(event.target.value)}
                                    placeholder="Explain the circumstances, business need, and specific authority requested."
                                />
                            </div>
                        )}

                        <div className="flex items-start gap-3 rounded-md border border-amber-200 bg-amber-50/50 p-4">
                            <Checkbox
                                id="evidenceException"
                                checked={requestEvidenceException}
                                onCheckedChange={checked => setRequestEvidenceException(checked === true)}
                            />
                            <div className="space-y-1">
                                <Label htmlFor="evidenceException">Request evidence exception</Label>
                                <p className="text-xs text-muted-foreground">
                                    This is an auditable exception route, not a silent waiver. Managing Director approval remains mandatory.
                                </p>
                            </div>
                        </div>
                        {requestEvidenceException && (
                            <div className="space-y-2">
                                <Label htmlFor="evidenceExceptionReason">Evidence-exception reason</Label>
                                <Textarea
                                    id="evidenceExceptionReason"
                                    rows={4}
                                    value={evidenceExceptionReason}
                                    onChange={event => setEvidenceExceptionReason(event.target.value)}
                                    placeholder="Identify the unavailable evidence, why it cannot be obtained before payment, and the compensating control."
                                />
                            </div>
                        )}
                        {(isExceptionalPayment || requestEvidenceException) && (
                            <p className="text-xs text-muted-foreground">
                                Each selected exception reason requires at least {paymentControl?.minimumExceptionReasonLength ?? 30} characters.
                            </p>
                        )}
                        {!paymentControl?.canSubmit && (
                            <div className="rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive">
                                No active payment approval policy could be resolved. A Finance workflow administrator must publish a matching policy before submission.
                            </div>
                        )}
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setSubmitDialogOpen(false)} disabled={isSubmitting}>Cancel</Button>
                        <Button
                            onClick={handleSubmit}
                            disabled={
                                isSubmitting ||
                                !paymentControl?.canSubmit ||
                                (isExceptionalPayment && exceptionalPaymentReason.trim().length < (paymentControl?.minimumExceptionReasonLength ?? 30)) ||
                                (requestEvidenceException && evidenceExceptionReason.trim().length < (paymentControl?.minimumExceptionReasonLength ?? 30))
                            }
                        >
                            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Submit and Freeze Policy
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            <Dialog open={reverseDialogOpen} onOpenChange={setReverseDialogOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Reverse posted vendor payment</DialogTitle>
                        <DialogDescription>
                            This posts a compensating journal and restores the AP invoice settlement. The original payment and journal remain visible for audit.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-4 py-2">
                        <div className="space-y-2">
                            <Label htmlFor="reversalReason">Reason</Label>
                            <Textarea
                                id="reversalReason"
                                value={reversalReason}
                                onChange={event => setReversalReason(event.target.value)}
                                placeholder="Describe the error, evidence reviewed, and why reversal is required."
                                rows={5}
                            />
                            <p className="text-xs text-muted-foreground">
                                The tenant policy requires at least {minimumReversalReasonLength} characters.
                            </p>
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="reversalDate">Preferred reversal date (optional)</Label>
                            <Input id="reversalDate" type="date" value={reversalDate} onChange={event => setReversalDate(event.target.value)} />
                            <p className="text-xs text-muted-foreground">Leave blank to let Finance select the valid date under the current-open-period policy.</p>
                        </div>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setReverseDialogOpen(false)} disabled={isReversing}>Cancel</Button>
                        <Button
                            variant="destructive"
                            onClick={handleReverse}
                            disabled={isReversing || reversalReason.trim().length < minimumReversalReasonLength}
                        >
                            {isReversing && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Post Reversal
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}

function PaymentDetailsSkeleton() {
    return (
        <div className="space-y-8 p-8 max-w-[1000px] mx-auto">
            <div className="flex justify-between">
                <Skeleton className="h-10 w-32" />
                <Skeleton className="h-10 w-64" />
            </div>
            <Skeleton className="h-[600px] w-full" />
        </div>
    )
}
