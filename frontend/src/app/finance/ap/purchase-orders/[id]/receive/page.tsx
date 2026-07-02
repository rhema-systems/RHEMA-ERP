'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { financePurchaseOrderService, FinancePurchaseOrder } from '@/services/financePurchaseOrderService';
import { Input } from '@/components/ui/input';
import { useToast } from '@/components/ui/use-toast';
import { useDocumentSequence } from '@/hooks/use-document-sequence';
import { FinanceDocumentTypes } from '@/types/document-numbering';

export default function ReceivePOPage({ params }: { params: { id: string } }) {
    const router = useRouter();
    const { toast } = useToast();
    const [po, setPo] = useState<FinancePurchaseOrder | null>(null);
    const [loading, setLoading] = useState(true);
    const [receiveQtys, setReceiveQtys] = useState<Record<string, number>>({});
    const [receiptNumber, setReceiptNumber] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const receiptSequence = useDocumentSequence('Finance', FinanceDocumentTypes.APGoodsReceipt);

    useEffect(() => {
        loadPO();
    }, [params.id]);

    const loadPO = async () => {
        try {
            const data = await financePurchaseOrderService.getPurchaseOrderById(params.id);
            setPo(data);
            
            // Initialize quantities to remaining
            const initialQtys: Record<string, number> = {};
            data.items.forEach(item => {
                if (!item.id) return;
                const remaining = item.orderedQuantity - (item.receivedQuantity || 0);
                initialQtys[item.id] = remaining > 0 ? remaining : 0;
            });
            setReceiveQtys(initialQtys);
        } catch (error) {
            toast({ title: 'Error', description: 'Failed to load PO', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    const handleReceive = async () => {
        if (!po) return;
        setSubmitting(true);
        try {
            const receiptItems = po.items
                .filter((item) => Boolean(item.id))
                .map(item => ({
                    financePurchaseOrderItemId: item.id as string,
                    quantityReceived: receiveQtys[item.id as string] || 0
                }))
                .filter(r => r.quantityReceived > 0);

            if (receiptItems.length === 0) {
                toast({ title: 'Warning', description: 'No quantities to receive', variant: 'destructive' });
                return;
            }

            const receipt = {
                financePurchaseOrderId: po.id,
                receiptNumber: receiptSequence.allowManualEntry && receiptNumber.trim() ? receiptNumber.trim() : undefined,
                receiptDate: new Date().toISOString(),
                remarks: 'E2E Testing Receipt',
                lines: receiptItems
            };

            const result = await financePurchaseOrderService.createReceipt(receipt);
            toast({ title: 'Success', description: 'GRV Created Successfully' });
            router.push(`/finance/ap/receipts/${result.id}`);
        } catch (error: any) {
            toast({ title: 'Error', description: error.message || 'Failed to create GRV', variant: 'destructive' });
        } finally {
            setSubmitting(false);
        }
    };

    if (loading) return <div className="p-8">Loading...</div>;
    if (!po) return <div className="p-8">PO not found</div>;

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div className="flex justify-between items-center">
                <div className="flex items-center space-x-4">
                    <Button variant="ghost" onClick={() => router.back()}>
                        <ArrowLeft className="h-4 w-4 mr-2" /> Back
                    </Button>
                    <h1 className="text-3xl font-bold tracking-tight">Receive PO: {po.orderNumber}</h1>
                </div>
                <Button onClick={handleReceive} disabled={submitting} className="bg-green-600">
                    <Save className="mr-2 h-4 w-4" /> {submitting ? 'Saving...' : 'Submit GRV'}
                </Button>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>Enter Quantities to Receive</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="mb-4 grid max-w-sm gap-2">
                        <label className="text-sm font-medium text-muted-foreground">GRV Receipt Number</label>
                        <Input
                            value={receiptSequence.allowManualEntry ? receiptNumber : receiptSequence.sampleNumber}
                            onChange={(event) => setReceiptNumber(event.target.value)}
                            disabled={!receiptSequence.allowManualEntry || receiptSequence.loading}
                            placeholder={receiptSequence.allowManualEntry ? `Auto: ${receiptSequence.sampleNumber}` : undefined}
                            className="bg-muted font-mono"
                        />
                        <p className="text-xs text-muted-foreground">Assigned by the configured Goods Receipt sequence when saved.</p>
                    </div>
                    <table className="w-full text-sm text-left">
                        <thead className="border-b bg-muted/50">
                            <tr>
                                <th className="p-3">Description</th>
                                <th className="p-3 text-right">Ordered Qty</th>
                                <th className="p-3 text-right">Prev Received</th>
                                <th className="p-3 text-right w-32">Receive Qty</th>
                            </tr>
                        </thead>
                        <tbody>
                            {po.items.map(item => {
                                const remaining = item.orderedQuantity - (item.receivedQuantity || 0);
                                return (
                                <tr key={item.id} className="border-b">
                                    <td className="p-3">{item.description}</td>
                                    <td className="p-3 text-right">{item.orderedQuantity}</td>
                                    <td className="p-3 text-right">{item.receivedQuantity || 0}</td>
                                    <td className="p-3 text-right">
                                        <Input 
                                            type="number" 
                                            min={0} 
                                            max={remaining}
                                            value={item.id ? receiveQtys[item.id] ?? 0 : 0}
                                            onChange={(e) => item.id && setReceiveQtys({ ...receiveQtys, [item.id]: Number(e.target.value) })}
                                            disabled={!item.id || remaining <= 0}
                                        />
                                    </td>
                                </tr>
                            )})}
                        </tbody>
                    </table>
                </CardContent>
            </Card>
        </div>
    );
}
