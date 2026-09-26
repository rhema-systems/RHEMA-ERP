'use client';

import { useState, useEffect, use } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Check, Truck, FileText, Calendar, DollarSign, Globe, Clipboard, Printer, Send, XCircle, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { financePurchaseOrderService, FinancePurchaseOrder } from '@/services/financePurchaseOrderService';
import { formatCurrency } from '@/lib/utils';
import { useToast } from '@/components/ui/use-toast';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { format } from 'date-fns';
import { useAuth } from '@/hooks/use-auth';
import { useWorkflowSummary } from '@/hooks/useWorkflowSummary';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Textarea } from '@/components/ui/textarea';

const FINANCE_PO_APPROVER_ROLES = [
    'SuperAdmin',
    'TenantAdmin',
    'Manager',
    'Accounts Officer',
    'Senior Accountant',
    'Finance Manager',
    'Financial Controller',
];

export default function PurchaseOrderDetailsPage({ params }: { params: Promise<{ id: string }> }) {
    const router = useRouter();
    const { toast } = useToast();
    const { hasAnyRole } = useAuth();
    const unwrappedParams = use(params);
    const id = unwrappedParams.id;
    const [po, setPo] = useState<FinancePurchaseOrder | null>(null);
    const [loading, setLoading] = useState(true);
    const [action, setAction] = useState<'submit' | 'approve' | 'reject' | null>(null);
    const canApproveRole = hasAnyRole(FINANCE_PO_APPROVER_ROLES);
    const workflow = useWorkflowSummary({ entityType: 'FinancePurchaseOrder', entityId: id });
    const [rejectOpen, setRejectOpen] = useState(false);
    const [rejectReason, setRejectReason] = useState('');

    useEffect(() => {
        loadPO();
    }, [id]);

    const loadPO = async () => {
        try {
            const data = await financePurchaseOrderService.getPurchaseOrderById(id);
            setPo(data);
        } catch (error) {
            toast({ title: 'Error', description: 'Failed to load PO', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    const handleApprove = async () => {
        if (!canApproveRole) {
            toast({
                title: 'Not authorized',
                description: 'Finance PO approval requires a finance approver role.',
                variant: 'destructive',
            });
            return;
        }

        try {
            setAction('approve');
            const updated = await financePurchaseOrderService.approvePurchaseOrder(id);
            const stillPendingApproval = updated.status === 9 || updated.status === 'PendingApproval' || updated.status === 'Pending Approval';

            toast({
                title: stillPendingApproval ? 'Review recorded' : 'PO approved',
                description: stillPendingApproval
                    ? 'The PO has moved to the next approval step.'
                    : 'The PO approval workflow is complete.',
            });
            setPo(updated);
            router.push('/finance/ap/purchase-orders');
        } catch (error: any) {
            toast({ title: 'Error', description: error.message || 'Failed to approve PO', variant: 'destructive' });
        } finally {
            setAction(null);
        }
    };

    const handleSubmitForApproval = async () => {
        try {
            setAction('submit');
            const saved = await financePurchaseOrderService.submitPurchaseOrderForApproval(id);
            toast({ title: saved.approvalRequired === false ? 'PO finalized' : 'Submitted',
                description: saved.approvalRequired === false ? 'The PO is ready for receiving.' : 'PO submitted for approval.' });
            await loadPO();
            await workflow.refresh();
        } catch (error: any) {
            toast({ title: 'Error', description: error.message || 'Failed to submit PO for approval', variant: 'destructive' });
        } finally {
            setAction(null);
        }
    };

    const handleReject = async () => {
        if (!canApproveRole) {
            toast({
                title: 'Not authorized',
                description: 'Finance PO rejection requires a finance approver role.',
                variant: 'destructive',
            });
            return;
        }

        const reason = rejectReason.trim();
        if (!reason) return false;

        try {
            setAction('reject');
            const updated = await financePurchaseOrderService.rejectPurchaseOrder(id, reason.trim());
            toast({ title: 'Rejected', description: 'PO rejected and returned to the creator.' });
            setPo(updated);
            router.push('/finance/ap/purchase-orders');
        } catch (error: any) {
            toast({ title: 'Error', description: error.message || 'Failed to reject PO', variant: 'destructive' });
            return false;
        } finally {
            setAction(null);
        }
    };

    const handlePrint = () => {
        const cleanup = () => {
            document.body.classList.remove('printing-finance-po');
            window.removeEventListener('afterprint', cleanup);
        };

        document.body.classList.add('printing-finance-po');
        window.addEventListener('afterprint', cleanup);
        window.print();
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <div className="text-lg font-medium text-muted-foreground animate-pulse">Loading purchase order details...</div>
            </div>
        );
    }
    
    if (!po) return <div className="p-8 text-center text-lg font-medium">PO not found</div>;

    const isDraft = po.status === 1 || po.status === 'Draft';
    const isPendingApproval = po.status === 9 || po.status === 'PendingApproval' || po.status === 'Pending Approval';
    const isRejected = po.status === 10 || po.status === 'Rejected';
    const canSubmitForApproval = isDraft || isRejected;
    const canApprove = canApproveRole && isPendingApproval && workflow.visibility.showApprovalControls &&
        workflow.summary?.canCurrentUserApprove === true;
    const canReject = canApprove;
    const canReceive = po.status === 2 || po.status === 'Approved' || po.status === 3 || po.status === 'PartiallyReceived';

    const getStatusBadge = (status: number | string) => {
        const isDraft = status === 1 || status === 'Draft';
        const isApproved = status === 2 || status === 'Approved';
        const isPartiallyReceived = status === 3 || status === 'PartiallyReceived';
        const isReceived = status === 4 || status === 'Received';
        const isPartiallyInvoiced = status === 5 || status === 'PartiallyInvoiced';
        const isInvoiced = status === 6 || status === 'Invoiced';
        const isClosed = status === 7 || status === 'Closed';
        const isCancelled = status === 8 || status === 'Cancelled';
        const isPendingApproval = status === 9 || status === 'PendingApproval' || status === 'Pending Approval';
        const isRejected = status === 10 || status === 'Rejected';

        if (isDraft) return <Badge variant="secondary" className="px-3 py-1 text-sm bg-slate-700/50 text-slate-300">Draft</Badge>;
        if (isPendingApproval) return <Badge className="px-3 py-1 text-sm bg-amber-600/20 text-amber-400 border border-amber-500/30">Pending Approval</Badge>;
        if (isApproved) return <Badge className="px-3 py-1 text-sm bg-blue-600/20 text-blue-400 border border-blue-500/30">{po.approvalRequired === false ? 'Ready for receiving' : 'Approved'}</Badge>;
        if (isPartiallyReceived) return <Badge className="px-3 py-1 text-sm bg-indigo-600/20 text-indigo-400 border border-indigo-500/30">Partially Received</Badge>;
        if (isReceived) return <Badge className="px-3 py-1 text-sm bg-green-600/20 text-green-400 border border-green-500/30">Received</Badge>;
        if (isPartiallyInvoiced) return <Badge className="px-3 py-1 text-sm bg-cyan-600/20 text-cyan-400 border border-cyan-500/30">Partially Invoiced</Badge>;
        if (isInvoiced) return <Badge className="px-3 py-1 text-sm bg-teal-600/20 text-teal-400 border border-teal-500/30">Invoiced</Badge>;
        if (isClosed) return <Badge variant="outline" className="px-3 py-1 text-sm text-slate-400">Closed</Badge>;
        if (isCancelled) return <Badge variant="destructive" className="px-3 py-1 text-sm">Cancelled</Badge>;
        if (isRejected) return <Badge variant="destructive" className="px-3 py-1 text-sm">Rejected</Badge>;
        return <Badge variant="secondary" className="px-3 py-1 text-sm">{String(status)}</Badge>;
    };

    const getStatusText = (status: number | string) => {
        if (status === 1 || status === 'Draft') return 'Draft';
        if (status === 2 || status === 'Approved') return po.approvalRequired === false ? 'Ready for receiving' : 'Approved';
        if (status === 3 || status === 'PartiallyReceived') return 'Partially Received';
        if (status === 4 || status === 'Received') return 'Received';
        if (status === 5 || status === 'PartiallyInvoiced') return 'Partially Invoiced';
        if (status === 6 || status === 'Invoiced') return 'Invoiced';
        if (status === 7 || status === 'Closed') return 'Closed';
        if (status === 8 || status === 'Cancelled') return 'Cancelled';
        if (status === 9 || status === 'PendingApproval' || status === 'Pending Approval') return 'Pending Approval';
        if (status === 10 || status === 'Rejected') return 'Rejected';
        return String(status);
    };

    // Calculations
    const totalTax = po.items?.reduce((sum, item) => sum + (item.taxAmount || 0), 0) || 0;
    const subTotal = po.totalAmount - totalTax;

    return (
        <>
        <ConfirmationDialog open={rejectOpen} onOpenChange={setRejectOpen} title="Reject purchase order"
            confirmText="Reject" variant="destructive" onConfirm={handleReject}
            confirmDisabled={!rejectReason.trim()} isLoading={action === 'reject'}>
            <Textarea aria-label="Rejection reason" placeholder="Reason for rejection" value={rejectReason}
                onChange={event => setRejectReason(event.target.value)} />
        </ConfirmationDialog>
        <div className="finance-po-no-print space-y-8 p-8 max-w-[1400px] mx-auto print:hidden">
            {/* Header Actions */}
            <div className="flex justify-between items-center no-print">
                <div className="flex items-center space-x-4">
                    <Button variant="ghost" onClick={() => router.push('/finance/ap/purchase-orders')} className="hover:bg-slate-800">
                        <ArrowLeft className="h-4 w-4 mr-2" /> Back to list
                    </Button>
                    <h1 className="text-3xl font-bold tracking-tight">PO: {po.orderNumber}</h1>
                    {getStatusBadge(po.status)}
                </div>
                <div className="space-x-2">
                    {workflow.error && <span role="alert" className="text-sm text-red-700">{workflow.error} <Button variant="link" onClick={() => void workflow.refresh()}>Retry</Button></span>}
                    {canSubmitForApproval && (
                        <Button onClick={handleSubmitForApproval} disabled={action !== null || !workflow.visibility.known} className="bg-amber-600 hover:bg-amber-700 font-medium px-5">
                            {action === 'submit' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
                            {!workflow.visibility.known ? 'Checking approval status…' : workflow.visibility.direct ? 'Finalize' : 'Submit for Approval'}
                        </Button>
                    )}
                    {canApprove && (
                        <Button onClick={handleApprove} disabled={action !== null} className="bg-blue-600 hover:bg-blue-700 font-medium px-5">
                            {action === 'approve' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Check className="mr-2 h-4 w-4" />}
                            Approve PO
                        </Button>
                    )}
                    {canReject && (
                        <Button variant="destructive" onClick={() => setRejectOpen(true)} disabled={action !== null} className="font-medium px-5">
                            {action === 'reject' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <XCircle className="mr-2 h-4 w-4" />}
                            Reject
                        </Button>
                    )}
                    {canReceive && (
                        <Button onClick={() => router.push(`/finance/ap/purchase-orders/${po.id}/receive`)} className="bg-green-600 hover:bg-green-700 font-medium px-5">
                            <Truck className="mr-2 h-4 w-4" /> Create GRV
                        </Button>
                    )}
                    <Button variant="outline" onClick={handlePrint} className="font-medium px-5">
                        <Printer className="mr-2 h-4 w-4" /> Print
                    </Button>
                </div>
            </div>

            {/* Two-Column Overview */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
                {/* Column 1 & 2: Main Details & Lines */}
                <div className="md:col-span-2 space-y-8">
                    {/* General & Vendor Information Card */}
                    <Card className="bg-slate-900/40 border-slate-800/80 backdrop-blur-md">
                        <CardHeader className="border-b border-slate-800/60 pb-4">
                            <CardTitle className="text-lg font-semibold flex items-center text-slate-200">
                                <Clipboard className="h-5 w-5 mr-2 text-blue-500" /> General Information
                            </CardTitle>
                        </CardHeader>
                        <CardContent className="pt-6">
                            <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
                                <div className="space-y-1">
                                    <span className="text-xs text-muted-foreground uppercase tracking-wider font-semibold">Vendor / Supplier</span>
                                    <p className="text-base font-bold text-slate-100">{po.vendorName || 'Not Specified'}</p>
                                    <p className="text-xs text-muted-foreground">ID: {po.businessPartnerId}</p>
                                </div>
                                <div className="space-y-1">
                                    <span className="text-xs text-muted-foreground uppercase tracking-wider font-semibold">System Reference</span>
                                    <p className="text-base font-medium text-slate-200">{po.orderNumber}</p>
                                </div>
                                <div className="space-y-1">
                                    <span className="text-xs text-muted-foreground uppercase tracking-wider font-semibold flex items-center">
                                        <Calendar className="h-3 w-3 mr-1 text-slate-400" /> Order Date
                                    </span>
                                    <p className="text-sm font-medium text-slate-200">
                                        {po.orderDate ? format(new Date(po.orderDate), 'MMMM dd, yyyy') : '-'}
                                    </p>
                                </div>
                                <div className="space-y-1">
                                    <span className="text-xs text-muted-foreground uppercase tracking-wider font-semibold flex items-center">
                                        <Calendar className="h-3 w-3 mr-1 text-slate-400" /> Expected Delivery
                                    </span>
                                    <p className="text-sm font-medium text-slate-200">
                                        {po.expectedDeliveryDate ? format(new Date(po.expectedDeliveryDate), 'MMMM dd, yyyy') : 'Immediate'}
                                    </p>
                                </div>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Lines Card */}
                    <Card className="bg-slate-900/40 border-slate-800/80 backdrop-blur-md">
                        <CardHeader className="border-b border-slate-800/60 pb-4">
                            <CardTitle className="text-lg font-semibold text-slate-200">PO Items / Lines</CardTitle>
                        </CardHeader>
                        <CardContent className="pt-0 px-0">
                            <div className="overflow-x-auto">
                                <table className="w-full text-sm text-left">
                                    <thead className="border-b border-slate-800 bg-slate-950/40 text-slate-300 font-medium">
                                        <tr>
                                            <th className="p-4 pl-6">Type</th>
                                            <th className="p-4">Description</th>
                                            <th className="p-4 text-right">Ordered</th>
                                            <th className="p-4 text-right">Received</th>
                                            <th className="p-4 text-right">Unit Price</th>
                                            <th className="p-4 text-right">Tax</th>
                                            <th className="p-4 text-right pr-6">Line Total</th>
                                        </tr>
                                    </thead>
                                    <tbody className="divide-y divide-slate-800/40 text-slate-300">
                                        {po.items?.map((item) => (
                                            <tr key={item.id} className="hover:bg-slate-900/20 transition-colors">
                                                <td className="p-4 pl-6">
                                                    <Badge variant="outline" className="font-normal border-slate-700/50 bg-slate-850/30 text-slate-300">
                                                        {item.lineType === 1 ? 'Inventory' : 'GL Account'}
                                                    </Badge>
                                                </td>
                                                <td className="p-4">
                                                    <p className="font-medium text-slate-200">{item.description}</p>
                                                    {item.glAccountId && (
                                                        <span className="text-[11px] text-muted-foreground block font-mono">
                                                            GL: {item.glAccountId}
                                                        </span>
                                                    )}
                                                </td>
                                                <td className="p-4 text-right font-mono font-medium">{item.orderedQuantity}</td>
                                                <td className="p-4 text-right font-mono text-slate-400">{item.receivedQuantity || 0}</td>
                                                <td className="p-4 text-right font-mono">{formatCurrency(item.unitPrice, po.currencyCode)}</td>
                                                <td className="p-4 text-right font-mono text-xs text-muted-foreground">
                                                    {item.taxRate && item.taxRate > 0 ? (
                                                        <span>{item.taxRate}% ({formatCurrency(item.taxAmount || 0, po.currencyCode)})</span>
                                                    ) : (
                                                        '-'
                                                    )}
                                                </td>
                                                <td className="p-4 text-right font-mono font-semibold text-slate-200 pr-6">
                                                    {formatCurrency(item.lineTotal || (item.orderedQuantity * item.unitPrice), po.currencyCode)}
                                                </td>
                                            </tr>
                                        ))}
                                        {(!po.items || po.items.length === 0) && (
                                            <tr>
                                                <td colSpan={7} className="p-8 text-center text-muted-foreground">
                                                    No lines found for this purchase order.
                                                </td>
                                            </tr>
                                        )}
                                    </tbody>
                                </table>
                            </div>
                        </CardContent>
                    </Card>
                </div>

                {/* Column 3: Multi-Currency & Totals Summary Panel */}
                <div className="space-y-8">
                    {/* Financial Summary Card */}
                    <Card className="bg-slate-900/40 border-slate-800/80 backdrop-blur-md overflow-hidden relative">
                        <div className="absolute top-0 right-0 p-6 opacity-5">
                            <DollarSign className="h-32 w-32" />
                        </div>
                        <CardHeader className="border-b border-slate-800/60 pb-4">
                            <CardTitle className="text-lg font-semibold flex items-center text-slate-200">
                                <DollarSign className="h-5 w-5 mr-2 text-emerald-500" /> Summary Totals
                            </CardTitle>
                        </CardHeader>
                        <CardContent className="pt-6 space-y-4">
                            <div className="flex justify-between text-sm text-slate-300">
                                <span>Subtotal</span>
                                <span className="font-mono">{formatCurrency(subTotal, po.currencyCode)}</span>
                            </div>
                            <div className="flex justify-between text-sm text-slate-300">
                                <span>Estimated Tax</span>
                                <span className="font-mono text-slate-400">+{formatCurrency(totalTax, po.currencyCode)}</span>
                            </div>
                            <Separator className="border-slate-800/80" />
                            <div className="flex justify-between items-baseline">
                                <span className="text-base font-bold text-slate-200">Grand Total</span>
                                <span className="text-2xl font-bold text-emerald-400 font-mono">
                                    {formatCurrency(po.totalAmount, po.currencyCode)}
                                </span>
                            </div>

                            {/* Multi-Currency Locking Panel */}
                            {po.currencyCode && po.currencyCode !== 'GHS' && po.exchangeRate && po.exchangeRate !== 1 && (
                                <div className="mt-6 pt-6 border-t border-slate-800/80 space-y-3 bg-slate-950/20 -mx-6 px-6 pb-2">
                                    <div className="flex items-center space-x-2 text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                                        <Globe className="h-3.5 w-3.5 text-blue-400" />
                                        <span>Multi-Currency Details</span>
                                    </div>
                                    <div className="flex justify-between text-xs text-slate-400">
                                        <span>Transaction Currency</span>
                                        <span className="font-bold text-slate-300">{po.currencyCode}</span>
                                    </div>
                                    <div className="flex justify-between text-xs text-slate-400">
                                        <span>Locked Exchange Rate</span>
                                        <span className="font-mono text-slate-300">{po.exchangeRate.toFixed(4)} GHS</span>
                                    </div>
                                    <div className="flex justify-between text-xs font-semibold text-slate-300 border-t border-slate-800/40 pt-2">
                                        <span>Base Currency Equivalent</span>
                                        <span className="font-mono text-blue-400">
                                            {formatCurrency(po.totalAmount * po.exchangeRate, 'GHS')}
                                        </span>
                                    </div>
                                </div>
                            )}
                        </CardContent>
                    </Card>

                    {/* Remarks/Notes Card */}
                    {po.remarks && (
                        <Card className="bg-slate-900/40 border-slate-800/80 backdrop-blur-md">
                            <CardHeader className="border-b border-slate-800/60 pb-3">
                                <CardTitle className="text-sm font-semibold flex items-center text-slate-300">
                                    <FileText className="h-4 w-4 mr-2 text-slate-400" /> Remarks & Internal Notes
                                </CardTitle>
                            </CardHeader>
                            <CardContent className="pt-4">
                                <p className="text-sm text-slate-300 leading-relaxed whitespace-pre-wrap">
                                    {po.remarks}
                                </p>
                            </CardContent>
                        </Card>
                    )}
                </div>
            </div>
        </div>
        <section className="finance-po-print-root hidden print:block bg-white p-8 text-black">
            <div className="mb-8 flex items-start justify-between border-b-2 border-black pb-4">
                <div>
                    <h1 className="text-3xl font-bold">Purchase Order</h1>
                    <p className="mt-1 text-sm text-slate-700">ERP System</p>
                </div>
                <div className="text-right">
                    <div className="text-2xl font-bold">{po.orderNumber}</div>
                    <div className="mt-1 text-sm uppercase tracking-wide">{getStatusText(po.status)}</div>
                </div>
            </div>

            <div className="mb-6 grid grid-cols-2 gap-6">
                <div className="rounded border border-slate-300 p-4">
                    <h2 className="mb-3 text-sm font-bold uppercase tracking-wide">Vendor / Supplier</h2>
                    <p className="text-base font-semibold">{po.vendorName || 'Not Specified'}</p>
                    <p className="mt-1 text-xs text-slate-600">ID: {po.businessPartnerId}</p>
                </div>
                <div className="rounded border border-slate-300 p-4">
                    <h2 className="mb-3 text-sm font-bold uppercase tracking-wide">Order Information</h2>
                    <div className="grid grid-cols-2 gap-3 text-sm">
                        <div>
                            <div className="text-xs font-semibold uppercase text-slate-600">Order Date</div>
                            <div>{po.orderDate ? format(new Date(po.orderDate), 'dd MMM yyyy') : '-'}</div>
                        </div>
                        <div>
                            <div className="text-xs font-semibold uppercase text-slate-600">Expected Delivery</div>
                            <div>{po.expectedDeliveryDate ? format(new Date(po.expectedDeliveryDate), 'dd MMM yyyy') : 'Immediate'}</div>
                        </div>
                        <div>
                            <div className="text-xs font-semibold uppercase text-slate-600">Currency</div>
                            <div>{po.currencyCode || '-'}</div>
                        </div>
                        <div>
                            <div className="text-xs font-semibold uppercase text-slate-600">Exchange Rate</div>
                            <div>{po.exchangeRate ? `${po.exchangeRate.toFixed(4)} GHS` : '-'}</div>
                        </div>
                    </div>
                </div>
            </div>

            <table className="mb-6 w-full border-collapse text-sm">
                <thead>
                    <tr className="bg-slate-100">
                        <th className="border border-slate-300 p-2 text-left">Type</th>
                        <th className="border border-slate-300 p-2 text-left">Description</th>
                        <th className="border border-slate-300 p-2 text-right">Ordered</th>
                        <th className="border border-slate-300 p-2 text-right">Received</th>
                        <th className="border border-slate-300 p-2 text-right">Unit Price</th>
                        <th className="border border-slate-300 p-2 text-right">Tax</th>
                        <th className="border border-slate-300 p-2 text-right">Line Total</th>
                    </tr>
                </thead>
                <tbody>
                    {po.items?.map((item) => (
                        <tr key={`print-${item.id}`}>
                            <td className="border border-slate-300 p-2">{item.lineType === 1 ? 'Inventory' : 'GL Account'}</td>
                            <td className="border border-slate-300 p-2">
                                <div className="font-semibold">{item.description}</div>
                                {item.glAccountId && <div className="mt-1 text-xs text-slate-600">GL: {item.glAccountId}</div>}
                            </td>
                            <td className="border border-slate-300 p-2 text-right">{item.orderedQuantity}</td>
                            <td className="border border-slate-300 p-2 text-right">{item.receivedQuantity || 0}</td>
                            <td className="border border-slate-300 p-2 text-right">{formatCurrency(item.unitPrice, po.currencyCode)}</td>
                            <td className="border border-slate-300 p-2 text-right">
                                {item.taxRate && item.taxRate > 0 ? `${item.taxRate}% (${formatCurrency(item.taxAmount || 0, po.currencyCode)})` : '-'}
                            </td>
                            <td className="border border-slate-300 p-2 text-right font-semibold">
                                {formatCurrency(item.lineTotal || (item.orderedQuantity * item.unitPrice), po.currencyCode)}
                            </td>
                        </tr>
                    ))}
                    {(!po.items || po.items.length === 0) && (
                        <tr>
                            <td colSpan={7} className="border border-slate-300 p-4 text-center text-slate-600">No lines found for this purchase order.</td>
                        </tr>
                    )}
                </tbody>
            </table>

            <div className="ml-auto w-80 rounded border border-slate-300 p-4 text-sm">
                <div className="mb-2 flex justify-between">
                    <span>Subtotal</span>
                    <span>{formatCurrency(subTotal, po.currencyCode)}</span>
                </div>
                <div className="mb-3 flex justify-between">
                    <span>Estimated Tax</span>
                    <span>{formatCurrency(totalTax, po.currencyCode)}</span>
                </div>
                <div className="flex justify-between border-t border-slate-300 pt-3 text-base font-bold">
                    <span>Grand Total</span>
                    <span>{formatCurrency(po.totalAmount, po.currencyCode)}</span>
                </div>
                {po.currencyCode && po.currencyCode !== 'GHS' && po.exchangeRate && po.exchangeRate !== 1 && (
                    <div className="mt-3 border-t border-slate-300 pt-3 text-xs text-slate-700">
                        Base Currency Equivalent: {formatCurrency(po.totalAmount * po.exchangeRate, 'GHS')}
                    </div>
                )}
            </div>

            {po.remarks && (
                <div className="mt-8 rounded border border-slate-300 p-4">
                    <h2 className="mb-2 text-sm font-bold uppercase tracking-wide">Remarks</h2>
                    <p className="whitespace-pre-wrap text-sm">{po.remarks}</p>
                </div>
            )}

            <div className="mt-16 grid grid-cols-3 gap-10 text-center text-sm">
                <div className="border-t border-black pt-2">Prepared By</div>
                {po.approvalRequired !== false && <div className="border-t border-black pt-2">Reviewed By</div>}
                {po.approvalRequired !== false && <div className="border-t border-black pt-2">Approved By</div>}
            </div>
        </section>
        </>
    );
}

