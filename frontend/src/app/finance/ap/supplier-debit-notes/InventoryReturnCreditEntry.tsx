'use client';

import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, RefreshCw } from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { SupplierReturnCreditPanel } from '@/app/inventory/supplier-returns/SupplierReturnCreditPanel';
import { inventoryReturnCreditService, type InventoryReturnCreditCandidate } from '@/services/inventoryReturnCreditService';

export function InventoryReturnCreditEntry({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission('Finance.Read');
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<InventoryReturnCreditCandidate | null>(null);
  const { data: returns = [], isLoading, error, refetch } = useQuery({
    queryKey: ['ap-return-credit-candidates', search],
    queryFn: () => inventoryReturnCreditService.getCandidates(search),
    enabled: open && canRead,
  });
  if (!canRead) return null;
  const failure = error as { response?: { data?: { detail?: string; message?: string; error?: string } }; message?: string } | null;
  return <Dialog open={open} onOpenChange={value => { if (!value) setSelected(null); onOpenChange(value); }}>
    <DialogContent className="flex flex-col overflow-hidden" style={{ width: '760px', maxWidth: 'calc(100vw - 32px)', height: 'min(560px, calc(100vh - 48px))' }}>
      <DialogHeader><DialogTitle>Credit from supplier return</DialogTitle><DialogDescription>Choose a dispatched return linked to a posted goods invoice.</DialogDescription></DialogHeader>
      {selected ? <div className="min-h-0 flex-1 overflow-y-auto">
        <Button size="sm" variant="ghost" onClick={() => setSelected(null)}><ArrowLeft className="mr-2 h-4 w-4" />Back to returns</Button>
        <div className="mt-3 font-medium">{selected.returnNumber} · {selected.supplierName}</div>
        <SupplierReturnCreditPanel key={selected.returnId} returnId={selected.returnId} returnNumber={selected.returnNumber} reason={selected.reason} />
      </div> : <>
        <div className="flex shrink-0 gap-2"><Input aria-label="Search dispatched returns" placeholder="Search return or supplier..." value={search} maxLength={100} onChange={event => setSearch(event.target.value)} />
          <Button variant="outline" size="icon" aria-label="Refresh dispatched returns" onClick={() => void refetch()}><RefreshCw className="h-4 w-4" /></Button></div>
        {failure && <p role="alert" className="text-sm text-destructive">{failure.response?.data?.detail || failure.response?.data?.message || failure.response?.data?.error || failure.message || 'Unable to load dispatched returns.'}</p>}
        <div className="min-h-0 flex-1 overflow-auto rounded-md border"><Table>
          <TableHeader className="sticky top-0 bg-background"><TableRow><TableHead>Return</TableHead><TableHead>Supplier</TableHead><TableHead>Dispatched</TableHead><TableHead className="text-right">Qty</TableHead><TableHead><span className="sr-only">Action</span></TableHead></TableRow></TableHeader>
          <TableBody>
            {isLoading ? <TableRow><TableCell colSpan={5}>Loading returns...</TableCell></TableRow> : !returns.length && !error ? <TableRow><TableCell colSpan={5} className="py-8 text-center text-muted-foreground">No eligible uncredited returns. Dispatch the stock and post its original goods invoice first.</TableCell></TableRow> : returns.map(source => <TableRow key={source.returnId}>
              <TableCell className="py-2 font-medium">{source.returnNumber}</TableCell><TableCell className="py-2">{source.supplierName}</TableCell>
              <TableCell className="whitespace-nowrap py-2">{new Date(source.shippedDate).toLocaleDateString()}</TableCell><TableCell className="py-2 text-right">{source.totalQuantity.toLocaleString()}</TableCell>
              <TableCell className="py-2"><Button size="sm" variant="outline" aria-label={`Select ${source.returnNumber}`} onClick={() => setSelected(source)}>Select</Button></TableCell>
            </TableRow>)}
          </TableBody>
        </Table></div>
        {returns.length === 100 && <p className="text-xs text-muted-foreground">Showing 100 returns. Search to narrow the list.</p>}
      </>}
    </DialogContent>
  </Dialog>;
}
