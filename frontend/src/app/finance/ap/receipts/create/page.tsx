'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Save, FileText, CheckSquare, Square, Package, AlertCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { financePurchaseOrderService, FinancePurchaseOrder } from '@/services/financePurchaseOrderService';

interface SelectedLine {
    id: string;
    description: string;
    lineType: number;
    orderedQuantity: number;
    receivedQuantity: number;
    remainingToReceive: number;
    quantityReceived: number; // custom receive amount entered by user
    selected: boolean;
}

export default function CreateReceiptPage() {
    const router = useRouter();
    const { toast } = useToast();
    
    // Selectable POs
    const [purchaseOrders, setPurchaseOrders] = useState<FinancePurchaseOrder[]>([]);
    const [selectedPoId, setSelectedPoId] = useState<string>('');
    const [selectedPo, setSelectedPo] = useState<FinancePurchaseOrder | null>(null);
    
    // Receipt Header
    const [receiptNumber, setReceiptNumber] = useState<string>('');
    const [receiptDate, setReceiptDate] = useState<string>(new Date().toISOString().split('T')[0]);
    const [remarks, setRemarks] = useState<string>('');
    
    // Selected Lines
    const [lines, setLines] = useState<SelectedLine[]>([]);
    
    // Page state
    const [loading, setLoading] = useState(true);
    const [submitting, setSubmitting] = useState(false);

    useEffect(() => {
        loadPOs();
    }, []);

    const loadPOs = async () => {
        try {
            const data = await financePurchaseOrderService.getPurchaseOrders();
            // Filter: Only Approved (2) or PartiallyReceived (3) POs
            const eligiblePOs = (data || []).filter((po: FinancePurchaseOrder) => {
                if (po.status !== 2 && po.status !== 3) return false;
                
                // Exclude POs where all lines have zero remaining quantity
                const hasRemaining = po.items.some(
                    item => item.orderedQuantity - (item.receivedQuantity || 0) > 0
                );
                return hasRemaining;
            });
            
            setPurchaseOrders(eligiblePOs);
        } catch (error) {
            console.error('Failed to load eligible POs', error);
            toast({ title: 'Error', description: 'Failed to load Purchase Orders', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    const handlePOChange = async (poId: string) => {
        setSelectedPoId(poId);
        if (!poId) {
            setSelectedPo(null);
            setLines([]);
            return;
        }

        try {
            setLoading(true);
            const poData = await financePurchaseOrderService.getPurchaseOrderById(poId);
            setSelectedPo(poData);
            
            // Set a default receipt number
            setReceiptNumber(`GRV-${poData.orderNumber.replace(/PO-/gi, '')}-${Math.floor(1000 + Math.random() * 9000)}`);
            
            // Scaffolding selected lines with initial values
            const initialLines: SelectedLine[] = poData.items.map(item => {
                const remaining = item.orderedQuantity - (item.receivedQuantity || 0);
                return {
                    id: item.id!,
                    description: item.description,
                    lineType: item.lineType,
                    orderedQuantity: item.orderedQuantity,
                    receivedQuantity: item.receivedQuantity || 0,
                    remainingToReceive: remaining > 0 ? remaining : 0,
                    quantityReceived: remaining > 0 ? remaining : 0,
                    selected: remaining > 0 // Auto-select lines with remaining qty
                };
            });
            setLines(initialLines);
        } catch (error) {
            toast({ title: 'Error', description: 'Failed to load PO lines', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    const toggleLineSelection = (id: string) => {
        setLines(lines.map(l => {
            if (l.id === id) {
                // If turning selection on but remaining is 0, keep selected as false
                if (!l.selected && l.remainingToReceive <= 0) return l;
                return { ...l, selected: !l.selected };
            }
            return l;
        }));
    };

    const handleQtyChange = (id: string, val: number) => {
        setLines(lines.map(l => {
            if (l.id === id) {
                // Bounds enforcement
                const max = l.remainingToReceive;
                let cleanVal = isNaN(val) ? 0 : val;
                if (cleanVal < 0) cleanVal = 0;
                if (cleanVal > max) cleanVal = max;
                return { ...l, quantityReceived: cleanVal };
            }
            return l;
        }));
    };

    const handleSubmit = async () => {
        if (!selectedPoId || !selectedPo) {
            toast({ title: 'Validation Error', description: 'Please select a Purchase Order', variant: 'destructive' });
            return;
        }
        if (!receiptNumber.trim()) {
            toast({ title: 'Validation Error', description: 'GRV Receipt Number is required', variant: 'destructive' });
            return;
        }

        const selectedLines = lines.filter(l => l.selected && l.quantityReceived > 0);
        if (selectedLines.length === 0) {
            toast({ title: 'Validation Error', description: 'At least one line item must be selected and have a receive quantity greater than 0', variant: 'destructive' });
            return;
        }

        // Final double check on over-receipt
        const hasOverReceipt = selectedLines.some(l => l.quantityReceived > l.remainingToReceive);
        if (hasOverReceipt) {
            toast({ title: 'Validation Error', description: 'One or more lines exceed the remaining quantity to receive', variant: 'destructive' });
            return;
        }

        setSubmitting(true);
        try {
            // Map strictly to backend CreateFinancePurchaseReceiptDto
            const payload = {
                financePurchaseOrderId: selectedPo.id,
                receiptNumber: receiptNumber,
                receiptDate: new Date(receiptDate).toISOString(),
                remarks: remarks,
                lines: selectedLines.map(l => ({
                    financePurchaseOrderItemId: l.id,
                    quantityReceived: Number(l.quantityReceived)
                }))
            };

            const result = await financePurchaseOrderService.createReceipt(payload);
            toast({ title: 'Success', description: `GRV ${result.receiptNumber} posted successfully` });
            router.push(`/finance/ap/receipts/${result.id}`);
        } catch (error: any) {
            toast({ title: 'Error', description: error.message || 'Failed to post GRV', variant: 'destructive' });
        } finally {
            setSubmitting(false);
        }
    };

    if (loading && purchaseOrders.length === 0) {
        return <div className="p-8 text-center text-muted-foreground">Loading operational GRV flow...</div>;
    }

    return (
        <div className="space-y-6 p-8 max-w-[1600px] mx-auto">
            <div className="flex justify-between items-center">
                <div className="flex items-center space-x-4">
                    <Button variant="outline" size="sm" onClick={() => router.push('/finance/ap/receipts')}>
                        <ArrowLeft className="h-4 w-4 mr-2" /> Back
                    </Button>
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight">Create Goods Receipt Voucher</h1>
                        <p className="text-muted-foreground mt-1">Receive PO items into stock / record account expenses</p>
                    </div>
                </div>
                
                <Button 
                    onClick={handleSubmit} 
                    disabled={submitting || !selectedPoId} 
                    className="bg-primary hover:bg-primary/95 text-primary-foreground font-semibold shadow-md px-5"
                >
                    <Save className="h-4 w-4 mr-2" />
                    {submitting ? 'Saving Receipt...' : 'Confirm & Save GRV'}
                </Button>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Selector & Header Card */}
                <div className="lg:col-span-1 space-y-6">
                    <Card className="border-border/40 shadow-sm bg-card/60 backdrop-blur-md">
                        <CardHeader>
                            <CardTitle>PO Receipt Context</CardTitle>
                            <CardDescription>Select Purchase Order and enter receipt details</CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="space-y-2">
                                <label className="text-sm font-medium text-muted-foreground">Reference Purchase Order</label>
                                <select
                                    value={selectedPoId}
                                    onChange={(e) => handlePOChange(e.target.value)}
                                    className="w-full p-2.5 rounded-md border bg-background text-sm focus:outline-none focus:ring-2 focus:ring-primary/40 transition"
                                >
                                    <option value="">-- Select Approved PO --</option>
                                    {purchaseOrders.map(po => (
                                        <option key={po.id} value={po.id}>
                                            {po.orderNumber} - {po.vendorName || 'Unknown Vendor'} ({po.currencyCode})
                                        </option>
                                    ))}
                                </select>
                            </div>

                            <div className="space-y-2">
                                <label className="text-sm font-medium text-muted-foreground">GRV Receipt Number</label>
                                <Input 
                                    value={receiptNumber} 
                                    onChange={(e) => setReceiptNumber(e.target.value)} 
                                    placeholder="e.g. GRV-10255"
                                    disabled={!selectedPoId}
                                />
                            </div>

                            <div className="space-y-2">
                                <label className="text-sm font-medium text-muted-foreground">Receipt Date</label>
                                <Input 
                                    type="date"
                                    value={receiptDate} 
                                    onChange={(e) => setReceiptDate(e.target.value)} 
                                    disabled={!selectedPoId}
                                />
                            </div>

                            <div className="space-y-2">
                                <label className="text-sm font-medium text-muted-foreground">Remarks / Receiving Notes</label>
                                <Textarea 
                                    value={remarks} 
                                    onChange={(e) => setRemarks(e.target.value)} 
                                    placeholder="Enter delivery details, carrier note reference, etc."
                                    disabled={!selectedPoId}
                                    rows={4}
                                />
                            </div>
                        </CardContent>
                    </Card>

                    {selectedPo && (
                        <Card className="border-border/30 bg-muted/20">
                            <CardHeader className="py-4">
                                <CardTitle className="text-sm font-semibold flex items-center gap-2">
                                    <FileText className="h-4 w-4 text-primary" /> Selected PO Metadata
                                </CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-3 text-sm py-2">
                                <div className="flex justify-between border-b border-border/40 pb-2">
                                    <span className="text-muted-foreground">Supplier / Vendor:</span>
                                    <span className="font-medium">{selectedPo.vendorName || '-'}</span>
                                </div>
                                <div className="flex justify-between border-b border-border/40 pb-2">
                                    <span className="text-muted-foreground">PO Order Date:</span>
                                    <span className="font-medium">{new Date(selectedPo.orderDate).toLocaleDateString()}</span>
                                </div>
                                <div className="flex justify-between border-b border-border/40 pb-2">
                                    <span className="text-muted-foreground">Document Currency:</span>
                                    <span className="font-medium">{selectedPo.currencyCode}</span>
                                </div>
                                <div className="flex justify-between border-b border-border/40 pb-2">
                                    <span className="text-muted-foreground">Header Exchange Rate:</span>
                                    <span className="font-medium">{selectedPo.exchangeRate.toFixed(4)}</span>
                                </div>
                                <div className="flex justify-between">
                                    <span className="text-muted-foreground">Total PO Value:</span>
                                    <span className="font-semibold text-primary">{selectedPo.currencyCode} {selectedPo.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
                                </div>
                            </CardContent>
                        </Card>
                    )}
                </div>

                {/* Lines Details Card */}
                <div className="lg:col-span-2">
                    <Card className="border-border/40 shadow-sm bg-card/60 backdrop-blur-md h-full flex flex-col">
                        <CardHeader>
                            <CardTitle>PO Items Selection & Quantities</CardTitle>
                            <CardDescription>Select lines and enter received quantities. Hover over rows to review values.</CardDescription>
                        </CardHeader>
                        <CardContent className="flex-1">
                            {!selectedPoId ? (
                                <div className="h-full flex flex-col justify-center items-center p-12 text-center border-2 border-dashed rounded-lg border-muted/50 bg-muted/5">
                                    <Package className="h-10 w-10 text-muted mb-4 animate-pulse" />
                                    <h3 className="font-semibold text-muted-foreground text-lg">No Purchase Order Selected</h3>
                                    <p className="text-sm text-muted-foreground/60 max-w-sm mt-1">Please select an Approved Purchase Order in the sidebar to load the receipt line items.</p>
                                </div>
                            ) : loading ? (
                                <div className="p-8 text-center text-muted-foreground">Loading PO lines...</div>
                            ) : (
                                <div className="rounded-md border border-border/30 overflow-hidden bg-background/50">
                                    <table className="w-full text-sm text-left">
                                        <thead className="border-b bg-muted/60 text-muted-foreground">
                                            <tr>
                                                <th className="p-3.5 w-12 text-center">Receive</th>
                                                <th className="p-3.5">Line Description</th>
                                                <th className="p-3.5 w-24">Type</th>
                                                <th className="p-3.5 text-right w-24">Ordered</th>
                                                <th className="p-3.5 text-right w-24">Prev Recvd</th>
                                                <th className="p-3.5 text-right w-24 text-primary font-medium">Remaining</th>
                                                <th className="p-3.5 text-right w-32">Qty to Receive</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {lines.map((line) => {
                                                const isExceeded = line.quantityReceived > line.remainingToReceive;
                                                return (
                                                    <tr 
                                                        key={line.id} 
                                                        className={`border-b hover:bg-muted/30 transition-colors ${!line.selected ? 'opacity-60 bg-muted/10' : ''}`}
                                                    >
                                                        <td className="p-3.5 text-center">
                                                            <button
                                                                type="button"
                                                                onClick={() => toggleLineSelection(line.id)}
                                                                disabled={line.remainingToReceive <= 0}
                                                                className={`p-1 rounded transition hover:bg-muted focus:outline-none ${line.remainingToReceive <= 0 ? 'cursor-not-allowed opacity-40' : ''}`}
                                                            >
                                                                {line.selected ? (
                                                                    <CheckSquare className="h-5 w-5 text-primary" />
                                                                ) : (
                                                                    <Square className="h-5 w-5 text-muted-foreground/60" />
                                                                )}
                                                            </button>
                                                        </td>
                                                        <td className="p-3.5">
                                                            <div className="font-medium text-foreground">{line.description}</div>
                                                        </td>
                                                        <td className="p-3.5">
                                                            <span className={`px-2 py-0.5 rounded text-xs font-medium ${line.lineType === 1 ? 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300' : 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300'}`}>
                                                                {line.lineType === 1 ? 'Inventory' : 'GL Account'}
                                                            </span>
                                                        </td>
                                                        <td className="p-3.5 text-right font-medium text-muted-foreground">{line.orderedQuantity}</td>
                                                        <td className="p-3.5 text-right font-medium text-muted-foreground">{line.receivedQuantity}</td>
                                                        <td className="p-3.5 text-right font-semibold text-primary">{line.remainingToReceive}</td>
                                                        <td className="p-3.5 text-right">
                                                            <div className="relative">
                                                                <Input
                                                                    type="number"
                                                                    min={0}
                                                                    max={line.remainingToReceive}
                                                                    value={line.quantityReceived}
                                                                    onChange={(e) => handleQtyChange(line.id, Number(e.target.value))}
                                                                    disabled={!line.selected || line.remainingToReceive <= 0}
                                                                    className={`w-28 text-right font-medium pr-2 h-9 border ${isExceeded ? 'border-destructive focus:ring-destructive' : ''}`}
                                                                />
                                                                {isExceeded && (
                                                                    <div className="absolute top-10 right-0 z-10 bg-destructive text-destructive-foreground text-xs px-2 py-1 rounded shadow flex items-center gap-1">
                                                                        <AlertCircle className="h-3 w-3" /> Max {line.remainingToReceive}
                                                                    </div>
                                                                )}
                                                            </div>
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
            </div>
        </div>
    );
}
