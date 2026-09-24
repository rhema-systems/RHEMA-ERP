'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { BookOpen, FilePlus2, Layers3, Loader2 } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useAuth } from '@/hooks/use-auth';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { AccountingBook } from '@/types/finance';

export default function DeltaLedgerWorkspacePage() {
  const { hasPermission, isLoading: authLoading } = useAuth();
  const canRead = hasPermission('Finance.Read');
  const [books, setBooks] = useState<AccountingBook[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (authLoading) return;
    if (!canRead) {
      setLoading(false);
      return;
    }
    let active = true;
    void financeDataService.getAccountingBooks(true)
      .then(items => {
        if (active) setBooks(items.filter(item => item.bookType === 'Delta' && item.lifecycleStatus !== 'Retired'));
      })
      .catch(reason => {
        if (active) setError(reason instanceof Error ? reason.message : 'Delta books could not be loaded.');
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => { active = false; };
  }, [authLoading, canRead]);

  if (authLoading || loading) {
    return <div className="flex min-h-[320px] items-center justify-center"><Loader2 className="h-6 w-6 animate-spin" /></div>;
  }
  if (!canRead) {
    return <Alert variant="destructive"><AlertTitle>Permission required</AlertTitle><AlertDescription>Finance.Read is required to use the Delta ledger workspace.</AlertDescription></Alert>;
  }

  return <div className="space-y-6 p-6">
    <div>
      <h1 className="text-3xl font-semibold">Delta Ledger</h1>
      <p className="text-muted-foreground">Open a Delta audit ledger, create an adjustment, or report the selected adjustment layer over its base book.</p>
    </div>
    {error && <Alert variant="destructive"><AlertTitle>Delta workspace unavailable</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
    {!error && books.length === 0 && <Card><CardHeader><CardTitle>No Delta books available</CardTitle><CardDescription>Create and activate a Delta book in Finance settings before posting governed adjustments.</CardDescription></CardHeader><CardContent><Button asChild><Link href="/finance/settings/accounting-books">Open accounting books</Link></Button></CardContent></Card>}
    <div className="grid gap-4 lg:grid-cols-2">
      {books.map(book => {
        const active = book.lifecycleStatus === 'Active' && book.allowsPosting;
        return <Card key={book.id}>
          <CardHeader>
            <div className="flex items-start justify-between gap-3"><div><CardTitle>{book.code} — {book.name}</CardTitle><CardDescription>{book.purpose}</CardDescription></div><Badge variant={active ? 'default' : 'secondary'}>{book.lifecycleStatus}</Badge></div>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">Base: {book.baseAccountingBookCode || 'Not configured'} · Currency inherited from base</p>
            <div className="flex flex-wrap gap-2">
              <Button asChild variant="outline"><Link href={`/finance/settings/accounting-books/${book.id}/delta-ledger`}><Layers3 className="mr-2 h-4 w-4" />Delta ledger</Link></Button>
              <Button asChild variant="outline"><Link href={`/finance/settings/accounting-books/${book.id}/delta-report`}><BookOpen className="mr-2 h-4 w-4" />Base + Delta report</Link></Button>
              {active && <Button asChild><Link href={`/finance/journal-entries/new?journalType=Delta%20Adjustment&book=${encodeURIComponent(book.code)}`}><FilePlus2 className="mr-2 h-4 w-4" />New adjustment</Link></Button>}
            </div>
            {!active && <p className="text-xs text-muted-foreground">New adjustments are available only while the Delta book is active and postable.</p>}
          </CardContent>
        </Card>;
      })}
    </div>
  </div>;
}
