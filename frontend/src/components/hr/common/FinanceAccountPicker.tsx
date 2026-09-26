'use client';

/**
 * Choose the chart-of-accounts row a unit or a team is charged to (demo feedback round 2, lane B2;
 * plan § 1.1 and § 6.2).
 *
 * ⚠ Reads `api/hr/finance-accounts`, HR's own narrow projection, NOT `api/finance/accounts`. The
 * Finance endpoint is gated on a Finance permission by a convention map, so an HR user gets a 403
 * and a picker fed from it renders empty for exactly the people who use it — the same trap the
 * currency pickers hit before `HrCurrenciesController` existed.
 *
 * ⚠ The chosen ACCOUNT ID is what is stored. The code shown here is Finance's, and the copy the
 * unit keeps is a snapshot taken at save time; renaming the account in Finance does not chase it.
 *
 * The search runs server-side and the list is capped, because a chart of accounts is long. Typing
 * narrows it; the currently selected account is always present even when it falls outside the cap
 * or has since been deactivated, so the box never goes blank over a real value.
 */

import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import { financeAccountService } from '@/services/hr/finance-account.service';
import type { HrFinanceAccount } from '@/types/hr/finance-account';

function format(account?: HrFinanceAccount | null): string {
  if (!account) return 'No account';
  const code = account.accountNumber || account.accountCode;
  return `${code} — ${account.accountName}`;
}

interface Props {
  id?: string;
  label?: string;
  value?: string | null;
  onChange: (accountId: string | null) => void;
  disabled?: boolean;
  /** Shown under the control; the forms use it to explain what the code is for. */
  description?: string;
}

export function FinanceAccountPicker({ id, label = 'Account code', value, onChange, disabled, description }: Props) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');

  const { data: accounts = [], isLoading } = useQuery({
    queryKey: ['hr', 'finance-accounts', search],
    queryFn: () => financeAccountService.search({ search: search || undefined, take: 50 }),
  });

  // The selected account, fetched on its own so it survives a search that excludes it — and so a
  // unit charged to a since-deactivated account still shows what it is charged to.
  const { data: selectedAccount } = useQuery({
    queryKey: ['hr', 'finance-accounts', 'selected', value],
    queryFn: () => financeAccountService.getById(value as string),
    enabled: !!value,
  });

  const options = useMemo(() => {
    if (!selectedAccount) return accounts;
    return accounts.some((a) => a.id === selectedAccount.id) ? accounts : [selectedAccount, ...accounts];
  }, [accounts, selectedAccount]);

  // Reset the typed search when the popover closes, so reopening starts from the top of the chart
  // rather than from whatever was last typed.
  useEffect(() => {
    if (!open) setSearch('');
  }, [open]);

  const selected = options.find((a) => a.id === value) ?? selectedAccount ?? null;

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
            <CommandInput placeholder="Search code, number or name…" value={search} onValueChange={setSearch} />
            <CommandList>
              <CommandEmpty>{isLoading ? 'Searching…' : 'No account found.'}</CommandEmpty>
              <CommandGroup>
                <CommandItem
                  value="__none__"
                  onSelect={() => {
                    onChange(null);
                    setOpen(false);
                  }}
                >
                  <Check className={cn('mr-2 h-4 w-4', !value ? 'opacity-100' : 'opacity-0')} />
                  No account
                </CommandItem>
                {options.map((account) => (
                  <CommandItem
                    key={account.id}
                    value={account.id}
                    onSelect={() => {
                      onChange(account.id);
                      setOpen(false);
                    }}
                  >
                    <Check className={cn('mr-2 h-4 w-4', value === account.id ? 'opacity-100' : 'opacity-0')} />
                    <span className="truncate">
                      {format(account)}
                      {account.isActive ? '' : ' (inactive)'}
                    </span>
                  </CommandItem>
                ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
      {description && <p className="text-xs text-muted-foreground">{description}</p>}
    </div>
  );
}
