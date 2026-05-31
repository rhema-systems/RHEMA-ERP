'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft, FileText } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { financePurchaseOrderService, FinancePurchaseOrderReceipt } from '@/services/financePurchaseOrderService';
import { useToast } from '@/components/ui/use-toast';

export default function ReceiptDetailsPage({ params }: { params: { id: string } }) {
    const router = useRouter();
    const { toast } = useToast();
    const [receipt, setReceipt] = useState<FinancePurchaseOrderReceipt | null>(null);
    const [loading, setLoading] = useState(true);
    const [converting, setConverting] = useState(false);

    useEffect(() => {
        loadReceipt();
    }, [params.id]);

    const loadReceipt = async () => {
        try {
            const data = await financePurchaseOrderService.getReceiptById(params.id);
            setReceipt(data);
        } catch (error) {
            toast({ title: 'Error', description: 'Failed to load Receipt', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    const handleConvertToInvoice = async () => {
        setConverting(true);
        try {
            const draftInvoice = await financePurchaseOrderService.convertToVendorInvoice(params.id);
            toast({ title: 'Success', description: 'Draft AP Invoice Created' });
            // Redirect to existing invoice page
            router.push(`/finance/ap/invoices/${draftInvoice.id}`);
        } catch (error: any) {
            toast({ title: 'Error', description: error.message || 'Failed to convert to invoice', variant: 'destructive' });
        } finally {
            setConverting(false);
        }
    };

    if (loading) return <div className="p-8">Loading...</div>;
    if (!receipt) return <div className="p-8">Receipt not found</div>;

    const hasUninvoiced = receipt.items.some(i => (i.quantityReceived - (i.invoicedQuantity || 0)) > 0);

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div className="flex justify-between items-center">
                <div className="flex items-center space-x-4">
                    <Button variant="ghost" onClick={() => router.push('/finance/ap/receipts')}>
                        <ArrowLeft className="h-4 w-4 mr-2" /> Back
                    </Button>
                    <h1 className="text-3xl font-bold tracking-tight">GRV: {receipt.receiptNumber}</h1>
                </div>
                <div className="space-x-2">
                    {hasUninvoiced && (
                        <Button onClick={handleConvertToInvoice} disabled={converting} className="bg-orange-600 hover:bg-orange-700">
                            <FileText className="mr-2 h-4 w-4" /> 
                            {converting ? 'Converting...' : 'Convert to Vendor Invoice'}
                        </Button>
                    )}
                </div>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>Received Items</CardTitle>
                </CardHeader>
                <CardContent>
                    <table className="w-full text-sm text-left">
                        <thead className="border-b bg-muted/50">
                            <tr>
                                <th className="p-3">PO Line Ref</th>
                                <th className="p-3 text-right">Received Qty</th>
                                <th className="p-3 text-right">Invoiced Qty</th>
                                <th className="p-3 text-right">Remaining to Invoice</th>
                            </tr>
                        </thead>
                        <tbody>
                            {receipt.items.map(item => {
                                const remaining = item.quantityReceived - (item.invoicedQuantity || 0);
                                return (
                                <tr key={item.id} className="border-b">
                                    <td className="p-3 font-mono text-xs text-muted-foreground">{item.financePurchaseOrderItemId}</td>
                                    <td className="p-3 text-right">{item.quantityReceived}</td>
                                    <td className="p-3 text-right">{item.invoicedQuantity || 0}</td>
                                    <td className="p-3 text-right font-medium">{remaining > 0 ? remaining : '-'}</td>
                                </tr>
                            )})}
                        </tbody>
                    </table>
                </CardContent>
            </Card>
        </div>
    );
}
