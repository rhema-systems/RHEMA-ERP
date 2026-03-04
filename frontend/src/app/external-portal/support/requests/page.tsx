'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { ClipboardList, Eye, Plus } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ehcServiceCatalogService, type EhcServiceRequestStatus } from '@/services/ehcServiceCatalogService';

const statusBadgeClassName = (s: EhcServiceRequestStatus) => {
  switch (s) {
    case 'Approved':
      return 'bg-green-600 text-white hover:bg-green-600/90 dark:bg-green-500 dark:hover:bg-green-500/90';
    case 'Rejected':
      return 'bg-red-600 text-white hover:bg-red-600/90 dark:bg-red-500 dark:hover:bg-red-500/90';
    case 'PendingApproval':
      return 'bg-blue-600 text-white hover:bg-blue-600/90 dark:bg-blue-500 dark:hover:bg-blue-500/90';
    default:
      return 'bg-slate-600 text-white hover:bg-slate-600/90 dark:bg-slate-500 dark:hover:bg-slate-500/90';
  }
};

export default function ExternalPortalServiceRequestsPage() {
  const router = useRouter();

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'external', 'service-catalog', 'requests'],
    queryFn: () => ehcServiceCatalogService.listMyRequestsExternal(),
  });

  const items = data || [];

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <ClipboardList className="w-7 h-7" />
            Service Requests
          </h1>
          <p className="text-slate-600 mt-1">Submit and track service requests.</p>
        </div>
        <div className="flex items-center gap-2">
          <Button onClick={() => router.push('/support/requests/new')}>
            <Plus className="w-4 h-4 mr-2" /> New request
          </Button>
          <Button variant="outline" onClick={() => refetch()}>
            Refresh
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>My requests</CardTitle>
          <CardDescription>{isLoading ? 'Loading…' : `${items.length} item(s)`}</CardDescription>
        </CardHeader>
        <CardContent>
          {error ? (
            <div className="text-sm text-red-600">Failed to load.</div>
          ) : (
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
                  <div className="flex items-center gap-2">
                    <Link href={`/support/requests/${r.id}`}>
                      <Button variant="outline" size="sm">
                        <Eye className="w-4 h-4 mr-1" /> View
                      </Button>
                    </Link>
                  </div>
                </div>
              ))}

              {!items.length && !isLoading ? <div className="text-sm text-slate-600">No requests yet.</div> : null}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

