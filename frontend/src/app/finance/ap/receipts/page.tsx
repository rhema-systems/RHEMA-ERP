'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Eye } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { financePurchaseOrderService, FinancePurchaseOrderReceipt } from '@/services/financePurchaseOrderService';

export default function ReceiptsPage() {
    const router = useRouter();
    const [receipts, setReceipts] = useState<FinancePurchaseOrderReceipt[]>([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        loadReceipts();
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

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Goods Receipt Vouchers</h1>
                    <p className="text-muted-foreground mt-2">Manage AP Finance GRVs</p>
                </div>
                <Button onClick={() => router.push('/finance/ap/receipts/create')} className="bg-primary hover:bg-primary/90 text-primary-foreground font-semibold px-4 py-2 rounded-lg transition-all duration-300 shadow-md hover:shadow-lg flex items-center gap-2">
                    Create GRV
                </Button>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>Receipts</CardTitle>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <p>Loading...</p>
                    ) : receipts.length === 0 ? (
                        <div className="text-center p-8 text-muted-foreground">No Receipts found.</div>
                    ) : (
                        <div className="rounded-md border">
                            <table className="w-full text-sm text-left">
                                <thead className="border-b bg-muted/50">
                                    <tr>
                                        <th className="p-3">Receipt Number</th>
                                        <th className="p-3">Receipt Date</th>
                                        <th className="p-3">PO ID (Ref)</th>
                                        <th className="p-3">Remarks</th>
                                        <th className="p-3">Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {receipts.map(receipt => (
                                        <tr key={receipt.id} className="border-b hover:bg-muted/50">
                                            <td className="p-3 font-medium">{receipt.receiptNumber}</td>
                                            <td className="p-3">{new Date(receipt.receiptDate).toLocaleDateString()}</td>
                                            <td className="p-3 text-xs text-muted-foreground">{receipt.financePurchaseOrderId}</td>
                                            <td className="p-3">{receipt.remarks || '-'}</td>
                                            <td className="p-3 space-x-2">
                                                <Button variant="ghost" size="sm" onClick={() => router.push(`/finance/ap/receipts/${receipt.id}`)}>
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
