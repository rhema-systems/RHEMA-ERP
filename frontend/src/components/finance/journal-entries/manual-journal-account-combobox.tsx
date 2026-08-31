'use client';

import React, { useState } from 'react';
import { Check, ChevronsUpDown, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import { isAccountEligibleForBook } from '@/lib/finance/accounting-books';
import type { Account, AccountingBook } from '@/types/finance';

interface ManualJournalAccountComboboxProps {
  accounts: Account[];
  selectedAccountId: string;
  lineNumber: number;
  targetAccountingBooks: AccountingBook[];
  fallbackBookCode: string;
  targetBookLabel: string;
  onSelect: (accountId: string) => void | Promise<void>;
  onClear: () => void;
}

/**
 * Shared searchable account control for manual journal create and edit forms.
 * Keeping one implementation prevents edit mode from regressing to an
 * unsearchable select as the chart of accounts grows.
 */
export function ManualJournalAccountCombobox({
  accounts,
  selectedAccountId,
  lineNumber,
  targetAccountingBooks,
  fallbackBookCode,
  targetBookLabel,
  onSelect,
  onClear,
}: ManualJournalAccountComboboxProps) {
  const [open, setOpen] = useState(false);
  const selectedAccount = accounts.find(account => account.id === selectedAccountId);
  const selectedLabel = selectedAccount
    ? `${selectedAccount.accountNumber || selectedAccount.accountCode} - ${selectedAccount.accountName}`
    : 'Select Account';

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-label={`Line ${lineNumber} account`}
          aria-expanded={open}
          className="h-10 w-full justify-between truncate text-left font-normal"
        >
          <span className="truncate">{selectedLabel}</span>
          <ChevronsUpDown className="ml-1 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[350px] p-0" align="start">
        <div className="border-b p-2">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="w-full justify-start"
            disabled={!selectedAccountId}
            onClick={() => {
              onClear();
              setOpen(false);
            }}
          >
            <X className="mr-2 h-4 w-4" />
            Clear account
          </Button>
        </div>
        <Command>
          <CommandInput placeholder="Search accounts..." />
          <CommandList>
            <CommandEmpty>No account found.</CommandEmpty>
            <CommandGroup>
              {accounts.map(account => {
                const eligible = targetAccountingBooks.length > 0
                  ? targetAccountingBooks.every(book => isAccountEligibleForBook(account, book.code))
                  : isAccountEligibleForBook(account, fallbackBookCode);
                const code = account.accountNumber || account.accountCode;
                return (
                  <CommandItem
                    key={account.id}
                    value={`${code} ${account.accountName}`}
                    disabled={!eligible}
                    onSelect={() => {
                      if (!eligible) return;
                      void onSelect(account.id);
                      setOpen(false);
                    }}
                    className={!eligible ? 'cursor-not-allowed opacity-50' : undefined}
                    title={!eligible ? `Not classified for ${targetBookLabel}` : undefined}
                  >
                    <Check
                      className={cn(
                        'mr-2 h-4 w-4',
                        selectedAccountId === account.id ? 'opacity-100' : 'opacity-0'
                      )}
                    />
                    <span className="truncate">{code} - {account.accountName}</span>
                    {!eligible && (
                      <span className="ml-2 rounded border border-amber-400 bg-amber-50 px-1.5 py-0.5 text-[10px] font-medium text-amber-800">
                        Not classified for {targetBookLabel}
                      </span>
                    )}
                  </CommandItem>
                );
              })}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
