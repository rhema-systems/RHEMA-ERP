'use client';
import { ActualLandedCostSummary } from '@/components/procurement/ActualLandedCostSummary';
import { landedCostTaxReviewPending } from '@/lib/landed-cost-tax';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
    ArrowLeft,
    Printer,
    Mail,
    Download,
    CreditCard,
    Ban,
    FileText,
    CheckCircle,
    Loader2,
    RefreshCw,
    Trash2
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
    CardFooter
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { accountsPayableService } from '@/services/accountsPayableService';
import { formatCurrency } from '@/lib/utils';
import { vendorInvoiceStatusLabel } from '@/lib/vendor-invoice-status';
import { format } from 'date-fns';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { ToastAction } from '@/components/ui/toast';
import { useAuth } from '@/hooks/use-auth';
import { useWorkflowSummary } from '@/hooks/useWorkflowSummary';
import {
    InvoiceThreeWayMatchControl,
    invoiceThreeWayMatchQueryKey,
} from '@/components/finance/InvoiceThreeWayMatchControl';
import { InvoiceMatchExceptionControl } from '@/components/finance/InvoiceMatchExceptionControl';
import {
    ApInvoicePrintDocument,
    printApInvoiceDocument,
} from '@/components/finance/ap/ApInvoicePrintDocument';
import printStyles from '@/components/finance/ap/ApInvoicePrintDocument.module.css';
import { useTenant } from '@/contexts/TenantContext';
import { SourceDocumentDimensionEvidence } from '@/components/finance/dimensions/source-document-dimension-panel';
import { InvoiceDistribution } from '@/components/finance/ap/InvoiceDistribution';
import { getFinancePostingErrorPresentation } from '@/lib/finance/posting-error';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';

export default function VendorInvoiceDetailsPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;
    const { toast } = useToast();
    const queryClient = useQueryClient();
    const { hasPermission, hasAnyPermission } = useAuth();
    const { currentTenant, currentTenantCode } = useTenant();
    const [showDeleteConfirmation, setShowDeleteConfirmation] = useState(false);
    const { summary: workflowSummary, visibility: workflowVisibility, error: workflowError, refresh: refreshWorkflow } =
        useWorkflowSummary({ entityType: 'VendorInvoice', entityId: id });

    const { data: invoice, isLoading, error: invoiceError, refetch: refetchInvoice } = useQuery({
        queryKey: ['vendor-invoice', id],
        queryFn: () => accountsPayableService.getInvoice(id),
    });

    const { data: matchReadiness, isLoading: isMatchReadinessLoading } = useQuery({
        queryKey: invoiceThreeWayMatchQueryKey(id),
        queryFn: () => accountsPayableService.getThreeWayMatchReadiness(id),
        enabled: Boolean(invoice?.purchaseOrderId && !invoice?.isOpeningBalance),
        retry: 1,
    });

    const voidInvoiceMutation = useMutation({
        mutationFn: (id: string) => accountsPayableService.voidInvoice(id, 'Voided by user'),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['vendor-invoice', id] });
            queryClient.invalidateQueries({ queryKey: invoiceThreeWayMatchQueryKey(id) });
            toast({
                title: 'Success',
                description: 'Vendor invoice voided successfully',
            });
        },
        onError: (error: any) => {
            toast({
                title: 'Error',
                description: error.message || 'Failed to void vendor invoice',
                variant: 'destructive',
            });
        },
    });

    const deleteInvoiceMutation = useMutation({
        mutationFn: () => accountsPayableService.deleteInvoice(id),
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: ['vendor-invoices'] });
            queryClient.removeQueries({ queryKey: ['vendor-invoice', id] });
            toast({ title: 'Draft invoice deleted', description: 'The vendor invoice draft was removed.' });
            router.push('/finance/ap/invoices');
        },
        onError: (error: any) => {
            toast({ title: 'Unable to delete invoice', description: error.message || 'The draft invoice could not be deleted.', variant: 'destructive' });
        },
    });

    const approveInvoiceMutation = useMutation({
        mutationFn: (id: string) => accountsPayableService.approveInvoice(id, 'Approved'),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['vendor-invoice', id] });
            queryClient.invalidateQueries({ queryKey: invoiceThreeWayMatchQueryKey(id) });
            toast({
                title: 'Success',
                description: 'Vendor invoice approved successfully',
            });
        },
        onError: (error: unknown) => {
            const postingError = getFinancePostingErrorPresentation(
                error,
                'Failed to approve the vendor invoice.',
                'Approval failed',
            );
            toast({
                ...postingError,
                variant: 'destructive',
            });
        },
    });

    const postOpeningBalanceMutation = useMutation({
        mutationFn: (invoiceId: string) => accountsPayableService.postInvoice(invoiceId),
        onSuccess: (postedInvoice) => {
            queryClient.invalidateQueries({ queryKey: ['vendor-invoice', id] });
            queryClient.invalidateQueries({ queryKey: ['vendor-invoices'] });
            queryClient.invalidateQueries({ queryKey: ['subledger-opening-balance-readiness'] });
            toast({
                title: 'Opening balance posted',
                description: postedInvoice.journalEntryId
                    ? 'The AP opening balance is linked to its governed journal.'
                    : 'Posting completed, but no journal reference was returned. Check Subledger Readiness before continuing.',
            });
        },
        onError: (error: unknown) => {
            const postingError = getFinancePostingErrorPresentation(
                error,
                'Failed to post the AP opening balance.',
                'Opening balance posting failed',
            );
            toast({ ...postingError, variant: 'destructive' });
        },
    });

    const submitInvoiceMutation = useMutation({
        mutationFn: (invoiceId: string) => accountsPayableService.submitInvoiceForApproval(invoiceId),
        onSuccess: (savedInvoice) => {
            queryClient.invalidateQueries({ queryKey: ['vendor-invoice', id] });
            queryClient.invalidateQueries({ queryKey: invoiceThreeWayMatchQueryKey(id) });
            void refreshWorkflow();
            toast({ title: 'Success', description: savedInvoice.approvalRequired === false
                ? 'Invoice completed. Approval is not required.' : 'Vendor invoice submitted for approval.' });
        },
        onError: (error: any) => {
            const postingError = getFinancePostingErrorPresentation(
                error,
                'Failed to submit the vendor invoice.',
                'Submission failed',
            );
            const message = error?.message || postingError.description;
            const budgetCellRequired = message.includes('requires an adopted Finance budget cell');
            toast({
                title: budgetCellRequired ? 'Budget cell required' : postingError.title,
                description: budgetCellRequired
                    ? 'Edit the invoice and select an adopted Finance budget cell for the affected expense line.'
                    : postingError.description,
                variant: 'destructive',
                action: budgetCellRequired ? (
                    <ToastAction
                        altText="Edit invoice to select a budget cell"
                        onClick={() => router.push(`/finance/ap/invoices/${id}/edit`)}
                    >
                        Edit Invoice
                    </ToastAction>
                ) : undefined,
            });
        },
    });

    const refreshBudgetMutation = useMutation({
        mutationFn: () => accountsPayableService.refreshInvoiceBudget(id),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['vendor-invoice', id] });
            toast({ title: 'Budget evidence refreshed', description: 'The reservation now reflects the current source dimensions.' });
        },
        onError: (error: any) => {
            toast({ title: 'Budget refresh failed', description: error.message || 'Unable to refresh budget evidence', variant: 'destructive' });
        },
    });

    if (isLoading) {
        return <InvoiceDetailsSkeleton />;
    }

    if (!invoice) {
        return (
            <div className="p-8 text-center">
                <h2 className="text-xl font-semibold">{invoiceError ? 'Unable to load vendor invoice' : 'Vendor Invoice not found'}</h2>
                {invoiceError && <>
                    <p role="alert" className="my-4 text-sm text-destructive">{invoiceError.message}</p>
                    <Button variant="outline" onClick={() => void refetchInvoice()}>Retry</Button>
                </>}
                <Button variant="link" onClick={() => router.push('/finance/ap/invoices')}>
                    Return to list
                </Button>
            </div>
        );
    }

    const getStatusBadge = (value: Parameters<typeof vendorInvoiceStatusLabel>[0]) => {
        switch (value.status) {
            case 'Draft': return <Badge variant="secondary">Draft</Badge>;
            case 'PendingApproval': return <Badge className="bg-yellow-600">Pending Approval</Badge>;
            case 'Approved': return <Badge className="bg-blue-600">{vendorInvoiceStatusLabel(value)}</Badge>;
            case 'PartiallyPaid': return <Badge className="bg-indigo-600">Partially Paid</Badge>;
            case 'Paid': return <Badge className="bg-green-600">Paid</Badge>;
            case 'Overdue': return <Badge variant="destructive">Overdue</Badge>;
            case 'Voided': return <Badge variant="outline" className="text-muted-foreground">Voided</Badge>;
            case 'Rejected': return <Badge variant="destructive">Rejected</Badge>;
            case 'OnHold': return <Badge variant="secondary" className="bg-orange-500">On Hold</Badge>;
            default: return <Badge variant="secondary">{value.status}</Badge>;
        }
    };

    const mandatoryMatchReady = !invoice.purchaseOrderId || invoice.isOpeningBalance || matchReadiness?.approvalReady === true;
    const hasBudgetLines = invoice.lineItems.some((line) => Boolean(line.budgetEntryId));
    const budgetReady = !hasBudgetLines || invoice.financeDimensions?.budgetEvidenceStatus === 'Current';
    const taxReviewPending = landedCostTaxReviewPending(invoice.lineItems);
    const withholdingDecisionPending = Boolean(invoice.withholdingDecisionPending);

    return (
        <>
            <div className={`${printStyles.screenRoot} space-y-8 p-8 max-w-[1000px] mx-auto`}>
            {/* Header Actions */}
            <div className="flex flex-wrap items-center justify-between gap-3 no-print">
                <div className="flex items-center space-x-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/ap/invoices')}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div className="flex items-center space-x-2">
                        <h1 className="text-2xl font-bold tracking-tight">Invoice {invoice.invoiceNumber}</h1>
                        {getStatusBadge(invoice)}
                    </div>
                </div>
                <div className="flex flex-wrap gap-2">
                    {invoice.status === 'Draft' &&
                        hasAnyPermission(['Finance.AP.Invoices.Edit', 'Finance.AP.Invoices.Write']) &&
                        <Button variant="outline" size="sm" onClick={() => router.push(`/finance/ap/invoices/${invoice.id}/edit`)}>Edit invoice</Button>}
                    {invoice.status === 'Draft' && hasAnyPermission(['Finance.AP.Invoices.Delete', 'Finance.AP.Invoices.Write']) && (
                        <Button variant="destructive" size="sm" onClick={() => setShowDeleteConfirmation(true)}>
                            <Trash2 className="mr-2 h-4 w-4" /> Delete draft
                        </Button>
                    )}
                    <InvoiceDistribution invoiceId={invoice.id} />
                    <Button variant="outline" size="sm" onClick={printApInvoiceDocument}>
                        <Printer className="mr-2 h-4 w-4" /> Print
                    </Button>
                    {invoice.status === 'Draft'
                        && hasBudgetLines
                        && hasAnyPermission(['Finance.AP.Invoices.Edit', 'Finance.AP.Invoices.Write']) && (
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={() => refreshBudgetMutation.mutate()}
                            disabled={refreshBudgetMutation.isPending}
                        >
                            {refreshBudgetMutation.isPending
                                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                : <RefreshCw className="mr-2 h-4 w-4" />}
                            Refresh budget
                        </Button>
                    )}
                    {invoice.status === 'Draft' && hasAnyPermission(['Finance.AP.Invoices.SubmitForApproval', 'Finance.AP.Invoices.Approve']) && (
                        <Button
                            size="sm"
                            variant="outline"
                            onClick={() => submitInvoiceMutation.mutate(invoice.id)}
                            disabled={!workflowVisibility.known || submitInvoiceMutation.isPending || isMatchReadinessLoading || !mandatoryMatchReady || !budgetReady || taxReviewPending || withholdingDecisionPending}
                            title={withholdingDecisionPending ? 'Choose Yes or No for withholding in Edit invoice.' : taxReviewPending ? 'Complete tax review in Edit invoice before submission.' : !mandatoryMatchReady
                                ? 'Resolve the mandatory three-way match before submission.'
                                : !budgetReady
                                    ? 'Refresh dimension-aware budget evidence before submission.'
                                    : undefined}
                        >
                            {submitInvoiceMutation.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CheckCircle className="mr-2 h-4 w-4" />}
                            {workflowVisibility.direct ? (invoice.isOpeningBalance ? 'Complete' : 'Post') : 'Submit for Approval'}
                        </Button>
                    )}
                    {invoice.status === 'PendingApproval' && invoice.approvalRequired !== false && workflowVisibility.showApprovalControls && hasPermission('Finance.AP.Invoices.Approve') && workflowSummary?.canCurrentUserApprove === true && (
                        <Button
                            size="sm"
                            onClick={() => approveInvoiceMutation.mutate(invoice.id)}
                            disabled={approveInvoiceMutation.isPending || isMatchReadinessLoading || !mandatoryMatchReady || taxReviewPending || withholdingDecisionPending}
                            title={withholdingDecisionPending ? 'Choose Yes or No for withholding in Edit invoice.' : taxReviewPending ? 'Tax review must be completed before approval.' : !mandatoryMatchReady ? 'Resolve the mandatory three-way match before approval.' : undefined}
                        >
                            {approveInvoiceMutation.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CheckCircle className="mr-2 h-4 w-4" />}
                            Approve
                        </Button>
                    )}
                    {invoice.isOpeningBalance && invoice.status === 'Approved' && !invoice.journalEntryId && hasPermission('Finance.AP.Invoices.Post') && (
                        <Button size="sm" onClick={() => postOpeningBalanceMutation.mutate(invoice.id)} disabled={postOpeningBalanceMutation.isPending}>
                            {postOpeningBalanceMutation.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CheckCircle className="mr-2 h-4 w-4" />}
                            Post opening balance
                        </Button>
                    )}
                    {(invoice.status === 'Approved' || invoice.status === 'PartiallyPaid') && invoice.balanceAmount > 0 && (!invoice.isOpeningBalance || Boolean(invoice.journalEntryId)) && (
                        <Button size="sm" onClick={() => router.push(`/finance/ap/payments/create?supplierId=${invoice.businessPartnerId}&invoiceId=${invoice.id}`)}>
                            <CreditCard className="mr-2 h-4 w-4" /> Schedule Payment
                        </Button>
                    )}
                    {(invoice.status === 'Approved' || invoice.status === 'Overdue') && hasPermission('Finance.AP.Invoices.Void') && (
                        <Button variant="destructive" size="sm" onClick={() => voidInvoiceMutation.mutate(invoice.id)} disabled={voidInvoiceMutation.isPending}>
                            {voidInvoiceMutation.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Ban className="mr-2 h-4 w-4" />}
                            Void
                        </Button>
                    )}
                </div>
            </div>

            {invoice.purchaseOrderId && !invoice.isOpeningBalance && (
                <>
                    <InvoiceThreeWayMatchControl
                        invoiceId={invoice.id}
                        canEvaluate={hasAnyPermission([
                            'Finance.AP.Invoices.Edit',
                            'Finance.AP.Invoices.Write',
                            'Finance.AP.Invoices.Approve',
                        ])}
                    />
                    <InvoiceMatchExceptionControl invoiceId={invoice.id} />
                </>
            )}

            <div className="no-print">
                {workflowError && <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-900">
                    {workflowError} <Button variant="link" size="sm" onClick={() => void refreshWorkflow()}>Retry</Button>
                </div>}
                {taxReviewPending &&
                    <div role="status" className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                        Tax review pending. Open Edit invoice and select the tax treatment for each landed-cost line before continuing. The displayed total is before any unreviewed tax.
                    </div>}
                {withholdingDecisionPending && <div role="status" className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">Withholding choice pending. Open Edit invoice and select Yes or No before continuing.</div>}
                {invoice.isOpeningBalance && invoice.status === 'Approved' && !invoice.journalEntryId && (
                    <div role="status" className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                        Approval is complete, but the AP opening balance is not yet posted. Use <strong>Post opening balance</strong>; do not recreate it in a GL batch.
                    </div>
                )}
                {!invoice.isOpeningBalance && <ActualLandedCostSummary invoiceId={invoice.id} purchaseOrderId={invoice.purchaseOrderId} />}
                <SourceDocumentDimensionEvidence evidence={invoice.financeDimensions} />
            </div>

            <Card className="print:shadow-none print:border-none">
                <CardHeader className="flex flex-row justify-between items-start border-b pb-8">
                    <div className="space-y-2">
                        <div className="flex items-center space-x-2">
                            <span className="text-xl font-bold">{invoice.supplierName}</span>
                        </div>
                        {invoice.supplierInvoiceNumber && (
                            <p className="text-sm text-muted-foreground font-medium flex items-center mt-2">
                                <FileText className="h-4 w-4 mr-1" />
                                Supplier Ref: {invoice.supplierInvoiceNumber}
                            </p>
                        )}
                    </div>
                    <div className="text-right space-y-1">
                        <h2 className="text-3xl font-bold text-gray-200 uppercase tracking-widest">BILL</h2>
                        <div className="flex justify-end space-x-4 pt-4">
                            <div className="text-left">
                                <p className="text-xs text-muted-foreground uppercase font-bold">System Ref #</p>
                                <p className="font-medium">{invoice.invoiceNumber}</p>
                            </div>
                            <div className="text-left">
                                <p className="text-xs text-muted-foreground uppercase font-bold">Date</p>
                                <p className="font-medium">{format(new Date(invoice.invoiceDate), 'MMM dd, yyyy')}</p>
                            </div>
                            <div className="text-left">
                                <p className="text-xs text-muted-foreground uppercase font-bold">Due Date</p>
                                <p className="font-medium">{invoice.dueDate ? format(new Date(invoice.dueDate), 'MMM dd, yyyy') : '-'}</p>
                            </div>
                        </div>
                    </div>
                </CardHeader>
                <CardContent className="pt-8 space-y-8">
                    {/* Bill From */}
                    <div className="grid grid-cols-2 gap-8">
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Bill From</p>
                            <p className="font-bold text-lg">{invoice.supplierName}</p>
                            <p className="text-sm text-muted-foreground mt-1">
                                Business Partner ID: {invoice.businessPartnerId}
                            </p>
                        </div>
                        <div className="text-right">
                            {/* Workflow info could go here */}
                            {invoice.approvalRequired !== false && invoice.approvedByUserId && (
                                <div className="mt-4">
                                    <p className="text-xs text-muted-foreground uppercase font-bold">Approved By</p>
                                    <p className="text-sm font-medium">{invoice.approvedByUserId}</p>
                                    {invoice.approvedAt && <p className="text-xs text-muted-foreground">{format(new Date(invoice.approvedAt), 'MMM dd, yyyy HH:mm')}</p>}
                                </div>
                            )}
                        </div>
                    </div>

                    {/* Line Items Table */}
                    <div className="rounded-md border">
                        <div className="grid grid-cols-12 gap-4 p-4 bg-muted/50 text-xs font-bold uppercase text-muted-foreground border-b">
                            <div className="col-span-2">Type</div>
                            <div className="col-span-4">Description / Item</div>
                            <div className="col-span-2 text-right">Qty</div>
                            <div className="col-span-2 text-right">Price</div>
                            <div className="col-span-2 text-right">Amount</div>
                        </div>
                        {invoice.lineItems?.map((item, index) => (
                            <div key={item.id || index} className="grid grid-cols-12 gap-4 p-4 border-b last:border-0 text-sm items-center">
                                <div className="col-span-2">
                                    <Badge variant="secondary" className="font-normal text-xs">{item.lineItemType || 'Expense'}</Badge>
                                </div>
                                <div className="col-span-4 space-y-1">
                                    <p className="font-medium">{item.description}</p>
                                    {item.glAccountId && <p className="text-xs text-muted-foreground">GL: {item.glAccountId}</p>}
                                    {item.inventoryItemId && <p className="text-xs text-muted-foreground">Item ID: {item.inventoryItemId}</p>}
                                </div>
                                <div className="col-span-2 text-right">{item.quantity} {item.unit}</div>
                                <div className="col-span-2 text-right">{formatCurrency(item.unitPrice, invoice.currencyCode)}</div>
                                <div className="col-span-2 text-right font-medium">{formatCurrency(item.lineTotal || (item.quantity * item.unitPrice), invoice.currencyCode)}</div>
                            </div>
                        ))}
                        {(!invoice.lineItems || invoice.lineItems.length === 0) && (
                            <div className="p-4 text-center text-muted-foreground text-sm">
                                No line items found.
                            </div>
                        )}
                    </div>

                    {/* Totals */}
                    <div className="flex justify-end">
                        <div className="w-1/3 space-y-2">
                            <div className="flex justify-between text-sm">
                                <span className="text-muted-foreground">Subtotal</span>
                                <span>{formatCurrency(invoice.totalAmount - (invoice.taxAmount || 0), invoice.currencyCode)}</span>
                            </div>
                            {(invoice.taxAmount || 0) > 0 && (
                                <div className="flex justify-between text-sm">
                                    <span className="text-muted-foreground">Tax</span>
                                    <span>{formatCurrency(invoice.taxAmount || 0, invoice.currencyCode)}</span>
                                </div>
                            )}
                            <Separator className="my-2" />
                            <div className="flex justify-between font-bold text-lg">
                                <span>Total</span>
                                <span>{formatCurrency(invoice.totalAmount, invoice.currencyCode)}</span>
                            </div>
                            <div className="flex justify-between text-sm text-muted-foreground pt-1">
                                <span>Amount Paid</span>
                                <span>-{formatCurrency(invoice.paidAmount, invoice.currencyCode)}</span>
                            </div>
                            <div className="flex justify-between font-bold text-lg pt-2 border-t">
                                <span>Balance Due</span>
                                <span className={invoice.balanceAmount > 0 ? 'text-red-600' : 'text-green-600'}>{formatCurrency(invoice.balanceAmount, invoice.currencyCode)}</span>
                            </div>
                        </div>
                    </div>

                    {/* Notes */}
                    {invoice.notes && (
                        <div className="pt-8 border-t">
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Notes</p>
                            <p className="text-sm text-muted-foreground">{invoice.notes}</p>
                        </div>
                    )}
                </CardContent>
            </Card>
            <ConfirmationDialog
                open={showDeleteConfirmation}
                onOpenChange={setShowDeleteConfirmation}
                title="Delete draft vendor invoice?"
                description={`${invoice.invoiceNumber} will be permanently removed. Posted or submitted invoices cannot be deleted.`}
                confirmText="Delete draft"
                variant="destructive"
                isLoading={deleteInvoiceMutation.isPending}
                onConfirm={async () => { try { await deleteInvoiceMutation.mutateAsync(); } catch { return false; } }}
                maxWidth="500px"
            />
            </div>
            <ApInvoicePrintDocument
                invoice={invoice}
                tenantName={currentTenant?.name}
                tenantCode={currentTenant?.code || currentTenantCode}
            />
        </>
    );
}

function InvoiceDetailsSkeleton() {
    return (
        <div className="space-y-8 p-8 max-w-[1000px] mx-auto">
            <div className="flex justify-between">
                <Skeleton className="h-10 w-32" />
                <Skeleton className="h-10 w-64" />
            </div>
            <Skeleton className="h-[800px] w-full" />
        </div>
    )
}
