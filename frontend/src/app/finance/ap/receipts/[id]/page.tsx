'use client';

import { use, useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
    ArrowLeft,
    Building2,
    CalendarDays,
    CheckCircle2,
    ExternalLink,
    FileText,
    Loader2,
    PackageCheck,
    Printer,
    Send,
    ShoppingCart,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { financePurchaseOrderService, FinancePurchaseOrder, FinancePurchaseOrderReceipt } from '@/services/financePurchaseOrderService';
import { formatCurrency } from '@/lib/utils';
import { useToast } from '@/components/ui/use-toast';

function formatDate(value?: string | null) {
    if (!value) return '-';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '-' : date.toLocaleDateString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
    });
}

function formatNumber(value: number) {
    return new Intl.NumberFormat('en-GH', { maximumFractionDigits: 2 }).format(value || 0);
}

function getReceiptWorkflowStatusName(receipt: FinancePurchaseOrderReceipt) {
    if (receipt.statusName) return receipt.statusName;
    if (typeof receipt.status === 'string') return receipt.status;

    switch (receipt.status) {
        case 1:
            return 'Draft';
        case 2:
            return 'PendingApproval';
        case 3:
            return 'Approved';
        case 4:
            return 'Rejected';
        case 5:
            return 'Cancelled';
        default:
            return 'Draft';
    }
}

function normalizeStatusLabel(value: string) {
    return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function isApprovedReceipt(receipt: FinancePurchaseOrderReceipt) {
    return getReceiptWorkflowStatusName(receipt).replace(/\s+/g, '').toLowerCase() === 'approved';
}

function getReceiptStatus(receipt: FinancePurchaseOrderReceipt) {
    const workflowStatus = getReceiptWorkflowStatusName(receipt);
    const workflowKey = workflowStatus.replace(/\s+/g, '').toLowerCase();

    if (workflowKey === 'draft') {
        return { label: 'Draft', className: 'bg-slate-600/10 text-slate-700 border-slate-200' };
    }

    if (workflowKey === 'pendingapproval') {
        return { label: 'Pending Approval', className: 'bg-amber-600/15 text-amber-700 border-amber-200' };
    }

    if (workflowKey === 'rejected') {
        return { label: 'Rejected', className: 'bg-red-600/15 text-red-700 border-red-200' };
    }

    if (workflowKey === 'cancelled') {
        return { label: 'Cancelled', className: 'bg-zinc-600/15 text-zinc-700 border-zinc-200' };
    }

    if (workflowKey !== 'approved') {
        return { label: normalizeStatusLabel(workflowStatus), className: 'bg-slate-600/10 text-slate-700 border-slate-200' };
    }

    if (receipt.vendorInvoiceId) {
        return { label: 'Converted To Invoice', className: 'bg-blue-600/15 text-blue-700 border-blue-200' };
    }

    const remaining = receipt.items.reduce((sum, item) => sum + Math.max(0, item.quantityReceived - (item.invoicedQuantity || 0)), 0);
    const invoiced = receipt.items.reduce((sum, item) => sum + (item.invoicedQuantity || 0), 0);

    if (remaining <= 0 && invoiced > 0) {
        return { label: 'Fully Matched', className: 'bg-emerald-600/15 text-emerald-700 border-emerald-200' };
    }

    if (invoiced > 0) {
        return { label: 'Partially Invoiced', className: 'bg-amber-600/15 text-amber-700 border-amber-200' };
    }

    return { label: 'Approved For Invoice', className: 'bg-emerald-600/15 text-emerald-700 border-emerald-200' };
}

export default function ReceiptDetailsPage({ params }: { params: Promise<{ id: string }> }) {
    const router = useRouter();
    const { toast } = useToast();
    const { id } = use(params);
    const [receipt, setReceipt] = useState<FinancePurchaseOrderReceipt | null>(null);
    const [purchaseOrder, setPurchaseOrder] = useState<FinancePurchaseOrder | null>(null);
    const [loading, setLoading] = useState(true);
    const [converting, setConverting] = useState(false);
    const [submittingApproval, setSubmittingApproval] = useState(false);

    useEffect(() => {
        void loadReceipt();
    }, [id]);

    const loadReceipt = async () => {
        try {
            setLoading(true);
            const data = await financePurchaseOrderService.getReceiptById(id);
            setReceipt(data);

            try {
                const linkedPo = await financePurchaseOrderService.getPurchaseOrderById(data.financePurchaseOrderId);
                setPurchaseOrder(linkedPo);
            } catch {
                setPurchaseOrder(null);
            }
        } catch (error) {
            toast({ title: 'Error', description: 'Failed to load receipt', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    const handleConvertToInvoice = async () => {
        if (!receipt || !isApprovedReceipt(receipt)) {
            toast({ title: 'Approval required', description: 'Approve the GRV before converting it to a vendor invoice.', variant: 'destructive' });
            return;
        }

        setConverting(true);
        try {
            const draftInvoice = await financePurchaseOrderService.convertToVendorInvoice(id);
            toast({ title: 'Draft invoice created', description: 'The GRV has been converted to an AP invoice.' });
            router.push(`/finance/ap/invoices/${draftInvoice.id}`);
        } catch (error: any) {
            toast({ title: 'Error', description: error.message || 'Failed to convert to invoice', variant: 'destructive' });
        } finally {
            setConverting(false);
        }
    };

    const handleSubmitForApproval = async () => {
        setSubmittingApproval(true);
        try {
            const data = await financePurchaseOrderService.submitReceiptForApproval(id);
            setReceipt(data);
            toast({ title: 'Submitted', description: 'The GRV has been submitted for approval.' });
        } catch (error: any) {
            toast({ title: 'Error', description: error.message || 'Failed to submit GRV for approval', variant: 'destructive' });
        } finally {
            setSubmittingApproval(false);
        }
    };

    const handlePrint = () => {
        const cleanup = () => {
            document.body.classList.remove('printing-finance-grv');
            window.removeEventListener('afterprint', cleanup);
        };

        document.body.classList.add('printing-finance-grv');
        window.addEventListener('afterprint', cleanup);
        window.print();
    };

    const poLinesById = useMemo(() => {
        return new Map((purchaseOrder?.items || []).map(item => [item.id, item]));
    }, [purchaseOrder]);

    if (loading) {
        return <div className="p-8 text-center text-muted-foreground">Loading goods receipt voucher...</div>;
    }

    if (!receipt) {
        return <div className="p-8 text-center text-muted-foreground">Receipt not found</div>;
    }

    const status = getReceiptStatus(receipt);
    const workflowStatusKey = getReceiptWorkflowStatusName(receipt).replace(/\s+/g, '').toLowerCase();
    const canSubmitForApproval = workflowStatusKey === 'draft';
    const hasUninvoiced = receipt.items.some(item => (item.quantityReceived - (item.invoicedQuantity || 0)) > 0);
    const canConvertToInvoice = isApprovedReceipt(receipt) && hasUninvoiced;
    const totalReceived = receipt.items.reduce((sum, item) => sum + item.quantityReceived, 0);
    const totalInvoiced = receipt.items.reduce((sum, item) => sum + (item.invoicedQuantity || 0), 0);
    const totalRemaining = receipt.items.reduce((sum, item) => sum + Math.max(0, item.quantityReceived - (item.invoicedQuantity || 0)), 0);
    const currencyCode = purchaseOrder?.currencyCode || 'GHS';
    const receiptValue = receipt.items.reduce((sum, item) => {
        const poLine = poLinesById.get(item.financePurchaseOrderItemId);
        return sum + item.quantityReceived * (poLine?.unitPrice || 0);
    }, 0);

    return (
        <>
        <div className="finance-grv-no-print mx-auto max-w-[1600px] space-y-6 p-8 print:hidden">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                <div className="flex min-w-0 flex-col gap-3 sm:flex-row sm:items-center">
                    <Button variant="ghost" onClick={() => router.push('/finance/ap/receipts')}>
                        <ArrowLeft className="mr-2 h-4 w-4" />
                        Back
                    </Button>
                    <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-3">
                            <h1 className="text-3xl font-bold tracking-tight">GRV: {receipt.receiptNumber}</h1>
                            <Badge variant="outline" className={status.className}>{status.label}</Badge>
                        </div>
                        <p className="mt-1 text-muted-foreground">
                            Goods receipt for {receipt.orderNumber || 'linked purchase order'} from {receipt.vendorName || 'supplier'}.
                        </p>
                    </div>
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button variant="outline" onClick={() => router.push(`/finance/ap/purchase-orders/${receipt.financePurchaseOrderId}`)}>
                        <ExternalLink className="mr-2 h-4 w-4" />
                        View PO
                    </Button>
                    <Button variant="outline" onClick={handlePrint}>
                        <Printer className="mr-2 h-4 w-4" />
                        Print
                    </Button>
                    {canSubmitForApproval && (
                        <Button onClick={handleSubmitForApproval} disabled={submittingApproval}>
                            {submittingApproval ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
                            Submit For Approval
                        </Button>
                    )}
                    {canConvertToInvoice && (
                        <Button onClick={handleConvertToInvoice} disabled={converting}>
                            {converting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileText className="mr-2 h-4 w-4" />}
                            Convert to Vendor Invoice
                        </Button>
                    )}
                </div>
            </div>

            <div className="grid gap-4 md:grid-cols-4">
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="flex items-center justify-between text-sm font-medium text-muted-foreground">
                            Supplier
                            <Building2 className="h-4 w-4" />
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-lg font-semibold">{receipt.vendorName || 'Unknown supplier'}</div>
                        <p className="mt-1 text-sm text-muted-foreground">{receipt.orderNumber || receipt.financePurchaseOrderId}</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="flex items-center justify-between text-sm font-medium text-muted-foreground">
                            Receipt Date
                            <CalendarDays className="h-4 w-4" />
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-lg font-semibold">{formatDate(receipt.receiptDate)}</div>
                        <p className="mt-1 text-sm text-muted-foreground">{receipt.items.length} line(s)</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="flex items-center justify-between text-sm font-medium text-muted-foreground">
                            Quantity Flow
                            <PackageCheck className="h-4 w-4" />
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-lg font-semibold">{formatNumber(totalReceived)} received</div>
                        <p className="mt-1 text-sm text-muted-foreground">{formatNumber(totalRemaining)} remaining to invoice</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="flex items-center justify-between text-sm font-medium text-muted-foreground">
                            Estimated Receipt Value
                            <ShoppingCart className="h-4 w-4" />
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-lg font-semibold">{formatCurrency(receiptValue, currencyCode)}</div>
                        <p className="mt-1 text-sm text-muted-foreground">{formatNumber(totalInvoiced)} quantity invoiced</p>
                    </CardContent>
                </Card>
            </div>

            {receipt.remarks && (
                <Card>
                    <CardHeader className="pb-3">
                        <CardTitle className="text-base">Receipt Remarks</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <p className="whitespace-pre-wrap text-sm text-muted-foreground">{receipt.remarks}</p>
                    </CardContent>
                </Card>
            )}

            <Card>
                <CardHeader>
                    <CardTitle>Received Items</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="overflow-hidden rounded-md border">
                        <table className="w-full text-left text-sm">
                            <thead className="border-b bg-muted/50">
                                <tr>
                                    <th className="p-3">Line</th>
                                    <th className="p-3 text-right">Ordered</th>
                                    <th className="p-3 text-right">Previously Received</th>
                                    <th className="p-3 text-right">Received This GRV</th>
                                    <th className="p-3 text-right">Invoiced</th>
                                    <th className="p-3 text-right">Remaining</th>
                                    <th className="p-3 text-right">Unit Price</th>
                                    <th className="p-3 text-right">Receipt Value</th>
                                </tr>
                            </thead>
                            <tbody>
                                {receipt.items.map(item => {
                                    const poLine = poLinesById.get(item.financePurchaseOrderItemId);
                                    const remaining = Math.max(0, item.quantityReceived - (item.invoicedQuantity || 0));
                                    const unitPrice = poLine?.unitPrice || 0;
                                    const lineValue = item.quantityReceived * unitPrice;

                                    return (
                                        <tr key={item.id} className="border-b hover:bg-muted/40">
                                            <td className="p-3">
                                                <div className="font-medium">{item.description || poLine?.description || 'Received line'}</div>
                                                <div className="mt-1 text-xs text-muted-foreground">
                                                    PO line: {item.financePurchaseOrderItemId}
                                                </div>
                                            </td>
                                            <td className="p-3 text-right font-mono">{formatNumber(item.orderedQuantity ?? 0)}</td>
                                            <td className="p-3 text-right font-mono">{formatNumber(item.previouslyReceived ?? 0)}</td>
                                            <td className="p-3 text-right font-mono font-semibold">{formatNumber(item.quantityReceived)}</td>
                                            <td className="p-3 text-right font-mono">{formatNumber(item.invoicedQuantity || 0)}</td>
                                            <td className="p-3 text-right font-mono">{formatNumber(remaining)}</td>
                                            <td className="p-3 text-right font-mono">{formatCurrency(unitPrice, currencyCode)}</td>
                                            <td className="p-3 text-right font-mono font-semibold">{formatCurrency(lineValue, currencyCode)}</td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>
                    </div>

                    <div className="mt-6 flex justify-end">
                        <div className="w-full max-w-sm space-y-3 rounded-md border p-4">
                            <div className="flex justify-between text-sm">
                                <span className="text-muted-foreground">Received quantity</span>
                                <span className="font-mono font-semibold">{formatNumber(totalReceived)}</span>
                            </div>
                            <div className="flex justify-between text-sm">
                                <span className="text-muted-foreground">Remaining to invoice</span>
                                <span className="font-mono font-semibold">{formatNumber(totalRemaining)}</span>
                            </div>
                            <Separator />
                            <div className="flex items-center justify-between">
                                <span className="font-semibold">Receipt value</span>
                                <span className="font-mono text-lg font-bold">{formatCurrency(receiptValue, currencyCode)}</span>
                            </div>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {receipt.vendorInvoiceId && (
                <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900">
                    <div className="flex items-start gap-3">
                        <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0" />
                        <div>
                            <div className="font-semibold">Converted to AP invoice</div>
                            <p className="mt-1">Vendor invoice reference: {receipt.vendorInvoiceId}</p>
                        </div>
                    </div>
                </div>
            )}
        </div>
        <section className="finance-grv-print-root hidden print:block bg-white p-8 text-black">
            <div className="mb-8 flex items-start justify-between border-b-2 border-black pb-4">
                <div>
                    <h1 className="text-3xl font-bold">Goods Receipt Voucher</h1>
                    <p className="mt-1 text-sm text-slate-700">ERP System</p>
                </div>
                <div className="text-right">
                    <div className="text-2xl font-bold">{receipt.receiptNumber}</div>
                    <div className="mt-1 text-sm uppercase tracking-wide">{status.label}</div>
                </div>
            </div>

            <div className="mb-6 grid grid-cols-2 gap-6">
                <div className="rounded border border-slate-300 p-4">
                    <h2 className="mb-3 text-sm font-bold uppercase tracking-wide">Vendor / Supplier</h2>
                    <p className="text-base font-semibold">{receipt.vendorName || 'Unknown supplier'}</p>
                    <p className="mt-1 text-xs text-slate-600">PO: {receipt.orderNumber || receipt.financePurchaseOrderId}</p>
                </div>
                <div className="rounded border border-slate-300 p-4">
                    <h2 className="mb-3 text-sm font-bold uppercase tracking-wide">Receipt Information</h2>
                    <div className="grid grid-cols-2 gap-3 text-sm">
                        <div>
                            <div className="text-xs font-semibold uppercase text-slate-600">Receipt Date</div>
                            <div>{formatDate(receipt.receiptDate)}</div>
                        </div>
                        <div>
                            <div className="text-xs font-semibold uppercase text-slate-600">Lines</div>
                            <div>{receipt.items.length}</div>
                        </div>
                        <div>
                            <div className="text-xs font-semibold uppercase text-slate-600">Currency</div>
                            <div>{currencyCode}</div>
                        </div>
                        <div>
                            <div className="text-xs font-semibold uppercase text-slate-600">Invoice Status</div>
                            <div>{status.label}</div>
                        </div>
                    </div>
                </div>
            </div>

            <table className="mb-6 w-full border-collapse text-sm">
                <thead>
                    <tr className="bg-slate-100">
                        <th className="border border-slate-300 p-2 text-left">Line</th>
                        <th className="border border-slate-300 p-2 text-right">Ordered</th>
                        <th className="border border-slate-300 p-2 text-right">Previously Received</th>
                        <th className="border border-slate-300 p-2 text-right">Received This GRV</th>
                        <th className="border border-slate-300 p-2 text-right">Invoiced</th>
                        <th className="border border-slate-300 p-2 text-right">Remaining</th>
                        <th className="border border-slate-300 p-2 text-right">Unit Price</th>
                        <th className="border border-slate-300 p-2 text-right">Receipt Value</th>
                    </tr>
                </thead>
                <tbody>
                    {receipt.items.map(item => {
                        const poLine = poLinesById.get(item.financePurchaseOrderItemId);
                        const remaining = Math.max(0, item.quantityReceived - (item.invoicedQuantity || 0));
                        const unitPrice = poLine?.unitPrice || 0;
                        const lineValue = item.quantityReceived * unitPrice;

                        return (
                            <tr key={`print-${item.id}`}>
                                <td className="border border-slate-300 p-2">
                                    <div className="font-semibold">{item.description || poLine?.description || 'Received line'}</div>
                                    <div className="mt-1 text-xs text-slate-600">PO line: {item.financePurchaseOrderItemId}</div>
                                </td>
                                <td className="border border-slate-300 p-2 text-right">{formatNumber(item.orderedQuantity ?? 0)}</td>
                                <td className="border border-slate-300 p-2 text-right">{formatNumber(item.previouslyReceived ?? 0)}</td>
                                <td className="border border-slate-300 p-2 text-right">{formatNumber(item.quantityReceived)}</td>
                                <td className="border border-slate-300 p-2 text-right">{formatNumber(item.invoicedQuantity || 0)}</td>
                                <td className="border border-slate-300 p-2 text-right">{formatNumber(remaining)}</td>
                                <td className="border border-slate-300 p-2 text-right">{formatCurrency(unitPrice, currencyCode)}</td>
                                <td className="border border-slate-300 p-2 text-right font-semibold">{formatCurrency(lineValue, currencyCode)}</td>
                            </tr>
                        );
                    })}
                </tbody>
            </table>

            <div className="ml-auto w-96 rounded border border-slate-300 p-4 text-sm">
                <div className="mb-2 flex justify-between">
                    <span>Received Quantity</span>
                    <span>{formatNumber(totalReceived)}</span>
                </div>
                <div className="mb-2 flex justify-between">
                    <span>Invoiced Quantity</span>
                    <span>{formatNumber(totalInvoiced)}</span>
                </div>
                <div className="mb-3 flex justify-between">
                    <span>Remaining to Invoice</span>
                    <span>{formatNumber(totalRemaining)}</span>
                </div>
                <div className="flex justify-between border-t border-slate-300 pt-3 text-base font-bold">
                    <span>Receipt Value</span>
                    <span>{formatCurrency(receiptValue, currencyCode)}</span>
                </div>
                {purchaseOrder?.currencyCode && purchaseOrder.currencyCode !== 'GHS' && purchaseOrder.exchangeRate && purchaseOrder.exchangeRate !== 1 && (
                    <div className="mt-3 border-t border-slate-300 pt-3 text-xs text-slate-700">
                        Base Currency Equivalent: {formatCurrency(receiptValue * purchaseOrder.exchangeRate, 'GHS')}
                    </div>
                )}
            </div>

            {receipt.remarks && (
                <div className="mt-8 rounded border border-slate-300 p-4">
                    <h2 className="mb-2 text-sm font-bold uppercase tracking-wide">Remarks</h2>
                    <p className="whitespace-pre-wrap text-sm">{receipt.remarks}</p>
                </div>
            )}

            {receipt.vendorInvoiceId && (
                <div className="mt-8 rounded border border-slate-300 p-4 text-sm">
                    <h2 className="mb-2 text-sm font-bold uppercase tracking-wide">AP Invoice Reference</h2>
                    <p>{receipt.vendorInvoiceId}</p>
                </div>
            )}

            <div className="mt-16 grid grid-cols-3 gap-10 text-center text-sm">
                <div className="border-t border-black pt-2">Received By</div>
                <div className="border-t border-black pt-2">Inspected By</div>
                <div className="border-t border-black pt-2">Approved By</div>
            </div>
        </section>
        </>
    );
}
