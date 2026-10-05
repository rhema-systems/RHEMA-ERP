'use client';

import { useState } from 'react';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import type { Account } from '@/types/finance';

interface Props { accounts: Account[]; value: string; lineNumber: number; onChange: (value: string) => void; }

export function RecurringJournalAccountCombobox({ accounts, value, lineNumber, onChange }: Props) {
  const [open, setOpen] = useState(false);
  const selected = accounts.find(account => account.id === value);
  return <Popover open={open} onOpenChange={setOpen}>
    <PopoverTrigger asChild><Button type="button" variant="outline" role="combobox" aria-label={`Line ${lineNumber} posting account`} aria-expanded={open} className="w-full justify-between font-normal"><span className="truncate">{selected ? `${selected.accountCode} · ${selected.accountName}` : 'Select posting account'}</span><ChevronsUpDown className="h-4 w-4 opacity-50" /></Button></PopoverTrigger>
    <PopoverContent className="w-[360px] p-0" align="start"><Command><CommandInput placeholder="Search posting accounts..." /><CommandList><CommandEmpty>No eligible posting account found.</CommandEmpty><CommandGroup>{accounts.map(account => <CommandItem key={account.id} value={`${account.accountCode} ${account.accountName}`} onSelect={() => { onChange(account.id); setOpen(false); }}><Check className={cn('mr-2 h-4 w-4', account.id === value ? 'opacity-100' : 'opacity-0')} /><span className="truncate">{account.accountCode} · {account.accountName}</span></CommandItem>)}</CommandGroup></CommandList></Command></PopoverContent>
  </Popover>;
}
