'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { ArrowRightLeft, Inbox, Loader2, Plus, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useDebounce } from '@/hooks/use-debounce';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import type { AssetTransferSummary } from '@/types/hr/assets';

const PAGE_SIZE = 20;
const ANY = '__any__';
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const STATUSES = ['Draft', 'Pending', 'Approved', 'InTransit', 'Completed', 'Rejected', 'Cancelled'];

function TransferTable({ rows }: { rows: AssetTransferSummary[] }) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Transfer</TableHead>
          <TableHead>Asset</TableHead>
          <TableHead>Kind</TableHead>
          <TableHead>From</TableHead>
          <TableHead>To</TableHead>
          <TableHead>Dated</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((t) => (
          <TableRow key={t.id}>
            <TableCell>
              <Link href={`/hr/assets/transfers/${t.id}`} className="hover:underline">
                {t.transferNumber}
              </Link>
            </TableCell>
            <TableCell>{t.assetName}</TableCell>
            <TableCell>{t.typeName}</TableCell>
            {/* Already resolved server-side to whichever side the transfer's type implies —
                a screen picking between four from/to pairs itself would get one wrong. */}
            <TableCell>{t.fromName ?? <span className="text-muted-foreground">—</span>}</TableCell>
            <TableCell>{t.toName ?? <span className="text-muted-foreground">—</span>}</TableCell>
            <TableCell>{fmtDate(t.transferDate)}</TableCell>
            <TableCell><StatusBadge status={t.statusName} /></TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * Assets moving between people, locations and units.
 *
 * ⚠ **Approval does not move the asset — completion does.** An approved transfer that nobody
 * completes leaves the register saying the asset is still where it was, which is correct: nothing
 * has physically moved. The detail screen makes that the explicit last step.
 */
export default function AssetTransfersPage() {
  const [view, setView] = useState<'all' | 'pending'>('all');
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<string>(ANY);
  const debouncedSearch = useDebounce(search, 300);

  const { data: paged, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'transfers', page, debouncedSearch, status],
    queryFn: () =>
      assetRegisterService.getTransfersPaged({
        pageNumber: page,
        pageSize: PAGE_SIZE,
        searchTerm: debouncedSearch || undefined,
        status: status === ANY ? undefined : status,
      }),
    enabled: view === 'all',
  });

  const { data: pending = [], isLoading: loadingPending } = useQuery({
    queryKey: ['hr', 'assets', 'transfers', 'pending'],
    queryFn: () => assetRegisterService.getPendingTransfers(),
    enabled: view === 'pending',
  });

  const rows = view === 'all' ? paged?.items ?? [] : pending;
  const busy = view === 'all' ? isLoading : loadingPending;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Asset transfers"
        description="Assets moving between people, locations and units."
        backHref="/hr/assets"
        actions={
          <Button asChild>
            <Link href="/hr/assets/transfers/new">
              <Plus className="mr-2 h-4 w-4" /> Raise a transfer
            </Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Button variant={view === 'all' ? 'default' : 'outline'} size="sm" onClick={() => setView('all')}>
          <ArrowRightLeft className="mr-2 h-4 w-4" /> All transfers
        </Button>
        <Button variant={view === 'pending' ? 'default' : 'outline'} size="sm"
          onClick={() => setView('pending')}>
          <Inbox className="mr-2 h-4 w-4" /> Out for approval
          {pending.length > 0 && <span className="ml-1">({pending.length})</span>}
        </Button>
      </div>

      {view === 'all' && (
        <Card>
          <CardContent className="flex flex-wrap gap-3 p-4">
            <div className="relative min-w-[240px] flex-1">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input className="pl-8" placeholder="Transfer number, asset, person…"
                value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
            </div>
            <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
              <SelectTrigger className="w-[190px]"><SelectValue placeholder="Any status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>Any status</SelectItem>
                {STATUSES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
              </SelectContent>
            </Select>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-0">
          {busy ? (
            <div className="flex justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={ArrowRightLeft}
              title={view === 'all' ? 'No transfers match' : 'Nothing out for approval'}
              description={view === 'all'
                ? 'No transfer answers to those filters.'
                : 'Every transfer is either still a draft or already decided.'}
            />
          ) : (
            <TransferTable rows={rows} />
          )}
        </CardContent>
      </Card>

      {view === 'all' && paged && paged.totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Page {paged.page} of {paged.totalPages} · {paged.totalCount} transfer
            {paged.totalCount === 1 ? '' : 's'}
          </p>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={!paged.hasPrevious}
              onClick={() => setPage((p) => p - 1)}>Previous</Button>
            <Button variant="outline" size="sm" disabled={!paged.hasNext}
              onClick={() => setPage((p) => p + 1)}>Next</Button>
          </div>
        </div>
      )}
    </div>
  );
}
