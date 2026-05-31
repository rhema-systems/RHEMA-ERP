'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import {
  Plus,
  Search,
  Filter,
  MoreHorizontal,
  FileText,
  RotateCcw,
  CheckCircle,
  FileDown
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
import { accountsPayableService } from '@/services/accountsPayableService';
import { formatCurrency } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';
import { useDebounce } from '@/hooks/use-debounce';
import { format } from 'date-fns';

export default function SupplierReturnsPage() {
  const router = useRouter();
  const [searchTerm, setSearchTerm] = useState('');
  const debouncedSearchTerm = useDebounce(searchTerm, 500);
  const [statusFilter, setStatusFilter] = useState<string>('');

  const { data: returns, isLoading } = useQuery({
    queryKey: ['supplier-returns'],
    queryFn: () => accountsPayableService.getSupplierReturns(),
  });

  const getStatusBadge = (status: number | string) => {
    // SupplierReturnStatus: Draft = 1, Approved = 2, Cancelled = 3
    const statusStr = typeof status === 'number' 
      ? (status === 1 ? 'Draft' : status === 2 ? 'Approved' : 'Cancelled')
      : status;

    switch (statusStr) {
      case 'Draft': 
      case '1':
        return <Badge variant="secondary" className="bg-slate-200 text-slate-800 dark:bg-slate-800 dark:text-slate-200">Draft</Badge>;
      case 'Approved': 
      case '2':
        return <Badge className="bg-emerald-600 text-white hover:bg-emerald-700">Approved</Badge>;
      case 'Cancelled': 
      case '3':
        return <Badge variant="destructive">Cancelled</Badge>;
      default: 
        return <Badge variant="secondary">{statusStr}</Badge>;
    }
  };

  const filteredReturns = returns?.filter((ret: any) => {
    const matchesSearch = 
      ret.returnNumber.toLowerCase().includes(debouncedSearchTerm.toLowerCase()) ||
      ret.vendorName.toLowerCase().includes(debouncedSearchTerm.toLowerCase()) ||
      (ret.reason && ret.reason.toLowerCase().includes(debouncedSearchTerm.toLowerCase()));

    const statusStr = typeof ret.status === 'number'
      ? (ret.status === 1 ? 'Draft' : ret.status === 2 ? 'Approved' : 'Cancelled')
      : ret.status;

    const matchesStatus = statusFilter === '' || statusStr === statusFilter;

    return matchesSearch && matchesStatus;
  }) || [];

  return (
    <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
            <RotateCcw className="h-8 w-8 text-primary" /> Supplier Returns
          </h1>
          <p className="text-muted-foreground mt-2">
            Record supplier return operational documents and generate financial Supplier Debit Notes.
          </p>
        </div>
        <Button onClick={() => router.push('/finance/ap/returns/create')}>
          <Plus className="mr-2 h-4 w-4" /> Create Return
        </Button>
      </div>

      {/* Summary Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        <Card className="glassmorphism border-primary/10">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Returns</CardTitle>
            <RotateCcw className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{filteredReturns.length}</div>
            <p className="text-xs text-muted-foreground mt-1">Operational return slips</p>
          </CardContent>
        </Card>
        <Card className="glassmorphism border-emerald-500/10">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Approved Returns</CardTitle>
            <CheckCircle className="h-4 w-4 text-emerald-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-emerald-600">
              {filteredReturns.filter((r: any) => r.status === 2 || r.status === 'Approved').length}
            </div>
            <p className="text-xs text-muted-foreground mt-1">Financial debit notes generated</p>
          </CardContent>
        </Card>
        <Card className="glassmorphism border-indigo-500/10">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Reclaimed Amount</CardTitle>
            <span className="font-bold text-indigo-500 text-sm">GHS</span>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-indigo-600">
              {formatCurrency(
                filteredReturns
                  .filter((r: any) => r.status === 2 || r.status === 'Approved')
                  .reduce((sum: number, r: any) => sum + r.baseCurrencyAmount, 0)
              )}
            </div>
            <p className="text-xs text-muted-foreground mt-1">Posted AP ledger reversals</p>
          </CardContent>
        </Card>
      </div>

      <Card className="glassmorphism border-slate-200/50 dark:border-slate-800/50">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Returns History</CardTitle>
            <div className="flex items-center space-x-2">
              <div className="relative w-64">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search returns..."
                  className="pl-8"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                />
              </div>
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="outline" size="icon">
                    <Filter className="h-4 w-4" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuLabel>Filter by Status</DropdownMenuLabel>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={() => setStatusFilter('')}>All</DropdownMenuItem>
                  <DropdownMenuItem onClick={() => setStatusFilter('Draft')}>Draft</DropdownMenuItem>
                  <DropdownMenuItem onClick={() => setStatusFilter('Approved')}>Approved</DropdownMenuItem>
                  <DropdownMenuItem onClick={() => setStatusFilter('Cancelled')}>Cancelled</DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border border-slate-200 dark:border-slate-800">
            <Table>
              <TableHeader>
                <TableRow className="bg-slate-50/50 dark:bg-slate-900/50">
                  <TableHead>Return #</TableHead>
                  <TableHead>Supplier</TableHead>
                  <TableHead>Return Date</TableHead>
                  <TableHead>Source Reference</TableHead>
                  <TableHead className="text-right">Returned Amount</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      <TableCell><Skeleton className="h-4 w-[100px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[180px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[100px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[120px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[80px] ml-auto" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[60px]" /></TableCell>
                      <TableCell><Skeleton className="h-8 w-8" /></TableCell>
                    </TableRow>
                  ))
                ) : filteredReturns.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="h-24 text-center text-muted-foreground">
                      No supplier returns found.
                    </TableCell>
                  </TableRow>
                ) : (
                  filteredReturns.map((ret: any) => (
                    <TableRow
                      key={ret.id}
                      className="cursor-pointer hover:bg-slate-50/50 dark:hover:bg-slate-900/30 transition-colors"
                      onClick={() => router.push(`/finance/ap/returns/${ret.id}`)}
                    >
                      <TableCell className="font-semibold text-primary">
                        {ret.returnNumber}
                      </TableCell>
                      <TableCell className="font-medium">{ret.vendorName}</TableCell>
                      <TableCell>{format(new Date(ret.returnDate), 'MMM dd, yyyy')}</TableCell>
                      <TableCell>
                        {ret.originalVendorInvoiceId ? (
                          <Badge variant="outline" className="border-blue-500/25 bg-blue-50/20 text-blue-600 dark:text-blue-400">
                            Invoice-Linked
                          </Badge>
                        ) : (
                          <Badge variant="outline" className="border-amber-500/25 bg-amber-50/20 text-amber-600 dark:text-amber-400">
                            GRV-Linked
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-right font-bold text-slate-800 dark:text-slate-200">
                        {formatCurrency(ret.totalAmount)} {ret.currencyCode}
                        {ret.currencyCode !== 'GHS' && (
                          <span className="block text-xs font-normal text-muted-foreground">
                            ({formatCurrency(ret.baseCurrencyAmount)} GHS)
                          </span>
                        )}
                      </TableCell>
                      <TableCell>{getStatusBadge(ret.status)}</TableCell>
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
                            <DropdownMenuItem onClick={() => router.push(`/finance/ap/returns/${ret.id}`)}>
                              <FileText className="mr-2 h-4 w-4" /> View Details
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
        </CardContent>
      </Card>
    </div>
  );
}
