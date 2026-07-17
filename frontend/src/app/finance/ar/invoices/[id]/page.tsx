'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
    ArrowLeft,
    Printer,
    Mail,
    Download,
    CreditCard,
    Ban,
    FileText
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
import { arService } from '@/services/ar-service';
import type { EstateArSource } from '@/services/ar-service';
import { formatCurrency } from '@/lib/utils';
import { format } from 'date-fns';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';

const ESTATE_AR_ACTIONS = [
    'Invoice created',
    'Statement issued',
    'Arrears updated',
    'Deposit handled',
    'GL posted',
    'Returned for correction',
];

export default function InvoiceDetailsPage() {
    const router = useRouter();
    const { toast } = useToast();
    const params = useParams();
    const id = params.id as string;
    const [estateSource, setEstateSource] = useState<EstateArSource>('facilities');
    const [estateAction, setEstateAction] = useState(ESTATE_AR_ACTIONS[0]);
    const [isNotifyingEstate, setIsNotifyingEstate] = useState(false);

    const { data: invoice, isLoading } = useQuery({
        queryKey: ['invoice', id],
        queryFn: () => arService.getInvoice(id),
    });

    if (isLoading) {
        return <InvoiceDetailsSkeleton />;
    }

    if (!invoice) {
        return (
            <div className="p-8 text-center">
                <h2 className="text-xl font-semibold">Invoice not found</h2>
                <Button variant="link" onClick={() => router.push('/finance/ar/invoices')}>
                    Return to list
                </Button>
            </div>
        );
    }

    const notifyEstate = async () => {
        // Estate/Finance integration: sends status back to Estate/Facilities or Estate/Property Management after AR processing.
        setIsNotifyingEstate(true);
        try {
            await arService.notifyEstateArResult(estateSource, {
                actionType: estateAction,
                financeArEntityId: invoice.id,
                financeArReference: invoice.invoiceNumber,
                customerId: invoice.customerId,
                customerName: invoice.customerName,
                amount: invoice.totalAmount,
                currencyCode: invoice.currencyCode,
                sourceRecordReference: invoice.invoiceNumber,
                notes: invoice.notes || null,
                actionUrl: `/finance/ar/invoices/${invoice.id}`,
            });
            toast({
                title: 'Estate notified',
                description:
                    estateSource === 'facilities'
                        ? 'Estate / Facilities has been notified of this AR result.'
                        : 'Estate / Property Management has been notified of this AR result.',
            });
        } catch (error: any) {
            toast({
                title: 'Notification failed',
                description: error.message || 'The Estate notification could not be created.',
                variant: 'destructive',
            });
        } finally {
            setIsNotifyingEstate(false);
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1000px] mx-auto">
            {/* Header Actions */}
            <div className="flex items-center justify-between no-print">
                <div className="flex items-center space-x-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/ar/invoices')}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div className="flex items-center space-x-2">
                        <h1 className="text-2xl font-bold tracking-tight">Invoice {invoice.invoiceNumber}</h1>
                        <Badge variant={
                            invoice.status === 'Paid' ? 'default' :
                                invoice.status === 'Overdue' ? 'destructive' :
                                    invoice.status === 'Void' ? 'secondary' : 'outline'
                        } className={invoice.status === 'Paid' ? 'bg-green-600' : ''}>
                            {invoice.status}
                        </Badge>
                    </div>
                </div>
                <div className="flex space-x-2">
                    <Button variant="outline" size="sm" onClick={() => window.print()}>
                        <Printer className="mr-2 h-4 w-4" /> Print
                    </Button>
                    <Button variant="outline" size="sm">
                        <Mail className="mr-2 h-4 w-4" /> Email
                    </Button>
                    {invoice.status === 'Posted' && invoice.balanceAmount > 0 && (
                        <Button size="sm" onClick={() => router.push(`/finance/ar/payments/new?customerId=${invoice.customerId}&invoiceId=${invoice.id}`)}>
                            <CreditCard className="mr-2 h-4 w-4" /> Record Payment
                        </Button>
                    )}
                </div>
            </div>

            <Card className="print:shadow-none print:border-none">
                <CardHeader className="flex flex-row justify-between items-start border-b pb-8">
                    <div className="space-y-2">
                        <div className="flex items-center space-x-2">
                            <div className="h-8 w-8 bg-black rounded-lg"></div>
                            <span className="text-xl font-bold">Acme Inc.</span>
                        </div>
                        <p className="text-sm text-muted-foreground w-[250px]">
                            123 Business Rd.<br />
                            Suite 100<br />
                            Tech City, TC 10101<br />
                            billing@acme.inc
                        </p>
                    </div>
                    <div className="text-right space-y-1">
                        <h2 className="text-3xl font-bold text-gray-200 uppercase tracking-widest">Invoice</h2>
                        <div className="flex justify-end space-x-4 pt-4">
                            <div className="text-left">
                                <p className="text-xs text-muted-foreground uppercase font-bold">Invoice #</p>
                                <p className="font-medium">{invoice.invoiceNumber}</p>
                            </div>
                            <div className="text-left">
                                <p className="text-xs text-muted-foreground uppercase font-bold">Date</p>
                                <p className="font-medium">{format(new Date(invoice.invoiceDate), 'MMM dd, yyyy')}</p>
                            </div>
                            <div className="text-left">
                                <p className="text-xs text-muted-foreground uppercase font-bold">Due Date</p>
                                <p className="font-medium">{format(new Date(invoice.dueDate), 'MMM dd, yyyy')}</p>
                            </div>
                        </div>
                    </div>
                </CardHeader>
                <CardContent className="pt-8 space-y-8">
                    {/* Bill To */}
                    <div className="grid grid-cols-2 gap-8">
                        <div>
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Bill To</p>
                            <p className="font-bold text-lg">{invoice.customerName}</p>
                            {/* We would fetch detailed customer address here ideally, assuming it's usually on the invoice object or we fetch it separately */}
                            <p className="text-sm text-muted-foreground mt-1">
                                Customer ID: {invoice.customerId}
                            </p>
                        </div>
                        <div className="text-right">
                            {/* Ship To or other details could go here */}
                        </div>
                    </div>

                    {/* Line Items Table */}
                    <div className="rounded-md border">
                        <div className="grid grid-cols-12 gap-4 p-4 bg-muted/50 text-xs font-bold uppercase text-muted-foreground border-b">
                            <div className="col-span-6">Description</div>
                            <div className="col-span-2 text-right">Qty</div>
                            <div className="col-span-2 text-right">Price</div>
                            <div className="col-span-2 text-right">Amount</div>
                        </div>
                        {invoice.lineItems.map((item, index) => (
                            <div key={item.id || index} className="grid grid-cols-12 gap-4 p-4 border-b last:border-0 text-sm">
                                <div className="col-span-6">
                                    <p className="font-medium">{item.description}</p>
                                </div>
                                <div className="col-span-2 text-right">{item.quantity}</div>
                                <div className="col-span-2 text-right">{formatCurrency(item.unitPrice)}</div>
                                <div className="col-span-2 text-right font-medium">{formatCurrency(item.lineTotal)}</div>
                            </div>
                        ))}
                    </div>

                    {/* Totals */}
                    <div className="flex justify-end">
                        <div className="w-1/3 space-y-2">
                            <div className="flex justify-between text-sm">
                                <span className="text-muted-foreground">Subtotal</span>
                                <span>{formatCurrency(invoice.totalAmount)}</span> {/* Assuming totalAmount includes tax for now or we calculate tax separately if provided */}
                            </div>
                            {/* Add Tax/Discount rows if available in Invoice object */}
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
                            <p className="text-xs text-muted-foreground uppercase font-bold mb-2">Notes / Terms</p>
                            <p className="text-sm text-muted-foreground">{invoice.notes}</p>
                        </div>
                    )}
                </CardContent>
            </Card>

            <Card className="no-print">
                {/* Estate/Finance integration: callback panel is for handoff visibility, not a replacement AR workflow. */}
                <CardHeader>
                    <CardTitle>Estate AR Callback</CardTitle>
                </CardHeader>
                <CardContent className="grid gap-3 md:grid-cols-[1fr_1fr_auto]">
                    <div className="space-y-2">
                        <label className="text-sm font-medium" htmlFor="estate-source">
                            Estate source
                        </label>
                        <select
                            id="estate-source"
                            className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                            value={estateSource}
                            onChange={(event) => setEstateSource(event.target.value as EstateArSource)}
                        >
                            <option value="facilities">Estate / Facilities</option>
                            <option value="property-management">Estate / Property Management</option>
                        </select>
                    </div>
                    <div className="space-y-2">
                        <label className="text-sm font-medium" htmlFor="estate-action">
                            Result
                        </label>
                        <select
                            id="estate-action"
                            className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                            value={estateAction}
                            onChange={(event) => setEstateAction(event.target.value)}
                        >
                            {ESTATE_AR_ACTIONS.map((action) => (
                                <option key={action}>{action}</option>
                            ))}
                        </select>
                    </div>
                    <div className="flex items-end">
                        <Button onClick={notifyEstate} disabled={isNotifyingEstate}>
                            {isNotifyingEstate ? 'Notifying...' : 'Notify Estate'}
                        </Button>
                    </div>
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
