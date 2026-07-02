'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Plus, Eye, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { financePurchaseOrderService, FinancePurchaseOrder } from '@/services/financePurchaseOrderService';
import { formatCurrency } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { useAuth } from '@/hooks/use-auth';

const FINANCE_PO_APPROVER_ROLES = [
    'SuperAdmin',
    'TenantAdmin',
    'Manager',
    'Accounts Officer',
    'Senior Accountant',
    'Finance Manager',
    'Financial Controller',
];

export default function PurchaseOrdersPage() {
    const router = useRouter();
    const { hasAnyRole } = useAuth();
    const [pos, setPos] = useState<FinancePurchaseOrder[]>([]);
    const [loading, setLoading] = useState(true);
    const canOpenApprovalQueue = hasAnyRole(FINANCE_PO_APPROVER_ROLES);

    useEffect(() => {
        loadPOs();
    }, []);

    const loadPOs = async () => {
        try {
            const data = await financePurchaseOrderService.getPurchaseOrders();
            setPos(data || []);
        } catch (error) {
            console.error('Failed to load POs', error);
        } finally {
            setLoading(false);
        }
    };

    const getStatusBadge = (status: any) => {
        const statusStr = typeof status === 'number' ? 
            (status === 1 ? 'Draft' : status === 2 ? 'Approved' : status === 3 ? 'PartiallyReceived' : status === 4 ? 'Received' : status === 5 ? 'PartiallyInvoiced' : status === 6 ? 'Invoiced' : status === 9 ? 'PendingApproval' : status === 10 ? 'Rejected' : 'Unknown') : 
            String(status);

        switch (statusStr) {
            case 'Draft': return <Badge variant="secondary">Draft</Badge>;
            case 'PendingApproval':
            case 'Pending Approval': return <Badge className="bg-amber-600">Pending Approval</Badge>;
            case 'Approved': return <Badge className="bg-blue-600">Approved</Badge>;
            case 'PartiallyReceived': return <Badge className="bg-indigo-600">Partially Received</Badge>;
            case 'Received': return <Badge className="bg-green-600">Received</Badge>;
            case 'PartiallyInvoiced': return <Badge className="bg-orange-500">Partially Invoiced</Badge>;
            case 'Invoiced': return <Badge className="bg-green-800">Invoiced</Badge>;
            case 'Rejected': return <Badge variant="destructive">Rejected</Badge>;
            default: return <Badge variant="secondary">Unknown ({status})</Badge>;
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Finance Purchase Orders</h1>
                    <p className="text-muted-foreground mt-2">Manage AP Finance POs</p>
                </div>
                <div className="flex gap-2">
                    {canOpenApprovalQueue && (
                        <Button variant="outline" onClick={() => router.push('/finance/ap/purchase-orders/approvals')}>
                            <ShieldCheck className="mr-2 h-4 w-4" /> Approval Queue
                        </Button>
                    )}
                    <Button onClick={() => router.push('/finance/ap/purchase-orders/create')}>
                        <Plus className="mr-2 h-4 w-4" /> Create PO
                    </Button>
                </div>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>Purchase Orders</CardTitle>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <p>Loading...</p>
                    ) : pos.length === 0 ? (
                        <div className="text-center p-8 text-muted-foreground">No Purchase Orders found.</div>
                    ) : (
                        <div className="rounded-md border">
                            <table className="w-full text-sm text-left">
                                <thead className="border-b bg-muted/50">
                                    <tr>
                                        <th className="p-3">PO Number</th>
                                        <th className="p-3">Date</th>
                                        <th className="p-3">Amount</th>
                                        <th className="p-3">Status</th>
                                        <th className="p-3">Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {pos.map(po => (
                                        <tr key={po.id} className="border-b hover:bg-muted/50">
                                            <td className="p-3 font-medium">{po.orderNumber}</td>
                                            <td className="p-3">{new Date(po.orderDate).toLocaleDateString()}</td>
                                            <td className="p-3">{formatCurrency(po.totalAmount, po.currencyCode || 'GHS')}</td>
                                            <td className="p-3">{getStatusBadge(po.status)}</td>
                                            <td className="p-3 space-x-2">
                                                <Button variant="ghost" size="sm" onClick={() => router.push(`/finance/ap/purchase-orders/${po.id}`)}>
                                                    <Eye className="h-4 w-4 mr-1" /> View
                                                </Button>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
