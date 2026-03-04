'use client';

import Link from 'next/link';
import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Eye, Plus, RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { ehcServiceCatalogService, type EhcServiceRequestStatus } from '@/services/ehcServiceCatalogService';

const statusBadgeClassName = (s: EhcServiceRequestStatus) => {
  switch (s) {
    case 'Approved':
      return 'bg-green-600 text-white hover:bg-green-600/90 dark:bg-green-500 dark:hover:bg-green-500/90';
    case 'Rejected':
      return 'bg-red-600 text-white hover:bg-red-600/90 dark:bg-red-500 dark:hover:bg-red-500/90';
    case 'PendingApproval':
      return 'bg-blue-600 text-white hover:bg-blue-600/90 dark:bg-blue-500 dark:hover:bg-blue-500/90';
    case 'Fulfilled':
      return 'bg-purple-600 text-white hover:bg-purple-600/90 dark:bg-purple-500 dark:hover:bg-purple-500/90';
    default:
      return 'bg-slate-600 text-white hover:bg-slate-600/90 dark:bg-slate-500 dark:hover:bg-slate-500/90';
  }
};

export default function HelpdeskServiceRequestsPage() {
  const router = useRouter();
  const [q, setQ] = useState('');
  const [status, setStatus] = useState<EhcServiceRequestStatus | ''>('');

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'internal', 'service-catalog', 'requests'],
    queryFn: () => ehcServiceCatalogService.listRequestsInternal(500),
  });

  const items = useMemo(() => {
    const all = data || [];
    const term = q.trim().toLowerCase();
    return all.filter((r) => {
      if (status && r.status !== status) return false;
      if (!term) return true;
      return (
        r.requestNumber.toLowerCase().includes(term) ||
        (r.requestTypeName || '').toLowerCase().includes(term) ||
        (r.title || '').toLowerCase().includes(term)
      );
    });
  }, [data, q, status]);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">Service Requests</h1>
          <p className="text-slate-600 mt-1">Approve, fulfill, and close service catalog requests.</p>
        </div>
        <div className="flex items-center gap-2">
          <Button onClick={() => router.push('/helpdesk/requests/new')}>
            <Plus className="w-4 h-4 mr-2" /> New request
          </Button>
          <Button variant="outline" onClick={() => refetch()}>
            <RefreshCw className="w-4 h-4 mr-2" /> Refresh
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Requests</CardTitle>
          <CardDescription>{isLoading ? 'Loading…' : `${items.length} item(s)`}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div className="sm:col-span-2">
              <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search by number, type, title…" />
            </div>
            <select
              className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
              value={status}
              onChange={(e) => setStatus((e.target.value || '') as any)}
            >
              <option value="">All statuses</option>
              <option value="Submitted">Submitted</option>
              <option value="PendingApproval">PendingApproval</option>
              <option value="Approved">Approved</option>
              <option value="Rejected">Rejected</option>
              <option value="Fulfilled">Fulfilled</option>
              <option value="Closed">Closed</option>
              <option value="Cancelled">Cancelled</option>
            </select>
          </div>

          {error ? <div className="text-sm text-red-600">Failed to load.</div> : null}

          <div className="space-y-2">
            {items.map((r) => (
              <div key={r.id} className="flex items-center justify-between rounded-md border p-3">
                <div className="space-y-1">
                  <div className="font-medium flex items-center gap-2">
                    <span>{r.requestNumber}</span>
                    <Badge className={statusBadgeClassName(r.status)}>{r.status}</Badge>
                  </div>
                  <div className="text-sm text-slate-700">
                    <span className="font-medium">{r.requestTypeName}</span>
                    {r.title ? <span className="text-slate-600"> · {r.title}</span> : null}
                  </div>
                  {r.submittedAtUtc ? <div className="text-xs text-slate-500">Submitted: {new Date(r.submittedAtUtc).toLocaleString()}</div> : null}
                </div>
                <Link href={`/helpdesk/requests/${r.id}`}>
                  <Button variant="outline" size="sm">
                    <Eye className="w-4 h-4 mr-1" /> View
                  </Button>
                </Link>
              </div>
            ))}
            {!items.length && !isLoading ? <div className="text-sm text-slate-600">No requests found.</div> : null}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
