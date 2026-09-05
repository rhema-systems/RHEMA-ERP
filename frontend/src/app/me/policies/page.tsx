'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, BookText, CheckCircle2, Clock, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  myPoliciesService,
  POLICY_CATEGORY_LABEL,
  type MyPolicy,
} from '@/services/hr/policies.service';
import { cn } from '@/lib/utils';

/**
 * Company policies (area 25 slice 12d) — what applies to this employee, and what still wants
 * their signature.
 *
 * Outstanding is the ABSENCE of an answer, so the screen leads with those. A declined policy
 * stays in that group deliberately: refusing is an answer to HR, not a way to clear the
 * obligation, and hiding it would let somebody believe the matter was closed.
 */

const fmtDate = (v?: string | null) =>
  v ? new Date(v).toLocaleDateString(undefined, { day: 'numeric', month: 'long', year: 'numeric' }) : '';

function PolicyRow({ policy }: { policy: MyPolicy }) {
  const overdue =
    policy.isOutstandingForMe
    && policy.acknowledgementDueBy
    && new Date(policy.acknowledgementDueBy).getTime() < Date.now();

  return (
    <Card className={cn(overdue && 'border-destructive/50')}>
      <CardContent className="flex flex-wrap items-center gap-x-4 gap-y-2 p-4">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-medium">{policy.title}</span>
            <Badge variant="outline">{POLICY_CATEGORY_LABEL[policy.category] ?? policy.categoryName}</Badge>
            {policy.versionLabel && (
              <Badge variant="secondary" className="font-mono">{policy.versionLabel}</Badge>
            )}
            {policy.myOutcome === 'Signed' && (
              <Badge className="border-0 bg-emerald-100 text-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-200">
                <CheckCircle2 className="mr-1 h-3 w-3" /> Acknowledged
              </Badge>
            )}
            {policy.myOutcome === 'Declined' && (
              <Badge className="border-0 bg-red-100 text-red-900 dark:bg-red-950/50 dark:text-red-200">
                <XCircle className="mr-1 h-3 w-3" /> You declined
              </Badge>
            )}
          </div>
          {policy.summary && (
            <p className="mt-0.5 text-sm text-muted-foreground">{policy.summary}</p>
          )}
          <p className="mt-1 text-xs text-muted-foreground">
            {policy.publishedAt ? `published ${fmtDate(policy.publishedAt)}` : ''}
            {policy.mySignedAt ? ` · you signed ${fmtDate(policy.mySignedAt)}` : ''}
            {policy.isOutstandingForMe && policy.acknowledgementDueBy && (
              <span className={cn('ml-1', overdue && 'font-medium text-destructive')}>
                {overdue ? '· overdue since ' : '· due by '}
                {fmtDate(policy.acknowledgementDueBy)}
              </span>
            )}
          </p>
        </div>

        <Button
          variant={policy.isOutstandingForMe ? 'default' : 'outline'}
          size="sm"
          asChild
          className="shrink-0"
        >
          <Link href={`/me/policies/${policy.id}`}>
            {policy.isOutstandingForMe ? 'Read & acknowledge' : 'Open'}
          </Link>
        </Button>
      </CardContent>
    </Card>
  );
}

export default function MyPoliciesPage() {
  const { data: policies = [], isLoading, isError } = useQuery({
    queryKey: ['me', 'policies'],
    queryFn: () => myPoliciesService.getMine(),
  });

  const outstanding = policies.filter((p) => p.isOutstandingForMe);
  const settled = policies.filter((p) => !p.isOutstandingForMe);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Company Policies"
        description="The policies that apply to you. Some ask you to confirm you have read them."
        backHref="/me"
      />

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-24" />
          <Skeleton className="h-24" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">
          Policies could not be loaded right now. Try again in a moment.
        </p>
      ) : policies.length === 0 ? (
        <EmptyState
          icon={BookText}
          title="No policies yet"
          description="When HR publishes a policy that applies to you, it appears here."
        />
      ) : (
        <div className="space-y-6">
          {outstanding.length > 0 && (
            <section>
              <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-amber-700 dark:text-amber-400">
                <AlertTriangle className="h-4 w-4" />
                Waiting for your acknowledgement ({outstanding.length})
              </h2>
              <div className="space-y-3">
                {outstanding.map((p) => (
                  <PolicyRow key={p.id} policy={p} />
                ))}
              </div>
            </section>
          )}

          {settled.length > 0 && (
            <section>
              <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                <Clock className="h-4 w-4" /> Everything else
              </h2>
              <div className="space-y-3">
                {settled.map((p) => (
                  <PolicyRow key={p.id} policy={p} />
                ))}
              </div>
            </section>
          )}
        </div>
      )}
    </div>
  );
}
