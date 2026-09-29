'use client';

/**
 * Choose the Finance (Sales) customer a consulting client is billed as (lane 8, slice 6).
 *
 * ⚠ Reads `api/hr/customers`, HR's own narrow projection of the Sales customer master — see
 * `consultantClientService.searchCustomers`. The chosen CUSTOMER ID is what is stored on the
 * client; leaving it empty is legitimate and means the invoices stay HR-side. The selected
 * customer is fetched on its own so it survives a search that excludes it, and so a client
 * linked to a since-deactivated customer still shows which one.
 *
 * Deliberately the same shape as {@link ../common/SupplierPicker}, which does the AP-side job.
 */

import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import { consultantClientService } from '@/services/hr/consultant.service';
import type { HrCustomerOption } from '@/types/hr/consultant';

export const hrCustomerSearchKey = (search: string) => ['hr', 'customers', 'search', search] as const;
export const hrCustomerKey = (id: string) => ['hr', 'customers', 'selected', id] as const;

function format(c?: HrCustomerOption | null): string {
  if (!c) return 'No Finance customer';
  return `${c.code} — ${c.name}`;
}

export function FinanceCustomerPicker({
  id,
  label = 'Finance customer',
  value,
  onChange,
  disabled,
  hint = 'The Finance customer this client is billed as. Without it, invoices stay HR-side.',
  noneLabel = 'No Finance customer — invoices stay HR-side',
}: {
  id?: string;
  label?: string;
  value?: string | null;
  onChange: (customerId: string | null, customer: HrCustomerOption | null) => void;
  disabled?: boolean;
  hint?: string;
  noneLabel?: string;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');

  const { data: customers = [], isLoading } = useQuery({
    queryKey: hrCustomerSearchKey(search),
    queryFn: () => consultantClientService.searchCustomers(search || undefined),
  });

  const { data: selectedCustomer } = useQuery({
    queryKey: hrCustomerKey(value ?? ''),
    queryFn: () => consultantClientService.getCustomer(value as string),
    enabled: !!value,
  });

  const options = useMemo(() => {
    if (!selectedCustomer) return customers;
    return customers.some((c) => c.id === selectedCustomer.id) ? customers : [selectedCustomer, ...customers];
  }, [customers, selectedCustomer]);

  useEffect(() => {
    if (!open) setSearch('');
  }, [open]);

  const selected = options.find((c) => c.id === value) ?? selectedCustomer ?? null;

  return (
    <div className="space-y-2">
      {label && <Label htmlFor={id}>{label}</Label>}
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            id={id}
            type="button"
            variant="outline"
            role="combobox"
            aria-expanded={open}
            disabled={disabled}
            className="w-full justify-between font-normal"
          >
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
              <CommandEmpty>{isLoading ? 'Searching…' : 'No Finance customer found.'}</CommandEmpty>
              <CommandGroup>
                <CommandItem value="__none__" onSelect={() => { onChange(null, null); setOpen(false); }}>
                  <Check className={cn('mr-2 h-4 w-4', !value ? 'opacity-100' : 'opacity-0')} />
                  {noneLabel}
                </CommandItem>
                {options.map((c) => (
                  <CommandItem key={c.id} value={c.id} onSelect={() => { onChange(c.id, c); setOpen(false); }}>
                    <Check className={cn('mr-2 h-4 w-4', value === c.id ? 'opacity-100' : 'opacity-0')} />
                    <span className="truncate">
                      {format(c)}
                      {c.currencyCode ? ` · ${c.currencyCode}` : ''}
                      {!c.isActive ? ' (inactive)' : ''}
                    </span>
                  </CommandItem>
                ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}
