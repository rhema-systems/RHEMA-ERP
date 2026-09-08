'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { ExternalLink, Loader2, Megaphone, TriangleAlert } from 'lucide-react';
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
 */
export default function LiveAdvertsPage() {
  const router = useRouter();
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
