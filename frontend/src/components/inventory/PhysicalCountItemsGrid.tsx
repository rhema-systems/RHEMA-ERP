'use client';

import React, { useMemo, useState } from 'react';
import { CheckCircle, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Clock, FileDown, FileUp, Info, Maximize2, Minimize2, Plus, Search, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import type { PhysicalCountDetailDto, PhysicalCountItemDto } from '@/services/inventoryManagementService';
import { InventoryCostValue } from './InventoryCostValue';

type Props = {
  count: PhysicalCountDetailDto;
  edits: Map<string, number>;
  busy: boolean;
  fullPage: boolean;
  currencyCode?: string;
  onToggleFullPage: () => void;
  onQuantity: (id: string, value: number | undefined) => void;
  onRemove: (item: PhysicalCountItemDto) => void;
  onAdd: () => void;
  onImport: () => void;
  onExport: () => void;
};

export function PhysicalCountItemsGrid({ count, edits, busy, fullPage, currencyCode, onToggleFullPage, onQuantity, onRemove, onAdd, onImport, onExport }: Props) {
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(25);
  const [showCosts, setShowCosts] = useState(false);
  const costsVisible = showCosts && count.systemQuantityVisible;
  const filtered = useMemo(() => {
    const term = query.trim().toLowerCase();
    return count.items.filter(item => !term || [item.itemCode, item.itemName, item.locationName].some(value => value?.toLowerCase().includes(term)));
  }, [count.items, query]);
  const pages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentPage = Math.min(page, pages - 1);
  const start = currentPage * pageSize;
  const items = filtered.slice(start, start + pageSize);
  const editable = ['InProgress', 'UnderReview'].includes(count.status) && !!count.canReview;
  const draft = count.status === 'Draft';

  return <div className="flex min-h-0 flex-1 flex-col gap-2" data-testid="count-items-grid">
    <div className="flex shrink-0 flex-wrap items-center gap-2">
      <div className="relative min-w-40 flex-1 sm:max-w-64"><Search className="pointer-events-none absolute left-2 top-2 h-3.5 w-3.5 text-muted-foreground" /><Input aria-label="Search count items" placeholder="Search code, name or location" value={query} onChange={e => { setQuery(e.target.value); setPage(0); }} className="h-8 pl-7 text-xs md:text-xs" /></div>
      <Button variant="outline" size="sm" className="h-8 px-2 text-xs" onClick={onExport}><FileDown className="mr-1 h-3.5 w-3.5" />Download count sheet</Button>
      {editable && <Button variant="outline" size="sm" className="h-8 px-2 text-xs" disabled={busy || edits.size > 0} onClick={onImport}><FileUp className="mr-1 h-3.5 w-3.5" />Upload count sheet</Button>}
      {draft && <Button size="sm" className="h-8 px-2 text-xs" disabled={busy} onClick={onAdd}><Plus className="mr-1 h-3.5 w-3.5" />Add item</Button>}
      {count.systemQuantityVisible && <Button variant="outline" size="sm" className="h-8 px-2 text-xs" aria-pressed={showCosts} onClick={() => setShowCosts(value => !value)}>{showCosts ? 'Hide costs' : 'Show costs'}</Button>}
      <Button variant="outline" size="sm" className="ml-auto h-8 px-2 text-xs" onClick={onToggleFullPage} aria-label={fullPage ? 'Restore dialog' : 'View items in full page'}>{fullPage ? <Minimize2 className="mr-1 h-3.5 w-3.5" /> : <Maximize2 className="mr-1 h-3.5 w-3.5" />}{fullPage ? 'Restore' : 'Full page'}</Button>
    </div>
    {costsVisible && <p className="shrink-0 text-xs text-muted-foreground">Count unit cost is the saved cost used for this count. Select the information icon for the current average.</p>}
    <div className="min-h-0 flex-1 overflow-auto rounded-md border" role="region" aria-label="Count items grid" tabIndex={0}>
      <table className={`w-full ${costsVisible ? 'min-w-[940px]' : 'min-w-[800px]'} table-fixed border-separate border-spacing-0 text-xs tabular-nums [&_th]:h-7 [&_th]:px-2 [&_th]:py-1 [&_th]:text-[11px] [&_th]:tracking-normal [&_td]:px-2 [&_td]:py-0.5 [&_td]:leading-4`}>
        <colgroup><col className="w-10" /><col className="w-40" /><col /><col className="w-28" /><col className="w-24" /><col className="w-24" /><col className="w-24" />{costsVisible && <col className="w-36" />}<col className="w-12" />{draft && <col className="w-12" />}</colgroup>
        <TableHeader className="sticky top-0 z-10 bg-slate-100 shadow-sm dark:bg-slate-900"><TableRow>
          <TableHead>#</TableHead><TableHead>Item code</TableHead><TableHead>Item name</TableHead><TableHead>Location</TableHead><TableHead className="text-right">System Qty</TableHead><TableHead className="text-right">Counted Qty</TableHead><TableHead className="text-right">Variance</TableHead>{costsVisible && <TableHead className="text-right">Count unit cost</TableHead>}<TableHead><span className="sr-only">Status</span></TableHead>{draft && <TableHead><span className="sr-only">Actions</span></TableHead>}
        </TableRow></TableHeader>
        <TableBody>
          {items.length === 0 && <TableRow><TableCell colSpan={(draft ? 9 : 8) + (costsVisible ? 1 : 0)} className="h-20 text-center text-muted-foreground">{count.items.length ? 'No items match your search.' : `No items in this count.${draft ? ' Add an item before starting.' : ''}`}</TableCell></TableRow>}
          {items.map((item, index) => <TableRow key={item.id} className="h-8">
            <TableCell className="text-muted-foreground">{start + index + 1}</TableCell>
            <TableCell><div className="truncate font-medium" title={item.itemCode}>{item.itemCode}</div></TableCell>
            <TableCell><div className="truncate" title={item.itemName}>{item.itemName}</div></TableCell>
            <TableCell><div className="truncate text-muted-foreground" title={item.locationName || 'No location'}>{item.locationName || '—'}</div></TableCell>
            <TableCell className="text-right">{count.systemQuantityVisible ? item.systemQuantity : <span className="text-muted-foreground">xxx</span>}</TableCell>
            <TableCell className="text-right">{editable ? <Input type="number" className="ml-auto h-6 w-20 rounded px-1.5 py-0 text-right text-xs md:text-xs" aria-label={`Counted quantity ${item.itemCode}`} min="0" step="0.0001" disabled={busy} placeholder={item.isCounted ? 'Saved' : ''} value={edits.get(item.id) ?? (count.systemQuantityVisible && item.isCounted ? item.countedQuantity : '')} onChange={e => onQuantity(item.id, e.target.value === '' ? undefined : Number(e.target.value))} /> : count.systemQuantityVisible ? item.countedQuantity : <span className="text-muted-foreground">xxx</span>}</TableCell>
            <TableCell className="text-right">{count.systemQuantityVisible ? <span className={`inline-block min-w-9 rounded px-1 py-0.5 font-medium ${item.varianceQuantity > 0 ? 'bg-green-100 text-green-800' : item.varianceQuantity < 0 ? 'bg-red-100 text-red-800' : 'text-muted-foreground'}`}>{item.varianceQuantity > 0 ? '+' : ''}{item.varianceQuantity}</span> : <span className="text-muted-foreground">xxx</span>}</TableCell>
            {costsVisible && <TableCell className="text-right"><div className="flex items-center justify-end gap-1"><InventoryCostValue value={item.countUnitCost} kind="count" currencyCode={currencyCode} /><Popover><PopoverTrigger asChild><Button variant="ghost" size="sm" className="h-6 w-6 p-0 text-muted-foreground" aria-label={`Current cost details ${item.itemCode}`}><Info className="h-3.5 w-3.5" /></Button></PopoverTrigger><PopoverContent align="end" className="w-72 space-y-2 text-sm"><p className="font-medium">Current item average</p><InventoryCostValue value={item.itemAverageCost} kind="item" currencyCode={currencyCode} /><p className="text-xs text-muted-foreground">Current average across owned warehouses. Later stock transactions may change it; the saved count unit cost remains unchanged.</p></PopoverContent></Popover></div></TableCell>}
            <TableCell>{item.isCounted ? <CheckCircle className="mx-auto h-3.5 w-3.5 text-green-600" aria-label="Counted" /> : <Clock className="mx-auto h-3.5 w-3.5 text-gray-400" aria-label="Not counted" />}</TableCell>
            {draft && <TableCell><Button variant="ghost" size="sm" className="h-6 w-6 p-0" aria-label={`Remove ${item.itemCode}`} title={`Remove ${item.itemCode}`} disabled={busy} onClick={() => onRemove(item)}><Trash2 className="h-3.5 w-3.5" /></Button></TableCell>}
          </TableRow>)}
        </TableBody>
      </table>
    </div>
    <div className="flex shrink-0 flex-wrap items-center justify-between gap-2 text-xs">
      <span aria-live="polite">{filtered.length ? start + 1 : 0}–{Math.min(start + pageSize, filtered.length)} of {filtered.length} items{filtered.length !== count.items.length ? ` (${count.items.length} total)` : ''}</span>
      <div className="flex items-center gap-2"><label className="flex items-center gap-1">Rows <select aria-label="Rows per page" className="h-7 rounded border bg-background px-1 text-xs" value={pageSize} onChange={e => { setPageSize(Number(e.target.value)); setPage(0); }}>{[25, 50, 100].map(size => <option key={size} value={size}>{size}</option>)}</select></label>
        <span>Page {currentPage + 1} of {pages}</span>
        <Button variant="outline" className="h-7 w-7 p-0" aria-label="First page" disabled={currentPage === 0} onClick={() => setPage(0)}><ChevronsLeft className="h-3.5 w-3.5" /></Button>
        <Button variant="outline" className="h-7 w-7 p-0" aria-label="Previous page" disabled={currentPage === 0} onClick={() => setPage(currentPage - 1)}><ChevronLeft className="h-3.5 w-3.5" /></Button>
        <Button variant="outline" className="h-7 w-7 p-0" aria-label="Next page" disabled={currentPage === pages - 1} onClick={() => setPage(currentPage + 1)}><ChevronRight className="h-3.5 w-3.5" /></Button>
        <Button variant="outline" className="h-7 w-7 p-0" aria-label="Last page" disabled={currentPage === pages - 1} onClick={() => setPage(pages - 1)}><ChevronsRight className="h-3.5 w-3.5" /></Button>
      </div>
    </div>
  </div>;
}
