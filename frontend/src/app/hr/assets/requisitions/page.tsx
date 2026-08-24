'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { ClipboardList, Inbox, Loader2, Plus, Search } from 'lucide-react';
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
import type { AssetRequisitionSummary } from '@/types/hr/assets';

const PAGE_SIZE = 20;
const ANY = '__any__';
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const STATUSES = ['Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'Fulfilled', 'Cancelled'];

/**
 * ⚠ Urgent is **1** and Low is **4** — the numbers run against reading order.
 *
 * The list only displays the name, so this is cosmetic here; the trap lives on the create form,
 * where a picker offering "1, 2, 3, 4" as increasing urgency would file every emergency as an
 * afterthought. The colouring below is keyed on the name for that reason, never the number.
 */
const PRIORITY_TONE: Record<string, string> = {
  Urgent: 'text-red-600 dark:text-red-500 font-medium',
  High: 'text-amber-600 dark:text-amber-500',
  Medium: '',
  Low: 'text-muted-foreground',
};

function RequisitionTable({ rows }: { rows: AssetRequisitionSummary[] }) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Request</TableHead>
          <TableHead>What for</TableHead>
          <TableHead>Raised by</TableHead>
          <TableHead>For</TableHead>
          <TableHead>Priority</TableHead>
          <TableHead>Needed by</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((r) => (
          <TableRow key={r.id}>
            <TableCell>
              <Link href={`/hr/assets/requisitions/${r.id}`} className="hover:underline">
                {r.requisitionNumber}
              </Link>
              <div className="text-xs text-muted-foreground">{fmtDate(r.requestDate)}</div>
            </TableCell>
            <TableCell>
              {r.assetTypeName}
              {r.quantity > 1 && <span className="text-muted-foreground"> × {r.quantity}</span>}
            </TableCell>
            <TableCell>{r.requestedByName}</TableCell>
            <TableCell>
              {/* Server-computed: the beneficiary where one is named, the requester otherwise. A
                  screen that showed only `beneficiaryEmployeeName` would be blank on most rows. */}
              {r.forEmployeeName}
              {r.isOnBehalf && (
                <span className="ml-1 text-xs text-muted-foreground">(on their behalf)</span>
              )}
            </TableCell>
            <TableCell className={PRIORITY_TONE[r.priorityName] ?? ''}>{r.priorityName}</TableCell>
            <TableCell>{fmtDate(r.requiredByDate)}</TableCell>
            <TableCell><StatusBadge status={r.statusName} /></TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * Equipment requests, and the approvals routed to the signed-in user.
 *
 * The approvals view is a purpose-built endpoint rather than a `status=Submitted` filter: approval
 * runs on the workflow engine, so "waiting on me" is a question about the engine's routing, not
 * about the requisition's own status column.
 */
export default function AssetRequisitionsPage() {
  const [view, setView] = useState<'all' | 'mine-to-approve'>('all');
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<string>(ANY);
  const debouncedSearch = useDebounce(search, 300);

  const { data: paged, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'requisitions', page, debouncedSearch, status],
    queryFn: () =>
      assetRegisterService.getRequisitionsPaged({
        pageNumber: page,
        pageSize: PAGE_SIZE,
        searchTerm: debouncedSearch || undefined,
        status: status === ANY ? undefined : status,
      }),
    enabled: view === 'all',
  });

  const { data: pending = [], isLoading: loadingPending } = useQuery({
    queryKey: ['hr', 'assets', 'requisitions', 'pending-approvals'],
    queryFn: () => assetRegisterService.getPendingRequisitionApprovals(),
    enabled: view === 'mine-to-approve',
  });

  const rows = view === 'all' ? paged?.items ?? [] : pending;
  const busy = view === 'all' ? isLoading : loadingPending;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Asset requisitions"
        description="Requests for equipment, and the approvals waiting on you."
        backHref="/hr/assets"
        actions={
          <Button asChild>
            <Link href="/hr/assets/requisitions/new">
              <Plus className="mr-2 h-4 w-4" /> Raise a request
            </Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Button variant={view === 'all' ? 'default' : 'outline'} size="sm"
          onClick={() => setView('all')}>
          <ClipboardList className="mr-2 h-4 w-4" /> All requests
        </Button>
        <Button variant={view === 'mine-to-approve' ? 'default' : 'outline'} size="sm"
          onClick={() => setView('mine-to-approve')}>
          <Inbox className="mr-2 h-4 w-4" /> Waiting on me
          {pending.length > 0 && <span className="ml-1">({pending.length})</span>}
        </Button>
      </div>

      {view === 'all' && (
        <Card>
          <CardContent className="flex flex-wrap gap-3 p-4">
            <div className="relative min-w-[240px] flex-1">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input className="pl-8" placeholder="Request number, requester, asset type…"
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
              icon={ClipboardList}
              title={view === 'all' ? 'No requests match' : 'Nothing waiting on you'}
              description={view === 'all'
                ? 'No requisition answers to those filters.'
                : 'The workflow engine has routed no asset requisition to you.'}
            />
          ) : (
            <RequisitionTable rows={rows} />
          )}
        </CardContent>
      </Card>

      {view === 'all' && paged && paged.totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Page {paged.page} of {paged.totalPages} · {paged.totalCount} request
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
