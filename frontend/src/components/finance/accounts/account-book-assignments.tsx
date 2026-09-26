'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { financeDataService } from '@/services/finance/finance-data.service';
import type {
    AccountAccountingBook,
    AccountBookAssignmentInput,
    AccountClassification,
    AccountingBook,
    AccountType,
} from '@/types/finance';
import { AlertTriangle, Loader2 } from 'lucide-react';

interface Props {
    accountType: AccountType;
    value: AccountBookAssignmentInput[];
    onChange: (value: AccountBookAssignmentInput[]) => void;
    historicalMappings?: AccountAccountingBook[];
}

export function AccountBookAssignments({ accountType, value, onChange, historicalMappings = [] }: Props) {
    const [books, setBooks] = useState<AccountingBook[]>([]);
    const [classifications, setClassifications] = useState<AccountClassification[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        let active = true;
        Promise.all([
            financeDataService.getAccountingBooks(true),
            financeDataService.getAccountClassifications(undefined, true),
        ]).then(([loadedBooks, loadedClassifications]) => {
            if (!active) return;
            setBooks(loadedBooks);
            setClassifications(loadedClassifications);
            if (value.length === 0) {
                const defaultBook = loadedBooks.find(book => book.isActive && book.isDefault && book.allowsPosting);
                if (defaultBook) onChange([{ accountingBookId: defaultBook.id, isEnabled: true }]);
            }
        }).catch(() => {
            if (active) setError('Accounting books and classifications could not be loaded.');
        }).finally(() => {
            if (active) setLoading(false);
        });
        return () => { active = false; };
    }, []);

    const displayedBooks = useMemo(() => {
        const historicalIds = new Set(historicalMappings.map(item => item.accountingBookId));
        return books.filter(book => (book.isActive && book.allowsPosting) || historicalIds.has(book.id));
    }, [books, historicalMappings]);

    const setEnabled = (book: AccountingBook, enabled: boolean) => {
        const existing = value.find(item => item.accountingBookId === book.id);
        if (existing) {
            onChange(value.map(item => item.accountingBookId === book.id ? { ...item, isEnabled: enabled } : item));
            return;
        }
        if (enabled) onChange([...value, { accountingBookId: book.id, isEnabled: true }]);
    };

    const setClassification = (bookId: string, classificationId: string) => {
        onChange(value.map(item => item.accountingBookId === bookId
            ? { ...item, accountClassificationId: classificationId, isEnabled: true }
            : item));
    };

    if (loading) return <div className="flex items-center gap-2 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />Loading accounting books…</div>;
    if (error) return <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>;
    if (displayedBooks.length === 0) return <Alert><AlertDescription>No active posting books are configured. Configure an accounting book before creating or editing an account.</AlertDescription></Alert>;

    return <div className="space-y-4">
        {displayedBooks.map(book => {
            const assignment = value.find(item => item.accountingBookId === book.id);
            const historical = historicalMappings.find(item => item.accountingBookId === book.id);
            const enabled = assignment?.isEnabled ?? false;
            const options = classifications.filter(item => item.accountingBookId === book.id
                && item.coreAccountType === accountType
                && item.status === 'Active'
                && item.isPostingClassification
                && item.isLeaf);
            const historicalClassification = historical?.accountClassificationId
                ? classifications.find(item => item.id === historical.accountClassificationId)
                : undefined;
            const unavailableHistorical = historicalClassification && historicalClassification.status !== 'Active';
            return <div key={book.id} className="space-y-2 rounded-md border p-3">
                <div className="flex items-center gap-2">
                    <Checkbox
                        id={`book-${book.id}`}
                        checked={enabled}
                        disabled={!book.isActive || !book.allowsPosting}
                        onCheckedChange={checked => setEnabled(book, checked === true)}
                    />
                    <Label htmlFor={`book-${book.id}`}>{book.name} <span className="text-muted-foreground">({book.code})</span></Label>
                </div>
                {enabled && options.length > 0 && <Select
                    value={assignment?.accountClassificationId ?? ''}
                    onValueChange={classificationId => setClassification(book.id, classificationId)}
                >
                    <SelectTrigger aria-label={`${book.name} classification`}><SelectValue placeholder="Select an active posting classification" /></SelectTrigger>
                    <SelectContent>{options.map(item => <SelectItem key={item.id} value={item.id}>{item.code} — {item.name}</SelectItem>)}</SelectContent>
                </Select>}
                {enabled && options.length === 0 && <Alert variant="destructive"><AlertDescription>No compatible active posting classifications exist for this book and account type.</AlertDescription></Alert>}
                {enabled && !assignment?.accountClassificationId && <p className="text-sm text-amber-700">A classification is required before this account can be saved.</p>}
                {historical && !historical.accountClassificationId && <p className="flex items-center gap-1 text-sm text-amber-700"><AlertTriangle className="h-4 w-4" />Legacy mapping is unclassified and blocks migration readiness.</p>}
                {unavailableHistorical && <p className="flex items-center gap-1 text-sm text-amber-700"><AlertTriangle className="h-4 w-4" />Historical classification {historicalClassification.code} is {historicalClassification.status.toLowerCase()}; select an active replacement.</p>}
                {(!book.isActive || !book.allowsPosting) && <p className="text-sm text-muted-foreground">Historical book; unavailable for new postings.</p>}
            </div>;
        })}
    </div>;
}
