'use client';

/**
 * Choose the Procurement supplier a recruitment cost is paid to (round 2b, R7).
 *
 * ⚠ Reads `api/hr/suppliers`, HR's own narrow projection, not `api/Suppliers` — see
 * `hr-supplier.service.ts`. The chosen SUPPLIER ID is what is stored; the name the cost keeps is
 * a snapshot taken at save time. The selected supplier is fetched on its own so it survives a
 * search that excludes it, and so a cost paid to a since-deactivated supplier still shows who.
 */

import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import { hrSupplierService, type HrSupplierOption } from '@/services/hr/hr-supplier.service';

function format(s?: HrSupplierOption | null): string {
  if (!s) return 'No supplier';
  return `${s.code} — ${s.name}`;
}

export function SupplierPicker({
  id,
  label = 'Supplier',
  value,
  onChange,
  disabled,
  noneLabel = 'No supplier — name the payee instead',
}: {
  id?: string;
  label?: string;
  value?: string | null;
  onChange: (supplierId: string | null, supplier: HrSupplierOption | null) => void;
  disabled?: boolean;
  noneLabel?: string;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');

  const { data: suppliers = [], isLoading } = useQuery({
    queryKey: ['hr', 'suppliers', search],
    queryFn: () => hrSupplierService.search({ search: search || undefined, take: 50 }),
  });

  const { data: selectedSupplier } = useQuery({
    queryKey: ['hr', 'suppliers', 'selected', value],
    queryFn: () => hrSupplierService.getById(value as string),
    enabled: !!value,
  });

  const options = useMemo(() => {
    if (!selectedSupplier) return suppliers;
    return suppliers.some((s) => s.id === selectedSupplier.id) ? suppliers : [selectedSupplier, ...suppliers];
  }, [suppliers, selectedSupplier]);

  useEffect(() => {
    if (!open) setSearch('');
  }, [open]);

  const selected = options.find((s) => s.id === value) ?? selectedSupplier ?? null;

  return (
    <div className="space-y-2">
      {label && <Label htmlFor={id}>{label}</Label>}
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button id={id} type="button" variant="outline" role="combobox" aria-expanded={open} disabled={disabled} className="w-full justify-between font-normal">
            <span className="truncate">
              {format(selected)}
              {selected && !selected.isActive ? ' (inactive)' : ''}
            </span>
            <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-[460px] p-0" align="start">
          <Command shouldFilter={false}>
            <CommandInput placeholder="Search code or name…" value={search} onValueChange={setSearch} />
            <CommandList>
              <CommandEmpty>{isLoading ? 'Searching…' : 'No supplier found.'}</CommandEmpty>
              <CommandGroup>
                <CommandItem value="__none__" onSelect={() => { onChange(null, null); setOpen(false); }}>
                  <Check className={cn('mr-2 h-4 w-4', !value ? 'opacity-100' : 'opacity-0')} />
                  {noneLabel}
                </CommandItem>
                {options.map((s) => (
                  <CommandItem key={s.id} value={s.id} onSelect={() => { onChange(s.id, s); setOpen(false); }}>
                    <Check className={cn('mr-2 h-4 w-4', value === s.id ? 'opacity-100' : 'opacity-0')} />
                    <span className="truncate">
                      {format(s)}
                      {!s.isActive ? ' (inactive)' : ''}
                    </span>
                  </CommandItem>
                ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
    </div>
  );
}
