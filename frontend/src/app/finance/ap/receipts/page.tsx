'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { CalendarDays, Eye, FileText, PackageCheck, Plus, Search, ShoppingCart } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { financePurchaseOrderService, FinancePurchaseOrderReceipt } from '@/services/financePurchaseOrderService';

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
        return { label: 'Invoiced', className: 'bg-blue-600/15 text-blue-700 border-blue-200' };
    }

    const remaining = receipt.items.reduce((sum, item) => sum + Math.max(0, item.quantityReceived - (item.invoicedQuantity || 0)), 0);
    const invoiced = receipt.items.reduce((sum, item) => sum + (item.invoicedQuantity || 0), 0);

    if (remaining <= 0 && invoiced > 0) {
        return { label: 'Fully Matched', className: 'bg-emerald-600/15 text-emerald-700 border-emerald-200' };
    }

    if (invoiced > 0) {
        return { label: 'Partially Invoiced', className: 'bg-amber-600/15 text-amber-700 border-amber-200' };
    }

    return { label: receipt.approvalRequired === false ? 'Completed' : 'Approved', className: 'bg-emerald-600/15 text-emerald-700 border-emerald-200' };
}

export default function ReceiptsPage() {
    const router = useRouter();
    const [receipts, setReceipts] = useState<FinancePurchaseOrderReceipt[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');

    useEffect(() => {
        void loadReceipts();
    }, []);

    const loadReceipts = async () => {
        try {
            const data = await financePurchaseOrderService.getReceipts();
            setReceipts(data || []);
        } catch (error) {
            console.error('Failed to load Receipts', error);
        } finally {
            setLoading(false);
        }
    };

    const filteredReceipts = useMemo(() => {
        const term = search.trim().toLowerCase();
        if (!term) return receipts;

        return receipts.filter(receipt =>
            [
                receipt.receiptNumber,
                receipt.orderNumber,
                receipt.vendorName,
                receipt.remarks,
                receipt.financePurchaseOrderId,
            ]
                .filter(Boolean)
                .some(value => String(value).toLowerCase().includes(term))
        );
    }, [receipts, search]);

    const totalLines = receipts.reduce((sum, receipt) => sum + receipt.items.length, 0);
    const totalReceived = receipts.reduce(
        (sum, receipt) => sum + receipt.items.reduce((lineSum, item) => lineSum + item.quantityReceived, 0),
        0
    );
    const totalRemaining = receipts.reduce(
        (sum, receipt) => sum + receipt.items.reduce((lineSum, item) => lineSum + Math.max(0, item.quantityReceived - (item.invoicedQuantity || 0)), 0),
        0
    );

    return (
        <div className="mx-auto max-w-[1600px] space-y-6 p-8">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Goods Receipt Vouchers</h1>
                    <p className="mt-2 text-muted-foreground">Track AP receipts, invoice matching, and linked purchase orders.</p>
                </div>
                <Button onClick={() => router.push('/finance/ap/receipts/create')} className="w-full md:w-auto">
                    <Plus className="mr-2 h-4 w-4" />
                    Create GRV
                </Button>
            </div>

            <div className="grid gap-4 md:grid-cols-3">
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="flex items-center justify-between text-sm font-medium text-muted-foreground">
                            Total GRVs
                            <FileText className="h-4 w-4" />
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-3xl font-bold">{receipts.length}</div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="flex items-center justify-between text-sm font-medium text-muted-foreground">
                            Received Lines
                            <PackageCheck className="h-4 w-4" />
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-3xl font-bold">{totalLines}</div>
                        <p className="mt-1 text-sm text-muted-foreground">{formatNumber(totalReceived)} total quantity</p>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="flex items-center justify-between text-sm font-medium text-muted-foreground">
                            Remaining To Invoice
                            <ShoppingCart className="h-4 w-4" />
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-3xl font-bold">{formatNumber(totalRemaining)}</div>
                    </CardContent>
                </Card>
            </div>

            <Card>
                <CardHeader className="space-y-4">
                    <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                        <CardTitle>Receipts</CardTitle>
                        <div className="relative w-full md:w-96">
                            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                            <Input
                                value={search}
                                onChange={event => setSearch(event.target.value)}
                                placeholder="Search GRV, PO, supplier..."
                                className="pl-9"
                            />
                        </div>
                    </div>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <div className="py-12 text-center text-muted-foreground">Loading goods receipt vouchers...</div>
                    ) : filteredReceipts.length === 0 ? (
                        <div className="rounded-lg border border-dashed py-12 text-center text-muted-foreground">
                            No goods receipt vouchers found.
                        </div>
                    ) : (
                        <div className="overflow-hidden rounded-md border">
                            <table className="w-full text-left text-sm">
                                <thead className="border-b bg-muted/50">
                                    <tr>
                                        <th className="p-3">GRV</th>
                                        <th className="p-3">Supplier</th>
                                        <th className="p-3">Purchase Order</th>
                                        <th className="p-3">Receipt Date</th>
                                        <th className="p-3 text-right">Received</th>
                                        <th className="p-3 text-right">Remaining</th>
                                        <th className="p-3">Status</th>
                                        <th className="p-3 text-right">Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredReceipts.map(receipt => {
                                        const status = getReceiptStatus(receipt);
                                        const receivedQuantity = receipt.items.reduce((sum, item) => sum + item.quantityReceived, 0);
                                        const remainingQuantity = receipt.items.reduce(
                                            (sum, item) => sum + Math.max(0, item.quantityReceived - (item.invoicedQuantity || 0)),
                                            0
                                        );

                                        return (
                                            <tr key={receipt.id} className="border-b hover:bg-muted/40">
                                                <td className="p-3">
                                                    <div className="font-semibold">{receipt.receiptNumber || 'Unnumbered GRV'}</div>
                                                    <div className="text-xs text-muted-foreground">{receipt.items.length} line(s)</div>
                                                </td>
                                                <td className="p-3">{receipt.vendorName || 'Unknown supplier'}</td>
                                                <td className="p-3">
                                                    <Button
                                                        variant="link"
                                                        className="h-auto p-0 text-left font-medium"
                                                        onClick={() => router.push(`/finance/ap/purchase-orders/${receipt.financePurchaseOrderId}`)}
                                                    >
                                                        {receipt.orderNumber || receipt.financePurchaseOrderId}
                                                    </Button>
                                                </td>
                                                <td className="p-3">
                                                    <span className="inline-flex items-center gap-2">
                                                        <CalendarDays className="h-4 w-4 text-muted-foreground" />
                                                        {formatDate(receipt.receiptDate)}
                                                    </span>
                                                </td>
                                                <td className="p-3 text-right font-mono">{formatNumber(receivedQuantity)}</td>
                                                <td className="p-3 text-right font-mono">{formatNumber(remainingQuantity)}</td>
                                                <td className="p-3">
                                                    <Badge variant="outline" className={status.className}>{status.label}</Badge>
                                                </td>
                                                <td className="p-3 text-right">
                                                    <Button variant="ghost" size="sm" onClick={() => router.push(`/finance/ap/receipts/${receipt.id}`)}>
                                                        <Eye className="mr-1 h-4 w-4" />
                                                        View
                                                    </Button>
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                            </table>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
