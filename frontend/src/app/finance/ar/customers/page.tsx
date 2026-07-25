'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import {
    Plus,
    Search,
    Filter,
    MoreHorizontal,
    Building2,
    Phone,
    Mail,
    ArrowUpDown,
    FileText,
    DollarSign
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { arService } from '@/services/ar-service';
import { formatCurrency } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';
import { useDebounce } from '@/hooks/use-debounce';

export default function CustomersPage() {
    const router = useRouter();
    const [searchTerm, setSearchTerm] = useState('');
    const debouncedSearchTerm = useDebounce(searchTerm, 500);
    const [page, setPage] = useState(1);
    const [pageSize] = useState(10);

    const { data: customersData, isLoading } = useQuery({
        queryKey: ['customers', page, pageSize, debouncedSearchTerm],
        queryFn: () => arService.getCustomers({
            page,
            pageSize,
            searchTerm: debouncedSearchTerm,
            includeBalances: true
        }),
    });

    const handleSearch = (e: React.ChangeEvent<HTMLInputElement>) => {
        setSearchTerm(e.target.value);
        setPage(1); // Reset to first page on search
    };

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Customers</h1>
                    <p className="text-muted-foreground mt-2">
                        Manage your customer base and view account statuses.
                    </p>
                </div>
                <Button onClick={() => router.push('/finance/ar/customers/new')}>
                    <Plus className="mr-2 h-4 w-4" /> New Customer
                </Button>
            </div>

            <Card>
                <CardHeader>
                    <div className="flex items-center justify-between">
                        <CardTitle>Customer List</CardTitle>
                        <div className="flex items-center space-x-2">
                            <div className="relative w-64">
                                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                                <Input
                                    placeholder="Search customers..."
                                    className="pl-8"
                                    value={searchTerm}
                                    onChange={handleSearch}
                                />
                            </div>
                            <Button variant="outline" size="icon">
                                <Filter className="h-4 w-4" />
                            </Button>
                        </div>
                    </div>
                </CardHeader>
                <CardContent>
                    <div className="rounded-md border">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Customer</TableHead>
                                    <TableHead>Contact Info</TableHead>
                                    <TableHead className="text-right">Outstanding Balance</TableHead>
                                    <TableHead className="text-right">Credit Limit</TableHead>
                                    <TableHead>Status</TableHead>
                                    <TableHead className="w-[70px]"></TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {isLoading ? (
                                    [...Array(5)].map((_, i) => (
                                        <TableRow key={i}>
                                            <TableCell><Skeleton className="h-4 w-[200px]" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[150px]" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[100px] ml-auto" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[100px] ml-auto" /></TableCell>
                                            <TableCell><Skeleton className="h-4 w-[80px]" /></TableCell>
                                            <TableCell><Skeleton className="h-8 w-8" /></TableCell>
                                        </TableRow>
                                    ))
                                ) : customersData?.items?.length === 0 ? (
                                    <TableRow>
                                        <TableCell colSpan={6} className="h-24 text-center">
                                            No customers found.
                                        </TableCell>
                                    </TableRow>
                                ) : (
                                    customersData?.items.map((customer) => (
                                        <TableRow key={customer.id} className="cursor-pointer hover:bg-muted/50" onClick={() => router.push(`/finance/ar/customers/${customer.id}`)}>
                                            <TableCell>
                                                <div className="flex flex-col">
                                                    <span className="font-medium">{customer.customerName}</span>
                                                    <span className="text-xs text-muted-foreground">{customer.customerCode}</span>
                                                </div>
                                            </TableCell>
                                            <TableCell>
                                                <div className="flex flex-col space-y-1">
                                                    {customer.email && (
                                                        <div className="flex items-center text-xs text-muted-foreground">
                                                            <Mail className="mr-1 h-3 w-3" /> {customer.email}
                                                        </div>
                                                    )}
                                                    {customer.phone && (
                                                        <div className="flex items-center text-xs text-muted-foreground">
                                                            <Phone className="mr-1 h-3 w-3" /> {customer.phone}
                                                        </div>
                                                    )}
                                                </div>
                                            </TableCell>
                                            <TableCell className="text-right font-medium">
                                                <span className={customer.outstandingBalance > 0 ? "text-red-600" : "text-green-600"}>
                                                    {formatCurrency(customer.outstandingBalance, 'GHS')}
                                                </span>
                                            </TableCell>
                                            <TableCell className="text-right text-muted-foreground">
                                                {formatCurrency(customer.creditLimit, 'GHS')}
                                            </TableCell>
                                            <TableCell>
                                                <Badge
                                                    variant={
                                                        customer.status === 'Active' ? 'default' :
                                                            customer.status === 'OnHold' ? 'destructive' : 'secondary'
                                                    }
                                                >
                                                    {customer.status}
                                                </Badge>
                                            </TableCell>
                                            <TableCell>
                                                <DropdownMenu>
                                                    <DropdownMenuTrigger asChild>
                                                        <Button variant="ghost" className="h-8 w-8 p-0" onClick={(e) => e.stopPropagation()}>
                                                            <span className="sr-only">Open menu</span>
                                                            <MoreHorizontal className="h-4 w-4" />
                                                        </Button>
                                                    </DropdownMenuTrigger>
                                                    <DropdownMenuContent align="end">
                                                        <DropdownMenuLabel>Actions</DropdownMenuLabel>
                                                        <DropdownMenuItem onClick={(e) => { e.stopPropagation(); router.push(`/finance/ar/customers/${customer.id}`); }}>
                                                            View Details
                                                        </DropdownMenuItem>
                                                        <DropdownMenuItem onClick={(e) => { e.stopPropagation(); router.push(`/finance/ar/customers/${customer.id}/edit`); }}>
                                                            Edit Customer
                                                        </DropdownMenuItem>
                                                        <DropdownMenuSeparator />
                                                        <DropdownMenuItem onClick={(e) => { e.stopPropagation(); router.push(`/finance/ar/invoices/new?customerId=${customer.id}`); }}>
                                                            <FileText className="mr-2 h-4 w-4" /> New Invoice
                                                        </DropdownMenuItem>
                                                        <DropdownMenuItem onClick={(e) => { e.stopPropagation(); router.push(`/finance/ar/receipts/new?customerId=${customer.id}`); }}>
                                                            <DollarSign className="mr-2 h-4 w-4" /> Record Receipt
                                                        </DropdownMenuItem>
                                                    </DropdownMenuContent>
                                                </DropdownMenu>
                                            </TableCell>
                                        </TableRow>
                                    ))
                                )}
                            </TableBody>
                        </Table>
                    </div>

                    {/* Pagination Controls */}
                    {customersData && customersData.totalPages > 1 && (
                        <div className="flex items-center justify-end space-x-2 py-4">
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => setPage((p) => Math.max(1, p - 1))}
                                disabled={!customersData.hasPreviousPage}
                            >
                                Previous
                            </Button>
                            <div className="text-sm text-muted-foreground">
                                Page {page} of {customersData.totalPages}
                            </div>
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => setPage((p) => Math.min(customersData.totalPages, p + 1))}
                                disabled={!customersData.hasNextPage}
                            >
                                Next
                            </Button>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
