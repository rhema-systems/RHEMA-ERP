'use client';

import { useParams, useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
    ArrowLeft,
    Mail,
    Phone,
    MapPin,
    Building2,
    DollarSign,
    Calendar,
    CreditCard,
    FileText,
    AlertCircle,
    MoreVertical,
    Edit,
    Trash2
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { arService } from '@/services/ar-service';
import { formatCurrency } from '@/lib/utils';
import { format } from 'date-fns';
import { CustomerTransactionHistory } from './CustomerTransactionHistory';
import { CustomerNotes } from './CustomerNotes';

export default function CustomerDetailsPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;

    const { data: customer, isLoading } = useQuery({
        queryKey: ['customer', id],
        queryFn: () => arService.getCustomer(id),
    });

    const { data: outstandingInvoices, isLoading: invoicesLoading } = useQuery({
        queryKey: ['customer-outstanding-invoices', id],
        queryFn: () => arService.getOutstandingInvoices(id),
    });

    if (isLoading) {
        return <CustomerDetailsSkeleton />;
    }

    if (!customer) {
        return (
            <div className="p-8 text-center">
                <h2 className="text-xl font-semibold">Customer not found</h2>
                <Button variant="link" onClick={() => router.push('/finance/ar/customers')}>
                    Return to list
                </Button>
            </div>
        );
    }

    const customerStatus = !customer.isActive
        ? 'Inactive'
        : customer.isBlacklisted ? 'Blacklisted' : 'Active';

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div className="flex items-center space-x-4">
                    <Button variant="ghost" size="icon" onClick={() => router.push('/finance/ar/customers')}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight">{customer.customerName}</h1>
                        <div className="flex items-center mt-1 text-muted-foreground">
                            <span className="mr-2 font-mono text-sm bg-muted px-2 py-0.5 rounded">
                                {customer.customerCode}
                            </span>
                            <Badge variant={
                                customerStatus === 'Active' ? 'default' :
                                    customerStatus === 'Blacklisted' ? 'destructive' : 'secondary'
                            }>
                                {customerStatus}
                            </Badge>
                        </div>
                    </div>
                </div>
                <div className="flex space-x-2">
                    <Button variant="outline" onClick={() => router.push(`/procurement/business-partners/${id}/edit`)}>
                        <Edit className="mr-2 h-4 w-4" /> Edit Business Partner
                    </Button>
                    <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon">
                                <MoreVertical className="h-4 w-4" />
                            </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                            <DropdownMenuLabel>Actions</DropdownMenuLabel>
                            <DropdownMenuItem onClick={() => router.push(`/finance/ar/invoices/new?businessPartnerId=${id}`)}>
                                Create Invoice
                            </DropdownMenuItem>
                            <DropdownMenuItem onClick={() => router.push(`/finance/ar/receipts/new?businessPartnerId=${id}`)}>
                                Record Receipt
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem onClick={() => router.push(`/procurement/business-partners/${id}`)}>
                                Open canonical master record
                            </DropdownMenuItem>
                        </DropdownMenuContent>
                    </DropdownMenu>
                </div>
            </div>

            <div className="grid gap-6 md:grid-cols-3">
                {/* Info Card */}
                <Card className="md:col-span-2">
                    <CardHeader>
                        <CardTitle>Contact Information</CardTitle>
                    </CardHeader>
                    <CardContent className="grid gap-4 md:grid-cols-2">
                        <div className="space-y-3">
                            <div className="flex items-center text-sm">
                                <Mail className="mr-2 h-4 w-4 text-muted-foreground" />
                                <span>{customer.email || 'No email provided'}</span>
                            </div>
                            <div className="flex items-center text-sm">
                                <Phone className="mr-2 h-4 w-4 text-muted-foreground" />
                                <span>{customer.phone || 'No phone provided'}</span>
                            </div>
                        </div>
                        <div className="space-y-3">
                            <div className="flex items-start text-sm">
                                <MapPin className="mr-2 h-4 w-4 text-muted-foreground mt-0.5" />
                                <span>
                                    {customer.address}<br />
                                    {customer.city}, {customer.country}
                                </span>
                            </div>
                        </div>
                    </CardContent>
                </Card>

                {/* Financial Summary */}
                <Card>
                    <CardHeader>
                        <CardTitle>Financial Summary</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div className="space-y-1">
                            <p className="text-sm font-medium text-muted-foreground">Receivable</p>
                            <p className={`text-2xl font-bold ${customer.outstandingBalance > 0 ? 'text-red-600' : 'text-green-600'}`}>
                                {formatCurrency(customer.outstandingBalance, customer.currencyCode)}
                            </p>
                        </div>
                        <div className="space-y-1">
                            <p className="text-sm font-medium text-muted-foreground">Customer Credit</p>
                            <p className={`text-xl font-semibold ${customer.customerCreditBalance > 0 ? 'text-blue-600' : 'text-muted-foreground'}`}>
                                {formatCurrency(customer.customerCreditBalance, customer.currencyCode)}
                            </p>
                        </div>
                        <div className="space-y-1">
                            <p className="text-sm font-medium text-muted-foreground">Credit Limit (Base: ₵)</p>
                            <p className="text-lg font-semibold">
                                {formatCurrency(customer.creditLimit, customer.currencyCode)}
                            </p>
                            <div className="h-2 w-full bg-secondary rounded-full overflow-hidden">
                                <div
                                    className={`h-full ${customer.outstandingBalance > customer.creditLimit ? 'bg-red-500' : 'bg-blue-500'}`}
                                    style={{ width: `${Math.min(100, (customer.outstandingBalance / (customer.creditLimit || 1)) * 100)}%` }}
                                />
                            </div>
                            <p className="text-xs text-muted-foreground mt-1">
                                {((customer.outstandingBalance / (customer.creditLimit || 1)) * 100).toFixed(1)}% utilized
                            </p>
                        </div>
                        <div className="space-y-1 pt-2 border-t">
                            <div className="flex justify-between text-sm">
                                <span className="text-muted-foreground">Payment Terms</span>
                                <span className="font-medium">{customer.paymentTermsDays} days</span>
                            </div>
                            <div className="flex justify-between text-sm">
                                <span className="text-muted-foreground">Price Group</span>
                                <span className="font-medium">{customer.priceGroup || 'Standard'}</span>
                            </div>
                        </div>
                    </CardContent>
                </Card>
            </div>

            <Tabs defaultValue="invoices">
                <TabsList>
                    <TabsTrigger value="invoices">Open Invoices</TabsTrigger>
                    <TabsTrigger value="history">Transaction History</TabsTrigger>
                    <TabsTrigger value="notes">Notes</TabsTrigger>
                </TabsList>
                <TabsContent value="invoices" className="pt-4">
                    <Card>
                        <CardHeader>
                            <CardTitle>Outstanding Invoices</CardTitle>
                            <CardDescription>Unpaid invoices for this customer</CardDescription>
                        </CardHeader>
                        <CardContent>
                            {outstandingInvoices && outstandingInvoices.length > 0 ? (
                                <div className="space-y-4">
                                    {outstandingInvoices.map((inv) => (
                                        <div key={inv.id} className="flex items-center justify-between p-4 border rounded-lg hover:bg-muted/50 cursor-pointer" onClick={() => router.push(`/finance/ar/invoices/${inv.id}`)}>
                                            <div className="space-y-1">
                                                <p className="font-medium">{inv.invoiceNumber}</p>
                                                <p className="text-sm text-muted-foreground">Due: {inv.dueDate ? format(new Date(inv.dueDate), 'MMM dd, yyyy') : '-'}</p>
                                            </div>
                                            <div className="text-right">
                                                <p className="font-bold">{formatCurrency(inv.balanceAmount, inv.currencyCode)}</p>
                                                {inv.dueDate && new Date(inv.dueDate) < new Date() && (
                                                    <Badge variant="destructive" className="mt-1 text-xs">Overdue</Badge>
                                                )}
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            ) : (
                                <div className="text-center py-8 text-muted-foreground">
                                    No outstanding invoices found.
                                </div>
                            )}
                        </CardContent>
                    </Card>
                </TabsContent>

                <TabsContent value="history">
                    <CustomerTransactionHistory customerId={id} />
                </TabsContent>
                <TabsContent value="notes">
                    <Card>
                        <CardHeader>
                            <CardTitle>Customer Notes</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <CustomerNotes customer={customer} />
                        </CardContent>
                    </Card>
                </TabsContent>
            </Tabs>
        </div>
    );
}

function CustomerDetailsSkeleton() {
    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div className="flex items-center justify-between">
                <div className="space-y-2">
                    <Skeleton className="h-8 w-64" />
                    <Skeleton className="h-4 w-32" />
                </div>
                <Skeleton className="h-10 w-32" />
            </div>
            <div className="grid gap-6 md:grid-cols-3">
                <Skeleton className="md:col-span-2 h-48" />
                <Skeleton className="h-48" />
            </div>
            <Skeleton className="h-96" />
        </div>
    )
}
