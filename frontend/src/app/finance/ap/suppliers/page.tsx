'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { FileText, Search, WalletCards } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { accountsPayableService } from '@/services/accountsPayableService';

export default function ApSuppliersPage() {
  const router = useRouter();
  const [search, setSearch] = useState('');
  const { data: suppliers = [], isLoading, isError, error, refetch } = useQuery({
    queryKey: ['finance', 'ap', 'entry-suppliers', 'register'],
    queryFn: () => accountsPayableService.getInvoiceSupplierEntryOptions(),
  });

  const visibleSuppliers = useMemo(() => {
    const term = search.trim().toLocaleLowerCase();
    if (!term) return suppliers;
    return suppliers.filter((supplier) =>
      `${supplier.name} ${supplier.code}`.toLocaleLowerCase().includes(term)
    );
  }, [search, suppliers]);

  return (
    <div className="mx-auto max-w-[1600px] space-y-8 p-8">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">AP Suppliers</h1>
        <p className="mt-2 max-w-4xl text-muted-foreground">
          Finance read-only view of every supplier identity accepted by AP. Shared Business
          Partners remain governed in Procurement; Finance preserves legacy AP identities and
          creates their controlled link only when a transaction requires it.
        </p>
      </div>

      <Card>
        <CardHeader className="gap-4 md:flex-row md:items-center md:justify-between">
          <CardTitle>Supplier register</CardTitle>
          <div className="relative w-full md:w-80">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              placeholder="Search name or code…"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
        </CardHeader>
        <CardContent>
          {isError ? (
            <div className="flex items-center justify-between gap-4 rounded-md border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
              <span>{error instanceof Error ? error.message : 'AP suppliers could not be loaded.'}</span>
              <Button variant="outline" size="sm" onClick={() => void refetch()}>Retry</Button>
            </div>
          ) : (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Supplier</TableHead>
                    <TableHead>AP identity</TableHead>
                    <TableHead>Shared master</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {isLoading ? Array.from({ length: 4 }).map((_, index) => (
                    <TableRow key={index}>
                      <TableCell><Skeleton className="h-5 w-64" /></TableCell>
                      <TableCell><Skeleton className="h-5 w-28" /></TableCell>
                      <TableCell><Skeleton className="h-5 w-36" /></TableCell>
                      <TableCell><Skeleton className="ml-auto h-8 w-40" /></TableCell>
                    </TableRow>
                  )) : visibleSuppliers.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={4} className="h-24 text-center text-muted-foreground">
                        No AP suppliers match this search.
                      </TableCell>
                    </TableRow>
                  ) : visibleSuppliers.map((supplier) => {
                    const isLinked = Boolean(supplier.supplierId && supplier.businessPartnerId);
                    const isSharedOnly = Boolean(!supplier.supplierId && supplier.businessPartnerId);
                    return (
                      <TableRow key={`${supplier.id}:${supplier.supplierId ?? 'pending'}`}>
                        <TableCell>
                          <div className="font-medium">{supplier.name}</div>
                          <div className="text-xs text-muted-foreground">{supplier.code}</div>
                        </TableCell>
                        <TableCell>
                          <Badge variant={supplier.supplierId ? 'default' : 'secondary'}>
                            {supplier.supplierId ? 'Transaction ready' : 'Linked on first use'}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          {isLinked ? 'Linked Business Partner' : isSharedOnly ? 'Approved Business Partner' : 'Legacy AP supplier'}
                        </TableCell>
                        <TableCell>
                          <div className="flex justify-end gap-2">
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => router.push(`/finance/ap/invoices/create?supplierId=${supplier.id}`)}
                            >
                              <FileText className="mr-2 h-4 w-4" /> Invoice
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => router.push(`/finance/ap/payments/create?supplierId=${supplier.id}`)}
                            >
                              <WalletCards className="mr-2 h-4 w-4" /> Payment
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
