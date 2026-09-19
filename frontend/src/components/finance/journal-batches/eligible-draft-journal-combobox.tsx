'use client';

import { Check, ChevronsUpDown, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import type { EligibleJournalBatchDraft } from '@/types/journal-batches';

interface EligibleDraftJournalComboboxProps {
    options: EligibleJournalBatchDraft[];
    selected?: EligibleJournalBatchDraft;
    search: string;
    onSearchChange: (value: string) => void;
    onSelect: (journal: EligibleJournalBatchDraft) => void;
    open: boolean;
    onOpenChange: (open: boolean) => void;
    loading: boolean;
    error?: string;
    currencyCode: string;
    disabled?: boolean;
}

const amount = (value: number, currencyCode: string) =>
    new Intl.NumberFormat('en-GH', { style: 'currency', currency: currencyCode }).format(value);

export function EligibleDraftJournalCombobox({
    options,
    selected,
    search,
    onSearchChange,
    onSelect,
    open,
    onOpenChange,
    loading,
    error,
    currencyCode,
    disabled = false,
}: EligibleDraftJournalComboboxProps) {
    return (
        <Popover open={open} onOpenChange={onOpenChange}>
            <PopoverTrigger asChild>
                <Button
                    type="button"
                    variant="outline"
                    role="combobox"
                    aria-label="Eligible draft journal"
                    aria-expanded={open}
                    disabled={disabled}
                    className="w-full justify-between font-normal"
                >
                    <span className={cn('min-w-0 truncate', !selected && 'text-muted-foreground')}>
                        {selected
                            ? `${selected.journalEntryNumber} — ${selected.description}`
                            : 'Search eligible draft journals'}
                    </span>
                    <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[min(680px,calc(100vw-2rem))] p-0" align="start">
                <Command shouldFilter={false}>
                    <CommandInput
                        value={search}
                        onValueChange={onSearchChange}
                        placeholder="Search journal number, reference, or description..."
                    />
                    <CommandList>
                        {loading && (
                            <div className="flex items-center justify-center gap-2 p-6 text-sm text-muted-foreground">
                                <Loader2 className="h-4 w-4 animate-spin" /> Loading eligible journals...
                            </div>
                        )}
                        {!loading && error && <CommandEmpty>{error}</CommandEmpty>}
                        {!loading && !error && options.length === 0 && (
                            <CommandEmpty>No eligible draft journals were found for this period and accounting book.</CommandEmpty>
                        )}
                        {!loading && !error && options.length > 0 && (
                            <CommandGroup heading="Eligible draft journals">
                                {options.map((journal) => (
                                    <CommandItem
                                        key={journal.id}
                                        value={journal.id}
                                        onSelect={() => {
                                            onSelect(journal);
                                            onOpenChange(false);
                                        }}
                                        className="items-start gap-2 py-3"
                                    >
                                        <Check className={cn('mt-0.5 h-4 w-4 shrink-0', selected?.id === journal.id ? 'opacity-100' : 'opacity-0')} />
                                        <div className="min-w-0 flex-1">
                                            <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
                                                <span className="font-mono font-medium">{journal.journalEntryNumber}</span>
                                                <span className="text-xs text-muted-foreground">
                                                    {new Date(journal.entryDate).toLocaleDateString()} · {amount(journal.totalDebit, currencyCode)}
                                                </span>
                                            </div>
                                            <div className="truncate text-sm">{journal.description}</div>
                                            <div className="truncate text-xs text-muted-foreground">
                                                {journal.referenceNumber || 'No reference'} · {journal.lineCount} lines
                                            </div>
                                        </div>
                                    </CommandItem>
                                ))}
                            </CommandGroup>
                        )}
                    </CommandList>
                </Command>
            </PopoverContent>
        </Popover>
    );
}
