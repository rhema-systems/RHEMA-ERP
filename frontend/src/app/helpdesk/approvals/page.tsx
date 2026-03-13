'use client';

import Link from 'next/link';
import { useMemo, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Eye, RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { getHelpdeskScopeConfig } from '@/lib/helpdesk-scope';
import { ehcServiceCatalogService, type WorkflowApprovalItem } from '@/services/ehcServiceCatalogService';

export default function HelpdeskApprovalsPage() {
  const searchParams = useSearchParams();
  const scopeParam = searchParams.get('scope');
  const scopeConfig = useMemo(() => getHelpdeskScopeConfig(scopeParam), [scopeParam]);
  const [q, setQ] = useState('');

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'internal', 'service-catalog', 'approvals', 'pending'],
    queryFn: () => ehcServiceCatalogService.listPendingApprovalsInternal(),
  });

  const items = useMemo(() => {
    const all = data || [];
    const term = q.trim().toLowerCase();
    if (!term) return all;
    return all.filter((i) =>
      (i.entityTitle || '').toLowerCase().includes(term) ||
      (i.entityDescription || '').toLowerCase().includes(term) ||
      (i.currentStep || '').toLowerCase().includes(term) ||
      (i.submittedBy || '').toLowerCase().includes(term),
    );
  }, [data, q]);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">
            {scopeParam ? `${scopeConfig.moduleLabel} Approvals` : 'Approvals'}
          </h1>
          <p className="text-slate-600 mt-1">
            {scopeParam ? `Approval queue for the ${scopeConfig.listTitle.toLowerCase()} branch.` : 'Items waiting for your approval decisions.'}
          </p>
        </div>
        <Button variant="outline" onClick={() => refetch()}>
          <RefreshCw className="w-4 h-4 mr-2" /> Refresh
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Pending approvals</CardTitle>
          <CardDescription>{isLoading ? 'Loading…' : `${items.length} item(s)`}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search by title, step, submitted by…" />

          {error ? <div className="text-sm text-red-600">Failed to load.</div> : null}

          <div className="space-y-2">
            {items.map((i: WorkflowApprovalItem) => (
              <div key={i.entityId} className="flex items-center justify-between rounded-md border p-3">
                <div className="space-y-1">
                  <div className="font-medium">{i.entityTitle}</div>
                  {i.entityDescription ? <div className="text-sm text-slate-700">{i.entityDescription}</div> : null}
                  <div className="text-xs text-slate-500">
                    Step: {i.currentStep || 'Approval'} · Submitted by: {i.submittedBy || 'Unknown'} · Pending: {i.daysPending} day(s)
                  </div>
                </div>
                <Link href={`/helpdesk/requests/${i.entityId}`}>
                  <Button variant="outline" size="sm">
                    <Eye className="w-4 h-4 mr-1" /> View
                  </Button>
                </Link>
              </div>
            ))}
            {!items.length && !isLoading ? <div className="text-sm text-slate-600">No pending approvals.</div> : null}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
