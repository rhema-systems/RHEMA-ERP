'use client';

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
    Loader2
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
import { format } from 'date-fns';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { workflowApiService } from '@/services/workflow-api.service';
import type { WorkflowEntitySummaryDto } from '@/types/workflow';

export default function VendorInvoiceDetailsPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;
    const { toast } = useToast();
    const queryClient = useQueryClient();
    const { hasPermission, hasAnyPermission } = useAuth();
    const [workflowSummary, setWorkflowSummary] = useState<WorkflowEntitySummaryDto | null>(null);

    const { data: invoice, isLoading } = useQuery({
        queryKey: ['vendor-invoice', id],
        queryFn: () => accountsPayableService.getInvoice(id),
    });

    useQuery({
        queryKey: ['vendor-invoice-workflow-summary', id],
        queryFn: async () => {
            try {
                const summary = await workflowApiService.getWorkflowEntitySummary('VendorInvoice', id);
                setWorkflowSummary(summary);
                return summary;
            } catch {
                setWorkflowSummary(null);
                return null;
            }
        },
    });

    const voidInvoiceMutation = useMutation({
        mutationFn: (id: string) => accountsPayableService.voidInvoice(id, 'Voided by user'),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['vendor-invoice', id] });
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

    const approveInvoiceMutation = useMutation({
        mutationFn: (id: string) => accountsPayableService.approveInvoice(id, 'Approved'),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['vendor-invoice', id] });
            toast({
                title: 'Success',
                description: 'Vendor invoice approved successfully',
            });
        },
        onError: (error: any) => {
            toast({
                title: 'Error',
                description: error.message || 'Failed to approve vendor invoice',
                variant: 'destructive',
            });
        },
    });

    const submitInvoiceMutation = useMutation({
        mutationFn: (invoiceId: string) => accountsPayableService.submitInvoiceForApproval(invoiceId),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['vendor-invoice', id] });
            toast({ title: 'Success', description: 'Vendor invoice submitted for approval.' });
        },
        onError: (error: any) => {
            toast({ title: 'Error', description: error.message || 'Failed to submit for approval', variant: 'destructive' });
        },
    });

    if (isLoading) {
        return <InvoiceDetailsSkeleton />;
    }

    if (!invoice) {
        return (
            <div className="p-8 text-center">
                <h2 className="text-xl font-semibold">Vendor Invoice not found</h2>
                <Button variant="link" onClick={() => router.push('/finance/ap/invoices')}>
                    Return to list
                </Button>
            </div>
        );
    }

    const getStatusBadge = (status: string) => {
        switch (status) {
            case 'Draft': return <Badge variant="secondary">Draft</Badge>;
            case 'PendingApproval': return <Badge className="bg-yellow-600">Pending Approval</Badge>;
            case 'Approved': return <Badge className="bg-blue-600">Approved</Badge>;
            case 'PartiallyPaid': return <Badge className="bg-indigo-600">Partially Paid</Badge>;
            case 'Paid': return <Badge className="bg-green-600">Paid</Badge>;
            case 'Overdue': return <Badge variant="destructive">Overdue</Badge>;
            case 'Voided': return <Badge variant="outline" className="text-muted-foreground">Voided</Badge>;
            case 'Rejected': return <Badge variant="destructive">Rejected</Badge>;
            case 'OnHold': return <Badge variant="secondary" className="bg-orange-500">On Hold</Badge>;
            default: return <Badge variant="secondary">{status}</Badge>;
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1000px] mx-auto">
            {/* Header Actions */}
            <div className="flex items-center justify-between no-print">
                <div className="flex items-center space-x-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/ap/invoices')}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div className="flex items-center space-x-2">
                        <h1 className="text-2xl font-bold tracking-tight">Invoice {invoice.invoiceNumber}</h1>
                        {getStatusBadge(invoice.status)}
                    </div>
                </div>
                <div className="flex space-x-2">
                    <Button variant="outline" size="sm" onClick={() => window.print()}>
                        <Printer className="mr-2 h-4 w-4" /> Print
                    </Button>
                    {invoice.status === 'Draft' && hasAnyPermission(['Finance.AP.Invoices.SubmitForApproval', 'Finance.AP.Invoices.Approve']) && (
                        <Button size="sm" variant="outline" onClick={() => submitInvoiceMutation.mutate(invoice.id)} disabled={submitInvoiceMutation.isPending}>
                            {submitInvoiceMutation.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CheckCircle className="mr-2 h-4 w-4" />}
                            Submit for Approval
                        </Button>
                    )}
                    {invoice.status === 'PendingApproval' && hasPermission('Finance.AP.Invoices.Approve') && (workflowSummary?.canCurrentUserApprove ?? true) && (
                        <Button size="sm" onClick={() => approveInvoiceMutation.mutate(invoice.id)} disabled={approveInvoiceMutation.isPending}>
                            {approveInvoiceMutation.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CheckCircle className="mr-2 h-4 w-4" />}
                            Approve
                        </Button>
                    )}
                    {(invoice.status === 'Approved' || invoice.status === 'PartiallyPaid') && invoice.balanceAmount > 0 && (
                        <Button size="sm" onClick={() => router.push(`/finance/ap/payments/create?supplierId=${invoice.supplierId}&invoiceId=${invoice.id}`)}>
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
                                Supplier ID: {invoice.supplierId}
                            </p>
                        </div>
                        <div className="text-right">
                            {/* Workflow info could go here */}
                            {invoice.approvedByUserId && (
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
                                <div className="col-span-2 text-right">{formatCurrency(item.unitPrice)}</div>
                                <div className="col-span-2 text-right font-medium">{formatCurrency(item.lineTotal || (item.quantity * item.unitPrice))}</div>
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
                                <span>{formatCurrency(invoice.totalAmount - (invoice.taxAmount || 0))}</span>
                            </div>
                            {(invoice.taxAmount || 0) > 0 && (
                                <div className="flex justify-between text-sm">
                                    <span className="text-muted-foreground">Tax</span>
                                    <span>{formatCurrency(invoice.taxAmount || 0)}</span>
                                </div>
                            )}
                            <Separator className="my-2" />
                            <div className="flex justify-between font-bold text-lg">
                                <span>Total</span>
                                <span>{formatCurrency(invoice.totalAmount)}</span>
                            </div>
                            <div className="flex justify-between text-sm text-muted-foreground pt-1">
                                <span>Amount Paid</span>
                                <span>-{formatCurrency(invoice.paidAmount)}</span>
                            </div>
                            <div className="flex justify-between font-bold text-lg pt-2 border-t">
                                <span>Balance Due</span>
                                <span className={invoice.balanceAmount > 0 ? 'text-red-600' : 'text-green-600'}>{formatCurrency(invoice.balanceAmount)}</span>
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
        </div>
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
