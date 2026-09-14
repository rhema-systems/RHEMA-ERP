'use client';

import React, { useState } from 'react';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import type { Account } from '@/types/finance';
import { cn } from '@/lib/utils';

export interface PostingAccountPickerProps {
  id: string;
  value?: string | null;
  accounts: Pick<Account, 'id' | 'accountCode' | 'accountNumber' | 'accountName'>[];
  onChange: (value: string | null) => void;
  placeholder?: string;
  disabled?: boolean;
  allowClear?: boolean;
  invalidValueLabel?: string;
}

/** Search the tenant's existing GL catalogue; clearing is an explicit null. */
export function PostingAccountPicker({ id, value, accounts, onChange, placeholder = 'Search accounts...', disabled,
  allowClear = true, invalidValueLabel = 'Saved account unavailable',
}: PostingAccountPickerProps) {
  const [open, setOpen] = useState(false);
  const selected = accounts.find(account => account.id === value);
  const label = (account: PostingAccountPickerProps['accounts'][number]) => `${account.accountNumber || account.accountCode} — ${account.accountName}`;

  return <Popover open={open} onOpenChange={setOpen}>
    <PopoverTrigger asChild>
      <Button id={id} type="button" variant="outline" role="combobox" aria-expanded={open} disabled={disabled}
        className="h-9 w-full min-w-0 justify-between font-normal">
        <span className="truncate">{selected ? label(selected) : value ? invalidValueLabel : 'Use default'}</span>
        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
      </Button>
    </PopoverTrigger>
    <PopoverContent className="w-[var(--radix-popover-trigger-width)] min-w-[280px] max-w-[calc(100vw-2rem)] p-0" align="start">
      <Command>
        <CommandInput placeholder={placeholder} />
        <CommandList>
          <CommandEmpty>No account found.</CommandEmpty>
          <CommandGroup>
            {allowClear && <CommandItem value="Use default clear account" onSelect={() => { onChange(null); setOpen(false); }}>
              <Check className={cn('mr-2 h-4 w-4', !value ? 'opacity-100' : 'opacity-0')} />Use default
            </CommandItem>}
            {accounts.map(account => <CommandItem key={account.id}
              value={`${account.accountCode} ${account.accountNumber} ${account.accountName}`}
              onSelect={() => { onChange(account.id); setOpen(false); }}>
              <Check className={cn('mr-2 h-4 w-4 shrink-0', value === account.id ? 'opacity-100' : 'opacity-0')} />
              <span className="truncate">{label(account)}</span>
            </CommandItem>)}
          </CommandGroup>
        </CommandList>
      </Command>
    </PopoverContent>
  </Popover>;
}
