'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, FileCheck2, FileText, Loader2, ShieldQuestion } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { useToast } from '@/hooks/use-toast';
import {
  myProfileService,
  type ProfileChangeRequest,
  type ProfileChangeRequestStatus,
} from '@/services/hr/my-profile.service';
import { cn } from '@/lib/utils';

/**
 * My change requests (area 25 slice 12a) — what I asked HR to correct, and what they said.
 *
 * The screen exists because a request without a visible outcome is worse than no request:
 * every row carries its before → after, and a refusal shows HR's reason, which the server
 * makes mandatory precisely so this page has something honest to display.
 */

const fmtDateTime = (v?: string | null) =>
  v ? new Date(v).toLocaleString(undefined, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—';

const STATUS_STYLE: Record<ProfileChangeRequestStatus, string> = {
  Pending: 'bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-200',
  Approved: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-200',
  Rejected: 'bg-red-100 text-red-900 dark:bg-red-950/50 dark:text-red-200',
  Cancelled: 'bg-muted text-muted-foreground',
};

function RequestCard({ request, onChanged }: { request: ProfileChangeRequest; onChanged: () => void }) {
  const { toast } = useToast();
  const isPending = request.status === 'Pending';

  const cancel = useMutation({
    mutationFn: () => myProfileService.cancelChangeRequest(request.id),
    onSuccess: () => {
      onChanged();
      toast({ title: 'Withdrawn', description: `${request.requestNumber} is no longer with HR.` });
    },
    onError: (e: any) =>
      toast({
        title: 'Not withdrawn',
        description: e?.message || 'The request could not be withdrawn.',
        variant: 'destructive',
      }),
  });

  return (
    <Card>
      <CardContent className="space-y-4 p-4">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-mono text-sm font-medium">{request.requestNumber}</span>
          <Badge className={cn('border-0', STATUS_STYLE[request.status])}>{request.statusName}</Badge>
          {request.bankAccountMasked && (
            <Badge variant="outline" className="font-mono">{request.bankAccountMasked}</Badge>
          )}
          <span className="ml-auto text-xs text-muted-foreground">
            filed {fmtDateTime(request.submittedAt)}
          </span>
        </div>

        <p className="text-sm text-muted-foreground">{request.reason}</p>

        {/* before → after, the substance of the request */}
        <div className="space-y-1.5">
          {request.items.map((item) => (
            <div key={item.id} className="flex flex-wrap items-center gap-2 rounded-md bg-muted/40 px-3 py-2 text-sm">
              <span className="font-medium">{item.fieldLabel}</span>
              <span className="text-muted-foreground line-through">{item.oldValue || 'not set'}</span>
              <ArrowRight className="h-3.5 w-3.5 text-muted-foreground" />
              <span className="font-medium">{item.newValue}</span>
              {item.appliedValue && (
                <Badge variant="outline" className="ml-auto gap-1">
                  <FileCheck2 className="h-3 w-3" /> applied
                </Badge>
              )}
            </div>
          ))}
        </div>

        {/* HR's answer — a refusal always carries a reason, by server rule */}
        {request.reviewComments && (
          <div className="rounded-md border-l-2 border-primary/50 bg-muted/30 px-3 py-2 text-sm">
            <div className="text-xs uppercase tracking-wide text-muted-foreground">
              HR{request.reviewedByName ? ` · ${request.reviewedByName}` : ''} · {fmtDateTime(request.reviewedAt)}
            </div>
            <p className="mt-0.5">{request.reviewComments}</p>
          </div>
        )}

        {/* Evidence: attachable only while the request is still open */}
        {request.hasEvidence ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <FileText className="h-4 w-4" />
            {request.evidenceFileName ?? 'Evidence attached'}
          </div>
        ) : isPending ? (
          <DocumentUploadField
            label="Attach evidence"
            endpoint={myProfileService.uploadEvidenceEndpoint(request.id)}
            helpText="The certificate, bank letter or ID that supports this change."
            onUploaded={onChanged}
          />
        ) : null}

        {isPending && (
          <Button variant="outline" size="sm" onClick={() => cancel.mutate()} disabled={cancel.isPending}>
            {cancel.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Withdraw request
          </Button>
        )}
      </CardContent>
    </Card>
  );
}

export default function MyChangeRequestsPage() {
  const queryClient = useQueryClient();

  const { data: requests = [], isLoading, isError } = useQuery({
    queryKey: ['me', 'profile', 'change-requests'],
    queryFn: () => myProfileService.getMyChangeRequests(),
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['me', 'profile', 'change-requests'] });
    queryClient.invalidateQueries({ queryKey: ['me', 'profile'] });
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Change Requests"
        description="Corrections you have asked HR to make to your personal details, and what they decided."
        backHref="/me/profile"
        actions={
          <Button variant="outline" asChild>
            <Link href="/me/profile">Back to my profile</Link>
          </Button>
        }
      />

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">
          Your requests could not be loaded right now. Try again in a moment.
        </p>
      ) : requests.length === 0 ? (
        <EmptyState
          icon={ShieldQuestion}
          title="No change requests"
          description="When you ask HR to correct your name, address, statutory numbers or bank details, the request and its outcome appear here."
          action={
            <Button asChild>
              <Link href="/me/profile">Go to my profile</Link>
            </Button>
          }
        />
      ) : (
        <div className="space-y-3">
          {requests.map((r) => (
            <RequestCard key={r.id} request={r} onChanged={refresh} />
          ))}
        </div>
      )}
    </div>
  );
}
