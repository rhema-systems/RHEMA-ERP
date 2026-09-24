'use client';

import { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { arService } from '@/services/ar-service';
import { formatCurrency } from '@/lib/utils';
import { format } from 'date-fns';
import { FileText, DollarSign, ArrowUpRight, ArrowDownLeft } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';

interface CustomerTransactionHistoryProps {
    customerId: string;
}

interface Transaction {
    id: string;
    date: Date;
    type: 'Invoice' | 'Payment';
    reference: string;
    amount: number;
    status: string;
    currencyCode: string;
}

export function CustomerTransactionHistory({ customerId }: CustomerTransactionHistoryProps) {
    const { data: invoices, isLoading: invoicesLoading } = useQuery({
        queryKey: ['customer-invoices', customerId],
        queryFn: () => arService.getInvoices({ customerId, pageSize: 50 })
    });

    const { data: payments, isLoading: paymentsLoading } = useQuery({
        queryKey: ['customer-payments', customerId],
        queryFn: () => arService.getPayments({ businessPartnerId: customerId, pageSize: 50 })
    });

    const isLoading = invoicesLoading || paymentsLoading;

    if (isLoading) {
        return <div className="space-y-2">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
        </div>;
    }

    // Merge and sort transactions
    const transactions: Transaction[] = [
        ...(invoices?.items || []).map(inv => ({
            id: inv.id,
            date: new Date(inv.invoiceDate),
            type: 'Invoice' as const,
            reference: inv.invoiceNumber,
            amount: inv.totalAmount,
            status: inv.status,
            currencyCode: inv.currencyCode
        })),
        ...(payments?.items || []).map(pay => ({
            id: pay.id,
            date: new Date(pay.paymentDate),
            type: 'Payment' as const,
            // paymentNumber is the canonical AR receipt reference. The optional legacy aliases
            // remain fallbacks for older API projections while the application is still in dev.
            reference: pay.paymentNumber || pay.paymentReference || pay.referenceNumber || pay.id,
            amount: pay.totalAmount ?? pay.amount,
            status: pay.status,
            currencyCode: pay.currencyCode
        }))
    ].sort((a, b) => b.date.getTime() - a.date.getTime());

    if (transactions.length === 0) {
        return (
            <div className="text-center py-12 text-muted-foreground bg-muted/10 rounded-lg border border-dashed">
                No transactions found for this customer.
            </div>
        );
    }

    return (
        <div className="rounded-md border">
            <Table>
                <TableHeader>
                    <TableRow>
                        <TableHead>Date</TableHead>
                        <TableHead>Type</TableHead>
                        <TableHead>Reference</TableHead>
                        <TableHead className="text-right">Amount</TableHead>
                        <TableHead>Status</TableHead>
                    </TableRow>
                </TableHeader>
                <TableBody>
                    {transactions.map((tx) => (
                        <TableRow key={`${tx.type}-${tx.id}`}>
                            <TableCell>
                                {format(tx.date, 'MMM dd, yyyy')}
                            </TableCell>
                            <TableCell>
                                <div className="flex items-center">
                                    {tx.type === 'Invoice' ? (
                                        <ArrowUpRight className="mr-2 h-4 w-4 text-orange-500" />
                                    ) : (
                                        <ArrowDownLeft className="mr-2 h-4 w-4 text-green-500" />
                                    )}
                                    <span className="font-medium">{tx.type}</span>
                                </div>
                            </TableCell>
                            <TableCell>{tx.reference}</TableCell>
                            <TableCell className="text-right font-medium">
                                {formatCurrency(tx.amount, tx.currencyCode)}
                            </TableCell>
                            <TableCell>
                                <Badge variant={
                                    tx.status === 'Paid' || tx.status === 'Posted' ? 'default' :
                                        tx.status === 'Overdue' ? 'destructive' : 'secondary'
                                }>
                                    {tx.status}
                                </Badge>
                            </TableCell>
                        </TableRow>
                    ))}
                </TableBody>
            </Table>
        </div>
    );
}
