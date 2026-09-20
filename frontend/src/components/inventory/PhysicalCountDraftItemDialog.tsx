'use client';

import React, { useEffect, useState } from 'react';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { inventoryManagementService, type AddCountItemDto, type BinStockDto, type PhysicalCountDetailDto, type WarehouseLocationDto } from '@/services/inventoryManagementService';

export function PhysicalCountDraftItemDialog({ open, count, busy, onOpenChange, onSave }: {
  open: boolean;
  count: PhysicalCountDetailDto;
  busy: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (item: AddCountItemDto) => Promise<boolean>;
}) {
  const [items, setItems] = useState<BinStockDto[]>([]);
  const [locations, setLocations] = useState<WarehouseLocationDto[]>([]);
  const [itemId, setItemId] = useState('');
  const [locationId, setLocationId] = useState('');
  const [loading, setLoading] = useState(false);
  const [itemsLoading, setItemsLoading] = useState(false);
  const [itemsError, setItemsError] = useState('');
  const [error, setError] = useState('');
  const [selectOpen, setSelectOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setItemId(''); setLocationId(count.locationId || ''); setError(''); setLoading(true);
    setItems([]); setLocations([]); setSelectOpen(false); setItemsError('');
    inventoryManagementService.getWarehouseLocations(count.warehouseId).then(bins => {
      if (cancelled) return;
      setLocations(bins.filter(bin => bin.isActive && bin.warehouseId === count.warehouseId && (!count.locationId || bin.id === count.locationId)));
    }).catch(() => { if (!cancelled) setError('Unable to load warehouse locations. Close and try again.'); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [open, count.id, count.warehouseId, count.locationId]);

  const validLocation = locations.some(location => location.id === locationId);
  useEffect(() => {
    let cancelled = false;
    setItems([]); setItemId(''); setItemsError(''); setSelectOpen(false);
    if (!open || !validLocation) { setItemsLoading(false); return; }
    setItemsLoading(true);
    (async () => {
      const rows: BinStockDto[] = [];
      for (let page = 1; ; page++) {
        const result = await inventoryManagementService.getBinStock({
          warehouseId: count.warehouseId, locationId, includeZero: true, page, pageSize: 200,
        });
        if (cancelled) return;
        rows.push(...result.items);
        if (rows.length >= result.totalCount) break;
        if (result.items.length === 0) throw new Error('Incomplete location inventory result.');
      }
      const unique = new Map(rows.filter(item => item.warehouseId === count.warehouseId && item.locationId === locationId)
        .map(item => [item.inventoryItemId, item]));
      if (!cancelled) setItems([...unique.values()]);
    })().catch(() => { if (!cancelled) setItemsError('Unable to load the items for this location. Close and try again.'); })
      .finally(() => { if (!cancelled) setItemsLoading(false); });
    return () => { cancelled = true; };
  }, [open, count.id, count.warehouseId, locationId, validLocation]);

  const available = items.filter(item => !count.items.some(line =>
    line.inventoryItemId === item.inventoryItemId && (line.locationId || count.locationId) === item.locationId));
  const selected = available.find(item => item.inventoryItemId === itemId && item.locationId === locationId);
  const canSave = count.status === 'Draft' && !!selected && validLocation && !busy && !loading && !itemsLoading && !error && !itemsError;

  return <Dialog open={open} onOpenChange={value => { if (!busy) onOpenChange(value); }}>
    <DialogContent className="sm:max-w-lg max-h-[90dvh] overflow-y-auto">
      <DialogHeader><DialogTitle>Add count item</DialogTitle><DialogDescription>{count.locationId ? 'Only items assigned to the selected count location are available.' : 'Choose a location in this warehouse, then an item assigned to it.'} Quantities are recorded after the count starts.</DialogDescription></DialogHeader>
      {(error || itemsError) && <p role="alert" className="text-sm text-destructive">{error || itemsError}</p>}
      <div className="space-y-2"><Label htmlFor="count-item-location">Location</Label><Select value={locationId} onValueChange={setLocationId} disabled={loading || busy || !!count.locationId}>
        <SelectTrigger id="count-item-location"><SelectValue placeholder="Select a location" /></SelectTrigger><SelectContent>{locations.map(location => <SelectItem key={location.id} value={location.id}>{location.locationCode}{location.name ? ` - ${location.name}` : ''}</SelectItem>)}</SelectContent>
      </Select>{!loading && !error && locations.length === 0 && <p className="text-sm text-destructive">No active location is available for this count.</p>}</div>
      <div className="space-y-2">
        <Label>Item</Label>
        <Popover open={selectOpen} onOpenChange={setSelectOpen}>
          <PopoverTrigger asChild><Button type="button" variant="outline" role="combobox" aria-label="Count item" aria-expanded={selectOpen} disabled={loading || itemsLoading || busy || !validLocation || !!error || !!itemsError} className="w-full justify-between">
            <span className="truncate">{loading || itemsLoading ? 'Loading items...' : !validLocation ? 'Select a location first' : selected ? `${selected.itemCode} - ${selected.itemName}` : 'Select an item...'}</span><ChevronsUpDown className="ml-2 h-4 w-4 shrink-0" />
          </Button></PopoverTrigger>
          <PopoverContent align="start" className="w-[var(--radix-popover-trigger-width)] p-0"><Command>
            <CommandInput placeholder="Search by name or code..." aria-label="Search count items" />
            <CommandList><CommandEmpty>No additional items in this location.</CommandEmpty><CommandGroup>
              {available.map(item => <CommandItem key={item.inventoryItemId} value={item.inventoryItemId} keywords={[item.itemCode, item.itemName]} onSelect={() => { setItemId(item.inventoryItemId); setSelectOpen(false); }}>
                <Check className={`mr-2 h-4 w-4 ${itemId === item.inventoryItemId ? 'opacity-100' : 'opacity-0'}`} /><div><div>{item.itemName}</div><div className="text-xs text-muted-foreground">{item.itemCode}</div></div>
              </CommandItem>)}
            </CommandGroup></CommandList>
          </Command></PopoverContent>
        </Popover>
      </div>
      <DialogFooter><Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>Cancel</Button><Button disabled={!canSave} onClick={async () => { if (canSave && await onSave({ inventoryItemId: itemId, locationId })) onOpenChange(false); }}>{busy ? 'Adding...' : 'Add item'}</Button></DialogFooter>
    </DialogContent>
  </Dialog>;
}
