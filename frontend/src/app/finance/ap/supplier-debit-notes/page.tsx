'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { format } from 'date-fns';
import { FileMinus2, Plus, Search } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { formatCurrency } from '@/lib/utils';
import { accountsPayableService } from '@/services/accountsPayableService';
import type { SupplierDebitNoteStatus } from '@/types/ap';
import { InventoryReturnCreditEntry } from './InventoryReturnCreditEntry';

const statuses: SupplierDebitNoteStatus[] = [
  'Draft',
  'PendingApproval',
  'Approved',
  'Posted',
  'Rejected',
  'Cancelled',
  'Reversed',
];

function statusBadge(status: SupplierDebitNoteStatus) {
  if (status === 'Posted')
    return <Badge className="bg-emerald-600">Posted</Badge>;
  if (status === 'Approved')
    return <Badge className="bg-blue-600">Approved</Badge>;
  if (status === 'PendingApproval')
    return <Badge className="bg-amber-600">Pending approval</Badge>;
  if (status === 'Rejected' || status === 'Cancelled')
    return <Badge variant="destructive">{status}</Badge>;
  if (status === 'Reversed') return <Badge variant="outline">Reversed</Badge>;
  return <Badge variant="secondary">Draft</Badge>;
}

export default function SupplierDebitNotesPage() {
  const router = useRouter();
  const { hasPermission } = useAuth();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<'all' | SupplierDebitNoteStatus>('all');
  const [returnCreditOpen, setReturnCreditOpen] = useState(false);
  const canManage = hasPermission('Finance.AP.SupplierDebitNotes.Manage');

  const {
    data: notes = [],
    isLoading,
    error,
  } = useQuery({
    queryKey: ['supplier-debit-notes', status],
    queryFn: () =>
      accountsPayableService.getSupplierDebitNotes({
        status: status === 'all' ? undefined : status,
      }),
  });

  const visible = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return notes;
    return notes.filter((note) =>
      [
        note.debitNoteNumber,
        note.vendorName,
        note.supplierCreditNoteReference,
        note.originalVendorInvoiceNumber,
        note.reason,
      ].some((value) => value?.toLowerCase().includes(needle))
    );
  }, [notes, search]);

  const postedAvailable = notes
    .filter((note) => note.statusName === 'Posted')
    .reduce((sum, note) => sum + note.remainingAmount, 0);

  return (
    <div className="mx-auto max-w-[1600px] space-y-8 p-8">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
            <FileMinus2 className="h-8 w-8 text-primary" /> Supplier Debit Notes
          </h1>
          <p className="mt-2 text-muted-foreground">
            Record supplier credits and apply returned-goods credits to their original invoices.
          </p>
        </div>
        {canManage && <div className="flex shrink-0 gap-2">
          {hasPermission('Finance.Read') && <Button variant="outline" onClick={() => setReturnCreditOpen(true)}>Credit from supplier return</Button>}
          <Button
            onClick={() =>
              router.push('/finance/ap/supplier-debit-notes/create')
            }
          >
            <Plus className="mr-2 h-4 w-4" /> New debit note
          </Button>
        </div>}
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Register records</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-bold">
            {notes.length}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Awaiting approval</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-bold">
            {
              notes.filter((note) => note.statusName === 'PendingApproval')
                .length
            }
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Posted and available</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-bold">
            {formatCurrency(postedAvailable, 'GHS')}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="flex-row items-center justify-between gap-4 space-y-0">
          <CardTitle>Debit-note register</CardTitle>
          <div className="flex gap-2">
            <div className="relative w-72">
              <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search note, supplier or invoice..."
                className="pl-9"
              />
            </div>
            <Select
              value={status}
              onValueChange={(value) => setStatus(value as typeof status)}
            >
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {statuses.map((item) => (
                  <SelectItem key={item} value={item}>
                    {item}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardHeader>
        <CardContent>
          {error && (
            <p className="mb-4 text-sm text-destructive">
              {error instanceof Error
                ? error.message
                : 'Unable to load supplier debit notes.'}
            </p>
          )}
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Debit note</TableHead>
                  <TableHead>Supplier</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead>Original invoice</TableHead>
                  <TableHead className="text-right">Total</TableHead>
                  <TableHead className="text-right">Remaining</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading &&
                  [...Array(5)].map((_, index) => (
                    <TableRow key={index}>
                      <TableCell colSpan={7}>
                        <Skeleton className="h-6 w-full" />
                      </TableCell>
                    </TableRow>
                  ))}
                {!isLoading && visible.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={7}
                      className="h-28 text-center text-muted-foreground"
                    >
                      No supplier debit notes match this view.
                    </TableCell>
                  </TableRow>
                )}
                {visible.map((note) => (
                  <TableRow
                    key={note.id}
                    className="cursor-pointer"
                    onClick={() =>
                      router.push(`/finance/ap/supplier-debit-notes/${note.id}`)
                    }
                  >
                    <TableCell className="font-semibold text-primary">
                      {note.debitNoteNumber}
                    </TableCell>
                    <TableCell>{note.vendorName}</TableCell>
                    <TableCell>
                      {format(new Date(note.debitNoteDate), 'dd MMM yyyy')}
                    </TableCell>
                    <TableCell>
                      {note.originalVendorInvoiceNumber ?? 'Standalone'}
                    </TableCell>
                    <TableCell className="text-right">
                      {formatCurrency(note.totalAmount, note.currencyCode)}
                    </TableCell>
                    <TableCell className="text-right">
                      {formatCurrency(note.remainingAmount, note.currencyCode)}
                    </TableCell>
                    <TableCell>{statusBadge(note.statusName)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
      {returnCreditOpen && <InventoryReturnCreditEntry open={returnCreditOpen} onOpenChange={setReturnCreditOpen} />}
    </div>
  );
}
