'use client';

import React, { useState } from 'react';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';

export interface RequisitionItemOption {
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  availableStock: number;
  unitOfMeasure?: string;
  locationLabel?: string;
}

export function RequisitionItemSelect({ items, value, onChange, loading, disabled }: {
  items: RequisitionItemOption[];
  value: string;
  onChange: (value: string) => void;
  loading: boolean;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const selected = items.find(item => item.inventoryItemId === value);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button type="button" variant="outline" role="combobox" aria-label="Inventory item"
          aria-expanded={open} disabled={disabled || loading} className="h-auto min-h-10 w-full justify-between text-left">
          <span className="truncate">{loading ? 'Loading items...' : selected
            ? `${selected.itemCode} - ${selected.itemName}` : 'Select an item...'}</span>
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-[var(--radix-popover-trigger-width)] p-0">
        <Command label="Search inventory items">
          <CommandInput aria-label="Search inventory items" placeholder="Search by item name or code..." />
          <CommandList>
            <CommandEmpty>No matching items in this warehouse/location.</CommandEmpty>
            <CommandGroup>
              {items.map(item => (
                <CommandItem key={item.inventoryItemId} value={item.inventoryItemId}
                  keywords={[item.itemCode, item.itemName, item.locationLabel || '']}
                  onSelect={() => { onChange(item.inventoryItemId); setOpen(false); }}>
                  <Check className={`mr-2 h-4 w-4 shrink-0 ${value === item.inventoryItemId ? 'opacity-100' : 'opacity-0'}`} />
                  <div className="min-w-0">
                    <div className="font-medium">{item.itemName}</div>
                    <div className="text-xs text-muted-foreground">{item.itemCode} · Available: {item.availableStock} {item.unitOfMeasure}{item.locationLabel ? ` · ${item.locationLabel}` : ''}</div>
                  </div>
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
