'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ExternalLink, Loader2, Megaphone, TimerOff, TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { jobPostingService } from '@/services/hr/recruitment.service';

type View = 'active' | 'expired-active';

/**
 * Every advert across all vacancies, for the recruiter who wants one list rather than a vacancy
 * at a time.
 *
 * The second view is the one that earns its place: **past its expiry date but still marked
 * active** — adverts that should have come down and have not. They are still collecting
 * applications for a role whose deadline has gone.
 *
 * The folder is `postings`, not `adverts`, on purpose: ad blockers (EasyList) block any URL with
 * `/adverts/` in it, which took out this page's own JS chunk and left it a ChunkLoadError.
 *
 * ⚠ **G-6.3 (2026-09-15): the overdue view used to be a diagnosis with no cure.** It found exactly
 * the adverts that needed taking down and offered no way to take any of them down — no Expire
 * action, no bulk selection. The user had to note each one, click through to its vacancy, find the
 * Adverts tab, and expire it there, one at a time. Given that clearing this list is the view's
 * entire reason for existing, the missing action was conspicuous. Both a per-row **Expire** and a
 * **Take them all down** now live on this page; the endpoint and the client method had existed all
 * along.
 *
 * The nightly sweep (G-6.2) now expires adverts on their closing date, so in steady state this
 * list should be empty or nearly so. The buttons remain because an advert can be missed — a sweep
 * that has not run yet, an expiry date corrected after the fact — and because a queue screen
 * should be able to act on what it finds.
 */
export default function LiveAdvertsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission } = useAuth();
  const canManage = hasAnyPermission(['HR.Recruitment.Write', 'HR.Recruitment.Admin']);
  const [view, setView] = useState<View>('active');

  const active = useQuery({
    queryKey: ['hr', 'postings', 'active'],
    queryFn: () => jobPostingService.getActive(),
    enabled: view === 'active',
  });

  const expired = useQuery({
    queryKey: ['hr', 'postings', 'expired-active'],
    queryFn: () => jobPostingService.getExpiredActive(),
    enabled: view === 'expired-active',
  });

  const query = view === 'active' ? active : expired;
  const rows = query.data ?? [];

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'postings'] });

  const expire = useMutation({
    mutationFn: (id: string) => jobPostingService.expire(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Advert taken down' });
    },
    onError: (e: any) =>
      toast({
        title: 'Could not expire the advert',
        description: e?.body?.message ?? e?.message,
        variant: 'destructive',
      }),
  });

  // Sequential rather than Promise.all: these are writes against one tenant's adverts, and a
  // partial failure should leave a clear picture of how far it got rather than an arbitrary
  // interleaving. The list is the set of adverts somebody forgot to take down, so it is small.
  const expireAll = useMutation({
    mutationFn: async () => {
      let done = 0;
      const failures: string[] = [];
      for (const posting of rows) {
        try {
          await jobPostingService.expire(posting.id);
          done++;
        } catch {
          failures.push(posting.title);
        }
      }
      return { done, failures };
    },
    onSuccess: async ({ done, failures }) => {
      await refresh();
      toast({
        title: `${done} advert${done === 1 ? '' : 's'} taken down`,
        description: failures.length
          ? `Could not expire: ${failures.join(', ')}.`
          : undefined,
        variant: failures.length ? 'destructive' : undefined,
      });
    },
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Adverts"
        description="Postings across every vacancy and channel."
        backHref="/hr/recruitment"
      />

      <div className="flex items-center gap-3">
        <Select value={view} onValueChange={(v) => setView(v as View)}>
          <SelectTrigger className="w-[260px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="active">Live adverts</SelectItem>
            <SelectItem value="expired-active">Past expiry but still live</SelectItem>
          </SelectContent>
        </Select>
        {view === 'expired-active' && (
          <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <TriangleAlert className="h-4 w-4 text-amber-600" />
            These are past their expiry date and still accepting applications.
          </p>
        )}
        {view === 'expired-active' && canManage && rows.length > 0 && (
          <Button
            variant="outline"
            className="ml-auto"
            disabled={expireAll.isPending || expire.isPending}
            onClick={() => expireAll.mutate()}
          >
            {expireAll.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <TimerOff className="mr-2 h-4 w-4" />
            )}
            Take them all down ({rows.length})
          </Button>
        )}
      </div>

      <Card>
        <CardContent className="p-0">
          {query.isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <div className="py-10">
              <EmptyState
                icon={Megaphone}
                title={view === 'active' ? 'No live adverts' : 'Nothing overdue'}
                description={
                  view === 'active'
                    ? 'Publish a vacancy and its adverts appear here.'
                    : 'Every live advert is still within its expiry date.'
                }
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Title</TableHead>
                  <TableHead>Vacancy</TableHead>
                  <TableHead>Channel</TableHead>
                  <TableHead>Published</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead className="text-right">Applications</TableHead>
                  <TableHead>Status</TableHead>
                  {view === 'expired-active' && canManage && <TableHead className="w-32" />}
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((p) => (
<TableRow
                    key={p.id}
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => router.push(`/hr/recruitment/vacancies/${p.jobVacancyId}?tab=adverts`)}
                  >
                    <TableCell className="font-medium">
                      {p.postingUrl ? (
                        <a
                          href={p.postingUrl}
                          target="_blank"
                          rel="noreferrer"
                          onClick={(e) => e.stopPropagation()}
                          className="inline-flex items-center gap-1 text-primary hover:underline"
                        >
                          {p.title} <ExternalLink className="h-3 w-3" />
                        </a>
                      ) : (
                        p.title
                      )}
                    </TableCell>
                    <TableCell className="font-mono text-xs">{p.vacancyNumber ?? '—'}</TableCell>
                    <TableCell>{humanizeEnum(p.channel)}</TableCell>
                    <TableCell>{formatDate(p.actualPublishDate ?? p.publishDate)}</TableCell>
                    <TableCell>{formatDate(p.expiryDate)}</TableCell>
                    <TableCell className="text-right tabular-nums">{p.applicationCount}</TableCell>
                    <TableCell>
                      <StatusBadge status={p.status} />
                    </TableCell>
                    {view === 'expired-active' && canManage && (
                      <TableCell>
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={expire.isPending || expireAll.isPending}
                          onClick={(e) => {
                            // The row itself navigates to the vacancy; the button must not.
                            e.stopPropagation();
                            expire.mutate(p.id);
                          }}
                        >
                          <TimerOff className="mr-2 h-4 w-4" />
                          Expire
                        </Button>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
